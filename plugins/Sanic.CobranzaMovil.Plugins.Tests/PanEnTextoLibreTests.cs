using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// PanEnTextoLibre enmascara en el Target los numeros de tarjeta del comentario de la interaccion y de las direcciones
    /// de la actualizacion de datos. Nunca rechaza (lo que llega del telefono sin conexion se perderia).
    /// </summary>
    public class PanEnTextoLibreTests
    {
        private const string Enmascarado = "•••• 1111";

        public static readonly object[][] Columnas =
        {
            new object[] { "sanic_cm_tbl_interaccion", "sanic_cm_in_comentario" },
            new object[] { "sanic_cm_tbl_actualizaciondatos", "sanic_cm_ad_direccioncasa" },
            new object[] { "sanic_cm_tbl_actualizaciondatos", "sanic_cm_ad_direccionec" },
            new object[] { "sanic_cm_tbl_actualizaciondatos", "sanic_cm_ad_direcciontrabajo" },
        };

        public static IEnumerable<object[]> ColumnaPorMensaje()
        {
            foreach (var msg in new[] { "Create", "Update" })
                foreach (var col in Columnas)
                    yield return new object[] { msg, col[0], col[1] };
        }

        private static Harness Preparar(string mensaje, string entidad, string columna, object valor)
        {
            var target = new Entity(entidad, Guid.NewGuid());
            target[columna] = valor;
            return new Harness(mensaje, entidad).ConTarget(target);
        }

        [Theory]
        [MemberData(nameof(ColumnaPorMensaje))]
        public void Enmascara_un_numero_que_pasa_Luhn(string mensaje, string entidad, string columna)
        {
            var h = Preparar(mensaje, entidad, columna, "pago con tarjeta 4111111111111111 en ventanilla");

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Equal("pago con tarjeta " + Enmascarado + " en ventanilla", h.Target.GetAttributeValue<string>(columna));
            Assert.Contains(columna, h.TodoElTexto());
            AfirmarSinPan(h.TodoElTexto());
        }

        [Theory]
        [MemberData(nameof(ColumnaPorMensaje))]
        public void Enmascara_con_separadores(string mensaje, string entidad, string columna)
        {
            var h = Preparar(mensaje, entidad, columna, "4111 1111-1111 1111");

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Equal(Enmascarado, h.Target.GetAttributeValue<string>(columna));
        }

        [Fact]
        public void Enmascara_varios_numeros_en_el_mismo_texto()
        {
            var h = Preparar("Create", "sanic_cm_tbl_interaccion", "sanic_cm_in_comentario",
                "dio 4111111111111111 y despues 4012 8888 8888 1881");

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Equal("dio " + Enmascarado + " y despues •••• 1881",
                h.Target.GetAttributeValue<string>("sanic_cm_in_comentario"));
        }

        [Theory]
        [InlineData("tel 5058888123456, recibo 50588881234567")]
        [InlineData("4111111111111112")]
        [InlineData("Cliente no estaba; volver el lunes")]
        [InlineData("")]
        public void No_toca_textos_sin_tarjeta_ni_13_digitos_que_no_pasan_Luhn(string texto)
        {
            foreach (var msg in new[] { "Create", "Update" })
            {
                foreach (var col in Columnas)
                {
                    var h = Preparar(msg, (string)col[0], (string)col[1], texto);

                    h.Ejecutar(new PanEnTextoLibre());

                    Assert.Equal(texto, h.Target.GetAttributeValue<string>((string)col[1]));
                    Assert.Contains("sin numeros de tarjeta", h.TodoElTexto());
                }
            }
        }

        [Fact]
        public void Solo_toca_sus_columnas()
        {
            // Telefonos y correo validan su formato en la app: no se enmascaran.
            var target = new Entity("sanic_cm_tbl_actualizaciondatos", Guid.NewGuid());
            target["sanic_cm_ad_telefonocelular"] = "4111111111111111";
            target["sanic_cm_ad_direccioncasa"] = "4111111111111111";
            var h = new Harness("Create", "sanic_cm_tbl_actualizaciondatos").ConTarget(target);

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Equal("4111111111111111", target.GetAttributeValue<string>("sanic_cm_ad_telefonocelular"));
            Assert.Equal(Enmascarado, target.GetAttributeValue<string>("sanic_cm_ad_direccioncasa"));
        }

        [Fact]
        public void Valor_nulo_no_falla()
        {
            var h = Preparar("Update", "sanic_cm_tbl_interaccion", "sanic_cm_in_comentario", null);

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Null(h.Target["sanic_cm_in_comentario"]);
        }

        [Fact]
        public void No_necesita_el_servicio_de_organizacion()
        {
            var h = Preparar("Create", "sanic_cm_tbl_interaccion", "sanic_cm_in_comentario", "4111111111111111");

            h.Ejecutar(new PanEnTextoLibre());

            h.Factory.Verify(f => f.CreateOrganizationService(It.IsAny<Guid?>()), Times.Never);
        }

        // ---------- nunca rechaza ----------

        [Fact]
        public void Entidad_sin_reglas_no_rechaza()
        {
            var h = Preparar("Create", "account", "name", "4111111111111111");

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Equal("4111111111111111", h.Target.GetAttributeValue<string>("name"));
        }

        [Fact]
        public void Mensaje_o_etapa_incorrectos_no_rechazan()
        {
            var h = new Harness("Delete", "sanic_cm_tbl_interaccion");
            h.Ejecutar(new PanEnTextoLibre());

            var post = Preparar("Create", "sanic_cm_tbl_interaccion", "sanic_cm_in_comentario", "4111111111111111");
            post.Context.SetupGet(c => c.Stage).Returns(40);
            post.Ejecutar(new PanEnTextoLibre());

            Assert.Contains("registro incorrecto", post.TodoElTexto());
        }

        [Fact]
        public void Sin_Target_no_rechaza()
        {
            var h = new Harness("Create", "sanic_cm_tbl_interaccion");

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Contains("sin Target", h.TodoElTexto());
        }

        [Fact]
        public void Funciona_tambien_en_PreValidation()
        {
            var target = new Entity("sanic_cm_tbl_interaccion", Guid.NewGuid());
            target["sanic_cm_in_comentario"] = "4111111111111111";
            var h = new Harness("Create", "sanic_cm_tbl_interaccion", 10).ConTarget(target);

            h.Ejecutar(new PanEnTextoLibre());

            Assert.Equal(Enmascarado, target.GetAttributeValue<string>("sanic_cm_in_comentario"));
        }

        private static void AfirmarSinPan(string texto)
        {
            Assert.DoesNotContain("4111111111111111", texto ?? string.Empty);
            Assert.False(PanDetector.ContieneSecuenciaDeDigitos(texto));
        }
    }
}
