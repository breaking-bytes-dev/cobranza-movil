using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// La cuenta se borra al salir de cartera pero la interaccion vive mas; por eso al crearse guarda con que identificar
    /// la cuenta en el core: ultimos 4, ID del cliente (CIF) y clave HMAC del numero, nunca el numero completo.
    /// Solo completa lo vacio y nunca rechaza, porque el telefono descarta lo rechazado: sin cuenta, o si no la
    /// encuentra, deja pasar y lo traza. Lee con el servicio del sistema; nunca traza valores.
    /// Registro: PreOperation sincrono, solo Create de sanic_cm_tbl_interaccion.
    /// </summary>
    public sealed class IdentidadEnInteraccion : PluginBase
    {
        protected override string Nombre
        {
            get { return "IdentidadEnInteraccion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Interaccion, Esquema.EtapaPreOperation, Esquema.MensajeCreate);

            var target = local.ObtenerTarget();
            var cuenta = target.GetAttributeValue<EntityReference>(Esquema.InteraccionCuentaId);
            if (cuenta == null)
            {
                local.Trace("la interaccion no trae cuenta; sin identidad que copiar.");
                return;
            }

            // RetrieveMultiple y no Retrieve: si la cuenta no existe devuelve vacio en lugar de una excepcion que rechazaria.
            var q = new QueryExpression(Esquema.Cuenta)
            {
                ColumnSet = new ColumnSet(Esquema.CuentaUltimos4, Esquema.CuentaCif, Esquema.CuentaClavePan),
                TopCount = 1,
                NoLock = true
            };
            q.Criteria.AddCondition(Esquema.CuentaId, ConditionOperator.Equal, cuenta.Id);
            var filas = local.ServicioSistema.RetrieveMultiple(q).Entities;
            if (filas.Count == 0)
            {
                local.Trace("no se encontro la cuenta; la interaccion entra sin identidad.");
                return;
            }

            var copiadas = 0;
            copiadas += Copiar(target, filas[0], Esquema.CuentaUltimos4, Esquema.InteraccionUltimos4);
            copiadas += Copiar(target, filas[0], Esquema.CuentaCif, Esquema.InteraccionCif);
            copiadas += Copiar(target, filas[0], Esquema.CuentaClavePan, Esquema.InteraccionClavePan);
            local.Trace("{0} dato(s) de identidad copiados de la cuenta.", copiadas);
        }

        private static int Copiar(Entity target, Entity cuenta, string deCuenta, string aInteraccion)
        {
            if (!string.IsNullOrWhiteSpace(target.GetAttributeValue<string>(aInteraccion))) return 0;
            var valor = cuenta.GetAttributeValue<string>(deCuenta);
            if (string.IsNullOrWhiteSpace(valor)) return 0;
            target[aInteraccion] = valor;
            return 1;
        }
    }
}
