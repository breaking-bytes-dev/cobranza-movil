# Guía 13 — Plan de pruebas · App - CM - Backoffice Canvas

> **Mandato:** probar que la app funcione de 0 a 100. Lo que MÁS importa: **cero problemas de
> delegación** — por eso el Bloque D va primero, es el más grande y manda sobre todo lo demás.
> **Regla de oro:** ninguna prueba 🔴 puede quedar en falla, y **CUALQUIER subrayado azul de
> delegación en Studio es una 🔴 automática**, esté o no listado acá.
>
> Orden de ejecución: **0 → D → R → F → X**. Los checklists de salida de las guías 02–12 ya se
> corrieron al construir; esta guía NO los repite: prueba **escenarios end-to-end** y **escala real**.
> Verificación cruzada SIEMPRE contra la verdad del servidor (vista de tabla del portal / MDA de
> respaldo con conteo real), no contra lo que la canvas quiera mostrar.

---

## 0. Precondiciones (sin esto, la corrida no es válida)

### 0.1 Configuración de la app

- [ ] Configuración → General → **Límite de filas de consultas no delegables = 2000** (anotalo; no lo subas ni bajes durante la corrida).
- [ ] App publicada y compartida con los 6 usuarios de prueba (§0.3).
- [ ] Monitor de Power Apps accesible (Studio → Herramientas avanzadas → Monitor).

### 0.2 Datos mínimos en Dev (sembrar lo que falte y ANOTAR los conteos reales del portal)

- [ ] **Interacciones Pendientes de ≥ 2 localizadores distintos**, incluyendo: ≥ 1 con pago (💵 `Aplica pago` con 1–3 filas en Pagos) y ≥ 1 con actualización de datos (✏️ `Actualiza info` con fila en `Actualizacion de datos`, mezclando campos con "antes" y campos nuevos).
- [ ] **> 500 interacciones Pendientes en total** (para D-02; si el día real no llega, sembrá hasta superar 500). Anotá el conteo exacto del portal: `N-PEND = ______`.
- [ ] **Especiales en los 4 estados**: Pendiente aprobación · Aprobada (con agente) · Rechazada (con motivo) · Cerrada (`[AS] Estado = No`). Al menos una Aprobada con retiro > 0 y una sin retiro (REGLA-MONTO-CIE).
- [ ] **Diarias activas con `'Última carga'` vieja** (anteriores a la última carga) — ≥ 5 filas, y **> 500 Diarias activas totales** como denominador (D-06). Anotá: `N-DIA = ______` · `N-DESAP = ______`.
- [ ] **`'CM | TBL | Carga diarias'` con > 2000 filas** acumuladas (histórico de imports; D-04). Anotá fecha/hora de la última fila real: `T-CARGA = ______`.
- [ ] **Clientes suficientes para que la búsqueda pague** (> 2000 filas en `Clientes` y `Cuentas`). Identificá un **cliente centinela** cuyo nombre NO esté en las primeras 2000 filas alfabéticas (p. ej. inicial "Z" o "Y") y una **cuenta centinela** igual de lejana. Anotá: `CLI-Z = ______` · `CTA-Z = ______`.
- [ ] **≥ 1 agente activo SIN usuario vinculado** (`[AG] Usuario` y `Email Usuario` en blanco).
- [ ] Un **agente CON usuario** cuyo celular esté disponible para la prueba cruzada F10.4.

### 0.3 Usuarios de prueba — ROLES REALES (sin tabla `Usuario App`; 00 §3.1)

`gblRol` se deriva de los roles de seguridad `SR | CM | *` (directos o heredados por TEAM — en Dev
se reparten vía los teams `CM · <perfil> · DEV`). Preparar por MEMBRESÍA de team:

| Alias | Miembro de team(s) | gblRol esperado |
|---|---|---|
| **U-BO** | CM · Backoffice · DEV | `BackOffice` |
| **U-SOL** | CM · Solicitantes · DEV | `Solicitante` |
| **U-SUP** | CM · Supervisores · DEV | `Supervisor` |
| **U-ADM** | CM · Admin y Carga · DEV | `Admin` |
| **U-MIX** | Backoffice **y** Admin y Carga (2 teams) — o un rol directo + un team | `BackOffice\|Admin` |
| **U-SIN** | **ningún team CM y ningún rol `SR \| CM`** | vacío → "sin acceso" |

