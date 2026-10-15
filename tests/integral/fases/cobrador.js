// F7 Cobrador (User5, web player de la app del cobrador): Cartera = lo que User5 puede ver (API suplantando),
// el cliente QA con sus cuentas, y un cobro real (interacción + pago) sobre la cuenta A desde la app.
// El web player corre en LÍNEA: lo offline (sin señal, sincronización, "Ver número") se prueba en el teléfono (guía 17, 📱).
const dv = require('../../backoffice-canvas/lib/dataverse');
const { AGENTE, U, esperar, dormir } = require('../lib/ctx');
const ui = require('../lib/ui');
const datos = require('../lib/datos');
const { pl, c, lo } = ui;

// Espera a que la pantalla visible sea `scr`: tras un toque, la navegación del player puede tardar más que la espera de red
// quieta y leer la pantalla UNA vez devuelve la anterior (F7-07, corrida de 2026-10-14). Devuelve la última pantalla vista.
const esperarPantalla = (app, scr) => ui.esperarEstable(() => lo.pantalla(app), { timeout: 20000, listo: (p) => p === scr });

// Espera a que no quede ningún aviso (Notify) en pantalla: si no, el aviso del intento anterior se lee como el del siguiente.
async function sinAvisos(app, ms = 20000) {
  const t = Date.now();
  while (Date.now() - t < ms && (await pl.notificaciones(app)).length) await pl.esperar(app, 500);
}

// Toca Guardar en la interacción y devuelve el aviso. El player da el GPS de prueba; si aún no llegó, el primer toque
// avisa y el segundo ("Guardar sin ubicación") guarda.
async function guardarInteraccion(ctx, app) {
  await sinAvisos(app);
  await (await pl.visible(app, 'btnRegistrar_1')).click();
  let n = await c.esperarNotif(app, /Guardado|ubicación|No se pudo|Ese número/i, 15000);
  if (/ubicación/i.test(n || '') && !/Guardado|No se pudo/.test(n)) {
    ctx.nota('sin GPS en el player: segundo toque en "Guardar sin ubicación"');
    await sinAvisos(app);
    await (await pl.visible(app, 'btnRegistrar_1')).click();
    n = await c.esperarNotif(app, /Guardado|No se pudo|Ese número/i, 15000);
  }
  return n;
}

// APP-04: hace que el SERVIDOR rechace la próxima creación de una interacción, sin tocar datos ni agentes. Se le saca a
// esa única request el vínculo a la cuenta y el plugin InteraccionDelCobrador la rechaza ("la interaccion tiene que tener
// la cuenta"): el rechazo es real (Dataverse, PreOperation) y no se crea nada. Si no encuentra el vínculo, corta la
// request (la app ve un error de red). Devuelve { estado, quitar() }.
const ES_DV = /crm\d*\.dynamics\.com\/api\/data/;
async function rechazarProximaInteraccion(app) {
  const estado = { hecho: '' };
  const sinCuenta = (json) => {
    const o = JSON.parse(json); const k = Object.keys(o).filter((x) => /cuentaid@odata\.bind$/i.test(x));
    if (!k.length) return null;
    k.forEach((x) => delete o[x]); return JSON.stringify(o);
  };
  const handler = async (route) => {
    const r = route.request();
    if (estado.hecho || r.method() !== 'POST') return route.fallback();
    let cuerpo = ''; try { const b = r.postDataBuffer(); cuerpo = b ? b.toString('utf8') : ''; } catch (e) { /* sin cuerpo */ }
    const directa = /\/sanic_cm_tbl_interaccions(\?|$)/i.test(r.url());
    const m = /^POST \S*sanic_cm_tbl_interaccions(\?\S*)? HTTP\/1\.1/im.exec(cuerpo);
    if (!directa && !m) return route.fallback();
    try {
      if (directa) {
        const nuevo = sinCuenta(cuerpo);
        if (nuevo) { estado.hecho = 'request directa sin el vínculo a la cuenta'; return route.fallback({ postData: nuevo }); }
      } else {
        const i = cuerpo.indexOf('{', m.index), j = cuerpo.indexOf('\n--', i), fin = cuerpo.lastIndexOf('}', j < 0 ? cuerpo.length : j);
        const nuevo = i > 0 && fin > i ? sinCuenta(cuerpo.slice(i, fin + 1)) : null;
        if (nuevo) {
          let parte = cuerpo.slice(m.index, i).replace(/Content-Length: \d+/i, `Content-Length: ${Buffer.byteLength(nuevo)}`);
          estado.hecho = 'creación dentro del $batch sin el vínculo a la cuenta';
          return route.fallback({ postData: cuerpo.slice(0, m.index) + parte + nuevo + cuerpo.slice(fin + 1) });
        }
      }
    } catch (e) { /* cuerpo no reconocible: se corta abajo */ }
    estado.hecho = 'request cortada (no se encontró el vínculo a la cuenta)';
    return route.abort('failed');
  };
  await app.ctx.route(ES_DV, handler);
  return { estado, quitar: () => app.ctx.unroute(ES_DV, handler) };
}

