// Datos de prueba QA-E2E: creación, registro (para limpiar aunque la corrida se caiga) y limpieza.
// REGLA: todo registro que la suite crea o hace crear a la app lleva el prefijo "QA-E2E".
const fs = require('fs');
const path = require('path');
const dv = require('./dataverse');
const cfg = require('./config');

const P = cfg.prefijoQA;
const REG = () => path.join(cfg.resultadosDir, 'registro-qa.json');

const ES = {
  cliente: ['sanic_cm_tbl_clientes', 'sanic_cm_tbl_clienteid'],
  cuenta: ['sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuentaid'],
  telefono: ['sanic_cm_tbl_telefonos', 'sanic_cm_tbl_telefonoid'],
  direccion: ['sanic_cm_tbl_direccions', 'sanic_cm_tbl_direccionid'],
  interaccion: ['sanic_cm_tbl_interaccions', 'sanic_cm_tbl_interaccionid'],
  pago: ['sanic_cm_tbl_pagos', 'sanic_cm_tbl_pagoid'],
  actualizacion: ['sanic_cm_tbl_actualizaciondatoses', 'sanic_cm_tbl_actualizaciondatosid'],
  asignacion: ['sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid'],
  carga: ['sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_cargadiariaid'],
};

function leerRegistro() { try { return JSON.parse(fs.readFileSync(REG(), 'utf8')); } catch (e) { return { creados: [] }; } }
function registrar(tipo, id, nota) {
  const r = leerRegistro(); r.creados.push({ tipo, id, nota, t: new Date().toISOString() });
  fs.mkdirSync(path.dirname(REG()), { recursive: true }); fs.writeFileSync(REG(), JSON.stringify(r, null, 1));
}
async function crear(tipo, data, nota) {
  const [set, pk] = ES[tipo];
  const rec = await dv.create(set, data);
  registrar(tipo, rec[pk], nota);
  return rec;
}
const bind = (set, id) => `/${set}(${id})`;

// Agente REAL activo usado como lookup de las interacciones QA (solo referencia, no se modifica).
async function agenteActivoConUsuario() {
  const r = await dv.get("sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre,sanic_cm_ag_emailusuario,_sanic_cm_ag_usuarioid_value&$filter=sanic_cm_ag_activo eq true and sanic_cm_ag_emailusuario ne null&$orderby=sanic_cm_ag_nombre");
  // Preferir uno que NO sea la cuenta admin (el admin está fuera del alcance de la suite).
  return r.value.find(a => !/^admin@/i.test(a.sanic_cm_ag_emailusuario)) || r.value[0];
}

// Siembra base: 1 cliente, 4 cuentas (0003 = falla controlada B-03, 0004 = reactivación B-11), 1 teléfono "antes", 2 interacciones pendientes (pago / actualización).
async function sembrar() {
  const prod = (await dv.get("sanic_cm_tbl_productos?$select=sanic_cm_pr_codigo,sanic_cm_pr_tipo&$filter=sanic_cm_pr_codigo eq 'TC001'")).value[0];
  const ag = await agenteActivoConUsuario();
  const cli = await crear('cliente', {
    sanic_cm_cl_cif: `${P}-CIF-001`, sanic_cm_cl_nombre: `${P} CLIENTE UNO`, sanic_cm_cl_departamento: `${P} Managua`,
  }, 'cliente base');
  const cliId = cli.sanic_cm_tbl_clienteid;
  const cuentas = [];
  for (const n of ['0001', '0002', '0003', '0004']) {
    const c = await crear('cuenta', {
      sanic_cm_ct_numerodecuenta: `${P}-CTA-${n}`, sanic_cm_ct_nombrecliente: `${P} CLIENTE UNO`,
      sanic_cm_ct_cifcliente: `${P}-CIF-001`, sanic_cm_ct_tipoproducto: prod.sanic_cm_pr_tipo,
      sanic_cm_ct_saldovencidonio: 1500, sanic_cm_ct_saldovencidousd: 0, sanic_cm_ct_diasmora: 30,
      'sanic_cm_ct_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cliId),
      'sanic_cm_ct_productoid@odata.bind': bind('sanic_cm_tbl_productos', prod.sanic_cm_tbl_productoid),
    }, `cuenta ${n}`);
    cuentas.push(c);
  }
  await crear('telefono', {
    sanic_cm_tl_cif: `${P}-CIF-001`, sanic_cm_tl_numero: '2222-0000', sanic_cm_tl_tipo: 'Casa',
    'sanic_cm_tl_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cliId),
  }, 'telefono antes');
  const ahora = Date.now();
  const mkInt = (min, extra, nota) => crear('interaccion', {
    sanic_cm_in_numerocuenta: `${P}-CTA-0001`, sanic_cm_in_nombreagente: `${P} Localizador`,
    sanic_cm_in_comentario: `${P} ${nota}`, sanic_cm_in_estadograbacion: 1,
    sanic_cm_in_fechacaptura: new Date(ahora - min * 60000).toISOString(),
    'sanic_cm_in_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', cuentas[0].sanic_cm_tbl_cuentaid),
    'sanic_cm_in_agente@odata.bind': bind('sanic_cm_tbl_agentes', ag.sanic_cm_tbl_agenteid),
    ...extra,
  }, nota);
  const int1 = await mkInt(120, { sanic_cm_cd_aplicapago: true, sanic_cm_in_actualizainfo: false }, 'interaccion con pago');
  const int2 = await mkInt(60, { sanic_cm_cd_aplicapago: false, sanic_cm_in_actualizainfo: true }, 'interaccion con actualizacion');
  const pago = await crear('pago', {
    sanic_cm_pg_monto: 250, sanic_cm_pg_moneda: 1, sanic_cm_pg_numerorecibo: `${P}-REC-1`,
    sanic_cm_pg_fecha: new Date().toISOString(), sanic_cm_pg_numerocuenta: `${P}-CTA-0001`,
    sanic_cm_pg_numerointeraccion: int1.sanic_cm_in_numerointeraccion,
    'sanic_cm_pg_interaccion@odata.bind': bind('sanic_cm_tbl_interaccions', int1.sanic_cm_tbl_interaccionid),
  }, 'pago de interaccion 1');
  const ad = await crear('actualizacion', {
    sanic_cm_ad_telefonocasa: '8888-0000', sanic_cm_ad_correoelectronico: 'qa-e2e@example.com',
    'sanic_cm_ad_interaccionid@odata.bind': bind('sanic_cm_tbl_interaccions', int2.sanic_cm_tbl_interaccionid),
  }, 'actualizacion de interaccion 2');
  return { prod, agente: ag, cliente: cli, cuentas, int1, int2, pago, ad };
}

