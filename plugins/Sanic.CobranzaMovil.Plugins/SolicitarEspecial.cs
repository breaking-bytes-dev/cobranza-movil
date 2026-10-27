using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Custom API global sanic_cm_SolicitarEspecial: pide una asignacion Especial para una cuenta. El Solicitante solo
    /// lee sus asignaciones, asi que la API busca las Especiales de la cuenta con el servicio del sistema:
    /// si hay una activa la informa, si hay una cerrada la reactiva a nombre del llamador (quitando el compartido
    /// al solicitante anterior) y si no hay ninguna la crea con los permisos del llamador.
    /// Valida rol y que el llamador vea la cuenta; las notas no pueden traer un PAN. No traza valores.
    /// </summary>
    public sealed class SolicitarEspecial : PluginBase
    {
        private const int LargoMaximoNotas = 2000;
        private const decimal RetiroMaximo = 1000000000m;
        private const int DigitosDePan = 13;
        private static readonly string[] RolesQueSolicitan = { RolesCm.Solicitante, RolesCm.Admin, RolesCm.Servicio };

        protected override string Nombre
        {
            get { return "SolicitarEspecial"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            if (!string.Equals(ctx.MessageName, Esquema.MensajeSolicitarEspecial, StringComparison.Ordinal)
                || ctx.Stage != Esquema.EtapaMainOperation)
            {
                throw local.ErrorTecnico("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeSolicitarEspecial + ".");
            }

            var roles = RolesCm.DelLlamador(local);
            if (!roles.SinRolesCm && !roles.TieneAlguno(RolesQueSolicitan))
            {
                throw local.Error("Solo un Solicitante (o un Admin) puede pedir una Especial.");
            }

            var cuentaId = Entrada<Guid>(ctx, Esquema.ParamCuentaId);
            if (cuentaId == Guid.Empty) throw local.Error("Falta la cuenta.");
            var notas = Entrada<string>(ctx, Esquema.ParamNotas);
            notas = notas == null ? null : notas.Trim();
            if (string.IsNullOrEmpty(notas)) throw local.Error("Faltan las notas de la solicitud (tipo y motivo).");
            if (notas.Length > LargoMaximoNotas) throw local.Error("Las notas pasan de " + LargoMaximoNotas + " caracteres.");
            if (PanDetector.ContieneSecuenciaDeDigitos(notas, DigitosDePan))
            {
                throw local.Error("Las notas no pueden llevar un número de tarjeta.");
            }
            var retiroNio = Retiro(local, ctx, Esquema.ParamRetiroNio);
            var retiroUsd = Retiro(local, ctx, Esquema.ParamRetiroUsd);

            ExigirCuentaVisible(local, cuentaId);

            var especiales = EspecialesDeLaCuenta(local.ServicioSistema, cuentaId);
            var activa = especiales.FirstOrDefault(e => e.GetAttributeValue<bool?>(Esquema.AsignacionEstado) == true);
            if (activa != null)
            {
                Responder(local, activa.Id, Esquema.ResultadoYaExiste, NombreDeQuienPidio(local, activa),
                    activa.GetAttributeValue<OptionSetValue>(Esquema.AsignacionEstadoAprobacion));
                return;
            }

            var llamador = new EntityReference(Esquema.Usuario, ctx.UserId);
            var cerrada = especiales.FirstOrDefault();
            if (cerrada != null)
            {
                var r = new Entity(Esquema.Asignacion, cerrada.Id);
                r[Esquema.AsignacionEstado] = true;
                r[Esquema.AsignacionEstadoAprobacion] = new OptionSetValue(Esquema.AprobacionPendiente);
                r[Esquema.AsignacionRetiroNio] = retiroNio;
                r[Esquema.AsignacionRetiroUsd] = retiroUsd;
                r[Esquema.AsignacionNotas] = notas;
                r[Esquema.AsignacionSolicitadoPor] = llamador;
                r[Esquema.AsignacionFechaAsignada] = HoyDelLlamador(local);
                r[Esquema.AsignacionAgente] = null;
                r[Esquema.AsignacionResolutor] = null;
                r[Esquema.AsignacionMotivo] = null;
                r[Esquema.AsignacionMotivoRechazo] = null;
                r[Esquema.AsignacionFechaCierre] = null;
                r[Esquema.Propietario] = llamador;
                local.ServicioSistema.Update(r);
                QuitarAlSolicitanteAnterior(local, cerrada);
                Responder(local, cerrada.Id, Esquema.ResultadoReactivada, null, new OptionSetValue(Esquema.AprobacionPendiente));
                return;
            }

            var nueva = new Entity(Esquema.Asignacion);
            nueva[Esquema.AsignacionCuentaId] = new EntityReference(Esquema.Cuenta, cuentaId);
            nueva[Esquema.AsignacionTipo] = new OptionSetValue(Esquema.TipoEspecial);
            nueva[Esquema.AsignacionEstado] = true;
            nueva[Esquema.AsignacionEstadoAprobacion] = new OptionSetValue(Esquema.AprobacionPendiente);
            nueva[Esquema.AsignacionRetiroNio] = retiroNio;
            nueva[Esquema.AsignacionRetiroUsd] = retiroUsd;
            nueva[Esquema.AsignacionNotas] = notas;
            nueva[Esquema.AsignacionSolicitadoPor] = llamador;
            nueva[Esquema.AsignacionFechaAsignada] = HoyDelLlamador(local);
            Guid id;
            try
            {
                id = local.Service.Create(nueva);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                // Lo mas probable: otra persona la pidio al mismo tiempo (clave alternativa). Solo se traza el codigo.
                local.Trace("el alta fallo con el codigo {0}.", ex.Detail == null ? 0 : ex.Detail.ErrorCode);
                throw local.Error("No se pudo crear la Especial. Volvé a intentar: si otra persona la pidió recién, "
                    + "la respuesta va a decir quién.");
            }
            Responder(local, id, Esquema.ResultadoCreada, null, new OptionSetValue(Esquema.AprobacionPendiente));
        }

        private static void QuitarAlSolicitanteAnterior(LocalContext local, Entity cerrada)
        {
            var anterior = cerrada.GetAttributeValue<EntityReference>(Esquema.AsignacionSolicitadoPor);
            if (anterior == null || anterior.Id == Guid.Empty || anterior.Id == local.Context.UserId) return;
            try
            {
                local.ServicioSistema.Execute(new RevokeAccessRequest
                {
                    Target = new EntityReference(Esquema.Asignacion, cerrada.Id),
                    Revokee = new EntityReference(Esquema.Usuario, anterior.Id),
                });
                local.Trace("compartido del solicitante anterior quitado.");
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                local.Trace("no se pudo quitar el compartido del solicitante anterior (codigo {0}).", ex.Detail == null ? 0 : ex.Detail.ErrorCode);
            }
        }

        private static T Entrada<T>(IPluginExecutionContext ctx, string nombre)
        {
            var inputs = ctx.InputParameters;
            if (inputs == null || !inputs.Contains(nombre) || inputs[nombre] == null) return default(T);
            if (!(inputs[nombre] is T)) return default(T);
            return (T)inputs[nombre];
        }

        /// <summary>Retiro opcional (la app manda vacio si no se completo): mismos limites que la columna.</summary>
        private static decimal? Retiro(LocalContext local, IPluginExecutionContext ctx, string nombre)
        {
            var inputs = ctx.InputParameters;
            if (inputs == null || !inputs.Contains(nombre) || inputs[nombre] == null) return null;
            var que = nombre == Esquema.ParamRetiroUsd ? "El retiro en dólares" : "El retiro en córdobas";
            if (!(inputs[nombre] is decimal)) throw local.Error(que + " tiene que ser un número.");
            var v = (decimal)inputs[nombre];
            if (v < 0 || v > RetiroMaximo) throw local.Error(que + " está fuera de rango.");
            return v;
        }

        /// <summary>La cuenta se lee con los permisos del LLAMADOR: si no la ve, la API no la toca.</summary>
        private static void ExigirCuentaVisible(LocalContext local, Guid cuentaId)
        {
            try
            {
                var cuenta = local.Service.Retrieve(Esquema.Cuenta, cuentaId, new ColumnSet(false));
                if (cuenta != null) return;
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                // sin acceso o inexistente: mismo mensaje, para no revelar cual de las dos
            }
            throw local.Error("La cuenta no existe o no tenés acceso a ella.");
        }

        private static List<Entity> EspecialesDeLaCuenta(IOrganizationService svc, Guid cuentaId)
        {
            var q = new QueryExpression(Esquema.Asignacion)
            {
                ColumnSet = new ColumnSet(Esquema.AsignacionEstado, Esquema.AsignacionEstadoAprobacion, Esquema.AsignacionSolicitadoPor),
            };
            q.Criteria.AddCondition(Esquema.AsignacionCuentaId, ConditionOperator.Equal, cuentaId);
            q.Criteria.AddCondition(Esquema.AsignacionTipo, ConditionOperator.Equal, Esquema.TipoEspecial);
            var filas = svc.RetrieveMultiple(q);
            return filas == null ? new List<Entity>() : filas.Entities.ToList();
        }

        /// <summary>Nombre de quien pidio la Especial activa: el de la referencia o, si no viene, el del usuario.</summary>
        private static string NombreDeQuienPidio(LocalContext local, Entity activa)
        {
            var quien = activa.GetAttributeValue<EntityReference>(Esquema.AsignacionSolicitadoPor);
            if (quien == null) return null;
            if (!string.IsNullOrEmpty(quien.Name)) return quien.Name;
            try
            {
                var u = local.ServicioSistema.Retrieve(Esquema.Usuario, quien.Id, new ColumnSet(Esquema.UsuarioNombreCompleto));
                return u == null ? null : u.GetAttributeValue<string>(Esquema.UsuarioNombreCompleto);
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                return null;
            }
        }

        private static void Responder(LocalContext local, Guid id, string resultado, string solicitadoPor, OptionSetValue aprobacion)
        {
            var salida = local.Context.OutputParameters;
            salida[Esquema.SalidaAsignacionId] = id;
            salida[Esquema.SalidaResultado] = resultado;
            salida[Esquema.SalidaSolicitadoPor] = solicitadoPor ?? string.Empty;
            salida[Esquema.SalidaEstadoAprobacion] = aprobacion == null ? 0 : aprobacion.Value;
            local.Trace("resultado: {0}.", resultado);
        }

        /// <summary>
        /// Fecha asignada (solo fecha): el dia de hoy en la zona horaria del llamador, porque en UTC puede ser ya
        /// el dia siguiente. Si no se puede leer la zona, usa el dia UTC.
        /// </summary>
        private static DateTime HoyDelLlamador(LocalContext local)
        {
            var ahora = DateTime.UtcNow;
            try
            {
                var q = new QueryExpression("usersettings") { ColumnSet = new ColumnSet("timezonecode"), TopCount = 1 };
                q.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, local.Context.UserId);
                var filas = local.ServicioSistema.RetrieveMultiple(q);
                var fila = filas == null ? null : filas.Entities.FirstOrDefault();
                var zona = fila == null ? null : fila.GetAttributeValue<int?>("timezonecode");
                if (zona.HasValue)
                {
                    var r = local.ServicioSistema.Execute(new LocalTimeFromUtcTimeRequest { TimeZoneCode = zona.Value, UtcTime = ahora })
                        as LocalTimeFromUtcTimeResponse;
                    if (r != null) return DateTime.SpecifyKind(r.LocalTime.Date, DateTimeKind.Unspecified);
                }
                local.Trace("sin zona horaria del llamador: la fecha asignada usa el dia UTC.");
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                local.Trace("no se pudo leer la zona horaria del llamador (codigo {0}): la fecha asignada usa el dia UTC.", ex.Detail == null ? 0 : ex.Detail.ErrorCode);
            }
            return DateTime.SpecifyKind(ahora.Date, DateTimeKind.Unspecified);
        }
    }
}
