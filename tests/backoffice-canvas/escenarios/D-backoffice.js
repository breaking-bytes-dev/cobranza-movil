// FASE D — User1 en su rol REAL (team "CM · Backoffice · DEV"; rol BackOffice con Write+Append en
// interacción desde la corrección de B-02). Transcripción E2E con el rol Backoffice SOLO.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const cfg = require('../lib/config');
const c = require('./comun');

const LOC_QA = 'QA-E2E Localizador';


// Flujo de transcripción de las 2 interacciones QA (lo usan F-01 y F-01m).
async function flujoTranscripcion(e, app, suite, yo) {
  const { int1, int2 } = suite.datos;
  await c.ir(app, 'Por transcribir', 'scrTranscripcion');
      app.red.marcar(`${app.fase}:scrTranscripcion(grabar)`);
      await pl.ctl(app, 'btnFilaLocsTrx').locator('visible=true').filter({ hasText: LOC_QA }).first().click();
      await pl.esperar(app, 3000);
      let pos = await pl.texto(app, 'lblPosTrx');
      e.check(pos.includes(int1.sanic_cm_in_numerointeraccion) && pos.startsWith('1 DE 2'), 'selección inicial = la QA más vieja (FIFO)', `1 DE 2 … ${int1.sanic_cm_in_numerointeraccion}`, pos);
      e.check((await pl.texto(app, 'lblCuentaGrandeTrx')) === 'QA-E2E-CTA-0001', 'detalle: cuenta QA', 'QA-E2E-CTA-0001', await pl.texto(app, 'lblCuentaGrandeTrx'));
      // Detalle de pagos
      const secP = await pl.texto(app, 'lblSecPagosTrx').catch(() => null);
      const montos = await pl.ctl(app, 'lblPagoMontoTrx').locator('visible=true').allInnerTexts();
      const recibos = await pl.ctl(app, 'lblPagoReciboTrx').locator('visible=true').allInnerTexts();
      e.check(secP === 'PAGOS (1)' && montos.includes('C$ 250.00') && recibos.some(r => r.includes('QA-E2E-REC-1')), 'detalle muestra el pago (C$ 250.00 · Recibo QA-E2E-REC-1)', 'PAGOS (1) · C$ 250.00 · Recibo QA-E2E-REC-1', `${secP} · ${montos} · ${recibos}`);
      e.evid(await pl.foto(app, 'F-01-detalle-pago'));
      // SEGURIDAD: solo se marca si la seleccionada es la interacción QA esperada
      if (!pos.includes(int1.sanic_cm_in_numerointeraccion)) throw new Error('Seguridad: la interacción seleccionada no es la QA');
      await pl.escribir(app, 'txtCodigoTrx', 'qae2e1', { enter: true, tab: false });
      const n1 = await c.esperarNotif(app, /grabada|error/i, 15000);
      e.check(new RegExp(`${int1.sanic_cm_in_numerointeraccion} grabada — siguiente: QA-E2E CLIENTE UNO`).test(n1 || ''), 'Enter graba y anuncia la siguiente', `✓ ${int1.sanic_cm_in_numerointeraccion} grabada — siguiente: QA-E2E CLIENTE UNO`, n1);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const r1 = await dv.get(`sanic_cm_tbl_interaccions(${int1.sanic_cm_tbl_interaccionid})?$select=sanic_cm_in_estadograbacion,sanic_cm_in_codigograbacion,sanic_cm_in_fechagrabacion,_sanic_cm_in_grabadopor_value`);
      e.check(r1.sanic_cm_in_estadograbacion === 2, 'Dataverse: estado = Grabado', 2, r1.sanic_cm_in_estadograbacion);
      if (r1.sanic_cm_in_estadograbacion !== 2) { const err = await c.erroresVisibles(app); e.check(!/grabada/.test(n1 || ''), 'si el Patch FALLA, la app NO anuncia "✓ grabada" ni saca la interacción de la cola', 'sin ✓ / interacción sigue en la cola', `Notify: ${n1} · banner: ${err.join(' | ').slice(0, 300)}`); e.evid(await pl.foto(app, `${app.fase}-F-01-patch-fallido`)); }
      e.check(r1.sanic_cm_in_codigograbacion === 'QAE2E1', 'Dataverse: código en MAYÚSCULAS', 'QAE2E1', r1.sanic_cm_in_codigograbacion);
      e.check(r1._sanic_cm_in_grabadopor_value === yo, 'Dataverse: grabado por = usuario de la sesión', yo, r1._sanic_cm_in_grabadopor_value);
      e.check(r1.sanic_cm_in_fechagrabacion >= v.hoy().inicioUTC, 'Dataverse: fecha de grabación = hoy', `>= ${v.hoy().inicioUTC}`, r1.sanic_cm_in_fechagrabacion);
      const patches = app.red.de(`${app.fase}:scrTranscripcion(grabar)`).flatMap(x => x.subs).filter(s => s.metodo !== 'GET');
      e.nota(`escrituras enviadas al marcar: ${patches.map(p => `${p.metodo} ${p.ruta}`).join(' | ')}`);
      // Avanzó a la siguiente (int2) y muestra el bloque de actualización de datos
      pos = await pl.texto(app, 'lblPosTrx');
      e.check(pos.includes(int2.sanic_cm_in_numerointeraccion) && pos.startsWith('1 DE 1'), 'avanzó a la siguiente QA en la misma posición', `1 DE 1 … ${int2.sanic_cm_in_numerointeraccion}`, pos);
      const codigo = await (await pl.visible(app, 'txtCodigoTrx')).locator('input').first().inputValue();
      e.check(codigo === '', 'input de código limpio tras grabar', '', codigo);
      const secD = await pl.existeVisible(app, 'lblSecDatosTrx');
      const ks = (await pl.ctl(app, 'lblCamKTrx').locator('visible=true').allInnerTexts()).map(s => s.trim());
      const antes = (await pl.ctl(app, 'lblCamAntesTrx').locator('visible=true').allInnerTexts()).map(s => s.trim());
      const nuevos = (await pl.ctl(app, 'lblCamNuevoTrx').locator('visible=true').allInnerTexts()).map(s => s.trim());
      const chipNuevo = await pl.ctl(app, 'chpCamNuevoTrx').locator('visible=true').count();
      e.check(secD && ks.includes('TELÉFONO CASA') && ks.includes('CORREO ELECTRÓNICO'), 'bloque ✏️ con los campos actualizados', 'TELÉFONO CASA + CORREO ELECTRÓNICO', ks);
      e.check(antes.includes('2222-0000') && nuevos.includes('8888-0000'), 'TELÉFONO CASA: antes 2222-0000 → nuevo 8888-0000', '2222-0000 → 8888-0000', `${antes} → ${nuevos}`);
      e.check(nuevos.includes('qa-e2e@example.com') && chipNuevo >= 1, 'CORREO: dato nuevo con chip NUEVO', 'qa-e2e@example.com + NUEVO', `${nuevos} · chips NUEVO=${chipNuevo}`);
      e.evid(await pl.foto(app, 'F-01-detalle-actualizacion'));
      // Segunda: con el BOTÓN
      if (!pos.includes(int2.sanic_cm_in_numerointeraccion)) throw new Error('Seguridad: la seleccionada no es la QA 2');
      await pl.escribir(app, 'txtCodigoTrx', 'QAE2E2', { tab: false });
      await (await pl.boton(app, 'btnGrabarTrx')).click();
      const n2 = await c.esperarNotif(app, /grabada|error/i, 15000);
      e.check(/grabada — ¡cola vacía!/.test(n2 || ''), 'botón graba y anuncia cola vacía', `✓ ${int2.sanic_cm_in_numerointeraccion} grabada — ¡cola vacía!`, n2);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const r2 = await dv.get(`sanic_cm_tbl_interaccions(${int2.sanic_cm_tbl_interaccionid})?$select=sanic_cm_in_estadograbacion,sanic_cm_in_codigograbacion,_sanic_cm_in_grabadopor_value`);
      e.check(r2.sanic_cm_in_estadograbacion === 2 && r2.sanic_cm_in_codigograbacion === 'QAE2E2' && r2._sanic_cm_in_grabadopor_value === yo, 'Dataverse: 2.ª grabada con código y usuario', `2/QAE2E2/${yo}`, `${r2.sanic_cm_in_estadograbacion}/${r2.sanic_cm_in_codigograbacion}/${r2._sanic_cm_in_grabadopor_value}`);
      const locs = (await pl.ctl(app, 'btnFilaLocsTrx').locator('visible=true').allInnerTexts()).join(' / ');
      e.check(!locs.includes(LOC_QA), 'el localizador QA desapareció de la columna', `sin ${LOC_QA}`, locs);
      e.evid(await pl.foto(app, 'F-01-cola-vacia'));

  const ok = e.checks.filter(x => /^Dataverse/.test(x.msg)).every(x => x.ok);
  if (ok) suite.datos.transcritoOk = true;
  return ok;
}

