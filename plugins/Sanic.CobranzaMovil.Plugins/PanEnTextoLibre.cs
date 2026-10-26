using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// PCI en los textos libres que escribe el localizador (comentario de la interaccion y direcciones de la
    /// actualizacion de datos): un numero de tarjeta (13 a 19 digitos, con espacios o guiones, que pasa Luhn) se
    /// reemplaza en el Target por "•••• " + sus ultimos 4 antes de guardarse. Ver <see cref="PanDetector.EnmascararPan"/>.
    ///
    /// Por que no es una regla de <see cref="PanGuard"/>: PanGuard RECHAZA, y lo que llega del telefono sin conexion no se
    /// puede rechazar (el runtime offline descarta el registro en silencio y se pierde la visita). Por eso este plugin
    /// nunca lanza: un registro mal configurado o un valor que no es texto solo se trazan. Ademas sus pasos van siempre
    /// activos, sin el ciclo de activacion de los de PanGuard.
    /// Telefonos y correo no se tocan: ya validan su formato en la app. Traza solo el nombre de la columna enmascarada.
    ///
    /// Registro: PreOperation sincrono, Create y Update (Update filtrado por estas columnas).
    /// </summary>
    public sealed class PanEnTextoLibre : PluginBase
    {
        private static readonly Dictionary<string, string[]> ColumnasPorEntidad =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { Esquema.Interaccion, new[] { Esquema.InteraccionComentario } },
                {
                    Esquema.ActualizacionDatos, new[]
                    {
                        Esquema.ActualizacionDireccionCasa, Esquema.ActualizacionDireccionEc, Esquema.ActualizacionDireccionTrabajo,
                    }
                },
            };

        protected override string Nombre
        {
            get { return "PanEnTextoLibre"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            string[] columnas;
            if (!local.EsMensaje(Esquema.MensajeCreate, Esquema.MensajeUpdate)
                || ctx.Stage > Esquema.EtapaPreOperation
                || !ColumnasPorEntidad.TryGetValue(ctx.PrimaryEntityName ?? string.Empty, out columnas))
            {
                // Despues de PreOperation el cambio al Target ya no se guarda; igual no se rechaza nada.
                local.Trace("registro incorrecto del paso (se espera Create/Update en PreOperation sobre {0} o {1}); no hace nada.",
                    Esquema.Interaccion, Esquema.ActualizacionDatos);
                return;
            }

            var inputs = ctx.InputParameters;
            var target = inputs != null && inputs.Contains("Target") ? inputs["Target"] as Entity : null;
            if (target == null)
            {
                local.Trace("sin Target de tipo entidad; no hace nada.");
                return;
            }

            var enmascaradas = new List<string>();
            foreach (var columna in columnas)
            {
                if (!target.Contains(columna)) continue;
                var texto = target[columna] as string;
                if (string.IsNullOrEmpty(texto)) continue;

                int cuantos;
                var limpio = PanDetector.EnmascararPan(texto, out cuantos);
                if (cuantos == 0) continue;
                target[columna] = limpio;
                enmascaradas.Add(columna);
            }

            if (enmascaradas.Count == 0)
            {
                local.Trace("sin numeros de tarjeta.");
                return;
            }
            local.Trace("enmascarado. columnas={0}", string.Join(",", enmascaradas));
        }
    }
}
