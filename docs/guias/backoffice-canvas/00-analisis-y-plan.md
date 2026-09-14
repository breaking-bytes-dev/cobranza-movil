# 00 — Análisis y plan · App - CM - Backoffice (Canvas)

> **Fuente de verdad del DISEÑO:** `docs/mockups/backoffice-canvas.html` (aprobado 2026-09-13).
> **Fuente de verdad del ESQUEMA:** auditoría de metadata en Dev del 2026-09-13 (workflow `diag-metadata`,
> runs 34807850526 / 34807932049) — todos los display names de este paquete salieron de ahí, no de memoria.
> **Reemplaza a:** la model-driven "App - CM - Backoffice" (`sanic_AppCMBackoffice`). La MDA queda como
> respaldo hasta que la canvas pase UAT; no se borra en este proyecto.

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
| LED-14 | scrInicio: KPIs "Aprobadas hoy" (Supervisor) y "Cierres pendientes" (Admin) | Omitidos en v1 | No hay columna de fecha de resolución verificable, y el conteo de cierres exigiría coleccionar cientos de filas solo para un número (00 §4.3). Declarado por la guía 02. |
| LED-15 | scrCierres: la fila del mockup mezcla saldo y retiro | Regla única horneada (`montoCie`): Especial con retiro > 0 → Moneda de retiros; todo lo demás → saldo vencido. El panel siempre muestra saldo vencido | Regla determinista en vez de ambigüedad visual. Declarado por la guía 10. |
| LED-16 | Panel de Transcritas muestra "📍 Ver en mapa" | Omitido en v1 en el panel del ARQUEO (las columnas `'[IN] Latitud'/'[IN] Longitud'` SÍ existen; la estación de transcripción — guía 03 — sí muestra el mapa, que es donde importa) | Valor marginal post-transcripción; quick-add si el UAT lo pide. |
| LED-17 | La nav muestra badges con conteo de pendientes por ítem | **Sin badges en v1.** Los conteos viven dentro de cada pantalla (KPIs de Inicio + contadores de las colas) | Un badge global exige estado compartido entre pantallas (colecciones fuera del OnVisible + sincronía al mutar) — fragilidad que no paga lo que aporta. Quick-add futuro: `gblBadgePend` refrescado por las pantallas que mutan. (Hallazgo R1-01.) |
| LED-18 | El CSS móvil del mockup ordena: localizadores → cola → detalle | Canvas: localizadores → **detalle** → cola (la narrativa aprobada por el usuario en el chat: "los localizadores pasan a fila deslizable arriba, luego el detalle y la cola debajo") | El orden del DOM del mockup fue un descuido del CSS, no una decisión: la aprobación explícita fue sobre la narrativa. (Hallazgo R1-03.) |

## 3. Backend faltante (backend-first: esto se hace ANTES de construir pantallas)

### 3.1 Tabla `CM | TBL | Usuario App` — el mapa usuario→roles ✅ CREADA (verificada en Dev 2026-09-14)

La app necesita saber el rol SIN leer roles de seguridad (canvas no puede). **Como quedó construida**
(difiere del diseño original a pedido del usuario: lookup en vez de email, y MULTI-rol):

| Columna (lógico) | Display real | Tipo | Nota |
|---|---|---|---|
| `sanic_cm_ua_usuarioid` | `[UA[ Usuario` ⚠ typo en Dev — renombrar a `[UA] Usuario` | **Lookup → Users** | La identidad. Las fórmulas siguen a IntelliSense |
| `sanic_cm_ua_nombre` | `[UA] Nombre` | Texto | Campo primario |
| `sanic_cm_ua_rol` | `[UA] Rol` | **Choice MULTI-SELECT local** `sanic_cm_tbl_usuarioapp_sanic_cm_ua_rol`: 1=Backoffice · 2=Solicitante · 3=Supervisor · 4=Admin | Una persona puede tener VARIOS roles |
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

**Regla madre: las colas del día se trabajan EN MEMORIA; a Dataverse solo van filtros delegables y Patch.**

### 4.1 Patrón "colección del día" (receta 7.8 en guía 01)

Las colas operativas son acotadas por naturaleza (lo pendiente de HOY, no la historia):

