# 00 — Análisis y plan · App - CM - Backoffice (Canvas)

> **Fuente de verdad del DISEÑO:** `docs/mockups/backoffice-canvas.html` (aprobado 2026-09-13).
> **Fuente de verdad del ESQUEMA:** auditoría de metadata en Dev del 2026-09-13 (workflow `diag-metadata`,
> runs 34807850526 / 34807932049) — todos los display names de este paquete salieron de ahí, no de memoria.
> **Reemplaza a:** la model-driven "App - CM - Backoffice" (`sanic_AppCMBackoffice`). La MDA se borró de DEV
> el 2026-10-23 a pedido del dueño (con su sitemap, sus 13 app actions y su biblioteca de comandos); su
> definición queda en el historial de git (`solutions/unpacked`, antes del commit que la quita).

## 1. Qué se construye

Una canvas app **responsive** (tableta/escritorio primero, usable en móvil) para los 4 roles de oficina.
Cada rol entra y ve **solo sus pantallas**; la seguridad de datos real la siguen dando los roles de
Dataverse (`SR | CM | *`) — la app es la primera puerta, no la única.

| Rol (gblRol) | Pantallas | Escribe sobre |
|---|---|---|
| `Backoffice` | scrInicio · scrTranscripcion · scrTranscritas | `interaccion` (estado grabación, código, fecha, grabado por) |
| `Solicitante` | scrInicio · scrNuevaSolicitud · scrMisSolicitudes | `asignacion` (crea Especiales `Pendiente aprobacion`) |
| `Supervisor` | scrInicio · scrAprobaciones | `asignacion` (estado de aprobación, agente, resolutor, motivo de rechazo) |
| `Admin` | scrInicio · scrMonitoreo · scrReconciliacion · scrCierres · scrAgentes · scrMaestros | `asignacion` (cierres), `agente` (usuario/activo), `cliente`/`telefono`/`direccion` (maestros) |

Inventario de pantallas (11) y su guía:

| # | Pantalla | Guía | Rol |
|---|---|---|---|
| 1 | `scrInicio` | 02 | todos (contenido por rol) |
| 2 | `scrTranscripcion` | 03 | Backoffice |
| 3 | `scrTranscritas` | 04 | Backoffice |
| 4 | `scrMisSolicitudes` | 05 | Solicitante |
| 5 | `scrNuevaSolicitud` | 06 | Solicitante |
| 6 | `scrAprobaciones` | 07 | Supervisor |
| 7 | `scrMonitoreo` | 08 | Admin |
| 8 | `scrReconciliacion` | 09 | Admin |
| 9 | `scrCierres` | 10 | Admin |
| 10 | `scrAgentes` | 11 | Admin |
| 11 | `scrMaestros` | 12 | Admin |

## 2. LEDGER de divergencias declaradas vs mockup

Toda diferencia entre el mockup aprobado y lo que la canvas puede/debe hacer se declara acá.
**Nada de divergencias silenciosas.** Las guías de pantalla referencian estas entradas por ID.

