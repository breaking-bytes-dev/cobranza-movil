#!/usr/bin/env node
// Prueba integral "0 a 100" de Cobranza Móvil en DEV: un CSV real por Smelter → reparto → todos los roles en las dos apps
// canvas → cierre → limpieza. Ver README.md.
// Uso:  cd tests/integral && NODE_PATH=../backoffice-canvas/node_modules node run.js [--hasta F5] [--solo-limpieza]
//       (depuración) --conservar (no limpia al final) · --desde F5 (reanuda sobre los datos de la última corrida conservada)
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const dv = require('../backoffice-canvas/lib/dataverse');
const v = require('../backoffice-canvas/lib/verdad');
const { Corrida, RAIZ, AGENTE, U, ahoraCorta, sinPAN } = require('./lib/ctx');
const datos = require('./lib/datos');

const FASES = [
  ['F0', 'Preparación (CSV QA)', 'carga', 'f0'],
  ['F1', 'Carga por Smelter + idempotencia', 'carga', 'f1'],
  ['F2', 'Reparto automático (flow de owner)', 'carga', 'f2'],
  ['F3', 'Admin (User4): monitoreo, reconciliación, agentes', 'admin', 'f3'],
  ['F4', 'Solicitante (User2): nuevas solicitudes', 'solicitante', 'f4'],
  ['F5', 'Supervisor (User3): aprobar y rechazar', 'supervisor', 'f5'],
  ['F6', 'Reparto tras aprobar', 'supervisor', 'f6'],
  ['F7', 'Cobrador (User5): cartera y cobro', 'cobrador', 'f7'],
  ['F8', 'BackOffice (User1): transcribir', 'backoffice', 'f8'],
  ['F9', 'Admin (User4): cierres y revocación', 'admin', 'f9'],
  ['F10', 'Solicitante (User2): seguimiento', 'solicitante', 'f10'],
];

const args = process.argv.slice(2);
const flag = (n) => args.includes(n);
const valor = (n) => (args.includes(n) ? args[args.indexOf(n) + 1] : null);
const soloLimpieza = flag('--solo-limpieza'), conservar = flag('--conservar');
const hasta = valor('--hasta'), desde = valor('--desde');
for (const f of [hasta, desde]) if (f && !FASES.some((x) => x[0] === f.toUpperCase())) { console.error(`Fase desconocida: ${f} (válidas: ${FASES.map((x) => x[0]).join(', ')})`); process.exit(2); }

const nuevoRunid = () => `${String(new Date().getDate()).padStart(2, '0')}${Array.from({ length: 3 }, () => 'ABCDEFGHJKLMNPQRSTUVWXYZ'[crypto.randomInt(24)]).join('')}`;

// Agente fijo: se fotografía al inicio y se compara al final (la suite no lo toca; esto lo demuestra).
const SEL_AG = 'sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre,sanic_cm_ag_emailusuario,_sanic_cm_ag_usuarioid_value,sanic_cm_ag_activo&$filter=startswith(sanic_cm_ag_nombre,\'QA-E2E Localizador User\')&$orderby=sanic_cm_ag_nombre';
const fotoAgentes = async () => (await dv.get(SEL_AG)).value.map((a) => [a.sanic_cm_ag_nombre, a.sanic_cm_ag_emailusuario, a._sanic_cm_ag_usuarioid_value, a.sanic_cm_ag_activo]);
// Intocables: tienen que seguir existiendo tal cual.
async function intocables() {
  const as = (await dv.get("sanic_cm_tbl_asignacions?$select=sanic_cm_as_numero,sanic_cm_as_estado&$filter=sanic_cm_as_numero eq 'AS-2077' or sanic_cm_as_numero eq 'AS-2134'")).value.map((x) => `${x.sanic_cm_as_numero}:${x.sanic_cm_as_estado}`).sort();
  const pg = (await dv.get("sanic_cm_tbl_pagos?$select=sanic_cm_pg_numerorecibo&$filter=sanic_cm_pg_numerorecibo eq 'QA-R1'")).value.length;
  const inn = (await dv.get("sanic_cm_tbl_interaccions?$select=sanic_cm_in_numerointeraccion&$filter=sanic_cm_in_numerointeraccion eq 'IN-1065'")).value.length;
  return { as, pago: pg, interaccion: inn };
}

