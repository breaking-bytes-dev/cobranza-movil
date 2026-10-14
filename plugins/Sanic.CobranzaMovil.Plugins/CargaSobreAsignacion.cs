using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Lo que hace la carga diaria (Smelter, sin tocar su codigo ni su plantilla) al volver a traer una asignacion que
    /// ya existe. La carga es la unica que escribe "ultima carga" (ColumnasAsignacion no se la deja a las personas).
    ///
    /// - DAT-10: una Diaria o Incobrable cerrada por el sistema que vuelve en el CSV se REABRE (estado activo, sin
    ///   motivo ni fecha de cierre). El sistema cierra con dos motivos: "fuera de cartera" (reconciliacion del
    ///   backoffice) y "Reclasificada a ..." (Reclasificacion, DAT-15). Antes quedaba cerrada para siempre: Smelter no
    ///   mapea el estado y el valor por defecto solo aplica al crear. Los cierres de una persona (Cierres) y las
    ///   Especiales no se reabren. El reparto corre igual: la carga manda el agente en cada fila.
    /// - DAT-31: no pisa la fecha de asignacion si ya tenia una (antes la reemplazaba por la fecha de carga cada dia).
    ///
    /// Registro: PreOperation sincrono, Update de sanic_cm_tbl_asignacion filtrado por ultima carga, con imagen previa
    /// "PreImage" de estado, motivo, tipo y fecha de asignacion. No consulta nada. Nunca traza valores.
    /// </summary>
    public sealed class CargaSobreAsignacion : PluginBase
    {
        protected override string Nombre
        {
            get { return "CargaSobreAsignacion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            if (!target.Contains(Esquema.AsignacionUltimaCarga))
            {
                local.Trace("el cambio no viene de la carga; sin cambios.");
                return;
            }
            var previa = local.ObtenerImagenPrevia();

            if (target.Contains(Esquema.AsignacionFechaAsignacion) && previa.GetAttributeValue<DateTime?>(Esquema.AsignacionFechaAsignacion) != null)
            {
                target.Attributes.Remove(Esquema.AsignacionFechaAsignacion);
                local.Trace("DAT-31: se conserva la fecha de asignacion.");
            }

            if (target.Contains(Esquema.AsignacionEstado) || previa.GetAttributeValue<bool?>(Esquema.AsignacionEstado) != false)
            {
                return;
            }
            var tipo = previa.GetAttributeValue<OptionSetValue>(Esquema.AsignacionTipo);
            if (tipo == null || (tipo.Value != Esquema.TipoDiaria && tipo.Value != Esquema.TipoIncobrable))
            {
                local.Trace("cerrada pero no es Diaria ni Incobrable; no se reabre.");
                return;
            }
            if (!CerradaPorElSistema(previa.GetAttributeValue<string>(Esquema.AsignacionMotivo)))
            {
                local.Trace("cerrada por una persona; no se reabre.");
                return;
            }

            target[Esquema.AsignacionEstado] = true;
            target[Esquema.AsignacionMotivo] = null;
            target[Esquema.AsignacionFechaCierre] = null;
            local.Trace("DAT-10: volvio en la carga; se reabre.");
        }

        private static bool CerradaPorElSistema(string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo)) return false;
            var m = motivo.Trim();
            return string.Equals(m, Esquema.MotivoFueraDeCartera, StringComparison.OrdinalIgnoreCase)
                || m.StartsWith(Esquema.PrefijoMotivoReclasificada, StringComparison.OrdinalIgnoreCase);
        }
    }
}
