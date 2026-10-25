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
    /// DAT-14 (decision del dueno, 2026-10-14: opcion a): Custom API global "sanic_cm_SolicitarEspecial", que reemplaza
    /// el Patch de scrNuevaSolicitud. El Solicitante lee solo sus asignaciones (lectura Basic): si la cuenta ya tenia una
    /// Especial de otra persona, la app no la veia y el alta chocaba con la clave alternativa (clave PAN, tipo).
    /// La API ve todas las Especiales de la cuenta con el servicio del sistema, sin ampliar la lectura del rol:
    ///  a) Especial ACTIVA: no la toca. Devuelve "ya existe", quien la pidio y su estado de aprobacion.
    ///  b) Especial CERRADA: la reactiva con las mismas columnas que la rama b) de la app (Pendiente, retiros, notas,
    ///     solicitado por = el llamador, fecha de asignacion = ahora, sin agente, resolutor, motivos ni fecha de cierre)
    ///     y la pasa al llamador como dueno. La escribe el servicio del sistema: la cerrada puede ser de otra persona.
    ///     S-7 (b1, dueno 2026-10-19): si la habia pedido otra persona, le quita el compartido de la asignacion que le dio
    ///     el reparto (si no, seguia leyendo una Especial que ya no es suya). Solo la asignacion: la cuenta, el cliente y el
    ///     fiador los reparte RepartoAsignacion y los revisa paso3_reconciliar. Si la revocacion falla, la reactivacion
    ///     sigue (traza el codigo): un usuario deshabilitado no puede trabar que otro pida la cuenta.
    ///  c) Ninguna: la crea CON LOS PERMISOS DEL LLAMADOR (como hoy la app), asi el dueno y "creado por" son el
    ///     Solicitante: la ve en "Mis solicitudes" y, al aprobarse, RepartoAsignacion se la comparte (S-7).
    /// Valida al LLAMADOR: rol Solicitante, Admin o Servicio (o sin roles de CM), y que pueda leer la cuenta con sus
    /// permisos (la API no sirve para averiguar cuentas ajenas). Las notas no pueden traer un PAN (13 digitos o mas).
    /// Los demas plugins de la asignacion corren igual: ClaveDesdeCuenta completa la clave, AprobacionEspecial no frena
    /// (Pendiente y quitar el agente no son cambios controlados) y RepartoAsignacion no reparte una Especial sin aprobar.
    /// Nunca traza ni cita valores: ni montos, ni notas, ni identificadores.
    /// Registro: Custom API global (sin entidad), no es funcion, sin pasos personalizados; este tipo es su plugin.
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
                throw local.Error("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeSolicitarEspecial + ".");
            }

            var roles = RolesCm.DelLlamador(local);
            if (!roles.SinRolesCm && !roles.TieneAlguno(RolesQueSolicitan))
            {
                throw local.Error("solo un Solicitante (o un Admin) puede pedir una asignacion Especial.");
            }

            var cuentaId = Entrada<Guid>(ctx, Esquema.ParamCuentaId);
            if (cuentaId == Guid.Empty) throw local.Error("falta la cuenta.");
            var notas = Entrada<string>(ctx, Esquema.ParamNotas);
            notas = notas == null ? null : notas.Trim();
            if (string.IsNullOrEmpty(notas)) throw local.Error("faltan las notas de la solicitud (tipo y motivo).");
            if (notas.Length > LargoMaximoNotas) throw local.Error("las notas pasan de " + LargoMaximoNotas + " caracteres.");
            if (PanDetector.ContieneSecuenciaDeDigitos(notas, DigitosDePan))
            {
                throw local.Error("las notas no pueden llevar un numero de tarjeta.");
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
                // Lo mas probable: otra persona la pidio en el mismo momento (clave alternativa). Solo se traza el codigo.
                local.Trace("el alta fallo con el codigo {0}.", ex.Detail == null ? 0 : ex.Detail.ErrorCode);
                throw local.Error("no se pudo crear la Especial. Volve a intentar: si otra persona la pidio recien, "
                    + "la respuesta va a decir quien.");
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
            if (!(inputs[nombre] is decimal)) throw local.Error("el parametro " + nombre + " debe ser decimal.");
            var v = (decimal)inputs[nombre];
            if (v < 0 || v > RetiroMaximo) throw local.Error("el parametro " + nombre + " esta fuera de rango.");
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
            throw local.Error("la cuenta no existe o no tenes acceso a ella.");
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
        /// "Fecha asignada" (DateOnly, 2026-10-25): el dia de HOY para quien pide, en la zona horaria de su configuracion
        /// personal de Dataverse (en UTC, despues de las 18:00 de Managua ya seria manana). Si no se puede leer, el dia UTC.
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
