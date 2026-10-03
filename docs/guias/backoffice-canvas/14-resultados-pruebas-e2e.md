# Guía 14 — Resultados de pruebas E2E · App - CM - Backoffice Canvas

> Corrida automatizada con **Playwright sobre el PLAYER real** (no Studio), usuarios reales por rol y
> verificación de cada número/escritura contra **Dataverse** (Web API con el Service Principal).
> Suite: [`tests/backoffice-canvas/`](../../../tests/backoffice-canvas/README.md) · plan base: guía 13.
> Prioridad del dueño: **cero problemas de DELEGACIÓN** → sección 4.
>
> **Corrida 2 (post-fixes, 2026-10-03 mediodía) → [sección 9](#9-corrida-2-post-fixes).** Las secciones 1–8 son la
> corrida 1 y se conservan como histórico.
>
> **Corrida 3 (sesiones sin caché de app, 2026-10-03 tarde) → [sección 10](#10-corrida-3-sesiones-sin-caché-de-app).**
> Corrige el hallazgo B-15 de la corrida 2: la versión publicada SÍ tenía los fixes; lo que estaba viejo era la
> copia cacheada por el player en las sesiones de prueba.

## 1. Datos de la corrida

| Ítem | Valor |
|---|---|
| Fecha | 2026-10-03 (corrida completa ~07:30–08:05 hora Managua (~35 min); re-chequeo de fases B+C 08:05–08:18) |
| Entorno | DEV — `org36e60d9d.crm.dynamics.com` · env `9c8994b8-b1bc-ee23-9184-2f6346502613` |
| App | `App - CM - Backoffice Canvas` · registro canvasapp `sanic_appcmbackofficecanvas_d0162` · **appversion `2026-10-03T12:46:12Z`** (= lastpublishtime) |
| Player | `apps.powerapps.com/play/e/9c8994b8-…/a/87fff00d-b086-4655-9166-7a7450015fe5` |
| Navegador | Chromium headless (Playwright 1.63), 1600×950, `timezoneId America/Managua` (UTC−6) |
| Resultados crudos | `tests/backoffice-canvas/resultados/corrida-completa/` (`resultados.json`, `resumen.md`, `log.txt`, `capturas/`) y `…/corrida-BC-recheck/` — carpeta **gitignoreada** |
| Datos de prueba | todo con prefijo `QA-E2E` (1–2 clientes, 2 cuentas, 1 teléfono, 2 interacciones pendientes con 1 pago y 1 actualización de datos, 2–3 Especiales, 1 fila de carga diaria "hoy" temporal). Borrado al final (§7) |

## 2. Usuarios y roles

| Usuario | Team(s) base | Rol efectivo en la corrida | Nota |
|---|---|---|---|
| User1 | CM · Backoffice · DEV | — | **Licencia de Power Apps vencida** ("Your Power Apps trial has expired"; sin más extensiones disponibles) → R-02 BLOQUEADO |
| User2 | CM · Solicitantes · DEV | — | **Licencia vencida** (la extensión exige aceptar términos y cargar un teléfono a nombre del usuario → **no se hizo**) → R-03 BLOQUEADO |
| User3 | CM · Supervisores · DEV | Supervisor (fase C) · **Solicitante sustituto** (fase B: movido TEMPORALMENTE a Solicitantes y fuera de Supervisores) | |
| User4 | CM · Admin y Carga · DEV | Admin (A, F, X) · **Backoffice sustituto** (D: solo Backoffice) · **multi-rol Backoffice+Admin** (E = R-06) | R-06 se pidió con User1; se hizo con User4 por la licencia |
| User10 | CM · Admin y Carga · DEV | — (no usado) | estaba en el team al inicio; se respetó |
| admin | — | — | **sesión no usada** (mandato) |

Las membresías se cambiaron por Web API (`teammembership_association/$ref`) con 30 s de propagación y se
restauraron a la foto inicial (verificado, §7). Los IDs marcados `*` (R-02*, R-03*) son los escenarios de rol
corridos con sustituto.

## 2.1 Veredicto

**NO ACEPTABLE para UAT.** La **delegación está bien resuelta** (todas las listas, conteos, sumas y búsquedas
viajan con `$filter`/`$apply`/`startswith` del lado servidor, sin descargas completas ni N+1), pero hay
**4 bugs críticos/altos que bloquean los flujos de 3 de los 4 roles**: el Backoffice no puede grabar
(permiso, B-02), el Solicitante no puede elegir cuenta y el Supervisor no puede elegir localizador
(combos, B-04), y el conteo "filas de carga hoy" da 0 el día de la carga (B-07). Detalle en §5.

## 3. Resultados por escenario

Leyenda: ✅ PASÓ · ❌ FALLÓ · ⛔ BLOQUEADO. Evidencia relativa a `tests/backoffice-canvas/resultados/corrida-completa/`.

| ID | Descripción | Usuario (rol) | Estado | Evidencia / detalle |
|---|---|---|:---:|---|
| **D-01** | Intercepción de red por pantalla: listas/conteos delegados, sin GET sin filtro a tablas grandes, sin N+1 | todos | ❌ | **Delegación: OK en las 11 pantallas** (ver §4). Falla SOLO por respuestas **HTTP 403** de Dataverse (permisos, B-02/B-05) |
| D-02a | Inicio Admin: KPIs con valor en la PRIMERA carga | User4 (Admin) | ❌ | KPIs vacíos y **0 consultas** del OnVisible (B-01) |
| D-02b | Inicio Admin (tras volver): Fuera de cartera, Agentes sin usuario, Carga de hoy, Cobrado hoy = API | User4 (Admin) | ✅ | 0 = 0 · 16 = 16 · "sin carga" · C$ 250.00 = Sum API · `D-02b-admin-inicio-tibio.png` |
| D-02c | Monitoreo: filas de carga hoy, cuentas, clientes = API | User4 (Admin) | ✅ | 0 · 1,004 · 959 (incluye QA) · `D-02c-monitoreo.png` (sin carga hoy el bug B-07 no se manifiesta → D-07) |
| D-02d | Cierres: contadores de pestañas = API | User4 (Admin) | ✅ | Incobrables 151 = 151 · Especiales 47 = 47 · `D-02d-cierres.png` |
| D-02e | Inicio Solicitante: PENDIENTES/APROBADAS/RECHAZADAS = API | User3 (Sol.*) | ❌ | Frío vacío (B-01); tibio 0/0/0 = API ✅ |
| D-02f | Inicio Supervisor: POR APROBAR = API | User3 (Sup.) | ❌ | Frío vacío (B-01); tibio 3 = API ✅ |
| D-02g | Inicio Backoffice: EN COLA/CON PAGO/TRANSCRITAS HOY = API | User4 (BO*) | ❌ | Frío vacío (B-01); tibio 6/1/0 = API ✅ |
| D-02h | Estación: chip "Todos" y encabezado "PENDIENTES · n" = API | User4 (BO*) | ✅ | 6 = 6 · `D-02h-transcripcion.png` |
| D-03 | Maestros: búsqueda por prefijo de CIF y de nombre encuentra un cliente fuera de la 1.ª página (puesto 701/958) | User4 (Admin) | ✅ | `startswith(sanic_cm_cl_cif,…)` y `startswith(sanic_cm_cl_nombre,…)` en `$filter` · `D-03-busqueda-*.png` |
| D-04 | Nueva solicitud: la búsqueda del combo de cuentas viaja con StartsWith | User3 (Sol.*) | ✅ | `startswith(sanic_cm_ct_numerodecuenta,'QA-E2E-CTA-00')` → servidor devuelve las 2 cuentas QA (pero el combo no las muestra: F-02a) |
| D-07 | Con 1 fila QA de carga con FechaCarga = HOY: "FILAS DE CARGA HOY" = API | User4 (Admin) | ❌ | App **0** vs API **1** (B-07) · `D-07-monitoreo-carga-hoy.png` |
| R-02 | User1 (Backoffice) ve solo su nav | User1 | ⛔ | Licencia vencida · `R-02-user1-licencia.png` |
| R-02* | Backoffice ve solo su nav (3 ítems) + sección BO de Inicio | User4 (BO*) | ✅ | `R-02-backoffice-inicio.png` |
| R-03 | User2 (Solicitante) ve solo su nav | User2 | ⛔ | Licencia vencida · `R-03-user2-licencia.png` |
| R-03* | Solicitante ve solo su nav (3 ítems) + sección Sol. | User3 (Sol.*) | ✅ | `R-03-solicitante-inicio.png` |
| R-04 | Supervisor ve solo su nav (2 ítems) + sección Sup. | User3 (Sup.) | ✅ | `R-04-supervisor-inicio.png` |
| R-05 | Admin ve solo su nav (6 ítems) + sección Admin | User4 (Admin) | ✅ | `R-05-admin-inicio.png` |
| R-06a | MULTI-ROL Backoffice+Admin: nav combinada (8 ítems, orden nfNav), Inicio con ambas secciones, pie "BackOffice · Admin", abre Transcripción y Maestros | User4 (BO+Admin) | ✅ | `R-06a-multirol-inicio.png` |
| R-06b | Quitado del team Backoffice → vuelve a solo Admin | User4 (Admin) | ✅ | `R-06b-solo-admin.png` |
| E-01a..d | Abrir la app sin banners de error de runtime (Admin / Sol. / Sup. / BO) | todos | ❌ ×4 | Todos: "Incorrect argument type, expected a table." (B-06). Sol./Sup.: además 403 Carga diarias (B-05) · `E-01*-errores.png` |
| F-01 | Backoffice transcribe 2 interacciones QA (Enter y botón) | User4 (BO*) | ❌ | Detalle, FIFO, pago (C$ 250.00 · Recibo QA-E2E-REC-1), bloque ✏️ antes→nuevo y chip NUEVO ✅; **Patch 403** sin `prvAppend` (B-02) y la app igual anuncia "✓ grabada" (B-03) · `D-F-01-patch-fallido.png` |
| F-01m | Mismo flujo con Backoffice+Admin | User4 (BO+Admin) | ✅ | Estado Grabado, código en MAYÚSCULAS (`qae2e1`→`QAE2E1`), grabado por = User4, fecha hoy; avance a la siguiente en la misma posición; input limpio; "¡cola vacía!" y el localizador QA desaparece · `F-01-*.png` |
| F-01b | Transcritas hoy: KPI = API y aparecen las 2 QA con su pago | User4 (BO+Admin) | ✅ | TRANSCRITAS 2 = API · PAGOS TRANSCRITOS C$ 250.00 · `F-01b-transcritas.png` |
| F-02a | El combo de cuentas muestra lo que devolvió el servidor y deja elegir | User3 (Sol.*) | ❌ | 15 opciones **sin texto** al abrir; tras buscar, flyout **"sin resultados"** (el servidor devolvió 2) (B-04) |
| F-02 | Solicitante crea una Especial QA y aparece en Mis solicitudes | User3 (Sol.*) | ❌ | Imposible por UI (B-04). Para no bloquear C/E se creó la MISMA Especial por API (owner User3) |
| F-02b | Anti-duplicado (segunda solicitud misma cuenta) | User3 (Sol.*) | ⛔ | Depende del combo (B-04) |
| F-03 | Supervisor aprueba la Especial QA asignando localizador | User3 (Sup.) | ❌ | "Aprobar" deshabilitado sin localizador ✅; el combo de agentes muestra **opciones vacías** y "sin resultados" al buscar "Carlos" (B-04) · `F-03-combo-agente.png` |
| F-03c | Comp-14: el owner pasa al usuario del agente | — | ❌ | Flow "Cloud Flow \| CM \| Cambio de Owner en Asignaciones" en **Draft (statecode 0)** (B-10); además no se pudo aprobar por UI |
| F-03b | Supervisor rechaza otra Especial QA con motivo | User3 (Sup.) | ✅ | Deshabilitado sin motivo y con solo espacios ✅ · Rechazada (4) + motivo + resolutor = User3 · sale de la cola sin recargar (re-chequeo B+C; en la corrida completa el check estaba mal escrito y se corrigió) · `corrida-BC-recheck/capturas/F-03b-rechazada.png` |
| F-04 | Admin cierra la Especial QA con motivo + detalle | User4 (BO+Admin) | ✅ | Motivos de Especial correctos; deshabilitado sin motivo; 1 solo PATCH; Estado=false, Motivo "Gestión completada — QA-E2E cierre F-04", Fecha de cierre hoy · `F-04-cerrada.png` |
| F-04b | Maestros: crear cliente QA con teléfono + anti-duplicado de CIF | User4 (Admin) | ✅ | Cliente + teléfono Celular con lookup y CIF denormalizado; reintento → "Ya existe un cliente con CIF…" y sigue 1 solo · `F-04b-*.png` |
| F-04c | Agentes (solo lectura): alerta "sin usuario" = API y abrir la pantalla NO escribe | User4 (Admin) | ❌ | Alerta 16 = API y nombres completos ✅; **abrir la pantalla dispara 2 PATCH** a agentes reales (B-08). Valores verificados idénticos antes/después |
| F-05 | Reconciliación sin carga de hoy: % vs API y cierre en lote bloqueado | User4 (Admin) | ✅ | "0 de 801 (0%)" = API; card ⛔ visible; sin lista no hay botón que armar · `F-05-reconciliacion.png` |
| F-05b | Reconciliación CON carga de hoy (fila QA): % vs API y freno > 30 % | User4 (Admin) | ✅ | "801 de 801 (100%)" = API; card ⛔ oculta; casilla y botón **deshabilitados** (umbral rojo). **No se cerró nada** · `F-05b-reconciliacion-carga-hoy.png` |
| F-06 | Re-solicitar Especial para una cuenta cuya Especial anterior está CERRADA | API (UI bloqueada por B-04) | ❌ | Dataverse rechaza: **412 "Alt Key \| CM \| Numero Cuenta x Tipo violated"** (B-11) |
| X-01 | Responsive a 800×900: barra móvil + hamburguesa, nav oculta hasta abrirla | User4 (Admin) | ❌ | Sin barra móvil ni hamburguesa; la app **escala** (nav 141 px en vez de 240) (B-09) · `X-01-*.png` |

**Totales:** 40 escenarios (E-01a..d cuentan como 4; F-03b según el re-chequeo) → ✅ 19 · ❌ 18 · ⛔ 3.

## 4. DELEGACIÓN (detalle)

### 4.1 Cómo se midió

El player manda TODO a Dataverse como `POST …/api/data/v9.0/$batch` (multipart). La suite intercepta con
`context.route()` y lee el **GET interno** de cada batch (el `$filter`/`$apply` real que ejecuta el
servidor) y la respuesta (filas, `result`, `nextLink`, HTTP status). Cada request queda etiquetada con la
pantalla y el rol en el momento en que se hizo. Clasificación:

- **DELEGADA** — lleva `$filter`, `$apply` (agregado: `CountIf`→`aggregate($count)`, `Sum`→`aggregate(x with sum)`), `$top` (`First(Sort())`) o es por clave/relación.
- **Alerta** — GET a tabla grande (interacciones, asignaciones, cuentas, clientes, pagos, carga diaria, teléfonos, direcciones, actualizaciones) SIN nada de lo anterior, o N+1 (≥ 10 consultas por clave distinta a la misma tabla en una pantalla).

### 4.2 Conclusión

- **Cero GET sin filtro a tablas grandes, cero N+1, cero descargas completas.** Todos los KPI, contadores de
  pestañas, denominadores y sumas viajan como `$apply=…/aggregate(...)` (conteo EXACTO en servidor); todas las
  colas/listas con `$filter`; los joins de cuenta/cliente con `$expand` en la MISMA consulta (no lookup por fila);
  las búsquedas con `startswith(...)` en `$filter`; `nfUltimaCarga` con `$orderby desc&$top=1`.
- **Todos los números comparados contra `$count`/`$apply` de la Web API coincidieron** (cuando la app llegó a
  pedirlos: ver B-01), incluidos D-03 (cliente en el puesto 701 de 958 encontrado por CIF y por nombre).
- **Hallazgos de delegación que SÍ requieren acción** (no son "subrayado azul" sino semántica/cota):
  1. **B-07** — `CountIf('[CD] FechaCarga' >= Today())` delega, pero el player convierte `Today()` a
     `2026-10-03T06:00:00Z` sobre una columna **DateOnly** (guardada como `…T00:00:00Z`) → el día de la carga cuenta **0**.
  2. **B-12** — la **cota real de las colecciones es 500, no 2000**: `colDesapRec` trajo 500 filas con
     `@odata.nextLink` que el player NO siguió, y la pantalla dijo "Mostrando las primeras 500" (con 801 desaparecidas).
     El `.msapr` del repo declara `DefaultConnectedDataSourceMaxGetRowsCount = 2000`, pero la app publicada se comporta con 500.
     Afecta a toda lista de trabajo (cola de transcripción, Cierres, Aprobaciones, Mis solicitudes) si superan 500.
  3. **Combo de cuentas al abrir** (`SearchText` vacío): `StartsWith(x, "")` se optimiza a **sin `$filter`** →
     trae la 1.ª página de 500 cuentas con `nextLink` (no la tabla entera). Paginado de servidor, aceptable, pero es
     payload innecesario (y con B-04 ni siquiera se muestra).
  4. **Búsqueda de Maestros por tecla**: 13 consultas `startswith` para un CIF de 14 caracteres (todas delegadas).
     Sugerencia: `DelayOutput = true` en `txtBuscarMae` (y en el combo).
  5. `scrAgentes` trae `Users` completo (172 filas, `Items = Users` del combo): catálogo chico hoy; si el tenant
     crece >500 usuarios, el combo se truncaría (B-12).
- **B-01 no es delegación sino orden de ejecución**: en la primera carga el `OnVisible` de Inicio **no hace
  ninguna consulta** (gblRol todavía vacío) — los KPIs quedan en blanco para todos los roles.

### 4.3 Requests por pantalla (corrida completa, deduplicadas; literales y GUID enmascarados)

`res=` es el resultado del agregado; el número solo = filas devueltas. Hora "06:00Z" = `Today()` local (Managua).

| Pantalla | Rol | Método | Consulta (OData interno del $batch) | Veredicto | Resultado |
|---|---|---|---|---|---|
| scrInicio | Admin | GET | `systemusers?$filter=internalemailaddress eq '…'&$top=1` | DELEGADA ($filter en servidor) | 1 |
| scrInicio | Admin | GET | `sanic_cm_tbl_cargadiarias?$orderby=sanic_cm_cd_fechacarga desc&$top=1` | DELEGADA ($top=1 + $orderby) | 1 |
| scrMonitoreo | Admin | GET | `sanic_cm_tbl_cargadiarias?$apply=filter(sanic_cm_cd_fechacarga ge 2026-10-03T06:00Z)/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrMonitoreo | Admin | GET | `sanic_cm_tbl_cuentas?$apply=aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=1004 |
| scrMonitoreo | Admin | GET | `sanic_cm_tbl_clientes?$apply=aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=959 |
| scrInicio | Admin | GET | `sanic_cm_tbl_pagos?$apply=filter((sanic_cm_pg_fecha ge 2026-10-03T06:00Z)and(sanic_cm_pg_moneda eq 1))/aggregate(sanic_cm_pg_monto with sum as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=250 |
| scrInicio | Admin | GET | `sanic_cm_tbl_pagos?$apply=filter((sanic_cm_pg_fecha ge 2026-10-03T06:00Z)and(sanic_cm_pg_moneda eq 2))/aggregate(sanic_cm_pg_monto with sum as result)` | DELEGADA ($apply agregado/filtro en servidor) | 1 |
| scrInicio | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))and(sanic_cm_as_ultimacarga lt 2026-06-20T00:00:00.000Z))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrInicio | Admin | GET | `sanic_cm_tbl_agentes?$apply=filter((sanic_cm_ag_activo eq true)and(sanic_cm_ag_emailusuario eq null))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=16 |
| scrReconciliacion | Admin | GET | `sanic_cm_tbl_asignacions?$filter=((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))and(sanic_cm_as_ultimacarga lt 2026-06-20T00:00:00.000Z)&$expand=sanic_cm_as_cuentaid(…)` | DELEGADA ($filter en servidor) | 0 |
| scrReconciliacion | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))and(sanic_cm_as_ultimacarga lt 2026-06-20T00:00:00.000Z))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrReconciliacion | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=801 |
| scrCierres | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter((sanic_cm_as_tipo eq 2)and(sanic_cm_as_estado eq true))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=151 |
| scrCierres | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter((sanic_cm_as_tipo eq 3)and(sanic_cm_as_estado eq true))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=47 |
| scrCierres | Admin | GET | `sanic_cm_tbl_asignacions?$filter=((sanic_cm_as_tipo eq 2)or(sanic_cm_as_tipo eq 3))and(sanic_cm_as_estado eq true)&$expand=sanic_cm_as_cuentaid(…)` | DELEGADA ($filter en servidor) | 198 |
| scrAgentes | Admin | GET | `sanic_cm_tbl_agentes?$apply=filter((sanic_cm_ag_activo eq true)and(sanic_cm_ag_emailusuario eq null))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=16 |
| scrAgentes | Admin | GET | `systemusers?$select=fullname,internalemailaddress,systemuserid` | SIN FILTRO (tabla chica/catálogo) | 172 |
| scrAgentes | Admin | GET | `sanic_cm_tbl_agentes?$orderby=sanic_cm_ag_nombre asc` | SIN $filter (tabla chica) | 18 |
| scrAgentes | Admin | GET | `systemusers?$filter=internalemailaddress eq '…'` | DELEGADA ($filter en servidor) | 1 |
| scrAgentes | Admin | GET | `sanic_cm_tbl_agentes?$filter=sanic_cm_ag_nombre eq '…'&$top=1` | DELEGADA ($filter en servidor) | 1 |
| scrAgentes | Admin | PATCH | `sanic_cm_tbl_agentes(<id>)` | ESCRITURA |  |
| scrMaestros | Admin | GET | `sanic_cm_tbl_cuentas?$filter=sanic_cm_ct_clienteid/sanic_cm_tbl_clienteid eq null` | DELEGADA ($filter en servidor) | 0 |
| scrMaestros | Admin | GET | `sanic_cm_tbl_clientes?$orderby=sanic_cm_cl_nombre asc` | DELEGADA (Sort en servidor + paginado: solo 1.ª página) | 100 |
| scrMaestros | Admin | GET | `sanic_cm_tbl_clientes?$filter=(startswith(sanic_cm_cl_cif,'…'))or(startswith(sanic_cm_cl_nombre,'…'))` | DELEGADA ($filter en servidor) | 100 |
| scrMaestros | Admin | GET | `sanic_cm_tbl_clientes?$filter=sanic_cm_cl_cif eq '…'&$top=1` | DELEGADA ($filter en servidor) | 0 |
| scrMaestros | Admin | POST | `sanic_cm_tbl_clientes` | ESCRITURA |  |
| scrMaestros | Admin | POST | `sanic_cm_tbl_telefonos` | ESCRITURA |  |
| scrMisSolicitudes | Solicitante | GET | `sanic_cm_tbl_cargadiarias?$orderby=sanic_cm_cd_fechacarga desc&$top=1` | DELEGADA ($top=1 + $orderby) | **HTTP 403** |
| scrMisSolicitudes | Solicitante | GET | `sanic_cm_tbl_asignacions?$filter=(sanic_cm_as_tipo eq 3)and(sanic_cm_as_solicitadopor/systemuserid eq <id>)&$expand=sanic_cm_as_cuentaid(…)` | DELEGADA ($filter en servidor) | 0 |
| scrInicio | Solicitante | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 3)and(sanic_cm_as_solicitadopor/systemuserid eq <id>))and(sanic_cm_as_estadoaprobacion eq 2))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrInicio | Solicitante | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 3)and(sanic_cm_as_solicitadopor/systemuserid eq <id>))and(sanic_cm_as_estadoaprobacion eq 3))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrInicio | Solicitante | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 3)and(sanic_cm_as_solicitadopor/systemuserid eq <id>))and(sanic_cm_as_estadoaprobacion eq 4))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrNuevaSolicitud | Solicitante | GET | `sanic_cm_tbl_cuentas?$select=sanic_cm_ct_diasmora,sanic_cm_ct_nivelmora,sanic_cm_ct_nombrecliente,sanic_cm_ct_numerodecuenta,sanic_cm_ct_saldovencidonio,sanic_cm_ct_saldovencidousd,sanic_cm_ct_tipopro` | SIN $filter pero PAGINADO en servidor (1.ª página: 500 filas + nextLink) — revisar | 500 |
| scrNuevaSolicitud | Solicitante | GET | `sanic_cm_tbl_cuentas?$filter=(startswith(sanic_cm_ct_numerodecuenta,'…'))or(startswith(sanic_cm_ct_nombrecliente,'…'))` | DELEGADA ($filter en servidor) | 2 |
| scrAprobaciones | Supervisor | GET | `sanic_cm_tbl_cargadiarias?$orderby=sanic_cm_cd_fechacarga desc&$top=1` | DELEGADA ($top=1 + $orderby) | **HTTP 403** |
| scrAprobaciones | Supervisor | GET | `sanic_cm_tbl_agentes?$filter=sanic_cm_ag_activo eq true` | DELEGADA ($filter en servidor) | 18 |
| scrAprobaciones | Supervisor | GET | `sanic_cm_tbl_asignacions?$filter=(sanic_cm_as_tipo eq 3)and(sanic_cm_as_estadoaprobacion eq 2)&$expand=sanic_cm_as_cuentaid(…)` | DELEGADA ($filter en servidor) | 3 |
| scrInicio | Supervisor | GET | `sanic_cm_tbl_asignacions?$apply=filter((sanic_cm_as_tipo eq 3)and(sanic_cm_as_estadoaprobacion eq 2))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=3 |
| scrAprobaciones | Supervisor | GET | `sanic_cm_tbl_asignacions?$filter=sanic_cm_as_numero eq '…'&$top=1` | DELEGADA ($filter en servidor) | 1 |
| scrAprobaciones | Supervisor | PATCH | `sanic_cm_tbl_asignacions(<id>)` | ESCRITURA |  |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_pagos?$filter=sanic_cm_pg_numerointeraccion eq null` | DELEGADA ($filter en servidor) | 1 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_interaccions?$filter=sanic_cm_in_estadograbacion eq 1&$expand=sanic_cm_in_cuentaid(…)` | DELEGADA ($filter en servidor) | 6 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_interaccions?$apply=filter(sanic_cm_in_estadograbacion eq 1)/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=6 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_pagos?$filter=sanic_cm_pg_numerointeraccion eq '…'` | DELEGADA ($filter en servidor) | 0 |
| scrInicio | BackOffice | GET | `sanic_cm_tbl_interaccions?$apply=filter(sanic_cm_in_estadograbacion eq 1)/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=6 |
| scrInicio | BackOffice | GET | `sanic_cm_tbl_interaccions?$apply=filter((sanic_cm_in_estadograbacion eq 1)and(sanic_cm_cd_aplicapago eq true))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=1 |
| scrInicio | BackOffice | GET | `sanic_cm_tbl_interaccions?$apply=filter(((sanic_cm_in_estadograbacion eq 2)and(sanic_cm_in_fechagrabacion ge 2026-10-03T06:00Z))and(sanic_cm_in_grabadopor/systemuserid eq <id>))/aggregate($count as re` | DELEGADA ($apply agregado/filtro en servidor) | res=0 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_interaccions?$filter=sanic_cm_in_numerointeraccion eq '…'&$top=1` | DELEGADA ($filter en servidor) | 1 |
| scrTranscripcion | BackOffice | PATCH | `sanic_cm_tbl_interaccions(<id>)` | ESCRITURA | **HTTP 403** |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_actualizaciondatoses?$filter=sanic_cm_ad_interaccionid/sanic_cm_tbl_interaccionid eq <id>` | DELEGADA ($filter en servidor) | 1 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_telefonos?$filter=sanic_cm_tl_cif eq '…'` | DELEGADA ($filter en servidor) | 1 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_direccions?$filter=sanic_cm_dr_cif eq '…'` | DELEGADA ($filter en servidor) | 0 |
| scrTranscripcion | BackOffice | GET | `sanic_cm_tbl_interaccions?$filter=sanic_cm_in_numerointeraccion eq null&$top=1` | DELEGADA ($filter en servidor) | 0 |
| scrTranscritas | BackOffice+Admin | GET | `sanic_cm_tbl_pagos?$filter=sanic_cm_pg_fecha ge 2026-10-03T06:00Z` | DELEGADA ($filter en servidor) | 1 |
| scrTranscritas | BackOffice+Admin | GET | `sanic_cm_tbl_interaccions?$apply=filter(((sanic_cm_in_estadograbacion eq 2)and(sanic_cm_in_fechagrabacion ge 2026-10-03T06:00Z))and(sanic_cm_in_grabadopor/systemuserid eq <id>))/aggregate($count as re` | DELEGADA ($apply agregado/filtro en servidor) | res=2 |
| scrTranscritas | BackOffice+Admin | GET | `sanic_cm_tbl_interaccions?$filter=((sanic_cm_in_estadograbacion eq 2)and(sanic_cm_in_fechagrabacion ge 2026-10-03T06:00Z))and(sanic_cm_in_grabadopor/systemuserid eq <id>)&$expand=sanic_cm_in_cuentaid(` | DELEGADA ($filter en servidor) | 2 |
| scrCierres | BackOffice+Admin | GET | `sanic_cm_tbl_asignacions?$filter=sanic_cm_as_numero eq '…'&$top=1` | DELEGADA ($filter en servidor) | 1 |
| scrCierres | BackOffice+Admin | PATCH | `sanic_cm_tbl_asignacions(<id>)` | ESCRITURA |  |
| scrInicio | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))and(sanic_cm_as_ultimacarga lt 2026-10-03T00:00:00.000Z))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=801 |
| scrReconciliacion | Admin | GET | `sanic_cm_tbl_asignacions?$filter=((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))and(sanic_cm_as_ultimacarga lt 2026-10-03T00:00:00.000Z)&$expand=sanic_cm_as_cuentaid(…)` | DELEGADA ($filter en servidor) | 500 |
| scrReconciliacion | Admin | GET | `sanic_cm_tbl_asignacions?$apply=filter(((sanic_cm_as_tipo eq 1)and(sanic_cm_as_estado eq true))and(sanic_cm_as_ultimacarga lt 2026-10-03T00:00:00.000Z))/aggregate($count as result)` | DELEGADA ($apply agregado/filtro en servidor) | res=801 |

