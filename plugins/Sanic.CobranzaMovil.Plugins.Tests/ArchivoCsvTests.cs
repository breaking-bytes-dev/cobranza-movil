using System;
using System.Linq;
using System.Text;
using Sanic.CobranzaMovil.Plugins;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Carga paralela: el CSV se parte a nivel de bytes y cada registro se copia tal cual. Cubre comillas (con comas, comillas
    /// dobles y saltos de linea adentro), CRLF/LF/CR, BOM, lineas vacias y los casos que no se pueden partir con seguridad.
    /// Datos inventados: ningun valor tiene forma de numero de tarjeta.
    /// </summary>
    public class ArchivoCsvTests
    {
        private static byte[] U(string s)
        {
            return new UTF8Encoding(false).GetBytes(s);
        }

        [Fact]
        public void Lee_encabezado_y_filas_con_comillas_comas_y_comillas_dobles()
        {
            var csv = ArchivoCsv.Leer(U("Nombre,CIF,Nota\r\n\"Perez, Ana\",X-1,\"dijo \"\"hola\"\"\"\r\nLuis,X-2,simple\r\n"), ',');
            Assert.Equal(new[] { "Nombre", "CIF", "Nota" }, csv.Columnas.ToArray());
            Assert.Equal(2, csv.Filas.Count);
            Assert.Equal("Perez, Ana", csv.Filas[0].Campos[0]);
            Assert.Equal("dijo \"hola\"", csv.Filas[0].Campos[2]);
            Assert.Equal("X-2", ArchivoCsv.Valor(csv.Filas[1], 1));
        }

        [Fact]
        public void Un_salto_de_linea_entre_comillas_no_corta_el_registro()
        {
            var csv = ArchivoCsv.Leer(U("A,B\nuno,\"linea 1\nlinea 2\"\ndos,x\n"), ',');
            Assert.Equal(2, csv.Filas.Count);
            Assert.Equal("linea 1\nlinea 2", csv.Filas[0].Campos[1]);
        }

        [Fact]
        public void Acepta_CRLF_LF_y_CR_y_saltea_lineas_vacias()
        {
            var csv = ArchivoCsv.Leer(U("A,B\r\n1,a\n\n2,b\r3,c\r\n\r\n"), ',');
            Assert.Equal(new[] { "1", "2", "3" }, csv.Filas.Select(f => f.Campos[0]).ToArray());
        }

        [Fact]
        public void Recorta_espacios_como_Smelter_y_busca_columnas_sin_distinguir_mayusculas()
        {
            var csv = ArchivoCsv.Leer(U(" Cif , Otra \r\n  X-9  , \" y \" \r\n"), ',');
            Assert.Equal(0, csv.Indice("CIF"));
            Assert.Equal(1, csv.Indice("otra"));
            Assert.Equal(-1, csv.Indice("Falta"));
            Assert.Equal("X-9", csv.Filas[0].Campos[0]);
            Assert.Equal("y", csv.Filas[0].Campos[1]);
        }

        [Fact]
        public void Armar_copia_los_bytes_originales_con_el_encabezado_y_el_BOM()
        {
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var cuerpo = U("Nombre,Dir\r\n\"Peña, José\",\"calle \"\"8\"\"\nsur\"\r\nAna,b\r\n");
            var csv = ArchivoCsv.Leer(bom.Concat(cuerpo).ToArray(), ',');
            Assert.True(csv.TieneBom);
            var armado = csv.Armar(new[] { 1, 0 });
            var esperado = bom.Concat(U("Nombre,Dir\r\nAna,b\r\n\"Peña, José\",\"calle \"\"8\"\"\nsur\"\r\n")).ToArray();
            Assert.Equal(esperado, armado);
            Assert.Equal("Peña, José", ArchivoCsv.Utf8(csv.Filas[0].Campos[0]));
        }

        [Fact]
        public void Releer_lo_armado_da_las_mismas_filas()
        {
            var csv = ArchivoCsv.Leer(U("A,B\n\"x,1\",\"q\"\"r\"\ny,2\n"), ',');
            var otra = ArchivoCsv.Leer(csv.Armar(new[] { 0, 1 }), ',');
            Assert.Equal(csv.Filas.Select(f => string.Join("|", f.Campos)), otra.Filas.Select(f => string.Join("|", f.Campos)));
        }

        [Fact]
        public void Usa_el_delimitador_de_la_plantilla()
        {
            var csv = ArchivoCsv.Leer(U("A;B\n\"1;2\";z\n"), ';');
            Assert.Equal("1;2", csv.Filas[0].Campos[0]);
            Assert.Equal("z", csv.Filas[0].Campos[1]);
        }

        [Fact]
        public void Comillas_sin_cerrar_es_un_error_sin_valores()
        {
            var ex = Assert.Throws<FormatException>(() => ArchivoCsv.Leer(U("A,B\nVALOR-SECRETO,\"abierta\n"), ','));
            Assert.Contains("comillas sin cerrar", ex.Message);
            Assert.DoesNotContain("VALOR-SECRETO", ex.Message);
        }

        [Fact]
        public void Una_fila_con_mas_columnas_que_el_encabezado_es_un_error()
        {
            var ex = Assert.Throws<FormatException>(() => ArchivoCsv.Leer(U("A,B\n1,2\nVALOR-SECRETO,2,3\n"), ','));
            Assert.Contains("La fila 3 tiene 3 columnas", ex.Message);
            Assert.DoesNotContain("VALOR-SECRETO", ex.Message);
        }

        [Fact]
        public void Una_fila_con_menos_columnas_se_acepta()
        {
            var csv = ArchivoCsv.Leer(U("A,B,C\n1,2\n"), ',');
            Assert.Equal(string.Empty, ArchivoCsv.Valor(csv.Filas[0], 2));
        }

        [Fact]
        public void Rechaza_UTF16_y_el_archivo_vacio()
        {
            Assert.Contains("UTF-16", Assert.Throws<FormatException>(() => ArchivoCsv.Leer(new byte[] { 0xFF, 0xFE, 0x41, 0 }, ',')).Message);
            Assert.Contains("vacío", Assert.Throws<FormatException>(() => ArchivoCsv.Leer(new byte[0], ',')).Message);
            Assert.Throws<FormatException>(() => ArchivoCsv.Leer(U("A,B\n"), '"'));
        }

        [Fact]
        public void Un_archivo_con_solo_encabezado_no_tiene_filas()
        {
            Assert.Empty(ArchivoCsv.Leer(U("A,B\r\n"), ',').Filas);
        }
    }
}
