using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>F6: el recibo repetido no se rechaza (F8): el pago entra marcado para que BackOffice lo revise.</summary>
    public class ReciboRepetidoTests
    {
        private const string Pago = "sanic_cm_tbl_pago";
        private const string Recibo = "sanic_cm_pg_numerorecibo";
        private const string Repetido = "sanic_cm_pg_reciborepetido";

        private static Harness Con(string recibo, int existentes)
        {
            var t = new Entity(Pago, Guid.NewGuid());
            if (recibo != null) t[Recibo] = recibo;
            var h = new Harness("Create", Pago).ConTarget(t);
            var filas = new EntityCollection(Enumerable.Range(0, existentes).Select(_ => new Entity(Pago, Guid.NewGuid())).ToList());
            h.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(filas);
            return h;
        }

        [Fact]
        public void Recibo_ya_usado_entra_marcado_y_no_se_rechaza()
        {
            var h = Con(" 74918 ", 1);
            h.Ejecutar(new ReciboRepetido());
            Assert.True(h.Target.GetAttributeValue<bool>(Repetido));
            var q = (QueryExpression)h.Service.Invocations.First(x => x.Method.Name == "RetrieveMultiple").Arguments[0];
            var c = Assert.Single(q.Criteria.Conditions);
            Assert.Equal(Recibo, c.AttributeName);
            Assert.Equal("74918", c.Values[0]);
            Assert.Equal(1, q.TopCount);
            Assert.DoesNotContain("74918", h.TodoElTexto());
        }

        [Fact]
        public void Recibo_nuevo_entra_sin_marca()
        {
            var h = Con("74999", 0);
            h.Ejecutar(new ReciboRepetido());
            Assert.True(h.Target.Contains(Repetido));
            Assert.False(h.Target.GetAttributeValue<bool>(Repetido));
        }

        [Fact]
        public void Sin_recibo_no_consulta()
        {
            var h = Con("  ", 1);
            h.Ejecutar(new ReciboRepetido());
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.False(h.Target.Contains(Repetido));
        }

        [Fact]
        public void Registrado_en_otra_tabla_falla()
        {
            var h = new Harness("Create", "sanic_cm_tbl_interaccion").ConTarget(new Entity("sanic_cm_tbl_interaccion", Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ReciboRepetido()));
        }
    }
}