// Filas visibles de la galería de cuentas del cliente (galMini → conMini): [{ txt, caja }]
async function filasCuentas(app) {
  const filas = pl.ctl(app, 'conMini').locator('visible=true');
  const n = await filas.count(); const out = [];
  for (let i = 0; i < n; i++) { const f = filas.nth(i); out.push({ f, txt: (await f.innerText()).replace(/\s+/g, ' ').trim() }); }
  return out;
}

// Abre el detalle de la cuenta `e` (fila del cliente con ese tipo de asignación). El clic va al centro de la tarjeta:
// encima hay un botón transparente que lo recibe.
async function abrirCuenta(ctx, app, e, tipo) {
  const filas = await filasCuentas(app);
  const f = filas.find((x) => x.txt.includes(`•••• ${e.ult4}`) && x.txt.includes(tipo));
  ctx.requiere(f, `no está la fila de ${e.clave} ${tipo} en el cliente`);
  await f.f.scrollIntoViewIfNeeded().catch(() => {});
  const caja = await f.f.boundingBox();
  await app.page.mouse.click(caja.x + caja.width / 2, caja.y + caja.height / 2);
  await app.red.quieta(app.page, { quietoMs: 2500, maxMs: 30000, minMs: 2500 });
  ctx.requiere((await esperarPantalla(app, 'scrDetalleCuenta')) === 'scrDetalleCuenta', `no se abrió el detalle de la cuenta ${e.clave}`);
  const t = await lo.texto(app, 'Text1_1');
  ctx.requiere(t === tipo, `el detalle abierto es "${t}", esperaba ${tipo}`);
}

// Intenta activar "Aplica pago": true si aparece el botón "Agregar pago" (el toggle estaba habilitado).
async function intentarActivarPago(app) {
  const tg = await pl.visible(app, 'tpPago');
  await tg.click({ force: true, timeout: 5000 }).catch(() => {});
  await pl.esperar(app, 1500);
  return pl.existeVisible(app, 'btnAddPago');
}

