// FASE B — User2 en su rol REAL (team "CM · Solicitantes · DEV"; rol Solicitante: Read/Write de
// asignaciones a nivel USUARIO = solo las propias).
// Cubre: nav y KPIs del rol, mínimo privilegio, buscador de cuentas, crear una Especial por la UI, anti-duplicado,
// falla CONTROLADA de escritura y reactivar una Especial CERRADA propia.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const dv = require('../lib/dataverse');
const cfg = require('../lib/config');
const qa = require('../lib/datosqa');
const c = require('./comun');

const SEL_AS = 'sanic_cm_tbl_asignacionid,sanic_cm_as_clavepan,sanic_cm_as_numero,sanic_cm_as_tipo,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,sanic_cm_as_notas,sanic_cm_as_retironio,sanic_cm_as_motivo,sanic_cm_as_fechacierre,sanic_cm_as_motivorechazo,_sanic_cm_as_solicitadopor_value,_sanic_cm_as_agenteid_value,_sanic_cm_as_resolutorid_value,_ownerid_value';
// Selector de cuenta: buscador + galería; la cuenta elegida se muestra en la tarjeta lblCtaSelNva.
const BUSCA_CTA = { buscador: 'txtBuscarCtaNva', galeria: 'galCtaNva', cambiar: 'btnCambiarCtaNva' };
// La Especial se busca por el lookup a la cuenta (Id), no por el número en claro. `cta` = registro de cuenta QA.
const especialesDe = async (cta) => (await dv.get(`sanic_cm_tbl_asignacions?$select=${SEL_AS}&$filter=_sanic_cm_as_cuentaid_value eq ${cta.sanic_cm_tbl_cuentaid} and sanic_cm_as_tipo eq 3`)).value;
// Solo cuentas QA: nombre QA + último-4 QA ("E2En"). Evita operar una cuenta real aunque el buscador la ofrezca.
const esQA = (cta) => /^QA-E2E-CTA-/.test(cta.sanic_cm_ct_numerodecuenta || '') && /^E2E/.test(cta.sanic_cm_ct_ultimos4 || '');

async function llenarSolicitud(app, cuenta, { nio, motivo, tipo = 'Retiro urgente' }) {
  // El Solicitante identifica la cuenta por los ÚLTIMOS 4 (+ cliente/CIF en la fila); la opción es "•••• E2En".
  const r = await c.elegirEnBuscador(app, { ...BUSCA_CTA, buscar: cuenta.sanic_cm_ct_ultimos4, opcion: qa.mascara(cuenta) });
  if (!r.ok) return r;
  await c.elegirCombo(app, 'cmbTipoNva', { opcion: tipo });
  await pl.escribir(app, 'txtRetiroNioNva', String(nio));
  await pl.escribir(app, 'txtMotivoNva', motivo);
  await pl.esperar(app, 1000);
  return r;
}

// Pide una Especial por la UI para `cuenta` (QA) y devuelve el Notify. Verifica que la cuenta elegida es QA.
async function pedirEspecial(app, cuenta, { nio, motivo, etiqueta }) {
  await c.ir(app, 'Nueva solicitud', etiqueta);
  const r = await llenarSolicitud(app, cuenta, { nio, motivo });
  if (!r.ok) throw new Error(`No se pudo elegir la cuenta ${qa.mascara(cuenta)} en el buscador (filas: ${r.textos.join(' / ')})`);
  const sel = await pl.texto(app, 'lblCtaSelNva');
  if (!sel.includes(qa.mascara(cuenta)) || !esQA(cuenta)) throw new Error(`Seguridad: la cuenta elegida no es QA (${sel})`);
  await (await pl.boton(app, 'btnEnviarNva')).click();
  const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe|No se pudo/, 20000);
  await app.red.quieta(app.page, { quietoMs: 5000, maxMs: 40000 });
  return n;
}

