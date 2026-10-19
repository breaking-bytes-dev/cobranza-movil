# Prueba integral "0 a 100" (DEV)

Recorre **de punta a punta** la solución, con **todos los roles** y **las dos apps canvas**, empezando por una **carga real de Smelter**:

```
CSV QA ─► Smelter (API) ─► reparto (plugin) ─► Admin ─► Solicitante ─► Supervisor ─► Cobrador ─► BackOffice ─► Admin (cierres) ─► Solicitante ─► limpieza
```

Es la versión automática de la [guía 17](../../docs/guias/17-prueba-integral-0-a-100.md). Lo que exige un **teléfono real** (modo avión, GPS,
"Ver número" en el dispositivo, sincronización offline) **no** está acá: sigue en la guía, marcado con 📱.

## Cómo correrla

```bash
cd tests/integral && NODE_PATH=../backoffice-canvas/node_modules node run.js
```

| Opción | Qué hace |
|---|---|
| `--hasta F5` | corre hasta esa fase (inclusive) y **limpia igual**. `--hasta F0` solo genera el CSV QA y lo deja en `resultados/` (se borra con `--solo-limpieza`) |
| `--solo-limpieza` | borra todo lo que lleve el prefijo `QA-E2E-INT` (de esta y de corridas caídas) y verifica que no quede nada |
| `--carga-ui` | la 1.ª importación (F1) la hace User4 desde la pantalla **Cargar archivo** del backoffice (prueba en seco + carga real, checks F1u-01..14) en lugar de la API de Smelter |
| `--conservar` | (depuración) NO limpia al final y guarda `resultados/estado.json` |
| `--desde F7` | (depuración) reanuda una corrida conservada desde esa fase, sobre los mismos datos |

Cada check imprime `✓`/`✗` con su ID y su detalle. Salida: `resultados/<corrida>/resumen.md`, `resultados.json`, `registro.json` (ids creados, **sin
números de tarjeta**) y `capturas/` (solo de pantallas sin PAN). El proceso sale con código **≠ 0** si hay algún `✗`.

## Fases

| Fase | Quién | Qué verifica |
|---|---|---|
| **F0** Preparación | — | CSV de 33 columnas con 1 cliente `QA-E2E-INT <runid>` y 3 cuentas: A Diaria, B Incobrable, C Especial (retiro, tipo, nota), todas con el agente `QA-E2E Localizador User5` y FechaCarga de hoy. PAN ficticios (BIN 9999, 16 dígitos, Luhn válido, aleatorios por corrida) |
| **F1** Carga | API de Smelter | job Completed (2), 0 fallidas, 0 logs; cliente; 3 cuentas con número enmascarado, últimos 4, `clavepan` de 64 hex (y HMAC correcto si está la llave); bóveda 1:1; 3 asignaciones con agente y tipo; **la Especial C nace Pendiente (2)**; 3 filas de carga diaria de hoy; **re-importar el mismo CSV no crea filas** |
| **F2** Reparto | plugin `RepartoAsignacion` | A y B pasan a User5 (≤ 3 min); C (Pendiente) conserva el dueño de la carga tras ~2 min; User5 lee A y B y no C |
| **F3** Admin (User4) | web | Inicio y Monitoreo = API; Reconciliación en estado **bloqueado** (umbral > 30%, casilla y cierre deshabilitados; no se pulsa nada); Agentes muestra al agente con su usuario (solo lectura) |
| **F4** Solicitante (User2) | web | Nueva solicitud sobre A (búsqueda por últimos 4) → Pendiente, sin agente, solicitada por User2; aviso anti-duplicado; segunda Especial sobre B |
| **F5** Supervisor (User3) | web | la cola trae C (CSV) y las 2 de User2; aprueba C y A-Especial con el localizador, rechaza B-Especial con motivo → `estadoaprobacion` 3/3/4, resolutor User3 |
| **F6** Reparto tras aprobar | plugin `RepartoAsignacion` | las aprobadas pasan a User5; la rechazada no; User5 lee las 3 cuentas |
| **F7** Cobrador (User5) | web player | Cartera = asignaciones visibles de User5 (API suplantando); el cliente QA con sus cuentas y sin la Especial rechazada; cobro con pago (recibo `QA-E2E-INT-<runid>-R1`, C$ 25) → interacción con agente y cuenta, pago con cuenta y N° de interacción completados por el plugin, fecha del pago |
| **F8** BackOffice (User1) | web | Por transcribir muestra la interacción con su pago y el número de cuenta completo (solo se verifica longitud y últimos 4: **no se lee, no se guarda, no se captura**); código `QAINT<runid>` + Enter → Grabado, mayúsculas, grabado por User1; Transcritas hoy la lista con su pago |
| **F9** Admin (User4) | web + API | Cierres: Incobrable B ("Recuperada") y Especial C ("Gestión completada" + detalle); luego User5 (suplantado con `MSCRMCallerID`) deja de leer B y C y sigue leyendo A |
| **F10** Solicitante (User2) | web | Mis solicitudes: A Aprobada con agente, B Rechazada con motivo; KPIs = API |
| **F11** Limpieza | API | siempre (también si algo falló): borra en orden de dependencias, verifica que no quede nada con el prefijo, que los intocables sigan y que Reconciliación vuelva a lo de antes |

