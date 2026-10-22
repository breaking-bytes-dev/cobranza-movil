using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sanic.CobranzaMovil.Plugins;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Carga paralela: que filas van a PADRES (la ultima aparicion de cada producto, fiador y agente, en orden original) y
    /// como se reparten las demas en PARTES (todo lo de un mismo cliente o una misma cuenta junto, parejo y determinista).
    /// Datos inventados, sin forma de numero de tarjeta.
    /// </summary>
    public class ParticionCargaTests
    {
        private static ArchivoCsv Csv(params string[] filas)
        {
            var texto = "CIF,Cuenta,Producto,Fiador,Agente\r\n" + string.Join("\r\n", filas) + "\r\n";
            return ArchivoCsv.Leer(new UTF8Encoding(false).GetBytes(texto), ',');
        }

        private static ParticionCarga.Clave Clave(string tabla, int columna, string porDefecto = null)
        {
            var c = new ParticionCarga.Clave { Tabla = tabla };
            c.Componentes.Add(new ParticionCarga.Componente { Columnas = new[] { columna }, PorDefecto = porDefecto });
            return c;
        }

        private static readonly ParticionCarga.Clave[] ClavesPadres =
        {
            Clave("producto", 2), Clave("fiador", 3, "(sin fiador)"), Clave("agente", 4),
        };

        private static readonly ParticionCarga.Clave[] ClavesPartes = { Clave("cliente", 0), Clave("cuenta", 1) };

        [Fact]
        public void Padres_toma_la_ultima_aparicion_de_cada_registro_en_orden_original()
        {
            var csv = Csv(
                "Z-1,K-1,P1,F1,A1",   // 0
                "Z-2,K-2,P2,F1,A1",   // 1
                "Z-3,K-3,P1,F2,A2",   // 2  ultima de P1
                "Z-4,K-4,P2,,A1",     // 3  ultima de P2, de A1 y del fiador vacio
                "Z-5,K-5,P3,F1,A2");  // 4  ultima de P3, F1 y A2
            Assert.Equal(new[] { 2, 3, 4 }, ParticionCarga.Padres(csv, ClavesPadres));
        }

        [Fact]
        public void Padres_elige_de_mas_si_los_valores_difieren_solo_en_mayusculas()
        {
            // Dataverse podria considerarlos el mismo producto: con las dos filas, la ultima escritura sigue siendo la ultima.
            var csv = Csv("Z-1,K-1,pa,F,A", "Z-2,K-2,PA,F,A", "Z-3,K-3,pa,F,A");
            Assert.Equal(new[] { 1, 2 }, ParticionCarga.Padres(csv, new[] { Clave("producto", 2) }));
        }

        [Fact]
        public void Padres_con_la_misma_fila_ultima_de_varias_tablas_la_incluye_una_vez()
        {
            var csv = Csv("Z-1,K-1,P1,F1,A1", "Z-2,K-2,P1,F1,A1");
            Assert.Equal(new[] { 1 }, ParticionCarga.Padres(csv, ClavesPadres));
        }

        [Fact]
        public void Partes_deja_todas_las_filas_de_un_CIF_en_la_misma_parte()
        {
            var filas = Enumerable.Range(0, 60).Select(i => "Z-" + (i % 7) + ",K-" + i + ",P,F,A").ToArray();
            var csv = Csv(filas);
            var partes = ParticionCarga.Partes(csv, ClavesPartes, 3);
            Assert.Equal(3, partes.Count);
            for (var c = 0; c < 7; c++)
            {
                var donde = partes.Select((p, i) => new { p, i }).Where(x => x.p.Any(f => csv.Filas[f].Campos[0] == "Z-" + c)).ToList();
                Assert.Single(donde);
            }
        }

        [Fact]
        public void Partes_junta_dos_CIF_que_comparten_una_cuenta()
        {
            var csv = Csv("Z-1,K-1,P,F,A", "Z-2,K-2,P,F,A", "Z-3,K-1,P,F,A", "Z-4,K-4,P,F,A");
            var partes = ParticionCarga.Partes(csv, ClavesPartes, 4);
            Assert.Equal(3, partes.Count);
            Assert.Contains(partes, p => p.SequenceEqual(new[] { 0, 2 }));
        }

        [Fact]
        public void Partes_normaliza_de_mas_la_clave()
        {
            // "z-1", "Z 1" y "Z1" se tratan como el mismo cliente: juntar de mas es seguro.
            var csv = Csv("z-1,K-1,P,F,A", "Z 1,K-2,P,F,A", "Z1,K-3,P,F,A", "Z-9,K-9,P,F,A");
            var partes = ParticionCarga.Partes(csv, ClavesPartes, 4);
            Assert.Contains(partes, p => p.SequenceEqual(new[] { 0, 1, 2 }));
        }

        [Fact]
        public void Partes_sin_clave_no_junta_filas()
        {
            var csv = Csv(",,P,F,A", ",,P,F,A", ",,P,F,A");
            Assert.Equal(3, ParticionCarga.Partes(csv, ClavesPartes, 3).Count);
        }

        [Fact]
        public void Partes_usa_el_valor_por_defecto_cuando_la_fuente_viene_vacia()
        {
            var claves = new[] { Clave("telefono", 0, "Casa") };
            var csv = Csv(",K-1,P,F,A", "casa,K-2,P,F,A", "Otro,K-3,P,F,A");
            var partes = ParticionCarga.Partes(csv, claves, 3);
            Assert.Contains(partes, p => p.SequenceEqual(new[] { 0, 1 }));
        }

        [Fact]
        public void Partes_reparte_parejo_y_cubre_cada_fila_una_sola_vez_en_orden()
        {
            var filas = new List<string>();
            for (var i = 0; i < 300; i++) filas.Add("Z-" + (i % 37) + ",K-" + i + ",P,F,A");
            var csv = Csv(filas.ToArray());
            var partes = ParticionCarga.Partes(csv, ClavesPartes, 3);
            Assert.Equal(Enumerable.Range(0, 300), partes.SelectMany(p => p).OrderBy(x => x));
            Assert.All(partes, p => Assert.Equal(p.OrderBy(x => x), p));
            Assert.True(partes.Max(p => p.Count) - partes.Min(p => p.Count) <= 9, "desbalance mayor a un grupo");
        }

        [Fact]
        public void Partes_es_determinista()
        {
            var csv = Csv(Enumerable.Range(0, 80).Select(i => "Z-" + (i * 7 % 13) + ",K-" + i + ",P,F,A").ToArray());
            var a = ParticionCarga.Partes(csv, ClavesPartes, 4);
            var b = ParticionCarga.Partes(csv, ClavesPartes, 4);
            Assert.Equal(a.Select(p => string.Join(",", p)), b.Select(p => string.Join(",", p)));
        }

        [Fact]
        public void Partes_con_menos_grupos_que_partes_no_deja_partes_vacias()
        {
            var csv = Csv("Z-1,K-1,P,F,A", "Z-1,K-2,P,F,A");
            var partes = ParticionCarga.Partes(csv, ClavesPartes, 4);
            Assert.Single(partes);
            Assert.Throws<ArgumentOutOfRangeException>(() => ParticionCarga.Partes(csv, ClavesPartes, 0));
        }

        [Fact]
        public void Normalizar_saca_acentos_signos_y_mayusculas()
        {
            Assert.Equal("PENA12", ParticionCarga.Normalizar(" peña-1.2 "));
            Assert.Equal(string.Empty, ParticionCarga.Normalizar(null));
        }
    }
}
