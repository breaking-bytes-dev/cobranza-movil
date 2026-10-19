// S-7 (b1) en vivo (1.0.8.3): al reactivar una Especial pedida por otra persona, SolicitarEspecial le quita a esa persona el
// compartido de la asignación. Hay un solo Solicitante QA (User2), así que "el anterior" es el Admin (User4):
// el Admin la pide → el Supervisor (User3) la aprueba → el reparto se la comparte al Admin y pasa el dueño al cobrador →
// se cierra → User2 la reactiva → el Admin ya no tiene el compartido → se aprueba de nuevo → User2 la lee y el Admin sigue
// sin compartido. El Admin lee todo por su rol, así que se mira el compartido (RetrieveSharedPrincipalsAndAccess), no la
// lectura. Limpieza con el prefijo QA. Sin PAN impreso.
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
const runid = 'S7B' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`;
const como = async (metodo, rel, cuerpo, usuario) => {
  try { const r = await dv.raw(metodo, rel, cuerpo, { MSCRMCallerID: usuario }); return { ok: true, status: r.status, json: r.json }; }
  catch (e) { return { ok: false, status: e.status, msg: sinPAN(e.message) }; }
};
const COLS = '$select=sanic_cm_as_estado,_ownerid_value,_sanic_cm_as_solicitadopor_value';
const compartidos = async (id) => {
  const t = encodeURIComponent(JSON.stringify({ '@odata.id': `sanic_cm_tbl_asignacions(${id})` }));
  const r = await dv.get(`RetrieveSharedPrincipalsAndAccess(Target=@t)?@t=${t}`);
  return (r.PrincipalAccesses || []).map((p) => p.Principal.systemuserid || p.Principal.teamid || p.Principal.ownerid);
};

(async () => {
  const agente = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  const prod = await dv.create('sanic_cm_tbl_productos', { sanic_cm_pr_codigo: `${PREFIJO}-${runid.slice(-4)}`, sanic_cm_pr_tipo: `${PREFIJO} Producto` });
  const aprobar = (espId) => como('PATCH', `sanic_cm_tbl_asignacions(${espId})`, {
    sanic_cm_as_estadoaprobacion: 3,
    'sanic_cm_as_agenteid@odata.bind': `/sanic_cm_tbl_agentes(${agente.sanic_cm_tbl_agenteid})`,
    'sanic_cm_as_resolutorid@odata.bind': `/systemusers(${U.sup})`,
  }, U.sup);
  const esperarCobrador = async (espId) => {
    let esp;
    for (let i = 0; i < 36; i++) { await dormir(5000); esp = await dv.get(`sanic_cm_tbl_asignacions(${espId})?${COLS}`); if (esp._ownerid_value === U.cob) break; }
    await dormir(5000); // el compartido al solicitante corre en el mismo reparto, después del dueño
    return esp;
  };
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
    const base = { CuentaId: cta.sanic_cm_tbl_cuentaid, Notas: 'VIP — QA S-7 b1', RetiroNio: 300 };
    await dormir(5000);

    let r = await como('POST', 'sanic_cm_SolicitarEspecial', base, U.adm);
    const espId = r.json?.AsignacionId;
    chk('S7B-01', r.ok && r.json.Resultado === 'creada', `el Admin pide la Especial: ${r.json?.Resultado || r.msg}`);
    r = await aprobar(espId);
    let esp = await esperarCobrador(espId);
    let comp = await compartidos(espId);
    chk('S7B-02', r.ok && esp._ownerid_value === U.cob && comp.includes(U.adm),
      `aprobada: dueño el cobrador y compartida con el Admin (precondición): dueño ${esp._ownerid_value === U.cob ? 'User5' : 'otro'}, Admin ${comp.includes(U.adm) ? 'sí' : 'no'}`);

    await dv.patch('sanic_cm_tbl_asignacions', espId, { sanic_cm_as_estado: false, sanic_cm_as_motivo: 'Rechazada', sanic_cm_as_estadoaprobacion: 4 });
    await dormir(15000); // el reparto asíncrono de la cerrada
    comp = await compartidos(espId);
    chk('S7B-03', comp.includes(U.adm), `cerrada: el Admin conserva el compartido (es lo que S-7 b1 tiene que quitar): ${comp.includes(U.adm) ? 'sí' : 'no'}`);

    r = await como('POST', 'sanic_cm_SolicitarEspecial', { ...base, Notas: 'VIP — QA S-7 b1 reactivada' }, U.sol);
    comp = await compartidos(espId);
    chk('S7B-04', r.ok && r.json.Resultado === 'reactivada' && !comp.includes(U.adm),
      `User2 la reactiva: ${r.json?.Resultado || r.msg} · el Admin ya no tiene el compartido: ${comp.includes(U.adm) ? 'NO, sigue' : 'sí'}`);

    r = await aprobar(espId);
    esp = await esperarCobrador(espId);
    comp = await compartidos(espId);
    const ve = await datos.leerComo(U.sol, `sanic_cm_tbl_asignacions(${espId})?$select=sanic_cm_as_estado`);
    chk('S7B-05', r.ok && esp._sanic_cm_as_solicitadopor_value === U.sol && comp.includes(U.sol) && ve.status === 200,
      `aprobada otra vez: compartida con User2 y User2 la lee (${ve.status})`);
    chk('S7B-06', !comp.includes(U.adm), `el reparto no se la vuelve a compartir al Admin: ${comp.includes(U.adm) ? 'NO' : 'sí'}`);
  } finally {
    const l = await datos.limpiar();
    console.log('  limpieza restantes', JSON.stringify(l.restantes || {}).slice(0, 220));
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
