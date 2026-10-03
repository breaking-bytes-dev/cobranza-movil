// FASE X — X-01 responsive: viewport 800x900 (< 920 px) con User4 (Admin).
const pl = require('../lib/player');
const inf = require('../lib/informe');
const c = require('./comun');

async function verificarMovil(e, app, pantalla) {
  e.check(await pl.existeVisible(app, 'conBarraMovilEncCmp'), `${pantalla}: barra móvil visible`, true, false);
  e.check(await pl.existeVisible(app, 'icoMenuEncCmp'), `${pantalla}: hamburguesa visible`, true, false);
  e.check(!(await pl.existeVisible(app, 'galNavCmp')), `${pantalla}: nav lateral oculta hasta abrirla`, 'oculta', 'visible');
  e.evid(await pl.foto(app, `X-01-${pantalla}-cerrada`));
  if (!(await pl.existeVisible(app, 'icoMenuEncCmp'))) { e.nota(`${pantalla}: sin hamburguesa no hay drawer que abrir (la app no entró en modo móvil)`); return false; }
  await (await pl.visible(app, 'icoMenuEncCmp')).click(); await pl.esperar(app, 1500);
  e.check(await pl.existeVisible(app, 'galNavCmp'), `${pantalla}: la hamburguesa abre la nav`, 'visible', 'oculta');
  e.evid(await pl.foto(app, `X-01-${pantalla}-abierta`));
  return true;
}

module.exports = async function faseX(suite) {
  const app = await c.abrir(suite, 'user4', 'X', 'Admin (800x900)', { viewport: { width: 800, height: 900 } });
  try {
    await inf.correr('X-01', 'Responsive < 920 px: barra móvil + hamburguesa; nav oculta hasta abrirla (Inicio y Monitoreo)', 'Admin', async (e) => {
      const movil = await verificarMovil(e, app, 'scrInicio');
      const ancho = await app.frame.evaluate(() => { const s = document.querySelector('[data-control-name="scrInicio"]'); return s ? Math.round(s.getBoundingClientRect().width) : null; });
      const nav = await app.frame.evaluate(() => { const n = Array.from(document.querySelectorAll('[data-control-name="conRaizNavCmp"]')).find(x => x.getClientRects().length); return n ? Math.round(n.getBoundingClientRect().width) : null; });
      e.nota(`viewport 800 px → pantalla renderizada ${ancho} px, nav lateral ${nav} px (diseño: 240). Si la nav mide < 240 la app está ESCALANDO (Escalar para ajustar) y nfMovil (Width < 920) nunca se cumple`);
      await c.ir(app, 'Monitoreo de carga', 'scrMonitoreo');
      if (movil) e.check(!(await pl.existeVisible(app, 'galNavCmp')), 'scrMonitoreo: al navegar el drawer se cierra solo', 'oculta', 'visible');
      await verificarMovil(e, app, 'scrMonitoreo');
    });
  } finally { await pl.cerrar(app); }
};