- [ ] Todos con el rol **"Usuario básico"** además (lectura de `Users` para el LookUp de identidad).
- [ ] U-MIX prueba TAMBIÉN la unión directo+team (asignale un rol directo y el otro por team).

---

## 1. BLOQUE D — DELEGACIÓN (el corazón del plan)

**El problema en una frase:** una fórmula no delegable trae las primeras 2000 filas, filtra ESO
en el cliente, y todo lo que está después **desaparece sin error ni aviso**. Con colas, arqueos y
maestros de miles de filas, un no-delegable oculto YA está escondiendo datos hoy.

### D-01 🔴 — Cero subrayados azules en las 11 pantallas (inventario estático)

**Pasos:** en Studio, abrí **App Checker → Delegación** (debe estar en CERO) y además recorré
pantalla por pantalla revisando a ojo las fórmulas calientes de esta tabla. Un solo subrayado
azul = parar, anotar la fórmula exacta y arreglar antes de seguir.

| # | Pantalla | Fórmulas a inspeccionar | OK |
|---|---|---|---|
| 0 | App (OnStart + Formulas) | LookUp `Users` · navegación N:N roles/teams (conjuntos de 1 usuario, sin riesgo) · `nfUltimaCarga` (Sort+First) | [ ] |
| 1 | scrInicio | OnVisible (pagos hoy, pendientes, transcritas, especiales, desap, agentes) + Valor de los 10 KPIs | [ ] |
| 2 | scrTranscripcion | OnVisible · btnActualizar · btnGrabar (Patch+LookUp) · galPagosTrx.Items · BLOQUE S (AD/tel/dir) | [ ] |
| 3 | scrTranscritas | OnVisible (choice+fecha+usuario) · btnActualizar · galPagosPanelTsc.Items | [ ] |
| 4 | scrMisSolicitudes | OnVisible (tipo+solicitante) · btnActualizar | [ ] |
| 5 | scrNuevaSolicitud | galCtaNva.Items (StartsWith, centinela < 2 caracteres) · anti-duplicado y Patch de btnEnviar | [ ] |
| 6 | scrAprobaciones | OnVisible (cola) · galAgeAprb.Items (StartsWith + Activo + Email <> Blank) · Patch aprobar/rechazar | [ ] |
| 7 | scrMonitoreo | OnVisible (carga hoy, cuentas, clientes) · btnActualizar | [ ] |
| 8 | scrReconciliacion | OnVisible (desap+denominador) · btnCerrarRec (ForAll+Patch) | [ ] |
| 9 | scrCierres | OnVisible (OR de choices+booleano) · btnCerrarCie | [ ] |
| 10 | scrAgentes | OnVisible (Sort) · galUsrAge.Items (Users, StartsWith) · Patch vincular (galUsrAge.OnSelect)/toggle | [ ] |
| 11 | scrMaestros | galClientesMae.Items (Sort/StartsWith) · OnSelect (tel/dir/ctas) · Patch editar/crear/anti-duplicado | [ ] |

**Esperado:** App Checker en cero y ninguna fórmula con banda azul. Cualquier hallazgo va a la bitácora como 🔴.

### D-02 🔴 — La cola de transcripción trae TODAS las pendientes (no 500)

1. Confirmá `N-PEND > 500` (§0.2) en el portal (vista de Interacciones filtrada por Estado grabación = Pendiente, conteo real).
2. Entrá como **U-BO** a scrTranscripcion.
3. Leé el chip de "Todos" en la columna de localizadores y el encabezado "PENDIENTES · n".

**Esperado:** el conteo de la app **= `N-PEND` exacto** (no 500, no un redondeo). El KPI "EN COLA" de scrInicio muestra el mismo número. [ ] OK [ ] Falla

### D-03 🔴 — Búsqueda de Maestros más allá de la página (StartsWith delega)

1. Entrá como **U-ADM** a scrMaestros.
2. Sin escribir nada, mirá la lista (ordenada por nombre — trae la primera página alfabética).
3. Escribí las primeras 3–4 letras de `CLI-Z` (el centinela que NO está en las primeras 2000 filas alfabéticas).