## Requisitos previos

- **Sesiones** de navegador en `local/sesion-user{1,2,3,4,5}.json` (Playwright `storageState`, gitignoreadas; ver [tests/backoffice-canvas](../backoffice-canvas/README.md) y [tests/localizador-canvas](../localizador-canvas/README.md)).
  La sesión `admin` está prohibida.
- **Credenciales del Service Principal** `# PowerPlatform-Dev` en `pp_secrets.env` (DATAVERSE_URL, PP_CLIENT_ID, PP_TENANT_ID, PP_CLIENT_SECRET). Nunca se imprimen.
- Dependencias del arnés del backoffice (`tests/backoffice-canvas/node_modules`, Playwright con Chromium); de ahí el `NODE_PATH`.
- (Opcional) la llave HMAC de DEV en `~/.config/cobranza-movil/pan_hmac_dev.key` (o `PAN_HMAC_KEY_FILE`): con ella F1 verifica que `clavepan` sea exactamente `HMAC(K, PAN)`.
- Roles reales por equipo: User1 BackOffice, User2 Solicitante, User3 Supervisor, User4 Admin y carga (con "Smelter Operador"), User5 Localizador.
- Agente `QA-E2E Localizador User5` ya vinculado a User5. **La suite no crea, modifica ni borra agentes.**

## Duración

Unos **20 a 25 minutos** (7 aperturas en frío de la app, de 1 a 2 minutos cada una, más la espera de los ~2 minutos de la compuerta S-6 en F2).

## Efectos colaterales

- **Reconciliación**: mientras existen las filas de carga de hoy, las ~800 Diarias viejas del mock de junio aparecen como "desaparecidas" y la app **bloquea** el cierre en lote (> 30%).
  La suite solo lo **lee**; se normaliza al borrar las filas QA en la limpieza (F11-06 lo verifica). **Si alguien está mirando esa pantalla en DEV mientras corre, va a ver el bloqueo.**
- Crea y borra jobs de importación de Smelter `QA-E2E-INT <runid> job N` (con su archivo), un producto y un fiador QA.
- Los flows y plugins del servidor corren de verdad (reparto con `RepartoAsignacion`, compartidos, numeración IN-/PG-/AS-): los consecutivos avanzan aunque se borren los registros.

## Reglas que respeta (no negociables)

- **Smelter**: solo se *usa* por su API para correr un job con un CSV QA. No se toca su código, plantilla, mapeos, soluciones, flows ni apps.
- Todo lo que se crea lleva el prefijo **`QA-E2E-INT`** y se borra al final, también si la corrida falla (cada fase corre dentro de `try/catch`, la limpieza en `finally`, y hay un registro de ids por corrida).
  El borrado se hace **solo** por ese prefijo (o por lo que cuelga del cliente/cuentas QA): imposible rozar `QA-R1` / `IN-1065`, `AS-2077`, `AS-2134` ni el agente (F11-04/05 lo comprueban).
