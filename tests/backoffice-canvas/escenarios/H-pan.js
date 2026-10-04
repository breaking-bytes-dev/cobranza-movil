// FASE H — PCI (fase 2): el número de cuenta completo NO se ve en ninguna pantalla salvo el detalle de
// Transcripción / Transcritas del rol BackOffice, que lo lee de la bóveda (sanic_cm_tbl_cuentapan).
//  · H-01 (API, con MSCRMCallerID): solo BackOffice lee la bóveda; Solicitante, Supervisor y Admin → 403 o vacío.
//  · H-02 (UI): Solicitante, Supervisor y Admin recorren sus pantallas y NINGUNA muestra 13+ dígitos seguidos
//    (ni con separadores), ni consulta la bóveda, ni pide las columnas viejas *_numerocuenta.
//  · H-03 (UI): BackOffice ve en Transcritas hoy el detalle con el número completo de la bóveda (el "PAN" QA es
//    `QA-E2E-CTA-0001`) y la lista enmascarada.
// SEGURIDAD: solo datos QA-E2E; la fase no toma capturas (un fallo podría dejar un PAN real en el PNG) y nunca
// imprime el texto de pantalla: ante un hallazgo informa la pantalla y la LONGITUD de la secuencia, no sus dígitos.
// Depende de la siembra (bóveda QA) y, para H-03, de que F-01 haya transcrito las 2 interacciones QA (fase D).
const pl = require('../lib/player');
const inf = require('../lib/informe');
const dv = require('../lib/dataverse');
const cfg = require('../lib/config');
const c = require('./comun');

// 13+ dígitos seguidos, o 13+ dígitos separados por espacio o guion (formato de tarjeta "1234 5678 …").
const RE_PAN = /\d{13,}/;
const RE_PAN_SEP = /(?:\d[ -]){12,}\d/;
const hayPan = (txt) => RE_PAN.test(txt) || RE_PAN_SEP.test(txt);

// Texto visible + aria-label/title de TODOS los frames (el runtime vive en un iframe).
async function textoDePantalla(app) {
  let todo = '';
  for (const f of app.page.frames()) {
    try {
      todo += '\n' + await f.evaluate(() => {
        const t = document.body ? document.body.innerText : '';
        const attrs = Array.from(document.querySelectorAll('[aria-label],[title]')).map(el => `${el.getAttribute('aria-label') || ''} ${el.getAttribute('title') || ''}`).join('\n');
        return t + '\n' + attrs;
      });
    } catch (e) { /* frame cruzado */ }
  }
  return todo;
}

// Barrido: navega a cada pantalla del rol, opcionalmente abre un panel de detalle, y revisa el texto.
// `acciones[titulo]` = async (app) => {} para abrir un detalle (solo lectura, sin pulsar nada que escriba).
async function barrer(e, app, rol, pantallas, acciones = {}) {
  const hallazgos = [];
  for (const [titulo, ruta] of pantallas) {
    await c.ir(app, titulo, `${ruta}(H)`);
    let txt = await textoDePantalla(app);
    if (hayPan(txt)) hallazgos.push(`${titulo}: lista/pantalla con ${(txt.match(RE_PAN) || txt.match(RE_PAN_SEP))[0].replace(/\D/g, '').length} dígitos seguidos`);
    if (acciones[titulo]) {
      const abrio = await acciones[titulo](app).then(() => true).catch(() => false);
      if (abrio) {
        txt = await textoDePantalla(app);
        if (hayPan(txt)) hallazgos.push(`${titulo}: detalle con ${(txt.match(RE_PAN) || txt.match(RE_PAN_SEP))[0].replace(/\D/g, '').length} dígitos seguidos`);
      } else e.nota(`${rol} · ${titulo}: no había fila que abrir (se revisó solo la lista)`);
    }
    e.nota(`${rol} · ${titulo}: revisada (${txt.length} caracteres de texto/atributos)`);
  }
  e.check(hallazgos.length === 0, `${rol}: ninguna pantalla muestra 13+ dígitos seguidos`, 'sin números de cuenta completos', hallazgos);
}

// Rutas pedidas en TODA la sesión del `app`.
const rutasDe = (app) => app.red.entradas.flatMap(x => x.subs.map(s => s.ruta));

