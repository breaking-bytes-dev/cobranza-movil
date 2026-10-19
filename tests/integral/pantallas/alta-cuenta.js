// DAT-20 y DAT-14 por la UI del backoffice, con datos QA y PAN FICTICIO (BIN 9999, solo en memoria):
//   1) User4 (Admin): pantalla "Alta de cuenta" → prueba en seco → carga real de una Especial (Custom API AltaCuenta + Smelter).
//   2) User2 (Solicitante): Nueva solicitud sobre esa cuenta → "ya existe" (la Especial de la carga) sin crear otra; se cierra por
//      API y la vuelve a pedir → "reactivada" a su nombre (Custom API SolicitarEspecial).
//   3) User1 (BackOffice) y User3 (Supervisor): la nav no muestra "Alta de cuenta".
// Un solo Chromium a la vez (cada app se cierra antes de abrir la siguiente). Nunca imprime un PAN: toda salida pasa por sinPAN.
// Al final, limpieza con el prefijo QA de la suite (cliente, cuenta, asignación, bóveda, jobs, producto, fiador).
// Uso (desde tests/integral): NODE_PATH=../backoffice-canvas/node_modules node pantallas/alta-cuenta.js
const path = require('path');
process.chdir(path.join(__dirname, '..'));
const dv = require('../../backoffice-canvas/lib/dataverse');
const { PREFIJO, AGENTE, U } = require('../lib/ctx');
const csv = require('../lib/csv');
const datos = require('../lib/datos');
const ui = require('../lib/ui');
const { pl, c } = ui;

const sinPAN = (s) => String(s).replace(/\d{13,19}/g, '[PAN]');
const dormir = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, mal = 0;
const chk = (id, cond, txt) => { cond ? ok++ : mal++; console.log(`${cond ? '✓' : '✗'} ${id}  ${sinPAN(txt)}`); };

const runid = 'ALU' + Date.now().toString(36).slice(-4).toUpperCase();
const CIF = `${PREFIJO}-${runid}`, NOMBRE = `${PREFIJO} ${runid}`;
// Campos opcionales de la opción (b) (2026-10-19): ubican al cliente en la calle. El teléfono tiene 8 dígitos (nunca 13 o más).
const EXTRA = { dirTrab: `${PREFIJO} ${runid} oficina`, depto: `${PREFIJO} Managua`, distrito: `${PREFIJO} D-1`, telCasa: '22501234', dirFiador: `${PREFIJO} ${runid} casa del fiador` };
const ESTADO_FINAL = /Prueba en seco limpia|Alta hecha|marcó la fila con error|Falló/;

