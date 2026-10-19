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
    /// DAT-13 + SEC-08: el alta de una interaccion.
    /// - Toda interaccion tiene cuenta y agente (eran ApplicationRequired, que el servidor no exige: habia interacciones
    ///   "Sin nombre" que el backoffice no podia atribuir).
    /// - Si la crea un cobrador (rol Localizador, sin Admin/Servicio), el agente es el vinculado a SU usuario: lo pone el
    ///   servidor y no la app, asi nadie registra visitas a nombre de otro. Con dos agentes vinculados vale el enviado
    ///   si es uno de ellos.
    /// - El cobrador solo registra en cuentas de su cartera (H3, <see cref="ExigirCuentaDeSuCartera"/>); con tolerancia por
    ///   fecha de captura si se la reasignaron mientras estaba sin senal (1.0.8.5, <see cref="EraSuyaALaFecha"/>).
    /// - La interaccion del cobrador nace Pendiente de grabar en el CORE: el estado, el codigo, la fecha y el usuario
    ///   de grabacion no los pone el telefono (DAT-11).
    /// Las capturas offline se suben con el usuario del cobrador, asi que la regla aplica igual al sincronizar.
    ///
    /// Registro: PreOperation sincrono, Create de sanic_cm_tbl_interaccion. Nunca traza ni cita valores.
    /// </summary>
    public sealed class InteraccionDelCobrador : PluginBase
    {
        /// <summary>Una captura no puede traer una fecha mas adelantada que el reloj del servidor mas este margen.</summary>
        public static readonly TimeSpan MargenDeReloj = TimeSpan.FromMinutes(5);

        /// <summary>Hasta cuanto tiempo atras vale la fecha de captura de una visita sincronizada tarde.</summary>
        public static readonly TimeSpan VentanaMaxima = TimeSpan.FromHours(48);

        private readonly Func<DateTime> ahora;

        public InteraccionDelCobrador() : this(() => DateTime.UtcNow)
        {
        }

        /// <summary>Para las pruebas: el reloj se inyecta.</summary>
        public InteraccionDelCobrador(Func<DateTime> ahora)
        {
            if (ahora == null) throw new ArgumentNullException("ahora");
            this.ahora = ahora;
        }

        protected override string Nombre
        {
            get { return "InteraccionDelCobrador"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Interaccion, Esquema.EtapaPreOperation, Esquema.MensajeCreate);

            var target = local.ObtenerTarget();
            if (target.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId) == null)
            {
                throw local.Error("la interaccion tiene que tener la cuenta.");
            }

            var roles = RolesCm.DelLlamador(local);
            if (roles.Tiene(RolesCm.Localizador) && !roles.EsAdministracion)
            {
                ExigirCuentaDeSuCartera(local, target.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId),
                    target.GetAttributeValue<DateTime?>(Esquema.InteraccionFechaCaptura));
                PonerAgenteDelCobrador(local, target);
                target[Esquema.InteraccionEstadoGrabacion] = new OptionSetValue(Esquema.GrabacionPendiente);
                target.Attributes.Remove(Esquema.InteraccionCodigoGrabacion);
                target.Attributes.Remove(Esquema.InteraccionFechaGrabacion);
                target.Attributes.Remove(Esquema.InteraccionGrabadoPor);
            }

            if (target.GetAttributeValue<EntityReference>(Esquema.InteraccionAgente) == null)
            {
                throw local.Error("la interaccion tiene que tener el agente.");
            }
        }

        /// <summary>
        /// H3 (matriz de roles, 2026-10-19): el cobrador registra visitas (y, por la interaccion, pagos) solo en cuentas de
        /// SU cartera: una asignacion activa de esa cuenta cuyo dueno es su usuario (el reparto se la pasa al aprobarla o
        /// al cargarla). Antes le alcanzaba con ver la cuenta: el rol tiene AppendTo Global en cuenta porque lo exige el
        /// lookup. Una cuenta de la "Cartera sin cobrador" (dueno el equipo, sin miembros) no es de ningun cobrador.
        ///
        /// Tolerancia por fecha (1.0.8.5): una captura offline se sincroniza despues, y entre medio el supervisor pudo
        /// reasignar la cuenta. Si la fecha de captura (la pone el telefono, asi que se acota: no mas de 5 min adelantada ni
        /// mas de 48 h atras) es de un momento en que la cuenta ERA del cobrador, la interaccion entra. Fuera de ese rango, o
        /// si no trae fecha, rige la regla estricta de siempre. Los pagos cuelgan de la interaccion: entran con ella.
        /// </summary>
        private void ExigirCuentaDeSuCartera(LocalContext local, EntityReference cuenta, DateTime? captura)
        {
            var q = new QueryExpression(Esquema.Asignacion) { ColumnSet = new ColumnSet(false), TopCount = 1 };
            q.Criteria.AddCondition(Esquema.AsignacionCuentaId, ConditionOperator.Equal, cuenta.Id);
            q.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.Propietario, ConditionOperator.Equal, local.Context.UserId);
            var filas = local.ServicioSistema.RetrieveMultiple(q);
            if (filas != null && filas.Entities.Count > 0) return;

            if (captura.HasValue)
            {
                var cuando = EnUtc(captura.Value);
                var reloj = EnUtc(ahora());
                if (cuando <= reloj + MargenDeReloj && cuando >= reloj - VentanaMaxima)
                {
                    if (EraSuyaALaFecha(local, cuenta.Id, cuando))
                    {
                        local.Trace("la cuenta ya no es de la cartera del cobrador, pero lo era al capturar: entra por tolerancia de fecha.");
                        return;
                    }
                }
                else
                {
                    local.Trace("la fecha de captura esta fuera del rango tolerado: rige la regla estricta.");
                }
            }

            throw local.Error("esa cuenta no esta en tu cartera: solo podes registrar visitas y pagos de las cuentas que tenes asignadas. "
                + "Si te la reasignaron recien, sincroniza y volve a intentar; si la capturaste sin senal, tiene que sincronizarse "
                + "dentro de las 48 horas.");
        }

        /// <summary>
        /// ¿Alguna asignacion de la cuenta, a la fecha dada, existia, estaba activa y era del llamador? Se responde con la
        /// auditoria de dueno y estado de la asignacion (la org audita 365 dias), con el servicio del sistema. Solo se llega
        /// aca cuando la regla estricta fallo y la fecha esta en el rango, asi que el costo es de la minoria. Si la
        /// auditoria no se puede leer, falla cerrado.
        /// </summary>
        private static bool EraSuyaALaFecha(LocalContext local, Guid cuentaId, DateTime fecha)
        {
            var q = new QueryExpression(Esquema.Asignacion)
            {
                ColumnSet = new ColumnSet(Esquema.CreadoEl, Esquema.Propietario, Esquema.AsignacionEstado),
                TopCount = 20,
            };
            q.Criteria.AddCondition(Esquema.AsignacionCuentaId, ConditionOperator.Equal, cuentaId);
            var asignaciones = local.ServicioSistema.RetrieveMultiple(q);
            if (asignaciones == null) return false;
            local.Trace("asignaciones de la cuenta a revisar: {0}.", asignaciones.Entities.Count);

            try
            {
                foreach (var a in asignaciones.Entities)
                {
                    var creada = a.GetAttributeValue<DateTime?>(Esquema.CreadoEl);
                    if (creada.HasValue && EnUtc(creada.Value) > fecha)
                    {
                        local.Trace("una asignacion todavia no existia a la fecha de captura.");
                        continue;
                    }

                    var duenoActual = a.GetAttributeValue<EntityReference>(Esquema.Propietario);
                    var dueno = ValorALaFecha<EntityReference>(local, a.Id, Esquema.Propietario, fecha, duenoActual);
                    if (dueno == null || dueno.Id != local.Context.UserId)
                    {
                        local.Trace("a la fecha de captura la asignacion no era del cobrador.");
                        continue;
                    }

                    var activa = ValorALaFecha<bool?>(local, a.Id, Esquema.AsignacionEstado, fecha,
                        a.GetAttributeValue<bool?>(Esquema.AsignacionEstado));
                    if (activa == true) return true;
                }
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                local.Trace("no se pudo leer la auditoria de la asignacion: rige la regla estricta.");
                return false;
            }
            return false;
        }

        /// <summary>
        /// Valor de la columna a la fecha dada: el valor anterior del PRIMER cambio posterior a esa fecha (cambios en la
        /// misma fecha ya cuentan como hechos) o, si no hubo ninguno despues, el valor actual.
        /// </summary>
        private static T ValorALaFecha<T>(LocalContext local, Guid asignacionId, string columna, DateTime fecha, T actual)
        {
            var pedido = new RetrieveAttributeChangeHistoryRequest
            {
                Target = new EntityReference(Esquema.Asignacion, asignacionId),
                AttributeLogicalName = columna,
                PagingInfo = new PagingInfo { Count = 500, PageNumber = 1 },
            };
            var respuesta = local.ServicioSistema.Execute(pedido) as RetrieveAttributeChangeHistoryResponse;
            var coleccion = respuesta == null ? null : respuesta.AuditDetailCollection;
            if (coleccion == null)
            {
                local.Trace("{0}: la auditoria no devolvio coleccion; se toma el valor actual.", columna);
                return actual;
            }

            foreach (var d in coleccion.AuditDetails)
            {
                local.Trace("{0}: detalle de tipo {1}, registro de auditoria: {2}.", columna, d.GetType().Name,
                    d.AuditRecord == null ? "ninguno" : string.Join(",", d.AuditRecord.Attributes.Keys));
            }
            var posteriores = new List<KeyValuePair<DateTime, AttributeAuditDetail>>();
            foreach (var detalle in coleccion.AuditDetails.OfType<AttributeAuditDetail>())
            {
                var cuando = detalle.AuditRecord == null ? null : detalle.AuditRecord.GetAttributeValue<DateTime?>(Esquema.AuditoriaCreadoEl);
                if (cuando.HasValue && EnUtc(cuando.Value) > fecha)
                {
                    posteriores.Add(new KeyValuePair<DateTime, AttributeAuditDetail>(EnUtc(cuando.Value), detalle));
                }
            }
            local.Trace("{0}: {1} cambio(s) auditado(s), {2} posterior(es) a la fecha de captura.", columna,
                coleccion.AuditDetails.Count, posteriores.Count);
            if (posteriores.Count == 0) return actual;

            var primero = posteriores.OrderBy(p => p.Key).First().Value;
            local.Trace("{0}: el cambio posterior trae valor anterior: {1}.", columna,
                primero.OldValue != null && primero.OldValue.Contains(columna));
            return primero.OldValue == null || !primero.OldValue.Contains(columna)
                ? default(T)
                : primero.OldValue.GetAttributeValue<T>(columna);
        }

        private static DateTime EnUtc(DateTime d)
        {
            return d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }

        private static void PonerAgenteDelCobrador(LocalContext local, Entity target)
        {
            var q = new QueryExpression(Esquema.Agente) { ColumnSet = new ColumnSet(Esquema.AgenteNombre), TopCount = 2 };
            q.Criteria.AddCondition(Esquema.AgenteUsuario, ConditionOperator.Equal, local.Context.UserId);
            var agentes = local.ServicioSistema.RetrieveMultiple(q).Entities;
            if (agentes.Count == 0)
            {
                throw local.Error("tu usuario no esta vinculado a ningun agente; pedile al Admin que lo vincule en Agentes.");
            }

            var enviado = target.GetAttributeValue<EntityReference>(Esquema.InteraccionAgente);
            var agente = agentes.Count == 1
                ? agentes[0]
                : agentes.FirstOrDefault(a => enviado != null && a.Id == enviado.Id);
            if (agente == null)
            {
                throw local.Error("tu usuario esta vinculado a mas de un agente; pedile al Admin que deje uno solo.");
            }

            target[Esquema.InteraccionAgente] = new EntityReference(Esquema.Agente, agente.Id);
            var nombre = agente.GetAttributeValue<string>(Esquema.AgenteNombre);
            if (!string.IsNullOrWhiteSpace(nombre)) target[Esquema.InteraccionNombreAgente] = nombre;
            local.Trace(enviado != null && enviado.Id == agente.Id
                ? "agente del usuario confirmado."
                : "agente puesto por el servidor (la app no mando el del usuario).");
        }
    }
}
