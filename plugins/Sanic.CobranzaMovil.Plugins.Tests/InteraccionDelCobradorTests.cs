using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Toda interaccion nace con cuenta y agente; si la crea un localizador, el agente es el vinculado a su usuario
    /// (lo pone el servidor, no la app) y la interaccion nace Pendiente de grabar en el CORE.
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
            a["sanic_cm_ag_activo"] = true;
            return a;
        }

        private static Entity Inactivo(Entity agente)
        {
            agente["sanic_cm_ag_activo"] = false;
            return agente;
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

        // ------------------------------------------------------------------ solo cuentas de su cartera

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
            // Dueno = el usuario: una cuenta cuyo dueno es el equipo de cartera sin asignar no es de nadie.
            Assert.Contains(c, x => x.AttributeName == "ownerid" && (Guid)x.Values[0] == Harness.UsuarioLlamador);
            Assert.Equal(1, q.TopCount);
        }

        // ------------------------------------------------------------------ cartera con tolerancia por fecha
        // El localizador captura sin senal en una cuenta suya que luego se reasigno. Si la captura es de hasta 48 h atras
        // (y no futura mas de 5 min) y a esa fecha tenia una asignacion activa suya segun la auditoria, entra.

        private static readonly DateTime Ahora = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        private const string InFechaCaptura = "sanic_cm_in_fechacaptura";
        private static readonly Guid Pedro = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Ana = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid Sistema = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

        private sealed class Cambio
        {
            public DateTime Cuando;
            public object Antes, Despues;
        }

        private static Cambio DuenoCambia(TimeSpan haceCuanto, Guid antes, Guid despues)
        {
            return new Cambio { Cuando = Ahora - haceCuanto, Antes = new EntityReference("systemuser", antes), Despues = new EntityReference("systemuser", despues) };
        }

        private static Cambio EstadoCambia(TimeSpan haceCuanto, bool antes, bool despues)
        {
            return new Cambio { Cuando = Ahora - haceCuanto, Antes = antes, Despues = despues };
        }

        private sealed class HistoriaAsignacion
        {
            public Guid Id = Guid.NewGuid();
            public DateTime CreadaEn = Ahora - TimeSpan.FromDays(30);
            public Guid DuenoActual;
            public bool ActivaAhora = true;
            public Cambio[] Duenos = new Cambio[0];
            public Cambio[] Estados = new Cambio[0];
        }

        private static AuditDetailCollection Auditoria(string columna, Cambio[] cambios)
        {
            var c = new AuditDetailCollection();
            foreach (var x in cambios)
            {
                var antes = new Entity(Asignacion);
                antes[columna] = x.Antes;
                var despues = new Entity(Asignacion);
                despues[columna] = x.Despues;
                var registro = new Entity("audit", Guid.NewGuid());
                registro["createdon"] = x.Cuando;
                registro["action"] = new OptionSetValue(2);
                c.AuditDetails.Add(new AttributeAuditDetail { AuditRecord = registro, OldValue = antes, NewValue = despues });
            }
            return c;
        }

        private static bool TieneCondicionDeDueno(QueryExpression q)
        {
            return q.Criteria.Conditions.Any(x => x.AttributeName == "ownerid");
        }

        /// <summary>
        /// El localizador (UsuarioLlamador) ya no es dueno de ninguna asignacion activa y la consulta historica (sin filtro
        /// de dueno) devuelve estas asignaciones con su auditoria.
        /// </summary>
        private static Harness Capturada(DateTime? captura, params HistoriaAsignacion[] asignaciones)
        {
            var agente = Guid.NewGuid();
            var t = Nueva(true, agente);
            if (captura.HasValue) t[InFechaCaptura] = captura.Value;
            var h = Crear(t, new[] { Localizador }, AgenteDe(agente, "QA"));
            var filas = new EntityCollection(asignaciones.Select(a =>
            {
                var e = new Entity(Asignacion, a.Id);
                e["createdon"] = a.CreadaEn;
                e["ownerid"] = new EntityReference("systemuser", a.DuenoActual);
                e["sanic_cm_as_estado"] = a.ActivaAhora;
                return e;
            }).ToList());
            h.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => ((QueryExpression)q).EntityName == Asignacion)))
                .Returns<QueryBase>(q => TieneCondicionDeDueno((QueryExpression)q) ? new EntityCollection() : filas);
            h.Service.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
                .Returns<OrganizationRequest>(r =>
                {
                    var req = (RetrieveAttributeChangeHistoryRequest)r;
                    var a = asignaciones.Single(x => x.Id == req.Target.Id);
                    var coleccion = req.AttributeLogicalName == "ownerid" ? Auditoria("ownerid", a.Duenos)
                        : req.AttributeLogicalName == "sanic_cm_as_estado" ? Auditoria("sanic_cm_as_estado", a.Estados)
                        : new AuditDetailCollection();
                    var resp = new RetrieveAttributeChangeHistoryResponse();
                    resp.Results["AuditDetailCollection"] = coleccion;
                    return resp;
                });
            return h;
        }

        private static void Ejecuta(Harness h)
        {
            h.Ejecutar(new InteraccionDelCobrador(() => Ahora));
        }

        private static InvalidPluginExecutionException Rechazada(Harness h)
        {
            return Assert.Throws<InvalidPluginExecutionException>(() => Ejecuta(h));
        }

        private static int Auditorias(Harness h)
        {
            return h.Service.Invocations.Count(i => i.Method.Name == "Execute");
        }

        private static HistoriaAsignacion ReasignadaAPedroHace(TimeSpan hace)
        {
            return new HistoriaAsignacion
            {
                DuenoActual = Pedro,
                Duenos = new[] { DuenoCambia(hace, Harness.UsuarioLlamador, Pedro) },
            };
        }

        [Fact]
        public void Tolerancia_la_cuenta_era_suya_al_capturar_y_se_la_reasignaron_despues_entra()
        {
            var h = Capturada(Ahora - TimeSpan.FromHours(3), ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            Ejecuta(h);
            Assert.Contains("tolerancia", h.TodoElTexto());
        }

        [Fact]
        public void Tolerancia_la_captura_posterior_a_la_reasignacion_se_rechaza()
        {
            var h = Capturada(Ahora - TimeSpan.FromMinutes(30), ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            Assert.Contains("cartera", Rechazada(h).Message);
        }

        [Fact]
        public void Tolerancia_captura_de_mas_de_48_horas_se_rechaza_sin_mirar_la_auditoria()
        {
            var h = Capturada(Ahora - TimeSpan.FromHours(49), ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            Assert.Contains("cartera", Rechazada(h).Message);
            Assert.Equal(0, Auditorias(h));
        }

        [Fact]
        public void Tolerancia_el_limite_de_48_horas_es_inclusivo()
        {
            Ejecuta(Capturada(Ahora - TimeSpan.FromHours(48), ReasignadaAPedroHace(TimeSpan.FromHours(1))));
        }

        [Fact]
        public void Tolerancia_fecha_futura_mas_alla_del_margen_de_reloj_se_rechaza_sin_mirar_la_auditoria()
        {
            var h = Capturada(Ahora + TimeSpan.FromMinutes(6), ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            Assert.Contains("cartera", Rechazada(h).Message);
            Assert.Equal(0, Auditorias(h));
        }

        [Fact]
        public void Tolerancia_hasta_5_minutos_de_reloj_adelantado_se_evalua_pero_si_ya_no_era_suya_se_rechaza()
        {
            var h = Capturada(Ahora + TimeSpan.FromMinutes(4), ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            Assert.Contains("cartera", Rechazada(h).Message);
            Assert.True(Auditorias(h) > 0);
        }

        [Fact]
        public void Tolerancia_sin_fecha_de_captura_rige_la_regla_estricta()
        {
            var h = Capturada(null, ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            Assert.Contains("cartera", Rechazada(h).Message);
            Assert.Equal(0, Auditorias(h));
        }

        [Fact]
        public void Tolerancia_una_cuenta_que_nunca_fue_suya_se_rechaza()
        {
            var nunca = new HistoriaAsignacion
            {
                DuenoActual = Pedro,
                Duenos = new[] { DuenoCambia(TimeSpan.FromHours(1), Sistema, Pedro) },
            };
            Assert.Contains("cartera", Rechazada(Capturada(Ahora - TimeSpan.FromHours(3), nunca)).Message);
        }

        [Fact]
        public void Tolerancia_una_cuenta_sin_asignaciones_se_rechaza()
        {
            Assert.Contains("cartera", Rechazada(Capturada(Ahora - TimeSpan.FromHours(3))).Message);
        }

        [Fact]
        public void Tolerancia_la_asignacion_que_siempre_fue_de_otro_sin_cambios_de_dueno_se_rechaza()
        {
            var siempreDePedro = new HistoriaAsignacion { DuenoActual = Pedro };
            Assert.Contains("cartera", Rechazada(Capturada(Ahora - TimeSpan.FromHours(3), siempreDePedro)).Message);
        }

        [Fact]
        public void Tolerancia_el_dueno_actual_entra_sin_mirar_la_auditoria()
        {
            var agente = Guid.NewGuid();
            var t = Nueva(true, agente);
            t[InFechaCaptura] = Ahora - TimeSpan.FromHours(100); // fuera de ventana: da igual, es el dueno actual
            var h = Crear(t, new[] { Localizador }, AgenteDe(agente, "QA"));
            Ejecuta(h);
            Assert.Equal(0, Auditorias(h));
        }

        [Fact]
        public void Tolerancia_reasignada_dos_veces_vale_el_dueno_a_la_fecha_de_captura()
        {
            // Juan -> Pedro (hace 5 h) -> Ana (hace 1 h)
            Func<DateTime, Harness> con = captura => Capturada(captura, new HistoriaAsignacion
            {
                DuenoActual = Ana,
                Duenos = new[]
                {
                    DuenoCambia(TimeSpan.FromHours(1), Pedro, Ana),
                    DuenoCambia(TimeSpan.FromHours(5), Harness.UsuarioLlamador, Pedro),
                },
            });
            Ejecuta(con(Ahora - TimeSpan.FromHours(8)));                                  // era de Juan
            Assert.Contains("cartera", Rechazada(con(Ahora - TimeSpan.FromHours(3))).Message);   // era de Pedro
            Assert.Contains("cartera", Rechazada(con(Ahora - TimeSpan.FromMinutes(30))).Message); // ya era de Ana
        }

        [Fact]
        public void Tolerancia_el_tramo_intermedio_de_Juan_entra_y_el_anterior_de_Pedro_no()
        {
            // Pedro -> Juan (hace 4 h) -> Ana (hace 1 h): la captura de hace 2 h cae en el tramo de Juan.
            Func<DateTime, Harness> con = captura => Capturada(captura, new HistoriaAsignacion
            {
                DuenoActual = Ana,
                Duenos = new[]
                {
                    DuenoCambia(TimeSpan.FromHours(1), Harness.UsuarioLlamador, Ana),
                    DuenoCambia(TimeSpan.FromHours(4), Pedro, Harness.UsuarioLlamador),
                },
            });
            Ejecuta(con(Ahora - TimeSpan.FromHours(2)));
            Assert.Contains("cartera", Rechazada(con(Ahora - TimeSpan.FromHours(6))).Message);
        }

        [Fact]
        public void Tolerancia_la_asignacion_cerrada_despues_de_la_captura_entra()
        {
            var cerrada = new HistoriaAsignacion
            {
                DuenoActual = Harness.UsuarioLlamador,
                ActivaAhora = false,
                Estados = new[] { EstadoCambia(TimeSpan.FromHours(1), true, false) },
            };
            Ejecuta(Capturada(Ahora - TimeSpan.FromHours(3), cerrada));
        }

        [Fact]
        public void Tolerancia_la_asignacion_cerrada_antes_de_la_captura_se_rechaza()
        {
            var cerrada = new HistoriaAsignacion
            {
                DuenoActual = Harness.UsuarioLlamador,
                ActivaAhora = false,
                Estados = new[] { EstadoCambia(TimeSpan.FromHours(5), true, false) },
            };
            Assert.Contains("cartera", Rechazada(Capturada(Ahora - TimeSpan.FromHours(3), cerrada)).Message);
        }

        [Fact]
        public void Tolerancia_cerrada_siempre_y_sin_historial_de_estado_se_rechaza()
        {
            var cerrada = new HistoriaAsignacion { DuenoActual = Harness.UsuarioLlamador, ActivaAhora = false };
            Assert.Contains("cartera", Rechazada(Capturada(Ahora - TimeSpan.FromHours(3), cerrada)).Message);
        }

        [Fact]
        public void Tolerancia_una_asignacion_creada_despues_de_la_captura_no_cuenta()
        {
            var nueva = ReasignadaAPedroHace(TimeSpan.FromMinutes(10));
            nueva.CreadaEn = Ahora - TimeSpan.FromHours(1); // nacio despues de la captura, aunque su primer dueno fuera Juan
            Assert.Contains("cartera", Rechazada(Capturada(Ahora - TimeSpan.FromHours(3), nueva)).Message);
        }

        [Fact]
        public void Tolerancia_alcanza_con_que_una_de_las_asignaciones_de_la_cuenta_fuera_suya()
        {
            var deOtro = new HistoriaAsignacion { DuenoActual = Pedro };
            Ejecuta(Capturada(Ahora - TimeSpan.FromHours(3), deOtro, ReasignadaAPedroHace(TimeSpan.FromHours(1))));
        }

        [Fact]
        public void Tolerancia_si_no_se_puede_leer_la_auditoria_falla_cerrado()
        {
            var h = Capturada(Ahora - TimeSpan.FromHours(3), ReasignadaAPedroHace(TimeSpan.FromHours(1)));
            h.Service.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
                .Throws(new System.ServiceModel.FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = unchecked((int)0x80040220) }));
            Assert.Contains("cartera", Rechazada(h).Message);
        }

        [Fact]
        public void Tolerancia_consulta_la_auditoria_de_la_asignacion_de_la_cuenta_con_el_servicio_del_sistema_y_no_traza_valores()
        {
            var a = ReasignadaAPedroHace(TimeSpan.FromHours(1));
            var h = Capturada(Ahora - TimeSpan.FromHours(3), a);
            Ejecuta(h);

            var pedidos = h.Service.Invocations.Where(i => i.Method.Name == "Execute")
                .Select(i => (RetrieveAttributeChangeHistoryRequest)i.Arguments[0]).ToList();
            Assert.NotEmpty(pedidos);
            Assert.All(pedidos, p => { Assert.Equal(a.Id, p.Target.Id); Assert.Equal(Asignacion, p.Target.LogicalName); });
            Assert.Contains(pedidos, p => p.AttributeLogicalName == "ownerid");

            var historica = h.Service.Invocations.Where(i => i.Method.Name == "RetrieveMultiple")
                .Select(i => i.Arguments[0] as QueryExpression).Where(q => q != null && q.EntityName == Asignacion && !TieneCondicionDeDueno(q)).ToList();
            Assert.Single(historica);
            Assert.Contains(historica[0].Criteria.Conditions, x => x.AttributeName == "sanic_cm_as_cuentaid");
            Assert.DoesNotContain(Pedro.ToString(), h.TodoElTexto());
            Assert.DoesNotContain(Harness.UsuarioLlamador.ToString(), h.TodoElTexto());
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
            Assert.Contains("no está vinculado a ningún localizador", Rechaza(Crear(Nueva(true, null), new[] { Localizador })).Message);
        }

        [Fact]
        public void Con_dos_agentes_vale_el_enviado_si_es_uno_de_ellos()
        {
            var a = Guid.NewGuid();
            var t = Nueva(true, a);
            Crear(t, new[] { Localizador }, AgenteDe(Guid.NewGuid(), "B"), AgenteDe(a, "A")).Ejecutar(new InteraccionDelCobrador());
            Assert.Equal(a, t.GetAttributeValue<EntityReference>(InAgente).Id);

            Assert.Contains("más de un localizador", Rechaza(Crear(Nueva(true, Guid.NewGuid()), new[] { Localizador },
                AgenteDe(Guid.NewGuid(), "B"), AgenteDe(Guid.NewGuid(), "A"))).Message);
        }

        // ------------------------------------------------------------------ agente desactivado

        [Fact]
        public void Un_cobrador_con_el_agente_desactivado_se_rechaza()
        {
            var t = Nueva(true, null);
            var e = Rechaza(Crear(t, new[] { Localizador }, Inactivo(AgenteDe(Guid.NewGuid(), "QA"))));
            Assert.Contains("desactivado", e.Message);
            Assert.False(t.Contains(InAgente));
        }

        [Fact]
        public void Un_agente_sin_el_dato_de_activo_se_rechaza()
        {
            var a = AgenteDe(Guid.NewGuid(), "QA");
            a.Attributes.Remove("sanic_cm_ag_activo");
            Assert.Contains("desactivado", Rechaza(Crear(Nueva(true, null), new[] { Localizador }, a)).Message);
        }

        [Fact]
        public void Con_dos_agentes_el_enviado_desactivado_se_rechaza()
        {
            var a = Guid.NewGuid();
            Assert.Contains("desactivado", Rechaza(Crear(Nueva(true, a), new[] { Localizador },
                AgenteDe(Guid.NewGuid(), "B"), Inactivo(AgenteDe(a, "A")))).Message);
        }

        [Fact]
        public void Lee_si_el_agente_esta_activo()
        {
            var h = Crear(Nueva(true, null), new[] { Localizador }, AgenteDe(Guid.NewGuid(), "QA"));
            h.Ejecutar(new InteraccionDelCobrador());
            Assert.Contains("sanic_cm_ag_activo", ConsultaDeAgente(h).ColumnSet.Columns);
        }

        [Fact]
        public void El_Admin_registra_aunque_el_agente_este_desactivado()
        {
            var elegido = Guid.NewGuid();
            var t = Nueva(true, elegido);
            Crear(t, new[] { Localizador, Admin }, Inactivo(AgenteDe(elegido, "QA"))).Ejecutar(new InteraccionDelCobrador());
            Assert.Equal(elegido, t.GetAttributeValue<EntityReference>(InAgente).Id);
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
            Assert.Contains("el localizador", Rechaza(Crear(Nueva(true, null), new[] { Admin })).Message);
            Assert.Contains("el localizador", Rechaza(Crear(Nueva(true, null), new string[0])).Message);
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
