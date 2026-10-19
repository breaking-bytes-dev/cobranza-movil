using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// S-11: cada rol de CM escribe solo las columnas de la asignacion que usa su pantalla. Antes el Supervisor (Write
    /// Global) podia cambiar cualquier columna de cualquier asignacion (cuenta, tipo, retiro, orden), y la seguridad de
    /// columna (FLS) no sirve aca: tambien aplicaria a la carga de Smelter y obligaria a probar pantalla por pantalla.
    ///
    /// - Localizador: el orden de su cartera.
    /// - Supervisor: aprobar, rechazar (y cerrar al rechazar, DAT-14), asignar o reasignar el agente (DAT-29).
    /// - Solicitante: ninguna (H1, matriz de roles 2026-10-19): pide y vuelve a pedir con la Custom API SolicitarEspecial,
    ///   que escribe con el servicio del sistema; "Mis solicitudes" solo lee.
    /// - Cualquier otro rol de CM: ninguna (falla cerrado).
    /// Sin roles de CM (la carga, los plugins con el servicio del sistema) y Admin/Servicio no se frenan.
    /// Solo se controlan las columnas de negocio (prefijo sanic_cm_as_); el dueno se cambia con Assign.
    ///
    /// Registro: PreOperation sincrono, Update de sanic_cm_tbl_asignacion, sin filtro de columnas.
    /// Nunca traza ni cita valores: solo nombres de columna.
    /// </summary>
    public sealed class ColumnasAsignacion : PluginBase
    {
        private static readonly Dictionary<string, string[]> Permitidas = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { RolesCm.Localizador, new[] { Esquema.AsignacionOrden } },
            {
                RolesCm.Supervisor, new[]
                {
                    Esquema.AsignacionEstadoAprobacion, Esquema.AsignacionAgente, Esquema.AsignacionResolutor,
                    Esquema.AsignacionMotivoRechazo, Esquema.AsignacionEstado, Esquema.AsignacionMotivo,
                    Esquema.AsignacionFechaCierre,
                }
            },
        };

        protected override string Nombre
        {
            get { return "ColumnasAsignacion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeUpdate);

            var columnas = local.ObtenerTarget().Attributes.Keys
                .Where(c => c.StartsWith(Esquema.PrefijoColumnaAsignacion, StringComparison.Ordinal))
                .ToList();
            if (columnas.Count == 0)
            {
                local.Trace("el cambio no toca columnas de negocio; sin control.");
                return;
            }

            var roles = RolesCm.DelLlamador(local);
            if (roles.SinRestriccion)
            {
                local.Trace("el llamador no tiene restriccion de columnas.");
                return;
            }

            var permitidas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rol in roles.Nombres)
            {
                string[] cols;
                if (Permitidas.TryGetValue(rol, out cols)) permitidas.UnionWith(cols);
            }
            var prohibidas = columnas.Where(c => !permitidas.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (prohibidas.Count == 0)
            {
                SolicitadoPor.ExigirQueSeaElLlamador(local, local.ObtenerTarget(), () => roles);
                local.Trace("{0} columna(s) permitidas para el rol.", columnas.Count);
                return;
            }
            throw local.Error("tu rol no puede cambiar " + string.Join(", ", prohibidas) + " de una asignacion.");
        }
    }
}
