using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Custom API global sanic_cm_MisRoles: perfiles de CM del usuario que llama, para que la app del backoffice arme
    /// su menu y sus permisos. Devuelve Roles = perfiles sin el prefijo "SR | CM | ", ordenados y separados por "|"
    /// (por ejemplo "Admin|BackOffice"); vacio si no tiene ninguno.
    /// Mira los roles directos y los de cualquier equipo del que sea miembro (tambien los de grupo de Entra ID), sin
    /// depender del nombre de los equipos. Cualquier usuario puede llamarla: solo devuelve lo suyo.
    /// "Servicio - Cobranza" no se devuelve: es la identidad de las integraciones, no un perfil de la app.
    /// No traza nombres de roles: solo la cantidad.
    /// </summary>
    public sealed class MisRoles : PluginBase
    {
        protected override string Nombre
        {
            get { return "MisRoles"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            if (!string.Equals(ctx.MessageName, Esquema.MensajeMisRoles, StringComparison.Ordinal)
                || ctx.Stage != Esquema.EtapaMainOperation)
            {
                throw local.Error("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeMisRoles + ".");
            }

            var perfiles = Perfiles(RolesCm.DelLlamador(local));
            ctx.OutputParameters[Esquema.SalidaRoles] = string.Join("|", perfiles);
            local.Trace("{0} perfiles de CM.", perfiles.Count);
        }

        /// <summary>Nombres de rol de CM sin el prefijo, sin la identidad de servicio, en orden fijo.</summary>
        public static IList<string> Perfiles(RolesCm roles)
        {
            return roles.Nombres
                .Where(n => !string.Equals(n, RolesCm.Servicio, StringComparison.Ordinal))
                .Select(n => n.Substring(Esquema.PrefijoRolCm.Length))
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }
    }
}
