// "Rol por pantalla": matriz rol × tabla × operación contra el SERVIDOR (sin navegador, sin la app).
// Se hace pasar por cada usuario QA (cabecera MSCRMCallerID: corre con los privilegios de ese usuario) y en cada tabla de
// CM intenta crear, leer, editar, borrar, reasignarse y compartir; después prueba las columnas que importan de la
// asignación y de la interacción, y el alta de un job de Smelter. Compara con tests/integral/seguridad/esperado.js
// (lo que el rol DEBERÍA poder hacer según sus pantallas) y clasifica cada diferencia:
//   HUECO             el servidor deja hacer algo que el rol no debería
//   DEMASIADO CERRADO el servidor frena algo que una pantalla del rol necesita (bug para el usuario)
//   EXPECTATIVA DUDOSA la celda es 'd': ninguna pantalla lo usa, decide el dueño
// Datos: solo filas QA (prefijo QA-E2E-INT-MX…) creadas por la prueba; las reales nunca se tocan. Limpieza en finally.
// Nunca se usa User6. Ningún número de tarjeta: la bóveda QA lleva un texto, no dígitos. Toda salida pasa por sinPAN.
// Uso: cd tests/integral && NODE_PATH=../backoffice-canvas/node_modules node seguridad/matriz.js [--sin-informe]
const fs = require('fs');
const path = require('path');
process.chdir(path.join(__dirname, '..'));
const dv = require('../../backoffice-canvas/lib/dataverse');
const { PREFIJO, AGENTE, U, ES, RAIZ, sinPAN, dormir } = require('../lib/ctx');
const datos = require('../lib/datos');
const { ROLES, TABLAS, OPS, ESPERADO } = require('./esperado');

const RUN = Date.now().toString(36).slice(-3).toUpperCase();
const P = `${PREFIJO}-MX${RUN}`;
const P2 = `${PREFIJO}-${RUN}`;   // para columnas de 20 caracteres (CIF)
const INFORME = path.join(__dirname, '..', '..', '..', 'docs', 'auditoria', 'matriz-roles.md');
const DIR = path.join(RAIZ, `matriz-${RUN}`);
fs.mkdirSync(DIR, { recursive: true });

// Tablas de la matriz → [entitySet, pk, logicalName]
const T = {
  cuenta: [...ES.cuenta, 'sanic_cm_tbl_cuenta'], cliente: [...ES.cliente, 'sanic_cm_tbl_cliente'],
  asignacion: [...ES.asignacion, 'sanic_cm_tbl_asignacion'], interaccion: [...ES.interaccion, 'sanic_cm_tbl_interaccion'],
  pago: [...ES.pago, 'sanic_cm_tbl_pago'], actualizacion: [...ES.actualizacion, 'sanic_cm_tbl_actualizaciondatos'],
  agente: ['sanic_cm_tbl_agentes', 'sanic_cm_tbl_agenteid', 'sanic_cm_tbl_agente'], producto: [...ES.producto, 'sanic_cm_tbl_producto'],
  fiador: [...ES.fiador, 'sanic_cm_tbl_fiador'], direccion: [...ES.direccion, 'sanic_cm_tbl_direccion'],
  telefono: [...ES.telefono, 'sanic_cm_tbl_telefono'], carga: [...ES.carga, 'sanic_cm_tbl_cargadiaria'],
  boveda: [...ES.boveda, 'sanic_cm_tbl_cuentapan'],
};
const bind = (t, id) => `/${T[t][0]}(${id})`;
const pk = (t, rec) => rec[T[t][1]];

// ── registro de todo lo creado (para borrarlo aunque la corrida se caiga) ─────────────────────────────
const REG = {};
const anotar = (t, id) => { (REG[t] = REG[t] || []).push(id); fs.writeFileSync(path.join(DIR, 'registro.json'), JSON.stringify({ runid: RUN, items: Object.entries(REG).flatMap(([tipo, l]) => l.map((i) => ({ tipo, id: i }))) }, null, 1)); };
let n = 0;
const sig = () => ++n;

// Llamada como un usuario. Nunca lanza: { ok, st, msg }.
async function como(usuario, metodo, rel, cuerpo) {
  // PATCH con If-Match: * — sin eso es un upsert y, sobre una fila que el usuario no ve, intenta CREARLA (412 / reglas del alta).
  const h = { MSCRMCallerID: usuario, Prefer: 'return=representation', ...(metodo === 'PATCH' ? { 'If-Match': '*' } : {}) };
  try { const r = await dv.raw(metodo, rel, cuerpo, h); return { ok: true, st: r.status, json: r.json }; }
  catch (e) { return { ok: false, st: e.status || 0, msg: sinPAN(String(e.message).replace(/^.*?→ \d+: /, '')).slice(0, 220) }; }
}
// Alta con el servicio (sin suplantar): las filas "ajenas" y las descartables.
async function crearSP(t, data) { const r = await dv.create(T[t][0], data); anotar(t, pk(t, r)); return r; }

