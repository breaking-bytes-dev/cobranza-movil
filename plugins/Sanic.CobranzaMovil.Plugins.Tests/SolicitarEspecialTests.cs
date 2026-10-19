using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// DAT-14 (a): la Custom API SolicitarEspecial crea, reactiva o avisa que ya existe la Especial de una cuenta. Ve las
    /// Especiales de otras personas con el servicio del sistema (el Solicitante lee solo las suyas), pero valida al
    /// llamador: su rol y que pueda leer la cuenta. El alta va con los permisos del llamador (dueno y "creado por" son
    /// el Solicitante); la reactivacion, con el servicio del sistema (la cerrada puede ser de otra persona).
    /// </summary>
    public class SolicitarEspecialTests
    {
        private const string Mensaje = "sanic_cm_SolicitarEspecial";
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Estado = "sanic_cm_as_estado";
        private const string Aprobacion = "sanic_cm_as_estadoaprobacion";
        private const string SolicitadoPor = "sanic_cm_as_solicitadopor";
        private const int Especial = 3, Pendiente = 2, Aprobado = 3;

        private const string Solicitante = "SR | CM | Solicitante";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string Localizador = "SR | CM | Localizador";
        private const string BackOffice = "SR | CM | BackOffice";
        private const string Admin = "SR | CM | Admin";

        private static readonly Guid CuentaId = Guid.NewGuid();

        /// <summary>Un llamado a la API con dos servicios: el del llamador (h.Service) y el del sistema.</summary>
        private sealed class Escenario
        {
            public readonly Harness H = new Harness(Mensaje, "none", 30);
            public readonly Mock<IOrganizationService> Sistema = new Mock<IOrganizationService>();
            public readonly Guid IdNuevo = Guid.NewGuid();
            public Entity Creada;
            public Entity Actualizada;

            public Escenario(string[] roles, params Entity[] especiales)
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
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Asignacion))).Returns(new EntityCollection(especiales));
                Sistema.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e => Actualizada = e);

                H.Service.Setup(s => s.Retrieve(Cuenta, CuentaId, It.IsAny<ColumnSet>())).Returns(new Entity(Cuenta, CuentaId));
                H.Service.Setup(s => s.Create(It.IsAny<Entity>())).Callback<Entity>(e => Creada = e).Returns(IdNuevo);

                H.Inputs["CuentaId"] = CuentaId;
                H.Inputs["RetiroNio"] = 1500m;
                H.Inputs["RetiroUsd"] = 40m;
                H.Inputs["Notas"] = "  Retiro — el cliente viaja  ";
            }

            public void Ejecutar()
            {
                H.Ejecutar(new SolicitarEspecial());
            }

            public InvalidPluginExecutionException Rechaza()
            {
                return Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar());
            }

            public int ConsultasDeAsignaciones()
            {
                var n = 0;
                foreach (var inv in Sistema.Invocations)
                {
                    if (inv.Method.Name == "RetrieveMultiple" && Tabla((QueryBase)inv.Arguments[0]) == Asignacion) n++;
                }
                return n;
            }

            private static string Tabla(QueryBase q)
            {
                var qe = q as QueryExpression;
                return qe == null ? null : qe.EntityName;
            }
        }

        private static Entity EspecialExistente(bool activa, int aprobacion, string nombreSolicitante)
        {
            var e = new Entity(Asignacion, Guid.NewGuid());
            e[Estado] = activa;
            e[Aprobacion] = new OptionSetValue(aprobacion);
            e[SolicitadoPor] = new EntityReference("systemuser", Guid.NewGuid()) { Name = nombreSolicitante };
            return e;
        }

        [Fact]
        public void Falla_si_se_registra_en_otro_mensaje()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Context.SetupGet(c => c.MessageName).Returns("Create");
            Assert.Contains("registro incorrecto", e.Rechaza().Message);
        }

        [Theory]
        [InlineData(Supervisor)]
        [InlineData(Localizador)]
        [InlineData(BackOffice)]
        public void Un_rol_que_no_solicita_no_puede_pedirla(string rol)
        {
            var e = new Escenario(new[] { rol });
            Assert.Contains("solo un Solicitante", e.Rechaza().Message);
            Assert.Null(e.Creada);
            Assert.Null(e.Actualizada);
            Assert.Equal(0, e.ConsultasDeAsignaciones());
        }

        [Fact]
        public void Sin_Especial_la_crea_con_los_permisos_del_llamador()
        {
            var e = new Escenario(new[] { Solicitante });
            var antes = DateTime.UtcNow.AddSeconds(-1);
            e.Ejecutar();

            Assert.NotNull(e.Creada);
            Assert.Null(e.Actualizada);
            e.H.Service.Verify(s => s.Create(It.IsAny<Entity>()), Times.Once());
            e.Sistema.Verify(s => s.Create(It.IsAny<Entity>()), Times.Never());
            var c = e.Creada;
            Assert.Equal(Asignacion, c.LogicalName);
            Assert.Equal(CuentaId, c.GetAttributeValue<EntityReference>("sanic_cm_as_cuentaid").Id);
            Assert.Equal(Cuenta, c.GetAttributeValue<EntityReference>("sanic_cm_as_cuentaid").LogicalName);
            Assert.Equal(Especial, c.GetAttributeValue<OptionSetValue>("sanic_cm_as_tipo").Value);
            Assert.True(c.GetAttributeValue<bool>(Estado));
            Assert.Equal(Pendiente, c.GetAttributeValue<OptionSetValue>(Aprobacion).Value);
            Assert.Equal(1500m, c.GetAttributeValue<decimal>("sanic_cm_as_retironio"));
            Assert.Equal(40m, c.GetAttributeValue<decimal>("sanic_cm_as_retirousd"));
            Assert.Equal("Retiro — el cliente viaja", c.GetAttributeValue<string>("sanic_cm_as_notas"));
            Assert.Equal(Harness.UsuarioLlamador, c.GetAttributeValue<EntityReference>(SolicitadoPor).Id);
            Assert.True(c.GetAttributeValue<DateTime>("sanic_cm_as_fechasignacion") >= antes);
            // Sin agente a proposito: lo pone el Supervisor al aprobar. Sin dueno: queda el llamador.
            Assert.False(c.Contains("sanic_cm_as_agenteid"));
            Assert.False(c.Contains("ownerid"));

            Assert.Equal(e.IdNuevo, e.H.Outputs["AsignacionId"]);
            Assert.Equal("creada", e.H.Outputs["Resultado"]);
            Assert.Equal(Pendiente, e.H.Outputs["EstadoAprobacion"]);
            Assert.Equal(string.Empty, e.H.Outputs["SolicitadoPor"]);
        }

        [Fact]
        public void Con_una_activa_no_toca_nada_y_avisa_quien_la_pidio()
        {
            var activa = EspecialExistente(true, Aprobado, "Ana Solicitante");
            var e = new Escenario(new[] { Solicitante }, activa);
            e.Ejecutar();

            Assert.Null(e.Creada);
            Assert.Null(e.Actualizada);
            Assert.Equal(activa.Id, e.H.Outputs["AsignacionId"]);
            Assert.Equal("ya existe", e.H.Outputs["Resultado"]);
            Assert.Equal("Ana Solicitante", e.H.Outputs["SolicitadoPor"]);
            Assert.Equal(Aprobado, e.H.Outputs["EstadoAprobacion"]);
        }

        [Fact]
        public void Si_la_referencia_no_trae_nombre_lo_lee_del_usuario()
        {
            var activa = EspecialExistente(true, Pendiente, null);
            var quien = activa.GetAttributeValue<EntityReference>(SolicitadoPor).Id;
            var e = new Escenario(new[] { Solicitante }, activa);
            var u = new Entity("systemuser", quien);
            u["fullname"] = "Beto Solicitante";
            e.Sistema.Setup(s => s.Retrieve("systemuser", quien, It.IsAny<ColumnSet>())).Returns(u);
            e.Ejecutar();
            Assert.Equal("Beto Solicitante", e.H.Outputs["SolicitadoPor"]);
        }

        [Fact]
        public void La_activa_gana_aunque_venga_despues_de_una_cerrada()
        {
            var cerrada = EspecialExistente(false, Aprobado, "X");
            var activa = EspecialExistente(true, Pendiente, "Ana");
            var e = new Escenario(new[] { Solicitante }, cerrada, activa);
            e.Ejecutar();
            Assert.Equal("ya existe", e.H.Outputs["Resultado"]);
            Assert.Equal(activa.Id, e.H.Outputs["AsignacionId"]);
            Assert.Null(e.Actualizada);
        }

        [Fact]
        public void Con_una_cerrada_la_reactiva_con_el_servicio_del_sistema_como_la_app()
        {
            var cerrada = EspecialExistente(false, Aprobado, "Otra persona");
            var e = new Escenario(new[] { Solicitante }, cerrada);
            e.Ejecutar();

            Assert.Null(e.Creada);
            e.H.Service.Verify(s => s.Update(It.IsAny<Entity>()), Times.Never());
            var r = e.Actualizada;
            Assert.NotNull(r);
            Assert.Equal(cerrada.Id, r.Id);
            Assert.True(r.GetAttributeValue<bool>(Estado));
            Assert.Equal(Pendiente, r.GetAttributeValue<OptionSetValue>(Aprobacion).Value);
            Assert.Equal(1500m, r.GetAttributeValue<decimal>("sanic_cm_as_retironio"));
            Assert.Equal(40m, r.GetAttributeValue<decimal>("sanic_cm_as_retirousd"));
            Assert.Equal("Retiro — el cliente viaja", r.GetAttributeValue<string>("sanic_cm_as_notas"));
            Assert.Equal(Harness.UsuarioLlamador, r.GetAttributeValue<EntityReference>(SolicitadoPor).Id);
            Assert.True(r.Contains("sanic_cm_as_fechasignacion"));
            foreach (var limpia in new[] { "sanic_cm_as_agenteid", "sanic_cm_as_resolutorid", "sanic_cm_as_motivo",
                "sanic_cm_as_motivorechazo", "sanic_cm_as_fechacierre" })
            {
                Assert.True(r.Contains(limpia), limpia);
                Assert.Null(r[limpia]);
            }
            // La cerrada podia ser de otra persona: pasa al llamador, para que la vea en "Mis solicitudes".
            Assert.Equal(Harness.UsuarioLlamador, r.GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.Equal("systemuser", r.GetAttributeValue<EntityReference>("ownerid").LogicalName);
            // No cambia la cuenta ni el tipo.
            Assert.False(r.Contains("sanic_cm_as_cuentaid"));
            Assert.False(r.Contains("sanic_cm_as_tipo"));

            Assert.Equal("reactivada", e.H.Outputs["Resultado"]);
            Assert.Equal(cerrada.Id, e.H.Outputs["AsignacionId"]);
            Assert.Equal(Pendiente, e.H.Outputs["EstadoAprobacion"]);
        }

        private static bool EsRevocacion(OrganizationRequest r, Guid asignacionId, Guid revocado)
        {
            var rv = r as Microsoft.Crm.Sdk.Messages.RevokeAccessRequest;
            return rv != null && rv.Target != null && rv.Target.LogicalName == Asignacion && rv.Target.Id == asignacionId
                && rv.Revokee != null && rv.Revokee.LogicalName == "systemuser" && rv.Revokee.Id == revocado;
        }

        [Fact]
        public void Al_reactivar_la_de_otra_persona_le_quita_el_compartido_con_el_servicio_del_sistema()
        {
            // S-7 (b1): el reparto le compartio la Especial a quien la pidio antes; al pasar a otro solicitante, la pierde.
            var cerrada = EspecialExistente(false, Aprobado, "Ana Solicitante");
            var anterior = cerrada.GetAttributeValue<EntityReference>(SolicitadoPor).Id;
            var e = new Escenario(new[] { Solicitante }, cerrada);
            e.Ejecutar();

            Assert.Equal("reactivada", e.H.Outputs["Resultado"]);
            e.Sistema.Verify(s => s.Execute(It.Is<OrganizationRequest>(r => EsRevocacion(r, cerrada.Id, anterior))), Times.Once());
            e.H.Service.Verify(s => s.Execute(It.IsAny<OrganizationRequest>()), Times.Never());
            // Solo la asignacion: los compartidos de cuenta, cliente y fiador los maneja el reparto (y paso3).
            e.Sistema.Verify(s => s.Execute(It.Is<OrganizationRequest>(r => r is Microsoft.Crm.Sdk.Messages.RevokeAccessRequest
                && ((Microsoft.Crm.Sdk.Messages.RevokeAccessRequest)r).Target.LogicalName != Asignacion)), Times.Never());
        }

        [Fact]
        public void Si_la_habia_pedido_el_mismo_llamador_no_revoca_nada()
        {
            var cerrada = EspecialExistente(false, Aprobado, "Yo");
            cerrada[SolicitadoPor] = new EntityReference("systemuser", Harness.UsuarioLlamador);
            var e = new Escenario(new[] { Solicitante }, cerrada);
            e.Ejecutar();
            Assert.Equal("reactivada", e.H.Outputs["Resultado"]);
            e.Sistema.Verify(s => s.Execute(It.IsAny<OrganizationRequest>()), Times.Never());
        }

        [Fact]
        public void Sin_solicitante_anterior_no_revoca_nada()
        {
            var cerrada = EspecialExistente(false, Aprobado, "X");
            cerrada.Attributes.Remove(SolicitadoPor);
            var e = new Escenario(new[] { Solicitante }, cerrada);
            e.Ejecutar();
            Assert.Equal("reactivada", e.H.Outputs["Resultado"]);
            e.Sistema.Verify(s => s.Execute(It.IsAny<OrganizationRequest>()), Times.Never());
        }

        [Fact]
        public void Crear_o_avisar_que_ya_existe_no_revoca_nada()
        {
            var crea = new Escenario(new[] { Solicitante });
            crea.Ejecutar();
            crea.Sistema.Verify(s => s.Execute(It.IsAny<OrganizationRequest>()), Times.Never());

            var existe = new Escenario(new[] { Solicitante }, EspecialExistente(true, Pendiente, "Ana"));
            existe.Ejecutar();
            existe.Sistema.Verify(s => s.Execute(It.IsAny<OrganizationRequest>()), Times.Never());
        }

        [Fact]
        public void Si_la_revocacion_falla_la_reactivacion_sigue_y_traza_solo_el_codigo()
        {
            var cerrada = EspecialExistente(false, Aprobado, "Ana Solicitante");
            var anterior = cerrada.GetAttributeValue<EntityReference>(SolicitadoPor).Id;
            var e = new Escenario(new[] { Solicitante }, cerrada);
            e.Sistema.Setup(s => s.Execute(It.IsAny<OrganizationRequest>()))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -2147220891 }, "fallo " + anterior));
            e.Ejecutar();

            Assert.Equal("reactivada", e.H.Outputs["Resultado"]);
            Assert.NotNull(e.Actualizada);
            var texto = e.H.TodoElTexto();
            Assert.Contains("-2147220891", texto);
            Assert.DoesNotContain(anterior.ToString(), texto);
            Assert.DoesNotContain(cerrada.Id.ToString(), texto);
            Assert.DoesNotContain("Ana", texto);
        }

        [Fact]
        public void Admin_y_sin_roles_de_CM_tambien_pueden()
        {
            new Escenario(new[] { Admin }).Ejecutar();
            var sinRoles = new Escenario(new string[0]);
            sinRoles.Ejecutar();
            Assert.NotNull(sinRoles.Creada);
        }

        [Fact]
        public void Una_cuenta_que_el_llamador_no_ve_se_rechaza_sin_buscar_Especiales()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Service.Setup(s => s.Retrieve(Cuenta, CuentaId, It.IsAny<ColumnSet>()))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault(), "sin acceso"));
            Assert.Contains("no existe o no tenes acceso", e.Rechaza().Message);
            Assert.Equal(0, e.ConsultasDeAsignaciones());
            Assert.Null(e.Creada);
        }

        [Fact]
        public void Sin_cuenta_o_sin_notas_se_rechaza()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Inputs.Remove("CuentaId");
            Assert.Contains("falta la cuenta", e.Rechaza().Message);

            var n = new Escenario(new[] { Solicitante });
            n.H.Inputs["Notas"] = "   ";
            Assert.Contains("notas", n.Rechaza().Message);
            Assert.Null(n.Creada);
        }

        [Fact]
        public void Notas_con_un_numero_de_tarjeta_se_rechazan_sin_citarlo()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Inputs["Notas"] = "pagar con 4111 1111 1111 1111";
            var ex = e.Rechaza();
            Assert.Contains("numero de tarjeta", ex.Message);
            Assert.DoesNotContain("4111", ex.Message);
            Assert.DoesNotContain("4111", e.H.TodoElTexto());
            Assert.Null(e.Creada);
        }

        [Fact]
        public void Un_monto_en_las_notas_no_es_una_tarjeta()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Inputs["Notas"] = "Retiro — 125000 cordobas";
            e.Ejecutar();
            Assert.NotNull(e.Creada);
        }

        [Theory]
        [InlineData("RetiroNio", -1.0)]
        [InlineData("RetiroUsd", 1000000001.0)]
        public void Un_retiro_fuera_de_rango_se_rechaza(string parametro, double valor)
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Inputs[parametro] = (decimal)valor;
            Assert.Contains("fuera de rango", e.Rechaza().Message);
        }

        [Fact]
        public void Sin_retiros_quedan_vacios()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Inputs.Remove("RetiroNio");
            e.H.Inputs["RetiroUsd"] = null;
            e.Ejecutar();
            Assert.True(e.Creada.Contains("sanic_cm_as_retironio"));
            Assert.Null(e.Creada["sanic_cm_as_retironio"]);
            Assert.Null(e.Creada["sanic_cm_as_retirousd"]);
        }

        [Fact]
        public void Si_el_alta_falla_explica_que_hacer_y_traza_solo_el_codigo()
        {
            var e = new Escenario(new[] { Solicitante });
            e.H.Service.Setup(s => s.Create(It.IsAny<Entity>()))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -2147088238 }, "duplicado 4111111111111111"));
            var ex = e.Rechaza();
            Assert.Contains("no se pudo crear la Especial", ex.Message);
            Assert.DoesNotContain("4111", e.H.TodoElTexto());
            Assert.Contains("-2147088238", e.H.TodoElTexto());
        }

        [Fact]
        public void Nunca_traza_montos_notas_ni_identificadores()
        {
            var e = new Escenario(new[] { Solicitante }, EspecialExistente(false, Aprobado, "Otra persona"));
            e.Ejecutar();
            var texto = e.H.TodoElTexto();
            Assert.DoesNotContain("1500", texto);
            Assert.DoesNotContain("viaja", texto);
            Assert.DoesNotContain(CuentaId.ToString(), texto);
            Assert.DoesNotContain("Otra persona", texto);
        }
    }
}
