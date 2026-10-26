using System;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// APP-48 (decision del dueno, 2026-10-25): Custom API global "sanic_cm_CerrarFueraDeCartera", que reemplaza el Patch
    /// masivo de scrReconciliacion. El Patch de la app tomaba solo las primeras 2000 Diarias (tope de filas de la lista),
    /// tardaba minutos sin aviso y dejaba un error de fecha en pantalla. Aca el servidor cierra por lotes:
    ///  - Corte: el DIA de la ultima carga diaria (maximo de [CD] FechaCarga). La app manda el corte que vio; si no coincide
    ///    (llego otra carga mientras la pantalla estaba abierta), no cierra nada.
    ///  - Fuera de la carga: Diaria activa con "Ultima carga" (DateOnly) anterior al corte. Mismo criterio que la pantalla.
    ///  - Esperadas: cuantas confirmo el Admin (en las vueltas siguientes, las que quedaban). Si hay MAS, no cierra: la
    ///    pantalla estaba vieja y el Admin no confirmo esas.
    ///  - Lote: cierra hasta Lote filas por llamado (por defecto 250, maximo 500) para no pasar el limite de 2 minutos de un
    ///    plugin; la app llama de nuevo mientras Restantes sea mayor que cero.
    ///  - SoloContar: true = no escribe; devuelve cuantas cerraria (pruebas y verificacion).
    /// Cierra con los permisos del LLAMADOR (la auditoria muestra al Admin, como el Patch de antes): estado = false, motivo
    /// "fuera de cartera", fecha de cierre = ahora. Los demas plugins de la asignacion corren igual (RepartoAsignacion le
    /// revoca la cuenta al cobrador). Solo Admin, Servicio o sin roles de CM. La carga en curso la sigue verificando la app
    /// (lee los Import Jobs de Smelter). Nunca traza valores: solo cantidades.
    /// Registro: Custom API global (sin entidad), no es funcion, sin pasos personalizados; este tipo es su plugin.
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
                throw local.Error("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeCerrarFueraDeCartera + ".");
            }

            if (!RolesCm.DelLlamador(local).SinRestriccion)
            {
                throw local.Error("solo el Admin puede cerrar en lote las asignaciones fuera de la carga.");
            }

            var corteApp = Entrada<string>(ctx, Esquema.ParamCorte);
            DateTime corte;
            if (string.IsNullOrWhiteSpace(corteApp)
                || !DateTime.TryParseExact(corteApp.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out corte))
            {
                throw local.Error("falta el corte (dia de la ultima carga, aaaa-mm-dd).");
            }
            var esperadas = Entrada<int?>(ctx, Esquema.ParamEsperadas);
            if (!esperadas.HasValue || esperadas.Value < 0) throw local.Error("falta la cantidad confirmada.");
            var lote = Entrada<int?>(ctx, Esquema.ParamLote) ?? LotePorDefecto;
            if (lote < 1 || lote > LoteMaximo) throw local.Error("el lote debe ser de 1 a " + LoteMaximo + ".");
            var soloContar = Entrada<bool?>(ctx, Esquema.ParamSoloContar) == true;

            var corteServidor = UltimaCarga(local.ServicioSistema);
            if (!corteServidor.HasValue) throw local.Error("no hay ninguna carga diaria: no hay nada que comparar.");
            if (corteServidor.Value != corte.Date)
            {
                throw local.Error("la ultima carga cambio desde que abriste la pantalla. Actualiza Reconciliacion y volve a confirmar.");
            }

            var pendientes = Contar(local.Service, corte.Date);
            if (pendientes > esperadas.Value)
            {
                throw local.Error("hay " + pendientes + " Diarias fuera de la carga, mas de las " + esperadas.Value
                    + " que confirmaste. Actualiza Reconciliacion y volve a confirmar.");
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
                    throw local.Error("no se pudo cerrar el lote (no se cerro ninguna de este tramo). Volve a intentar.");
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
