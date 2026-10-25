using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// S-11: cada rol de CM escribe solo las columnas de la asignacion que usa su pantalla. Antes el Supervisor (Write
    /// Global) podia cambiar cualquier columna de cualquier asignacion: cuenta, tipo, retiro, orden.
    /// Sin roles de CM (la carga de Smelter, los plugins con el servicio del sistema) y Admin/Servicio no se frenan.
    /// </summary>
    public class ColumnasAsignacionTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Localizador = "SR | CM | Localizador";
        private const string Solicitante = "SR | CM | Solicitante";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string BackOffice = "SR | CM | BackOffice";
        private const string Admin = "SR | CM | Admin";
        private const string Servicio = "SR | CM | Servicio - Cobranza";
        private const string SinCobrador = "SR | CM | Cartera sin cobrador";

        private static Entity Cambio(params string[] columnas)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            foreach (var c in columnas) t[c] = null;
            return t;
        }

        private static Harness Actualizar(Entity target, params string[] roles)
        {
            return new Harness("Update", Asignacion).ConTarget(target).RolesDelLlamador(roles);
        }

        private static void Pasa(Entity target, params string[] roles)
        {
            Actualizar(target, roles).Ejecutar(new ColumnasAsignacion());
        }

        private static InvalidPluginExecutionException Rechaza(Entity target, params string[] roles)
        {
            return Assert.Throws<InvalidPluginExecutionException>(() => Actualizar(target, roles).Ejecutar(new ColumnasAsignacion()));
        }

        [Fact]
        public void Falla_si_se_registra_en_Create()
        {
            var h = new Harness("Create", Asignacion).ConTarget(Cambio("sanic_cm_as_orden")).RolesDelLlamador(Localizador);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ColumnasAsignacion()));
            Assert.Contains("registro incorrecto", ex.Message);
        }

        [Fact]
        public void Un_cambio_sin_columnas_de_la_asignacion_no_consulta_roles()
        {
            // El reparto cambia el dueno con el servicio del sistema: ownerid no es una columna de negocio.
            var t = new Entity(Asignacion, Guid.NewGuid());
            t["ownerid"] = new EntityReference("team", Guid.NewGuid());
            t["modifiedon"] = DateTime.UtcNow;
            var h = Actualizar(t, Supervisor);
            h.Ejecutar(new ColumnasAsignacion());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void El_Supervisor_aprueba_rechaza_y_asigna_el_agente()
        {
            Pasa(Cambio("sanic_cm_as_estadoaprobacion", "sanic_cm_as_agenteid", "sanic_cm_as_resolutorid"), Supervisor);
            Pasa(Cambio("sanic_cm_as_estadoaprobacion", "sanic_cm_as_motivorechazo", "sanic_cm_as_resolutorid"), Supervisor);
        }

        [Fact]
        public void La_clave_primaria_en_el_Target_no_cuenta_como_columna()
        {
            var t = Cambio("sanic_cm_as_orden");
            t["sanic_cm_tbl_asignacionid"] = t.Id;
            Pasa(t, Localizador);
        }

        [Fact]
        public void El_Supervisor_cierra_al_rechazar()
        {
            Pasa(Cambio("sanic_cm_as_estado", "sanic_cm_as_motivo", "sanic_cm_as_fechacierre"), Supervisor);
        }

        [Theory]
        [InlineData("sanic_cm_as_retironio")]
        [InlineData("sanic_cm_as_tipo")]
        [InlineData("sanic_cm_as_cuentaid")]
        [InlineData("sanic_cm_as_orden")]
        [InlineData("sanic_cm_as_solicitadopor")]
        [InlineData("sanic_cm_as_ultimacarga")]
        public void El_Supervisor_no_cambia_columnas_que_no_usa(string columna)
        {
            var ex = Rechaza(Cambio("sanic_cm_as_estadoaprobacion", columna), Supervisor);
            Assert.Contains(columna, ex.Message);
            Assert.DoesNotContain("sanic_cm_as_estadoaprobacion", ex.Message);
        }

        [Fact]
        public void El_Localizador_solo_cambia_el_orden()
        {
            Pasa(Cambio("sanic_cm_as_orden"), Localizador);
            Rechaza(Cambio("sanic_cm_as_estado"), Localizador);
            Rechaza(Cambio("sanic_cm_as_orden", "sanic_cm_as_agenteid"), Localizador);
        }

        [Fact]
        public void H1_el_Solicitante_no_edita_ninguna_columna_de_la_asignacion()
        {
            // Matriz de roles H1 (dueno, 2026-10-19): volver a pedir una Especial cerrada lo hace SolicitarEspecial con el
            // servicio del sistema (DAT-14); el Solicitante solo pide y lee. Antes editaba retiro y notas, y hasta cerraba
            // la Especial que S-7 le compartia ya aprobada.
            foreach (var columna in new[] { "sanic_cm_as_estado", "sanic_cm_as_estadoaprobacion", "sanic_cm_as_retironio",
                "sanic_cm_as_retirousd", "sanic_cm_as_notas", "sanic_cm_as_solicitadopor", "sanic_cm_as_fechaasignada",
                "sanic_cm_as_agenteid", "sanic_cm_as_resolutorid", "sanic_cm_as_motivo", "sanic_cm_as_motivorechazo",
                "sanic_cm_as_fechacierre", "sanic_cm_as_tipo", "sanic_cm_as_cuentaid", "sanic_cm_as_orden" })
            {
                Rechaza(Cambio(columna), Solicitante);
            }
        }

        [Fact]
        public void Con_dos_roles_se_suman_las_columnas_permitidas()
        {
            Pasa(Cambio("sanic_cm_as_orden", "sanic_cm_as_estadoaprobacion"), Localizador, Supervisor);
        }

        [Theory]
        [InlineData(Admin)]
        [InlineData(Servicio)]
        public void Admin_y_Servicio_cambian_cualquier_columna(string rol)
        {
            Pasa(Cambio("sanic_cm_as_tipo", "sanic_cm_as_cuentaid", "sanic_cm_as_retironio", "sanic_cm_as_orden"), Supervisor, rol);
        }

        [Fact]
        public void Sin_roles_de_CM_no_se_frena_la_carga()
        {
            Pasa(Cambio("sanic_cm_as_tipo", "sanic_cm_as_cuentaid", "sanic_cm_as_clavepan", "sanic_cm_as_ultimacarga",
                "sanic_cm_as_fechaasignada", "sanic_cm_as_agenteid"));
        }

        [Theory]
        [InlineData(BackOffice)]
        [InlineData(SinCobrador)]
        public void Un_rol_de_CM_sin_columnas_definidas_no_escribe_ninguna(string rol)
        {
            Rechaza(Cambio("sanic_cm_as_notas"), rol);
        }

        [Fact]
        public void El_mensaje_cita_columnas_y_nunca_valores()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t["sanic_cm_as_notas"] = "4111 1111 1111 1111 nota";
            var h = Actualizar(t, Localizador);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ColumnasAsignacion()));
            Assert.Contains("sanic_cm_as_notas", ex.Message);
            Assert.DoesNotContain("4111", ex.Message);
            Assert.DoesNotContain("4111", h.TodoElTexto());
        }
    

        // ------------------------------------------------------------------ S-7: "Solicitado por"

        private static Entity ConSolicitadoPor(Guid usuario)
        {
            var t = Cambio("sanic_cm_as_estado", "sanic_cm_as_estadoaprobacion");
            t["sanic_cm_as_solicitadopor"] = new EntityReference("systemuser", usuario);
            return t;
        }

        [Fact]
        public void S7_H1_ni_el_Solicitante_ni_el_Supervisor_cambian_Solicitado_por()
        {
            // RepartoAsignacion comparte la Especial aprobada con "Solicitado por": ningun rol de persona lo edita.
            var otro = Guid.NewGuid();
            foreach (var rol in new[] { Solicitante, Supervisor })
            {
                Rechaza(ConSolicitadoPor(Harness.UsuarioLlamador), rol);
                var h = Actualizar(ConSolicitadoPor(otro), rol);
                Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new ColumnasAsignacion()));
                Assert.DoesNotContain(otro.ToString(), h.TodoElTexto());
            }
        }

        [Theory]
        [InlineData(Admin)]
        [InlineData(Servicio)]
        public void S7_Admin_y_Servicio_ponen_a_cualquiera_como_Solicitado_por(string rol)
        {
            Pasa(ConSolicitadoPor(Guid.NewGuid()), rol);
        }

        [Fact]
        public void S7_sin_roles_de_CM_se_pone_a_cualquiera_como_Solicitado_por()
        {
            // SolicitarEspecial reactiva con el servicio del sistema y pone a quien la pidio.
            Pasa(ConSolicitadoPor(Guid.NewGuid()));
        }
    }
}
