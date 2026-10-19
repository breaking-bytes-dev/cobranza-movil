using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// S-6: solo un Supervisor (o Admin) aprueba, rechaza o pone agente a una asignacion Especial.
    /// Frena a las PERSONAS con roles de CM (Solicitante, BackOffice, Localizador). Quien no tiene roles de CM
    /// (SYSTEM, que es la identidad de la carga de Smelter; administradores del entorno) no se frena: las Especiales
    /// del CSV no llegan al cobrador hasta que se aprueban, pero la carga no se rompe.
    /// </summary>
    public class AprobacionEspecialTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Tipo = "sanic_cm_as_tipo";
        private const string Estado = "sanic_cm_as_estadoaprobacion";
        private const string AgenteId = "sanic_cm_as_agenteid";
        private const string Orden = "sanic_cm_as_orden";
        private const int Diaria = 1, Especial = 3;
        private const int Borrador = 1, Pendiente = 2, Aprobado = 3, Rechazada = 4;

        private const string Solicitante = "SR | CM | Solicitante";
        private const string BackOffice = "SR | CM | BackOffice";
        private const string Localizador = "SR | CM | Localizador";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string Admin = "SR | CM | Admin";
        private const string Servicio = "SR | CM | Servicio - Cobranza";

        private static Entity Nueva(int? tipo, int? estado, bool conAgente)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            if (tipo.HasValue) t[Tipo] = new OptionSetValue(tipo.Value);
            if (estado.HasValue) t[Estado] = new OptionSetValue(estado.Value);
            if (conAgente) t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            return t;
        }

        private static Entity Previa(Guid id, int tipo, int estado, Guid? agente)
        {
            var p = new Entity(Asignacion, id);
            p[Tipo] = new OptionSetValue(tipo);
            p[Estado] = new OptionSetValue(estado);
            if (agente.HasValue) p[AgenteId] = new EntityReference(Agente, agente.Value);
            return p;
        }

        private static Harness Crear(Entity target, params string[] roles)
        {
            return new Harness("Create", Asignacion).ConTarget(target).RolesDelLlamador(roles);
        }

        private static Harness Actualizar(Entity target, Entity previa, params string[] roles)
        {
            return new Harness("Update", Asignacion).ConTarget(target).ConPreImage(previa).RolesDelLlamador(roles);
        }

        private static void Rechaza(Harness h)
        {
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.Contains("Supervisor", ex.Message);
        }

        // ------------------------------------------------------------------ Create

        [Theory]
        [InlineData(Diaria, null)]
        [InlineData(Diaria, Pendiente)]
        [InlineData(2, null)]
        public void DAT_28_una_Diaria_o_Incobrable_nace_en_Borrador(int tipo, int? estado)
        {
            var t = Nueva(tipo, estado, conAgente: true);
            var h = Crear(t, new string[0]);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(Borrador, t.GetAttributeValue<OptionSetValue>(Estado).Value);
        }

        [Fact]
        public void DAT_28_una_Especial_conserva_su_estado_y_un_estado_explicito_no_se_toca()
        {
            var especial = Nueva(Especial, null, conAgente: false);
            Crear(especial, Solicitante).Ejecutar(new AprobacionEspecial());
            Assert.False(especial.Contains(Estado)); // nace Pendiente por el valor por defecto de la columna

            var diariaAprobada = Nueva(Diaria, Aprobado, conAgente: true);
            Crear(diariaAprobada, new string[0]).Ejecutar(new AprobacionEspecial());
            Assert.Equal(Aprobado, diariaAprobada.GetAttributeValue<OptionSetValue>(Estado).Value);
        }

        [Fact]
        public void DAT_28_en_un_Update_no_se_toca_el_estado()
        {
            var previa = Previa(Guid.NewGuid(), Diaria, Pendiente, null);
            var t = new Entity(Asignacion, previa.Id);
            t[Orden] = 3;
            Actualizar(t, previa, Localizador).Ejecutar(new AprobacionEspecial());
            Assert.False(t.Contains(Estado));
        }

        [Fact]
        public void La_solicitud_normal_pendiente_y_sin_agente_pasa_sin_consultar_roles()
        {
            var h = Crear(Nueva(Especial, Pendiente, conAgente: false), Solicitante);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void El_Solicitante_no_puede_crear_una_Especial_con_agente()
        {
            Rechaza(Crear(Nueva(Especial, Pendiente, conAgente: true), Solicitante));
        }

        [Fact]
        public void El_Solicitante_no_puede_crear_una_Especial_ya_aprobada()
        {
            Rechaza(Crear(Nueva(Especial, Aprobado, conAgente: false), Solicitante));
        }

        [Fact]
        public void Sin_tipo_la_asignacion_nace_Especial_y_se_controla_igual()
        {
            // El valor por defecto de as_tipo es 3 (Especial).
            Rechaza(Crear(Nueva(null, Pendiente, conAgente: true), Solicitante));
        }

        // ------------------------------------------------------------------ H2: Diarias e Incobrables solo por carga

        private static void RechazaH2(Harness h)
        {
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.Contains("solo llegan por la carga", ex.Message);
        }

        [Theory]
        [InlineData(Diaria, Solicitante)]
        [InlineData(Diaria, BackOffice)]
        [InlineData(Diaria, Localizador)]
        [InlineData(Diaria, Supervisor)]
        [InlineData(Diaria, Admin)]
        [InlineData(2, Solicitante)]
        [InlineData(2, Supervisor)]
        [InlineData(2, Admin)]
        public void H2_ninguna_persona_de_CM_crea_una_Diaria_o_Incobrable(int tipo, string rol)
        {
            // Dueno, 2026-10-19: "las diarias solo deberian venir por carga". La carga (Smelter, tambien el alta del
            // Admin con AltaCuenta, que pasa por un job de Smelter) crea como SYSTEM, sin roles de CM.
            RechazaH2(Crear(Nueva(tipo, null, conAgente: true), rol));
            RechazaH2(Crear(Nueva(tipo, Borrador, conAgente: false), rol));
        }

        [Theory]
        [InlineData(Diaria)]
        [InlineData(2)]
        public void H2_la_carga_y_el_Servicio_crean_Diarias_e_Incobrables(int tipo)
        {
            Crear(Nueva(tipo, null, conAgente: true), new string[0]).Ejecutar(new AprobacionEspecial());
            Crear(Nueva(tipo, null, conAgente: true), "System Administrator").Ejecutar(new AprobacionEspecial());
            Crear(Nueva(tipo, null, conAgente: true), Servicio).Ejecutar(new AprobacionEspecial());
            Crear(Nueva(tipo, null, conAgente: true), Admin, Servicio).Ejecutar(new AprobacionEspecial());
        }

        [Theory]
        [InlineData(Diaria, Admin)]
        [InlineData(2, Supervisor)]
        public void H2_convertir_en_Diaria_o_Incobrable_es_crear_una_y_tampoco_se_puede(int tipo, string rol)
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Tipo] = new OptionSetValue(tipo);
            RechazaH2(Actualizar(t, Previa(id, Especial, Aprobado, Guid.NewGuid()), rol));
        }

        [Fact]
        public void H2_la_carga_puede_reclasificar_y_reenviar_el_mismo_tipo_no_es_un_cambio()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Tipo] = new OptionSetValue(2);
            Actualizar(t, Previa(id, Diaria, Borrador, Guid.NewGuid())).Ejecutar(new AprobacionEspecial());

            var mismo = new Entity(Asignacion, id);
            mismo[Tipo] = new OptionSetValue(Diaria);
            Actualizar(mismo, Previa(id, Diaria, Borrador, Guid.NewGuid()), Supervisor).Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void H2_el_Supervisor_sigue_aprobando_y_poniendo_agente_en_una_Especial()
        {
            Crear(Nueva(Especial, Aprobado, conAgente: true), Supervisor).Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void Quien_no_tiene_roles_de_CM_no_se_frena_carga_de_Smelter()
        {
            var h = Crear(Nueva(Especial, Borrador, conAgente: true));
            h.Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void Un_rol_que_no_es_de_CM_no_cuenta_como_persona_de_CM()
        {
            var h = Crear(Nueva(Especial, Pendiente, conAgente: true), "System Administrator");
            h.Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void El_Supervisor_puede_crear_una_Especial_aprobada_con_agente()
        {
            Crear(Nueva(Especial, Aprobado, conAgente: true), Supervisor).Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void El_Admin_y_el_Servicio_tambien_pueden()
        {
            Crear(Nueva(Especial, Aprobado, conAgente: true), Admin).Ejecutar(new AprobacionEspecial());
            Crear(Nueva(Especial, Aprobado, conAgente: true), Servicio).Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void Supervisor_mas_otro_rol_de_CM_puede()
        {
            Crear(Nueva(Especial, Aprobado, conAgente: true), Solicitante, Supervisor).Ejecutar(new AprobacionEspecial());
        }

        // ------------------------------------------------------------------ Update

        [Fact]
        public void El_Supervisor_aprueba_y_asigna_agente_en_un_solo_cambio()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Estado] = new OptionSetValue(Aprobado);
            t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            Actualizar(t, Previa(id, Especial, Pendiente, null), Supervisor).Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void El_Solicitante_no_puede_aprobar()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Estado] = new OptionSetValue(Aprobado);
            Rechaza(Actualizar(t, Previa(id, Especial, Pendiente, null), Solicitante));
        }

        [Fact]
        public void El_Solicitante_no_puede_rechazar()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Estado] = new OptionSetValue(Rechazada);
            Rechaza(Actualizar(t, Previa(id, Especial, Pendiente, null), Solicitante));
        }

        [Fact]
        public void El_BackOffice_no_puede_cambiar_el_agente_de_una_Especial_aprobada()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            Rechaza(Actualizar(t, Previa(id, Especial, Aprobado, Guid.NewGuid()), BackOffice));
        }

        [Fact]
        public void Reenviar_el_mismo_agente_y_estado_no_es_un_cambio()
        {
            var id = Guid.NewGuid();
            var agente = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[AgenteId] = new EntityReference(Agente, agente);
            t[Estado] = new OptionSetValue(Aprobado);
            var h = Actualizar(t, Previa(id, Especial, Aprobado, agente), Localizador);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Quitar_el_agente_no_requiere_Supervisor()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[AgenteId] = null;
            var h = Actualizar(t, Previa(id, Especial, Borrador, Guid.NewGuid()), Solicitante);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Convertir_en_Especial_una_asignacion_con_agente_requiere_Supervisor()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Tipo] = new OptionSetValue(Especial);
            Rechaza(Actualizar(t, Previa(id, Diaria, Borrador, Guid.NewGuid()), BackOffice));
        }

        [Fact]
        public void Pasar_a_Pendiente_no_requiere_Supervisor()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Estado] = new OptionSetValue(Pendiente);
            var h = Actualizar(t, Previa(id, Especial, Borrador, null), Solicitante);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Un_cambio_que_no_toca_aprobacion_ni_agente_pasa_el_orden_del_cobrador()
        {
            var id = Guid.NewGuid();
            var t = new Entity(Asignacion, id);
            t[Orden] = 4;
            var h = Actualizar(t, Previa(id, Especial, Borrador, Guid.NewGuid()), Localizador);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Sin_imagen_previa_en_Update_falla_cerrado()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            var h = new Harness("Update", Asignacion).ConTarget(t).RolesDelLlamador(Solicitante);
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
        }

        [Fact]
        public void Registrado_en_otra_tabla_falla()
        {
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(new Entity("sanic_cm_tbl_pago", Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
        }

        [Fact]
        public void El_mensaje_de_error_no_cita_valores_del_registro()
        {
            var agente = Guid.NewGuid();
            var t = Nueva(Especial, Pendiente, conAgente: false);
            t[AgenteId] = new EntityReference(Agente, agente);
            var h = Crear(t, Solicitante);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.DoesNotContain(agente.ToString(), ex.Message);
            Assert.DoesNotContain(agente.ToString(), h.TodoElTexto());
        }
    

        // ------------------------------------------------------------------ S-7: "Solicitado por" en el alta

        private static Entity EspecialPedidaPor(Guid usuario)
        {
            var t = Nueva(Especial, Pendiente, conAgente: false);
            t["sanic_cm_as_solicitadopor"] = new EntityReference("systemuser", usuario);
            return t;
        }

        [Fact]
        public void S7_el_Solicitante_crea_la_Especial_a_su_nombre_sin_consultar_roles()
        {
            var h = Crear(EspecialPedidaPor(Harness.UsuarioLlamador), Solicitante);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Theory]
        [InlineData(Solicitante)]
        [InlineData(Localizador)]
        [InlineData(BackOffice)]
        [InlineData(Supervisor)]
        public void S7_una_persona_de_CM_no_crea_la_Especial_a_nombre_de_otro(string rol)
        {
            // RepartoAsignacion comparte la Especial aprobada con "Solicitado por": a nombre de otro, le regalaria
            // lectura y escritura a un tercero.
            var otro = Guid.NewGuid();
            var h = Crear(EspecialPedidaPor(otro), rol);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.Contains("Solicitado por", ex.Message);
            Assert.DoesNotContain(otro.ToString(), ex.Message);
            Assert.DoesNotContain(otro.ToString(), h.TodoElTexto());
        }

        [Theory]
        [InlineData(Admin)]
        [InlineData(Servicio)]
        public void S7_Admin_y_Servicio_crean_a_nombre_de_cualquiera(string rol)
        {
            Crear(EspecialPedidaPor(Guid.NewGuid()), rol).Ejecutar(new AprobacionEspecial());
        }

        [Fact]
        public void S7_sin_roles_de_CM_se_crea_a_nombre_de_cualquiera()
        {
            Crear(EspecialPedidaPor(Guid.NewGuid()), new string[0]).Ejecutar(new AprobacionEspecial());
        }
    }
}
