// FASE E — MULTI-ROL (R-06) y cierre de la Especial QA aprobada (F-04).
// User1 (pedido original para R-06) tiene la licencia vencida → se usa User4 con Backoffice + Admin y Carga.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const dv = require('../lib/dataverse');
const v = require('../lib/verdad');
const c = require('./comun');
const cfg = require('../lib/config');
const { flujoTranscripcion, arqueo } = require('./D-backoffice');

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

module.exports = async function faseE(suite) {
  const U = 'user4';
  await c.cambiarTeams(U, { agregar: ['AdminCarga'] }); // ahora: Backoffice + Admin y Carga
  const app = await c.abrir(suite, U, 'E', 'BackOffice|Admin (multi-rol)');
  try {
    await inf.correr('R-06a', 'MULTI-ROL: User4 en Backoffice + Admin → nav combinada e Inicio con ambas secciones', 'BackOffice|Admin', async (e) => {
      await c.verificarRol(e, app, ['BackOffice', 'Admin'], /BackOffice · Admin|Admin · BackOffice/);
      e.evid(await pl.foto(app, 'R-06a-multirol-inicio'));
      // ambas pantallas abren (Backoffice y Admin)
      await c.ir(app, 'Por transcribir', 'scrTranscripcion');
      e.check(await pl.existeVisible(app, 'lblHeadColaTrx'), 'abre la estación de transcripción', true, false);
      await c.ir(app, 'Datos maestros', 'scrMaestros');
      e.check(await pl.existeVisible(app, 'txtBuscarMae'), 'abre Datos maestros', true, false);
    });

    const yo = cfg.usuarios.user4.id;
    if (!suite.datos.transcritoOk) {
      await inf.correr('F-01m', 'Transcripción E2E con Backoffice+Admin (el rol Backoffice solo no puede grabar — ver F-01)', 'BackOffice|Admin', (e) => flujoTranscripcion(e, app, suite, yo));
      if (suite.datos.transcritoOk) await inf.correr('F-01b', 'Transcritas hoy: KPI = API y aparecen las 2 QA con su pago', 'BackOffice|Admin', (e) => arqueo(e, app, yo));
    }

    await inf.correr('F-04', 'Admin cierra la Especial QA (de F-02/F-03) con motivo + detalle', 'Admin', async (e) => {
      await c.ir(app, 'Cierres', 'scrCierres');
      app.red.marcar('E:scrCierres(cerrar)');
      const esp = suite.datos.especialF02;
      if (!esp) throw new Error('No hay Especial QA de F-02 para cerrar');
      await (await pl.boton(app, 'btnTabEspCie')).click(); await pl.esperar(app, 1500);
      const ok = await abrirCierreQA(app, esp.sanic_cm_as_numerocuenta || 'QA-E2E-CTA-0001');
      if (!ok) throw new Error('No se encontró la Especial QA en la pestaña Especiales');
      // SEGURIDAD: el panel muestra la cuenta QA
      const cta = await pl.texto(app, 'lblV2Cie');
      if (!/^QA-E2E-CTA-/.test(cta)) throw new Error(`Seguridad: panel con cuenta no QA (${cta})`);
      const montoPanel = await pl.texto(app, 'lblV4Cie').catch(() => '');
      e.nota(`panel: cuenta ${cta} · saldo vencido ${montoPanel}`);
      e.check(!(await pl.botonHabilitado(app, 'btnCerrarCie')), 'Cerrar deshabilitado sin motivo', 'Disabled', 'Edit');
      const r = await c.elegirCombo(app, 'cmbMotivoCie', { opcion: 'Gestión completada' });
      e.check(r.ok && r.textos.join('|') === 'Gestión completada|Ya no aplica|Solicitud errónea', 'motivos de Especial', 'Gestión completada|Ya no aplica|Solicitud errónea', r.textos.join('|'));
      await pl.escribir(app, 'txtDetalleCie', 'QA-E2E cierre F-04');
      e.check(await pl.botonHabilitado(app, 'btnCerrarCie'), 'Cerrar habilitado con motivo', true, false);
      await (await pl.boton(app, 'btnCerrarCie')).click();
      const n = await c.esperarNotif(app, /Asignación cerrada|error/i);
      e.check(/Asignación cerrada — Gestión completada/.test(n || ''), 'Notify de cierre', '🔒 Asignación cerrada — Gestión completada', n);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const a = await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=sanic_cm_as_estado,sanic_cm_as_motivo,sanic_cm_as_fechacierre`);
      e.check(a.sanic_cm_as_estado === false, 'Dataverse: [AS] Estado = false', false, a.sanic_cm_as_estado);
      e.check(a.sanic_cm_as_motivo === 'Gestión completada — QA-E2E cierre F-04', 'Dataverse: motivo = "Motivo — detalle"', 'Gestión completada — QA-E2E cierre F-04', a.sanic_cm_as_motivo);
      e.check(a.sanic_cm_as_fechacierre && a.sanic_cm_as_fechacierre >= v.hoy().inicioUTC, 'Dataverse: fecha de cierre = hoy', `>= ${v.hoy().inicioUTC}`, a.sanic_cm_as_fechacierre);
      const escrituras = app.red.de('E:scrCierres(cerrar)').flatMap(x => x.subs).filter(s => s.metodo !== 'GET').length;
      e.check(escrituras === 1, '1 sola escritura (Patch) al cerrar', 1, escrituras);
      e.evid(await pl.foto(app, 'F-04-cerrada'));
    });
  } finally {
    await pl.cerrar(app);
    await c.cambiarTeams(U, { quitar: ['Backoffice'] }); // vuelve a SOLO Admin y Carga (su base)
  }
};
