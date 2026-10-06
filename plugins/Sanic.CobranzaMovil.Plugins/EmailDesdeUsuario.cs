using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// El correo del agente (sanic_cm_ag_emailusuario) se deriva SIEMPRE del usuario vinculado: su UPN en minusculas.
    /// La app del cobrador identifica a su agente con Lower(User().Email), y User().Email devuelve el UPN, no el SMTP
    /// (documentado por Microsoft). El backoffice guardaba Lower('Primary Email'): si el UPN y el correo difieren, el
    /// cobrador quedaba sin agente ("Sin nombre") en todo lo que registra.
    /// - Se vincula un usuario: correo = UPN en minusculas (pisa lo que venga en el Target).
    /// - Se desvincula: correo vacio.
    /// - Un Create sin usuario (la carga de Smelter) o un Update que no toca el usuario: no hace nada.
    ///
    /// Lee el UPN con el servicio del sistema (dato de configuracion del usuario, no de negocio).
    /// Registro: PreOperation sincrono. Create y Update de sanic_cm_tbl_agente; el Update filtrado por sanic_cm_ag_usuarioid.
    /// </summary>
    public sealed class EmailDesdeUsuario : PluginBase
    {
        protected override string Nombre
        {
            get { return "EmailDesdeUsuario"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Agente, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            if (!target.Contains(Esquema.AgenteUsuario))
            {
                local.Trace("el cambio no toca el usuario; sin cambios.");
                return;
            }

            var usuario = target.GetAttributeValue<EntityReference>(Esquema.AgenteUsuario);
            if (usuario == null)
            {
                target[Esquema.AgenteEmail] = null;
                local.Trace("usuario desvinculado: correo vacio.");
                return;
            }

            var fila = local.ServicioSistema.Retrieve(Esquema.Usuario, usuario.Id, new ColumnSet(Esquema.UsuarioUpn));
            var upn = fila == null ? null : fila.GetAttributeValue<string>(Esquema.UsuarioUpn);
            target[Esquema.AgenteEmail] = string.IsNullOrWhiteSpace(upn) ? null : upn.Trim().ToLowerInvariant();
            local.Trace(string.IsNullOrWhiteSpace(upn) ? "el usuario no tiene UPN: correo vacio." : "correo derivado del UPN del usuario.");
        }
    }
}