Observaciones de la tabla: las filas `HTTP 403` son de permisos (B-02, B-05), no de delegación. La consulta
`sanic_cm_tbl_pagos?$filter=sanic_cm_pg_numerointeraccion eq null` (galería de pagos sin interacción
seleccionada) devuelve 1 pago huérfano real (`PG-1000`); la galería está oculta en ese estado → sin impacto visible.

## 5. Bugs encontrados

Severidad: 🔴 CRÍTICA (bloquea un rol) · 🟠 ALTA (dato incorrecto o riesgo de datos) · 🟡 MEDIA · ⚪ BAJA.
Ninguno se corrigió (mandato: no tocar la app). Las causas marcadas "hipótesis" hay que confirmarlas en Studio.

| # | Sev. | Dónde | Qué pasa | Cómo reproducir | Evidencia |
|---|:---:|---|---|---|---|
| B-01 | 🟠 | scrInicio (todos los roles) | En la **primera carga** los KPIs de Inicio quedan **vacíos** y el "Cobrado hoy" muestra `C$ 0.00` sin consultar: el `OnVisible` de la StartScreen corre **antes** de que `App.OnStart` termine de setear `gblRol` → todos los `If("X" in gblRol, …)` son falsos → **0 requests**. Al ir a otra pantalla y volver, los KPIs cuadran exacto con la API | Abrir el player en frío con cualquier rol y mirar Inicio | D-02a/e/f/g (KPIs frío `""`, red de `X:scrInicio(frío)` sin consultas de KPI) |
| B-02 | 🔴 | Rol `SR \| CM \| BackOffice` | El rol tiene `Write` pero **NO `Append`** sobre `interaccion`; el Patch de transcripción setea el lookup `'[IN] Grabado por'` → **HTTP 403 `missing prvAppendsanic_cm_tbl_interaccion`**. Un usuario solo-Backoffice **no puede transcribir nada** | Usuario solo en el team Backoffice → Por transcribir → código + Enter | F-01, `D-F-01-patch-fallido.png`, D-01 (`D:scrTranscripcion(grabar)` 403). Con Admin+Backoffice el mismo flujo pasa (F-01m) |
| B-03 | 🟠 | scrTranscripcion · `btnGrabarTrx` | Si el Patch **falla**, la app igual muestra **"✓ IN-xxxx grabada — siguiente: …"**, **saca la interacción de la cola local**, recalcula la columna de localizadores y avanza. El banner nativo aparece, pero el operador cree que grabó y la interacción "desaparece" hasta el próximo "Actualizar" | Igual que B-02 | F-01: Notify "✓ IN-1019 grabada…" con la fila en Dataverse aún `Pendiente`. Fix: `IfError(Patch(...), Notify(error); …)` o `If(IsEmpty(Errors(...)), …)` ANTES de `Remove`/`Notify` |
| B-04 | 🔴 | `cmbCuentaNva` (Nueva solicitud) y `cmbAgenteAprb` (Aprobaciones) | Los ComboBox clásicos muestran **opciones con texto vacío** al abrir y, al tipear, el flyout queda en **"sin resultados"** aunque el servidor devolvió filas (la búsqueda de cuentas trajo 2 y 253 filas en pruebas). → **El Solicitante no puede crear una Especial** y **el Supervisor no puede aprobar** (no hay forma de elegir localizador). Hipótesis: `DisplayFields`/`SearchFields` con nombres LÓGICOS (`sanic_cm_ct_numerodecuenta`, `sanic_cm_ag_nombre`) sobre tablas armadas con `AddColumns`/colección que el runtime no resuelve (el combo de motivos de Cierres, con `["Value"]`, funciona). Revisar también `cmbUsuarioAge` (mismo patrón; no se probó para no escribir) | Solicitante → Nueva solicitud → abrir "Cuenta" → tipear "8030" · Supervisor → Aprobaciones → abrir una solicitud → "Localizador" | F-02a, F-03, `F-03-combo-agente.png`, `D-04-combo-cuentas.png` |
| B-05 | 🟠 | Roles `SR \| CM \| Solicitante` y `SR \| CM \| Supervisor` | Faltan privilegios de LECTURA: Solicitante sin Read en **cargadiaria** y **pagos**; Supervisor sin Read en **cargadiaria**. `nfUltimaCarga` se evalúa para todos → banner 403 al abrir y en cada pantalla; "Cobrado hoy" del Solicitante falla con 403 | Abrir la app con un usuario solo Solicitante o solo Supervisor | E-01b/c (`E-01*-errores.png`), D-01 (403 en `cargadiarias` y `pagos`). Fix: Read (org/BU) en esas tablas, o no evaluar `nfUltimaCarga`/Sum fuera del rol que los usa |
| B-06 | 🟡 | App (todas las pantallas, todos los roles) | Banner **"Incorrect argument type, expected a table."** al abrir. Origen no identificable desde el player. Sospechosos: `DefaultSelectedItems: =Blank()` de `cmbAgenteAprb`/`cmbMotivoCie` (esperan tabla) o `DefaultSelectedItems` con `If(…, Filter(Users, …))` de `cmbUsuarioAge` (sin rama falsa) | Abrir el player con cualquier rol | E-01a..d. Verificar en Studio con el Monitor |
| B-07 | 🟠 | scrMonitoreo · KPI "FILAS DE CARGA HOY" | `CountIf('[CD] FechaCarga' >= Today())` viaja como `sanic_cm_cd_fechacarga ge 2026-10-03T06:00:00Z`; la columna es **DateOnly / TimeZoneIndependent** (valor `2026-10-03T00:00:00Z`) → **0 filas el día de la carga**. La card verde (`nfUltimaCarga >= Today()`, en memoria) sí dice "completada" → la pantalla se contradice | Con una fila de carga con FechaCarga = hoy (la suite crea una QA): Monitoreo muestra 0 | D-07: app 0, API con la fecha = 1, API con la misma condición del player = 0 · `D-07-monitoreo-carga-hoy.png`. Fix candidato (mismo truco de 00 §4.4): comparar contra `DateAdd(Today(), TimeZoneOffset(Today()), TimeUnit.Minutes)` (= 00:00Z) — verificar la request resultante |
| B-08 | 🟠 | scrAgentes · `cmbUsuarioAge.OnChange` | **Solo abrir la pantalla escribe en Dataverse**: el `DefaultSelectedItems` (asíncrono) dispara `OnChange` en cada agente vinculado → `PATCH` de `[AG] Usuario` + `Email Usuario` (mismos valores) y `Notify "✓ … vinculado"` espurio. Riesgo: si la preselección resolviera vacía, la rama `Blank()` **desvincula** al agente | Admin → Agentes y usuarios (sin tocar nada) | F-04c: 2 PATCH (Álvaro Zeledón, Carlos Mendoza), `modifiedon` cambió; valores verificados idénticos. Fix: ejecutar el Patch solo si `Self.Selected.'Primary Email' <> ThisItem.'Email Usuario'`, o mover el vínculo a un botón |
| B-09 | 🟠 | App · configuración de pantalla | **Responsive roto**: el `.msapr` tiene `DocumentLayoutScaleToFit = true` (00 §7 exige *Escalar para ajustar = Desactivado*). A 800 px la app se **escala** (nav 141 px) y `nfMovil = App.ActiveScreen.Width < 920` nunca se cumple → sin barra móvil ni hamburguesa | Abrir el player con ventana de 800 px | X-01 · `X-01-scrInicio-cerrada.png` |
| B-10 | 🟠 | Flow Comp-14 "Cloud Flow \| CM \| Cambio de Owner en Asignaciones" | Está en **Draft (statecode 0)** (modificado 2026-10-03 12:45Z). Al aprobar, el owner no pasa al usuario del agente → sin aislamiento ni sync al celular | Consultar `workflows` por nombre | F-03c |
| B-11 | 🟡 | Dataverse · alt key `sanic_cm_altkey_numerocuentaxtipo` | La clave alternativa (tipo + número de cuenta) **no incluye el estado** → una cuenta cuya Especial se CERRÓ no puede recibir otra: **412 "Alt Key … violated"**. Contradice el comentario de `btnEnviarNva` ("una cerrada no bloquea pedir otra"): el Solicitante vería el error duro de Dataverse | Cerrar una Especial y crear otra para la misma cuenta | F-06 (hecho por Web API con la misma escritura que el Patch; la UI está bloqueada por B-04). Decidir: reactivar la existente, o cambiar la clave |
| B-12 | 🟡 | Colecciones de trabajo (todas) | **Cota efectiva 500 filas, no 2000** (00 §4.1): `colDesapRec` trajo 500 + `nextLink` no seguido; la UI lo declaró ("Mostrando las primeras 500"). Los conteos (`CountIf`) siguen exactos | Con > 500 Diarias desaparecidas (la suite lo provoca con la carga QA de hoy) | F-05b (`F-05b-reconciliacion-carga-hoy.png`), D-01 (`F:scrReconciliacion` 500 filas + nextLink). Verificar el Data row limit de la versión PUBLICADA |
| B-13 | ⚪ | `txtBuscarMae` | 1 consulta por tecla (13 para un CIF) | Tipear un CIF en Maestros | D-01 `A:scrMaestros(búsqueda CIF)`. `DelayOutput = true` |
| B-14 | ⚪ | Entorno de prueba (no la app) | User1 y User2 con la licencia trial de Power Apps vencida → no pueden abrir el player | Abrir el player con esas sesiones | R-02, R-03 (`R-0x-userN-licencia.png`). Asignar licencia o usar otros testers |

