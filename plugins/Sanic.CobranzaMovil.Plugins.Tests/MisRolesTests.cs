using System;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// La Custom API MisRoles devuelve los perfiles de CM del llamador (directos y por equipo), sin el prefijo
    /// "SR | CM | ", separados por "|" y sin repetir. La identidad de servicio no es un perfil de la app.
    /// Tambien devuelve la version de la solucion de CM, que las apps muestran en pantalla.
    /// </summary>
    public class MisRolesTests
    {
        private const string Mensaje = "sanic_cm_MisRoles";

        private sealed class Escenario
        {
            public readonly Harness H = new Harness(Mensaje, "none", 30);
            public readonly Mock<IOrganizationService> Sistema = new Mock<IOrganizationService>();

            public Escenario(string[] directos, string[] porEquipo)
            {
                H.Factory.Setup(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null))).Returns(Sistema.Object);
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Enlace(q) == "systemuserroles"))).Returns(Filas(directos));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Enlace(q) == "teamroles"))).Returns(Filas(porEquipo));
                ConVersion("1.0.10.9");
            }

            public void ConVersion(string version)
            {
                var filas = new EntityCollection();
                if (version != null)
                {
                    var sol = new Entity("solution", Guid.NewGuid());
                    sol["version"] = version;
                    filas.Entities.Add(sol);
                }
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => EsDeSolucion(q)))).Returns(filas);
            }

            public string Version()
            {
                H.Ejecutar(new MisRoles());
                return (string)H.Outputs["Version"];
            }

            public string Roles()
            {
                H.Ejecutar(new MisRoles());
                return (string)H.Outputs["Roles"];
            }

            private static EntityCollection Filas(string[] nombres)
            {
                var filas = new EntityCollection();
                foreach (var n in nombres)
                {
                    var r = new Entity("role", Guid.NewGuid());
                    r["name"] = n;
                    filas.Entities.Add(r);
                }
                return filas;
            }

            public static bool EsDeSolucion(QueryBase q)
            {
                var qe = q as QueryExpression;
                return qe != null && qe.EntityName == "solution";
            }

            public static string Enlace(QueryBase q)
            {
                var qe = q as QueryExpression;
                return qe == null || qe.LinkEntities.Count == 0 ? null : qe.LinkEntities[0].LinkToEntityName;
            }
        }

        [Fact]
        public void Falla_si_se_registra_en_otro_mensaje()
        {
            var e = new Escenario(new[] { "SR | CM | Admin" }, new string[0]);
            e.H.Context.SetupGet(c => c.MessageName).Returns("Update");
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => e.H.Ejecutar(new MisRoles()));
            Assert.Contains("registro incorrecto", ex.Message);
        }

        [Fact]
        public void Devuelve_el_perfil_de_un_rol_directo_sin_el_prefijo()
        {
            var e = new Escenario(new[] { "SR | CM | BackOffice" }, new string[0]);
            Assert.Equal("BackOffice", e.Roles());
        }

        [Fact]
        public void Devuelve_el_perfil_que_llega_por_equipo()
        {
            var e = new Escenario(new string[0], new[] { "SR | CM | Supervisor" });
            Assert.Equal("Supervisor", e.Roles());
        }

        [Fact]
        public void Un_rol_directo_y_por_equipo_sale_una_sola_vez()
        {
            var e = new Escenario(new[] { "SR | CM | BackOffice" }, new[] { "SR | CM | BackOffice", "SR | CM | Admin" });
            Assert.Equal("Admin|BackOffice", e.Roles());
        }

        [Fact]
        public void Sin_roles_devuelve_cadena_vacia()
        {
            var e = new Escenario(new string[0], new string[0]);
            Assert.Equal("", e.Roles());
        }

        [Fact]
        public void Ignora_los_roles_que_no_son_de_CM()
        {
            var e = new Escenario(
                new[] { "System Administrator", "Basic User", "CM | Admin" },
                new[] { "SR | XX | Admin", "sr | cm | Admin", "SR | CM | Solicitante" });
            Assert.Equal("Solicitante", e.Roles());
        }

        [Fact]
        public void La_identidad_de_servicio_no_es_un_perfil_de_la_app()
        {
            var e = new Escenario(new[] { "SR | CM | Servicio - Cobranza" }, new string[0]);
            Assert.Equal("", e.Roles());

            var mixto = new Escenario(new[] { "SR | CM | Servicio - Cobranza" }, new[] { "SR | CM | Admin" });
            Assert.Equal("Admin", mixto.Roles());
        }

        [Fact]
        public void Consulta_directos_y_por_equipo_del_llamador_con_el_servicio_del_sistema()
        {
            var e = new Escenario(new[] { "SR | CM | Admin" }, new string[0]);
            e.Roles();

            var consultas = e.Sistema.Invocations
                .Where(i => i.Method.Name == "RetrieveMultiple")
                .Select(i => (QueryExpression)i.Arguments[0])
                .Where(q => !Escenario.EsDeSolucion(q))
                .ToList();
            Assert.Equal(2, consultas.Count);
            Assert.Contains(consultas, q => Escenario.Enlace(q) == "systemuserroles");
            Assert.Contains(consultas, q => Escenario.Enlace(q) == "teamroles");
            Assert.All(consultas, q =>
            {
                var conds = q.LinkEntities.SelectMany(l => new[] { l }.Concat(l.LinkEntities))
                    .SelectMany(l => l.LinkCriteria.Conditions).ToList();
                Assert.Contains(conds, c => c.AttributeName == "systemuserid" && (Guid)c.Values[0] == Harness.UsuarioLlamador);
            });
            Assert.Equal(0, e.H.ConsultasAlServicio());
        }

        [Fact]
        public void No_traza_los_nombres_de_los_roles()
        {
            var e = new Escenario(new[] { "SR | CM | BackOffice" }, new[] { "SR | CM | Admin" });
            e.Roles();
            Assert.DoesNotContain("BackOffice", e.H.TodoElTexto());
            Assert.DoesNotContain("SR | CM", e.H.TodoElTexto());
        }

        [Fact]
        public void Devuelve_la_version_de_la_solucion_de_CM()
        {
            var e = new Escenario(new[] { "SR | CM | Admin" }, new string[0]);
            e.ConVersion("1.2.3.4");
            Assert.Equal("1.2.3.4", e.Version());
        }

        [Fact]
        public void Busca_la_solucion_por_su_nombre_unico_con_el_servicio_del_sistema()
        {
            var e = new Escenario(new[] { "SR | CM | Admin" }, new string[0]);
            e.Version();

            var q = e.Sistema.Invocations
                .Where(i => i.Method.Name == "RetrieveMultiple")
                .Select(i => (QueryExpression)i.Arguments[0])
                .Single(Escenario.EsDeSolucion);
            Assert.Contains(q.Criteria.Conditions, c => c.AttributeName == "uniquename" && (string)c.Values[0] == "cm_core_cobranzamovil");
            Assert.Contains("version", q.ColumnSet.Columns);
            Assert.Equal(0, e.H.ConsultasAlServicio());
        }

        [Fact]
        public void Version_vacia_si_no_encuentra_la_solucion()
        {
            var e = new Escenario(new[] { "SR | CM | Admin" }, new string[0]);
            e.ConVersion(null);
            Assert.Equal("", e.Version());
            Assert.Equal("Admin", (string)e.H.Outputs["Roles"]);
        }

        [Fact]
        public void Version_vacia_si_la_consulta_de_la_solucion_falla_y_sigue_devolviendo_los_roles()
        {
            var e = new Escenario(new[] { "SR | CM | BackOffice" }, new string[0]);
            e.Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Escenario.EsDeSolucion(q))))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault(), "sin acceso"));
            Assert.Equal("", e.Version());
            Assert.Equal("BackOffice", (string)e.H.Outputs["Roles"]);
        }

        [Fact]
        public void Devuelve_roles_y_version_en_la_misma_llamada()
        {
            var e = new Escenario(new[] { "SR | CM | Supervisor" }, new string[0]);
            e.ConVersion("2.0.0.1");
            e.H.Ejecutar(new MisRoles());
            Assert.Equal("Supervisor", (string)e.H.Outputs["Roles"]);
            Assert.Equal("2.0.0.1", (string)e.H.Outputs["Version"]);
        }
    }
}
