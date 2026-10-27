using System;
using Microsoft.Xrm.Sdk;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    public class PanGuardTests
    {
        // Columnas donde un valor con 5+ digitos es un PAN en claro (el valor valido es el enmascarado ****1234).
        public static readonly object[][] ColumnasPan =
        {
            new object[] { "sanic_cm_tbl_cuenta", "sanic_cm_ct_numerodecuenta" },
            new object[] { "sanic_cm_tbl_cargadiaria", "sanic_cm_cd_numerocuenta" },
            new object[] { "sanic_cm_tbl_cuentapan", "sanic_cm_cp_name" },
        };

        public static System.Collections.Generic.IEnumerable<object[]> PanPorMensaje()
        {
            foreach (var msg in new[] { "Create", "Update" })
                foreach (var col in ColumnasPan)
                    foreach (var valor in new[] { "4111111111111111", "4111 1111 1111 1111", "4111-1111-1111-1111", "12345" })
                        yield return new object[] { msg, col[0], col[1], valor };
        }

        public static System.Collections.Generic.IEnumerable<object[]> EntidadColumna()
        {
            foreach (var msg in new[] { "Create", "Update" })
                foreach (var col in ColumnasPan) yield return new object[] { msg, col[0], col[1] };
        }

        private static Harness Preparar(string mensaje, string entidad, string columna, object valor)
        {
            var target = new Entity(entidad, Guid.NewGuid());
            target[columna] = valor;
            return new Harness(mensaje, entidad).ConTarget(target);
        }

        // ---------- rechazos ----------

        [Theory]
        [MemberData(nameof(PanPorMensaje))]
        public void Rechaza_un_pan_en_claro_en_la_columna_de_texto(string mensaje, string entidad, string columna, string pan)
        {
            var h = Preparar(mensaje, entidad, columna, pan);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));

            Assert.Contains("número de tarjeta completo", ex.Message);
            Assert.DoesNotContain(columna, ex.Message);
            Assert.Contains(columna, h.TodoElTexto());
            AfirmarSinValor(ex.Message, pan);
            AfirmarSinValor(h.TodoElTexto(), pan);
        }

        [Fact]
        public void No_inspecciona_cp_pan_de_la_boveda_pero_si_su_nombre_principal()
        {
            // La boveda SI guarda el PAN completo en cp_pan: esa columna no se evalua. cp_name es el enmascarado.
            var target = new Entity("sanic_cm_tbl_cuentapan", Guid.NewGuid());
            target["sanic_cm_cp_name"] = "4111111111111111";
            target["sanic_cm_cp_pan"] = "4111111111111111";
            var h = new Harness("Create", "sanic_cm_tbl_cuentapan").ConTarget(target);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));

            Assert.Contains("sanic_cm_cp_name", h.TodoElTexto());
            Assert.DoesNotContain("sanic_cm_cp_pan", h.TodoElTexto());
            Assert.DoesNotContain("sanic_cm_cp_name", ex.Message);
        }

        [Fact]
        public void La_boveda_acepta_el_pan_completo_en_cp_pan_si_el_nombre_va_enmascarado()
        {
            var target = new Entity("sanic_cm_tbl_cuentapan", Guid.NewGuid());
            target["sanic_cm_cp_name"] = "****1111";
            target["sanic_cm_cp_pan"] = "4111111111111111";
            var h = new Harness("Create", "sanic_cm_tbl_cuentapan").ConTarget(target);

            h.Ejecutar(new PanGuard());
        }

        // ---------- permitidos ----------

        [Theory]
        [InlineData("****1234")]
        [InlineData("1234")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("QA-1234")]
        public void Permite_valores_enmascarados_vacios_o_cortos_en_las_columnas_de_texto(string valor)
        {
            foreach (var msg in new[] { "Create", "Update" })
            {
                foreach (var col in ColumnasPan)
                {
                    var h = Preparar(msg, (string)col[0], (string)col[1], valor);

                    h.Ejecutar(new PanGuard()); // no debe lanzar

                    Assert.Equal(valor, h.Target.GetAttributeValue<string>((string)col[1]));
                }
            }
        }

        [Theory]
        [MemberData(nameof(EntidadColumna))]
        public void Update_sin_la_columna_en_el_Target_no_hace_nada(string mensaje, string entidad, string columna)
        {
            // Aunque OTRAS columnas lleven digitos (clave HMAC en hex, importes, telefonos), no se inspeccionan.
            var target = new Entity(entidad, Guid.NewGuid());
            target["sanic_cm_ct_clavepan"] = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
            target["otra_columna"] = "4111111111111111";
            var h = new Harness(mensaje, entidad).ConTarget(target);

            h.Ejecutar(new PanGuard());

            Assert.False(target.Contains(columna));
        }

        [Fact]
        public void Un_valor_distinto_de_texto_en_la_columna_se_evalua_como_texto()
        {
            var h = Preparar("Create", "sanic_cm_tbl_cuenta", "sanic_cm_ct_numerodecuenta", 4111111111111111L);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));

            Assert.Contains("sanic_cm_ct_numerodecuenta", h.TodoElTexto());
            AfirmarSinValor(ex.Message, "4111111111111111");
        }

        // ---------- contexto / fail-closed ----------

        [Fact]
        public void No_necesita_el_servicio_de_organizacion()
        {
            var h = Preparar("Create", "sanic_cm_tbl_cuenta", "sanic_cm_ct_numerodecuenta", "****1234");

            h.Ejecutar(new PanGuard());

            Assert.Equal(0, h.RetrievesAlServicio());
            h.Factory.Verify(f => f.CreateOrganizationService(It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public void Funciona_tambien_en_PreValidation()
        {
            var target = new Entity("sanic_cm_tbl_cuenta", Guid.NewGuid());
            target["sanic_cm_ct_numerodecuenta"] = "4111111111111111";
            var h = new Harness("Create", "sanic_cm_tbl_cuenta", 10).ConTarget(target);

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));
        }

        [Fact]
        public void Falla_cerrado_si_se_registra_sobre_una_entidad_sin_reglas()
        {
            var h = Preparar("Create", "account", "name", "****1234");

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));

            Assert.Contains("account", ex.Message);
        }

        [Fact]
        public void Falla_cerrado_si_falta_el_Target()
        {
            var h = new Harness("Create", "sanic_cm_tbl_cuenta");

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));
        }

        [Fact]
        public void Falla_cerrado_si_el_mensaje_no_es_Create_ni_Update()
        {
            var h = Preparar("Delete", "sanic_cm_tbl_cuenta", "sanic_cm_ct_numerodecuenta", "****1234");

            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new PanGuard()));
        }

        [Fact]
        public void Execute_con_proveedor_nulo_lanza_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PanGuard().Execute(null));
        }

        // ---------- helpers ----------

        private static void AfirmarSinValor(string texto, string valor)
        {
            Assert.DoesNotContain(valor, texto ?? string.Empty);
            // ni siquiera fragmentos de 4+ digitos del valor enviado
            if (valor != null && valor.Length >= 4)
                Assert.DoesNotContain(valor.Substring(0, 4), texto ?? string.Empty);
            // ni ninguna secuencia de 5+ digitos en el texto (estas rutas no tienen razon de contener digitos)
            Assert.False(PanDetector.ContieneSecuenciaDeDigitos(texto));
        }
    }
}
