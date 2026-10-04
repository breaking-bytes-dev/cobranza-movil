using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Red de seguridad PCI: impide que un PAN en claro llegue a las columnas "visibles" de CM.
    ///  - Columnas de texto (nombre principal de cuenta, carga diaria y boveda): rechaza 5+ digitos seguidos.
    ///  - Columnas denormalizadas viejas (asignacion, interaccion, pago): rechaza cualquier valor no vacio.
    /// El mensaje de error nombra la COLUMNA, nunca el valor. No usa la clave K ni el servicio de organizacion.
    ///
    /// Registro: PreOperation sincrono, Create y Update (Update filtrado por la columna regulada).
    /// IMPORTANTE: los pasos de este plugin se registran DESACTIVADOS y se activan en el paso 8 de la
    /// migracion, porque hasta entonces la carga de Smelter todavia escribe el PAN en esas columnas.
    /// </summary>
    public sealed class PanGuard : PluginBase
    {
        private enum Tipo
        {
            /// <summary>Rechaza secuencias de 5+ digitos.</summary>
            PanEnClaro,
            /// <summary>Rechaza cualquier valor no vacio (columna en desuso).</summary>
            DebeQuedarVacia
        }

        private sealed class Regla
        {
            public Regla(string columna, Tipo tipo)
            {
                Columna = columna;
                Tipo = tipo;
            }

            public string Columna { get; private set; }
            public Tipo Tipo { get; private set; }
        }

        private static readonly Dictionary<string, Regla[]> ReglasPorEntidad =
            new Dictionary<string, Regla[]>(StringComparer.Ordinal)
            {
                { Esquema.Cuenta, new[] { new Regla(Esquema.CuentaNumero, Tipo.PanEnClaro) } },
                { Esquema.CargaDiaria, new[] { new Regla(Esquema.CargaDiariaNumero, Tipo.PanEnClaro) } },
                { Esquema.CuentaPan, new[] { new Regla(Esquema.CuentaPanNombre, Tipo.PanEnClaro) } },
                { Esquema.Asignacion, new[] { new Regla(Esquema.AsignacionNumeroLegacy, Tipo.DebeQuedarVacia) } },
                { Esquema.Interaccion, new[] { new Regla(Esquema.InteraccionNumeroLegacy, Tipo.DebeQuedarVacia) } },
                { Esquema.Pago, new[] { new Regla(Esquema.PagoNumeroLegacy, Tipo.DebeQuedarVacia) } },
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

            Regla[] reglas;
            if (!ReglasPorEntidad.TryGetValue(ctx.PrimaryEntityName ?? string.Empty, out reglas))
                throw local.Error("la entidad '" + ctx.PrimaryEntityName + "' no tiene reglas configuradas.");

            var target = local.ObtenerTarget();

            var infractoras = new List<string>();
            foreach (var regla in reglas)
            {
                // Update: solo las columnas presentes en el Target. Create: idem (lo no enviado queda vacio).
                if (!target.Contains(regla.Columna)) continue;

                var texto = AComoTexto(target[regla.Columna]);
                if (string.IsNullOrEmpty(texto)) continue;

                var viola = regla.Tipo == Tipo.DebeQuedarVacia
                    || PanDetector.ContieneSecuenciaDeDigitos(texto);
                if (viola) infractoras.Add(regla.Columna);
            }

            if (infractoras.Count == 0)
            {
                local.Trace("sin infracciones.");
                return;
            }

            // Solo nombres de columna: ni el valor ni fragmentos suyos.
            local.Trace("rechazado. columnas={0}", string.Join(",", infractoras));
            throw local.Error(MensajePara(ctx.PrimaryEntityName, reglas, infractoras));
        }

        private static string MensajePara(string entidad, Regla[] reglas, List<string> infractoras)
        {
            var columnas = string.Join(", ", infractoras);
            var hayLegacy = false;
            foreach (var r in reglas)
            {
                if (r.Tipo == Tipo.DebeQuedarVacia && infractoras.Contains(r.Columna)) hayLegacy = true;
            }

            return hayLegacy
                ? "la columna " + columnas + " esta en desuso y debe quedar vacia. "
                  + "El numero de cuenta completo vive solo en la boveda " + Esquema.CuentaPan + "."
                : "la columna " + columnas + " no admite un numero de cuenta completo. "
                  + "Solo se permite el valor enmascarado con los ultimos 4 digitos.";
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
