using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// APP-24: una Actualizacion de datos tiene que colgar de una interaccion que tenga cuenta; por ese camino
    /// (actualizacion -> interaccion -> cuenta -> CIF) el backoffice sabe de que cliente es.
    /// </summary>
    public class ActualizacionConInteraccionTests
    {
        private const string Actualizacion = "sanic_cm_tbl_actualizaciondatos";
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string AdInteraccion = "sanic_cm_ad_interaccionid";
        private const string InCuenta = "sanic_cm_in_cuentaid";
        private const string AdTelefono = "sanic_cm_ad_telefonocelular";

        private static Entity InteraccionCon(Guid id, bool conCuenta)
        {
            var i = new Entity(Interaccion, id);
            if (conCuenta) i[InCuenta] = new EntityReference(Cuenta, Guid.NewGuid());
            return i;
        }

        [Fact]
        public void Crear_sin_interaccion_se_rechaza()
        {
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdTelefono] = "8888-1234";
            var h = new Harness("Create", Actualizacion).ConTarget(t);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Contains("interaccion", ex.Message);
            Assert.DoesNotContain("8888", ex.Message);
        }

        [Fact]
        public void Crear_con_interaccion_que_tiene_cuenta_pasa_con_una_lectura()
        {
            var interaccion = Guid.NewGuid();
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdInteraccion] = new EntityReference(Interaccion, interaccion);
            var h = new Harness("Create", Actualizacion).ConTarget(t)
                .RetrieveDevuelve(InteraccionCon(interaccion, conCuenta: true), InCuenta);

            h.Ejecutar(new ActualizacionConInteraccion());

            Assert.Equal(1, h.RetrievesAlServicio());
        }

        [Fact]
        public void Crear_con_interaccion_sin_cuenta_se_rechaza()
        {
            var interaccion = Guid.NewGuid();
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdInteraccion] = new EntityReference(Interaccion, interaccion);
            var h = new Harness("Create", Actualizacion).ConTarget(t)
                .RetrieveDevuelve(InteraccionCon(interaccion, conCuenta: false), InCuenta);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Contains("cuenta", ex.Message);
        }

        [Fact]
        public void Quitar_la_interaccion_se_rechaza()
        {
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdInteraccion] = null;
            var h = new Harness("Update", Actualizacion).ConTarget(t);
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
        }

        [Fact]
        public void Cambiar_a_otra_interaccion_con_cuenta_pasa()
        {
            var interaccion = Guid.NewGuid();
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdInteraccion] = new EntityReference(Interaccion, interaccion);
            var h = new Harness("Update", Actualizacion).ConTarget(t)
                .RetrieveDevuelve(InteraccionCon(interaccion, conCuenta: true), InCuenta);

            h.Ejecutar(new ActualizacionConInteraccion());
        }

        [Fact]
        public void Un_Update_que_no_toca_la_interaccion_no_lee_nada()
        {
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdTelefono] = "8888-1234";
            var h = new Harness("Update", Actualizacion).ConTarget(t);

            h.Ejecutar(new ActualizacionConInteraccion());

            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Registrado_en_otra_tabla_falla()
        {
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(new Entity("sanic_cm_tbl_pago", Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
        }
    }
}
