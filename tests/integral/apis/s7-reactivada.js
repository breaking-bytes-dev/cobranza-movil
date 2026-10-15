// S-7 tras reactivar, en vivo (1.0.8.2). El Admin crea la Especial con SolicitarEspecial, se cierra, el Solicitante (User2)
// la reactiva y el Supervisor (User3) la aprueba como lo hace la pantalla Aprobaciones. RepartoAsignacion pasa el dueño al
// cobrador (User5) y le comparte la Especial a "Solicitado por" aunque no sea quien la creó: User2 la sigue leyendo.
// Negativos: una persona de CM no puede poner a otro en "Solicitado por" (ni al editar ni al crear).
// Limpieza con el prefijo QA. Sin PAN impreso.
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
const runid = 'S7' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`;
const como = async (metodo, rel, cuerpo, usuario) => {
  try { const r = await dv.raw(metodo, rel, cuerpo, { MSCRMCallerID: usuario }); return { ok: true, status: r.status, json: r.json }; }
  catch (e) { return { ok: false, status: e.status, msg: sinPAN(e.message) }; }
};
const COLS = '$select=sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,_ownerid_value,_createdby_value,_sanic_cm_as_solicitadopor_value';

(async () => {
  const agente = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  const prod = await dv.create('sanic_cm_tbl_productos', { sanic_cm_pr_codigo: `${PREFIJO}-${runid.slice(-4)}`, sanic_cm_pr_tipo: `${PREFIJO} Producto` });
  try {
    const r0 = await como('POST', 'sanic_cm_AltaCuenta', { CIF, Cliente: `${PREFIJO} ${runid}`, NumeroCuenta: csv.panFicticio(),
      SaldoVencidoNIO: 900, SaldoVencidoUSD: 0, TotalSaldoVencido: 900, DiasMora: 30, NivelMora: 1, ProductoId: prod.sanic_cm_tbl_productoid,
      AgenteId: agente.sanic_cm_tbl_agenteid, NombreFianza: `${PREFIJO} ${runid} Fiador`, TipoAsignacion: 'Diaria', EsPrueba: false }, U.adm);
    if (!r0.ok) throw new Error('alta: ' + r0.msg);
    await dormir(20000);
    await dv.raw('PATCH', `sanic_tbl_smlt_importjobs(${r0.json.JobId})`, { sanic_smlt_ij_status: 0 }, { MSCRMCallerID: U.adm });
    let cta;
    for (let i = 0; i < 60 && !cta; i++) { await dormir(5000); cta = (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid&$filter=sanic_cm_ct_cifcliente eq '${CIF}'`)).value[0]; }
    if (!cta) throw new Error('la cuenta QA no apareció');
    const base = { CuentaId: cta.sanic_cm_tbl_cuentaid, Notas: 'VIP — QA S-7', RetiroNio: 300 };
    await dormir(5000);

    let r = await como('POST', 'sanic_cm_SolicitarEspecial', base, U.adm);
    const espId = r.json?.AsignacionId;
    let esp = espId && await dv.get(`sanic_cm_tbl_asignacions(${espId})?${COLS}`);
    chk('S7-01', r.ok && r.json.Resultado === 'creada' && esp._createdby_value === U.adm, `el Admin crea la Especial: ${r.json?.Resultado || r.msg}`);
    await dv.patch('sanic_cm_tbl_asignacions', espId, { sanic_cm_as_estado: false, sanic_cm_as_motivo: 'Rechazada', sanic_cm_as_estadoaprobacion: 4 });
    await dormir(10000); // el reparto asíncrono de la cerrada

    r = await como('POST', 'sanic_cm_SolicitarEspecial', { ...base, Notas: 'VIP — QA S-7 reactivada' }, U.sol);
    esp = await dv.get(`sanic_cm_tbl_asignacions(${espId})?${COLS}`);
    chk('S7-02', r.ok && r.json.Resultado === 'reactivada' && esp._createdby_value === U.adm && esp._sanic_cm_as_solicitadopor_value === U.sol,
      `User2 la reactiva: ${r.json?.Resultado || r.msg} · creada por el Admin, pedida por User2`);

    r = await como('PATCH', `sanic_cm_tbl_asignacions(${espId})`, {
      sanic_cm_as_estadoaprobacion: 3,
      'sanic_cm_as_agenteid@odata.bind': `/sanic_cm_tbl_agentes(${agente.sanic_cm_tbl_agenteid})`,
      'sanic_cm_as_resolutorid@odata.bind': `/systemusers(${U.sup})`,
    }, U.sup);
    chk('S7-03', r.ok, `el Supervisor (User3) la aprueba como la pantalla Aprobaciones: ${r.ok ? r.status : r.msg}`);

    for (let i = 0; i < 36; i++) { await dormir(5000); esp = await dv.get(`sanic_cm_tbl_asignacions(${espId})?${COLS}`); if (esp._ownerid_value === U.cob) break; }
    chk('S7-04', esp._ownerid_value === U.cob, `el reparto pasó el dueño al cobrador (User5): ${esp._ownerid_value === U.cob ? 'sí' : 'no'}`);
    await dormir(5000);
    const ve = await datos.leerComo(U.sol, `sanic_cm_tbl_asignacions(${espId})?$select=sanic_cm_as_estado`);
    chk('S7-05', ve.status === 200, `User2 la sigue leyendo aprobada aunque no la creó (antes de 1.0.8.2: la perdía): ${ve.status}`);

    r = await como('PATCH', `sanic_cm_tbl_asignacions(${espId})`, { 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.sup})` }, U.sol);
    chk('S7-06', !r.ok && /Solicitado por/.test(r.msg), `User2 pone a otro en "Solicitado por": ${r.ok ? 'PASÓ' : r.msg.slice(0, 160)}`);
    r = await como('PATCH', `sanic_cm_tbl_asignacions(${espId})`, { 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.sol})` }, U.sol);
    chk('S7-07', r.ok, `User2 se pone a sí mismo en "Solicitado por": ${r.ok ? r.status : r.msg.slice(0, 160)}`);
    r = await como('POST', 'sanic_cm_tbl_asignacions', {
      'sanic_cm_as_cuentaid@odata.bind': `/sanic_cm_tbl_cuentas(${cta.sanic_cm_tbl_cuentaid})`, sanic_cm_as_tipo: 3, sanic_cm_as_estado: true,
      sanic_cm_as_notas: 'QA S-7 a nombre de otro', 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.sup})`,
    }, U.sol);
    chk('S7-08', !r.ok && /Solicitado por/.test(r.msg), `User2 crea una Especial a nombre de otro: ${r.ok ? 'PASÓ' : r.msg.slice(0, 160)}`);
    if (r.ok && r.json?.sanic_cm_tbl_asignacionid) await dv.raw('DELETE', `sanic_cm_tbl_asignacions(${r.json.sanic_cm_tbl_asignacionid})`);
  } finally {
    const l = await datos.limpiar();
    console.log('  limpieza restantes', JSON.stringify(l.restantes || {}).slice(0, 220));
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
