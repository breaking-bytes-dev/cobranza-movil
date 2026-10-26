// Versión que muestra la app: antes de probar se verifica que sea la versión de la solución cm_core_cobranzamovil
// instalada en el ambiente (la app la pide al servidor con la Custom API sanic_cm_MisRoles). Se lee en el pie de la nav
// (backoffice) o en Sincronización (localizador). Si todavía muestra «—» o otra versión se cierra, se espera y se
// reabre, hasta 5 min. Cada coincidencia queda en local/version-servida.log. Con CM_SIN_VERSION=1 no se verifica.
// Ojo: la versión sale del servidor, así que esto NO detecta que el player sirva una app publicada vieja.
const fs = require('fs');
const path = require('path');
const dv = require('./dataverse');

const LOG = path.join(__dirname, '..', '..', '..', 'local', 'version-servida.log');
const SOLUCION = 'cm_core_cobranzamovil';
const MAX_MS = 5 * 60 * 1000;
const PAUSA_MS = 30000;

async function esperada() {
  const r = await dv.get(`solutions?$select=version&$filter=uniquename eq '${SOLUCION}'`);
  return (r.value[0] || {}).version || null;
}

// "Versión 1.1.0.0" / "Versión de la app: 1.1.0.0" → "1.1.0.0"; «—» u otro texto → el texto tal cual.
function sello(texto) {
  const m = String(texto || '').match(/(\d+\.\d+\.\d+\.\d+)\s*$/);
  return m ? m[1] : (String(texto || '').trim() || null);
}

// abrir(): abre la app y la devuelve. leer(app): texto con la versión; `undefined` = este diseño no la muestra (no se
// verifica), null o un texto distinto = no coincide. cerrar(app): cierra lo que abrió abrir().
async function abrirVerificada(nombre, { abrir, leer, cerrar }) {
  if (process.env.CM_SIN_VERSION) return abrir();
  const esp = await esperada();
  if (!esp) throw new Error(`No se encontró la solución ${SOLUCION} para verificar la versión`);
  const t0 = Date.now();
  for (let intento = 1; ; intento++) {
    const app = await abrir();
    let leido;
    try { leido = await leer(app); } catch (e) { leido = null; }
    if (leido === undefined) return app;
    const v = sello(leido);
    if (v === esp) {
      const linea = { cuando: new Date().toISOString(), app: nombre, version: v, intentos: intento,
                      seg_esperando: Math.round((Date.now() - t0) / 1000) };
      try { fs.appendFileSync(LOG, JSON.stringify(linea) + '\n'); } catch (e) { /* sin local/ */ }
      console.log(`  ··· ${nombre}: muestra la versión de la solución (${v}); intento ${intento}`);
      return app;
    }
    if (Date.now() - t0 + PAUSA_MS > MAX_MS) {
      await cerrar(app);
      const err = new Error(`${nombre} muestra la versión "${v}" y la solución ${SOLUCION} es "${esp}"; `
        + `se reintentó ${intento} veces durante ${Math.round((Date.now() - t0) / 1000)} s`);
      err.versionDistinta = true;   // no es una falla de carga: quien reintente la apertura NO debe reintentar esto
      throw err;
    }
    console.log(`  ··· ${nombre}: muestra "${v}", la solución es "${esp}"; reabro en ${PAUSA_MS / 1000} s (intento ${intento})`);
    await cerrar(app);
    await new Promise((r) => setTimeout(r, PAUSA_MS));
  }
}

module.exports = { abrirVerificada, esperada, sello };
