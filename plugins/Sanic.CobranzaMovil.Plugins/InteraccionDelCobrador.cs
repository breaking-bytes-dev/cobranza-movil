using System.Linq;
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
    /// - El cobrador solo registra en cuentas de su cartera (H3, <see cref="ExigirCuentaDeSuCartera"/>).
    /// - La interaccion del cobrador nace Pendiente de grabar en el CORE: el estado, el codigo, la fecha y el usuario
    ///   de grabacion no los pone el telefono (DAT-11).
    /// Las capturas offline se suben con el usuario del cobrador, asi que la regla aplica igual al sincronizar.
    ///
    /// Registro: PreOperation sincrono, Create de sanic_cm_tbl_interaccion. Nunca traza ni cita valores.
    /// </summary>
    public sealed class InteraccionDelCobrador : PluginBase
    {
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
                ExigirCuentaDeSuCartera(local, target.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId));
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
        /// </summary>
        private static void ExigirCuentaDeSuCartera(LocalContext local, EntityReference cuenta)
        {
            var q = new QueryExpression(Esquema.Asignacion) { ColumnSet = new ColumnSet(false), TopCount = 1 };
            q.Criteria.AddCondition(Esquema.AsignacionCuentaId, ConditionOperator.Equal, cuenta.Id);
            q.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.Propietario, ConditionOperator.Equal, local.Context.UserId);
            var filas = local.ServicioSistema.RetrieveMultiple(q);
            if (filas != null && filas.Entities.Count > 0) return;
            throw local.Error("esa cuenta no esta en tu cartera: solo podes registrar visitas y pagos de las cuentas que tenes asignadas. "
                + "Si te la reasignaron recien, sincroniza y volve a intentar.");
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