Lo que **funcionó bien** y vale la pena conservar: navegación por rol exacta para los 4 perfiles y multi-rol
(incluida la reversión), guards visuales de botones (aprobar/rechazar/cerrar/crear deshabilitados sin datos,
motivo de solo espacios no habilita), anti-duplicado de CIF, cierre con 1 solo Patch y limpieza local,
transcripción en ráfaga (Enter → siguiente, input limpio, avance en la misma posición, "¡cola vacía!"),
bloque de actualización de datos con "antes → nuevo" y chip NUEVO, arqueo de Transcritas hoy exacto, freno de
Reconciliación (card ⛔ sin carga de hoy y umbral rojo > 30 % que deshabilita casilla y botón).

## 6. Límites de esta corrida (qué NO se probó y por qué)

- **R-02/R-03 con User1/User2**: licencias vencidas (B-14). Extender el trial de User2 pedía aceptar términos de
  uso y un número de teléfono a nombre del usuario → **no se hizo**. Cubierto con sustitutos (R-02*, R-03*).
- **Aprobación por UI y owner de Comp-14 end-to-end**: bloqueados por B-04 y B-10.
- **Anti-duplicado de Especiales (F-02b) y re-solicitud por UI (F-06)**: bloqueados por B-04 (F-06 se probó por API).
- **Cierre en lote de Reconciliación**: NUNCA se ejecuta (mandato); solo se verificó que está bloqueado.
- **Vincular/desvincular agentes**: no se tocó (mandato); B-08 lo hace la app sola al abrir la pantalla.
- **> 2000 filas / D-10 de la guía 13**: DEV tiene 4–6 pendientes; la cota real observada es 500 (B-12).
- **X-02 (error nativo con privilegio quitado)**: no se modificaron roles; B-02 lo mostró de forma natural (el banner nativo aparece, pero ver B-03).
- Studio / App Checker (D-01 de la guía 13) no se usó: la sesión admin está prohibida. La delegación se verificó en la RED del player.

## 7. Limpieza (verificada)

Al final de cada corrida (y también con Ctrl+C) la suite borró por Web API todo lo `QA-E2E` en orden
pagos → actualizaciones → interacciones → asignaciones → carga → cuentas → teléfonos/direcciones → clientes,
y luego barrió por prefijo. Verificación independiente posterior (consulta `$count` por prefijo `QA`/`qa-e2e`
en las 9 tablas): **0 registros en todas**.

| Verificación | Resultado |
|---|---|
| Restos QA-E2E (clientes, cuentas, interacciones, asignaciones, pagos, teléfonos, direcciones, carga diaria, actualizaciones) | **0 / 0 / 0 / 0 / 0 / 0 / 0 / 0 / 0** |
| Totales de DEV vs inicio | pendientes 4 · asignaciones 1003 · cuentas 1002 · clientes 958 · carga diaria 1000 · pagos 3 — **idénticos al inicio** |
| Membresías de teams | Backoffice = [User1] · Solicitantes = [User2] · Supervisores = [User3] · Admin y Carga = [User10, User4] — **idénticas a la foto inicial** (`restaurarMembresias` → `iguales: true`) |
| Agentes reales | Carlos Mendoza → user1@… / Álvaro Zeledón → admin@… — usuario, email y activo **sin cambios** (B-08 los re-escribió con los mismos valores; `modifiedon` quedó actualizado) |
| Fila QA de "carga de hoy" | creada solo durante la fase F y borrada al terminarla (HTTP 204) |

## 8. Cómo re-ejecutar

```bash
cd tests/backoffice-canvas
npm install && npx playwright install chromium      # una vez
node run.js                                          # ~35 min; resultados en ./resultados (gitignoreada)
node run.js --solo-limpieza                          # si una corrida se cortó
```

Después de corregir: B-02/B-05 (roles), B-04 (combos), B-07 (fecha DateOnly), B-01 (orden OnStart/OnVisible)
y B-09 (escalado), re-correr la suite completa; los escenarios hoy en ❌ por esas causas deberían pasar sin
cambiar el test (F-02/F-02b/F-03 ya tienen la ruta por UI escrita y la ruta por API es solo el respaldo; F-06 se prueba por API contra la alt key).

---

## 9. Corrida 2 (post-fixes)

### 9.1 Datos de la corrida

