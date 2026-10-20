using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// APP-24: una Actualizacion de datos tiene que colgar de una interaccion que tenga cuenta; por ese camino
    /// (actualizacion -> interaccion -> cuenta -> CIF) el backoffice sabe de que cliente es.
    /// F7: en el telefono (offline) la actualizacion llega sin la interaccion; trae su cuenta y la fecha de captura,
    /// y el servidor la cuelga de la interaccion del mismo cobrador, cuenta y captura.
    /// </summary>
    public class ActualizacionConInteraccionTests
    {
        private const string Actualizacion = "sanic_cm_tbl_actualizaciondatos";
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string AdInteraccion = "sanic_cm_ad_interaccionid";
        private const string InCuenta = "sanic_cm_in_cuentaid";
        private const string AdTelefono = "sanic_cm_ad_telefonocelular";
        private const string AdCuenta = "sanic_cm_ad_cuentaid";
        private const string AdFechaCaptura = "sanic_cm_ad_fechacaptura";
        private const string InFechaCaptura = "sanic_cm_in_fechacaptura";
        private const string InActualizaInfo = "sanic_cm_in_actualizainfo";
        private static readonly DateTime Captura = new DateTime(2026, 10, 20, 18, 50, 33, DateTimeKind.Utc);

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

        // ------------------------------------------------------------------ F7: llega sin interaccion (offline)

        private static Entity SinInteraccion(Guid cuenta)
        {
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdCuenta] = new EntityReference(Cuenta, cuenta);
            t[AdFechaCaptura] = Captura;
            t[AdTelefono] = "8888-0000";
            return t;
        }

        private static Harness ConInteraccionesQueCoinciden(Entity target, params Guid[] interacciones)
        {
            var h = new Harness("Create", Actualizacion).ConTarget(target);
            var filas = new EntityCollection(interacciones.Select(id => new Entity(Interaccion, id)).ToList());
            h.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => ((QueryExpression)q).EntityName == Interaccion)))
                .Returns(filas);
            return h;
        }

        private static QueryExpression ConsultaDeInteraccion(Harness h)
        {
            foreach (var inv in h.Service.Invocations)
            {
                if (inv.Method.Name != "RetrieveMultiple") continue;
                var q = inv.Arguments[0] as QueryExpression;
                if (q != null && q.EntityName == Interaccion) return q;
            }
            return null;
        }

        [Fact]
        public void F7_sin_interaccion_se_cuelga_de_la_interaccion_del_mismo_cobrador_cuenta_y_captura()
        {
            var cuenta = Guid.NewGuid();
            var interaccion = Guid.NewGuid();
            var h = ConInteraccionesQueCoinciden(SinInteraccion(cuenta), interaccion);

            h.Ejecutar(new ActualizacionConInteraccion());

            var asignada = h.Target.GetAttributeValue<EntityReference>(AdInteraccion);
            Assert.NotNull(asignada);
            Assert.Equal(interaccion, asignada.Id);
            Assert.Equal(Interaccion, asignada.LogicalName);

            var q = ConsultaDeInteraccion(h);
            Assert.NotNull(q);
            var c = q.Criteria.Conditions;
            Assert.Contains(c, x => x.AttributeName == InCuenta && x.Operator == ConditionOperator.Equal && (Guid)x.Values[0] == cuenta);
            Assert.Contains(c, x => x.AttributeName == "createdby" && x.Operator == ConditionOperator.Equal && (Guid)x.Values[0] == Harness.UsuarioLlamador);
            Assert.Contains(c, x => x.AttributeName == InActualizaInfo && x.Operator == ConditionOperator.Equal && (bool)x.Values[0]);
            var f = c.Single(x => x.AttributeName == InFechaCaptura);
            Assert.Equal(ConditionOperator.Between, f.Operator);
            Assert.True((DateTime)f.Values[0] < Captura && (DateTime)f.Values[1] > Captura);
            Assert.True((Captura - (DateTime)f.Values[0]).TotalSeconds <= 2 && ((DateTime)f.Values[1] - Captura).TotalSeconds <= 2);
            Assert.Equal(2, q.TopCount);
        }

        [Fact]
        public void F7_sin_interaccion_y_sin_coincidencias_se_rechaza_para_reintentar()
        {
            var h = ConInteraccionesQueCoinciden(SinInteraccion(Guid.NewGuid()));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Contains("todavia", ex.Message);
            Assert.DoesNotContain("8888", ex.Message);
        }

        [Fact]
        public void F7_sin_interaccion_y_dos_coincidencias_se_rechaza_por_ambigua()
        {
            var h = ConInteraccionesQueCoinciden(SinInteraccion(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid());
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Contains("mas de una", ex.Message);
            Assert.Null(h.Target.GetAttributeValue<EntityReference>(AdInteraccion));
        }

        [Fact]
        public void F7_sin_interaccion_con_cuenta_pero_sin_fecha_se_rechaza_sin_consultar()
        {
            var t = SinInteraccion(Guid.NewGuid());
            t.Attributes.Remove(AdFechaCaptura);
            var h = ConInteraccionesQueCoinciden(t, Guid.NewGuid());
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Null(ConsultaDeInteraccion(h));
        }

        [Fact]
        public void F7_con_interaccion_y_sin_cuenta_completa_la_cuenta_desde_la_interaccion()
        {
            var interaccion = Guid.NewGuid();
            var fila = InteraccionCon(interaccion, conCuenta: true);
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdInteraccion] = new EntityReference(Interaccion, interaccion);
            var h = new Harness("Create", Actualizacion).ConTarget(t).RetrieveDevuelve(fila, InCuenta);

            h.Ejecutar(new ActualizacionConInteraccion());

            Assert.Equal(fila.GetAttributeValue<EntityReference>(InCuenta).Id, t.GetAttributeValue<EntityReference>(AdCuenta).Id);
        }

        [Fact]
        public void F7_con_interaccion_de_otra_cuenta_se_rechaza()
        {
            var interaccion = Guid.NewGuid();
            var t = new Entity(Actualizacion, Guid.NewGuid());
            t[AdInteraccion] = new EntityReference(Interaccion, interaccion);
            t[AdCuenta] = new EntityReference(Cuenta, Guid.NewGuid());
            var h = new Harness("Create", Actualizacion).ConTarget(t)
                .RetrieveDevuelve(InteraccionCon(interaccion, conCuenta: true), InCuenta);

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Contains("otra cuenta", ex.Message);
        }

        [Fact]
        public void F7_en_un_Update_quitar_la_interaccion_se_rechaza_aunque_traiga_cuenta()
        {
            var t = SinInteraccion(Guid.NewGuid());
            t[AdInteraccion] = null;
            var h = new Harness("Update", Actualizacion).ConTarget(t);
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
            Assert.Null(ConsultaDeInteraccion(h));
        }

        [Fact]
        public void Registrado_en_otra_tabla_falla()
        {
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(new Entity("sanic_cm_tbl_pago", Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ActualizacionConInteraccion()));
        }
    }
}