async function limpiezaFinal(ctx, base) {
  ctx.faseActual = 'F11';
  const t = Date.now();
  console.log('\n══ F11 · Limpieza (siempre)');
  try {
    const r = await datos.limpiar();
    const total = Object.entries(r.borrados).map(([k, n]) => `${k}:${n}`).join(' ');
    ctx.chk('F11-01', r.errores.length === 0, `borrado en orden de dependencias (${total})${r.errores.length ? ' · ERRORES: ' + r.errores.join(' | ') : ''}`);
    const vivos = Object.entries(r.restantes).filter(([, n]) => n > 0);
    ctx.chk('F11-02', vivos.length === 0, `verificación: nada con el prefijo QA-E2E-INT queda en Dataverse${vivos.length ? ' — QUEDAN: ' + vivos.map(([k, n]) => `${k}:${n}`).join(', ') : ''}`);
    const csvs = []; for (const d of fs.readdirSync(RAIZ)) { const dir = path.join(RAIZ, d); if (fs.statSync(dir).isDirectory()) for (const f of fs.readdirSync(dir)) if (/\.csv$/i.test(f)) csvs.push(path.join(d, f)); }
    ctx.chk('F11-03', csvs.length === 0, `ningún CSV de carga queda en resultados/${csvs.length ? ': ' + csvs.join(', ') : ''}`);
    ctx.marcarLimpiado(vivos.length === 0 && r.errores.length === 0);
  } catch (e) { ctx.chk('F11-01', false, `la limpieza falló: ${sinPAN(e.message).slice(0, 300)}`); }
  if (base) try {
    const ahora = { agentes: await fotoAgentes(), intocables: await intocables(), cargaHoy: await v.cargaHoy() };
    ctx.chk('F11-04', JSON.stringify(ahora.agentes) === JSON.stringify(base.agentes), `los agentes QA no cambiaron (nombre, correo, usuario, activo): ${ahora.agentes.length} agentes`);
    ctx.chk('F11-05', JSON.stringify(ahora.intocables) === JSON.stringify(base.intocables) && ahora.intocables.as.length === 2 && ahora.intocables.pago === 1 && ahora.intocables.interaccion === 1,
      `intocables vivos: AS-2077 y AS-2134 (${ahora.intocables.as.join(', ')}), pago QA-R1 (${ahora.intocables.pago}), interacción IN-1065 (${ahora.intocables.interaccion})`);
    ctx.chk('F11-06', ahora.cargaHoy === base.cargaHoy, `Reconciliación/Monitoreo vuelven a lo de antes: filas de carga de hoy ${ahora.cargaHoy} (antes de la corrida: ${base.cargaHoy})`);
  } catch (e) { ctx.chk('F11-04', false, `no se pudo verificar lo intocable: ${sinPAN(e.message).slice(0, 300)}`); }
  ctx.fases.push({ id: 'F11', titulo: 'Limpieza y verificación', seg: Math.round((Date.now() - t) / 1000) });
}

