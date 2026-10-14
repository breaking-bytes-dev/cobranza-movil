using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Lo que hace la carga diaria al volver a traer una asignacion que ya existe:
    /// - DAT-10: una Diaria o Incobrable que cerro el sistema (reconciliacion "fuera de cartera" o reclasificacion) y
    ///   vuelve en el CSV se reabre. Los cierres de una persona y las Especiales no se tocan.
    /// - DAT-31: no pisa la fecha de asignacion (antes la reemplazaba por la fecha de carga cada dia).
    /// </summary>
    public class CargaSobreAsignacionTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Estado = "sanic_cm_as_estado";
        private const string Motivo = "sanic_cm_as_motivo";
        private const string FechaCierre = "sanic_cm_as_fechacierre";
        private const string FechaAsignacion = "sanic_cm_as_fechasignacion";
        private const string UltimaCarga = "sanic_cm_as_ultimacarga";
        private const string Tipo = "sanic_cm_as_tipo";
        private const int Diaria = 1, Incobrable = 2, Especial = 3;

        private static Entity Previa(bool estado, int tipo, string motivo, DateTime? fechaAsignacion = null)
        {
            var p = new Entity(Asignacion, Guid.NewGuid());
            p[Estado] = estado;
            p[Tipo] = new OptionSetValue(tipo);
            if (motivo != null) p[Motivo] = motivo;
            if (fechaAsignacion.HasValue) p[FechaAsignacion] = fechaAsignacion.Value;
            return p;
        }

        private static Entity Carga(Entity previa)
        {
            var t = new Entity(Asignacion, previa.Id);
            t[UltimaCarga] = DateTime.UtcNow.Date;
            t[FechaAsignacion] = DateTime.UtcNow.Date;
            t["sanic_cm_as_agenteid"] = new EntityReference("sanic_cm_tbl_agente", Guid.NewGuid());
            return t;
        }

        private static Entity Ejecutar(Entity previa, Entity target = null)
        {
            target = target ?? Carga(previa);
            var h = new Harness("Update", Asignacion).ConTarget(target).ConPreImage(previa);
            h.Ejecutar(new CargaSobreAsignacion());
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.Equal(0, h.RetrievesAlServicio());
            return target;
        }

        private static void Reabierta(Entity t)
        {
            Assert.True(t.GetAttributeValue<bool>(Estado));
            Assert.True(t.Contains(Motivo) && t[Motivo] == null);
            Assert.True(t.Contains(FechaCierre) && t[FechaCierre] == null);
        }

        [Fact]
        public void Falla_si_se_registra_en_Create()
        {
            var h = new Harness("Create", Asignacion).ConTarget(new Entity(Asignacion));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new CargaSobreAsignacion()));
            Assert.Contains("registro incorrecto", ex.Message);
        }

        [Fact]
        public void Una_Diaria_cerrada_fuera_de_cartera_que_vuelve_se_reabre()
        {
            Reabierta(Ejecutar(Previa(false, Diaria, "fuera de cartera")));
        }

        [Fact]
        public void El_motivo_se_compara_sin_mayusculas_ni_espacios()
        {
            Reabierta(Ejecutar(Previa(false, Diaria, "  Fuera de Cartera ")));
        }

        [Fact]
        public void Una_Incobrable_reclasificada_que_vuelve_se_reabre()
        {
            Reabierta(Ejecutar(Previa(false, Incobrable, "Reclasificada a Diaria")));
        }

        [Theory]
        [InlineData("Pagada")]
        [InlineData("Cliente fallecido — detalle")]
        [InlineData(null)]
        public void Un_cierre_de_una_persona_no_se_reabre(string motivo)
        {
            var t = Ejecutar(Previa(false, Diaria, motivo));
            Assert.False(t.Contains(Estado));
        }

        [Fact]
        public void Una_Especial_cerrada_no_se_reabre()
        {
            Assert.False(Ejecutar(Previa(false, Especial, "fuera de cartera")).Contains(Estado));
        }

        [Fact]
        public void Una_activa_no_cambia_de_estado()
        {
            Assert.False(Ejecutar(Previa(true, Diaria, null)).Contains(Estado));
        }

        [Fact]
        public void Si_el_cambio_trae_el_estado_se_respeta()
        {
            var previa = Previa(false, Diaria, "fuera de cartera");
            var t = Carga(previa);
            t[Estado] = false;
            Assert.False(Ejecutar(previa, t).GetAttributeValue<bool>(Estado));
        }

        [Fact]
        public void La_carga_no_pisa_la_fecha_de_asignacion()
        {
            var t = Ejecutar(Previa(true, Diaria, null, new DateTime(2026, 6, 22)));
            Assert.False(t.Contains(FechaAsignacion));
            Assert.True(t.Contains(UltimaCarga));
        }

        [Fact]
        public void Sin_fecha_de_asignacion_previa_la_carga_la_completa()
        {
            Assert.True(Ejecutar(Previa(true, Diaria, null)).Contains(FechaAsignacion));
        }

        [Fact]
        public void Un_cambio_sin_ultima_carga_no_hace_nada()
        {
            var previa = Previa(false, Diaria, "fuera de cartera", new DateTime(2026, 6, 22));
            var t = new Entity(Asignacion, previa.Id);
            t[FechaAsignacion] = DateTime.UtcNow;
            Ejecutar(previa, t);
            Assert.True(t.Contains(FechaAsignacion));
            Assert.False(t.Contains(Estado));
        }

        [Fact]
        public void Sin_imagen_previa_falla_cerrado()
        {
            var t = Carga(Previa(false, Diaria, null));
            var h = new Harness("Update", Asignacion).ConTarget(t);
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new CargaSobreAsignacion()));
            Assert.Contains("PreImage", ex.Message);
        }
    }
}
