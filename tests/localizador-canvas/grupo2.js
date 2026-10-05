// Pruebas web (Playwright, User5, DEV) de los hallazgos del cobrador que NO necesitan el teléfono:
// APP-07, 15, 16, 17, 18, 19, 24, 25 (parcial), 27, 30 (parcial), 32, formato numérico y la vista de pagos de APP-05.
// El web player corre la app EN LÍNEA: lo offline (sincronización, N° de interacción vacío) se verifica en el teléfono.
// Datos: solo las carteras QA-E2E-LOC de User5. Crea 1 interacción QA (APP-27). Nunca imprime números de tarjeta.
// Uso: NODE_PATH=../backoffice-canvas/node_modules node grupo2.js
const a = require('./lib/app');
const dv = require('../backoffice-canvas/lib/dataverse.js');

const USER5 = '539834c4-6116-f111-8342-000d3a316609';
const res = [];
const ok = (id, cond, detalle) => { res.push({ id, ok: !!cond, detalle }); console.log(cond ? '✓' : '✗', id.padEnd(10), detalle); };
const txt = async (app, c) => a.texto(app, c).catch(() => '');
const porTexto = (app, t) => app.frame.getByText(t, { exact: false }).locator('visible=true').first();
// El texto está debajo del botón transparente de la tarjeta: el clic (force) cae en ese botón, que es el que se quiere tocar.
async function clicTexto(app, t) { await porTexto(app, t).click({ timeout: 15000, force: true }); await app.red.quieta(app.page, { quietoMs: 2500, maxMs: 30000, minMs: 2500 }); }
async function color(app, c, sel = '') {
  const el = (await a.visible(app, c)).locator(sel || ':scope').first();
  // El texto de un botón clásico vive en .appmagic-button-label (el <button> no tiene el color del texto).
  return el.evaluate(e => { const x = e.querySelector('.appmagic-button-label') || e; return getComputedStyle(x).color; });
}
async function valor(app, c) { return (await a.visible(app, c)).locator('input,textarea').first().inputValue(); }
async function hoyManagua() { const d = new Date(Date.now() - 6 * 3600e3); return d.toISOString().slice(0, 10) + 'T06:00:00Z'; }