**Esperado:** `CLI-Z` aparece en los resultados — el `StartsWith` corrió EN EL SERVIDOR. Si no aparece, el filtro se ejecutó sobre la página local: 🔴 delegación rota. [ ] OK [ ] Falla

### D-04 🔴 — `nfUltimaCarga` correcta con > 2000 filas en Carga diarias

1. Confirmá que `'CM | TBL | Carga diarias'` supera 2000 filas y anotá `T-CARGA` (última fila real, portal ordenado por FechaCarga descendente).
2. Entrá como **U-ADM**: mirá el hero "CARGA DE HOY" de scrInicio y la card de estado de scrMonitoreo.

**Esperado:** ambas muestran exactamente `T-CARGA` (`dd/mm HH:mm`). El `Sort + First` delega: encuentra la última fila aunque esté fuera de cualquier página de 2000. [ ] OK [ ] Falla

### D-05 🔴 — El arqueo (`colTscTsc`) filtra fecha + usuario EN EL SERVIDOR

1. Asegurate de que el histórico de interacciones **Grabadas** supere 2000 filas (todo el histórico cuenta).
2. Como **U-BO**, transcribí 3 interacciones (podés reusar F2) y abrí scrTranscritas.
3. En el portal, contá las Grabadas de HOY por U-BO: `N-ARQ = ______`.

**Esperado:** el KPI "TRANSCRITAS" y las filas de la galería = `N-ARQ` exacto. Si el filtro (choice + `Fecha grabacion >= Today()` + `Grabado por`) no delegara, con > 2000 grabadas históricas el arqueo saldría vacío o incompleto. [ ] OK [ ] Falla

### D-06 🔴 — Reconciliación cuenta bien contra > 500 Diarias activas

1. Con `N-DIA > 500` y `N-DESAP` conocidos (§0.2), entrá como **U-ADM** a scrReconciliacion (después del import del día).
2. Leé el resumen "N de M Diarias activas (pct%)".

**Esperado:** N = `N-DESAP` y M = `N-DIA` exactos contra el portal; el % coincide con el cálculo a mano. Ambos filtros (desaparecidas y denominador) delegaron. [ ] OK [ ] Falla

### D-07 🔴 — El combo de cuentas de NuevaSolicitud encuentra cuentas fuera de la primera página

1. Entrá como **U-SOL** a scrNuevaSolicitud.
2. En el combo de cuenta escribí el inicio del número de `CTA-Z` (la cuenta centinela lejana).
3. Repetí buscando por el inicio del NOMBRE del cliente de esa cuenta.

**Esperado:** la cuenta aparece por las dos vías (número y nombre) — `StartsWith` delegó al servidor. Elegirla muestra el preview con saldo/mora correctos. [ ] OK [ ] Falla

### D-08 🔴 — Monitor: los Filter viajan como `$filter` OData, no como descarga completa

1. Abrí la app con Monitor conectado. Navegá: scrInicio → scrTranscripcion → scrTranscritas (U-BO) y scrReconciliacion → scrCierres (U-ADM).
2. Por cada llamada a Dataverse, inspeccioná la URL/petición.

**Esperado:**
- Toda consulta de cola/arqueo/desaparecidas lleva **`$filter`** con sus condiciones (choice por valor, fechas, usuario) y **`$select`** con columnas acotadas — el filtrado ocurre en el servidor.
- **Excepción declarada (no es falla):** los catálogos `colCtas*`/`colCli*`/`colAgentes*` viajan SIN `$filter` (ShowColumns de tabla completa, cota 2000 — 00 §4.1).
- **Bandera roja:** una cola u arqueo que baja la tabla entera y filtra en el cliente, o N llamadas idénticas por fila de galería (lookup anidado). [ ] OK [ ] Falla

### D-09 🔴 — Los KPIs de Inicio cuadran contra el portal (la colección no truncó)

1. En el portal/MDA anotá los conteos reales: Pendientes (`N-PEND`), Grabadas hoy por U-BO, Especiales pendientes de aprobación, Diarias fuera de cartera (`N-DESAP`), agentes activos sin usuario, y la suma de pagos de hoy por moneda.
2. Entrá a scrInicio con cada rol (U-BO, U-SOL, U-SUP, U-ADM) y compará KPI por KPI, más el "Cobrado hoy: C$ … · US$ …" del encabezado.

