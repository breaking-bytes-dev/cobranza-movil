// F8 BackOffice (User1): Por transcribir (la interacción de F7 con su pago y su cuenta) → código + Enter → Transcritas hoy.
// SEGURIDAD PCI: el detalle de "Por transcribir" muestra el número COMPLETO de la cuenta (solo lo ve BackOffice). La suite lo
// evalúa dentro del navegador y solo saca un booleano (16 dígitos y últimos 4 correctos); no lo lee, no lo imprime y NO
// captura pantallas de esta fase.
const dv = require('../../backoffice-canvas/lib/dataverse');
const v = require('../../backoffice-canvas/lib/verdad');
const { AGENTE, U } = require('../lib/ctx');
const ui = require('../lib/ui');
const { pl, c } = ui;

async function f8(ctx) {
  const D = ctx.datos; const A = D.cuentas?.find((e) => e.clave === (D.cobro?.clave || 'A')); // cuenta donde cobró F7
  ctx.requiere(D.interaccion?.numero && D.pago, 'F7 no dejó la interacción con pago');
  const it = D.interaccion; const mask = `•••• ${A.ult4}`;
  const codigo = `QAINT${ctx.runid}`;           // 10 caracteres: el máximo del campo
  const app = await ui.abrirBackoffice('user1', 'F8');
  ctx.capturador = ui.capturador(ctx, app, { sensible: true });
  try {
    await ctx.paso('F8-01', 'Por transcribir', async () => {
      // KPIs de Inicio: se cargan de forma asíncrona tras abrir la app. Se espera a que coincidan con la API (que se vuelve a
      // leer en cada vuelta, por si la cola cambia) y a que se mantengan iguales en dos lecturas consecutivas.
      const leer = async () => ({ k: await pl.kpis(app), cola: await v.pendientes(), conPago: await v.pendientesConPago() });
      const listo = (x) => c.num(x.k['EN COLA']) === x.cola && c.num(x.k['CON PAGO']) === x.conPago && x.conPago >= 1;
      const { k, cola, conPago } = await ui.esperarEstable(leer, { timeout: 45000, intervalo: 1000, listo });
      ctx.chk('F8-01', c.num(k['EN COLA']) === cola && c.num(k['CON PAGO']) === conPago && conPago >= 1, `Inicio BackOffice: EN COLA / CON PAGO = API (${k['EN COLA']}/${k['CON PAGO']} vs ${cola}/${conPago})`);
      await ui.ir(app, 'Por transcribir', 'scrTranscripcion');
      await pl.ctl(app, 'btnFilaLocsTrx').locator('visible=true').filter({ hasText: AGENTE }).first().click();
      await pl.esperar(app, 2500);
      // la cola del localizador puede tener otras interacciones: se elige LA de la corrida por su cliente
      const fila = pl.ctl(app, 'btnFilaColaTrx').locator('visible=true').filter({ hasText: ctx.nombreCliente }).first();
      ctx.requiere(await fila.count(), `la interacción de "${ctx.nombreCliente}" no está en la cola de ${AGENTE}`);
      await fila.click({ force: true }); await pl.esperar(app, 3000);
      const pos = await pl.texto(app, 'lblPosTrx');
      ctx.requiere(pos.includes(it.numero), `Seguridad: la seleccionada no es ${it.numero} (${pos})`);
      ctx.chk('F8-02', pos.toUpperCase().includes(AGENTE.toUpperCase()), `detalle: interacción ${it.numero} del localizador "${AGENTE}" ("${pos}")`);
      const coment = await pl.texto(app, 'lblComentTrx').catch(() => '');
      ctx.chk('F8-03', coment.includes(it.comentario), `detalle: comentario "${coment}"`);
      const secP = await pl.texto(app, 'lblSecPagosTrx').catch(() => null);
      const montos = await pl.ctl(app, 'lblPagoMontoTrx').locator('visible=true').allInnerTexts();
      const recibos = await pl.ctl(app, 'lblPagoReciboTrx').locator('visible=true').allInnerTexts();
      ctx.chk('F8-04', secP === 'PAGOS (1)' && montos.some((m) => m.includes('C$ 25.00')) && recibos.some((r) => r.includes(it.recibo)), `detalle: ${secP} · ${montos.join(',')} · recibo ${recibos.join(',')}`);
      const cta = await (await pl.visible(app, 'lblCuentaGrandeTrx')).evaluate((el, u4) => { const t = (el.innerText || '').replace(/\s/g, ''); return { largo: t.length, soloDigitos: /^\d+$/.test(t), ult4: t.endsWith(u4) }; }, A.ult4);
      ctx.chk('F8-05', cta.largo === 16 && cta.soloDigitos && cta.ult4, `detalle: la cuenta se ve COMPLETA para BackOffice (${cta.largo} dígitos, termina en ${A.ult4}; el número no se lee ni se guarda)`);
      const nombre = await pl.texto(app, 'lblNombreTrx').catch(() => '');
      ctx.chk('F8-06', nombre.toUpperCase().includes(ctx.nombreCliente.toUpperCase()), `detalle: cliente "${nombre}"`);
      const colaMeta = (await pl.ctl(app, 'lblColaMetaTrx').locator('visible=true').allInnerTexts()).join(' | ');
      ctx.chk('F8-07', colaMeta.includes(mask) && !/\d{13,}/.test(colaMeta.replace(/[\s-]/g, '')), 'la cola muestra la cuenta enmascarada (el número completo solo en el detalle)');

      // Grabar: código en minúsculas + Enter → la app lo pasa a mayúsculas
      // Enter en el campo solo lleva el foco a "Grabar"; el segundo Enter (sobre el botón) graba (2026-10-09).
      await pl.escribir(app, 'txtCodigoTrx', codigo.toLowerCase(), { enter: true, tab: false });
      await app.page.keyboard.press('Enter');
      const n = await c.esperarNotif(app, /grabada|error/i, 20000);
      ctx.chk('F8-08', new RegExp(`${it.numero} grabada`).test(n || ''), `Enter graba: aviso "${n}"`);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      const r = await dv.get(`sanic_cm_tbl_interaccions(${it.id})?$select=sanic_cm_in_estadograbacion,sanic_cm_in_codigograbacion,sanic_cm_in_fechagrabacion,_sanic_cm_in_grabadopor_value`);
      ctx.chk('F8-09', r.sanic_cm_in_estadograbacion === 2 && r.sanic_cm_in_codigograbacion === codigo && r._sanic_cm_in_grabadopor_value === U.bo && r.sanic_cm_in_fechagrabacion >= ctx.hoy.inicioUTC,
        `Dataverse ${it.numero}: Grabado (${r.sanic_cm_in_estadograbacion}), código "${r.sanic_cm_in_codigograbacion}" en MAYÚSCULAS, grabado por User1, fecha ${r.sanic_cm_in_fechagrabacion}`);
      const sigue = (await pl.ctl(app, 'btnFilaColaTrx').locator('visible=true').filter({ hasText: ctx.nombreCliente }).count());
      ctx.chk('F8-10', sigue === 0, 'la interacción salió de la cola sin recargar');
    });
    await ctx.paso('F8-11', 'Transcritas hoy', async () => {
      await ui.ir(app, 'Transcritas hoy', 'scrTranscritas');
      const k = await pl.kpis(app);
      const t = await v.transcritasHoy(U.bo);
      ctx.chk('F8-11', c.num(k['TRANSCRITAS']) === t && t >= 1, `Transcritas hoy: KPI TRANSCRITAS = API (${k['TRANSCRITAS']} vs ${t})`);
      // pagos de mis transcritas de hoy (misma regla que la app: pago de hoy ∩ N° de interacción transcrito por mí)
      const mias = (await dv.getAll(`sanic_cm_tbl_interaccions?$select=sanic_cm_in_numerointeraccion&$filter=sanic_cm_in_estadograbacion eq 2 and sanic_cm_in_fechagrabacion ge ${ctx.hoy.inicioUTC} and _sanic_cm_in_grabadopor_value eq ${U.bo}`)).map((x) => x.sanic_cm_in_numerointeraccion);
      const pagos = (await dv.getAll(`sanic_cm_tbl_pagos?$select=sanic_cm_pg_monto,sanic_cm_pg_moneda,sanic_cm_pg_numerointeraccion&$filter=sanic_cm_pg_fecha ge ${ctx.hoy.inicioUTC}`)).filter((p) => mias.includes(p.sanic_cm_pg_numerointeraccion));
      const nio = pagos.filter((p) => p.sanic_cm_pg_moneda === 1).reduce((s, p) => s + Number(p.sanic_cm_pg_monto), 0);
      const usd = pagos.filter((p) => p.sanic_cm_pg_moneda === 2).reduce((s, p) => s + Number(p.sanic_cm_pg_monto), 0);
      ctx.chk('F8-12', k['PAGOS TRANSCRITOS'] === v.moneda(nio, usd), `Transcritas hoy: PAGOS TRANSCRITOS = suma API (${k['PAGOS TRANSCRITOS']} vs ${v.moneda(nio, usd)})`);
      const f = await ui.filas(app, 'galTscTsc').lista();
      const fila = f.filter({ hasText: it.comentario }).first();
      const txt = (await fila.count()) ? (await fila.innerText()).replace(/\s+/g, ' ').trim() : '';
      ctx.chk('F8-13', !!txt && txt.includes(mask) && txt.includes('💵 C$ 25.00') && txt.includes(`Grabada · ${codigo}`) && !/\d{13,}/.test(txt.replace(/[\s-]/g, '')),
        `Transcritas hoy: la fila de la corrida muestra la cuenta ${mask}, "💵 C$ 25.00" y "Grabada · ${codigo}" (sin número completo)`);
    });
  } finally { await ui.cerrar(app); }
}

module.exports = { f8 };
