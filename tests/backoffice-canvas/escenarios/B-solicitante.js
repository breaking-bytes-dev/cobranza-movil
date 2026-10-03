// FASE B — Rol Solicitante. User2 (dueño del rol) tiene la licencia de Power Apps vencida →
// se usa User3 movido TEMPORALMENTE al team "CM · Solicitantes · DEV" (y fuera de Supervisores).
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const cfg = require('../lib/config');
const qa = require('../lib/datosqa');
const c = require('./comun');

async function llenarSolicitud(app, cuenta, { nio, motivo, tipo = 'Retiro urgente' }) {
  const r = await c.elegirCombo(app, 'cmbCuentaNva', { buscar: cuenta.slice(0, 14), opcion: cuenta });
  if (!r.ok) return r;
  await c.elegirCombo(app, 'cmbTipoNva', { opcion: tipo });
  await pl.escribir(app, 'txtRetiroNioNva', String(nio));
  await pl.escribir(app, 'txtMotivoNva', motivo);
  await pl.esperar(app, 1000);
  return r;
}

module.exports = async function faseB(suite) {
  const U = 'user3', yo = cfg.usuarios.user3.id;
  const ctaQA = suite.datos.cuentas[0].sanic_cm_ct_numerodecuenta; // QA-E2E-CTA-0001
  await c.cambiarTeams(U, { quitar: ['Supervisores'], agregar: ['Solicitantes'] });
  const app = await c.abrir(suite, U, 'B', 'Solicitante (User3 sustituto)');
  try {
    await inf.correr('R-03*', 'Solicitante (User3 en team Solicitantes) ve SOLO su nav e Inicio con su sección', 'Solicitante', async (e) => {
      await c.verificarRol(e, app, ['Solicitante'], /^Solicitante$/);
      e.evid(await pl.foto(app, 'R-03-solicitante-inicio'));
    });

    await c.errorAlAbrir('E-01b', app, 'Solicitante');

    await inf.correr('D-02e', 'Inicio Solicitante: KPIs (frío y tibio) = API', 'Solicitante', async (e) => {
      const k = await c.kpisInicio(app, 'Mis solicitudes', 'scrMisSolicitudes');
      e.nota(`frío: ${JSON.stringify(k.frio)} · tibio: ${JSON.stringify(k.tibio)}`);
      e.check(k.frio.PENDIENTES !== '' && k.frio.PENDIENTES != null, 'PENDIENTES con valor en la primera carga', 'número', k.frio.PENDIENTES);
      const [p, a, r] = [await v.especialesDe(yo, 2), await v.especialesDe(yo, 3), await v.especialesDe(yo, 4)];
      e.check(c.num(k.tibio.PENDIENTES) === p, 'PENDIENTES = API', p, k.tibio.PENDIENTES);
      e.check(c.num(k.tibio.APROBADAS) === a, 'APROBADAS = API', a, k.tibio.APROBADAS);
      e.check(c.num(k.tibio.RECHAZADAS) === r, 'RECHAZADAS = API', r, k.tibio.RECHAZADAS);
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
      e.check(filas === 2, 'el servidor devuelve las 2 cuentas QA', 2, filas);
      e.evid(await pl.foto(app, 'D-04-combo-cuentas'));
    });

    await inf.correr('F-02a', 'Nueva solicitud: el combo MUESTRA lo que devolvió el servidor y deja elegir la cuenta', 'Solicitante', async (e) => {
      e.nota(`opciones al abrir (sin buscar): ${combo.iniciales.length} — textos: ${JSON.stringify(combo.iniciales.slice(0, 5))}`);
      e.check(combo.iniciales.length === 0 || combo.iniciales.some(t => t.length > 0), 'las opciones iniciales muestran número/cliente (no vacías)', 'texto visible', JSON.stringify(combo.iniciales.slice(0, 5)));
      e.check(combo.ok, `tras buscar, la cuenta ${ctaQA} aparece y se puede elegir`, ctaQA, combo.sinResultados ? 'flyout "sin resultados" (0 opciones)' : combo.textos.slice(0, 5));
    });

    await inf.correr('F-02', 'Solicitante crea una Especial QA (retiro NIO) y aparece en Mis solicitudes', 'Solicitante', async (e) => {
      app.red.marcar('B:scrNuevaSolicitud(enviar)');
      const motivo = 'QA-E2E solicitud F-02';
      if (!combo || !combo.ok) {
        e.check(false, 'crear la Especial desde la UI', 'Especial creada con el formulario', 'imposible: el combo de cuentas no muestra resultados (F-02a)');
        // Para no bloquear F-03/F-04 se crea la MISMA Especial que haría btnEnviarNva, por API (marcado en notas).
        const a = await qa.crearEspecialPendiente(suite.datos.cuentas[0], yo, 'F-02 (API)', { notas: `Retiro urgente — ${motivo}`, nio: 350 });
        suite.datos.especialF02 = a; suite.datos.especialF02PorApi = true;
        // Dueño = User3, como si la hubiera creado la app (el rol Solicitante lee SUS registros).
        await dv.patch('sanic_cm_tbl_asignacions', a.sanic_cm_tbl_asignacionid, { 'ownerid@odata.bind': `/systemusers(${yo})` });
        e.nota(`Especial ${a.sanic_cm_as_numero} creada POR API (solicitado por User3) para continuar F-03/F-04`);
        // Verificación auxiliar (no cuenta como check: la fila la creó la API, no la app). El player
        // cachea consultas idénticas un rato → se espera y se usa "↻ Actualizar".
        await new Promise(r2 => setTimeout(r2, 35000));
        await c.ir(app, 'Mis solicitudes', 'scrMisSolicitudes(tras API)');
        await (await pl.boton(app, 'btnActualizarSol')).click().catch(() => {});
        await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 20000 });
        const filas = (await pl.ctl(app, 'lblCuentaFilaSol').locator('visible=true').allInnerTexts()).map(x => x.trim());
        const chip = (await pl.ctl(app, 'chpEstadoFilaSol').locator('visible=true').allInnerTexts()).map(x => x.trim());
        e.nota(`Mis solicitudes tras ↻: cuentas ${JSON.stringify(filas.slice(0, 5))} · chips ${JSON.stringify(chip.slice(0, 5))}`);
        e.evid(await pl.foto(app, 'F-02-mis-solicitudes-api'));
        return;
      }
      const r = await llenarSolicitud(app, ctaQA, { nio: 350, motivo });
      if (!r.ok) throw new Error(`No se pudo elegir la cuenta QA en el combo (opciones: ${r.textos.join(' / ')})`);
      const sel = await pl.texto(app, 'cmbCuentaNva');
      if (!sel.includes(ctaQA)) throw new Error(`Seguridad: la cuenta elegida no es QA (${sel})`);
      e.check((await pl.texto(app, 'lblHintNva')).includes('Todo listo'), 'hint "✓ Todo listo"', '✓ Todo listo', await pl.texto(app, 'lblHintNva'));
      e.check(await pl.botonHabilitado(app, 'btnEnviarNva'), 'Enviar habilitado', true, false);
      await (await pl.boton(app, 'btnEnviarNva')).click();
      const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe/);
      e.check(/Solicitud enviada/.test(n || ''), 'Notify de envío', '✨ Solicitud enviada …', n);
      await app.red.quieta(app.page, { quietoMs: 6000, maxMs: 45000 });
      const rows = (await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_as_numero,sanic_cm_as_tipo,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,sanic_cm_as_notas,sanic_cm_as_retironio,sanic_cm_as_retirousd,_sanic_cm_as_solicitadopor_value,_sanic_cm_as_agenteid_value,_ownerid_value&$filter=sanic_cm_as_numerocuenta eq '${ctaQA}' and sanic_cm_as_tipo eq 3`)).value;
      e.check(rows.length === 1, 'Dataverse: 1 Especial para la cuenta QA', 1, rows.length);
      const a = rows[0] || {};
      suite.datos.especialF02 = a;
      e.check(a.sanic_cm_as_estadoaprobacion === 2, 'estado de aprobación = Pendiente aprobacion (2)', 2, a.sanic_cm_as_estadoaprobacion);
      e.check(a.sanic_cm_as_estado === true, '[AS] Estado = activo', true, a.sanic_cm_as_estado);
      e.check(a._sanic_cm_as_solicitadopor_value === yo, 'Solicitado por = User3', yo, a._sanic_cm_as_solicitadopor_value);
      e.check(a.sanic_cm_as_notas === `Retiro urgente — ${motivo}`, 'Notas = "<tipo> — <motivo>"', `Retiro urgente — ${motivo}`, a.sanic_cm_as_notas);
      e.check(Number(a.sanic_cm_as_retironio) === 350, 'Retiro NIO = 350', 350, a.sanic_cm_as_retironio);
      e.check(!a._sanic_cm_as_agenteid_value, 'nace SIN agente', null, a._sanic_cm_as_agenteid_value);
      // navegó a Mis solicitudes y la muestra
      await pl.esperar(app, 3000);
      const filas = await pl.ctl(app, 'lblCuentaFilaSol').locator('visible=true').allInnerTexts();
      e.check(filas.map(s => s.trim()).includes(ctaQA), 'aparece en Mis solicitudes', ctaQA, filas.slice(0, 5));
      e.evid(await pl.foto(app, 'F-02-mis-solicitudes'));
    });

    await inf.correr('F-02b', 'Anti-duplicado: segunda solicitud para la misma cuenta → aviso y NO se crea', 'Solicitante', async (e) => {
      if (!combo || !combo.ok) { e.bloquear('depende de elegir la cuenta en el combo, que no muestra resultados (F-02a)'); return; }
      await c.ir(app, 'Nueva solicitud', 'scrNuevaSolicitud(dup)');
      const r = await llenarSolicitud(app, ctaQA, { nio: 10, motivo: 'QA-E2E duplicado F-02b' });
      if (!r.ok) throw new Error('No se pudo elegir la cuenta QA');
      if (!(await pl.texto(app, 'cmbCuentaNva')).includes(ctaQA)) throw new Error('Seguridad: cuenta no QA');
      await (await pl.boton(app, 'btnEnviarNva')).click();
      const n = await c.esperarNotif(app, /Ya existe|Solicitud enviada/);
      e.check(/Ya existe una Especial ACTIVA/.test(n || ''), 'Notify anti-duplicado', 'Ya existe una Especial ACTIVA …', n);
      e.evid(await pl.foto(app, 'F-02b-anti-duplicado'));
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 15000 });
      const k = (await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_as_numero&$filter=sanic_cm_as_numerocuenta eq '${ctaQA}' and sanic_cm_as_tipo eq 3`)).value.length;
      e.check(k === 1, 'Dataverse: sigue habiendo 1 sola Especial', 1, k);
    });
  } finally {
    await pl.cerrar(app);
    await c.cambiarTeams(U, { quitar: ['Solicitantes'], agregar: ['Supervisores'] });
  }
};

module.exports.llenarSolicitud = llenarSolicitud;
