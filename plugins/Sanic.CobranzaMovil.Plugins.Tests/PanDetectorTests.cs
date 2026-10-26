using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    public class PanDetectorTests
    {
        [Theory]
        [InlineData("12345")]
        [InlineData("4111111111111111")]
        [InlineData("abc12345def")]
        [InlineData("cuenta 4111111111111")]
        // separadores habituales de un PAN escrito a mano o pegado desde un extracto
        [InlineData("4111 1111 1111 1111")]
        [InlineData("4111-1111-1111-1111")]
        [InlineData("4111  1111  1111")]
        [InlineData("4111 1111 1111")]
        [InlineData("12 34 5")]
        // digitos Unicode (arabe-indicos, ancho completo) tambien cuentan
        [InlineData("١٢٣٤٥")]
        [InlineData("１２３４５")]
        public void Detecta_cinco_o_mas_digitos(string valor)
        {
            Assert.True(PanDetector.ContieneSecuenciaDeDigitos(valor));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("****1234")]
        [InlineData("1234")]
        [InlineData("**** 1234")]
        [InlineData("•••• 1234")]
        [InlineData("ABC-1234")]
        [InlineData("12 ab 34 cd 5")]
        [InlineData("12x34")]
        [InlineData("QA-xyz")]
        public void Permite_menos_de_cinco_digitos_y_enmascarados(string valor)
        {
            Assert.False(PanDetector.ContieneSecuenciaDeDigitos(valor));
        }

        [Fact]
        public void El_minimo_es_configurable_y_por_defecto_5()
        {
            Assert.False(PanDetector.ContieneSecuenciaDeDigitos("1234"));
            Assert.True(PanDetector.ContieneSecuenciaDeDigitos("1234", 4));
        }

        // ---------- EnmascararPan: tarjetas en textos libres ----------

        [Theory]
        [InlineData("4111111111111111", "\u2022\u2022\u2022\u2022 1111")]
        [InlineData("pago con 4012888888881881 ayer", "pago con \u2022\u2022\u2022\u2022 1881 ayer")]
        [InlineData("amex 378282246310005.", "amex \u2022\u2022\u2022\u2022 0005.")]
        [InlineData("4222222222222", "\u2022\u2022\u2022\u2022 2222")]
        public void Enmascara_un_numero_que_pasa_Luhn(string texto, string esperado)
        {
            int n;
            Assert.Equal(esperado, PanDetector.EnmascararPan(texto, out n));
            Assert.Equal(1, n);
        }

        [Theory]
        [InlineData("tarjeta 4111 1111 1111 1111 fin", "tarjeta \u2022\u2022\u2022\u2022 1111 fin")]
        [InlineData("tarjeta 4111-1111-1111-1111", "tarjeta \u2022\u2022\u2022\u2022 1111")]
        [InlineData("tarjeta 4111\u00A01111\u00A01111\u00A01111", "tarjeta \u2022\u2022\u2022\u2022 1111")]
        [InlineData("4111  1111 - 1111 1111", "\u2022\u2022\u2022\u2022 1111")]
        public void Enmascara_con_espacios_y_guiones_entre_digitos(string texto, string esperado)
        {
            int n;
            Assert.Equal(esperado, PanDetector.EnmascararPan(texto, out n));
            Assert.Equal(1, n);
        }

        [Theory]
        // 13+ digitos que no pasan Luhn: telefonos, recibos, cuentas de otro tipo
        [InlineData("4111111111111112")]
        [InlineData("tel 5058888123456")]
        [InlineData("recibo 50588881234567")]
        [InlineData("12345678901234567890")]
        public void No_toca_13_o_mas_digitos_que_no_pasan_Luhn(string texto)
        {
            int n;
            Assert.Same(texto, PanDetector.EnmascararPan(texto, out n));
            Assert.Equal(0, n);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Cliente no estaba, volver el lunes")]
        [InlineData("tel 8888-1234, pago C$ 1500")]
        [InlineData("\u2022\u2022\u2022\u2022 1111")]
        public void Deja_intacto_un_texto_sin_numeros_de_tarjeta(string texto)
        {
            int n;
            Assert.Same(texto, PanDetector.EnmascararPan(texto, out n));
            Assert.Equal(0, n);
        }

        [Fact]
        public void Enmascara_varios_numeros_en_el_mismo_texto()
        {
            int n;
            var r = PanDetector.EnmascararPan("una 4111111111111111 y otra 6011-1111-1111-1117, tel 8888-1234", out n);

            Assert.Equal("una \u2022\u2022\u2022\u2022 1111 y otra \u2022\u2022\u2022\u2022 1117, tel 8888-1234", r);
            Assert.Equal(2, n);
        }

        [Fact]
        public void Separa_la_tarjeta_de_un_telefono_pegado_con_un_espacio()
        {
            int n;
            var r = PanDetector.EnmascararPan("88881234 4111111111111111", out n);

            Assert.Equal("88881234 \u2022\u2022\u2022\u2022 1111", r);
            Assert.Equal(1, n);
        }

        [Fact]
        public void Enmascara_digitos_Unicode_por_su_valor()
        {
            int n;
            var r = PanDetector.EnmascararPan("\uFF14\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11\uFF11", out n);

            Assert.Equal("\u2022\u2022\u2022\u2022 1111", r);
            Assert.Equal(1, n);
        }

        [Theory]
        [InlineData("4111111111111111", true)]
        [InlineData("4111111111111112", false)]
        [InlineData("", false)]
        [InlineData("41x1", false)]
        public void Luhn(string digitos, bool esperado)
        {
            Assert.Equal(esperado, PanDetector.PasaLuhn(digitos));
        }
    }
}
