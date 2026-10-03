// FASE B — User2 en su rol REAL (team "CM · Solicitantes · DEV"; rol Solicitante: Read/Write de
// asignaciones a nivel USUARIO = solo las propias).
// Cubre: R-03, E-01b, D-02e (B-01 frío + subtítulo B-05), D-05 (mínimo privilegio), D-04/F-02a (combo B-04),
// F-02 (crear por UI), F-02b (anti-duplicado), X-02 (falla CONTROLADA de escritura → B-03) y F-02d (reactivar
// una Especial CERRADA propia → B-11).
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const cfg = require('../lib/config');
const qa = require('../lib/datosqa');
const c = require('./comun');

const SEL_AS = 'sanic_cm_tbl_asignacionid,sanic_cm_as_numero,sanic_cm_as_tipo,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,sanic_cm_as_notas,sanic_cm_as_retironio,sanic_cm_as_motivo,sanic_cm_as_fechacierre,sanic_cm_as_motivorechazo,_sanic_cm_as_solicitadopor_value,_sanic_cm_as_agenteid_value,_sanic_cm_as_resolutorid_value,_ownerid_value';
const especialesDe = async (cta) => (await dv.get(`sanic_cm_tbl_asignacions?$select=${SEL_AS}&$filter=sanic_cm_as_numerocuenta eq '${cta}' and sanic_cm_as_tipo eq 3`)).value;

async function llenarSolicitud(app, cuenta, { nio, motivo, tipo = 'Retiro urgente' }) {
  const r = await c.elegirCombo(app, 'cmbCuentaNva', { buscar: cuenta.slice(0, 14), opcion: cuenta });
  if (!r.ok) return r;
  await c.elegirCombo(app, 'cmbTipoNva', { opcion: tipo });
  await pl.escribir(app, 'txtRetiroNioNva', String(nio));
  await pl.escribir(app, 'txtMotivoNva', motivo);
  await pl.esperar(app, 1000);
  return r;
}

// Pide una Especial por la UI para `cuenta` (QA) y devuelve el Notify. Verifica que la cuenta elegida es QA.
async function pedirEspecial(app, cuenta, { nio, motivo, etiqueta }) {
  await c.ir(app, 'Nueva solicitud', etiqueta);
  const r = await llenarSolicitud(app, cuenta, { nio, motivo });
  if (!r.ok) throw new Error(`No se pudo elegir la cuenta ${cuenta} en el combo (opciones: ${r.textos.join(' / ')})`);
  const sel = await pl.texto(app, 'cmbCuentaNva');
  if (!sel.includes(cuenta) || !/^QA-E2E-CTA-/.test(cuenta)) throw new Error(`Seguridad: la cuenta elegida no es QA (${sel})`);
  await (await pl.boton(app, 'btnEnviarNva')).click();
  const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe|No se pudo/, 20000);
  await app.red.quieta(app.page, { quietoMs: 5000, maxMs: 40000 });
  return n;
}

