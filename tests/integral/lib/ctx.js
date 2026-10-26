// Contexto de la corrida integral: constantes, informe (✓/✗), registro de ids creados y utilidades de espera.
// REGLA: ningún número de tarjeta completo se imprime ni se guarda acá. Todo texto que sale (consola, resumen,
// JSON) pasa por `sinPAN()`, que enmascara cualquier secuencia de 13 a 19 dígitos.
const fs = require('fs');
const path = require('path');
const cfg = require('../../backoffice-canvas/lib/config');
const v = require('../../backoffice-canvas/lib/verdad');

const PREFIJO = 'QA-E2E-INT';
const AGENTE = 'QA-E2E Localizador User5';          // agente fijo, ya vinculado a User5 (no se crea ni se toca)
const USER5 = '539834c4-6116-f111-8342-000d3a316609'; // localizador
const U = { bo: cfg.usuarios.user1.id, sol: cfg.usuarios.user2.id, sup: cfg.usuarios.user3.id, adm: cfg.usuarios.user4.id, cob: USER5 };
const RAIZ = path.join(__dirname, '..', 'resultados');

// Tablas que toca la suite: [entitySet, pk]
const ES = {
  cliente: ['sanic_cm_tbl_clientes', 'sanic_cm_tbl_clienteid'],
  cuenta: ['sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuentaid'],
  boveda: ['sanic_cm_tbl_cuentapans', 'sanic_cm_tbl_cuentapanid'],
  asignacion: ['sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid'],
  carga: ['sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_cargadiariaid'],
  direccion: ['sanic_cm_tbl_direccions', 'sanic_cm_tbl_direccionid'],
  telefono: ['sanic_cm_tbl_telefonos', 'sanic_cm_tbl_telefonoid'],
  interaccion: ['sanic_cm_tbl_interaccions', 'sanic_cm_tbl_interaccionid'],
  pago: ['sanic_cm_tbl_pagos', 'sanic_cm_tbl_pagoid'],
  actualizacion: ['sanic_cm_tbl_actualizaciondatoses', 'sanic_cm_tbl_actualizaciondatosid'],
  producto: ['sanic_cm_tbl_productos', 'sanic_cm_tbl_productoid'],
  fiador: ['sanic_cm_tbl_fiadors', 'sanic_cm_tbl_fiadorid'],
  job: ['sanic_tbl_smlt_importjobs', 'sanic_tbl_smlt_importjobid'],
};

const sinPAN = (s) => String(s).replace(/\d{13,19}/g, (m) => `****${m.slice(-4)}`);
const dormir = (ms) => new Promise((r) => setTimeout(r, ms));
const ahoraCorta = () => new Date().toISOString().replace(/[-:]/g, '').replace(/\..*/, '').replace('T', '-');

// Espera hasta que `fn` devuelva algo "verdadero" (se devuelve ese valor) o venza el plazo (se devuelve null).
async function esperar(fn, { maxMs = 180000, cadaMs = 10000, log } = {}) {
  const t0 = Date.now(); let ult = null;
  for (;;) {
    ult = await fn();
    if (ult) return ult;
    if (Date.now() - t0 + cadaMs > maxMs) return null;
    if (log) log(Math.round((Date.now() - t0) / 1000));
    await dormir(cadaMs);
  }
}

class BloqueoError extends Error {}

class Corrida {
  constructor({ runid, dir }) {
    this.runid = runid; this.dir = dir; fs.mkdirSync(dir, { recursive: true });
    this.hoy = v.hoy();
    this.checks = []; this.fases = []; this.notas = []; this.hallazgos = [];
    this.datos = {};       // ids, últimos 4, números de asignación… (NUNCA números de tarjeta completos)
    this.faseActual = '-';
    this.capturador = null;   // función (nombre) → captura de pantalla de la app abierta (la arma cada fase de UI)
    this.t0 = Date.now();
    this.registroPath = path.join(dir, 'registro.json');
    this.registro = { runid, creado: new Date().toISOString(), items: [], limpiado: false };
    this._guardarRegistro();
  }
  get nombreCliente() { return `${PREFIJO} ${this.runid}`; }
  get cif() { return `${PREFIJO}-${this.runid}`; }

