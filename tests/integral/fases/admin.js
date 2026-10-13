// F3 Admin (User4): lo que ve tras la carga · F9 Admin (User4): cierres + revocación del compartido.
// SEGURIDAD: en Reconciliación NO se pulsa nada (ni casilla ni cierre en lote). En Agentes NO se vincula ni desvincula.
const dv = require('../../backoffice-canvas/lib/dataverse');
const v = require('../../backoffice-canvas/lib/verdad');
const { AGENTE, U, esperar } = require('../lib/ctx');
const ui = require('../lib/ui');
const datos = require('../lib/datos');
const { pl, c } = ui;

const mascara = (e) => `•••• ${e.ult4}`;

// ── F3 ─────────────────────────────────────────────────────────────────────────────────────────
async function f3(ctx) {
  const D = ctx.datos; ctx.requiere(D.cuentas?.every((e) => e.id), 'F1 no dejó las cuentas');
  const dd = ctx.hoy.fecha.slice(8, 10), mm = ctx.hoy.fecha.slice(5, 7);
  const selAg = 'sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre,sanic_cm_ag_emailusuario,_sanic_cm_ag_usuarioid_value,sanic_cm_ag_activo&$orderby=sanic_cm_ag_nombre';
  const agAntes = (await dv.getAll(selAg)).map((a) => [a.sanic_cm_ag_nombre, a.sanic_cm_ag_emailusuario, a._sanic_cm_ag_usuarioid_value, a.sanic_cm_ag_activo]);
  const app = await ui.abrirBackoffice('user4', 'F3');
  ctx.capturador = ui.capturador(ctx, app);
  try {
    await ctx.paso('F3-01', 'Inicio', async () => {
      const k = await pl.kpis(app);
      const carga = k['CARGA DE HOY'] || '';
      ctx.chk('F3-01', carga && carga !== 'sin carga' && carga.includes(`${dd}/${mm}`), `Inicio: "CARGA DE HOY" muestra la fecha de hoy ("${carga}", esperaba ${dd}/${mm})`);
      const desap = await v.desaparecidas();
      ctx.chk('F3-02', c.num(k['DIARIAS FUERA DE CARTERA']) === desap, `Inicio: "DIARIAS FUERA DE CARTERA" = API (${k['DIARIAS FUERA DE CARTERA']} vs ${desap})`);
    });
    await ctx.paso('F3-03', 'Monitoreo', async () => {
      await ui.ir(app, 'Monitoreo de carga', 'scrMonitoreo');
      const k = await pl.kpis(app);
      const [hoy, cuentas, clientes] = [await v.cargaHoy(), await v.cuentas(), await v.clientes()];
      ctx.chk('F3-03', c.num(k['FILAS DE CARGA HOY']) === hoy && hoy >= 3, `Monitoreo: "FILAS DE CARGA HOY" = API (${k['FILAS DE CARGA HOY']} vs ${hoy}; las 3 de la corrida están incluidas)`);
      ctx.chk('F3-04', c.num(k['CUENTAS TOTALES']) === cuentas && c.num(k['CLIENTES TOTALES']) === clientes, `Monitoreo: cuentas y clientes totales = API (${k['CUENTAS TOTALES']} / ${k['CLIENTES TOTALES']} vs ${cuentas} / ${clientes})`);
      ctx.chk('F3-05', await pl.existeVisible(app, 'conEstadoOkMon'), 'Monitoreo: card verde "carga de hoy completada"');
      await ui.captura(ctx, app, 'monitoreo');
    });
    await ctx.paso('F3-06', 'Reconciliación', async () => {
      await ui.ir(app, 'Reconciliación', 'scrReconciliacion');
      const res = await pl.texto(app, 'lblResumenRec');
      const m = res.match(/^([\d,]+) de ([\d,]+) Diarias activas \((\d+)%\)/);
      const [desap, dia] = [await v.desaparecidas(), await v.diariasActivas()];
      const pct = Math.round(100 * desap / Math.max(1, dia));
      ctx.chk('F3-06', !!m && c.num(m[1]) === desap && c.num(m[2]) === dia && Number(m[3]) === pct, `Reconciliación: "${res}" = API (${desap} de ${dia}, ${pct}%)`);
      ctx.chk('F3-07', !(await pl.existeVisible(app, 'conBloqueoRec')), 'Reconciliación: la card "todavía no llegó la carga de HOY" NO se ve (ya hay carga de hoy)');
      // Estado bloqueado: umbral > 30% (efecto esperado de cargar con fecha de hoy). SOLO lectura: nada se pulsa.
      if (pct > 30) {
        const umbral = await pl.texto(app, 'lblUmbralRec').catch(() => '');
        ctx.chk('F3-08', /Umbral superado/.test(umbral), `Reconciliación: card de umbral superado (>30%) visible: "${umbral.slice(0, 60)}…"`);
        const chk = await pl.existeVisible(app, 'chkConfirmarRec');
        ctx.chk('F3-09', chk && (await c.chkDeshabilitado(app, 'chkConfirmarRec')) && !(await pl.botonHabilitado(app, 'btnCerrarRec')), 'Reconciliación: casilla de confirmación y botón de cierre en lote DESHABILITADOS (no se pulsaron)');
      } else {
        ctx.nota(`Reconciliación con ${pct}% de desaparecidas (≤ 30%): el bloqueo por umbral no aplica; solo se verifica que el cierre no esté armado`);
        ctx.chk('F3-08', !(await pl.existeVisible(app, 'btnCerrarRec')) || !(await pl.botonHabilitado(app, 'btnCerrarRec')), 'Reconciliación: el botón de cierre en lote NO está habilitado sin confirmar');
      }
      await ui.captura(ctx, app, 'reconciliacion');
    });
    await ctx.paso('F3-10', 'Agentes', async () => {
      await ui.ir(app, 'Agentes y usuarios', 'scrAgentes');
      const u = (await dv.get(`systemusers(${U.cob})?$select=fullname,internalemailaddress`));
      const lista = await ui.filas(app, 'galAgentesAge').lista();
      const fila = lista.filter({ hasText: AGENTE }).first();
      const txt = (await fila.count()) ? (await fila.innerText()).replace(/\s+/g, ' ').trim() : '';
      ctx.chk('F3-10', !!txt && txt.toLowerCase().includes(u.internalemailaddress.toLowerCase()) && txt.includes(u.fullname),
        `Agentes: "${AGENTE}" se ve vinculado a ${u.fullname} (${u.internalemailaddress.toLowerCase()}) — fila: "${txt.slice(0, 140)}"`);
      const escr = app.red.de('F3:scrAgentes').flatMap((x) => x.subs).filter((s) => s.metodo !== 'GET');
      ctx.chk('F3-11', escr.length === 0, `Agentes: solo mirar la pantalla no escribe nada (${escr.length} escrituras)`);
      await ui.captura(ctx, app, 'agentes');
    });
  } finally { await ui.cerrar(app); }
  const agDesp = (await dv.getAll(selAg)).map((a) => [a.sanic_cm_ag_nombre, a.sanic_cm_ag_emailusuario, a._sanic_cm_ag_usuarioid_value, a.sanic_cm_ag_activo]);
  ctx.chk('F3-12', JSON.stringify(agAntes) === JSON.stringify(agDesp), `los agentes (nombre, correo, usuario, activo) quedaron idénticos tras abrir la pantalla (${agDesp.length} agentes)`);
}

