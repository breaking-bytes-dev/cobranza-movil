// Registro de escenarios, análisis de delegación de la red capturada y volcado de resultados.
const fs = require('fs');
const path = require('path');
const cfg = require('./config');

const escenarios = [];
const capturasRed = []; // { fase, usuario, rolEfectivo, entradas }
const hallazgos = [];   // observaciones libres (errores de runtime, etc.)
const aperturas = [];   // traza: versión de la app que cargó el player en cada apertura

class Escenario {
  constructor(id, desc, rol) { this.id = id; this.desc = desc; this.rol = rol; this.checks = []; this.evidencia = []; this.notas = []; this.estado = null; this.t0 = Date.now(); }
  check(ok, msg, esperado, obtenido) {
    this.checks.push({ ok: !!ok, msg, esperado: fmt(esperado), obtenido: fmt(obtenido) });
    console.log(`   ${ok ? '✔' : '✘'} [${this.id}] ${msg}${ok ? '' : ` — esperado: ${fmt(esperado)} · obtenido: ${fmt(obtenido)}`}`);
    return !!ok;
  }
  evid(p) { if (p) this.evidencia.push(p); }
  nota(t) { this.notas.push(t); console.log(`   · [${this.id}] ${t}`); }
  bloquear(motivo) { this.estado = 'BLOQUEADO'; this.notas.push(`BLOQUEADO: ${motivo}`); console.log(`   ⛔ [${this.id}] BLOQUEADO: ${motivo}`); }
  error(e) { this.estado = 'FALLÓ'; this.notas.push(`Excepción: ${String(e && e.message || e).slice(0, 400)}`); console.log(`   ✘ [${this.id}] EXCEPCIÓN: ${String(e && e.message || e).slice(0, 300)}`); }
  fin() {
    if (!this.estado) this.estado = this.checks.length && this.checks.every(c => c.ok) ? 'PASÓ' : (this.checks.length ? 'FALLÓ' : 'BLOQUEADO');
    this.seg = Math.round((Date.now() - this.t0) / 1000);
    console.log(`== ${this.id} → ${this.estado}`);
    return this;
  }
}
const fmt = v => (v === undefined ? undefined : typeof v === 'string' ? v : JSON.stringify(v));

function escenario(id, desc, rol) { const e = new Escenario(id, desc, rol); escenarios.push(e); console.log(`\n▶ ${id} — ${desc}${rol ? ` (${rol})` : ''}`); return e; }
// Ejecuta un bloque como escenario: cualquier excepción lo marca FALLÓ con el mensaje.
async function correr(id, desc, rol, fn) {
  const e = escenario(id, desc, rol);
  try { await fn(e); } catch (err) { e.error(err); }
  return e.fin();
}

// ── Delegación ─────────────────────────────────────────────────────────────────────────
const GRANDES = new Set(['sanic_cm_tbl_interaccions', 'sanic_cm_tbl_asignacions', 'sanic_cm_tbl_cuentas', 'sanic_cm_tbl_clientes',
  'sanic_cm_tbl_pagos', 'sanic_cm_tbl_cuentapans', 'sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_telefonos', 'sanic_cm_tbl_direccions', 'sanic_cm_tbl_actualizaciondatoses']);