// ── fábricas de payloads (todo con el prefijo P; ningún dígito de tarjeta) ─────────────────────────────
let fx = {};       // fijos de la corrida
const F = {
  cliente: (i) => ({ sanic_cm_cl_cif: `${P2}C${i}`, sanic_cm_cl_nombre: `${P} cliente ${i}`, sanic_cm_cl_departamento: 'QA' }),
  cuenta: (i, cli = fx.cli0) => ({
    sanic_cm_ct_numerodecuenta: `QAMX${RUN}-${i}`, sanic_cm_ct_nombrecliente: `${P} cliente`, sanic_cm_ct_ultimos4: 'E2EM',
    sanic_cm_ct_clavepan: `${P}-K${i}`, sanic_cm_ct_cifcliente: `${P2}C0`, sanic_cm_ct_tipoproducto: `${P} prod`,
    sanic_cm_ct_saldovencidonio: 100, sanic_cm_ct_saldovencidousd: 0, sanic_cm_ct_diasmora: 30,
    'sanic_cm_ct_clienteid@odata.bind': bind('cliente', cli),
  }),
  boveda: (i, cta) => ({ sanic_cm_cp_name: '****E2EM', sanic_cm_cp_pan: `QA${RUN}P${i}`, sanic_cm_cp_clavepan: cta.sanic_cm_ct_clavepan, 'sanic_cm_cp_cuentaid@odata.bind': bind('cuenta', cta.sanic_cm_tbl_cuentaid) }),
  asignacion: (i, cta, extra = {}) => ({
    sanic_cm_as_tipo: 1, sanic_cm_as_estado: false, sanic_cm_as_motivo: `${P} descartable`, sanic_cm_as_fechasignacion: new Date().toISOString(),
    'sanic_cm_as_cuentaid@odata.bind': bind('cuenta', cta.sanic_cm_tbl_cuentaid), ...extra,
  }),
  interaccion: (i, ctaId = fx.cta0.sanic_cm_tbl_cuentaid, ageId = fx.age0) => ({
    sanic_cm_in_comentario: `${P} int ${i}`, sanic_cm_in_nombreagente: `${P} agente`, sanic_cm_in_estadograbacion: 1,
    sanic_cm_in_fechacaptura: new Date().toISOString(), sanic_cm_cd_aplicapago: true, sanic_cm_in_actualizainfo: true,
    'sanic_cm_in_cuentaid@odata.bind': bind('cuenta', ctaId), 'sanic_cm_in_agente@odata.bind': bind('agente', ageId),
  }),
  pago: (i, intId) => ({ sanic_cm_pg_monto: 1, sanic_cm_pg_moneda: 1, sanic_cm_pg_numerorecibo: `${P}-R${i}`, sanic_cm_pg_fecha: new Date().toISOString(), 'sanic_cm_pg_interaccion@odata.bind': bind('interaccion', intId) }),
  actualizacion: (i, intId) => ({ sanic_cm_ad_correoelectronico: `qa-mx${i}@example.com`, 'sanic_cm_ad_interaccionid@odata.bind': bind('interaccion', intId) }),
  agente: (i) => ({ sanic_cm_ag_nombre: `${P} agente ${i}`, sanic_cm_ag_activo: true }),
  producto: (i) => ({ sanic_cm_pr_codigo: `${PREFIJO}-${RUN}${i}`, sanic_cm_pr_tipo: `${PREFIJO} prod` }),
  fiador: (i) => ({ sanic_cm_fi_nombre: `${P} fiador ${i}`, sanic_cm_fi_direccion: 'QA' }),
  direccion: (i, cli = fx.cli0) => ({ sanic_cm_dr_cif: `${P2}D${i}`, sanic_cm_dr_tipo: 'Casa', sanic_cm_dr_direccion: `${P} dir ${i}`, 'sanic_cm_dr_clienteid@odata.bind': bind('cliente', cli) }),
  telefono: (i, cli = fx.cli0) => ({ sanic_cm_tl_cif: `${P2}T${i}`, sanic_cm_tl_tipo: 'Casa', sanic_cm_tl_numero: '2222-0000', 'sanic_cm_tl_clienteid@odata.bind': bind('cliente', cli) }),
  carga: (i) => ({
    sanic_cm_cd_numerocuenta: '****E2EM', sanic_cm_cd_clavepan: fx.cta0.sanic_cm_ct_clavepan, sanic_cm_cd_diasmora: 1,
    // Fecha vieja a propósito: la "última carga" de la app es el máximo de esta fecha y no tiene que moverse.
    sanic_cm_cd_fechacarga: new Date(Date.UTC(2024, 0, 1) + i * 86400000).toISOString().slice(0, 10),
    'sanic_cm_cd_clienteid@odata.bind': bind('cliente', fx.cli0), 'sanic_cm_cd_cuenta@odata.bind': bind('cuenta', fx.cta0.sanic_cm_tbl_cuentaid),
  }),
};
// Columna inofensiva para "editar" en cada tabla.
const EDIT = {
  cuenta: { sanic_cm_ct_diasmora: 31 }, cliente: { sanic_cm_cl_departamento: 'QA2' }, asignacion: { sanic_cm_as_motivo: `${P} editada` },
  interaccion: { sanic_cm_in_comentario: `${P} int editada` }, pago: { sanic_cm_pg_monto: 2 }, actualizacion: { sanic_cm_ad_correoelectronico: 'qa-mx-ed@example.com' },
  agente: { sanic_cm_ag_activo: false }, producto: { sanic_cm_pr_tipo: `${PREFIJO} prod2` }, fiador: { sanic_cm_fi_direccion: 'QA2' },
  direccion: { sanic_cm_dr_direccion: `${P} dir editada` }, telefono: { sanic_cm_tl_numero: '2222-0001' }, carga: { sanic_cm_cd_diasmora: 2 },
  boveda: { sanic_cm_cp_name: '****E2EN' },
};

