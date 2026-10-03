#!/usr/bin/env node
// Runner E2E de "App - CM - Backoffice Canvas" sobre el PLAYER real (DEV).
// Uso:  node run.js [--fases A,B,C,D,E,F,G,X] [--solo-limpieza]
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
const fases = args.includes('--fases') ? fasesArg : ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'X'];

// D-01 — análisis de delegación de TODA la red capturada (por fase y pantalla).
function d01() {
  const e = inf.escenario('D-01', 'Intercepción de red: listas/conteos delegados ($filter/$apply) — sin GET sin filtro a tablas grandes, sin N+1, sin errores', 'todos');
  const out = inf.volcar(); // genera el análisis
  const res = JSON.parse(fs.readFileSync(out, 'utf8'));
  for (const d of res.delegacion) {
    for (const [pantalla, lista] of Object.entries(d.pantallas)) {
      const gets = lista.filter(x => x.metodo === 'GET');
      const alertas = lista.filter(x => x.alerta);
      // Errores de LECTURA (permisos/consultas) cuentan acá; los de ESCRITURA los juzga su escenario funcional
      // (X-02 provoca uno a propósito; F-02c puede fallar por la app) y se listan como nota.
      const errores = lista.filter(x => (x.status >= 400 || x.error) && x.metodo === 'GET');
      const errEscr = lista.filter(x => (x.status >= 400 || x.error) && x.metodo && x.metodo !== 'GET');
      e.check(alertas.length === 0, `${pantalla} (${d.rolEfectivo}): ${gets.length} GET, sin consultas sin filtro a tablas grandes ni N+1`, 'ninguna', alertas.map(a => `${a.entidad}: ${a.veredicto} ${a.ruta || ''}`.slice(0, 220)));
      if (errores.length) e.check(false, `${pantalla} (${d.rolEfectivo}): respuestas de error de Dataverse en LECTURAS`, 'ninguna', errores.map(x => `${x.status} ${x.entidad} ${x.error || ''}`.slice(0, 220)));
      if (errEscr.length) e.nota(`${pantalla} (${d.rolEfectivo}): escrituras rechazadas (ver su escenario): ${errEscr.map(x => `${x.status} ${x.metodo} ${x.entidad} ${x.error || ''}`.slice(0, 200)).join(' | ')}`);
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
  // El mapeo fijo de roles (lib/config.js) es precondición: si la foto no coincide, se avisa (y se restaura la foto).
  const esperado = cfg.membresiasEsperadas();
  const comoMapa = (foto) => Object.fromEntries(Object.values(foto).map(m => [m.nombre, m.upn.map(x => x.toLowerCase()).sort()]));
  const difMapeo = (foto) => Object.keys(esperado).filter(t => JSON.stringify(comoMapa(foto)[t]) !== JSON.stringify(esperado[t]));
  if (difMapeo(fotoMembresias).length) inf.hallazgo(`Membresías al inicio NO coinciden con el mapeo fijo en: ${difMapeo(fotoMembresias).join(', ')} — esperado ${JSON.stringify(esperado)}`);

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
    if (fases.some(f => f !== 'X')) {
      Object.assign(suite.datos, await qa.sembrar());
      console.log(`Siembra QA: ${suite.datos.int1.sanic_cm_in_numerointeraccion}, ${suite.datos.int2.sanic_cm_in_numerointeraccion}, cuentas ${suite.datos.cuentas.map(x => x.sanic_cm_ct_numerodecuenta)}, agente ${suite.datos.agente.sanic_cm_ag_nombre}`);
    }
    for (const f of fases) {
      console.log(`\n================ FASE ${f} ================`);
      try { await FASES[f](suite); }
      catch (err) { inf.hallazgo(`Fase ${f} abortada por excepción del runner: ${err.message.slice(0, 300)}`); console.error(err); }
    }
  } finally {
    await suite.browser.close().catch(() => {});
    await cerrarTodo();
    d01();
    let mapeoFinal = null;
    try { const fin = await qa.membresias(); mapeoFinal = { comoMapeo: difMapeo(fin).length === 0, diferencias: difMapeo(fin), equipos: comoMapa(fin) }; } catch (e) { mapeoFinal = { error: e.message }; }
    const p = inf.volcar({ app: { nombre: app.name, version: app.appversion, publicada: app.lastpublishtime }, membresiasInicio: fotoMembresias, limpieza, restauracion, mapeoFinal, agentes });
    console.log('Membresías finales = mapeo fijo:', JSON.stringify(mapeoFinal));
    console.log('\nLimpieza:', JSON.stringify(limpieza && limpieza.restantes), '· errores:', JSON.stringify(Object.fromEntries(Object.entries((limpieza && limpieza.borrado) || {}).map(([k, x]) => [k, x.errores.length]))));
    console.log('Agentes reales intactos:', JSON.stringify(agentes));
    console.log('Membresías restauradas:', restauracion && restauracion.iguales, JSON.stringify(restauracion && restauracion.cambios));
    console.log('\nRESUMEN'); for (const e of inf.escenarios) console.log(`  ${e.id.padEnd(7)} ${e.estado}`);
    console.log(`\n${p}`);
  }
}

main().catch(e => { console.error('ERROR FATAL', e); process.exit(1); });
