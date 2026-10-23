using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Carga paralela (decision del dueno, 2026-10-22): Custom API global "sanic_cm_PartirCarga". La carga real de 6.000 filas
    /// corria en UN Import Job de Smelter en serie (~100 min: lotes de 100 filas de ~100 s contra el limite de 120 s). El
    /// backoffice crea el job como siempre (plantilla "Cobranza movil", sin estado, con el archivo) y llama a esta API, que:
    ///  1) Convierte ESE job en el de PADRES: su archivo pasa a tener solo las filas necesarias para producto, fiador y agente
    ///     (la ULTIMA aparicion de cada uno, en orden original: mismo resultado que en serie), su plantilla pasa a
    ///     "Cobranza movil · padres" y su nombre termina en " · padres". Corre primero y solo.
    ///  2) Crea N jobs de PARTES (plantilla "Cobranza movil · partes", sin estado, no prueba en seco, nombre
    ///     "&lt;nombre&gt; · parte i/N") con TODAS las filas repartidas por cliente: las filas que escriben un mismo registro
    ///     (mismo CIF, misma cuenta, ...) van a la misma parte. Ver <see cref="ParticionCarga"/>.
    ///  3) Devuelve los ids y los conteos de filas (nunca valores).
    /// La app pone En cola el de padres y, SOLO si termina Completed, las partes (cada una 20 s despues de su archivo).
    ///
    /// Que mira de la plantilla principal (no hay columnas fijas en el codigo): el delimitador, las columnas de dedup de
    /// cada tabla y su columna fuente (la del CIF es la que mapea a sanic_cm_cl_cif). Exige que las plantillas de padres y
    /// partes sean copia exacta de la principal (las genera scripts/pan/paso20_plantillas_paralelas.py): si alguien edito
    /// la principal y no las regenero, falla sin tocar nada.
    ///
    /// Validaciones: Admin, Servicio o sin roles de CM (como AltaCuenta); el job existe y el llamador lo ve; es de la
    /// plantilla principal; no es prueba en seco; NUNCA se proceso (sin estado); tiene archivo; 1 &lt;= Partes &lt;= 4; ningun
    /// valor es mas largo que su columna de texto (1.0.9.8: la prueba en seco de Smelter no lo detecta).
    /// SoloValidar = true (1.0.9.9, opcional): solo esa ultima medicion, sin escribir nada, tambien para la prueba en seco
    /// y la carga en serie (ver Validar).
    /// Los jobs se crean y modifican con los permisos del LLAMADOR, como en "Cargar archivo": el rol de Smelter llega por el
    /// equipo, el job queda del equipo (AssignJobOwner de Smelter) y "Creado por" es la persona (el historial filtra por ahi).
    /// Atomicidad: primero se crean las partes, despues se reemplaza el archivo del job original y al final su nombre y
    /// plantilla. Si algo falla se borran las partes creadas (con el servicio del sistema: el rol Operador de Smelter no
    /// tiene Delete) y el job original vuelve a quedar como estaba; ademas la Custom API corre en la transaccion del
    /// pedido, que se revierte al lanzar el error.
    /// PCI: el archivo trae el PAN completo. Solo vive en memoria; nunca se traza ni se cita: solo nombres de columna y
    /// conteos.
    /// </summary>
    public sealed class PartirCarga : PluginBase
    {
        public const int MaximoPartes = 4;
        /// <summary>Con menos filas que esto, una sola parte: partir no ahorra nada y suma jobs.</summary>
        public const int MinimoFilasParaPartir = 300;
        /// <summary>El archivo vive en memoria del sandbox (un CSV de 6.000 filas pesa ~3 MB).</summary>
        public const long TamanoMaximo = 50L * 1024 * 1024;
        private const int Bloque = 4 * 1024 * 1024;
        private const int EstadoActivo = 1;
        /// <summary>Cuantos valores largos cita el error (el resto se cuenta).</summary>
        public const int MaximoCasosLargo = 5;

        private static readonly string[] TablasPadres = { Esquema.Producto, Esquema.Fiador, Esquema.Agente };
        private static readonly Regex Literal = new Regex("\"[^\"]*\"", RegexOptions.CultureInvariant);
        private static readonly Regex Identificador = new Regex("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant);

        protected override string Nombre
        {
            get { return "PartirCarga"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            if (!string.Equals(ctx.MessageName, Esquema.MensajePartirCarga, StringComparison.Ordinal)
                || ctx.Stage != Esquema.EtapaMainOperation)
            {
                throw local.Error("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajePartirCarga + ".");
            }

            var roles = RolesCm.DelLlamador(local);
            if (!roles.SinRestriccion)
            {
                throw local.Error("solo un Admin puede partir una carga.");
            }

            var inputs = ctx.InputParameters;
            if (!inputs.Contains(Esquema.ParamJobId) || !(inputs[Esquema.ParamJobId] is Guid) || (Guid)inputs[Esquema.ParamJobId] == Guid.Empty)
            {
                throw local.Error("falta el job de la carga (JobId).");
            }
            var jobId = (Guid)inputs[Esquema.ParamJobId];
            var soloValidar = inputs.Contains(Esquema.ParamSoloValidar) && inputs[Esquema.ParamSoloValidar] is bool && (bool)inputs[Esquema.ParamSoloValidar];
            var partes = 0;
            if (!soloValidar)
            {
                if (!inputs.Contains(Esquema.ParamPartes) || !(inputs[Esquema.ParamPartes] is int))
                {
                    throw local.Error("falta la cantidad de partes (Partes, entero).");
                }
                partes = (int)inputs[Esquema.ParamPartes];
                if (partes < 1 || partes > MaximoPartes)
                {
                    throw local.Error("Partes tiene que estar entre 1 y " + MaximoPartes + ".");
                }
            }

            var svc = local.Service;

            // 1) El job, con los permisos del llamador --------------------------------------------------------------------
            Entity job;
            try
            {
                job = svc.Retrieve(Esquema.JobSmelter, jobId, new ColumnSet(Esquema.JobSmelterNombre, Esquema.JobSmelterPlantilla,
                    Esquema.JobSmelterEsPrueba, Esquema.JobSmelterEstado));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                job = null;
            }
            if (job == null) throw local.Error("no existe la importacion o tu usuario no la ve (equipo de carga).");
            if (!soloValidar && job.GetAttributeValue<bool?>(Esquema.JobSmelterEsPrueba) == true)
            {
                throw local.Error("la importacion es una prueba en seco: solo se parte la carga real.");
            }
            if (job.GetAttributeValue<OptionSetValue>(Esquema.JobSmelterEstado) != null)
            {
                throw local.Error("la importacion ya tiene estado (se proceso o esta en cola): solo se parte un job sin estado.");
            }
            var plantillaJob = job.GetAttributeValue<EntityReference>(Esquema.JobSmelterPlantilla);
            var nombreJob = (job.GetAttributeValue<string>(Esquema.JobSmelterNombre) ?? string.Empty).Trim();

            // 2) Las tres plantillas (el llamador las ve si esta en el equipo) y sus mapeos -------------------------------
            var plantillas = Plantillas(svc);
            Entity principal, padresTpl, partesTpl;
            plantillas.TryGetValue(Esquema.NombrePlantillaCobranza, out principal);
            plantillas.TryGetValue(Esquema.NombrePlantillaPadres, out padresTpl);
            plantillas.TryGetValue(Esquema.NombrePlantillaPartes, out partesTpl);
            if (principal == null) throw local.Error("no se ve la plantilla \"" + Esquema.NombrePlantillaCobranza + "\": tu usuario no esta en el equipo de carga.");
            if (plantillaJob == null || plantillaJob.Id != principal.Id)
            {
                throw local.Error("la importacion no es de la plantilla \"" + Esquema.NombrePlantillaCobranza + "\".");
            }
            if (soloValidar)
            {
                Validar(local, svc, principal, jobId);
                return;
            }
            if (padresTpl == null || partesTpl == null)
            {
                throw local.Error("faltan las plantillas \"" + Esquema.NombrePlantillaPadres + "\" y \"" + Esquema.NombrePlantillaPartes
                    + "\" (o tu usuario no las ve): hay que correr scripts/pan/paso20_plantillas_paralelas.py --aplicar.");
            }
            var mapeos = Mapeos(svc, principal.Id, padresTpl.Id, partesTpl.Id);
            var deLaPrincipal = mapeos.Where(m => m.Plantilla == principal.Id).ToList();
            var dePadres = deLaPrincipal.Where(m => EsPadre(m.Tabla)).ToList();
            var dePartes = deLaPrincipal.Where(m => !EsPadre(m.Tabla)).ToList();
            foreach (var t in TablasPadres)
            {
                if (!dePadres.Any(m => m.Tabla == t)) throw local.Error("la plantilla principal no carga la tabla " + t + ".");
            }
            if (dePartes.Count == 0) throw local.Error("la plantilla principal no carga ninguna tabla aparte de producto, fiador y agente.");
            ExigirCopia(local, principal, padresTpl, dePadres, mapeos.Where(m => m.Plantilla == padresTpl.Id).ToList());
            ExigirCopia(local, principal, partesTpl, dePartes, mapeos.Where(m => m.Plantilla == partesTpl.Id).ToList());
            // 3) El archivo ------------------------------------------------------------------------------------------------
            string nombreArchivo;
            byte[] original;
            var csv = LeerArchivo(local, svc, principal, jobId, out nombreArchivo, out original);
            ExigirCif(local, csv, dePartes);
            ExigirLargos(local, svc, csv, deLaPrincipal);
            var clavesPadres = dePadres.Select(m => Clave(local, csv, m, true)).ToList();
            var clavesPartes = dePartes.Select(m => Clave(local, csv, m, false)).Where(c => c != null).ToList();

            // 4) La particion ----------------------------------------------------------------------------------------------
            var filasPadres = ParticionCarga.Padres(csv, clavesPadres);
            var pedidas = csv.Filas.Count < MinimoFilasParaPartir ? 1 : partes;
            var filasPartes = ParticionCarga.Partes(csv, clavesPartes, pedidas);
            local.Trace("{0} filas: padres {1}; {2} parte(s) de {3} filas (pedidas {4}). Columnas de clave: {5}.",
                csv.Filas.Count, filasPadres.Count, filasPartes.Count, string.Join("/", filasPartes.Select(p => p.Count)), partes,
                string.Join(", ", clavesPartes.Concat(clavesPadres).SelectMany(c => c.Componentes).SelectMany(c => c.Columnas)
                    .Distinct().OrderBy(i => i).Select(i => csv.Columnas[i])));

            // 5) Escritura, de lo que se puede deshacer a lo que no --------------------------------------------------------
            var raiz = Raiz(nombreArchivo);
            var creados = new List<Guid>();
            var archivoReemplazado = false;
            try
            {
                for (var i = 0; i < filasPartes.Count; i++)
                {
                    var parte = new Entity(Esquema.JobSmelter);
                    parte[Esquema.JobSmelterNombre] = nombreJob + " · parte " + (i + 1) + "/" + filasPartes.Count;
                    parte[Esquema.JobSmelterPlantilla] = partesTpl.ToEntityReference();
                    parte[Esquema.JobSmelterEsPrueba] = false;
                    var id = svc.Create(parte);
                    creados.Add(id);
                    Subir(svc, id, raiz + "-parte-" + (i + 1) + "-de-" + filasPartes.Count + ".csv", csv.Armar(filasPartes[i]));
                }
                Subir(svc, jobId, raiz + "-padres.csv", csv.Armar(filasPadres));
                archivoReemplazado = true;
                var cambio = new Entity(Esquema.JobSmelter, jobId);
                cambio[Esquema.JobSmelterNombre] = nombreJob + " · padres";
                cambio[Esquema.JobSmelterPlantilla] = padresTpl.ToEntityReference();
                svc.Update(cambio);
            }
            catch (Exception ex)
            {
                var falla = ex as FaultException<OrganizationServiceFault>;
                local.Trace("fallo la escritura ({0}, codigo {1}); se deshace.", ex.GetType().Name,
                    falla == null || falla.Detail == null ? 0 : falla.Detail.ErrorCode);
                Deshacer(local, creados, jobId, archivoReemplazado, nombreArchivo, original);
                throw local.Error("no se pudo partir la carga; no quedo nada a medias y la importacion original sigue igual. Volve a intentar.");
            }

            var salida = ctx.OutputParameters;
            salida[Esquema.SalidaPadresJobId] = jobId;
            salida[Esquema.SalidaPartesJobIds] = string.Join(",", creados.Select(g => g.ToString("D")));
            salida[Esquema.SalidaPartes] = creados.Count;
            salida[Esquema.SalidaFilasArchivo] = csv.Filas.Count;
            salida[Esquema.SalidaFilasPadres] = filasPadres.Count;
            salida[Esquema.SalidaFilasPartes] = string.Join(",", filasPartes.Select(p => p.Count.ToString(CultureInfo.InvariantCulture)));
            local.Trace("listo: job de padres y {0} parte(s) sin estado.", creados.Count);
        }

        /// <summary>
        /// Modo "solo validar" (1.0.9.9): la prueba en seco y la carga en serie no pasan por la particion, asi que Cargar
        /// archivo llama a la API con SoloValidar = true antes de poner el job en cola. Mide el largo de los valores con la
        /// misma regla que al partir (ExigirLargos sobre los mapeos de la principal) y NO escribe nada. No pide Partes, ni
        /// las plantillas derivadas, ni el CIF: nada de eso hace falta para cargar en un solo job. Si lanza, el job queda igual.
        /// </summary>
        private static void Validar(LocalContext local, IOrganizationService svc, Entity principal, Guid jobId)
        {
            var mapeos = Mapeos(svc, principal.Id).Where(m => m.Plantilla == principal.Id).ToList();
            string nombreArchivo;
            byte[] original;
            var csv = LeerArchivo(local, svc, principal, jobId, out nombreArchivo, out original);
            ExigirLargos(local, svc, csv, mapeos);

            var salida = local.Context.OutputParameters;
            salida[Esquema.SalidaPadresJobId] = jobId;
            salida[Esquema.SalidaPartesJobIds] = string.Empty;
            salida[Esquema.SalidaPartes] = 0;
            salida[Esquema.SalidaFilasArchivo] = csv.Filas.Count;
            salida[Esquema.SalidaFilasPadres] = 0;
            salida[Esquema.SalidaFilasPartes] = string.Empty;
            local.Trace("solo validar: {0} filas, largos dentro de su columna; no se escribio nada.", csv.Filas.Count);
        }

        /// <summary>Baja y lee el archivo del job con el delimitador de la plantilla principal (que tiene que tener encabezado).</summary>
        private static ArchivoCsv LeerArchivo(LocalContext local, IOrganizationService svc, Entity principal, Guid jobId,
            out string nombreArchivo, out byte[] original)
        {
            if (principal.GetAttributeValue<bool?>(Esquema.PlantillaSmelterEncabezado) != true)
            {
                throw local.Error("la plantilla principal no tiene fila de encabezado: no se puede leer el archivo por columnas.");
            }
            var delim = principal.GetAttributeValue<string>(Esquema.PlantillaSmelterDelimitador);
            if (string.IsNullOrEmpty(delim)) delim = ",";
            if (delim.Length != 1) throw local.Error("el delimitador de la plantilla tiene que ser un solo caracter.");

            original = Descargar(local, svc, jobId, out nombreArchivo);
            ArchivoCsv csv;
            try
            {
                csv = ArchivoCsv.Leer(original, delim[0]);
            }
            catch (FormatException ex)
            {
                throw local.Error(ex.Message); // los mensajes de ArchivoCsv no citan valores
            }
            if (csv.Filas.Count == 0) throw local.Error("el archivo no tiene filas de datos.");
            return csv;
        }

        private static bool EsPadre(string tabla)
        {
            return Array.IndexOf(TablasPadres, tabla) >= 0;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Plantillas y mapeos

        private sealed class Mapeo
        {
            public Guid Id;
            public Guid Plantilla;
            public string Tabla;
            public string Firma;
            public List<string> Dedup = new List<string>();
            public readonly List<Columna> Columnas = new List<Columna>();
        }

        private sealed class Columna
        {
            public string Destino;
            public string Fuente;
            public string Transformacion;
            public string PorDefecto;
            public string Firma;
        }

        private static Dictionary<string, Entity> Plantillas(IOrganizationService svc)
        {
            var q = new QueryExpression(Esquema.PlantillaSmelter)
            {
                ColumnSet = new ColumnSet(Esquema.PlantillaSmelterNombre, Esquema.PlantillaSmelterDelimitador, Esquema.PlantillaSmelterEncabezado,
                    Esquema.PlantillaSmelterLote, Esquema.PlantillaSmelterEstado, Esquema.PlantillaSmelterOrigen),
            };
            q.Criteria.AddCondition(Esquema.PlantillaSmelterNombre, ConditionOperator.In,
                Esquema.NombrePlantillaCobranza, Esquema.NombrePlantillaPadres, Esquema.NombrePlantillaPartes);
            var r = svc.RetrieveMultiple(q);
            var res = new Dictionary<string, Entity>(StringComparer.Ordinal);
            if (r == null) return res;
            foreach (var g in r.Entities.GroupBy(e => e.GetAttributeValue<string>(Esquema.PlantillaSmelterNombre)))
            {
                if (g.Key == null) continue;
                if (g.Count() > 1)
                {
                    throw new InvalidPluginExecutionException("PartirCarga: hay " + g.Count() + " plantillas llamadas \"" + g.Key + "\": deje una sola.");
                }
                res[g.Key] = g.First();
            }
            return res;
        }

        private static List<Mapeo> Mapeos(IOrganizationService svc, params Guid[] plantillas)
        {
            var qt = new QueryExpression(Esquema.MapeoTabla)
            {
                ColumnSet = new ColumnSet(Esquema.MapeoTablaPlantilla, Esquema.MapeoTablaNombre, Esquema.MapeoTablaDestino, Esquema.MapeoTablaDedup,
                    Esquema.MapeoTablaOrden, Esquema.MapeoTablaCrear, Esquema.MapeoTablaActualizar, Esquema.MapeoTablaIntermedia),
            };
            qt.Criteria.AddCondition(Esquema.MapeoTablaPlantilla, ConditionOperator.In, plantillas.Cast<object>().ToArray());
            var mapeos = new Dictionary<Guid, Mapeo>();
            foreach (var e in Todas(svc, qt))
            {
                var dedup = (e.GetAttributeValue<string>(Esquema.MapeoTablaDedup) ?? string.Empty)
                    .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                var plantilla = e.GetAttributeValue<EntityReference>(Esquema.MapeoTablaPlantilla);
                mapeos[e.Id] = new Mapeo
                {
                    Id = e.Id,
                    Plantilla = plantilla == null ? Guid.Empty : plantilla.Id,
                    Tabla = e.GetAttributeValue<string>(Esquema.MapeoTablaDestino),
                    Dedup = dedup,
                    Firma = Firmar(e.GetAttributeValue<string>(Esquema.MapeoTablaDestino), e.GetAttributeValue<string>(Esquema.MapeoTablaNombre),
                        string.Join(",", dedup), Numero(e, Esquema.MapeoTablaOrden), e.GetAttributeValue<bool?>(Esquema.MapeoTablaCrear),
                        e.GetAttributeValue<bool?>(Esquema.MapeoTablaActualizar), e.GetAttributeValue<bool?>(Esquema.MapeoTablaIntermedia)),
                };
            }

            var qc = new QueryExpression(Esquema.MapeoColumna)
            {
                ColumnSet = new ColumnSet(Esquema.MapeoColumnaTabla, Esquema.MapeoColumnaNombre, Esquema.MapeoColumnaDestino, Esquema.MapeoColumnaFuente,
                    Esquema.MapeoColumnaTipo, Esquema.MapeoColumnaRequerida, Esquema.MapeoColumnaDefault, Esquema.MapeoColumnaLookupTabla,
                    Esquema.MapeoColumnaLookupClave, Esquema.MapeoColumnaOpciones, Esquema.MapeoColumnaTransformacion),
            };
            var tm = qc.AddLink(Esquema.MapeoTabla, Esquema.MapeoColumnaTabla, Esquema.MapeoTablaId);
            tm.LinkCriteria.AddCondition(Esquema.MapeoTablaPlantilla, ConditionOperator.In, plantillas.Cast<object>().ToArray());
            foreach (var e in Todas(svc, qc))
            {
                var padre = e.GetAttributeValue<EntityReference>(Esquema.MapeoColumnaTabla);
                Mapeo m;
                if (padre == null || !mapeos.TryGetValue(padre.Id, out m)) continue;
                var c = new Columna
                {
                    Destino = e.GetAttributeValue<string>(Esquema.MapeoColumnaDestino),
                    Fuente = e.GetAttributeValue<string>(Esquema.MapeoColumnaFuente),
                    Transformacion = e.GetAttributeValue<string>(Esquema.MapeoColumnaTransformacion),
                    PorDefecto = e.GetAttributeValue<string>(Esquema.MapeoColumnaDefault),
                };
                c.Firma = Firmar(e.GetAttributeValue<string>(Esquema.MapeoColumnaNombre), c.Destino, c.Fuente, Numero(e, Esquema.MapeoColumnaTipo),
                    e.GetAttributeValue<bool?>(Esquema.MapeoColumnaRequerida), c.PorDefecto, e.GetAttributeValue<string>(Esquema.MapeoColumnaLookupTabla),
                    e.GetAttributeValue<string>(Esquema.MapeoColumnaLookupClave), e.GetAttributeValue<string>(Esquema.MapeoColumnaOpciones), c.Transformacion);
                m.Columnas.Add(c);
            }
            foreach (var m in mapeos.Values)
            {
                m.Firma += "{" + string.Join("}{", m.Columnas.Select(c => c.Firma).OrderBy(f => f, StringComparer.Ordinal)) + "}";
            }
            return mapeos.Values.ToList();
        }

        private static IEnumerable<Entity> Todas(IOrganizationService svc, QueryExpression q)
        {
            q.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };
            while (true)
            {
                var r = svc.RetrieveMultiple(q);
                if (r == null) yield break;
                foreach (var e in r.Entities) yield return e;
                if (!r.MoreRecords) yield break;
                q.PageInfo.PageNumber++;
                q.PageInfo.PagingCookie = r.PagingCookie;
            }
        }

        /// <summary>
        /// Firma comparable: partes separadas por "|" (null = vacio). No se usa string.Join(string, params object[]): en .NET
        /// Framework devuelve "" si el primer elemento es null.
        /// </summary>
        private static string Firmar(params object[] partes)
        {
            return string.Join("|", partes.Select(x => x == null ? string.Empty : Convert.ToString(x, CultureInfo.InvariantCulture)));
        }

        /// <summary>Opcion o entero como texto (el tipo de dato de la columna es una opcion de Smelter).</summary>
        private static string Numero(Entity e, string columna)
        {
            if (!e.Contains(columna) || e[columna] == null) return string.Empty;
            var o = e[columna] as OptionSetValue;
            if (o != null) return o.Value.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(e[columna], CultureInfo.InvariantCulture);
        }

        /// <summary>La plantilla derivada tiene que ser copia exacta (mapeos y configuracion) de su parte de la principal.</summary>
        private static void ExigirCopia(LocalContext local, Entity principal, Entity derivada, List<Mapeo> esperados, List<Mapeo> reales)
        {
            var nombre = derivada.GetAttributeValue<string>(Esquema.PlantillaSmelterNombre);
            var estado = derivada.GetAttributeValue<OptionSetValue>(Esquema.PlantillaSmelterEstado);
            if (estado != null && estado.Value != EstadoActivo)
            {
                throw local.Error("la plantilla \"" + nombre + "\" no esta activa: Smelter no la usaria.");
            }
            var config = new[] { Esquema.PlantillaSmelterDelimitador, Esquema.PlantillaSmelterEncabezado, Esquema.PlantillaSmelterLote, Esquema.PlantillaSmelterOrigen };
            var igualConfig = config.All(c => string.Equals(Texto(principal, c), Texto(derivada, c), StringComparison.Ordinal));
            var a = esperados.Select(m => m.Firma).OrderBy(f => f, StringComparer.Ordinal);
            var b = reales.Select(m => m.Firma).OrderBy(f => f, StringComparer.Ordinal);
            if (!igualConfig || !a.SequenceEqual(b, StringComparer.Ordinal))
            {
                throw local.Error("la plantilla \"" + nombre + "\" no coincide con \"" + Esquema.NombrePlantillaCobranza
                    + "\": hay que regenerarla con scripts/pan/paso20_plantillas_paralelas.py --aplicar.");
            }
        }

        private static string Texto(Entity e, string columna)
        {
            if (!e.Contains(columna) || e[columna] == null) return string.Empty;
            var o = e[columna] as OptionSetValue;
            return o != null ? o.Value.ToString(CultureInfo.InvariantCulture) : Convert.ToString(e[columna], CultureInfo.InvariantCulture);
        }

        /// <summary>El CIF del cliente sale de la plantilla (la columna fuente que mapea a sanic_cm_cl_cif) y tiene que estar en el archivo.</summary>
        private static void ExigirCif(LocalContext local, ArchivoCsv csv, List<Mapeo> partes)
        {
            var cliente = partes.FirstOrDefault(m => m.Tabla == Esquema.Cliente);
            var cif = cliente == null ? null : cliente.Columnas.FirstOrDefault(c => c.Destino == Esquema.ClienteCif && !string.IsNullOrWhiteSpace(c.Fuente));
            if (cliente == null || cif == null || !cliente.Dedup.Contains(Esquema.ClienteCif))
            {
                throw local.Error("la plantilla principal no deduplica el cliente por " + Esquema.ClienteCif + " desde una columna del archivo: no se puede partir por CIF.");
            }
            if (csv.Indice(cif.Fuente) < 0)
            {
                throw local.Error("el encabezado del archivo no tiene la columna " + cif.Fuente.Trim() + " (CIF del cliente).");
            }
        }

        private sealed class Medida
        {
            public int Indice;
            public string Fuente;
            public string Tabla;
            public string Destino;
            public int Maximo;
        }

        /// <summary>
        /// Un valor mas largo que su columna de texto hace fallar la fila en Smelter y su prueba en seco no lo detecta
        /// (2026-10-22: un codigo de 27 caracteres en una columna de 20 rompio los padres de la carga real). Se mide contra
        /// el MaxLength de la metadata de Dataverse antes de escribir nada. Solo columnas con fuente en el archivo y sin
        /// transformacion (lo escrito es el valor tal cual, recortado y en UTF-8 como lo lee Smelter); lookups y opciones no
        /// tienen largo. Una consulta de metadata por tabla. El error cita fila (1 = la primera despues del encabezado),
        /// columna, destino y largos: NUNCA el valor.
        /// </summary>
        private static void ExigirLargos(LocalContext local, IOrganizationService svc, ArchivoCsv csv, List<Mapeo> mapeos)
        {
            var maximos = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var medidas = new List<Medida>();
            foreach (var m in mapeos)
            {
                if (string.IsNullOrWhiteSpace(m.Tabla)) continue;
                foreach (var c in m.Columnas)
                {
                    if (string.IsNullOrWhiteSpace(c.Fuente) || string.IsNullOrWhiteSpace(c.Destino) || !string.IsNullOrWhiteSpace(c.Transformacion)) continue;
                    var i = csv.Indice(c.Fuente);
                    if (i < 0) continue;
                    Dictionary<string, int> deTabla;
                    if (!maximos.TryGetValue(m.Tabla, out deTabla))
                    {
                        deTabla = LargosDeTabla(local, svc, m.Tabla);
                        maximos[m.Tabla] = deTabla;
                    }
                    int maximo;
                    if (!deTabla.TryGetValue(c.Destino.Trim(), out maximo)) continue;
                    medidas.Add(new Medida { Indice = i, Fuente = csv.Columnas[i], Tabla = m.Tabla, Destino = c.Destino.Trim(), Maximo = maximo });
                }
            }

            var casos = new List<string>();
            var total = 0;
            for (var f = 0; f < csv.Filas.Count; f++)
            {
                foreach (var x in medidas)
                {
                    var largo = ArchivoCsv.Utf8(ArchivoCsv.Valor(csv.Filas[f], x.Indice)).Length;
                    if (largo <= x.Maximo) continue;
                    total++;
                    if (casos.Count < MaximoCasosLargo)
                    {
                        casos.Add("fila " + (f + 1) + ", columna " + x.Fuente + " (" + x.Tabla + "." + x.Destino + "): " + largo
                            + " caracteres, maximo " + x.Maximo);
                    }
                }
            }
            if (total == 0) return;
            local.Trace("{0} valor(es) mas largos que su columna ({1}); no se parte.", total,
                string.Join(", ", medidas.Select(x => x.Tabla + "." + x.Destino).Distinct()));
            throw local.Error("hay " + total + " valor(es) mas largos de lo que admite su columna; no se creo nada y la importacion sigue igual. Corregi el archivo: "
                + string.Join("; ", casos) + (total > casos.Count ? "; y " + (total - casos.Count) + " mas" : string.Empty) + ".");
        }

        /// <summary>MaxLength de las columnas de texto (y texto largo) de la tabla, por nombre logico.</summary>
        private static Dictionary<string, int> LargosDeTabla(LocalContext local, IOrganizationService svc, string tabla)
        {
            EntityMetadata meta;
            try
            {
                meta = svc.Execute(new RetrieveEntityRequest { LogicalName = tabla, EntityFilters = EntityFilters.Attributes })["EntityMetadata"] as EntityMetadata;
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                throw local.Error("no se pudo leer la metadata de " + tabla + " para medir el largo de los valores.");
            }
            var res = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (meta == null || meta.Attributes == null) return res;
            foreach (var a in meta.Attributes)
            {
                int? maximo = null;
                var texto = a as StringAttributeMetadata;
                if (texto != null) maximo = texto.MaxLength;
                var memo = a as MemoAttributeMetadata;
                if (memo != null) maximo = memo.MaxLength;
                if (maximo.HasValue && maximo.Value > 0 && !string.IsNullOrEmpty(a.LogicalName)) res[a.LogicalName] = maximo.Value;
            }
            return res;
        }

        /// <summary>
        /// Clave de dedup de una tabla en columnas del archivo. Padres: tiene que haberla (sin dedup no hay "ultima fila").
        /// Partes: una tabla sin dedup crea un registro por fila y no se cruza con nadie (null). Una transformacion en la
        /// clave de partes solo se acepta si es funcion de su columna fuente y respeta la igualdad (HMAC, TRIM, UPPER,
        /// LOWER): con otra (por ejemplo RIGHT) dos valores distintos podrian ser el mismo registro.
        /// </summary>
        private static ParticionCarga.Clave Clave(LocalContext local, ArchivoCsv csv, Mapeo m, bool padres)
        {
            if (m.Dedup.Count == 0)
            {
                if (padres) throw local.Error("la tabla " + m.Tabla + " no tiene columnas de dedup: no se puede elegir su ultima fila.");
                return null;
            }
            var clave = new ParticionCarga.Clave { Tabla = m.Tabla };
            foreach (var destino in m.Dedup)
            {
                var col = m.Columnas.FirstOrDefault(c => string.Equals(c.Destino, destino, StringComparison.OrdinalIgnoreCase));
                if (col == null) throw local.Error("la columna de dedup " + destino + " de " + m.Tabla + " no tiene mapeo en la plantilla.");
                var columnas = new List<int>();
                if (!string.IsNullOrWhiteSpace(col.Fuente))
                {
                    var i = csv.Indice(col.Fuente);
                    if (i < 0) throw local.Error("el encabezado del archivo no tiene la columna " + col.Fuente.Trim() + " (dedup de " + m.Tabla + ").");
                    columnas.Add(i);
                }
                if (!string.IsNullOrWhiteSpace(col.Transformacion))
                {
                    var referidas = Identificador.Matches(Literal.Replace(col.Transformacion, " ")).Cast<Match>()
                        .Select(x => csv.Indice(x.Value)).Where(i => i >= 0).Distinct().ToList();
                    if (!padres && !TransformacionSegura(col, referidas, csv))
                    {
                        throw local.Error("la clave " + destino + " de " + m.Tabla + " usa una transformacion que no se puede partir con seguridad.");
                    }
                    columnas.AddRange(referidas.Where(i => !columnas.Contains(i)));
                }
                if (columnas.Count == 0) continue; // constante (valor por defecto sin columna): igual para todas las filas
                clave.Componentes.Add(new ParticionCarga.Componente { Columnas = columnas.ToArray(), PorDefecto = col.PorDefecto });
            }
            if (clave.Componentes.Count == 0)
            {
                if (padres) return clave; // todas las filas son el mismo registro: va la ultima
                throw local.Error("la tabla " + m.Tabla + " escribe siempre el mismo registro: no se puede partir.");
            }
            return clave;
        }

        private static bool TransformacionSegura(Columna col, List<int> referidas, ArchivoCsv csv)
        {
            if (string.IsNullOrWhiteSpace(col.Fuente)) return false;
            var fuente = csv.Indice(col.Fuente);
            if (referidas.Any(i => i != fuente)) return false;
            var patron = "^\\s*(HMAC|TRIM|UPPER|LOWER)\\s*\\(\\s*" + Regex.Escape(col.Fuente.Trim()) + "\\s*[,)]";
            return Regex.IsMatch(col.Transformacion, patron, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Archivo

        private static byte[] Descargar(LocalContext local, IOrganizationService svc, Guid jobId, out string nombreArchivo)
        {
            OrganizationResponse ini;
            try
            {
                var pedido = new OrganizationRequest("InitializeFileBlocksDownload");
                pedido["Target"] = new EntityReference(Esquema.JobSmelter, jobId);
                pedido["FileAttributeName"] = Esquema.JobSmelterArchivo;
                ini = svc.Execute(pedido);
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                throw local.Error("la importacion no tiene archivo.");
            }
            var token = ini.Results.Contains("FileContinuationToken") ? ini["FileContinuationToken"] as string : null;
            var tamano = ini.Results.Contains("FileSizeInBytes") ? Convert.ToInt64(ini["FileSizeInBytes"], CultureInfo.InvariantCulture) : 0L;
            nombreArchivo = ini.Results.Contains("FileName") ? ini["FileName"] as string : null;
            if (string.IsNullOrEmpty(token) || tamano <= 0) throw local.Error("la importacion no tiene archivo.");
            if (tamano > TamanoMaximo) throw local.Error("el archivo pasa de " + (TamanoMaximo / (1024 * 1024)) + " MB: no se parte.");

            var datos = new byte[tamano];
            long leidos = 0;
            while (leidos < tamano)
            {
                var largo = Math.Min(Bloque, tamano - leidos);
                var bajar = new OrganizationRequest("DownloadBlock");
                bajar["Offset"] = leidos;
                bajar["BlockLength"] = largo;
                bajar["FileContinuationToken"] = token;
                var parte = svc.Execute(bajar)["Data"] as byte[];
                if (parte == null || parte.Length == 0) throw local.Error("no se pudo leer el archivo completo.");
                var n = (int)Math.Min(parte.Length, tamano - leidos);
                Buffer.BlockCopy(parte, 0, datos, (int)leidos, n);
                leidos += n;
            }
            return datos;
        }

        /// <summary>Sube el archivo en bloques de 4 MB (el limite de UploadBlock), como AltaCuenta.</summary>
        private static void Subir(IOrganizationService svc, Guid jobId, string nombreArchivo, byte[] contenido)
        {
            var iniciar = new OrganizationRequest("InitializeFileBlocksUpload");
            iniciar["Target"] = new EntityReference(Esquema.JobSmelter, jobId);
            iniciar["FileAttributeName"] = Esquema.JobSmelterArchivo;
            iniciar["FileName"] = nombreArchivo;
            var token = (string)svc.Execute(iniciar)["FileContinuationToken"];

            var bloques = new List<string>();
            for (var desde = 0; desde < contenido.Length || bloques.Count == 0; desde += Bloque)
            {
                var largo = Math.Max(0, Math.Min(Bloque, contenido.Length - desde));
                var trozo = new byte[largo];
                Buffer.BlockCopy(contenido, desde, trozo, 0, largo);
                var id = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
                var subir = new OrganizationRequest("UploadBlock");
                subir["BlockId"] = id;
                subir["BlockData"] = trozo;
                subir["FileContinuationToken"] = token;
                svc.Execute(subir);
                bloques.Add(id);
            }

            var confirmar = new OrganizationRequest("CommitFileBlocksUpload");
            confirmar["FileName"] = nombreArchivo;
            confirmar["MimeType"] = "text/csv";
            confirmar["BlockList"] = bloques.ToArray();
            confirmar["FileContinuationToken"] = token;
            svc.Execute(confirmar);
        }

        /// <summary>Nombre del archivo sin ".csv", para los archivos nuevos (padres y partes).</summary>
        private static string Raiz(string nombreArchivo)
        {
            var n = string.IsNullOrWhiteSpace(nombreArchivo) ? "carga" : nombreArchivo.Trim();
            return n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n;
        }

        /// <summary>
        /// Deshace lo hecho: borra las partes creadas (servicio del sistema: el Operador de Smelter no tiene Delete; solo
        /// estos ids, creados en esta misma ejecucion) y, si ya se habia reemplazado, devuelve su archivo al job original
        /// (su nombre y plantilla no llegaron a cambiar: es lo ultimo que se escribe). Nunca lanza.
        /// </summary>
        private static void Deshacer(LocalContext local, List<Guid> creados, Guid jobId, bool archivoReemplazado, string nombreArchivo, byte[] original)
        {
            foreach (var id in creados)
            {
                try
                {
                    local.ServicioSistema.Delete(Esquema.JobSmelter, id);
                }
                catch (Exception ex)
                {
                    local.Trace("no se pudo borrar una parte ({0}); queda sin estado y no dispara nada.", ex.GetType().Name);
                }
            }
            if (!archivoReemplazado) return;
            try
            {
                Subir(local.Service, jobId, string.IsNullOrWhiteSpace(nombreArchivo) ? "carga.csv" : nombreArchivo, original);
            }
            catch (Exception ex)
            {
                local.Trace("no se pudo devolver el archivo original ({0}): el job queda sin estado y no dispara nada.", ex.GetType().Name);
            }
        }
    }
}
