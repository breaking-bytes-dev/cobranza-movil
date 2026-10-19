// Visita offline de una cuenta reasignada (1.0.8.5, opcion 2 del dueno: tolerancia por fecha de captura), en vivo.
// El Admin da de alta una cuenta QA (AltaCuenta) con el agente del cobrador (User5): el reparto le pasa la asignacion.
// Despues se la reasigna DOS veces (User5 -> User2 -> User3, con el servicio, sin tocar el reparto) y User5 sincroniza
// capturas con distintas fechas, como lo haria el telefono al volver la senal:
//   captura anterior a las dos reasignaciones      -> entra (y su pago tambien)
//   captura entre la primera y la segunda          -> rechazada (ya era de User2)
//   captura posterior a las dos                    -> rechazada
//   captura de hace mas de 48 h / futura / sin fecha -> rechazada (regla estricta)
// Ademas: lo aceptado queda de User5 (creador) y el Supervisor lo lee. Limpieza con el prefijo QA. Sin PAN impreso.
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
const runid = 'OFF' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`;
const como = async (metodo, rel, cuerpo, usuario) => {
  try { const r = await dv.raw(metodo, rel, cuerpo, { MSCRMCallerID: usuario }); return { ok: true, status: r.status, json: r.json }; }
  catch (e) { return { ok: false, status: e.status, msg: sinPAN(e.message) }; }
};
const iso = (ms) => new Date(ms).toISOString();

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
    const ctaId = cta.sanic_cm_tbl_cuentaid;

    let asg;
    for (let i = 0; i < 60; i++) {
      await dormir(5000);
      asg = (await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid,_ownerid_value&$filter=_sanic_cm_as_cuentaid_value eq ${ctaId}`)).value[0];
      if (asg && asg._ownerid_value === U.cob) break;
    }
    chk('OFF-01', asg && asg._ownerid_value === U.cob, `la asignación QA es de User5 (reparto): ${asg && asg._ownerid_value === U.cob ? 'sí' : 'no'}`);
    const asgId = asg.sanic_cm_tbl_asignacionid;
    await dormir(15000);

    const cuerpo = (n, captura) => ({
      sanic_cm_in_comentario: `${PREFIJO} ${runid} ${n}`, sanic_cm_in_nombreagente: `${PREFIJO} agente`, sanic_cm_in_estadograbacion: 1,
      ...(captura ? { sanic_cm_in_fechacaptura: captura } : {}),
      'sanic_cm_in_cuentaid@odata.bind': `/sanic_cm_tbl_cuentas(${ctaId})`,
      'sanic_cm_in_agente@odata.bind': `/sanic_cm_tbl_agentes(${agente.sanic_cm_tbl_agenteid})`,
    });
    const reasignar = (a) => dv.patch('sanic_cm_tbl_asignacions', asgId, { 'ownerid@odata.bind': `/systemusers(${a})` });

    // Linea de tiempo (reloj local; separaciones de 15 s para que la auditoria del servidor las distinga)
    const tAntes = Date.now();
    await dormir(15000);
    await reasignar(U.sol);                       // User5 -> User2
    await dormir(15000);
    const tEntre = Date.now();
    await dormir(15000);
    await reasignar(U.sup);                       // User2 -> User3
    await dormir(15000);
    const tDespues = Date.now();
    // La auditoria de Dataverse se escribe con demora (de segundos a algunos minutos): en el telefono la visita se sincroniza
    // horas despues de la reasignacion, asi que aca se espera a que ambos cambios esten auditados antes de sincronizar.
    const rotulo = (id) => (id === U.cob ? 'User5' : id === U.sol ? 'User2' : id === U.sup ? 'User3' : String(id || '').slice(0, 8));
    const historia = async () => {
      const h = await dv.get(`RetrieveAttributeChangeHistory(Target=@t,AttributeLogicalName='ownerid',PagingInfo=@p)?@t=${encodeURIComponent(JSON.stringify({ '@odata.id': `sanic_cm_tbl_asignacions(${asgId})` }))}&@p=${encodeURIComponent(JSON.stringify({ Count: 50, PageNumber: 1 }))}`);
      return h.AuditDetailCollection.AuditDetails.filter((d) => d.OldValue || d.NewValue)
        .map((d) => `${rotulo(d.OldValue && d.OldValue._ownerid_value)}->${rotulo(d.NewValue && d.NewValue._ownerid_value)}`);
    };
    let audit = [];
    const t0 = Date.now();
    while (Date.now() - t0 < 600000) { audit = await historia(); if (audit.includes('User2->User3') && audit.includes('User5->User2')) break; await dormir(15000); }
    console.log(`  auditoría de ownerid (tras ${Math.round((Date.now() - t0) / 1000)} s de espera): ${audit.join(' · ')}`);
    const dueno = (await dv.get(`sanic_cm_tbl_asignacions(${asgId})?$select=_ownerid_value`))._ownerid_value;
    chk('OFF-02', dueno === U.sup, `reasignada dos veces; dueño actual User3: ${dueno === U.sup ? 'sí' : 'no'}`);

    // 1) antes de las dos reasignaciones: entra
    let r = await como('POST', 'sanic_cm_tbl_interaccions', cuerpo('A', iso(tAntes)), U.cob);
    if (r.ok && !r.json) {
      const f = (await dv.get(`sanic_cm_tbl_interaccions?$select=sanic_cm_tbl_interaccionid&$filter=sanic_cm_in_comentario eq '${PREFIJO} ${runid} A'`)).value[0];
      r.json = f || {};
    }
    chk('OFF-03', r.ok, `captura anterior a las reasignaciones: ${r.ok ? 'aceptada ' + r.status : r.msg.slice(0, 200)}`);
    if (r.ok) {
      const intId = r.json.sanic_cm_tbl_interaccionid;
      const i = await dv.get(`sanic_cm_tbl_interaccions(${intId})?$select=_ownerid_value,sanic_cm_in_estadograbacion`);
      chk('OFF-04', i._ownerid_value === U.cob && i.sanic_cm_in_estadograbacion === 1, `la interacción es de User5 y nace Pendiente de grabar: ${i._ownerid_value === U.cob ? 'dueño User5' : 'otro dueño'}, estado ${i.sanic_cm_in_estadograbacion}`);
      const p = await como('POST', 'sanic_cm_tbl_pagos', { sanic_cm_pg_monto: 1, sanic_cm_pg_moneda: 1, sanic_cm_pg_numerorecibo: `${PREFIJO}-${runid}-R1`,
        sanic_cm_pg_fecha: iso(tAntes), 'sanic_cm_pg_interaccion@odata.bind': `/sanic_cm_tbl_interaccions(${intId})` }, U.cob);
      chk('OFF-05', p.ok, `el pago colgado de esa visita entra: ${p.ok ? p.status : p.msg.slice(0, 200)}`);
      if (p.ok) {
        const pg = (await dv.get(`sanic_cm_tbl_pagos?$select=_sanic_cm_pg_cuentaid_value&$filter=sanic_cm_pg_numerorecibo eq '${PREFIJO}-${runid}-R1'`)).value[0];
        chk('OFF-06', pg._sanic_cm_pg_cuentaid_value === ctaId, `el pago hereda la cuenta de la interacción: ${pg._sanic_cm_pg_cuentaid_value === ctaId ? 'sí' : 'no'}`);
      }
      const sup = await datos.leerComo(U.sup, `sanic_cm_tbl_interaccions(${intId})?$select=sanic_cm_in_comentario`);
      chk('OFF-07', sup.status === 200, `el Supervisor (User3) la lee: ${sup.status}`);
    }

    // 2) rechazos
    r = await como('POST', 'sanic_cm_tbl_interaccions', cuerpo('B', iso(tEntre)), U.cob);
    chk('OFF-08', !r.ok && /cartera/.test(r.msg), `captura entre las dos reasignaciones (era de User2): ${r.ok ? 'PASÓ' : r.msg.slice(0, 120)}`);
    r = await como('POST', 'sanic_cm_tbl_interaccions', cuerpo('C', iso(tDespues)), U.cob);
    chk('OFF-09', !r.ok && /cartera/.test(r.msg), `captura posterior a las dos reasignaciones: ${r.ok ? 'PASÓ' : r.msg.slice(0, 120)}`);
    r = await como('POST', 'sanic_cm_tbl_interaccions', cuerpo('D', iso(Date.now() - 49 * 3600 * 1000)), U.cob);
    chk('OFF-10', !r.ok && /cartera/.test(r.msg), `captura de hace 49 h (fuera de la ventana: rige lo estricto): ${r.ok ? 'PASÓ' : r.msg.slice(0, 120)}`);
    r = await como('POST', 'sanic_cm_tbl_interaccions', cuerpo('E', iso(Date.now() + 10 * 60 * 1000)), U.cob);
    chk('OFF-11', !r.ok && /cartera/.test(r.msg), `captura 10 min en el futuro: ${r.ok ? 'PASÓ' : r.msg.slice(0, 120)}`);
    r = await como('POST', 'sanic_cm_tbl_interaccions', cuerpo('F', null), U.cob);
    chk('OFF-12', !r.ok && /cartera/.test(r.msg), `sin fecha de captura: ${r.ok ? 'PASÓ' : r.msg.slice(0, 120)}`);
  } finally {
    const l = await datos.limpiar();
    console.log('  limpieza restantes', JSON.stringify(l.restantes || {}).slice(0, 220));
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
