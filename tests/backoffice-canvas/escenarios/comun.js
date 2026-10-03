// Utilidades compartidas por las fases de la suite.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const qa = require('../lib/datosqa');
const cfg = require('../lib/config');

// Nav esperada por rol (fuente: App.Formulas nfNav — Orden + Roles).
const NAV = {
  BackOffice: ['Inicio', 'Por transcribir', 'Transcritas hoy'],
  Solicitante: ['Inicio', 'Nueva solicitud', 'Mis solicitudes'],
  Supervisor: ['Inicio', 'Aprobaciones'],
  Admin: ['Inicio', 'Monitoreo de carga', 'Reconciliación', 'Cierres', 'Agentes y usuarios', 'Datos maestros'],
};
const ORDEN = ['Inicio', 'Por transcribir', 'Transcritas hoy', 'Nueva solicitud', 'Mis solicitudes', 'Aprobaciones',
  'Monitoreo de carga', 'Reconciliación', 'Cierres', 'Agentes y usuarios', 'Datos maestros'];
const navEsperada = (...roles) => ORDEN.filter(t => roles.some(r => NAV[r].includes(t)));

// Sección de KPIs de Inicio por rol (contenedores de scrInicio).
const SECCION = { BackOffice: 'conKpisBoIni', Solicitante: 'conKpisSolIni', Supervisor: 'conKpisSupIni', Admin: 'conKpisAdmIni' };

async function abrir(suite, usuario, fase, rolEfectivo, opts = {}) {
  console.log(`\n··· Abriendo player como ${usuario} (${rolEfectivo}) — fase ${fase}`);
  const app = await pl.abrirApp(suite.browser, usuario, { etiqueta: `${fase}:scrInicio(frío)`, ...opts });
  inf.registrarRed(fase, usuario, rolEfectivo, app.red);
  inf.registrarApertura(fase, usuario, rolEfectivo, app.version);
  app.fase = fase;
  return app;
}

async function ir(app, titulo, pantalla) { await pl.navegar(app, titulo, { etiqueta: `${app.fase}:${pantalla}` }); }

