// Custom API sanic_cm_AltaCuenta en vivo, llamada como User4 (Admin) y User2 (Solicitante), con datos QA y PAN FICTICIOS
// (BIN 9999) que viven solo en memoria. El job queda En cola (0) como con la pantalla y lo procesa el orquestador de Smelter.
// Al final limpia con el prefijo QA de la suite. Nunca imprime un PAN: toda salida pasa por sinPAN.
const path = require('path');
process.chdir(path.join(__dirname, '..'));
const dv = require('../../backoffice-canvas/lib/dataverse');
const { PREFIJO, AGENTE, U } = require('../lib/ctx');
const csv = require('../lib/csv');
const datos = require('../lib/datos');
const sinPAN = (s) => String(s).replace(/\d{13,19}/g, '[PAN]');
const dormir = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, mal = 0;
const chk = (id, cond, txt) => { cond ? ok++ : mal++; console.log(`${cond ? '✓' : '✗'} ${id}  ${sinPAN(txt)}`); };

const runid = 'ALT' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`, NOMBRE = `${PREFIJO} ${runid}`;

async function alta(como, p) {
  try {
    const r = await dv.raw('POST', 'sanic_cm_AltaCuenta', p, { MSCRMCallerID: como });
    return { ok: true, json: r.json };
  } catch (e) { return { ok: false, msg: sinPAN(e.message) }; }
}
async function encolarYEsperar(jobId) {
  await dormir(20000);
  await dv.raw('PATCH', `sanic_tbl_smlt_importjobs(${jobId})`, { sanic_smlt_ij_status: 0 }, { MSCRMCallerID: U.adm });
  let j;
  for (let i = 0; i < 60; i++) {
    await dormir(5000);
    j = await dv.get(`sanic_tbl_smlt_importjobs(${jobId})?$select=sanic_smlt_ij_status,sanic_smlt_ij_totalrows,sanic_smlt_ij_successcount,sanic_smlt_ij_failedcount,sanic_smlt_ij_name,sanic_smlt_ij_isdryrun`);
    if ([2, 3, 4].includes(j.sanic_smlt_ij_status)) break;
  }
  const logs = await dv.getAll(`sanic_tbl_smlt_importjoblogs?$select=sanic_smlt_ijl_errormessage,sanic_smlt_ijl_targettable&$filter=_sanic_smlt_ijl_importjobid_value eq ${jobId}`);
  return { j, logs };
}
const maxCarga = async () => (await dv.get("sanic_cm_tbl_cargadiarias?$select=sanic_cm_cd_fechacarga&$orderby=sanic_cm_cd_fechacarga desc&$top=1")).value[0]?.sanic_cm_cd_fechacarga || null;
// Diarias activas que la reconciliación vería "fuera de cartera" con el corte = día de la última carga.
async function fueraDeCartera() {
  const m = await maxCarga(); if (!m) return { corte: null, n: 0 };
  const corte = m.slice(0, 10) + 'T00:00:00Z';
  const r = await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid&$count=true&$top=1&$filter=sanic_cm_as_tipo eq 1 and sanic_cm_as_estado eq true and sanic_cm_as_ultimacarga lt ${corte}`);
  return { corte, n: r['@odata.count'] };
}

