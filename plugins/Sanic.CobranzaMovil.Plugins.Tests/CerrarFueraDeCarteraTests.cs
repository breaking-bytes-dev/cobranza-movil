using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// APP-48: la Custom API CerrarFueraDeCartera cierra en el servidor, por lotes, las Diarias activas cuya "Ultima carga"
    /// es anterior al dia de la ultima carga diaria. Solo el Admin (o Servicio, o sin roles de CM). No cierra si el corte
    /// que vio la app no es el del servidor ni si hay mas de las que el Admin confirmo.
    /// </summary>
    public class CerrarFueraDeCarteraTests
    {
        private const string Mensaje = "sanic_cm_CerrarFueraDeCartera";
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string CargaDiaria = "sanic_cm_tbl_cargadiaria";
        private const string Admin = "SR | CM | Admin";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string BackOffice = "SR | CM | BackOffice";
        private static readonly DateTime Dia = new DateTime(2026, 10, 25);

        private sealed class Escenario
        {
            public readonly Harness H = new Harness(Mensaje, "none", 30);
            public readonly Mock<IOrganizationService> Sistema = new Mock<IOrganizationService>();
            public readonly List<Entity> Actualizadas = new List<Entity>();
            public QueryExpression ConsultaDeFilas;

            public Escenario(string[] roles, int pendientes, int filasDelLote, DateTime? ultimaCarga)
            {
                H.Factory.Setup(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null))).Returns(Sistema.Object);
                var filasRoles = new EntityCollection();
                foreach (var n in roles)
                {
                    var r = new Entity("role", Guid.NewGuid());
                    r["name"] = n;
                    filasRoles.Entities.Add(r);
                }
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == "role"))).Returns(filasRoles);
                var cargas = new EntityCollection();
                if (ultimaCarga.HasValue)
                {
                    var c = new Entity(CargaDiaria, Guid.NewGuid());
                    c["sanic_cm_cd_fechacarga"] = ultimaCarga.Value;
                    cargas.Entities.Add(c);
                }
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == CargaDiaria))).Returns(cargas);

                var conteo = new Entity(Asignacion);
                conteo["n"] = new AliasedValue(Asignacion, "sanic_cm_tbl_asignacionid", pendientes);
                H.Service.Setup(s => s.RetrieveMultiple(It.IsAny<FetchExpression>())).Returns(new EntityCollection(new[] { conteo }));
                H.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Asignacion)))
                    .Callback<QueryBase>(q => ConsultaDeFilas = (QueryExpression)q)
                    .Returns(new EntityCollection(Enumerable.Range(0, filasDelLote).Select(i => new Entity(Asignacion, Guid.NewGuid())).ToList()));
                H.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e => Actualizadas.Add(e));

                H.Inputs["Corte"] = "2026-10-25";
                H.Inputs["Esperadas"] = pendientes;
            }

            public void Ejecutar() { H.Ejecutar(new CerrarFueraDeCartera()); }

            public InvalidPluginExecutionException Rechaza()
            {
                return Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar());
            }

            private static string Tabla(QueryBase q)
            {
                var qe = q as QueryExpression;
                return qe == null ? null : qe.EntityName;
            }
        }

        [Fact]
        public void Falla_si_se_registra_en_otro_mensaje()
        {
            var e = new Escenario(new[] { Admin }, 5, 5, Dia);
            e.H.Context.SetupGet(c => c.MessageName).Returns("Update");
            Assert.Contains("registro incorrecto", e.Rechaza().Message);
        }

        [Theory]
        [InlineData(Supervisor)]
        [InlineData(BackOffice)]
        public void Solo_el_Admin_puede_cerrar(string rol)
        {
            var e = new Escenario(new[] { rol }, 5, 5, Dia);
            Assert.Contains("solo el Admin", e.Rechaza().Message);
            Assert.Empty(e.Actualizadas);
        }

        [Fact]
        public void Cierra_el_lote_con_motivo_y_fecha_y_devuelve_las_restantes()
        {
            var e = new Escenario(new[] { Admin }, 600, 250, Dia);
            e.Ejecutar();
            Assert.Equal(250, e.Actualizadas.Count);
            Assert.All(e.Actualizadas, a =>
            {
                Assert.Equal(false, a["sanic_cm_as_estado"]);
                Assert.Equal("fuera de cartera", a["sanic_cm_as_motivo"]);
                Assert.IsType<DateTime>(a["sanic_cm_as_fechacierre"]);
                Assert.Equal(3, a.Attributes.Count);
            });
            Assert.Equal(250, e.H.Outputs["Cerradas"]);
            Assert.Equal(350, e.H.Outputs["Restantes"]);
            Assert.Equal("2026-10-25", e.H.Outputs["Corte"]);
            Assert.Equal(250, e.ConsultaDeFilas.TopCount);
        }

        [Fact]
        public void Busca_Diarias_activas_con_ultima_carga_anterior_al_corte()
        {
            var e = new Escenario(new[] { Admin }, 3, 3, Dia);
            e.Ejecutar();
            var c = e.ConsultaDeFilas.Criteria.Conditions;
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_tipo" && x.Operator == ConditionOperator.Equal && (int)x.Values[0] == 1);
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_estado" && (bool)x.Values[0]);
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_ultimacarga" && x.Operator == ConditionOperator.LessThan && (DateTime)x.Values[0] == Dia);
        }

        [Fact]
        public void Respeta_el_lote_pedido()
        {
            var e = new Escenario(new[] { Admin }, 100, 40, Dia);
            e.H.Inputs["Lote"] = 40;
            e.Ejecutar();
            Assert.Equal(40, e.ConsultaDeFilas.TopCount);
            Assert.Equal(60, e.H.Outputs["Restantes"]);
        }

        [Fact]
        public void Lote_cero_usa_el_de_por_defecto()
        {
            // Dataverse manda 0 en un parametro entero opcional que la app no paso.
            var e = new Escenario(new[] { Admin }, 600, 250, Dia);
            e.H.Inputs["Lote"] = 0;
            e.Ejecutar();
            Assert.Equal(250, e.ConsultaDeFilas.TopCount);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(501)]
        public void Rechaza_un_lote_fuera_de_rango(int lote)
        {
            var e = new Escenario(new[] { Admin }, 10, 10, Dia);
            e.H.Inputs["Lote"] = lote;
            Assert.Contains("el lote", e.Rechaza().Message);
            Assert.Empty(e.Actualizadas);
        }

        [Fact]
        public void No_cierra_si_llego_otra_carga()
        {
            var e = new Escenario(new[] { Admin }, 10, 10, Dia.AddDays(1));
            Assert.Contains("la ultima carga cambio", e.Rechaza().Message);
            Assert.Empty(e.Actualizadas);
        }

        [Fact]
        public void No_cierra_si_hay_mas_de_las_confirmadas()
        {
            var e = new Escenario(new[] { Admin }, 120, 120, Dia);
            e.H.Inputs["Esperadas"] = 100;
            Assert.Contains("mas de las 100", e.Rechaza().Message);
            Assert.Empty(e.Actualizadas);
        }

        [Fact]
        public void Solo_contar_no_escribe()
        {
            var e = new Escenario(new[] { Admin }, 42, 42, Dia);
            e.H.Inputs["SoloContar"] = true;
            e.Ejecutar();
            Assert.Empty(e.Actualizadas);
            Assert.Equal(0, e.H.Outputs["Cerradas"]);
            Assert.Equal(42, e.H.Outputs["Restantes"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("25/10/2026")]
        public void Exige_el_corte_en_formato_dia(string corte)
        {
            var e = new Escenario(new[] { Admin }, 1, 1, Dia);
            e.H.Inputs["Corte"] = corte;
            Assert.Contains("falta el corte", e.Rechaza().Message);
        }

        [Fact]
        public void Sin_cargas_diarias_no_cierra()
        {
            var e = new Escenario(new[] { Admin }, 1, 1, null);
            Assert.Contains("no hay ninguna carga", e.Rechaza().Message);
        }

        [Fact]
        public void Si_una_fila_falla_corta_con_un_mensaje_sin_valores()
        {
            var e = new Escenario(new[] { Admin }, 3, 3, Dia);
            e.H.Service.Setup(s => s.Update(It.IsAny<Entity>()))
                .Throws(new System.ServiceModel.FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -1 }));
            Assert.Contains("no se pudo cerrar el lote", e.Rechaza().Message);
        }
    }
}
