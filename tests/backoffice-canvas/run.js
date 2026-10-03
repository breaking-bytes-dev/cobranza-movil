#!/usr/bin/env node
// Runner E2E de "App - CM - Backoffice Canvas" sobre el PLAYER real (DEV).
// Uso:  node run.js [--fases L,A,B,C,D,E,F,G,X] [--solo-limpieza]
// Ver README.md. Las sesiones y secretos se leen de rutas FUERA del repo (lib/config.js).
const fs = require('fs');
const path = require('path');
const cfg = require('./lib/config');
const dv = require('./lib/dataverse');
const qa = require('./lib/datosqa');
const pl = require('./lib/player');
const inf = require('./lib/informe');

const FASES = {
  A: require('./escenarios/A-admin'),
  B: require('./escenarios/B-solicitante'),
  C: require('./escenarios/C-supervisor'),
  D: require('./escenarios/D-backoffice'),
  E: require('./escenarios/E-multirol'),
  F: require('./escenarios/F-carga-hoy'),
  G: require('./escenarios/G-reapertura'),
  X: require('./escenarios/X-responsive'),
};

const args = process.argv.slice(2);
const soloLimpieza = args.includes('--solo-limpieza');
const fasesArg = (args[args.indexOf('--fases') + 1] || '').split(',').filter(Boolean);
const fases = args.includes('--fases') ? fasesArg : ['L', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'X'];

// L — licencias: User1/User2 abren el player y se documenta el bloqueo si la licencia está vencida.
async function faseL(suite) {
  for (const [u, id, rol] of [['user1', 'R-02', 'BackOffice'], ['user2', 'R-03', 'Solicitante']]) {
    await inf.correr(id, `${cfg.usuarios[u].upn.split('@')[0]} (${rol}) abre la app y ve SOLO su nav`, rol, async (e) => {
      const ctx = await suite.browser.newContext({ storageState: cfg.sesionPath(u), viewport: { width: 1600, height: 950 }, timezoneId: 'America/Managua' });
      const page = await ctx.newPage();
      try {
        await page.goto(cfg.playerUrl, { waitUntil: 'domcontentloaded', timeout: 120000 });
        let txt = '';
        for (let i = 0; i < 30; i++) {
          await page.waitForTimeout(3000);
          txt = (await Promise.all(page.frames().map(f => f.evaluate(() => document.body ? document.body.innerText : '').catch(() => '')))).join('\n');
          if (/trial has expired|galNavCmp|Inicio/i.test(txt)) break;
        }
        const p = path.join(cfg.resultadosDir, 'capturas', `${id}-${u}-licencia.png`);
        fs.mkdirSync(path.dirname(p), { recursive: true }); await page.screenshot({ path: p });
        e.evid(path.relative(cfg.resultadosDir, p));
        if (/trial has expired/i.test(txt)) e.bloquear(`licencia de Power Apps VENCIDA para ${cfg.usuarios[u].upn} ("Your Power Apps trial has expired"). Extenderla exige aceptar términos y dar un teléfono a nombre del usuario → no se hizo. Cubierto con sustituto (${id}*).`);
        else e.nota('el player abrió: correr la fase con este usuario');
      } finally { await ctx.close(); }
    });
  }
}

// D-01 — análisis de delegación de TODA la red capturada (por fase y pantalla).
function d01() {
  const e = inf.escenario('D-01', 'Intercepción de red: listas/conteos delegados ($filter/$apply) — sin GET sin filtro a tablas grandes, sin N+1, sin errores', 'todos');
  const out = inf.volcar(); // genera el análisis
  const res = JSON.parse(fs.readFileSync(out, 'utf8'));
  for (const d of res.delegacion) {
    for (const [pantalla, lista] of Object.entries(d.pantallas)) {
      const gets = lista.filter(x => x.metodo === 'GET');
      const alertas = lista.filter(x => x.alerta);
      const errores = lista.filter(x => x.status >= 400 || x.error);
      e.check(alertas.length === 0, `${pantalla} (${d.rolEfectivo}): ${gets.length} GET, sin consultas sin filtro a tablas grandes ni N+1`, 'ninguna', alertas.map(a => `${a.entidad}: ${a.veredicto} ${a.ruta || ''}`.slice(0, 220)));
      if (errores.length) e.check(false, `${pantalla} (${d.rolEfectivo}): respuestas de error de Dataverse`, 'ninguna', errores.map(x => `${x.status} ${x.entidad} ${x.error || ''}`.slice(0, 220)));
    }
  }
  e.fin();
}