| Ítem | Valor |
|---|---|
| Fecha | 2026-10-03 — corrida completa 11:59–12:10 hora Managua (UTC−6) + re-chequeo de fases B, C, F y G 12:10–12:18 |
| App | `sanic_appcmbackofficecanvas_d0162` · **appversion `2026-10-03T17:42:41Z`** (= lastpublishtime). ⚠ Lo que el player sirvió a las sesiones fue una **copia cacheada más vieja** (ver B-15 y la sección 10) |
| Usuarios | **mapeo real y fijo, sin sustitutos**: User1 → Backoffice · User2 → Solicitantes · User3 → Supervisores · User4 → Admin y Carga (+ User10, ajeno). El único cambio temporal fue R-06: User1 + "Admin y Carga" en la fase E, quitado en la misma fase |
| Resultados crudos | `tests/backoffice-canvas/resultados/corrida-2/` y `…/corrida-2-recheck-BCFG/` (gitignoreadas) |
| Cambios de la suite | sin fase L ni sustitutos. Nuevos escenarios: X-02 (falla controlada, B-03), F-02d (reactivar una Especial cerrada propia, B-11), F-02e (visibilidad tras aprobar o cerrar), D-05 (mínimo privilegio, B-05) y F-02c por UI. Checks nuevos: botón "Desvincular" en F-04c, B-13 en D-03, B-12 en F-05b, KPIs en frío **= API** (antes solo "no vacío"), 403 en la red al abrir (E-01). F-03c aprueba por API si la UI no puede (B-04), para observar igual el flow |

### 9.2 Veredicto

**NO ACEPTABLE — pero por un motivo distinto al que se escribió originalmente.**

> **Corrección (corrida 3):** este apartado decía "la versión publicada no tiene los fixes de código" (B-15). **Eso
> era falso.** La app publicada sí los tenía. Lo que se probó fue una **copia vieja de la app cacheada por el player**
> en las sesiones de navegador de prueba (ver B-15 en §9.6 y la sección 10). Las conclusiones por bug de esta corrida
> sobre B-01, B-03..B-09, B-11..B-13 y B-18 **no valen como estado de la app publicada**: describen una versión
> congelada, y la sección 10 las reemplaza.

Lo que la corrida 2 observó realmente (sobre esa copia cacheada de las sesiones):

- El Solicitante veía "Cobrado hoy…" en lugar de "Tus solicitudes Especiales".
- Los combos mostraban 15 opciones vacías.
- Aparecía el banner "expected a table".
- Abrir Agentes generaba 2 PATCH y no existía `btnDesvincularAge`.
- La búsqueda de Maestros disparaba 13 consultas.
- La app escalaba en lugar de pasar al diseño móvil.

La inferencia posterior ("el `.msapr` commiteado tampoco tiene los fixes, porque solo cambió `Properties.json`") es un
dato cierto del archivo pero **no explica** el comportamiento: el player no sirve ese `.msapr`, sirve la versión
publicada (o, en estas sesiones, su copia cacheada).

**Lo que sí era del lado servidor:**

- **B-02**: el rol BackOffice ya tiene `Append` sobre interacción y User1 transcribió solo con el rol Backoffice.
- **B-14**: User1 y User2 tienen licencia.
- **B-10 a medias**: el flow está **activado**, pero su lógica es un **no-op** (ver B-10 y B-16).

**Delegación: sigue LIMPIA** (§9.5).

### 9.3 Resultados por escenario

Leyenda: ✅ PASÓ · ❌ FALLÓ · ⛔ BLOQUEADO. Evidencia relativa a `resultados/corrida-2/` (✱ = tomado del re-chequeo
`corrida-2-recheck-BCFG/` porque el test se corrigió o completó, ver nota).

| ID | Descripción | Usuario (rol) | Estado | Evidencia / detalle |
|---|---|---|:---:|---|
| R-05 | Admin ve solo su nav (6) + sección Admin | User4 (Admin) | ✅ | `R-05-admin-inicio.png` |
| E-01a | Abrir sin banners ni 403 (Admin) | User4 | ❌ | "Incorrect argument type, expected a table." (B-06) |
| D-02a | Inicio Admin: KPIs en la PRIMERA carga = API | User4 | ❌ | Frío `""`/`""` vs API 0/16; **0 consultas** del OnVisible (B-01) |
| D-02b | Inicio Admin (tibio) = API | User4 | ✅ | 0 = 0 · 16 = 16 · "sin carga" · C$ 250.00 = Sum API |
| D-02c | Monitoreo: cuentas, clientes, carga de hoy = API | User4 | ✅ | 1,006 · 959 · 0 (sin carga hoy B-07 no se manifiesta) |
| F-05 | Reconciliación sin carga de hoy: % y freno | User4 | ✅ | `F-05-reconciliacion.png` |
| D-02d | Cierres: contadores = API | User4 | ✅ | `D-02d-cierres.png` |
| F-04c | Agentes: 0 escrituras al abrir + botón "Desvincular" aparte | User4 | ❌ | **2 PATCH** (Álvaro Zeledón, Carlos Mendoza) y **0 botones "Desvincular"** (B-08). Valores idénticos antes/después; alerta 16 = API ✅ |
| D-03 | Maestros: búsqueda delegada + B-13 | User4 | ❌ | `startswith` en `$filter` ✅ y centinela (701/958) encontrado ✅; **13 consultas** para 14 caracteres (B-13) |
| F-04b | Alta de cliente QA + anti-duplicado | User4 | ✅ | `F-04b-*.png` |
| R-03 | **User2** (Solicitante real) abre y ve solo su nav | User2 | ✅ | B-14 resuelto · `R-03-solicitante-inicio.png` |
| E-01b | Abrir sin banners ni 403 (Solicitante) | User2 | ❌ | Banner B-06 + **403 Carga diarias** (B-05) |
| D-02e | Inicio Solicitante: frío = API + subtítulo propio | User2 | ❌ | Subtítulo "Cobrado hoy por los localizadores: C$ 0.00" (B-05); frío vacío (B-01); tibio = API ✅ |
| D-04 | Combo de cuentas: búsqueda con `startswith` | User2 | ✅ | El servidor devuelve las 4 cuentas QA |
| F-02a | El combo muestra número + cliente y deja elegir | User2 | ❌ | 15 opciones **vacías**; "sin resultados" tras buscar (B-04) |
| F-02 | Crear Especial por la UI | User2 | ❌ | Imposible por B-04 → creada por API para no bloquear C, F y G |
| F-02b | Anti-duplicado | User2 | ⛔ | B-04 |
| X-02 | **Falla controlada (B-03)**: Especial cerrada invisible para el Solicitante → alt key → error mostrado, nada dado por hecho | User2 | ⛔ | B-04 (no se puede elegir cuenta) |
| F-02d | Reactivar Especial cerrada PROPIA (B-11) | User2 | ⛔ | B-04 |
| D-05 | Mínimo privilegio: sin consultas a Pagos/Carga ni 403 | User2 | ❌ | Consultas a `cargadiarias` (`nfUltimaCarga`) y a `pagos` (Sum de Inicio): **403** (B-05) |
| R-04 | Supervisor ve solo su nav | User3 | ✅ | |
| E-01c | Abrir sin banners ni 403 (Supervisor) | User3 | ❌ | B-06 + 403 Carga diarias (B-05) |
| D-02f | POR APROBAR frío = API | User3 | ❌ | Frío vacío (B-01); tibio 5 = API ✅ |
| F-03 | Aprobar con el combo de localizador | User3 | ❌ | 15 opciones vacías (debería ofrecer exactamente 2: Carlos Mendoza, Álvaro Zeledón); "sin resultados" (B-04) · `F-03-combo-agente.png` |
| F-03c ✱ | Comp-14: owner → usuario del agente (≤ 3 min) | API | ❌ | Flow **Activado**; aprobación hecha por API (misma escritura que la app); a los **187 s** el owner sigue siendo User2 (esperado User1). Flowrun 18:12:39Z **Succeeded sin actualizar** → B-10 / B-16 |
| F-03b | Rechazar con motivo | User3 | ✅ | Guards ✅ · Rechazada (4) + motivo + resolutor User3 · sale de la cola |
| R-02 | **User1** (Backoffice real) abre y ve solo su nav | User1 | ✅ | B-14 resuelto |
| E-01d | Abrir sin banners ni 403 (Backoffice) | User1 | ❌ | B-06 (sin 403) |
| D-02g | Inicio BO: frío = API | User1 | ❌ | Frío vacío y "Cobrado hoy C$ 0.00" vs API C$ 250.00 (B-01); tibio = API ✅ |
| D-02h | Estación: chip "Todos" y encabezado = API | User1 | ✅ | |
| F-01 | **Transcripción con el rol Backoffice SOLO** | User1 | ✅ | **B-02 RESUELTO**: IN-1023/IN-1024 Grabado, `QAE2E1`/`QAE2E2`, grabado por User1, fecha de hoy; FIFO, avance, input limpio, ✏️ antes→nuevo, chip NUEVO, "¡cola vacía!" |
| F-01b | Transcritas hoy = API | User1 | ✅ | |
| R-06a | Multi-rol User1 (BO + Admin y Carga) | User1 | ❌ | Nav combinada (8), ambas secciones, pie "BackOffice · Admin", abre Transcripción y Maestros ✅; **falla solo por el banner B-06** |
| R-06b | Quitado de Admin y Carga → solo Backoffice | User1 | ✅ | `R-06b-solo-backoffice.png` |
| F-04 | Admin cierra la Especial QA (UI) | User4 | ✅ | 1 PATCH, Estado=false, motivo "Gestión completada — QA-E2E cierre F-04", fecha de hoy |
| D-07 | Carga de HOY (1 fila QA): FILAS DE CARGA HOY = API | User4 | ❌ | App **0** vs API **1**; la request sigue viajando `ge 2026-10-03T06:00:00Z` (B-07) |
| F-05b | Reconciliación con carga de hoy + **B-12** | User4 | ❌ | % = API (801 de 801) ✅, freno ✅ (botón y casilla deshabilitados, **no se pulsó nada**); **"Mostrando las primeras 500"**, botón "Cerrar **500**", 1 página de 500 + `nextLink` no seguido (B-12) |
| F-02e ✱ | Tras aprobar y cerrar: el Solicitante sigue viendo SUS Especiales | User2 | ✅ | KPIs (tibio) = API y Mis solicitudes la lista. **Pasa solo porque el flow no movió el owner** (ver B-17) |
| F-02c ✱ | Re-solicitar por UI tras el ciclo real (B-11) | User2 | ⛔ | B-04 |
| X-01 | Responsive a 800×900 | User4 | ❌ | Sin barra móvil ni hamburguesa; nav **141 px** = la app escala (B-09) |
| D-01 | Delegación + errores de lectura en toda la red | todos | ❌ | **Delegación OK** (0 alertas); falla solo por los **403** de lectura de B-05 |

**Totales:** 41 escenarios → ✅ **17** · ❌ **20** · ⛔ **4**.

Nota ✱: en la corrida principal había 3 problemas del test que se corrigieron en el re-chequeo:

- **F-03c** pasaba solo con "flow activado", sin observar el owner. Ahora aprueba por API cuando la UI no puede.
- **F-02e** leía los KPIs en frío y mezclaba B-01. Ahora los lee en tibio.
- **F-02c** cortaba con una excepción por B-04. Ahora queda ⛔.

El resto del re-chequeo repitió el mismo resultado que la corrida principal.

### 9.4 Estado de los bugs de la corrida 1

| # | Estado | Evidencia (corrida 2) |
|---|:---:|---|
| B-01 KPIs vacíos en la 1.ª carga | **PERSISTE** | D-02a/e/f/g: frío `""` en los 4 roles, 0 consultas del OnVisible; tibio = API |
| B-02 BackOffice sin Append | **RESUELTO** | F-01 con User1 solo-Backoffice graba (Dataverse: Grabado, grabado por User1). Rol `SR \| CM \| BackOffice`: `Appendinteraccion: Global` |
| B-03 Escritura fallida dada por hecha | **SIN VERIFICAR** | La corrida 2 midió la copia cacheada (ver §10). X-02, la falla controlada, quedó ⛔ por B-04 (sigue ⛔ en la corrida 3) |
| B-04 Combos con opciones vacías | **PERSISTE** | F-02a, F-03: 15 opciones vacías y "sin resultados" (el servidor devolvió 4 cuentas) |
| B-05 403 por falta de privilegios | **PERSISTE** | 403 en `cargadiarias` (Solicitante y Supervisor) y en `pagos` (Solicitante); subtítulo "Cobrado hoy" para el Solicitante |
| B-06 Banner "expected a table" | **PERSISTE** | E-01a..d y R-06a |
| B-07 Carga de hoy = 0 | **PERSISTE** | D-07: 0 vs 1; request `ge …T06:00:00Z` |
| B-08 Abrir Agentes escribe | **PERSISTE** | F-04c: 2 PATCH y sin `btnDesvincularAge` (valores intactos) |
| B-09 Responsive / ScaleToFit | **PERSISTE** | X-01: nav 141 px, sin barra móvil |
| B-10 Flow de owner | **PERSISTE (causa nueva)** | Activado (statecode 1) pero no-op. La condición `Verificar_que_el_agente_tenga_asignado_un_usuario` es `equals("", "")`, siempre verdadero, y su rama verdadera está vacía. `Actualizar_owner_de_la_asignacion` está en el `else` y nunca corre (la condición interna también es `equals(owner, "")`). F-03c: owner sin cambio tras 187 s; flowrun Succeeded |
| B-11 412 al re-pedir sobre cerrada | **SIN VERIFICAR** | F-02c/F-02d ⛔ por B-04 (también en la corrida 3). Ver además **B-17** (riesgo del fix tal como está escrito) |
| B-12 Cota de 500 filas | **PERSISTE** | F-05b: "Mostrando las primeras 500", botón "Cerrar 500" con 801 desaparecidas; 1 página + `nextLink` |
| B-13 Consulta por tecla | **PERSISTE** | D-03: 13 consultas para un CIF de 14 caracteres |
| B-14 Licencias User1/User2 | **RESUELTO** | R-02 y R-03 con los dueños reales |

### 9.5 Delegación (corrida 2)

**Sigue limpia.** Hay **cero GET sin filtro a tablas grandes**, **cero N+1** y **cero descargas completas**, en 42 combinaciones de pantalla y rol. Los conteos y las sumas viajan como `$apply=…/aggregate(...)` y las listas con `$filter` y `$expand`. Todos los números comparados contra la Web API coincidieron cuando la app los pidió.

Requests por pantalla (GET / escrituras, deduplicadas por pantalla y momento):