module.exports = async function faseB(suite) {
  const U = 'user2', yo = cfg.usuarios.user2.id;
  const [cta1, , cta3, cta4] = suite.datos.cuentas;
  const ctaQA = cta1.sanic_cm_ct_numerodecuenta; // QA-E2E-CTA-0001
  // Precondiciones de X-02 y F-02d (por API, solo cuentas QA):
  //  · CTA-0003: Especial CERRADA cuyo dueño es el Service Principal → el Solicitante (Read nivel usuario) NO la ve.
  //  · CTA-0004: Especial CERRADA cuyo dueño es User2 → la ve y la app debe REACTIVARLA.
  suite.datos.cerradaAjena = await qa.crearEspecialPendiente(cta3, cfg.usuarios.user3.id, 'X-02 cerrada ajena', { estado: false });
  suite.datos.cerradaPropia = await qa.crearEspecialPendiente(cta4, yo, 'F-02d cerrada propia', { estado: false, owner: yo });
  const app = await c.abrir(suite, U, 'B', 'Solicitante');
  try {
    await inf.correr('R-03', 'User2 (Solicitante) abre la app y ve SOLO su nav e Inicio con su sección', 'Solicitante', async (e) => {
      await c.verificarRol(e, app, ['Solicitante'], /^Solicitante$/);
      e.evid(await pl.foto(app, 'R-03-solicitante-inicio'));
    });

    await c.errorAlAbrir('E-01b', app, 'Solicitante');

    await inf.correr('D-02e', 'Inicio Solicitante: KPIs en la PRIMERA carga = API (B-01) y subtítulo propio (B-05)', 'Solicitante', async (e) => {
      const sub = await pl.texto(app, 'lblSubEncCmp').catch(() => null);
      e.check(sub === 'Tus solicitudes Especiales', 'subtítulo del Solicitante = "Tus solicitudes Especiales" (no "Cobrado hoy")', 'Tus solicitudes Especiales', sub);
      const k = await c.kpisInicio(app, 'Mis solicitudes', 'scrMisSolicitudes');
      e.nota(`frío: ${JSON.stringify(k.frio)} · tibio: ${JSON.stringify(k.tibio)}`);
      const [p, a, r] = [await v.especialesDe(yo, 2), await v.especialesDe(yo, 3), await v.especialesDe(yo, 4)];
      for (const [et, val] of [['PENDIENTES', p], ['APROBADAS', a], ['RECHAZADAS', r]]) {
        e.check(c.num(k.frio[et]) === val, `${et} en la PRIMERA carga = API`, val, k.frio[et]);
        e.check(c.num(k.tibio[et]) === val, `${et} (tibio) = API`, val, k.tibio[et]);
      }
    });

    let combo = null;
    await inf.correr('D-04', 'Nueva solicitud: la búsqueda del combo de cuentas viaja con StartsWith ($filter en servidor)', 'Solicitante', async (e) => {
      await c.ir(app, 'Nueva solicitud', 'scrNuevaSolicitud');
      app.red.marcar('B:scrNuevaSolicitud(combo)');
      combo = await c.elegirCombo(app, 'cmbCuentaNva', { buscar: 'QA-E2E-CTA-000', opcion: ctaQA });
      const ents = app.red.de('B:scrNuevaSolicitud(combo)');
      const i = ents.findIndex(x => x.subs.some(s2 => /sanic_cm_tbl_cuentas.*startswith\(sanic_cm_ct_numerodecuenta,'QA-E2E-CTA/.test(s2.ruta)));
      e.check(i >= 0, "la request lleva startswith(sanic_cm_ct_numerodecuenta, 'QA-E2E-CTA-000') en $filter", 'startswith en $filter', ents.flatMap(x => x.subs.map(s2 => s2.ruta)).slice(-1)[0]);
      const filas = i >= 0 ? ents[i].respuestas?.[0]?.filas : null;
      e.check(filas === suite.datos.cuentas.length, `el servidor devuelve las ${suite.datos.cuentas.length} cuentas QA`, suite.datos.cuentas.length, filas);
      e.evid(await pl.foto(app, 'D-04-combo-cuentas'));
    });

    await inf.correr('F-02a', 'Nueva solicitud: el combo MUESTRA número + nombre del cliente y deja elegir la cuenta (B-04)', 'Solicitante', async (e) => {
      e.nota(`opciones al abrir (sin buscar): ${combo.iniciales.length} — textos: ${JSON.stringify(combo.iniciales.slice(0, 3))}`);
      e.check(combo.iniciales.length > 0 && combo.iniciales.every(t => t.length > 0), 'las opciones iniciales tienen texto (no vacías)', 'texto visible', JSON.stringify(combo.iniciales.slice(0, 3)));
      e.check(combo.textos.some(t => t.includes(ctaQA) && t.includes('QA-E2E CLIENTE UNO')), 'tras buscar, la opción muestra número de cuenta + nombre del cliente', `${ctaQA} + QA-E2E CLIENTE UNO`, JSON.stringify(combo.textos.slice(0, 4)));
      e.check(combo.ok, `la cuenta ${ctaQA} se puede elegir`, ctaQA, combo.sinResultados ? 'flyout "sin resultados"' : combo.textos.slice(0, 4));
      e.check(!combo.sinResultados, 'el flyout NO dice "sin resultados"', false, combo.sinResultados);
    });

    await inf.correr('F-02', 'Solicitante crea una Especial QA (retiro NIO) por la UI y aparece en Mis solicitudes', 'Solicitante', async (e) => {
      const motivo = 'QA-E2E solicitud F-02';
      if (!combo || !combo.ok) {
        e.check(false, 'crear la Especial desde la UI', 'Especial creada con el formulario', 'imposible: el combo de cuentas no deja elegir (F-02a)');
        // Respaldo para no bloquear C/F/G: la MISMA Especial por API (dueño User2).
        suite.datos.especialF02 = await qa.crearEspecialPendiente(cta1, yo, 'F-02 (API)', { notas: `Retiro urgente — ${motivo}`, nio: 350, owner: yo });
        e.nota(`Especial ${suite.datos.especialF02.sanic_cm_as_numero} creada POR API para continuar`);
        return;
      }
      await c.elegirCombo(app, 'cmbTipoNva', { opcion: 'Retiro urgente' });
      await pl.escribir(app, 'txtRetiroNioNva', '350');
      await pl.escribir(app, 'txtMotivoNva', motivo);
      await pl.esperar(app, 1000);
      const sel = await pl.texto(app, 'cmbCuentaNva');
      if (!sel.includes(ctaQA)) throw new Error(`Seguridad: la cuenta elegida no es QA (${sel})`);
      e.check((await pl.texto(app, 'lblHintNva')).includes('Todo listo'), 'hint "✓ Todo listo"', '✓ Todo listo', await pl.texto(app, 'lblHintNva'));
      e.check(await pl.botonHabilitado(app, 'btnEnviarNva'), 'Enviar habilitado', true, false);
      app.red.marcar('B:scrNuevaSolicitud(enviar)');
      await (await pl.boton(app, 'btnEnviarNva')).click();
      const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe|No se pudo/, 20000);
      e.check(/Solicitud enviada/.test(n || ''), 'Notify de envío', '✨ Solicitud enviada …', n);
      await app.red.quieta(app.page, { quietoMs: 6000, maxMs: 45000 });
      const rows = await especialesDe(ctaQA);
      e.check(rows.length === 1, 'Dataverse: 1 Especial para la cuenta QA', 1, rows.length);
      const a = rows[0] || {};
      suite.datos.especialF02 = a;
      e.check(a.sanic_cm_as_estadoaprobacion === 2, 'estado de aprobación = Pendiente aprobacion (2)', 2, a.sanic_cm_as_estadoaprobacion);
      e.check(a.sanic_cm_as_estado === true, '[AS] Estado = activo', true, a.sanic_cm_as_estado);
      e.check(a._sanic_cm_as_solicitadopor_value === yo, 'Solicitado por = User2', yo, a._sanic_cm_as_solicitadopor_value);
      e.check(a._ownerid_value === yo, 'owner = User2 (la creó la app con su identidad)', yo, a._ownerid_value);
      e.check(a.sanic_cm_as_notas === `Retiro urgente — ${motivo}`, 'Notas = "<tipo> — <motivo>"', `Retiro urgente — ${motivo}`, a.sanic_cm_as_notas);
      e.check(Number(a.sanic_cm_as_retironio) === 350, 'Retiro NIO = 350', 350, a.sanic_cm_as_retironio);
      e.check(!a._sanic_cm_as_agenteid_value, 'nace SIN agente', null, a._sanic_cm_as_agenteid_value);
      await pl.esperar(app, 3000);
      const filas = (await pl.ctl(app, 'lblCuentaFilaSol').locator('visible=true').allInnerTexts()).map(s => s.trim());
      e.check(filas.includes(ctaQA), 'navegó a Mis solicitudes y la muestra', ctaQA, filas.slice(0, 5));
      e.evid(await pl.foto(app, 'F-02-mis-solicitudes'));
    });

    await inf.correr('F-02b', 'Anti-duplicado: segunda solicitud para la misma cuenta (Especial ACTIVA) → aviso y NO se crea', 'Solicitante', async (e) => {
      if (!combo || !combo.ok) { e.bloquear('depende de elegir la cuenta en el combo (F-02a)'); return; }
      const n = await pedirEspecial(app, ctaQA, { nio: 10, motivo: 'QA-E2E duplicado F-02b', etiqueta: 'scrNuevaSolicitud(dup)' });
      e.check(/Ya existe una Especial ACTIVA/.test(n || ''), 'Notify anti-duplicado', 'Ya existe una Especial ACTIVA …', n);
      e.evid(await pl.foto(app, 'F-02b-anti-duplicado'));
      const k = (await especialesDe(ctaQA)).length;
      e.check(k === 1, 'Dataverse: sigue habiendo 1 sola Especial', 1, k);
    });

    await inf.correr('X-02', 'Falla CONTROLADA de escritura (B-03): pedir Especial sobre una cuenta QA cuya Especial cerrada el Solicitante NO puede ver → la app muestra el error y NO da por hecho', 'Solicitante', async (e) => {
      if (!combo || !combo.ok) { e.bloquear('depende del combo de cuentas (F-02a)'); return; }
      const cta = cta3.sanic_cm_ct_numerodecuenta; const previa = suite.datos.cerradaAjena;
      e.nota(`Precondición: ${previa.sanic_cm_as_numero} (cuenta ${cta}) CERRADA y con dueño = Service Principal; el rol Solicitante lee asignaciones a nivel USUARIO → la app no la ve, intenta CREAR y Dataverse rechaza por la alt key cuenta+tipo.`);
      const n = await pedirEspecial(app, cta, { nio: 77, motivo: 'QA-E2E falla controlada X-02', etiqueta: 'scrNuevaSolicitud(falla controlada)' });
      e.check(new RegExp(`No se pudo enviar la solicitud de la cuenta ${cta}`).test(n || ''), 'Notify de ERROR con el detalle (IfError)', `No se pudo enviar la solicitud de la cuenta ${cta}: …`, n);
      e.check(!/Solicitud enviada/.test(n || ''), 'NO anuncia "Solicitud enviada"', 'sin ✨', n);
      const sigue = await pl.existeVisible(app, 'cmbCuentaNva');
      const sel = sigue ? await pl.texto(app, 'cmbCuentaNva').catch(() => '') : '';
      e.check(sigue && sel.includes(cta), 'se queda en Nueva solicitud con el formulario intacto (no Reset, no Navigate)', `cmbCuentaNva = ${cta}`, sigue ? sel : 'navegó fuera');
      const escr = c.erroresRed(app, 'B:scrNuevaSolicitud(falla controlada)');
      e.nota(`respuesta de Dataverse: ${escr.join(' | ') || '(sin error en red)'}`);
      e.evid(await pl.foto(app, 'X-02-falla-controlada'));
      const rows = await especialesDe(cta);
      e.check(rows.length === 1 && rows[0].sanic_cm_as_estado === false, 'Dataverse: nada cambió (1 Especial, sigue CERRADA)', '1 · cerrada', `${rows.length} · ${rows.map(r => r.sanic_cm_as_estado)}`);
    });

    await inf.correr('F-02d', 'Re-solicitar Especial sobre cuenta con Especial CERRADA PROPIA → la app REACTIVA el mismo registro (B-11)', 'Solicitante', async (e) => {
      if (!combo || !combo.ok) { e.bloquear('depende del combo de cuentas (F-02a)'); return; }
      const cta = cta4.sanic_cm_ct_numerodecuenta; const previa = suite.datos.cerradaPropia;
      const n = await pedirEspecial(app, cta, { nio: 88, motivo: 'QA-E2E reactivación F-02d', etiqueta: 'scrNuevaSolicitud(reactivar)' });
      e.check(/Solicitud enviada/.test(n || ''), 'Notify de envío (sin 412)', '✨ Solicitud enviada …', n);
      const rows = await especialesDe(cta);
      const a = rows[0] || {};
      e.check(rows.length === 1 && a.sanic_cm_tbl_asignacionid === previa.sanic_cm_tbl_asignacionid, 'Dataverse: sigue 1 sola Especial y es el MISMO registro (reactivado, no duplicado)', previa.sanic_cm_as_numero, rows.map(r => r.sanic_cm_as_numero));
      e.check(a.sanic_cm_as_estado === true && a.sanic_cm_as_estadoaprobacion === 2, 'reactivada: Estado activo + Pendiente aprobación', 'true · 2', `${a.sanic_cm_as_estado} · ${a.sanic_cm_as_estadoaprobacion}`);
      e.check(a.sanic_cm_as_notas === 'Retiro urgente — QA-E2E reactivación F-02d' && Number(a.sanic_cm_as_retironio) === 88, 'datos nuevos (notas + retiro)', 'Retiro urgente — QA-E2E reactivación F-02d · 88', `${a.sanic_cm_as_notas} · ${a.sanic_cm_as_retironio}`);
      e.check(!a.sanic_cm_as_motivo && !a.sanic_cm_as_fechacierre && !a._sanic_cm_as_agenteid_value && !a._sanic_cm_as_resolutorid_value, 'limpia el ciclo anterior (motivo, fecha de cierre, agente, resolutor)', 'vacíos', `${a.sanic_cm_as_motivo} · ${a.sanic_cm_as_fechacierre} · ${a._sanic_cm_as_agenteid_value} · ${a._sanic_cm_as_resolutorid_value}`);
      e.evid(await pl.foto(app, 'F-02d-reactivada'));
    });

    await inf.correr('D-05', 'Mínimo privilegio (B-05): en toda la sesión del Solicitante no hay consultas a Pagos ni Carga diaria ni respuestas 403', 'Solicitante', async (e) => {
      const rutas = app.red.entradas.flatMap(x => x.subs.map(s2 => s2.ruta));
      const prohibidas = rutas.filter(r => /^sanic_cm_tbl_(pagos|cargadiarias)/.test(r));
      e.check(prohibidas.length === 0, 'sin consultas a sanic_cm_tbl_pagos / sanic_cm_tbl_cargadiarias', 0, prohibidas.slice(0, 4));
      const e403 = c.erroresRed(app).filter(x => /^403/.test(x));
      e.check(e403.length === 0, 'sin respuestas 403 en toda la sesión', 0, e403.slice(0, 4));
    });
  } finally {
    await pl.cerrar(app);
  }
};

module.exports.llenarSolicitud = llenarSolicitud;
module.exports.pedirEspecial = pedirEspecial;
module.exports.especialesDe = especialesDe;
