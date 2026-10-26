// Custom API sanic_cm_SolicitarEspecial en vivo. La cuenta QA se da de alta con AltaCuenta (Admin, Diaria) y
// después: Admin pide la Especial (creada) → Solicitante la pide (ya existe, pedida por el Admin) → se cierra → el
// Solicitante la pide (reactivada, dueño = Solicitante) → Localizador rechazado. Limpieza con el prefijo QA. Sin PAN impreso.
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
const runid = 'SOL' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`;
const llamar = async (api, como, p) => {
  try { return { ok: true, json: (await dv.raw('POST', api, p, { MSCRMCallerID: como })).json }; }
  catch (e) { return { ok: false, msg: sinPAN(e.message) }; }
};

// Uso (desde tests/integral): NODE_PATH=../backoffice-canvas/node_modules node apis/solicitar-especial.js
(async () => {
  const agente = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  const prod = await dv.create('sanic_cm_tbl_productos', { sanic_cm_pr_codigo: `${PREFIJO}-${runid.slice(-4)}`, sanic_cm_pr_tipo: `${PREFIJO} Producto` });
  try {
    const r0 = await llamar('sanic_cm_AltaCuenta', U.adm, { CIF, Cliente: `${PREFIJO} ${runid}`, NumeroCuenta: csv.panFicticio(),
      SaldoVencidoNIO: 900, SaldoVencidoUSD: 0, TotalSaldoVencido: 900, DiasMora: 30, NivelMora: 1, ProductoId: prod.sanic_cm_tbl_productoid,
      AgenteId: agente.sanic_cm_tbl_agenteid, NombreFianza: `${PREFIJO} ${runid} Fiador`, TipoAsignacion: 'Diaria', EsPrueba: false });
    if (!r0.ok) throw new Error('alta: ' + r0.msg);
    await dormir(20000);
    await dv.raw('PATCH', `sanic_tbl_smlt_importjobs(${r0.json.JobId})`, { sanic_smlt_ij_status: 0 }, { MSCRMCallerID: U.adm });
    let cta;
    for (let i = 0; i < 60 && !cta; i++) { await dormir(5000); cta = (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid&$filter=sanic_cm_ct_cifcliente eq '${CIF}'`)).value[0]; }
    if (!cta) throw new Error('la cuenta QA no apareció');
    const base = { CuentaId: cta.sanic_cm_tbl_cuentaid, Notas: 'VIP — QA solicitud', RetiroNio: 300 };
    await dormir(5000);

    let r = await llamar('sanic_cm_SolicitarEspecial', U.cob, base);
    chk('SOL-01', !r.ok && /Solicitante|Admin/.test(r.msg), `Localizador (User5) rechazado: ${r.msg || 'PASÓ'}`);
    r = await llamar('sanic_cm_SolicitarEspecial', U.adm, base);
    chk('SOL-02', r.ok && r.json.Resultado === 'creada', `Admin pide la Especial: ${r.json?.Resultado || r.msg}`);
    const espId = r.json?.AsignacionId;
    const esp = await dv.get(`sanic_cm_tbl_asignacions(${espId})?$select=sanic_cm_as_tipo,sanic_cm_as_estadoaprobacion,sanic_cm_as_estado,_ownerid_value,_createdby_value,_sanic_cm_as_solicitadopor_value`);
    chk('SOL-03', esp.sanic_cm_as_tipo === 3 && esp.sanic_cm_as_estadoaprobacion === 2 && esp._createdby_value === U.adm && esp._sanic_cm_as_solicitadopor_value === U.adm,
      `Especial Pendiente, creada y pedida por el Admin (no por SYSTEM): tipo ${esp.sanic_cm_as_tipo}, aprob ${esp.sanic_cm_as_estadoaprobacion}`);
    r = await llamar('sanic_cm_SolicitarEspecial', U.sol, base);
    chk('SOL-04', r.ok && r.json.Resultado === 'ya existe' && !!r.json.SolicitadoPor && r.json.AsignacionId === espId,
      `Solicitante pide la misma: ${r.json?.Resultado || r.msg} · pedida por "${r.json?.SolicitadoPor || ''}" · estado ${r.json?.EstadoAprobacion}`);
    await dv.patch('sanic_cm_tbl_asignacions', espId, { sanic_cm_as_estado: false, sanic_cm_as_motivo: 'Rechazada', sanic_cm_as_estadoaprobacion: 4 });
    r = await llamar('sanic_cm_SolicitarEspecial', U.sol, { ...base, Notas: 'VIP — QA segunda vez' });
    chk('SOL-05', r.ok && r.json.Resultado === 'reactivada' && r.json.AsignacionId === espId, `Solicitante la vuelve a pedir cerrada: ${r.json?.Resultado || r.msg}`);
    const re = await dv.get(`sanic_cm_tbl_asignacions(${espId})?$select=sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,_ownerid_value,_sanic_cm_as_solicitadopor_value,sanic_cm_as_motivo`);
    chk('SOL-06', re.sanic_cm_as_estado === true && re.sanic_cm_as_estadoaprobacion === 2 && re._ownerid_value === U.sol && re._sanic_cm_as_solicitadopor_value === U.sol && !re.sanic_cm_as_motivo,
      `reactivada: activa, Pendiente, dueño y solicitante = User2, sin motivo de cierre`);
    const ve = await datos.leerComo(U.sol, `sanic_cm_tbl_asignacions(${espId})?$select=sanic_cm_as_estado`);
    chk('SOL-07', ve.status === 200, `el Solicitante la ve en Mis solicitudes (lectura como User2: ${ve.status})`);
    r = await llamar('sanic_cm_SolicitarEspecial', U.sol, { ...base, Notas: 'pan 4111111111111111' });
    chk('SOL-08', !r.ok && !/\d{13,19}/.test(r.msg), `notas con un número de tarjeta rechazadas: ${r.msg || 'PASÓ'}`);
  } finally {
    const l = await datos.limpiar();
    console.log('  limpieza restantes', JSON.stringify(l.restantes || {}).slice(0, 220));
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