(async () => {
  const b = await a.lanzar(); const app = await a.abrir(b);
  const t0 = new Date().toISOString();

  // ---- formato numérico (es-NI mostraba "160,00") ----
  // ---- APP-21 + formato: "Cobrado hoy" por fecha del pago = suma en Dataverse (se espera a que cargue) ----
  const desde0 = await hoyManagua();
  const pagosHoy = await dv.getAll(`sanic_cm_tbl_pagos?$select=sanic_cm_pg_monto&$filter=_createdby_value eq ${USER5} and sanic_cm_pg_moneda eq 1 and sanic_cm_pg_fecha ge ${desde0}`);
  const esperado = pagosHoy.reduce((t, p) => t + Number(p.sanic_cm_pg_monto), 0).toFixed(2);
  let cob = '';
  for (let i = 0; i < 20 && !cob.endsWith(esperado); i++) { cob = await txt(app, 'lblCobradoNIO'); if (!cob.endsWith(esperado)) await a.esperar(app, 1000); }
  ok('FORMATO', /\d\.\d{2}\s*$/.test(cob), `Cobrado hoy = "${cob}" (punto decimal con el navegador en es-NI)`);
  ok('APP-21 web', cob.endsWith(esperado), `Cobrado hoy (por fecha del pago) = "${cob}"; suma en Dataverse = ${esperado}`);

  // ---- APP-15: "Clientes" de hoy por CIF ----
  const desde = await hoyManagua();
  const ints = await dv.getAll(`sanic_cm_tbl_interaccions?$select=_sanic_cm_in_cuentaid_value&$filter=_createdby_value eq ${USER5} and createdon ge ${desde}`);
  const cifs = new Set();
  for (const i of ints) if (i._sanic_cm_in_cuentaid_value) cifs.add((await dv.get(`sanic_cm_tbl_cuentas(${i._sanic_cm_in_cuentaid_value})?$select=sanic_cm_ct_cifcliente`)).sanic_cm_ct_cifcliente);
  const kpi = await txt(app, 'lblSv_2');
  ok('APP-15', Number(kpi) === cifs.size, `KPI Clientes = ${kpi}; clientes distintos (CIF) con interacción hoy en Dataverse = ${cifs.size}`);

  // ---- APP-19 / 17 / 16 / 05(vista): Cliente desde Inicio sin pasar por Cartera ----
  app.red.marcar('cliente');
  await clicTexto(app, 'QA-E2E-LOC CLIENTE TRES');
  ok('APP-19', (await a.pantalla(app)) === 'scrCliente' && /CLIENTE TRES/.test(await txt(app, 'cmpHeaderBack_1')), `título: "${(await txt(app, 'cmpHeaderBack_1')).split('\n')[0]}"`);
  const cInter = await color(app, 'Button6_1');
  ok('APP-17', cInter === 'rgb(255, 255, 255)', `pestaña "Interacciones" seleccionada: texto ${cInter} (antes heredaba el color de la otra)`);
  const ctasTres = await dv.getAll(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid&$filter=sanic_cm_ct_cifcliente eq 'QA-E2E-LOC-CIF-003'`);
  let nInt = 0; for (const c of ctasTres) nInt += await dv.count('sanic_cm_tbl_interaccions', `_sanic_cm_in_cuentaid_value eq ${c.sanic_cm_tbl_cuentaid}`, 'sanic_cm_tbl_interaccionid');
  const tab = await txt(app, 'Button6_1');
  ok('APP-16', tab.includes(`(${nInt})`), `pestaña "${tab}"; interacciones del cliente en Dataverse = ${nInt}`);
  const sinFiltro = app.red.de('cliente').flatMap(e => e.subs).filter(s => /sanic_cm_tbl_(interaccions|asignacions)\?/.test(s.ruta) && !/\$filter=/.test(s.ruta));
  ok('APP-16/28', sinFiltro.length === 0, `consultas a interacciones/asignaciones sin $filter al abrir Cliente: ${sinFiltro.length}`);
  ok('APP-05 vista', (await app.frame.getByText('rec. QA-R1').count()) > 0, 'el pago QA-R1 aparece en la tarjeta de su interacción (búsqueda por la relación)');

  // ---- APP-07: varAsig sucio. Cartera → MOVIL → cuenta Especial (desde Cliente) → Inicio → cuenta Diaria (desde Inicio) ----
  await a.tocar(app, 'Icon1');                                  // ← a Inicio
  await a.tocar(app, 'icoLedger');                              // barra: Cartera
  await clicTexto(app, 'QA-E2E-LOC CLIENTE MOVIL');
  await clicTexto(app, '5498');                                 // cuenta Especial (por sus últimos 4)
  const tipoEsp = await txt(app, 'Text1_1');
  await a.tocar(app, 'Icon1'); await a.tocar(app, 'Icon1');     // ← Cliente, ← Cartera
  await a.tocar(app, 'icoHome');                                // barra: Inicio
  ok('APP-30', (await a.pantalla(app)) === 'scrInicio', 'Cartera → barra "Inicio" vuelve al Inicio (ahora con Back(), sin apilar)');
  await a.tocar(app, 'btnModoCta');
  await clicTexto(app, '7113');
  const tipoDia = await txt(app, 'Text1_1');
  ok('APP-07', tipoEsp === 'Especial' && tipoDia === 'Diaria', `desde Cliente: "${tipoEsp}" · después, desde Inicio, otra cuenta: "${tipoDia}" (antes quedaba el tipo de la anterior)`);
  const cDet = await color(app, 'Button6_3');
  ok('APP-17 det', cDet === 'rgb(255, 255, 255)', `Detalle abierto desde Inicio arranca en "Interacciones" (texto ${cDet})`);

  // ---- APP-18 / 24 / 32 en Interacción (sin guardar) ----
  await a.tocar(app, 'btnRegistrar');
  await a.escribir(app, 'txtComentario', 'Hola QA', { tab: true });
  await porTexto(app, 'Promesa de pago').click({ force: true }); await a.esperar(app, 1500);
  const com = await valor(app, 'txtComentario');
  ok('APP-18', com === 'Hola QA. Promesa de pago', `chip sobre texto escrito → "${com}" (antes reemplazaba lo escrito)`);
  await a.tocar(app, 'tgDatos');
  await a.escribir(app, 'inTelCel', '12ab', { tab: true }); await a.esperar(app, 1000);
  const hint1 = await txt(app, 'lblHint'); const hab1 = await a.botonHabilitado(app, 'btnRegistrar_1');
  await a.escribir(app, 'inTelCel', '8888-1234', { tab: true }); await a.esperar(app, 1000);
  const hint2 = await txt(app, 'lblHint'); const hab2 = await a.botonHabilitado(app, 'btnRegistrar_1');
  ok('APP-24', /teléfonos de 7 a 20/.test(hint1) && !hab1 && !/teléfonos/.test(hint2) && hab2,
     `"12ab" → "${hint1}", botón ${hab1 ? 'habilitado' : 'bloqueado'} · "8888-1234" → ${hab2 ? 'habilitado' : 'bloqueado'}`);
  await a.tocar(app, 'tgDatos');
  await a.tocar(app, 'tpPago'); await a.tocar(app, 'btnAddPago');
  const monto = await valor(app, 'txtMonto'); const mon = await valor(app, 'Dropdown1');
  ok('APP-32 NIO', monto === '' && /NIO/.test(mon), `cuenta en córdobas: monto "${monto}" (antes 0), moneda ${mon}`);
  await a.tocar(app, 'Icon1'); await a.tocar(app, 'Icon1');     // salir SIN guardar: ← Detalle, ← Inicio
  await clicTexto(app, '5498');
  await a.tocar(app, 'btnRegistrar');
  const com2 = await valor(app, 'txtComentario');
  ok('APP-18 arr', com2 === '', `otra cuenta abre con el comentario vacío ("${com2}")`);
  await a.tocar(app, 'tpPago'); await a.tocar(app, 'btnAddPago');
  const monU = await valor(app, 'Dropdown1');
  ok('APP-32 USD', /USD/.test(monU), `cuenta solo en dólares: moneda ${monU}`);
  await a.tocar(app, 'tpPago');

  // ---- APP-25 (parcial): etiquetas y contraste ----
  // Los botones clásicos no admiten AccessibleLabel (se anuncian por su texto): se miran íconos y campos.
  const etq = ['Volver', 'Comentario de la interacción', 'Aplica pago', 'Actualizar datos del cliente'];
  const hay = []; for (const e of etq) if (await app.frame.locator(`[aria-label="${e}"]`).count()) hay.push(e);
  ok('APP-25 etiq', hay.length === etq.length, `etiquetas para lector de pantalla presentes: ${hay.length}/${etq.length} (${hay.join(', ')})`);
  const cMuted = await app.frame.getByText('APLICAR A ESTOS PRODUCTOS').first().evaluate(e => getComputedStyle(e).color);
  ok('APP-25 contr', cMuted === 'rgb(95, 107, 126)', `texto secundario ${cMuted} (5,4:1; antes #98A2B3 2,6:1)`);

  // ---- APP-27: el agente se resuelve con la fórmula con nombre (crea 1 interacción QA) ----
  await a.escribir(app, 'txtComentario', 'QA web APP-27 agente', { tab: true });
  await a.tocar(app, 'btnRegistrar_1', { esperaMs: 6000 });
  const nueva = (await dv.getAll(`sanic_cm_tbl_interaccions?$select=_sanic_cm_in_agente_value,sanic_cm_in_nombreagente&$filter=_createdby_value eq ${USER5} and createdon ge ${t0} and sanic_cm_in_comentario eq 'QA web APP-27 agente'`));
  ok('APP-27', nueva.length === 1 && nueva[0]._sanic_cm_in_agente_value && nueva[0].sanic_cm_in_nombreagente !== 'Sin nombre',
     `interacciones creadas: ${nueva.length}; agente ${nueva[0]?._sanic_cm_in_agente_value ? 'vinculado' : 'VACÍO'}; nombre "${nueva[0]?.sanic_cm_in_nombreagente}"`);

  console.log(`\n${res.filter(r => r.ok).length}/${res.length} OK`);
  require('fs').writeFileSync(require('path').join(__dirname, 'resultados', 'grupo2.json'), JSON.stringify(res, null, 2));
  await a.cerrar(app); await b.close();
  process.exit(res.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.log('ERR', e.message); process.exit(2); });
