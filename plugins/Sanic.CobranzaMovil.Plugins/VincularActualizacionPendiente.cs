using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// F8: una actualizacion de datos que llego ANTES que su interaccion entra marcada como sin vincular
    /// (ActualizacionConInteraccion ya no la rechaza: el telefono descarta lo rechazado y no lo reintenta).
    /// Cuando llega una interaccion con actualiza info, esta la cuelga: la actualizacion sin vincular mas vieja de las
    /// ultimas horas, de la misma cuenta y del mismo cobrador. Le pone la interaccion y le quita la marca; el Update
    /// pasa por ActualizacionConInteraccion, que valida la cuenta.
    ///
    /// Corre con el servicio del SISTEMA: el rol del cobrador no tiene por que poder modificar actualizaciones.
    /// Registro: PostOperation ASINCRONO, Create de sanic_cm_tbl_interaccion. Asincrono a proposito: si fallara
    /// sincrono, se perderia la interaccion del telefono con ella. Nunca traza valores.
    /// </summary>
    public sealed class VincularActualizacionPendiente : PluginBase
    {
        protected override string Nombre
        {
            get { return "VincularActualizacionPendiente"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Interaccion, Esquema.EtapaPostOperation, Esquema.MensajeCreate);

            var target = local.ObtenerTarget();
            if (!target.GetAttributeValue<bool>(Esquema.InteraccionActualizaInfo))
            {
                local.Trace("la interaccion no actualiza datos; sin cambios.");
                return;
            }
            var cuenta = target.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId);
            if (cuenta == null)
            {
                local.Trace("la interaccion no tiene cuenta; sin cambios.");
                return;
            }
            var interaccionId = local.Context.PrimaryEntityId != Guid.Empty ? local.Context.PrimaryEntityId : target.Id;

            var q = new QueryExpression(Esquema.ActualizacionDatos) { ColumnSet = new ColumnSet(false), TopCount = 1, NoLock = true };
            q.Criteria.AddCondition(Esquema.ActualizacionSinVincular, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.ActualizacionInteraccion, ConditionOperator.Null);
            q.Criteria.AddCondition(Esquema.ActualizacionCuentaId, ConditionOperator.Equal, cuenta.Id);
            q.Criteria.AddCondition(Esquema.CreadoPor, ConditionOperator.Equal, local.Context.UserId);
            q.Criteria.AddCondition(Esquema.CreadoEl, ConditionOperator.GreaterEqual, DateTime.UtcNow.AddHours(-Esquema.VentanaSinFechaHoras));
            q.AddOrder(Esquema.CreadoEl, OrderType.Ascending);

            var filas = local.ServicioSistema.RetrieveMultiple(q).Entities;
            if (filas.Count == 0)
            {
                local.Trace("no hay actualizaciones sin vincular de esta cuenta; sin cambios.");
                return;
            }

            var cambio = new Entity(Esquema.ActualizacionDatos, filas[0].Id);
            cambio[Esquema.ActualizacionInteraccion] = new EntityReference(Esquema.Interaccion, interaccionId);
            cambio[Esquema.ActualizacionSinVincular] = false;
            local.ServicioSistema.Update(cambio);
            local.Trace("actualizacion sin vincular colgada de la interaccion que acaba de llegar.");
        }
    }
}