  // ── informe ────────────────────────────────────────────────────────────────────────────
  chk(id, ok, detalle = '') {
    const d = sinPAN(detalle);
    this.checks.push({ id, ok: !!ok, detalle: d, fase: this.faseActual });
    console.log(`${ok ? '✓' : '✗'} ${id.padEnd(8)} ${d}`);
    return !!ok;
  }
  nota(t) { const d = sinPAN(t); this.notas.push({ fase: this.faseActual, texto: d }); console.log(`  · ${d}`); }
  hallazgo(t) { const d = sinPAN(t); this.hallazgos.push({ fase: this.faseActual, texto: d }); console.log(`  ⚑ ${d}`); }
  // Un paso suelto: si revienta, queda como ✗ y la fase sigue con lo que no depende de él.
  async paso(id, desc, fn) {
    try { return await fn(); }
    catch (e) {
      this.chk(id, false, `${desc} — ${e instanceof BloqueoError ? 'precondición: ' : 'excepción: '}${String(e.message).split('\n')[0].slice(0, 300)}`);
      if (this.capturador) await this.capturador(`fallo-${id}`).catch(() => {});
      return undefined;
    }
  }
  requiere(cond, msg) { if (!cond) throw new BloqueoError(msg); }
  async fase(id, titulo, fn) {
    this.faseActual = id; const t = Date.now();
    console.log(`\n══ ${id} · ${titulo}`);
    try { await fn(); }
    catch (e) { this.chk(`${id}-ERR`, false, `${e instanceof BloqueoError ? 'precondición: ' : 'excepción: '}${String(e.message).split('\n')[0].slice(0, 400)}`); }
    this.capturador = null;
    const f = { id, titulo, seg: Math.round((Date.now() - t) / 1000) };
    this.fases.push(f); this.guardarEstado();
    return f;
  }

  // ── registro de lo creado (ids; ni un solo número de tarjeta) ───────────────────────────
  _guardarRegistro() { fs.writeFileSync(this.registroPath, JSON.stringify(this.registro, null, 1)); }
  registrar(tipo, id, nota) { this.registro.items.push({ tipo, id, nota, t: new Date().toISOString() }); this._guardarRegistro(); }
  registrados(tipo) { return this.registro.items.filter((x) => x.tipo === tipo).map((x) => x.id); }
  marcarLimpiado(ok) { this.registro.limpiado = ok; this._guardarRegistro(); }

  // Estado reanudable (solo para depurar con --desde): ids y valores esperados. Sin PAN.
  guardarEstado() {
    fs.writeFileSync(path.join(RAIZ, 'estado.json'), JSON.stringify({ runid: this.runid, dir: this.dir, datos: this.datos, registro: this.registro, t0: this.t0 }, null, 1));
  }

  // ── salida ─────────────────────────────────────────────────────────────────────────────
  resumen() {
    const mal = this.checks.filter((c) => !c.ok);
    const L = [`# Prueba integral 0 a 100 — corrida ${this.runid}`, '',
      `Generado: ${new Date().toISOString()} · duración ${Math.round((Date.now() - this.t0) / 60000)} min · ${this.checks.length - mal.length}/${this.checks.length} checks ✓`, '',
      '| Fase | Título | Checks ✓/✗ | Seg |', '|---|---|---|---|'];
    for (const f of this.fases) {
      const cs = this.checks.filter((c) => c.fase === f.id);
      L.push(`| ${f.id} | ${f.titulo} | ${cs.filter((c) => c.ok).length}/${cs.filter((c) => !c.ok).length} | ${f.seg} |`);
    }
    L.push('', '## Checks', '');
    for (const c of this.checks) L.push(`- ${c.ok ? '✓' : '✗'} **${c.id}** ${c.detalle}`);
    if (this.hallazgos.length) { L.push('', '## Hallazgos (posibles defectos de producto)', ''); for (const h of this.hallazgos) L.push(`- (${h.fase}) ${h.texto}`); }
    if (this.notas.length) { L.push('', '## Notas', ''); for (const n of this.notas) L.push(`- (${n.fase}) ${n.texto}`); }
    return L.join('\n');
  }
  volcar() {
    fs.writeFileSync(path.join(this.dir, 'resumen.md'), this.resumen());
    fs.writeFileSync(path.join(this.dir, 'resultados.json'), JSON.stringify({ runid: this.runid, checks: this.checks, fases: this.fases, hallazgos: this.hallazgos, notas: this.notas, limpiado: this.registro.limpiado }, null, 1));
  }
  get fallas() { return this.checks.filter((c) => !c.ok); }
}

module.exports = { PREFIJO, AGENTE, USER5, U, ES, RAIZ, Corrida, BloqueoError, sinPAN, dormir, esperar, ahoraCorta };
