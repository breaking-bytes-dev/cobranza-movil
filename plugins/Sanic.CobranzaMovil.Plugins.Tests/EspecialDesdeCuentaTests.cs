using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// F3: la Especial de la carga toma el tipo, la nota y el retiro de la cuenta (la carga los escribe ahi y la app los
    /// lee de la asignacion). Solo lo vacio y solo sin solicitante: nunca pisa lo que escribio una persona.
    /// </summary>
    public class EspecialDesdeCuentaTests
    {
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Tipo = "sanic_cm_as_tipo";
        private const string CuentaId = "sanic_cm_as_cuentaid";
        private const string SolicitadoPor = "sanic_cm_as_solicitadopor";
        private const string Notas = "sanic_cm_as_notas";
        private const string RetiroNio = "sanic_cm_as_retironio";
        private const string RetiroUsd = "sanic_cm_as_retirousd";
        private const string UltimaCarga = "sanic_cm_as_ultimacarga";
        private const int Diaria = 1, Especial = 3;
        private static readonly string[] ColumnasCuenta =
            { "sanic_cm_ct_tipoespecial", "sanic_cm_ct_nota", "sanic_cm_ct_retironio", "sanic_cm_ct_retirousd" };

        private static Entity LaCuenta(Guid id, string tipo = "Judicial", string nota = "Llamar antes", decimal? nio = 29203.94m, decimal? usd = 0m)
        {
            var c = new Entity(Cuenta, id);
            if (tipo != null) c["sanic_cm_ct_tipoespecial"] = tipo;
            if (nota != null) c["sanic_cm_ct_nota"] = nota;
            if (nio.HasValue) c["sanic_cm_ct_retironio"] = nio.Value;
            if (usd.HasValue) c["sanic_cm_ct_retirousd"] = usd.Value;
            return c;
        }

        private static Entity Nueva(int tipo, Guid cuenta)
        {
            var t = new Entity(Asignacion, Guid.NewGuid());
            t[Tipo] = new OptionSetValue(tipo);
            t[CuentaId] = new EntityReference(Cuenta, cuenta);
            return t;
        }

        private static Harness Crear(Entity target, Entity cuenta)
        {
            var h = new Harness("Create", Asignacion).ConTarget(target);
            if (cuenta != null) h.RetrieveDevuelve(cuenta, ColumnasCuenta);
            h.Ejecutar(new EspecialDesdeCuenta());
            return h;
        }

        [Fact]
        public void La_Especial_de_la_carga_toma_tipo_nota_y_retiro_de_la_cuenta()
        {
            var id = Guid.NewGuid();
            var h = Crear(Nueva(Especial, id), LaCuenta(id));
            Assert.Equal("Judicial — Llamar antes", h.Target.GetAttributeValue<string>(Notas));
            Assert.Equal(29203.94m, h.Target.GetAttributeValue<decimal>(RetiroNio));
            Assert.Equal(0m, h.Target.GetAttributeValue<decimal>(RetiroUsd));
            Assert.DoesNotContain("Judicial", h.TodoElTexto());
            Assert.DoesNotContain("29203", h.TodoElTexto());
        }

        [Fact]
        public void Sin_nota_queda_el_tipo_con_el_separador()
        {
            var id = Guid.NewGuid();
            var h = Crear(Nueva(Especial, id), LaCuenta(id, nota: null));
            Assert.Equal("Judicial — ", h.Target.GetAttributeValue<string>(Notas));
        }

        [Fact]
        public void Sin_tipo_queda_solo_la_nota()
        {
            var id = Guid.NewGuid();
            var h = Crear(Nueva(Especial, id), LaCuenta(id, tipo: "  "));
            Assert.Equal("Llamar antes", h.Target.GetAttributeValue<string>(Notas));
        }

        [Fact]
        public void La_cuenta_sin_datos_no_escribe_nada()
        {
            var id = Guid.NewGuid();
            var h = Crear(Nueva(Especial, id), LaCuenta(id, tipo: null, nota: null, nio: null, usd: null));
            Assert.False(h.Target.Contains(Notas));
            Assert.False(h.Target.Contains(RetiroNio));
            Assert.False(h.Target.Contains(RetiroUsd));
        }

        [Fact]
        public void No_pisa_lo_que_ya_tiene_la_asignacion()
        {
            var id = Guid.NewGuid();
            var t = Nueva(Especial, id);
            t[Notas] = "Cobro especial — pedido";
            t[RetiroNio] = 1500m;
            var h = Crear(t, LaCuenta(id));
            Assert.Equal("Cobro especial — pedido", h.Target.GetAttributeValue<string>(Notas));
            Assert.Equal(1500m, h.Target.GetAttributeValue<decimal>(RetiroNio));
            Assert.Equal(0m, h.Target.GetAttributeValue<decimal>(RetiroUsd));
        }

        [Fact]
        public void Con_todo_completo_no_consulta()
        {
            var t = Nueva(Especial, Guid.NewGuid());
            t[Notas] = "VIP — x";
            t[RetiroNio] = 1m;
            t[RetiroUsd] = 0m;
            var h = Crear(t, null);
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void La_Especial_pedida_por_una_persona_no_se_toca()
        {
            var id = Guid.NewGuid();
            var t = Nueva(Especial, id);
            t[SolicitadoPor] = new EntityReference("systemuser", Guid.NewGuid());
            var h = Crear(t, LaCuenta(id));
            Assert.False(h.Target.Contains(Notas));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Una_Diaria_no_se_toca()
        {
            var id = Guid.NewGuid();
            var h = Crear(Nueva(Diaria, id), LaCuenta(id));
            Assert.False(h.Target.Contains(Notas));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void En_la_carga_de_otro_dia_completa_la_Especial_que_quedo_vacia()
        {
            var id = Guid.NewGuid();
            var previa = Nueva(Especial, id);
            var t = new Entity(Asignacion, previa.Id);
            t[UltimaCarga] = DateTime.UtcNow.Date;
            var h = new Harness("Update", Asignacion).ConTarget(t).ConPreImage(previa);
            h.RetrieveDevuelve(LaCuenta(id), ColumnasCuenta);
            h.Ejecutar(new EspecialDesdeCuenta());
            Assert.Equal("Judicial — Llamar antes", t.GetAttributeValue<string>(Notas));
            Assert.Equal(29203.94m, t.GetAttributeValue<decimal>(RetiroNio));
        }

        [Fact]
        public void En_la_carga_no_pisa_lo_que_ya_tenia()
        {
            var id = Guid.NewGuid();
            var previa = Nueva(Especial, id);
            previa[Notas] = "Judicial — editada";
            previa[RetiroNio] = 100m;
            previa[RetiroUsd] = 0m;
            var t = new Entity(Asignacion, previa.Id);
            t[UltimaCarga] = DateTime.UtcNow.Date;
            var h = new Harness("Update", Asignacion).ConTarget(t).ConPreImage(previa);
            h.Ejecutar(new EspecialDesdeCuenta());
            Assert.False(t.Contains(Notas));
            Assert.False(t.Contains(RetiroNio));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Registrado_en_PostOperation_falla()
        {
            var h = new Harness("Create", Asignacion, 40).ConTarget(Nueva(Especial, Guid.NewGuid()));
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new EspecialDesdeCuenta()));
            Assert.Contains("registro incorrecto", ex.Message);
        }
    }
}