**Esperado:** todos los números **idénticos** a los del portal. Una diferencia = colección truncada o filtro no delegado: 🔴. [ ] OK [ ] Falla

### D-10 🟡 — La cota 2000: degradación DECLARADA, no bug (qué pasa exacto al superarla)

Comportamiento contractual de 00 §4.1 — verificalo si podés sembrar > 2000 pendientes; si no, dejalo documentado como límite aceptado:

1. Con > 2000 interacciones Pendientes, la cola de scrTranscripcion muestra **exactamente 2000** (las que entrega la primera página del filtro delegable) — sin error, sin banner.
2. El KPI "FILAS DE CARGA HOY" de scrMonitoreo, al clavar 2000, muestra el detalle **"⚠ cota 2000 alcanzada — conteo saturado"** — esa es la señal para el Admin.
3. Nada se rompe: transcribir sigue funcionando; al bajar de 2000, la cola vuelve a ser exacta.

**Esperado:** la app degrada EXACTAMENTE así. Cualquier otro síntoma (error, cero filas, conteo fantasma) sí es 🔴. [ ] OK [ ] Falla [ ] No ejecutado (documental)

---

## 2. BLOQUE R — ROLES Y ACCESO

### R-01 🔴 — Cada rol ve SOLO su navegación

Entrá con cada usuario y compará la nav lateral contra esta tabla (ni un ítem de más ni de menos):

| Usuario | Ítems esperados en la nav |
|---|---|
| U-BO | Inicio · Por transcribir · Transcritas hoy |
| U-SOL | Inicio · Nueva solicitud · Mis solicitudes |
| U-SUP | Inicio · Aprobaciones |
| U-ADM | Inicio · Monitoreo de carga · Reconciliación · Cierres · Agentes y usuarios · Datos maestros |

En scrInicio, cada uno ve SOLO sus KPIs y sus cards de acceso. [ ] OK [ ] Falla

### R-02 🔴 — Navegación forzada: el guard rebota

1. En Studio (vista previa) con la sesión de un tester, forzá desde la barra de fórmulas: `Set(gblRol, "Solicitante")` y luego `Navigate(scrTranscripcion)`.
2. Repetí la matriz mínima: Solicitante→scrCierres · Backoffice→scrAprobaciones · Supervisor→scrMaestros · Admin→scrTranscripcion (esta última debe rebotar: Admin NO es Backoffice).

**Esperado:** cada intento rebota a scrInicio en la primera línea del OnVisible, **sin parpadeo de datos** (Monitor: cero ClearCollect de la pantalla prohibida). [ ] OK [ ] Falla

### R-03 🔴 — Usuario SIN fila: pantalla sin acceso y nada más alcanzable

1. Entrá con **U-SIN** (sin ningún rol `SR | CM` ni team CM).
2. Observá scrInicio y abrí el Monitor.

**Esperado:** solo el estado 🔐 "Sin acceso" centrado — sin nav, sin encabezado, sin hamburguesa, sin KPIs. El OnVisible no dispara colecciones de rol. No existe ningún camino de UI hacia otra pantalla. [ ] OK [ ] Falla

### R-04 🔴 — Usuario MULTI-ROL: secciones apiladas y nav combinada

1. Entrá con **U-MIX** (Backoffice + Admin).
2. En scrInicio: los KPIs Backoffice (EN COLA, TRANSCRITAS HOY, CON PAGO) arriba y los Admin (CARGA DE HOY, DIARIAS FUERA DE CARTERA, AGENTES SIN USUARIO) apilados debajo, sin superponerse; las cards de acceso de AMBOS roles.
3. La nav muestra la UNIÓN: Inicio + los 2 ítems Backoffice + los 5 Admin.
4. Entrá a scrTranscripcion Y a scrMaestros.

**Esperado:** ambas pantallas abren sin rebote (guards con `in` sobre `"Backoffice|Admin"`); el pie de la nav muestra el rol combinado. [ ] OK [ ] Falla

### R-05 🔴 — Desactivar la fila lo saca en el próximo inicio de sesión

