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
node run.js                       # todas las fases: A,B,C,D,E,F,G,H,X  (~15 min)
node run.js --fases A,X           # solo algunas (B→C→F→G dependen entre sí: corrélas en orden)
node run.js --solo-limpieza       # borra restos QA-E2E de corridas anteriores
BO_RESULTADOS_DIR=$PWD/resultados/corrida-N node run.js   # guardar cada corrida aparte
```

## Usuarios: mapeo REAL y fijo (sin sustitutos)

| Sesión | Team | Rol |
|---|---|---|
| `sesion-user1.json` | CM · Backoffice · DEV | BackOffice |
| `sesion-user2.json` | CM · Solicitantes · DEV | Solicitante |
| `sesion-user3.json` | CM · Supervisores · DEV | Supervisor |
| `sesion-user4.json` | CM · Admin y Carga · DEV | Admin (User10 también está en ese team y no se toca) |

`run.js` verifica la foto inicial de membresías contra este mapeo (`cfg.membresiasEsperadas()`), la restaura al
final y la vuelve a verificar. El ÚNICO cambio temporal es R-06: User1 se agrega a "Admin y Carga" en la fase E
y se lo quita en la misma fase.

## Qué hace (y en qué orden)

| Fase | Usuario (rol) | Escenarios |
|---|---|---|
| A | User4 (Admin) | R-05, E-01a (sin banners ni 403 al abrir), D-02a (KPIs en la PRIMERA carga = API, B-01), D-02b..d, F-05, F-04c (Agentes: 0 escrituras al abrir, botón "Desvincular" aparte — nunca se pulsa; panel "Vincular/Cambiar": se abre, se busca un usuario (StartsWith delegado) y se CANCELA — nunca se toca una fila, B-04), D-03 (búsqueda delegada + B-13 DelayOutput), F-04b |
| B | User2 (Solicitante) | R-03, E-01b, D-02e (frío + subtítulo "Tus solicitudes Especiales"), D-04, F-02a (buscador + galería: número + cliente, B-04), F-02 (crear por UI), F-02b (anti-duplicado), **X-02** (falla CONTROLADA de escritura, B-03: Especial cerrada que el Solicitante no ve → alt key → error mostrado, nada dado por hecho), **F-02d** (reactivar Especial cerrada propia, B-11), **D-05** (mínimo privilegio: sin consultas a Pagos/Carga ni 403) |
| C | User3 (Supervisor) | R-04, E-01c, D-02f, F-03 (aprobar eligiendo localizador en buscador + galería, B-04), F-03c (owner → usuario del agente por el plugin asíncrono RepartoAsignacion, hasta ~3 min, B-10), F-03b (rechazar) |
| D | User1 (Backoffice) | R-02, E-01d, D-02g, D-02h, F-01 (transcribir con el rol BO solo, B-02), F-01b |
| E | User1 + Admin y Carga (temporal) | R-06a (multi-rol), R-06b (quitado → solo Backoffice) |
| F | User4 (Admin) + 1 fila QA de carga HOY | F-04 (cerrar la Especial QA), D-07 (carga de hoy, B-07), F-05b (freno + lista completa sin tope de 500, B-12; el cierre en lote NUNCA se pulsa) |
| G | User2 (Solicitante) | F-02e (sigue viendo sus Especiales tras aprobación/cierre), F-02c (re-solicitar por UI tras el ciclo real, B-11) |
| H | API (suplantando) + User2, User3, User4, User1 | **PCI fase 2**: H-01 (solo BackOffice lee la bóveda `cuentapan`; Solicitante/Supervisor/Admin → 403 o vacío), H-02 (Solicitante, Supervisor y Admin: ninguna pantalla muestra 13+ dígitos ni toca la bóveda ni las columnas `*_numerocuenta`), H-03 (BackOffice: Transcritas hoy lista enmascarada y detalle con el número completo de la bóveda). Sin capturas: un fallo no debe dejar un PAN en un PNG. Depende de la siembra y de F-01 (fase D) |
| X | User4 a 800×900 | X-01 (responsive: barra móvil + hamburguesa, B-09) |
| — | todas | **D-01**: análisis de TODA la red capturada por pantalla (delegación, N+1, errores de lectura) |

## Reglas de seguridad que el código hace cumplir

1. **PCI**: los datos QA no llevan PANs reales. La cuenta QA `QA-E2E-CTA-000n` es a la vez su nombre principal y el "PAN" de su fila en la bóveda; la app la muestra como `•••• E2En` (`ct_ultimos4`) y su clave sustituta es `QA-E2E-CLAVE-000n`. La suite ya no escribe `as_/in_/pg_numerocuenta`.
2. Todo dato que la suite crea (o hace crear a la app) lleva el prefijo **`QA-E2E`** (CIF, nombre, cuenta,
   localizador, recibo, notas, código). Antes de cada acción destructiva en la UI se verifica que el registro
   en pantalla es QA (número de interacción, número de asignación, cuenta) o se aborta el escenario.
3. El **cierre en lote** de Reconciliación NUNCA se ejecuta: solo se verifica que esté deshabilitado.
4. **Limpieza** al final (pase o falle, también con Ctrl+C): borra por API todo lo QA-E2E (pagos →
   actualizaciones → interacciones → asignaciones → carga → bóveda → cuentas → teléfonos/direcciones → clientes),
   verifica CERO restos, restaura las **membresías de teams** a la foto tomada al inicio y verifica que los
   **agentes reales** conservan usuario/email/activo (si no, los restaura y lo reporta).
5. Nada sensible en el repo: sesiones y secretos se leen de rutas externas; `resultados/` está gitignoreada.

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
