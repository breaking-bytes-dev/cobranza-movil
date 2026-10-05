const a = require('./lib/app');
(async () => {
  const b = await a.lanzar(); const app = await a.abrir(b);
  console.log('cargó; textos del encabezado:', await a.texto(app, 'lblTitulo_1').catch(e => 'ERR ' + e.message.slice(0, 60)));
  console.log('consultas a Dataverse:', app.red.entradas.length, '| errores de consola:', app.consola.length);
  console.log(await a.foto(app, 'humo-inicio'));
  await a.cerrar(app); await b.close();
})().catch(e => { console.log('ERR', e.message); process.exit(1); });
