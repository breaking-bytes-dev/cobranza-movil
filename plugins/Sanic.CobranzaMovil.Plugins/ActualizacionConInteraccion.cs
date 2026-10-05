using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// APP-24: una Actualizacion de datos debe colgar de una interaccion que tenga cuenta. Por ese camino
    /// (actualizacion -> interaccion -> cuenta -> CIF) el backoffice sabe de que cliente es y arma el "antes".
    /// La app ya la crea despues de la interaccion; esto lo garantiza tambien fuera de la app.
    ///
    /// Lee la interaccion con los permisos del llamador (el cobrador lee las suyas). Nunca cita valores.
    /// Registro: PreOperation sincrono. Create y Update de sanic_cm_tbl_actualizaciondatos; el Update filtrado
    /// por sanic_cm_ad_interaccionid.
    /// </summary>
    public sealed class ActualizacionConInteraccion : PluginBase
    {
        protected override string Nombre
        {
            get { return "ActualizacionConInteraccion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.ActualizacionDatos, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            if (local.EsMensaje(Esquema.MensajeUpdate) && !target.Contains(Esquema.ActualizacionInteraccion))
            {
                local.Trace("el cambio no toca la interaccion; sin control.");
                return;
            }

            var interaccion = target.GetAttributeValue<EntityReference>(Esquema.ActualizacionInteraccion);
            if (interaccion == null)
            {
                throw local.Error("la actualizacion de datos tiene que estar vinculada a una interaccion.");
            }

            var fila = local.Service.Retrieve(Esquema.Interaccion, interaccion.Id, new ColumnSet(Esquema.InteraccionCuentaId));
            if (fila == null || fila.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId) == null)
            {
                throw local.Error("la interaccion de la actualizacion de datos no tiene cuenta: no se sabe de que cliente es.");
            }
            local.Trace("interaccion con cuenta; ok.");
        }
    }
}
