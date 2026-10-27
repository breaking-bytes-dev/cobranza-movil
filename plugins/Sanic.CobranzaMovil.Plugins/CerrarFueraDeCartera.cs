using System;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Custom API global sanic_cm_CerrarFueraDeCartera: cierra por lotes las Diarias activas que quedaron fuera de la
    /// ultima carga (su "Ultima carga" es anterior al corte). Una lista de la app no puede pasar de 2000 filas.
    ///  - Corte: el dia de la ultima carga diaria. Si no coincide con el que vio la app (llego otra carga), no cierra nada.
    ///  - Esperadas: cuantas confirmo el Admin. Si hay mas, no cierra: la pantalla estaba desactualizada.
    ///  - Lote: hasta Lote filas por llamado (0 = 250, maximo 500) para no pasar el limite de 2 minutos de un plugin;
    ///    la app vuelve a llamar mientras Restantes sea mayor que cero.
    ///  - SoloContar: no escribe; devuelve cuantas cerraria.
    /// Cierra con los permisos del llamador para que la auditoria muestre al Admin; los demas plugins de la asignacion
    /// corren igual (RepartoAsignacion revoca la cuenta al localizador). Solo Admin, Servicio o sin roles de CM.
    /// Nunca traza valores: solo cantidades.
    /// </summary>
    public sealed class CerrarFueraDeCartera : PluginBase
    {
        public const string Motivo = "fuera de cartera";
        private const int LotePorDefecto = 250;
        private const int LoteMaximo = 500;

        protected override string Nombre
        {
            get { return "CerrarFueraDeCartera"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            if (!string.Equals(ctx.MessageName, Esquema.MensajeCerrarFueraDeCartera, StringComparison.Ordinal)
                || ctx.Stage != Esquema.EtapaMainOperation)
            {
                throw local.ErrorTecnico("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeCerrarFueraDeCartera + ".");
            }

            if (!RolesCm.DelLlamador(local).SinRestriccion)
            {
                throw local.Error("Solo el Admin puede cerrar en lote las asignaciones fuera de la carga.");
            }

            var corteApp = Entrada<string>(ctx, Esquema.ParamCorte);
            DateTime corte;
            if (string.IsNullOrWhiteSpace(corteApp)
                || !DateTime.TryParseExact(corteApp.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out corte))
            {
                throw local.Error("Falta el corte (día de la última carga, aaaa-mm-dd).");
            }
            var esperadas = Entrada<int?>(ctx, Esquema.ParamEsperadas);
            if (!esperadas.HasValue || esperadas.Value < 0) throw local.Error("Falta la cantidad confirmada.");
            // Opcional: Dataverse manda 0 cuando la app no lo pasa, asi que 0 = el lote por defecto.
            var lote = Entrada<int?>(ctx, Esquema.ParamLote) ?? 0;
            if (lote == 0) lote = LotePorDefecto;
            if (lote < 1 || lote > LoteMaximo) throw local.Error("El lote tiene que ser de 1 a " + LoteMaximo + ".");
            var soloContar = Entrada<bool?>(ctx, Esquema.ParamSoloContar) == true;

            var corteServidor = UltimaCarga(local.ServicioSistema);
            if (!corteServidor.HasValue) throw local.Error("No hay ninguna carga diaria: no hay nada que comparar.");
            if (corteServidor.Value != corte.Date)
            {
                throw local.Error("La última carga cambió desde que abriste la pantalla. Actualizá Reconciliación y volvé a confirmar.");
            }

            var pendientes = Contar(local.Service, corte.Date);
            if (pendientes > esperadas.Value)
            {
                throw local.Error("Hay " + pendientes + " Diarias fuera de la carga, más de las " + esperadas.Value
                    + " que confirmaste. Actualizá Reconciliación y volvé a confirmar.");
            }
            if (soloContar || pendientes == 0)
            {
                Responder(local, 0, pendientes, corte);
                return;
            }

            var q = Consulta(corte.Date);
            q.ColumnSet = new ColumnSet(false);
            q.TopCount = lote;
            var filas = local.Service.RetrieveMultiple(q);
            var ahora = DateTime.UtcNow;
            var cerradas = 0;
            foreach (var fila in filas == null ? Enumerable.Empty<Entity>() : filas.Entities)
            {
                var r = new Entity(Esquema.Asignacion, fila.Id);
                r[Esquema.AsignacionEstado] = false;
                r[Esquema.AsignacionMotivo] = Motivo;
                r[Esquema.AsignacionFechaCierre] = ahora;
                try
                {
                    local.Service.Update(r);
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    local.Trace("el cierre fallo despues de {0} filas con el codigo {1}.", cerradas, ex.Detail == null ? 0 : ex.Detail.ErrorCode);
                    throw local.Error("No se pudo cerrar el lote (no se cerró ninguna de este tramo). Volvé a intentar.");
                }
                cerradas++;
            }
            Responder(local, cerradas, Math.Max(0, pendientes - cerradas), corte);
        }

        private static T Entrada<T>(IPluginExecutionContext ctx, string nombre)
        {
            var inputs = ctx.InputParameters;
            if (inputs == null || !inputs.Contains(nombre) || inputs[nombre] == null) return default(T);
            if (!(inputs[nombre] is T)) return default(T);
            return (T)inputs[nombre];
        }

        /// <summary>Dia de la ultima carga diaria ([CD] FechaCarga es TimeZoneIndependent: la fecha se toma tal cual).</summary>
        private static DateTime? UltimaCarga(IOrganizationService svc)
        {
            var q = new QueryExpression(Esquema.CargaDiaria) { ColumnSet = new ColumnSet(Esquema.CargaDiariaFecha), TopCount = 1 };
            q.Criteria.AddCondition(Esquema.CargaDiariaFecha, ConditionOperator.NotNull);
            q.AddOrder(Esquema.CargaDiariaFecha, OrderType.Descending);
            var filas = svc.RetrieveMultiple(q);
            var fila = filas == null ? null : filas.Entities.FirstOrDefault();
            var f = fila == null ? null : fila.GetAttributeValue<DateTime?>(Esquema.CargaDiariaFecha);
            return f.HasValue ? f.Value.Date : (DateTime?)null;
        }

        private static QueryExpression Consulta(DateTime corte)
        {
            var q = new QueryExpression(Esquema.Asignacion);
            q.Criteria.AddCondition(Esquema.AsignacionTipo, ConditionOperator.Equal, Esquema.TipoDiaria);
            q.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);
            q.Criteria.AddCondition(Esquema.AsignacionUltimaCarga, ConditionOperator.LessThan, corte);
            return q;
        }

        /// <summary>Cuantas quedan fuera de la carga, con los permisos del llamador (lo mismo que ve en la pantalla).</summary>
        private static int Contar(IOrganizationService svc, DateTime corte)
        {
            var fx = "<fetch aggregate='true'><entity name='" + Esquema.Asignacion + "'>"
                + "<attribute name='" + Esquema.Asignacion + "id' alias='n' aggregate='count'/>"
                + "<filter><condition attribute='" + Esquema.AsignacionTipo + "' operator='eq' value='" + Esquema.TipoDiaria + "'/>"
                + "<condition attribute='" + Esquema.AsignacionEstado + "' operator='eq' value='1'/>"
                + "<condition attribute='" + Esquema.AsignacionUltimaCarga + "' operator='lt' value='" + corte.ToString("yyyy-MM-dd") + "'/>"
                + "</filter></entity></fetch>";
            var filas = svc.RetrieveMultiple(new FetchExpression(fx));
            var fila = filas == null ? null : filas.Entities.FirstOrDefault();
            var n = fila == null ? null : fila.GetAttributeValue<AliasedValue>("n");
            return n == null || n.Value == null ? 0 : Convert.ToInt32(n.Value);
        }

        private static void Responder(LocalContext local, int cerradas, int restantes, DateTime corte)
        {
            var salida = local.Context.OutputParameters;
            salida[Esquema.SalidaCerradas] = cerradas;
            salida[Esquema.SalidaRestantes] = restantes;
            salida[Esquema.SalidaCorte] = corte.ToString("yyyy-MM-dd");
            local.Trace("cerradas {0}, restantes {1}.", cerradas, restantes);
        }
    }
}