async function main() {
  fs.mkdirSync(cfg.resultadosDir, { recursive: true });
  console.log(`Resultados en ${cfg.resultadosDir}`);
  // 0) limpieza previa de restos QA de corridas anteriores
  const previa = await qa.limpiar();
  console.log('Limpieza previa:', JSON.stringify(previa.restantes));
  try { fs.unlinkSync(path.join(cfg.resultadosDir, 'registro-qa.json')); } catch (e) { /* no había */ }
  if (soloLimpieza) return;

  const app = (await dv.get("canvasapps?$select=name,displayname,appversion,lastpublishtime&$filter=displayname eq 'App - CM - Backoffice Canvas'")).value[0] || {};
  const fotoMembresias = await qa.membresias();
  fs.writeFileSync(path.join(cfg.resultadosDir, 'membresias-inicio.json'), JSON.stringify(fotoMembresias, null, 1));
  console.log('Membresías al inicio:', Object.values(fotoMembresias).map(m => `${m.nombre}=[${m.upn.join(', ')}]`).join(' · '));

  // Red de seguridad: foto de los agentes REALES (la pantalla Agentes re-escribe vínculos al abrirse —
  // hallazgo de esta suite). Al final se verifica que usuario/email/activo sigan idénticos y, si no, se restauran.
  const SEL_AG = 'sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre,sanic_cm_ag_emailusuario,_sanic_cm_ag_usuarioid_value,sanic_cm_ag_activo';
  const fotoAgentes = await dv.getAll(SEL_AG);
  const suite = { browser: await pl.lanzar(), datos: {} };
  let limpieza = null, restauracion = null, agentes = null;
  const cerrarTodo = async () => {
    try { limpieza = await qa.limpiar(); } catch (e) { limpieza = { error: e.message }; }
    try { restauracion = await qa.restaurarMembresias(fotoMembresias); } catch (e) { restauracion = { error: e.message }; }
    try {
      const ahora = await dv.getAll(SEL_AG); const restaurados = [];
      for (const a of fotoAgentes) {
        const b = ahora.find(x => x.sanic_cm_tbl_agenteid === a.sanic_cm_tbl_agenteid); if (!b) continue;
        if (a.sanic_cm_ag_emailusuario !== b.sanic_cm_ag_emailusuario || a._sanic_cm_ag_usuarioid_value !== b._sanic_cm_ag_usuarioid_value || a.sanic_cm_ag_activo !== b.sanic_cm_ag_activo) {
          await dv.patch('sanic_cm_tbl_agentes', a.sanic_cm_tbl_agenteid, {
            sanic_cm_ag_emailusuario: a.sanic_cm_ag_emailusuario, sanic_cm_ag_activo: a.sanic_cm_ag_activo,
            'sanic_cm_ag_usuarioid@odata.bind': a._sanic_cm_ag_usuarioid_value ? `/systemusers(${a._sanic_cm_ag_usuarioid_value})` : null,
          }).catch(async () => { await dv.patch('sanic_cm_tbl_agentes', a.sanic_cm_tbl_agenteid, { sanic_cm_ag_emailusuario: a.sanic_cm_ag_emailusuario, sanic_cm_ag_activo: a.sanic_cm_ag_activo }); });
          restaurados.push(a.sanic_cm_ag_nombre);
        }
      }
      agentes = { identicos: restaurados.length === 0, restaurados };
    } catch (e) { agentes = { error: e.message }; }
  };
  process.on('SIGINT', async () => { console.log('\nInterrumpido — limpiando…'); await cerrarTodo(); inf.volcar({ app, limpieza, restauracion, interrumpido: true }); process.exit(130); });
  try {
    if (fases.some(f => f !== 'L' && f !== 'X')) {
      Object.assign(suite.datos, await qa.sembrar());
      console.log(`Siembra QA: ${suite.datos.int1.sanic_cm_in_numerointeraccion}, ${suite.datos.int2.sanic_cm_in_numerointeraccion}, cuentas ${suite.datos.cuentas.map(x => x.sanic_cm_ct_numerodecuenta)}, agente ${suite.datos.agente.sanic_cm_ag_nombre}`);
    }
    for (const f of fases) {
      console.log(`\n================ FASE ${f} ================`);
      try { await (f === 'L' ? faseL(suite) : FASES[f](suite)); }
      catch (err) { inf.hallazgo(`Fase ${f} abortada por excepción del runner: ${err.message.slice(0, 300)}`); console.error(err); }
    }
  } finally {
    await suite.browser.close().catch(() => {});
    await cerrarTodo();
    d01();
    const p = inf.volcar({ app: { nombre: app.name, version: app.appversion, publicada: app.lastpublishtime }, membresiasInicio: fotoMembresias, limpieza, restauracion, agentes });
    console.log('\nLimpieza:', JSON.stringify(limpieza && limpieza.restantes), '· errores:', JSON.stringify(Object.fromEntries(Object.entries((limpieza && limpieza.borrado) || {}).map(([k, x]) => [k, x.errores.length]))));
    console.log('Agentes reales intactos:', JSON.stringify(agentes));
    console.log('Membresías restauradas:', restauracion && restauracion.iguales, JSON.stringify(restauracion && restauracion.cambios));
    console.log('\nRESUMEN'); for (const e of inf.escenarios) console.log(`  ${e.id.padEnd(7)} ${e.estado}`);
    console.log(`\n${p}`);
  }
}

main().catch(e => { console.error('ERROR FATAL', e); process.exit(1); });
