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

// E-01: la app abre SIN banners de error de runtime para ese rol (permisos, tipos).
async function errorAlAbrir(id, app, rol) {
  return inf.correr(id, `Al abrir como ${rol}: sin banners de error de runtime (permisos / tipos)`, rol, async (e) => {
    const err = await c_err(app);
    e.check(err.length === 0, 'sin errores visibles al abrir', [], err);
    if (err.length) e.evid(await pl.foto(app, `${id}-errores`));
  });
}
const c_err = (app) => erroresVisibles(app);

const num = s => (s === null || s === undefined || s === '' ? null : Number(String(s).replace(/[^\d.-]/g, '')));

module.exports = { errorAlAbrir, NAV, navEsperada, SECCION, abrir, ir, erroresVisibles, esperarNotif, elegirCombo, chkDeshabilitado, kpisInicio, cambiarTeams, verificarRol, num };
