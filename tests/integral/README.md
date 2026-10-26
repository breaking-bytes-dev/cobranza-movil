# Suite integral

Recorre la solución de punta a punta: genera un archivo de carga con datos ficticios, lo importa por Smelter, verifica el reparto y pasa por cada rol en las dos apps hasta los cierres. Incluye además la matriz de roles contra el servidor y pruebas en vivo de las Custom APIs.

> ⚠️ Crea y borra datos (prefijo `QA-E2E-INT`) y *jobs* de importación. Correrla **solo en un ambiente de prueba**, y nunca a la vez que otra suite.

Configuración, usuarios de prueba, fases, cómo adaptarla a otro ambiente y trampas conocidas: **[docs/tecnica/10-pruebas.md](../../docs/tecnica/10-pruebas.md)**.

## Requisitos

- Las dependencias instaladas en [`tests/backoffice-canvas/`](../backoffice-canvas/) (de ahí el `NODE_PATH`).
- El archivo de secretos del Service Principal y las sesiones `sesion-user1.json` … `sesion-user5.json`.
- El agente del localizador de prueba vinculado a su usuario y la plantilla de Smelter instalada.

## Uso

```bash
cd tests/integral
NODE_PATH=../backoffice-canvas/node_modules node run.js                 # F0 a F10 y limpieza
NODE_PATH=../backoffice-canvas/node_modules node run.js --hasta F5      # hasta una fase, limpia igual
NODE_PATH=../backoffice-canvas/node_modules node run.js --carga-ui      # la carga por la pantalla Cargar archivo
NODE_PATH=../backoffice-canvas/node_modules node run.js --solo-limpieza
NODE_PATH=../backoffice-canvas/node_modules node seguridad/matriz.js --sin-informe
NODE_PATH=../backoffice-canvas/node_modules node apis/<prueba>.js
```

Depuración: `--conservar` no limpia y guarda el estado; `--desde <fase>` reanuda sobre esos datos.

Resultados en `resultados/corrida-<fecha>/` (`resumen.md`, `resultados.json`, `registro.json`, `capturas/`). Sale con código distinto de 0 si algún chequeo falla.

## Estructura

| Carpeta o archivo | Qué hace |
|---|---|
| [`run.js`](run.js) | Fases, `--hasta`/`--desde` y limpieza en `finally` |
| [`lib/`](lib/) | Contexto e informe, CSV ficticio, importación por Smelter, limpieza por prefijo, apertura de las apps |
| [`fases/`](fases/) | Carga, Admin, Solicitante, Supervisor, localizador y BackOffice |
| [`seguridad/`](seguridad/) | Matriz rol × tabla × operación y lo esperado por rol |
| [`apis/`](apis/) | Pruebas en vivo de Custom APIs y reglas del servidor, sin navegador |
| [`pantallas/`](pantallas/) | Alta de cuenta y solicitud de Especial por la app |
