// Fase 4, Solicitante (User2): pide Especiales · Fase 10, Solicitante (User2): seguimiento (Mis solicitudes e Inicio).
// La cuenta se elige por sus ÚLTIMOS 4 y se confirma por el NOMBRE del cliente QA: una búsqueda por últimos 4 también
// devuelve cuentas reales de otros clientes, así que jamás se toma "la primera fila".
const dv = require('../../backoffice-canvas/lib/dataverse');
const v = require('../../backoffice-canvas/lib/verdad');
const { AGENTE, U } = require('../lib/ctx');
const ui = require('../lib/ui');
const { pl, c } = ui;

const BUSCA_CTA = { buscador: 'txtBuscarCtaNva', galeria: 'galCtaNva', cambiar: 'btnCambiarCtaNva' };
const SEL_AS = 'sanic_cm_tbl_asignacionid,sanic_cm_as_numero,sanic_cm_as_tipo,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,sanic_cm_as_notas,sanic_cm_as_retironio,sanic_cm_as_clavepan,sanic_cm_as_motivorechazo,_sanic_cm_as_solicitadopor_value,_sanic_cm_as_agenteid_value,_sanic_cm_as_resolutorid_value,_ownerid_value';
const especialesDe = async (e) => (await dv.get(`sanic_cm_tbl_asignacions?$select=${SEL_AS}&$filter=_sanic_cm_as_cuentaid_value eq ${e.id} and sanic_cm_as_tipo eq 3`)).value;

// Pide una Especial desde la UI para la cuenta `e` del cliente de la corrida; devuelve el aviso (Notify).
async function pedirEspecial(ctx, app, e, { nio, motivo, etiqueta }) {
  await ui.ir(app, 'Nueva solicitud', etiqueta);
  const mask = `•••• ${e.ult4}`;
  // La galería del buscador a veces sigue mostrando la lista anterior (la consulta tarda más que la espera fija de
  // buscarEnBuscador): se repite la búsqueda hasta 3 veces antes de dar la precondición por rota.
  let r;
  for (let i = 0; i < 3; i++) {
    r = await c.elegirEnBuscador(app, { ...BUSCA_CTA, buscar: e.ult4, opcion: ctx.nombreCliente });
    if (r.ok) break;
    await pl.esperar(app, 3000);
  }
  ctx.requiere(r.ok, `no se pudo elegir la cuenta ${mask} de "${ctx.nombreCliente}" (filas: ${r.textos.slice(0, 4).join(' / ')})`);
  const sel = await pl.texto(app, 'lblCtaSelNva');
  ctx.requiere(sel.includes(mask) && sel.includes(ctx.nombreCliente), `Seguridad: la cuenta elegida no es la QA (${sel.slice(0, 80)})`);
  await c.elegirCombo(app, 'cmbTipoNva', { opcion: 'Retiro urgente' });
  await pl.escribir(app, 'txtRetiroNioNva', String(nio));
  await pl.escribir(app, 'txtMotivoNva', motivo);
  await pl.esperar(app, 1000);
  await (await pl.boton(app, 'btnEnviarNva')).click();
  const n = await c.esperarNotif(app, /Solicitud enviada|Ya existe|No se pudo/, 20000);
  await app.red.quieta(app.page, { quietoMs: 5000, maxMs: 40000 });
  return n;
}