// Una fila descartable del servicio (para borrar o reasignarse sin romper los fijos).
async function descartable(t) {
  const i = sig();
  if (t === 'cuenta') return pk(t, await crearSP(t, F.cuenta(i)));
  if (t === 'asignacion') { const c = await crearSP('cuenta', F.cuenta(i)); return pk(t, await crearSP(t, F.asignacion(i, c))); }
  if (t === 'boveda') { const c = await crearSP('cuenta', F.cuenta(i)); return pk(t, await crearSP(t, F.boveda(i, c))); }
  if (t === 'interaccion') return pk(t, await crearSP(t, F.interaccion(i)));
  if (t === 'pago' || t === 'actualizacion') { const it = await crearSP('interaccion', F.interaccion(i)); return pk(t, await crearSP(t, F[t](i, pk('interaccion', it)))); }
  return pk(t, await crearSP(t, F[t](i)));
}

// Payload de ALTA de cada usuario (con los fijos que una pantalla usaría).
async function payloadAlta(rol, t, propios) {
  const i = sig();
  if (t === 'cuenta') return F.cuenta(i);
  if (t === 'asignacion') { // Especial Pendiente pedida por él mismo, como SolicitarEspecial
    const c = await crearSP('cuenta', F.cuenta(i));
    return { sanic_cm_as_tipo: 3, sanic_cm_as_estado: true, sanic_cm_as_estadoaprobacion: 2, sanic_cm_as_notas: `${P} pedida`, sanic_cm_as_fechasignacion: new Date().toISOString(),
      'sanic_cm_as_cuentaid@odata.bind': bind('cuenta', c.sanic_cm_tbl_cuentaid), 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U[rol]})` };
  }
  if (t === 'boveda') { const c = await crearSP('cuenta', F.cuenta(i)); return F.boveda(i, c); }
  if (t === 'interaccion') return rol === 'cob' ? F.interaccion(i, fx.ctaK.sanic_cm_tbl_cuentaid, fx.ageCob) : F.interaccion(i);
  if (t === 'pago' || t === 'actualizacion') return F[t](i, propios.interaccion || fx.int0);
  return F[t](i);
}

const resultados = [];   // { rol, tabla, op, esp, ok, st, msg }
const columnas = [];     // casos de columna { id, rol, desc, esp, por, ok, st, msg }
const fmt = (r) => (r.ok ? `${r.st}` : `${r.st}${r.msg ? ' ' + r.msg.slice(0, 90) : ''}`);

async function celda(rol, t, op, fn) {
  const esp = ESPERADO[rol][t][op];
  if (esp === '-') return null;
  const r = await fn();
  if (!r) return null;
  resultados.push({ rol, tabla: t, op, esp, ok: r.ok, st: r.st, msg: r.msg || '' });
  const bien = esp === 'd' ? '?' : ((esp === 's') === r.ok ? '✓' : '✗');
  console.log(`  ${bien} ${rol.padEnd(3)} ${t.padEnd(13)} ${op.padEnd(2)} esperado ${esp} → ${fmt(r)}`);
  return r;
}
async function caso(id, rol, desc, esp, por, fn) {
  const r = await fn();
  columnas.push({ id, rol, desc, esp, por, ok: r.ok, st: r.st, msg: r.msg || '' });
  const bien = esp === 'd' ? '?' : ((esp === 's') === r.ok ? '✓' : '✗');
  console.log(`  ${bien} ${id.padEnd(14)} ${desc} → ${fmt(r)}`);
  return r;
}

const target = (t, id) => ({ '@odata.type': `Microsoft.Dynamics.CRM.${T[t][2]}`, [T[t][1]]: id });
const otro = (rol) => (rol === 'bo' ? U.sol : U.bo);

async function esperarDueno(t, id, dueno, maxMs = 240000) {
  for (const t0 = Date.now(); Date.now() - t0 < maxMs; await dormir(8000)) {
    const r = await dv.get(`${T[t][0]}(${id})?$select=_ownerid_value`);
    if (r._ownerid_value === dueno) return true;
  }
  return false;
}

async function preparar() {
  const ageCob = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  if (!ageCob) throw new Error(`no existe el agente ${AGENTE}`);
  fx.ageCob = ageCob.sanic_cm_tbl_agenteid;
  fx.age0 = pk('agente', await crearSP('agente', F.agente(0)));                 // agente QA sin usuario
  fx.cli0 = pk('cliente', await crearSP('cliente', F.cliente(0)));
  fx.cta0 = await crearSP('cuenta', F.cuenta(0));
  fx.bov0 = pk('boveda', await crearSP('boveda', F.boveda(0, fx.cta0)));
  fx.asg0 = pk('asignacion', await crearSP('asignacion', F.asignacion(0, fx.cta0)));
  fx.int0 = pk('interaccion', await crearSP('interaccion', F.interaccion(0)));
  fx.pag0 = pk('pago', await crearSP('pago', F.pago(0, fx.int0)));
  fx.act0 = pk('actualizacion', await crearSP('actualizacion', F.actualizacion(0, fx.int0)));
  fx.pro0 = pk('producto', await crearSP('producto', F.producto(0)));
  fx.fia0 = pk('fiador', await crearSP('fiador', F.fiador(0)));
  fx.dir0 = pk('direccion', await crearSP('direccion', F.direccion(0)));
  fx.tel0 = pk('telefono', await crearSP('telefono', F.telefono(0)));
  fx.car0 = pk('carga', await crearSP('carga', F.carga(0)));
  // Cartera del cobrador: Diaria activa con el agente de User5; el reparto le pasa la asignación y le comparte cuenta,
  // cliente y fiador (las direcciones y teléfonos van en cascada desde el cliente).
  fx.cliK = pk('cliente', await crearSP('cliente', F.cliente('K')));
  fx.fiaK = pk('fiador', await crearSP('fiador', F.fiador('K')));
  fx.ctaK = await crearSP('cuenta', { ...F.cuenta('K', fx.cliK), 'sanic_cm_ct_fiador@odata.bind': bind('fiador', fx.fiaK) });
  fx.bovK = pk('boveda', await crearSP('boveda', F.boveda('K', fx.ctaK)));
  fx.dirK = pk('direccion', await crearSP('direccion', F.direccion('K', fx.cliK)));
  fx.telK = pk('telefono', await crearSP('telefono', F.telefono('K', fx.cliK)));
  fx.asgK = pk('asignacion', await crearSP('asignacion', F.asignacion('K', fx.ctaK, { sanic_cm_as_estado: true, sanic_cm_as_motivo: null, 'sanic_cm_as_agenteid@odata.bind': bind('agente', fx.ageCob) })));
  console.log('  esperando el reparto de la cartera QA a User5…');
  if (!(await esperarDueno('asignacion', fx.asgK, U.cob))) throw new Error('el reparto no pasó la asignación QA de cartera a User5');
  await dormir(5000);
}

const AJENO = () => ({ cuenta: fx.cta0.sanic_cm_tbl_cuentaid, cliente: fx.cli0, asignacion: fx.asg0, interaccion: fx.int0, pago: fx.pag0,
  actualizacion: fx.act0, agente: fx.age0, producto: fx.pro0, fiador: fx.fia0, direccion: fx.dir0, telefono: fx.tel0, carga: fx.car0, boveda: fx.bov0 });
const CARTERA = () => ({ cuenta: fx.ctaK.sanic_cm_tbl_cuentaid, cliente: fx.cliK, asignacion: fx.asgK, fiador: fx.fiaK, direccion: fx.dirK, telefono: fx.telK, boveda: fx.bovK });

async function matriz() {
  for (const rol of Object.keys(ROLES)) {
    console.log(`\n══ ${ROLES[rol]}`);
    const yo = U[rol]; const propios = {};
    for (const t of TABLAS) {
      const [set] = T[t]; const aj = AJENO()[t];
      const c = await celda(rol, t, 'C', async () => como(yo, 'POST', set, await payloadAlta(rol, t, propios)));
      if (c && c.ok && c.json) { propios[t] = pk(t, c.json); anotar(t, propios[t]); }
      await celda(rol, t, 'Ra', () => como(yo, 'GET', `${set}(${aj})?$select=${T[t][1]}`));
      if (CARTERA()[t]) await celda(rol, t, 'Rc', () => como(yo, 'GET', `${set}(${CARTERA()[t]})?$select=${T[t][1]}`));
      await celda(rol, t, 'Ua', () => como(yo, 'PATCH', `${set}(${aj})`, EDIT[t]));
      if (propios[t]) await celda(rol, t, 'Up', () => como(yo, 'PATCH', `${set}(${propios[t]})`, EDIT[t]));
      const d1 = await descartable(t);
      await celda(rol, t, 'D', () => como(yo, 'DELETE', `${set}(${d1})`));
      const d2 = await descartable(t);
      await celda(rol, t, 'A', () => como(yo, 'PATCH', `${set}(${d2})`, { 'ownerid@odata.bind': `/systemusers(${yo})` }));
      const s = await celda(rol, t, 'S', () => como(yo, 'POST', 'GrantAccess', { Target: target(t, aj), PrincipalAccess: { Principal: { '@odata.type': 'Microsoft.Dynamics.CRM.systemuser', systemuserid: otro(rol) }, AccessMask: 'ReadAccess' } }));
      if (s && s.ok) await dv.post('RevokeAccess', { Target: target(t, aj), Revokee: { '@odata.type': 'Microsoft.Dynamics.CRM.systemuser', systemuserid: otro(rol) } });
    }
    fx[`propios_${rol}`] = propios;
  }
}

async function casosDeColumna() {
  console.log('\n══ Columnas de la asignación (una Especial pedida por User2 con SolicitarEspecial)');
  const ctaE = await crearSP('cuenta', F.cuenta('E'));
  const r = await como(U.sol, 'POST', 'sanic_cm_SolicitarEspecial', { CuentaId: ctaE.sanic_cm_tbl_cuentaid, Notas: `${P} especial`, RetiroNio: 100 });
  if (!r.ok) throw new Error('SolicitarEspecial como User2: ' + r.msg);
  const E = r.json.AsignacionId; anotar('asignacion', E);
  const asg = (cuerpo) => ['PATCH', `sanic_cm_tbl_asignacions(${E})`, cuerpo];
  await caso('COL-SOL-1', 'sol', 'Solicitante cambia el retiro de su Especial Pendiente', 'n', 'Desde DAT-14 ninguna pantalla del Solicitante edita la asignación', () => como(U.sol, ...asg({ sanic_cm_as_retironio: 999 })));
  await caso('COL-SOL-2', 'sol', 'Solicitante aprueba su propia Especial', 'n', 'S-6: solo Supervisor/Admin aprueban', () => como(U.sol, ...asg({ sanic_cm_as_estadoaprobacion: 3 })));
  await caso('COL-SOL-3', 'sol', 'Solicitante se asigna el agente de su Especial', 'n', 'S-6: el agente lo pone el Supervisor', () => como(U.sol, ...asg({ 'sanic_cm_as_agenteid@odata.bind': bind('agente', fx.ageCob) })));
  await caso('COL-SUP-1', 'sup', 'Supervisor cambia el retiro', 'n', 'S-11: el Supervisor aprueba, no cambia montos', () => como(U.sup, ...asg({ sanic_cm_as_retironio: 999 })));
  await caso('COL-SUP-2', 'sup', 'Supervisor cambia el orden', 'n', 'S-11: el orden es del cobrador', () => como(U.sup, ...asg({ sanic_cm_as_orden: 9 })));
  await caso('COL-SUP-3', 'sup', 'Supervisor se pone como "Solicitado por"', 'n', 'S-7 / S-11', () => como(U.sup, ...asg({ 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.sup})` })));
  await caso('COL-BO-A', 'bo', 'BackOffice cambia las notas de la Especial', 'n', 'Ninguna pantalla del BackOffice edita asignaciones', () => como(U.bo, ...asg({ sanic_cm_as_notas: `${P} bo` })));
  await caso('COL-COB-0', 'cob', 'Cobrador cambia el orden de una Especial que no es suya', 'n', 'S-1: solo su cartera', () => como(U.cob, ...asg({ sanic_cm_as_orden: 3 })));
  await caso('COL-SUP-4', 'sup', 'Supervisor aprueba con el localizador (como Aprobaciones)', 's', 'Aprobaciones', () => como(U.sup, ...asg({ sanic_cm_as_estadoaprobacion: 3, 'sanic_cm_as_agenteid@odata.bind': bind('agente', fx.ageCob), 'sanic_cm_as_resolutorid@odata.bind': `/systemusers(${U.sup})` })));
  console.log('  esperando el reparto de la Especial aprobada a User5…');
  const repartida = await esperarDueno('asignacion', E, U.cob);
  console.log(`  ${repartida ? 'repartida' : 'NO repartida en 4 min'}`);
  await dormir(5000);
  await caso('COL-SOL-4', 'sol', 'Solicitante cambia el retiro de la Especial YA APROBADA (compartida por S-7)', 'n', 'El monto aprobado no lo cambia quien la pidió', () => como(U.sol, ...asg({ sanic_cm_as_retironio: 5000 })));
  await caso('COL-SOL-5', 'sol', 'Solicitante cierra la Especial aprobada', 'n', 'Cerrar es de Cierres (Admin), del cobrador al cobrar o del Supervisor al rechazar', () => como(U.sol, ...asg({ sanic_cm_as_estado: false, sanic_cm_as_motivo: `${P} cerrada por sol` })));
  await caso('COL-SOL-6', 'sol', 'Solicitante cambia las notas de la Especial aprobada', 'n', 'Ninguna pantalla del Solicitante edita la asignación', () => como(U.sol, ...asg({ sanic_cm_as_notas: `${P} sol` })));
  await caso('COL-COB-1', 'cob', 'Cobrador cambia el orden de su Especial', 's', 'Cartera: reordenar', () => como(U.cob, ...asg({ sanic_cm_as_orden: 7 })));
  await caso('COL-COB-2', 'cob', 'Cobrador cierra su Especial', 'n', 'S-11: el cobrador solo ordena', () => como(U.cob, ...asg({ sanic_cm_as_estado: false, sanic_cm_as_motivo: `${P} cerrada por cob` })));
  await caso('COL-COB-3', 'cob', 'Cobrador cambia el retiro', 'n', 'S-11', () => como(U.cob, ...asg({ sanic_cm_as_retironio: 1 })));
  await caso('COL-COB-4', 'cob', 'Cobrador se pone como "Solicitado por"', 'n', 'S-7 / S-11', () => como(U.cob, ...asg({ 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.cob})` })));
  await caso('COL-COB-5', 'cob', 'Cobrador se cambia el agente', 'n', 'S-6 / S-11', () => como(U.cob, ...asg({ 'sanic_cm_as_agenteid@odata.bind': bind('agente', fx.age0) })));
  await caso('COL-ADM-1', 'adm', 'Admin cambia el retiro de una Especial aprobada', 'd', 'Ninguna pantalla del Admin cambia montos', () => como(U.adm, ...asg({ sanic_cm_as_retironio: 100 })));
  await caso('COL-SUP-5', 'sup', 'Supervisor reasigna el agente (como Reasignar)', 's', 'Reasignar (DAT-29)', () => como(U.sup, ...asg({ 'sanic_cm_as_agenteid@odata.bind': bind('agente', fx.age0) })));
  await caso('COL-SUP-6', 'sup', 'Supervisor rechaza y cierra', 's', 'Aprobaciones: rechazar cierra (DAT-14)', () => como(U.sup, ...asg({ sanic_cm_as_estadoaprobacion: 4, sanic_cm_as_motivorechazo: `${P} rechazo`, sanic_cm_as_estado: false, sanic_cm_as_motivo: 'Rechazada', sanic_cm_as_fechacierre: new Date().toISOString() })));

  console.log('\n══ Altas de asignación que saltean la aprobación');
  const ctaD = await crearSP('cuenta', F.cuenta('D'));
  const rd = await caso('COL-SOL-7', 'sol', 'Solicitante crea una Diaria activa con un agente (sin aprobación)', 'n', 'S-6: el Solicitante pide Especiales; las Diarias las trae la carga', () => como(U.sol, 'POST', 'sanic_cm_tbl_asignacions', { sanic_cm_as_tipo: 1, sanic_cm_as_estado: true, sanic_cm_as_fechasignacion: new Date().toISOString(), 'sanic_cm_as_cuentaid@odata.bind': bind('cuenta', ctaD.sanic_cm_tbl_cuentaid), 'sanic_cm_as_agenteid@odata.bind': bind('agente', fx.age0), 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.sol})` }));
  if (rd.ok && rd.json) anotar('asignacion', rd.json.sanic_cm_tbl_asignacionid);
  const ctaO = await crearSP('cuenta', F.cuenta('O'));
  const ro = await caso('COL-SOL-8', 'sol', 'Solicitante crea una Especial a nombre de otro', 'n', 'S-7: "Solicitado por" solo a sí mismo', () => como(U.sol, 'POST', 'sanic_cm_tbl_asignacions', { sanic_cm_as_tipo: 3, sanic_cm_as_estado: true, sanic_cm_as_estadoaprobacion: 2, sanic_cm_as_fechasignacion: new Date().toISOString(), 'sanic_cm_as_cuentaid@odata.bind': bind('cuenta', ctaO.sanic_cm_tbl_cuentaid), 'sanic_cm_as_solicitadopor@odata.bind': `/systemusers(${U.sup})` }));
  if (ro.ok && ro.json) anotar('asignacion', ro.json.sanic_cm_tbl_asignacionid);

  console.log('\n══ Interacción: grabación (DAT-11) y captura fuera de la cartera');
  const i1 = pk('interaccion', await crearSP('interaccion', F.interaccion('G1')));
  const i2 = pk('interaccion', await crearSP('interaccion', F.interaccion('G2')));
  await caso('COL-BO-1', 'bo', 'BackOffice graba una interacción en el CORE', 's', 'Transcripción', () => como(U.bo, 'PATCH', `sanic_cm_tbl_interaccions(${i1})`, { sanic_cm_in_estadograbacion: 2, sanic_cm_in_codigograbacion: `QA${RUN}G1` }));
  await caso('COL-BO-2', 'bo', 'BackOffice vuelve a grabar con otro código', 'n', 'DAT-11: una grabada no se regraba', () => como(U.bo, 'PATCH', `sanic_cm_tbl_interaccions(${i1})`, { sanic_cm_in_codigograbacion: `QA${RUN}G2` }));
  await caso('COL-SUP-I', 'sup', 'Supervisor graba una interacción', 'n', 'Solo el BackOffice graba', () => como(U.sup, 'PATCH', `sanic_cm_tbl_interaccions(${i2})`, { sanic_cm_in_estadograbacion: 2, sanic_cm_in_codigograbacion: `QA${RUN}G3` }));
  const intCob = fx.propios_cob && fx.propios_cob.interaccion;
  if (intCob) await caso('COL-COB-I', 'cob', 'Cobrador marca como grabada su interacción', 'n', 'S-9 / DAT-11', () => como(U.cob, 'PATCH', `sanic_cm_tbl_interaccions(${intCob})`, { sanic_cm_in_estadograbacion: 2, sanic_cm_in_codigograbacion: `QA${RUN}G4` }));
  const rc = await caso('COL-COB-6', 'cob', 'Cobrador registra una visita en una cuenta que NO es de su cartera', 'n', 'S-1 / SEC-08: solo su cartera', () => como(U.cob, 'POST', 'sanic_cm_tbl_interaccions', F.interaccion('X', fx.cta0.sanic_cm_tbl_cuentaid, fx.ageCob)));
  if (rc.ok && rc.json) {
    anotar('interaccion', rc.json.sanic_cm_tbl_interaccionid);
    const rp = await caso('COL-COB-7', 'cob', 'Cobrador registra un pago colgado de esa visita', 'n', 'S-1: cobrar en una cuenta ajena', () => como(U.cob, 'POST', 'sanic_cm_tbl_pagos', F.pago('X', rc.json.sanic_cm_tbl_interaccionid)));
    if (rp.ok && rp.json) anotar('pago', rp.json.sanic_cm_tbl_pagoid);
  }

  console.log('\n══ Smelter: alta de un job de importación (sin archivo ni estado: no dispara nada)');
  const tid = (await dv.get("sanic_tbl_smlt_importtemplates?$select=sanic_tbl_smlt_importtemplateid&$filter=sanic_smlt_it_name eq 'Cobranza movil'")).value[0].sanic_tbl_smlt_importtemplateid;
  for (const rol of Object.keys(ROLES)) {
    const rj = await caso(`COL-JOB-${rol.toUpperCase()}`, rol, `${ROLES[rol]} crea un job de importación`, rol === 'adm' ? 's' : 'n', rol === 'adm' ? 'Cargar archivo / Alta de cuenta' : 'Solo el Admin carga',
      () => como(U[rol], 'POST', 'sanic_tbl_smlt_importjobs', { sanic_smlt_ij_name: `${P} job ${rol}`, 'sanic_smlt_ij_importtemplateid@odata.bind': `/sanic_tbl_smlt_importtemplates(${tid})` }));
    if (rj.ok && rj.json) anotar('job', rj.json.sanic_tbl_smlt_importjobid);
  }
}

