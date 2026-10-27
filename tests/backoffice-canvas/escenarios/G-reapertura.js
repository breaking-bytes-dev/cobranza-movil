// FASE G — User2 (Solicitante) vuelve después del ciclo completo de la Especial QA pedida en la fase B:
// aprobada por el Supervisor (fase C, el owner pasa al usuario del agente) y cerrada por el Admin (fase F).
//  · ¿Sigue viendo sus Especiales (KPIs de Inicio y Mis solicitudes)?
//  · Re-solicitar por la UI sobre esa misma cuenta → la app debe REACTIVAR la cerrada.
const pl = require('../lib/player');
const inf = require('../lib/informe');
const v = require('../lib/verdad');
const cfg = require('../lib/config');
const qa = require('../lib/datosqa');
const c = require('./comun');
const { pedirEspecial, especialesDe } = require('./B-solicitante');

module.exports = async function faseG(suite) {
  const yo = cfg.usuarios.user2.id;
  const cta = suite.datos.cuentas[0]; // registro de la cuenta QA 0001
  const mCta = qa.mascara(cta);       // "•••• E2E1": lo único que ve el Solicitante
  const app = await c.abrir(suite, 'user2', 'G', 'Solicitante');
  try {
    await inf.correr('F-02e', 'Tras aprobar (owner → localizador) y cerrar: el Solicitante sigue viendo SUS Especiales (KPIs de Inicio y Mis solicitudes = API)', 'Solicitante', async (e) => {
      const previa = (await especialesDe(cta))[0] || {};
      e.nota(`Especial ${previa.sanic_cm_as_numero}: aprobación ${previa.sanic_cm_as_estadoaprobacion} · estado ${previa.sanic_cm_as_estado} · owner ${previa._ownerid_value} (User2 = ${yo})`);
      // KPIs en TIBIO (salir y volver): en la primera carga pueden venir vacíos y eso se prueba en otra fase.
      const k = (await c.kpisInicio(app, 'Mis solicitudes', 'scrMisSolicitudes(kpi)')).tibio;
      const [p, a, r] = [await v.especialesDe(yo, 2), await v.aprobadasActivasDe(yo), await v.especialesDe(yo, 4)];
      e.check(c.num(k.PENDIENTES) === p, 'PENDIENTES = API', p, k.PENDIENTES);
      e.check(c.num(k.APROBADAS) === a, 'APROBADAS = API (solo las aprobadas activas; la cerrada ya no cuenta)', a, k.APROBADAS);
      e.check(c.num(k.RECHAZADAS) === r, 'RECHAZADAS = API', r, k.RECHAZADAS);
      await c.ir(app, 'Mis solicitudes', 'scrMisSolicitudes');
      const filas = (await pl.ctl(app, 'lblCuentaFilaSol').locator('visible=true').allInnerTexts()).map(x => x.trim());
      const api = (await Promise.all(suite.datos.cuentas.map(x => especialesDe(x)))).flat()
        .filter(x => x._sanic_cm_as_solicitadopor_value === yo).map(x => x.sanic_cm_as_numero);
      e.nota(`Mis solicitudes (QA visibles): ${JSON.stringify(filas.filter(x => /E2E/.test(x)))} · API (solicitadas por User2): ${api.length}`);
      e.check(filas.includes(mCta), `Mis solicitudes muestra la Especial aprobada y cerrada de ${mCta}`, mCta, filas.filter(x => /E2E/.test(x)));
      e.evid(await pl.foto(app, 'F-02e-mis-solicitudes'));
    });

    await inf.correr('F-02c', 'Re-solicitar por la UI una Especial cuya anterior fue APROBADA y CERRADA (ciclo real) → la app la REACTIVA (B-11)', 'Solicitante', async (e) => {
      const previa = await especialesDe(cta);
      if (!(previa.length === 1 && previa[0].sanic_cm_as_estado === false)) { e.bloquear(`precondición: 1 Especial CERRADA en ${mCta} (hay ${previa.length}, estados ${JSON.stringify(previa.map(x => x.sanic_cm_as_estado))})`); return; }
      let n;
      try { n = await pedirEspecial(app, cta, { nio: 120, motivo: 'QA-E2E re-solicitud F-02c', etiqueta: 'scrNuevaSolicitud(re-solicitud)' }); }
      catch (err) { if (/No se pudo elegir la cuenta/.test(err.message)) { e.bloquear(`el buscador de cuentas no deja elegir (B-04): ${err.message.slice(0, 120)}`); return; } throw err; }
      e.check(/Solicitud enviada/.test(n || ''), 'Notify de envío', '✨ Solicitud enviada …', n);
      const rows = await especialesDe(cta); const a = rows[0] || {};
      e.check(rows.length === 1 && a.sanic_cm_tbl_asignacionid === previa[0].sanic_cm_tbl_asignacionid && a.sanic_cm_as_estado === true && a.sanic_cm_as_estadoaprobacion === 2,
        'Dataverse: el MISMO registro reactivado (Estado activo, Pendiente aprobación)', `${previa[0].sanic_cm_as_numero} · true · 2`, rows.map(x => `${x.sanic_cm_as_numero} · ${x.sanic_cm_as_estado} · ${x.sanic_cm_as_estadoaprobacion}`));
      if (!/Solicitud enviada/.test(n || '')) {
        e.nota(`respuesta de Dataverse: ${c.erroresRed(app, 'G:scrNuevaSolicitud(re-solicitud)').join(' | ') || '(sin error en red)'}`);
        const lk = app.red.de('G:scrNuevaSolicitud(re-solicitud)').flatMap(x => x.subs.map((s, i) => ({ s, r: x.respuestas?.[i] })))
          .filter(({ s }) => /sanic_cm_tbl_asignacions\?.*sanic_cm_as_estado eq false/.test(s.ruta));
        e.nota(`LookUp de la Especial CERRADA (como User2): ${lk.map(x => `${x.r?.filas ?? '?'} fila(s)`).join(', ') || 'no se vio en la red'}`);
      }
      e.evid(await pl.foto(app, 'F-02c-re-solicitud'));
    });
  } finally { await pl.cerrar(app); }
};