function clasificar(sub, resp) {
  const [base, qs = ''] = sub.ruta.split('?');
  const entidad = base.replace(/\(.*$/, '').replace(/\/.*$/, '');
  const porClave = /\([0-9a-f-]{36}\)/i.test(base);
  const tiene = k => new RegExp(`(^|&)\\${k}=`).test(qs);
  const filtro = (qs.match(/(?:^|&)\$filter=([^&]*)/) || [])[1];
  const apply = (qs.match(/(?:^|&)\$apply=([^&]*)/) || [])[1];
  const top = (qs.match(/(?:^|&)\$top=(\d+)/) || [])[1];
  const grande = GRANDES.has(entidad);
  let veredicto;
  if (sub.metodo !== 'GET') veredicto = 'ESCRITURA';
  else if (apply) veredicto = 'DELEGADA ($apply agregado/filtro en servidor)';
  else if (filtro) veredicto = 'DELEGADA ($filter en servidor)';
  else if (porClave) veredicto = 'POR CLAVE/RELACIÓN (1 registro)';
  else if (top) veredicto = `DELEGADA ($top=${top}${tiene('$orderby') ? ' + $orderby' : ''})`;
  else if (tiene('$orderby')) veredicto = grande ? 'DELEGADA (Sort en servidor + paginado: solo 1.ª página)' : 'SIN $filter (tabla chica)';
  else if (grande && resp?.nextLink && (resp?.filas ?? 0) <= 500) veredicto = `SIN $filter pero PAGINADO en servidor (1.ª página: ${resp.filas} filas + nextLink) — revisar`;
  else veredicto = grande ? '⚠ SIN FILTRO A TABLA GRANDE' : 'SIN FILTRO (tabla chica/catálogo)';
  // Alerta: GET a tabla grande sin $filter/$apply/$top/clave. Un Sort de galería que trae SOLO la primera
  // página (≤ 500 filas) es paginado del servidor, no descarga completa.
  const paginado = (tiene('$orderby') || resp?.nextLink) && (resp?.filas ?? 0) <= 500;
  const alerta = sub.metodo === 'GET' && grande && !apply && !filtro && !porClave && !top && !paginado;
  return { entidad, metodo: sub.metodo, ruta: sub.ruta.slice(0, 400), veredicto, alerta, filas: resp?.filas, resultado: resp?.resultado, status: resp?.status, nextLink: resp?.nextLink, error: resp?.error };
}

function analizarRed(entradas) {
  const porPantalla = {};
  for (const e of entradas) {
    const lista = porPantalla[e.pantalla] || (porPantalla[e.pantalla] = []);
    e.subs.forEach((s, i) => lista.push({ t: e.t, ...clasificar(s, e.respuestas?.[i]) }));
  }
  // N+1: muchas consultas a la misma entidad por CLAVE o igualdad distinta (lookup por fila de galería).
  // No cuentan las búsquedas progresivas (startswith por tecla): se reportan aparte como rendimiento.
  for (const [p, lista] of Object.entries(porPantalla)) {
    const n = {}, teclas = {};
    lista.filter(x => x.metodo === 'GET').forEach(x => {
      if (/startswith\(/.test(x.ruta)) { teclas[x.entidad] = (teclas[x.entidad] || 0) + 1; return; }
      const clave = (x.ruta.match(/\(([0-9a-f-]{36})\)|eq '([^']+)'|eq ([0-9a-f-]{36})/i) || []).slice(1).find(Boolean);
      if (clave) (n[x.entidad] = n[x.entidad] || new Set()).add(clave);
    });
    for (const [ent, s] of Object.entries(n)) if (s.size >= 10) lista.push({ entidad: ent, veredicto: `⚠ POSIBLE N+1: ${s.size} consultas por clave distinta a ${ent}`, alerta: true });
    for (const [ent, k] of Object.entries(teclas)) if (k >= 5) lista.push({ entidad: ent, veredicto: `ℹ búsqueda por tecla: ${k} consultas startswith (delegadas; sugerencia DelayOutput=true)`, alerta: false });
  }
  return porPantalla;
}

function registrarRed(fase, usuario, rolEfectivo, red) { capturasRed.push({ fase, usuario, rolEfectivo, entradas: red.entradas }); }
function registrarApertura(fase, usuario, rolEfectivo, version) { aperturas.push({ fase, usuario, rolEfectivo, version: version || '(sin dato)' }); console.log(`   · app.version (player) = ${version || '(sin dato)'}`); }
function hallazgo(t) { hallazgos.push(t); console.log(`   ⚑ ${t}`); }

function volcar(extra = {}) {
  fs.mkdirSync(cfg.resultadosDir, { recursive: true });
  const delegacion = capturasRed.map(c => ({ fase: c.fase, usuario: c.usuario, rolEfectivo: c.rolEfectivo, pantallas: analizarRed(c.entradas) }));
  const out = { generado: new Date().toISOString(), escenarios: escenarios.map(e => ({ ...e, t0: undefined })), delegacion, hallazgos, aperturas, ...extra };
  const p = path.join(cfg.resultadosDir, 'resultados.json');
  fs.writeFileSync(p, JSON.stringify(out, null, 1));
  // Resumen legible
  const L = ['# Resultados E2E — App - CM - Backoffice Canvas', '', `Generado: ${out.generado}`, '', '| ID | Descripción | Estado | Evidencia |', '|---|---|---|---|'];
  for (const e of escenarios) L.push(`| ${e.id} | ${e.desc} | ${e.estado} | ${e.evidencia.join('<br>')} |`);
  L.push('', '## Checks fallidos', '');
  for (const e of escenarios) for (const c of e.checks.filter(c => !c.ok)) L.push(`- **${e.id}** ${c.msg} — esperado \`${c.esperado}\` · obtenido \`${c.obtenido}\``);
  L.push('', '## Delegación por pantalla', '');
  for (const d of delegacion) for (const [p, lista] of Object.entries(d.pantallas)) {
    L.push(`### ${d.fase} · ${d.usuario} (${d.rolEfectivo}) · ${p}`, '', '| Método | Entidad | Veredicto | Filas/Resultado | Consulta |', '|---|---|---|---|---|');
    for (const x of lista) L.push(`| ${x.metodo || ''} | ${x.entidad} | ${x.veredicto} | ${x.filas ?? ''}${x.resultado !== undefined ? ` (res=${x.resultado})` : ''}${x.error ? ` ERR ${x.error}` : ''} | \`${(x.ruta || '').replace(/\|/g, '¦').slice(0, 260)}\` |`);
    L.push('');
  }
  L.push('## Versión de la app probada (por apertura del player)', '', `Publicada (Dataverse): ${extra.app ? `${extra.app.version || ''} (lastpublishtime ${extra.app.publicada || ''})` : '(n/d)'}`, '', '| Fase | Usuario | Rol | app.version del player |', '|---|---|---|---|');
  for (const a of aperturas) L.push(`| ${a.fase} | ${a.usuario} | ${a.rolEfectivo} | ${a.version} |`);
  L.push('', `Versiones distintas vistas: ${[...new Set(aperturas.map(a => a.version))].join(', ') || '(ninguna)'}`, '');
  L.push('## Hallazgos', '', ...hallazgos.map(h => `- ${h}`));
  fs.writeFileSync(path.join(cfg.resultadosDir, 'resumen.md'), L.join('\n'));
  return p;
}

module.exports = { escenario, correr, registrarRed, registrarApertura, hallazgo, volcar, analizarRed, escenarios };
