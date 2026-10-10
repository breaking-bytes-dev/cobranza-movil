// Carga del CSV por la pantalla "Cargar archivo" del backoffice (User4 = Admin), en lugar de la API de Smelter.
// Lo usa `node run.js --carga-ui` (F1) y sirve de prueba funcional de la pantalla: prueba en seco → carga real.
// REGLA: el CSV tiene números de tarjeta ficticios; esto solo lo sube al control de archivos y NUNCA imprime su contenido.
const path = require('path');
const dv = require('../../backoffice-canvas/lib/dataverse');
const cfg = require('../../backoffice-canvas/lib/config');
const v = require('../../backoffice-canvas/lib/verdad');
const ui = require('./ui');
const smelter = require('./smelter');
const { sinPAN, U } = require('./ctx');
const pl = ui.pl;

const SEL = 'sanic_smlt_ij_name,sanic_smlt_ij_status,sanic_smlt_ij_isdryrun,sanic_smlt_ij_totalrows,sanic_smlt_ij_processedrows,sanic_smlt_ij_successcount,sanic_smlt_ij_failedcount,sanic_smlt_ij_skippedcount,sanic_smlt_ij_sourcefile_name,_ownerid_value,_createdby_value,createdon';
const TERMINALES = ['Terminada', 'Terminada con errores', 'Falló'];

// Jobs creados por la pantalla para ese archivo (el nombre del job lleva el nombre del archivo), del más viejo al más nuevo.
async function jobsDelArchivo(nombreArchivo) {
  return (await dv.getAll(`sanic_tbl_smlt_importjobs?$select=${SEL}&$filter=contains(sanic_smlt_ij_name,'${nombreArchivo}')&$orderby=createdon asc`));
}

async function elegirArchivo(app, ruta) {
  const cont = await pl.visible(app, 'attArchivoCar');
  await cont.locator('input[type="file"]').first().setInputFiles(ruta);
  await esperarTexto(app, 'lblArchivoMsgCar', /^✓/, 30000);
}

async function esperarTexto(app, control, re, maxMs) {
  const t0 = Date.now(); let ult = '';
  while (Date.now() - t0 < maxMs) {
    try { ult = await pl.texto(app, control, { timeout: 3000 }); if (re.test(ult)) return ult; } catch (e) { /* aún no está */ }
    await app.page.waitForTimeout(1000);
  }
  throw new Error(`[control ${control}] no llegó a /${re.source}/ en ${Math.round(maxMs / 1000)} s (último: "${ult}")`);
}

// Espera a que el chip de resultado diga Terminada / Terminada con errores / Falló. Devuelve { chip, kpis, aviso }.
async function esperarResultado(app, maxMs) {
  const t0 = Date.now(); let chip = '';
  while (Date.now() - t0 < maxMs) {
    try { chip = await pl.texto(app, 'chpEstadoCar', { timeout: 3000 }); } catch (e) { chip = ''; }
    if (TERMINALES.includes(chip)) break;
    await app.page.waitForTimeout(2000);
  }
  await app.page.waitForTimeout(2500);
  const k = await pl.kpis(app);
  const aviso = (await pl.existeVisible(app, 'lblFallosCar')) ? await pl.texto(app, 'lblFallosCar') : '';
  return { chip, seg: Math.round((Date.now() - t0) / 1000), kpis: k, aviso };
}

const num = (s) => Number(String(s || '').replace(/[^\d]/g, ''));

