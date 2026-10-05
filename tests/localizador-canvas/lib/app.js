// Apertura de la app del COBRADOR en el web player (DEV) con la sesión de User5, para las pruebas que no
// necesitan el teléfono. Reutiliza red, selectores y capturas del arnés del backoffice.
// OJO: el web player corre la app EN LÍNEA (sin base local ni sincronización): lo offline se prueba en el teléfono.
const fs = require('fs');
const path = require('path');
const pl = require('../../backoffice-canvas/lib/player.js');
const cfg = require('../../backoffice-canvas/lib/config.js');

const APP_ID = 'dc5e56bb-9598-4f68-baa5-c04dc56ccba4';
const URL = `https://apps.powerapps.com/play/e/9c8994b8-b1bc-ee23-9184-2f6346502613/a/${APP_ID}?tenantId=7bcfee03-896e-4ad3-947b-8a2d9ce17974`;
const CACHE_APP = /^(PAAppLoadCache-|PAPreloadKeyV2-)/;  // ver B-15 en el arnés del backoffice
const esRuntime = f => /runtime-app\.[^/]*powerplatform\.com\/publishedapp/.test(f.url()) && !f.isDetached();

async function abrir(browser, { usuario = 'user5', viewport = { width: 430, height: 860 } } = {}) {
  const s = JSON.parse(fs.readFileSync(cfg.sesionPath(usuario), 'utf8'));
  for (const o of s.origins || []) o.localStorage = (o.localStorage || []).filter(i => !CACHE_APP.test(i.name));
  const ctx = await browser.newContext({ storageState: s, viewport, timezoneId: 'America/Managua', locale: 'es-NI',
                                         geolocation: { latitude: 12.1364, longitude: -86.2514 }, permissions: ['geolocation'] });
  const red = new pl.RedDataverse(); red.marcar('carga'); await red.instalar(ctx);
  const page = await ctx.newPage();
  const consola = []; page.on('console', m => { if (m.type() === 'error') consola.push(m.text().slice(0, 200)); });
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 120000 });
  const app = { ctx, page, red, usuario, consola, _frame: null };
  Object.defineProperty(app, 'frame', { get: () => {
    if (app._frame && !app._frame.isDetached()) return app._frame;
    const f = page.frames().filter(esRuntime).pop(); if (!f) throw new Error('Iframe del runtime no disponible'); return (app._frame = f);
  } });
  const t0 = Date.now();
  while (Date.now() - t0 < 150000) {
    for (const f of page.frames().filter(esRuntime)) {
      try { if (await f.locator('[data-control-name="cmpNav_1"]').count()) { app._frame = f; await red.quieta(page, { quietoMs: 6000, maxMs: 60000 }); return app; } } catch (e) {}
    }
    await page.waitForTimeout(1000);
  }
  throw new Error('La app del cobrador no terminó de cargar (no apareció cmpNav_1)');
}

async function foto(app, nombre) {
  const dir = path.join(__dirname, '..', 'resultados'); fs.mkdirSync(dir, { recursive: true });
  const p = path.join(dir, `${nombre}.png`); await app.page.screenshot({ path: p }); return p;
}

// Pantalla visible: cada pantalla tiene un control propio (los nombres de los componentes se repiten).
const MARCAS = { scrInicio: 'lblTitulo_1', scrCartera: 'galCartera_clientes', scrCliente: 'cmpHeaderBack_1',
  scrDetalleCuenta: 'btnRegistrar', scrInteraccion: 'btnRegistrar_1', scrPagos: 'galGrupos', scrSincronizacion: 'btnBuscar' };
async function pantalla(app) {
  for (const [scr, c] of Object.entries(MARCAS)) if (await pl.existeVisible(app, c)) return scr;
  return '?';
}
async function tocar(app, nombre, { esperaMs = 2500 } = {}) {
  await (await pl.visible(app, nombre)).click({ timeout: 15000 });
  await app.red.quieta(app.page, { quietoMs: 2500, maxMs: 30000, minMs: esperaMs });
}
module.exports = { ...pl, abrir, foto, pantalla, tocar, APP_ID };
