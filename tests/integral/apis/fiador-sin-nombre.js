// Fiador sin nombre (opcion 2a-2, paso13_fiador_sin_nombre.py) en vivo, con datos QA y PAN FICTICIOS (BIN 9999, solo en memoria).
// Carga por la API de Smelter (como lib/smelter.js) de 2 filas SIN NombreFianza + 1 con fiador QA; 2.a carga igual; Alta de cuenta
// (Custom API sanic_cm_AltaCuenta como User4) sin fiador. Verifica: un solo fiador "(sin fiador)", cuentas sin fiador con el lookup
// vacio, la cuenta con fiador enlazada a su fiador QA, jobs Completed sin filas fallidas y ningun fiador vacio nuevo.
// Al final limpia con el prefijo QA (el fiador "(sin fiador)" NO lleva prefijo: es el placeholder permanente y se queda).
// Uso (desde tests/integral): NODE_PATH=../backoffice-canvas/node_modules node apis/fiador-sin-nombre.js
const path = require('path');
process.chdir(path.join(__dirname, '..'));
const dv = require('../../backoffice-canvas/lib/dataverse');
const { PREFIJO, AGENTE, U, RAIZ, Corrida, sinPAN } = require('../lib/ctx');
const csv = require('../lib/csv');
const smelter = require('../lib/smelter');
const datos = require('../lib/datos');
const dormir = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, mal = 0;
const chk = (id, cond, txt) => { cond ? ok++ : mal++; console.log(`${cond ? '✓' : '✗'} ${id}  ${sinPAN(txt)}`); };
const ESTADO = { 0: 'Queued', 1: 'Processing', 2: 'Completed', 3: 'CompletedWithErrors', 4: 'Failed' };
const PH = '(sin fiador)';

const runid = 'FSN' + Date.now().toString(36).slice(-4).toUpperCase();
const ctx = new Corrida({ runid, dir: path.join(RAIZ, `fiador-${runid}`) });
const FIADOR_QA = `${PREFIJO} Fiador`;

const fiadores = async (filtro) => (await dv.getAll(`sanic_cm_tbl_fiadors?$select=sanic_cm_fi_nombre,sanic_cm_fi_direccion&$filter=${encodeURIComponent(filtro)}`));
const vacios = async () => (await dv.getAll('sanic_cm_tbl_fiadors?$select=sanic_cm_fi_nombre&$filter=sanic_cm_fi_nombre eq null')).length;
const cuentasDe = async (cif) => (await dv.getAll(`sanic_cm_tbl_cuentas?$select=sanic_cm_ct_ultimos4,_sanic_cm_ct_fiador_value,sanic_cm_ct_nombrefiador&$filter=sanic_cm_ct_cifcliente eq '${cif}'`));
const fallos = (logs) => logs.map((l) => `${l.sanic_smlt_ijl_targettable}#${l.sanic_smlt_ijl_rownumber}: ${(l.sanic_smlt_ijl_errormessage || '').slice(0, 160)}`).join(' | ');

// CSV de la suite (3 cuentas de un cliente QA) con NombreFianza/DireccionFianza vacios en A y B; C conserva el fiador QA.
function armarCsv() {
  const g = csv.generar(ctx);
  const [enc, ...filas] = g.contenido.toString('utf8').split('\r\n').filter(Boolean);
  const cols = enc.split(','), iN = cols.indexOf('NombreFianza'), iD = cols.indexOf('DireccionFianza');
  const out = filas.map((l, i) => { const c = l.split(','); if (i < 2) { c[iN] = ''; c[iD] = ''; } return c.join(','); });
  return { contenido: Buffer.from([enc, ...out].join('\r\n') + '\r\n', 'utf8'), cuentas: g.cuentas };
}

