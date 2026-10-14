using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Reparto de la asignacion (reemplaza al flow "Cambio de Owner en Asignaciones"): pasa el dueno al usuario del
    /// agente, le comparte en lectura cuenta, cliente y fiador, y le revoca esos compartidos al dueno anterior si ya no
    /// tiene otra asignacion activa que los justifique. Corre ASINCRONO (PostOperation): nunca dentro de los 2 minutos
    /// del trozo de Smelter. Lee el estado ACTUAL de la asignacion (converge, no repite el evento).
    /// </summary>
    public class RepartoAsignacionTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Cliente = "sanic_cm_tbl_cliente";
        private const string Fiador = "sanic_cm_tbl_fiador";
        private const string Usuario = "systemuser";
        private const string Equipo = "team";
        private const string PrefijoSinCobrador = "CM \u00B7 Cartera sin cobrador";
        private const int Diaria = 1, Especial = 3;
        private const int Pendiente = 2, Aprobado = 3, Rechazada = 4;

        private static readonly int ShareConcurrente = unchecked((int)0x80048105);
        private static readonly int RevocarAlDueno = unchecked((int)0x80040223);
        private static readonly int NoExiste = unchecked((int)0x80040217);

        // ------------------------------------------------------------------ mundo de prueba

        /// <summary>
        /// Un Dataverse en miniatura sobre el Harness: filas por (tabla, id), y registro de cada Update y cada
        /// GrantAccess/RevokeAccess en orden. Se pueden inyectar fallas por solicitud.
        /// </summary>
        private sealed class Mundo
        {
            public readonly Harness H;
            public readonly Guid AsignacionId = Guid.NewGuid();
            public readonly Guid AgenteId = Guid.NewGuid();
            public readonly Guid CuentaId = Guid.NewGuid();
            public readonly Guid ClienteId = Guid.NewGuid();
            public readonly Guid FiadorId = Guid.NewGuid();
            public readonly Guid Cobrador = Guid.NewGuid();     // usuario del agente
            public readonly Guid Anterior = Guid.NewGuid();     // dueno anterior (otro cobrador)
            public readonly Guid Sistema = Guid.NewGuid();      // SYSTEM, dueno de lo que crea la carga
            public readonly Guid Solicitante = Guid.NewGuid();
            public readonly Guid EquipoSinCobrador = Guid.NewGuid(); // dueno de la cartera sin cobrador (DAT-07)

            /// <summary>Equipos que devuelve la busqueda de "Cartera sin cobrador" (por defecto, uno).</summary>
            public readonly List<Entity> Equipos = new List<Entity>();
            public readonly List<QueryExpression> ConsultasEquipo = new List<QueryExpression>();

            public readonly Dictionary<string, Entity> Filas = new Dictionary<string, Entity>();
            public readonly List<object> Operaciones = new List<object>(); // Entity (Update) u OrganizationRequest
            public readonly List<QueryExpression> Consultas = new List<QueryExpression>();
            public readonly List<int> Esperas = new List<int>();

            /// <summary>Para que tablas ("cuenta", "cliente", "fiador") el dueno anterior conserva otra asignacion activa.</summary>
            public readonly HashSet<string> ConservaOtra = new HashSet<string>();

            /// <summary>Si devuelve un codigo, la solicitud falla con ese codigo de Dataverse.</summary>
            public Func<OrganizationRequest, int?> Falla = r => null;

            public Mundo(string mensaje = "Create", int modo = 1, int etapa = 40)
            {
                H = new Harness(mensaje, Asignacion, etapa);
                H.Context.SetupGet(c => c.Mode).Returns(modo);
                H.Context.SetupGet(c => c.PrimaryEntityId).Returns(AsignacionId);
                H.ConTarget(new Entity(Asignacion, AsignacionId));

                Poner(Asignacion, AsignacionId, a =>
                {
                    a["sanic_cm_as_estado"] = true;
                    a["sanic_cm_as_tipo"] = new OptionSetValue(Diaria);
                    a["sanic_cm_as_agenteid"] = new EntityReference(Agente, AgenteId);
                    a["sanic_cm_as_cuentaid"] = new EntityReference(Cuenta, CuentaId);
                    a["ownerid"] = new EntityReference(Usuario, Sistema);
                    a["createdby"] = new EntityReference(Usuario, Sistema);
                });
                var equipo = new Entity(Equipo, EquipoSinCobrador);
                equipo["name"] = PrefijoSinCobrador + " \u00B7 DEV";
                Equipos.Add(equipo);
                Poner(Agente, AgenteId, a => a["sanic_cm_ag_usuarioid"] = new EntityReference(Usuario, Cobrador));
                Poner(Cuenta, CuentaId, c =>
                {
                    c["sanic_cm_ct_clienteid"] = new EntityReference(Cliente, ClienteId);
                    c["sanic_cm_ct_fiador"] = new EntityReference(Fiador, FiadorId);
                    c["ownerid"] = new EntityReference(Usuario, Sistema);
                });

                H.Service.Setup(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ColumnSet>()))
                    .Returns<string, Guid, ColumnSet>((t, id, cs) =>
                    {
                        Entity e;
                        if (Filas.TryGetValue(t + "|" + id, out e)) return e;
                        throw Fault(NoExiste);
                    });
                H.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e => Operaciones.Add(e));
                H.Service.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
                    .Returns<OrganizationRequest>(r =>
                    {
                        Operaciones.Add(r);
                        var codigo = Falla(r);
                        if (codigo.HasValue) throw Fault(codigo.Value);
                        return new OrganizationResponse();
                    });
                H.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>()))
                    .Returns<QueryBase>(q =>
                    {
                        var qe = (QueryExpression)q;
                        if (qe.EntityName == Equipo)
                        {
                            ConsultasEquipo.Add(qe);
                            return new EntityCollection(Equipos.ToList());
                        }
                        Consultas.Add(qe);
                        var res = new EntityCollection();
                        if (ConservaOtra.Contains(TablaDeLaConsulta(qe))) res.Entities.Add(new Entity(Asignacion, Guid.NewGuid()));
                        return res;
                    });
            }

            public void Poner(string tabla, Guid id, Action<Entity> llenar)
            {
                var e = new Entity(tabla, id);
                llenar(e);
                Filas[tabla + "|" + id] = e;
            }

            public Entity Fila(string tabla, Guid id)
            {
                return Filas[tabla + "|" + id];
            }

            public void Ejecutar()
            {
                H.Ejecutar(new RepartoAsignacion(ms => Esperas.Add(ms)));
            }

            public IEnumerable<GrantAccessRequest> Grants
            {
                get { return Operaciones.OfType<GrantAccessRequest>(); }
            }

            public IEnumerable<RevokeAccessRequest> Revokes
            {
                get { return Operaciones.OfType<RevokeAccessRequest>(); }
            }

            public IEnumerable<Entity> Updates
            {
                get { return Operaciones.OfType<Entity>(); }
            }

            public bool Compartio(string tabla, Guid id, Guid usuario, AccessRights permisos)
            {
                return Grants.Any(g => g.Target.LogicalName == tabla && g.Target.Id == id
                    && g.PrincipalAccess.Principal.Id == usuario && g.PrincipalAccess.AccessMask == permisos);
            }

            public bool Revoco(string tabla, Guid id, Guid principal)
            {
                return Revokes.Any(r => r.Target.LogicalName == tabla && r.Target.Id == id && r.Revokee.Id == principal);
            }
        }

        private static FaultException<OrganizationServiceFault> Fault(int codigo)
        {
            return new FaultException<OrganizationServiceFault>(
                new OrganizationServiceFault { ErrorCode = codigo, Message = "falla simulada" }, new FaultReason("falla simulada"));
        }

        /// <summary>"cuenta" si filtra por la cuenta de la asignacion; "cliente"/"fiador" si cruza a la cuenta por esa columna.</summary>
        private static string TablaDeLaConsulta(QueryExpression q)
        {
            if (q.Criteria.Conditions.Any(c => c.AttributeName == "sanic_cm_as_cuentaid")) return "cuenta";
            foreach (var l in q.LinkEntities)
            {
                if (l.LinkCriteria.Conditions.Any(c => c.AttributeName == "sanic_cm_ct_clienteid")) return "cliente";
                if (l.LinkCriteria.Conditions.Any(c => c.AttributeName == "sanic_cm_ct_fiador")) return "fiador";
            }
            return "?";
        }

        private static void Reasignada(Mundo m)
        {
            m.Fila(Asignacion, m.AsignacionId)["ownerid"] = new EntityReference(Usuario, m.Anterior);
        }

        // ------------------------------------------------------------------ registro

        [Fact]
        public void Falla_cerrado_si_el_paso_se_registra_sincrono()
        {
            // Sincrono correria DENTRO del trozo de Smelter (100 filas, 2 minutos por mensaje): prohibido.
            var m = new Mundo(modo: 0);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            Assert.Contains("asincrono", ex.Message);
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void Falla_cerrado_si_el_paso_no_es_PostOperation()
        {
            var m = new Mundo(etapa: 20);
            Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void Usa_el_servicio_del_sistema_y_nunca_el_del_llamador()
        {
            var m = new Mundo();
            m.Ejecutar();
            m.H.Factory.Verify(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null)), Times.AtLeastOnce());
            m.H.Factory.Verify(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == Harness.UsuarioLlamador)), Times.Never());
        }

        // ------------------------------------------------------------------ asignacion activa

        [Fact]
        public void Recien_cargada_pasa_el_dueno_al_cobrador_y_le_comparte_cuenta_cliente_y_fiador()
        {
            var m = new Mundo();
            m.Ejecutar();

            var u = Assert.Single(m.Updates);
            Assert.Equal(m.AsignacionId, u.Id);
            Assert.Equal(m.Cobrador, u.GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.Equal(Usuario, u.GetAttributeValue<EntityReference>("ownerid").LogicalName);
            Assert.True(m.Compartio(Cuenta, m.CuentaId, m.Cobrador, AccessRights.ReadAccess));
            Assert.True(m.Compartio(Cliente, m.ClienteId, m.Cobrador, AccessRights.ReadAccess));
            Assert.True(m.Compartio(Fiador, m.FiadorId, m.Cobrador, AccessRights.ReadAccess));
        }

        [Fact]
        public void No_le_revoca_la_cuenta_a_SYSTEM_cuando_SYSTEM_es_su_dueno()
        {
            // Causa (a) de las fallas del flow: "Only owner can revoke access to the owner".
            var m = new Mundo();
            m.Ejecutar();
            Assert.False(m.Revoco(Cuenta, m.CuentaId, m.Sistema));
        }

        [Fact]
        public void Revocar_al_dueno_del_registro_se_toma_como_nada_que_revocar()
        {
            var m = new Mundo();
            m.Falla = r => r is RevokeAccessRequest ? RevocarAlDueno : (int?)null;
            m.Ejecutar(); // no lanza
            Assert.True(m.Compartio(Fiador, m.FiadorId, m.Cobrador, AccessRights.ReadAccess));
        }

        [Fact]
        public void Si_el_cobrador_ya_es_el_dueno_no_actualiza_ni_revoca_y_reasegura_los_compartidos()
        {
            var m = new Mundo();
            m.Fila(Asignacion, m.AsignacionId)["ownerid"] = new EntityReference(Usuario, m.Cobrador);
            m.Ejecutar();
            Assert.Empty(m.Updates);
            Assert.Empty(m.Revokes);
            Assert.Equal(3, m.Grants.Count());
        }

        [Fact]
        public void Reasignada_le_revoca_cuenta_cliente_y_fiador_al_cobrador_anterior()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.Ejecutar();

            Assert.Equal(m.Cobrador, Assert.Single(m.Updates).GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.True(m.Revoco(Cuenta, m.CuentaId, m.Anterior));
            Assert.True(m.Revoco(Cliente, m.ClienteId, m.Anterior));
            Assert.True(m.Revoco(Fiador, m.FiadorId, m.Anterior));
            Assert.False(m.Revokes.Any(r => r.Revokee.Id == m.Cobrador));
        }

        [Fact]
        public void El_cambio_de_dueno_va_antes_de_buscar_otras_asignaciones_del_anterior()
        {
            // Dos asignaciones del mismo cliente reasignadas a la vez: si cada una cambia de dueno ANTES de mirar
            // las otras, al menos una de las dos ve a la otra ya movida y revoca el cliente.
            var m = new Mundo("Update");
            Reasignada(m);
            var ordenDelUpdate = -1;
            m.H.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e =>
            {
                ordenDelUpdate = m.Consultas.Count;
                m.Operaciones.Add(e);
            });
            m.Ejecutar();
            Assert.Equal(0, ordenDelUpdate);
            Assert.NotEmpty(m.Consultas);
        }

        [Fact]
        public void Si_el_anterior_conserva_otra_asignacion_del_cliente_no_le_revoca_el_cliente()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.ConservaOtra.Add("cliente");
            m.Ejecutar();
            Assert.True(m.Revoco(Cuenta, m.CuentaId, m.Anterior));
            Assert.False(m.Revoco(Cliente, m.ClienteId, m.Anterior));
            Assert.True(m.Revoco(Fiador, m.FiadorId, m.Anterior));
        }

        [Fact]
        public void Si_el_anterior_conserva_otra_asignacion_de_la_cuenta_no_le_revoca_la_cuenta()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.ConservaOtra.Add("cuenta");
            m.Ejecutar();
            Assert.False(m.Revoco(Cuenta, m.CuentaId, m.Anterior));
        }

        [Fact]
        public void Las_otras_asignaciones_se_buscan_del_anterior_activas_y_sin_contar_esta()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.Ejecutar();
            Assert.Equal(3, m.Consultas.Count);
            foreach (var q in m.Consultas)
            {
                Assert.Equal(Asignacion, q.EntityName);
                var c = q.Criteria.Conditions;
                Assert.Contains(c, x => x.AttributeName == "ownerid" && x.Operator == ConditionOperator.Equal && (Guid)x.Values[0] == m.Anterior);
                Assert.Contains(c, x => x.AttributeName == "sanic_cm_as_estado" && x.Operator == ConditionOperator.Equal && (bool)x.Values[0]);
                Assert.Contains(c, x => x.AttributeName == "sanic_cm_tbl_asignacionid" && x.Operator == ConditionOperator.NotEqual && (Guid)x.Values[0] == m.AsignacionId);
                Assert.Equal(1, q.TopCount);
            }
        }

        [Fact]
        public void Si_el_dueno_anterior_es_un_equipo_le_revoca_al_equipo()
        {
            var m = new Mundo("Update");
            var equipo = Guid.NewGuid();
            m.Fila(Asignacion, m.AsignacionId)["ownerid"] = new EntityReference("team", equipo);
            m.Ejecutar();
            Assert.Contains(m.Revokes, r => r.Revokee.LogicalName == "team" && r.Revokee.Id == equipo && r.Target.Id == m.ClienteId);
        }

        [Fact]
        public void Cuenta_sin_cliente_ni_fiador_solo_comparte_la_cuenta()
        {
            var m = new Mundo();
            m.Poner(Cuenta, m.CuentaId, c => c["ownerid"] = new EntityReference(Usuario, m.Sistema));
            m.Ejecutar();
            var g = Assert.Single(m.Grants);
            Assert.Equal(Cuenta, g.Target.LogicalName);
        }

        // ------------------------------------------------------------------ cuando no se reparte

        [Fact]
        public void Especial_pendiente_no_se_reparte()
        {
            // S-6: una Especial sin aprobar no cambia de dueno ni se comparte, venga de donde venga.
            var m = new Mundo();
            var a = m.Fila(Asignacion, m.AsignacionId);
            a["sanic_cm_as_tipo"] = new OptionSetValue(Especial);
            a["sanic_cm_as_estadoaprobacion"] = new OptionSetValue(Pendiente);
            m.Ejecutar();
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void Especial_rechazada_no_se_reparte()
        {
            var m = new Mundo();
            var a = m.Fila(Asignacion, m.AsignacionId);
            a["sanic_cm_as_tipo"] = new OptionSetValue(Especial);
            a["sanic_cm_as_estadoaprobacion"] = new OptionSetValue(Rechazada);
            m.Ejecutar();
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void Especial_aprobada_se_reparte_y_se_comparte_con_el_solicitante_que_la_creo()
        {
            var m = new Mundo();
            var a = m.Fila(Asignacion, m.AsignacionId);
            a["sanic_cm_as_tipo"] = new OptionSetValue(Especial);
            a["sanic_cm_as_estadoaprobacion"] = new OptionSetValue(Aprobado);
            a["sanic_cm_as_solicitadopor"] = new EntityReference(Usuario, m.Solicitante);
            a["createdby"] = new EntityReference(Usuario, m.Solicitante);
            m.Ejecutar();
            Assert.Single(m.Updates);
            Assert.True(m.Compartio(Asignacion, m.AsignacionId, m.Solicitante,
                AccessRights.ReadAccess | AccessRights.WriteAccess | AccessRights.AppendAccess));
        }

        [Fact]
        public void S7_si_Solicitado_por_no_es_quien_creo_la_Especial_no_se_comparte_con_nadie()
        {
            // "Solicitado por" lo editan Solicitante, Supervisor y Localizador: cambiandolo se le regalaba lectura,
            // escritura y anexar a un tercero. Quien la creo (createdby) no se puede editar.
            var m = new Mundo();
            var a = m.Fila(Asignacion, m.AsignacionId);
            a["sanic_cm_as_tipo"] = new OptionSetValue(Especial);
            a["sanic_cm_as_estadoaprobacion"] = new OptionSetValue(Aprobado);
            a["sanic_cm_as_solicitadopor"] = new EntityReference(Usuario, m.Solicitante);
            a["createdby"] = new EntityReference(Usuario, m.Anterior);
            m.Ejecutar(); // no lanza: el resto del reparto sigue

            Assert.DoesNotContain(m.Grants, g => g.Target.LogicalName == Asignacion);
            Assert.Equal(m.Cobrador, Assert.Single(m.Updates).GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.True(m.Compartio(Cuenta, m.CuentaId, m.Cobrador, AccessRights.ReadAccess));
            Assert.Contains("S-7", m.H.TodoElTexto());
        }

        [Fact]
        public void S7_la_Especial_se_lee_con_su_creador()
        {
            var m = new Mundo();
            ColumnSet leidas = null;
            m.H.Service.Setup(s => s.Retrieve(Asignacion, m.AsignacionId, It.IsAny<ColumnSet>()))
                .Returns<string, Guid, ColumnSet>((t, id, cs) => { leidas = cs; return m.Fila(Asignacion, id); });
            m.Ejecutar();
            Assert.Contains("createdby", leidas.Columns);
        }

        // ------------------------------------------------------------------ sin cobrador (DAT-07)

        [Fact]
        public void Agente_sin_usuario_pasa_la_asignacion_al_equipo_sin_cobrador_sin_compartir_nada()
        {
            var m = new Mundo();
            m.Fila(Agente, m.AgenteId).Attributes.Remove("sanic_cm_ag_usuarioid");
            m.Ejecutar();

            var dueno = Assert.Single(m.Updates).GetAttributeValue<EntityReference>("ownerid");
            Assert.Equal(Equipo, dueno.LogicalName);
            Assert.Equal(m.EquipoSinCobrador, dueno.Id);
            Assert.Empty(m.Grants);
        }

        [Fact]
        public void Reasignada_a_un_agente_sin_usuario_le_revoca_al_cobrador_anterior()
        {
            // Antes el reparto no hacia nada y el cobrador anterior seguia con la asignacion en el telefono.
            var m = new Mundo("Update");
            Reasignada(m);
            m.Fila(Agente, m.AgenteId).Attributes.Remove("sanic_cm_ag_usuarioid");
            m.Ejecutar();

            Assert.Equal(m.EquipoSinCobrador, Assert.Single(m.Updates).GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.True(m.Revoco(Cuenta, m.CuentaId, m.Anterior));
            Assert.True(m.Revoco(Cliente, m.ClienteId, m.Anterior));
            Assert.True(m.Revoco(Fiador, m.FiadorId, m.Anterior));
            Assert.Empty(m.Grants);
        }

        [Fact]
        public void Sin_agente_tambien_pasa_al_equipo_sin_cobrador()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.Fila(Asignacion, m.AsignacionId).Attributes.Remove("sanic_cm_as_agenteid");
            m.Ejecutar();
            Assert.Equal(m.EquipoSinCobrador, Assert.Single(m.Updates).GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.True(m.Revoco(Cuenta, m.CuentaId, m.Anterior));
        }

        [Fact]
        public void Si_ya_es_del_equipo_sin_cobrador_no_hace_nada()
        {
            var m = new Mundo("Update");
            m.Fila(Asignacion, m.AsignacionId)["ownerid"] = new EntityReference(Equipo, m.EquipoSinCobrador);
            m.Fila(Agente, m.AgenteId).Attributes.Remove("sanic_cm_ag_usuarioid");
            m.Ejecutar();
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void Sin_el_equipo_sin_cobrador_falla_sin_tocar_nada_y_sin_reintento()
        {
            // Mejor un trabajo fallido visible que revocarle al dueno algo que sigue siendo suyo.
            var m = new Mundo("Update");
            Reasignada(m);
            m.Fila(Agente, m.AgenteId).Attributes.Remove("sanic_cm_ag_usuarioid");
            m.Equipos.Clear();
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());

            Assert.NotEqual(OperationStatus.Retry, ex.Status);
            Assert.Contains("Cartera sin cobrador", ex.Message);
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void Con_dos_equipos_sin_cobrador_falla_sin_tocar_nada()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.Fila(Agente, m.AgenteId).Attributes.Remove("sanic_cm_ag_usuarioid");
            m.Equipos.Add(new Entity(Equipo, Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            Assert.Empty(m.Operaciones);
        }

        [Fact]
        public void El_equipo_sin_cobrador_se_busca_por_prefijo_entre_los_equipos_duenos()
        {
            // El nombre lleva el entorno al final ("... · DEV"), como los demas equipos de CM.
            var m = new Mundo();
            m.Fila(Agente, m.AgenteId).Attributes.Remove("sanic_cm_ag_usuarioid");
            m.Ejecutar();

            var q = Assert.Single(m.ConsultasEquipo);
            Assert.Contains(q.Criteria.Conditions, c => c.AttributeName == "name" && c.Operator == ConditionOperator.BeginsWith
                && (string)c.Values[0] == PrefijoSinCobrador);
            Assert.Contains(q.Criteria.Conditions, c => c.AttributeName == "teamtype" && c.Operator == ConditionOperator.Equal
                && (int)c.Values[0] == 0);
            Assert.Equal(2, q.TopCount);
        }

        [Fact]
        public void Con_cobrador_no_busca_el_equipo_sin_cobrador()
        {
            var m = new Mundo();
            m.Ejecutar();
            Assert.Empty(m.ConsultasEquipo);
        }

        [Fact]
        public void Si_la_asignacion_ya_no_existe_termina_sin_error()
        {
            var m = new Mundo();
            m.Filas.Remove(Asignacion + "|" + m.AsignacionId);
            m.Ejecutar();
            Assert.Empty(m.Operaciones);
        }

        // ------------------------------------------------------------------ asignacion cerrada

        [Fact]
        public void Cerrada_le_revoca_al_dueno_actual_sin_compartir_ni_cambiar_el_dueno()
        {
            var m = new Mundo("Update");
            var a = m.Fila(Asignacion, m.AsignacionId);
            a["sanic_cm_as_estado"] = false;
            a["ownerid"] = new EntityReference(Usuario, m.Cobrador);
            m.Ejecutar();
            Assert.Empty(m.Updates);
            Assert.Empty(m.Grants);
            Assert.True(m.Revoco(Cuenta, m.CuentaId, m.Cobrador));
            Assert.True(m.Revoco(Cliente, m.ClienteId, m.Cobrador));
            Assert.True(m.Revoco(Fiador, m.FiadorId, m.Cobrador));
        }

        [Fact]
        public void Cerrada_con_otra_asignacion_activa_de_la_cuenta_no_revoca_la_cuenta()
        {
            var m = new Mundo("Update");
            var a = m.Fila(Asignacion, m.AsignacionId);
            a["sanic_cm_as_estado"] = false;
            a["ownerid"] = new EntityReference(Usuario, m.Cobrador);
            m.ConservaOtra.Add("cuenta");
            m.Ejecutar();
            Assert.False(m.Revoco(Cuenta, m.CuentaId, m.Cobrador));
        }

        // ------------------------------------------------------------------ concurrencia y fallas

        [Fact]
        public void Share_concurrente_reintenta_con_espera_y_termina_bien()
        {
            // Causa (b) de las fallas del flow: "More than one concurrent Share requests detected".
            var m = new Mundo();
            var fallas = 2;
            m.Falla = r =>
            {
                var g = r as GrantAccessRequest;
                if (g != null && g.Target.LogicalName == Cliente && fallas > 0) { fallas--; return ShareConcurrente; }
                return null;
            };
            m.Ejecutar(); // no lanza
            Assert.Equal(2, m.Esperas.Count);
            Assert.All(m.Esperas, ms => Assert.InRange(ms, 1, 5000));
            Assert.Equal(3, m.Grants.Count(g => g.Target.LogicalName == Cliente));
        }

        [Fact]
        public void Si_un_compartido_sigue_fallando_igual_revoca_al_anterior_y_pide_reintento()
        {
            // En el flow, un Grant fallido salteaba el fiador y la revocacion: el cobrador anterior seguia viendo la cuenta.
            var m = new Mundo("Update");
            Reasignada(m);
            m.Falla = r =>
            {
                var g = r as GrantAccessRequest;
                return g != null && g.Target.LogicalName == Cliente ? ShareConcurrente : (int?)null;
            };
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());

            Assert.Equal(OperationStatus.Retry, ex.Status);
            Assert.True(m.Compartio(Fiador, m.FiadorId, m.Cobrador, AccessRights.ReadAccess));
            Assert.True(m.Revoco(Cuenta, m.CuentaId, m.Anterior));
            Assert.True(m.Revoco(Cliente, m.ClienteId, m.Anterior));
            Assert.True(m.Revoco(Fiador, m.FiadorId, m.Anterior));
            Assert.Contains("cliente", ex.Message);
        }

        [Fact]
        public void Una_falla_no_transitoria_no_corta_los_demas_pasos_y_falla_sin_reintento()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.Falla = r =>
            {
                var g = r as GrantAccessRequest;
                return g != null && g.Target.LogicalName == Cuenta ? unchecked((int)0x80040220) : (int?)null; // sin privilegio
            };
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());

            Assert.NotEqual(OperationStatus.Retry, ex.Status);
            Assert.Empty(m.Esperas);
            Assert.True(m.Compartio(Cliente, m.ClienteId, m.Cobrador, AccessRights.ReadAccess));
            Assert.True(m.Revoco(Fiador, m.FiadorId, m.Anterior));
        }

        [Fact]
        public void Ni_las_trazas_ni_el_error_citan_identificadores_de_registros()
        {
            var m = new Mundo("Update");
            Reasignada(m);
            m.Falla = r => r is GrantAccessRequest ? ShareConcurrente : (int?)null;
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => m.Ejecutar());
            var texto = m.H.TodoElTexto() + "\n" + ex.Message;
            foreach (var id in new[] { m.CuentaId, m.ClienteId, m.FiadorId, m.Cobrador, m.Anterior, m.AsignacionId })
            {
                Assert.DoesNotContain(id.ToString(), texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