// Uso (desde tests/integral): NODE_PATH=../backoffice-canvas/node_modules node apis/alta-cuenta.js
(async () => {
  const agente = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  if (!agente) throw new Error('falta el agente QA de la suite');
  const prod = await dv.create('sanic_cm_tbl_productos', { sanic_cm_pr_codigo: `${PREFIJO}-${runid.slice(-4)}`, sanic_cm_pr_tipo: `${PREFIJO} Producto` });
  const base = {
    CIF, Cliente: NOMBRE, DireccionCasa: `${PREFIJO} casa, con coma`, Departamento: 'Managua', NumeroCelular: '00000000',
    SaldoVencidoNIO: 1200.5, SaldoVencidoUSD: 0, TotalSaldoVencido: 1200.5, DiasMora: 45, NivelMora: 2,
    FechaPago: '2026-10-20', FechaUltimoPago: '2026-09-01', Ciclo: '05',
    ProductoId: prod.sanic_cm_tbl_productoid, AgenteId: agente.sanic_cm_tbl_agenteid,
    NombreFianza: `${PREFIJO} ${runid} Fiador`, DireccionFianza: `${PREFIJO} dir fiador`,
  };
  const panE = csv.panFicticio(), panD = csv.panFicticio();
  const k = csv.claveHmac();
  const antes = await fueraDeCartera();
  try {
    // --- rechazos ---
    let r = await alta(U.sol, { ...base, NumeroCuenta: panE, TipoAsignacion: 'Especial', EsPrueba: true });
    chk('ALT-01', !r.ok && /solo un Admin/.test(r.msg), `Solicitante (User2) rechazado: ${r.msg || 'PASÓ'}`);
    const malo = panE.slice(0, -1) + String((Number(panE.slice(-1)) + 1) % 10);
    r = await alta(U.adm, { ...base, NumeroCuenta: malo, TipoAsignacion: 'Especial', EsPrueba: true });
    chk('ALT-02', !r.ok && /digito verificador/.test(r.msg) && !/\d{13,19}/.test(r.msg), `PAN sin Luhn rechazado y el mensaje no trae el número: ${r.msg || 'PASÓ'}`);
    r = await alta(U.adm, { ...base, NumeroCuenta: panE, TipoAsignacion: 'Especial', EsPrueba: true, Nota: 'tarjeta 4111111111111111' });
    chk('ALT-03', !r.ok && /numero de tarjeta/.test(r.msg), `Nota con un número de tarjeta rechazada: ${r.msg || 'PASÓ'}`);
    const jobsAntes = (await dv.get(`sanic_tbl_smlt_importjobs?$select=sanic_tbl_smlt_importjobid&$filter=contains(sanic_smlt_ij_name,'${CIF}')`)).value.length;
    chk('ALT-04', jobsAntes === 0, `los rechazos no crearon jobs (${jobsAntes})`);

    // --- cliente nuevo, Especial: prueba en seco ---
    r = await alta(U.adm, { ...base, NumeroCuenta: panE, TipoAsignacion: 'Especial', EsPrueba: true });
    chk('ALT-05', r.ok && r.json?.JobId, `prueba en seco aceptada · avisos: ${(r.json?.Advertencias || r.msg || '').replace(/\n/g, ' | ').slice(0, 400)}`);
    if (!r.ok) throw new Error('sin job de prueba');
    let job = await dv.get(`sanic_tbl_smlt_importjobs(${r.json.JobId})?$select=sanic_smlt_ij_name,_createdby_value,sanic_smlt_ij_isdryrun,sanic_smlt_ij_sourcefile_name,sanic_smlt_ij_status`);
    chk('ALT-06', !/\d{13,19}/.test(job.sanic_smlt_ij_name) && job._createdby_value === U.adm && job.sanic_smlt_ij_isdryrun === true && !!job.sanic_smlt_ij_sourcefile_name && !/\d{13,}/.test(job.sanic_smlt_ij_sourcefile_name) && job.sanic_smlt_ij_status == null,
      `job "${job.sanic_smlt_ij_name}" creado por User4, prueba, con archivo "${job.sanic_smlt_ij_sourcefile_name}", sin estado (${job.sanic_smlt_ij_status})`);
    let res = await encolarYEsperar(r.json.JobId);
    chk('ALT-07', res.j.sanic_smlt_ij_status === 2 && res.j.sanic_smlt_ij_totalrows === 1 && !res.j.sanic_smlt_ij_failedcount,
      `Smelter procesó la prueba: estado ${res.j.sanic_smlt_ij_status}, filas ${res.j.sanic_smlt_ij_totalrows}, fallos ${res.j.sanic_smlt_ij_failedcount}, logs ${res.logs.length} ${res.logs.map((l) => l.sanic_smlt_ijl_errormessage).join(' | ').slice(0, 300)}`);
    let cuentas = (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid&$filter=sanic_cm_ct_cifcliente eq '${CIF}'`)).value;
    chk('ALT-08', cuentas.length === 0, `la prueba en seco no creó cuentas (${cuentas.length})`);

    // --- real ---
    r = await alta(U.adm, { ...base, NumeroCuenta: panE, TipoAsignacion: 'Especial', EsPrueba: false });
    chk('ALT-09', r.ok, `carga real aceptada ${r.msg || ''}`);
    res = await encolarYEsperar(r.json.JobId);
    chk('ALT-10', res.j.sanic_smlt_ij_status === 2 && !res.j.sanic_smlt_ij_failedcount && res.logs.every((l) => !/\d{13,19}/.test(l.sanic_smlt_ijl_errormessage || '')),
      `Smelter procesó la real: estado ${res.j.sanic_smlt_ij_status}, éxitos ${res.j.sanic_smlt_ij_successcount}, fallos ${res.j.sanic_smlt_ij_failedcount}, logs sin PAN`);
    const ct = (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid,sanic_cm_ct_ultimos4,sanic_cm_ct_clavepan,sanic_cm_ct_numerodecuenta,sanic_cm_ct_nombrecliente,sanic_cm_ct_fechapago&$filter=sanic_cm_ct_cifcliente eq '${CIF}'`)).value;
    const c = ct[0];
    chk('ALT-11', ct.length === 1 && c.sanic_cm_ct_ultimos4 === panE.slice(-4) && (!k || c.sanic_cm_ct_clavepan === csv.hmacPan(k, panE)),
      `cuenta creada: 1, últimos 4 ok, clave = HMAC(K) ${k ? 'verificada' : '(sin K local, no verificada)'}`);
    chk('ALT-12', !c.sanic_cm_ct_numerodecuenta || !/\d{13,19}/.test(c.sanic_cm_ct_numerodecuenta), `la cuenta no guarda el número completo ("${c.sanic_cm_ct_numerodecuenta || ''}")`);
    const bov = (await dv.get(`sanic_cm_tbl_cuentapans?$select=sanic_cm_tbl_cuentapanid&$filter=_sanic_cm_cp_cuentaid_value eq ${c.sanic_cm_tbl_cuentaid}`)).value;
    chk('ALT-13', bov.length === 1, `bóveda: ${bov.length} fila`);
    const as = (await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_as_tipo,sanic_cm_as_estadoaprobacion,sanic_cm_as_estado,_sanic_cm_as_agenteid_value&$filter=_sanic_cm_as_cuentaid_value eq ${c.sanic_cm_tbl_cuentaid}`)).value;
    chk('ALT-14', as.length === 1 && as[0].sanic_cm_as_tipo === 3 && as[0].sanic_cm_as_estadoaprobacion === 2 && as[0].sanic_cm_as_estado === true && as[0]._sanic_cm_as_agenteid_value === agente.sanic_cm_tbl_agenteid,
      `asignación Especial, Pendiente de aprobación, activa, con el agente elegido (${JSON.stringify(as.map((a) => [a.sanic_cm_as_tipo, a.sanic_cm_as_estadoaprobacion, a.sanic_cm_as_estado]))})`);
    const despues = await fueraDeCartera();
    chk('ALT-15', despues.n === antes.n, `reconciliación: Diarias fuera de cartera ${antes.n} → ${despues.n} (corte ${antes.corte} → ${despues.corte})`);

    // --- cliente existente, Diaria, con otro nombre: no lo pisa ---
    r = await alta(U.adm, { ...base, Cliente: 'OTRO NOMBRE QA', NumeroCuenta: panD, TipoAsignacion: 'Diaria', EsPrueba: false });
    chk('ALT-16', r.ok && /ya existe/.test(r.json?.Advertencias || '') && /reconciliacion/.test(r.json?.Advertencias || ''),
      `cliente existente + Diaria aceptada con avisos: ${(r.json?.Advertencias || r.msg || '').replace(/\n/g, ' | ').slice(0, 400)}`);
    if (r.ok) {
      res = await encolarYEsperar(r.json.JobId);
      const cli = (await dv.get(`sanic_cm_tbl_clientes?$select=sanic_cm_cl_nombre&$filter=sanic_cm_cl_cif eq '${CIF}'`)).value;
      const n2 = (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid&$filter=sanic_cm_ct_cifcliente eq '${CIF}'`)).value.length;
      chk('ALT-17', res.j.sanic_smlt_ij_status === 2 && cli.length === 1 && cli[0].sanic_cm_cl_nombre === NOMBRE && n2 === 2,
        `Diaria cargada (estado ${res.j.sanic_smlt_ij_status}), 2 cuentas del cliente (${n2}) y el nombre no cambió ("${cli[0]?.sanic_cm_cl_nombre}")`);
    }
    // --- misma tarjeta (mismos últimos 4) en el mismo cliente: rechazo ---
    r = await alta(U.adm, { ...base, NumeroCuenta: panE, TipoAsignacion: 'Incobrable', EsPrueba: true });
    chk('ALT-18', !r.ok && /ya tiene una cuenta/.test(r.msg), `misma cuenta del mismo cliente rechazada: ${r.msg || 'PASÓ'}`);
    // --- trazas de la plataforma sin PAN ---
    const trazas = await dv.get(`plugintracelogs?$select=messageblock,exceptiondetails&$filter=contains(typename,'Sanic.CobranzaMovil.Plugins.AltaCuenta') and createdon ge ${new Date(Date.now() - 3600e3).toISOString()}`);
    const conPan = trazas.value.filter((t) => [panE, panD].some((p) => `${t.messageblock}${t.exceptiondetails}`.includes(p)) || /\d{13,19}/.test(`${t.messageblock}${t.exceptiondetails}`));
    chk('ALT-19', conPan.length === 0, `trazas de AltaCuenta: ${trazas.value.length}, con secuencias de 13 a 19 dígitos: ${conPan.length}`);
  } finally {
    const l = await datos.limpiar();
    console.log('  limpieza:', sinPAN(JSON.stringify(l.borrados || l)).slice(0, 300), 'restantes', JSON.stringify(l.restantes || {}).slice(0, 200));
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
