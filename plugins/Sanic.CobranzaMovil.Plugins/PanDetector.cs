namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Detecta si un texto contiene una secuencia de digitos que podria ser (parte de) un PAN.
    /// Logica pura, sin SDK: se prueba aparte.
    /// </summary>
    public static class PanDetector
    {
        public const int MinimoPorDefecto = 5;

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

        private static bool EsSeparador(char c)
        {
            return c == ' ' || c == '-' || c == ' ';
        }
    }
}