| ID | Mockup | Canvas (decisión) | Por qué |
|---|---|---|---|
| LED-01 | Shortname estilo core (`GUTIÉRREZ/ROSA`) | `Upper()` del nombre completo (`ROSA AMELIA GUTIÉRREZ`) | No existe columna shortname en `cliente`/`cuenta` y reordenar apellidos en Power Fx es frágil con nombres de 2–5 palabras. Si el negocio lo exige, es columna nueva del CSV+mapping (backlog, no bloquea). |
| LED-02 | Clic en el nº de cuenta copia al portapapeles | `Copy(...)` de Power Fx **si la versión de Studio lo ofrece**; si IntelliSense no lo reconoce, el número queda solo visual (grande y seleccionable) | `Copy()` es función reciente; no se garantiza en todas las versiones del player. |
| LED-03 | Atajos de teclado Enter / ↑ / ↓ | **Enter**: sí — el `OnChange` del input de código dispara con Enter; el flujo "código → Enter → siguiente" se conserva. **↑/↓**: no existen eventos de teclado en canvas — la navegación por la cola es por clic/tap | Canvas no expone keydown. La fluidez la sostiene el auto-avance tras marcar. |
| LED-04 | Barra de demo para cambiar rol | No existe en la app real: `gblRol` sale de la tabla `Usuario App` (§3.1). Para pruebas, cada tester entra con su cuenta | La barra era solo del mockup. |
| LED-05 | Toasts flotantes | `Notify(...)` nativo | Equivalente funcional. |
| LED-06 | "Hace 2 días" en reconciliación | Fecha corta real (`dd/mm`) del campo `'Última carga'` | Fechas relativas en Power Fx = fórmula frágil; la fecha concreta es más auditable. |
| LED-07 | Monitoreo muestra "3 asignaciones nuevas" y "0 errores de fila" | v1 muestra: conteo de filas de `'CM | TBL | Carga diarias'` del día, cuentas/clientes totales, y fecha/hora de la última carga. Errores de fila viven en Smelter (otra app) — se enlaza, no se duplica | El log de errores es de la tabla interna de Smelter; leerla desde esta app acopla productos. |
| LED-08 | Panel de cliente en Maestros permite "Editar" | v1: crear cliente + editar teléfonos/direcciones/datos básicos. La edición de CUENTAS no existe (las cuentas las crea/actualiza el import) | La cuenta es del core; editarla a mano contradice DEC-012. |
| LED-09 | El detalle de transcripción muestra "antes" del dato actualizado | El "antes" sale de `'CM | TBL | Telefonos'`/`'CM | TBL | Direccions'` por CIF (colección en memoria); si no hay dato previo → tag NUEVO | Igual que el mockup, con la fuente real nombrada. |
| LED-10 | Fila "Todos" en la columna de localizadores | Se conserva, pero la cola "Todos" se limita a la colección del día (§4.1) igual que las demás | Sin costo extra. |
| LED-11 | "Tipo de especial" como campo propio de la solicitud | No hay columna en `asignacion`: v1 lo antepone al texto de `'Notas'` → `"Retiro urgente — <motivo>"`. Columna choice propia = backlog | El `'[CT] Tipo Especial'` de la cuenta es del CSV, no de la solicitud. |
| LED-12 | Monitoreo con mini-tabla de últimas 4 cargas | v1: solo la carga de HOY (conteo + fecha/hora de `nfUltimaCarga`). El historial por día exige agregación no delegable sobre ~7 000 filas | Amplía LED-07. Backlog: tabla espejo `cargaresumen` server-side. |
| LED-13 | Maestros: botón "Editar" genérico | v1: crear cliente completo · editar datos básicos del cliente · AGREGAR teléfonos/direcciones. Editar/borrar tel-dir existentes = backlog | Acota LED-08 a lo que un form canvas hace sin riesgo. |
| LED-14 | scrInicio: KPIs "Aprobadas hoy" (Supervisor) y "Cierres pendientes" (Admin) | Omitidos en v1 | No hay columna de fecha de resolución verificable, y el conteo de cierres exigiría coleccionar cientos de filas solo para un número (00 §4.3). Declarado por la guía 02. ⚠ La segunda razón quedó superada por la corrección de §4.3 (`CountIf` delega): "Cierres pendientes" es quick-add con `CountIf`; "Aprobadas hoy" sigue bloqueado por la falta de fecha de resolución. |
| LED-15 | scrCierres: la fila del mockup mezcla saldo y retiro | Regla única horneada (`montoCie`): Especial con retiro > 0 → Moneda de retiros; todo lo demás → saldo vencido. El panel siempre muestra saldo vencido | Regla determinista en vez de ambigüedad visual. Declarado por la guía 10. |
| LED-16 | Panel de Transcritas muestra "📍 Ver en mapa" | Omitido en v1 en el panel del ARQUEO (las columnas `'[IN] Latitud'/'[IN] Longitud'` SÍ existen; la estación de transcripción — guía 03 — sí muestra el mapa, que es donde importa) | Valor marginal post-transcripción; quick-add si el UAT lo pide. |
| LED-17 | La nav muestra badges con conteo de pendientes por ítem | **Sin badges en v1.** Los conteos viven dentro de cada pantalla (KPIs de Inicio + contadores de las colas) | Un badge global exige estado compartido entre pantallas (colecciones fuera del OnVisible + sincronía al mutar) — fragilidad que no paga lo que aporta. Quick-add futuro: `gblBadgePend` refrescado por las pantallas que mutan. (Hallazgo R1-01.) |
| LED-18 | El CSS móvil del mockup ordena: localizadores → cola → detalle | Canvas: localizadores → **detalle** → cola (la narrativa aprobada por el usuario en el chat: "los localizadores pasan a fila deslizable arriba, luego el detalle y la cola debajo") | El orden del DOM del mockup fue un descuido del CSS, no una decisión: la aprobación explícita fue sobre la narrativa. (Hallazgo R1-03.) |
| LED-19 | Las filas del mockup son clicables enteras (un `<div>` con hover) | **Un tab stop por fila** (decisión del usuario, Opción A, validación en Studio): overlay `btnFila…` (`Botón clásico` transparente, ARRIBA del z-order, `TabIndex 0`, `Text` descriptivo, foco visible, `HoverFill` translúcido — receta 01 §7.3). Las etiquetas del template van con `TabIndex -1` y sin `OnSelect` propio; `FocusedBorderThickness > 0` en todo control interactivo; rectángulos decorativos `TabIndex -1` + `AccessibleLabel ""`. **El Accessibility checker marca ~255 "Missing tab stop" sobre etiquetas decorativas: FALSO POSITIVO CONSCIENTE, no se corrige** | Doc oficial: *"Label, Image, Icon, Shape controls: Set TabIndex to 0 if they are meant to be interactive. Otherwise, set TabIndex to -1"* ([Create accessible canvas apps](https://learn.microsoft.com/power-apps/maker/canvas-apps/accessible-apps#keyboard)); *"Screen readers will treat Labels as buttons when TabIndex is zero or greater"* ([Label control](https://learn.microsoft.com/power-apps/maker/canvas-apps/controls/control-text-box#accessibility-guidelines)); `AccessibleLabel` vacío oculta Image/Icon/Shape al lector ([Accessibility properties](https://learn.microsoft.com/power-apps/maker/canvas-apps/controls/properties-accessibility)); "Focus isn't showing" es error si `FocusedBorderThickness = 0` ([Accessibility checker](https://learn.microsoft.com/power-apps/maker/canvas-apps/accessibility-checker#types-of-issues)). Darle tab stop a cada etiqueta multiplicaría los Tab por fila y las anunciaría como botones. |
| LED-20 | (no aplica al mockup — diseño original de 01 §4) Guard de rol por pantalla: `If(!("Rol" in gblRol), Navigate(scrInicio))` en el `OnVisible` | **Eliminado de TODAS las pantallas.** El acceso visual lo da la nav filtrada por rol (`cmpNavLateral` + `nfNav`); la seguridad REAL, los roles de Dataverse `SR \| CM \| *` | Studio rechaza `Navigate` en `OnVisible` (*"would automatically always navigate away"*). Un usuario que llegue a una pantalla ajena por URL/deep link ve la pantalla, pero Dataverse no le devuelve ni le deja escribir lo que su rol no permite. |

## 3. Backend faltante (backend-first: esto se hace ANTES de construir pantallas)

### 3.1 Identidad por ROLES DE SEGURIDAD reales — SIN tabla adicional (decisión 2026-09-14)

**Reemplaza a la tabla `Usuario App`** (el diseño anterior de esta sección queda documentado abajo
como historia; la tabla se elimina de la solución). El usuario objetó la doble administración y
tiene razón: los 4 perfiles de la app mapean 1:1 con los roles `SR | CM | *`, así que la app deriva
`gblRol` de la seguridad REAL (verificado en Dev que los roles se reparten VÍA TEAMS).

**Como quedó tras la validación en Studio (2026-10-02) — dos fuentes, unidas:**

1. **Roles DIRECTOS** (`systemuserroles`): `gblUsuarioDV.'Security Roles (systemuserroles_association)'`
   filtrado por `StartsWith(Name, "SR | CM | ")` → se quita el prefijo (`"SR | CM | Admin"` → `"Admin"`).
2. **Perfil por TEAM, derivado del NOMBRE del team** `"CM · <perfil> · <ENV>"`:
   `Index(Split('Team Name', " · "), 2).Value` → `Backoffice`→`BackOffice` · `Solicitantes`→`Solicitante` ·
   `Supervisores`→`Supervisor` · `Admin y Carga`→`Admin` (el sufijo `DEV`/`PRD` se ignora).

⚠ **Por qué no se navega Team → Security Roles** (el diseño original de esta sección):
canvas NO deja leer `Tm.'Security Roles (teamroles_association)'` dentro de un `ForAll`
(*"not accessible in this context"* — verificado en Studio, y tampoco funciona con
`LookUp(Teams, ...)` para re-obtener el team). Las relaciones N:N de un registro solo se navegan
desde el registro "raíz" (`gblUsuarioDV`), un nivel.

- **Privilegios**: verificado en Dev (diag-roles-canvas) que los 6 roles `SR | CM | *` ya tienen
  `prvReadRole` y `prvReadTeam`. `Users` se lee con "Usuario básico" (estándar del proyecto).
- **Datasources en la app**: `Users`, `Security Roles` y `Teams` (los agrega el usuario en Studio —
  no viajan por YAML). Se retira `'CM | TBL | Usuario Apps'`.
- **Delegación**: no aplica — navegación de relaciones de UN registro (conjuntos de puñados de
  filas) + operaciones en memoria; corre una vez en OnStart. Sin colección intermedia (dos `Concat`
  → `Split` → `Distinct`; ni `Collect` dentro de `ForAll`).
- **Fragilidad declarada (DOS acoples)**: (a) el prefijo `"SR | CM | "` de los roles directos;
  (b) el **nombre de los teams** `"CM · <perfil> · <ENV>"` — **renombrar un team rompe el mapeo en
  silencio** (el usuario pierde la nav de ese perfil). Si se agrega un perfil/team nuevo, se agrega
  su caso al `Switch` de 01 §4. Ambas son convenciones estables del proyecto.
- **Multi-rol**: una persona con 2 roles (2 teams o directo+team) obtiene `gblRol = "BackOffice|Admin"`
  — el resto de la app no cambia (el `in` es case-insensitive).
- Fórmula canónica: 01 §4. Sin ROL `SR | CM | *` ni team `CM · …` → `gblRol` queda vacío → estado
  "sin acceso" de scrInicio (igual que antes).

#### (Histórico — diseño anterior con tabla, eliminado)
Tabla `CM | TBL | Usuario App` ✅ creada (2026-09-14) y luego DESCARTADA:

La app necesita saber el rol SIN leer roles de seguridad (canvas no puede). **Como quedó construida**
(difiere del diseño original a pedido del usuario: lookup en vez de email, y MULTI-rol):

| Columna (lógico) | Display real | Tipo | Nota |
|---|---|---|---|
| `sanic_cm_ua_usuarioid` | `[UA[ Usuario` ⚠ typo en Dev — renombrar a `[UA] Usuario` | **Lookup → Users** | La identidad. Las fórmulas siguen a IntelliSense |
| `sanic_cm_ua_nombre` | `[UA] Nombre` | Texto | Campo primario |
| `sanic_cm_ua_rol` | `[UA] Rol` | **(Histórico: la tabla `usuarioapp` se borró el 2026-10-15, DAT-30; ya no existe.)** **Choice MULTI-SELECT local** `sanic_cm_tbl_usuarioapp_sanic_cm_ua_rol`: 1=Backoffice · 2=Solicitante · 3=Supervisor · 4=Admin | Una persona puede tener VARIOS roles |
| `sanic_cm_ua_activo` | `[UA] Activo` | Sí/No | Soft-delete |

- **Modelo multi-rol en la app**: `gblRol` es el texto de TODAS las etiquetas unidas con `|`
  (`"Backoffice|Admin"`). Los guards y Visibles preguntan `"Rol" in gblRol`. scrInicio muestra las
  secciones de TODOS los roles del usuario, apiladas (adenda de la guía 02).
- La administran los Admin (v1: directo en la tabla vía portal/MDA; pantalla propia = backlog).
- La seguridad REAL no depende de esto: un Solicitante que "hackee" la navegación sigue sin poder
  escribir `interaccion` porque su rol Dataverse no tiene ese privilegio.

### 3.2 Sello de auditoría de transcripción

v1: la app escribe `'[IN] Fecha grabacion' = Now()` y `'[IN] Grabado por' = gblUsuarioDV` en el mismo
`Patch` del código (receta 7.8 de la guía 01). El flujo server-side "Auditoría de transcripción"
(diseño del doc 08) queda como refuerzo posterior — cuando se active, la app DEJA de escribir esos dos
campos (cambio de una línea, anotado en la guía 03).
⚠️ Si se aplica el Field Security Profile del doc 08 (personas no escriben `fechagrabacion`/`grabadopor`),
este Patch falla → activar el flujo ANTES del perfil. Orden documentado, no negociable.

### 3.3 Verificar el Required Level de `'[AS] Agente'`

La solicitud de Especial nace **sin localizador** (lo asigna el Supervisor al aprobar). Si
`sanic_cm_as_agenteid` está como *Application required*, el `Patch` de la guía 06 falla → bajarlo a
**Opcional** en Dev antes de construir esa pantalla (verificación de 1 minuto en el portal).

### 3.4 Roles de seguridad a ajustar (no bloquea la construcción, sí el UAT)

- `SR | CM | Solicitante` hoy tiene Create/Write **Global** sobre cliente/cuenta/direccion/telefono —
  contradice el rediseño (solo Admin toca maestros). Trimear a Read antes del UAT del Solicitante.
- Falta rol para el equipo de oficina en la tabla `Usuario App` (Read propio o Global según rol Admin).

## 4. Delegación — la arquitectura que la evita por diseño

**Regla madre (corregida tras la validación en Studio): las LISTAS de trabajo del día se trabajan EN
MEMORIA; los NÚMEROS (KPIs, totales, denominadores, contadores de pestañas) se piden DIRECTO al
datasource con `CountIf`/`Sum` delegables; a Dataverse van filtros delegables, agregados y Patch.**

### 4.1 Patrón "colección del día" (receta 7.8 en guía 01)

Las colas operativas son acotadas por naturaleza (lo pendiente de HOY, no la historia):

| Colección | Fuente delegable | Cota real |
|---|---|---|
| `colPend` | `Filter('CM \| TBL \| Interaccions', '[IN] Estado grabacion' = 'Choice \| CM \| Estado grabacion Interaccio'.Pendiente)` | decenas–cientos/día |
| `colAprob` | `Filter('CM \| TBL \| Asignacions', '[AS] Tipo' = ...Especial && '[AS] Estado de aprobacion' = ...'Pendiente aprobacion')` | unidades |
| `colDesap` | `Filter('CM \| TBL \| Asignacions', '[AS] Tipo' = ...Diaria && '[AS] Estado' = true && 'Última carga' < varCorteRec)` — corte = 00:00 **UTC** del día de `nfUltimaCarga` (§4.4) | unidades–decenas |
| `colCierres` | Incobrables/Especiales activas (filtro por tipo+estado) | cientos máx |

Sobre la colección: GroupBy por localizador, búsqueda, orden FIFO — todo en memoria, cero
delegación, fluidez de estación. **Cota declarada: 2 000 filas por colección** (= *Data row limit*
de la app, 01 §1 — ⚠ estuvo en 500 hasta la validación porque el ajuste nunca se había aplicado).
Si un día la cola pendiente supera 2 000, la app muestra las 2 000 más viejas — aceptado y anotado
en la guía 03; el monitoreo del Admin lo delataría. **Los contadores NO salen de la colección**
(saldrían truncados al límite): salen de `CountIf` sobre el datasource (§4.2), y la pantalla puede
avisar "mostrando N de M" comparando ambos (p.ej. `lblTopeRec` en la guía 09).

⚠ **Colecciones de CATÁLOGO prohibidas como fuente de joins** (`colCtasTrx`, `colCtasAprb`…): un
`ShowColumns('CM | TBL | Cuentas', ...)` trae como máximo el data row limit de filas, así que el
`LookUp` contra esa colección devolvía `"—"` para toda cuenta que quedara fuera. El patrón venía de
la app del localizador, donde el OFFLINE lo justifica; en una app ONLINE es un tope artificial.
Reemplazo: **navegación del lookup** (`ThisRecord.'[AS] Cuenta'.'Nombre Cliente'`, receta 01 §7.7).

### 4.2 Filtros que SÍ van directo al datasource (delegables verificados en Dataverse)

- Igualdad y `&&` sobre: choice por valor (`'[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial`),
  booleano, lookup por registro (`'[AS] Cuenta' = gblCuenta`), texto exacto.
- Comparación de fechas (`'Última carga' < gblUltimaCarga`, `'Fecha del pago' >= Today()`).
- `StartsWith(columna, texto)` — la búsqueda de Maestros usa esto contra `'[CL] CIF'` y `'[CL] Nombre'`.
- `Sort`/`SortByColumns` sobre columna escalar + `First()` — así se obtiene `gblUltimaCarga`
  (`First(Sort('CM | TBL | Carga diarias', '[CD] FechaCarga', SortOrder.Descending)).'[CD] FechaCarga'`).
- `LookUp` por alt key o columna indexada (`LookUp(Users, 'Primary Email' = Lower(User().Email))` —
  el `Lower()` va sobre el ARGUMENTO local, no sobre la columna: delegable).
- **Agregados** `CountIf`/`Sum` sobre el datasource (filtrado o no) — ver tabla abajo.
- Lookup por PK (`'[CT] Cliente'.'CM | TBL | Cliente' = varCliMae.'CM | TBL | Cliente'`) — así filtra
  `galCtasMae` directo al datasource.

**Tabla de referencia (Dataverse, verificada en Studio: CERO warnings de delegación con estas formas).**
Fuente: [Power Apps delegable functions and operations for Dataverse](https://learn.microsoft.com/power-apps/maker/canvas-apps/connections/connection-common-data-service#power-apps-delegable-functions-and-operations-for-dataverse)
y [Count, CountA, CountIf, and CountRows](https://learn.microsoft.com/power-platform/power-fx/reference/function-table-counts).

| Operación | ¿Delega? | Uso en este proyecto |
|---|---|---|
| `CountIf(tabla, condición)` | **Sí — EXACTO hasta 50 000 filas** | KPIs, contadores de pestañas, denominadores (`varNDesapRec`, `varNPendIni`…) |
| `CountIf(tabla, true)` | Sí — total exacto (< 50 000) | Totales de Monitoreo (`varNCtasMon`, `varNCliMon`) |
| `CountRows(tabla)` sin filtro | Sí, pero devuelve un valor **CACHEADO** (se refresca periódicamente) | NO usar para números que se muestran; usar `CountIf(t, true)` |
| `Sum/Min/Max/Average(Filter(...), col)` | Sí, para columnas **numéricas** (no DateTime) — tope de agregado 50 000 | "Cobrado hoy" de Inicio: `Sum` directo sobre `'CM \| TBL \| Pagos'` |
| `=`, `<>`, `<`, `>=`… sobre número/texto/fecha; `=` sobre choice; `&&`/`\|\|`/`!` | Sí (`<`/`>` sobre **choice**: NO) | Filtros de todas las colas |
| `StartsWith(col, texto)` | Sí | Búsqueda de Maestros (receta 01 §7.9) |
| `Sort`/`SortByColumns` + `First` | Sí | `nfUltimaCarga` |
| `col = Blank()` / `IsBlank(col)` sobre texto/número/fecha | Sí | `'Email Usuario' = Blank()` (agentes sin usuario) |
| `IsBlank(col)` sobre una columna **LOOKUP** | **NO** (warning verificado) | Reemplazar por una columna de TEXTO equivalente: `'Email Usuario' = Blank()` en vez de `IsBlank('[AG] Usuario')` |
| `Search()`, `in` (substring), `Len()`, `Trim`, aritmética en el predicado, `Text(col)` | NO | Prohibidos (§4.3) |
| `GroupBy`, `Concat`, `FirstN/Last`, `ClearCollect`, `Collect` | NO (son locales) | Solo sobre colecciones |

### 4.3 PROHIBIDO en fórmulas contra datasource (la lista negra del revisor R2)

- `Search()`, `in` (substring), `Len()`, `Trim()`, aritmética en el predicado, `Text(col)`.
  (`Upper()/Lower()` sobre columnas figuran como soportados en la doc de Dataverse, pero el proyecto
  no los necesita: se sigue normalizando el ARGUMENTO local.)
- `IsBlank()` sobre una columna **lookup** (no delega) → columna de texto equivalente.
- `CountRows(datasource)` para un número visible (valor cacheado) → `CountIf(datasource, true)`.
- ~~`Sum/CountRows/Max` directos sobre datasource; los KPI salen de colecciones~~ — **ERROR del
  diseño original, corregido (2026-10-02)**: `CountIf` y `Sum/Min/Max/Avg` (numéricos) SÍ delegan
  en Dataverse (tabla de §4.2). Contar desde una colección era el bug: la colección se trunca al
  data row limit y el KPI mentía en silencio. **Regla nueva: KPIs/totales/denominadores = `CountIf`/
  `Sum` directo al datasource; colecciones SOLO para listas de trabajo.**
- ~~Joins EN MEMORIA contra colecciones de catálogo por `'[AS] NumeroCuenta'`/CIF~~ — **reemplazado**:
  la colección de catálogo se trunca (§4.1). Las columnas de la cuenta/cliente se leen por
  **navegación del lookup** dentro del `AddColumns` que hornea la colección de trabajo
  (`ThisRecord.'[AS] Cuenta'.'Nombre Cliente'`). Lo que sigue prohibido es navegar relaciones
  DENTRO del predicado de un `Filter` contra datasource.
- `ClearCollect(col, If(cond, Filter(...)))` — el `If` va POR FUERA (gotcha documentado en 01).

### 4.4 Zona horaria de las fechas de carga (bug real, corregido en la validación)

- `'Última carga'` (asignación) es **DateTime con comportamiento *User local*** y el import la graba
  a **medianoche UTC**; `'[CD] FechaCarga'` (`nfUltimaCarga`) es **DateOnly**.
- El diseño original comparaba `'Última carga' < nfUltimaCarga` / contra "00:00 del día local":
  en Nicaragua (UTC−6) la medianoche UTC del día de hoy cae a las 18:00 de AYER → **el 100% de las
  diarias salía como "desaparecida"** (falso positivo; lo bloqueó el umbral del 30% de la guía 09).
- **Fix (receta 01 §7.15)**: corte = `DateAdd(DateTime(Y,M,D,0,0,0), -TimeZoneOffset(d0), TimeUnit.Minutes)`
  (= 00:00 UTC del día de la carga); para MOSTRAR la fecha, `DateAdd(x, TimeZoneOffset(x), TimeUnit.Minutes)`.
  En templates de galería vacíos, guardar con `If(IsBlank(x), "—", ...)`: `TimeZoneOffset(blank)` da
  error de RUNTIME.
- **Backlog**: cambiar `'Última carga'` a **DateOnly** (o a *Time zone independent*) elimina todo el
  ajuste.

## 5. Performance

- `App.OnStart` mínimo: tokens de estilo + `gblRol`/`gblUsuarioDV` (+2 LookUp delegables) — nada de colecciones.
- Cada pantalla llena SUS colecciones en `OnVisible` con `Concurrent()` donde haya 2+.
- La estación de transcripción NO re-consulta al marcar: `Patch` + `RemoveIf(colPend, ...)` local +
  reselección — 1 llamada de red por transcripción. Botón «Actualizar» explícito re-colecta.
- Joins horneados una vez por carga de colección (`AddColumns`), no `LookUp` por fila en galerías —
  y horneados por **navegación del lookup** (`ThisRecord.'[AS] Cuenta'.…`), no contra colecciones de
  catálogo (§4.1).
- **(Validación en Studio)** Nada de mutar colecciones dentro de `ForAll`: un `Patch` masivo con
  una tabla de registros que lleva la PK (`Patch(ds, ForAll(col As D, {PK: D.PK, ...}))`), y las tablas
  se arman con `ForAll` y se cargan de una (`ClearCollect(col, ForAll(...))`).
  `gal.AllItemsCount` en vez de `CountRows(gal.AllItems)`; `Index(t, n)` en vez de `Last(FirstN(t, n))`;
  catálogos estáticos como **fórmula con nombre** (`nfNav`), no como colección del OnStart.

## 6. Estructura de la app en Studio (orden que exige el paso 4/5)

- **Solo contenedores manuales** (`Contenedor` clásico, sin auto-layout). Posiciones/tamaños SIEMPRE
  relativos (`Parent.Width - 2*EPad`, nunca literales absolutos salvo alturas de fila y paddings tokenizados).
- Nombres únicos a nivel app con sufijo de pantalla: `galColaTrx`, `galColaAprb`, `conPanelMae`…
  (patrón `<tipo><Qué><Sufijo3letras>`: Trx, Tsc, Sol, Nva, Aprb, Mon, Rec, Cie, Age, Mae, Ini).
- Componentes locales (guía 01 §6): `cmpNavLateral`, `cmpEncabezado`, `cmpKpi`, `cmpVacio` — todo lo
  repetitivo entre pantallas vive ahí; una pantalla no re-implementa lo que ya es componente.
- Orden del árbol por pantalla: `conRaiz` → `conNav` (componente) → `conContenido` → secciones
  numeradas en el orden visual (arriba→abajo, izquierda→derecha).

## 7. Responsive (contrato)

- Formato tableta, **Escalar para ajustar = Desactivado**, Bloquear relación de aspecto = Desactivado,
  Bloquear orientación = Desactivado.
- Breakpoint único: `nfMovil = App.ActiveScreen.Width < 920` (named formula, definida en 01 §3).
- Desktop: nav lateral fija de `EAnchoNav` (240); contenido `Parent.Width - EAnchoNav`.
- Móvil: nav lateral oculta (`Visible = !gblMovil`), barra superior con hamburguesa
  (`Visible = gblMovil`) que muestra `conNavMovil` overlay; columnas apiladas
  (las guías dan ambas fórmulas: `If(gblMovil, ...)` en X/Y/W/H).
- La estación de 3 columnas colapsa: localizadores → fila horizontal deslizable (galería horizontal),
  detalle arriba, cola debajo (orden decidido en LED-18).

## 8. Checklist previa a construir (gate del paso 4)

- [ ] ~~Tabla `CM | TBL | Usuario App`~~ (descartada, §3.1) → datasources `Users`, `Security Roles` y
      `Teams` agregados, y teams nombrados `CM · <perfil> · <ENV>` en el entorno (§3.1).
- [ ] Confirmar en Studio el plural exacto de los datasources al conectarlos (esperados:
      `'CM | TBL | Interaccions'`, `'CM | TBL | Asignacions'`, `'CM | TBL | Cuentas'`, `'CM | TBL | Clientes'`,
      `'CM | TBL | Agentes'`, `'CM | TBL | Pagos'`, `'CM | TBL | Telefonos'`, `'CM | TBL | Direccions'`,
      `'CM | TBL | Carga diarias'`, `'CM | TBL | Actualizacion de datos'`(⚠ plural raro — verificar), `'CM | TBL | Usuario Apps'`,
      `Users`). **Si IntelliSense difiere de la guía, el IntelliSense manda** — anotar y avisar.
- [ ] Límite de filas no delegables de la app = **2000** — verificado EN EL EXPORT
      (`msapp/Properties.json` → `DefaultConnectedDataSourceMaxGetRowsCount`), no en Settings (01 §1).
- [ ] Icono de la app (paso 3) subido.
