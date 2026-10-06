// Uso de Smelter SOLO por su API ya publicada (igual que scripts/pan/paso6_prueba.py): se crea un Import Job sobre la
// plantilla "Cobranza movil", se sube el CSV a su columna de archivo y se maneja con las Custom API prepareimport /
// processchunk. NO se toca el estado a 0 (eso dispara el flow orquestador) y NO se modifica nada de Smelter
// (plantilla, mapeos, soluciones, flows, apps): solo se crean y luego se borran los jobs de la corrida.
const dv = require('../../backoffice-canvas/lib/dataverse');
const { PREFIJO, sinPAN } = require('./ctx');

const CONTADORES = 'sanic_smlt_ij_status,sanic_smlt_ij_totalrows,sanic_smlt_ij_processedrows,sanic_smlt_ij_successcount,sanic_smlt_ij_failedcount,sanic_smlt_ij_skippedcount,sanic_smlt_ij_sourcefile_name';

async function plantillaId() {
  const r = await dv.get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'");
  if (!r.value.length) throw new Error('No existe la plantilla "Cobranza movil" de Smelter');
  return r.value[0].sanic_tbl_smlt_importtemplateid;
}

async function logsDe(jobId) {
  return dv.getAll(`sanic_tbl_smlt_importjoblogs?$select=sanic_smlt_ijl_errormessage,sanic_smlt_ijl_targettable,sanic_smlt_ijl_errorcategory,sanic_smlt_ijl_rownumber&$filter=_sanic_smlt_ijl_importjobid_value eq ${jobId}`);
}

// Importa `contenido` (Buffer con el CSV). Devuelve { jobId, job, logs, chunks }.
async function importar(ctx, contenido, n) {
  const tid = await plantillaId();
  const job = await dv.create('sanic_tbl_smlt_importjobs', {
    sanic_smlt_ij_name: `${PREFIJO} ${ctx.runid} job ${n}`,
    'sanic_smlt_ij_importtemplateid@odata.bind': `/sanic_tbl_smlt_importtemplates(${tid})`,
  });
  const jobId = job.sanic_tbl_smlt_importjobid;
  ctx.registrar('job', jobId, `import ${n}`);
  await dv.subirArchivo(`sanic_tbl_smlt_importjobs(${jobId})/sanic_smlt_ij_sourcefile`, contenido, `${PREFIJO.toLowerCase()}-${ctx.runid}-${n}.csv`);
  await dv.post('sanic_capi_smelter_prepareimport', { JobId: jobId });
  let chunks = 0;
  for (; chunks < 60;) {
    const r = await dv.post('sanic_capi_smelter_processchunk', { JobId: jobId });
    chunks++;
    if (!r || !r.HasMore) break;
  }
  const leer = () => dv.get(`sanic_tbl_smlt_importjobs(${jobId})?$select=${CONTADORES}`);
  let j = await leer();
  // El estado final puede tardar unos segundos en reflejarse (processchunk cierra el job al terminar).
  for (let i = 0; i < 12 && ![2, 3, 4].includes(j.sanic_smlt_ij_status); i++) { await new Promise((r) => setTimeout(r, 5000)); j = await leer(); }
  const logs = await logsDe(jobId);
  return { jobId, job: j, logs, chunks };
}

const estadoTxt = (j) => `${j['sanic_smlt_ij_status@OData.Community.Display.V1.FormattedValue'] || j.sanic_smlt_ij_status} (${j.sanic_smlt_ij_status})`;
const resumenJob = (j) => `estado ${estadoTxt(j)} · filas ${j.sanic_smlt_ij_totalrows} · procesadas ${j.sanic_smlt_ij_processedrows} · éxitos ${j.sanic_smlt_ij_successcount} · fallos ${j.sanic_smlt_ij_failedcount} · omitidas ${j.sanic_smlt_ij_skippedcount}`;
const textoLogs = (logs) => logs.map((l) => `${l.sanic_smlt_ijl_targettable || '?'}#${l.sanic_smlt_ijl_rownumber}: ${sinPAN((l.sanic_smlt_ijl_errormessage || '').slice(0, 160))}`).join(' | ');

module.exports = { importar, logsDe, resumenJob, textoLogs, estadoTxt };
