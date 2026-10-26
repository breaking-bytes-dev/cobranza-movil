using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Segregacion de funciones en la asignacion Especial. Solo un Supervisor (o Admin, o la identidad de servicio de CM)
    /// puede, sobre una Especial:
    /// - pasarla a Aprobado o Rechazada;
    /// - ponerle o cambiarle el agente;
    /// - convertir en Especial una asignacion que ya tiene agente.
    /// Se hace en el servidor porque una business rule de formulario no corre ni en canvas ni en la API.
    ///
    /// Frena a las PERSONAS con algun rol de CM (prefijo "SR | CM | ") que no sea Supervisor/Admin/Servicio. No frena a
    /// quien no tiene roles de CM: SYSTEM (identidad de la carga de Smelter) y los administradores del entorno. Las
    /// Especiales del CSV no llegan al localizador hasta que se aprueban: eso lo cuidan el reparto y la app.
    ///
    /// Ademas: las Diarias e Incobrables solo las trae la carga (<see cref="SoloLaCargaTraeDiariasEIncobrables"/>) y, en
    /// el alta, una persona de CM solo puede ponerse a si misma en "Solicitado por" (<see cref="SolicitadoPor"/>).
    ///
    /// Solo consulta roles si el cambio es de los controlados, y nunca a SYSTEM (ver UsuarioSistema), para no sumar
    /// consultas a cada fila de la carga. Nunca traza ni cita valores del registro.
    /// Registro: PreOperation sincrono. Create y Update de sanic_cm_tbl_asignacion; el Update filtrado por
    /// agente, estado de aprobacion y tipo, con imagen previa "PreImage" de esas tres columnas.
    /// </summary>
    public sealed class AprobacionEspecial : PluginBase
    {
        private static readonly string[] RolesQueAprueban = { RolesCm.Supervisor, RolesCm.Admin, RolesCm.Servicio };

        protected override string Nombre
        {
            get { return "AprobacionEspecial"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            RolesCm roles = null;
            Func<RolesCm> rolesDelLlamador = () => roles ?? (roles = RolesCm.DelLlamadorSalvoSistema(local));
            if (local.EsMensaje(Esquema.MensajeCreate))
            {
                SinAprobacionSiNoEsEspecial(local, target);
                SolicitadoPor.ExigirQueSeaElLlamador(local, target, rolesDelLlamador);
            }
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
            SoloLaCargaTraeDiariasEIncobrables(local, target, previa, rolesDelLlamador);

            if (!CambioControlado(target, previa))
            {
                local.Trace("no es un cambio controlado; sin control.");
                return;
            }

            if (PuedeAprobar(rolesDelLlamador()))
            {
                local.Trace("el llamador puede aprobar Especiales.");
                return;
            }
            throw local.Error("solo un Supervisor puede aprobar, rechazar o asignar el agente de una asignacion Especial.");
        }

        /// <summary>
        /// El estado de aprobacion es solo de las Especiales, pero su valor por defecto es Pendiente (lo necesitan las
        /// Especiales del CSV para entrar a la cola del Supervisor). Una Diaria o Incobrable nace en Borrador para no
        /// aparecer como "pendiente de aprobacion" en una consulta que no filtre por tipo.
        /// </summary>
        private static void SinAprobacionSiNoEsEspecial(LocalContext local, Entity target)
        {
            var tipo = target.GetAttributeValue<OptionSetValue>(Esquema.AsignacionTipo);
            if (tipo == null || tipo.Value == Esquema.TipoEspecial) return;
            var aprobacion = target.GetAttributeValue<OptionSetValue>(Esquema.AsignacionEstadoAprobacion);
            if (aprobacion != null && aprobacion.Value != Esquema.AprobacionPendiente) return;
            target[Esquema.AsignacionEstadoAprobacion] = new OptionSetValue(Esquema.AprobacionBorrador);
            local.Trace("Diaria o Incobrable nace en Borrador.");
        }

        /// <summary>
        /// Crear una Diaria o Incobrable, o convertir una asignacion en una, es cosa de la carga: Smelter crea como SYSTEM
        /// (sin roles de CM), tambien en el alta del Admin con AltaCuenta. Asi nadie se salta la aprobacion de la Especial.
        /// Frena a toda persona con roles de CM, Admin incluido; no frena al Servicio ni a quien no tiene roles de CM.
        /// </summary>
        private static void SoloLaCargaTraeDiariasEIncobrables(LocalContext local, Entity target, Entity previa, Func<RolesCm> roles)
        {
            if (!target.Contains(Esquema.AsignacionTipo)) return; // Create sin tipo: nace Especial
            var tipo = target.GetAttributeValue<OptionSetValue>(Esquema.AsignacionTipo);
            if (tipo == null || (tipo.Value != Esquema.TipoDiaria && tipo.Value != Esquema.TipoIncobrable)) return;
            if (previa != null)
            {
                var antes = previa.GetAttributeValue<OptionSetValue>(Esquema.AsignacionTipo);
                if (antes != null && antes.Value == tipo.Value) return;
            }
            var r = roles();
            if (r.SinRolesCm || r.Tiene(RolesCm.Servicio)) return;
            throw local.Error("las asignaciones Diarias e Incobrables solo llegan por la carga del banco; una persona no puede crearlas ni convertir otra en una.");
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

        /// <summary>Roles del llamador (directos y por equipo); sin roles de CM no se frena (SYSTEM / Smelter, administradores).</summary>
        private static bool PuedeAprobar(RolesCm roles)
        {
            return roles.SinRolesCm || roles.TieneAlguno(RolesQueAprueban);
        }
    }
}
