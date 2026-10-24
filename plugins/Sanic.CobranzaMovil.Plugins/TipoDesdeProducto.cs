using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// O3: la carga (Smelter, sin tocar su plantilla) resuelve el producto de la cuenta por CodigoProducto en el lookup
    /// ct_productoid, pero no llena ct_tipoproducto, que es lo que muestran Cliente, Detalle, Interaccion, Pagos e Inicio
    /// del cobrador ("Tarjeta Oro · •••• 1234"). Resultado: el tipo salia vacio en todas las cuentas. Esto copia el tipo del
    /// producto a la cuenta, en la misma operacion (sin un Update extra) y offline sin agregar tablas al perfil.
    ///
    /// Cada carga reescribe ct_productoid con el mismo valor: si el producto no cambio y la cuenta ya tiene tipo, no lee
    /// nada (sin esto serian ~6.000 lecturas de producto por carga). Si se cambia el tipo de un producto, las cuentas lo
    /// toman cuando cambie su producto; no se propaga solo.
    /// Lee el producto con el servicio del SISTEMA (la carga corre sin roles de CM, el alta no). Nunca traza valores.
    /// Registro: PreOperation sincrono. Create de sanic_cm_tbl_cuenta, y Update filtrado por ct_productoid con imagen previa
    /// "PreImage" de ct_productoid y ct_tipoproducto.
    /// </summary>
    public sealed class TipoDesdeProducto : PluginBase
    {
        protected override string Nombre
        {
            get { return "TipoDesdeProducto"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Cuenta, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            if (!target.Contains(Esquema.CuentaProductoId))
            {
                local.Trace("la operacion no trae el producto; sin cambios.");
                return;
            }

            var producto = target.GetAttributeValue<EntityReference>(Esquema.CuentaProductoId);
            if (producto == null)
            {
                // Create sin producto: no hay nada que copiar. Update que lo quita: el tipo viejo ya no corresponde.
                if (local.EsMensaje(Esquema.MensajeUpdate)) target[Esquema.CuentaTipoProducto] = null;
                local.Trace("cuenta sin producto; tipo vacio.");
                return;
            }

            if (local.EsMensaje(Esquema.MensajeUpdate))
            {
                var previa = local.ObtenerImagenPrevia();
                var anterior = previa.GetAttributeValue<EntityReference>(Esquema.CuentaProductoId);
                var tipoActual = previa.GetAttributeValue<string>(Esquema.CuentaTipoProducto);
                if (anterior != null && anterior.Id == producto.Id && !string.IsNullOrWhiteSpace(tipoActual))
                {
                    local.Trace("mismo producto y la cuenta ya tiene tipo; sin cambios.");
                    return;
                }
            }

            var fila = local.ServicioSistema.Retrieve(Esquema.Producto, producto.Id, new ColumnSet(Esquema.ProductoTipo));
            var tipo = fila == null ? null : fila.GetAttributeValue<string>(Esquema.ProductoTipo);
            target[Esquema.CuentaTipoProducto] = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim();
            local.Trace("O3: tipo de producto copiado a la cuenta ({0}).", string.IsNullOrWhiteSpace(tipo) ? "vacio" : "con valor");
        }
    }
}