// Errores de runtime visibles (banner nativo del player) — p.ej. falta de privilegios.
async function erroresVisibles(app) {
  const out = [];
  for (const f of app.page.frames()) {
    try {
      const t = await f.evaluate(() => document.body ? document.body.innerText : '');
      for (const l of t.split('\n')) if (/Error when trying|don't have permission|Incorrect argument type|is missing prv|error occurred/i.test(l)) out.push(l.trim().slice(0, 400));
    } catch (e) { /* frame cruzado */ }
  }
  return [...new Set(out)];
}

async function esperarNotif(app, re, ms = 12000) {
  const t = Date.now(); let ult = [];
  while (Date.now() - t < ms) {
    ult = await pl.notificaciones(app);
    const hit = ult.find(x => re.test(x)); if (hit) return hit;
    await pl.esperar(app, 400);
  }
  return ult.length ? `(otras: ${ult.join(' | ')})` : null;
}

// ComboBox clásico: abre, (opcional) escribe la búsqueda, elige la opción por texto.
async function elegirCombo(app, nombre, { buscar, opcion }) {
  const c = await pl.visible(app, nombre);
  await c.locator('[role="button"]').first().click();
  await pl.esperar(app, 1200);
  const opts = app.frame.locator('[role="option"]:visible');
  const iniciales = (await opts.allInnerTexts()).map(s => s.trim());
  if (buscar) {
    // Al abrir, el foco queda en el searchbox del flyout (powerapps-flyout-portal): se tipea ahí.
    await app.page.keyboard.type(buscar, { delay: 80 });
    await app.red.quieta(app.page, { quietoMs: 2500, maxMs: 20000, minMs: 1500 });
    await pl.esperar(app, 2000);
  }
  const textos = (await opts.allInnerTexts()).map(s => s.trim());
  const sinResultados = await app.frame.locator('[class*="NoResults"]:visible').count() > 0;
  const o = opts.filter({ hasText: opcion }).first();
  if (!(await o.count())) { await app.page.keyboard.press('Escape'); return { ok: false, textos, iniciales, sinResultados }; }
  await o.click(); await pl.esperar(app, 800);
  if (await app.frame.locator('[role="option"]:visible').count()) await app.page.keyboard.press('Escape');
  return { ok: true, textos, iniciales, sinResultados };
}

// Buscador (Classic/TextInput) + galería de resultados — el patrón que reemplazó a los ComboBox
// clásicos sobre Dataverse (B-04: el combo no pintaba el texto de los registros).
// Tipea `buscar` en el TextInput `buscador` (DelayOutput: la consulta sale al terminar de tipear),
// espera la red quieta y devuelve las filas visibles de `galeria`. Si el buscador está oculto porque ya
// hay una selección (tarjeta con botón "Cambiar"), primero pulsa `cambiar`.
// Filas: cada `[role="listitem"]` de la galería; si el player no expone ese rol, el overlay de fila
// (`btnFila*`, cuyo Text describe la fila: el patrón de la app).
async function buscarEnBuscador(app, { buscador, galeria, buscar, cambiar }) {
  if (cambiar && !(await pl.existeVisible(app, buscador)) && await pl.existeVisible(app, cambiar)) {
    await (await pl.visible(app, cambiar)).click(); await pl.esperar(app, 1000);
  }
  const cont = await pl.visible(app, buscador);
  const inp = cont.locator('input, textarea').first();
  await inp.click(); await inp.fill('');
  await inp.pressSequentially(buscar, { delay: 60 });
  await app.red.quieta(app.page, { quietoMs: 2500, maxMs: 20000, minMs: 1500 });
  await pl.esperar(app, 1500);
  const gal = pl.ctl(app, galeria).locator('visible=true').first();
  if (!(await gal.count())) return { filas: null, textos: [], sinResultados: true, galeriaVisible: false };
  let filas = gal.locator('[role="listitem"]').locator('visible=true');
  if (!(await filas.count())) filas = gal.locator('[data-control-name^="btnFila"]').locator('visible=true');
  const textos = (await filas.allInnerTexts()).map(s => s.replace(/\s+/g, ' ').trim()).filter(Boolean);
  return { filas, textos, sinResultados: textos.length === 0, galeriaVisible: true };
}

// Busca y ELIGE: toma la fila que contiene `opcion` y la pulsa vía su overlay (btnFila*).
// Devuelve { ok, textos, sinResultados } como elegirCombo (sin `iniciales`: con < 2 caracteres la
// galería no consulta ni se muestra — B-19).
async function elegirEnBuscador(app, { buscador, galeria, buscar, opcion, cambiar }) {
  const r = await buscarEnBuscador(app, { buscador, galeria, buscar, cambiar });
  if (!r.filas) return { ok: false, textos: r.textos, sinResultados: true };
  const fila = r.filas.filter({ hasText: opcion }).first();
  if (!(await fila.count())) return { ok: false, textos: r.textos, sinResultados: r.sinResultados };
  const nombre = await fila.getAttribute('data-control-name');
  const overlay = nombre && nombre.startsWith('btnFila') ? fila : fila.locator('[data-control-name^="btnFila"]').first();
  await overlay.click(); await pl.esperar(app, 1200);
  return { ok: true, textos: r.textos, sinResultados: r.sinResultados };
}

// Estado deshabilitado de una casilla clásica (Classic/CheckBox).
async function chkDeshabilitado(app, nombre) {
  const c = await pl.visible(app, nombre);
  return c.evaluate(el => {
    const cb = el.querySelector('[role="checkbox"], input[type="checkbox"]');
    if (cb && (cb.disabled || cb.getAttribute('aria-disabled') === 'true')) return true;
    return !!el.querySelector('[aria-disabled="true"], .disabled, [disabled]');
  });
}

// Inicio: KPIs en FRÍO (recién abierta) y en TIBIO (después de salir y volver).
async function kpisInicio(app, otraPantalla, nombreOtra) {
  const frio = await pl.kpis(app);
  await ir(app, otraPantalla, nombreOtra);
  await ir(app, 'Inicio', 'scrInicio(tibio)');
  const tibio = await pl.kpis(app);
  return { frio, tibio };
}

// Cambia membresías de un usuario de prueba (quitar/agregar teams) y espera la propagación.
async function cambiarTeams(usuario, { quitar = [], agregar = [] }) {
  const uid = cfg.usuarios[usuario].id;
  for (const t of quitar) await qa.quitarDeTeam(cfg.teams[t], uid);
  for (const t of agregar) await qa.agregarATeam(cfg.teams[t], uid);
  console.log(`   ↺ ${usuario}: -[${quitar}] +[${agregar}] — esperando propagación de privilegios`);
  await new Promise(r => setTimeout(r, 30000));
}

// Verifica nav + secciones de Inicio + pie de rol. Devuelve true si todo cuadra.
async function verificarRol(e, app, roles, etiquetaPie) {
  const nav = await pl.itemsNav(app);
  e.check(JSON.stringify(nav) === JSON.stringify(navEsperada(...roles)), `nav = ítems de ${roles.join('+')}`, navEsperada(...roles), nav);
  for (const [rol, cont] of Object.entries(SECCION)) {
    const vis = await pl.existeVisible(app, cont);
    e.check(vis === roles.includes(rol), `sección Inicio ${rol} ${roles.includes(rol) ? 'visible' : 'oculta'}`, roles.includes(rol), vis);
  }
  if (etiquetaPie) {
    const pie = await pl.texto(app, 'lblRolNavCmp').catch(() => null);
    e.check(pie && etiquetaPie.test(pie), 'pie de la nav muestra el rol', String(etiquetaPie), pie);
  }
}

// Respuestas de error (HTTP >= 400) de Dataverse capturadas en la red de `app` (opcional: solo una etiqueta).
function erroresRed(app, etiqueta) {
  const out = [];
  for (const en of app.red.entradas) {
    if (etiqueta && en.pantalla !== etiqueta) continue;
    (en.respuestas || []).forEach((r, i) => {
      if (r.status >= 400) out.push(`${r.status} ${(en.subs[i] || en.subs[0] || {}).metodo || ''} ${((en.subs[i] || en.subs[0] || {}).ruta || '').split('?')[0]} ${r.error || ''}`.slice(0, 260));
    });
  }
  return out;
}

// E-01: la app abre SIN banners de error de runtime para ese rol (permisos, tipos) y SIN respuestas 4xx/5xx
// de Dataverse en la carga en frío (B-05: mínimo privilegio, B-06: "expected a table").
async function errorAlAbrir(id, app, rol) {
  return inf.correr(id, `Al abrir como ${rol}: sin banners de error de runtime ni respuestas de error (403) de Dataverse`, rol, async (e) => {
    const err = await c_err(app);
    e.check(err.length === 0, 'sin errores visibles al abrir (banner)', [], err);
    const red = erroresRed(app, `${app.fase}:scrInicio(frío)`);
    e.check(red.length === 0, 'sin respuestas HTTP >= 400 de Dataverse en la carga en frío', [], red);
    e.evid(await pl.foto(app, `${id}-al-abrir`));
  });
}
const c_err = (app) => erroresVisibles(app);

const num = s => (s === null || s === undefined || s === '' ? null : Number(String(s).replace(/[^\d.-]/g, '')));

module.exports = { errorAlAbrir, erroresRed, NAV, navEsperada, SECCION, abrir, ir, erroresVisibles, esperarNotif, elegirCombo, buscarEnBuscador, elegirEnBuscador, chkDeshabilitado, kpisInicio, cambiarTeams, verificarRol, num };
