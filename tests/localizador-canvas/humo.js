// Prueba de humo: abre la app del localizador en el web player, muestra el título, cuántas consultas hizo
// y cuántos errores de consola hubo, y guarda una captura de Inicio.
// Uso: NODE_PATH=../backoffice-canvas/node_modules node humo.js
const a = require('./lib/app');
(async () => {
  const b = await a.lanzar(); const app = await a.abrir(b);
  console.log('cargó; textos del encabezado:', await a.texto(app, 'lblTitulo_1').catch(e => 'ERR ' + e.message.slice(0, 60)));
  console.log('consultas a Dataverse:', app.red.entradas.length, '| errores de consola:', app.consola.length);
  console.log(await a.foto(app, 'humo-inicio'));
  await a.cerrar(app); await b.close();
})().catch(e => { console.log('ERR', e.message); process.exit(1); });