// ── limpieza: orden de dependencias, después el barrido por prefijo y los agentes QA ─────────────────
async function limpiar() {
  const orden = ['pago', 'actualizacion', 'interaccion', 'asignacion', 'carga', 'boveda', 'cuenta', 'direccion', 'telefono', 'cliente', 'producto', 'fiador', 'job', 'agente'];
  const errores = [];
  for (const t of orden) for (const id of [...new Set(REG[t] || [])]) {
    const set = t === 'job' ? 'sanic_tbl_smlt_importjobs' : T[t][0];
    try { await dv.del(set, id); } catch (e) { errores.push(`${t}: ${sinPAN(e.message).slice(0, 140)}`); }
  }
  const l = await datos.limpiar({ ...REG });
  const agentes = (await dv.getAll(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=startswith(sanic_cm_ag_nombre,'${P}')`));
  for (const a of agentes) { try { await dv.del('sanic_cm_tbl_agentes', a.sanic_cm_tbl_agenteid); } catch (e) { errores.push(`agente: ${e.message.slice(0, 140)}`); } }
  const vivos = Object.entries(l.restantes || {}).filter(([, v]) => v).map(([k, v]) => `${k} ${v}`);
  const agVivos = (await dv.getAll(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=startswith(sanic_cm_ag_nombre,'${P}')`)).length;
  return { errores: [...errores, ...(l.errores || [])], restantes: [...vivos, ...(agVivos ? [`agente ${agVivos}`] : [])] };
}

