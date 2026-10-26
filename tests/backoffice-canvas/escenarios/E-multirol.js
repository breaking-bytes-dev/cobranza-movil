// FASE E — MULTI-ROL: User1 (Backoffice) se agrega TEMPORALMENTE al team "CM · Admin y Carga · DEV",
// se prueba la nav combinada y se lo QUITA; luego se verifica que vuelve a SOLO Backoffice.
// (run.js además restaura la foto de membresías al final, pase lo que pase.)
const pl = require('../lib/player');
const inf = require('../lib/informe');
const c = require('./comun');

module.exports = async function faseE(suite) {
  const U = 'user1';
  await c.cambiarTeams(U, { agregar: ['AdminCarga'] }); // ahora: Backoffice + Admin y Carga
  let app;
  try {
    app = await c.abrir(suite, U, 'E', 'BackOffice|Admin (multi-rol)');
    await inf.correr('R-06a', 'MULTI-ROL: User1 en Backoffice + Admin y Carga → nav combinada, Inicio con ambas secciones, abre pantallas de los dos roles', 'BackOffice|Admin', async (e) => {
      await c.verificarRol(e, app, ['BackOffice', 'Admin'], /BackOffice · Admin|Admin · BackOffice/);
      e.evid(await pl.foto(app, 'R-06a-multirol-inicio'));
      await c.ir(app, 'Por transcribir', 'scrTranscripcion');
      e.check(await pl.existeVisible(app, 'lblHeadColaTrx'), 'abre la estación de transcripción', true, false);
      await c.ir(app, 'Datos maestros', 'scrMaestros');
      e.check(await pl.existeVisible(app, 'txtBuscarMae'), 'abre Datos maestros', true, false);
      await c.ir(app, 'Monitoreo de carga', 'scrMonitoreo');
      const err = await c.erroresVisibles(app);
      e.check(err.length === 0, 'sin banners de error con los dos roles', [], err);
    });
  } finally {
    if (app) await pl.cerrar(app);
    await c.cambiarTeams(U, { quitar: ['AdminCarga'] }); // vuelve a SOLO Backoffice (su team real)
  }
  const app2 = await c.abrir(suite, U, 'E2', 'BackOffice (multi-rol revertido)');
  try {
    await inf.correr('R-06b', 'MULTI-ROL revertido: User1 fuera de Admin y Carga → vuelve a SOLO la nav Backoffice', 'BackOffice', async (e) => {
      await c.verificarRol(e, app2, ['BackOffice'], /^BackOffice$/);
      e.evid(await pl.foto(app2, 'R-06b-solo-backoffice'));
    });
  } finally { await pl.cerrar(app2); }
};