// ── Fase 4 ─────────────────────────────────────────────────────────────────────────────────────────
async function f4(ctx) {
  const D = ctx.datos; ctx.requiere(D.cuentas?.every((e) => e.id), 'F1 no dejó las cuentas');
  const [A, B] = ['A', 'B'].map((k) => D.cuentas.find((e) => e.clave === k));
  const app = await ui.abrirBackoffice('user2', 'F4');
  ctx.capturador = ui.capturador(ctx, app);
  try {
    // A: Especial sobre la cuenta Diaria
    await ctx.paso('F4-01', 'Especial sobre A', async () => {
      const motivo = `${ctx.nombreCliente} pedido`;
      const n = await pedirEspecial(ctx, app, A, { nio: 350, motivo, etiqueta: 'scrNuevaSolicitud(A)' });
      ctx.chk('F4-01', /Solicitud enviada/.test(n || ''), `Nueva solicitud sobre A (****${A.ult4}): aviso "${n}"`);
      const rows = await especialesDe(A);
      const a = rows[0] || {};
      ctx.chk('F4-02', rows.length === 1 && a.sanic_cm_as_estadoaprobacion === 2 && a.sanic_cm_as_estado === true && !a._sanic_cm_as_agenteid_value && a._sanic_cm_as_solicitadopor_value === U.sol,
        `Dataverse: 1 Especial ${a.sanic_cm_as_numero} Pendiente (2), activa, SIN agente, solicitada por User2 (filas ${rows.length}, estado ${a.sanic_cm_as_estadoaprobacion}, agente ${a._sanic_cm_as_agenteid_value || 'ninguno'})`);
      ctx.chk('F4-03', a.sanic_cm_as_notas === `Retiro urgente — ${motivo}` && Number(a.sanic_cm_as_retironio) === 350 && a.sanic_cm_as_clavepan === A.clavepan && a._ownerid_value === U.sol,
        `Dataverse: notas "${a.sanic_cm_as_notas}", retiro C$ ${a.sanic_cm_as_retironio}, clavepan del plugin ClaveDesdeCuenta = la de la cuenta, dueño User2`);
      A.espId = a.sanic_cm_tbl_asignacionid; A.espNum = a.sanic_cm_as_numero;
      ctx.registrar('asignacion', A.espId, 'especial A');
      await ui.captura(ctx, app, 'mis-solicitudes-A');
    });
    // Reintento sobre la misma cuenta: anti-duplicado
    await ctx.paso('F4-04', 'anti-duplicado', async () => {
      const n = await pedirEspecial(ctx, app, A, { nio: 10, motivo: `${ctx.nombreCliente} duplicado`, etiqueta: 'scrNuevaSolicitud(dup)' });
      ctx.chk('F4-04', /Ya existe una Especial ACTIVA/.test(n || ''), `Reintento sobre A: aviso anti-duplicado ("${n}")`);
      const k = (await especialesDe(A)).length;
      ctx.chk('F4-05', k === 1, `Dataverse: sigue habiendo 1 sola Especial para A (${k})`);
    });
    // B: segunda Especial, la que se va a rechazar
    await ctx.paso('F4-06', 'Especial sobre B', async () => {
      const motivo = `${ctx.nombreCliente} rechazar`;
      const n = await pedirEspecial(ctx, app, B, { nio: 120, motivo, etiqueta: 'scrNuevaSolicitud(B)' });
      ctx.chk('F4-06', /Solicitud enviada/.test(n || ''), `Nueva solicitud sobre B (****${B.ult4}): aviso "${n}"`);
      const rows = await especialesDe(B);
      const b = rows[0] || {};
      ctx.chk('F4-07', rows.length === 1 && b.sanic_cm_as_estadoaprobacion === 2 && !b._sanic_cm_as_agenteid_value && b._sanic_cm_as_solicitadopor_value === U.sol,
        `Dataverse: 1 Especial ${b.sanic_cm_as_numero} Pendiente sin agente, solicitada por User2`);
      B.espId = b.sanic_cm_tbl_asignacionid; B.espNum = b.sanic_cm_as_numero;
      ctx.registrar('asignacion', B.espId, 'especial B');
    });
    // Mis solicitudes: las dos Pendientes
    await ctx.paso('F4-08', 'Mis solicitudes', async () => {
      ctx.requiere(A.espNum && B.espNum, 'no se crearon las dos Especiales');
      await ui.ir(app, 'Mis solicitudes', 'scrMisSolicitudes');
      const lista = await ui.textosFilas(app, 'galSolSol');
      const fa = lista.find((t) => t.includes(A.espNum)), fb = lista.find((t) => t.includes(B.espNum));
      ctx.chk('F4-08', !!fa && !!fb && /Pendiente/.test(fa) && /Pendiente/.test(fb) && !fa.includes('🏍️') && !fb.includes('🏍️'),
        `Mis solicitudes: ${A.espNum} y ${B.espNum} aparecen Pendientes y sin localizador`);
      await ui.ir(app, 'Inicio', 'scrInicio(tibio)');
      const k = await pl.kpis(app);
      const pend = await v.especialesDe(U.sol, 2);
      ctx.chk('F4-09', c.num(k['PENDIENTES']) === pend && pend >= 2, `Inicio de Solicitante: PENDIENTES = API (${k['PENDIENTES']} vs ${pend})`);
    });
  } finally { await ui.cerrar(app); }
}

