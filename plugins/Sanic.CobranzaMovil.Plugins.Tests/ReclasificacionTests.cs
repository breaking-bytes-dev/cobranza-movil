using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// DAT-15 (DEC-021): una cuenta tiene una sola asignacion activa entre Diaria e Incobrable. Cuando la carga trae
    /// la cuenta con el otro tipo, la anterior se cierra con motivo "Reclasificada a {tipo}". Las Especiales no se tocan.
    /// </summary>
    public class ReclasificacionTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Estado = "sanic_cm_as_estado";
        private const string Tipo = "sanic_cm_as_tipo";
        private const string CuentaId = "sanic_cm_as_cuentaid";
        private const string Motivo = "sanic_cm_as_motivo";
        private const string FechaCierre = "sanic_cm_as_fechacierre";
        private const int Diaria = 1, Incobrable = 2, Especial = 3;
        private const int PostOperation = 40;

        private readonly Guid cuenta = Guid.NewGuid();
        private readonly List<Entity> escritas = new List<Entity>();

        private Entity Fila(int tipo, bool? estado, bool conCuenta = true)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[Tipo] = new OptionSetValue(tipo);
            if (estado.HasValue) t[Estado] = estado.Value;
            if (conCuenta) t[CuentaId] = new EntityReference(Cuenta, cuenta);
            return t;
        }

        private Harness Preparar(Harness h, params Guid[] otrasActivas)
        {
            var filas = new EntityCollection();
            foreach (var id in otrasActivas) filas.Entities.Add(new Entity(Asignacion, id));
            h.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(filas);
            h.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e => escritas.Add(e));
            return h;
        }

        private Harness Crear(Entity target, params Guid[] otrasActivas)
        {
            return Preparar(new Harness("Create", Asignacion, PostOperation).ConTarget(target), otrasActivas);
        }

        private Harness Actualizar(Entity target, Entity previa, params Guid[] otrasActivas)
        {
            return Preparar(new Harness("Update", Asignacion, PostOperation).ConTarget(target).ConPreImage(previa), otrasActivas);
        }

        private static QueryExpression Consulta(Harness h)
        {
            foreach (var inv in h.Service.Invocations)
            {
                if (inv.Method.Name == "RetrieveMultiple") return (QueryExpression)inv.Arguments[0];
            }
            return null;
        }

        private static object Condicion(QueryExpression q, string columna)
        {
            foreach (var c in q.Criteria.Conditions)
            {
                if (c.AttributeName == columna) return c.Values[0];
            }
            return null;
        }

        [Fact]
        public void Falla_si_se_registra_en_PreOperation()
        {
            var h = new Harness("Create", Asignacion, 20).ConTarget(Fila(Incobrable, true));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new Reclasificacion()));
            Assert.Contains("registro incorrecto", ex.Message);
        }

        [Fact]
        public void Una_Incobrable_nueva_cierra_la_Diaria_activa_de_la_cuenta()
        {
            var diaria = Guid.NewGuid();
            var antes = DateTime.UtcNow.AddSeconds(-1);
            Crear(Fila(Incobrable, null), diaria).Ejecutar(new Reclasificacion());

            var e = Assert.Single(escritas);
            Assert.Equal(diaria, e.Id);
            Assert.False(e.GetAttributeValue<bool>(Estado));
            Assert.Equal("Reclasificada a Incobrable", e.GetAttributeValue<string>(Motivo));
            Assert.True(e.GetAttributeValue<DateTime>(FechaCierre) >= antes);
            Assert.Equal(3, e.Attributes.Count); // solo estado, motivo y fecha de cierre
        }

        [Fact]
        public void Una_Diaria_nueva_cierra_la_Incobrable_y_busca_el_otro_tipo_de_la_misma_cuenta()
        {
            var h = Crear(Fila(Diaria, true), Guid.NewGuid());
            h.Ejecutar(new Reclasificacion());

            Assert.Equal("Reclasificada a Diaria", Assert.Single(escritas).GetAttributeValue<string>(Motivo));
            var q = Consulta(h);
            Assert.Equal(cuenta, Condicion(q, CuentaId));
            Assert.Equal(Incobrable, Condicion(q, Tipo));
            Assert.Equal(true, Condicion(q, Estado));
        }

        [Fact]
        public void Escribe_con_el_servicio_del_sistema()
        {
            var h = Crear(Fila(Incobrable, true), Guid.NewGuid());
            h.Ejecutar(new Reclasificacion());
            h.Factory.Verify(f => f.CreateOrganizationService(null), Times.AtLeastOnce());
        }

        [Fact]
        public void Una_Especial_no_reclasifica_nada()
        {
            var h = Crear(Fila(Especial, true), Guid.NewGuid());
            h.Ejecutar(new Reclasificacion());
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.Empty(escritas);
        }

        [Fact]
        public void Una_cerrada_o_sin_cuenta_no_reclasifica_nada()
        {
            var h = Crear(Fila(Incobrable, false), Guid.NewGuid());
            h.Ejecutar(new Reclasificacion());
            Assert.Equal(0, h.ConsultasAlServicio());

            var h2 = Crear(Fila(Incobrable, true, conCuenta: false), Guid.NewGuid());
            h2.Ejecutar(new Reclasificacion());
            Assert.Equal(0, h2.ConsultasAlServicio());
            Assert.Empty(escritas);
        }

        [Fact]
        public void Una_Diaria_reabierta_por_la_carga_cierra_la_Incobrable()
        {
            var previa = Fila(Diaria, false);
            var t = new Entity(Asignacion, previa.Id);
            t[Estado] = true; // lo puso CargaSobreAsignacion
            t["sanic_cm_as_ultimacarga"] = DateTime.UtcNow.Date;
            Actualizar(t, previa, Guid.NewGuid()).Ejecutar(new Reclasificacion());
            Assert.Single(escritas);
        }

        [Fact]
        public void En_la_carga_diaria_de_una_activa_sin_otro_tipo_no_escribe()
        {
            var previa = Fila(Diaria, true);
            var t = new Entity(Asignacion, previa.Id);
            t["sanic_cm_as_ultimacarga"] = DateTime.UtcNow.Date;
            var h = Actualizar(t, previa);
            h.Ejecutar(new Reclasificacion());
            Assert.Equal(1, h.ConsultasAlServicio());
            Assert.Empty(escritas);
        }

        [Fact]
        public void Cerrar_una_asignacion_no_reclasifica()
        {
            var previa = Fila(Incobrable, true);
            var t = new Entity(Asignacion, previa.Id);
            t[Estado] = false;
            var h = Actualizar(t, previa, Guid.NewGuid());
            h.Ejecutar(new Reclasificacion());
            Assert.Equal(0, h.ConsultasAlServicio());
        }
    }
}