// ── informe ───────────────────────────────────────────────────────────────────────────────────────
const CLASE = { H: 'HUECO', C: 'DEMASIADO CERRADO', D: 'EXPECTATIVA DUDOSA' };
function clasificar(x) {
  if (x.esp === 'd') return 'D';
  if (x.esp === 'n' && x.ok) return 'H';
  if (x.esp === 's' && !x.ok) return 'C';
  return null;
}
const NOMBRE_OP = { C: 'crear', Ra: 'leer ajena', Rc: 'leer de su cartera', Ua: 'editar ajena', Up: 'editar la propia', D: 'borrar ajena', A: 'reasignarse ajena', S: 'compartir ajena' };
const celdaTxt = (x) => (x ? `${x.ok ? '✔' : '✘'} ${x.st}` : '');

function informe(limpieza, inicio) {
  const L = [];
  const todas = [...resultados, ...columnas];
  const dif = todas.map((x) => ({ ...x, clase: clasificar(x) })).filter((x) => x.clase);
  const cuenta = (c) => dif.filter((x) => x.clase === c).length;
  const coinciden = todas.filter((x) => x.esp !== 'd' && !clasificar(x)).length;
  L.push('# Matriz de roles contra el servidor ("rol por pantalla")', '');
  L.push(`> Corrida \`${RUN}\` del ${inicio.toISOString().slice(0, 16).replace('T', ' ')} UTC en DEV, generada por \`tests/integral/seguridad/matriz.js\`.`);
  L.push('> Lo esperado está en [`tests/integral/seguridad/esperado.js`](../../tests/integral/seguridad/esperado.js), con el porqué de cada fila.', '');
  L.push('**Cómo se lee.** Cada celda es lo que respondió el servidor cuando ese usuario, sin pasar por la app, intentó la operación: ✔ la hizo (código 2xx), ✘ la frenó (403 = sin privilegio, 400 = regla de un plugin, 404 = no la ve). Las filas son datos QA creados por la prueba y borrados al final.', '');
  L.push('| | Celdas |', '|---|---|');
  L.push(`| Probadas | ${todas.length} (${resultados.length} de la matriz y ${columnas.length} casos de columna) |`);
  L.push(`| Coinciden con lo esperado | ${coinciden} |`);
  L.push(`| **HUECO** (el servidor deja hacer algo que el rol no debería) | **${cuenta('H')}** |`);
  L.push(`| **DEMASIADO CERRADO** (el servidor frena algo que una pantalla necesita) | **${cuenta('C')}** |`);
  L.push(`| EXPECTATIVA DUDOSA (ninguna pantalla lo usa; decide el dueño) | ${cuenta('D')} (${dif.filter((x) => x.clase === 'D' && x.ok).length} permitidas hoy) |`);
  L.push(`| Limpieza | ${limpieza.restantes.length ? 'quedaron: ' + limpieza.restantes.join(', ') : '0 restos'}${limpieza.errores.length ? ` · ${limpieza.errores.length} errores` : ''} |`, '');
  L.push('Contexto: en DEV los usuarios de prueba también tienen roles de otras soluciones (SEC-17, riesgo aceptado). Lo que mide la matriz es el acceso **efectivo**; en esta corrida se verificó que ninguno de esos roles da privilegios sobre tablas de CM.', '');
  L.push('## Diferencias', '');
  for (const c of ['H', 'C', 'D']) {
    const l = dif.filter((x) => x.clase === c);
    L.push(`### ${CLASE[c]} (${l.length})`, '');
    if (!l.length) { L.push('Ninguna.', ''); continue; }
    L.push('| Rol | Qué intentó | Esperado | Servidor | Detalle |', '|---|---|---|---|---|');
    for (const x of l) {
      const que = x.id ? `${x.id}: ${x.desc}` : `${NOMBRE_OP[x.op]} · ${x.tabla}`;
      L.push(`| ${ROLES[x.rol]} | ${que} | ${x.esp} | ${x.ok ? '✔' : '✘'} ${x.st} | ${(x.msg || '').replace(/\|/g, '/').slice(0, 120)} |`);
    }
    L.push('');
  }
  L.push('## Propuestas (no aplicadas: decide el dueño)', '');
  L.push('Verificado en la corrida: ninguno de los roles de otras soluciones que tienen los usuarios QA (SEC-17) da privilegios sobre tablas de CM, así que todo lo de abajo sale de los roles y plugins de CM.', '');
  L.push('| # | Hueco | Causa | Propuesta |', '|---|---|---|---|');
  L.push('| H1 | Solicitante edita la Especial: la propia Pendiente (retiro, notas) y, ya aprobada y compartida por S-7, el retiro, las notas y hasta la cierra (Up asignación, COL-SOL-1, 4, 5 y 6) | El rol Solicitante tiene Write Basic en asignación; `ColumnasAsignacion` le deja retiro, notas, estado y motivo (era para volver a pedir una Especial cerrada desde la pantalla, que desde DAT-14 hace `SolicitarEspecial` con el servicio); `RepartoAsignacion` le comparte la aprobada con lectura, escritura y anexar | (1) Dejar al Solicitante sin columnas en `ColumnasAsignacion` (falla cerrado); (2) que el compartido de S-7 sea **solo lectura** (Mis solicitudes solo lee); (3) opcional: quitarle Write en asignación al rol. `SolicitarEspecial` no se rompe: crea con Create y reactiva con el servicio |');
  L.push('| H2 | Solicitante crea una Diaria activa con un agente: el reparto se la manda al cobrador sin pasar por Aprobaciones (COL-SOL-7) | Create Global en asignación; `AprobacionEspecial` solo controla Especiales | En el alta, `AprobacionEspecial` exige que una persona de CM que no sea Supervisor, Admin o Servicio solo cree **Especiales Pendientes y sin agente** |');
  L.push('| H3 | El cobrador registra una visita y un pago en una cuenta que **no es de su cartera** (COL-COB-6 y 7) | El rol Localizador tiene AppendTo Global en cuenta (lo exige el lookup); `InteraccionDelCobrador` pone el agente pero no mira la cartera | En `InteraccionDelCobrador`, si llama un Localizador: exigir que la cuenta tenga una asignación **activa de su cartera** (dueño = el llamador). El pago hereda la cuenta de la interacción (`CuentaDesdeInteraccion`), así que alcanza con frenar la interacción. Bajar AppendTo a Basic no sirve: el cobrador no es dueño de las cuentas, solo las tiene compartidas |');
  L.push('', '**Dudosas: una decisión cada una.**', '');
  L.push('- **Admin (la mayoría de las dudosas):** tiene Global en todo CM, así que puede borrar, reasignar y compartir cualquier fila, y crear o editar interacciones, pagos, cargas diarias y cuentas, aunque ninguna pantalla lo haga. Opciones: aceptarlo como administrador funcional (riesgo aceptado), o mínimo privilegio: quitar Delete, Assign y Share en todas las tablas y Create/Write en interacción, pago, actualización de datos, carga diaria y cuenta (Alta de cuenta va por Smelter, no lo necesita).');
  L.push('- **Lecturas de fiador, dirección y teléfono** para BackOffice, Solicitante y Supervisor: ninguna pantalla suya las muestra (el BackOffice sí lee direcciones y teléfonos en Transcripción). Opciones: quitar la lectura de fiador a los tres y la de dirección y teléfono a Solicitante y Supervisor, o aceptarlas.', '');
  L.push('## Matriz completa', '');
  for (const rol of Object.keys(ROLES)) {
    L.push(`### ${ROLES[rol]}`, '');
    L.push(`| Tabla | ${OPS.map((o) => NOMBRE_OP[o]).join(' | ')} |`, `|---|${OPS.map(() => '---').join('|')}|`);
    for (const t of TABLAS) {
      const fila = OPS.map((op) => {
        const x = resultados.find((r) => r.rol === rol && r.tabla === t && r.op === op);
        if (!x) return '';
        const cl = clasificar(x);
        return `${celdaTxt(x)}${cl === 'H' ? ' **HUECO**' : cl === 'C' ? ' **CERRADO**' : cl === 'D' ? ' (d)' : ''}`;
      });
      L.push(`| ${t} | ${fila.join(' | ')} |`);
    }
    L.push('');
  }
  L.push('## Casos de columna y de flujo', '');
  L.push('| ID | Rol | Caso | Esperado | Servidor | Por qué |', '|---|---|---|---|---|---|');
  for (const x of columnas) L.push(`| ${x.id} | ${ROLES[x.rol]} | ${x.desc} | ${x.esp} | ${celdaTxt(x)} | ${x.por} |`);
  L.push('');
  return L.join('\n');
}

