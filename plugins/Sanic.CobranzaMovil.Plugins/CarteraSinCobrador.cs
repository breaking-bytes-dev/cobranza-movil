using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// DAT-07/08: una asignacion activa cuyo agente no tiene usuario (o que no tiene agente) pasa a ser del equipo
    /// "CM · Cartera sin cobrador · &lt;entorno&gt;": un equipo dueno SIN miembros, con un rol minimo (solo lectura de la
    /// asignacion, para poder ser su dueno), que crea scripts/seguridad/paso5_cartera_sin_cobrador.py. Asi nadie la ve
    /// en el telefono y el cobrador anterior la pierde. Antes el reparto no hacia nada y se la dejaba al anterior.
    /// </summary>
    public static class CarteraSinCobrador
    {
        private static readonly int ErrorReparto = unchecked((int)0x80040265);

        /// <summary>El unico equipo dueno cuyo nombre empieza con el prefijo; si no hay uno solo, falla sin reintento.</summary>
        public static EntityReference Exigir(IOrganizationService svc, string plugin)
        {
            var q = new QueryExpression(Esquema.Equipo) { ColumnSet = new ColumnSet(false), TopCount = 2 };
            q.Criteria.AddCondition(Esquema.EquipoNombre, ConditionOperator.BeginsWith, Esquema.PrefijoEquipoSinCobrador);
            q.Criteria.AddCondition(Esquema.EquipoTipo, ConditionOperator.Equal, Esquema.EquipoTipoDueno);
            var filas = svc.RetrieveMultiple(q);
            if (filas != null && filas.Entities.Count == 1)
            {
                return new EntityReference(Esquema.Equipo, filas.Entities[0].Id);
            }
            throw new InvalidPluginExecutionException(OperationStatus.Failed, ErrorReparto,
                plugin + ": no hay un unico equipo dueno \"" + Esquema.PrefijoEquipoSinCobrador + " · <entorno>\" (Cartera sin cobrador)."
                + " Correr scripts/seguridad/paso5_cartera_sin_cobrador.py --aplicar.");
        }
    }
}