// Arqueo "Transcritas hoy" (F-01b).
async function arqueo(e, app, yo) {
      await c.ir(app, 'Transcritas hoy', 'scrTranscritas');
      const k = await pl.kpis(app);
      const t = await v.transcritasHoy(yo);
      e.check(c.num(k.TRANSCRITAS) === t, 'KPI TRANSCRITAS = API (grabadas hoy por mí)', t, k.TRANSCRITAS);
      e.check(k['PAGOS TRANSCRITOS'] === 'C$ 250.00', 'KPI PAGOS TRANSCRITOS = pagos de hoy de mis transcritas', 'C$ 250.00', k['PAGOS TRANSCRITOS']);
      const filas = (await pl.ctl(app, 'lblCuentaTsc').locator('visible=true').allInnerTexts()).filter(x => x.includes('QA-E2E-CTA-0001'));
      e.check(filas.length === 2, 'las 2 interacciones QA están en el arqueo', 2, filas.length);
      e.evid(await pl.foto(app, 'F-01b-transcritas'));
}

module.exports = async function faseD(suite) {
  const U = 'user1', yo = cfg.usuarios.user1.id;
  const app = await c.abrir(suite, U, 'D', 'BackOffice');
  try {
    await inf.correr('R-02', 'User1 (Backoffice) abre la app y ve SOLO su nav e Inicio con su sección', 'BackOffice', async (e) => {
      await c.verificarRol(e, app, ['BackOffice'], /^BackOffice$/);
      e.evid(await pl.foto(app, 'R-02-backoffice-inicio'));
    });

    await c.errorAlAbrir('E-01d', app, 'BackOffice');

    await inf.correr('D-02g', 'Inicio Backoffice: EN COLA / CON PAGO / TRANSCRITAS HOY en la PRIMERA carga y en tibio = API (B-01)', 'BackOffice', async (e) => {
      const subF = await pl.texto(app, 'lblSubEncCmp').catch(() => '');
      const k = await c.kpisInicio(app, 'Por transcribir', 'scrTranscripcion');
      e.nota(`frío: ${JSON.stringify(k.frio)} · tibio: ${JSON.stringify(k.tibio)} · subtítulo: ${subF}`);
      const [p, cp, t] = [await v.pendientes(), await v.pendientesConPago(), await v.transcritasHoy(yo)];
      for (const [et, val] of [['EN COLA', p], ['CON PAGO', cp], ['TRANSCRITAS HOY', t]]) e.check(c.num(k.frio[et]) === val, `${et} en la PRIMERA carga = API`, val, k.frio[et]);
      const cob = await v.cobradoHoy();
      e.check((subF || '').endsWith(v.moneda(cob.nio, cob.usd)), 'Cobrado hoy (subtítulo, rol BO) = Sum API', v.moneda(cob.nio, cob.usd), subF);
      e.check(c.num(k.tibio['EN COLA']) === p, 'EN COLA = API', p, k.tibio['EN COLA']);
      e.check(c.num(k.tibio['CON PAGO']) === cp, 'CON PAGO = API', cp, k.tibio['CON PAGO']);
      e.check(c.num(k.tibio['TRANSCRITAS HOY']) === t, 'TRANSCRITAS HOY = API', t, k.tibio['TRANSCRITAS HOY']);
    });

    await inf.correr('D-02h', 'Estación de transcripción: chip "Todos" y encabezado = pendientes API', 'BackOffice', async (e) => {
      await c.ir(app, 'Por transcribir', 'scrTranscripcion');
      const p = await v.pendientes();
      const locs = (await pl.ctl(app, 'btnFilaLocsTrx').locator('visible=true').allInnerTexts()).map(s => s.trim());
      const todos = locs.find(x => x.startsWith('Todos'));
      e.check(c.num(todos && todos.match(/\((\d+) pendientes\)/)?.[1]) === p, 'chip "Todos" = pendientes API', p, todos);
      const head = await pl.texto(app, 'lblHeadColaTrx');
      e.check(c.num(head.match(/PENDIENTES · (\d+)/)?.[1]) === p, 'encabezado de la cola = pendientes API', p, head);
      e.check(locs.some(x => x.includes(LOC_QA)), 'aparece el localizador QA con sus 2 pendientes', `${LOC_QA} (2 pendientes)`, locs);
      e.evid(await pl.foto(app, 'D-02h-transcripcion'));
    });

    await inf.correr('F-01', 'Backoffice (rol SOLO) transcribe las 2 interacciones QA (Enter y botón), avance y detalle (B-02)', 'BackOffice', (e) => flujoTranscripcion(e, app, suite, yo));

    await inf.correr('F-01b', 'Transcritas hoy: KPI = API y aparecen las 2 QA con su pago', 'BackOffice', async (e) => {
      if (!suite.datos.transcritoOk) { e.bloquear('depende de F-01 (no se grabó)'); return; }
      await arqueo(e, app, yo);
    });
  } finally {
    await pl.cerrar(app);
  }
};

module.exports.flujoTranscripcion = flujoTranscripcion;
module.exports.arqueo = arqueo;
