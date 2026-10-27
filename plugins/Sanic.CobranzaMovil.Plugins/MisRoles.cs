using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Custom API global sanic_cm_MisRoles: perfiles de CM del usuario que llama, para que la app del backoffice arme
    /// su menu y sus permisos. Devuelve Roles = perfiles sin el prefijo "SR | CM | ", ordenados y separados por "|"
    /// (por ejemplo "Admin|BackOffice"); vacio si no tiene ninguno.
    /// Mira los roles directos y los de cualquier equipo del que sea miembro (tambien los de grupo de Entra ID), sin
    /// depender del nombre de los equipos. Cualquier usuario puede llamarla: solo devuelve lo suyo.
    /// "Servicio - Cobranza" no se devuelve: es la identidad de las integraciones, no un perfil de la app.
    /// Devuelve tambien Version = version de la solucion cm_core_cobranzamovil, que las apps muestran en pantalla
    /// (las personas no pueden leer la tabla de soluciones); vacia si no se encuentra, sin fallar por eso.
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
                throw local.ErrorTecnico("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeMisRoles + ".");
            }

            var perfiles = Perfiles(RolesCm.DelLlamador(local));
            ctx.OutputParameters[Esquema.SalidaRoles] = string.Join("|", perfiles);
            local.Trace("{0} perfiles de CM.", perfiles.Count);
            ctx.OutputParameters[Esquema.SalidaVersion] = VersionDeLaSolucion(local);
        }

        /// <summary>Version de la solucion de CM, leida con el servicio del sistema; vacia si no esta o no se puede leer.</summary>
        private static string VersionDeLaSolucion(LocalContext local)
        {
            var q = new QueryExpression(Esquema.Solucion)
            {
                ColumnSet = new ColumnSet(Esquema.SolucionVersion),
                TopCount = 1,
            };
            q.Criteria.AddCondition(Esquema.SolucionNombreUnico, ConditionOperator.Equal, Esquema.SolucionCm);
            try
            {
                var filas = local.ServicioSistema.RetrieveMultiple(q);
                var version = filas == null ? null : filas.Entities.Select(f => f.GetAttributeValue<string>(Esquema.SolucionVersion)).FirstOrDefault();
                if (string.IsNullOrEmpty(version)) local.Trace("no se encontro la version de la solucion.");
                return version ?? "";
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                local.Trace("no se pudo leer la version de la solucion: {0}", ex.Message);
                return "";
            }
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
