# Guía 14 — Resultados de pruebas E2E · App - CM - Backoffice Canvas

> Corrida automatizada con **Playwright sobre el PLAYER real** (no Studio), usuarios reales por rol y
> verificación de cada número/escritura contra **Dataverse** (Web API con el Service Principal).
> Suite: [`tests/backoffice-canvas/`](../../../tests/backoffice-canvas/README.md) · plan base: guía 13.
> Prioridad del dueño: **cero problemas de DELEGACIÓN** → sección 4.

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