| Pantalla (rol) | GET | Escr. | Observación |
|---|:---:|:---:|---|
| scrInicio frío (Admin / Sol. / Sup. / BO) | 4 c/u | 0 | Solo identidad + `nfUltimaCarga`; **sin KPIs** (B-01). Sol./Sup.: **403** `cargadiarias` |
| scrInicio tibio (Admin / Sol. / Sup. / BO) | 3–5 | 0 | KPIs delegados (`aggregate`). Sol.: **403** `pagos` (Sum de Cobrado hoy) |
| scrMonitoreo (Admin) | 3–6 | 0 | `cargadiarias ge …T06:00Z` (B-07), conteos de cuentas y clientes con `aggregate` |
| scrReconciliacion (Admin) | 3 | 0 | Lista con `$filter` + `$expand`; con carga de hoy, **500 filas + nextLink** (B-12) |
| scrCierres (Admin) | 4 | 1 al cerrar | `$filter` tipo 2/3 + estado; cierre = 1 PATCH |
| scrAgentes (Admin) | 14 | **2** | `systemusers` sin filtro (172, catálogo chico) y agentes (18); **2 PATCH al abrir** (B-08) |
| scrMaestros (Admin) | 2 + 13 + 9 | 2 al crear | Búsqueda `startswith` en servidor; 13 y 9 consultas por tecla (B-13) |
| scrMisSolicitudes (Sol.) | 3 | 0 | `$filter` tipo 3 + solicitado por; **403** `cargadiarias` |
| scrNuevaSolicitud (Sol.) | 1–2 | 0 | Combo vacío: **1.ª página de 500 cuentas sin `$filter`** (paginado del servidor, no descarga completa); con texto, `startswith` |
| scrAprobaciones (Sup.) | 3 | 1 al rechazar | **403** `cargadiarias` |
| scrTranscripcion (BO) | 5–12 | 2 al grabar | Cola `$filter` estado; detalle por clave; PATCH 204 (sin 403: B-02 resuelto) |
| scrTranscritas (BO) | 3 | 0 | |

**Único GET a tabla grande sin `$filter`**: `sanic_cm_tbl_cuentas` al abrir el combo de Nueva solicitud (`StartsWith(x, "")` se optimiza a sin filtro). Trae 1 página de 500 con `nextLink`. Es paginado de servidor, igual que en la corrida 1; aceptable, pero es payload innecesario. Los otros GET sin filtro son catálogos chicos (`systemusers` 172, `sanic_cm_tbl_agentes` 18) y metadatos de choice (`EntityDefinitions`).

### 9.6 Bugs nuevos

| # | Sev. | Dónde | Qué pasa | Evidencia |
|---|:---:|---|---|---|
| B-15 | ⚪ RETRACTADO (era 🔴) | Entorno de prueba (caché del player), **no** despliegue | **Hallazgo original (falso):** "la versión publicada no contiene los fixes de la ronda 6". **Causa real (verificada en la corrida 3):** el player de Power Apps guarda la app en `localStorage` (`PAAppLoadCache-*`, `PAPreloadKeyV2-*`) y arranca con esa copia mientras revalida en segundo plano (*stale-while-revalidate*). Las sesiones `storageState` de `local/` estaban congeladas con versiones viejas (User4 05:46Z, User3 12:46Z), así que cada corrida probó **la versión cacheada el día que se capturó la sesión**, no la publicada. El import con `--publish-changes` **sí publica**: no hacía falta "pegar los `.pa.yaml` en Studio" | Corrida 3: con `sesionSinCacheDeApp` (descarta esas dos claves) las 10 aperturas del player cargaron la versión `2026-10-03T18:42:50Z` = `lastpublishtime`, y B-05, B-07, B-08, B-09, B-12, B-13 y B-18 aparecen resueltos. **Acción**: ninguna sobre el despliegue. Mantener `sesionSinCacheDeApp` y la traza `app.version` por apertura (resultados.json / resumen.md) para que toda corrida diga contra qué versión se probó |
| B-16 | 🟡 MEDIA | Flow Comp-14 · disparador | El disparador (create+update, scope Organization, filtro `sanic_cm_as_agenteid`) **corre en cada alta de asignación** y **falla** cuando no hay agente (`GetItem` con id nulo). Eso pasa en cada Especial nueva y potencialmente en cada fila del import diario | `flowruns`: 7 runs **Failed** "ActionFailed" en los instantes de alta de las asignaciones QA (18:02–18:11Z). Fix: condición de disparo `@not(equals(triggerOutputs()?['body/_sanic_cm_as_agenteid_value'], null))` |
| B-17 | 🟠 ALTA (latente) | Modelo de seguridad + fix de B-11 | El rol **Solicitante lee y escribe asignaciones a nivel USUARIO** (Basic). Cuando Comp-14 funcione y pase el owner al usuario del localizador: (a) el Solicitante **deja de ver** sus Especiales aprobadas (Mis solicitudes, KPI APROBADAS); (b) el `LookUp` de la reactivación (fix de B-11) **no ve** la Especial cerrada → intenta CREAR → **412 por la alt key** (B-11 vuelve). Hoy no se manifiesta porque el flow es no-op (B-10) | `RetrieveRolePrivilegesRole`: `Readasignacion: Basic`, `Writeasignacion: Basic`. X-02 estaba diseñado para provocar exactamente esto (⛔ por B-04). Fix: Read a nivel BU/Org para el Solicitante (filtrando por "Solicitado por" en la app), o compartir la Especial con el solicitante en el flow, o reactivar del lado servidor |
| B-18 | 🟡 MEDIA | scrAprobaciones + KPI "POR APROBAR" (código viejo **y** nuevo) | La cola y el conteo filtran tipo + "Pendiente aprobación" pero **no** `'[AS] Estado' = true`. Una Especial que el Admin cierra estando pendiente (Cierres la deja cerrar) **sigue en la cola del Supervisor**, que puede "aprobar" una asignación cerrada | F-03b: la cola mostraba las Especiales QA cerradas (`X-02 cerrada ajena`, `F-02d cerrada propia`). En la corrida principal, F-04 cerró por UI AS-2014 todavía pendiente. Fix: agregar `&& '[AS] Estado' = true` (delegable) en `colAprobAprb` y en `varNSaprIni`/POR APROBAR |

### 9.7 Limpieza (verificada)

| Verificación | Resultado |
|---|---|
| Restos QA-E2E en las 9 tablas (borrado por la suite + verificación independiente posterior por prefijo y por registro, con existencia real) | **0** en las 9 tablas, en las dos corridas |
| Membresías de teams | Backoffice = [User1] · Solicitantes = [User2] · Supervisores = [User3] · Admin y Carga = [User10, User4]: **exactamente el mapeo** (`mapeoFinal.comoMapeo = true`; `restaurarMembresias` sin cambios pendientes). User1 se agregó y se quitó de "Admin y Carga" dentro de la fase E |
| Agentes reales | Carlos Mendoza → user1@… · Álvaro Zeledón → admin@… — usuario, email y activo **sin cambios**. B-08 los re-escribió con los mismos valores (`modifiedon` cambió). Nunca se pulsó Vincular ni Desvincular |
| Cierre en lote | **Nunca se pulsó**: solo se leyó su texto y su estado (deshabilitado) |
| Fila QA de carga de hoy | Creada solo en la fase F y borrada al terminarla (HTTP 204) |
| Sesión admin / app / guías 00–13 | No se usaron ni se modificaron. Sin commit ni push |

### 9.8 Próximo paso

1. ~~Publicar de verdad los fixes (B-15)~~ — retractado: no era un problema de publicación (ver §10). Re-correr con sesiones sin caché.
2. Corregir el flow (B-10 / B-16) y decidir el modelo de lectura del Solicitante (B-17) **antes** de dar por cerrado B-11.
3. Agregar el filtro de estado en Aprobaciones (B-18).
4. Re-correr: `BO_RESULTADOS_DIR=$PWD/resultados/corrida-3 node run.js`. X-02, F-02b, F-02c y F-02d ya tienen la ruta por UI escrita y se desbloquean solos con B-04.

---

## 10. Corrida 3 (sesiones sin caché de app)

### 10.1 Datos de la corrida

| Ítem | Valor |
|---|---|
| Fecha | 2026-10-03 — corrida completa 12:50–13:04 hora Managua (UTC−6; 18:50–19:04Z) + re-chequeo de las fases A, B y C 13:07–13:13 (19:07–19:13Z) |
| App publicada | `sanic_appcmbackofficecanvas_d0162` · **appversion `2026-10-03T18:42:50Z`** (= `lastpublishtime`), re-importada y publicada con el fix B-18 |
| **Versión que cargó el player** | **`2026-10-03T18:42:50Z` en las 10 aperturas** (A, B, C, D, E, E2, F, F2, G, X) y en las 3 del re-chequeo. Una sola versión, y es la última *Published*. Queda registrada por apertura en `resultados.json` (`aperturas`) y en la sección "Versión de la app probada" de `resumen.md` |
| Cambio de la suite | `lib/player.js`: `sesionSinCacheDeApp` descarta `PAAppLoadCache-*` y `PAPreloadKeyV2-*` de la sesión antes de abrir; `app.version` guarda la versión que cargó el player (sufijo de la clave `PAPreloadKeyV2-`). `lib/informe.js` + `escenarios/comun.js`: cada apertura registra su `app.version`. `lib/verdad.js`: el oráculo de POR APROBAR cuenta solo asignaciones **activas** (B-18) |
| Usuarios | mapeo real y fijo, igual que la corrida 2. Único cambio temporal: R-06 (User1 + "Admin y Carga" en la fase E, quitado en la misma fase) |
| Resultados crudos | `tests/backoffice-canvas/resultados/corrida-3/` y `…/corrida-3-retry-ABC/` (gitignoreadas) |
| Reintentos | 3 escenarios se reintentaron una vez (fases A, B y C, corriendo todo el bloque): **R-05** (lectura de la nav antes de que renderizara: `[]`), **D-02a** (KPIs de Admin vacíos en frío) y **D-02f** (el oráculo del test no contaba el filtro de B-18, ver §10.4). El resultado de cada caso en la tabla usa el reintento donde aplica y lo marca con ↻ |

### 10.2 Veredicto

**MEJORA GRANDE, TODAVÍA NO ACEPTABLE.** Con la app publicada de verdad (sin la copia cacheada), **28 de 41**
escenarios pasan (en la corrida 2 eran 17). Se resolvieron **B-05, B-07, B-08, B-09, B-12, B-13 y B-18**, y el
banner de 403 desapareció en los cuatro roles.

Lo que sigue roto, y por qué:

- **B-04 (combos de Cuenta y de Localizador con opciones vacías)** sigue vivo y es lo que más pesa: bloquea
  F-02a, F-02, F-03 y deja en ⛔ a F-02b, X-02, F-02d y F-02c. El Solicitante **no puede crear una Especial por la UI**
  y el Supervisor **no puede elegir localizador**.
- **B-06** (banner "Incorrect argument type, expected a table.") sigue al abrir, en los cuatro roles (E-01a..d).
- **B-10 / B-16 (flow Comp-14)** sigue sin hacer nada: **pendiente B-10 / B-17, no es una regresión** (§10.5).
- **B-19 (nuevo)**: la pantalla Nueva solicitud descarga la tabla de cuentas completa. Es lo único que falla en D-01.

**Delegación: limpia salvo B-19.** 43 combinaciones pantalla/rol, 0 errores de lectura, 0 N+1 y un solo GET sin filtro a una tabla grande (§10.6).

### 10.3 Resultados por escenario

Leyenda: ✅ PASÓ · ❌ FALLÓ · ⛔ BLOQUEADO · ↻ resultado del reintento · "C2" = estado en la corrida 2.
Evidencia relativa a `resultados/corrida-3/` (↻ = `corrida-3-retry-ABC/`).

