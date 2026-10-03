// FASE F — User4 de vuelta a SOLO Admin (R-06b) + D-07: una fila QA de carga diaria con FechaCarga = HOY
// para probar Monitoreo/Inicio/Reconciliación "después del import" (sin tocar datos reales).
// La fila QA se borra apenas termina la fase. NUNCA se cierra nada en lote.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const qa = require('../lib/datosqa');
const c = require('./comun');

module.exports = async function faseF(suite) {
  const { cliente, cuentas } = suite.datos;
  const carga = await qa.crearCargaHoy(cliente.sanic_cm_tbl_clienteid, cuentas[0].sanic_cm_tbl_cuentaid, v.hoy().fecha);
  const app = await c.abrir(suite, 'user4', 'F', 'Admin');
  try {
    await inf.correr('R-06b', 'MULTI-ROL revertido: User4 fuera de Backoffice → vuelve a SOLO la nav Admin', 'Admin', async (e) => {
      await c.verificarRol(e, app, ['Admin'], /^Admin$/);
      e.evid(await pl.foto(app, 'R-06b-solo-admin'));
    });

    await inf.correr('D-07', 'Con carga de HOY (1 fila QA): Monitoreo "filas de carga hoy" = API', 'Admin', async (e) => {
      const k = await c.kpisInicio(app, 'Monitoreo de carga', 'scrMonitoreo');
      e.check(k.tibio['CARGA DE HOY'] && k.tibio['CARGA DE HOY'] !== 'sin carga', 'Inicio: CARGA DE HOY muestra la fecha (no "sin carga")', `${v.hoy().fecha.slice(8, 10)}/${v.hoy().fecha.slice(5, 7)} 00:00`, k.tibio['CARGA DE HOY']);
      const desap = await v.desaparecidas();
      e.check(c.num(k.tibio['DIARIAS FUERA DE CARTERA']) === desap, 'Inicio: DIARIAS FUERA DE CARTERA = API con el corte de hoy', desap, k.tibio['DIARIAS FUERA DE CARTERA']);
      await c.ir(app, 'Monitoreo de carga', 'scrMonitoreo(carga hoy)');
      const km = await pl.kpis(app);
      const [real, comoApp] = [await v.cargaHoy(), await v.cargaHoyComoLaApp()];
      e.check(await pl.existeVisible(app, 'conEstadoOkMon'), 'card verde "carga de hoy completada"', true, await pl.existeVisible(app, 'conEstadoOkMon'));
      e.check(c.num(km['FILAS DE CARGA HOY']) === real, 'FILAS DE CARGA HOY = filas con FechaCarga = hoy (API)', real, km['FILAS DE CARGA HOY']);
      const req = app.red.de('F:scrMonitoreo(carga hoy)').flatMap(x => x.subs.map(s => s.ruta)).find(r => /cargadiarias\?\$apply/.test(r));
      e.nota(`request del player: ${req} · API con esa misma condición = ${comoApp} · API con la fecha = ${real}`);
      e.evid(await pl.foto(app, 'D-07-monitoreo-carga-hoy'));
    });

    await inf.correr('F-05b', 'Reconciliación con carga de hoy: % vs API y freno >30% (casilla y botón deshabilitados) — sin cerrar nada', 'Admin', async (e) => {
      await c.ir(app, 'Reconciliación', 'scrReconciliacion(carga hoy)');
      const res = await pl.texto(app, 'lblResumenRec');
      const m = res.match(/^(\d+) de (\d+) Diarias activas \((\d+)%\)/);
      const [desap, dia] = [await v.desaparecidas(), await v.diariasActivas()];
      const pct = Math.round(100 * desap / Math.max(1, dia));
      e.check(m && Number(m[1]) === desap && Number(m[2]) === dia && Number(m[3]) === pct, 'resumen "N de M (pct%)" = API', `${desap} de ${dia} (${pct}%)`, res);
      e.check(!(await pl.existeVisible(app, 'conBloqueoRec')), 'card ⛔ oculta (ya hay carga de hoy)', false, true);
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
