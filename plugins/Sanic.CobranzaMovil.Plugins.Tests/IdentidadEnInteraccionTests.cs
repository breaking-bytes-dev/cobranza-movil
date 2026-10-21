using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Borrado por salida de cartera: la interaccion guarda los ultimos 4, el CIF y la clave HMAC de su cuenta, para seguir
    /// identificandola en el core cuando la cuenta se borre. Solo lo vacio y nunca rechaza (F8).
    /// </summary>
    public class IdentidadEnInteraccionTests
    {
        private const string Interaccion = "sanic_cm_tbl_interaccion";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string CuentaId = "sanic_cm_in_cuentaid";
        private const string Ultimos4 = "sanic_cm_in_ultimos4";
        private const string Cif = "sanic_cm_in_cifcliente";
        private const string Clave = "sanic_cm_in_clavepan";
        private const string ClaveHmac = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

        private static Entity LaCuenta(string u4 = "9477", string cif = "041-061101-7518D", string clave = ClaveHmac)
        {
            var c = new Entity(Cuenta, Guid.NewGuid());
            if (u4 != null) c["sanic_cm_ct_ultimos4"] = u4;
            if (cif != null) c["sanic_cm_ct_cifcliente"] = cif;
            if (clave != null) c["sanic_cm_ct_clavepan"] = clave;
            return c;
        }

        private static Harness Con(Entity target, params Entity[] cuentas)
        {
            var h = new Harness("Create", Interaccion).ConTarget(target);
            h.Service.Setup(s => s.RetrieveMultiple(It.IsAny<QueryBase>())).Returns(new EntityCollection(cuentas.ToList()));
            return h;
        }

        private static Entity Nueva(Guid? cuenta)
        {
            var t = new Entity(Interaccion, Guid.NewGuid());
            if (cuenta.HasValue) t[CuentaId] = new EntityReference(Cuenta, cuenta.Value);
            return t;
        }

        [Fact]
        public void Copia_ultimos_4_CIF_y_clave_de_la_cuenta()
        {
            var cuenta = LaCuenta();
            var h = Con(Nueva(cuenta.Id), cuenta);
            h.Ejecutar(new IdentidadEnInteraccion());
            Assert.Equal("9477", h.Target.GetAttributeValue<string>(Ultimos4));
            Assert.Equal("041-061101-7518D", h.Target.GetAttributeValue<string>(Cif));
            Assert.Equal(ClaveHmac, h.Target.GetAttributeValue<string>(Clave));
        }

        [Fact]
        public void Busca_la_cuenta_por_su_id_con_una_sola_fila()
        {
            var cuenta = LaCuenta();
            var h = Con(Nueva(cuenta.Id), cuenta);
            h.Ejecutar(new IdentidadEnInteraccion());
            var q = (QueryExpression)h.Service.Invocations.Single(x => x.Method.Name == "RetrieveMultiple").Arguments[0];
            Assert.Equal(Cuenta, q.EntityName);
            Assert.Equal(1, q.TopCount);
            var c = Assert.Single(q.Criteria.Conditions);
            Assert.Equal("sanic_cm_tbl_cuentaid", c.AttributeName);
            Assert.Equal(cuenta.Id, c.Values[0]);
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void No_pisa_lo_que_ya_trae_la_interaccion()
        {
            var cuenta = LaCuenta();
            var t = Nueva(cuenta.Id);
            t[Ultimos4] = "1234";
            var h = Con(t, cuenta);
            h.Ejecutar(new IdentidadEnInteraccion());
            Assert.Equal("1234", h.Target.GetAttributeValue<string>(Ultimos4));
            Assert.Equal("041-061101-7518D", h.Target.GetAttributeValue<string>(Cif));
        }

        [Fact]
        public void Sin_cuenta_no_consulta_ni_rechaza()
        {
            var h = Con(Nueva(null));
            h.Ejecutar(new IdentidadEnInteraccion());
            Assert.Equal(0, h.ConsultasAlServicio());
            Assert.False(h.Target.Contains(Ultimos4));
        }

        [Fact]
        public void Cuenta_que_no_existe_no_rechaza()
        {
            var h = Con(Nueva(Guid.NewGuid()));
            h.Ejecutar(new IdentidadEnInteraccion());
            Assert.False(h.Target.Contains(Cif));
        }

        [Fact]
        public void Lo_vacio_de_la_cuenta_no_se_copia()
        {
            var cuenta = LaCuenta(cif: "  ", clave: null);
            var h = Con(Nueva(cuenta.Id), cuenta);
            h.Ejecutar(new IdentidadEnInteraccion());
            Assert.Equal("9477", h.Target.GetAttributeValue<string>(Ultimos4));
            Assert.False(h.Target.Contains(Cif));
            Assert.False(h.Target.Contains(Clave));
        }

        [Fact]
        public void Nunca_traza_los_valores()
        {
            var cuenta = LaCuenta();
            var h = Con(Nueva(cuenta.Id), cuenta);
            h.Ejecutar(new IdentidadEnInteraccion());
            var texto = h.TodoElTexto();
            Assert.DoesNotContain("9477", texto);
            Assert.DoesNotContain("041-061101", texto);
            Assert.DoesNotContain(ClaveHmac, texto);
        }

        [Fact]
        public void Solo_corre_en_el_Create_PreOperation_de_la_interaccion()
        {
            var h = new Harness("Update", Interaccion).ConTarget(Nueva(Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new IdentidadEnInteraccion()));
        }
    }
}
