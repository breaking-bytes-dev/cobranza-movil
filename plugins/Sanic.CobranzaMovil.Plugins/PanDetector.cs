using System.Collections.Generic;
using System.Text;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Detecta si un texto contiene una secuencia de digitos que podria ser (parte de) un PAN.
    /// Logica pura, sin SDK: se prueba aparte.
    /// </summary>
    public static class PanDetector
    {
        public const int MinimoPorDefecto = 5;
        /// <summary>Largo de un PAN segun ISO/IEC 7812.</summary>
        public const int MinimoDigitosPan = 13;
        public const int MaximoDigitosPan = 19;
        /// <summary>Lo que queda en lugar del numero: el mismo formato que muestran las apps.</summary>
        public const string PrefijoEnmascarado = "\u2022\u2022\u2022\u2022 ";

        /// <summary>
        /// true si el texto tiene <paramref name="minimo"/> o mas digitos seguidos. Se toleran separadores
        /// (espacio, guion, espacio de no separacion) ENTRE digitos, para no dejar pasar "4111 1111 1111 1111"
        /// o "4111-1111-1111-1111". Cuentan los digitos Unicode (arabe-indicos, ancho completo).
        /// Un valor enmascarado como ****1234 (4 digitos) no dispara.
        /// </summary>
        public static bool ContieneSecuenciaDeDigitos(string valor, int minimo = MinimoPorDefecto)
        {
            if (string.IsNullOrEmpty(valor)) return false;

            var digitos = 0;
            for (var i = 0; i < valor.Length; i++)
            {
                var c = valor[i];
                if (char.IsDigit(c))
                {
                    digitos++;
                    if (digitos >= minimo) return true;
                }
                else if (digitos > 0 && EsSeparador(c))
                {
                    // Un separador no corta la racha; solo cuenta si luego vuelve un digito.
                    // Si lo que sigue no es digito ni separador, la racha se reinicia en el else siguiente.
                    continue;
                }
                else
                {
                    digitos = 0;
                }
            }
            return false;
        }

        /// <summary>
        /// Reemplaza cada numero de tarjeta del texto por "•••• " + sus ultimos 4. Un candidato es una racha de 13 a 19
        /// digitos (con los mismos separadores que <see cref="ContieneSecuenciaDeDigitos"/>) que pasa Luhn: asi no se tocan
        /// telefonos, recibos ni importes, que casi nunca son de ese largo y ademas validos por Luhn.
        /// En una racha mas larga (por ejemplo, un telefono pegado a la tarjeta con un espacio) se prueban los tramos que
        /// empiezan y terminan en un borde de grupo, del mas largo al mas corto: dentro de una tira de digitos sin
        /// separadores no se busca, porque casi cualquier ventana de 13+ digitos pasaria Luhn una de cada diez veces.
        /// Devuelve el texto (el mismo objeto si no hubo cambios) y cuantos numeros enmascaro.
        /// </summary>
        public static string EnmascararPan(string texto, out int enmascarados)
        {
            enmascarados = 0;
            if (string.IsNullOrEmpty(texto)) return texto;

            StringBuilder resultado = null;
            var copiadoHasta = 0;
            var i = 0;
            while (i < texto.Length)
            {
                if (!char.IsDigit(texto[i]))
                {
                    i++;
                    continue;
                }
                var grupos = Grupos(texto, i);
                var g = 0;
                while (g < grupos.Count)
                {
                    var fin = -1;
                    string digitos = null;
                    for (var h = grupos.Count - 1; h >= g && fin < 0; h--)
                    {
                        var cuantos = 0;
                        for (var k = g; k <= h; k++) cuantos += grupos[k].Fin - grupos[k].Inicio;
                        if (cuantos < MinimoDigitosPan || cuantos > MaximoDigitosPan) continue;
                        var candidato = Digitos(texto, grupos[g].Inicio, grupos[h].Fin);
                        if (!PasaLuhn(candidato)) continue;
                        fin = h;
                        digitos = candidato;
                    }
                    if (fin < 0)
                    {
                        g++;
                        continue;
                    }
                    if (resultado == null) resultado = new StringBuilder(texto.Length);
                    resultado.Append(texto, copiadoHasta, grupos[g].Inicio - copiadoHasta);
                    resultado.Append(PrefijoEnmascarado).Append(digitos.Substring(digitos.Length - 4));
                    copiadoHasta = grupos[fin].Fin;
                    enmascarados++;
                    g = fin + 1;
                }
                i = grupos[grupos.Count - 1].Fin;
            }
            if (resultado == null) return texto;
            resultado.Append(texto, copiadoHasta, texto.Length - copiadoHasta);
            return resultado.ToString();
        }

        /// <summary>Luhn (mod 10) sobre digitos ASCII.</summary>
        public static bool PasaLuhn(string digitos)
        {
            if (string.IsNullOrEmpty(digitos)) return false;
            var suma = 0;
            var doblar = false;
            for (var i = digitos.Length - 1; i >= 0; i--)
            {
                var d = digitos[i] - '0';
                if (d < 0 || d > 9) return false;
                if (doblar)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                suma += d;
                doblar = !doblar;
            }
            return suma % 10 == 0;
        }

        private struct Grupo
        {
            public int Inicio;
            public int Fin; // exclusivo
        }

        /// <summary>Grupos de digitos seguidos de una racha que empieza en <paramref name="desde"/>, unidos por separadores.</summary>
        private static List<Grupo> Grupos(string texto, int desde)
        {
            var grupos = new List<Grupo>();
            var i = desde;
            while (true)
            {
                var inicio = i;
                while (i < texto.Length && char.IsDigit(texto[i])) i++;
                grupos.Add(new Grupo { Inicio = inicio, Fin = i });
                var j = i;
                while (j < texto.Length && EsSeparador(texto[j])) j++;
                if (j == i || j >= texto.Length || !char.IsDigit(texto[j])) return grupos;
                i = j;
            }
        }

        /// <summary>Los digitos del tramo como ASCII (los Unicode, arabe-indicos o de ancho completo, por su valor).</summary>
        private static string Digitos(string texto, int inicio, int fin)
        {
            var sb = new StringBuilder(fin - inicio);
            for (var i = inicio; i < fin; i++)
            {
                if (char.IsDigit(texto[i])) sb.Append((char)('0' + (int)char.GetNumericValue(texto[i])));
            }
            return sb.ToString();
        }

        private static bool EsSeparador(char c)
        {
            return c == ' ' || c == '-' || c == ' ';
        }
    }
}
