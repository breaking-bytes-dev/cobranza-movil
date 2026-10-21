using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// F6: el numero de recibo es unico en toda la cartera. La app ya lo valida, pero offline el telefono solo ve sus
    /// propios pagos (y dos telefonos pueden usar el mismo talonario). El servidor NO rechaza (F8: el telefono descarta
    /// lo rechazado y el pago se perderia): marca el pago nuevo como recibo repetido y BackOffice lo revisa al transcribir.
    ///
    /// Busca con el servicio del SISTEMA: el cobrador no ve los pagos de los demas. Nunca traza el numero.
    /// Registro: PreOperation sincrono, solo Create de sanic_cm_tbl_pago.
    /// </summary>
    public sealed class ReciboRepetido : PluginBase
    {
        protected override string Nombre
        {
            get { return "ReciboRepetido"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Pago, Esquema.EtapaPreOperation, Esquema.MensajeCreate);

            var target = local.ObtenerTarget();
            var recibo = (target.GetAttributeValue<string>(Esquema.PagoNumeroRecibo) ?? string.Empty).Trim();
            if (recibo.Length == 0)
            {
                local.Trace("el pago no trae recibo; sin control.");
                return;
            }

            // La comparacion de texto de Dataverse no distingue mayusculas, igual que la app (Upper(Trim(...))).
            var q = new QueryExpression(Esquema.Pago) { ColumnSet = new ColumnSet(false), TopCount = 1, NoLock = true };
            q.Criteria.AddCondition(Esquema.PagoNumeroRecibo, ConditionOperator.Equal, recibo);
            var repetido = local.ServicioSistema.RetrieveMultiple(q).Entities.Count > 0;

            target[Esquema.PagoReciboRepetido] = repetido;
            local.Trace(repetido ? "recibo repetido: el pago entra marcado para BackOffice." : "recibo sin repetir.");
        }
    }
}
