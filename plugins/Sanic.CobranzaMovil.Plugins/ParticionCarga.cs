using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Carga paralela: decide que filas van al archivo de PADRES y como se reparten las demas en PARTES. Logica pura.
    ///
    /// PADRES (producto, fiador y agente; corre primero y solo): por cada tabla y cada registro distinto, la ULTIMA fila
    /// del archivo donde aparece; las filas elegidas se escriben en su ORDEN ORIGINAL. Asi, para cada registro, la ultima
    /// escritura es su ultima aparicion: el mismo resultado que hoy en serie. La clave es el valor EXACTO (recortado) de
    /// las columnas fuente de su dedup: si Dataverse considera iguales dos valores distintos (mayusculas, acentos), se
    /// eligen las dos ultimas apariciones y la mas tardia sigue siendo la ultima: elegir de mas nunca cambia el resultado.
    ///
    /// PARTES (todas las demas tablas; corren en paralelo): dos filas que escriben el MISMO registro de cualquier tabla
    /// (cliente por CIF, cuenta y asignacion por la clave del numero de cuenta, etc.) van a la misma parte, con
    /// union-find sobre las claves de dedup. La clave se normaliza de mas (sin acentos, mayusculas, solo letras y digitos):
    /// juntar de mas solo desbalancea; juntar de menos haria que dos partes escriban el mismo registro a la vez (la
    /// fila duplicada del 2026-10-12). Grupos repartidos por tamano (el mas grande a la parte con menos filas); dentro de
    /// cada parte, el orden original. Determinista.
    /// </summary>
    public static class ParticionCarga
    {
        private const char SeparadorClave = '\u001F';

        /// <summary>Componentes de la clave de dedup de una tabla: las columnas del archivo de las que depende.</summary>
        public sealed class Clave
        {
            public string Tabla;
            public List<Componente> Componentes = new List<Componente>();
        }

        public sealed class Componente
        {
            /// <summary>Columnas del archivo (posiciones del encabezado) que determinan el valor.</summary>
            public int[] Columnas;
            /// <summary>Valor por defecto del mapeo (Smelter lo usa si la fuente viene vacia); null si no tiene.</summary>
            public string PorDefecto;
        }

        /// <summary>Filas de padres: la ultima aparicion de cada registro de cada tabla, en orden original.</summary>
        public static List<int> Padres(ArchivoCsv csv, IEnumerable<Clave> claves)
        {
            var elegidas = new HashSet<int>();
            foreach (var clave in claves)
            {
                var ultima = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < csv.Filas.Count; i++)
                {
                    ultima[Exacta(csv.Filas[i], clave)] = i;
                }
                foreach (var i in ultima.Values) elegidas.Add(i);
            }
            var lista = elegidas.ToList();
            lista.Sort();
            return lista;
        }

        /// <summary>
        /// Reparte TODAS las filas en hasta <paramref name="partes"/> partes no vacias (menos si hay menos grupos).
        /// </summary>
        public static List<List<int>> Partes(ArchivoCsv csv, IEnumerable<Clave> claves, int partes)
        {
            if (partes < 1) throw new ArgumentOutOfRangeException("partes");
            var n = csv.Filas.Count;
            var padre = new int[n];
            for (var i = 0; i < n; i++) padre[i] = i;

            foreach (var clave in claves)
            {
                var primera = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < n; i++)
                {
                    var k = Normalizada(csv.Filas[i], clave);
                    if (k == null) continue;
                    int otra;
                    if (primera.TryGetValue(k, out otra)) Unir(padre, i, otra);
                    else primera[k] = i;
                }
            }

            var grupos = new Dictionary<int, List<int>>();
            for (var i = 0; i < n; i++)
            {
                var r = Raiz(padre, i);
                List<int> g;
                if (!grupos.TryGetValue(r, out g)) grupos[r] = g = new List<int>();
                g.Add(i); // en orden: el primero es la fila mas temprana del grupo
            }

            var cantidad = Math.Min(partes, Math.Max(1, grupos.Count));
            var resultado = new List<List<int>>();
            var cargas = new int[cantidad];
            for (var p = 0; p < cantidad; p++) resultado.Add(new List<int>());
            foreach (var g in grupos.Values.OrderByDescending(x => x.Count).ThenBy(x => x[0]))
            {
                var destino = 0;
                for (var p = 1; p < cantidad; p++)
                {
                    if (cargas[p] < cargas[destino]) destino = p;
                }
                resultado[destino].AddRange(g);
                cargas[destino] += g.Count;
            }
            foreach (var r in resultado) r.Sort();
            return resultado.Where(r => r.Count > 0).ToList();
        }

        /// <summary>Clave exacta de padres: los valores recortados, sin normalizar.</summary>
        private static string Exacta(ArchivoCsv.Registro fila, Clave clave)
        {
            var sb = new StringBuilder();
            foreach (var c in clave.Componentes)
            {
                foreach (var col in c.Columnas)
                {
                    sb.Append(ArchivoCsv.Valor(fila, col)).Append(SeparadorClave);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clave de partes, normalizada de mas. null si todos los componentes vienen vacios (sin clave Smelter no deduplica:
        /// esas filas no se juntan con nadie por esta tabla).
        /// </summary>
        private static string Normalizada(ArchivoCsv.Registro fila, Clave clave)
        {
            var sb = new StringBuilder(clave.Tabla).Append(SeparadorClave);
            var algo = false;
            foreach (var c in clave.Componentes)
            {
                foreach (var col in c.Columnas)
                {
                    var v = Normalizar(ArchivoCsv.Utf8(ArchivoCsv.Valor(fila, col)));
                    if (v.Length == 0 && c.Columnas.Length == 1 && !string.IsNullOrEmpty(c.PorDefecto)) v = Normalizar(c.PorDefecto);
                    if (v.Length > 0) algo = true;
                    sb.Append(v).Append(SeparadorClave);
                }
            }
            return algo ? sb.ToString() : null;
        }

        /// <summary>Sin acentos, en mayusculas y solo letras y digitos (espacios, guiones y puntos fuera).</summary>
        public static string Normalizar(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;
            var descompuesto = valor.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (var ch in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            }
            return sb.ToString();
        }

        private static int Raiz(int[] padre, int i)
        {
            while (padre[i] != i)
            {
                padre[i] = padre[padre[i]];
                i = padre[i];
            }
            return i;
        }

        private static void Unir(int[] padre, int a, int b)
        {
            var ra = Raiz(padre, a);
            var rb = Raiz(padre, b);
            if (ra == rb) return;
            if (ra < rb) padre[rb] = ra;
            else padre[ra] = rb;
        }
    }
}
