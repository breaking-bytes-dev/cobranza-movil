// Datos de prueba QA-E2E: creación, registro (para limpiar aunque la corrida se caiga) y limpieza.
// REGLA: la limpieza borra solo lo anotado en el registro de la corrida y lo que cuelga de eso (por lookup o por la
// clave exacta anotada), nunca por prefijo: otras suites (integral, localizador) usan datos QA-E2E en el mismo ambiente.
const fs = require('fs');
const path = require('path');
const dv = require('./dataverse');
const cfg = require('./config');

const P = cfg.prefijoQA;
const REG = () => path.join(cfg.resultadosDir, 'registro-qa.json');

// El número de cuenta completo vive SOLO en la bóveda (cuentapan.cp_pan). Los datos QA no usan PANs reales:
// el "PAN" QA es `QA-E2E-CTA-000n`, los últimos 4 son `E2En` (no chocan con dígitos reales) y la clave sustituta
// es `QA-E2E-CLAVE-000n` en vez de un HMAC. Así se cumplen las claves alternativas (clavepan, tipo) / (clavepan, fecha)
// y la relación 1:1 con la bóveda sin tocar un solo dígito real.
const ES = {
  cliente: ['sanic_cm_tbl_clientes', 'sanic_cm_tbl_clienteid'],
  cuenta: ['sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuentaid'],
  boveda: ['sanic_cm_tbl_cuentapans', 'sanic_cm_tbl_cuentapanid'],
  telefono: ['sanic_cm_tbl_telefonos', 'sanic_cm_tbl_telefonoid'],
  direccion: ['sanic_cm_tbl_direccions', 'sanic_cm_tbl_direccionid'],
  interaccion: ['sanic_cm_tbl_interaccions', 'sanic_cm_tbl_interaccionid'],
  pago: ['sanic_cm_tbl_pagos', 'sanic_cm_tbl_pagoid'],
  actualizacion: ['sanic_cm_tbl_actualizaciondatoses', 'sanic_cm_tbl_actualizaciondatosid'],
  asignacion: ['sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid'],
  carga: ['sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_cargadiariaid'],
};

function leerRegistro() {
  let r; try { r = JSON.parse(fs.readFileSync(REG(), 'utf8')); } catch (e) { r = {}; }
  return { creados: r.creados || [], claves: r.claves || [] };
}
function guardarRegistro(r) { fs.mkdirSync(path.dirname(REG()), { recursive: true }); fs.writeFileSync(REG(), JSON.stringify(r, null, 1)); }
function registrar(tipo, id, nota) {
  const r = leerRegistro(); r.creados.push({ tipo, id, nota, t: new Date().toISOString() }); guardarRegistro(r);
}
// Clave exacta de lo que crea la APP (o de lo que se va a crear): se anota ANTES de crearlo para poder
// encontrarlo aunque la corrida se caiga en el medio. campo: 'clavepan' (cuenta) o 'cif' (cliente).
function registrarClave(campo, valor, nota) {
  const r = leerRegistro();
  if (!r.claves.some(c => c.campo === campo && c.valor === valor)) { r.claves.push({ campo, valor, nota, t: new Date().toISOString() }); guardarRegistro(r); }
}
// Claves fijas de la siembra: siempre se buscan, para que un resto de una corrida anterior de ESTA suite
// (con el registro perdido) no duplique la siembra. Son valores exactos, no prefijos.
const CUENTAS_SIEMBRA = ['0001', '0002', '0003', '0004'];
const CIF_SIEMBRA = `${P}-CIF-001`;
const CLAVES_SIEMBRA = [{ campo: 'cif', valor: CIF_SIEMBRA }, ...CUENTAS_SIEMBRA.map(n => ({ campo: 'clavepan', valor: `${P}-CLAVE-${n}` }))];
async function crear(tipo, data, nota) {
  const [set, pk] = ES[tipo];
  const rec = await dv.create(set, data);
  registrar(tipo, rec[pk], nota);
  return rec;
}
const bind = (set, id) => `/${set}(${id})`;
// Lo que la app muestra de una cuenta QA: "•••• E2E1" (CuentaMasc de App.Formulas).
const mascara = (cuenta) => `•••• ${cuenta.sanic_cm_ct_ultimos4}`;

