using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Escala de la cartera: cada asignacion lleva un tramo 0..19 que sale de su cuenta, para que la app la traiga en partes
    /// por debajo del limite de 2000 filas. Estable, parejo, solo lo vacio y sin rechazar.
    /// </summary>
    public class TramoCarteraTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Tramo = "sanic_cm_as_tramo";
        private const int TramoEsperadoPython = 2; // calculado con scripts/pan/paso18_tramo_cartera.py

        private static Harness Crear(Guid? cuenta, int? tramo = null)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            if (cuenta.HasValue) t["sanic_cm_as_cuentaid"] = new EntityReference("sanic_cm_tbl_cuenta", cuenta.Value);
            if (tramo.HasValue) t[Tramo] = tramo.Value;
            var h = new Harness("Create", Asignacion).ConTarget(t);
            h.Ejecutar(new TramoCartera());
            return h;
        }

        [Fact]
        public void Pone_un_tramo_entre_0_y_19()
        {
            var h = Crear(Guid.NewGuid());
            var tramo = h.Target.GetAttributeValue<int>(Tramo);
            Assert.InRange(tramo, 0, 19);
        }

        [Fact]
        public void La_misma_cuenta_cae_siempre_en_el_mismo_tramo()
        {
            var cuenta = Guid.NewGuid();
            Assert.Equal(Crear(cuenta).Target.GetAttributeValue<int>(Tramo), Crear(cuenta).Target.GetAttributeValue<int>(Tramo));
            Assert.Equal(TramoCartera.Tramo(cuenta), Crear(cuenta).Target.GetAttributeValue<int>(Tramo));
        }

        [Fact]
        public void Reparte_parejo_y_ningun_tramo_pasa_el_limite()
        {
            // 30.000 cuentas: cada tramo queda cerca de 1.500 (el promedio), lejos del limite de 2000
            var r = new Random(20261021);
            var conteo = new int[20];
            for (var i = 0; i < 30000; i++)
            {
                var b = new byte[16];
                r.NextBytes(b);
                conteo[TramoCartera.Tramo(new Guid(b))]++;
            }
            Assert.True(conteo.Min() > 1300, "tramo mas chico: " + conteo.Min());
            Assert.True(conteo.Max() < 1700, "tramo mas grande: " + conteo.Max());
        }

        [Fact]
        public void Reparte_parejo_aunque_los_ids_sean_secuenciales()
        {
            // Dataverse crea ids secuenciales: solo cambian unos bytes entre uno y el siguiente
            var conteo = new int[20];
            var base_ = Guid.Parse("75459bb3-63cd-f111-a05b-7ced8d706b47").ToByteArray();
            for (var i = 0; i < 6000; i++)
            {
                var b = (byte[])base_.Clone();
                b[0] = (byte)(i & 0xFF);
                b[1] = (byte)((i >> 8) & 0xFF);
                conteo[TramoCartera.Tramo(new Guid(b))]++;
            }
            Assert.True(conteo.Min() > 220 && conteo.Max() < 380, "min " + conteo.Min() + " max " + conteo.Max());
        }

        [Fact]
        public void Mismo_valor_que_el_relleno_en_Python()
        {
            // En Python: fnv1a(uuid.UUID('75459bb3-63cd-f111-a05b-7ced8d706b47').bytes_le) % 20
            Assert.Equal(TramoEsperadoPython, TramoCartera.Tramo(Guid.Parse("75459bb3-63cd-f111-a05b-7ced8d706b47")));
        }

        [Fact]
        public void No_pisa_un_tramo_que_ya_viene()
        {
            Assert.Equal(7, Crear(Guid.NewGuid(), 7).Target.GetAttributeValue<int>(Tramo));
        }

        [Fact]
        public void Sin_cuenta_no_pone_tramo_ni_rechaza()
        {
            Assert.False(Crear(null).Target.Contains(Tramo));
        }

        [Fact]
        public void No_consulta_al_servidor()
        {
            var h = Crear(Guid.NewGuid());
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void La_cuenta_nueva_recibe_el_mismo_tramo_que_sus_asignaciones()
        {
            var id = Guid.NewGuid();
            var h = new Harness("Create", "sanic_cm_tbl_cuenta", 40).ConTarget(new Entity("sanic_cm_tbl_cuenta", id));
            h.Context.Setup(c => c.PrimaryEntityId).Returns(id);
            Entity escrita = null;
            h.Service.Setup(s => s.Update(Moq.It.IsAny<Entity>())).Callback<Entity>(e => escrita = e);
            h.Ejecutar(new TramoCartera());
            Assert.NotNull(escrita);
            Assert.Equal(id, escrita.Id);
            Assert.Equal(Crear(id).Target.GetAttributeValue<int>(Tramo), escrita.GetAttributeValue<int>("sanic_cm_ct_tramo"));
            Assert.Single(escrita.Attributes);
        }

        [Fact]
        public void La_cuenta_solo_en_el_Create_PostOperation()
        {
            var h = new Harness("Create", "sanic_cm_tbl_cuenta", 20).ConTarget(new Entity("sanic_cm_tbl_cuenta", Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new TramoCartera()));
        }

        [Fact]
        public void El_pago_recibe_el_tramo_de_su_interaccion()
        {
            var interaccion = Guid.NewGuid();
            var t = new Entity("sanic_cm_tbl_pago", Guid.NewGuid());
            t["sanic_cm_pg_interaccion"] = new EntityReference("sanic_cm_tbl_interaccion", interaccion);
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(t);
            h.Ejecutar(new TramoCartera());
            Assert.Equal(TramoCartera.Tramo(interaccion), h.Target.GetAttributeValue<int>("sanic_cm_pg_tramo"));
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void El_pago_sin_interaccion_queda_sin_tramo_y_no_rechaza()
        {
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(new Entity("sanic_cm_tbl_pago", Guid.NewGuid()));
            h.Ejecutar(new TramoCartera());
            Assert.False(h.Target.Contains("sanic_cm_pg_tramo"));
        }

        [Fact]
        public void El_pago_no_pisa_un_tramo_que_ya_viene()
        {
            var t = new Entity("sanic_cm_tbl_pago", Guid.NewGuid());
            t["sanic_cm_pg_interaccion"] = new EntityReference("sanic_cm_tbl_interaccion", Guid.NewGuid());
            t["sanic_cm_pg_tramo"] = 7;
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(t);
            h.Ejecutar(new TramoCartera());
            Assert.Equal(7, h.Target.GetAttributeValue<int>("sanic_cm_pg_tramo"));
        }

        [Fact]
        public void Solo_corre_en_el_Create_PreOperation_de_la_asignacion()
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            var h = new Harness("Update", Asignacion).ConTarget(t);
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new TramoCartera()));
        }
    }
}
