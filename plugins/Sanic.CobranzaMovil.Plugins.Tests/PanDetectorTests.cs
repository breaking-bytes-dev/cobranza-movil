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
    }
}