| Colección | Fuente delegable | Cota real |
|---|---|---|
| `colPend` | `Filter('CM \| TBL \| Interaccions', '[IN] Estado grabacion' = 'Choice \| CM \| Estado grabacion Interaccio'.Pendiente)` | decenas–cientos/día |
| `colAprob` | `Filter('CM \| TBL \| Asignacions', '[AS] Tipo' = ...Especial && '[AS] Estado de aprobacion' = ...'Pendiente aprobacion')` | unidades |
| `colDesap` | `Filter('CM \| TBL \| Asignacions', '[AS] Tipo' = ...Diaria && '[AS] Estado' = true && 'Última carga' < gblUltimaCarga)` | unidades–decenas |
| `colCierres` | Incobrables/Especiales activas (filtro por tipo+estado) | cientos máx |

Sobre la colección: GroupBy por localizador, búsqueda, orden FIFO, conteos — todo en memoria, cero
delegación, fluidez de estación. **Cota declarada: 2 000 filas por colección** (límite de
no-delegables subido a 2 000). Si un día la cola pendiente supera 2 000, la app muestra las 2 000 más
viejas — aceptado y anotado en la guía 03; el monitoreo del Admin lo delataría.

### 4.2 Filtros que SÍ van directo al datasource (delegables verificados en Dataverse)

- Igualdad y `&&` sobre: choice por valor (`'[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial`),
  booleano, lookup por registro (`'[AS] Cuenta' = gblCuenta`), texto exacto.
- Comparación de fechas (`'Última carga' < gblUltimaCarga`, `'Fecha del pago' >= Today()`).
- `StartsWith(columna, texto)` — la búsqueda de Maestros usa esto contra `'[CL] CIF'` y `'[CL] Nombre'`.
- `Sort`/`SortByColumns` sobre columna escalar + `First()` — así se obtiene `gblUltimaCarga`
  (`First(Sort('CM | TBL | Carga diarias', '[CD] FechaCarga', SortOrder.Descending)).'[CD] FechaCarga'`).
- `LookUp` por alt key o columna indexada (`LookUp('CM | TBL | Usuario Apps', '[UA] Email' = Lower(User().Email))` —
  el `Lower()` va sobre el ARGUMENTO local, no sobre la columna: delegable).

### 4.3 PROHIBIDO en fórmulas contra datasource (la lista negra del revisor R2)

- `Search()`, `in`, `Upper()/Lower()` **sobre columnas**, `Len()`, aritmética en el predicado.
- `Sum/CountRows/Max` directos sobre datasource filtrado no-delegable; los KPI salen de colecciones.
- Navegar relaciones dentro de `Filter` (`'[AS] Cuenta'.'[CT] Cliente'...`) — joins EN MEMORIA por
  `'[AS] NumeroCuenta'`/CIF denormalizados, igual que la app del localizador.
- `ClearCollect(col, If(cond, Filter(...)))` — el `If` va POR FUERA (gotcha documentado en 01).

## 5. Performance

- `App.OnStart` mínimo: tokens de estilo + `gblRol`/`gblUsuarioDV` (+2 LookUp delegables) — nada de colecciones.
- Cada pantalla llena SUS colecciones en `OnVisible` con `Concurrent()` donde haya 2+.
- La estación de transcripción NO re-consulta al marcar: `Patch` + `RemoveIf(colPend, ...)` local +
  reselección — 1 llamada de red por transcripción. Botón «Actualizar» explícito re-colecta.
- Joins horneados una vez por carga de colección (`AddColumns`), no `LookUp` por fila en galerías.

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

- [ ] Tabla `CM | TBL | Usuario App` + choice `Rol backoffice` creados en Dev (§3.1) — o decisión
      explícita de construir con el fallback `gblRol="Admin"`.
- [ ] Confirmar en Studio el plural exacto de los datasources al conectarlos (esperados:
      `'CM | TBL | Interaccions'`, `'CM | TBL | Asignacions'`, `'CM | TBL | Cuentas'`, `'CM | TBL | Clientes'`,
      `'CM | TBL | Agentes'`, `'CM | TBL | Pagos'`, `'CM | TBL | Telefonos'`, `'CM | TBL | Direccions'`,
      `'CM | TBL | Carga diarias'`, `'CM | TBL | Actualizacion de datos'`(⚠ plural raro — verificar), `'CM | TBL | Usuario Apps'`,
      `Users`). **Si IntelliSense difiere de la guía, el IntelliSense manda** — anotar y avisar.
- [ ] Límite de filas no delegables de la app = **2000**.
- [ ] Icono de la app (paso 3) subido.