| ID | Descripción | Usuario (rol) | C2 | C3 | Detalle |
|---|---|---|:---:|:---:|---|
| R-05 | Admin ve solo su nav (6) + sección Admin | User4 (Admin) | ✅ | ✅ ↻ | 1.er intento: nav `[]` (la lectura corrió antes de renderizar, la captura posterior sí la muestra completa). Reintento: nav exacta |
| E-01a | Abrir sin banners ni 403 (Admin) | User4 | ❌ | ❌ | **Sin 403** ✅. Banner "expected a table" (B-06) · `E-01a-al-abrir.png` |
| D-02a | Inicio Admin: KPIs en la PRIMERA carga = API | User4 | ❌ | ✅ ↻ | 1.er intento: frío `""`/`""` con solo 2 consultas del OnVisible. Reintento: 0 y 16 = API, 4 consultas. **B-01 intermitente en Admin** (§10.4) |
| D-02b | Inicio Admin (tibio) = API | User4 | ✅ | ✅ | 0 · 16 · "sin carga" · C$ 250.00 |
| D-02c | Monitoreo: cuentas, clientes, carga de hoy = API | User4 | ✅ | ✅ | 1,006 · 959 · 0 |
| F-05 | Reconciliación sin carga de hoy: % y freno | User4 | ✅ | ✅ | 0 de 801 (0 %), card ⛔ visible |
| D-02d | Cierres: contadores = API | User4 | ✅ | ✅ | Incobrables (151) · Especiales (47) |
| F-04c | Agentes: 0 escrituras al abrir + botón "Desvincular" aparte | User4 | ❌ | ✅ | **B-08 RESUELTO**: 0 escrituras, valores idénticos, 2 botones "Desvincular" (no se pulsaron), alerta 16 = API |
| D-03 | Maestros: búsqueda delegada + B-13 | User4 | ❌ | ✅ | **B-13 RESUELTO**: 14 caracteres = **1** consulta (antes 13); `startswith` en `$filter`; centinela (puesto 701 de 958) encontrado |
| F-04b | Alta de cliente QA + anti-duplicado | User4 | ✅ | ✅ | |
| R-03 | User2 (Solicitante real) abre y ve solo su nav | User2 | ✅ | ✅ | |
| E-01b | Abrir sin banners ni 403 (Solicitante) | User2 | ❌ | ❌ | **Sin 403** ✅ (B-05 resuelto). Solo falla el banner B-06 |
| D-02e | Inicio Solicitante: frío = API + subtítulo propio | User2 | ❌ | ✅ | **B-05 y B-01 OK**: subtítulo "Tus solicitudes Especiales"; PENDIENTES/APROBADAS/RECHAZADAS en frío y tibio = API |
| D-04 | Combo de cuentas: búsqueda con `startswith` | User2 | ✅ | ✅ | El servidor devuelve las 4 cuentas QA |
| F-02a | El combo muestra número + cliente y deja elegir | User2 | ❌ | ❌ | 15 opciones **vacías** al abrir; "sin resultados" tras buscar aunque el servidor devolvió 4 filas (B-04) |
| F-02 | Crear Especial por la UI | User2 | ❌ | ❌ | Imposible por B-04 → AS-2022 creada por API para no bloquear C, F y G |
| F-02b | Anti-duplicado | User2 | ⛔ | ⛔ | B-04 |
| X-02 | Falla controlada de escritura (B-03) | User2 | ⛔ | ⛔ | B-04 |
| F-02d | Reactivar Especial cerrada PROPIA (B-11) | User2 | ⛔ | ⛔ | B-04 |
| D-05 | Mínimo privilegio: sin consultas a Pagos/Carga ni 403 | User2 | ❌ | ✅ | **B-05 RESUELTO**: cero consultas a `pagos` y `cargadiarias`, cero 403 en toda la sesión |
| R-04 | Supervisor ve solo su nav | User3 | ✅ | ✅ | |
| E-01c | Abrir sin banners ni 403 (Supervisor) | User3 | ❌ | ❌ | Sin 403 ✅. Solo banner B-06 |
| D-02f | POR APROBAR frío y tibio = API | User3 | ❌ | ✅ ↻ | **3 = 3** con el oráculo corregido (solo activas, B-18). En el 1.er intento el test esperaba 5 (contaba 2 Especiales QA cerradas estando pendientes) |
| F-03 | Aprobar con el combo de localizador | User3 | ❌ | ❌ | 15 opciones vacías (debería ofrecer exactamente 2); "sin resultados" al buscar "Carlos" (B-04) · `F-03-combo-agente.png` |
| F-03c | Comp-14: owner → usuario del agente (≤ 3 min) | API | ❌ | ❌ | **Pendiente B-10 / B-16 / B-17.** Flow **Activado** (modificado 18:46:43Z); aprobación por API (misma escritura que la app); a los ~185 s el owner **sigue siendo User2** (esperado User1). Ver §10.5 |
| F-03b | Rechazar con motivo | User3 | ✅ | ✅ | Guards ✅ · Rechazada (4) + motivo + resolutor User3 · sale de la cola. La cola muestra solo asignaciones activas (3 filas) |
| R-02 | User1 (Backoffice real) abre y ve solo su nav | User1 | ✅ | ✅ | |
| E-01d | Abrir sin banners ni 403 (Backoffice) | User1 | ❌ | ❌ | Solo banner B-06 |
| D-02g | Inicio BO: frío = API | User1 | ❌ | ✅ | **B-01 OK en BO**: EN COLA 6 · CON PAGO 1 · TRANSCRITAS HOY 0 en frío y tibio; "Cobrado hoy C$ 250.00" |
| D-02h | Estación: chip "Todos" y encabezado = API | User1 | ✅ | ✅ | |
| F-01 | Transcripción con el rol Backoffice SOLO | User1 | ✅ | ✅ | IN-1027/IN-1028: FIFO, Enter y botón, `QAE2E1`, grabado por User1, fecha de hoy, ✏️ antes→nuevo |
| F-01b | Transcritas hoy = API | User1 | ✅ | ✅ | |
| R-06a | Multi-rol User1 (BO + Admin y Carga) | User1 | ❌ | ✅ | Nav combinada, ambas secciones, pie "BackOffice · Admin", abre Transcripción y Maestros. **Sin banner** en esta apertura (ver nota de B-06) |
| R-06b | Quitado de Admin y Carga → solo Backoffice | User1 | ✅ | ✅ | |
| F-04 | Admin cierra la Especial QA (UI) | User4 | ✅ | ✅ | 1 PATCH, Estado=false, motivo, fecha de hoy |
| D-07 | Carga de HOY (1 fila QA): FILAS DE CARGA HOY = API | User4 | ❌ | ✅ | **B-07 RESUELTO**: la request viaja `ge 2026-10-03T00:00:00Z` (antes `06:00Z`); app 1 = API 1 |
| F-05b | Reconciliación con carga de hoy + B-12 | User4 | ❌ | ✅ | **B-12 RESUELTO**: lista de 801 en **1 página, sin `nextLink`**, sin aviso "primeras 500", botón "Cerrar 801" (texto leído; **nunca se pulsó**), casilla y botón deshabilitados por umbral rojo |
| F-02e | Tras aprobar y cerrar: el Solicitante sigue viendo SUS Especiales | User2 | ✅ | ✅ | KPIs = API y Mis solicitudes lista las 3. **Pasa solo porque el flow no movió el owner** (B-17) |
| F-02c | Re-solicitar por UI tras el ciclo real (B-11) | User2 | ⛔ | ⛔ | B-04 |
| X-01 | Responsive a 800×900 | User4 | ❌ | ✅ | **B-09 RESUELTO**: pantalla 799 px, nav 240 px (no escala), barra móvil + hamburguesa, drawer se cierra al navegar |
| D-01 | Delegación + errores de lectura en toda la red | todos | ❌ | ❌ | **Delegación OK salvo B-19** (GET sin filtro a `cuentas` al abrir Nueva solicitud). **Cero errores de lectura** (los 403 de la corrida 2 desaparecieron) |

**Totales:** 41 escenarios → ✅ **28** · ❌ **9** · ⛔ **4** (corrida 2: ✅ 17 · ❌ 20 · ⛔ 4).

Corrida completa sin reintentos: ✅ 25 · ❌ 12 · ⛔ 4. Los 3 casos del reintento (R-05, D-02a, D-02f) pasan de ❌ a ✅ y dan 28.

**Los 9 ❌ restantes:** E-01a, E-01b, E-01c, E-01d (todos B-06), F-02a, F-02, F-03 (B-04), F-03c (B-10/B-16/B-17 pendiente) y D-01 (B-19).
**Los 4 ⛔:** F-02b, X-02, F-02d y F-02c, todos por B-04.

**Comparación con la corrida 2:** 11 casos pasaron de ❌ a ✅ (D-02a, F-04c, D-03, D-02e, D-05, D-02f, D-02g, R-06a, D-07, F-05b, X-01). Ninguno pasó de ✅ a ❌. Los 4 ⛔ siguen igual. Los 9 ❌ de ahora ya estaban en ❌.

### 10.4 Estado de los bugs

| # | Estado | Evidencia (corrida 3) |
|---|:---:|---|
| B-01 KPIs vacíos en la 1.ª carga | **MEJORADO — intermitente en Admin** | D-02e/f/g (Solicitante, Supervisor y BO): frío = API en las dos corridas. Admin: D-02a falló en la corrida principal (frío `""`, solo 2 consultas del OnVisible) y pasó en el reintento (4 consultas). Probable carrera entre el `OnVisible` de Inicio y la evaluación de las fórmulas con nombre (hipótesis), no un 0-consultas determinista como antes |
| B-02 BackOffice sin Append | **RESUELTO** | F-01 con User1 solo-Backoffice graba |
| B-03 Escritura fallida dada por hecha | **SIN VERIFICAR** | X-02 sigue ⛔ por B-04. La falla controlada no se pudo provocar por la UI |
| B-04 Combos con opciones vacías | **PERSISTE** | F-02a, F-03: 15 opciones vacías y "sin resultados". Los datos SÍ llegan: la request trae `$select=…numerodecuenta,…nombrecliente…` y 1,006 filas (o 4 al buscar). Falla el render del texto. El código ya está en la variante "Filter directo sin AddColumns" (`Src/scrNuevaSolicitud.pa.yaml` L76–85, `Src/scrAprobaciones.pa.yaml` L575–585), así que el fix de la ronda 6 **estaba desplegado y no alcanzó en el player**, aunque el comentario del código dice "verificado en Studio". Hipótesis: `DisplayFields`/`SearchFields` con nombres lógicos (`sanic_cm_ct_numerodecuenta`, `sanic_cm_ag_nombre`) que el runtime no resuelve en un combo cuyas `Items` vienen de una tabla Dataverse con prefijo de publisher; los combos que funcionan usan `["Value"]` (opciones de Choice) o columnas nativas (`fullname`). Verificar en Studio → Monitor y en modo Play, no solo en el editor |
| B-05 403 por falta de privilegios | **RESUELTO** | D-05 ✅; cero 403 en E-01b/c; subtítulo "Tus solicitudes Especiales" |
| B-06 Banner "expected a table" | **PERSISTE** | E-01a..d en las dos corridas. Aparece en **Inicio de los cuatro roles**, así que el origen está en lo que se evalúa al abrir (fórmulas de `App.pa.yaml`, controles de `scrInicio`, nav), no en `scrAgentes`/`scrAprobaciones` (pantallas que aún no se cargaron). Nota: en R-06a (multi-rol) no apareció, lo que apunta a una dependencia del orden de evaluación. Hipótesis sin confirmar: alguna expresión que devuelve Blank donde se espera una tabla (p. ej. las relaciones `Security Roles`/`Teams` de `gblUsuarioDV` mientras el `LookUp` aún no resuelve). Requiere el Monitor de Studio |
| B-07 Carga de hoy = 0 | **RESUELTO** | D-07: app 1 = API 1; filtro contra `00:00Z` |
| B-08 Abrir Agentes escribe | **RESUELTO** | F-04c: 0 escrituras al abrir, botón "Desvincular" aparte (no se pulsó) |
| B-09 Responsive / ScaleToFit | **RESUELTO** | X-01: nav 240 px a 800 px de ancho, barra móvil + hamburguesa |
| B-10 Flow de owner | **CORREGIDO (post-corrida, §10.10)** | Corrida 3: F-03c sin cambio de owner. Corregido después y verificado por API; falta re-correr F-03c |
| B-11 412 al re-pedir sobre cerrada | **SIN VERIFICAR** | F-02c/F-02d ⛔ por B-04 |
| B-12 Cota de 500 filas | **RESUELTO** | F-05b: 801 filas en 1 página, sin `nextLink`, sin aviso. El límite efectivo ahora es mayor (≥ 1,006: B-19 trajo 1,006 sin cortar). No se verificó el valor exacto del *Data row limit* |
| B-13 Consulta por tecla | **RESUELTO** | D-03: 1 consulta para 14 caracteres |
| B-14 Licencias User1/User2 | **RESUELTO** | R-02 y R-03 |
| B-15 "La versión publicada no tenía los fixes" | **RETRACTADO** | Causa real: caché de localStorage del player en las sesiones de prueba (§10.7) |
| B-16 Disparador del flow falla sin agente | **CORREGIDO (post-corrida, §10.10)** | Corrida 3, `flowruns` del 18:40Z en adelante: 5 runs, **4 Failed `ActionFailed`** (alta de asignaciones sin agente) y 1 Succeeded (la aprobación, sin cambiar el owner) |
| B-17 Rol Solicitante lee a nivel usuario | **CORREGIDO por compartido (§10.10)** | Decisión: el flow comparte la Especial con "Solicitado por" (mínimo privilegio, el rol sigue en Basic). Falta re-correr F-02e, X-02, F-02c y F-02d |
| B-18 Cola y KPI POR APROBAR con cerradas | **RESUELTO** | KPI POR APROBAR = 3 = API con `Estado = true`; la API sin ese filtro daba 5 (2 Especiales QA cerradas estando pendientes). La cola de Aprobaciones muestra 3 filas y ninguna cerrada · `D-02f`, `F-03-combo-agente.png` |

### 10.5 Lo que depende del flow "Cambio de Owner en Asignaciones" (pendiente B-10 / B-17)

Por decisión, el flow no se corrigió. Estos casos **no son regresiones**:

| Caso | Estado | Por qué |
|---|:---:|---|
| F-03c | ❌ pendiente B-10 / B-16 | Flow Activado, pero no actualiza el owner: tras ~185 s sigue siendo User2 (esperado: el usuario de Carlos Mendoza, User1). El run de la aprobación termina Succeeded sin escribir |
| F-02e | ✅ (condicionado) | Pasa porque el owner no se mueve. Cuando el flow funcione, este caso **debe volver a evaluarse**: ahí aparece B-17 (el Solicitante deja de ver sus Especiales aprobadas) |
| X-02 / F-02d / F-02c | ⛔ | Hoy bloqueados por B-04, pero cuando B-04 se resuelva su resultado dependerá de B-10 y B-17 (el `LookUp` de reactivación no vería la Especial cerrada si el owner cambió) |

### 10.6 Delegación (corrida 3)

**Limpia, con una excepción nueva (B-19).** De 43 combinaciones pantalla/rol:

- **0 errores de lectura** (en la corrida 2 había 403 de B-05).
- **0 N+1.**
- **1 alerta**, repetida en dos momentos (B y G): `sanic_cm_tbl_cuentas` sin `$filter` ni `$top`, **1,006 filas en una sola respuesta, sin `nextLink`**.
- Conteos y sumas siguen yendo como `$apply=…/aggregate(...)`, listas con `$filter` y `$expand`, y la búsqueda de Maestros con `startswith` (1 consulta).
- Escrituras observadas, todas sobre registros QA-E2E: alta de cliente y teléfono (2 POST), rechazo (1 PATCH), transcripción (2 PATCH), cierre (1 PATCH). Abrir Agentes ya no escribe.

Requests por pantalla relevantes (GET / escrituras): Inicio frío 6–11 / 0 (Admin 6, BO 8, multi-rol 11) · Monitoreo 3–8 / 0 · Reconciliación 3 / 0 · Cierres 4 / 1 al cerrar · Agentes 7 / 0 · Maestros 2 + 1 + 1 / 2 al crear · Mis solicitudes 2 / 0 · **Nueva solicitud 1 (+1 combo) / 0** · Aprobaciones 2 / 1 al rechazar · Transcripción 5–12 / 2 al grabar · Transcritas 3 / 0.

### 10.7 Bugs nuevos y corrección de B-15

| # | Sev. | Dónde | Qué pasa | Evidencia / causa probable |
|---|:---:|---|---|---|
| B-19 | 🟡 MEDIA (delegación) | `Src/scrNuevaSolicitud.pa.yaml` · `cmbCuentaNva.Items` (L76–85) | Al abrir **Nueva solicitud** la app hace **1 GET sin filtro** a `sanic_cm_tbl_cuentas` (`$select=…&$count=true`) y baja la tabla **entera**: **1,006 filas en una respuesta, sin `nextLink`**. En la corrida 2 eran 500 filas + `nextLink` (el *Data row limit* efectivo era 500); con el límite ya mayor, la página única es la tabla completa. En producción (~7,000 cuentas) bajarían hasta el tope del límite en cada apertura, con la lista truncada en silencio | D-01 `B:scrNuevaSolicitud` y `G:scrNuevaSolicitud(re-solicitud)`: veredicto "⚠ SIN FILTRO A TABLA GRANDE", 1,006 filas, sin `nextLink`. **No afecta la corrección de la búsqueda**: con texto, la request lleva `startswith(...)` en `$filter` y devuelve las 4 cuentas QA (D-04 ✅). **Causa probable**: con `SearchText` vacío, `StartsWith(x, "") \|\| StartsWith(y, "")` es verdadero para todo y el optimizador de delegación lo reduce a "sin `$filter`". **Fix candidato** (no aplicado): no consultar sin texto, o limitar con `FirstN` (que sí delega como `$top`), p. ej. `If(Len(cmbCuentaNva.SearchText) < 2, FirstN('CM \| TBL \| Cuentas', 20), Filter(…))` |

