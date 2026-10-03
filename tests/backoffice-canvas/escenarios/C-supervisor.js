// FASE C — User3 con su rol BASE (Supervisor): roles, KPI, aprobar la Especial QA de F-02 asignando
// agente (+ verificación del flow de owner Comp-14) y rechazar otra Especial QA creada por API.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const qa = require('../lib/datosqa');
const cfg = require('../lib/config');
const c = require('./comun');

// Abre en el panel la fila QA cuyas NOTAS contienen `marca` (las 2 Especiales QA son del mismo cliente).
async function abrirFilaQA(app, marca) {
  if (await pl.existeVisible(app, 'icoCerrarAprb')) { await (await pl.visible(app, 'icoCerrarAprb')).click().catch(() => {}); await pl.esperar(app, 1200); }
  const filas = pl.ctl(app, 'btnFilaAprb').locator('visible=true').filter({ hasText: 'QA-E2E CLIENTE UNO' });
  const n = await filas.count();
  for (let i = 0; i < n; i++) {
    await filas.nth(i).click(); await pl.esperar(app, 2000);
    const notas = await pl.texto(app, 'lblNotasAprb').catch(() => '');
    if (notas.includes(marca)) return notas;
    await (await pl.visible(app, 'icoCerrarAprb')).click().catch(() => {}); await pl.esperar(app, 1000);
  }
  return null;
}

