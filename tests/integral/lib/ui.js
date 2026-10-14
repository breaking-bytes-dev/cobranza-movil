// Helpers de UI de la suite integral: abrir/cerrar la app (UN solo Chromium a la vez), capturas seguras y filas de galería.
// Reutiliza el arnés del backoffice (player.js, comun.js) y el del cobrador (localizador-canvas/lib/app.js).
const fs = require('fs');
const path = require('path');
const pl = require('../../backoffice-canvas/lib/player');
const c = require('../../backoffice-canvas/escenarios/comun');
const lo = require('../../localizador-canvas/lib/app');

// Abre la app del BackOffice como `usuario` en un Chromium NUEVO. Siempre se cierra con cerrar() en un finally.
async function abrirBackoffice(usuario, etiqueta) {
  console.log(`  ··· abriendo la app de BackOffice como ${usuario} (carga en frío, 1 a 2 min)`);
  const browser = await pl.lanzar();
  try {
    const app = await pl.abrirApp(browser, usuario, { etiqueta: `${etiqueta}:scrInicio(frío)` });
    app.fase = etiqueta; app.browser = browser;
    return app;
  } catch (e) { await browser.close().catch(() => {}); throw e; }
}

// Abre la app del cobrador (web player) como User5 en un Chromium NUEVO.
async function abrirCobrador() {
  console.log('  ··· abriendo la app del cobrador como user5 (carga en frío, 1 a 2 min)');
  const browser = await pl.lanzar();
  try {
    const app = await lo.abrir(browser);
    app.browser = browser;
    return app;
  } catch (e) { await browser.close().catch(() => {}); throw e; }
}

async function cerrar(app) {
  if (!app) return;
  await pl.cerrar(app);
  if (app.browser) await app.browser.close().catch(() => {});
}

// Captura de pantalla. Las pantallas que pueden mostrar un PAN completo (detalle de Por transcribir / panel de Transcritas)
// NO se capturan nunca: se pasa `sensible: true` y la función no hace nada.
async function captura(ctx, app, nombre, { sensible = false } = {}) {
  if (sensible) return null;
  try {
    const dir = path.join(ctx.dir, 'capturas'); fs.mkdirSync(dir, { recursive: true });
    const p = path.join(dir, `${ctx.faseActual}-${nombre}.png`);
    await app.page.screenshot({ path: p });
    return p;
  } catch (e) { return null; }
}

// Acciones con el nombre del control en el error (los timeouts de Playwright no dicen cuál control faltó).
const conControl = async (nombre, fn) => { try { return await fn(); } catch (e) { e.message = `[control ${nombre}] ${String(e.message).split('\n')[0]}`; throw e; } };
const tocar = (app, nombre, opts) => conControl(nombre, () => lo.tocar(app, nombre, opts));
const escribir = (app, nombre, valor, opts) => conControl(nombre, () => lo.escribir(app, nombre, valor, opts));
const clic = (app, nombre) => conControl(nombre, async () => (await pl.visible(app, nombre)).click());

// Devuelve la función que usa ctx.paso() para dejar una captura cuando un paso falla (no hace nada si es sensible).
const capturador = (ctx, app, opts) => (nombre) => captura(ctx, app, nombre, opts);

const ir = (app, titulo, etiqueta) => pl.navegar(app, titulo, { etiqueta: `${app.fase}:${etiqueta}` });

// Filas (items) de una galería: `[role="listitem"]` visibles o, si el player no los expone, los overlays btnFila*.
function filas(app, galeria) {
  const gal = pl.ctl(app, galeria).locator('visible=true').first();
  return {
    gal,
    async lista() {
      let f = gal.locator('[role="listitem"]').locator('visible=true');
      if (!(await f.count())) f = gal.locator('[data-control-name^="btnFila"]').locator('visible=true');
      return f;
    },
  };
}
async function textosFilas(app, galeria) {
  const f = await filas(app, galeria).lista();
  return (await f.allInnerTexts()).map((s) => s.replace(/\s+/g, ' ').trim()).filter(Boolean);
}

// Espera robusta de un valor de pantalla que se carga de forma asíncrona (KPIs de Inicio, texto de un botón calculado…).
// El problema que resuelve: leer UNA vez justo después de abrir/navegar agarra el valor intermedio (vacío, "sin carga", "0",
// un conteo parcial) mientras la app todavía está resolviendo sus consultas, y el chequeo falla de forma intermitente.
// Sondea `leer()` cada `intervalo` ms hasta que el valor (a) no sea el de "cargando" y (b) sea IGUAL en dos lecturas
// consecutivas. `listo(valor)` es opcional y afina (a): por ejemplo, "coincide con lo que dice la API". Si vence el plazo
// devuelve la última lectura (NO lanza): el chequeo de afuera sigue afirmando lo mismo y falla con el valor real a la vista.
const esVacio = (x) => x === null || x === undefined || x === '' || (typeof x === 'string' && /^(…|\.\.\.|—|-)$/.test(x.trim()));
async function esperarEstable(leer, { timeout = 40000, intervalo = 500, listo = (x) => !esVacio(x) } = {}) {
  const t0 = Date.now(); let previo, tieneprevio = false, ult, ultErr;
  for (;;) {
    let actual;
    try { ult = await leer(); ultErr = null; actual = JSON.stringify(ult); } catch (e) { ultErr = e; tieneprevio = false; }   // control aún no pintado / frame en transición
    if (actual !== undefined && listo(ult) && tieneprevio && actual === previo) return ult;
    previo = actual; tieneprevio = actual !== undefined && listo(ult);
    if (Date.now() - t0 + intervalo > timeout) { if (ultErr && ult === undefined) throw ultErr; return ult; }
    await new Promise((r) => setTimeout(r, intervalo));
  }
}

module.exports = { esperarEstable, esVacio, capturador, tocar, escribir, clic, conControl, pl, c, lo, abrirBackoffice, abrirCobrador, cerrar, captura, ir, filas, textosFilas };
