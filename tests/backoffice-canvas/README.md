# Suite de punta a punta de la app de la oficina

Prueba la app de la oficina en el reproductor web de Power Apps con un usuario real por rol (Admin, BackOffice, Solicitante, Supervisor) y compara cada número y cada escritura contra la Web API de Dataverse. También es el arnés compartido de [`tests/integral/`](../integral/) y [`tests/localizador-canvas/`](../localizador-canvas/).

> ⚠️ Crea y borra datos (prefijo `QA-E2E`) y cambia membresías de equipos. Correrla **solo en un ambiente de prueba**.

Configuración, usuarios de prueba, qué cubre cada fase, cómo adaptarla a otro ambiente y trampas conocidas: **[docs/tecnica/10-pruebas.md](../../docs/tecnica/10-pruebas.md)**.

## Instalación

```bash
npm install
npx playwright install chromium
```

Necesita el archivo de secretos del Service Principal (`BO_SECRETS_FILE`) y, en `BO_SESIONES_DIR`, una sesión `sesion-<clave>.json` por cada usuario de prueba de `cfg.usuarios` (uno por rol: Admin, BackOffice, Solicitante y Supervisor). Cómo generarlas: [Pruebas](../../docs/tecnica/10-pruebas.md#usuarios-de-prueba).

## Uso

```bash
node run.js                     # fases A, B, C, D, E, F, G, H, X
node run.js --fases A,X         # algunas fases (B → C → F → G dependen entre sí; H depende de D)
node run.js --solo-limpieza     # borra lo que dejó anotado la corrida anterior
BO_HEADED=1 node run.js         # con el navegador visible
```

Sale con código distinto de 0 si algún escenario no pasó o una fase se abortó; el detalle está en `resultados/resumen.md`. La limpieza borra solo lo que la corrida anotó en `resultados/registro-qa.json` y lo que cuelga de eso, nunca por prefijo, así que no toca los datos QA de otras suites.

## Estructura

| Archivo | Qué hace |
|---|---|
| [`run.js`](run.js) | Orquesta las fases, el análisis de red y la limpieza final |
| [`lib/config.js`](lib/config.js) | URL de la app, usuarios por rol, equipos, rutas (sin secretos) |
| [`lib/dataverse.js`](lib/dataverse.js) | Cliente de la Web API con el Service Principal |
| [`lib/player.js`](lib/player.js) | Apertura del reproductor, controles por nombre, red, capturas |
| [`lib/version.js`](lib/version.js) | Espera a que el ambiente sirva la versión desplegada |
| [`lib/datosqa.js`](lib/datosqa.js) | Siembra, registro de lo creado, limpieza y membresías |
| [`lib/verdad.js`](lib/verdad.js) | Conteos y sumas por API con la misma condición que la app |
| [`lib/informe.js`](lib/informe.js) | `resultados.json`, `resumen.md` y análisis de delegación |
| [`escenarios/`](escenarios/) | Una fase por archivo y utilidades comunes |
