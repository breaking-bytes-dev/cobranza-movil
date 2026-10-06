using System;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// La app del cobrador identifica a su agente con Lower(User().Email), y User().Email es el UPN (no el SMTP).
    /// El backoffice guardaba Lower('Primary Email'): si el UPN y el correo difieren, el cobrador queda "Sin nombre".
    /// El correo del agente se deriva siempre del usuario vinculado: UPN en minusculas.
    /// </summary>
    public class EmailDesdeUsuarioTests
    {
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Usuario = "systemuser";
        private const string AgUsuario = "sanic_cm_ag_usuarioid";
        private const string AgEmail = "sanic_cm_ag_emailusuario";
        private const string Upn = "domainname";

        private static Entity UsuarioCon(Guid id, string upn)
        {
            var u = new Entity(Usuario, id);
            if (upn != null) u[Upn] = upn;
            return u;
        }

        [Fact]
        public void Al_crear_con_usuario_el_correo_es_el_UPN_en_minusculas()
        {
            var uid = Guid.NewGuid();
            var t = new Entity(Agente, Guid.NewGuid());
            t[AgUsuario] = new EntityReference(Usuario, uid);
            var h = new Harness("Create", Agente).ConTarget(t).RetrieveDevuelve(UsuarioCon(uid, "User5@Contoso.OnMicrosoft.com"), Upn);

            h.Ejecutar(new EmailDesdeUsuario());

            Assert.Equal("user5@contoso.onmicrosoft.com", t.GetAttributeValue<string>(AgEmail));
            Assert.Equal(1, h.RetrievesAlServicio());
        }

        [Fact]
        public void Pisa_el_correo_SMTP_que_manda_el_backoffice()
        {
            var uid = Guid.NewGuid();
            var t = new Entity(Agente, Guid.NewGuid());
            t[AgUsuario] = new EntityReference(Usuario, uid);
            t[AgEmail] = "juan.perez@banco.com.ni";
            var h = new Harness("Update", Agente).ConTarget(t).RetrieveDevuelve(UsuarioCon(uid, "jperez@banco.onmicrosoft.com"), Upn);

            h.Ejecutar(new EmailDesdeUsuario());

            Assert.Equal("jperez@banco.onmicrosoft.com", t.GetAttributeValue<string>(AgEmail));
        }

        [Fact]
        public void Al_desvincular_el_usuario_se_vacia_el_correo()
        {
            var t = new Entity(Agente, Guid.NewGuid());
            t[AgUsuario] = null;
            var h = new Harness("Update", Agente).ConTarget(t);

            h.Ejecutar(new EmailDesdeUsuario());

            Assert.True(t.Contains(AgEmail));
            Assert.Null(t[AgEmail]);
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Crear_sin_usuario_no_toca_nada_carga_de_Smelter()
        {
            var t = new Entity(Agente, Guid.NewGuid());
            t["sanic_cm_ag_nombre"] = "AGENTE CSV";
            var h = new Harness("Create", Agente).ConTarget(t);

            h.Ejecutar(new EmailDesdeUsuario());

            Assert.False(t.Contains(AgEmail));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Un_Update_que_no_toca_el_usuario_no_hace_nada()
        {
            var t = new Entity(Agente, Guid.NewGuid());
            t["sanic_cm_ag_activo"] = false;
            var h = new Harness("Update", Agente).ConTarget(t);

            h.Ejecutar(new EmailDesdeUsuario());

            Assert.False(t.Contains(AgEmail));
            Assert.Equal(0, h.RetrievesAlServicio());
        }

        [Fact]
        public void Usuario_sin_UPN_deja_el_correo_vacio()
        {
            var uid = Guid.NewGuid();
            var t = new Entity(Agente, Guid.NewGuid());
            t[AgUsuario] = new EntityReference(Usuario, uid);
            t[AgEmail] = "algo@banco.com.ni";
            var h = new Harness("Create", Agente).ConTarget(t).RetrieveDevuelve(UsuarioCon(uid, null), Upn);

            h.Ejecutar(new EmailDesdeUsuario());

            Assert.Null(t[AgEmail]);
        }

        [Fact]
        public void Registrado_en_otra_tabla_falla()
        {
            var h = new Harness("Create", "sanic_cm_tbl_pago").ConTarget(new Entity("sanic_cm_tbl_pago", Guid.NewGuid()));
            Assert.Throws<InvalidPluginExecutionException>(() => h.Ejecutar(new EmailDesdeUsuario()));
        }
    }
}
