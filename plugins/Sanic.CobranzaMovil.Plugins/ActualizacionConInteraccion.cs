using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Una Actualizacion de datos debe colgar de una interaccion con cuenta: por ese camino
    /// (actualizacion -&gt; interaccion -&gt; cuenta -&gt; CIF) el backoffice sabe de que cliente es y arma el "antes".
    ///
    /// Offline, el lookup a la interaccion recien creada llega vacio: la app manda la cuenta y la misma fecha de
    /// captura de la interaccion, y el servidor busca la del mismo localizador para vincularla. Si no la encuentra,
    /// la acepta marcada como sin vincular (rechazar perderia el dato). Con la interaccion, completa la cuenta si
    /// falta y rechaza si no coincide.
    ///
    /// Lee la interaccion con los permisos del llamador. Nunca cita valores.
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
            // Traza los NOMBRES de columnas recibidas (nunca valores): offline el runtime descarta columnas en silencio.
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
                // Rechazar aca perderia el dato (el telefono descarta lo rechazado). Se acepta marcada:
                // VincularActualizacionPendiente la vincula cuando llegue la interaccion; si no, la ve el backoffice.
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
        /// La interaccion del mismo localizador y cuenta, marcada con actualiza info. Con fecha de captura: exactamente
        /// una de esa captura (tolerancia de unos segundos). Sin fecha (el runtime offline puede descartarla): la mas
        /// vieja de las ultimas horas sin actualizacion de datos, porque la sincronizacion sube en orden.
        /// Si no hay, o es ambigua, devuelve null.
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
