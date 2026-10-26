using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    public class CuentaDesdeInteraccionTests
    {
        private const string Pago = "sanic_cm_tbl_pago";
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string PgCuenta = "sanic_cm_pg_cuentaid";
        private const string PgInteraccion = "sanic_cm_pg_interaccion";
        private const string InCuenta = "sanic_cm_in_cuentaid";
        private const string InNumero = "sanic_cm_in_numerointeraccion";
        private const string PgNumero = "sanic_cm_pg_numerointeraccion";

        private static Entity InteraccionConCuenta(Guid id, Guid? cuentaId, string numero = null)
        {
            var i = new Entity(Interaccion, id);
            if (cuentaId.HasValue) i[InCuenta] = new EntityReference(Cuenta, cuentaId.Value);
            if (numero != null) i[InNumero] = numero;
            return i;
        }

        [Fact]
        public void Completa_la_cuenta_del_pago_desde_la_interaccion()
        {
            var interaccionId = Guid.NewGuid();
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, cuentaId), InCuenta, InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            var er = target.GetAttributeValue<EntityReference>(PgCuenta);
            Assert.NotNull(er);
            Assert.Equal(Cuenta, er.LogicalName);
            Assert.Equal(cuentaId, er.Id);
            Assert.Equal(1, h.RetrievesAlServicio()); // una sola lectura, solo de las columnas que faltan
        }

        [Fact]
        public void Una_cuenta_nula_en_el_Target_tambien_se_completa()
        {
            var interaccionId = Guid.NewGuid();
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            target[PgCuenta] = null;
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, cuentaId), InCuenta, InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.Equal(cuentaId, target.GetAttributeValue<EntityReference>(PgCuenta).Id);
        }

        [Fact]
        public void No_hace_nada_si_el_pago_ya_trae_cuenta_y_numero()
        {
            var cuentaExistente = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, Guid.NewGuid());
            target[PgCuenta] = new EntityReference(Cuenta, cuentaExistente);
            target[PgNumero] = "IN-1001";
            var h = new Harness("Create", Pago).ConTarget(target);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.Equal(cuentaExistente, target.GetAttributeValue<EntityReference>(PgCuenta).Id);
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Rechaza_un_pago_sin_interaccion()
        {
            // El servidor no exige pg_interaccion aunque el formulario la pida.
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgCuenta] = new EntityReference(Cuenta, Guid.NewGuid());
            target[PgNumero] = "IN-1001";
            var h = new Harness("Create", Pago).ConTarget(target);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new CuentaDesdeInteraccion()));
            Assert.Contains("interaccion", ex.Message);
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Rechaza_un_pago_con_la_interaccion_nula()
        {
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = null;
            var h = new Harness("Create", Pago).ConTarget(target);

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new CuentaDesdeInteraccion()));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Si_la_interaccion_no_tiene_cuenta_deja_el_pago_sin_cuenta()
        {
            var interaccionId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, null), InCuenta, InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.False(target.Contains(PgCuenta));
        }

        [Fact]
        public void Usa_el_usuario_que_llama_y_no_SYSTEM()
        {
            var interaccionId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, Guid.NewGuid()), InCuenta, InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            h.Factory.Verify(f => f.CreateOrganizationService(Harness.UsuarioLlamador), Moq.Times.Once);
        }

        // La app (sobre todo offline) manda la cuenta pero no el N° de interacción, que lo asigna el servidor.
        [Fact]
        public void Con_cuenta_y_sin_numero_completa_solo_el_numero()
        {
            var interaccionId = Guid.NewGuid();
            var cuentaExistente = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            target[PgCuenta] = new EntityReference(Cuenta, cuentaExistente);
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, null, "IN-1042"), InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.Equal("IN-1042", target.GetAttributeValue<string>(PgNumero));
            Assert.Equal(cuentaExistente, target.GetAttributeValue<EntityReference>(PgCuenta).Id);
            Assert.Equal(1, h.RetrievesAlServicio());
        }

        [Fact]
        public void Un_numero_en_blanco_cuenta_como_faltante()
        {
            var interaccionId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            target[PgCuenta] = new EntityReference(Cuenta, Guid.NewGuid());
            target[PgNumero] = "  ";
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, null, "IN-1043"), InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.Equal("IN-1043", target.GetAttributeValue<string>(PgNumero));
        }

        [Fact]
        public void Sin_cuenta_ni_numero_completa_ambos_con_una_lectura()
        {
            var interaccionId = Guid.NewGuid();
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, cuentaId, "IN-1044"), InCuenta, InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.Equal(cuentaId, target.GetAttributeValue<EntityReference>(PgCuenta).Id);
            Assert.Equal("IN-1044", target.GetAttributeValue<string>(PgNumero));
            Assert.Equal(1, h.RetrievesAlServicio());
        }

        [Fact]
        public void Si_la_interaccion_aun_no_tiene_numero_no_lo_escribe()
        {
            var interaccionId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            target[PgCuenta] = new EntityReference(Cuenta, Guid.NewGuid());
            var h = new Harness("Create", Pago).ConTarget(target)
                .RetrieveDevuelve(InteraccionConCuenta(interaccionId, null), InNumero);

            h.Ejecutar(new CuentaDesdeInteraccion());

            Assert.False(target.Contains(PgNumero));
        }

        [Theory]
        [InlineData("Create", "sanic_cm_tbl_pago", 10)]
        [InlineData("Create", "sanic_cm_tbl_pago", 40)]
        [InlineData("Update", "sanic_cm_tbl_pago", 20)]
        [InlineData("Create", "sanic_cm_tbl_cuenta", 20)]
        public void Falla_cerrado_si_se_registra_fuera_de_Create_PreOperation_de_pago(string mensaje, string entidad, int stage)
        {
            var target = new Entity(entidad, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, Guid.NewGuid());
            var h = new Harness(mensaje, entidad, stage).ConTarget(target);

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new CuentaDesdeInteraccion()));
        }

        [Fact]
        public void Un_error_inesperado_se_envuelve_sin_filtrar_su_mensaje()
        {
            var interaccionId = Guid.NewGuid();
            var target = new Entity(Pago, Guid.NewGuid());
            target[PgInteraccion] = new EntityReference(Interaccion, interaccionId);
            var h = new Harness("Create", Pago).ConTarget(target);
            h.Service.Setup(s => s.Retrieve(Interaccion, interaccionId, Moq.It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()))
                .Throws(new InvalidOperationException("detalle con 4111111111111111"));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new CuentaDesdeInteraccion()));

            Assert.DoesNotContain("4111", ex.Message);
            Assert.DoesNotContain("4111", h.TodoElTexto());
        }
    }
}
