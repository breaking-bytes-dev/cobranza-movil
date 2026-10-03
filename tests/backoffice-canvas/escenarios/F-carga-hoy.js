// FASE F — User4 en su rol REAL (Admin y Carga):
//  1) F-04: cierra la Especial QA aprobada en C (motivo + detalle).
//  2) D-07 / F-05b / B-12: una fila QA de carga diaria con FechaCarga = HOY para probar Monitoreo, Inicio y
//     Reconciliación "después del import" (sin tocar datos reales). La fila QA se borra apenas termina la fase.
//     NUNCA se ejecuta el cierre en lote: solo se lee su estado y su texto.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const qa = require('../lib/datosqa');
const c = require('./comun');

// Abre en el panel de Cierres la fila QA cuya cuenta es `cuenta`.
async function abrirCierreQA(app, cuenta) {
  const filas = pl.ctl(app, 'btnFilaCie').locator('visible=true').filter({ hasText: 'QA-E2E CLIENTE UNO' });
  const n = await filas.count();
  for (let i = 0; i < n; i++) {
    await filas.nth(i).click(); await pl.esperar(app, 2000);
    if ((await pl.texto(app, 'lblV2Cie').catch(() => '')) === cuenta) return true;
    await (await pl.boton(app, 'btnCancelarCie')).click().catch(() => {}); await pl.esperar(app, 1000);
  }
  return false;
}

