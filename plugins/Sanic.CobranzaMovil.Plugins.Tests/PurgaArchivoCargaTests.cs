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
    /// PurgaArchivoCarga borra el CSV del banco de los jobs de Smelter de las plantillas de CM cuando llegan a un estado
    /// terminal (y barre los viejos que quedaron con archivo). Todo con el servicio del sistema; nunca toca otras plantillas.
    /// </summary>
    public class PurgaArchivoCargaTests
    {
        private const string Job = "sanic_tbl_smlt_importjob";
        private const string Plantilla = "sanic_tbl_smlt_importtemplate";
        private const string Archivo = "sanic_smlt_ij_sourcefile";
        private const int EnCola = 0, Procesando = 1, Completado = 2, ConErrores = 3, Fallido = 4;

        // Ids fijos: xunit puede serializar los datos de la teoria al descubrirla y volver a crear la clase al correrla.
        private static readonly Guid Principal = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid Padres = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        private static readonly Guid Partes = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        private static readonly Guid Ajena = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

        private sealed class Escenario
        {
            public readonly Harness H;
            public readonly Mock<IOrganizationService> Sistema = new Mock<IOrganizationService>();
            public readonly List<Guid> Borrados = new List<Guid>();
            public readonly Guid JobId = Guid.NewGuid();
            public readonly Guid ArchivoId = Guid.NewGuid();
            public QueryExpression Barrido;
            public List<Entity> Viejos = new List<Entity>();
            public Entity Leido;

            public Escenario(Guid plantilla, int? estado, bool conArchivo = true, bool esPrueba = false, int modo = 1)
            {
                H = new Harness("Update", Job, 40);
                H.Context.SetupGet(c => c.Mode).Returns(modo);
                var target = new Entity(Job, JobId);
                target["sanic_smlt_ij_status"] = estado.HasValue ? new OptionSetValue(estado.Value) : null;
                H.ConTarget(target);

                H.Factory.Setup(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null))).Returns(Sistema.Object);

                var plantillas = new EntityCollection(new[] { Principal, Padres, Partes }.Select(id => new Entity(Plantilla, id)).ToList());
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Plantilla))).Returns(plantillas);

                Leido = new Entity(Job, JobId);
                Leido["sanic_smlt_ij_importtemplateid"] = new EntityReference(Plantilla, plantilla);
                if (estado.HasValue) Leido["sanic_smlt_ij_status"] = new OptionSetValue(estado.Value);
                Leido["sanic_smlt_ij_isdryrun"] = esPrueba;
                if (conArchivo) Leido[Archivo] = ArchivoId;
                Sistema.Setup(s => s.Retrieve(Job, JobId, It.IsAny<ColumnSet>())).Returns(() => Leido);

                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Job)))
                    .Callback<QueryBase>(q => Barrido = (QueryExpression)q)
                    .Returns(() => new EntityCollection(Viejos));

                Sistema.Setup(s => s.Execute(It.Is<OrganizationRequest>(r => r.RequestName == "DeleteFile")))
                    .Callback<OrganizationRequest>(r => Borrados.Add((Guid)r["FileId"]))
                    .Returns(new OrganizationResponse());
            }

            public Guid AgregarViejo()
            {
                var archivo = Guid.NewGuid();
                var e = new Entity(Job, Guid.NewGuid());
                e[Archivo] = archivo;
                Viejos.Add(e);
                return archivo;
            }

            public void Ejecutar() { H.Ejecutar(new PurgaArchivoCarga()); }
        }

        private static string Tabla(QueryBase q)
        {
            var qe = q as QueryExpression;
            return qe == null ? null : qe.EntityName;
        }

        // ---------- el job que cambio de estado ----------

        public static IEnumerable<object[]> TerminalesPorPlantilla()
        {
            foreach (var p in new[] { Principal, Padres, Partes })
                foreach (var e in new[] { Completado, ConErrores, Fallido })
                    yield return new object[] { p, e };
        }

        [Theory]
        [MemberData(nameof(TerminalesPorPlantilla))]
        public void Estado_terminal_de_una_plantilla_de_CM_borra_el_archivo(Guid plantilla, int estado)
        {
            var e = new Escenario(plantilla, estado);

            e.Ejecutar();

            Assert.Equal(new[] { e.ArchivoId }, e.Borrados);
            e.Sistema.Verify(s => s.Update(It.IsAny<Entity>()), Times.Never);
            e.Sistema.Verify(s => s.Delete(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        }

        [Theory]
        [InlineData(Completado)]
        [InlineData(Fallido)]
        public void Plantilla_ajena_no_toca_nada(int estado)
        {
            var e = new Escenario(Ajena, estado);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
        }

        [Theory]
        [InlineData(EnCola)]
        [InlineData(Procesando)]
        public void Estado_no_terminal_no_borra(int estado)
        {
            var e = new Escenario(Principal, estado);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
        }

        [Fact]
        public void Sin_estado_no_borra()
        {
            // Un job sin estado todavia lo puede leer PartirCarga o ponerse en cola (o ser una parte por relanzar).
            var e = new Escenario(Partes, null);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
        }

        [Fact]
        public void Relee_el_estado_y_no_borra_si_ya_no_es_terminal()
        {
            // El evento trajo Completed, pero cuando corre el paso asincrono el job volvio a la cola.
            var e = new Escenario(Principal, Completado);
            e.Leido["sanic_smlt_ij_status"] = new OptionSetValue(EnCola);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
        }

        [Theory]
        [InlineData(Completado)]
        [InlineData(ConErrores)]
        [InlineData(Fallido)]
        public void Prueba_en_seco_terminada_borra_el_archivo(int estado)
        {
            // La carga real es otro job con el archivo subido de nuevo: la prueba en seco no se vuelve a leer.
            var e = new Escenario(Principal, estado, esPrueba: true);

            e.Ejecutar();

            Assert.Equal(new[] { e.ArchivoId }, e.Borrados);
        }

        [Fact]
        public void Prueba_en_seco_en_cola_no_borra()
        {
            var e = new Escenario(Principal, EnCola, esPrueba: true);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
        }

        [Fact]
        public void Trabajo_sin_archivo_no_falla()
        {
            var e = new Escenario(Principal, Completado, conArchivo: false);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
        }

        [Fact]
        public void Si_el_job_ya_no_existe_sigue_con_el_barrido()
        {
            var e = new Escenario(Principal, Completado);
            e.Sistema.Setup(s => s.Retrieve(Job, e.JobId, It.IsAny<ColumnSet>()))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault()));
            var viejo = e.AgregarViejo();

            e.Ejecutar();

            Assert.Equal(new[] { viejo }, e.Borrados);
        }

        [Fact]
        public void Si_falla_el_borrado_no_lanza()
        {
            var e = new Escenario(Principal, Completado);
            e.Sistema.Setup(s => s.Execute(It.Is<OrganizationRequest>(r => r.RequestName == "DeleteFile")))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault()));

            e.Ejecutar();

            Assert.Contains("no se pudo borrar", e.H.TodoElTexto());
        }

        // ---------- barrido ----------

        [Fact]
        public void Barre_los_viejos_que_siguen_con_archivo()
        {
            var e = new Escenario(Ajena, Completado);
            var a = e.AgregarViejo();
            var b = e.AgregarViejo();

            e.Ejecutar();

            Assert.Equal(new[] { a, b }, e.Borrados);
        }

        [Fact]
        public void El_barrido_es_acotado_y_solo_de_las_plantillas_de_CM()
        {
            var e = new Escenario(Principal, Procesando);
            var antes = DateTime.UtcNow;

            e.Ejecutar();

            var q = e.Barrido;
            Assert.NotNull(q);
            Assert.Equal(50, q.TopCount);
            var plantillas = Condicion(q.Criteria, "sanic_smlt_ij_importtemplateid");
            Assert.Equal(ConditionOperator.In, plantillas.Operator);
            Assert.Equal(new[] { Principal, Padres, Partes }.OrderBy(g => g), plantillas.Values.Cast<Guid>().OrderBy(g => g));
            Assert.Equal(ConditionOperator.NotNull, Condicion(q.Criteria, "sanic_smlt_ij_sourcefile_name").Operator);
            var creado = Condicion(q.Criteria, "createdon");
            Assert.Equal(ConditionOperator.LessThan, creado.Operator);
            var corte = (DateTime)creado.Values[0];
            Assert.True(corte <= antes.AddDays(-7).AddMinutes(1) && corte >= antes.AddDays(-7).AddMinutes(-1));
            Assert.Equal(e.JobId, (Guid)Condicion(q.Criteria, "sanic_tbl_smlt_importjobid").Values[0]);
        }

        [Fact]
        public void El_barrido_toma_terminales_o_pruebas_en_seco_sin_estado()
        {
            var e = new Escenario(Principal, Completado);

            e.Ejecutar();

            var o = e.Barrido.Criteria.Filters.Single();
            Assert.Equal(LogicalOperator.Or, o.FilterOperator);
            var terminales = o.Conditions.Single(c => c.AttributeName == "sanic_smlt_ij_status");
            Assert.Equal(ConditionOperator.In, terminales.Operator);
            Assert.Equal(new[] { Completado, ConErrores, Fallido }, terminales.Values.Cast<int>().OrderBy(x => x));
            var prueba = o.Filters.Single();
            Assert.Equal(LogicalOperator.And, prueba.FilterOperator);
            Assert.True((bool)prueba.Conditions.Single(c => c.AttributeName == "sanic_smlt_ij_isdryrun").Values[0]);
            Assert.Equal(ConditionOperator.Null, prueba.Conditions.Single(c => c.AttributeName == "sanic_smlt_ij_status").Operator);
        }

        [Fact]
        public void Un_viejo_sin_id_de_archivo_se_salta()
        {
            var e = new Escenario(Ajena, Completado);
            e.Viejos.Add(new Entity(Job, Guid.NewGuid()));
            var b = e.AgregarViejo();

            e.Ejecutar();

            Assert.Equal(new[] { b }, e.Borrados);
        }

        // ---------- registro y PCI ----------

        [Fact]
        public void Registrado_sincrono_no_hace_nada_ni_frena_a_Smelter()
        {
            var e = new Escenario(Principal, Completado, modo: 0);

            e.Ejecutar();

            Assert.Empty(e.Borrados);
            e.Sistema.Verify(s => s.RetrieveMultiple(It.IsAny<QueryBase>()), Times.Never);
        }

        [Fact]
        public void Sin_plantillas_de_CM_no_hace_nada()
        {
            var e = new Escenario(Principal, Completado);
            e.Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Plantilla))).Returns(new EntityCollection());

            e.Ejecutar();

            Assert.Empty(e.Borrados);
            e.Sistema.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ColumnSet>()), Times.Never);
        }

        [Fact]
        public void Usa_solo_el_servicio_del_sistema()
        {
            var e = new Escenario(Principal, Completado);

            e.Ejecutar();

            e.H.Factory.Verify(f => f.CreateOrganizationService(It.Is<Guid?>(g => g != null)), Times.Never);
        }

        [Fact]
        public void Las_trazas_no_citan_ids_de_archivo()
        {
            var e = new Escenario(Principal, Completado);
            e.AgregarViejo();

            e.Ejecutar();

            Assert.DoesNotContain(e.ArchivoId.ToString(), e.H.TodoElTexto());
            Assert.Contains("archivo borrado", e.H.TodoElTexto());
        }

        private static ConditionExpression Condicion(FilterExpression f, string columna)
        {
            return f.Conditions.Single(c => c.AttributeName == columna);
        }
    }
}