1. En el portal, poné `[UA] Activo = No` a la fila de U-BO.
2. Cerrá la app POR COMPLETO y volvé a abrirla con U-BO.
3. Restaurá `Activo = Sí` al terminar.

**Esperado:** al reabrir, U-BO cae en "Sin acceso" (gblRol en blanco). La sesión ya abierta puede seguir viva hasta reabrir — aceptado: el corte es al próximo OnStart. [ ] OK [ ] Falla

---

## 3. BLOQUES F1–F11 — FLUJOS END-TO-END POR PANTALLA

### F1 🟡 — scrInicio: el tablero por rol

1. Con U-ADM ANTES del import del día: hero "CARGA DE HOY" = "sin carga". 2. Después del import: fecha `dd/mm HH:mm`. 3. Con cada rol, clic en cada card de acceso navega a su pantalla. 4. "Cobrado hoy" omite la moneda en cero (`C$ 0.00` si no hay pagos).
**Esperado:** todo lo anterior, con los números ya validados en D-09. [ ] OK [ ] Falla

### F2 🔴 — scrTranscripcion: la ráfaga completa (marcar y seguir)

1. Como U-BO, clic en un localizador con ≥ 3 pendientes: la cola filtra FIFO (la más vieja arriba) y auto-selecciona la primera; la fila queda teal.
2. Escribí un código (p. ej. `pag01`) y **Enter**. **Esperado:** marca (1 solo Patch en Monitor), Notify "✓ N grabada — siguiente: <cliente>", avanza al siguiente en la MISMA posición, input limpio, foco de vuelta en el input sin clic.
3. Repetí 2 veces más de corrido (solo teclado): la ráfaga fluye sin tocar el mouse.
4. En Dataverse: las 3 interacciones quedaron `Grabado`, con `Codigo Grabacion` en MAYÚSCULAS, `Fecha grabacion` de ahora y `Grabado por` = U-BO.
5. "Saltar →" avanza circular SIN escribir en Dataverse (Monitor: cero llamadas).
6. Seleccioná una interacción con 💵: muestra sus pagos con símbolo por moneda y "Recibo N". Una con ✏️: bloque violeta con tachado → nuevo y chip NUEVO donde no había dato previo.
7. Vaciá la cola de un localizador: 🎉 "Cola vacía" y el localizador desaparece de la columna.
[ ] OK [ ] Falla

### F3 🔴 — scrTranscritas: arqueo e inmutabilidad

1. Como U-BO, abrí scrTranscritas: las 3 transcripciones de F2 están arriba (orden por fecha de grabación descendente); KPIs cuadran (conteo y montos `Moneda()`).
2. Abrí el panel de una con pago: 100 % lectura — código con 🔒, "· por vos", nota de inmutabilidad, **ningún input** en todo el panel.
3. Entrá con OTRO usuario Backoffice (o U-MIX): su arqueo NO muestra las transcripciones de U-BO.
[ ] OK [ ] Falla

### F4 🔴 — scrNuevaSolicitud: crear la Especial

1. Como U-SOL: el hint arranca "Falta: cuenta · monto · motivo" (ámbar) y va limpiándose; motivo con solo espacios NO habilita; solo una moneda > 0 alcanza.
2. Enviá sobre una cuenta CON Especial activa: Notify ámbar y NO crea registro (verificá en el portal que no apareció fila).
3. Enviá una válida sobre otra cuenta. **Esperado:** navega a Mis solicitudes con la solicitud visible; en Dataverse la fila tiene Tipo=Especial, Estado=activo, `Pendiente aprobacion`, Notas con prefijo `"<tipo> — "`, `Solicitado por` = U-SOL y **SIN agente**.
[ ] OK [ ] Falla

### F5 🟡 — scrMisSolicitudes: seguimiento por estado

1. Como U-SOL: la lista muestra SOLO lo suyo, la más reciente arriba; chips: Pendiente (ámbar) · Aprobada (verde) · Rechazada (roja) · Cerrada (neutra, gana sobre el estado de aprobación); chip 🏍️ solo con agente.
2. Panel de una Rechazada: card roja con el motivo. Panel de una Aprobada: fila Localizador.
[ ] OK [ ] Falla

### F6 🔴 — scrAprobaciones: aprobar (con Comp-14) y rechazar

