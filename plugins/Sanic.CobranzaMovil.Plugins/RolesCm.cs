using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Roles de CM (prefijo "SR | CM | ") del usuario que llama, directos y por equipo: en DEV llegan por equipo
    /// ("CM · Localizadores · DEV"); si solo se miraran los directos, nadie tendria rol de CM.
    /// Se leen con el servicio del sistema: una persona no tiene por que poder leer la tabla de roles.
    ///
    /// Sin roles de CM no es una persona de la operacion: SYSTEM (la carga de Smelter, los plugins que escriben con el
    /// servicio del sistema) o un administrador del entorno. Los plugins que usan esta clase no los frenan.
    /// </summary>
    public sealed class RolesCm
    {
        public const string Localizador = Esquema.PrefijoRolCm + "Localizador";
        public const string Solicitante = Esquema.PrefijoRolCm + "Solicitante";
        public const string Supervisor = Esquema.PrefijoRolCm + "Supervisor";
        public const string BackOffice = Esquema.PrefijoRolCm + "BackOffice";
        public const string Admin = Esquema.PrefijoRolCm + "Admin";
        public const string Servicio = Esquema.PrefijoRolCm + "Servicio - Cobranza";

        private readonly HashSet<string> nombres;

        private RolesCm(HashSet<string> nombres)
        {
            this.nombres = nombres;
        }

        public static RolesCm DelLlamador(LocalContext local)
        {
            var nombres = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in new[] { Directos(local.Context.UserId), PorEquipo(local.Context.UserId) })
            {
                var filas = local.ServicioSistema.RetrieveMultiple(q);
                if (filas == null) continue;
                foreach (var r in filas.Entities)
                {
                    var n = r.GetAttributeValue<string>("name");
                    if (n != null && n.StartsWith(Esquema.PrefijoRolCm, StringComparison.Ordinal)) nombres.Add(n);
                }
            }
            return new RolesCm(nombres);
        }

        /// <summary>Roles de CM del llamador, sin repetir.</summary>
        public IEnumerable<string> Nombres
        {
            get { return nombres; }
        }

        /// <summary>No tiene ningun rol de CM: no es una persona de la operacion.</summary>
        public bool SinRolesCm
        {
            get { return nombres.Count == 0; }
        }

        /// <summary>Admin de CM o la identidad de servicio: pueden todo lo que su rol les da.</summary>
        public bool EsAdministracion
        {
            get { return nombres.Contains(Admin) || nombres.Contains(Servicio); }
        }

        /// <summary>Sin roles de CM, o Admin/Servicio: los controles por rol no lo frenan.</summary>
        public bool SinRestriccion
        {
            get { return SinRolesCm || EsAdministracion; }
        }

        public bool Tiene(string rol)
        {
            return nombres.Contains(rol);
        }

        public bool TieneAlguno(params string[] roles)
        {
            foreach (var r in roles)
            {
                if (nombres.Contains(r)) return true;
            }
            return false;
        }

        private static QueryExpression Directos(Guid usuario)
        {
            var q = new QueryExpression(Esquema.Rol) { ColumnSet = new ColumnSet("name") };
            var ur = q.AddLink("systemuserroles", "roleid", "roleid");
            ur.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, usuario);
            return q;
        }

        private static QueryExpression PorEquipo(Guid usuario)
        {
            var q = new QueryExpression(Esquema.Rol) { ColumnSet = new ColumnSet("name") };
            var tr = q.AddLink("teamroles", "roleid", "roleid");
            var tm = tr.AddLink("teammembership", "teamid", "teamid");
            tm.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, usuario);
            return q;
        }
    }
}