// Agente REAL activo usado como lookup de las interacciones QA (solo referencia, no se modifica).
async function agenteActivoConUsuario() {
  const r = await dv.get("sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre,sanic_cm_ag_emailusuario,_sanic_cm_ag_usuarioid_value&$filter=sanic_cm_ag_activo eq true and sanic_cm_ag_emailusuario ne null&$orderby=sanic_cm_ag_nombre");
  // Preferir uno que NO sea la cuenta admin (el admin está fuera del alcance de la suite).
  return r.value.find(a => !/^admin@/i.test(a.sanic_cm_ag_emailusuario)) || r.value[0];
}

// Siembra base: 1 cliente, 4 cuentas (0003 = falla controlada, 0004 = reactivación), 1 teléfono "antes", 2 interacciones pendientes (pago / actualización).
async function sembrar() {
  const prod = (await dv.get("sanic_cm_tbl_productos?$select=sanic_cm_pr_codigo,sanic_cm_pr_tipo&$filter=sanic_cm_pr_codigo eq 'TC001'")).value[0];
  const ag = await agenteActivoConUsuario();
  for (const k of CLAVES_SIEMBRA) registrarClave(k.campo, k.valor, 'siembra');
  const cli = await crear('cliente', {
    sanic_cm_cl_cif: CIF_SIEMBRA, sanic_cm_cl_nombre: `${P} CLIENTE UNO`, sanic_cm_cl_departamento: `${P} Managua`,
  }, 'cliente base');
  const cliId = cli.sanic_cm_tbl_clienteid;
  const cuentas = [];
  for (const n of CUENTAS_SIEMBRA) {
    const c = await crear('cuenta', {
      sanic_cm_ct_numerodecuenta: `${P}-CTA-${n}`, sanic_cm_ct_nombrecliente: `${P} CLIENTE UNO`,
      sanic_cm_ct_ultimos4: `E2E${n.slice(-1)}`, sanic_cm_ct_clavepan: `${P}-CLAVE-${n}`,
      sanic_cm_ct_cifcliente: CIF_SIEMBRA, sanic_cm_ct_tipoproducto: prod.sanic_cm_pr_tipo,
      sanic_cm_ct_saldovencidonio: 1500, sanic_cm_ct_saldovencidousd: 0, sanic_cm_ct_diasmora: 30,
      'sanic_cm_ct_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cliId),
      'sanic_cm_ct_productoid@odata.bind': bind('sanic_cm_tbl_productos', prod.sanic_cm_tbl_productoid),
    }, `cuenta ${n}`);
    cuentas.push(c);
    // Bóveda 1:1 (la lee el rol BackOffice): el nombre principal va enmascarado, el "PAN" QA es la cuenta QA.
    await crear('boveda', {
      sanic_cm_cp_name: `****${c.sanic_cm_ct_ultimos4}`, sanic_cm_cp_pan: c.sanic_cm_ct_numerodecuenta,
      sanic_cm_cp_clavepan: c.sanic_cm_ct_clavepan,
      'sanic_cm_cp_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', c.sanic_cm_tbl_cuentaid),
    }, `boveda ${n}`);
  }
  await crear('telefono', {
    sanic_cm_tl_cif: CIF_SIEMBRA, sanic_cm_tl_numero: '2222-0000', sanic_cm_tl_tipo: 'Casa',
    'sanic_cm_tl_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cliId),
  }, 'telefono antes');
  const ahora = Date.now();
  const mkInt = (min, extra, nota) => crear('interaccion', {
    sanic_cm_in_nombreagente: `${P} Localizador`,
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
    sanic_cm_pg_fecha: new Date().toISOString(),
    'sanic_cm_pg_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', cuentas[0].sanic_cm_tbl_cuentaid),
    sanic_cm_pg_numerointeraccion: int1.sanic_cm_in_numerointeraccion,
    'sanic_cm_pg_interaccion@odata.bind': bind('sanic_cm_tbl_interaccions', int1.sanic_cm_tbl_interaccionid),
  }, 'pago de interaccion 1');
  const ad = await crear('actualizacion', {
    sanic_cm_ad_telefonocasa: '8888-0000', sanic_cm_ad_correoelectronico: 'qa-e2e@example.com',
    'sanic_cm_ad_interaccionid@odata.bind': bind('sanic_cm_tbl_interaccions', int2.sanic_cm_tbl_interaccionid),
  }, 'actualizacion de interaccion 2');
  return { prod, agente: ag, cliente: cli, cuentas, int1, int2, pago, ad };
}

