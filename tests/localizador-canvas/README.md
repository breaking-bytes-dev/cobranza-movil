# Pruebas web de la app del localizador

Prueban en el reproductor web de Power Apps lo que no necesita el teléfono: Inicio, Cliente, Cartera, formulario de interacción y accesibilidad. El reproductor corre la app **en línea**, sin base local ni sincronización: lo *offline* se prueba en el teléfono.

> ⚠️ `grupo2.js` crea y borra interacciones del localizador de prueba. Correrlas **solo en un ambiente de prueba**.

Configuración, usuarios de prueba, cómo adaptarlas a otro ambiente y trampas conocidas: **[docs/tecnica/10-pruebas.md](../../docs/tecnica/10-pruebas.md)**.

## Requisitos

- Las dependencias instaladas en [`tests/backoffice-canvas/`](../backoffice-canvas/).
- El archivo de secretos del Service Principal y la sesión `sesion-<clave>.json` del usuario localizador de prueba (la clave por defecto está en [`lib/app.js`](lib/app.js)). Ver [Pruebas](../../docs/tecnica/10-pruebas.md#usuarios-de-prueba).
- Las carteras QA del localizador, creadas con [`scripts/pan/fixture_telefonos.py`](../../scripts/pan/fixture_telefonos.py). `grupo2.js` tiene fijos valores de esas carteras: ajustarlos al ambiente.

## Uso

```bash
cd tests/localizador-canvas
NODE_PATH=../backoffice-canvas/node_modules node humo.js     # abre la app y saca una captura
NODE_PATH=../backoffice-canvas/node_modules node grupo2.js   # pantallas y formularios
```

Resultados y capturas en `resultados/`.

## Estructura

| Archivo | Qué hace |
|---|---|
| [`lib/app.js`](lib/app.js) | Abre la app del localizador con el arnés de la oficina y reconoce la pantalla visible |
| [`humo.js`](humo.js) | Prueba de humo: carga, consultas, errores de consola y captura |
| [`grupo2.js`](grupo2.js) | Chequeos de pantallas, formularios y accesibilidad contra la Web API |
