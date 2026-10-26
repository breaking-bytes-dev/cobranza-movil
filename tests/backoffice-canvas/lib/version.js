// Versión servida: antes de probar, la app que sirve DEV tiene que ser la que se desplegó.
// paso7_desplegar sella "<commit>[+dirty] <aaaa-mm-dd HH:MM UTC>" en nfVersion de las dos apps y lo guarda en
// local/version-desplegada.json. Al abrir una app se lee el sello que muestra (backoffice: pie de la nav; localizador:
// Sincronización). Si no coincide se cierra, se espera y se reabre, hasta 10 min, porque el servicio puede seguir
// sirviendo la versión anterior un rato después de publicar. Cada coincidencia queda en local/version-servida.log.
// Sin local/version-desplegada.json (nunca se desplegó con sello) o con CM_SIN_VERSION=1 no se verifica.
const fs = require('fs');
const path = require('path');

const LOCAL = path.join(__dirname, '..', '..', '..', 'local');
const ARCHIVO = path.join(LOCAL, 'version-desplegada.json');
const LOG = path.join(LOCAL, 'version-servida.log');
const MAX_MS = 10 * 60 * 1000;
const PAUSA_MS = 30000;

function esperada() {
  try { return JSON.parse(fs.readFileSync(ARCHIVO, 'utf8')); } catch (e) { return null; }
}

// "Versión 1a2b3c4 2026-10-15 22:10" → "1a2b3c4 2026-10-15 22:10"
function sello(texto) {
  const m = String(texto || '').match(/([0-9a-f]{7,40}(?:\+dirty)? \d{4}-\d{2}-\d{2} \d{2}:\d{2}|\bdev)\s*$/);
  return m ? m[1] : (String(texto || '').trim() || null);
}

// abrir(): abre la app y la devuelve. leer(app): texto del sello; `undefined` = este diseño no lo muestra (no se verifica),
// null o un texto distinto = no coincide. cerrar(app): cierra lo que abrió abrir().
async function abrirVerificada(nombre, { abrir, leer, cerrar }) {
  // Con el despliegue por parche (paso7 --parche) cada app tiene su versión: por_app[nombre] manda si está.
  const todo = esperada();
  const esp = todo && ((todo.por_app && todo.por_app[nombre]) || todo);
  if (!esp || process.env.CM_SIN_VERSION) return abrir();
  const t0 = Date.now();
  for (let intento = 1; ; intento++) {
    const app = await abrir();
    let leido;
    try { leido = await leer(app); } catch (e) { leido = null; }
    if (leido === undefined) return app;
    const v = sello(leido);
    if (v === esp.version) {
      const desdeImport = Math.round((Date.now() - Date.parse(esp.importado)) / 1000);
      const linea = { cuando: new Date().toISOString(), app: nombre, version: v, intentos: intento,
                      seg_esperando: Math.round((Date.now() - t0) / 1000), seg_desde_import: desdeImport };
      try { fs.appendFileSync(LOG, JSON.stringify(linea) + '\n'); } catch (e) { /* sin local/ */ }
      console.log(`  ··· ${nombre}: versión servida = desplegada (${v}); intento ${intento}, ${linea.seg_esperando} s esperando, ${desdeImport} s desde el import`);
      return app;
    }
    if (Date.now() - t0 + PAUSA_MS > MAX_MS) {
      await cerrar(app);
      const err = new Error(`DEV sirve la versión "${v}" de ${nombre}, se desplegó "${esp.version}" (import ${esp.importado}); `
        + `se reintentó ${intento} veces durante ${Math.round((Date.now() - t0) / 1000)} s`);
      err.versionDistinta = true;   // no es una falla de carga: quien reintente la apertura NO debe reintentar esto
      throw err;
    }
    console.log(`  ··· ${nombre}: DEV sirve "${v}", se desplegó "${esp.version}"; reabro en ${PAUSA_MS / 1000} s (intento ${intento})`);
    await cerrar(app);
    await new Promise((r) => setTimeout(r, PAUSA_MS));
  }
}

module.exports = { abrirVerificada, esperada, sello };
