// FASE A — User4 en su rol REAL (Admin y Carga): roles, conteos, búsqueda delegada, reconciliación,
// agentes (solo lectura) y Datos maestros (alta de cliente QA + anti-duplicado).
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const cfg = require('../lib/config');
const c = require('./comun');

module.exports = async function faseA(suite) {
  const app = await c.abrir(suite, 'user4', 'A', 'Admin');
  try {
    await inf.correr('R-05', 'User4 (Admin) ve SOLO la nav Admin e Inicio con su sección', 'Admin', async (e) => {
      await c.verificarRol(e, app, ['Admin'], /^Admin$/);
      e.evid(await pl.foto(app, 'R-05-admin-inicio'));
    });

    await c.errorAlAbrir('E-01a', app, 'Admin');

    let k;
    await inf.correr('D-02a', 'Inicio Admin: KPIs en la PRIMERA carga (frío) tienen valor', 'Admin', async (e) => {
      k = await c.kpisInicio(app, 'Monitoreo de carga', 'scrMonitoreo');
      e.nota(`KPIs frío: ${JSON.stringify(k.frio)} · tibio: ${JSON.stringify(k.tibio)}`);
      const api = { 'DIARIAS FUERA DE CARTERA': await v.desaparecidas(), 'AGENTES SIN USUARIO': await v.agentesSinUsuario() };
      for (const et of Object.keys(api)) e.check(c.num(k.frio[et]) === api[et], `"${et}" en la PRIMERA carga = API`, api[et], k.frio[et]);
      const cob = await v.cobradoHoy(); const subF = (await pl.texto(app, 'lblSubEncCmp').catch(() => '')) || '';
      e.nota(`subtítulo en tibio: ${subF}`);
      e.check(subF.endsWith(v.moneda(cob.nio, cob.usd)), 'Cobrado hoy = Sum API', v.moneda(cob.nio, cob.usd), subF);
      const reqIni = app.red.de('A:scrInicio(frío)').flatMap(x => x.subs.map(s => s.ruta)).filter(r => /asignacions|agentes|pagos/.test(r));
      e.check(reqIni.length > 0, 'el OnVisible de Inicio consultó Dataverse en la primera carga', '>0 consultas KPI', reqIni.length);
    });

    await inf.correr('D-02b', 'Inicio Admin (tibio): KPIs = conteo exacto Web API', 'Admin', async (e) => {
      const desap = await v.desaparecidas(); const ag = await v.agentesSinUsuario();
      const cob = await v.cobradoHoy(); const cargaHoy = await v.cargaHoy();
      e.check(c.num(k.tibio['DIARIAS FUERA DE CARTERA']) === desap, 'DIARIAS FUERA DE CARTERA = API', desap, k.tibio['DIARIAS FUERA DE CARTERA']);
      e.check(c.num(k.tibio['AGENTES SIN USUARIO']) === ag, 'AGENTES SIN USUARIO = API', ag, k.tibio['AGENTES SIN USUARIO']);
      if (cargaHoy === 0) e.check(k.tibio['CARGA DE HOY'] === 'sin carga', 'CARGA DE HOY = "sin carga" (no hay carga hoy)', 'sin carga', k.tibio['CARGA DE HOY']);
      const sub = await pl.texto(app, 'lblSubEncCmp');
      e.check(sub.endsWith(v.moneda(cob.nio, cob.usd)), 'Cobrado hoy = Sum API (Moneda)', v.moneda(cob.nio, cob.usd), sub);
      e.evid(await pl.foto(app, 'D-02b-admin-inicio-tibio'));
    });

    await inf.correr('D-02c', 'Monitoreo: filas de carga hoy, cuentas y clientes = API', 'Admin', async (e) => {
      await c.ir(app, 'Monitoreo de carga', 'scrMonitoreo');
      const kk = await pl.kpis(app);
      const [cta, cli, cargaHoy, cargaApp] = [await v.cuentas(), await v.clientes(), await v.cargaHoy(), await v.cargaHoyComoLaApp()];
      e.check(c.num(kk['CUENTAS TOTALES']) === cta, 'CUENTAS TOTALES = API', v.miles(cta), kk['CUENTAS TOTALES']);
      e.check(c.num(kk['CLIENTES TOTALES']) === cli, 'CLIENTES TOTALES = API', v.miles(cli), kk['CLIENTES TOTALES']);
      e.check(c.num(kk['FILAS DE CARGA HOY']) === cargaHoy, 'FILAS DE CARGA HOY = API (FechaCarga = hoy)', cargaHoy, kk['FILAS DE CARGA HOY']);
      e.nota(`API con la condición que envía el player (ge 00:00 local en UTC): ${cargaApp}`);
      const ok = await pl.existeVisible(app, 'conEstadoOkMon');
      e.check(ok === (cargaHoy > 0), 'card de estado coherente con la carga de hoy', cargaHoy > 0 ? 'verde' : 'alerta', ok ? 'verde' : 'alerta');
      e.evid(await pl.foto(app, 'D-02c-monitoreo'));
    });

    await inf.correr('F-05', 'Reconciliación: % correcto vs API y cierre en lote BLOQUEADO sin carga de hoy', 'Admin', async (e) => {
      await c.ir(app, 'Reconciliación', 'scrReconciliacion');
      const res = await pl.texto(app, 'lblResumenRec');
      const m = res.match(/^(\d+) de (\d+) Diarias activas \((\d+)%\)/);
      const [desap, dia] = [await v.desaparecidas(), await v.diariasActivas()];
      const pct = Math.round(100 * desap / Math.max(1, dia));
      e.check(m && Number(m[1]) === desap, 'numerador (desaparecidas) = API', desap, m && m[1]);
      e.check(m && Number(m[2]) === dia, 'denominador (Diarias activas) = API', dia, m && m[2]);
      e.check(m && Number(m[3]) === pct, 'porcentaje = cálculo API', pct, m && m[3]);
      const cargaHoy = await v.cargaHoy();
      if (cargaHoy === 0) {
        e.check(await pl.existeVisible(app, 'conBloqueoRec'), 'card ⛔ "todavía no llegó la carga" visible', true, await pl.existeVisible(app, 'conBloqueoRec'));
        if (await pl.existeVisible(app, 'chkConfirmarRec')) {
          e.check(await c.chkDeshabilitado(app, 'chkConfirmarRec'), 'casilla de confirmación DESHABILITADA', true, await c.chkDeshabilitado(app, 'chkConfirmarRec'));
          e.check(!(await pl.botonHabilitado(app, 'btnCerrarRec')), 'botón de cierre en lote DESHABILITADO', 'Disabled', 'Edit');
        } else e.nota('conAccionRec no visible (lista vacía) — no hay botón que armar');
      } else e.nota(`Hay ${cargaHoy} filas de carga hoy — el freno lo cubre D-07`);
      e.evid(await pl.foto(app, 'F-05-reconciliacion'));
    });

    await inf.correr('D-02d', 'Cierres: contadores de pestañas = API', 'Admin', async (e) => {
      await c.ir(app, 'Cierres', 'scrCierres');
      const ti = await pl.texto(app, 'btnTabIncobCie'), te = await pl.texto(app, 'btnTabEspCie');
      const [inc, esp] = [await v.incobrablesActivas(), await v.especialesActivas()];
      e.check(c.num(ti.match(/\((\d+)\)/)?.[1]) === inc, 'pestaña Incobrables = API', inc, ti);
      e.check(c.num(te.match(/\((\d+)\)/)?.[1]) === esp, 'pestaña Especiales = API', esp, te);
      e.evid(await pl.foto(app, 'D-02d-cierres'));
    });

    await inf.correr('F-04c', 'Agentes (solo lectura): alerta "sin usuario" = API (conteo y nombres) y abrir la pantalla NO escribe', 'Admin', async (e) => {
      const sel = 'sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre,sanic_cm_ag_emailusuario,_sanic_cm_ag_usuarioid_value,sanic_cm_ag_activo,modifiedon&$orderby=sanic_cm_ag_nombre';
      const antes = await dv.getAll(sel);
      await c.ir(app, 'Agentes y usuarios', 'scrAgentes');
      const notif = await pl.notificaciones(app);
      const escr = app.red.de('A:scrAgentes').flatMap(x => x.subs).filter(s => s.metodo !== 'GET');
      e.check(escr.length === 0, 'solo VER la pantalla no genera escrituras a Dataverse', 0, escr.map(s => `${s.metodo} ${s.ruta}`));
      if (notif.length) e.nota(`notificaciones al abrir (sin tocar nada): ${notif.join(' | ')}`);
      const despues = await dv.getAll(sel);
      const cambiados = despues.filter((d, i) => d.modifiedon !== antes[i]?.modifiedon).map(d => d.sanic_cm_ag_nombre);
      const valoresDistintos = despues.filter((d, i) => d.sanic_cm_ag_emailusuario !== antes[i]?.sanic_cm_ag_emailusuario || d._sanic_cm_ag_usuarioid_value !== antes[i]?._sanic_cm_ag_usuarioid_value || d.sanic_cm_ag_activo !== antes[i]?.sanic_cm_ag_activo).map(d => d.sanic_cm_ag_nombre);
      e.check(valoresDistintos.length === 0, 'los VALORES de usuario/email/activo de los agentes no cambiaron', [], valoresDistintos);
      if (cambiados.length) e.nota(`registros de agente re-escritos (modifiedon cambió) solo por abrir la pantalla: ${cambiados.join(', ')}`);
      const n = await v.agentesSinUsuario(); const nombres = await v.nombresAgentesSinUsuario();
      const tit = await pl.texto(app, 'lblAlertaTituloAge').catch(() => '');
      e.check(c.num(tit.match(/⚠\s*(\d+)/)?.[1]) === n, 'título de la alerta = conteo API', n, tit);
      const det = await pl.texto(app, 'lblAlertaDetalleAge').catch(() => '');
      const faltan = nombres.filter(x => !det.includes(x));
      e.check(faltan.length === 0, 'el detalle lista TODOS los agentes sin usuario de la API', [], faltan);
      // B-08: desvincular salió del OnChange a un botón aparte. Solo se verifica que EXISTE (nunca se pulsa:
      // los agentes son reales).
      const conUsuario = despues.filter(a => a.sanic_cm_ag_emailusuario).length;
      const nDesv = await pl.ctl(app, 'btnDesvincularAge').locator('visible=true').count();
      const txtDesv = nDesv ? (await pl.ctl(app, 'btnDesvincularAge').locator('visible=true').first().innerText()).trim() : null;
      e.check(nDesv === conUsuario && txtDesv === 'Desvincular', 'botón "Desvincular" aparte, uno por agente con usuario (no se pulsa)', `${conUsuario} × "Desvincular"`, `${nDesv} × ${JSON.stringify(txtDesv)}`);
      e.evid(await pl.foto(app, 'F-04c-agentes'));
      // B-04: el combo de usuario de la fila se reemplazó por "Vincular/Cambiar" + UN panel con buscador +
      // galería sobre Users. SEGURIDAD: los agentes son REALES → se abre el panel, se busca y se CANCELA.
      // NUNCA se toca una fila de galUsrAge (tocarla CONFIRMA el vínculo).
      const nVinc = await pl.ctl(app, 'btnVincularAge').locator('visible=true').count();
      e.check(nVinc > 0, 'cada fila de agente tiene su botón Vincular/Cambiar (sin combo en la fila)', '> 0', nVinc);
      if (!nVinc) return;
      const agente = (await pl.ctl(app, 'lblNombreAge').locator('visible=true').first().innerText()).trim();
      app.red.marcar('A:scrAgentes(panel vínculo)');
      await pl.ctl(app, 'btnVincularAge').locator('visible=true').first().click(); await pl.esperar(app, 1500);
      const titPanel = await pl.texto(app, 'lblTituloPanelAge').catch(() => '');
      e.check(titPanel.includes(agente), 'Vincular/Cambiar abre el panel del agente de esa fila', agente, titPanel);
      const galAntes = await pl.existeVisible(app, 'galUsrAge');
      e.check(!galAntes, 'sin búsqueda (< 2 caracteres) la galería de usuarios está oculta', false, galAntes);
      const u = await dv.get(`systemusers(${cfg.usuarios.user3.id})?$select=fullname`);
      const pref = u.fullname.slice(0, Math.min(5, u.fullname.length));
      const r = await c.buscarEnBuscador(app, { buscador: 'txtBuscarUsrAge', galeria: 'galUsrAge', buscar: pref });
      const rutas = app.red.de('A:scrAgentes(panel vínculo)').flatMap(x => x.subs.map(s2 => s2.ruta));
      e.check(rutas.some(ru => /systemusers/.test(ru) && /startswith\(fullname/.test(ru)), 'la búsqueda de usuarios viaja con startswith(fullname, …) en $filter (delegada)', 'startswith en $filter', rutas.filter(ru => /systemusers/.test(ru)).slice(-1)[0] || '(sin request a systemusers)');
      e.check(r.textos.some(t => t.includes(u.fullname)), `buscar "${pref}" muestra al usuario ${u.fullname}`, u.fullname, r.textos.slice(0, 5));
      e.evid(await pl.foto(app, 'F-04c-panel-vinculo'));
      await (await pl.visible(app, 'btnCancelarPanelAge')).click(); await pl.esperar(app, 1200);
      const panelAbierto = await pl.existeVisible(app, 'conPanelAge');
      e.check(!panelAbierto, 'Cancelar cierra el panel', false, panelAbierto);
      const escrPanel = app.red.de('A:scrAgentes(panel vínculo)').flatMap(x => x.subs).filter(s => s.metodo !== 'GET');
      e.check(escrPanel.length === 0, 'abrir, buscar y cancelar el panel NO escribe en Dataverse', 0, escrPanel.map(s => `${s.metodo} ${s.ruta}`));
    });

    await inf.correr('D-03', 'Datos maestros: búsqueda delegada (StartsWith) encuentra un cliente fuera de la 1.ª página', 'Admin', async (e) => {
      await c.ir(app, 'Datos maestros', 'scrMaestros');
      const todos = (await dv.getAll('sanic_cm_tbl_clientes?$select=sanic_cm_cl_nombre,sanic_cm_cl_cif&$orderby=sanic_cm_cl_nombre')).filter(x => !/^QA-E2E/.test(x.sanic_cm_cl_cif));
      const pos = Math.min(todos.length - 1, 700);
      const obj = todos[pos];
      e.nota(`Centinela: "${obj.sanic_cm_cl_nombre}" (CIF ${obj.sanic_cm_cl_cif}) — puesto ${pos + 1} de ${todos.length} por nombre`);
      // 1) por prefijo de CIF
      const pCif = obj.sanic_cm_cl_cif.slice(0, Math.max(4, obj.sanic_cm_cl_cif.length - 2));
      app.red.marcar('A:scrMaestros(búsqueda CIF)');
      await pl.escribir(app, 'txtBuscarMae', pCif);
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 20000 });
      let filas = (await pl.ctl(app, 'lblNomFilaMae').locator('visible=true').allInnerTexts()).map(s => s.trim());
      e.check(filas.includes(obj.sanic_cm_cl_nombre), `búsqueda por CIF "${pCif}" muestra al centinela`, obj.sanic_cm_cl_nombre, filas.slice(0, 6));
      const rCif = app.red.de('A:scrMaestros(búsqueda CIF)').flatMap(x => x.subs.map(s => s.ruta)).filter(r => /sanic_cm_tbl_clientes/.test(r));
      e.check(rCif.some(r => /startswith\(sanic_cm_cl_cif,/.test(r)), 'la request a clientes lleva startswith(sanic_cm_cl_cif, …) en $filter', 'startswith en $filter', rCif.slice(-1)[0]);
      // B-13: DelayOutput = true → 1 consulta al terminar de tipear (antes: 1 por tecla, 13 para un CIF).
      const nBusq = rCif.filter(r => /startswith\(/.test(r)).length;
      e.check(nBusq >= 1 && nBusq <= 3, `B-13: tipear ${pCif.length} caracteres dispara ≤ 3 consultas (DelayOutput)`, '1..3', nBusq);
      e.evid(await pl.foto(app, 'D-03-busqueda-cif'));
      // 2) por prefijo de nombre (dos primeras palabras recortadas)
      const pNom = obj.sanic_cm_cl_nombre.split(' ').slice(0, 2).join(' ').slice(0, 12);
      app.red.marcar('A:scrMaestros(búsqueda nombre)');
      await pl.escribir(app, 'txtBuscarMae', pNom);
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 20000 });
      filas = (await pl.ctl(app, 'lblNomFilaMae').locator('visible=true').allInnerTexts()).map(s => s.trim());
      const nApi = todos.filter(x => x.sanic_cm_cl_nombre.startsWith(pNom) || x.sanic_cm_cl_cif.startsWith(pNom)).length;
      e.check(filas.includes(obj.sanic_cm_cl_nombre) || nApi > filas.length, `búsqueda por nombre "${pNom}" muestra al centinela`, obj.sanic_cm_cl_nombre, filas.slice(0, 8));
      const rNom = app.red.de('A:scrMaestros(búsqueda nombre)').flatMap(x => x.subs.map(s => s.ruta)).filter(r => /sanic_cm_tbl_clientes/.test(r));
      e.check(rNom.some(r => /startswith\(sanic_cm_cl_nombre,/.test(r)), 'la request lleva startswith(sanic_cm_cl_nombre, …) en $filter', 'startswith en $filter', rNom.slice(-1)[0]);
      e.nota(`coincidencias API para "${pNom}": ${nApi} · filas visibles: ${filas.length}`);
      e.evid(await pl.foto(app, 'D-03-busqueda-nombre'));
      await pl.escribir(app, 'txtBuscarMae', '');
    });

    await inf.correr('F-04b', 'Datos maestros: crear cliente QA con teléfono + anti-duplicado de CIF', 'Admin', async (e) => {
      const cif = `QA-E2E-CIF-N${Date.now().toString().slice(-6)}`;
      suite.datos.cifNuevo = cif;
      app.red.marcar('A:scrMaestros(crear)');
      await (await pl.boton(app, 'btnNuevoMae')).click(); await pl.esperar(app, 1500);
      await pl.escribir(app, 'txtCifNvoMae', cif);
      await pl.escribir(app, 'txtNombreNvoMae', 'QA-E2E CLIENTE NUEVO');
      await pl.escribir(app, 'txtTelCelNvoMae', '8777-0000');
      e.check(await pl.botonHabilitado(app, 'btnCrearMae'), 'botón Crear habilitado con CIF+nombre', true, false);
      // Seguridad: el CIF que se va a crear es QA
      if (!/^QA-E2E/.test(cif)) throw new Error('CIF no QA — abortado');
      await (await pl.boton(app, 'btnCrearMae')).click();
      const n1 = await c.esperarNotif(app, /creado|Ya existe/);
      e.check(/Cliente QA-E2E CLIENTE NUEVO creado/.test(n1 || ''), 'Notify de creación', '✓ Cliente … creado', n1);
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 20000 });
      const cli = (await dv.get(`sanic_cm_tbl_clientes?$select=sanic_cm_cl_nombre&$filter=sanic_cm_cl_cif eq '${cif}'`)).value;
      e.check(cli.length === 1, 'Dataverse: 1 cliente con ese CIF', 1, cli.length);
      if (cli[0]) {
        const tel = (await dv.get(`sanic_cm_tbl_telefonos?$select=sanic_cm_tl_numero,sanic_cm_tl_tipo,sanic_cm_tl_cif,_sanic_cm_tl_clienteid_value&$filter=_sanic_cm_tl_clienteid_value eq ${cli[0].sanic_cm_tbl_clienteid}`)).value;
        e.check(tel.length === 1 && tel[0].sanic_cm_tl_numero === '8777-0000' && tel[0].sanic_cm_tl_tipo === 'Celular' && tel[0].sanic_cm_tl_cif === cif,
          'Dataverse: teléfono Celular con lookup + CIF denormalizado', '8777-0000/Celular/' + cif, tel.map(t => `${t.sanic_cm_tl_numero}/${t.sanic_cm_tl_tipo}/${t.sanic_cm_tl_cif}`));
      }
      e.evid(await pl.foto(app, 'F-04b-cliente-creado'));
      // Reintento con el MISMO CIF
      await (await pl.boton(app, 'btnNuevoMae')).click(); await pl.esperar(app, 1500);
      await pl.escribir(app, 'txtCifNvoMae', cif);
      await pl.escribir(app, 'txtNombreNvoMae', 'QA-E2E CLIENTE DUPLICADO');
      await (await pl.boton(app, 'btnCrearMae')).click();
      const n2 = await c.esperarNotif(app, /Ya existe|creado/);
      e.check(/Ya existe un cliente con CIF/.test(n2 || ''), 'Notify anti-duplicado', 'Ya existe un cliente con CIF …', n2);
      e.evid(await pl.foto(app, 'F-04b-anti-duplicado'));
      await app.red.quieta(app.page, { quietoMs: 3000, maxMs: 15000 });
      const n = (await dv.get(`sanic_cm_tbl_clientes?$select=sanic_cm_cl_nombre&$filter=startswith(sanic_cm_cl_cif,'${cif}')`)).value.length;
      e.check(n === 1, 'Dataverse: sigue habiendo 1 solo cliente con ese CIF', 1, n);
      await (await pl.boton(app, 'btnCancelarNvoMae')).click().catch(() => {});
    });
  } finally { await pl.cerrar(app); }
};
