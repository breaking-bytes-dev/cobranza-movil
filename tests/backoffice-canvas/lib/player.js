// Helpers del PLAYER de Power Apps (apps.powerapps.com/play) para Playwright.
//
// Comportamientos del player en los que se apoyan estos helpers:
//  - La app corre dentro del iframe runtime-app.preview.powerplatform.com. Cada control
//    expone su nombre de Studio en el atributo data-control-name → selector estable
//    `[data-control-name="btnFilaNavCmp"]` (no hace falta depender de textos ni posiciones).
//  - Las consultas a Dataverse van como POST .../api/data/v9.0/$batch con cuerpo multipart.
//    `request.postData()` del evento 'request' viene VACÍO (Blob); con `context.route()` el
//    cuerpo SÍ está disponible → la intercepción de red se hace con route + continue.
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const cfg = require('./config');
const version = require('./version');

const ES_DV = /crm\d*\.dynamics\.com\/api\/data/;

// ── Red ────────────────────────────────────────────────────────────────────────────────
class RedDataverse {
  constructor() { this.entradas = []; this.etiqueta = 'carga'; this.t0 = Date.now(); this.ultima = Date.now(); this.pendientes = 0; }
  marcar(etiqueta) { this.etiqueta = etiqueta; }
  async instalar(ctx) {
    const porReq = new Map();
    await ctx.route(ES_DV, async (route) => {
      const r = route.request();
      let cuerpo = '';
      try { const b = r.postDataBuffer(); cuerpo = b ? b.toString('utf8') : ''; } catch (e) { /* sin cuerpo */ }
      const subs = [];
      if (/\$batch/.test(r.url())) {
        for (const m of cuerpo.matchAll(/^(GET|POST|PATCH|PUT|DELETE) (\S+) HTTP\/1\.1/mg)) subs.push({ metodo: m[1], ruta: decodeURIComponent(m[2].replace(/\+/g, ' ')) });
      } else {
        subs.push({ metodo: r.method(), ruta: decodeURIComponent(r.url().replace(/^.*\/api\/data\/v[\d.]+\//, '')) });
      }
      const e = { t: (Date.now() - this.t0) / 1000, pantalla: this.etiqueta, subs, cuerpoPatch: subs.some(s => s.metodo !== 'GET') ? extraerJsonBatch(cuerpo) : undefined };
      this.entradas.push(e); porReq.set(r, e); this.pendientes++; this.ultima = Date.now();
      await route.continue();
    });
    const fin = async (r, ok) => {
      const e = porReq.get(r); if (!e) return;
      this.pendientes = Math.max(0, this.pendientes - 1); this.ultima = Date.now();
      if (!ok) { e.fallo = r.failure()?.errorText; return; }
      try {
        const txt = await (await r.response()).text();
        e.respuestas = txt.split(/--batchresponse_[\w-]+/).map(parte => {
          const st = parte.match(/HTTP\/1\.1 (\d{3})/); if (!st) return null;
          const i = parte.indexOf('{'), j = parte.lastIndexOf('}');
          return resumirResp(st[1], i >= 0 && j > i ? parte.slice(i, j + 1) : '');
        }).filter(Boolean);
        if (!e.respuestas.length) e.respuestas = [{ status: (await r.response()).status() }];
      } catch (err) { /* respuesta no legible */ }
    };
    ctx.on('requestfinished', r => fin(r, true));
    ctx.on('requestfailed', r => fin(r, false));
  }
  // Espera "red quieta": sin requests nuevas a Dataverse durante `quietoMs`.
  async quieta(page, { quietoMs = 6000, maxMs = 60000, minMs = 3000 } = {}) {
    const t = Date.now(); await page.waitForTimeout(minMs);
    while (Date.now() - t < maxMs) {
      if (this.pendientes === 0 && Date.now() - this.ultima >= quietoMs) return true;
      await page.waitForTimeout(500);
    }
    return false;
  }
  de(pantalla) { return this.entradas.filter(e => e.pantalla === pantalla); }
}

function extraerJsonBatch(cuerpo) {
  const m = cuerpo.match(/\{[\s\S]*\}/); return m ? m[0].slice(0, 1500) : undefined;
}
function resumirResp(status, json) {
  const r = { status: Number(status) };
  let j = null; try { j = JSON.parse(json); } catch (e) { /* no-json */ }
  if (!j) return r;
  if (j['@odata.context']) r.contexto = j['@odata.context'].replace(/^.*#/, '').slice(0, 160);
  if (Array.isArray(j.value)) {
    r.filas = j.value.length;
    if (j.value.length === 1 && j.value[0].result !== undefined) r.resultado = j.value[0].result;
  }
  if (j['@odata.count'] !== undefined) r.count = j['@odata.count'];
  if (j['@odata.nextLink']) r.nextLink = true;
  if (j.error) r.error = String(j.error.message || '').slice(0, 200);
  return r;
}

// ── Apertura ─────────────────────────────────────────────────────────────────────────────
async function lanzar() { return chromium.launch({ headless: cfg.headless }); }

// El player guarda la app en localStorage (PAAppLoadCache-*, PAPreloadKeyV2-*) y arranca con esa copia
// mientras revalida en segundo plano. Como la sesión guardada nunca se actualiza, se probaría la versión
// cacheada y no la publicada: se descartan esas claves y se conserva la autenticación (msal.*).
const CACHE_APP = /^(PAAppLoadCache-|PAPreloadKeyV2-)/;
function sesionSinCacheDeApp(usuario) {
  const s = JSON.parse(fs.readFileSync(cfg.sesionPath(usuario), 'utf8'));
  for (const o of s.origins || []) o.localStorage = (o.localStorage || []).filter(i => !CACHE_APP.test(i.name));
  return s;
}

// Abre la app y verifica que DEV sirva la versión desplegada (pie de la nav). En el diseño móvil la nav no se ve
// y no se verifica.
async function abrirApp(browser, usuario, opts = {}) {
  const ancho = (opts.viewport || { width: 1600 }).width;
  return version.abrirVerificada('backoffice', {
    abrir: () => abrirAppUnaVez(browser, usuario, opts),
    leer: async (app) => (ancho < 920 ? undefined : texto(app, 'lblVersionNavCmp', { timeout: 10000 }).catch(() => null)),
    cerrar: (app) => cerrar(app),
  });
}

async function abrirAppUnaVez(browser, usuario, { viewport = { width: 1600, height: 950 }, etiqueta = 'carga' } = {}) {
  const ctx = await browser.newContext({
    storageState: sesionSinCacheDeApp(usuario), viewport, timezoneId: 'America/Managua',
  });
  const red = new RedDataverse(); red.marcar(etiqueta); await red.instalar(ctx);
  const page = await ctx.newPage();
  const consola = [];
  page.on('console', m => { if (m.type() === 'error') consola.push(m.text().slice(0, 200)); });
  await page.goto(cfg.playerUrl, { waitUntil: 'domcontentloaded', timeout: 120000 });
  const app = { ctx, page, red, usuario, consola, _frame: null };
  // El iframe del runtime se REEMPLAZA durante la carga (preload → app): se resuelve perezoso.
  Object.defineProperty(app, 'frame', { get: () => frameApp(app) });
  // Carga en frío: esperar a que la nav lateral, la barra móvil o el estado "sin acceso" estén VISIBLES.
  // No basta con que el control exista: el iframe de precarga tiene la app oculta y daría una nav vacía.
  const sel = '[data-control-name="lblTituloNavCmp"], [data-control-name="cmpVacioIni"], [data-control-name="conBarraMovilEncCmp"]';
  const buscar = async () => {
    for (const f of page.frames().filter(esRuntime).reverse()) {
      try { if (await f.locator(sel).locator('visible=true').count()) return f; } catch (e) { /* frame en transición */ }
    }
    return null;
  };
  const t0 = Date.now(); let f = null;
  while (!f && Date.now() - t0 < cfg.tCargaMs + 90000) { f = await buscar(); if (!f) await page.waitForTimeout(1000); }
  if (!f) throw new Error('La app no terminó de cargar (sin nav ni estado sin-acceso visibles)');
  app._frame = f;
  await red.quieta(page, { quietoMs: 8000, maxMs: cfg.tCargaMs, minMs: 5000 });
  app._frame = (await buscar()) || app._frame;   // por si el iframe se reemplazó durante la espera
  await cerrarAviso(page);
  // Versión publicada que efectivamente cargó el player (traza para el reporte).
  app.version = await page.evaluate(() => Object.keys(localStorage)
    .filter(k => k.startsWith('PAPreloadKeyV2-')).map(k => k.slice(-20)).join(',')).catch(() => '');
  return app;
}

// El player muestra arriba "This app is in a Developer environment": tapa el encabezado y corre el contenido.
async function cerrarAviso(page) {
  for (const f of [page.mainFrame(), ...page.frames()]) {
    try {
      const b = f.locator('[role="alert"] button, [class*="MessageBar"] button, [class*="messageBar"] button').locator('visible=true').first();
      if (await b.count()) { await b.click({ timeout: 2000 }); await page.waitForTimeout(800); return true; }
    } catch (e) { /* sin aviso */ }
  }
  return false;
}

const esRuntime = f => /runtime-app\.[^/]*powerplatform\.com\/publishedapp/.test(f.url()) && !f.isDetached();
function frameApp(app) {
  if (app._frame && !app._frame.isDetached()) return app._frame;
  const f = app.page.frames().filter(esRuntime).pop();
  if (!f) throw new Error('Iframe del runtime no disponible');
  app._frame = f; return f;
}

async function cerrar(app) { try { await app.ctx.close(); } catch (e) { /* ya cerrado */ } }

// ── Localizadores ───────────────────────────────────────────────────────────────────────
const ctl = (app, nombre) => app.frame.locator(`[data-control-name="${nombre}"]`);

// Primer control VISIBLE con ese nombre (los componentes repiten nombres internos por pantalla).
async function visible(app, nombre, { timeout = 15000 } = {}) {
  const loc = ctl(app, nombre).locator('visible=true').first();
  await loc.waitFor({ state: 'visible', timeout });
  return loc;
}
async function texto(app, nombre, opts) { return (await (await visible(app, nombre, opts)).innerText()).trim(); }
async function existeVisible(app, nombre) { return (await ctl(app, nombre).locator('visible=true').count()) > 0; }

// Ítems de la nav lateral visibles (títulos).
async function itemsNav(app) {
  return (await ctl(app, 'lblTituloNavCmp').locator('visible=true').allInnerTexts()).map(s => s.trim()).filter(Boolean);
}

// Navega por la nav lateral por texto exacto del ítem y espera la red quieta.
async function navegar(app, titulo, { etiqueta, esperar = true } = {}) {
  if (etiqueta) app.red.marcar(etiqueta);
  const btn = ctl(app, 'btnFilaNavCmp').locator('visible=true').filter({ hasText: new RegExp(`^\\s*${escRe(titulo)}\\s*$`) }).first();
  await btn.click({ timeout: 20000 });
  if (esperar) await app.red.quieta(app.page, { quietoMs: 6000, maxMs: cfg.tOnVisibleMs + 40000, minMs: 4000 });
}

// KPIs visibles (componente cmpKpi): { ETIQUETA: valor }
async function kpis(app) {
  return app.frame.evaluate(() => {
    const out = {};
    const vis = (el) => !!el && el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden';
    document.querySelectorAll('[data-control-name="conRaizKpiCmp"]').forEach(r => {
      if (!vis(r)) return;
      const et = r.querySelector('[data-control-name="lblEtiquetaKpiCmp"]');
      const va = r.querySelector('[data-control-name="lblValorKpiCmp"]');
      if (et) out[et.innerText.trim()] = va ? va.innerText.trim() : null;
    });
    return out;
  });
}

// Botones de un control "Classic/Button": el <button> interno.
async function boton(app, nombre) { return (await visible(app, nombre)).locator('button').first(); }
async function botonHabilitado(app, nombre) {
  const b = await boton(app, nombre);
  const dis = await b.evaluate(el => el.disabled || el.getAttribute('aria-disabled') === 'true');
  return !dis;
}

// Escribir en un Classic/TextInput (input o textarea interno) y confirmar con Tab/Enter.
async function escribir(app, nombre, valor, { enter = false, tab = true } = {}) {
  const cont = await visible(app, nombre);
  const inp = cont.locator('input, textarea').first();
  await inp.click(); await inp.fill('');
  await inp.pressSequentially(valor, { delay: 30 });
  if (enter) await inp.press('Enter'); else if (tab) await inp.press('Tab');
}

// Notificaciones (Notify) visibles en el player: devuelve sus textos.
async function notificaciones(app) {
  const textos = [];
  for (const f of [app.frame, app.page.mainFrame()]) {
    try {
      const t = await f.evaluate(() => Array.from(document.querySelectorAll('[role="alert"], [role="status"], .notification, [class*="otification"]'))
        .filter(e => e.getClientRects().length > 0).map(e => e.innerText.trim()).filter(Boolean));
      textos.push(...t);
    } catch (e) { /* frame sin acceso */ }
  }
  return [...new Set(textos)];
}

// Captura de pantalla por paso.
async function foto(app, nombre) {
  const dir = path.join(cfg.resultadosDir, 'capturas'); fs.mkdirSync(dir, { recursive: true });
  const p = path.join(dir, `${nombre}.png`);
  await app.page.screenshot({ path: p });
  return path.relative(cfg.resultadosDir, p);
}

const escRe = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const esperar = (app, ms) => app.page.waitForTimeout(ms);

module.exports = {
  lanzar, abrirApp, cerrar, ctl, visible, texto, existeVisible, itemsNav, navegar, kpis,
  boton, botonHabilitado, escribir, notificaciones, foto, esperar, RedDataverse,
};