// Lee lblEstadoAlt hasta que diga algo final (o vence el plazo). Smelter tarda entre 30 s y unos minutos.
async function esperarEstado(app, re, maxMs = 420000) {
  const t0 = Date.now(); let t = '';
  while (Date.now() - t0 < maxMs) {
    t = await pl.texto(app, 'lblEstadoAlt', { timeout: 5000 }).catch(() => '');
    if (re.test(t)) return t;
    await dormir(4000);
  }
  return t;
}
const valorInput = async (app, nombre) => (await pl.visible(app, nombre)).locator('input, textarea').first().inputValue();
const deshabilitado = async (app, nombre) => (await pl.visible(app, nombre)).locator('input, textarea').first().isDisabled();
const jobsDelCif = async () => (await dv.get(`sanic_tbl_smlt_importjobs?$select=sanic_smlt_ij_name,sanic_smlt_ij_status,sanic_smlt_ij_isdryrun,sanic_smlt_ij_failedcount&$filter=contains(sanic_smlt_ij_name,'${CIF}')&$orderby=createdon asc`)).value;
const cuentasDelCif = async () => (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid,sanic_cm_ct_ultimos4,sanic_cm_ct_numerodecuenta&$filter=sanic_cm_ct_cifcliente eq '${CIF}'`)).value;
const especialesDe = async (id) => (await dv.get(`sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,_sanic_cm_as_agenteid_value,_sanic_cm_as_solicitadopor_value,_ownerid_value&$filter=_sanic_cm_as_cuentaid_value eq ${id} and sanic_cm_as_tipo eq 3`)).value;

async function altaPorPantalla(prodCodigo, pan) {
  const app = await ui.abrirBackoffice('user4', 'ALTA');
  try {
    const nav = await pl.itemsNav(app);
    chk('ALTUI-01', nav.includes('Alta de cuenta') && nav.indexOf('Alta de cuenta') === nav.indexOf('Cargar archivo') + 1,
      `Admin (User4): la nav muestra "Alta de cuenta" después de "Cargar archivo" (${nav.join(' · ')})`);
    await ui.ir(app, 'Alta de cuenta', 'scrAltaCuenta');
    await pl.escribir(app, 'txtCifAlt', CIF);
    await pl.escribir(app, 'txtCliAlt', NOMBRE);
    await pl.escribir(app, 'txtPanAlt', pan);
    const tipoPan = await (await pl.visible(app, 'txtPanAlt')).locator('input').first().getAttribute('type');
    chk('ALTUI-02', tipoPan === 'password', `el número de tarjeta se escribe enmascarado (input type="${tipoPan}")`);
    let r = await c.elegirEnBuscador(app, { buscador: 'txtBuscarProdAlt', galeria: 'galProdAlt', buscar: prodCodigo, opcion: prodCodigo, cambiar: 'btnCambiarProdAlt' });
    if (!r.ok) throw new Error(`no se pudo elegir el producto QA (filas: ${r.textos.slice(0, 4).join(' / ')})`);
    r = await c.elegirEnBuscador(app, { buscador: 'txtBuscarAgeAlt', galeria: 'galAgeAlt', buscar: AGENTE, opcion: AGENTE, cambiar: 'btnCambiarAgeAlt' });
    if (!r.ok) throw new Error(`no se pudo elegir el agente QA (filas: ${r.textos.slice(0, 4).join(' / ')})`);
    for (const [n, v] of [['txtSvNioAlt', '900'], ['txtSvUsdAlt', '0'], ['txtTotalAlt', '900'], ['txtDiasAlt', '30'], ['txtNivelAlt', '1'],
      ['txtFPagoAlt', '2026-10-20'], ['txtFiadorAlt', `${NOMBRE} Fiador`], ['txtDirFiadorAlt', EXTRA.dirFiador], ['txtTelCasaAlt', EXTRA.telCasa], ['txtDirTrabAlt', EXTRA.dirTrab],
      ['txtDeptoAlt', EXTRA.depto], ['txtDistritoAlt', EXTRA.distrito], ['txtNotaAlt', `${PREFIJO} alta por pantalla`], ['txtRetNioAlt', '300']]) await pl.escribir(app, n, v);
    await pl.esperar(app, 1500);
    const falta = await pl.texto(app, 'lblFaltaAlt');
    const aviso = await pl.texto(app, 'lblTipoAvisoAlt');
    chk('ALTUI-03', /Datos completos/.test(falta) && /Especial/.test(aviso), `formulario completo ("${falta}"), tipo por defecto Especial ("${aviso.slice(0, 80)}")`);

    // Prueba en seco
    await (await pl.boton(app, 'btnValidarAlt')).click();
    let est = await esperarEstado(app, ESTADO_FINAL);
    let jobs = await jobsDelCif();
    chk('ALTUI-04', /Prueba en seco limpia/.test(est) && jobs.length === 1 && jobs[0].sanic_smlt_ij_isdryrun === true && jobs[0].sanic_smlt_ij_status === 2,
      `prueba en seco: "${est}" · job "${jobs[0]?.sanic_smlt_ij_name}" estado ${jobs[0]?.sanic_smlt_ij_status}`);
    chk('ALTUI-05', (await cuentasDelCif()).length === 0, 'la prueba en seco no creó la cuenta');
    const avisos = await pl.texto(app, 'lblAvisosAlt').catch(() => '');
    chk('ALTUI-06', /Especial/.test(avisos) && !/\d{13,19}/.test(avisos), `avisos del servidor a la vista: "${avisos.replace(/\n/g, ' | ').slice(0, 200)}"`);
    chk('ALTUI-07', await deshabilitado(app, 'txtCifAlt') && await pl.botonHabilitado(app, 'btnCargarAlt') && !(await pl.botonHabilitado(app, 'btnValidarAlt')),
      'tras la prueba limpia: formulario bloqueado, «Cargar de verdad» habilitado y «Validar» deshabilitado');
    if (!/Prueba en seco limpia/.test(est)) throw new Error('la prueba en seco no salió limpia');

    // Carga real
    await (await pl.boton(app, 'btnCargarAlt')).click();
    await (await pl.boton(app, 'btnConfirmarAlt')).click();
    est = await esperarEstado(app, /Alta hecha|marcó la fila con error|Falló/);
    jobs = await jobsDelCif();
    chk('ALTUI-08', /Alta hecha: Especial/.test(est) && /Pendiente de aprobación/.test(est) && jobs.length === 2 && jobs[1].sanic_smlt_ij_isdryrun === false && jobs[1].sanic_smlt_ij_status === 2,
      `carga real: "${est}" · job "${jobs[1]?.sanic_smlt_ij_name}" estado ${jobs[1]?.sanic_smlt_ij_status}`);
    await pl.esperar(app, 2000);
    const panQueda = await valorInput(app, 'txtPanAlt'), cifQueda = await valorInput(app, 'txtCifAlt');
    chk('ALTUI-09', panQueda === '' && cifQueda === '', `formulario limpio después del alta (número de tarjeta vacío: ${panQueda === ''}, CIF vacío: ${cifQueda === ''})`);
    await ui.captura({ dir: path.join(__dirname, '..', 'resultados', `pantallas-${runid}`), faseActual: 'ALTA' }, app, 'alta-hecha');
  } finally { await ui.cerrar(app); }
}

async function solicitudes(cta, especialId) {
  const app = await ui.abrirBackoffice('user2', 'SOL');
  try {
    const nav = await pl.itemsNav(app);
    chk('ALTUI-12', !nav.includes('Alta de cuenta'), `Solicitante (User2): la nav no muestra "Alta de cuenta" (${nav.join(' · ')})`);
    const pedir = async (motivo) => {
      await ui.ir(app, 'Nueva solicitud', 'scrNuevaSolicitud');
      let r;
      for (let i = 0; i < 3; i++) {
        r = await c.elegirEnBuscador(app, { buscador: 'txtBuscarCtaNva', galeria: 'galCtaNva', buscar: cta.sanic_cm_ct_ultimos4, opcion: NOMBRE, cambiar: 'btnCambiarCtaNva' });
        if (r.ok) break; await pl.esperar(app, 3000);
      }
      if (!r.ok) throw new Error(`no se pudo elegir la cuenta QA (filas: ${r.textos.slice(0, 4).join(' / ')})`);
      await c.elegirCombo(app, 'cmbTipoNva', { opcion: 'Retiro urgente' });
      await pl.escribir(app, 'txtRetiroNioNva', '150');
      await pl.escribir(app, 'txtMotivoNva', motivo);
      await pl.esperar(app, 1000);
      await (await pl.boton(app, 'btnEnviarNva')).click();
      const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe|No se pudo/, 30000);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      return n || '';
    };
    // a) la cuenta ya tiene la Especial de la carga (Pendiente): no se crea otra y se dice quién la pidió
    let n = await pedir(`${NOMBRE} pedido 1`);
    let esp = await especialesDe(cta.sanic_cm_tbl_cuentaid);
    chk('ALTUI-13', /Ya existe una Especial ACTIVA/.test(n) && /pendiente de aprobación/.test(n) && /pedida por/.test(n) && esp.length === 1,
      `Solicitante pide la misma: aviso "${n}" · Especiales de la cuenta: ${esp.length}`);
    // b) se cierra (como lo haría Rechazar) y la vuelve a pedir: se reactiva a su nombre
    await dv.patch('sanic_cm_tbl_asignacions', especialId, { sanic_cm_as_estado: false, sanic_cm_as_motivo: 'Rechazada', sanic_cm_as_estadoaprobacion: 4 });
    n = await pedir(`${NOMBRE} pedido 2`);
    esp = await especialesDe(cta.sanic_cm_tbl_cuentaid);
    const e = esp[0] || {};
    chk('ALTUI-14', /se reactivó/.test(n) && esp.length === 1 && e.sanic_cm_as_estado === true && e.sanic_cm_as_estadoaprobacion === 2 && e._ownerid_value === U.sol && e._sanic_cm_as_solicitadopor_value === U.sol,
      `cerrada y vuelta a pedir: aviso "${n}" · activa ${e.sanic_cm_as_estado}, estado ${e.sanic_cm_as_estadoaprobacion}, dueño y solicitante User2: ${e._ownerid_value === U.sol && e._sanic_cm_as_solicitadopor_value === U.sol}`);
  } finally { await ui.cerrar(app); }
}

async function navSinAlta(usuario, rol, id) {
  const app = await ui.abrirBackoffice(usuario, rol);
  try {
    const nav = await pl.itemsNav(app);
    chk(id, nav.length > 0 && !nav.includes('Alta de cuenta'), `${rol} (${usuario}): la nav no muestra "Alta de cuenta" (${nav.join(' · ')})`);
  } finally { await ui.cerrar(app); }
}

(async () => {
  const agente = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  if (!agente) throw new Error('falta el agente QA de la suite');
  const prodCodigo = `${PREFIJO}-${runid.slice(-4)}`;
  await dv.create('sanic_cm_tbl_productos', { sanic_cm_pr_codigo: prodCodigo, sanic_cm_pr_tipo: `${PREFIJO} Producto` });
  const pan = csv.panFicticio();
  console.log(`Alta por pantalla · corrida ${runid} · CIF ${CIF}`);
  try {
    await altaPorPantalla(prodCodigo, pan);
    const cts = await cuentasDelCif();
    const cta = cts[0];
    chk('ALTUI-10', cts.length === 1 && cta.sanic_cm_ct_ultimos4 === pan.slice(-4) && !/\d{13,19}/.test(cta.sanic_cm_ct_numerodecuenta || ''),
      `Dataverse: 1 cuenta del CIF, últimos 4 correctos, sin el número completo ("${cta?.sanic_cm_ct_numerodecuenta || ''}")`);
    if (!cta) throw new Error('la carga real no creó la cuenta');
    const cli = (await dv.get(`sanic_cm_tbl_clientes?$select=sanic_cm_cl_departamento,sanic_cm_cl_distrito&$filter=sanic_cm_cl_cif eq '${CIF}'`)).value[0] || {};
    const dirs = (await dv.get(`sanic_cm_tbl_direccions?$select=sanic_cm_dr_direccion,sanic_cm_dr_tipo&$filter=sanic_cm_dr_cif eq '${CIF}'`)).value;
    const tels = (await dv.get(`sanic_cm_tbl_telefonos?$select=sanic_cm_tl_numero,sanic_cm_tl_tipo&$filter=sanic_cm_tl_cif eq '${CIF}'`)).value;
    const fia = (await dv.get(`sanic_cm_tbl_fiadors?$select=sanic_cm_fi_direccion&$filter=sanic_cm_fi_nombre eq '${NOMBRE} Fiador'`)).value[0] || {};
    const dirTrab = dirs.find((d) => d.sanic_cm_dr_direccion === EXTRA.dirTrab), telCasa = tels.find((x) => x.sanic_cm_tl_numero === EXTRA.telCasa);
    chk('ALTUI-10b', cli.sanic_cm_cl_departamento === EXTRA.depto && cli.sanic_cm_cl_distrito === EXTRA.distrito && !!dirTrab && !!telCasa && fia.sanic_cm_fi_direccion === EXTRA.dirFiador,
      `Dataverse: departamento y distrito del cliente ${cli.sanic_cm_cl_departamento === EXTRA.depto && cli.sanic_cm_cl_distrito === EXTRA.distrito}, `
      + `dirección del trabajo ${!!dirTrab} (tipo ${dirTrab?.sanic_cm_dr_tipo}), teléfono de la casa ${!!telCasa} (tipo ${telCasa?.sanic_cm_tl_tipo}), dirección del fiador ${fia.sanic_cm_fi_direccion === EXTRA.dirFiador}`);
    const esp = await especialesDe(cta.sanic_cm_tbl_cuentaid);
    chk('ALTUI-11', esp.length === 1 && esp[0].sanic_cm_as_estado === true && esp[0].sanic_cm_as_estadoaprobacion === 2 && esp[0]._sanic_cm_as_agenteid_value === agente.sanic_cm_tbl_agenteid,
      `Dataverse: Especial activa, Pendiente (2), con el localizador elegido (${esp.length} fila)`);
    await solicitudes(cta, esp[0].sanic_cm_tbl_asignacionid);
    await navSinAlta('user1', 'BackOffice', 'ALTUI-15');
    await navSinAlta('user3', 'Supervisor', 'ALTUI-16');
  } catch (e) {
    chk('ALTUI-ERR', false, `la prueba se cortó: ${e.message}`);
  } finally {
    const l = await datos.limpiar();
    const quedan = Object.entries(l.restantes || {}).filter(([, n]) => n > 0);
    chk('ALTUI-17', quedan.length === 0 && !(l.errores || []).length, `limpieza: nada con el prefijo queda (${quedan.map(([k, n]) => `${k}:${n}`).join(', ') || 'ok'})`);
    console.log(`\n${ok}/${ok + mal} ✓ · ${mal} ✗`);
    process.exit(mal ? 1 : 0);
  }
})().catch((e) => { console.error('ERROR', sinPAN(e.message || e)); process.exit(1); });
