using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Al vincular, cambiar o quitar el usuario de un agente, RepartoPorAgente reescribe el mismo agente en cada asignacion
    /// activa cuyo dueno no corresponde: eso dispara RepartoAsignacion en cada una y la logica del reparto queda en un solo
    /// lugar. Corre asincrono.
    /// </summary>
    public class RepartoPorAgenteTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Usuario = "systemuser";
        private const string Equipo = "team";
        private const int Especial = 3, Aprobado = 3;
        private static readonly int NoExiste = unchecked((int)0x80040217);

        private sealed class Mundo
        {
            public readonly Harness H;
            public readonly Guid AgenteId = Guid.NewGuid();
            public readonly Guid Cobrador = Guid.NewGuid();
            public readonly Guid EquipoSinCobrador = Guid.NewGuid();

            public readonly Dictionary<string, Entity> Filas = new Dictionary<string, Entity>();
            public readonly List<Entity> Pendientes = new List<Entity>(); // lo que devuelve la consulta de asignaciones
            public readonly List<Entity> Equipos = new List<Entity>();
            public readonly List<QueryExpression> Consultas = new List<QueryExpression>();
            public readonly List<Entity> Updates = new List<Entity>();

            /// <summary>Si devuelve un codigo, el Update falla con ese codigo de Dataverse.</summary>
            public Func<Entity, int?> Falla = e => null;

            public Mundo(string mensaje = "Update", string entidad = Agente, int modo = 1, int etapa = 40)
            {
                H = new Harness(mensaje, entidad, etapa);
                H.Context.SetupGet(c => c.Mode).Returns(modo);
                H.Context.SetupGet(c => c.PrimaryEntityId).Returns(AgenteId);
                var target = new Entity(Agente, AgenteId);
                target["sanic_cm_ag_usuarioid"] = new EntityReference(Usuario, Cobrador);
                H.ConTarget(target);

                var agente = new Entity(Agente, AgenteId);
                agente["sanic_cm_ag_usuarioid"] = new EntityReference(Usuario, Cobrador);
                Filas[Agente + "|" + AgenteId] = agente;
                Equipos.Add(new Entity(Equipo, EquipoSinCobrador));
                for (var i = 0; i < 3; i++) Pendientes.Add(new Entity(Asignacion, Guid.NewGuid()));

                H.Service.Setup(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ColumnSet>()))
                    .Returns<string, Guid, ColumnSet>((t, id, cs) =>
                    {
                        Entity e;
                        if (Filas.TryGetValue(t + "|" + id, out e)) return e;
                        throw Fault(NoExiste);
                    });
                H.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                    .Returns<QueryBase>(q =>
                    {
                        var qe = (QueryExpression)q;
                        Consultas.Add(qe);
                        var filas = qe.EntityName == Equipo ? Equipos : Pendientes;
                        return new EntityCollection(filas.Take(qe.TopCount ?? int.MaxValue).ToList());
                    });
                H.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e =>
                {
                    Updates.Add(e);
                    var codigo = Falla(e);
                    if (codigo.HasValue) throw Fault(codigo.Value);
                });
            }

            public Entity Agente_ { get { return Filas[Agente + "|" + AgenteId]; } }

            public void Desvinculado()
            {
                Agente_.Attributes.Remove("sanic_cm_ag_usuarioid");
                H.Target["sanic_cm_ag_usuarioid"] = null;
            }

            public QueryExpression ConsultaDeAsignaciones
            {
                get { return Assert.Single(Consultas.Where(q => q.EntityName == Asignacion)); }
            }

            public void Ejecutar()
            {
                H.Ejecutar(new RepartoPorAgente());
            }
        }

        private static FaultException<OrganizationServiceFault> Fault(int codigo)
        {
            return new FaultException<OrganizationServiceFault>(
                new OrganizationServiceFault { ErrorCode = codigo, Message = "falla simulada" }, new FaultReason("falla simulada"));
        }

        // ------------------------------------------------------------------ registro

        [Fact]
        public void Falla_cerrado_si_el_paso_se_registra_sincrono()
        {
            var m = new Mundo(modo: 0);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            Assert.Contains("asincrono", ex.Message);
            Assert.Empty(m.Updates);
        }

        [Fact]
        public void Falla_cerrado_si_no_es_el_Update_del_agente()
        {
            Assert.Throws<InvalidPluginExecutionException>(() => new Mundo(entidad: Asignacion).Ejecutar());
            Assert.Throws<InvalidPluginExecutionException>(() => new Mundo(mensaje: "Create").Ejecutar());
            Assert.Throws<InvalidPluginExecutionException>(() => new Mundo(etapa: 20).Ejecutar());
        }

        [Fact]
        public void Usa_el_servicio_del_sistema_y_nunca_el_del_llamador()
        {
            var m = new Mundo();
            m.Ejecutar();
            m.H.Factory.Verify(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null)), Times.AtLeastOnce());
            m.H.Factory.Verify(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == Harness.UsuarioLlamador)), Times.Never());
        }

        // ------------------------------------------------------------------ vincular o cambiar el usuario

        [Fact]
        public void Al_vincular_el_usuario_reescribe_el_mismo_agente_en_cada_asignacion_pendiente()
        {
            var m = new Mundo();
            m.Ejecutar();

            Assert.Equal(m.Pendientes.Select(p => p.Id), m.Updates.Select(u => u.Id));
            Assert.All(m.Updates, u =>
            {
                Assert.Equal(Asignacion, u.LogicalName);
                var unica = Assert.Single(u.Attributes);
                Assert.Equal("sanic_cm_as_agenteid", unica.Key);
                Assert.Equal(m.AgenteId, ((EntityReference)unica.Value).Id);
                Assert.Equal(Agente, ((EntityReference)unica.Value).LogicalName);
            });
        }

        [Fact]
        public void Busca_las_activas_del_agente_cuyo_dueno_no_es_el_usuario_y_sin_Especiales_por_aprobar()
        {
            var m = new Mundo();
            m.Ejecutar();

            var q = m.ConsultaDeAsignaciones;
            var c = q.Criteria.Conditions;
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_agenteid" && x.Operator == ConditionOperator.Equal && (Guid)x.Values[0] == m.AgenteId);
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_estado" && x.Operator == ConditionOperator.Equal && (bool)x.Values[0]);
            Assert.Contains(c, x => x.AttributeName == "ownerid" && x.Operator == ConditionOperator.NotEqual && (Guid)x.Values[0] == m.Cobrador);
            var o = Assert.Single(q.Criteria.Filters);
            Assert.Equal(LogicalOperator.Or, o.FilterOperator);
            Assert.Contains(o.Conditions, x => x.AttributeName == "sanic_cm_as_tipo" && x.Operator == ConditionOperator.NotEqual && (int)x.Values[0] == Especial);
            Assert.Contains(o.Conditions, x => x.AttributeName == "sanic_cm_as_estadoaprobacion" && x.Operator == ConditionOperator.Equal && (int)x.Values[0] == Aprobado);
            Assert.False(q.ColumnSet.AllColumns);
            Assert.Empty(q.ColumnSet.Columns);
        }

        [Fact]
        public void Lee_el_usuario_actual_del_agente_y_no_el_del_evento()
        {
            // Asincrono: entre el evento y la ejecucion el Admin pudo volver a cambiarlo. Manda lo que hay ahora.
            var m = new Mundo();
            m.Agente_["sanic_cm_ag_usuarioid"] = new EntityReference(Usuario, Guid.NewGuid());
            m.Ejecutar();
            var duenoDistinto = m.ConsultaDeAsignaciones.Criteria.Conditions.Single(x => x.AttributeName == "ownerid");
            Assert.Equal(m.Agente_.GetAttributeValue<EntityReference>("sanic_cm_ag_usuarioid").Id, (Guid)duenoDistinto.Values[0]);
        }

        [Fact]
        public void Sin_asignaciones_pendientes_no_escribe_nada()
        {
            var m = new Mundo();
            m.Pendientes.Clear();
            m.Ejecutar();
            Assert.Empty(m.Updates);
        }

        [Fact]
        public void Si_el_agente_ya_no_existe_termina_sin_error()
        {
            var m = new Mundo();
            m.Filas.Clear();
            m.Ejecutar();
            Assert.Empty(m.Updates);
        }

        // ------------------------------------------------------------------ quitar el usuario

        [Fact]
        public void Al_desvincular_reescribe_las_que_no_son_del_equipo_sin_cobrador()
        {
            var m = new Mundo();
            m.Desvinculado();
            m.Ejecutar();

            var duenoDistinto = m.ConsultaDeAsignaciones.Criteria.Conditions.Single(x => x.AttributeName == "ownerid");
            Assert.Equal(m.EquipoSinCobrador, (Guid)duenoDistinto.Values[0]);
            Assert.Equal(3, m.Updates.Count);
        }

        [Fact]
        public void Desvinculado_y_sin_equipo_sin_cobrador_falla_sin_escribir_y_sin_reintento()
        {
            var m = new Mundo();
            m.Desvinculado();
            m.Equipos.Clear();
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            Assert.NotEqual(OperationStatus.Retry, ex.Status);
            Assert.Contains("Cartera sin cobrador", ex.Message);
            Assert.Empty(m.Updates);
        }

        // ------------------------------------------------------------------ volumen y fallas

        [Fact]
        public void Con_mas_de_mil_escribe_mil_y_pide_reintento_para_el_resto()
        {
            // 2 minutos por ejecucion: se corta en 1000 y el reintento retoma (las ya movidas no vuelven a salir).
            var m = new Mundo();
            m.Pendientes.Clear();
            for (var i = 0; i < 1001; i++) m.Pendientes.Add(new Entity(Asignacion, Guid.NewGuid()));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());

            Assert.Equal(OperationStatus.Retry, ex.Status);
            Assert.Equal(1000, m.Updates.Count);
            Assert.Equal(1001, m.ConsultaDeAsignaciones.TopCount);
        }

        [Fact]
        public void Si_falla_una_sigue_con_las_demas_y_pide_reintento()
        {
            var m = new Mundo();
            var segunda = m.Pendientes[1].Id;
            m.Falla = e => e.Id == segunda ? unchecked((int)0x80040265) : (int?)null;
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());

            Assert.Equal(OperationStatus.Retry, ex.Status);
            Assert.Equal(3, m.Updates.Count);
        }

        [Fact]
        public void Ni_las_trazas_ni_el_error_citan_identificadores_de_registros()
        {
            var m = new Mundo();
            m.Falla = e => unchecked((int)0x80040265);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            var texto = m.H.TodoElTexto() + "\n" + ex.Message;
            foreach (var id in m.Pendientes.Select(p => p.Id).Concat(new[] { m.AgenteId, m.Cobrador }))
            {
                Assert.DoesNotContain(id.ToString(), texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
