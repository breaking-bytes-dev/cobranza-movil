// Fase 5, Supervisor (User3): cola de Aprobaciones, aprobar C y A-Especial con localizador, rechazar B-Especial.
// Fase 6, reparto tras aprobar (plugin RepartoAsignacion): las aprobadas pasan a User5, la rechazada no.
const dv = require('../../backoffice-canvas/lib/dataverse');
const v = require('../../backoffice-canvas/lib/verdad');
const { AGENTE, U, esperar } = require('../lib/ctx');
const ui = require('../lib/ui');
const datos = require('../lib/datos');
const { pl, c } = ui;

// Abre en el panel la fila de la cola cuyo título (N° de asignación) es `numero`. Devuelve true/false.
async function abrirFila(ctx, app, numero) {
  if (await pl.existeVisible(app, 'icoCerrarAprb')) { await (await pl.visible(app, 'icoCerrarAprb')).click().catch(() => {}); await pl.esperar(app, 1200); }
  const filas = pl.ctl(app, 'btnFilaAprb').locator('visible=true').filter({ hasText: ctx.nombreCliente });
  const n = await filas.count();
  for (let i = 0; i < n; i++) {
    await filas.nth(i).click(); await pl.esperar(app, 2000);
    const tit = await pl.texto(app, 'lblPanelTituloAprb').catch(() => '');
    if (tit.includes(numero)) return true;
    await (await pl.visible(app, 'icoCerrarAprb')).click().catch(() => {}); await pl.esperar(app, 1000);
  }
  return false;
}

// Sondea la asignación en Dataverse (Web API) hasta que `listo(fila)` sea verdadero o venza el plazo; devuelve la última lectura.
// Es la CONFIRMACIÓN del resultado: el aviso (Notify) dura pocos segundos y el sondeo del navegador puede no verlo,
// mientras que el dato en Dataverse no se pierde.
const SEL_ASIG = '$select=sanic_cm_as_estadoaprobacion,sanic_cm_as_motivorechazo,_sanic_cm_as_agenteid_value,_sanic_cm_as_resolutorid_value';
async function esperarAsig(id, listo, ms = 60000) {
  let fila = {};
  await esperar(async () => {
    try { fila = await dv.get(`sanic_cm_tbl_asignacions(${id})?${SEL_ASIG}`); return listo(fila); } catch (e) { return false; }
  }, { maxMs: ms, cadaMs: 3000 });
  return fila;
}

