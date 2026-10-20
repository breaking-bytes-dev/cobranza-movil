// F7 (plugins 1.0.8.6, opcion B del dueno): la actualizacion de datos que el telefono sube SIN la interaccion se vincula en
// el servidor por cuenta + fecha de captura, en vivo y como User5 (MSCRMCallerID), igual que la sincronizacion offline:
//   sin interaccion, con cuenta y la captura de una interaccion suya con actualiza info -> entra y queda vinculada
//   sin interaccion y captura sin interaccion (todavia no llego)                        -> rechazada (se reintenta)
//   sin interaccion ni cuenta/fecha                                                     -> rechazada
//   la captura es de una interaccion SIN actualiza info                                 -> rechazada
//   dos interacciones de la misma cuenta y captura                                      -> rechazada (ambigua)
//   con interaccion y sin cuenta (camino web)                                           -> entra y completa la cuenta
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
const runid = 'ADO' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`;
const como = async (metodo, rel, cuerpo, usuario) => {
  try { const r = await dv.raw(metodo, rel, cuerpo, { MSCRMCallerID: usuario, Prefer: 'return=representation' }); return { ok: true, status: r.status, json: r.json }; }
  catch (e) { return { ok: false, status: e.status, msg: sinPAN(e.message) }; }
};
const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z');

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
      asg = (await dv.get(`sanic_cm_tbl_asignacions?$select=_ownerid_value&$filter=_sanic_cm_as_cuentaid_value eq ${ctaId}`)).value[0];
      if (asg && asg._ownerid_value === U.cob) break;
    }
    chk('ADO-00', asg && asg._ownerid_value === U.cob, `la asignación QA es de User5 (reparto): ${asg && asg._ownerid_value === U.cob ? 'sí' : 'no'}`);
    await dormir(15000);

    const interaccion = (n, captura, actualiza) => como('POST', 'sanic_cm_tbl_interaccions', {
      sanic_cm_in_comentario: `${PREFIJO} ${runid} ${n}`, sanic_cm_in_nombreagente: `${PREFIJO} agente`, sanic_cm_in_estadograbacion: 1,
      sanic_cm_in_actualizainfo: actualiza, sanic_cm_in_fechacaptura: captura,
      'sanic_cm_in_cuentaid@odata.bind': `/sanic_cm_tbl_cuentas(${ctaId})`,
      'sanic_cm_in_agente@odata.bind': `/sanic_cm_tbl_agentes(${agente.sanic_cm_tbl_agenteid})`,
    }, U.cob);
    const actualizacion = (extra) => como('POST', 'sanic_cm_tbl_actualizaciondatoses', { sanic_cm_ad_telefonocelular: '88880000', ...extra }, U.cob);
    const conCuenta = (captura) => ({ 'sanic_cm_ad_cuentaid@odata.bind': `/sanic_cm_tbl_cuentas(${ctaId})`, ...(captura ? { sanic_cm_ad_fechacaptura: captura } : {}) });
    const base = Date.now() - 10 * 60000;

    // 1) El caso del telefono: sin interaccion, con cuenta y la misma captura
    const t1 = iso(base);
    const i1 = await interaccion('con datos', t1, true);
    chk('ADO-01', i1.ok, `User5 crea la interacción con actualiza info: ${i1.ok ? 'sí' : i1.msg}`);
    const a1 = await actualizacion(conCuenta(t1));
    const v1 = a1.ok && a1.json._sanic_cm_ad_interaccionid_value === i1.json.sanic_cm_tbl_interaccionid;
    chk('ADO-02', v1, `actualización sin interacción, con cuenta y captura → vinculada a su interacción: ${a1.ok ? (v1 ? 'sí' : 'a otra') : a1.msg}`);
    chk('ADO-03', a1.ok && a1.json._sanic_cm_ad_cuentaid_value === ctaId, 'y conserva su cuenta');

    // 2) Todavia no llego la interaccion
    const a2 = await actualizacion(conCuenta(iso(base - 60 * 60000)));
    chk('ADO-04', !a2.ok && /todavia/.test(a2.msg || ''), `captura sin interacción → rechazada para reintentar: ${a2.ok ? 'ENTRÓ' : 'sí'}`);
    chk('ADO-05', !/88880000/.test(a2.msg || ''), 'el rechazo no cita el teléfono');

    // 3) Sin interaccion ni datos para buscarla
    const a3 = await actualizacion({});
    chk('ADO-06', !a3.ok && /vinculada a una interaccion/.test(a3.msg || ''), `sin interacción ni cuenta → rechazada: ${a3.ok ? 'ENTRÓ' : 'sí'}`);

    // 4) La captura es de una interaccion sin actualiza info
    const t4 = iso(base + 60000);
    const i4 = await interaccion('sin datos', t4, false);
    const a4 = await actualizacion(conCuenta(t4));
    chk('ADO-07', i4.ok && !a4.ok, `la interacción de esa captura no actualiza datos → rechazada: ${a4.ok ? 'ENTRÓ' : 'sí'}`);

    // 5) Ambigua: dos interacciones de la misma cuenta y captura
    const t5 = iso(base + 120000);
    const i5a = await interaccion('doble A', t5, true), i5b = await interaccion('doble B', t5, true);
    const a5 = await actualizacion(conCuenta(t5));
    chk('ADO-08', i5a.ok && i5b.ok && !a5.ok && /mas de una/.test(a5.msg || ''), `dos interacciones con la misma captura → rechazada por ambigua: ${a5.ok ? 'ENTRÓ' : 'sí'}`);

    // 6) Camino web: con la interaccion y sin cuenta → completa la cuenta
    const t6 = iso(base + 180000);
    const i6 = await interaccion('web', t6, true);
    const a6 = await actualizacion({ 'sanic_cm_ad_interaccionid@odata.bind': `/sanic_cm_tbl_interaccions(${i6.json && i6.json.sanic_cm_tbl_interaccionid})` });
    chk('ADO-09', a6.ok && a6.json._sanic_cm_ad_cuentaid_value === ctaId, `con interacción y sin cuenta → entra y completa la cuenta: ${a6.ok ? (a6.json._sanic_cm_ad_cuentaid_value === ctaId ? 'sí' : 'sin cuenta') : a6.msg}`);
  } finally {
    const l = await datos.limpiar();
    console.log('  limpieza restantes', JSON.stringify(l.restantes || {}).slice(0, 220));
    console.log(`\n${ok}/${ok + mal} ${mal ? 'CON FALLAS' : 'OK'}`);
    process.exit(mal ? 1 : 0);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
