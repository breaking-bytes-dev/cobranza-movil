using System;
using System.Diagnostics;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Al vincular, cambiar o quitar el usuario de un agente, mueve sus asignaciones: el reparto solo corre al tocar
    /// la asignacion, y sin esto las Incobrables y Especiales nunca cambiarian de dueno.
    ///
    /// Lee el usuario ACTUAL del agente y busca sus asignaciones activas cuyo dueno no es el que corresponde (el
    /// usuario o, sin usuario, el equipo de cartera sin localizador), sin las Especiales por aprobar. A cada una le
    /// vuelve a escribir el MISMO agente: eso dispara RepartoAsignacion, que mueve el dueno, comparte y revoca.
    /// Asi la logica del reparto vive en un solo lugar.
    ///
    /// Hasta <see cref="MaximoPorEjecucion"/> por ejecucion y sin pasar de <see cref="TiempoMaximo"/> (el limite de
    /// Dataverse es 2 minutos y cada reparto tarda ~0,3 s): si quedan mas, o si alguna escritura falla, termina pidiendo
    /// reintento al servicio asincrono; la consulta del reintento ya no trae las que se movieron. El servicio da 5 ejecuciones
    /// por trabajo: en la ultima, si esta vuelta movio algo, vuelve a escribir el mismo usuario en el agente para encolar un
    /// trabajo nuevo. Si una vuelta no mueve nada no se encadena (falla como siempre); Dataverse corta las cadenas largas.
    /// Corre con el servicio del SISTEMA (ninguna persona tiene por que poder escribir asignaciones ajenas).
    /// Registro: PostOperation ASINCRONO, Update de sanic_cm_tbl_agente filtrado por sanic_cm_ag_usuarioid.
    /// Nunca traza ni cita identificadores ni valores de registros.
    /// </summary>
    public sealed class RepartoPorAgente : PluginBase
    {
        public const int MaximoPorEjecucion = 300;
        public static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(90);

        private readonly TimeSpan presupuesto;

        public RepartoPorAgente() : this(TiempoMaximo)
        {
        }

        /// <summary>Para pruebas: otro tope de tiempo.</summary>
        public RepartoPorAgente(TimeSpan presupuesto)
        {
            this.presupuesto = presupuesto;
        }

        /// <summary>Reintentos que da el servicio asincrono antes de dar el trabajo por fallido (5 ejecuciones).</summary>
        public const int UltimoReintento = 4;

        private const string TrabajoAsincrono = "asyncoperation";
        private const string Reintentos = "retrycount";

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
                throw local.ErrorTecnico("el paso debe registrarse asincrono: recorre todas las asignaciones del agente.");
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
            var reloj = Stopwatch.StartNew();
            for (var i = 0; i < aEscribir; i++)
            {
                if (i > 0 && reloj.Elapsed >= presupuesto)
                {
                    // Cortar antes del limite de Dataverse: el reintento retoma las que faltan.
                    aEscribir = i;
                    break;
                }

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
                if (aEscribir - fallidas > 0 && EsUltimoIntento(local))
                {
                    var toque = new Entity(Esquema.Agente, agenteId);
                    toque[Esquema.AgenteUsuario] = usuario;
                    svc.Update(toque);
                    local.Trace("ultimo intento del trabajo: se encola un reparto nuevo para lo que falta.");
                    return;
                }

                throw new InvalidPluginExecutionException(OperationStatus.Retry, ErrorReparto,
                    Nombre + ": quedaron asignaciones del agente sin repartir (" + fallidas + " fallidas"
                    + (quedan > 0 ? ", " + quedan + " o mas pendientes" : "") + "). Se reintenta.");
            }
        }

        /// <summary>True si este trabajo ya no tiene reintentos. Ante cualquier duda, false: se pide reintento como siempre.</summary>
        private static bool EsUltimoIntento(LocalContext local)
        {
            var trabajo = local.Context.OperationId;
            if (trabajo == Guid.Empty) return false;
            try
            {
                var fila = local.ServicioSistema.Retrieve(TrabajoAsincrono, trabajo, new ColumnSet(Reintentos));
                return fila.GetAttributeValue<int>(Reintentos) >= UltimoReintento;
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                return false;
            }
        }
    }
}
