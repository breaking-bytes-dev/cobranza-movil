using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// 1.0.9.6: la carga de Smelter escribe como SYSTEM, que no tiene roles de CM. AprobacionEspecial y ColumnasAsignacion
    /// ya no le consultan los roles (dos RetrieveMultiple por fila que siempre daban "sin roles de CM") y ClaveDesdeCuenta
    /// no relee la cuenta que la carga referencia por su clave alternativa. SYSTEM se reconoce con un WhoAmI del servicio
    /// del sistema, una vez por organizacion.
    /// Las reglas de seguridad no cambian: a una PERSONA se le consultan los roles como siempre (S-6, H1, H2, S-7, S-11),
    /// y si no se sabe quien es SYSTEM se consulta como antes.
    /// </summary>
    public class CargaComoSistemaTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Tipo = "sanic_cm_as_tipo";
        private const string Aprobacion = "sanic_cm_as_estadoaprobacion";
        private const string AgenteId = "sanic_cm_as_agenteid";
        private const string SolicitadoPor = "sanic_cm_as_solicitadopor";
        private const string CuentaId = "sanic_cm_as_cuentaid";
        private const string ClaveAs = "sanic_cm_as_clavepan";
        private const string ClaveCt = "sanic_cm_ct_clavepan";
        private const string UltimaCarga = "sanic_cm_as_ultimacarga";
        private const int Diaria = 1, Incobrable = 2, Especial = 3;
        private const int Borrador = 1, Pendiente = 2, Aprobado = 3;

        private const string Localizador = "SR | CM | Localizador";
        private const string Solicitante = "SR | CM | Solicitante";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string Admin = "SR | CM | Admin";

        // HMAC-SHA256 hex de ejemplo (nunca la clave real)
        private const string ClaveA = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        private const string ClaveB = "60303ae22b998861bce3b28f33eec1be758a213c86c93c076dbe9f558c11c752";

        private static readonly Guid Sistema = Guid.Parse("99999999-9999-9999-9999-999999999999");

        private static Harness Carga(string mensaje, Entity target)
        {
            return new Harness(mensaje, Asignacion).ConTarget(target).RolesDelLlamador().ConSistema(Sistema, llamadorEsSistema: true);
        }

        private static Harness Persona(string mensaje, Entity target, params string[] roles)
        {
            return new Harness(mensaje, Asignacion).ConTarget(target).RolesDelLlamador(roles).ConSistema(Sistema, llamadorEsSistema: false);
        }

        private static Entity Nueva(int tipo, int? aprobacion, bool conAgente)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[Tipo] = new OptionSetValue(tipo);
            if (aprobacion.HasValue) t[Aprobacion] = new OptionSetValue(aprobacion.Value);
            if (conAgente) t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            return t;
        }

        private static Entity Previa(Guid id, int tipo, int aprobacion, Guid? agente)
        {
            var p = new Entity(Asignacion, id);
            p[Tipo] = new OptionSetValue(tipo);
            p[Aprobacion] = new OptionSetValue(aprobacion);
            if (agente.HasValue) p[AgenteId] = new EntityReference(Agente, agente.Value);
            return p;
        }

        // ------------------------------------------------------------------ AprobacionEspecial: la carga

        [Theory]
        [InlineData(Diaria)]
        [InlineData(Incobrable)]
        public void La_carga_crea_una_Diaria_o_Incobrable_sin_consultar_roles_y_nace_en_Borrador(int tipo)
        {
            var t = Nueva(tipo, null, conAgente: true);
            var h = Carga("Create", t);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.Equal(Borrador, t.GetAttributeValue<OptionSetValue>(Aprobacion).Value); // DAT-28 sigue igual
        }

        [Fact]
        public void La_carga_crea_una_Especial_con_agente_y_solicitante_ajeno_sin_consultar_roles()
        {
            var t = Nueva(Especial, null, conAgente: true);
            t[SolicitadoPor] = new EntityReference("systemuser", Guid.NewGuid());
            var h = Carga("Create", t);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void La_carga_cambia_el_agente_de_una_Especial_sin_consultar_roles()
        {
            var previa = Previa(Guid.NewGuid(), Especial, Aprobado, Guid.NewGuid());
            var t = new Entity(Asignacion, previa.Id);
            t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            t[Tipo] = new OptionSetValue(Especial);
            var h = Carga("Update", t).ConPreImage(previa);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void SYSTEM_se_pregunta_una_sola_vez_por_organizacion()
        {
            var h = Carga("Create", Nueva(Diaria, null, conAgente: true));
            h.Ejecutar(new AprobacionEspecial());
            h.ConTarget(Nueva(Incobrable, null, conAgente: true));
            h.Ejecutar(new AprobacionEspecial());
            h.ConTarget(Nueva(Diaria, null, conAgente: false));
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(1, h.WhoAmIAlServicio());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Si_no_se_sabe_quien_es_SYSTEM_se_consultan_los_roles_como_antes()
        {
            // Sin respuesta de WhoAmI: no hay atajo (falla hacia el comportamiento anterior, no hacia la exencion).
            var t = Nueva(Diaria, null, conAgente: true);
            var h = new Harness("Create", Asignacion).ConTarget(t).RolesDelLlamador();
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(2, h.ConsultasAlServicio());
            Assert.Equal(Borrador, t.GetAttributeValue<OptionSetValue>(Aprobacion).Value);
        }

        // ------------------------------------------------------------------ AprobacionEspecial: las personas, igual que antes

        [Theory]
        [InlineData(Localizador)]
        [InlineData(Solicitante)]
        public void S6_una_persona_sin_Supervisor_sigue_sin_poder_aprobar_aunque_SYSTEM_este_identificado(string rol)
        {
            var previa = Previa(Guid.NewGuid(), Especial, Pendiente, null);
            var t = new Entity(Asignacion, previa.Id);
            t[Aprobacion] = new OptionSetValue(Aprobado);
            var h = Persona("Update", t, rol).ConPreImage(previa);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.Contains("Supervisor", ex.Message);
            Assert.Equal(2, h.ConsultasAlServicio());
        }

        [Fact]
        public void S6_el_Supervisor_sigue_aprobando_consultando_sus_roles()
        {
            var previa = Previa(Guid.NewGuid(), Especial, Pendiente, null);
            var t = new Entity(Asignacion, previa.Id);
            t[Aprobacion] = new OptionSetValue(Aprobado);
            var h = Persona("Update", t, Supervisor).ConPreImage(previa);
            h.Ejecutar(new AprobacionEspecial());
            Assert.Equal(2, h.ConsultasAlServicio());
        }

        [Theory]
        [InlineData(Admin)]
        [InlineData(Supervisor)]
        public void H2_una_persona_con_roles_de_CM_sigue_sin_poder_crear_una_Diaria(string rol)
        {
            var h = Persona("Create", Nueva(Diaria, null, conAgente: true), rol);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.Contains("carga del banco", ex.Message);
        }

        [Fact]
        public void S7_una_persona_sigue_sin_poder_poner_a_otro_como_solicitante()
        {
            var t = Nueva(Especial, Pendiente, conAgente: false);
            t[SolicitadoPor] = new EntityReference("systemuser", Guid.NewGuid());
            var h = Persona("Create", t, Solicitante);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new AprobacionEspecial()));
            Assert.Contains("Solicitado por", ex.Message);
        }

        // ------------------------------------------------------------------ ColumnasAsignacion (S-11)

        [Fact]
        public void S11_la_carga_actualiza_sus_columnas_sin_consultar_roles()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[AgenteId] = new EntityReference(Agente, Guid.NewGuid());
            t[CuentaId] = new EntityReference(Cuenta, Guid.NewGuid());
            t[ClaveAs] = ClaveA;
            t[UltimaCarga] = DateTime.UtcNow.Date;
            t[Tipo] = new OptionSetValue(Diaria);
            var h = Carga("Update", t);
            h.Ejecutar(new ColumnasAsignacion());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void S11_el_Localizador_sigue_sin_poder_cambiar_el_tipo_aunque_SYSTEM_este_identificado()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[Tipo] = new OptionSetValue(Especial);
            var h = Persona("Update", t, Localizador);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ColumnasAsignacion()));
            Assert.Contains(Tipo, ex.Message);
            Assert.Equal(2, h.ConsultasAlServicio());
        }

        [Fact]
        public void S11_H1_el_Solicitante_sigue_sin_poder_escribir_ninguna_columna()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t["sanic_cm_as_orden"] = 1;
            var h = Persona("Update", t, Solicitante);
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ColumnasAsignacion()));
        }

        // ------------------------------------------------------------------ ClaveDesdeCuenta

        private static EntityReference CuentaResueltaPorClave(Guid id, string clave)
        {
            // Lo que llega de la carga: referencia por clave alternativa (R26) que la plataforma ya resolvio a un Id.
            return new EntityReference(Cuenta, new KeyAttributeCollection { { ClaveCt, clave } }) { Id = id };
        }

        [Theory]
        [InlineData("Create")]
        [InlineData("Update")]
        public void La_carga_con_la_cuenta_resuelta_por_clave_completa_la_clave_sin_leer_la_cuenta(string mensaje)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[CuentaId] = CuentaResueltaPorClave(Guid.NewGuid(), ClaveA);
            var h = Carga(mensaje, t);
            h.Ejecutar(new ClaveDesdeCuenta());
            Assert.Equal(ClaveA, t.GetAttributeValue<string>(ClaveAs));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void La_carga_con_otra_clave_en_el_Target_se_sigue_rechazando_sin_citar_valores()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[CuentaId] = CuentaResueltaPorClave(Guid.NewGuid(), ClaveA);
            t[ClaveAs] = ClaveB;
            var h = Carga("Update", t);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));
            Assert.DoesNotContain(ClaveA, ex.Message);
            Assert.DoesNotContain(ClaveB, ex.Message);
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Una_persona_con_Id_y_clave_en_la_referencia_se_valida_contra_la_cuenta_leida()
        {
            // Con Id explicito la plataforma no tiene por que mirar la clave que acompana a la referencia: a una persona no
            // se le cree. La cuenta tiene ClaveA; la referencia dice ClaveB y el Target tambien: se rechaza como siempre.
            var cuentaId = Guid.NewGuid();
            var cuenta = new Entity(Cuenta, cuentaId);
            cuenta[ClaveCt] = ClaveA;
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[CuentaId] = CuentaResueltaPorClave(cuentaId, ClaveB);
            t[ClaveAs] = ClaveB;
            var h = Persona("Create", t, Admin).RetrieveDevuelve(cuenta, ClaveCt);
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ClaveDesdeCuenta()));
            Assert.Equal(1, h.RetrievesAlServicio());
        }

        [Fact]
        public void La_carga_con_la_cuenta_por_Id_sin_clave_en_la_referencia_lee_la_cuenta_como_antes()
        {
            var cuentaId = Guid.NewGuid();
            var cuenta = new Entity(Cuenta, cuentaId);
            cuenta[ClaveCt] = ClaveA;
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[CuentaId] = new EntityReference(Cuenta, cuentaId);
            var h = Carga("Create", t).RetrieveDevuelve(cuenta, ClaveCt);
            h.Ejecutar(new ClaveDesdeCuenta());
            Assert.Equal(ClaveA, t.GetAttributeValue<string>(ClaveAs));
            Assert.Equal(1, h.RetrievesAlServicio());
        }
    }
}