function revisarRed(e, app, rol) {
  const rutas = rutasDe(app);
  // La app ya no lee ni escribe las columnas viejas: si aparecen en $select/$filter/cuerpo, algo las sigue usando.
  const viejas = rutas.filter(r => /sanic_cm_(as|in|pg|cd)_numerocuenta/.test(r));
  e.check(viejas.length === 0, `${rol}: ninguna request usa las columnas viejas as_/in_/pg_/cd_numerocuenta`, 'ninguna', viejas.slice(0, 3).map(r => r.slice(0, 160)));
  const escrituras = app.red.entradas.filter(x => x.cuerpoPatch && /numerocuenta/i.test(x.cuerpoPatch));
  e.check(escrituras.length === 0, `${rol}: ninguna escritura envía *numerocuenta`, 'ninguna', escrituras.length);
  const boveda = rutas.filter(r => /sanic_cm_tbl_cuentapans/.test(r));
  e.check(boveda.length === 0, `${rol}: ninguna request a la bóveda (sanic_cm_tbl_cuentapans)`, 'ninguna', boveda.slice(0, 2).map(r => r.slice(0, 120)));
  const e403 = c.erroresRed(app).filter(x => /^403/.test(x));
  e.check(e403.length === 0, `${rol}: sin respuestas 403 en la sesión`, 0, e403.slice(0, 3));
  // Hasta el paso 8 el nombre principal de la cuenta aún guarda el PAN: es un dato a vigilar, no un fallo de la app.
  e.nota(`${rol}: ¿alguna request pide el nombre principal sanic_cm_ct_numerodecuenta? ${rutas.some(r => /\$select=[^&]*sanic_cm_ct_numerodecuenta/.test(r))} (debería ser inocuo tras el paso 8)`);
}

// Lectura de la bóveda como un usuario (el Service Principal suplanta con MSCRMCallerID). Solo se pide el nombre
// principal enmascarado y, filtrando por clave QA, el "PAN" QA.
async function leerBovedaComo(userId) {
  try {
    const r = await dv.raw('GET', "sanic_cm_tbl_cuentapans?$select=sanic_cm_cp_name&$top=1", undefined, { MSCRMCallerID: userId });
    return { status: r.status, filas: (r.json?.value || []).length };
  } catch (err) { return { status: err.status || 0, filas: 0 }; }
}

