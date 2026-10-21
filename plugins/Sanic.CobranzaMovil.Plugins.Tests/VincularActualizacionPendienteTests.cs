using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// F8: la actualizacion de datos que llego antes que su interaccion entra sin vincular; al llegar la interaccion
    /// (con actualiza info) se cuelga la sin vincular mas vieja de esa cuenta y de ese cobrador.
    /// </summary>
    public class VincularActualizacionPendienteTests
    {
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Actualizacion = "sanic_cm_tbl_actualizaciondatos";
        private const string Cuenta = "sanic_cm_tbl_cuenta";

        private static Harness Con(Entity interaccion, params Guid[] pendientes)
        {
            var h = new Harness("Create", Interaccion, 40).ConTarget(interaccion);
            h.Context.SetupGet(c => c.PrimaryEntityId).Returns(interaccion.Id);
            var filas = new EntityCollection(pendientes.Select(id => new Entity(Actualizacion, id)).ToList());
            h.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(filas);
            return h;
        }

        private static Entity NuevaInteraccion(Guid cuenta, bool actualiza)
        {
            var i = new Entity(Interaccion, Guid.NewGuid());
            i["sanic_cm_in_cuentaid"] = new EntityReference(Cuenta, cuenta);
            i["sanic_cm_in_actualizainfo"] = actualiza;
            return i;
        }

        private static QueryExpression Consulta(Harness h)
        {
            var inv = h.Service.Invocations.FirstOrDefault(x => x.Method.Name == "RetrieveMultiple");
            return inv == null ? null : (QueryExpression)inv.Arguments[0];
        }

        [Fact]
        public void Cuelga_la_actualizacion_sin_vincular_mas_vieja_de_la_cuenta_y_le_quita_la_marca()
        {
            var cuenta = Guid.NewGuid();
            var ad = Guid.NewGuid();
            var interaccion = NuevaInteraccion(cuenta, true);
            Entity actualizado = null;
            var h = Con(interaccion, ad);
            h.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e => actualizado = e);

            h.Ejecutar(new VincularActualizacionPendiente());

            Assert.NotNull(actualizado);
            Assert.Equal(ad, actualizado.Id);
            Assert.Equal(interaccion.Id, actualizado.GetAttributeValue<EntityReference>("sanic_cm_ad_interaccionid").Id);
            Assert.False(actualizado.GetAttributeValue<bool>("sanic_cm_ad_sinvincular"));
            Assert.True(actualizado.Contains("sanic_cm_ad_sinvincular"));

            var q = Consulta(h);
            Assert.Equal(Actualizacion, q.EntityName);
            var c = q.Criteria.Conditions;
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_ad_sinvincular" && (bool)x.Values[0]);
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_ad_interaccionid" && x.Operator == ConditionOperator.Null);
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_ad_cuentaid" && (Guid)x.Values[0] == cuenta);
            Assert.Contains(c, x => x.AttributeName == "createdby" && (Guid)x.Values[0] == Harness.UsuarioLlamador);
            Assert.Contains(c, x => x.AttributeName == "createdon" && x.Operator == ConditionOperator.GreaterEqual);
            Assert.Equal(OrderType.Ascending, Assert.Single(q.Orders).OrderType);
            Assert.Equal(1, q.TopCount);
        }

        [Fact]
        public void Sin_pendientes_no_modifica_nada()
        {
            var h = Con(NuevaInteraccion(Guid.NewGuid(), true));
            h.Ejecutar(new VincularActualizacionPendiente());
            h.Service.Verify(s => s.Update(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void Una_interaccion_que_no_actualiza_datos_no_consulta()
        {
            var h = Con(NuevaInteraccion(Guid.NewGuid(), false), Guid.NewGuid());
            h.Ejecutar(new VincularActualizacionPendiente());
            Assert.Equal(0, h.ConsultasAlServicio());
            h.Service.Verify(s => s.Update(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void Una_interaccion_sin_cuenta_no_consulta()
        {
            var i = NuevaInteraccion(Guid.NewGuid(), true);
            i.Attributes.Remove("sanic_cm_in_cuentaid");
            var h = Con(i, Guid.NewGuid());
            h.Ejecutar(new VincularActualizacionPendiente());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Registrado_en_PreOperation_falla()
        {
            var h = new Harness("Create", Interaccion, 20).ConTarget(NuevaInteraccion(Guid.NewGuid(), true));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new VincularActualizacionPendiente()));
        }
    }
}