module.exports = async function faseC(suite) {
  const yo = cfg.usuarios.user3.id;
  // Especial QA para rechazar, creada por API (solicitante: User2).
  suite.datos.especialRech = await qa.crearEspecialPendiente(suite.datos.cuentas[1], cfg.usuarios.user2.id, 'rechazo F-03');
  const app = await c.abrir(suite, 'user3', 'C', 'Supervisor');
  try {
    await inf.correr('R-04', 'User3 (Supervisor) ve SOLO su nav e Inicio con su sección', 'Supervisor', async (e) => {
      await c.verificarRol(e, app, ['Supervisor'], /^Supervisor$/);
      e.evid(await pl.foto(app, 'R-04-supervisor-inicio'));
    });

    await c.errorAlAbrir('E-01c', app, 'Supervisor');

    await inf.correr('D-02f', 'Inicio Supervisor: POR APROBAR (frío y tibio) = API', 'Supervisor', async (e) => {
      const k = await c.kpisInicio(app, 'Aprobaciones', 'scrAprobaciones');
      const p = await v.porAprobar();
      e.nota(`frío: ${JSON.stringify(k.frio)} · tibio: ${JSON.stringify(k.tibio)}`);
      e.check(k.frio['POR APROBAR'] !== '' && k.frio['POR APROBAR'] != null, 'POR APROBAR con valor en la primera carga', p, k.frio['POR APROBAR']);
      e.check(c.num(k.tibio['POR APROBAR']) === p, 'POR APROBAR = API', p, k.tibio['POR APROBAR']);
    });

    await inf.correr('F-03', 'Supervisor aprueba la Especial QA asignando localizador (combo de agentes)', 'Supervisor', async (e) => {
      await c.ir(app, 'Aprobaciones', 'scrAprobaciones');
      app.red.marcar('C:scrAprobaciones(aprobar)');
      const notas = await abrirFilaQA(app, 'QA-E2E solicitud F-02');
      if (!notas) throw new Error('No se encontró la Especial QA de F-02 en la cola');
      const tit = await pl.texto(app, 'lblPanelTituloAprb');
      const esp = suite.datos.especialF02;
      if (!esp || !tit.includes(esp.sanic_cm_as_numero)) throw new Error(`Seguridad: el panel (${tit}) no es la Especial QA ${esp && esp.sanic_cm_as_numero}`);
      e.check(!(await pl.botonHabilitado(app, 'btnAprobarAprb')), 'Aprobar deshabilitado sin localizador', 'Disabled', 'Edit');
      const ag = suite.datos.agente;
      const r = await c.elegirCombo(app, 'cmbAgenteAprb', { buscar: ag.sanic_cm_ag_nombre.split(' ')[0], opcion: ag.sanic_cm_ag_nombre });
      e.nota(`opciones del combo al abrir: ${r.iniciales.length} — textos ${JSON.stringify(r.iniciales.slice(0, 5))}`);
      e.check(r.iniciales.some(t => t.length > 0), 'las opciones del combo de localizador muestran el nombre del agente', 'nombres visibles', JSON.stringify(r.iniciales.slice(0, 5)));
      e.check(r.ok, `buscar "${ag.sanic_cm_ag_nombre.split(' ')[0]}" ofrece al agente ${ag.sanic_cm_ag_nombre}`, ag.sanic_cm_ag_nombre, r.sinResultados ? 'flyout "sin resultados"' : r.textos);
      if (!r.ok) { suite.datos.aprobacionUiImposible = true; await (await pl.visible(app, 'icoCerrarAprb')).click().catch(() => {}); e.evid(await pl.foto(app, 'F-03-combo-agente')); return; }
      const conEmail = (await dv.getAll("sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre&$filter=sanic_cm_ag_activo eq true and sanic_cm_ag_emailusuario ne null")).map(a => a.sanic_cm_ag_nombre);
      e.nota(`agentes activos con usuario (API): ${conEmail.join(', ')}`);
      e.check(await pl.botonHabilitado(app, 'btnAprobarAprb'), 'Aprobar habilitado con localizador', true, false);
      const ownerAntes = (await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=_ownerid_value`))._ownerid_value;
      suite.datos.ownerAntesAprob = ownerAntes;
      await (await pl.boton(app, 'btnAprobarAprb')).click();
      const n = await c.esperarNotif(app, /Aprobada|error/i);
      e.check(/Aprobada — asignada a/.test(n || ''), 'Notify de aprobación', '✓ Aprobada — asignada a …', n);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const a = await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=sanic_cm_as_estadoaprobacion,_sanic_cm_as_agenteid_value,_sanic_cm_as_resolutorid_value,_ownerid_value`);
      e.check(a.sanic_cm_as_estadoaprobacion === 3, 'Dataverse: Aprobado (3)', 3, a.sanic_cm_as_estadoaprobacion);
      e.check(a._sanic_cm_as_agenteid_value === ag.sanic_cm_tbl_agenteid, 'Dataverse: agente asignado', ag.sanic_cm_tbl_agenteid, a._sanic_cm_as_agenteid_value);
      e.check(a._sanic_cm_as_resolutorid_value === yo, 'Dataverse: resolutor = User3', yo, a._sanic_cm_as_resolutorid_value);
      e.evid(await pl.foto(app, 'F-03-aprobada'));
    });

    await inf.correr('F-03c', 'Comp-14: al aprobar con agente, el owner pasa al usuario del agente (flow activo)', 'Supervisor', async (e) => {
      const esp = suite.datos.especialF02; const ag = suite.datos.agente;
      const flow = (await dv.get("workflows?$select=statecode,statuscode,modifiedon&$filter=name eq 'Cloud Flow | CM | Cambio de Owner en Asignaciones'")).value[0];
      e.check(flow && flow.statecode === 1, 'el flow "Cloud Flow | CM | Cambio de Owner en Asignaciones" está ACTIVADO', 'statecode 1 (Activated)', flow ? `statecode ${flow.statecode} (0 = Draft/apagado) · modificado ${flow.modifiedon}` : 'no existe');
      if (suite.datos.aprobacionUiImposible) { e.nota('No se aprobó por la UI (combo de localizador inutilizable, F-03): el cambio de owner no se puede observar end-to-end.'); return; }
      const a = await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=_ownerid_value`);
      const ownerAntes = suite.datos.ownerAntesAprob || a._ownerid_value;
      let ownerDesp = a._ownerid_value;
      const vueltas = flow && flow.statecode === 1 ? 12 : 3; // flow apagado: 30 s alcanzan para documentarlo
      for (let i = 0; i < vueltas && ownerDesp === ownerAntes; i++) { await new Promise(r2 => setTimeout(r2, 10000)); ownerDesp = (await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=_ownerid_value`))._ownerid_value; }
      e.check(ownerDesp === ag._sanic_cm_ag_usuarioid_value, 'Comp-14: owner = usuario del agente (esperado ≤ 2 min)', ag._sanic_cm_ag_usuarioid_value, `${ownerDesp} (antes ${ownerAntes})`);

    });

    await inf.correr('F-03b', 'Supervisor rechaza otra Especial QA con motivo', 'Supervisor', async (e) => {
      app.red.marcar('C:scrAprobaciones(rechazar)');
      const notas = await abrirFilaQA(app, 'rechazo F-03');
      if (!notas) throw new Error('No se encontró la Especial QA de rechazo en la cola');
      const esp = suite.datos.especialRech;
      const tit = await pl.texto(app, 'lblPanelTituloAprb');
      if (!tit.includes(esp.sanic_cm_as_numero)) throw new Error(`Seguridad: panel ${tit} ≠ ${esp.sanic_cm_as_numero}`);
      e.check(!(await pl.botonHabilitado(app, 'btnRechazarAprb')), 'Rechazar deshabilitado sin motivo', 'Disabled', 'Edit');
      await pl.escribir(app, 'txtMotRechAprb', '   ');
      e.check(!(await pl.botonHabilitado(app, 'btnRechazarAprb')), 'Rechazar deshabilitado con motivo de solo espacios', 'Disabled', 'Edit');
      await pl.escribir(app, 'txtMotRechAprb', 'QA-E2E motivo de rechazo');
      e.check(await pl.botonHabilitado(app, 'btnRechazarAprb'), 'Rechazar habilitado con motivo', true, false);
      await (await pl.boton(app, 'btnRechazarAprb')).click();
      const n = await c.esperarNotif(app, /rechazada|error/i);
      e.check(/Solicitud rechazada/.test(n || ''), 'Notify de rechazo', 'Solicitud rechazada', n);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const a = await dv.get(`sanic_cm_tbl_asignacions(${esp.sanic_cm_tbl_asignacionid})?$select=sanic_cm_as_estadoaprobacion,sanic_cm_as_motivorechazo,_sanic_cm_as_resolutorid_value`);
      e.check(a.sanic_cm_as_estadoaprobacion === 4, 'Dataverse: Rechazada (4)', 4, a.sanic_cm_as_estadoaprobacion);
      e.check(a.sanic_cm_as_motivorechazo === 'QA-E2E motivo de rechazo', 'Dataverse: motivo de rechazo', 'QA-E2E motivo de rechazo', a.sanic_cm_as_motivorechazo);
      e.check(a._sanic_cm_as_resolutorid_value === yo, 'Dataverse: resolutor = User3', yo, a._sanic_cm_as_resolutorid_value);
      const quedan = await pl.ctl(app, 'lblFilaNotasAprb').locator('visible=true').allInnerTexts();
      e.check(!quedan.some(x => /rechazo F-03/.test(x)), 'la Especial rechazada salió de la cola sin recargar', 'sin la fila rechazada', quedan.filter(x => /QA-E2E/.test(x)));
      if (!suite.datos.aprobacionUiImposible) e.check(!quedan.some(x => /solicitud F-02/.test(x)), 'la Especial aprobada también salió de la cola', 'sin la fila aprobada', quedan.filter(x => /QA-E2E/.test(x)));
      e.evid(await pl.foto(app, 'F-03b-rechazada'));
    });
  } finally { await pl.cerrar(app); }
};
