using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// "Solicitado por" decide con quien se comparte la Especial aprobada (<see cref="RepartoAsignacion"/>). Una
    /// persona de CM solo puede ponerse a si misma: poniendo a otro le daria acceso de lectura a la Especial a un
    /// tercero (solo lectura: ver RepartoAsignacion). Vaciarlo no comparte con nadie y se permite.
    /// Admin, Servicio y quien no tiene roles de CM (SYSTEM: la carga, SolicitarEspecial al reactivar) no se frenan.
    /// Lo aplican AprobacionEspecial (alta) y ColumnasAsignacion (edicion). Nunca cita el valor.
    /// </summary>
    internal static class SolicitadoPor
    {
        /// <summary>Los roles se piden solo si hace falta (el alta comun no consulta nada).</summary>
        public static void ExigirQueSeaElLlamador(LocalContext local, Entity target, Func<RolesCm> roles)
        {
            if (!target.Contains(Esquema.AsignacionSolicitadoPor)) return;
            var valor = target.GetAttributeValue<EntityReference>(Esquema.AsignacionSolicitadoPor);
            if (valor == null || valor.Id == local.Context.UserId) return;
            if (roles().SinRestriccion)
            {
                local.Trace("\"Solicitado por\" es otra persona; el llamador no tiene restriccion.");
                return;
            }
            throw local.Error("En «Solicitado por» solo puede ir quien pide la Especial.");
        }
    }
}
