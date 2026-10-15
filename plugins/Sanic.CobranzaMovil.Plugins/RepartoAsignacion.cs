using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Reparto de la asignacion. Reemplaza al flow "Cloud Flow | CM | Cambio de Owner en Asignaciones", que fallaba
    /// en ~40% de las corridas (2026-10-12) por dos causas:
    ///  a) intentaba revocarle la cuenta a SYSTEM siendo SYSTEM su dueno ("Only owner can revoke access to the owner");
    ///  b) varias asignaciones del mismo cliente compartian el cliente a la vez ("More than one concurrent Share
    ///     requests detected") y, como las acciones iban encadenadas, se salteaban el fiador y la REVOCACION al
    ///     cobrador anterior.
    ///
    /// Que hace, sobre el estado ACTUAL de la asignacion (lo lee al correr: converge, no repite el evento):
    ///  - Activa, y no es una Especial sin aprobar (S-6):
    ///    1) pasa el dueno al usuario del agente o, si el agente no tiene usuario o no hay agente, al equipo
    ///       "Cartera sin cobrador" (DAT-07; antes no hacia nada y el cobrador anterior la conservaba). Va antes de
    ///       mirar otras asignaciones: ver RevocarSiNoQuedaOtra;
    ///    2) si es Especial, se la comparte (lectura, escritura, anexar) a "Solicitado por" (S-7: una persona de CM
    ///       solo puede ponerse a si misma, ver SolicitadoPor);
    ///    3) si hay cobrador, le comparte en lectura la cuenta, el cliente y el fiador;
    ///    4) si cambio el dueno, al dueno anterior le revoca cuenta, cliente y fiador que ya no justifique otra
    ///       asignacion activa suya.
    ///  - Cerrada: al dueno actual le revoca lo que ya no justifique otra asignacion activa suya.
    /// Cada paso corre aunque falle otro (un compartido fallido ya no deja sin revocar al anterior). Un compartido
    /// concurrente se reintenta aca mismo con espera; si algo queda sin hacer, la ejecucion termina en error: con
    /// reintento del servicio asincrono si la falla es transitoria (4 intentos mas) y, si no, como trabajo del sistema
    /// fallido, visible con el Service Principal. scripts/seguridad/paso3_reconciliar.py queda como red de seguridad.
    ///
    /// Corre con el servicio del SISTEMA: dar y quitar acceso no es algo que ningun rol de persona tenga.
    /// Registro: PostOperation ASINCRONO, Create y Update de sanic_cm_tbl_asignacion; el Update filtrado por agente,
    /// estado y estado de aprobacion. NUNCA sincrono: correria dentro del trozo de Smelter (100 filas por mensaje,
    /// 2 minutos por mensaje, y si se pasa se deshace el trozo entero).
    /// Nunca traza ni cita identificadores ni valores de registros: solo nombres de tablas y pasos.
    /// </summary>
    public sealed class RepartoAsignacion : PluginBase
    {
        private static readonly int ErrorShareConcurrente = unchecked((int)0x80048105);
        private static readonly int ErrorRevocarAlDueno = unchecked((int)0x80040223);
        private static readonly int ErrorNoExiste = unchecked((int)0x80040217);
        private static readonly int ErrorReparto = unchecked((int)0x80040265); // ISV: la logica de negocio aborto

        private const int IntentosPorPaso = 3;
        private const int EsperaMaximaTotalMs = 60000;

        private readonly Action<int> esperar;

        public RepartoAsignacion() : this(ms => Thread.Sleep(ms))
        {
        }

        /// <summary>Para las pruebas: la espera entre reintentos se inyecta (no se duerme de verdad).</summary>
        public RepartoAsignacion(Action<int> esperar)
        {
            if (esperar == null) throw new ArgumentNullException("esperar");
            this.esperar = esperar;
        }

        protected override string Nombre
        {
            get { return "RepartoAsignacion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPostOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);
            if (local.Context.Mode != Esquema.ModoAsincrono)
            {
                throw local.Error("el paso debe registrarse asincrono: sincrono correria dentro del trozo de la carga de Smelter.");
            }

            var svc = local.ServicioSistema;
            var id = local.Context.PrimaryEntityId;
            var asignacion = Leer(svc, Esquema.Asignacion, id, Esquema.AsignacionEstado, Esquema.AsignacionTipo,
                Esquema.AsignacionEstadoAprobacion, Esquema.AsignacionAgente, Esquema.AsignacionCuentaId,
                Esquema.AsignacionSolicitadoPor, Esquema.Propietario);
            if (asignacion == null)
            {
                local.Trace("la asignacion ya no existe; nada que repartir.");
                return;
            }

            var dueno = asignacion.GetAttributeValue<EntityReference>(Esquema.Propietario);
            var cuentaRef = asignacion.GetAttributeValue<EntityReference>(Esquema.AsignacionCuentaId);
            var pasos = new Pasos(local, esperar);

            if (asignacion.GetAttributeValue<bool?>(Esquema.AsignacionEstado) == true)
            {
                Repartir(local, svc, asignacion, dueno, cuentaRef, pasos, Nombre);
            }
            else if (dueno != null)
            {
                local.Trace("asignacion cerrada: se revoca al dueno actual.");
                var cuenta = cuentaRef == null ? null : LeerCuenta(svc, cuentaRef.Id);
                RevocarSiNoQuedaOtra(svc, id, Principal(dueno), cuentaRef, cuenta, pasos);
            }

            pasos.Terminar(Nombre);
        }

        private static void Repartir(LocalContext local, IOrganizationService svc, Entity asignacion, EntityReference dueno,
            EntityReference cuentaRef, Pasos pasos, string nombre)
        {
            var tipo = asignacion.GetAttributeValue<OptionSetValue>(Esquema.AsignacionTipo);
            var aprobacion = asignacion.GetAttributeValue<OptionSetValue>(Esquema.AsignacionEstadoAprobacion);
            var esEspecial = tipo != null && tipo.Value == Esquema.TipoEspecial;
            if (esEspecial && (aprobacion == null || aprobacion.Value != Esquema.AprobacionAprobado))
            {
                local.Trace("Especial sin aprobar (S-6): no se reparte.");
                return;
            }

            var agenteRef = asignacion.GetAttributeValue<EntityReference>(Esquema.AsignacionAgente);
            var agente = agenteRef == null ? null : Leer(svc, Esquema.Agente, agenteRef.Id, Esquema.AgenteUsuario);
            var usuarioRef = agente == null ? null : agente.GetAttributeValue<EntityReference>(Esquema.AgenteUsuario);
            var cobrador = usuarioRef == null ? null : new EntityReference(Esquema.Usuario, usuarioRef.Id);

            // Sin cobrador, la asignacion pasa a la cartera sin cobrador (DAT-07). Si ese equipo no existe, falla sin
            // tocar nada: mejor un trabajo fallido visible que revocarle al dueno algo que sigue siendo suyo.
            EntityReference nuevoDueno = cobrador;
            if (nuevoDueno == null)
            {
                local.Trace(agenteRef == null ? "sin agente: pasa a la cartera sin cobrador."
                    : "el agente no tiene usuario: pasa a la cartera sin cobrador.");
                nuevoDueno = CarteraSinCobrador.Exigir(svc, nombre);
            }

            var asignacionRef = new EntityReference(Esquema.Asignacion, asignacion.Id);
            var cambiaDueno = dueno == null || dueno.Id != nuevoDueno.Id;
            var cuenta = cuentaRef == null ? null : LeerCuenta(svc, cuentaRef.Id);

            // 1) Dueno primero: si dos asignaciones del mismo cliente se reasignan a la vez y cada una mueve su dueno
            //    ANTES de buscar las otras del anterior, al menos una ve a la otra ya movida y revoca el cliente.
            if (cambiaDueno)
            {
                pasos.Correr(cobrador != null ? "pasar el dueno al cobrador" : "pasar el dueno a la cartera sin cobrador", () =>
                {
                    var cambio = new Entity(Esquema.Asignacion, asignacion.Id);
                    cambio[Esquema.Propietario] = nuevoDueno;
                    svc.Update(cambio);
                });
            }

            // 2) La Especial aprobada se le comparte a quien la pidio (S-7). "Solicitado por" no lo elige el cliente: una
            //    persona de CM solo puede ponerse a si misma (SolicitadoPor). Antes se exigia ademas que fuera quien la
            //    creo, y quien reactivaba con SolicitarEspecial una Especial creada por otro la perdia al aprobarse.
            var solicitante = asignacion.GetAttributeValue<EntityReference>(Esquema.AsignacionSolicitadoPor);
            if (esEspecial && solicitante != null)
            {
                pasos.Correr("compartir la asignacion con el solicitante", () => Compartir(svc, asignacionRef,
                    new EntityReference(Esquema.Usuario, solicitante.Id),
                    AccessRights.ReadAccess | AccessRights.WriteAccess | AccessRights.AppendAccess));
            }

            // 3) Lectura de cuenta, cliente y fiador para el cobrador (la cartera sin cobrador no comparte nada).
            if (cobrador != null && cuentaRef != null)
            {
                pasos.Correr("compartir la cuenta", () => Compartir(svc, Ref(Esquema.Cuenta, cuentaRef), cobrador, AccessRights.ReadAccess));
                var cliente = cuenta == null ? null : cuenta.GetAttributeValue<EntityReference>(Esquema.CuentaCliente);
                if (cliente != null)
                {
                    pasos.Correr("compartir el cliente", () => Compartir(svc, Ref(Esquema.Cliente, cliente), cobrador, AccessRights.ReadAccess));
                }
                var fiador = cuenta == null ? null : cuenta.GetAttributeValue<EntityReference>(Esquema.CuentaFiador);
                if (fiador != null)
                {
                    pasos.Correr("compartir el fiador", () => Compartir(svc, Ref(Esquema.Fiador, fiador), cobrador, AccessRights.ReadAccess));
                }
            }

            // 4) Al dueno anterior se le revoca lo que ya no justifica, haya fallado o no algun compartido.
            if (cambiaDueno && dueno != null)
            {
                RevocarSiNoQuedaOtra(svc, asignacion.Id, Principal(dueno), cuentaRef, cuenta, pasos);
            }
        }

        /// <summary>
        /// Revoca a <paramref name="principal"/> la cuenta, el cliente y el fiador si no tiene OTRA asignacion activa
        /// (de la que es dueno) que los justifique. A la cuenta no se le revoca a su propio dueno (SYSTEM, para lo que
        /// crea la carga); para cliente y fiador ese caso lo resuelve <see cref="Revocar"/>.
        /// </summary>
        private static void RevocarSiNoQuedaOtra(IOrganizationService svc, Guid asignacionId, EntityReference principal,
            EntityReference cuentaRef, Entity cuenta, Pasos pasos)
        {
            if (cuentaRef == null) return;

            var duenoCuenta = cuenta == null ? null : cuenta.GetAttributeValue<EntityReference>(Esquema.Propietario);
            if (duenoCuenta == null || duenoCuenta.Id != principal.Id)
            {
                pasos.Correr("revocar la cuenta al dueno anterior", () =>
                {
                    if (!TieneOtra(svc, principal, asignacionId, null, cuentaRef.Id)) Revocar(svc, Ref(Esquema.Cuenta, cuentaRef), principal);
                });
            }

            var cliente = cuenta == null ? null : cuenta.GetAttributeValue<EntityReference>(Esquema.CuentaCliente);
            if (cliente != null)
            {
                pasos.Correr("revocar el cliente al dueno anterior", () =>
                {
                    if (!TieneOtra(svc, principal, asignacionId, Esquema.CuentaCliente, cliente.Id)) Revocar(svc, Ref(Esquema.Cliente, cliente), principal);
                });
            }

            var fiador = cuenta == null ? null : cuenta.GetAttributeValue<EntityReference>(Esquema.CuentaFiador);
            if (fiador != null)
            {
                pasos.Correr("revocar el fiador al dueno anterior", () =>
                {
                    if (!TieneOtra(svc, principal, asignacionId, Esquema.CuentaFiador, fiador.Id)) Revocar(svc, Ref(Esquema.Fiador, fiador), principal);
                });
            }
        }

        /// <summary>
        /// ¿El principal es dueno de otra asignacion activa (no esta) de la misma cuenta, o cuya cuenta tenga ese
        /// cliente/fiador? <paramref name="columnaDeLaCuenta"/> null = misma cuenta.
        /// </summary>
        private static bool TieneOtra(IOrganizationService svc, EntityReference principal, Guid asignacionId,
            string columnaDeLaCuenta, Guid valor)
        {
            var q = new QueryExpression(Esquema.Asignacion) { ColumnSet = new ColumnSet(false), TopCount = 1 };
            q.Criteria.AddCondition(Esquema.Propietario, ConditionOperator.Equal, principal.Id);
            q.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.AsignacionId, ConditionOperator.NotEqual, asignacionId);
            if (columnaDeLaCuenta == null)
            {
                q.Criteria.AddCondition(Esquema.AsignacionCuentaId, ConditionOperator.Equal, valor);
            }
            else
            {
                var cuenta = q.AddLink(Esquema.Cuenta, Esquema.AsignacionCuentaId, Esquema.CuentaId);
                cuenta.LinkCriteria.AddCondition(columnaDeLaCuenta, ConditionOperator.Equal, valor);
            }
            var filas = svc.RetrieveMultiple(q);
            return filas != null && filas.Entities.Count > 0;
        }

        private static void Compartir(IOrganizationService svc, EntityReference registro, EntityReference principal, AccessRights permisos)
        {
            svc.Execute(new GrantAccessRequest
            {
                Target = registro,
                PrincipalAccess = new PrincipalAccess { Principal = principal, AccessMask = permisos },
            });
        }

        /// <summary>Revocarle a alguien el acceso a lo que es suyo no se puede ni hace falta: se toma como hecho.</summary>
        private static void Revocar(IOrganizationService svc, EntityReference registro, EntityReference principal)
        {
            try
            {
                svc.Execute(new RevokeAccessRequest { Target = registro, Revokee = principal });
            }
            catch (FaultException<OrganizationServiceFault> f)
            {
                if (Codigo(f) != ErrorRevocarAlDueno) throw;
            }
        }

        private static Entity LeerCuenta(IOrganizationService svc, Guid id)
        {
            return Leer(svc, Esquema.Cuenta, id, Esquema.CuentaCliente, Esquema.CuentaFiador, Esquema.Propietario);
        }

        /// <summary>Retrieve de columnas justas; null si el registro ya no existe.</summary>
        private static Entity Leer(IOrganizationService svc, string tabla, Guid id, params string[] columnas)
        {
            try
            {
                return svc.Retrieve(tabla, id, new ColumnSet(columnas));
            }
            catch (FaultException<OrganizationServiceFault> f)
            {
                if (Codigo(f) == ErrorNoExiste) return null;
                throw;
            }
        }

        private static EntityReference Ref(string tabla, EntityReference r)
        {
            return new EntityReference(tabla, r.Id);
        }

        /// <summary>El dueno puede ser un usuario o un equipo: se revoca a quien sea, con su tipo.</summary>
        private static EntityReference Principal(EntityReference dueno)
        {
            return new EntityReference(string.IsNullOrEmpty(dueno.LogicalName) ? Esquema.Usuario : dueno.LogicalName, dueno.Id);
        }

        private static int Codigo(FaultException<OrganizationServiceFault> f)
        {
            return f.Detail == null ? 0 : f.Detail.ErrorCode;
        }

        /// <summary>
        /// Corre cada paso aislado: reintenta el compartido concurrente con espera creciente y aleatoria, anota lo
        /// que falla y sigue con el resto. Al final, si algo fallo, termina la ejecucion en error.
        /// </summary>
        private sealed class Pasos
        {
            private readonly LocalContext local;
            private readonly Action<int> esperar;
            private readonly Random azar = new Random(Guid.NewGuid().GetHashCode());
            private readonly List<string> fallidos = new List<string>();
            private bool hayTransitorio;
            private int esperadoMs;

            public Pasos(LocalContext local, Action<int> esperar)
            {
                this.local = local;
                this.esperar = esperar;
            }

            public void Correr(string paso, Action accion)
            {
                for (var intento = 1; ; intento++)
                {
                    try
                    {
                        accion();
                        return;
                    }
                    catch (FaultException<OrganizationServiceFault> f)
                    {
                        var codigo = Codigo(f);
                        var concurrente = codigo == ErrorShareConcurrente;
                        if (concurrente && intento < IntentosPorPaso && esperadoMs < EsperaMaximaTotalMs)
                        {
                            var ms = 1000 * intento + azar.Next(0, 1000);
                            esperadoMs += ms;
                            local.Trace("{0}: compartido concurrente, reintento {1} en {2} ms.", paso, intento, ms);
                            esperar(ms);
                            continue;
                        }
                        Fallo(paso, "codigo 0x" + codigo.ToString("X8"), concurrente);
                        return;
                    }
                    catch (TimeoutException)
                    {
                        Fallo(paso, "tiempo agotado", true);
                        return;
                    }
                    catch (InvalidPluginExecutionException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Fallo(paso, "error de tipo " + ex.GetType().Name, false);
                        return;
                    }
                }
            }

            private void Fallo(string paso, string detalle, bool transitorio)
            {
                local.Trace("{0}: fallo ({1}).", paso, detalle);
                fallidos.Add(paso);
                hayTransitorio |= transitorio;
            }

            public void Terminar(string nombre)
            {
                if (fallidos.Count == 0)
                {
                    local.Trace("reparto completo.");
                    return;
                }
                var mensaje = nombre + ": no se pudo " + string.Join(", ", fallidos) + "."
                    + (hayTransitorio ? " Se reintenta." : " Avise al administrador; paso3_reconciliar lo completa.");
                throw new InvalidPluginExecutionException(hayTransitorio ? OperationStatus.Retry : OperationStatus.Failed,
                    ErrorReparto, mensaje);
            }
        }
    }
}
