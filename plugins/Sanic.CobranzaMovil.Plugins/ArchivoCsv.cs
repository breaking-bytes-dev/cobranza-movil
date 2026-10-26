using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Carga paralela (PartirCarga): lee el CSV del banco a nivel de BYTES para partirlo sin tocar su contenido.
    /// Cada registro se copia tal cual (mismos bytes, misma codificacion, comillas y saltos internos) a los archivos de
    /// padres y partes, asi Smelter lee cada fila exactamente como la leeria en el archivo entero.
    ///
    /// Reglas (las mismas de Smelter, que usa CsvHelper con RFC 4180):
    ///  - Un registro termina en CRLF, LF o CR fuera de comillas; las comillas dobles abren y cierran un valor ("" es una
    ///    comilla literal). Las comillas, el delimitador y los saltos de linea son bytes ASCII, que en UTF-8 nunca forman
    ///    parte de un caracter multibyte: por eso se puede partir sin decodificar.
    ///  - BOM de UTF-8: se respeta y se repite en cada archivo nuevo. Un BOM de UTF-16/32 es un error (no se parte).
    ///  - Lineas vacias: se ignoran (CsvHelper tambien). El primer registro es el encabezado.
    ///  - Comillas sin cerrar al final, o una fila con MAS columnas que el encabezado (senal de que una comilla suelta
    ///    junto dos filas), es un error: partir mal podria cargar dos filas de un mismo cliente en paralelo.
    /// Los valores se guardan como texto ISO-8859-1 (un caracter por byte, biyectivo): sirven de clave exacta. Los
    /// mensajes de error nunca citan valores: solo numeros de fila y cantidades.
    /// </summary>
    public sealed class ArchivoCsv
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        private static readonly byte[] BomUtf8 = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] SaltoCrLf = { (byte)'\r', (byte)'\n' };

        private readonly byte[] datos;
        private readonly Registro encabezado;
        private readonly List<Registro> filas;
        private readonly string[] columnas;

        private ArchivoCsv(byte[] datos, bool tieneBom, Registro encabezado, List<Registro> filas)
        {
            this.datos = datos;
            TieneBom = tieneBom;
            this.encabezado = encabezado;
            this.filas = filas;
            columnas = new string[encabezado.Campos.Count];
            for (var i = 0; i < columnas.Length; i++) columnas[i] = Utf8(encabezado.Campos[i]).Trim();
        }

        /// <summary>Un registro: donde empieza y cuanto mide en el archivo, y sus valores (ISO-8859-1, sin comillas, recortados).</summary>
        public sealed class Registro
        {
            public int Inicio;
            public int Largo;
            public List<string> Campos;
        }

        public bool TieneBom { get; private set; }

        /// <summary>Nombres de columna del encabezado (UTF-8, recortados), en orden.</summary>
        public IList<string> Columnas
        {
            get { return columnas; }
        }

        /// <summary>Filas de datos (sin el encabezado ni las lineas vacias), en el orden del archivo.</summary>
        public IList<Registro> Filas
        {
            get { return filas; }
        }

        /// <summary>Posicion de la columna (sin distinguir mayusculas, como Smelter); -1 si no esta.</summary>
        public int Indice(string columna)
        {
            if (columna == null) return -1;
            var buscada = columna.Trim();
            for (var i = 0; i < columnas.Length; i++)
            {
                if (string.Equals(columnas[i], buscada, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>Valor de la columna en la fila (ISO-8859-1, exacto); vacio si la fila tiene menos columnas.</summary>
        public static string Valor(Registro fila, int columna)
        {
            return columna >= 0 && columna < fila.Campos.Count ? fila.Campos[columna] : string.Empty;
        }

        /// <summary>El valor como lo lee Smelter (UTF-8; un byte invalido queda como U+FFFD, igual que en Smelter).</summary>
        public static string Utf8(string latin1)
        {
            return Encoding.UTF8.GetString(Latin1.GetBytes(latin1 ?? string.Empty));
        }

        /// <summary>
        /// Archivo nuevo: el BOM (si el original lo tenia), el encabezado ORIGINAL y las filas pedidas, en el orden en que se
        /// piden, cada registro con sus bytes originales y terminado en CRLF.
        /// </summary>
        public byte[] Armar(IEnumerable<int> indices)
        {
            using (var ms = new MemoryStream())
            {
                if (TieneBom) ms.Write(BomUtf8, 0, BomUtf8.Length);
                ms.Write(datos, encabezado.Inicio, encabezado.Largo);
                ms.Write(SaltoCrLf, 0, SaltoCrLf.Length);
                foreach (var i in indices)
                {
                    var r = filas[i];
                    ms.Write(datos, r.Inicio, r.Largo);
                    ms.Write(SaltoCrLf, 0, SaltoCrLf.Length);
                }
                return ms.ToArray();
            }
        }

        /// <summary>Lee el archivo. <paramref name="delimitador"/> es un caracter ASCII (el de la plantilla).</summary>
        public static ArchivoCsv Leer(byte[] datos, char delimitador)
        {
            if (datos == null || datos.Length == 0) throw new FormatException("el archivo esta vacio.");
            if (delimitador == '"' || delimitador == '\r' || delimitador == '\n' || delimitador > 127)
            {
                throw new FormatException("el delimitador de la plantilla no se puede usar para partir (tiene que ser un caracter ASCII).");
            }
            if ((datos.Length >= 2 && ((datos[0] == 0xFF && datos[1] == 0xFE) || (datos[0] == 0xFE && datos[1] == 0xFF)))
                || (datos.Length >= 4 && datos[0] == 0 && datos[1] == 0 && datos[2] == 0xFE && datos[3] == 0xFF))
            {
                throw new FormatException("el archivo esta en UTF-16 o UTF-32: tiene que venir en UTF-8.");
            }
            var bom = datos.Length >= 3 && datos[0] == BomUtf8[0] && datos[1] == BomUtf8[1] && datos[2] == BomUtf8[2];
            var d = (byte)delimitador;

            var registros = new List<Registro>();
            var inicio = bom ? 3 : 0;
            var entreComillas = false;
            var n = datos.Length;
            for (var i = inicio; i < n; i++)
            {
                var b = datos[i];
                if (b == (byte)'"')
                {
                    entreComillas = !entreComillas;
                }
                else if (!entreComillas && (b == (byte)'\r' || b == (byte)'\n'))
                {
                    Agregar(registros, datos, inicio, i, d);
                    if (b == (byte)'\r' && i + 1 < n && datos[i + 1] == (byte)'\n') i++;
                    inicio = i + 1;
                }
            }
            if (entreComillas)
            {
                throw new FormatException("el archivo tiene comillas sin cerrar (fila " + (registros.Count + 1).ToString(CultureInfo.InvariantCulture) + ").");
            }
            Agregar(registros, datos, inicio, n, d);
            if (registros.Count == 0) throw new FormatException("el archivo no tiene encabezado.");

            var encabezado = registros[0];
            registros.RemoveAt(0);
            for (var i = 0; i < registros.Count; i++)
            {
                if (registros[i].Campos.Count > encabezado.Campos.Count)
                {
                    throw new FormatException("la fila " + (i + 2).ToString(CultureInfo.InvariantCulture) + " tiene "
                        + registros[i].Campos.Count.ToString(CultureInfo.InvariantCulture) + " columnas y el encabezado "
                        + encabezado.Campos.Count.ToString(CultureInfo.InvariantCulture)
                        + " (una comilla suelta o un delimitador de mas): no se puede partir con seguridad.");
                }
            }
            return new ArchivoCsv(datos, bom, encabezado, registros);
        }

        private static void Agregar(List<Registro> registros, byte[] datos, int inicio, int fin, byte delimitador)
        {
            if (fin <= inicio) return; // linea vacia
            var campos = new List<string>();
            var desde = inicio;
            var entre = false;
            for (var i = inicio; i < fin; i++)
            {
                var b = datos[i];
                if (b == (byte)'"') entre = !entre;
                else if (!entre && b == delimitador)
                {
                    campos.Add(Campo(datos, desde, i));
                    desde = i + 1;
                }
            }
            campos.Add(Campo(datos, desde, fin));
            registros.Add(new Registro { Inicio = inicio, Largo = fin - inicio, Campos = campos });
        }

        /// <summary>Valor sin comillas (RFC 4180, "" = comilla) y recortado, como lo deja CsvHelper con TrimOptions.Trim.</summary>
        private static string Campo(byte[] datos, int desde, int hasta)
        {
            while (desde < hasta && (datos[desde] == (byte)' ' || datos[desde] == (byte)'\t')) desde++;
            while (hasta > desde && (datos[hasta - 1] == (byte)' ' || datos[hasta - 1] == (byte)'\t')) hasta--;
            if (desde >= hasta) return string.Empty;
            if (datos[desde] != (byte)'"') return Latin1.GetString(datos, desde, hasta - desde);

            var sb = new StringBuilder(hasta - desde);
            var k = desde + 1;
            while (k < hasta)
            {
                var b = datos[k];
                if (b == (byte)'"')
                {
                    if (k + 1 < hasta && datos[k + 1] == (byte)'"')
                    {
                        sb.Append('"');
                        k += 2;
                        continue;
                    }
                    k++;
                    // Lo que quede despues de la comilla de cierre se toma tal cual (CsvHelper sin BadDataFound).
                    if (k < hasta) sb.Append(Latin1.GetString(datos, k, hasta - k));
                    break;
                }
                sb.Append((char)b);
                k++;
            }
            return sb.ToString().Trim(' ', '\t');
        }
    }
}