1. Como U-SUP: la solicitud de F4 está en la cola (FIFO, la más vieja arriba). Abrila: cuenta, retiro, notas completas.
2. "✓ Aprobar y asignar" deshabilitado sin localizador; el combo solo ofrece agentes activos CON `Email Usuario`.
3. Elegí agente y aprobá. **Esperado inmediato:** fila fuera de la cola sin recargar, Notify verde; en Dataverse: `Aprobado` + `[AS] Agente` + `[AS] Resolutor` = U-SUP.
4. **Verificación Comp-14 🔴:** esperá 1–2 min y revisá en Dataverse el **Owner (Propietario)** del registro: cambió al usuario del agente asignado. Sin ese cambio no hay aislamiento ni sync al celular — es falla del flow, pero BLOQUEA el cierre igual.
5. Con otra solicitud: "Rechazar" deshabilitado sin motivo (espacios no cuentan); rechazá con motivo. **Esperado:** `Rechazada` + `Motivo de rechazo` + `Resolutor` en Dataverse; U-SOL la ve roja con el motivo en F5.
[ ] OK [ ] Falla

### F7 🟡 — scrMonitoreo: ¿llegó el archivo?

1. Como U-ADM con carga de hoy: card verde "✓ Carga de hoy completada · HH:mm" (24 h) + 3 conteos con separador de miles que cuadran entre card y KPIs.
2. Simulá "sin carga" (`Set(varHayCargaMon, false)` en la barra de fórmulas de Studio): card roja con última carga y la línea de Smelter.
3. "↻ Actualizar" = 3 llamadas en Monitor + Notify.
[ ] OK [ ] Falla

### F8 🔴 — scrReconciliacion: umbral y cierre en lote

1. Como U-ADM ANTES del import (o simulá `nfUltimaCarga` de ayer): card ⛔ visible y casilla + botón **Disabled** aunque el % sea bajo.
2. Después del import, con `N-DESAP` reales: resumen "N de M (pct%)", meter proporcional; marcá la casilla → botón peligro habilitado.
3. **Simulá % > 30** (en Studio achicá el denominador: `Set(varPctRec, 45); Set(varRojoRec, true)`): borde, leyenda y meter en ROJO, casilla Disabled → el botón NO se puede armar. Es el freno anti archivo-truncado.
4. Restaurá el estado real (< 30 %) y cerrá en lote. **Esperado:** Notify con el conteo, lista vacía → "✨ Cartera cuadrada", card de acción desaparece; en Dataverse TODAS las filas del lote: `Estado = No`, `Motivo = "fuera de cartera"`, `Fecha de cierre` de hoy.
5. Volvé a entrar: la casilla arranca desmarcada.
[ ] OK [ ] Falla

### F9 🔴 — scrCierres: cerrar por tipo con motivo

1. Como U-ADM: tabs con conteos correctos; alternar tabs NO genera llamadas (Monitor).
2. Fila Especial con retiro muestra el monto del RETIRO; sin retiro y toda Incobrable, el saldo vencido (REGLA-MONTO-CIE).
3. Abrí una Incobrable: motivos `Recuperada / Baja / Fuera de cartera`; una Especial: `Gestión completada / Ya no aplica / Solicitud errónea`.
4. Botón deshabilitado sin motivo; cerrá una con motivo + detalle. **Esperado:** 1 Patch (Monitor), fila fuera de la lista sin recargar; en Dataverse `Estado = No`, `Fecha de cierre` sellada y `Motivo` = `"Motivo — detalle"`.
5. Abrí OTRA fila: motivo/detalle llegan limpios (no arrastra el cierre anterior).
[ ] OK [ ] Falla

### F10 🔴 — scrAgentes: vincular usuario (las DOS columnas) y prueba cruzada

1. Como U-ADM: la card ⚠ lista los agentes activos sin usuario (§0.2).
2. Vinculá el agente de prueba a su usuario de Entra. **Esperado:** Notify "✓ … vinculado", chip "✓ Vinculado"; en Dataverse quedaron escritas **LAS DOS columnas**: `[AG] Usuario` (lookup) **y** `Email Usuario` (texto en minúsculas). Una sola de las dos = 🔴 (estado inconsistente: entra pero no se aísla, o al revés).
3. Al vincular el último sin usuario, la card ⚠ desaparece sola.
4. **Prueba cruzada 🔴:** con el celular del agente, entrá a la **app del localizador**: el login por `Email Usuario` funciona y ve su cartera.
5. Borrá la selección del combo: "Vínculo quitado" y AMBAS columnas en blanco. Rehacé el vínculo. 6. Desactivar/Activar alterna chip y registro en Dataverse.
[ ] OK [ ] Falla