async function f7(ctx) {
  const D = ctx.datos; ctx.requiere(D.cuentas?.every((e) => e.asigId) && D.cuentas.find((e) => e.clave === 'A')?.espId, 'faltan datos de F1..F6');
  const [A, B, Cc] = ['A', 'B', 'C'].map((k) => D.cuentas.find((e) => e.clave === k));
  const mask = (e) => `•••• ${e.ult4}`;
  const cuentaIds = D.cuentas.map((e) => e.id);
  // Lo que User5 ve (seguridad por fila, suplantándolo): activas y, si son Especiales, solo las Aprobadas — la regla de la Cartera.
  const visibles = await datos.getAllComo(U.cob, "sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid,sanic_cm_as_tipo,sanic_cm_as_estadoaprobacion,_sanic_cm_as_cuentaid_value&$filter=sanic_cm_as_estado eq true and (sanic_cm_as_tipo ne 3 or sanic_cm_as_estadoaprobacion eq 3)");
  const delCliente = visibles.filter((x) => cuentaIds.includes(x._sanic_cm_as_cuentaid_value));
  const esperadoCliente = 4; // A Diaria + A Especial + B Incobrable + C Especial (la Especial de B está Rechazada y no se ve)
  ctx.chk('F7-00', delCliente.length === esperadoCliente, `API (suplantando a User5): ve ${delCliente.length} asignaciones del cliente QA (esperado ${esperadoCliente}: A Diaria, A Especial, B Incobrable, C Especial; la Especial rechazada de B no)`);

  const app = await ui.abrirCobrador();
  ctx.capturador = ui.capturador(ctx, app);
  try {
    await ctx.paso('F7-01', 'Cartera', async () => {
      await lo.tocar(app, 'icoLedger');
      // la Cartera carga sus colecciones al entrar: se espera a que el resumen deje de decir "0 cuentas"
      let meta = '';
      await esperar(async () => { meta = await lo.texto(app, 'lblTotalMeta').catch(() => ''); return /^[1-9]\d* cuentas/.test(meta); }, { maxMs: 90000, cadaMs: 3000 });
      const m = meta.match(/^(\d+) cuentas · (\d+) clientes/);
      ctx.chk('F7-01', !!m && Number(m[1]) === visibles.length, `Cartera: "${meta}" = ${visibles.length} asignaciones visibles para User5 según la API`);
      await ui.captura(ctx, app, 'cartera');
    });
    await ctx.paso('F7-02', 'cliente QA', async () => {
      await lo.escribir(app, 'txtBuscar', ctx.nombreCliente, { tab: true });
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 30000, minMs: 2500 });
      const tarjeta = app.frame.getByText(ctx.nombreCliente, { exact: false }).locator('visible=true').first();
      ctx.chk('F7-02', (await tarjeta.count()) > 0, `Cartera: el cliente "${ctx.nombreCliente}" aparece al buscarlo`);
      const cnt = (await pl.ctl(app, 'lblCnt').locator('visible=true').allInnerTexts()).map((s) => s.trim());
      ctx.chk('F7-03', cnt.includes(`${esperadoCliente} cuenta(s)`), `Cartera: la tarjeta del cliente dice "${esperadoCliente} cuenta(s)" (visibles: ${cnt.join(' | ')})`);
      await tarjeta.click({ timeout: 15000, force: true });
      await app.red.quieta(app.page, { quietoMs: 2500, maxMs: 30000, minMs: 2500 });
      ctx.requiere((await esperarPantalla(app, 'scrCliente')) === 'scrCliente', 'no se abrió la pantalla del cliente');
      const tab = await lo.texto(app, 'Button6');
      ctx.chk('F7-04', tab === `Cuentas (${esperadoCliente})`, `Cliente: pestaña "${tab}"`);
      const filas = await filasCuentas(app);
      const con = (e, t) => filas.filter((x) => x.txt.includes(mask(e)) && (!t || x.txt.includes(t))).length;
      ctx.chk('F7-05', con(A, 'Diaria') === 1 && con(A, 'Especial') === 1 && con(B, 'Incobrable') === 1 && con(Cc, 'Especial') === 1 && con(B) === 1,
        `Cliente: A Diaria (${con(A, 'Diaria')}), A Especial (${con(A, 'Especial')}), B Incobrable (${con(B, 'Incobrable')}), C Especial (${con(Cc, 'Especial')}) y NINGUNA Especial de B (filas de B: ${con(B)})`);
      ctx.requiere(filas.some((x) => x.txt.includes(mask(A)) && x.txt.includes('Diaria')), 'no está la fila de la cuenta A Diaria');
      await abrirCuenta(ctx, app, A, 'Diaria');
      const tipo = await lo.texto(app, 'Text1_1');
      ctx.chk('F7-06', tipo === 'Diaria', `Detalle de la cuenta A (${mask(A)}): tipo "${tipo}"`);
      // DAT-26: el CSV QA trae FechaPago "2026-10-20" (aaaa-mm-dd); la app la muestra dd/mm/aaaa sin depender del idioma.
      const fechaPago = await lo.texto(app, 'lblTelVal_3').catch(() => '');
      ctx.chk('F7-06b', fechaPago === '20/10/2026', `Detalle de la cuenta A: fecha de pago "${fechaPago}" (esperado "20/10/2026")`);
    });
    await ctx.paso('F7-07', 'cobro', async () => {
      // 1) Lo pedido: cobrar sobre la cuenta A (Diaria, y además con una Especial aprobada).
      let cobro = A;
      await ui.tocar(app, 'btnRegistrar');
      ctx.requiere((await esperarPantalla(app, 'scrInteraccion')) === 'scrInteraccion', 'no se abrió la pantalla de interacción');
      // Una fila por CUENTA (fix de F7-07): la A tiene Diaria + Especial aprobada y tiene que contar como 1 producto.
      // El texto del botón se calcula con colProdInt (una fila por cuenta): justo al entrar puede verse vacío o con un conteo
      // intermedio. Se espera a que muestre el valor esperado y se mantenga; si nunca llega, el chequeo falla con el texto real.
      const guardar = await ui.esperarEstable(() => lo.texto(app, 'btnRegistrar_1').catch(() => ''), { timeout: 30000, listo: (t) => /Guardar en 1 producto\b/.test(t) });
      ctx.chk('F7-07b', /Guardar en 1 producto\b/.test(guardar), `Cuenta A (2 asignaciones activas): el botón dice "${guardar}" (esperado "Guardar en 1 producto"; antes "2 productos" y se grababan 2 interacciones)`);
      const pagoOk = await intentarActivarPago(app);
      const nota = await lo.texto(app, 'lblSelNota').catch(() => '');
      ctx.chk('F7-07', pagoOk, `Cuenta A (Diaria + Especial aprobada): se puede activar "Aplica pago" en la interacción — ${pagoOk ? 'sí' : `NO: la pantalla pre-marca los 2 productos de la cuenta y dice "${nota}"`}`);
      if (!pagoOk) {
        ctx.hallazgo('DEFECTO (app del cobrador, scrInteraccion): una cuenta con DOS asignaciones activas visibles (Diaria + Especial aprobada, p. ej. la A de esta corrida) no permite registrar pago ni actualizar datos. colProdInt trae una fila por asignación y `sel`/`esActual` se calculan por CUENTA, así que ambas filas quedan marcadas y con el botón deshabilitado (btnRow.DisplayMode = esActual…); tpPago se deshabilita porque CountRows(Filter(colProdInt, sel)) > 1 y no hay forma de desmarcar ninguna. El cobrador no puede cobrar esa cuenta desde la app.');
        await ui.captura(ctx, app, 'defecto-pago-bloqueado-cuenta-A');
        // 2) Para no cortar el recorrido (BackOffice, cierres) el cobro se hace en la cuenta B (Incobrable, una sola asignación visible).
        ctx.nota('el cobro continúa sobre la cuenta B (Incobrable), que no tiene el problema; el recorrido de BackOffice usa esa interacción');
        await ui.tocar(app, 'Icon1'); await ui.tocar(app, 'Icon1');     // ← Detalle, ← Cliente
        ctx.requiere((await esperarPantalla(app, 'scrCliente')) === 'scrCliente', 'no se pudo volver a la pantalla del cliente');
        await abrirCuenta(ctx, app, B, 'Incobrable');
        await ui.tocar(app, 'btnRegistrar');
        ctx.requiere(await intentarActivarPago(app), 'tampoco se puede activar "Aplica pago" en la cuenta B');
        cobro = B;
      }
      D.cobro = { clave: cobro.clave };
      const comentario = `${ctx.nombreCliente} visita 1`;
      const recibo = `${ctx.nombreCliente.replace(' ', '-')}-R1`;
      try {
        await ui.escribir(app, 'txtComentario', comentario, { tab: true });
        if (!(await pl.existeVisible(app, 'btnAddPago'))) await ui.tocar(app, 'tpPago');
        await ui.tocar(app, 'btnAddPago');
        await ui.escribir(app, 'txtMonto', '25', { tab: true });
        await ui.escribir(app, 'txtRecibo', recibo, { tab: true });
      } catch (e) { await ui.captura(ctx, app, 'fallo-cobro'); throw e; }
      await pl.esperar(app, 1500);
      const hint = await lo.texto(app, 'lblHint').catch(() => '');
      ctx.chk('F7-08', hint === '' && (await lo.botonHabilitado(app, 'btnRegistrar_1')), `Interacción sobre ${cobro.clave} (${mask(cobro)}): formulario completo (comentario, pago C$ 25, recibo ${recibo}); hint "${hint}"`);
      // APP-04: primer intento rechazado por el servidor → la app avisa, no dice "✓ Guardado" y no deja pagos huérfanos.
      const rechazo = await rechazarProximaInteraccion(app);
      let n0;
      try { n0 = await guardarInteraccion(ctx, app); } finally { await rechazo.quitar(); }
      ctx.chk('F7-08b', !!rechazo.estado.hecho && /No se pudo guardar la interacción/.test(n0 || '') && !/Guardado/.test(n0 || ''),
        `APP-04: el servidor rechaza la interacción (${rechazo.estado.hecho || 'no se interceptó ninguna creación'}) → aviso "${n0}"`);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      await dormir(5000);
      const huerfI = (await dv.get(`sanic_cm_tbl_interaccions?$select=sanic_cm_tbl_interaccionid&$filter=sanic_cm_in_comentario eq '${comentario}'`)).value.length;
      const huerfP = (await dv.get(`sanic_cm_tbl_pagos?$select=sanic_cm_tbl_pagoid&$filter=sanic_cm_pg_numerorecibo eq '${recibo}'`)).value.length;
      ctx.chk('F7-08c', huerfI === 0 && huerfP === 0, `APP-04: tras el rechazo, Dataverse no tiene la interacción (${huerfI}) ni pagos huérfanos con el recibo ${recibo} (${huerfP})`);
      ctx.chk('F7-08d', await pl.existeVisible(app, 'btnRegistrar_1'), 'APP-04: la app se queda en la interacción con lo cargado, lista para reintentar');
      // Reintento sin rechazo: se guarda UNA vez (F7-10 y F7-12 cuentan exactamente 1 interacción y 1 pago).
      const n = await guardarInteraccion(ctx, app);
      ctx.chk('F7-09', /✓ Guardado/.test(n || ''), `Interacción guardada en el reintento: aviso "${n}"`);
      await app.red.quieta(app.page, { quietoMs: 4000, maxMs: 30000 });
      // Dataverse: interacción y pago
      const inter = await esperar(async () => {
        const r = (await dv.get(`sanic_cm_tbl_interaccions?$select=sanic_cm_tbl_interaccionid,sanic_cm_in_numerointeraccion,sanic_cm_in_comentario,sanic_cm_cd_aplicapago,sanic_cm_in_estadograbacion,sanic_cm_in_nombreagente,_sanic_cm_in_agente_value,_sanic_cm_in_cuentaid_value,_createdby_value&$filter=sanic_cm_in_comentario eq '${comentario}'`)).value;
        return r.length ? r : null;
      }, { maxMs: 40000, cadaMs: 4000 });
      ctx.chk('F7-10', !!inter && inter.length === 1, `Dataverse: 1 interacción "${comentario}" (${inter ? inter.length : 0})`);
      ctx.requiere(inter && inter.length === 1, 'la interacción no llegó a Dataverse');
      const it = inter[0];
      ctx.registrar('interaccion', it.sanic_cm_tbl_interaccionid, `cobro ${cobro.clave}`);
      D.interaccion = { id: it.sanic_cm_tbl_interaccionid, numero: it.sanic_cm_in_numerointeraccion, comentario, recibo };
      ctx.chk('F7-11', it._sanic_cm_in_agente_value === D.agente.id && it._sanic_cm_in_cuentaid_value === cobro.id && it.sanic_cm_cd_aplicapago === true && it.sanic_cm_in_estadograbacion === 1 && it._createdby_value === U.cob && it.sanic_cm_in_nombreagente === AGENTE,
        `interacción ${it.sanic_cm_in_numerointeraccion}: agente "${it.sanic_cm_in_nombreagente}" vinculado, cuenta ${cobro.clave}, aplica pago, Pendiente de grabar, creada por User5`);
      const pagos = await esperar(async () => {
        const r = (await dv.get(`sanic_cm_tbl_pagos?$select=sanic_cm_tbl_pagoid,sanic_cm_pg_numero,sanic_cm_pg_monto,sanic_cm_pg_moneda,sanic_cm_pg_fecha,sanic_cm_pg_numerorecibo,sanic_cm_pg_numerointeraccion,_sanic_cm_pg_cuentaid_value,_sanic_cm_pg_interaccion_value&$filter=sanic_cm_pg_numerorecibo eq '${recibo}'`)).value;
        return r.length ? r : null;
      }, { maxMs: 30000, cadaMs: 3000 });
      const pg = pagos && pagos[0];
      ctx.chk('F7-12', !!pagos && pagos.length === 1, `Dataverse: 1 pago con el recibo ${recibo} (${pagos ? pagos.length : 0})`);
      if (pg) {
        ctx.registrar('pago', pg.sanic_cm_tbl_pagoid, `cobro ${cobro.clave}`);
        ctx.chk('F7-13', pg._sanic_cm_pg_cuentaid_value === cobro.id && pg.sanic_cm_pg_numerointeraccion === it.sanic_cm_in_numerointeraccion && pg._sanic_cm_pg_interaccion_value === it.sanic_cm_tbl_interaccionid,
          `pago ${pg.sanic_cm_pg_numero}: cuenta ${cobro.clave} y N° de interacción (${pg.sanic_cm_pg_numerointeraccion}) completados — plugin CuentaDesdeInteraccion`);
        ctx.chk('F7-14', Number(pg.sanic_cm_pg_monto) === 25 && pg.sanic_cm_pg_moneda === 1 && !!pg.sanic_cm_pg_fecha && pg.sanic_cm_pg_fecha >= ctx.hoy.inicioUTC,
          `pago: monto C$ ${pg.sanic_cm_pg_monto}, moneda NIO, fecha del pago ${pg.sanic_cm_pg_fecha} (de hoy)`);
        D.pago = { id: pg.sanic_cm_tbl_pagoid, numero: pg.sanic_cm_pg_numero };
      }
    });
  } finally { await ui.cerrar(app); }
}

module.exports = { f7 };