module.exports = async function faseF(suite) {
  const { cliente, cuentas } = suite.datos;
  // ── 1) F-04 ───────────────────────────────────────────────────────────────────────────
  const app1 = await c.abrir(suite, 'user4', 'F', 'Admin');
  try {
    await inf.correr('F-04', 'Admin cierra la Especial QA (de F-02/F-03) con motivo + detalle', 'Admin', async (e) => {
      await c.ir(app1, 'Cierres', 'scrCierres');
      app1.red.marcar('F:scrCierres(cerrar)');
      const esp = suite.datos.especialF02;
      if (!esp) throw new Error('No hay Especial QA de F-02 para cerrar');
      await (await pl.boton(app1, 'btnTabEspCie')).click(); await pl.esperar(app1, 1500);
      const ok = await abrirCierreQA(app1, 'QA-E2E-CTA-0001');
      if (!ok) throw new Error('No se encontró la Especial QA en la pestaña Especiales');
      const cta = await pl.texto(app1, 'lblV2Cie');
      if (cta !== 'QA-E2E-CTA-0001') throw new Error(`Seguridad: panel con cuenta no QA (${cta})`);
      e.check(!(await pl.botonHabilitado(app1, 'btnCerrarCie')), 'Cerrar deshabilitado sin motivo', 'Disabled', 'Edit');
      const r = await c.elegirCombo(app1, 'cmbMotivoCie', { opcion: 'Gestión completada' });
      e.check(r.ok && r.textos.join('|') === 'Gestión completada|Ya no aplica|Solicitud errónea', 'motivos de Especial', 'Gestión completada|Ya no aplica|Solicitud errónea', r.textos.join('|'));
      await pl.escribir(app1, 'txtDetalleCie', 'QA-E2E cierre F-04');
      e.check(await pl.botonHabilitado(app1, 'btnCerrarCie'), 'Cerrar habilitado con motivo', true, false);
      await (await pl.boton(app1, 'btnCerrarCie')).click();
      const n = await c.esperarNotif(app1, /Asignación cerrada|No se pudo/i);
      e.check(/Asignación cerrada — Gestión completada/.test(n || ''), 'Notify de cierre', '🔒 Asignación cerrada — Gestión completada', n);
      await app1.red.quieta(app1.page, { quietoMs: 4000, maxMs: 30000 });
      const a = await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=sanic_cm_as_estado,sanic_cm_as_motivo,sanic_cm_as_fechacierre`);
      e.check(a.sanic_cm_as_estado === false, 'Dataverse: [AS] Estado = false', false, a.sanic_cm_as_estado);
      e.check(a.sanic_cm_as_motivo === 'Gestión completada — QA-E2E cierre F-04', 'Dataverse: motivo = "Motivo — detalle"', 'Gestión completada — QA-E2E cierre F-04', a.sanic_cm_as_motivo);
      e.check(a.sanic_cm_as_fechacierre && a.sanic_cm_as_fechacierre >= v.hoy().inicioUTC, 'Dataverse: fecha de cierre = hoy', `>= ${v.hoy().inicioUTC}`, a.sanic_cm_as_fechacierre);
      const escrituras = app1.red.de('F:scrCierres(cerrar)').flatMap(x => x.subs).filter(s => s.metodo !== 'GET').length;
      e.check(escrituras === 1, '1 sola escritura (Patch) al cerrar', 1, escrituras);
      e.evid(await pl.foto(app1, 'F-04-cerrada'));
    });
  } finally { await pl.cerrar(app1); }

  // ── 2) Carga de hoy ───────────────────────────────────────────────────────────────────
  const carga = await qa.crearCargaHoy(cliente.sanic_cm_tbl_clienteid, cuentas[0].sanic_cm_tbl_cuentaid, v.hoy().fecha);
  const app = await c.abrir(suite, 'user4', 'F2', 'Admin (carga de hoy)');
  try {
    await inf.correr('D-07', 'Con carga de HOY (1 fila QA): Inicio y Monitoreo "filas de carga hoy" = API (B-07)', 'Admin', async (e) => {
      const k = await c.kpisInicio(app, 'Monitoreo de carga', 'scrMonitoreo');
      e.check(k.frio['CARGA DE HOY'] && k.frio['CARGA DE HOY'] !== 'sin carga', 'Inicio (primera carga): CARGA DE HOY muestra la fecha (no "sin carga")', `${v.hoy().fecha.slice(8, 10)}/${v.hoy().fecha.slice(5, 7)} …`, k.frio['CARGA DE HOY']);
      const desap = await v.desaparecidas();
      e.check(c.num(k.tibio['DIARIAS FUERA DE CARTERA']) === desap, 'Inicio: DIARIAS FUERA DE CARTERA = API con el corte de hoy', desap, k.tibio['DIARIAS FUERA DE CARTERA']);
      await c.ir(app, 'Monitoreo de carga', 'scrMonitoreo(carga hoy)');
      const km = await pl.kpis(app);
      const real = await v.cargaHoy();
      e.check(await pl.existeVisible(app, 'conEstadoOkMon'), 'card verde "carga de hoy completada"', true, await pl.existeVisible(app, 'conEstadoOkMon'));
      e.check(c.num(km['FILAS DE CARGA HOY']) === real, 'FILAS DE CARGA HOY = filas con FechaCarga = hoy (API)', real, km['FILAS DE CARGA HOY']);
      const req = app.red.entradas.filter(x => /scrMonitoreo/.test(x.pantalla)).flatMap(x => x.subs.map(s => s.ruta)).filter(r => /cargadiarias\?\$apply/.test(r)).pop();
      e.nota(`request del player: ${req} · API con la fecha = ${real}`);
      e.check(/sanic_cm_cd_fechacarga ge \d{4}-\d{2}-\d{2}T00:00:00/.test(req || ''), 'el filtro viaja contra 00:00Z (no contra 06:00Z)', 'ge <hoy>T00:00:00Z', req);
      e.evid(await pl.foto(app, 'D-07-monitoreo-carga-hoy'));
    });

    await inf.correr('F-05b', 'Reconciliación con carga de hoy: % vs API, freno >30% y lista COMPLETA de desaparecidas sin tope de 500 (B-12) — sin cerrar nada', 'Admin', async (e) => {
      await c.ir(app, 'Reconciliación', 'scrReconciliacion(carga hoy)');
      const res = await pl.texto(app, 'lblResumenRec');
      const m = res.match(/^([\d,]+) de ([\d,]+) Diarias activas \((\d+)%\)/);
      const [desap, dia] = [await v.desaparecidas(), await v.diariasActivas()];
      const pct = Math.round(100 * desap / Math.max(1, dia));
      e.check(m && c.num(m[1]) === desap && c.num(m[2]) === dia && Number(m[3]) === pct, 'resumen "N de M (pct%)" = API', `${desap} de ${dia} (${pct}%)`, res);
      e.check(!(await pl.existeVisible(app, 'conBloqueoRec')), 'card ⛔ oculta (ya hay carga de hoy)', false, true);
      // B-12: la colección debe traer TODAS las desaparecidas (≤ 2000), sin el aviso "Mostrando las primeras N".
      const tope = await pl.existeVisible(app, 'lblTopeRec');
      e.check(!tope, 'sin aviso "Mostrando las primeras …"', 'oculto', tope ? await pl.texto(app, 'lblTopeRec') : 'oculto');
      const txtBtn = (await pl.existeVisible(app, 'btnCerrarRec')) ? await pl.texto(app, 'btnCerrarRec') : '';
      const nLista = c.num((txtBtn.match(/Cerrar ([\d,]+) asignaciones/) || [])[1]);
      e.check(nLista === Math.min(desap, 2000), 'CountRows(colDesapRec) (texto del botón, NO se pulsa) = desaparecidas API', Math.min(desap, 2000), txtBtn);
      const paginas = app.red.de('F2:scrReconciliacion(carga hoy)').flatMap(x => x.subs.map((s, i) => ({ s, r: x.respuestas?.[i] })))
        .filter(({ s }) => s.metodo === 'GET' && /^sanic_cm_tbl_asignacions\?/.test(s.ruta) && !/\$apply/.test(s.ruta));
      e.nota(`páginas de la lista en la red: ${paginas.length} · filas ${paginas.map(p => p.r?.filas).join('+')} · nextLink en la última: ${paginas.slice(-1)[0]?.r?.nextLink || false}`);
      if (await pl.existeVisible(app, 'chkConfirmarRec')) {
        const chkDis = await c.chkDeshabilitado(app, 'chkConfirmarRec');
        e.check(pct > 30 ? chkDis : true, `casilla ${pct > 30 ? 'DESHABILITADA por umbral rojo' : 'habilitable'}`, pct > 30, chkDis);
        e.check(!(await pl.botonHabilitado(app, 'btnCerrarRec')), 'botón de cierre en lote DESHABILITADO (no se arma sin casilla)', 'Disabled', 'Edit');
      }
      e.evid(await pl.foto(app, 'F-05b-reconciliacion-carga-hoy'));
    });
  } finally {
    await pl.cerrar(app);
    const st = await dv.del('sanic_cm_tbl_cargadiarias', carga.sanic_cm_tbl_cargadiariaid);
    console.log(`   🧹 fila QA de carga de hoy borrada (HTTP ${st})`);
  }
};
