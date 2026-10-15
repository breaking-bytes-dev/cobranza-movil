using System;
using System.Collections.Generic;
using System.Text;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// DAT-20: arma el CSV de UNA fila con el formato del archivo del banco (el de test-data/cobranza-mock-1000.csv y
    /// tests/integral/lib/csv.js), para que el alta del Admin entre por la plantilla "Cobranza movil" de Smelter igual que
    /// la carga diaria. Logica pura, sin SDK: se prueba aparte.
    /// Smelter lee el archivo con CsvHelper (RFC 4180, src/Smelter.Core/Services/CsvParserService.cs): un valor con coma,
    /// comillas o salto de linea va entre comillas y las comillas internas se duplican.
    /// </summary>
    public static class FilaDeCarga
    {
        /// <summary>Encabezado EXACTO del archivo del banco: 33 columnas, en este orden.</summary>
        public static readonly string[] Encabezado =
        {
            "Cliente", "CIF", "DireccionCasa", "DireccionEC", "DireccionTrabajo", "Departamento", "NumeroCelular",
            "NumeroCasa", "NumeroCuenta", "SaldoVencidoNIO", "SaldoVencidoUSD", "TotalSaldoVencido", "ContagiadoUSD",
            "PagoMinimoNIO", "PagoMinimoUSD", "DiasMora", "NivelMora", "FechaUltimoPago", "FechaPago", "Ciclo",
            "CodigoProducto", "TipoProducto", "NombreFianza", "DireccionFianza", "RetiroNIO", "RetiroUSD", "TipoEspecial",
            "Nota", "Distrito", "Trabajo", "TipoAsignacion", "Agente", "FechaCarga",
        };

        /// <summary>
        /// Encabezado + una fila, separados y terminados en CRLF, en UTF-8 sin BOM (como el CSV de la suite).
        /// Una columna que no esta en <paramref name="fila"/> va vacia; una clave que no es del encabezado es un error.
        /// </summary>
        public static byte[] Armar(IDictionary<string, string> fila)
        {
            if (fila == null) throw new ArgumentNullException("fila");
            foreach (var clave in fila.Keys)
            {
                if (Array.IndexOf(Encabezado, clave) < 0) throw new ArgumentException("columna desconocida: " + clave);
            }
            var valores = new string[Encabezado.Length];
            for (var i = 0; i < Encabezado.Length; i++)
            {
                string v;
                valores[i] = Escapar(fila.TryGetValue(Encabezado[i], out v) ? v : null);
            }
            var texto = string.Join(",", Encabezado) + "\r\n" + string.Join(",", valores) + "\r\n";
            return new UTF8Encoding(false).GetBytes(texto);
        }

        /// <summary>RFC 4180: entre comillas si trae coma, comillas o salto de linea; las comillas internas se duplican.</summary>
        public static string Escapar(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;
            if (valor.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return valor;
            return "\"" + valor.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// El numero de tarjeta sin espacios ni guiones (la canonizacion del HMAC de Smelter). null si trae otra cosa que
        /// digitos ASCII, espacios o guiones, o si no tiene entre 13 y 19 digitos.
        /// </summary>
        public static string Canonizar(string pan)
        {
            if (string.IsNullOrWhiteSpace(pan)) return null;
            var sb = new StringBuilder(pan.Length);
            foreach (var c in pan.Trim())
            {
                if (c >= '0' && c <= '9') sb.Append(c);
                else if (c != ' ' && c != '-') return null;
            }
            return sb.Length >= 13 && sb.Length <= 19 ? sb.ToString() : null;
        }

        /// <summary>Digito verificador de Luhn sobre una cadena de digitos ASCII.</summary>
        public static bool LuhnValido(string digitos)
        {
            if (string.IsNullOrEmpty(digitos)) return false;
            var suma = 0;
            var doble = false;
            for (var i = digitos.Length - 1; i >= 0; i--)
            {
                var c = digitos[i];
                if (c < '0' || c > '9') return false;
                var d = c - '0';
                if (doble)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                suma += d;
                doble = !doble;
            }
            return suma % 10 == 0;
        }
    }
}
