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
        public void El_Solicitante_reactiva_su_Especial_cerrada()
        {
            // scrNuevaSolicitud, rama b): las columnas que escribe al volver a pedir una Especial cerrada.
            Pasa(Cambio("sanic_cm_as_estado", "sanic_cm_as_estadoaprobacion", "sanic_cm_as_retironio", "sanic_cm_as_retirousd",
                "sanic_cm_as_notas", "sanic_cm_as_solicitadopor", "sanic_cm_as_fechasignacion", "sanic_cm_as_agenteid",
                "sanic_cm_as_resolutorid", "sanic_cm_as_motivo", "sanic_cm_as_motivorechazo", "sanic_cm_as_fechacierre"), Solicitante);
        }

        [Fact]
        public void El_Solicitante_no_cambia_tipo_cuenta_ni_orden()
        {
            Rechaza(Cambio("sanic_cm_as_tipo"), Solicitante);
            Rechaza(Cambio("sanic_cm_as_cuentaid"), Solicitante);
            Rechaza(Cambio("sanic_cm_as_orden"), Solicitante);
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
                "sanic_cm_as_fechasignacion", "sanic_cm_as_agenteid"));
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
    }
}
