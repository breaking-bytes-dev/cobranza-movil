using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// La Custom API MisRoles devuelve los perfiles de CM del llamador (directos y por equipo), sin el prefijo
    /// "SR | CM | ", separados por "|" y sin repetir. La identidad de servicio no es un perfil de la app.
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
    }
}