// Sube `rutaCsv` por la pantalla: prueba en seco (sin tocar Cobranza Móvil) y después la carga real.
// Devuelve algo con la forma de smelter.importar(): { jobId, job, logs, chunks } (+ jobPrueba, ui).
async function importarPorPantalla(ctx, rutaCsv, { chequeoSinFilas } = {}) {
  const nombre = path.basename(rutaCsv);
  const cargaHoyAntes = await v.cargaHoy();
  const out = { ui: {} };
  const app = await ui.abrirBackoffice('user4', 'F1ui');
  try {
    await ui.ir(app, 'Cargar archivo', 'scrCargaArchivo');
    ctx.chk('F1u-01', (await pl.itemsNav(app)).includes('Cargar archivo') && await pl.existeVisible(app, 'btnValidarCar'),
      'User4 (Admin) ve «Cargar archivo» en la nav y abre la pantalla');
    ctx.chk('F1u-02', !(await pl.botonHabilitado(app, 'btnValidarCar')) && !(await pl.botonHabilitado(app, 'btnCargarCar')),
      '«Validar» y «Cargar» arrancan deshabilitados (sin archivo)');
    await elegirArchivo(app, rutaCsv);
    ctx.chk('F1u-03', await pl.botonHabilitado(app, 'btnValidarCar') && !(await pl.botonHabilitado(app, 'btnCargarCar')),
      `elegido ${nombre}: «Validar» habilitado y «Cargar» sigue deshabilitado (falta la prueba en seco)`);

    // ── Prueba en seco ──
    await (await pl.boton(app, 'btnValidarCar')).click();
    await app.page.waitForTimeout(1500);
    ctx.chk('F1u-04', !(await pl.botonHabilitado(app, 'btnValidarCar')), 'mientras corre, «Validar» se deshabilita (sin doble envío)');
    const r1 = await esperarResultado(app, 300000);
    out.ui.prueba = r1;
    await ctx.capturador?.('prueba-en-seco');
    ctx.chk('F1u-05', r1.chip === 'Terminada' && num(r1.kpis['FALLOS']) === 0 && num(r1.kpis['FILAS DEL ARCHIVO']) === 3,
      `prueba en seco: "${r1.chip}" en ~${r1.seg} s · filas ${r1.kpis['FILAS DEL ARCHIVO']} · procesadas ${r1.kpis['PROCESADAS']} · éxitos ${r1.kpis['ÉXITOS']} · fallos ${r1.kpis['FALLOS']} · omitidas ${r1.kpis['OMITIDAS']}`);
    const cargaHoyDespuesDry = await v.cargaHoy();
    const cli = (await dv.get(`sanic_cm_tbl_clientes?$select=sanic_cm_tbl_clienteid&$filter=sanic_cm_cl_cif eq '${ctx.cif}'`)).value.length;
    ctx.chk('F1u-06', cargaHoyDespuesDry === cargaHoyAntes && cli === 0,
      `la prueba en seco NO creó nada en Cobranza Móvil: filas de carga de hoy ${cargaHoyAntes} → ${cargaHoyDespuesDry}, clientes QA ${cli}`);
    ctx.chk('F1u-07', await pl.botonHabilitado(app, 'btnCargarCar'), 'tras la prueba limpia, «Cargar» queda habilitado');

    // ── Carga real (con confirmación) ──
    await (await pl.boton(app, 'btnCargarCar')).click();
    await pl.visible(app, 'btnConfirmarCar', { timeout: 10000 });
    ctx.chk('F1u-08', (await pl.existeVisible(app, 'btnConfirmarCar')) && (await jobsDelArchivo(nombre)).length === 1,
      '«Cargar» pide confirmación y todavía no creó el job real');
    await (await pl.boton(app, 'btnConfirmarCar')).click();
    const r2 = await esperarResultado(app, 420000);
    out.ui.real = r2;
    await ctx.capturador?.('carga-real');
    ctx.chk('F1u-09', r2.chip === 'Terminada' && num(r2.kpis['FALLOS']) === 0 && num(r2.kpis['FILAS DEL ARCHIVO']) === 3,
      `carga real: "${r2.chip}" en ~${r2.seg} s · filas ${r2.kpis['FILAS DEL ARCHIVO']} · procesadas ${r2.kpis['PROCESADAS']} · éxitos ${r2.kpis['ÉXITOS']} · fallos ${r2.kpis['FALLOS']} · omitidas ${r2.kpis['OMITIDAS']}`);
    ctx.chk('F1u-10', await pl.existeVisible(app, 'btnIrMonitoreoCar'), 'tras la carga real aparece «Ir a Monitoreo de carga»');
    // Historial: lista las dos cargas propias.
    const filas = await ui.textosFilas(app, 'galHistCar');
    out.ui.historial = filas.length;
    ctx.chk('F1u-11', filas.length >= 2, `historial propio con las cargas recién hechas (${filas.length} filas visibles)`);
  } finally { await ui.cerrar(app); }

  // ── Verdad en Dataverse ──
  const jobs = await jobsDelArchivo(nombre);
  const dry = jobs.find((j) => j.sanic_smlt_ij_isdryrun === true), real = jobs.find((j) => j.sanic_smlt_ij_isdryrun === false);
  ctx.chk('F1u-12', jobs.length === 2 && !!dry && !!real && dry.sanic_smlt_ij_status === 2 && real.sanic_smlt_ij_status === 2 && !dry.sanic_smlt_ij_failedcount && !real.sanic_smlt_ij_failedcount,
    `Dataverse: 2 jobs del archivo (prueba ${dry?.sanic_tbl_smlt_importjobid} y real ${real?.sanic_tbl_smlt_importjobid}), ambos Completed (2) con 0 fallos · prueba: ${smelter.resumenJob(dry || {})} · real: ${smelter.resumenJob(real || {})}`);
  // El rol Smelter Operador llega por el team «CM · Admin y Carga», así que Dataverse deja el job a nombre del team:
  // lo que identifica a quien cargó es 'Created By' (y por eso el historial de la pantalla filtra por ahí).
  const teamCarga = cfg.teams.AdminCarga;
  ctx.chk('F1u-13', jobs.every((j) => j._createdby_value === U.adm && [U.adm, teamCarga].includes(j._ownerid_value) && j.sanic_smlt_ij_sourcefile_name === nombre),
    `los jobs los creó User4 (creador ${jobs.map((j) => j._createdby_value).join(', ')} · dueño ${jobs.map((j) => (j._ownerid_value === teamCarga ? 'team Admin y Carga' : j._ownerid_value)).join(', ')}) y llevan el archivo ${nombre}`);
  ctx.chk('F1u-14', jobs.every((j) => !/\d{13,19}/.test(j.sanic_smlt_ij_name)), `ningún nombre de job contiene secuencias de 13 a 19 dígitos (${jobs.map((j) => sinPAN(j.sanic_smlt_ij_name)).join(' | ')})`);
  ctx.registrar && jobs.forEach((j) => ctx.registrar('job', j.sanic_tbl_smlt_importjobid, 'cargado por la pantalla'));
  if (!real) throw new Error('la pantalla no dejó un job real');
  return { jobId: real.sanic_tbl_smlt_importjobid, job: real, logs: await smelter.logsDe(real.sanic_tbl_smlt_importjobid), chunks: 0, jobPrueba: dry, ...out };
}

module.exports = { importarPorPantalla, jobsDelArchivo };
