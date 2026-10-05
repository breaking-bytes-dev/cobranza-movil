using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// S-6: segregacion de funciones en la asignacion Especial. Solo un Supervisor (o Admin, o la identidad de
    /// servicio de CM) puede, sobre una Especial:
    /// - pasarla a Aprobado o Rechazada;
    /// - ponerle o cambiarle el agente;
    /// - convertir en Especial una asignacion que ya tiene agente.
    /// Antes la unica traba era una business rule de formulario: no corre ni en canvas ni en la API.
    ///
    /// A quien frena: a las PERSONAS con algun rol de CM (prefijo "SR | CM | ") que no sea Supervisor/Admin/Servicio.
    /// Quien no tiene roles de CM no se frena: SYSTEM (identidad de la carga de Smelter, sin roles) y los
    /// administradores del entorno. Las Especiales del CSV no llegan al cobrador hasta que se aprueban: eso lo
    /// cuidan el flow (no comparte) y la app (no muestra), sin romper la carga.
    ///
    /// Solo consulta roles si el cambio es de los controlados. Nunca traza ni cita valores del registro.
    /// Registro: PreOperation sincrono. Create y Update de sanic_cm_tbl_asignacion; el Update filtrado por
    /// agente, estado de aprobacion y tipo, con imagen previa "PreImage" de esas tres columnas.
    /// </summary>
    public sealed class AprobacionEspecial : PluginBase
    {
        private static readonly string[] RolesQueAprueban =
        {
            Esquema.PrefijoRolCm + "Supervisor",
            Esquema.PrefijoRolCm + "Admin",
            Esquema.PrefijoRolCm + "Servicio - Cobranza",
        };

        protected override string Nombre
        {
            get { return "AprobacionEspecial"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            Entity previa = null;
            if (local.EsMensaje(Esquema.MensajeUpdate))
            {
                if (!target.Contains(Esquema.AsignacionAgente) && !target.Contains(Esquema.AsignacionEstadoAprobacion)
                    && !target.Contains(Esquema.AsignacionTipo))
                {
                    local.Trace("el cambio no toca agente, aprobacion ni tipo; sin control.");
                    return;
                }
                previa = local.ObtenerImagenPrevia();
            }

            if (!CambioControlado(target, previa))
            {
                local.Trace("no es un cambio controlado; sin control.");
                return;
            }

            if (PuedeAprobar(local))
            {
                local.Trace("el llamador puede aprobar Especiales.");
                return;
            }
            throw local.Error("solo un Supervisor puede aprobar, rechazar o asignar el agente de una asignacion Especial.");
        }

        private static bool CambioControlado(Entity target, Entity previa)
        {
            // Valores resultantes: lo que trae el Target pisa a la imagen previa. Sin tipo en un Create, la
            // asignacion nace con el valor por defecto de la columna, que es Especial.
            var tipo = Opcion(target, previa, Esquema.AsignacionTipo);
            var esEspecial = tipo.HasValue ? tipo.Value == Esquema.TipoEspecial : previa == null;
            if (!esEspecial) return false;

            var agenteAntes = previa == null ? null : previa.GetAttributeValue<EntityReference>(Esquema.AsignacionAgente);
            var agenteDespues = target.Contains(Esquema.AsignacionAgente)
                ? target.GetAttributeValue<EntityReference>(Esquema.AsignacionAgente)
                : agenteAntes;

            // 1) Poner o cambiar el agente (quitarlo no se controla).
            if (target.Contains(Esquema.AsignacionAgente) && agenteDespues != null
                && (agenteAntes == null || agenteAntes.Id != agenteDespues.Id))
            {
                return true;
            }

            // 2) Pasar a Aprobado o Rechazada.
            if (target.Contains(Esquema.AsignacionEstadoAprobacion))
            {
                var nuevo = target.GetAttributeValue<OptionSetValue>(Esquema.AsignacionEstadoAprobacion);
                var antes = previa == null ? null : previa.GetAttributeValue<OptionSetValue>(Esquema.AsignacionEstadoAprobacion);
                if (nuevo != null && (nuevo.Value == Esquema.AprobacionAprobado || nuevo.Value == Esquema.AprobacionRechazada)
                    && (antes == null || antes.Value != nuevo.Value))
                {
                    return true;
                }
            }

            // 3) Convertir en Especial una asignacion que ya tiene agente.
            if (previa != null && target.Contains(Esquema.AsignacionTipo) && agenteDespues != null)
            {
                var tipoAntes = previa.GetAttributeValue<OptionSetValue>(Esquema.AsignacionTipo);
                if (tipoAntes == null || tipoAntes.Value != Esquema.TipoEspecial) return true;
            }
            return false;
        }

        private static int? Opcion(Entity target, Entity previa, string columna)
        {
            OptionSetValue v = null;
            if (target.Contains(columna)) v = target.GetAttributeValue<OptionSetValue>(columna);
            else if (previa != null) v = previa.GetAttributeValue<OptionSetValue>(columna);
            return v == null ? (int?)null : v.Value;
        }

        /// <summary>
        /// Roles del llamador (directos y por equipo). Se leen con el servicio del sistema: un Solicitante no
        /// tiene por que poder leer la tabla de roles.
        /// </summary>
        private static bool PuedeAprobar(LocalContext local)
        {
            var nombres = new List<string>();
            nombres.AddRange(NombresDeRoles(local, RolesDirectos(local.Context.UserId)));
            nombres.AddRange(NombresDeRoles(local, RolesPorEquipo(local.Context.UserId)));

            var tieneRolCm = false;
            foreach (var n in nombres)
            {
                if (n == null || !n.StartsWith(Esquema.PrefijoRolCm, StringComparison.Ordinal)) continue;
                tieneRolCm = true;
                if (Array.IndexOf(RolesQueAprueban, n) >= 0) return true;
            }
            // Sin roles de CM no es una persona de la operacion (SYSTEM / Smelter, administradores): no se frena.
            return !tieneRolCm;
        }

        private static IEnumerable<string> NombresDeRoles(LocalContext local, QueryExpression q)
        {
            var filas = local.ServicioSistema.RetrieveMultiple(q);
            if (filas == null) yield break;
            foreach (var r in filas.Entities) yield return r.GetAttributeValue<string>("name");
        }

        private static QueryExpression RolesDirectos(Guid usuario)
        {
            var q = new QueryExpression(Esquema.Rol) { ColumnSet = new ColumnSet("name") };
            var ur = q.AddLink("systemuserroles", "roleid", "roleid");
            ur.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, usuario);
            return q;
        }

        private static QueryExpression RolesPorEquipo(Guid usuario)
        {
            var q = new QueryExpression(Esquema.Rol) { ColumnSet = new ColumnSet("name") };
            var tr = q.AddLink("teamroles", "roleid", "roleid");
            var tm = tr.AddLink("teammembership", "teamid", "teamid");
            tm.LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, usuario);
            return q;
        }
    }
}
