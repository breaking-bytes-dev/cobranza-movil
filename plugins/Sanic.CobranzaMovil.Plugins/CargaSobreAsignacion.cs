using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Ajusta lo que hace la carga diaria al volver a traer una asignacion existente. Solo la carga escribe
    /// "ultima carga" (ColumnasAsignacion no se la deja a las personas).
    /// - Una Diaria o Incobrable cerrada por el sistema ("fuera de cartera" o "Reclasificada a ...") se reabre, porque
    ///   la carga no mapea el estado. Los cierres de una persona y las Especiales no se reabren.
    /// - No pisa la fecha de asignacion si ya tenia una.
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

            if (target.Contains(Esquema.AsignacionFechaAsignada) && previa.GetAttributeValue<DateTime?>(Esquema.AsignacionFechaAsignada) != null)
            {
                target.Attributes.Remove(Esquema.AsignacionFechaAsignada);
                local.Trace("DAT-31: se conserva la fecha asignada.");
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
