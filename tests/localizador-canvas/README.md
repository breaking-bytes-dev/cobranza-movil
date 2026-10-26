# Pruebas web de la app del localizador

Suite automatizada **para desarrolladores** que recorre en el reproductor web de Power Apps lo que no necesita el teléfono: Inicio, Cliente, Cartera, formulario de interacción y accesibilidad. El reproductor corre la app **en línea**, sin base local ni sincronización: lo *offline* se prueba en el teléfono.

No hace falta para instalar, operar ni verificar un ambiente. La verificación de un ambiente se hace a mano con la guía de [Pruebas](../../docs/tecnica/10-pruebas.md): los casos de esta suite están ahí como casos manuales del área [Localizador](../../docs/tecnica/10-pruebas.md#5-localizador-cartera-y-visita-offline-loc).

> ⚠️ `grupo2.js` crea y borra interacciones del localizador de prueba. Correrla **solo en un ambiente de prueba**.

## Qué necesita

- El arnés de [`tests/backoffice-canvas/`](../backoffice-canvas/) (esta suite usa sus dependencias y su configuración, [`config.js`](../backoffice-canvas/lib/config.js)).
- Una sesión de navegador guardada del usuario localizador de prueba (la clave del usuario por defecto está en [`lib/app.js`](lib/app.js)).
- `grupo2.js`, además, lee y escribe filas en Dataverse con el acceso que configura [`dataverse.js`](../backoffice-canvas/lib/dataverse.js), y tiene fijos valores de las carteras de prueba de un ambiente concreto: hay que ajustarlos al ambiente donde se corra.

Resultados y capturas en `resultados/`.

## Estructura

| Archivo | Qué hace |
|---|---|
| [`lib/app.js`](lib/app.js) | Abre la app del localizador con el arnés de la oficina y reconoce la pantalla visible |
| [`humo.js`](humo.js) | Prueba de humo: carga, consultas, errores de consola y captura |
| [`grupo2.js`](grupo2.js) | Chequeos de pantallas, formularios y accesibilidad contra la Web API |
