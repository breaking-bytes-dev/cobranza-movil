# Suite E2E — App - CM - Backoffice Canvas (player real, DEV)

Pruebas end-to-end con **Playwright** sobre el **player** de Power Apps (no Studio), con usuarios reales
por rol, verificando cada número y cada escritura contra **Dataverse** (Web API con el Service Principal).
Prioridad 1: que **no haya problemas de delegación** (Bloque D).

Resultados de la última corrida: [`docs/guias/backoffice-canvas/14-resultados-pruebas-e2e.md`](../../docs/guias/backoffice-canvas/14-resultados-pruebas-e2e.md).

## Requisitos

- Node 18+ (`fetch` nativo) y Playwright con Chromium:
  ```bash
  cd tests/backoffice-canvas
  npm install
  npx playwright install chromium
  ```
- **Sesiones de navegador** (storageState) de los usuarios de prueba: `sesion-user1.json` … `sesion-user4.json`
  en una carpeta FUERA del repo (por defecto `local/`, gitignoreada). Se generan iniciando sesión a mano una
  vez con Playwright y guardando `context.storageState()`. **La sesión `admin` está prohibida** (solo Studio).
- **Secretos del Service Principal** (System Administrator en DEV) en un `.env` fuera del repo (por defecto
  `pp_secrets.env`, gitignoreado) con `DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_TENANT_ID`, `PP_CLIENT_SECRET`.
  El runner nunca los imprime.

| Variable | Default | Para qué |
|---|---|---|
| `BO_SESIONES_DIR` | `<repo>/local` | carpeta de `sesion-userN.json` |
| `BO_SECRETS_FILE` | `<repo>/pp_secrets.env` | credenciales del SP |
| `BO_RESULTADOS_DIR` | `tests/backoffice-canvas/resultados` (gitignoreada) | capturas, `resultados.json`, `resumen.md` |
| `BO_PLAYER_URL` | URL del player DEV | otra app/entorno |
| `BO_HEADED` | (vacío) | `1` para ver el navegador |

## Correr

```bash
node run.js                       # todas las fases: L,A,B,C,D,E,F,G,X  (~25 min)
node run.js --fases A,X           # solo algunas (B→C→E→G dependen entre sí: corrélas en orden)
node run.js --solo-limpieza       # borra restos QA-E2E de corridas anteriores
```

## Qué hace (y en qué orden)

| Fase | Usuario (rol efectivo) | Escenarios |
|---|---|---|
| L | User1, User2 | R-02 / R-03 con el usuario original (detecta licencia vencida → BLOQUEADO con captura) |
| A | User4 (Admin) | R-05, E-01a (sin banners de error al abrir), D-02a..d (KPIs Inicio/Monitoreo/Cierres vs API), F-05 (Reconciliación), F-04c (Agentes, solo lectura), D-03 (búsqueda delegada en Maestros), F-04b (alta de cliente QA + anti-duplicado) |
| B | User3 → team Solicitantes | R-03*, E-01b, D-02e, D-04 (búsqueda del combo con StartsWith), F-02a (el combo muestra/permite elegir), F-02 (crear Especial QA; si la UI no deja, se crea por API para no bloquear C/E) y F-02b (anti-duplicado) |
| C | User3 (Supervisor) | R-04, E-01c, D-02f, F-03 (aprobar con localizador), F-03c (owner por Comp-14), F-03b (rechazar con motivo) |
| D | User4 → team Backoffice | R-02*, E-01d, D-02g/h, F-01 (transcribir 2 interacciones QA), F-01b (Transcritas hoy, si F-01 grabó) |
| E | User4 → Backoffice + Admin | R-06a (multi-rol), F-01m + F-01b (transcripción con BO+Admin si el rol BO solo no pudo grabar), F-04 (cerrar la Especial QA) |
| F | User4 (Admin) + 1 fila QA de carga HOY | R-06b, D-07 (Monitoreo con carga de hoy), F-05b (freno de Reconciliación) |
| G | — (Web API) | F-06 (re-solicitar Especial tras cerrar la anterior: misma escritura que el Patch de la app, contra la alt key cuenta+tipo) |
| X | User4 a 800×900 | X-01 (responsive: barra móvil + hamburguesa) |
| — | todas | **D-01**: análisis de TODA la red capturada por pantalla (delegación, N+1, errores) |

Los usuarios marcados con `*` son **sustitutos**: si el dueño del rol no puede abrir el player (p. ej. licencia
vencida), la suite mueve TEMPORALMENTE a otro usuario de prueba entre teams `CM · <perfil> · DEV` por Web API.

## Reglas de seguridad que el código hace cumplir

1. Todo dato que la suite crea (o hace crear a la app) lleva el prefijo **`QA-E2E`** (CIF, nombre, cuenta,
   localizador, recibo, notas, código). Antes de cada acción destructiva en la UI se verifica que el registro
   en pantalla es QA (número de interacción, número de asignación, cuenta) o se aborta el escenario.
2. El **cierre en lote** de Reconciliación NUNCA se ejecuta: solo se verifica que esté deshabilitado.
3. **Limpieza** al final (pase o falle, también con Ctrl+C): borra por API todo lo QA-E2E (pagos →
   actualizaciones → interacciones → asignaciones → carga → cuentas → teléfonos/direcciones → clientes),
   verifica CERO restos, restaura las **membresías de teams** a la foto tomada al inicio y verifica que los
   **agentes reales** conservan usuario/email/activo (si no, los restaura y lo reporta).
4. Nada sensible en el repo: sesiones y secretos se leen de rutas externas; `resultados/` está gitignoreada.

## Estructura

```
run.js                 orquestador (fases, D-01, limpieza, volcado)
lib/config.js          rutas, usuarios, teams (sin secretos)
lib/dataverse.js       cliente Web API con token client_credentials
lib/player.js          abrir player, frame del runtime, data-control-name, nav, KPIs, Notify, capturas, red
lib/datosqa.js         siembra QA, registro de creados, limpieza, membresías
lib/verdad.js          conteos/sumas EXACTOS por API con la misma condición que la app
lib/informe.js         escenarios, análisis de delegación, resultados.json + resumen.md
escenarios/*.js        una fase por archivo
```

## Selectores (gotchas del player)

- Todo control tiene `data-control-name="<nombre de Studio>"` dentro del iframe `runtime-app…/publishedapp`;
  el iframe se reemplaza durante la carga → se resuelve perezoso.
- La red a Dataverse es `POST …/$batch`; el cuerpo solo es legible con `context.route()` (no con el evento
  `request`). Ahí se ven los `$filter`/`$apply` reales.
- La primera carga de la app ejecuta el `OnVisible` de Inicio antes de que termine `OnStart` (ver hallazgo
  B-01 en la guía 14): por eso los KPIs de Inicio se miden "en frío" y "en tibio".