module.exports = async function faseH(suite) {
  const U = cfg.usuarios;

  // ── H-01 · API por rol ───────────────────────────────────────────────────────────────
  await inf.correr('H-01', 'Bóveda de PAN (API, suplantando al usuario): BackOffice la lee; Solicitante, Supervisor y Admin → 403 o vacío', 'todos', async (e) => {
    const bo = await leerBovedaComo(U.user1.id);
    e.check(bo.status === 200 && bo.filas > 0, 'BackOffice (User1) lee la bóveda', '200 con filas', `${bo.status} · ${bo.filas} fila(s)`);
    const qaPan = await dv.raw('GET', "sanic_cm_tbl_cuentapans?$select=sanic_cm_cp_pan&$filter=startswith(sanic_cm_cp_clavepan,'QA-E2E')&$top=5", undefined, { MSCRMCallerID: U.user1.id }).then(r => r.json.value.map(x => x.sanic_cm_cp_pan)).catch(() => []);
    e.check(qaPan.length > 0 && qaPan.every(x => /^QA-E2E-CTA-/.test(x)), 'BackOffice lee el "PAN" de las cuentas QA de la bóveda (cadenas QA-E2E-CTA-…)', 'QA-E2E-CTA-…', qaPan.map(x => x.slice(0, 12)));
    for (const k of ['user2', 'user3', 'user4']) {
      const r = await leerBovedaComo(U[k].id);
      e.check(r.status === 403 || (r.status === 200 && r.filas === 0), `${U[k].rol} (${U[k].alias}) NO lee la bóveda`, '403 o vacío', `${r.status} · ${r.filas} fila(s)`);
    }
    // El nombre principal de la bóveda y de la carga diaria nunca tiene 5+ dígitos (solo QA acá; el barrido real es del paso 8).
    const nombres = await dv.getAll("sanic_cm_tbl_cuentapans?$select=sanic_cm_cp_name&$filter=startswith(sanic_cm_cp_clavepan,'QA-E2E')");
    e.check(nombres.length > 0 && nombres.every(x => !/\d{5,}/.test(x.sanic_cm_cp_name || '')), 'el nombre principal de la bóveda QA va enmascarado (sin 5+ dígitos)', 'sin 5+ dígitos', nombres.length);
  });

  // ── H-02 · UI de los roles SIN acceso a la bóveda ──────────────────────────────────────
  const roles = [
    { user: 'user2', rol: 'Solicitante', pantallas: [['Nueva solicitud', 'scrNuevaSolicitud'], ['Mis solicitudes', 'scrMisSolicitudes']],
      acciones: {
        'Nueva solicitud': async (app) => { await c.buscarEnBuscador(app, { buscador: 'txtBuscarCtaNva', galeria: 'galCtaNva', cambiar: 'btnCambiarCtaNva', buscar: 'QA-E2E CLIENTE' }); },
        'Mis solicitudes': async (app) => { await (await pl.ctl(app, 'btnFilaSol').locator('visible=true').first()).click({ timeout: 8000 }); await pl.esperar(app, 1500); },
      } },
    { user: 'user3', rol: 'Supervisor', pantallas: [['Aprobaciones', 'scrAprobaciones']],
      acciones: { 'Aprobaciones': async (app) => { await (await pl.ctl(app, 'btnFilaAprb').locator('visible=true').first()).click({ timeout: 8000 }); await pl.esperar(app, 1500); } } },
    { user: 'user4', rol: 'Admin', pantallas: [['Reconciliación', 'scrReconciliacion'], ['Cierres', 'scrCierres'], ['Datos maestros', 'scrMaestros']],
      acciones: { 'Cierres': async (app) => { await (await pl.ctl(app, 'btnFilaCie').locator('visible=true').first()).click({ timeout: 8000 }); await pl.esperar(app, 1500); } } },
  ];
  for (const r of roles) {
    const app = await c.abrir(suite, r.user, 'H', r.rol);
    try {
      await inf.correr(`H-02-${r.rol}`, `${r.rol}: ninguna pantalla muestra el número de cuenta completo (13+ dígitos) ni toca la bóveda`, r.rol, async (e) => {
        await barrer(e, app, r.rol, r.pantallas, r.acciones);
        revisarRed(e, app, r.rol);
      });
    } finally { await pl.cerrar(app); }
  }

  // ── H-03 · BackOffice ──────────────────────────────────────────────────────────────────
  const app = await c.abrir(suite, 'user1', 'H', 'BackOffice');
  try {
    await inf.correr('H-03', 'BackOffice (Transcritas hoy): lista enmascarada y detalle con el número completo leído de la bóveda (QA)', 'BackOffice', async (e) => {
      if (!suite.datos.transcritoOk) { e.bloquear('depende de F-01 (no hay interacciones QA transcritas hoy)'); return; }
      await c.ir(app, 'Transcritas hoy', 'scrTranscritas(H)');
      const lista = (await pl.ctl(app, 'lblCuentaTsc').locator('visible=true').allInnerTexts()).filter(x => /E2E/.test(x));
      e.check(lista.length > 0 && lista.every(x => x.includes('•••• E2E1') && !x.includes('QA-E2E-CTA')), 'la lista muestra la cuenta QA enmascarada (•••• E2E1), nunca el número completo', '•••• E2E1', lista.slice(0, 3));
      app.red.marcar('H:scrTranscritas(detalle)');
      const fila = pl.ctl(app, 'btnFilaTsc').locator('visible=true').filter({ hasText: 'QA-E2E CLIENTE UNO' }).first();
      if (!(await fila.count())) { e.check(false, 'hay una fila QA que abrir', 'fila QA', 'ninguna'); return; }
      await fila.click(); await pl.esperar(app, 1500);
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 20000, minMs: 1500 });
      const det = await pl.texto(app, 'lblVCuentaTsc');
      e.check(det.startsWith('QA-E2E-CTA-0001'), 'el detalle muestra el número completo (QA) leído de la bóveda', 'QA-E2E-CTA-0001 · …', det.replace(/\d{5,}/g, '#'));
      const bov = app.red.de('H:scrTranscritas(detalle)').flatMap(x => x.subs.map(s => s.ruta)).filter(r => /^sanic_cm_tbl_cuentapans\?/.test(r));
      e.check(bov.length > 0 && bov.length === 1 && bov.every(r => /(_sanic_cm_cp_cuentaid_value|sanic_cm_cp_cuentaid\/sanic_cm_tbl_cuentaid) eq [0-9a-f-]{36}/i.test(decodeURIComponent(r))), 'la lectura de la bóveda es UNA consulta filtrada por la cuenta del registro (no N+1, no tabla entera)', '$filter=_sanic_cm_cp_cuentaid_value eq <id>', bov.slice(0, 2));
      const fallos = c.erroresRed(app, 'H:scrTranscritas(detalle)');
      e.check(fallos.length === 0, 'sin errores de Dataverse al leer la bóveda', [], fallos);
    });
  } finally { await pl.cerrar(app); }
};
