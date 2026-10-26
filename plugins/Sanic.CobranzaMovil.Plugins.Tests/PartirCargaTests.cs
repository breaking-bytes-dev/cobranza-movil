using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Sanic.CobranzaMovil.Plugins;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Carga paralela: la Custom API PartirCarga convierte el job de la carga real en el de PADRES y crea N jobs de PARTES
    /// sin estado. Cubre permisos, validaciones del job y de las plantillas, la particion (por CIF y por cuenta), los
    /// archivos (encabezado igual, bytes originales), la atomicidad (rollback si falla a mitad) y que nunca traza valores.
    /// Datos inventados: ningun valor tiene 13 digitos seguidos ni forma de numero de tarjeta.
    /// </summary>
    public class PartirCargaTests
    {
        private const string Mensaje = "sanic_cm_PartirCarga";
        private const string Admin = "SR | CM | Admin";
        private const string Servicio = "SR | CM | Servicio - Cobranza";
        private const string BackOffice = "SR | CM | BackOffice";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string Job = "sanic_tbl_smlt_importjob";
        private const string Plantilla = "sanic_tbl_smlt_importtemplate";
        private const string MapeoTabla = "sanic_tbl_smlt_tablemapping";
        private const string MapeoColumna = "sanic_tbl_smlt_columnmapping";
        private const string NombreJob = "Carga 2026-10-22 08:00 · banco.csv · real";
        private const string Encabezado = "Cliente,CIF,NumeroCuenta,CodigoProducto,NombreFianza,DireccionFianza,Agente,TipoAsignacion";

        // Marcas de los valores inventados: no pueden aparecer en trazas ni mensajes.
        private static readonly string[] Marcas = { "ZQX-", "CTQ-", "PRQ", "FIQ", "AGQ", "Nombre Q" };

        private static readonly Guid Principal = Guid.NewGuid();
        private static readonly Guid Padres = Guid.NewGuid();
        private static readonly Guid PartesTpl = Guid.NewGuid();

        /// <summary>Fila i del archivo de prueba: 37 clientes, cuentas unicas salvo una compartida entre dos clientes.</summary>
        private static string Fila(int i)
        {
            var cif = "ZQX-" + (i % 37);
            var cuenta = i == 5 ? "CTQ-0" : "CTQ-" + i; // la fila 5 comparte la cuenta de la fila 0 (otro cliente)
            var fiador = i % 11 == 0 ? string.Empty : "FIQ" + (i % 4);
            return "\"Nombre Q, " + i + "\"," + cif + "," + cuenta + ",PRQ" + (i % 3) + "," + fiador + ",\"Dir, " + i + "\",AGQ" + (i % 2) + ",Diaria";
        }

        private static byte[] Archivo(int filas)
        {
            var sb = new StringBuilder(Encabezado).Append("\r\n");
            for (var i = 0; i < filas; i++) sb.Append(Fila(i)).Append("\r\n");
            return new UTF8Encoding(false).GetBytes(sb.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Plantilla principal (reducida) y sus copias

        private sealed class Tm
        {
            public string Tabla, Nombre, Dedup;
            public int Orden;
            public bool Actualizar = true;
            public List<object[]> Cols = new List<object[]>(); // destino, fuente, tipo, default, transform, lookup tabla, lookup clave
        }

        private static List<Tm> MapeosPrincipal()
        {
            const string Hmac = "HMAC(NumeroCuenta, \"cm-pan\")";
            return new List<Tm>
            {
                new Tm { Tabla = "sanic_cm_tbl_cliente", Nombre = "Cliente", Dedup = "sanic_cm_cl_cif", Orden = 1, Cols = {
                    new object[] { "sanic_cm_cl_cif", "CIF", 0, null, null, null, null },
                    new object[] { "sanic_cm_cl_nombre", "Cliente", 0, null, null, null, null } } },
                new Tm { Tabla = "sanic_cm_tbl_producto", Nombre = "Producto", Dedup = "sanic_cm_pr_codigo", Orden = 1, Cols = {
                    new object[] { "sanic_cm_pr_codigo", "CodigoProducto", 0, null, null, null, null } } },
                new Tm { Tabla = "sanic_cm_tbl_agente", Nombre = "Agente", Dedup = "sanic_cm_ag_nombre", Orden = 1, Actualizar = false, Cols = {
                    new object[] { "sanic_cm_ag_nombre", "Agente", 0, null, null, null, null } } },
                new Tm { Tabla = "sanic_cm_tbl_fiador", Nombre = "Fiador", Dedup = "sanic_cm_fi_nombre", Orden = 1, Cols = {
                    new object[] { "sanic_cm_fi_nombre", "NombreFianza", 0, "(sin fiador)", null, null, null },
                    new object[] { "sanic_cm_fi_direccion", "DireccionFianza", 0, null, null, null, null } } },
                new Tm { Tabla = "sanic_cm_tbl_direccion", Nombre = "Direccion (casa)", Dedup = "sanic_cm_dr_cif,sanic_cm_dr_tipo", Orden = 2, Cols = {
                    new object[] { "sanic_cm_dr_cif", "CIF", 0, null, null, null, null },
                    new object[] { "sanic_cm_dr_tipo", null, 0, "Casa", null, null, null } } },
                new Tm { Tabla = "sanic_cm_tbl_cuenta", Nombre = "Cuenta", Dedup = "sanic_cm_ct_clavepan", Orden = 3, Cols = {
                    new object[] { "sanic_cm_ct_clavepan", "NumeroCuenta", 0, null, Hmac, null, null },
                    new object[] { "sanic_cm_ct_productoid", "CodigoProducto", 5, null, null, "sanic_cm_tbl_producto", "sanic_cm_pr_codigo" },
                    new object[] { "sanic_cm_ct_fiador", "NombreFianza", 5, null, null, "sanic_cm_tbl_fiador", "sanic_cm_fi_nombre" } } },
                new Tm { Tabla = "sanic_cm_tbl_asignacion", Nombre = "Asignacion", Dedup = "sanic_cm_as_clavepan,sanic_cm_as_tipo", Orden = 5, Cols = {
                    new object[] { "sanic_cm_as_clavepan", "NumeroCuenta", 0, null, Hmac, null, null },
                    new object[] { "sanic_cm_as_tipo", "TipoAsignacion", 6, null, null, null, null },
                    new object[] { "sanic_cm_as_agenteid", "Agente", 5, null, null, "sanic_cm_tbl_agente", "sanic_cm_ag_nombre" } } },
            };
        }

        private static bool EsPadre(string tabla)
        {
            return tabla == "sanic_cm_tbl_producto" || tabla == "sanic_cm_tbl_fiador" || tabla == "sanic_cm_tbl_agente";
        }

        private sealed class Escenario
        {
            public readonly Harness H = new Harness(Mensaje, "none", 30);
            public readonly Mock<IOrganizationService> Sistema = new Mock<IOrganizationService>();
            public readonly Guid JobId = Guid.NewGuid();
            public byte[] Original;
            public Entity JobFila;
            public bool SinArchivo;
            public bool PadresVisible = true;
            public int? EstadoPadres;
            public List<Tm> Principal_ = MapeosPrincipal();
            public Func<List<Tm>, List<Tm>> AjustarPartes = x => x;
            public int FallaUploadEnParte; // 1-based: falla el UploadBlock del archivo de esa parte
            public bool FallaUpdate;
            public readonly List<Entity> Creados = new List<Entity>();
            public readonly List<Guid> IdsCreados = new List<Guid>();
            public readonly List<Entity> Updates = new List<Entity>();
            public readonly List<Guid> Borrados = new List<Guid>();
            public readonly List<string> Orden = new List<string>();
            /// <summary>MaxLength de las columnas de texto (metadata de Dataverse), por tabla; lookups y opciones no tienen largo.</summary>
            public readonly Dictionary<string, Dictionary<string, int>> Largos = LargosPorDefecto();
            public readonly List<string> TablasConsultadas = new List<string>();
            /// <summary>Ultimo archivo confirmado por job (nombre y bytes).</summary>
            public readonly Dictionary<Guid, KeyValuePair<string, byte[]>> Archivos = new Dictionary<Guid, KeyValuePair<string, byte[]>>();
            private readonly Dictionary<string, Guid> destinoDelToken = new Dictionary<string, Guid>();
            private readonly Dictionary<string, List<byte[]>> bloques = new Dictionary<string, List<byte[]>>();

            public Escenario(params string[] roles)
            {
                Original = Archivo(400);
                JobFila = new Entity(Job, JobId);
                JobFila["sanic_smlt_ij_name"] = NombreJob;
                JobFila["sanic_smlt_ij_importtemplateid"] = new EntityReference(Plantilla, Principal);
                JobFila["sanic_smlt_ij_isdryrun"] = false;

                H.Factory.Setup(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null))).Returns(Sistema.Object);
                var filasRoles = new EntityCollection();
                foreach (var n in roles)
                {
                    var r = new Entity("role", Guid.NewGuid());
                    r["name"] = n;
                    filasRoles.Entities.Add(r);
                }
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == "role"))).Returns(filasRoles);
                Sistema.Setup(s => s.Delete(Job, It.IsAny<Guid>())).Callback<string, Guid>((t, id) => { Borrados.Add(id); Orden.Add("borrar"); });

                H.Service.Setup(s => s.Retrieve(Job, JobId, It.IsAny<ColumnSet>())).Returns(() => JobFila);
                H.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Plantilla))).Returns(() => Plantillas());
                H.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == MapeoTabla))).Returns(() => MapeosDeTabla());
                H.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == MapeoColumna))).Returns(() => MapeosDeColumna());
                H.Service.Setup(s => s.Create(It.IsAny<Entity>())).Returns<Entity>(e =>
                {
                    var id = Guid.NewGuid();
                    Creados.Add(e);
                    IdsCreados.Add(id);
                    Orden.Add("crear");
                    return id;
                });
                H.Service.Setup(s => s.Update(It.IsAny<Entity>())).Callback<Entity>(e =>
                {
                    Orden.Add("update");
                    if (FallaUpdate) throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -2147220891 }, "fallo");
                    Updates.Add(e);
                });
                H.Service.Setup(s => s.Execute(It.IsAny<OrganizationRequest>())).Returns<OrganizationRequest>(Ejecutar);
            }

            private OrganizationResponse Ejecutar(OrganizationRequest r)
            {
                var resp = new OrganizationResponse();
                switch (r.RequestName)
                {
                    case "InitializeFileBlocksDownload":
                        if (SinArchivo) throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault(), "sin archivo");
                        resp.Results["FileContinuationToken"] = "bajar";
                        resp.Results["FileSizeInBytes"] = (long)Original.Length;
                        resp.Results["FileName"] = "banco.csv";
                        break;
                    case "DownloadBlock":
                        var desde = (long)r["Offset"];
                        var largo = (int)Math.Min((long)r["BlockLength"], Original.Length - desde);
                        var data = new byte[largo];
                        Buffer.BlockCopy(Original, (int)desde, data, 0, largo);
                        resp.Results["Data"] = data;
                        break;
                    case "RetrieveEntity":
                        var tabla = (string)r["LogicalName"];
                        TablasConsultadas.Add(tabla);
                        resp.Results["EntityMetadata"] = Metadata(tabla);
                        break;
                    case "InitializeFileBlocksUpload":
                        var token = "t" + destinoDelToken.Count;
                        destinoDelToken[token] = ((EntityReference)r["Target"]).Id;
                        bloques[token] = new List<byte[]>();
                        resp.Results["FileContinuationToken"] = token;
                        break;
                    case "UploadBlock":
                        var t = (string)r["FileContinuationToken"];
                        var destino = destinoDelToken[t];
                        var parte = IdsCreados.IndexOf(destino) + 1;
                        if (FallaUploadEnParte > 0 && parte == FallaUploadEnParte)
                        {
                            throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -2147220970 }, "fallo");
                        }
                        bloques[t].Add((byte[])r["BlockData"]);
                        break;
                    case "CommitFileBlocksUpload":
                        var tk = (string)r["FileContinuationToken"];
                        Archivos[destinoDelToken[tk]] = new KeyValuePair<string, byte[]>((string)r["FileName"], bloques[tk].SelectMany(b => b).ToArray());
                        Orden.Add("archivo " + (destinoDelToken[tk] == JobId ? "original" : "parte"));
                        break;
                }
                return resp;
            }

            private static Dictionary<string, Dictionary<string, int>> LargosPorDefecto()
            {
                var res = new Dictionary<string, Dictionary<string, int>>();
                foreach (var m in MapeosPrincipal())
                {
                    res[m.Tabla] = m.Cols.Where(c => (int)c[2] == 0).ToDictionary(c => (string)c[0], c => 100);
                }
                return res;
            }

            private EntityMetadata Metadata(string tabla)
            {
                var atributos = new List<AttributeMetadata>();
                Dictionary<string, int> largos;
                if (Largos.TryGetValue(tabla, out largos))
                {
                    atributos.AddRange(largos.Select(l => new StringAttributeMetadata { LogicalName = l.Key, MaxLength = l.Value }));
                }
                foreach (var c in MapeosPrincipal().Where(m => m.Tabla == tabla).SelectMany(m => m.Cols).Where(c => (int)c[2] != 0))
                {
                    atributos.Add((int)c[2] == 5 ? (AttributeMetadata)new LookupAttributeMetadata { LogicalName = (string)c[0] }
                        : new PicklistAttributeMetadata { LogicalName = (string)c[0] });
                }
                var em = new EntityMetadata { LogicalName = tabla };
                typeof(EntityMetadata).GetProperty("Attributes").SetValue(em, atributos.ToArray()); // setter interno del SDK
                return em;
            }

            private EntityCollection Plantillas()
            {
                var res = new EntityCollection();
                res.Entities.Add(PlantillaFila(Principal, "Cobranza movil", null));
                if (PadresVisible) res.Entities.Add(PlantillaFila(Padres, "Cobranza movil · padres", EstadoPadres));
                res.Entities.Add(PlantillaFila(PartesTpl, "Cobranza movil · partes", null));
                return res;
            }

            private static Entity PlantillaFila(Guid id, string nombre, int? estado)
            {
                var e = new Entity(Plantilla, id);
                e["sanic_smlt_it_name"] = nombre;
                e["sanic_smlt_it_delimiter"] = ",";
                e["sanic_smlt_it_hasheaderrow"] = true;
                e["sanic_smlt_it_chunksize"] = 50;
                e["sanic_smlt_it_status"] = new OptionSetValue(estado ?? 1);
                e["sanic_smlt_it_sourcetype"] = new OptionSetValue(0);
                return e;
            }

            private IEnumerable<KeyValuePair<Guid, Tm>> Todas()
            {
                foreach (var m in Principal_) yield return new KeyValuePair<Guid, Tm>(Principal, m);
                foreach (var m in Principal_.Where(x => EsPadre(x.Tabla))) yield return new KeyValuePair<Guid, Tm>(Padres, m);
                foreach (var m in AjustarPartes(Principal_.Where(x => !EsPadre(x.Tabla)).ToList())) yield return new KeyValuePair<Guid, Tm>(PartesTpl, m);
            }

            private List<KeyValuePair<Entity, Tm>> cache;

            private List<KeyValuePair<Entity, Tm>> Filas()
            {
                if (cache != null) return cache;
                cache = new List<KeyValuePair<Entity, Tm>>();
                foreach (var par in Todas())
                {
                    var e = new Entity(MapeoTabla, Guid.NewGuid());
                    e["sanic_smlt_tm_importtemplateid"] = new EntityReference(Plantilla, par.Key);
                    e["sanic_smlt_tm_name"] = par.Value.Nombre;
                    e["sanic_smlt_tm_targettable"] = par.Value.Tabla;
                    e["sanic_smlt_tm_dedupcolumns"] = par.Value.Dedup;
                    e["sanic_smlt_tm_processingorder"] = par.Value.Orden;
                    e["sanic_smlt_tm_createifnotexists"] = true;
                    e["sanic_smlt_tm_updateifexists"] = par.Value.Actualizar;
                    e["sanic_smlt_tm_isjunctiontable"] = false;
                    cache.Add(new KeyValuePair<Entity, Tm>(e, par.Value));
                }
                return cache;
            }

            private EntityCollection MapeosDeTabla()
            {
                return new EntityCollection(Filas().Select(p => p.Key).ToList());
            }

            private EntityCollection MapeosDeColumna()
            {
                var res = new EntityCollection();
                foreach (var p in Filas())
                {
                    foreach (var c in p.Value.Cols)
                    {
                        var e = new Entity(MapeoColumna, Guid.NewGuid());
                        e["sanic_smlt_cm_tablemappingid"] = p.Key.ToEntityReference();
                        e["sanic_smlt_cm_name"] = (string)c[0];
                        e["sanic_smlt_cm_targetcolumn"] = (string)c[0];
                        e["sanic_smlt_cm_sourcecolumn"] = (string)c[1];
                        e["sanic_smlt_cm_datatype"] = new OptionSetValue((int)c[2]);
                        e["sanic_smlt_cm_isrequired"] = false;
                        e["sanic_smlt_cm_defaultvalue"] = (string)c[3];
                        e["sanic_smlt_cm_transformexpression"] = (string)c[4];
                        e["sanic_smlt_cm_lookuptable"] = (string)c[5];
                        e["sanic_smlt_cm_lookupmatchcolumn"] = (string)c[6];
                        res.Entities.Add(e);
                    }
                }
                return res;
            }

            public void Ejecutar()
            {
                H.Ejecutar(new PartirCarga());
            }

            public InvalidPluginExecutionException Rechaza()
            {
                var ex = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar());
                SinValores(ex.Message);
                SinValores(H.TodoElTexto());
                return ex;
            }

            public void NoEscribioNada()
            {
                Assert.Empty(Creados);
                Assert.Empty(Updates);
                Assert.Empty(Archivos);
            }

            public static void SinValores(string texto)
            {
                foreach (var m in Marcas) Assert.DoesNotContain(m, texto);
            }

            /// <summary>Filas de datos (texto) de un archivo armado; verifica que el encabezado es el original.</summary>
            public static List<string> FilasDe(byte[] archivo)
            {
                var lineas = Encoding.UTF8.GetString(archivo).Split(new[] { "\r\n" }, StringSplitOptions.None).ToList();
                Assert.Equal(Encabezado, lineas[0]);
                Assert.Equal(string.Empty, lineas[lineas.Count - 1]);
                return lineas.Skip(1).Take(lineas.Count - 2).ToList();
            }
        }

        private static string Tabla(QueryBase q)
        {
            var qe = q as QueryExpression;
            return qe == null ? null : qe.EntityName;
        }

        private static void Entradas(Escenario e, int partes)
        {
            e.H.Inputs["JobId"] = e.JobId;
            e.H.Inputs["Partes"] = partes;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Camino feliz

        [Fact]
        public void Parte_en_padres_y_tres_partes_sin_estado()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Ejecutar();

            // Tres partes nuevas, sin estado, de la plantilla de partes, carga real, con nombre numerado.
            Assert.Equal(3, e.Creados.Count);
            for (var i = 0; i < 3; i++)
            {
                var p = e.Creados[i];
                Assert.Equal(Job, p.LogicalName);
                Assert.Equal(NombreJob + " · parte " + (i + 1) + "/3", p["sanic_smlt_ij_name"]);
                Assert.Equal(PartesTpl, ((EntityReference)p["sanic_smlt_ij_importtemplateid"]).Id);
                Assert.False((bool)p["sanic_smlt_ij_isdryrun"]);
                Assert.False(p.Contains("sanic_smlt_ij_status"));
                Assert.Equal("banco-parte-" + (i + 1) + "-de-3.csv", e.Archivos[e.IdsCreados[i]].Key);
            }

            // El job original pasa a padres: nombre, plantilla y archivo; el estado no se toca.
            var u = Assert.Single(e.Updates);
            Assert.Equal(e.JobId, u.Id);
            Assert.Equal(NombreJob + " · padres", u["sanic_smlt_ij_name"]);
            Assert.Equal(Padres, ((EntityReference)u["sanic_smlt_ij_importtemplateid"]).Id);
            Assert.False(u.Contains("sanic_smlt_ij_status"));
            Assert.Equal("banco-padres.csv", e.Archivos[e.JobId].Key);

            // Orden: primero las partes, despues el archivo del original y al final su nombre y plantilla.
            Assert.Equal(new[] { "crear", "archivo parte", "crear", "archivo parte", "crear", "archivo parte", "archivo original", "update" }, e.Orden);

            // Salidas: ids y conteos.
            var o = e.H.Outputs;
            Assert.Equal(e.JobId, o["PadresJobId"]);
            Assert.Equal(string.Join(",", e.IdsCreados.Select(g => g.ToString("D"))), o["PartesJobIds"]);
            Assert.Equal(3, o["Partes"]);
            Assert.Equal(400, o["FilasArchivo"]);
            var porParte = ((string)o["FilasPartes"]).Split(',').Select(int.Parse).ToList();
            Assert.Equal(400, porParte.Sum());
            Assert.Equal(e.IdsCreados.Select(id => Escenario.FilasDe(e.Archivos[id].Value).Count), porParte);
            Assert.Equal(Escenario.FilasDe(e.Archivos[e.JobId].Value).Count, o["FilasPadres"]);
            Escenario.SinValores(e.H.TodoElTexto());
        }

        [Fact]
        public void Las_partes_cubren_todas_las_filas_y_cada_cliente_y_cuenta_queda_en_una_sola()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Ejecutar();
            var partes = e.IdsCreados.Select(id => Escenario.FilasDe(e.Archivos[id].Value)).ToList();
            var todas = Enumerable.Range(0, 400).Select(Fila).ToList();
            Assert.Equal(todas.OrderBy(x => x, StringComparer.Ordinal), partes.SelectMany(p => p).OrderBy(x => x, StringComparer.Ordinal));
            foreach (var p in partes)
            {
                // Orden original dentro de cada parte.
                Assert.Equal(p.OrderBy(f => todas.IndexOf(f)), p);
            }
            for (var c = 0; c < 37; c++)
            {
                Assert.Equal(1, partes.Count(p => p.Any(f => f.Contains(",ZQX-" + c + ","))));
            }
            // La fila 5 comparte la cuenta de la fila 0 aunque es de otro cliente: misma parte.
            Assert.Single(partes, p => p.Contains(Fila(0)) && p.Contains(Fila(5)));
            Assert.True(partes.Max(p => p.Count) - partes.Min(p => p.Count) <= 30);
        }

        [Fact]
        public void Padres_lleva_la_ultima_fila_de_cada_producto_fiador_y_agente_en_orden_original()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Ejecutar();
            var padres = Escenario.FilasDe(e.Archivos[e.JobId].Value);
            var todas = Enumerable.Range(0, 400).Select(Fila).ToList();
            Assert.Equal(padres.OrderBy(f => todas.IndexOf(f)), padres);

            // Esperado calculado aparte: por columna, el indice de la ultima aparicion.
            Func<int, string> producto = i => "PRQ" + (i % 3);
            Func<int, string> fiador = i => i % 11 == 0 ? string.Empty : "FIQ" + (i % 4);
            Func<int, string> agente = i => "AGQ" + (i % 2);
            var esperadas = new SortedSet<int>();
            foreach (var f in new[] { producto, fiador, agente })
            {
                foreach (var g in Enumerable.Range(0, 400).GroupBy(f)) esperadas.Add(g.Max());
            }
            Assert.Equal(esperadas.Select(Fila), padres);
        }

        [Fact]
        public void Con_pocas_filas_hay_una_sola_parte()
        {
            var e = new Escenario(Servicio);
            e.Original = Archivo(PartirCarga.MinimoFilasParaPartir - 1);
            Entradas(e, 4);
            e.Ejecutar();
            Assert.Single(e.Creados);
            Assert.Equal(NombreJob + " · parte 1/1", e.Creados[0]["sanic_smlt_ij_name"]);
            Assert.Equal(1, e.H.Outputs["Partes"]);
        }

        [Fact]
        public void Sin_roles_de_CM_no_se_frena()
        {
            var e = new Escenario();
            Entradas(e, 2);
            e.Ejecutar();
            Assert.Equal(2, e.Creados.Count);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Permisos y validaciones: nada se escribe

        [Fact]
        public void Falla_si_se_registra_en_otro_mensaje()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.H.Context.SetupGet(c => c.MessageName).Returns("Create");
            Assert.Contains("registro incorrecto", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Theory]
        [InlineData(BackOffice)]
        [InlineData(Supervisor)]
        public void Solo_el_Admin_parte_una_carga(string rol)
        {
            var e = new Escenario(rol);
            Entradas(e, 3);
            Assert.Contains("solo un Admin", e.Rechaza().Message);
            e.NoEscribioNada();
            e.H.Service.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ColumnSet>()), Times.Never());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void Partes_entre_1_y_4(int partes)
        {
            var e = new Escenario(Admin);
            Entradas(e, partes);
            Assert.Contains("entre 1 y 4", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Exige_el_job()
        {
            var e = new Escenario(Admin);
            e.H.Inputs["Partes"] = 3;
            Assert.Contains("JobId", e.Rechaza().Message);
        }

        [Fact]
        public void Un_job_que_no_se_ve_se_rechaza()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.H.Service.Setup(s => s.Retrieve(Job, e.JobId, It.IsAny<ColumnSet>()))
                .Throws(new FaultException<OrganizationServiceFault>(new OrganizationServiceFault(), "no existe"));
            Assert.Contains("no existe la importacion", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Una_prueba_en_seco_no_se_parte()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.JobFila["sanic_smlt_ij_isdryrun"] = true;
            Assert.Contains("prueba en seco", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void Un_job_con_estado_no_se_parte(int estado)
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.JobFila["sanic_smlt_ij_status"] = new OptionSetValue(estado);
            Assert.Contains("ya tiene estado", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Un_job_de_otra_plantilla_no_se_parte()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.JobFila["sanic_smlt_ij_importtemplateid"] = new EntityReference(Plantilla, Padres);
            Assert.Contains("no es de la plantilla", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Sin_la_plantilla_de_padres_pide_copiarlas_de_la_principal_en_Smelter()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.PadresVisible = false;
            var m = e.Rechaza().Message;
            Assert.Contains("faltan las plantillas", m);
            Assert.Contains("copiandolas de \"Cobranza movil\" en Smelter", m);
            Assert.DoesNotContain("scripts", m);
            e.NoEscribioNada();
        }

        [Fact]
        public void Una_plantilla_derivada_inactiva_se_rechaza()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.EstadoPadres = 2;
            Assert.Contains("no esta activa", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Una_plantilla_de_partes_desincronizada_se_rechaza()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            // Alguien agrego una columna a la principal y no regenero las derivadas: a la copia le falta.
            e.AjustarPartes = l => l.Select(m => m.Tabla != "sanic_cm_tbl_cuenta" ? m : new Tm
            {
                Tabla = m.Tabla, Nombre = m.Nombre, Dedup = m.Dedup, Orden = m.Orden, Cols = m.Cols.Take(m.Cols.Count - 1).ToList(),
            }).ToList();
            var msg = e.Rechaza().Message;
            Assert.Contains("no coincide", msg);
            Assert.Contains("resincronizar a mano", msg);
            Assert.Contains("exportar la plantilla principal e importarla", msg);
            Assert.DoesNotContain("scripts", msg);
            e.NoEscribioNada();
        }

        [Fact]
        public void Sin_la_columna_del_CIF_en_el_encabezado_se_rechaza()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Original = new UTF8Encoding(false).GetBytes(Encabezado.Replace("CIF", "Documento") + "\r\n" + Fila(1) + "\r\n");
            Assert.Contains("columna CIF", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Una_transformacion_insegura_en_la_clave_de_partes_se_rechaza()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            // RIGHT(...) juntaria dos cuentas distintas en un mismo registro: no se puede partir por la cuenta.
            var cuenta = e.Principal_.Single(m => m.Tabla == "sanic_cm_tbl_cuenta");
            cuenta.Cols[0][4] = "RIGHT(NumeroCuenta, 4)";
            Assert.Contains("transformacion", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Sin_archivo_se_rechaza()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.SinArchivo = true;
            Assert.Contains("no tiene archivo", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        [Fact]
        public void Un_archivo_con_comillas_sin_cerrar_se_rechaza_sin_citar_valores()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Original = new UTF8Encoding(false).GetBytes(Encabezado + "\r\n" + Fila(1) + "\r\n\"Nombre Q abierta,ZQX-1\r\n");
            Assert.Contains("comillas sin cerrar", e.Rechaza().Message);
            e.NoEscribioNada();
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Largo de los valores: la prueba en seco de la carga no lo detecta y un valor mas largo que la columna
        // rompe los padres de la carga real

        private static byte[] ArchivoDe(params string[] filas)
        {
            return new UTF8Encoding(false).GetBytes(Encabezado + "\r\n" + string.Join("\r\n", filas) + "\r\n");
        }

        [Fact]
        public void Un_valor_mas_largo_que_su_columna_se_rechaza_sin_crear_nada_ni_citar_el_valor()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Original = ArchivoDe(Fila(0), Fila(1), Fila(100)); // "Dir, 100": 8 caracteres
            e.Largos["sanic_cm_tbl_fiador"]["sanic_cm_fi_direccion"] = 7;
            var m = e.Rechaza().Message;
            Assert.Contains("hay 1 valor(es) mas largos", m);
            Assert.Contains("fila 3, columna DireccionFianza (sanic_cm_tbl_fiador.sanic_cm_fi_direccion): 8 caracteres, maximo 7", m);
            Assert.DoesNotContain("Dir, 100", m);
            e.NoEscribioNada();
        }

        [Fact]
        public void Cita_hasta_cinco_casos_y_cuenta_el_resto()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Largos["sanic_cm_tbl_producto"]["sanic_cm_pr_codigo"] = 3; // "PRQn": 4 en las 400 filas
            var m = e.Rechaza().Message;
            Assert.Contains("hay 400 valor(es)", m);
            Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(m, "fila \\d+, columna").Count);
            Assert.Contains("y 395 mas", m);
            e.NoEscribioNada();
        }

        [Fact]
        public void El_largo_se_mide_en_caracteres_como_lo_lee_Smelter_no_en_bytes()
        {
            var e = new Escenario(Admin);
            Entradas(e, 1);
            e.Original = ArchivoDe(Fila(0), "\"Nombre Q, 1\",ZQX-1,CTQ-1,PRQ1,FIQ1,Dir ñ1,AGQ1,Diaria"); // "Dir ñ1": 6 caracteres, 7 bytes ("Dir, 0" tambien tiene 6)
            e.Largos["sanic_cm_tbl_fiador"]["sanic_cm_fi_direccion"] = 6;
            e.Ejecutar();
            Assert.Single(e.Creados);
        }

        [Fact]
        public void Una_columna_con_transformacion_no_se_mide()
        {
            // La clave HMAC del PAN: lo que se escribe no es el valor del archivo.
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Largos["sanic_cm_tbl_cuenta"]["sanic_cm_ct_clavepan"] = 2;
            e.Ejecutar();
            Assert.Equal(3, e.Creados.Count);
        }

        [Fact]
        public void Lee_la_metadata_una_vez_por_tabla()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.Ejecutar();
            Assert.Equal(MapeosPrincipal().Select(m => m.Tabla).Distinct().OrderBy(t => t, StringComparer.Ordinal),
                e.TablasConsultadas.OrderBy(t => t, StringComparer.Ordinal));
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Solo validar: la prueba en seco y la carga en serie no pasan por la particion, pero el largo se mide igual

        private static void SoloValidar(Escenario e)
        {
            e.H.Inputs["JobId"] = e.JobId;
            e.H.Inputs["SoloValidar"] = true;
        }

        [Fact]
        public void Solo_validar_un_archivo_sano_no_escribe_nada_y_devuelve_las_filas()
        {
            var e = new Escenario(Admin);
            SoloValidar(e);
            e.Ejecutar();
            e.NoEscribioNada();
            Assert.Equal(400, e.H.Outputs["FilasArchivo"]);
            Assert.Equal(0, e.H.Outputs["Partes"]);
            Assert.Equal(e.JobId, e.H.Outputs["PadresJobId"]);
            Assert.Equal(string.Empty, e.H.Outputs["PartesJobIds"]);
        }

        [Fact]
        public void Solo_validar_rechaza_un_valor_largo_con_el_mismo_mensaje_sin_escribir_nada()
        {
            var e = new Escenario(Admin);
            SoloValidar(e);
            e.Original = ArchivoDe(Fila(0), Fila(1), Fila(100));
            e.Largos["sanic_cm_tbl_fiador"]["sanic_cm_fi_direccion"] = 7;
            var m = e.Rechaza().Message;
            Assert.Contains("fila 3, columna DireccionFianza (sanic_cm_tbl_fiador.sanic_cm_fi_direccion): 8 caracteres, maximo 7", m);
            Assert.DoesNotContain("Dir, 100", m);
            e.NoEscribioNada();
        }

        [Fact]
        public void Solo_validar_acepta_la_prueba_en_seco()
        {
            var e = new Escenario(Admin);
            SoloValidar(e);
            e.JobFila["sanic_smlt_ij_isdryrun"] = true;
            e.Ejecutar();
            e.NoEscribioNada();
        }

        [Fact]
        public void Solo_validar_no_pide_Partes_ni_las_plantillas_derivadas()
        {
            // La carga en serie no las necesita: sin padres visibles y con la de partes desincronizada igual valida.
            var e = new Escenario(Admin);
            SoloValidar(e);
            e.PadresVisible = false;
            e.AjustarPartes = l => l.Take(1).ToList();
            e.Ejecutar();
            e.NoEscribioNada();
        }

        [Fact]
        public void Solo_validar_no_exige_la_columna_del_CIF()
        {
            var e = new Escenario(Admin);
            SoloValidar(e);
            e.Original = new UTF8Encoding(false).GetBytes(Encabezado.Replace("CIF", "Documento") + "\r\n" + Fila(1) + "\r\n");
            e.Ejecutar();
            e.NoEscribioNada();
        }

        [Fact]
        public void Solo_validar_sigue_exigiendo_la_plantilla_principal_y_un_job_sin_estado()
        {
            var otra = new Escenario(Admin);
            SoloValidar(otra);
            otra.JobFila["sanic_smlt_ij_importtemplateid"] = new EntityReference(Plantilla, Padres);
            Assert.Contains("no es de la plantilla", otra.Rechaza().Message);

            var conEstado = new Escenario(Admin);
            SoloValidar(conEstado);
            conEstado.JobFila["sanic_smlt_ij_status"] = new OptionSetValue(0);
            Assert.Contains("ya tiene estado", conEstado.Rechaza().Message);
        }

        [Theory]
        [InlineData(BackOffice)]
        [InlineData(Supervisor)]
        public void Solo_validar_tambien_es_solo_del_Admin(string rol)
        {
            var e = new Escenario(rol);
            SoloValidar(e);
            Assert.Contains("solo un Admin", e.Rechaza().Message);
        }

        [Fact]
        public void SoloValidar_en_false_parte_como_siempre()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.H.Inputs["SoloValidar"] = false;
            e.Ejecutar();
            Assert.Equal(3, e.Creados.Count);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Atomicidad

        [Fact]
        public void Si_falla_una_parte_borra_las_creadas_y_el_original_queda_igual()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.FallaUploadEnParte = 2;
            Assert.Contains("no quedo nada a medias", e.Rechaza().Message);
            Assert.Equal(e.IdsCreados.Take(2), e.Borrados);
            Assert.Empty(e.Updates);
            Assert.False(e.Archivos.ContainsKey(e.JobId));
            e.H.Service.Verify(s => s.Delete(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never());
        }

        [Fact]
        public void Si_falla_el_cambio_del_original_devuelve_su_archivo_y_borra_las_partes()
        {
            var e = new Escenario(Admin);
            Entradas(e, 3);
            e.FallaUpdate = true;
            Assert.Contains("no quedo nada a medias", e.Rechaza().Message);
            Assert.Equal(e.IdsCreados, e.Borrados);
            Assert.Equal("banco.csv", e.Archivos[e.JobId].Key);
            Assert.Equal(e.Original, e.Archivos[e.JobId].Value);
        }
    }
}