**Corrección de B-15 (causa real).** El player de Power Apps guarda la app en `localStorage`
(`PAAppLoadCache-<env>-<app>`, `PAPreloadKeyV2-/providers/Microsoft.PowerApps/apps/<app>-<versión>`) y arranca con esa copia mientras revalida en segundo plano (*stale-while-revalidate*). Como la sesión `storageState` es un JSON congelado que nunca se vuelve a guardar, cada corrida abría **la versión que estaba publicada el día que se capturó la sesión**: User4 la de 05:46Z y User3 la de 12:46Z. Por eso la corrida 2 reprodujo la corrida 1 punto por punto aunque ya hubiera código nuevo publicado. El import con `--publish-changes` **sí publica**.

Prueba de la corrección: con `sesionSinCacheDeApp` (descarta esas dos claves y deja la autenticación `msal.*`) las 13 aperturas cargaron la versión `2026-10-03T18:42:50Z`, igual a `lastpublishtime`, y 7 bugs que la corrida 2 daba por "no desplegados" aparecen resueltos. El sufijo de la clave `PAPreloadKeyV2-` es la versión publicada que el player cargó, y de ahí sale `app.version`.

Observaciones menores del test:

- **R-05** leyó la nav antes de que renderizara (`[]`). El reintento pasó. Conviene esperar a que `galNavCmp` tenga ítems antes de leerla; no es un bug de la app.
- El **oráculo de D-02f** quedó viejo cuando se arregló B-18; ya cuenta solo asignaciones activas.

### 10.8 Limpieza (verificada)

| Verificación | Resultado |
|---|---|
| Restos QA-E2E en las 9 tablas (limpieza previa y final de la suite) | **0** en las 9 tablas, en la corrida completa y en el re-chequeo; 0 errores de borrado |
| Membresías de teams | Backoffice = [User1] · Solicitantes = [User2] · Supervisores = [User3] · Admin y Carga = [User10, User4]: **exactamente el mapeo** (`mapeoFinal.comoMapeo = true`; `restaurarMembresias` sin cambios pendientes) en ambas corridas. User1 se agregó y se quitó de "Admin y Carga" dentro de la fase E |
| Agentes reales | usuario, email y activo **idénticos** a la foto inicial (`agentes.identicos = true`, 0 restaurados). Esta vez Agentes no los re-escribió (B-08 resuelto). Nunca se pulsó Vincular ni Desvincular |
| Cierre en lote | **Nunca se pulsó**: solo se leyó el texto del botón ("Cerrar 801 asignaciones en lote") y su estado deshabilitado |
| Fila QA de carga de hoy | Creada solo en la fase F y borrada al terminarla |
| Sesión admin / app / flow / roles / Smelter / guías 00–13 | No se usaron ni se modificaron. Sin despliegues. Sin commit ni push |

### 10.9 Próximo paso

1. **B-04** (combos): es lo que más bloquea (7 escenarios entre ❌ y ⛔). Reproducir en Studio en modo Play y con el Monitor; probar `DisplayFields` con otros nombres antes de cambiar la estructura de `Items`.
2. **B-06** (banner): Monitor de Studio al abrir Inicio con un rol.
3. ~~B-10 / B-16 / B-17~~: hecho (§10.10). Re-evaluar F-02e, F-03c, X-02, F-02d y F-02c en la próxima corrida.
4. **B-19**: no consultar la tabla de cuentas sin texto de búsqueda.
5. **B-01** en Admin: investigar la carrera en frío (intermitente).
6. Cualquier corrida futura debe usar `sesionSinCacheDeApp` (ya está en `lib/player.js`) y revisar la tabla "Versión de la app probada" de `resumen.md` contra el último *Published*.

### 10.10 Corrección del flow "Cambio de Owner en Asignaciones" (después de la corrida 3)

**Decisión de B-17:** compartir desde el flow. El rol Solicitante sigue con lectura **Basic**; el flow, al mover el owner al usuario del localizador, comparte esa Especial con su "Solicitado por". Alternativas descartadas: Read a nivel BU (el Solicitante podría leer todas las asignaciones de la BU por API) y que las Especiales no cambien de owner (invierte el modelo solo para un tipo).

Defectos corregidos en la definición (despliegue por Web API, `workflows(3b897aa2-…)`.`clientdata`):

| # | Antes | Después |
|---|---|---|
| B-16 | El disparador corría en cada alta y `GetItem` fallaba con id nulo | Condición de disparo `@not(equals(triggerOutputs()?['body/_sanic_cm_as_agenteid_value'], null))` |
| B-10 (1) | If externo `equals("", "")`: siempre verdadero, con la rama verdadera vacía → **nunca hacía nada** | `equals(<usuario del agente>, null)`: sin usuario no hace nada; con usuario sigue |
| B-10 (2) | If interno comparaba el owner contra `""` | Compara el owner contra el usuario del agente; si ya es el owner no reasigna (y ya no corta con `Terminate`) |
| B-10 (3) | `ownerid@odata.bind` con el GUID pelado | `systemusers(<guid>)` |
| B-17 | — | Acción nueva `GrantAccess_al_solicitante` (*Perform an unbound action* · `GrantAccess`) si `sanic_cm_as_tipo = 3` (Especial) y hay "Solicitado por": `ReadAccess,WriteAccess,AppendAccess` (Append porque la reactivación de B-11 setea lookups). Es idempotente |

Gotchas del conector encontrados al desplegar:

- Los parámetros se validan contra el esquema del conector al guardar (`WorkflowOperationParametersExtraParameter`). Para `GrantAccess` funciona `item/Target = "sanic_cm_tbl_asignacions(@{id})"` y `item/PrincipalAccess` como **objeto** (`Principal` + `AccessMask`). Las claves planas `item/Target/@odata.type` no existen.
- Dentro de un objeto literal, `@odata.type` se interpreta como expresión: hay que escribirlo `@@odata.type`.

**Verificación por API** (registros QA-E2E, borrados al terminar):

| Prueba | Resultado |
|---|---|
| Especial QA solicitada por User2, se le asigna el agente real Carlos Mendoza (→ User1; el agente no se modifica) | Owner = **User1 a los 16 s**; `RetrievePrincipalAccess` de User2 = `ReadAccess, WriteAccess, AppendAccess…`; run **Succeeded** |
| Especial QA **sin** agente | El flow **no dispara** (90 s sin runs nuevos) |

Pendiente: re-correr F-03c, F-02e, X-02, F-02c y F-02d (los tres últimos siguen bloqueados por B-04). El flow corregido queda en el repo (`solutions/unpacked`) con el próximo `solution-release` → `solution-sync`.

---

## 11. Corrida 4 (buscador + galería, banner B-06 y flow de owner corregidos)

### 11.1 Datos de la corrida

| Ítem | Valor |
|---|---|
| Fecha | 2026-10-03 — corrida completa terminada 16:45 hora Managua (UTC−6; 22:45Z) + re-chequeo de la fase B 16:50 (22:50Z) |
| App publicada | `sanic_appcmbackofficecanvas_d0162` · **appversion `2026-10-03T22:25:20Z`** (= `lastpublishtime`) |
| **Versión que cargó el player** | **`2026-10-03T22:25:20Z` en las 10 aperturas** (A, B, C, D, E, E2, F, F2, G, X) y en la apertura del re-chequeo de B. Una sola versión, la última *Published* (≥ 22:25:20Z). Queda en `resultados.json` (`aperturas`) y en "Versión de la app probada" de `resumen.md` |
| Flow "Cambio de Owner en Asignaciones" | Activado (`statecode 1`), modificado 22:28:11Z (la versión de §10.10) |
| Cambio de la suite | `escenarios/comun.js` · `elegirCombo`: no pulsa la opción si el combo ya la muestra elegida (ver 11.5). Es la única corrección de test |
| Usuarios | mapeo real y fijo. Único cambio temporal: R-06 (User1 + "Admin y Carga" en la fase E, quitado en la misma fase) |
| Resultados crudos | `tests/backoffice-canvas/resultados/corrida-4/` y `…/corrida-4-retry-B/` (gitignoreadas) |
| Reintentos | **Ninguno por infraestructura.** Solo se re-corrió la fase B completa (11 escenarios) tras corregir el test en 11.5; el resultado de F-02 y F-02d en la tabla usa ese re-chequeo y lo marca con ↻ |

### 11.2 Veredicto

**ACEPTABLE, CON UN DEFECTO NUEVO MENOR (B-20).** En la corrida completa pasaron **39 de 41** escenarios y, con el re-chequeo, **41 de 41**; los 2 ❌ (F-02 y F-02d) eran por B-20 y por el helper del test (11.5). Con el test corregido, la fase B completa se re-corrió en verde (11/11).

| | C2 | C3 | **C4 (corrida completa)** | **C4 (con ↻)** |
|---|:---:|:---:|:---:|:---:|
| ✅ PASÓ | 17 | 28 | **39** | **41** |
| ❌ FALLÓ | 20 | 9 | 2 | 0 |
| ⛔ BLOQUEADO | 4 | 4 | **0** | **0** |

Lo que cambió respecto de la corrida 3:

- **B-04 y B-19 resueltos**: buscador + galería elige cuenta, localizador y usuario; el Solicitante **crea** una Especial por la UI y el Supervisor **elige** localizador. Los 4 ⛔ (F-02b, X-02, F-02d, F-02c) corren y pasan.
- **B-06 resuelto**: los cuatro `E-01` pasan (cero banners, cero 403).
- **B-10, B-16 y B-17 resueltos**: el flow mueve el owner en ~6 s y el Solicitante sigue viendo sus Especiales (compartido).
- **Delegación limpia**: 49 combinaciones pantalla/rol, 0 errores de lectura, 0 N+1, **0 GET sin filtro a tablas grandes**.
- **Bug nuevo B-20** (BAJA): el combo "Tipo de solicitud" se puede dejar vacío y la nota queda `" — motivo"`.

### 11.3 Resultados por escenario

Leyenda: ✅ PASÓ · ❌ FALLÓ · ⛔ BLOQUEADO · ↻ resultado del re-chequeo de la fase B · "C3" = estado en la corrida 3.
Evidencia relativa a `resultados/corrida-4/` (↻ = `corrida-4-retry-B/`).

| ID | Descripción | Usuario (rol) | C3 | C4 | Detalle |
|---|---|---|:---:|:---:|---|
| R-05 | Admin ve solo su nav (6) + sección Admin | User4 (Admin) | ✅ | ✅ | |
| E-01a | Abrir sin banners ni 403 (Admin) | User4 | ❌ | ✅ | **B-06 RESUELTO**: sin banner, sin respuestas ≥ 400 |
| D-02a | Inicio Admin: KPIs en la PRIMERA carga = API | User4 | ✅ ↻ | ✅ | A la primera: 0 y 16 = API, 4 consultas del OnVisible (la intermitencia de la corrida 3 no se reprodujo) |
| D-02b | Inicio Admin (tibio) = API | User4 | ✅ | ✅ | |
| D-02c | Monitoreo: cuentas, clientes, carga de hoy = API | User4 | ✅ | ✅ | 1,006 · 959 · 0 |
| F-05 | Reconciliación sin carga de hoy: % y freno | User4 | ✅ | ✅ | card ⛔ visible |
| D-02d | Cierres: contadores = API | User4 | ✅ | ✅ | 151 · 47 |
| F-04c | Agentes: 0 escrituras al abrir + panel Vincular/Cambiar | User4 | ✅ | ✅ | **B-04/B-19 en Agentes**: botón de vínculo por fila abre el panel del agente; sin 2 caracteres la galería está oculta; "# Use" viaja con `startswith(fullname,…)` y muestra a # User 1/2/3; **Cancelar** cierra el panel; 0 escrituras. "Desvincular" ×2 visibles, **nunca pulsado**; ninguna fila de `galUsrAge` se tocó |
| D-03 | Maestros: búsqueda delegada + B-13 | User4 | ✅ | ✅ | 14 caracteres = 1 consulta |
| F-04b | Alta de cliente QA + anti-duplicado | User4 | ✅ | ✅ | |
| R-03 | User2 (Solicitante real) abre y ve solo su nav | User2 | ✅ | ✅ | |
| E-01b | Abrir sin banners ni 403 (Solicitante) | User2 | ❌ | ✅ | B-06 resuelto |
| D-02e | Inicio Solicitante: frío = API + subtítulo propio | User2 | ✅ | ✅ | |
| D-04 | Nueva solicitud: búsqueda de cuentas delegada | User2 | ✅ | ✅ | Sin texto la galería está oculta; `startswith(numerodecuenta,'QA-E2E-CTA-000')` en `$filter`; devuelve las 4 cuentas QA |
| F-02a | La galería muestra número + cliente y deja elegir | User2 | ❌ | ✅ | **B-04 RESUELTO**: 4 filas con texto ("QA-E2E-CTA-0001 · QA-E2E CLIENTE UNO · C$ 1,500.00 · Mora 30 d"); la tarjeta elegida muestra número, cliente y saldo vencido; la galería se oculta al elegir |
| F-02 | Crear Especial QA por la UI | User2 | ❌ | ❌ → ✅ ↻ | 1.ª: todo bien salvo `Notas = " — QA-E2E solicitud F-02"` (sin tipo): B-20 + helper (11.5). ↻: `Retiro urgente — QA-E2E solicitud F-02`, owner = Solicitado por = User2, pendiente, sin agente, aparece en Mis solicitudes |
| F-02b | Anti-duplicado | User2 | ⛔ | ✅ | "Ya existe una Especial ACTIVA…", sigue 1 sola |
| X-02 | Falla controlada de escritura (B-03) | User2 | ⛔ | ✅ | **B-03 RESUELTO**: sobre una Especial cerrada que el Solicitante no ve, el POST da 412 (alt key); la app muestra "No se pudo enviar la solicitud de la cuenta QA-E2E-CTA-0003: …", **no** dice "Solicitud enviada", el formulario queda intacto y Dataverse no cambia |
| F-02d | Reactivar Especial cerrada PROPIA (B-11) | User2 | ⛔ | ❌ → ✅ ↻ | 1.ª: reactivó el MISMO registro (AS-2032, sin 412, limpia el ciclo anterior) pero con `Notas = " — …"` (B-20). ↻: `Retiro urgente — QA-E2E reactivación F-02d · 88` |
| D-05 | Mínimo privilegio: sin Pagos/Carga ni 403 | User2 | ✅ | ✅ | |
| R-04 | Supervisor ve solo su nav | User3 | ✅ | ✅ | |
| E-01c | Abrir sin banners ni 403 (Supervisor) | User3 | ❌ | ✅ | B-06 resuelto |
| D-02f | POR APROBAR frío y tibio = API | User3 | ✅ ↻ | ✅ | |
| F-03 | Aprobar eligiendo localizador (buscador + galería) | User3 | ❌ | ✅ | **B-04 RESUELTO**: Aprobar deshabilitado sin localizador; buscar "Carlos" → 1 fila "Carlos Mendoza · user1@…" con `startswith(sanic_cm_ag_nombre,'Carlos')` + activo + con usuario (= API); aprobada (3), agente asignado, resolutor = User3; Notify "Aprobada — asignada a Carlos Mendoza" |
| F-03c | Comp-14: owner → usuario del agente | API | ❌ | ✅ | **B-10 RESUELTO**: owner User2 → **User1 en ~6 s** (el flow tardó 5.3 s; no hizo falta esperar los 3 min) |
| F-03b | Rechazar con motivo | User3 | ✅ | ✅ | |
| R-02 | User1 (Backoffice real) abre y ve solo su nav | User1 | ✅ | ✅ | |
| E-01d | Abrir sin banners ni 403 (Backoffice) | User1 | ❌ | ✅ | B-06 resuelto |
| D-02g | Inicio BO: frío = API | User1 | ✅ | ✅ | |
| D-02h | Estación: chip "Todos" y encabezado = API | User1 | ✅ | ✅ | |
| F-01 | Transcripción con el rol Backoffice SOLO | User1 | ✅ | ✅ | |
| F-01b | Transcritas hoy = API | User1 | ✅ | ✅ | |
| R-06a | Multi-rol User1 (BO + Admin y Carga) | User1 | ✅ | ✅ | |
| R-06b | Quitado de Admin y Carga → solo Backoffice | User1 | ✅ | ✅ | |
| F-04 | Admin cierra la Especial QA (UI) | User4 | ✅ | ✅ | |
| D-07 | Carga de HOY (1 fila QA) = API | User4 | ✅ | ✅ | |
| F-05b | Reconciliación con carga de hoy + B-12 | User4 | ✅ | ✅ | 801 filas en 1 página, sin `nextLink`; botón "Cerrar 801…" solo **leído**, casilla y botón deshabilitados |
| F-02e | Tras aprobar y cerrar: el Solicitante sigue viendo SUS Especiales | User2 | ✅ (condicionado) | ✅ | **Ya sin condición (B-17 RESUELTO)**: AS-2033 con owner = **User1** (no User2) y el Solicitante igual la ve: KPIs 1 · 1 · 1 = API y Mis solicitudes lista las 3 de la API |
| F-02c | Re-solicitar por UI tras el ciclo real (B-11) | User2 | ⛔ | ✅ | La app reactiva el MISMO registro (AS-2033, `true · 2`) aunque el owner ya es User1: el compartido le deja leerlo y escribirlo |
| X-01 | Responsive a 800×900 | User4 | ✅ | ✅ | |
| D-01 | Delegación + errores de lectura en toda la red | todos | ❌ | ✅ | **B-19 RESUELTO** (11.6). Única escritura rechazada: el 412 esperado de X-02 |