// Especial QA pendiente creada por API (para el escenario de rechazo del Supervisor).
// `owner`: systemuserid dueño (como si la hubiera creado ese usuario desde la app); `estado`: false = CERRADA.
async function crearEspecialPendiente(cuenta, solicitanteId, nota, { notas, nio = 100, owner, estado = true } = {}) {
  const a = await crear('asignacion', {
    sanic_cm_as_clavepan: cuenta.sanic_cm_ct_clavepan, sanic_cm_as_tipo: 3, sanic_cm_as_estado: estado,
    sanic_cm_as_estadoaprobacion: 2, sanic_cm_as_retironio: nio, sanic_cm_as_notas: notas || `Verificación — ${P} ${nota}`,
    sanic_cm_as_fechaasignada: new Date().toISOString().slice(0, 10),
    ...(estado ? {} : { sanic_cm_as_motivo: `Gestión completada — ${P} ${nota}`, sanic_cm_as_fechacierre: new Date().toISOString() }),
    'sanic_cm_as_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', cuenta.sanic_cm_tbl_cuentaid),
    'sanic_cm_as_solicitadopor@odata.bind': bind('systemusers', solicitanteId),
  }, nota);
  if (owner) await dv.patch('sanic_cm_tbl_asignacions', a.sanic_cm_tbl_asignacionid, { 'ownerid@odata.bind': `/systemusers(${owner})` });
  return a;
}

// Fila QA de carga diaria con FechaCarga = HOY (DateOnly).
async function crearCargaHoy(cliId, cuenta, fechaIso) {
  const cuentaId = cuenta.sanic_cm_tbl_cuentaid;
  return crear('carga', {
    // cd_numerocuenta es el nombre principal (no se muestra); la clave alternativa es (cd_clavepan, cd_fechacarga).
    sanic_cm_cd_numerocuenta: cuenta.sanic_cm_ct_numerodecuenta, sanic_cm_cd_clavepan: cuenta.sanic_cm_ct_clavepan, sanic_cm_cd_fechacarga: fechaIso, sanic_cm_cd_tipoasignacion: `${P}`,
    'sanic_cm_cd_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cliId),
    'sanic_cm_cd_cuenta@odata.bind': bind('sanic_cm_tbl_cuentas', cuentaId),
  }, 'carga de hoy QA');
}

