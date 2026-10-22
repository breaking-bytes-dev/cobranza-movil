using System;
using System.Collections.Concurrent;
using Microsoft.Crm.Sdk.Messages;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Reconoce al usuario SYSTEM sin consultar sus roles. La carga de Smelter escribe como SYSTEM (su plugin usa
    /// CreateOrganizationService(null), R28 de fase2-pan-diseno.md), y los plugins de CM que escriben con el servicio del
    /// sistema tambien. SYSTEM no tiene roles de CM: pedirle los roles en cada fila de la carga (dos consultas) siempre
    /// devolvia lo mismo, "sin roles de CM".
    ///
    /// Como se sabe quien es SYSTEM: la documentacion de CreateOrganizationService dice que, en un plugin, null es el
    /// usuario SYSTEM; un WhoAmI con ese servicio devuelve su id. Se pregunta UNA vez por organizacion y proceso del
    /// sandbox (el id de SYSTEM no cambia nunca) y se guarda en memoria. No depende de nombres ni de datos editables.
    /// Si el WhoAmI no devuelve un id, no se guarda nada y quien lo usa sigue por el camino de siempre (consultar roles):
    /// falla hacia el comportamiento anterior, nunca hacia la exencion.
    /// </summary>
    internal static class UsuarioSistema
    {
        private static readonly ConcurrentDictionary<Guid, Guid> PorOrganizacion = new ConcurrentDictionary<Guid, Guid>();

        /// <summary>true si quien escribe (Context.UserId) es SYSTEM.</summary>
        public static bool EsElLlamador(LocalContext local)
        {
            var organizacion = local.Context.OrganizationId;
            Guid sistema;
            if (!PorOrganizacion.TryGetValue(organizacion, out sistema))
            {
                sistema = Preguntar(local);
                if (sistema == Guid.Empty) return false;
                PorOrganizacion[organizacion] = sistema;
            }
            return local.Context.UserId == sistema;
        }

        private static Guid Preguntar(LocalContext local)
        {
            var respuesta = local.ServicioSistema.Execute(new WhoAmIRequest());
            if (respuesta == null || respuesta.Results == null || !respuesta.Results.Contains("UserId")) return Guid.Empty;
            var id = respuesta.Results["UserId"];
            return id is Guid ? (Guid)id : Guid.Empty;
        }
    }
}
