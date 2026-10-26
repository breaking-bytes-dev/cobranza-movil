using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Borra el CSV del banco (columna de archivo sanic_smlt_ij_sourcefile, trae los PAN completos) de los jobs de Smelter
    /// de las plantillas de CM ("Cobranza movil", "· padres" y "· partes") en cuanto ya nadie lo va a leer. El job se
    /// conserva (contadores y logs). Smelter es de otro equipo: solo se toca la columna de archivo de sus jobs.
    ///
    /// Regla: se borra el archivo de un job que esta en un estado TERMINAL (Completed, CompletedWithErrors o Failed), sea
    /// prueba en seco o carga real. Por que es seguro:
    ///  - Cada envio crea un job nuevo con su propio archivo: la carga real NO reusa la prueba en seco (la pantalla vuelve
    ///    a subir el adjunto y AltaCuenta crea otro job). Un job terminado nunca vuelve a la cola.
    ///  - PartirCarga solo lee el archivo de un job SIN estado (ver PartirCarga, "ya tiene estado"), y la prueba en seco
    ///    con SoloValidar corre antes de ponerlo en cola.
    ///  - El job de padres termina despues de que PartirCarga creo las partes, cada una con SU archivo: los padres ya no se
    ///    leen. Las partes sin estado esperan a que los padres terminen bien; "Relanzar" solo pone en cola partes SIN estado
    ///    y "Descartar" las pasa a Failed sin correrlas.
    ///  - El estado se relee al correr (el paso es asincrono): si ya no es terminal, no se toca.
    /// Un job sin estado o En cola/Procesando nunca se purga por este camino.
    ///
    /// Barrido: en cada corrida ademas purga hasta <see cref="Esquema.MaximoPurgasPorCorrida"/> jobs de esas plantillas con
    /// archivo y de mas de <see cref="Esquema.DiasConservarArchivoCarga"/> dias que esten en estado terminal, o que sean
    /// pruebas en seco sin estado (abandonadas: la pantalla nunca relanza una prueba). Asi no queda nada para siempre
    /// por un paso que fallo o por jobs anteriores a este plugin. Las partes sin estado NO se barren: se pueden relanzar.
    ///
    /// Usa el servicio del sistema: el estado lo cambia el orquestador de Smelter (no un usuario de CM) y el barrido tiene
    /// que ver los jobs de todos. Nunca lanza por un job puntual: traza el tipo de error y sigue.
    ///
    /// Registro: PostOperation ASINCRONO, Update de sanic_tbl_smlt_importjob, filtrado por sanic_smlt_ij_status.
    /// </summary>
    public sealed class PurgaArchivoCarga : PluginBase
    {
        private static readonly int[] Terminales =
        {
            Esquema.JobEstadoCompletado, Esquema.JobEstadoConErrores, Esquema.JobEstadoFallido,
        };

        protected override string Nombre
        {
            get { return "PurgaArchivoCarga"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            if (ctx.Mode != Esquema.ModoAsincrono)
            {
                // Sincrono correria dentro de la actualizacion de estado de Smelter: no se frena su orquestador.
                local.Trace("registro incorrecto: el paso tiene que ser asincrono; no hace nada.");
                return;
            }
            local.ExigirRegistro(Esquema.JobSmelter, Esquema.EtapaPostOperation, Esquema.MensajeUpdate);

            var svc = local.ServicioSistema;
            var plantillas = PlantillasCm(svc);
            if (plantillas.Count == 0)
            {
                local.Trace("no hay plantillas de CM en Smelter; no hace nada.");
                return;
            }

            var target = local.ObtenerTarget();
            var propio = PurgarSiCorresponde(local, svc, target.Id, plantillas);
            var barridos = Barrer(local, svc, plantillas, target.Id);
            local.Trace("listo. este job: {0}; barrido: {1} archivo(s).", propio, barridos);
        }

        /// <summary>Ids de las plantillas de CM (por nombre, como PartirCarga).</summary>
        private static HashSet<Guid> PlantillasCm(IOrganizationService svc)
        {
            var q = new QueryExpression(Esquema.PlantillaSmelter) { ColumnSet = new ColumnSet(Esquema.PlantillaSmelterNombre) };
            q.Criteria.AddCondition(Esquema.PlantillaSmelterNombre, ConditionOperator.In,
                Esquema.NombrePlantillaCobranza, Esquema.NombrePlantillaPadres, Esquema.NombrePlantillaPartes);
            var r = svc.RetrieveMultiple(q);
            var ids = new HashSet<Guid>();
            if (r != null)
            {
                foreach (var e in r.Entities) ids.Add(e.Id);
            }
            return ids;
        }

        /// <summary>Relee el job (el estado puede haber cambiado desde el evento) y borra su archivo si la regla lo permite.</summary>
        private static string PurgarSiCorresponde(LocalContext local, IOrganizationService svc, Guid jobId, HashSet<Guid> plantillas)
        {
            Entity job;
            try
            {
                job = svc.Retrieve(Esquema.JobSmelter, jobId, new ColumnSet(Esquema.JobSmelterPlantilla, Esquema.JobSmelterEstado,
                    Esquema.JobSmelterEsPrueba, Esquema.JobSmelterArchivo));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                return "ya no existe";
            }
            if (job == null) return "ya no existe";

            var plantilla = job.GetAttributeValue<EntityReference>(Esquema.JobSmelterPlantilla);
            if (plantilla == null || !plantillas.Contains(plantilla.Id)) return "de otra plantilla";
            if (!EsTerminal(job.GetAttributeValue<OptionSetValue>(Esquema.JobSmelterEstado))) return "no terminal";
            var archivo = IdDeArchivo(job);
            if (archivo == Guid.Empty) return "sin archivo";
            return Borrar(local, svc, archivo) ? "archivo borrado" : "no se pudo borrar";
        }

        private static int Barrer(LocalContext local, IOrganizationService svc, HashSet<Guid> plantillas, Guid excepto)
        {
            var q = new QueryExpression(Esquema.JobSmelter)
            {
                ColumnSet = new ColumnSet(Esquema.JobSmelterArchivo),
                TopCount = Esquema.MaximoPurgasPorCorrida,
            };
            q.Criteria.AddCondition(Esquema.JobSmelterPlantilla, ConditionOperator.In, plantillas.Cast<object>().ToArray());
            // La columna de nombre del archivo es texto filtrable; la de archivo, no.
            q.Criteria.AddCondition(Esquema.JobSmelterArchivoNombre, ConditionOperator.NotNull);
            q.Criteria.AddCondition(Esquema.CreadoEl, ConditionOperator.LessThan,
                DateTime.UtcNow.AddDays(-Esquema.DiasConservarArchivoCarga));
            q.Criteria.AddCondition(Esquema.JobSmelterId, ConditionOperator.NotEqual, excepto);
            var cuales = new FilterExpression(LogicalOperator.Or);
            cuales.AddCondition(Esquema.JobSmelterEstado, ConditionOperator.In, Terminales.Cast<object>().ToArray());
            var pruebaSinEstado = new FilterExpression(LogicalOperator.And);
            pruebaSinEstado.AddCondition(Esquema.JobSmelterEsPrueba, ConditionOperator.Equal, true);
            pruebaSinEstado.AddCondition(Esquema.JobSmelterEstado, ConditionOperator.Null);
            cuales.AddFilter(pruebaSinEstado);
            q.Criteria.AddFilter(cuales);
            q.AddOrder(Esquema.CreadoEl, OrderType.Ascending);

            var r = svc.RetrieveMultiple(q);
            if (r == null) return 0;
            var borrados = 0;
            foreach (var job in r.Entities)
            {
                var archivo = IdDeArchivo(job);
                if (archivo != Guid.Empty && Borrar(local, svc, archivo)) borrados++;
            }
            return borrados;
        }

        private static bool EsTerminal(OptionSetValue estado)
        {
            return estado != null && Array.IndexOf(Terminales, estado.Value) >= 0;
        }

        /// <summary>Al leer una columna de archivo, el SDK devuelve el id del archivo (no su contenido).</summary>
        private static Guid IdDeArchivo(Entity job)
        {
            if (!job.Contains(Esquema.JobSmelterArchivo)) return Guid.Empty;
            var valor = job[Esquema.JobSmelterArchivo];
            if (valor is Guid) return (Guid)valor;
            Guid id;
            var texto = valor as string;
            return texto != null && Guid.TryParse(texto, out id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Una columna de archivo no se vacia con Update: se borra con el mensaje DeleteFile y el id del archivo. Se arma
        /// como pedido generico, igual que las demas operaciones de archivo del ensamblado. Nunca lanza.
        /// </summary>
        private static bool Borrar(LocalContext local, IOrganizationService svc, Guid archivo)
        {
            try
            {
                var pedido = new OrganizationRequest("DeleteFile");
                pedido["FileId"] = archivo;
                svc.Execute(pedido);
                return true;
            }
            catch (Exception ex)
            {
                local.Trace("no se pudo borrar un archivo ({0}); lo toma el proximo barrido.", ex.GetType().Name);
                return false;
            }
        }
    }
}