// Especial QA creada por API (para el rechazo de F-03).
// `owner`: systemuserid dueño (como si la hubiera creado ese usuario desde la app); `estado`: false = CERRADA.
async function crearEspecialPendiente(cuenta, solicitanteId, nota, { notas, nio = 100, owner, estado = true } = {}) {
  const a = await crear('asignacion', {
    sanic_cm_as_numerocuenta: cuenta.sanic_cm_ct_numerodecuenta, sanic_cm_as_tipo: 3, sanic_cm_as_estado: estado,
    sanic_cm_as_estadoaprobacion: 2, sanic_cm_as_retironio: nio, sanic_cm_as_notas: notas || `Verificación — ${P} ${nota}`,
    sanic_cm_as_fechasignacion: new Date().toISOString(),
    ...(estado ? {} : { sanic_cm_as_motivo: `Gestión completada — ${P} ${nota}`, sanic_cm_as_fechacierre: new Date().toISOString() }),
    'sanic_cm_as_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', cuenta.sanic_cm_tbl_cuentaid),
    'sanic_cm_as_solicitadopor@odata.bind': bind('systemusers', solicitanteId),
  }, nota);
  if (owner) await dv.patch('sanic_cm_tbl_asignacions', a.sanic_cm_tbl_asignacionid, { 'ownerid@odata.bind': `/systemusers(${owner})` });
  return a;
}

// Fila QA de carga diaria con FechaCarga = HOY (DateOnly) — escenario D-07.
async function crearCargaHoy(cliId, cuentaId, fechaIso) {
  return crear('carga', {
    sanic_cm_cd_numerocuenta: `${P}-CTA-0001`, sanic_cm_cd_fechacarga: fechaIso, sanic_cm_cd_tipoasignacion: `${P}`,
    'sanic_cm_cd_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cliId),
    'sanic_cm_cd_cuenta@odata.bind': bind('sanic_cm_tbl_cuentas', cuentaId),
  }, 'carga de hoy QA');
}

