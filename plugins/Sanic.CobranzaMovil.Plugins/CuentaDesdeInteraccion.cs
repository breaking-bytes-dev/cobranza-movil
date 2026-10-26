using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// En el Create de un pago exige pg_interaccion y completa lo que falte de la interaccion:
    /// - pg_cuentaid con in_cuentaid: offline solo se admite un nivel de lookup (pago -&gt; interaccion -&gt; cuenta no funciona).
    /// - pg_numerointeraccion con in_numerointeraccion: lo asigna el servidor, offline la app no lo conoce, y el
    ///   backoffice busca los pagos de una interaccion por ese numero.
    /// Una sola lectura de la interaccion, solo de las columnas que faltan; si no falta nada, no lee.
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

            // Todo pago cuelga de una interaccion; el requerido de la columna no lo exige el servidor.
            var interaccion = target.GetAttributeValue<EntityReference>(Esquema.PagoInteraccion);
            if (interaccion == null)
            {
                throw local.Error("el pago tiene que estar vinculado a una interaccion.");
            }

            var faltaCuenta = target.GetAttributeValue<EntityReference>(Esquema.PagoCuentaId) == null;
            var faltaNumero = string.IsNullOrWhiteSpace(target.GetAttributeValue<string>(Esquema.PagoNumeroInteraccion));
            if (!faltaCuenta && !faltaNumero)
            {
                local.Trace("el pago ya trae cuenta y numero de interaccion; sin cambios.");
                return;
            }

            var columnas = new List<string>();
            if (faltaCuenta) columnas.Add(Esquema.InteraccionCuentaId);
            if (faltaNumero) columnas.Add(Esquema.InteraccionNumero);
            var fila = local.Service.Retrieve(Esquema.Interaccion, interaccion.Id, new ColumnSet(columnas.ToArray()));
            if (fila == null) return;

            if (faltaCuenta)
            {
                var cuenta = fila.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId);
                if (cuenta != null)
                {
                    target[Esquema.PagoCuentaId] = new EntityReference(cuenta.LogicalName, cuenta.Id);
                    local.Trace("{0} completada desde la interaccion.", Esquema.PagoCuentaId);
                }
                else local.Trace("la interaccion no tiene {0}.", Esquema.InteraccionCuentaId);
            }
            if (faltaNumero)
            {
                var numero = fila.GetAttributeValue<string>(Esquema.InteraccionNumero);
                if (!string.IsNullOrWhiteSpace(numero))
                {
                    target[Esquema.PagoNumeroInteraccion] = numero;
                    local.Trace("{0} completado desde la interaccion.", Esquema.PagoNumeroInteraccion);
                }
                else local.Trace("la interaccion aun no tiene {0}.", Esquema.InteraccionNumero);
            }
        }
    }
}
