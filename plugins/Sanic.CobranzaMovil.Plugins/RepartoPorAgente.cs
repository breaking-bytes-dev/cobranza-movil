using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// DAT-08: vincular, cambiar o quitar el usuario de un agente no movia sus asignaciones; el reparto solo corria al
    /// tocar la asignacion (las Diarias se curaban con la carga del dia siguiente, las Incobrables y Especiales nunca).
    ///
    /// Que hace: lee el usuario ACTUAL del agente y busca sus asignaciones activas cuyo dueno no es el que corresponde
    /// (el usuario o, sin usuario, el equipo "Cartera sin cobrador"), sin las Especiales por aprobar (S-6). A cada una
    /// le vuelve a escribir el MISMO agente: eso dispara RepartoAsignacion (su Update esta filtrado por el agente), que
    /// mueve el dueno, comparte y revoca. Asi la logica del reparto vive en un solo lugar.
    ///
    /// Hasta <see cref="MaximoPorEjecucion"/> por ejecucion (2 minutos): si quedan mas, o si alguna escritura falla,
    /// termina pidiendo reintento al servicio asincrono; la consulta del reintento ya no trae las que se movieron.
    /// Corre con el servicio del SISTEMA (ninguna persona tiene por que poder escribir asignaciones ajenas).
    /// Registro: PostOperation ASINCRONO, Update de sanic_cm_tbl_agente filtrado por sanic_cm_ag_usuarioid.
    /// Nunca traza ni cita identificadores ni valores de registros.
    /// </summary>
    public sealed class RepartoPorAgente : PluginBase
    {
        public const int MaximoPorEjecucion = 1000;

        private static readonly int ErrorNoExiste = unchecked((int)0x80040217);
        private static readonly int ErrorReparto = unchecked((int)0x80040265);

        protected override string Nombre
        {
            get { return "RepartoPorAgente"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Agente, Esquema.EtapaPostOperation, Esquema.MensajeUpdate);
            if (local.Context.Mode != Esquema.ModoAsincrono)
            {
                throw local.Error("el paso debe registrarse asincrono: recorre todas las asignaciones del agente.");
            }

            var svc = local.ServicioSistema;
            var agenteId = local.Context.PrimaryEntityId;
            Entity agente;
            try
            {
                agente = svc.Retrieve(Esquema.Agente, agenteId, new ColumnSet(Esquema.AgenteUsuario));
            }
            catch (FaultException<OrganizationServiceFault> f)
            {
                if (f.Detail == null || f.Detail.ErrorCode != ErrorNoExiste) throw;
                local.Trace("el agente ya no existe; nada que repartir.");
                return;
            }

            var usuario = agente.GetAttributeValue<EntityReference>(Esquema.AgenteUsuario);
            var dueno = usuario != null
                ? new EntityReference(Esquema.Usuario, usuario.Id)
                : CarteraSinCobrador.Exigir(svc, Nombre);
            local.Trace(usuario != null ? "agente con usuario." : "agente sin usuario: sus asignaciones van a la cartera sin cobrador.");

            var q = new QueryExpression(Esquema.Asignacion) { ColumnSet = new ColumnSet(false), TopCount = MaximoPorEjecucion + 1 };
            q.Criteria.AddCondition(Esquema.AsignacionAgente, ConditionOperator.Equal, agenteId);
            q.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.Propietario, ConditionOperator.NotEqual, dueno.Id);
            var noPorAprobar = q.Criteria.AddFilter(LogicalOperator.Or);
            noPorAprobar.AddCondition(Esquema.AsignacionTipo, ConditionOperator.NotEqual, Esquema.TipoEspecial);
            noPorAprobar.AddCondition(Esquema.AsignacionEstadoAprobacion, ConditionOperator.Equal, Esquema.AprobacionAprobado);
            var filas = svc.RetrieveMultiple(q).Entities;

            var aEscribir = Math.Min(filas.Count, MaximoPorEjecucion);
            var fallidas = 0;
            for (var i = 0; i < aEscribir; i++)
            {
                var toque = new Entity(Esquema.Asignacion, filas[i].Id);
                toque[Esquema.AsignacionAgente] = new EntityReference(Esquema.Agente, agenteId);
                try
                {
                    svc.Update(toque);
                }
                catch (FaultException<OrganizationServiceFault>)
                {
                    fallidas++;
                }
                catch (TimeoutException)
                {
                    fallidas++;
                }
            }

            var quedan = filas.Count - aEscribir;
            local.Trace("asignaciones a repartir: {0} escritas, {1} fallidas, quedan {2} o mas.", aEscribir - fallidas, fallidas, quedan);
            if (fallidas > 0 || quedan > 0)
            {
                throw new InvalidPluginExecutionException(OperationStatus.Retry, ErrorReparto,
                    Nombre + ": quedaron asignaciones del agente sin repartir (" + fallidas + " fallidas"
                    + (quedan > 0 ? ", mas de " + MaximoPorEjecucion + " pendientes" : "") + "). Se reintenta.");
            }
        }
    }
}
