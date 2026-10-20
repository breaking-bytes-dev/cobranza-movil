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
                    return;
                }
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
            local.Trace("interaccion con cuenta; ok.");
        }

        /// <summary>
        /// F7: la interaccion del mismo cobrador, cuenta y fecha de captura (tolerancia de unos segundos), marcada con
        /// actualiza info. Exactamente una; si no hay, todavia no llego (se reintenta); si hay mas, no se adivina.
        /// </summary>
        private static EntityReference BuscarInteraccion(LocalContext local, Entity target)
        {
            var cuenta = target.GetAttributeValue<EntityReference>(Esquema.ActualizacionCuentaId);
            var captura = target.GetAttributeValue<DateTime?>(Esquema.ActualizacionFechaCaptura);
            if (cuenta == null || !captura.HasValue)
            {
                local.Trace("sin interaccion y sin {0}/{1}; no se puede buscar.", Esquema.ActualizacionCuentaId, Esquema.ActualizacionFechaCaptura);
                return null;
            }

            var utc = captura.Value.Kind == DateTimeKind.Local ? captura.Value.ToUniversalTime() : DateTime.SpecifyKind(captura.Value, DateTimeKind.Utc);
            var q = new QueryExpression(Esquema.Interaccion) { ColumnSet = new ColumnSet(false), TopCount = 2, NoLock = true };
            q.Criteria.AddCondition(Esquema.InteraccionCuentaId, ConditionOperator.Equal, cuenta.Id);
            q.Criteria.AddCondition(Esquema.CreadoPor, ConditionOperator.Equal, local.Context.UserId);
            q.Criteria.AddCondition(Esquema.InteraccionActualizaInfo, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.InteraccionFechaCaptura, ConditionOperator.Between,
                utc.AddSeconds(-Esquema.ToleranciaCapturaSegundos), utc.AddSeconds(Esquema.ToleranciaCapturaSegundos));

            var filas = local.Service.RetrieveMultiple(q).Entities;
            if (filas.Count == 0)
            {
                throw local.Error("la interaccion de esta actualizacion de datos todavia no llego al servidor; se reintenta en la proxima sincronizacion.");
            }
            if (filas.Count > 1)
            {
                throw local.Error("hay mas de una interaccion de la misma cuenta y captura; no se puede saber de cual es la actualizacion de datos.");
            }
            local.Trace("interaccion encontrada por cuenta y captura (offline).");
            return new EntityReference(Esquema.Interaccion, filas[0].Id);
        }
    }
}
