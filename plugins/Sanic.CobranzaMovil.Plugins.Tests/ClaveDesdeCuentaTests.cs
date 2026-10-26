using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    public class ClaveDesdeCuentaTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string ColCuentaId = "sanic_cm_as_cuentaid";
        private const string ColClaveAs = "sanic_cm_as_clavepan";
        private const string ColClaveCt = "sanic_cm_ct_clavepan";

        // HMAC-SHA256 hex de ejemplo (clave de prueba, nunca la real)
        private const string ClaveA = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        private const string ClaveB = "60303ae22b998861bce3b28f33eec1be758a213c86c93c076dbe9f558c11c752";

        private static Entity CuentaConClave(Guid id, string clave)
        {
            var c = new Entity(Cuenta, id);
            if (clave != null) c[ColClaveCt] = clave;
            return c;
        }

        private static Harness Preparar(string mensaje, Entity target, Entity cuenta)
        {
            var h = new Harness(mensaje, Asignacion).ConTarget(target);
            if (cuenta != null) h.RetrieveDevuelve(cuenta, ColClaveCt);
            return h;
        }

        // ---------- completar ----------

        [Theory]
        [InlineData("Create")]
        [InlineData("Update")]
        public void Si_el_Target_no_trae_clave_la_copia_de_la_cuenta(string mensaje)
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = Preparar(mensaje, target, CuentaConClave(cuentaId, ClaveA));

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
            Assert.Equal(1, h.RetrievesAlServicio());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Una_clave_vacia_o_nula_en_el_Target_tambien_se_completa(string vacio)
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            target[ColClaveAs] = vacio;
            var h = Preparar("Create", target, CuentaConClave(cuentaId, ClaveA));

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
        }

        [Fact]
        public void Lee_una_sola_columna_de_la_cuenta_con_el_usuario_que_llama()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = Preparar("Create", target, CuentaConClave(cuentaId, ClaveA));

            h.Ejecutar(new ClaveDesdeCuenta());

            // RetrieveDevuelve solo responde si el ColumnSet es EXACTAMENTE {ct_clavepan} y no AllColumns:
            // si el plugin pidiera otra cosa, la clave no se habria completado.
            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
            Assert.Equal(1, h.RetrievesAlServicio());
            h.Factory.Verify(f => f.CreateOrganizationService(Harness.UsuarioLlamador), Moq.Times.Once);
        }

        // ---------- sin cambios ----------

        [Fact]
        public void Si_la_clave_del_Target_coincide_no_cambia_nada()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            target[ColClaveAs] = ClaveA;
            var h = Preparar("Create", target, CuentaConClave(cuentaId, ClaveA));

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
        }

        [Fact]
        public void Si_la_cuenta_se_borra_del_Target_deja_todo_como_esta()
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = null;
            var h = Preparar("Update", target, null);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.False(target.Contains(ColClaveAs));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Update_sin_cuenta_ni_clave_en_el_Target_no_hace_nada()
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target["sanic_cm_as_orden"] = 3;
            var h = Preparar("Update", target, null);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.False(target.Contains(ColClaveAs));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Si_la_cuenta_todavia_no_tiene_clave_no_bloquea_ni_inventa()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = Preparar("Create", target, CuentaConClave(cuentaId, null));

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.False(target.Contains(ColClaveAs));
        }

        // ---------- rechazos ----------

        // ---------- cuenta referenciada por clave alternativa (upsert de la carga) ----------
        // La doc no garantiza que la referencia ya traiga el Id en PreOperation: con Id vacío se usa la clave de la referencia.

        private static EntityReference CuentaPorClave(string clave) =>
            new EntityReference(Cuenta, new KeyAttributeCollection { { ColClaveCt, clave } });

        [Theory]
        [InlineData("Create")]
        [InlineData("Update")]
        public void Cuenta_por_clave_alternativa_sin_Id_completa_la_clave_sin_leer_la_cuenta(string mensaje)
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = CuentaPorClave(ClaveA);
            var h = Preparar(mensaje, target, null);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Cuenta_por_clave_alternativa_sin_Id_con_la_misma_clave_en_el_Target_pasa()
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = CuentaPorClave(ClaveA);
            target[ColClaveAs] = ClaveA;
            var h = Preparar("Create", target, null);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Cuenta_por_clave_alternativa_sin_Id_con_otra_clave_en_el_Target_se_rechaza()
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = CuentaPorClave(ClaveA);
            target[ColClaveAs] = ClaveB;
            var h = Preparar("Create", target, null);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));

            Assert.DoesNotContain(ClaveA, ex.Message);
            Assert.DoesNotContain(ClaveB, ex.Message);
        }

        [Fact]
        public void Cuenta_sin_Id_y_sin_la_clave_HMAC_en_la_referencia_no_lee_ni_cambia_nada()
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, new KeyAttributeCollection { { "sanic_cm_ct_numerodecuenta", "x" } });
            var h = Preparar("Create", target, null);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.False(target.Contains(ColClaveAs));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Theory]
        [InlineData("Create")]
        [InlineData("Update")]
        public void Rechaza_si_la_clave_del_Target_no_coincide_con_la_de_la_cuenta(string mensaje)
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            target[ColClaveAs] = ClaveB;
            var h = Preparar(mensaje, target, CuentaConClave(cuentaId, ClaveA));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));

            Assert.Contains(ColClaveAs, ex.Message);
            Assert.Contains(ColCuentaId, ex.Message);
            Assert.DoesNotContain(ClaveA, ex.Message);
            Assert.DoesNotContain(ClaveB, ex.Message);
            Assert.DoesNotContain(ClaveA, h.TodoElTexto());
            Assert.DoesNotContain(ClaveB, h.TodoElTexto());
            Assert.Equal(ClaveB, target.GetAttributeValue<string>(ColClaveAs)); // el Target no se toca
        }

        [Fact]
        public void Update_que_solo_cambia_la_clave_se_valida_contra_la_cuenta_de_la_PreImage()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColClaveAs] = ClaveB;
            var pre = new Entity(Asignacion, target.Id);
            pre[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = Preparar("Update", target, CuentaConClave(cuentaId, ClaveA)).ConPreImage(pre);

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));
        }

        [Fact]
        public void Update_que_solo_pone_la_clave_correcta_con_PreImage_pasa()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColClaveAs] = ClaveA;
            var pre = new Entity(Asignacion, target.Id);
            pre[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = Preparar("Update", target, CuentaConClave(cuentaId, ClaveA)).ConPreImage(pre);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(ClaveA, target.GetAttributeValue<string>(ColClaveAs));
        }

        [Fact]
        public void Update_que_solo_cambia_la_clave_sin_PreImage_no_puede_validar_y_no_hace_nada()
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColClaveAs] = ClaveB;
            var h = Preparar("Update", target, null);

            h.Ejecutar(new ClaveDesdeCuenta());

            Assert.Equal(0, h.RetrievesAlServicio());
        }

        // ---------- contexto ----------

        [Theory]
        [InlineData(10)]
        [InlineData(40)]
        public void Exige_PreOperation_para_poder_modificar_el_Target(int stage)
        {
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, Guid.NewGuid());
            var h = new Harness("Create", Asignacion, stage).ConTarget(target);

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));
        }

        [Fact]
        public void Falla_cerrado_si_se_registra_en_otra_entidad_o_mensaje()
        {
            var t1 = new Entity("account", Guid.NewGuid());
            Assert.Throws<InvalidPluginExecutionException>(() =>
                new Harness("Create", "account").ConTarget(t1).Ejecutar(new ClaveDesdeCuenta()));

            var t2 = new Entity(Asignacion, Guid.NewGuid());
            Assert.Throws<InvalidPluginExecutionException>(() =>
                new Harness("Delete", Asignacion).ConTarget(t2).Ejecutar(new ClaveDesdeCuenta()));
        }

        [Fact]
        public void Un_error_inesperado_del_servicio_se_envuelve_sin_filtrar_su_mensaje()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = new Harness("Create", Asignacion).ConTarget(target);
            h.Service.Setup(s => s.Retrieve(Cuenta, cuentaId, Moq.It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()))
                .Throws(new InvalidOperationException("fallo con valor 4111111111111111"));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));

            Assert.DoesNotContain("4111", ex.Message);
            Assert.DoesNotContain("4111", h.TodoElTexto());
        }

        [Fact]
        public void Una_InvalidPluginExecutionException_del_servicio_se_propaga_tal_cual()
        {
            var cuentaId = Guid.NewGuid();
            var target = new Entity(Asignacion, Guid.NewGuid());
            target[ColCuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = new Harness("Create", Asignacion).ConTarget(target);
            var original = new InvalidPluginExecutionException("sin permiso");
            h.Service.Setup(s => s.Retrieve(Cuenta, cuentaId, Moq.It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()))
                .Throws(original);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));

            Assert.Same(original, ex);
        }
    }
}