// ── F9 ─────────────────────────────────────────────────────────────────────────────────────────
// Abre en el panel de Cierres la fila del cliente de la corrida cuya cuenta es `mask` (la galería virtualiza: se baja con la rueda).
async function abrirCierre(app, nombre, mask) {
  const gal = pl.ctl(app, 'galCierresCie'); const caja = await gal.boundingBox().catch(() => null);
  const firmaVisibles = async () => (await pl.ctl(app, 'btnFilaCie').locator('visible=true').allInnerTexts()).join('|');
  let ultima = null;
  for (let vuelta = 0; vuelta < 80; vuelta++) {
    const filas = pl.ctl(app, 'btnFilaCie').locator('visible=true').filter({ hasText: nombre });
    const n = await filas.count();
    for (let i = 0; i < n; i++) {
      await filas.nth(i).click(); await pl.esperar(app, 2000);
      if ((await pl.texto(app, 'lblV2Cie').catch(() => '')) === mask) return true;
      await (await pl.boton(app, 'btnCancelarCie')).click().catch(() => {}); await pl.esperar(app, 1000);
    }
    if (!caja) break;
    const firma = await firmaVisibles();
    if (firma === ultima) break;               // la lista ya no se mueve: llegó al final
    ultima = firma;
    await app.page.mouse.move(caja.x + caja.width / 2, caja.y + caja.height / 2);
    await app.page.mouse.wheel(0, caja.height * 0.8); await pl.esperar(app, 1200);
  }
  return false;
}

async function cerrarAsignacion(ctx, app, { id, e, tabEsp, tipoTxt, motivo, detalle }) {
  await (await pl.boton(app, tabEsp ? 'btnTabEspCie' : 'btnTabIncobCie')).click(); await pl.esperar(app, 1500);
  const ok = await abrirCierre(app, ctx.nombreCliente, mascara(e));
  ctx.requiere(ok, `no apareció la ${tipoTxt} de ${mascara(e)} en Cierres`);
  const tipo = await pl.texto(app, 'lblV3Cie').catch(() => '');
  ctx.requiere(tipo === tipoTxt, `el panel abierto es "${tipo}", esperaba ${tipoTxt}`);
  ctx.chk(`${id}a`, !(await pl.botonHabilitado(app, 'btnCerrarCie')), `${tipoTxt} ${e.clave}: "Cerrar" deshabilitado sin motivo`);
  const r = await c.elegirCombo(app, 'cmbMotivoCie', { opcion: motivo });
  ctx.requiere(r.ok, `el motivo "${motivo}" no está en la lista (${r.textos.join('|')})`);
  if (detalle) await pl.escribir(app, 'txtDetalleCie', detalle);
  ctx.chk(`${id}b`, await pl.botonHabilitado(app, 'btnCerrarCie'), `${tipoTxt} ${e.clave}: "Cerrar" habilitado con motivo`);
  await (await pl.boton(app, 'btnCerrarCie')).click();
  const n = await c.esperarNotif(app, /Asignación cerrada|No se pudo/i, 20000);
  await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
  return n;
}