// Chequeo del aviso con el dato como árbitro: si el aviso se vio, tiene que decir lo esperado (un aviso de error o con otro texto
// tumba la prueba); si NO se capturó ningún aviso pero el dato sí llegó a Dataverse, no se tumba: queda como observación en el log.
function chkAviso(ctx, id, n, re, desc, datoLlego) {
  const visto = !!n && !/^\(otras:/.test(n);
  if (visto) return ctx.chk(id, re.test(n), `${desc}: aviso "${n}"`);
  if (datoLlego) {
    ctx.nota(`${id}: no se capturó el aviso de la app (${n || 'ninguno visible'}) pero el dato llegó a Dataverse; se confirma por el dato`);
    return ctx.chk(id, true, `${desc}: aviso no capturado, resultado confirmado en Dataverse`);
  }
  return ctx.chk(id, false, `${desc}: aviso "${n}" y el dato no llegó a Dataverse`);
}

async function aprobar(ctx, app, id, e, numero, etiqueta) {
  app.red.marcar(`F5:scrAprobaciones(${etiqueta})`);
  ctx.requiere(await abrirFila(ctx, app, numero), `la Especial ${numero} no está en la cola de Aprobaciones`);
  ctx.chk(`${id}a`, !(await pl.botonHabilitado(app, 'btnAprobarAprb')), `${etiqueta}: "Aprobar" deshabilitado sin localizador`);
  const r = await c.elegirEnBuscador(app, { buscador: 'txtBuscarAgeAprb', galeria: 'galAgeAprb', cambiar: 'btnCambiarAgeAprb', buscar: AGENTE, opcion: AGENTE });
  ctx.requiere(r.ok, `la búsqueda "${AGENTE}" no ofreció al localizador (${r.textos.slice(0, 3).join(' / ')})`);
  const tarjeta = await pl.texto(app, 'lblAgeSelAprb').catch(() => '');
  ctx.chk(`${id}b`, tarjeta.includes(AGENTE) && (await pl.botonHabilitado(app, 'btnAprobarAprb')), `${etiqueta}: localizador elegido ("${tarjeta.slice(0, 40)}") y "Aprobar" habilitado`);
  await (await pl.boton(app, 'btnAprobarAprb')).click();
  e.tAprob = Date.now();
  const n = await c.esperarNotif(app, /Aprobada|error/i, 30000);
  await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
  return n;
}

async function f5(ctx) {
  const D = ctx.datos; ctx.requiere(D.cuentas?.every((e) => e.asigId), 'faltan datos de F1');
  const [A, B, Cc] = ['A', 'B', 'C'].map((k) => D.cuentas.find((e) => e.clave === k));
  ctx.requiere(A.espNum && B.espNum, 'faltan las Especiales de F4');
  const app = await ui.abrirBackoffice('user3', 'F5');
  ctx.capturador = ui.capturador(ctx, app);
  try {
    await ctx.paso('F5-01', 'cola', async () => {
      // El KPI se llena en el OnVisible de Inicio: se espera a que coincida con la API y se mantenga (no una sola lectura).
      const leer = async () => { const k = await pl.kpis(app).catch(() => ({})); return { k, porAprobar: await v.porAprobar() }; };
      const { k, porAprobar } = await ui.esperarEstable(leer, { timeout: 45000, intervalo: 1000,
        listo: (x) => c.num(x.k['POR APROBAR']) === x.porAprobar });
      ctx.chk('F5-01', c.num(k['POR APROBAR']) === porAprobar && porAprobar >= 3, `Inicio Supervisor: "POR APROBAR" = API (${k['POR APROBAR']} vs ${porAprobar}; incluye las 3 Especiales de la corrida)`);
      await ui.ir(app, 'Aprobaciones', 'scrAprobaciones');
      const filas = (await pl.ctl(app, 'lblFilaClienteAprb').locator('visible=true').allInnerTexts()).map((s) => s.trim()).filter((t) => t === ctx.nombreCliente);
      ctx.chk('F5-02', filas.length === 3, `Aprobaciones: la cola trae las 3 Especiales de "${ctx.nombreCliente}": la del CSV (${Cc.asigNum}) y las 2 de User2 (${A.espNum}, ${B.espNum}) — filas ${filas.length}`);
      await ui.captura(ctx, app, 'cola');
    });
    await ctx.paso('F5-03', 'aprobar C', async () => {
      const n = await aprobar(ctx, app, 'F5-03', Cc, Cc.asigNum, 'C (CSV)');
      const a = await esperarAsig(Cc.asigId, (x) => x.sanic_cm_as_estadoaprobacion === 3);
      chkAviso(ctx, 'F5-03', n, /Aprobada — asignada a/, 'aprobar C', a.sanic_cm_as_estadoaprobacion === 3);
      ctx.chk('F5-04', a.sanic_cm_as_estadoaprobacion === 3 && a._sanic_cm_as_agenteid_value === D.agente.id && a._sanic_cm_as_resolutorid_value === U.sup,
        `Dataverse C ${Cc.asigNum}: Aprobado (3) con "${AGENTE}", resolutor User3 (estado ${a.sanic_cm_as_estadoaprobacion})`);
    });
    await ctx.paso('F5-05', 'aprobar A-Especial', async () => {
      const n = await aprobar(ctx, app, 'F5-05', A, A.espNum, 'A (Especial de User2)');
      const a = await esperarAsig(A.espId, (x) => x.sanic_cm_as_estadoaprobacion === 3);
      chkAviso(ctx, 'F5-05', n, /Aprobada — asignada a/, 'aprobar A-Especial', a.sanic_cm_as_estadoaprobacion === 3);
      ctx.chk('F5-06', a.sanic_cm_as_estadoaprobacion === 3 && a._sanic_cm_as_agenteid_value === D.agente.id && a._sanic_cm_as_resolutorid_value === U.sup,
        `Dataverse A-Especial ${A.espNum}: Aprobado (3) con "${AGENTE}", resolutor User3 (estado ${a.sanic_cm_as_estadoaprobacion})`);
    });
    await ctx.paso('F5-07', 'rechazar B-Especial', async () => {
      app.red.marcar('F5:scrAprobaciones(rechazar)');
      ctx.requiere(await abrirFila(ctx, app, B.espNum), `la Especial ${B.espNum} no está en la cola`);
      ctx.chk('F5-07', !(await pl.botonHabilitado(app, 'btnRechazarAprb')), '"Rechazar" deshabilitado sin motivo');
      await pl.escribir(app, 'txtMotRechAprb', `${ctx.nombreCliente} no corresponde`);
      ctx.chk('F5-08', await pl.botonHabilitado(app, 'btnRechazarAprb'), '"Rechazar" habilitado con motivo');
      await (await pl.boton(app, 'btnRechazarAprb')).click();
      const n = await c.esperarNotif(app, /rechazada|error/i, 30000);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const b = await esperarAsig(B.espId, (x) => x.sanic_cm_as_estadoaprobacion === 4);
      chkAviso(ctx, 'F5-09', n, /Solicitud rechazada/, 'rechazar B-Especial', b.sanic_cm_as_estadoaprobacion === 4);
      ctx.chk('F5-10', b.sanic_cm_as_estadoaprobacion === 4 && b.sanic_cm_as_motivorechazo === `${ctx.nombreCliente} no corresponde` && b._sanic_cm_as_resolutorid_value === U.sup && !b._sanic_cm_as_agenteid_value,
        `Dataverse B-Especial ${B.espNum}: Rechazada (4), motivo "${b.sanic_cm_as_motivorechazo}", resolutor User3, sin agente (estado ${b.sanic_cm_as_estadoaprobacion})`);
      const quedan = await pl.ctl(app, 'lblFilaClienteAprb').locator('visible=true').allInnerTexts();
      ctx.chk('F5-11', !quedan.some((t) => t.trim() === ctx.nombreCliente), `la cola ya no tiene filas de "${ctx.nombreCliente}" (aprobadas y rechazada salieron)`);
      await ui.captura(ctx, app, 'cola-vacia');
    });
  } finally { await ui.cerrar(app); }
}

// ── Fase 6 ─────────────────────────────────────────────────────────────────────────────────────────
const ownerDe = async (id) => (await dv.get(`sanic_cm_tbl_asignacions(${id})?$select=_ownerid_value`))._ownerid_value;

async function f6(ctx) {
  const D = ctx.datos; const [A, B, Cc] = ['A', 'B', 'C'].map((k) => D.cuentas.find((e) => e.clave === k));
  ctx.requiere(A?.espId && B?.espId && Cc?.asigId, 'faltan datos de F4/F5');
  const objetivos = [['C (CSV)', Cc.asigId, Cc], ['A-Especial', A.espId, A]];
  const pend = new Set(objetivos.map((o) => o[1])); const seg = {};
  await esperar(async () => {
    for (const [, id, e] of objetivos) if (pend.has(id) && (await ownerDe(id)) === U.cob) { pend.delete(id); seg[id] = Math.round((Date.now() - (e.tAprob || Date.now())) / 1000); }
    return pend.size === 0;
  }, { maxMs: 180000, cadaMs: 8000 });
  objetivos.forEach(([n, id], i) => ctx.chk(`F6-0${i + 1}`, !pend.has(id), `${n}: tras aprobar, el reparto pasó el dueño a User5 ${pend.has(id) ? 'NO ocurrió en 3 min' : `(≤ ${seg[id]} s después de aprobar)`}`));
  const oB = await ownerDe(B.espId);
  ctx.chk('F6-03', oB !== U.cob, `B-Especial rechazada: su dueño NO es User5 (sigue siendo ${oB === U.sol ? 'User2' : oB})`);
  const lee = async (e) => (await datos.leerComo(U.cob, `sanic_cm_tbl_cuentas(${e.id})?$select=sanic_cm_tbl_cuentaid`)).status;
  ctx.chk('F6-04', (await lee(Cc)) === 200 && (await lee(A)) === 200 && (await lee(B)) === 200, `User5 (suplantado) lee ahora las 3 cuentas (C ${await lee(Cc)} · A ${await lee(A)} · B ${await lee(B)})`);
}

module.exports = { f5, f6 };
