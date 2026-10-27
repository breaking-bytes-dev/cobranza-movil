using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// La grabacion en el CORE la controla el servidor: una interaccion Grabada no se vuelve a grabar (dos personas no
    /// pisan el codigo de la otra), pasar a Grabado exige el codigo, y la fecha y el usuario los pone el servidor.
    /// El BackOffice escribe solo las columnas de grabacion.
    /// </summary>
    public class GrabacionInteraccionTests
    {
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Estado = "sanic_cm_in_estadograbacion";
        private const string Codigo = "sanic_cm_in_codigograbacion";
        private const string Fecha = "sanic_cm_in_fechagrabacion";
        private const string GrabadoPor = "sanic_cm_in_grabadopor";
        private const string Comentario = "sanic_cm_in_comentario";
        private const int Pendiente = 1, Grabado = 2;

        private const string BackOffice = "SR | CM | BackOffice";
        private const string Localizador = "SR | CM | Localizador";
        private const string Admin = "SR | CM | Admin";

        private static Entity Previa(int estado, string codigo)
        {
            var p = new Entity(Interaccion, Guid.NewGuid());
            p[Estado] = new OptionSetValue(estado);
            if (codigo != null) p[Codigo] = codigo;
            return p;
        }

        private static Entity Grabar(Entity previa, string codigo)
        {
            var t = new Entity(Interaccion, previa.Id);
            t[Estado] = new OptionSetValue(Grabado);
            if (codigo != null) t[Codigo] = codigo;
            return t;
        }

        private static Harness Actualizar(Entity target, Entity previa, params string[] roles)
        {
            return new Harness("Update", Interaccion).ConTarget(target).ConPreImage(previa).RolesDelLlamador(roles);
        }

        private static InvalidPluginExecutionException Rechaza(Harness h)
        {
            return Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new GrabacionInteraccion()));
        }

        [Fact]
        public void Falla_si_se_registra_en_Create()
        {
            var h = new Harness("Create", Interaccion).ConTarget(new Entity(Interaccion)).RolesDelLlamador(BackOffice);
            Assert.Contains("registro incorrecto", Rechaza(h).Message);
        }

        [Fact]
        public void Grabar_estampa_fecha_y_usuario_del_servidor_y_pisa_lo_del_cliente()
        {
            var previa = Previa(Pendiente, null);
            var t = Grabar(previa, "TRX-1");
            t[Fecha] = new DateTime(2000, 1, 1);
            t[GrabadoPor] = new EntityReference("systemuser", Guid.NewGuid());
            var antes = DateTime.UtcNow.AddSeconds(-1);

            Actualizar(t, previa, BackOffice).Ejecutar(new GrabacionInteraccion());

            var fecha = t.GetAttributeValue<DateTime>(Fecha);
            Assert.True(fecha >= antes && fecha <= DateTime.UtcNow.AddSeconds(1));
            Assert.Equal(Harness.UsuarioLlamador, t.GetAttributeValue<EntityReference>(GrabadoPor).Id);
            Assert.Equal("systemuser", t.GetAttributeValue<EntityReference>(GrabadoPor).LogicalName);
        }

        [Fact]
        public void La_clave_primaria_en_el_Target_no_cuenta_como_columna()
        {
            // Dataverse agrega sanic_cm_tbl_interaccionid al Target de un Update.
            var previa = Previa(Pendiente, null);
            var t = Grabar(previa, "TRX-1");
            t["sanic_cm_tbl_interaccionid"] = previa.Id;
            Actualizar(t, previa, BackOffice).Ejecutar(new GrabacionInteraccion());
            Assert.True(t.Contains(Fecha));

            var soloClave = new Entity(Interaccion, previa.Id);
            soloClave["sanic_cm_tbl_interaccionid"] = previa.Id;
            var h = new Harness("Update", Interaccion).ConTarget(soloClave).RolesDelLlamador(BackOffice);
            h.Ejecutar(new GrabacionInteraccion());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Grabar_sin_codigo_se_rechaza()
        {
            var previa = Previa(Pendiente, null);
            Assert.Contains("código", Rechaza(Actualizar(Grabar(previa, null), previa, BackOffice)).Message);
            Assert.Contains("código", Rechaza(Actualizar(Grabar(previa, "  "), previa, BackOffice)).Message);
        }

        [Fact]
        public void El_codigo_puede_venir_de_antes()
        {
            var previa = Previa(Pendiente, "TRX-1");
            var t = Grabar(previa, null);
            Actualizar(t, previa, BackOffice).Ejecutar(new GrabacionInteraccion());
            Assert.True(t.Contains(Fecha));
        }

        [Fact]
        public void Una_interaccion_ya_grabada_no_se_vuelve_a_grabar()
        {
            // Dos personas con la misma interaccion abierta: la segunda recibe el error en lugar de pisar el codigo.
            var previa = Previa(Grabado, "TRX-1");
            Assert.Contains("ya fue grabada", Rechaza(Actualizar(Grabar(previa, "TRX-2"), previa, BackOffice)).Message);

            var soloCodigo = new Entity(Interaccion, previa.Id);
            soloCodigo[Codigo] = "TRX-2";
            Assert.Contains("ya fue grabada", Rechaza(Actualizar(soloCodigo, previa, BackOffice)).Message);
        }

        [Fact]
        public void El_Admin_puede_corregir_una_grabada_y_se_vuelve_a_estampar()
        {
            var previa = Previa(Grabado, "TRX-1");
            var t = Grabar(previa, "TRX-9");
            Actualizar(t, previa, Admin).Ejecutar(new GrabacionInteraccion());
            Assert.True(t.Contains(Fecha));
            Assert.Equal(Harness.UsuarioLlamador, t.GetAttributeValue<EntityReference>(GrabadoPor).Id);
        }

        [Fact]
        public void El_BackOffice_no_cambia_lo_que_capturo_el_cobrador()
        {
            var previa = Previa(Pendiente, null);
            var t = Grabar(previa, "TRX-1");
            t[Comentario] = "otro";
            var h = Actualizar(t, previa, BackOffice);
            var ex = Rechaza(h);
            Assert.Equal("Tu rol no puede cambiar esos datos de la interacción.", ex.Message);
            Assert.Contains(Comentario, h.TodoElTexto());
            Assert.DoesNotContain("otro", ex.Message);
            Assert.DoesNotContain("otro", h.TodoElTexto());
        }

        [Fact]
        public void El_Localizador_no_cambia_ninguna_columna()
        {
            var previa = Previa(Pendiente, null);
            var t = new Entity(Interaccion, previa.Id);
            t[Comentario] = "x";
            Rechaza(Actualizar(t, previa, Localizador));
        }

        [Fact]
        public void Fecha_y_usuario_sin_grabar_se_descartan()
        {
            var previa = Previa(Pendiente, null);
            var t = new Entity(Interaccion, previa.Id);
            t[Codigo] = "TRX-1";
            t[Fecha] = new DateTime(2000, 1, 1);
            t[GrabadoPor] = new EntityReference("systemuser", Guid.NewGuid());
            Actualizar(t, previa, BackOffice).Ejecutar(new GrabacionInteraccion());
            Assert.False(t.Contains(Fecha));
            Assert.False(t.Contains(GrabadoPor));
            Assert.Equal("TRX-1", t.GetAttributeValue<string>(Codigo));
        }

        [Fact]
        public void Sin_roles_de_CM_no_hay_restriccion_de_columnas_pero_si_codigo()
        {
            var previa = Previa(Pendiente, null);
            var t = new Entity(Interaccion, previa.Id);
            t[Comentario] = "x";
            Actualizar(t, previa).Ejecutar(new GrabacionInteraccion());
            Rechaza(Actualizar(Grabar(previa, null), previa));
        }

        [Fact]
        public void Un_cambio_sin_columnas_de_negocio_no_consulta_roles_ni_pide_imagen()
        {
            var t = new Entity(Interaccion, Guid.NewGuid());
            t["ownerid"] = new EntityReference("systemuser", Guid.NewGuid());
            var h = new Harness("Update", Interaccion).ConTarget(t).RolesDelLlamador(BackOffice);
            h.Ejecutar(new GrabacionInteraccion());
            Assert.Equal(0, h.ConsultasAlServicio());
        }

        [Fact]
        public void Sin_imagen_previa_falla_cerrado()
        {
            var t = Grabar(Previa(Pendiente, null), "TRX-1");
            var h = new Harness("Update", Interaccion).ConTarget(t).RolesDelLlamador(BackOffice);
            Assert.Contains("PreImage", Rechaza(h).Message);
        }
    }
}
