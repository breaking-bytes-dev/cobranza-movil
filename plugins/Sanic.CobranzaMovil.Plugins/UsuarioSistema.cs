using System;
using System.Collections.Concurrent;
using Microsoft.Crm.Sdk.Messages;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Reconoce al usuario SYSTEM (con el que escriben la carga y el servicio del sistema) sin consultar sus roles en
    /// cada fila. Su id sale de un WhoAmI con CreateOrganizationService(null), una vez por organizacion y proceso.
    /// Si el WhoAmI no devuelve id no se cachea nada y el llamador consulta roles: nunca falla hacia la exencion.
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