async function main() {
  fs.mkdirSync(RAIZ, { recursive: true });

  // ── solo limpieza ───────────────────────────────────────────────────────────────────────────
  if (soloLimpieza) {
    const ctx = new Corrida({ runid: 'limpieza', dir: path.join(RAIZ, `limpieza-${ahoraCorta()}`) });
    ctx.fases.push({ id: 'F11', titulo: 'Limpieza', seg: 0 });
    await limpiezaFinal(ctx, null); // sin foto previa: solo F11-01..03
    ctx.volcar(); console.log(`\n${ctx.dir}/resumen.md`);
    process.exit(ctx.fallas.length ? 1 : 0);
  }

  // ── corrida ─────────────────────────────────────────────────────────────────────────────────
  let ctx;
  const reanudar = !!desde;
  if (reanudar) {
    const est = JSON.parse(fs.readFileSync(path.join(RAIZ, 'estado.json'), 'utf8'));
    ctx = new Corrida({ runid: est.runid, dir: est.dir });
    ctx.datos = est.datos; ctx.registro = est.registro; ctx.t0 = est.t0; ctx._guardarRegistro();
    console.log(`Reanudando la corrida ${ctx.runid} desde ${desde.toUpperCase()}`);
  } else {
    ctx = new Corrida({ runid: nuevoRunid(), dir: path.join(RAIZ, `corrida-${ahoraCorta()}`) });
  }
  console.log(`Prueba integral 0 a 100 · corrida ${ctx.runid} · cliente "${ctx.nombreCliente}" · hoy (Managua) ${ctx.hoy.fecha}\nResultados en ${ctx.dir}`);

  // limpieza previa (restos de corridas caídas) — sin tocar nada si se reanuda
  const base = {};
  if (!reanudar) {
    const prev = await datos.limpiar();
    console.log(`Limpieza previa: borrado ${JSON.stringify(prev.borrados)} · restantes ${JSON.stringify(prev.restantes)}`);
  }
  base.agentes = await fotoAgentes(); base.intocables = await intocables(); base.cargaHoy = await v.cargaHoy();
  if (reanudar && ctx.datos.base) Object.assign(base, ctx.datos.base);
  ctx.datos.base = base;
  console.log(`Foto inicial: filas de carga de hoy ${base.cargaHoy} · agentes QA ${base.agentes.length} · intocables ${JSON.stringify(base.intocables)}`);

  let cerrando = false;
  const cerrar = async (motivo) => {
    if (cerrando) return; cerrando = true;
    if (motivo) console.log(`\n${motivo} — limpiando…`);
    if (!conservar && !(hasta && hasta.toUpperCase() === 'F0')) await limpiezaFinal(ctx, base);
    else if (conservar) console.log('\n--conservar: NO se limpia. Los datos QA-E2E-INT quedan en DEV; limpiar con: node run.js --solo-limpieza');
    ctx.volcar();
  };
  process.on('SIGINT', async () => { await cerrar('Interrumpido'); process.exit(130); });
  process.on('SIGTERM', async () => { await cerrar('Terminado'); process.exit(143); });

  try {
    const ini = desde ? FASES.findIndex((f) => f[0] === desde.toUpperCase()) : 0;
    const fin = hasta ? FASES.findIndex((f) => f[0] === hasta.toUpperCase()) : FASES.length - 1;
    for (const [id, titulo, modulo, fn] of FASES.slice(ini, fin + 1)) {
      let mod; try { mod = require(`./fases/${modulo}`); } catch (e) { ctx.faseActual = id; ctx.chk(`${id}-ERR`, false, `no se pudo cargar fases/${modulo}.js: ${e.message}`); continue; }
      await ctx.fase(id, titulo, () => mod[fn](ctx, { conservarCsv: hasta && hasta.toUpperCase() === 'F0' }));
    }
  } finally {
    await cerrar();
    const mal = ctx.fallas;
    console.log(`\n══ RESUMEN · corrida ${ctx.runid}`);
    for (const f of ctx.fases) { const cs = ctx.checks.filter((c) => c.fase === f.id); console.log(`  ${f.id.padEnd(4)} ${cs.every((c) => c.ok) ? '✓' : '✗'} ${String(cs.filter((c) => c.ok).length).padStart(2)}/${String(cs.length).padEnd(2)} ${f.titulo} (${f.seg}s)`); }
    if (ctx.hallazgos.length) { console.log('\n  Hallazgos:'); for (const h of ctx.hallazgos) console.log(`   ⚑ (${h.fase}) ${h.texto}`); }
    console.log(`\n  ${ctx.checks.length - mal.length}/${ctx.checks.length} checks ✓ · ${mal.length} ✗ · ${ctx.dir}/resumen.md`);
    if (mal.length) { console.log('  Fallidos:'); for (const c of mal) console.log(`   ✗ ${c.id} ${c.detalle}`); }
    process.exitCode = mal.length ? 1 : 0;
  }
}

main().catch((e) => { console.error('ERROR FATAL', sinPAN(e.stack || e.message)); process.exit(2); });