- **Nunca** se pulsa el cierre en lote de Reconciliación y **nunca** se vincula/desvincula un agente.
- **Nunca** se imprime ni se guarda un número de tarjeta completo: los PAN ficticios viven en memoria y en el CSV temporal (se borra apenas se importa); todo texto de salida pasa por un
  enmascarador de secuencias de 13 a 19 dígitos; no se capturan pantallas del detalle de BackOffice.
- **Un solo Chromium a la vez**: cada fase de UI abre un navegador, lo cierra al terminar y no corre nada en paralelo.
- Solo corre contra DEV, con las sesiones existentes.

## Hallazgos conocidos que esta suite deja a la vista

- **F7-07 (defecto de la app del cobrador):** una cuenta con **dos asignaciones activas visibles** (por ejemplo, Diaria + Especial aprobada, como la cuenta A de esta prueba) **no permite registrar pago ni actualizar datos**:
  la pantalla de interacción pre-marca las dos filas de la misma cuenta y no deja desmarcarlas, y "Aplica pago" queda deshabilitado. La suite deja ese check en ✗ y sigue cobrando sobre la cuenta B (Incobrable)
  para poder recorrer BackOffice y los cierres.
- **F2-04 / F9-05 / F9-07 (intermitente, ~40% de las corridas en DEV, 2026-10-06):** en algunas corridas User5 **ya puede leer** la cuenta, la bóveda y la asignación de la Especial C **Pendiente** sin que haya
  ningún compartido (`RetrieveSharedPrincipalsAndAccess` vacío, dueño SYSTEM, `RetrievePrincipalAccess` de User5 = `ReadAccess`), y al cerrar B y C **no pierde** ese acceso (≥ 15 min). En las corridas "buenas" el mismo escenario da 403 y se revoca en ≤ 20 s.
  La suite deja el diagnóstico (dueño, compartidos, acceso efectivo) en las notas del resumen. No se pudo atribuir al flow ni a la suite: queda abierto.
- **DAT-01 (conocido):** Smelter mapea `RetiroNIO`, `TipoEspecial` y `Nota` solo a la **cuenta**; la asignación Especial del CSV llega sin retiro ni notas (queda anotado en F1).

## Estructura

```
run.js            runner (fases, --hasta/--desde, limpieza en finally)
lib/ctx.js        informe ✓/✗, registro de ids, utilidades (esperar, sinPAN)
lib/csv.js        F0: CSV y PAN ficticios
lib/smelter.js    importación por la API de Smelter
lib/datos.js      inventario por prefijo, limpieza, lectura suplantando a un usuario
lib/ui.js         abrir/cerrar apps (un Chromium a la vez), capturas seguras, filas de galería
fases/*.js        carga (F0-F2) · admin (F3, F9) · solicitante (F4, F10) · supervisor (F5, F6) · cobrador (F7) · backoffice (F8)
```

## Matriz de roles contra el servidor ("rol por pantalla")

```bash
cd tests/integral && NODE_PATH=../backoffice-canvas/node_modules node seguridad/matriz.js
```

Sin navegador: se hace pasar por User1 a User5 (nunca User6) y en cada tabla de CM intenta crear, leer, editar, borrar, reasignarse y
compartir, más los casos de columna de la asignación y la interacción y el alta de un job de Smelter. Compara con
`seguridad/esperado.js` (lo que cada rol debería poder hacer según sus pantallas, con el porqué) y escribe
[docs/auditoria/matriz-roles.md](../../docs/auditoria/matriz-roles.md). Solo datos QA propios, borrados al final. Toma el lock de DEV
(`local/dev.lock`) igual que las suites: su limpieza por prefijo borraría los datos de una corrida en paralelo.
`--solo-informe resultados/matriz-<id>` rearma el informe sin volver a correr.
