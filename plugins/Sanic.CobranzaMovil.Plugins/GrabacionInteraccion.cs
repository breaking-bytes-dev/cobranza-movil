using System;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// La grabacion de una interaccion en el CORE la controla el servidor, no la pantalla de Transcripcion.
    /// - Una interaccion Grabada no se vuelve a grabar ni cambia su codigo: si dos personas del BackOffice tienen la
    ///   misma abierta, la segunda recibe el error en lugar de pisar el codigo de la primera.
    /// - Pasar a Grabado exige el codigo de grabacion.
    /// - La fecha y el usuario de grabacion los pone el servidor; fuera de una grabacion, lo que mande una persona
    ///   en esas dos columnas se descarta.
    /// - El BackOffice escribe solo las columnas de grabacion (su permiso de escritura global no debe tocar lo que
    ///   capturo el localizador). Ningun otro rol de CM, salvo Admin/Servicio, escribe la interaccion.
    /// Sin roles de CM y Admin/Servicio no tienen restriccion de columnas; el codigo se exige igual.
    ///
    /// Limite: la imagen previa se lee antes de bloquear la fila. Dos grabaciones en el mismo instante (milisegundos)
    /// podrian pasar las dos; el caso real (dos personas con la pantalla abierta) queda cubierto.
    /// Registro: PreOperation sincrono, Update de sanic_cm_tbl_interaccion, sin filtro, con imagen previa "PreImage"
    /// de estado y codigo de grabacion. Nunca traza ni cita valores.
    /// </summary>
    public sealed class GrabacionInteraccion : PluginBase
    {
        private static readonly string[] ColumnasGrabacion =
        {
            Esquema.InteraccionEstadoGrabacion, Esquema.InteraccionCodigoGrabacion,
            Esquema.InteraccionFechaGrabacion, Esquema.InteraccionGrabadoPor,
        };

        protected override string Nombre
        {
            get { return "GrabacionInteraccion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Interaccion, Esquema.EtapaPreOperation, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            // El Target de Dataverse trae tambien la clave primaria (sanic_cm_tbl_interaccionid): no es una columna de negocio.
            var clave = target.LogicalName + "id";
            var columnas = target.Attributes.Keys
                .Where(c => c.StartsWith(Esquema.PrefijoColumnaCm, StringComparison.Ordinal) && c != clave)
                .ToList();
            if (columnas.Count == 0)
            {
                local.Trace("el cambio no toca columnas de negocio; sin control.");
                return;
            }
            var previa = local.ObtenerImagenPrevia();
            var roles = RolesCm.DelLlamador(local);

            var estadoAntes = previa.GetAttributeValue<OptionSetValue>(Esquema.InteraccionEstadoGrabacion);
            var yaGrabada = estadoAntes != null && estadoAntes.Value == Esquema.GrabacionGrabado;

            if (!roles.SinRestriccion)
            {
                var permitidas = roles.Tiene(RolesCm.BackOffice) ? ColumnasGrabacion : new string[0];
                var prohibidas = columnas.Where(c => Array.IndexOf(permitidas, c) < 0).OrderBy(c => c, StringComparer.Ordinal).ToList();
                if (prohibidas.Count > 0)
                {
                    throw local.Error("tu rol no puede cambiar " + string.Join(", ", prohibidas) + " de una interaccion.");
                }
                if (yaGrabada && columnas.Any(c => Array.IndexOf(ColumnasGrabacion, c) >= 0))
                {
                    throw local.Error("la interaccion ya fue grabada en el CORE (por otra persona o en otra pestana); "
                        + "no se puede volver a grabar ni cambiar su codigo. Actualiza la lista.");
                }
            }

            var estadoNuevo = target.Contains(Esquema.InteraccionEstadoGrabacion)
                ? target.GetAttributeValue<OptionSetValue>(Esquema.InteraccionEstadoGrabacion)
                : null;
            if (estadoNuevo != null && estadoNuevo.Value == Esquema.GrabacionGrabado)
            {
                var codigo = target.Contains(Esquema.InteraccionCodigoGrabacion)
                    ? target.GetAttributeValue<string>(Esquema.InteraccionCodigoGrabacion)
                    : previa.GetAttributeValue<string>(Esquema.InteraccionCodigoGrabacion);
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    throw local.Error("para marcarla Grabada hace falta el codigo de grabacion del CORE.");
                }
                target[Esquema.InteraccionFechaGrabacion] = DateTime.UtcNow;
                target[Esquema.InteraccionGrabadoPor] = new EntityReference(Esquema.Usuario, local.Context.UserId);
                local.Trace("grabada: fecha y usuario puestos por el servidor.");
            }
            else if (!roles.SinRestriccion)
            {
                target.Attributes.Remove(Esquema.InteraccionFechaGrabacion);
                target.Attributes.Remove(Esquema.InteraccionGrabadoPor);
            }
        }
    }
}
