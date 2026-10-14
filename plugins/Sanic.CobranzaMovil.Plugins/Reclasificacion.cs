using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// DAT-15 (DEC-021): una cuenta tiene UNA sola asignacion activa entre Diaria e Incobrable. Cuando la carga trae la
    /// cuenta con el otro tipo (crea la asignacion nueva, o reabre una que estaba cerrada), la del tipo anterior se
    /// cierra con motivo "Reclasificada a {tipo}". Antes quedaban las dos activas en el telefono. Las Especiales no se
    /// tocan: una Diaria y una Especial de la misma cuenta conviven.
    /// El cierre dispara RepartoAsignacion (filtra por estado), que le revoca la cuenta al cobrador si ya no la tiene
    /// por otra asignacion. Si la cuenta vuelve con el tipo anterior, CargaSobreAsignacion la reabre (DAT-10).
    ///
    /// Valores resultantes sin leer la fila: el Target pisa a la imagen previa; en un Create sin estado, la asignacion
    /// nace activa (valor por defecto). Escribe con el servicio del sistema (la carga ya corre como SYSTEM).
    /// Registro: PostOperation SINCRONO (en la misma transaccion que la fila de la carga). Create de
    /// sanic_cm_tbl_asignacion; Update filtrado por ultima carga, estado y tipo, con imagen previa "PreImage" de tipo,
    /// estado y cuenta. Nunca traza valores.
    /// </summary>
    public sealed class Reclasificacion : PluginBase
    {
        protected override string Nombre
        {
            get { return "Reclasificacion"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPostOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            var previa = local.EsMensaje(Esquema.MensajeUpdate) ? local.ObtenerImagenPrevia() : null;

            var tipo = Valor<OptionSetValue>(target, previa, Esquema.AsignacionTipo);
            var activa = target.Contains(Esquema.AsignacionEstado) || previa != null
                ? Valor<bool?>(target, previa, Esquema.AsignacionEstado) == true
                : true; // Create sin estado: nace activa
            var cuenta = Valor<EntityReference>(target, previa, Esquema.AsignacionCuentaId);
            if (!activa || tipo == null || cuenta == null
                || (tipo.Value != Esquema.TipoDiaria && tipo.Value != Esquema.TipoIncobrable))
            {
                local.Trace("no es una Diaria o Incobrable activa con cuenta; nada que reclasificar.");
                return;
            }
            if (previa != null && !target.Contains(Esquema.AsignacionEstado) && !target.Contains(Esquema.AsignacionTipo)
                && !target.Contains(Esquema.AsignacionUltimaCarga))
            {
                return;
            }

            var otro = tipo.Value == Esquema.TipoDiaria ? Esquema.TipoIncobrable : Esquema.TipoDiaria;
            var q = new QueryExpression(Esquema.Asignacion) { ColumnSet = new ColumnSet(false) };
            q.Criteria.AddCondition(Esquema.AsignacionCuentaId, ConditionOperator.Equal, cuenta.Id);
            q.Criteria.AddCondition(Esquema.AsignacionTipo, ConditionOperator.Equal, otro);
            q.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);

            var svc = local.ServicioSistema;
            var motivo = Esquema.PrefijoMotivoReclasificada + (tipo.Value == Esquema.TipoDiaria ? "Diaria" : "Incobrable");
            var cerradas = 0;
            foreach (var fila in svc.RetrieveMultiple(q).Entities)
            {
                var cierre = new Entity(Esquema.Asignacion, fila.Id);
                cierre[Esquema.AsignacionEstado] = false;
                cierre[Esquema.AsignacionMotivo] = motivo;
                cierre[Esquema.AsignacionFechaCierre] = DateTime.UtcNow;
                svc.Update(cierre);
                cerradas++;
            }
            local.Trace("asignaciones del otro tipo cerradas: {0}.", cerradas);
        }

        private static T Valor<T>(Entity target, Entity previa, string columna)
        {
            if (target.Contains(columna)) return target.GetAttributeValue<T>(columna);
            return previa == null ? default(T) : previa.GetAttributeValue<T>(columna);
        }
    }
}
