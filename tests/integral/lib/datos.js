// Acceso a datos de la corrida: inventario por prefijo, conteos, lectura suplantando a User5 y LIMPIEZA (F11).
// REGLA de borrado: solo se borra lo que (a) cuelga del cliente/cuentas con prefijo QA-E2E-INT, (b) lleva el prefijo en su
// propio texto (comentario, recibo, nombre del job, código de producto, nombre de fiador) o (c) quedó en el registro de
// la corrida. Nunca por otro criterio: así no hay forma de rozar QA-R1 / IN-1065, AS-2077, AS-2134 ni el agente.
const fs = require('fs');
const path = require('path');
const dv = require('../../backoffice-canvas/lib/dataverse');
const { PREFIJO, ES, RAIZ, sinPAN } = require('./ctx');

const sel = (tipo) => ES[tipo][1];
const ids = async (tipo, filtro) => (await dv.getAll(`${ES[tipo][0]}?$select=${sel(tipo)}&$filter=${encodeURIComponent(filtro)}`)).map((x) => x[sel(tipo)]);
const uniq = (a) => [...new Set(a)];
const orEq = (campo, lista) => lista.map((id) => `${campo} eq ${id}`).join(' or ');
const porLista = async (tipo, campo, lista) => (lista.length ? ids(tipo, orEq(campo, lista)) : []);
const comienza = (campo) => `startswith(${campo},'${PREFIJO}')`;

// Todo lo de la corrida (y de corridas anteriores) que lleva el prefijo, agrupado por tabla.
async function inventario(reg = {}) {
  const deReg = (t) => reg[t] || [];
  const cliente = uniq([...(await ids('cliente', comienza('sanic_cm_cl_cif'))), ...deReg('cliente')]);
  const cuenta = uniq([...(await ids('cuenta', comienza('sanic_cm_ct_cifcliente'))), ...(await porLista('cuenta', '_sanic_cm_ct_clienteid_value', cliente)), ...deReg('cuenta')]);
  const interaccion = uniq([...(await ids('interaccion', comienza('sanic_cm_in_comentario'))), ...(await porLista('interaccion', '_sanic_cm_in_cuentaid_value', cuenta)), ...deReg('interaccion')]);
  const pago = uniq([...(await ids('pago', comienza('sanic_cm_pg_numerorecibo'))), ...(await porLista('pago', '_sanic_cm_pg_cuentaid_value', cuenta)), ...(await porLista('pago', '_sanic_cm_pg_interaccion_value', interaccion)), ...deReg('pago')]);
  const actualizacion = uniq([...(await porLista('actualizacion', '_sanic_cm_ad_interaccionid_value', interaccion)), ...(await porLista('actualizacion', '_sanic_cm_ad_cuentaid_value', cuenta)), ...deReg('actualizacion')]);
  const asignacion = uniq([...(await porLista('asignacion', '_sanic_cm_as_cuentaid_value', cuenta)), ...deReg('asignacion')]);
  const carga = uniq([...(await porLista('carga', '_sanic_cm_cd_cuenta_value', cuenta)), ...(await porLista('carga', '_sanic_cm_cd_clienteid_value', cliente)), ...deReg('carga')]);
  const boveda = uniq([...(await porLista('boveda', '_sanic_cm_cp_cuentaid_value', cuenta)), ...deReg('boveda')]);
  const direccion = uniq([...(await ids('direccion', comienza('sanic_cm_dr_cif'))), ...deReg('direccion')]);
  const telefono = uniq([...(await ids('telefono', comienza('sanic_cm_tl_cif'))), ...deReg('telefono')]);
  const producto = uniq([...(await ids('producto', comienza('sanic_cm_pr_codigo'))), ...deReg('producto')]);
  const fiador = uniq([...(await ids('fiador', comienza('sanic_cm_fi_nombre'))), ...deReg('fiador')]);
  // Los jobs creados desde la pantalla "Cargar archivo" se llaman "Carga <fecha> · <archivo>.csv · prueba|real": llevan el prefijo en el
  // NOMBRE DEL ARCHIVO (QA-E2E-INT-…), no al comienzo del nombre del job.
  const job = uniq([...(await ids('job', comienza('sanic_smlt_ij_name'))), ...(await ids('job', `contains(sanic_smlt_ij_name,'${PREFIJO}')`)), ...deReg('job')]);
  return { interaccion, pago, actualizacion, asignacion, carga, boveda, cuenta, direccion, telefono, cliente, producto, fiador, job };
}

// El registro de TODAS las corridas previas (por si una se cayó sin limpiar).
function registrosPrevios() {
  const out = {};
  try {
    for (const d of fs.readdirSync(RAIZ)) {
      const p = path.join(RAIZ, d, 'registro.json');
      if (!fs.existsSync(p)) continue;
      for (const it of JSON.parse(fs.readFileSync(p, 'utf8')).items || []) (out[it.tipo] = out[it.tipo] || []).push(it.id);
    }
  } catch (e) { /* sin corridas previas */ }
  return out;
}

const existe = async (tipo, id) => {
  try { await dv.get(`${ES[tipo][0]}(${id})?$select=${sel(tipo)}`); return true; }
  catch (e) { if (e.status === 404) return false; throw e; }
};

