using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Red de seguridad PCI: impide que un PAN en claro llegue a las columnas "visibles" de CM (nombre principal de
    /// cuenta, carga diaria y boveda): rechaza 5+ digitos seguidos, porque el valor valido es el enmascarado.
    /// El mensaje de error nombra la COLUMNA, nunca el valor. No usa la clave K ni el servicio de organizacion.
    /// Los textos libres que vienen del telefono no se rechazan: los enmascara <see cref="PanEnTextoLibre"/>.
    ///
    /// Registro: PreOperation sincrono, Create y Update (Update filtrado por la columna regulada).
    /// </summary>
    public sealed class PanGuard : PluginBase
    {
        private static readonly Dictionary<string, string[]> ColumnasPorEntidad =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { Esquema.Cuenta, new[] { Esquema.CuentaNumero } },
                { Esquema.CargaDiaria, new[] { Esquema.CargaDiariaNumero } },
                { Esquema.CuentaPan, new[] { Esquema.CuentaPanNombre } },
            };

        protected override string Nombre
        {
            get { return "PanGuard"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;

            if (!local.EsMensaje(Esquema.MensajeCreate, Esquema.MensajeUpdate))
                throw local.Error("solo se registra en Create y Update.");

            string[] columnas;
            if (!ColumnasPorEntidad.TryGetValue(ctx.PrimaryEntityName ?? string.Empty, out columnas))
                throw local.Error("la entidad '" + ctx.PrimaryEntityName + "' no tiene reglas configuradas.");

            var target = local.ObtenerTarget();

            var infractoras = new List<string>();
            foreach (var columna in columnas)
            {
                // Update: solo las columnas presentes en el Target. Create: idem (lo no enviado queda vacio).
                if (!target.Contains(columna)) continue;

                var texto = AComoTexto(target[columna]);
                if (string.IsNullOrEmpty(texto)) continue;

                if (PanDetector.ContieneSecuenciaDeDigitos(texto)) infractoras.Add(columna);
            }

            if (infractoras.Count == 0)
            {
                local.Trace("sin infracciones.");
                return;
            }

            // Solo nombres de columna: ni el valor ni fragmentos suyos.
            local.Trace("rechazado. columnas={0}", string.Join(",", infractoras));
            throw local.Error("la columna " + string.Join(", ", infractoras) + " no admite un numero de cuenta completo. "
                + "Solo se permite el valor enmascarado con los ultimos 4 digitos.");
        }

        private static string AComoTexto(object valor)
        {
            if (valor == null) return null;
            var texto = valor as string;
            if (texto != null) return texto;
            return Convert.ToString(valor, CultureInfo.InvariantCulture);
        }
    }
}
