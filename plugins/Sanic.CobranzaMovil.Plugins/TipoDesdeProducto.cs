using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Copia el tipo del producto (ct_productoid) a ct_tipoproducto de la cuenta en la misma operacion, porque la carga
    /// solo llena el lookup y las pantallas del localizador muestran el tipo sin consultar la tabla de productos.
    /// Si el producto no cambio y la cuenta ya tiene tipo no lee nada (la carga reescribe el lookup en cada fila).
    /// Cambiar el tipo de un producto no se propaga a sus cuentas. Lee con el servicio del sistema; nunca traza valores.
    /// Registro: PreOperation sincrono, Create y Update de sanic_cm_tbl_cuenta (filtrado por ct_productoid) con
    /// imagen previa "PreImage" de ct_productoid y ct_tipoproducto.
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
            local.Trace("tipo de producto copiado a la cuenta ({0}).", string.IsNullOrWhiteSpace(tipo) ? "vacio" : "con valor");
        }
    }
}