module.exports = async function faseB(suite) {
  const U = 'user2', yo = cfg.usuarios.user2.id;
  const [cta1, , cta3, cta4] = suite.datos.cuentas;
  const ctaQA = cta1; // registro de la cuenta QA 0001 (se muestra como "•••• E2E1")
  const mQA = qa.mascara(ctaQA);
  // Precondiciones de la falla controlada y la reactivación (por API, solo cuentas QA):
  //  · CTA-0003: Especial CERRADA cuyo dueño es el Service Principal → el Solicitante (Read nivel usuario) NO la ve.
  //  · CTA-0004: Especial CERRADA cuyo dueño es User2 → la ve y la app debe REACTIVARLA.
  suite.datos.cerradaAjena = await qa.crearEspecialPendiente(cta3, cfg.usuarios.user3.id, 'X-02 cerrada ajena', { estado: false });
  suite.datos.cerradaPropia = await qa.crearEspecialPendiente(cta4, yo, 'F-02d cerrada propia', { estado: false, owner: yo });
  const app = await c.abrir(suite, U, 'B', 'Solicitante');
  try {
    await inf.correr('R-03', 'User2 (Solicitante) abre la app y ve SOLO su nav e Inicio con su sección', 'Solicitante', async (e) => {
      await c.verificarRol(e, app, ['Solicitante'], /^Solicitante$/);
      e.evid(await pl.foto(app, 'R-03-solicitante-inicio'));
    });

    await c.errorAlAbrir('E-01b', app, 'Solicitante');

    await inf.correr('D-02e', 'Inicio Solicitante: KPIs en la PRIMERA carga = API (B-01) y subtítulo propio (B-05)', 'Solicitante', async (e) => {
      const sub = await pl.texto(app, 'lblSubEncCmp').catch(() => null);
      e.check(sub === 'Tus solicitudes Especiales', 'subtítulo del Solicitante = "Tus solicitudes Especiales" (no "Cobrado hoy")', 'Tus solicitudes Especiales', sub);
      const k = await c.kpisInicio(app, 'Mis solicitudes', 'scrMisSolicitudes');
      e.nota(`frío: ${JSON.stringify(k.frio)} · tibio: ${JSON.stringify(k.tibio)}`);
      const [p, a, r] = [await v.especialesDe(yo, 2), await v.aprobadasActivasDe(yo), await v.especialesDe(yo, 4)];
      for (const [et, val] of [['PENDIENTES', p], ['APROBADAS', a], ['RECHAZADAS', r]]) {
        e.check(c.num(k.frio[et]) === val, `${et} en la PRIMERA carga = API`, val, k.frio[et]);
        e.check(c.num(k.tibio[et]) === val, `${et} (tibio) = API`, val, k.tibio[et]);
      }
    });

    let busq = null;
    await inf.correr('D-04', 'Nueva solicitud: la búsqueda de cuentas (buscador + galería) viaja con StartsWith ($filter en servidor)', 'Solicitante', async (e) => {
      await c.ir(app, 'Nueva solicitud', 'scrNuevaSolicitud');
      // Sin búsqueda (< 2 caracteres) la galería de resultados no se muestra.
      const galAntes = await pl.existeVisible(app, 'galCtaNva');
      e.check(!galAntes, 'sin búsqueda la galería de cuentas está oculta (no lista la tabla entera)', false, galAntes);
      app.red.marcar('B:scrNuevaSolicitud(buscador)');
      // Se busca por NOMBRE del cliente (StartsWith); el número de cuenta no es buscable.
      busq = await c.elegirEnBuscador(app, { ...BUSCA_CTA, buscar: 'QA-E2E CLIENTE', opcion: mQA });
      const ents = app.red.de('B:scrNuevaSolicitud(buscador)');
      const i = ents.findIndex(x => x.subs.some(s2 => /sanic_cm_tbl_cuentas.*startswith\(sanic_cm_ct_nombrecliente,'QA-E2E CLIENTE/.test(s2.ruta)));
      e.check(i >= 0, "la request lleva startswith(sanic_cm_ct_nombrecliente, 'QA-E2E CLIENTE') en $filter", 'startswith en $filter', ents.flatMap(x => x.subs.map(s2 => s2.ruta)).slice(-1)[0]);
      const rutasCta = ents.flatMap(x => x.subs.map(s2 => s2.ruta)).filter(r => /^sanic_cm_tbl_cuentas\?/.test(r));
      e.check(!rutasCta.some(r => /\$filter=[^&]*sanic_cm_ct_numerodecuenta/.test(r)), 'ningún $filter busca por sanic_cm_ct_numerodecuenta', 'sin numerodecuenta en $filter', rutasCta.slice(-1)[0]);
      e.nota(`¿$select de cuentas incluye el nombre principal sanic_cm_ct_numerodecuenta? ${rutasCta.some(r => /\$select=[^&]*sanic_cm_ct_numerodecuenta/.test(r))} (hasta el paso 8 esa columna aún guarda el PAN; el paso 8 lo enmascara)`);
      const filas = i >= 0 ? ents[i].respuestas?.[0]?.filas : null;
      e.check(filas === suite.datos.cuentas.length, `el servidor devuelve las ${suite.datos.cuentas.length} cuentas QA`, suite.datos.cuentas.length, filas);
      e.evid(await pl.foto(app, 'D-04-buscador-cuentas'));
    });

    await inf.correr('F-02a', 'Nueva solicitud: la galería de resultados MUESTRA número + nombre del cliente y deja elegir la cuenta (B-04)', 'Solicitante', async (e) => {
      e.nota(`filas tras buscar: ${busq.textos.length} — ${JSON.stringify(busq.textos.slice(0, 3))}`);
      e.check(busq.textos.length > 0 && busq.textos.every(t => t.length > 0), 'las filas de resultados tienen texto (no vacías)', 'texto visible', JSON.stringify(busq.textos.slice(0, 3)));
      e.check(busq.textos.some(t => t.includes(mQA) && t.includes('QA-E2E CLIENTE UNO') && t.includes('QA-E2E-CIF-001')), 'la fila muestra tarjeta enmascarada (últimos 4) + cliente + CIF', `${mQA} + QA-E2E CLIENTE UNO + QA-E2E-CIF-001`, JSON.stringify(busq.textos.slice(0, 4)));
      e.check(!busq.textos.some(t => /\d{13,}/.test(t.replace(/[\s-]/g, ''))), 'ninguna fila de resultados trae 13+ dígitos seguidos', 'sin PAN', busq.textos.map(t => /\d{13,}/.test(t.replace(/[\s-]/g, ''))));
      e.check(busq.ok, `la cuenta ${mQA} se puede elegir`, mQA, busq.sinResultados ? 'galería sin resultados' : busq.textos.slice(0, 4));
      e.check(!busq.sinResultados, 'la galería NO queda vacía', false, busq.sinResultados);
      const tarjeta = busq.ok ? await pl.texto(app, 'lblCtaSelNva').catch(() => '') : '';
      e.check(tarjeta.includes(mQA) && tarjeta.includes('QA-E2E CLIENTE UNO'), 'la tarjeta de cuenta elegida muestra tarjeta enmascarada + cliente', `${mQA} · QA-E2E CLIENTE UNO`, tarjeta);
      const saldo = busq.ok ? await pl.texto(app, 'lblPrevSaldoNva').catch(() => '') : '';
      e.check(/Saldo vencido: .*\d/.test(saldo), 'la tarjeta muestra el saldo vencido (columnas traídas por la galería)', 'Saldo vencido: …', saldo);
      e.check(!(await pl.existeVisible(app, 'galCtaNva')), 'al elegir, la galería de resultados se oculta', false, true);
    });

    await inf.correr('D-04b', 'Nueva solicitud (D5): búsqueda por ÚLTIMOS 4 (igualdad exacta) y por CIF viajan en $filter; sin 4 caracteres no hay búsqueda por último-4', 'Solicitante', async (e) => {
      if (!busq || !busq.ok) { e.bloquear('depende de elegir la cuenta en el buscador (F-02a)'); return; }
      // a) CIF (StartsWith): las 4 cuentas QA comparten el CIF del cliente QA. Se deja el buscador abierto sin elegir.
      app.red.marcar('B:scrNuevaSolicitud(cif)');
      const rCif = await c.buscarEnBuscador(app, { ...BUSCA_CTA, buscar: 'QA-E2E-CIF', cambiar: BUSCA_CTA.cambiar });
      const rutasCif = app.red.de('B:scrNuevaSolicitud(cif)').flatMap(x => x.subs.map(s2 => s2.ruta));
      e.check(rutasCif.some(r => /sanic_cm_tbl_cuentas.*startswith\(sanic_cm_ct_cifcliente,'QA-E2E-CIF/.test(r)), "la request lleva startswith(sanic_cm_ct_cifcliente, 'QA-E2E-CIF') en $filter", 'startswith en $filter', rutasCif.slice(-1)[0]);
      e.check(rCif.textos.length > 0 && rCif.textos.some(t => t.includes(mQA)), 'la búsqueda por CIF ofrece la cuenta QA con su tarjeta enmascarada', mQA, JSON.stringify(rCif.textos.slice(0, 4)));
      // b) Últimos 4 exactos ("E2E1"): igualdad en servidor, 1 sola fila.
      app.red.marcar('B:scrNuevaSolicitud(ult4)');
      const r4 = await c.elegirEnBuscador(app, { ...BUSCA_CTA, buscar: ctaQA.sanic_cm_ct_ultimos4, opcion: mQA });
      const ents = app.red.de('B:scrNuevaSolicitud(ult4)');
      const i = ents.findIndex(x => x.subs.some(s2 => /sanic_cm_tbl_cuentas.*sanic_cm_ct_ultimos4 eq 'E2E1'/.test(s2.ruta)));
      e.check(i >= 0, "la request lleva sanic_cm_ct_ultimos4 eq 'E2E1' en $filter (delegado)", 'eq en $filter', ents.flatMap(x => x.subs.map(s2 => s2.ruta)).slice(-1)[0]);
      e.check(r4.ok, `se puede elegir la cuenta ${mQA} tecleando sus últimos 4`, mQA, r4.textos.slice(0, 4));
      // c) Con 3 caracteres NO se envía el último-4 real (centinela §§): no se descarga la tabla.
      await (await pl.visible(app, 'btnCambiarCtaNva')).click(); await pl.esperar(app, 1000);
      app.red.marcar('B:scrNuevaSolicitud(ult4-3car)');
      await c.buscarEnBuscador(app, { ...BUSCA_CTA, buscar: 'E2E' });
      const r3 = app.red.de('B:scrNuevaSolicitud(ult4-3car)').flatMap(x => x.subs.map(s2 => s2.ruta)).filter(r => /^sanic_cm_tbl_cuentas\?/.test(r));
      e.check(!r3.some(r => /sanic_cm_ct_ultimos4 eq 'E2E'/.test(r)), 'con 3 caracteres no se consulta por último-4', 'sin ultimos4 eq E2E', r3.slice(-1)[0] || '(sin request)');
      // d) Vuelve a quedar elegida la cuenta QA 0001 para los escenarios siguientes.
      const r5 = await c.elegirEnBuscador(app, { ...BUSCA_CTA, buscar: ctaQA.sanic_cm_ct_ultimos4, opcion: mQA });
      e.check(r5.ok, 'queda elegida la cuenta QA 0001 para F-02', mQA, r5.textos.slice(0, 4));
    });

    await inf.correr('F-02', 'Solicitante crea una Especial QA (retiro NIO) por la UI y aparece en Mis solicitudes', 'Solicitante', async (e) => {
      const motivo = 'QA-E2E solicitud F-02';
      if (!busq || !busq.ok) {
        e.check(false, 'crear la Especial desde la UI', 'Especial creada con el formulario', 'imposible: el buscador de cuentas no deja elegir (F-02a)');
        // Respaldo para no bloquear C/F/G: la MISMA Especial por API (dueño User2).
        suite.datos.especialF02 = await qa.crearEspecialPendiente(cta1, yo, 'F-02 (API)', { notas: `Retiro urgente — ${motivo}`, nio: 350, owner: yo });
        e.nota(`Especial ${suite.datos.especialF02.sanic_cm_as_numero} creada POR API para continuar`);
        return;
      }
      await c.elegirCombo(app, 'cmbTipoNva', { opcion: 'Retiro urgente' });
      await pl.escribir(app, 'txtRetiroNioNva', '350');
      await pl.escribir(app, 'txtMotivoNva', motivo);
      await pl.esperar(app, 1000);
      const sel = await pl.texto(app, 'lblCtaSelNva');
      if (!sel.includes(mQA) || !esQA(ctaQA)) throw new Error(`Seguridad: la cuenta elegida no es QA (${sel})`);
      e.check((await pl.texto(app, 'lblHintNva')).includes('Todo listo'), 'hint "✓ Todo listo"', '✓ Todo listo', await pl.texto(app, 'lblHintNva'));
      e.check(await pl.botonHabilitado(app, 'btnEnviarNva'), 'Enviar habilitado', true, false);
      app.red.marcar('B:scrNuevaSolicitud(enviar)');
      await (await pl.boton(app, 'btnEnviarNva')).click();
      const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe|No se pudo/, 20000);
      e.check(/Solicitud enviada/.test(n || ''), 'Notify de envío', '✨ Solicitud enviada …', n);
      await app.red.quieta(app.page, { quietoMs: 6000, maxMs: 45000 });
      const rows = await especialesDe(ctaQA);
      e.check(rows.length === 1, 'Dataverse: 1 Especial para la cuenta QA', 1, rows.length);
      const a = rows[0] || {};
      suite.datos.especialF02 = a;
      e.check(a.sanic_cm_as_estadoaprobacion === 2, 'estado de aprobación = Pendiente aprobacion (2)', 2, a.sanic_cm_as_estadoaprobacion);
      e.check(a.sanic_cm_as_estado === true, '[AS] Estado = activo', true, a.sanic_cm_as_estado);
      e.check(a.sanic_cm_as_clavepan === ctaQA.sanic_cm_ct_clavepan, 'la clave PAN de la asignación la completó el plugin ClaveDesdeCuenta (la app no la escribe)', ctaQA.sanic_cm_ct_clavepan, a.sanic_cm_as_clavepan);
      e.check(a._sanic_cm_as_solicitadopor_value === yo, 'Solicitado por = User2', yo, a._sanic_cm_as_solicitadopor_value);
      e.check(a._ownerid_value === yo, 'owner = User2 (la creó la app con su identidad)', yo, a._ownerid_value);
      e.check(a.sanic_cm_as_notas === `Retiro urgente — ${motivo}`, 'Notas = "<tipo> — <motivo>"', `Retiro urgente — ${motivo}`, a.sanic_cm_as_notas);
      e.check(Number(a.sanic_cm_as_retironio) === 350, 'Retiro NIO = 350', 350, a.sanic_cm_as_retironio);
      e.check(!a._sanic_cm_as_agenteid_value, 'nace SIN agente', null, a._sanic_cm_as_agenteid_value);
      await pl.esperar(app, 3000);
      const filas = (await pl.ctl(app, 'lblCuentaFilaSol').locator('visible=true').allInnerTexts()).map(s => s.trim());
      e.check(filas.includes(mQA), 'navegó a Mis solicitudes y la muestra (enmascarada)', mQA, filas.slice(0, 5));
      e.evid(await pl.foto(app, 'F-02-mis-solicitudes'));
    });

    await inf.correr('F-02b', 'Anti-duplicado: segunda solicitud para la misma cuenta (Especial ACTIVA) → aviso y NO se crea', 'Solicitante', async (e) => {
      if (!busq || !busq.ok) { e.bloquear('depende de elegir la cuenta en el buscador (F-02a)'); return; }
      const n = await pedirEspecial(app, ctaQA, { nio: 10, motivo: 'QA-E2E duplicado F-02b', etiqueta: 'scrNuevaSolicitud(dup)' });
      e.check(/Ya existe una Especial ACTIVA/.test(n || ''), 'Notify anti-duplicado', 'Ya existe una Especial ACTIVA …', n);
      e.evid(await pl.foto(app, 'F-02b-anti-duplicado'));
      const k = (await especialesDe(ctaQA)).length;
      e.check(k === 1, 'Dataverse: sigue habiendo 1 sola Especial', 1, k);
    });

    // Una Especial cerrada ajena no hace fallar el envío (SolicitarEspecial la REACTIVA a nombre del Solicitante),
    // así que la falla controlada se provoca con una regla del servidor: la API rechaza notas con 13 o más dígitos
    // seguidos (podrían ser un número de tarjeta). La secuencia se arma en tiempo de ejecución y no es una tarjeta.
    await inf.correr('X-02', 'Falla CONTROLADA de escritura (B-03): notas con 13 dígitos seguidos → el servidor rechaza, la app muestra el error y NO da por hecho', 'Solicitante', async (e) => {
      if (!busq || !busq.ok) { e.bloquear('depende del buscador de cuentas (F-02a)'); return; }
      const cta = cta3; const mCta = qa.mascara(cta);
      const n = await pedirEspecial(app, cta, { nio: 77, motivo: `QA-E2E falla controlada X-02 ref ${'1'.repeat(13)}`, etiqueta: 'scrNuevaSolicitud(falla controlada)' });
      e.check(new RegExp(`No se pudo enviar la solicitud de la cuenta ${mCta}`).test(n || ''), 'Notify de ERROR con el detalle (IfError)', `No se pudo enviar la solicitud de la cuenta ${mCta}: …`, (n || '').replace(/\d{13,}/g, '[13+ dígitos]'));
      e.check(!/Solicitud enviada/.test(n || ''), 'NO anuncia "Solicitud enviada"', 'sin ✨', (n || '').replace(/\d{13,}/g, '[13+ dígitos]'));
      const sigue = await pl.existeVisible(app, 'lblCtaSelNva');
      const sel = sigue ? await pl.texto(app, 'lblCtaSelNva').catch(() => '') : '';
      e.check(sigue && sel.includes(mCta), 'se queda en Nueva solicitud con el formulario intacto (no Reset, no Navigate)', `lblCtaSelNva = ${mCta}`, sigue ? sel : 'navegó fuera');
      e.evid(await pl.foto(app, 'X-02-falla-controlada'));
      const rows = await especialesDe(cta);
      e.check(rows.length === 1 && rows[0].sanic_cm_as_estado === false, 'Dataverse: nada cambió (1 Especial, sigue CERRADA)', '1 · cerrada', `${rows.length} · ${rows.map(r => r.sanic_cm_as_estado)}`);
    });

    await inf.correr('F-02f', 'Especial CERRADA de otra persona que el Solicitante no ve → la app la REACTIVA a su nombre (DAT-14, SolicitarEspecial)', 'Solicitante', async (e) => {
      if (!busq || !busq.ok) { e.bloquear('depende del buscador de cuentas (F-02a)'); return; }
      const cta = cta3; const previa = suite.datos.cerradaAjena;
      const n = await pedirEspecial(app, cta, { nio: 66, motivo: 'QA-E2E reactivación ajena F-02f', etiqueta: 'scrNuevaSolicitud(reactivar ajena)' });
      e.check(/Solicitud enviada/.test(n || '') && /reactiv/i.test(n || ''), 'Notify "Solicitud enviada (se reactivó …)"', '✨ … se reactivó …', n);
      const rows = await especialesDe(cta);
      e.check(rows.length === 1 && rows[0].sanic_cm_tbl_asignacionid === previa.sanic_cm_tbl_asignacionid, 'Dataverse: el MISMO registro, sin duplicar', previa.sanic_cm_as_numero, rows.map(r => r.sanic_cm_as_numero || r.sanic_cm_tbl_asignacionid));
      const a = await dv.get(`sanic_cm_tbl_asignacions(${previa.sanic_cm_tbl_asignacionid})?$select=sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,_sanic_cm_as_solicitadopor_value,_ownerid_value`);
      e.check(a.sanic_cm_as_estado === true && a.sanic_cm_as_estadoaprobacion === 2, 'activa y Pendiente de aprobación', 'true · 2', `${a.sanic_cm_as_estado} · ${a.sanic_cm_as_estadoaprobacion}`);
      e.check(a._sanic_cm_as_solicitadopor_value === yo && a._ownerid_value === yo, 'solicitante y dueño = User2', yo, `${a._sanic_cm_as_solicitadopor_value} / ${a._ownerid_value}`);
      e.evid(await pl.foto(app, 'F-02f-reactivada-ajena'));
    });

    await inf.correr('F-02d', 'Re-solicitar Especial sobre cuenta con Especial CERRADA PROPIA → la app REACTIVA el mismo registro (B-11)', 'Solicitante', async (e) => {
      if (!busq || !busq.ok) { e.bloquear('depende del buscador de cuentas (F-02a)'); return; }
      const cta = cta4; const previa = suite.datos.cerradaPropia;
      const n = await pedirEspecial(app, cta, { nio: 88, motivo: 'QA-E2E reactivación F-02d', etiqueta: 'scrNuevaSolicitud(reactivar)' });
      e.check(/Solicitud enviada/.test(n || ''), 'Notify de envío (sin 412)', '✨ Solicitud enviada …', n);
      const rows = await especialesDe(cta);
      const a = rows[0] || {};
      e.check(rows.length === 1 && a.sanic_cm_tbl_asignacionid === previa.sanic_cm_tbl_asignacionid, 'Dataverse: sigue 1 sola Especial y es el MISMO registro (reactivado, no duplicado)', previa.sanic_cm_as_numero, rows.map(r => r.sanic_cm_as_numero));
      e.check(a.sanic_cm_as_estado === true && a.sanic_cm_as_estadoaprobacion === 2, 'reactivada: Estado activo + Pendiente aprobación', 'true · 2', `${a.sanic_cm_as_estado} · ${a.sanic_cm_as_estadoaprobacion}`);
      e.check(a.sanic_cm_as_notas === 'Retiro urgente — QA-E2E reactivación F-02d' && Number(a.sanic_cm_as_retironio) === 88, 'datos nuevos (notas + retiro)', 'Retiro urgente — QA-E2E reactivación F-02d · 88', `${a.sanic_cm_as_notas} · ${a.sanic_cm_as_retironio}`);
      e.check(!a.sanic_cm_as_motivo && !a.sanic_cm_as_fechacierre && !a._sanic_cm_as_agenteid_value && !a._sanic_cm_as_resolutorid_value, 'limpia el ciclo anterior (motivo, fecha de cierre, agente, resolutor)', 'vacíos', `${a.sanic_cm_as_motivo} · ${a.sanic_cm_as_fechacierre} · ${a._sanic_cm_as_agenteid_value} · ${a._sanic_cm_as_resolutorid_value}`);
      e.evid(await pl.foto(app, 'F-02d-reactivada'));
    });

    await inf.correr('D-05', 'Mínimo privilegio (B-05): en toda la sesión del Solicitante no hay consultas a Pagos ni Carga diaria ni respuestas 403', 'Solicitante', async (e) => {
      const rutas = app.red.entradas.flatMap(x => x.subs.map(s2 => s2.ruta));
      const prohibidas = rutas.filter(r => /^sanic_cm_tbl_(pagos|cargadiarias)/.test(r));
      e.check(prohibidas.length === 0, 'sin consultas a sanic_cm_tbl_pagos / sanic_cm_tbl_cargadiarias', 0, prohibidas.slice(0, 4));
      const e403 = c.erroresRed(app).filter(x => /^403/.test(x));
      e.check(e403.length === 0, 'sin respuestas 403 en toda la sesión', 0, e403.slice(0, 4));
    });
  } finally {
    await pl.cerrar(app);
  }
};

module.exports.llenarSolicitud = llenarSolicitud;
module.exports.pedirEspecial = pedirEspecial;
module.exports.especialesDe = especialesDe;