**Totales:** 41 escenarios → ✅ **39** · ❌ **2** · ⛔ **0** en la corrida completa; ✅ **41** con el re-chequeo de F-02 y F-02d (corrida 3: 28 / 9 / 4).

**Comparación con la corrida 3:** los 9 ❌ de la corrida 3 (E-01a..d, F-02a, F-02, F-03, F-03c, D-01) pasan (F-02 con el re-chequeo) y los 4 ⛔ (F-02b, X-02, F-02d, F-02c) corren y pasan (F-02d con el re-chequeo): 13 casos mejoran. Ninguno pasó de ✅ a ❌.

### 11.4 Estado de los bugs (B-01 … B-19)

| # | Estado | Evidencia (corrida 4) |
|---|:---:|---|
| B-01 KPIs vacíos en la 1.ª carga | **NO REPRODUCIDO** | D-02a/e/f/g: frío = API en los 4 roles a la primera (Admin con 4 consultas del OnVisible). La intermitencia de Admin vista en la corrida 3 no apareció en esta corrida; con 1 sola muestra no se puede darlo por cerrado |
| B-02 BackOffice sin Append | **RESUELTO** | F-01 |
| B-03 Escritura fallida dada por hecha | **RESUELTO** | X-02: error mostrado, nada dado por hecho |
| B-04 Combos con opciones vacías | **RESUELTO** | Buscador + galería en Nueva solicitud, Aprobaciones y Agentes: F-02a, F-03, F-04c, D-04 |
| B-05 403 por falta de privilegios | **RESUELTO** | D-05 y E-01b/c/d |
| B-06 Banner "expected a table" | **RESUELTO** | E-01a..d ✅ (0 errores visibles) |
| B-07 Carga de hoy = 0 | **RESUELTO** | D-07 |
| B-08 Abrir Agentes escribe | **RESUELTO** | F-04c: 0 escrituras |
| B-09 Responsive / ScaleToFit | **RESUELTO** | X-01 |
| B-10 Flow de owner | **RESUELTO** | F-03c: owner → User1 en ~6 s |
| B-11 412 al re-pedir sobre cerrada | **RESUELTO** | F-02d (AS-2032) y F-02c (AS-2033): mismo registro reactivado, sin 412 |
| B-12 Cota de 500 filas | **RESUELTO** | F-05b: 801 en 1 página |
| B-13 Consulta por tecla | **RESUELTO** | D-03: 1 consulta |
| B-14 Licencias User1/User2 | **RESUELTO** | R-02, R-03 |
| B-15 "Versión publicada sin fixes" | **RETRACTADO** | (§10.7) |
| B-16 Disparador del flow falla sin agente | **RESUELTO** | `flowruns` desde las 22:28Z (modificación del flow): **1 run, Succeeded** (la aprobación de F-03). Las altas y reactivaciones sin agente (F-02, F-02d, F-02c, siembra) **no dispararon** el flow: 0 runs Failed |
| B-17 Rol Solicitante lee a nivel usuario | **RESUELTO** | F-02e y F-02c con el owner movido a User1 |
| B-18 Cola y KPI POR APROBAR con cerradas | **RESUELTO** | D-02f: POR APROBAR 4 = 4 de la API, frío y tibio; la cola de Aprobaciones trae `Estado = true` y `estadoaprobacion = 2` (4 filas, las pendientes activas) |
| B-19 Nueva solicitud descarga la tabla de cuentas | **RESUELTO** | 11.6 |

### 11.5 Corrección del test (cambio mínimo) y causa de B-20

**Síntoma.** En la corrida completa F-02 y F-02d fallaron en un solo check: `Notas = " — <motivo>"` en vez de `"Retiro urgente — <motivo>"`. Todo lo demás pasó (creación, owner, estado, reactivación del mismo registro, ciclo limpio).

**Causa (diagnosticada con la app, sin escribir datos).** `cmbTipoNva` (`Src/scrNuevaSolicitud.pa.yaml` L365–383, `Classic/ComboBox`, `SelectMultiple: false`) nace con `DefaultSelectedItems: =["Retiro urgente"]`. Abierta como User2: el combo muestra "Retiro urgente"; **volver a pulsar esa misma opción lo DESELECCIONA** y el combo queda vacío (`innerText ""`). El helper `elegirCombo` abría el combo y pulsaba "Retiro urgente", o sea, la deseleccionaba. Como `Notas` se arma con `cmbTipoNva.Selected.Value & " — " & Trim(txtMotivoNva.Text)` y ni el hint ni `btnEnviarNva` exigen tipo, la solicitud se envía igual con la nota sin tipo. Por eso es **a la vez** un defecto del test y un defecto real de la app (B-20).

**Corrección del test (`tests/backoffice-canvas/escenarios/comun.js` · `elegirCombo`).** Si el combo ya muestra la opción pedida (y no se pidió búsqueda), no se pulsa: devuelve `ok` con `yaElegida: true`. Afecta a `cmbTipoNva` (B-solicitante.js L22 y L110) y no cambia el resto de usos (`cmbMotivoCie` nace vacío). Se corrió de nuevo la fase B completa: **11/11 ✅** (F-02, F-02d incluidos; la versión probada sigue siendo `2026-10-03T22:25:20Z`).

**Helper `elegirEnBuscador` / `buscarEnBuscador`**: no hizo falta tocarlo. Funcionó a la primera con los buscadores de cuenta (Nueva solicitud) y de agente (Aprobaciones); el panel de Agentes se recorrió con `buscarEnBuscador` (buscar y cancelar).

### 11.6 Delegación (corrida 4)

**Limpia.** 49 combinaciones pantalla/rol (corrida 3: 43):

- **0 errores de lectura**, **0 N+1**, **0 alertas de GET sin filtro a tabla grande** (en la corrida 3 había 1: B-19).
- **Nueva solicitud ya no baja la tabla de cuentas (B-19 confirmado):** al abrir la pantalla hay **1 GET** a `sanic_cm_tbl_cuentas` con `$filter=(startswith(sanic_cm_ct_numerodecuenta,'§§'))or(startswith(sanic_cm_ct_nombrecliente,'§§'))` → el centinela, **0 filas** (antes: 1,006 filas sin filtro). Al buscar `QA-E2E-CTA-000` la consulta lleva `startswith(...)` en `$filter` y devuelve las 4 cuentas QA. Lo mismo en Aprobaciones: al abrir `startswith(sanic_cm_ag_nombre,'§§')` (0 filas) y al buscar "Carlos" `startswith(sanic_cm_ag_nombre,'Carlos')` + activo + con usuario (1 fila); en Agentes: `systemusers?$filter=startswith(fullname,'§§')…` (0 filas) y `'# Use'` (10 filas) al buscar.
- Conteos y sumas siguen como `$apply=…/aggregate(...)`; listas con `$filter` y `$expand`; Maestros con `startswith` (1 consulta por búsqueda).
- Escrituras observadas, todas sobre registros QA-E2E: alta de cliente y teléfono, creación de Especial (POST), 1 POST rechazado con 412 (X-02, esperado), reactivación (PATCH ×2), aprobación y rechazo (PATCH), transcripciones (PATCH ×2), cierre (PATCH). Abrir Agentes y abrir/buscar/cancelar el panel de vínculo: 0 escrituras.

Requests por pantalla (GET): Inicio frío 6–11 · Monitoreo 3–6 · Reconciliación 3 · Cierres 4 · Agentes 4 + 1 (panel) · Maestros 2 + 1 + 1 · Mis solicitudes 2 · **Nueva solicitud 1 (centinela, 0 filas) + 1 por búsqueda** · Aprobaciones 2 (cola + centinela) · Transcripción 5–12 · Transcritas 3.

### 11.7 Bugs nuevos

| # | Sev. | Dónde | Qué pasa | Evidencia / causa / fix candidato |
|---|:---:|---|---|---|
| B-20 | 🟢 BAJA | `Src/scrNuevaSolicitud.pa.yaml` · `cmbTipoNva` (L365–383), `btnEnviarNva.OnSelect` (L598 y L618), `lblHintNva` / `btnEnviarNva.DisplayMode` (L524–554) | El combo "Tipo de solicitud" admite quedar **vacío** (un segundo clic sobre la opción elegida la deselecciona) y nada lo valida: la solicitud se envía con `Notas = " — <motivo>"`, sin tipo. El Supervisor la ve en la cola de Aprobaciones con la nota " — QA-E2E reactivación F-02d" | F-02 y F-02d (1.ª pasada): `obtenido " — QA-E2E solicitud F-02"` / `" — QA-E2E reactivación F-02d · 88"`; el resto de los checks en verde. Diagnóstico: el combo muestra "Retiro urgente", tras pulsarla queda `""`. **Fix candidato** (no aplicado; la app no se toca en esta corrida): (a) exigir tipo en `lblHintNva` y `btnEnviarNva.DisplayMode` (`!IsBlank(cmbTipoNva.Selected.Value)`), o (b) `Coalesce(cmbTipoNva.Selected.Value, "Retiro urgente")` en los 2 `Notas`, o (c) cambiar el control a un Dropdown (no admite vaciar) |

Observación menor (no es bug): al buscar, la primera fila de `galCtaNva` / `galAgeAprb` / `galUsrAge` figura como "Selected" por el `Default` de la galería (se ve en el texto accesible "Item 1. Selected."). No selecciona nada hasta que se pulsa una fila: F-02a verifica que la tarjeta de cuenta aparece solo tras elegir.

### 11.8 Limpieza (verificada)

| Verificación | Resultado |
|---|---|
| Restos QA-E2E en las 9 tablas (limpieza previa y final) | **0** en las 9 tablas, en la corrida completa y en el re-chequeo de B; 0 errores de borrado |
| Membresías de teams | Backoffice = [User1] · Solicitantes = [User2] · Supervisores = [User3] · Admin y Carga = [User10, User4]: **exactamente el mapeo** (`mapeoFinal.comoMapeo = true`, `restauracion.iguales = true`, 0 cambios pendientes) en ambas corridas. User1 se agregó y se quitó de "Admin y Carga" dentro de la fase E |
| Agentes reales | usuario, email y activo **idénticos** a la foto inicial (`agentes.identicos = true`, 0 restaurados) en ambas corridas. Nunca se pulsó Vincular ni Desvincular; en Agentes solo se abrió el panel, se buscó y se **canceló**; ninguna fila de `galUsrAge` se tocó |
| Cierre en lote | **Nunca se pulsó**: solo se leyó el texto del botón ("Cerrar 801 asignaciones en lote") y su estado deshabilitado |
| Fila QA de carga de hoy | Creada solo en la fase F y borrada al terminar |
| Flow | Solo lectura: se consultaron `workflows` y `flowruns`. No se modificó |
| App / flow / roles / Smelter / sesión admin / guías 00–13 | No se usaron ni se modificaron. Sin despliegues. Sin commit ni push |

### 11.9 Próximo paso

1. **B-20**: decidir el fix (11.7) y desplegarlo; hoy es BAJA porque el motivo sí se guarda y el tipo por defecto es el correcto salvo que el usuario lo vacíe a propósito.
2. **B-01** en Admin: dejarlo en observación (no se reprodujo). Si reaparece, capturar con el Monitor de Studio.
3. Llevar el flow corregido a `solutions/unpacked` con el próximo `solution-release` → `solution-sync` (§10.10).
4. Con los 41 escenarios en verde y delegación limpia, la suite queda como **regresión previa a cada despliegue**: `BO_RESULTADOS_DIR=$PWD/resultados/corrida-N node run.js` y revisar "Versión de la app probada" contra el último *Published*.


### 11.x Corrección de B-20 (después de la corrida 4)

`scrNuevaSolicitud`: el tipo de solicitud pasa a ser **obligatorio**. El aviso agrega "tipo" a "Falta: …" cuando `cmbTipoNva.Selected.Value` está vacío, y `btnEnviarNva.DisplayMode` exige `!IsBlank(cmbTipoNva.Selected.Value)`. Así `Notas` nunca se graba como `" — motivo"`. Se mantiene el combo estático porque funciona; lo que se cerró es el hueco de validación.