### F11 🔴 — scrMaestros: crear / editar / agregar con anti-duplicado de CIF

1. Como U-ADM: búsqueda vacía lista por nombre y pagina al scrollear; buscar por CIF y por inicio de nombre filtra en vivo (la escala ya se probó en D-03).
2. Ficha de un cliente: KV + teléfonos + direcciones + cuentas; las cuentas SIN ningún control de edición (LED-08).
3. "✏️ Editar": precarga los 4 campos, **no existe input de CIF**; guardá un cambio → la ficha vuelve con el dato nuevo (y en Dataverse).
4. Agregá un teléfono y una dirección: aparecen en las galerías del modo ver; en Dataverse el hijo tiene lookup Cliente + CIF denormalizado.
5. "＋ Nuevo cliente" con un **CIF EXISTENTE**: Notify warning "Ya existe…" y NO crea (verificá que no hay fila nueva).
6. Creá uno con CIF nuevo + teléfonos/direcciones llenos. **Esperado:** cliente + hijos creados con `[TL]/[DR] CIF` y lookup seteados; aparece al buscarlo.
[ ] OK [ ] Falla

---

## 4. BLOQUE X — RESPONSIVE Y ERRORES

### X-01 🔴 — < 920 px en las 11 pantallas

Achicá la ventana por debajo de 920 px (o probá en tableta vertical) y recorré TODAS las
pantallas con el usuario de su rol. En cada una: la nav colapsa a **hamburguesa** que abre el
drawer flotante POR ENCIMA del contenido, y nada queda cortado ni superpuesto.

| Pantalla | Además verificar | OK |
|---|---|---|
| scrInicio | KPIs en 2 columnas, cards apiladas a ancho completo | [ ] |
| scrTranscripcion | orden móvil: **localizadores (tira horizontal) → detalle → cola** (LED-18) | [ ] |
| scrTranscritas | KPIs apilados, comentario oculto en fila, panel a pantalla completa | [ ] |
| scrMisSolicitudes | filas altas apiladas, panel full-width | [ ] |
| scrNuevaSolicitud | NIO/USD apilados, card a ancho completo | [ ] |
| scrAprobaciones | columna de notas oculta, panel full-width | [ ] |
| scrMonitoreo | KPIs en 1 columna | [ ] |
| scrReconciliacion | cards a ancho completo | [ ] |
| scrCierres | tabs usables, panel full-width | [ ] |
| scrAgentes | filas apiladas de 148, combo a ancho completo | [ ] |
| scrMaestros | panel full-width en los 3 modos | [ ] |

[ ] OK [ ] Falla

### X-02 🔴 — El error NATIVO se ve (decisión: sin App.OnError)

La app **no define App.OnError** a propósito (01 §4): el banner nativo de Power Apps debe mostrar
el error CRUDO para que el usuario lo lea y lo reporte. Probalo con un fallo REAL:

1. En el portal, quitá temporalmente el privilegio de **Escritura sobre `interaccion`** al rol Dataverse del tester Backoffice (o un privilegio equivalente de otra tabla/rol).
2. Como U-BO, intentá marcar una transcripción (código + Enter).
3. **Esperado:** aparece el **banner nativo** de Power Apps con el mensaje de error de Dataverse (permiso denegado) **visible y legible** — no un Notify genérico, no un silencio. La app no queda rota: cerrando el banner se puede seguir navegando.
4. Verificá en Dataverse que la interacción NO cambió de estado (el Patch falló limpio).
5. **Restaurá el privilegio** y volvé a marcar: ahora funciona.
[ ] OK [ ] Falla

---

## 5. Resumen de resultados y regla de cierre