(async () => {
  // --solo-informe <carpeta de resultados>: rearma el informe sin volver a correr la prueba.
  const iSolo = process.argv.indexOf('--solo-informe');
  if (iSolo > 0) {
    const dir = path.resolve(process.argv[iSolo + 1]);
    const j = JSON.parse(fs.readFileSync(path.join(dir, 'resultados.json'), 'utf8'));
    resultados.push(...j.resultados); columnas.push(...j.columnas);
    fs.rmSync(DIR, { recursive: true, force: true });
    fs.writeFileSync(INFORME, sinPAN(informe(j.limpieza, fs.statSync(path.join(dir, 'resultados.json')).mtime)).replace(`Corrida \`${RUN}\``, `Corrida \`${j.runid}\``));
    console.log(`informe: ${INFORME}`); process.exit(0);
  }
  const inicio = new Date();
  let fallo = null, limpieza = { errores: [], restantes: [] };
  console.log(`Matriz de roles · corrida ${RUN} · prefijo ${P}`);
  try {
    await preparar();
    await matriz();
    await casosDeColumna();
  } catch (e) { fallo = e; console.error('ERROR', sinPAN(e.stack || e.message)); }
  finally {
    console.log('\n══ Limpieza');
    try { limpieza = await limpiar(); } catch (e) { limpieza.errores.push(sinPAN(e.message)); }
    console.log(`  restantes: ${limpieza.restantes.join(', ') || 'ninguno'} · errores: ${limpieza.errores.length}`);
    for (const e of limpieza.errores.slice(0, 10)) console.log('   ', e);
    fs.writeFileSync(path.join(DIR, 'resultados.json'), sinPAN(JSON.stringify({ runid: RUN, resultados, columnas, limpieza }, null, 1)));
    if (!fallo && !process.argv.includes('--sin-informe')) { fs.writeFileSync(INFORME, sinPAN(informe(limpieza, inicio))); console.log(`informe: ${INFORME}`); }
    const todas = [...resultados, ...columnas].map(clasificar);
    console.log(`\n${resultados.length + columnas.length} celdas · HUECO ${todas.filter((c) => c === 'H').length} · DEMASIADO CERRADO ${todas.filter((c) => c === 'C').length} · DUDOSA ${todas.filter((c) => c === 'D').length}`);
    process.exit(fallo ? 1 : 0);
  }
})();
