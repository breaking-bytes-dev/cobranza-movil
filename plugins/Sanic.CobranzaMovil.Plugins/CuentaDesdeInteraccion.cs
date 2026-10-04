using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// En el Create de un pago, si pg_cuentaid viene vacio y pg_interaccion esta informado, completa
    /// pg_cuentaid con in_cuentaid de la interaccion. Hace falta porque el modo offline solo admite un
    /// nivel de lookup (pago -> interaccion -> cuenta no funciona).
    ///
    /// Registro: PreOperation sincrono, solo Create de sanic_cm_tbl_pago.
    /// </summary>
    public sealed class CuentaDesdeInteraccion : PluginBase
    {
        protected override string Nombre
        {
            get { return "CuentaDesdeInteraccion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Pago, Esquema.EtapaPreOperation, Esquema.MensajeCreate);

            var target = local.ObtenerTarget();

            if (target.GetAttributeValue<EntityReference>(Esquema.PagoCuentaId) != null)
            {
                local.Trace("el pago ya trae {0}; sin cambios.", Esquema.PagoCuentaId);
                return;
            }

            var interaccion = target.GetAttributeValue<EntityReference>(Esquema.PagoInteraccion);
            if (interaccion == null)
            {
                local.Trace("el pago no trae {0}; sin cambios.", Esquema.PagoInteraccion);
                return;
            }

            // Una sola lectura, de una sola columna.
            var fila = local.Service.Retrieve(Esquema.Interaccion, interaccion.Id, new ColumnSet(Esquema.InteraccionCuentaId));
            var cuenta = fila == null ? null : fila.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId);
            if (cuenta == null)
            {
                local.Trace("la interaccion no tiene {0}; sin cambios.", Esquema.InteraccionCuentaId);
                return;
            }

            target[Esquema.PagoCuentaId] = new EntityReference(cuenta.LogicalName, cuenta.Id);
            local.Trace("{0} completada desde la interaccion.", Esquema.PagoCuentaId);
        }
    }
}
