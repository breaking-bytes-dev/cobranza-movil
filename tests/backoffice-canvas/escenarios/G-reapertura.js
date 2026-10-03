// FASE G — F-06: después de CERRAR la Especial QA (F-04), ¿se puede pedir OTRA para la misma cuenta?
// Diseño declarado (btnEnviarNva): "'[AS] Estado' = true acota a la Especial ACTIVA: una cerrada no bloquea
// pedir otra". Riesgo: la alt key de Dataverse sanic_cm_altkey_numerocuentaxtipo (tipo + numerocuenta) NO
// incluye el estado → el Patch de la app chocaría con "clave duplicada".
// Por la UI no se puede (el combo de cuentas no muestra resultados — F-02a): se prueba la MISMA operación que
// hace el Patch de la app (crear Especial tipo 3 con la misma cuenta) contra Dataverse con el SP.
const inf = require('../lib/informe');
const dv = require('../lib/dataverse');
const qa = require('../lib/datosqa');
const cfg = require('../lib/config');

module.exports = async function faseG(suite) {
  const cta = suite.datos.cuentas[0];
  await inf.correr('F-06', 'Re-solicitar Especial para una cuenta cuya Especial anterior está CERRADA (alt key cuenta+tipo)', 'Solicitante', async (e) => {
    const previa = (await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_as_estado,sanic_cm_as_numero&$filter=sanic_cm_as_numerocuenta eq '${cta.sanic_cm_ct_numerodecuenta}' and sanic_cm_as_tipo eq 3`)).value;
    if (!(previa.length === 1 && previa[0].sanic_cm_as_estado === false)) { e.bloquear(`precondición: la Especial previa debe estar cerrada (hay ${previa.length}, estados ${JSON.stringify(previa.map(p => p.sanic_cm_as_estado))})`); return; }
    e.nota(`Especial previa ${previa[0].sanic_cm_as_numero} cerrada (Estado = false). UI no utilizable (F-02a) → misma escritura por Web API.`);
    let creada = null, error = null;
    try { creada = await qa.crearEspecialPendiente(cta, cfg.usuarios.user3.id, 'F-06 re-solicitud', { notas: 'Retiro urgente — QA-E2E re-solicitud F-06', nio: 120 }); }
    catch (err) { error = err.message; }
    e.check(!!creada, 'Dataverse acepta una nueva Especial para la cuenta (la anterior está cerrada)', 'creada', error ? `rechazada: ${error.slice(0, 260)}` : 'creada');
  });
};