async function f9(ctx) {
  const D = ctx.datos; ctx.requiere(D.cuentas?.every((e) => e.asigId) && D.cuentas.find((e) => e.clave === 'A')?.espId, 'faltan datos de fases previas');
  const [A, B, Cc] = ['A', 'B', 'C'].map((k) => D.cuentas.find((e) => e.clave === k));
  const app = await ui.abrirBackoffice('user4', 'F9');
  ctx.capturador = ui.capturador(ctx, app);
  try {
    await ui.ir(app, 'Cierres', 'scrCierres');
    await ctx.paso('F9-01', 'cerrar Incobrable B', async () => {
      const n = await cerrarAsignacion(ctx, app, { id: 'F9-01', e: B, tabEsp: false, tipoTxt: 'Incobrable', motivo: 'Recuperada' });
      ctx.chk('F9-01c', /Asignación cerrada — Recuperada/.test(n || ''), `Incobrable B: aviso de cierre ("${n}")`);
      const a = await dv.get(`sanic_cm_tbl_asignacions(${B.asigId})?$select=sanic_cm_as_estado,sanic_cm_as_motivo,sanic_cm_as_fechacierre`);
      ctx.chk('F9-02', a.sanic_cm_as_estado === false && a.sanic_cm_as_motivo === 'Recuperada' && a.sanic_cm_as_fechacierre >= ctx.hoy.inicioUTC,
        `Dataverse B ${B.asigNum}: estado cerrada, motivo "${a.sanic_cm_as_motivo}", fecha de cierre ${a.sanic_cm_as_fechacierre}`);
      B.tCierre = Date.now();
    });
    await ctx.paso('F9-03', 'cerrar Especial C', async () => {
      const n = await cerrarAsignacion(ctx, app, { id: 'F9-03', e: Cc, tabEsp: true, tipoTxt: 'Especial', motivo: 'Gestión completada', detalle: 'QA-E2E-INT' });
      ctx.chk('F9-03c', /Asignación cerrada — Gestión completada/.test(n || ''), `Especial C: aviso de cierre ("${n}")`);
      const a = await dv.get(`sanic_cm_tbl_asignacions(${Cc.asigId})?$select=sanic_cm_as_estado,sanic_cm_as_motivo,sanic_cm_as_fechacierre`);
      ctx.chk('F9-04', a.sanic_cm_as_estado === false && a.sanic_cm_as_motivo === 'Gestión completada — QA-E2E-INT' && a.sanic_cm_as_fechacierre >= ctx.hoy.inicioUTC,
        `Dataverse C ${Cc.asigNum}: estado cerrada, motivo "${a.sanic_cm_as_motivo}", fecha de cierre ${a.sanic_cm_as_fechacierre}`);
      Cc.tCierre = Date.now();
    });
  } finally { await ui.cerrar(app); }

  // Revocación del compartido (plugin RepartoAsignacion): User5 (suplantado) deja de leer la cuenta B; A (Diaria activa) sigue.
  const lee = async (e) => (await datos.leerComo(U.cob, `sanic_cm_tbl_cuentas(${e.id})?$select=sanic_cm_tbl_cuentaid`)).status;
  ctx.requiere(B.tCierre, 'la cuenta B no se cerró en Cierres: la revocación no se puede evaluar');
  const t0 = Date.now();
  const sB = await esperar(async () => ((await lee(B)) !== 200 ? true : null), { maxMs: 180000, cadaMs: 10000 });
  const stB = await lee(B), stC = await lee(Cc), stA = await lee(A);
  if (stB === 200 || stC === 200) for (const e of [A, B, Cc]) ctx.nota(`diagnóstico cuenta ${e.clave}: ${await datos.diagnosticoCuenta(e.id)}`);
  ctx.chk('F9-05', !!sB && stB !== 200, `Revocación: User5 ya NO lee la cuenta B (HTTP ${stB}) ${sB ? `≤ ${Math.round((Date.now() - B.tCierre) / 1000)} s después del cierre` : 'NO se revocó en 3 min'}`);
  ctx.chk('F9-06', stA === 200, `User5 sigue leyendo la cuenta A (Diaria activa): HTTP ${stA}`);
  const sC = stC !== 200 || (await esperar(async () => ((await lee(Cc)) !== 200 ? true : null), { maxMs: 120000, cadaMs: 10000 }));
  ctx.chk('F9-07', !!sC, `Revocación: User5 tampoco lee la cuenta C (Especial cerrada) — HTTP ${await lee(Cc)}`);
}

module.exports = { f3, f9 };