| Caso | Qué prueba | Criticidad | Resultado |
|---|---|:---:|:---:|
| D-01 | Cero subrayados azules (11 pantallas + App) | 🔴 | [ ] OK [ ] ✗ |
| D-02 | Cola trae TODAS las pendientes (> 500) | 🔴 | [ ] OK [ ] ✗ |
| D-03 | Búsqueda Maestros fuera de la página (StartsWith) | 🔴 | [ ] OK [ ] ✗ |
| D-04 | nfUltimaCarga con > 2000 cargas (Sort+First) | 🔴 | [ ] OK [ ] ✗ |
| D-05 | Arqueo filtra fecha+usuario en servidor | 🔴 | [ ] OK [ ] ✗ |
| D-06 | Reconciliación cuenta contra > 500 Diarias | 🔴 | [ ] OK [ ] ✗ |
| D-07 | Combo de cuentas fuera de la primera página | 🔴 | [ ] OK [ ] ✗ |
| D-08 | Monitor: $filter OData, no descarga completa | 🔴 | [ ] OK [ ] ✗ |
| D-09 | KPIs de Inicio cuadran contra el portal | 🔴 | [ ] OK [ ] ✗ |
| D-10 | Cota 2000: degradación declarada | 🟡 | [ ] OK [ ] ✗ [ ] N/E |
| R-01 | Nav por rol (solo lo suyo) | 🔴 | [ ] OK [ ] ✗ |
| R-02 | Guard rebota navegación forzada | 🔴 | [ ] OK [ ] ✗ |
| R-03 | Sin fila → sin acceso total | 🔴 | [ ] OK [ ] ✗ |
| R-04 | Multi-rol: apilado + nav combinada | 🔴 | [ ] OK [ ] ✗ |
| R-05 | Activo=No saca al reabrir | 🔴 | [ ] OK [ ] ✗ |
| F1 | Inicio por rol | 🟡 | [ ] OK [ ] ✗ |
| F2 | Ráfaga de transcripción completa | 🔴 | [ ] OK [ ] ✗ |
| F3 | Arqueo inmutable | 🔴 | [ ] OK [ ] ✗ |
| F4 | Crear solicitud (validación + anti-duplicado) | 🔴 | [ ] OK [ ] ✗ |
| F5 | Mis solicitudes por estado | 🟡 | [ ] OK [ ] ✗ |
| F6 | Aprobar/rechazar + owner por Comp-14 | 🔴 | [ ] OK [ ] ✗ |
| F7 | Monitoreo de carga | 🟡 | [ ] OK [ ] ✗ |
| F8 | Reconciliación: umbral + cierre en lote | 🔴 | [ ] OK [ ] ✗ |
| F9 | Cierres por tipo con motivo | 🔴 | [ ] OK [ ] ✗ |
| F10 | Vincular agente (2 columnas + prueba cruzada) | 🔴 | [ ] OK [ ] ✗ |
| F11 | Maestros: crear/editar/agregar + CIF único | 🔴 | [ ] OK [ ] ✗ |
| X-01 | Responsive < 920 px (11 pantallas) | 🔴 | [ ] OK [ ] ✗ |
| X-02 | Error nativo visible (sin App.OnError) | 🔴 | [ ] OK [ ] ✗ |

**Regla de cierre:**

- La app se acepta SOLO con **todas las 🔴 en OK**. Una sola 🔴 en falla → se arregla, se re-corre
  ESE caso (y D-01 de nuevo si el fix tocó fórmulas), y recién ahí se firma.
- **CUALQUIER subrayado azul de delegación** encontrado en cualquier momento de la corrida —
  aunque el caso donde apareció sea 🟡 — se registra como **🔴 automática**.
- Las 🟡 en falla se juntan para una pasada de fixes; no bloquean el cierre pero sí se documentan.
- D-10 puede quedar "No ejecutado" solo si no se pudo sembrar > 2000 pendientes; se deja constancia.

**Veredicto:** [ ] **ACEPTADA** (todas las 🔴 en OK) · [ ] **Necesita arreglos** (bitácora abajo)

### Bitácora de fallas

| # | Caso | Usuario | Pantalla | Qué pasó (fórmula/captura si hay) | Severidad |
|---|---|---|---|---|:---:|
| 1 | | | | | 🔴 / 🟡 |
| 2 | | | | | |
| 3 | | | | | |