// Barrido por prefijo (incluye lo que creó la APP: Especiales por UI, cliente/teléfono de Maestros).
async function inventarioQA() {
  const q = async (tipo, filtro) => {
    const [set, pk] = ES[tipo];
    return (await dv.getAll(`${set}?$select=${pk}&$filter=${encodeURIComponent(filtro)}`)).map(x => x[pk]);
  };
  const reg = leerRegistro().creados;
  const deReg = t => reg.filter(x => x.tipo === t).map(x => x.id);
  const uniq = a => [...new Set(a)];
  const inter = await q('interaccion', `startswith(sanic_cm_in_numerocuenta,'${P}')`);
  const adPorInt = [];
  for (const id of uniq([...inter, ...deReg('interaccion')])) {
    adPorInt.push(...(await dv.getAll(`sanic_cm_tbl_actualizaciondatoses?$select=sanic_cm_tbl_actualizaciondatosid&$filter=_sanic_cm_ad_interaccionid_value eq ${id}`)).map(x => x.sanic_cm_tbl_actualizaciondatosid));
  }
  return {
    pago: uniq([...await q('pago', `startswith(sanic_cm_pg_numerocuenta,'${P}') or startswith(sanic_cm_pg_numerorecibo,'${P}')`), ...deReg('pago')]),
    actualizacion: uniq([...adPorInt, ...await q('actualizacion', "startswith(sanic_cm_ad_correoelectronico,'qa-e2e')"), ...deReg('actualizacion')]),
    interaccion: uniq([...inter, ...deReg('interaccion')]),
    asignacion: uniq([...await q('asignacion', `startswith(sanic_cm_as_numerocuenta,'${P}')`), ...deReg('asignacion')]),
    carga: uniq([...await q('carga', `startswith(sanic_cm_cd_numerocuenta,'${P}')`), ...deReg('carga')]),
    cuenta: uniq([...await q('cuenta', `startswith(sanic_cm_ct_numerodecuenta,'${P}')`), ...deReg('cuenta')]),
    telefono: uniq([...await q('telefono', `startswith(sanic_cm_tl_cif,'${P}')`), ...deReg('telefono')]),
    direccion: uniq([...await q('direccion', `startswith(sanic_cm_dr_cif,'${P}')`), ...deReg('direccion')]),
    cliente: uniq([...await q('cliente', `startswith(sanic_cm_cl_cif,'${P}')`), ...deReg('cliente')]),
  };
}

async function limpiar() {
  const inv = await inventarioQA();
  const res = {};
  for (const tipo of ['pago', 'actualizacion', 'interaccion', 'asignacion', 'carga', 'cuenta', 'telefono', 'direccion', 'cliente']) {
    res[tipo] = { borrados: 0, yaNoExistian: 0, errores: [] };
    for (const id of inv[tipo]) {
      try { const st = await dv.del(ES[tipo][0], id); if (st === 404) res[tipo].yaNoExistian++; else res[tipo].borrados++; }
      catch (e) { res[tipo].errores.push(`${id}: ${e.message.slice(0, 160)}`); }
    }
  }
  // Verificación: el barrido por prefijo debe dar CERO en todas las tablas.
  const despues = await inventarioQA();
  const restantes = {};
  for (const [t, ids] of Object.entries(despues)) {
    // los ids del registro que ya no existen siguen apareciendo en deReg: verificar existencia real
    let vivos = 0;
    for (const id of ids) { try { await dv.get(`${ES[t][0]}(${id})?$select=${ES[t][1]}`); vivos++; } catch (e) { if (e.status !== 404) vivos++; } }
    restantes[t] = vivos;
  }
  return { borrado: res, restantes };
}

// ── Membresías de teams ────────────────────────────────────────────────────────────────
async function membresias() {
  const out = {};
  for (const [nombre, id] of Object.entries(cfg.teams)) {
    const m = await dv.get(`teams(${id})/teammembership_association?$select=systemuserid,domainname`);
    out[id] = { nombre, usuarios: m.value.map(u => u.systemuserid).sort(), upn: m.value.map(u => u.domainname).sort() };
  }
  return out;
}
async function agregarATeam(teamId, userId) {
  await dv.post(`teams(${teamId})/teammembership_association/$ref`, { '@odata.id': `${dv.API}/systemusers(${userId})` });
}
async function quitarDeTeam(teamId, userId) {
  return dv.delRef(`teams(${teamId})/teammembership_association(${userId})/$ref`);
}
// Restaura EXACTAMENTE la foto inicial (solo toca diferencias).
async function restaurarMembresias(foto) {
  const actual = await membresias(); const cambios = [];
  for (const [teamId, f] of Object.entries(foto)) {
    const a = actual[teamId].usuarios;
    for (const u of f.usuarios) if (!a.includes(u)) { await agregarATeam(teamId, u); cambios.push(`+${f.nombre}:${u}`); }
    for (const u of a) if (!f.usuarios.includes(u)) { await quitarDeTeam(teamId, u); cambios.push(`-${f.nombre}:${u}`); }
  }
  const fin = await membresias();
  const iguales = Object.keys(foto).every(t => JSON.stringify(foto[t].usuarios) === JSON.stringify(fin[t].usuarios));
  return { cambios, iguales, fin };
}

module.exports = {
  ES, sembrar, crearEspecialPendiente, crearCargaHoy, registrar, leerRegistro, inventarioQA, limpiar,
  membresias, agregarATeam, quitarDeTeam, restaurarMembresias,
};