// Inventario de la corrida: lo anotado en el registro y lo que cuelga de eso. Lo que crea la APP (Especiales por
// UI, cliente/teléfono de Maestros) se encuentra por lookup a las cuentas/clientes anotados o por la clave exacta
// anotada (clave sustituta de la cuenta, que el plugin ClaveDesdeCuenta copia a la asignación; CIF del cliente).
async function inventarioQA() {
  const uniq = a => [...new Set(a)];
  const q = async (tipo, filtro) => {
    const [set, pk] = ES[tipo];
    return (await dv.getAll(`${set}?$select=${pk}&$filter=${encodeURIComponent(filtro)}`)).map(x => x[pk]);
  };
  // `campo eq v1 or campo eq v2 …`, en tandas para no pasarse del largo de la URL.
  const porLista = async (tipo, campo, valores, comillas = false) => {
    const out = [];
    for (let i = 0; i < valores.length; i += 40) {
      out.push(...await q(tipo, valores.slice(i, i + 40).map(v => `${campo} eq ${comillas ? `'${String(v).replace(/'/g, "''")}'` : v}`).join(' or ')));
    }
    return out;
  };
  const reg = leerRegistro();
  const deReg = t => reg.creados.filter(x => x.tipo === t).map(x => x.id);
  const claves = [...CLAVES_SIEMBRA, ...reg.claves];
  const clavesPan = uniq(claves.filter(c => c.campo === 'clavepan').map(c => c.valor));
  const cifs = uniq(claves.filter(c => c.campo === 'cif').map(c => c.valor));

  const cliente = uniq([...await porLista('cliente', 'sanic_cm_cl_cif', cifs, true), ...deReg('cliente')]);
  const cuenta = uniq([
    ...await porLista('cuenta', 'sanic_cm_ct_clavepan', clavesPan, true),
    ...await porLista('cuenta', '_sanic_cm_ct_clienteid_value', cliente), ...deReg('cuenta')]);
  const interaccion = uniq([...await porLista('interaccion', '_sanic_cm_in_cuentaid_value', cuenta), ...deReg('interaccion')]);
  return {
    pago: uniq([
      ...await porLista('pago', '_sanic_cm_pg_cuentaid_value', cuenta),
      ...await porLista('pago', '_sanic_cm_pg_interaccion_value', interaccion), ...deReg('pago')]),
    actualizacion: uniq([...await porLista('actualizacion', '_sanic_cm_ad_interaccionid_value', interaccion), ...deReg('actualizacion')]),
    interaccion,
    asignacion: uniq([
      ...await porLista('asignacion', 'sanic_cm_as_clavepan', clavesPan, true),
      ...await porLista('asignacion', '_sanic_cm_as_cuentaid_value', cuenta), ...deReg('asignacion')]),
    carga: uniq([
      ...await porLista('carga', 'sanic_cm_cd_clavepan', clavesPan, true),
      ...await porLista('carga', '_sanic_cm_cd_cuenta_value', cuenta), ...deReg('carga')]),
    boveda: uniq([
      ...await porLista('boveda', 'sanic_cm_cp_clavepan', clavesPan, true),
      ...await porLista('boveda', '_sanic_cm_cp_cuentaid_value', cuenta), ...deReg('boveda')]),
    cuenta,
    telefono: uniq([
      ...await porLista('telefono', 'sanic_cm_tl_cif', cifs, true),
      ...await porLista('telefono', '_sanic_cm_tl_clienteid_value', cliente), ...deReg('telefono')]),
    direccion: uniq([
      ...await porLista('direccion', 'sanic_cm_dr_cif', cifs, true),
      ...await porLista('direccion', '_sanic_cm_dr_clienteid_value', cliente), ...deReg('direccion')]),
    cliente,
  };
}

async function limpiar() {
  const inv = await inventarioQA();
  const res = {};
  for (const tipo of ['pago', 'actualizacion', 'interaccion', 'asignacion', 'carga', 'boveda', 'cuenta', 'telefono', 'direccion', 'cliente']) {
    res[tipo] = { borrados: 0, yaNoExistian: 0, errores: [] };
    for (const id of inv[tipo]) {
      try { const st = await dv.del(ES[tipo][0], id); if (st === 404) res[tipo].yaNoExistian++; else res[tipo].borrados++; }
      catch (e) { res[tipo].errores.push(`${id}: ${e.message.slice(0, 160)}`); }
    }
  }
  // Verificación: el inventario de la corrida debe dar CERO en todas las tablas.
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
  ES, mascara, sembrar, crearEspecialPendiente, crearCargaHoy, registrar, registrarClave, leerRegistro, inventarioQA, limpiar,
  membresias, agregarATeam, quitarDeTeam, restaurarMembresias,
};