// Borra en orden de dependencias y verifica que no quede nada con el prefijo. Devuelve { borrados, errores, restantes }.
async function limpiar(extraReg) {
  const reg = { ...registrosPrevios() };
  for (const [k, l] of Object.entries(extraReg || {})) reg[k] = [...(reg[k] || []), ...l];
  const inv = await inventario(reg);
  const borrados = {}, errores = [];
  // Pagos y actualizaciones antes que su interacción (DAT-18: interacción→pago/actualización es Delete = Restrict), luego lo que
  // cuelga de cuenta/cliente (la carga diaria antes que el cliente, también Restrict), el cliente y los maestros.
  const orden = ['pago', 'actualizacion', 'interaccion', 'asignacion', 'carga', 'boveda', 'cuenta', 'direccion', 'telefono', 'cliente', 'producto', 'fiador'];
  for (const tipo of orden) {
    borrados[tipo] = 0;
    for (const id of inv[tipo]) {
      try { const st = await dv.del(ES[tipo][0], id); if (st !== 404) borrados[tipo]++; }
      catch (e) { errores.push(`${tipo} ${id}: ${sinPAN(e.message).slice(0, 200)}`); }
    }
  }
  // Import jobs (con su archivo y, si la cascada no los borra, sus logs).
  borrados.job = 0;
  for (const id of inv.job) {
    try {
      const st = await dv.del(ES.job[0], id); if (st !== 404) borrados.job++;
      for (const l of await dv.getAll(`sanic_tbl_smlt_importjoblogs?$select=sanic_tbl_smlt_importjoblogid&$filter=_sanic_smlt_ijl_importjobid_value eq ${id}`)) await dv.del('sanic_tbl_smlt_importjoblogs', l.sanic_tbl_smlt_importjoblogid);
    } catch (e) { errores.push(`job ${id}: ${sinPAN(e.message).slice(0, 200)}`); }
  }
  // CSV temporales de cualquier corrida.
  let csv = 0;
  try { for (const d of fs.readdirSync(RAIZ)) { const dir = path.join(RAIZ, d); if (!fs.statSync(dir).isDirectory()) continue; for (const f of fs.readdirSync(dir)) if (/\.csv$/i.test(f)) { fs.unlinkSync(path.join(dir, f)); csv++; } } } catch (e) { /* nada */ }
  borrados.csv = csv;
  // Verificación: el barrido por prefijo tiene que dar cero y los ids ya no existen.
  const resto = await inventario(reg);
  const restantes = {};
  for (const [tipo, lista] of Object.entries(resto)) {
    let vivos = 0;
    for (const id of lista) if (await existe(tipo, id)) vivos++;
    restantes[tipo] = vivos;
  }
  return { borrados, errores, restantes };
}

// Conteos de la carga (para idempotencia): por cliente/cuentas de la corrida.
async function conteosCarga(ctx) {
  const cli = await ids('cliente', `sanic_cm_cl_cif eq '${ctx.cif}'`);
  const cuentas = await porLista('cuenta', '_sanic_cm_ct_clienteid_value', cli);
  return {
    clientes: cli.length, cuentas: cuentas.length,
    boveda: (await porLista('boveda', '_sanic_cm_cp_cuentaid_value', cuentas)).length,
    asignaciones: (await porLista('asignacion', '_sanic_cm_as_cuentaid_value', cuentas)).length,
    cargas: (await porLista('carga', '_sanic_cm_cd_cuenta_value', cuentas)).length,
    direcciones: (await ids('direccion', `sanic_cm_dr_cif eq '${ctx.cif}'`)).length,
    telefonos: (await ids('telefono', `sanic_cm_tl_cif eq '${ctx.cif}'`)).length,
    productos: (await ids('producto', comienza('sanic_cm_pr_codigo'))).length,
    fiadores: (await ids('fiador', comienza('sanic_cm_fi_nombre'))).length,
  };
}

// Lectura suplantando a un usuario (cabecera MSCRMCallerID). Devuelve { status, json }; 403/404 NO lanzan.
async function leerComo(userId, rel) {
  try { const r = await dv.raw('GET', rel, undefined, { MSCRMCallerID: userId }); return { status: r.status, json: r.json }; }
  catch (e) { if (e.status === 403 || e.status === 404) return { status: e.status, json: null, error: e.message }; throw e; }
}
async function getAllComo(userId, rel) {
  let out = [], next = rel;
  while (next) {
    const r = await dv.raw('GET', next, undefined, { MSCRMCallerID: userId, Prefer: 'odata.include-annotations="*",odata.maxpagesize=5000' });
    out = out.concat(r.json.value); next = r.json['@odata.nextLink'];
  }
  return out;
}

// Diagnóstico de acceso a una cuenta: dueño y con quién está compartida (para entender un 200/403 inesperado).
async function diagnosticoCuenta(id) {
  const t = encodeURIComponent(JSON.stringify({ '@odata.id': `sanic_cm_tbl_cuentas(${id})` }));
  const r = await dv.get(`RetrieveSharedPrincipalsAndAccess(Target=@t)?@t=${t}`);
  const own = (await dv.get(`sanic_cm_tbl_cuentas(${id})?$select=_ownerid_value`))._ownerid_value;
  const lista = (r.PrincipalAccesses || []).map((p) => `${(p.Principal['@odata.type'] || '').split('.').pop()}:${p.Principal.systemuserid || p.Principal.teamid || p.Principal.ownerid || '?'}=${p.AccessMask}`);
  let efectivo = '?';
  try { efectivo = (await dv.get(`systemusers(${require('./ctx').USER5})/Microsoft.Dynamics.CRM.RetrievePrincipalAccess(Target=@t)?@t=${t}`)).AccessRights; }
  catch (e) { efectivo = `error ${e.message.slice(-80)}`; }
  return `dueño ${own} · compartida con [${lista.join(', ')}] · acceso efectivo de User5: ${efectivo}`;
}

module.exports = { diagnosticoCuenta, inventario, limpiar, conteosCarga, leerComo, getAllComo, ids, porLista, orEq, existe };
