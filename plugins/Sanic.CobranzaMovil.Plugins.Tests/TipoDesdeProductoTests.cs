using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// O3: la carga resuelve el producto de la cuenta por CodigoProducto (lookup ct_productoid) pero no llena
    /// ct_tipoproducto, que es lo que muestra la app ("Tarjeta Oro · •••• 1234"). La cuenta toma el tipo de su producto.
    /// </summary>
    public class TipoDesdeProductoTests
    {
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Producto = "sanic_cm_tbl_producto";
        private const string ProductoId = "sanic_cm_ct_productoid";
        private const string TipoProducto = "sanic_cm_ct_tipoproducto";
        private const string TipoDelProducto = "sanic_cm_pr_tipo";

        private static Entity ElProducto(Guid id, string tipo = "Tarjeta Oro")
        {
            var p = new Entity(Producto, id);
            if (tipo != null) p[TipoDelProducto] = tipo;
            return p;
        }

        private static Entity LaCuenta(Guid? producto)
        {
            var c = new Entity(Cuenta, Guid.NewGuid());
            c[ProductoId] = producto.HasValue ? new EntityReference(Producto, producto.Value) : null;
            return c;
        }

        private static Harness Crear(Entity target, Entity producto)
        {
            var h = new Harness("Create", Cuenta).ConTarget(target);
            if (producto != null) h.RetrieveDevuelve(producto, TipoDelProducto);
            h.Ejecutar(new TipoDesdeProducto());
            return h;
        }

        private static Harness Actualizar(Entity target, Entity previa, Entity producto)
        {
            var h = new Harness("Update", Cuenta).ConTarget(target).ConPreImage(previa);
            if (producto != null) h.RetrieveDevuelve(producto, TipoDelProducto);
            h.Ejecutar(new TipoDesdeProducto());
            return h;
        }

        private static Entity Previa(Guid cuenta, Guid? producto, string tipo)
        {
            var p = new Entity(Cuenta, cuenta);
            if (producto.HasValue) p[ProductoId] = new EntityReference(Producto, producto.Value);
            if (tipo != null) p[TipoProducto] = tipo;
            return p;
        }

        [Fact]
        public void La_cuenta_nueva_toma_el_tipo_de_su_producto()
        {
            var prod = Guid.NewGuid();
            var h = Crear(LaCuenta(prod), ElProducto(prod));
            Assert.Equal("Tarjeta Oro", h.Target.GetAttributeValue<string>(TipoProducto));
        }

        [Fact]
        public void Nunca_traza_el_tipo()
        {
            var prod = Guid.NewGuid();
            var h = Crear(LaCuenta(prod), ElProducto(prod));
            Assert.DoesNotContain("Tarjeta Oro", h.TodoElTexto());
        }

        [Fact]
        public void Sin_producto_no_consulta_ni_escribe()
        {
            var t = new Entity(Cuenta, Guid.NewGuid());
            var h = Crear(t, null);
            Assert.Equal(0, h.RetrievesAlServicio());
            Assert.False(h.Target.Contains(TipoProducto));
        }

        [Fact]
        public void Un_producto_sin_tipo_deja_el_tipo_vacio()
        {
            var prod = Guid.NewGuid();
            var h = Crear(LaCuenta(prod), ElProducto(prod, tipo: null));
            Assert.True(h.Target.Contains(TipoProducto));
            Assert.Null(h.Target.GetAttributeValue<string>(TipoProducto));
        }

        [Fact]
        public void Si_cambia_el_producto_toma_el_tipo_nuevo()
        {
            var viejo = Guid.NewGuid();
            var nuevo = Guid.NewGuid();
            var t = LaCuenta(nuevo);
            var h = Actualizar(t, Previa(t.Id, viejo, "Tarjeta Clásica"), ElProducto(nuevo, "Préstamo Personal"));
            Assert.Equal("Préstamo Personal", h.Target.GetAttributeValue<string>(TipoProducto));
        }

        [Fact]
        public void La_carga_del_dia_con_el_mismo_producto_y_tipo_no_consulta()
        {
            // Cada carga reescribe ct_productoid con el mismo valor: sin esto serian ~6.000 lecturas de producto por carga.
            var prod = Guid.NewGuid();
            var t = LaCuenta(prod);
            var h = Actualizar(t, Previa(t.Id, prod, "Tarjeta Oro"), null);
            Assert.Equal(0, h.RetrievesAlServicio());
            Assert.False(h.Target.Contains(TipoProducto));
        }

        [Fact]
        public void Con_el_mismo_producto_pero_sin_tipo_lo_completa()
        {
            // Las cuentas que ya existian antes de este plugin se completan con la carga siguiente.
            var prod = Guid.NewGuid();
            var t = LaCuenta(prod);
            var h = Actualizar(t, Previa(t.Id, prod, null), ElProducto(prod));
            Assert.Equal("Tarjeta Oro", h.Target.GetAttributeValue<string>(TipoProducto));
        }

        [Fact]
        public void Si_le_quitan_el_producto_borra_el_tipo()
        {
            var t = LaCuenta(null);
            var h = Actualizar(t, Previa(t.Id, Guid.NewGuid(), "Tarjeta Oro"), null);
            Assert.Equal(0, h.RetrievesAlServicio());
            Assert.True(h.Target.Contains(TipoProducto));
            Assert.Null(h.Target.GetAttributeValue<string>(TipoProducto));
        }

        [Fact]
        public void Un_update_sin_el_producto_no_hace_nada()
        {
            var t = new Entity(Cuenta, Guid.NewGuid());
            t["sanic_cm_ct_tramo"] = 3;
            var h = Actualizar(t, Previa(t.Id, Guid.NewGuid(), null), null);
            Assert.Equal(0, h.RetrievesAlServicio());
            Assert.False(h.Target.Contains(TipoProducto));
        }
    }
}
