using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// APP-24: una Actualizacion de datos debe colgar de una interaccion que tenga cuenta. Por ese camino
    /// (actualizacion -> interaccion -> cuenta -> CIF) el backoffice sabe de que cliente es y arma el "antes".
    /// La app ya la crea despues de la interaccion; esto lo garantiza tambien fuera de la app.
    ///
    /// F7: en el telefono (runtime offline) el lookup a la interaccion recien creada llega vacio. La app manda ademas
    /// la cuenta (que ya existe en el servidor) y la MISMA fecha de captura que puso en la interaccion; con eso el
    /// servidor busca la interaccion del mismo cobrador, cuenta y captura (con actualiza info) y la vincula. Si no la
    /// encuentra todavia, rechaza: la sincronizacion la reintenta. Con la interaccion, completa la cuenta si falta y
    /// rechaza si no coincide.
    ///
    /// Lee la interaccion con los permisos del llamador (el cobrador lee las suyas). Nunca cita valores.
    /// Registro: PreOperation sincrono. Create y Update de sanic_cm_tbl_actualizaciondatos; el Update filtrado
    /// por sanic_cm_ad_interaccionid.
    /// </summary>
    public sealed class ActualizacionConInteraccion : PluginBase
    {
        protected override string Nombre
        {
            get { return "ActualizacionConInteraccion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.ActualizacionDatos, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            var esCreate = local.EsMensaje(Esquema.MensajeCreate);
            // F7: que columnas manda el telefono (solo NOMBRES, nunca valores). Offline el runtime descarta columnas en silencio.
            local.Trace("columnas recibidas: {0}", string.Join(",", new System.Collections.Generic.List<string>(target.Attributes.Keys).ToArray()));
            if (!esCreate && !target.Contains(Esquema.ActualizacionInteraccion))
            {
                local.Trace("el cambio no toca la interaccion; sin control.");
                return;
            }

            var interaccion = target.GetAttributeValue<EntityReference>(Esquema.ActualizacionInteraccion);
            if (interaccion == null && esCreate)
            {
                var encontrada = BuscarInteraccion(local, target);
                if (encontrada != null)
                {
                    // La busqueda ya filtro por la cuenta del target: no hace falta volver a leer la interaccion.
                    target[Esquema.ActualizacionInteraccion] = encontrada;
                    target[Esquema.ActualizacionSinVincular] = false;
                    return;
                }
                // F8: un rechazo aca es un dato perdido (el telefono descarta lo rechazado y no reintenta). Se acepta
                // marcada: VincularActualizacionPendiente la cuelga cuando llegue la interaccion; si no, la ve BackOffice.
                target[Esquema.ActualizacionSinVincular] = true;
                local.Trace("sin interaccion: se acepta marcada como sin vincular.");
                return;
            }
            if (interaccion == null)
            {
                throw local.Error("la actualizacion de datos tiene que estar vinculada a una interaccion.");
            }

            var fila = local.Service.Retrieve(Esquema.Interaccion, interaccion.Id, new ColumnSet(Esquema.InteraccionCuentaId));
            var cuentaDeInteraccion = fila == null ? null : fila.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId);
            if (cuentaDeInteraccion == null)
            {
                throw local.Error("la interaccion de la actualizacion de datos no tiene cuenta: no se sabe de que cliente es.");
            }

            var cuenta = target.GetAttributeValue<EntityReference>(Esquema.ActualizacionCuentaId);
            if (cuenta == null)
            {
                if (esCreate)
                {
                    target[Esquema.ActualizacionCuentaId] = new EntityReference(Esquema.Cuenta, cuentaDeInteraccion.Id);
                    local.Trace("{0} completada desde la interaccion.", Esquema.ActualizacionCuentaId);
                }
            }
            else if (cuenta.Id != cuentaDeInteraccion.Id)
            {
                throw local.Error("la interaccion de la actualizacion de datos es de otra cuenta.");
            }
            target[Esquema.ActualizacionSinVincular] = false;
            local.Trace("interaccion con cuenta; ok.");
        }

        /// <summary>
        /// F7: la interaccion del mismo cobrador y cuenta, marcada con actualiza info. Con fecha de captura: la de esa
        /// captura (tolerancia de unos segundos), exactamente una. Sin fecha (el runtime offline la descarta, 1.0.8.8):
        /// la mas vieja de las ultimas horas que todavia no tiene actualizacion de datos (la sincronizacion sube en orden).
        /// Si no hay, o es ambigua, devuelve null (la actualizacion entra marcada como sin vincular, F8).
        /// </summary>
        private static EntityReference BuscarInteraccion(LocalContext local, Entity target)
        {
            var cuenta = target.GetAttributeValue<EntityReference>(Esquema.ActualizacionCuentaId);
            if (cuenta == null)
            {
                local.Trace("sin interaccion y sin {0}; no se puede buscar.", Esquema.ActualizacionCuentaId);
                return null;
            }

            var captura = target.GetAttributeValue<DateTime?>(Esquema.ActualizacionFechaCaptura);
            var q = new QueryExpression(Esquema.Interaccion) { ColumnSet = new ColumnSet(false), NoLock = true };
            q.Criteria.AddCondition(Esquema.InteraccionCuentaId, ConditionOperator.Equal, cuenta.Id);
            q.Criteria.AddCondition(Esquema.CreadoPor, ConditionOperator.Equal, local.Context.UserId);
            q.Criteria.AddCondition(Esquema.InteraccionActualizaInfo, ConditionOperator.Equal, true);
            if (captura.HasValue)
            {
                var utc = captura.Value.Kind == DateTimeKind.Local ? captura.Value.ToUniversalTime() : DateTime.SpecifyKind(captura.Value, DateTimeKind.Utc);
                q.TopCount = 2;
                q.Criteria.AddCondition(Esquema.InteraccionFechaCaptura, ConditionOperator.Between,
                    utc.AddSeconds(-Esquema.ToleranciaCapturaSegundos), utc.AddSeconds(Esquema.ToleranciaCapturaSegundos));
            }
            else
            {
                q.TopCount = 1;
                q.Criteria.AddCondition(Esquema.CreadoEl, ConditionOperator.GreaterEqual, DateTime.UtcNow.AddHours(-Esquema.VentanaSinFechaHoras));
                var ad = q.AddLink(Esquema.ActualizacionDatos, Esquema.InteraccionId, Esquema.ActualizacionInteraccion, JoinOperator.LeftOuter);
                ad.EntityAlias = AliasActualizacion;
                q.Criteria.AddCondition(AliasActualizacion, Esquema.ActualizacionId, ConditionOperator.Null);
                q.AddOrder(Esquema.CreadoEl, OrderType.Ascending);
            }

            var filas = local.Service.RetrieveMultiple(q).Entities;
            if (filas.Count == 0)
            {
                local.Trace("la interaccion todavia no llego al servidor.");
                return null;
            }
            if (filas.Count > 1)
            {
                local.Trace("hay mas de una interaccion de la misma cuenta y captura; no se adivina.");
                return null;
            }
            local.Trace(captura.HasValue ? "interaccion encontrada por cuenta y captura (offline)." : "interaccion encontrada por cuenta: la pendiente mas vieja (offline, sin fecha).");
            return new EntityReference(Esquema.Interaccion, filas[0].Id);
        }

        private const string AliasActualizacion = "ad";
    }
}
