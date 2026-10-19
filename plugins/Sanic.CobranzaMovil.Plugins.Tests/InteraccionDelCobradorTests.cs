using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// DAT-13 + SEC-08: toda interaccion nace con cuenta y agente; si la crea un cobrador, el agente es el vinculado a
    /// su usuario (lo pone el servidor, no la app) y la interaccion nace Pendiente de grabar en el CORE.
    /// </summary>
    public class InteraccionDelCobradorTests
    {
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string InCuenta = "sanic_cm_in_cuentaid";
        private const string InAgente = "sanic_cm_in_agente";
        private const string InNombreAgente = "sanic_cm_in_nombreagente";
        private const string Estado = "sanic_cm_in_estadograbacion";
        private const string Codigo = "sanic_cm_in_codigograbacion";
        private const string Fecha = "sanic_cm_in_fechagrabacion";
        private const string GrabadoPor = "sanic_cm_in_grabadopor";
        private const int Pendiente = 1, Grabado = 2;

        private const string Localizador = "SR | CM | Localizador";
        private const string Admin = "SR | CM | Admin";

        private static Entity Nueva(bool conCuenta, Guid? agente)
        {
            var t = new Entity(Interaccion);
            if (conCuenta) t[InCuenta] = new EntityReference(Cuenta, Guid.NewGuid());
            if (agente.HasValue) t[InAgente] = new EntityReference(Agente, agente.Value);
            return t;
        }

        private static Entity AgenteDe(Guid id, string nombre)
        {
            var a = new Entity(Agente, id);
            a["sanic_cm_ag_nombre"] = nombre;
            return a;
        }

        /// <summary>Roles para las consultas sobre "role" y agentes vinculados para la consulta sobre el agente.</summary>
        private static Harness Crear(Entity target, string[] roles, params Entity[] agentesDelUsuario)
        {
            var h = new Harness("Create", Interaccion).ConTarget(target);
            var filasRoles = new EntityCollection();
            foreach (var n in roles)
            {
                var r = new Entity("role", Guid.NewGuid());
                r["name"] = n;
                filasRoles.Entities.Add(r);
            }
            var filasAgentes = new EntityCollection(new System.Collections.Generic.List<Entity>(agentesDelUsuario));
            h.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                .Returns<QueryBase>(q => ((QueryExpression)q).EntityName == Agente ? filasAgentes
                    : ((QueryExpression)q).EntityName == Asignacion ? new EntityCollection(new System.Collections.Generic.List<Entity> { new Entity(Asignacion, Guid.NewGuid()) })
                    : filasRoles);
            return h;
        }

        private static QueryExpression ConsultaDeAgente(Harness h)
        {
            foreach (var inv in h.Service.Invocations)
            {
                if (inv.Method.Name != "RetrieveMultiple") continue;
                var q = inv.Arguments[0] as QueryExpression;
                if (q != null && q.EntityName == Agente) return q;
            }
            return null;
        }

        private static InvalidPluginExecutionException Rechaza(Harness h)
        {
            return Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new InteraccionDelCobrador()));
        }

        // ------------------------------------------------------------------ H3: solo cuentas de su cartera

        private static void FueraDeCartera(Harness h)
        {
            h.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => ((QueryExpression)q).EntityName == Asignacion)))
                .Returns(new EntityCollection());
        }

        private static QueryExpression ConsultaDeCartera(Harness h)
        {
            foreach (var inv in h.Service.Invocations)
            {
                if (inv.Method.Name != "RetrieveMultiple") continue;
                var q = inv.Arguments[0] as QueryExpression;
                if (q != null && q.EntityName == Asignacion) return q;
            }
            return null;
        }

        [Fact]
        public void H3_el_cobrador_no_registra_visitas_en_una_cuenta_que_no_es_de_su_cartera()
        {
            var agente = Guid.NewGuid();
            var t = Nueva(true, agente);
            var h = Crear(t, new[] { Localizador }, AgenteDe(agente, "QA"));
            FueraDeCartera(h);
            var ex = Rechaza(h);
            Assert.Contains("cartera", ex.Message);
            Assert.DoesNotContain(t.GetAttributeValue<EntityReference>(InCuenta).Id.ToString(), ex.Message + h.TodoElTexto());
        }

        [Fact]
        public void H3_la_cartera_es_una_asignacion_activa_de_esa_cuenta_cuyo_dueno_es_el_cobrador()
        {
            var agente = Guid.NewGuid();
            var t = Nueva(true, agente);
            var h = Crear(t, new[] { Localizador }, AgenteDe(agente, "QA"));
            h.Ejecutar(new InteraccionDelCobrador());

            var q = ConsultaDeCartera(h);
            Assert.NotNull(q);
            var c = q.Criteria.Conditions;
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_cuentaid" && x.Operator == ConditionOperator.Equal
                && (Guid)x.Values[0] == t.GetAttributeValue<EntityReference>(InCuenta).Id);
            Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_estado" && (bool)x.Values[0]);
            // Dueno = el usuario: una cuenta de la "Cartera sin cobrador" (dueno el equipo) no es de nadie.
            Assert.Contains(c, x => x.AttributeName == "ownerid" && (Guid)x.Values[0] == Harness.UsuarioLlamador);
            Assert.Equal(1, q.TopCount);
        }

        [Fact]
        public void H3_el_Admin_no_necesita_cartera()
        {
            var h = Crear(Nueva(true, Guid.NewGuid()), new[] { Admin });
            FueraDeCartera(h);
            h.Ejecutar(new InteraccionDelCobrador());
            Assert.Null(ConsultaDeCartera(h));
        }

        [Fact]
        public void Falla_si_se_registra_en_Update()
        {
            var h = new Harness("Update", Interaccion).ConTarget(Nueva(true, Guid.NewGuid())).RolesDelLlamador(Localizador);
            Assert.Contains("registro incorrecto", Rechaza(h).Message);
        }

        [Fact]
        public void Sin_cuenta_se_rechaza_sin_consultar_nada()
        {
            var h = Crear(Nueva(false, Guid.NewGuid()), new[] { Localizador });
            Assert.Contains("cuenta", Rechaza(h).Message);
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void El_agente_del_cobrador_lo_pone_el_servidor()
        {
            var suyo = Guid.NewGuid();
            var t = Nueva(true, Guid.NewGuid()); // la app mando otro agente
            var h = Crear(t, new[] { Localizador }, AgenteDe(suyo, "QA Agente"));

            h.Ejecutar(new InteraccionDelCobrador());

            Assert.Equal(suyo, t.GetAttributeValue<EntityReference>(InAgente).Id);
            Assert.Equal(Agente, t.GetAttributeValue<EntityReference>(InAgente).LogicalName);
            Assert.Equal("QA Agente", t.GetAttributeValue<string>(InNombreAgente));
        }

        [Fact]
        public void Busca_el_agente_vinculado_al_usuario_que_llama()
        {
            var h = Crear(Nueva(true, null), new[] { Localizador }, AgenteDe(Guid.NewGuid(), "QA"));
            h.Ejecutar(new InteraccionDelCobrador());

            var q = ConsultaDeAgente(h);
            Assert.NotNull(q);
            var c = q.Criteria.Conditions[0];
            Assert.Equal("sanic_cm_ag_usuarioid", c.AttributeName);
            Assert.Equal(Harness.UsuarioLlamador, c.Values[0]);
            Assert.Equal(2, q.TopCount);
        }

        [Fact]
        public void Un_cobrador_sin_agente_vinculado_se_rechaza()
        {
            Assert.Contains("agente", Rechaza(Crear(Nueva(true, null), new[] { Localizador })).Message);
        }

        [Fact]
        public void Con_dos_agentes_vale_el_enviado_si_es_uno_de_ellos()
        {
            var a = Guid.NewGuid();
            var t = Nueva(true, a);
            Crear(t, new[] { Localizador }, AgenteDe(Guid.NewGuid(), "B"), AgenteDe(a, "A")).Ejecutar(new InteraccionDelCobrador());
            Assert.Equal(a, t.GetAttributeValue<EntityReference>(InAgente).Id);

            Assert.Contains("mas de un agente", Rechaza(Crear(Nueva(true, Guid.NewGuid()), new[] { Localizador },
                AgenteDe(Guid.NewGuid(), "B"), AgenteDe(Guid.NewGuid(), "A"))).Message);
        }

        [Fact]
        public void La_captura_nace_pendiente_de_grabar()
        {
            var t = Nueva(true, null);
            t[Estado] = new OptionSetValue(Grabado);
            t[Codigo] = "TRX";
            t[Fecha] = DateTime.UtcNow;
            t[GrabadoPor] = new EntityReference("systemuser", Guid.NewGuid());
            Crear(t, new[] { Localizador }, AgenteDe(Guid.NewGuid(), "QA")).Ejecutar(new InteraccionDelCobrador());

            Assert.Equal(Pendiente, t.GetAttributeValue<OptionSetValue>(Estado).Value);
            Assert.False(t.Contains(Codigo));
            Assert.False(t.Contains(Fecha));
            Assert.False(t.Contains(GrabadoPor));
        }

        [Fact]
        public void El_Admin_crea_con_el_agente_que_elige()
        {
            var elegido = Guid.NewGuid();
            var t = Nueva(true, elegido);
            var h = Crear(t, new[] { Localizador, Admin });
            h.Ejecutar(new InteraccionDelCobrador());
            Assert.Equal(elegido, t.GetAttributeValue<EntityReference>(InAgente).Id);
            Assert.Null(ConsultaDeAgente(h));
        }

        [Fact]
        public void Sin_cobrador_el_agente_es_obligatorio()
        {
            Assert.Contains("agente", Rechaza(Crear(Nueva(true, null), new[] { Admin })).Message);
            Assert.Contains("agente", Rechaza(Crear(Nueva(true, null), new string[0])).Message);
        }

        [Fact]
        public void No_traza_el_nombre_del_agente()
        {
            var t = Nueva(true, null);
            var h = Crear(t, new[] { Localizador }, AgenteDe(Guid.NewGuid(), "Nombre Privado"));
            h.Ejecutar(new InteraccionDelCobrador());
            Assert.DoesNotContain("Nombre Privado", h.TodoElTexto());
        }
    }
}