async function alta(p) {
  try { return { ok: true, json: (await dv.raw('POST', 'sanic_cm_AltaCuenta', p, { MSCRMCallerID: U.adm })).json }; }
  catch (e) { return { ok: false, msg: sinPAN(e.message) }; }
}
async function encolarYEsperar(jobId) {
  await dormir(20000);
  await dv.raw('PATCH', `sanic_tbl_smlt_importjobs(${jobId})`, { sanic_smlt_ij_status: 0 }, { MSCRMCallerID: U.adm });
  let j;
  for (let i = 0; i < 60; i++) {
    await dormir(5000);
    j = await dv.get(`sanic_tbl_smlt_importjobs(${jobId})?$select=sanic_smlt_ij_status,sanic_smlt_ij_totalrows,sanic_smlt_ij_successcount,sanic_smlt_ij_failedcount`);
    if ([2, 3, 4].includes(j.sanic_smlt_ij_status)) break;
  }
  return { j, logs: await smelter.logsDe(jobId) };
}

(async () => {
  const cfg = (await dv.get("sanic_tbl_smlt_columnmappings?$select=sanic_smlt_cm_defaultvalue&$filter=sanic_smlt_cm_sourcecolumn eq 'NombreFianza' and sanic_smlt_cm_targetcolumn eq 'sanic_cm_fi_nombre'")).value;
  chk('FSN-00', cfg.length >= 1 && cfg.every((c) => c.sanic_smlt_cm_defaultvalue === PH), `Column Mapping NombreFianza → sanic_cm_fi_nombre con default "${PH}" (${cfg.map((c) => c.sanic_smlt_cm_defaultvalue).join(',')})`);
  const vaciosAntes = await vacios(), phAntes = (await fiadores(`sanic_cm_fi_nombre eq '${PH}'`)).length;
  console.log(`  antes: fiadores vacíos ${vaciosAntes} · "${PH}" ${phAntes}`);
  try {
    // --- carga 1 y carga 2 (idempotencia) por la API de Smelter ---
    for (const n of [1, 2]) {
      const { contenido, cuentas } = armarCsv();
      if (n === 2) { /* mismos CIF/fiador QA; PAN nuevos: 3 cuentas mas */ }
      const imp = await smelter.importar(ctx, contenido, n);
      const j = imp.job;
      chk(`FSN-${n}1`, j.sanic_smlt_ij_status === 2 && !j.sanic_smlt_ij_failedcount && j.sanic_smlt_ij_totalrows === 3 && imp.logs.length === 0,
        `carga ${n}: estado ${ESTADO[j.sanic_smlt_ij_status]} (${j.sanic_smlt_ij_status}), filas ${j.sanic_smlt_ij_totalrows}, éxitos ${j.sanic_smlt_ij_successcount}, fallos ${j.sanic_smlt_ij_failedcount}, logs ${imp.logs.length} ${fallos(imp.logs)}`);
      const ph = await fiadores(`sanic_cm_fi_nombre eq '${PH}'`);
      chk(`FSN-${n}2`, ph.length === 1, `(a) fiadores "${PH}" tras la carga ${n}: ${ph.length} (dirección: ${JSON.stringify(ph[0]?.sanic_cm_fi_direccion ?? null)})`);
      const qa = await fiadores(`sanic_cm_fi_nombre eq '${FIADOR_QA}'`);
      chk(`FSN-${n}3`, qa.length === 1, `fiador QA "${FIADOR_QA}": ${qa.length}`);
      const cts = await cuentasDe(ctx.cif);
      const conF = cts.filter((c) => c._sanic_cm_ct_fiador_value), sinF = cts.filter((c) => !c._sanic_cm_ct_fiador_value);
      const fqaId = (await dv.get(`sanic_cm_tbl_fiadors?$select=sanic_cm_tbl_fiadorid&$filter=sanic_cm_fi_nombre eq '${FIADOR_QA}'`)).value[0]?.sanic_cm_tbl_fiadorid;
      const esperadoConF = n * 1, esperadoSinF = n * 2;
      chk(`FSN-${n}4`, sinF.length === esperadoSinF && sinF.every((c) => !c.sanic_cm_ct_nombrefiador), `(b) cuentas sin fiador con sanic_cm_ct_fiador vacío: ${sinF.length}/${esperadoSinF} (nombrefiador vacío: ${sinF.every((c) => !c.sanic_cm_ct_nombrefiador)})`);
      chk(`FSN-${n}5`, conF.length === esperadoConF && conF.every((c) => c._sanic_cm_ct_fiador_value === fqaId), `(c) cuentas con fiador enlazadas al fiador QA: ${conF.length}/${esperadoConF}`);
      chk(`FSN-${n}6`, (await vacios()) === vaciosAntes, `(f) fiadores vacíos: ${await vacios()} (antes ${vaciosAntes})`);
    }
    // --- Alta de cuenta sin fiador (Custom API + orquestador) ---
    const agente = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
    const prod = await dv.create('sanic_cm_tbl_productos', { sanic_cm_pr_codigo: `${PREFIJO}-${runid.slice(-4)}`, sanic_cm_pr_tipo: `${PREFIJO} Producto` });
    const CIF = `${PREFIJO}-A${runid}`;
    const base = {
      CIF, Cliente: `${PREFIJO} ${runid} alta`, DireccionCasa: `${PREFIJO} casa`, Departamento: 'Managua', NumeroCelular: '00000000',
      SaldoVencidoNIO: 900, SaldoVencidoUSD: 0, TotalSaldoVencido: 900, DiasMora: 30, NivelMora: 1, FechaPago: '2026-10-20', FechaUltimoPago: '2026-09-01', Ciclo: '05',
      ProductoId: prod.sanic_cm_tbl_productoid, AgenteId: agente.sanic_cm_tbl_agenteid, NumeroCuenta: csv.panFicticio(), TipoAsignacion: 'Especial', EsPrueba: false,
    };
    const r = await alta(base);
    chk('FSN-70', r.ok && !!r.json?.JobId, `Alta de cuenta sin fiador aceptada por la API ${r.msg || ''}`);
    if (r.ok) {
      const res = await encolarYEsperar(r.json.JobId);
      chk('FSN-71', res.j.sanic_smlt_ij_status === 2 && !res.j.sanic_smlt_ij_failedcount && res.logs.length === 0,
        `(d)(e) job del Alta: ${ESTADO[res.j.sanic_smlt_ij_status]} (${res.j.sanic_smlt_ij_status}), éxitos ${res.j.sanic_smlt_ij_successcount}, fallos ${res.j.sanic_smlt_ij_failedcount} ${fallos(res.logs)}`);
      const cts = await cuentasDe(CIF);
      chk('FSN-72', cts.length === 1 && !cts[0]._sanic_cm_ct_fiador_value, `(b) cuenta del Alta creada (${cts.length}) con sanic_cm_ct_fiador vacío`);
    }
    chk('FSN-80', (await fiadores(`sanic_cm_fi_nombre eq '${PH}'`)).length === 1, `(a) tras todo: fiadores "${PH}" = ${(await fiadores(`sanic_cm_fi_nombre eq '${PH}'`)).length}`);
    chk('FSN-81', (await vacios()) === vaciosAntes, `(f) fiadores vacíos al final: ${await vacios()} (antes ${vaciosAntes})`);
  } catch (e) {
    chk('FSN-ERR', false, `la prueba se cortó: ${e.message}`);
  } finally {
    const l = await datos.limpiar();
    const quedan = Object.entries(l.restantes || {}).filter(([, n]) => n > 0);
    console.log('  limpieza:', sinPAN(JSON.stringify(l.borrados || l)).slice(0, 300));
    chk('FSN-90', quedan.length === 0 && !(l.errores || []).length, `limpieza: restos QA ${quedan.map(([k, n]) => `${k}:${n}`).join(', ') || '0'} · errores ${(l.errores || []).length}`);
    chk('FSN-91', (await fiadores(`sanic_cm_fi_nombre eq '${PH}'`)).length === 1, `el placeholder "${PH}" se queda (1)`);
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
    process.exit(mal ? 1 : 0);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