// ── Fase 10 ────────────────────────────────────────────────────────────────────────────────────────
async function f10(ctx) {
  const D = ctx.datos; const [A, B] = ['A', 'B'].map((k) => D.cuentas.find((e) => e.clave === k));
  ctx.requiere(A?.espNum && B?.espNum, 'faltan las Especiales de F4');
  const app = await ui.abrirBackoffice('user2', 'F10');
  ctx.capturador = ui.capturador(ctx, app);
  try {
    await ctx.paso('F10-01', 'Mis solicitudes', async () => {
      await ui.ir(app, 'Mis solicitudes', 'scrMisSolicitudes');
      const f = await ui.filas(app, 'galSolSol').lista();
      const lista = (await f.allInnerTexts()).map((s) => s.replace(/\s+/g, ' ').trim());
      const fa = lista.find((t) => t.includes(A.espNum)), fb = lista.find((t) => t.includes(B.espNum));
      ctx.chk('F10-01', !!fa && /Aprobada/.test(fa) && fa.includes(AGENTE), `Mis solicitudes: ${A.espNum} (cuenta A) Aprobada con el agente "${AGENTE}" — fila: "${(fa || '(no está)').slice(0, 130)}"`);
      ctx.chk('F10-02', !!fb && /Rechazada/.test(fb) && !fb.includes('🏍️'), `Mis solicitudes: ${B.espNum} (cuenta B) Rechazada, sin localizador — fila: "${(fb || '(no está)').slice(0, 130)}"`);
      // El motivo del rechazo se ve en el panel de la solicitud rechazada.
      const fila = f.filter({ hasText: B.espNum }).first();
      await fila.locator('[data-control-name="btnFilaSol"]').first().click({ force: true }); await pl.esperar(app, 1500);
      const motivo = await pl.texto(app, 'lblRechSol').catch(() => '');
      ctx.chk('F10-03', motivo.includes(`${ctx.nombreCliente} no corresponde`), `panel de la rechazada: "${motivo}"`);
      await ui.captura(ctx, app, 'mis-solicitudes');
      await (await pl.visible(app, 'btnCerrarPanSol')).click().catch(() => {});
    });
    await ctx.paso('F10-04', 'KPIs de Inicio', async () => {
      await ui.ir(app, 'Inicio', 'scrInicio(tibio)');
      const k = await pl.kpis(app);
      const [p, a, r] = [await v.especialesDe(U.sol, 2), await v.especialesDe(U.sol, 3), await v.especialesDe(U.sol, 4)];
      ctx.chk('F10-04', c.num(k['PENDIENTES']) === p && c.num(k['APROBADAS']) === a && c.num(k['RECHAZADAS']) === r && a >= 1 && r >= 1,
        `Inicio Solicitante: PENDIENTES/APROBADAS/RECHAZADAS = API (${k['PENDIENTES']}/${k['APROBADAS']}/${k['RECHAZADAS']} vs ${p}/${a}/${r})`);
    });
  } finally { await ui.cerrar(app); }
}

module.exports = { f4, f10 };
