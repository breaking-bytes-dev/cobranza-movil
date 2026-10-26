# Registro de Decisiones (ADR) — Cobranza Móvil

> Bitácora de cada decisión de diseño, con contexto y porqué. Fuente de verdad de "por qué hicimos las
> cosas así". Formato: una entrada por decisión. No se borran; si una decisión se revierte, se agrega una
> nueva entrada que la reemplaza (estado `Reemplazada por DEC-xxx`).

---

## DEC-001 — La app de campo es offline-first

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Los localizadores cobran en moto, en zonas donde el celular puede quedarse sin datos.
- **Decisión:** La app de campo debe **funcionar sin conexión** (registrar gestiones y pagos offline) y
  **sincronizar al recuperar señal**. Se construye en **Canvas con offline-first nativo** (perfil offline +
  store local + sync), no online-only.
- **Porqué:** Un cobrador bloqueado por falta de señal en plena calle vuelve al papelito. El offline es
  requisito de negocio, no un lujo.
- **Implicaciones:**
  - Define el esqueleto entero de la app de campo (no se puede "agregar offline después" sin reescribir).
  - Hay que definir qué tablas entran al perfil offline y la resolución de conflictos.
  - Habilita el problema de la **numeración de recibo offline** (ver DEC-003).
- **Alternativas descartadas:** *Online-only* (más simple, pero deja al cobrador bloqueado sin señal).
  *Híbrido/buffer mínimo* (no garantiza el flujo completo en campo).

---

## DEC-002 — Write-back al core: manual asistido; el código lo asigna el BACKOFFICE (no la gestión)

- **Estado:** Aceptada (revisada) · 2026-06-21
- **Contexto:** El software de cobro del banco graba interacciones con **códigos propios**. La interacción
  que captura el localizador es un **comentario en texto libre** (ej. *"el cliente no contesta el teléfono"*);
  **NO trae código**. El **backoffice** conoce el código del core y se lo asigna al transcribir.
- **Decisión:** El backoffice **transcribe manualmente** al core las interacciones que el localizador
  registró en la app:
  1. El localizador captura la interacción como **comentario libre** (sin código).
  2. El backoffice la graba en el core y registra en la interacción: el **código del core que asignó**, la
     **fecha** y **qué backoffice** la grabó.
  3. **Estado de transcripción** (`Pendiente` → `Grabada en core`) para que **otro backoffice no la vuelva a
     cargar** (anti-doble-carga).
  4. El código asignado **se guarda** para, a futuro, **entrenar un modelo que auto-clasifique** las
     interacciones y reduzca el trabajo del backoffice.
- **Porqué:** Refleja el proceso real (el código es conocimiento del backoffice, no del localizador) y
  siembra datos limpios para automatización futura. El core no tiene API de ingesta en esta fase.
- **Implicaciones:**
  - La gestión **NO nace con código**. Se **elimina** el catálogo *"tipogestion que espeja códigos del core"*
    como mecanismo de captura.
  - Campos en la gestión: `sanic_cm_in_codigograbacion` (asignado por backoffice), `sanic_cm_in_fechagrabacion`, `sanic_cm_in_grabadopor` (user),
    estado de transcripción.
  - Vista de backoffice "gestiones pendientes de grabar".
- **Reemplaza** el entendimiento previo (el código NO nace con la gestión).
- **Alternativas descartadas:** *Write-back automatizado* (no hay API de ingesta al core aún).

---

## DEC-003 — Pago en papel por ahora; recibo digital como feature on/off futuro

- **Estado:** Aceptada (revisada) · 2026-06-21
- **Contexto:** Hoy el cliente recibe un **recibo FÍSICO** (talonario) al pagar, y **muchos clientes aún
  prefieren el papel**. Los pagos pueden ser en **NIO o USD**.
- **Decisión:** Por ahora el recibo sigue siendo **físico**. La **interacción** registra el pago como
  atributos: **¿aplica pago?** (boolean), **monto**, **moneda** (NIO/USD) y el **número del recibo físico**
  que el localizador anota. El **recibo digital** se diseña como una funcionalidad **encendible/apagable a
  demanda** (por cliente que lo acepte), en una fase posterior.
- **Porqué:** Respeta la realidad operativa (talonario en mano) y la preferencia del cliente por el papel,
  sin cerrar la puerta al recibo digital.
- **Implicaciones:**
  - El pago se modela como **entidad separada** `sanic_cm_tbl_pago` (ver **DEC-010**, que reemplaza el "pago =
    atributos" que decía originalmente esta decisión).
  - **Desaparece la urgencia** de numeración offline del recibo (el número lo da el talonario físico).
  - La numeración anti-colisión (DEC-007) queda relevante **solo** para el recibo digital futuro.
  - Requisitos legales/fiscales: los **básicos** cuando se active el digital (P-5).
- **Reemplaza** el entendimiento previo (recibo digital inmediato).
- **Alternativas descartadas:** *Recibo digital ya* (los clientes aún prefieren papel).

---

## DEC-004 — Localizadores como empleados internos (Entra ID / AAD)

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Definir identidad y licenciamiento de los localizadores.
- **Decisión:** Los localizadores son **empleados internos** con cuenta **Entra ID (AAD)**. Usan **Canvas
  app con licencia Power Apps**. La seguridad es **row-level** (cada localizador ve solo su cartera) vía
  ownership + Business Units / Teams.
- **Porqué:** Es el camino más directo y seguro: identidad corporativa nativa, sin portal externo
  (Power Pages) ni manejo de invitados.
- **Implicaciones:**
  - Cada localizador = un usuario AAD vinculado a un registro `sanic_cm_tbl_agente`.
  - Modelo de seguridad Dataverse por ownership/BU.
  - Costo de licenciamiento Power Apps por localizador (a dimensionar).
- **Alternativas descartadas:** *Contratistas externos / Power Pages* (más complejo en identidad y costo).

---

## DEC-005 — Tabla de asignación diaria dedicada

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Se requiere **trazabilidad total** de "qué cuenta se asignó a qué localizador en qué día",
  y el localizador necesita **ordenar/priorizar** su cartera del día (P-2).
- **Decisión propuesta:** Crear `sanic_cm_tbl_asignacion` (cuenta, localizador, fecha, orden/prioridad, estado) en
  la **capa operativa**, en vez de reusar `sanic_cm_tbl_cargadiaria`.
- **Porqué:**
  - `cargadiaria` es un **snapshot financiero** (montos, mora) cuya alt key (`numerocuenta+fechacarga`) se
    **pisa por upsert** en cada recarga del CSV. Si guardáramos ahí el orden del localizador, **una recarga
    del día lo borraría**.
  - Separar responsabilidades (snapshot financiero ≠ asignación operativa) mantiene el modelo limpio y
    "gritando" su propósito.
  - Da **transparencia directa** de quién tiene qué asignado.
- **Tradeoff:** hay que **poblar** `sanic_cm_tbl_asignacion` desde el import (un table mapping/flow adicional).
  Beneficio (capa operativa limpia y no-pisable) > costo.
- **Alternativa:** reusar `cargadiaria` (cero trabajo extra) pero mezcla responsabilidades y el orden manual
  se pierde en recargas.

---

## DEC-006 — El "Agente" del import ES el localizador (vinculado a systemuser)

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** P-1. El "Agente" del CSV es la misma persona que cobra en campo.
- **Decisión:** No se crea una entidad localizador separada. Se agrega a `sanic_cm_tbl_agente` una **columna lookup
  a `systemuser`** (usuario AAD del ambiente), ligando el agente con su identidad AAD.
- **Porqué:** Evita duplicar entidades y **habilita la seguridad row-level**: el localizador ve su cartera
  porque su user AAD = el agente de la cuenta.
- **Implicaciones:**
  - La seguridad de la app de campo se ancla en `sanic_cm_tbl_agente` ↔ `systemuser`.
  - El import sigue resolviendo el agente **por nombre**; el vínculo a `systemuser` lo setea admin (o un
    proceso) **una vez por localizador**.

---

## DEC-007 — Numeración anti-colisión por prefijo de localizador (PROPUESTA, para recibo digital futuro)

- **Estado:** Propuesta · 2026-06-21
- **Contexto:** P-6. Cuando se active el **recibo digital** (DEC-003), dos dispositivos **offline** no pueden
  generar el mismo número sin coordinarse con el servidor.
- **Decisión propuesta:** Numerar **libre de colisiones por construcción**, sin coordinación:
  `{codigoLocalizador}-{yyyymmdd}-{secuenciaLocal}`. Cada localizador tiene **código único** y trabaja su
  **propia cartera**, así que el **prefijo garantiza unicidad global**; la secuencia local solo debe ser
  única dentro de `(localizador, día)` y la lleva el store local del dispositivo.
- **Porqué:** El offline-first exige numeración **sin coordinación con el servidor**; el prefijo por
  localizador lo logra por diseño.
- **Tradeoff vs rangos pre-asignados:** los rangos (el servidor entrega bloques) **requieren conectividad**
  para pedir el bloque → contradice offline-first. El prefijo no necesita nada.
- **Nota:** **No bloqueante hoy** (recibo en papel); se activa con el recibo digital.

---

## DEC-008 — Capturar geo-ubicación (lat/long) en cada interacción

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Trazabilidad de campo: saber **dónde** se hizo cada gestión.
- **Decisión:** Cada interacción guarda **latitud/longitud** capturadas por el dispositivo al registrarla.
- **Porqué:** El celular ya tiene la ubicación (costo bajo), habilita **auditar visitas reales** y mapear la
  actividad de cobro. Funciona offline (se captura local y viaja en el sync).
- **Implicaciones:** Dos columnas (`sanic_cm_in_latitud`, `sanic_cm_in_longitud`) en `sanic_cm_tbl_interaccion`. Considerar **aviso/consentimiento** de
  captura de ubicación al empleado (política interna).

---

## DEC-009 — La tabla de captura se llama `sanic_cm_tbl_interaccion` (no `sanic_gestion`)

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** El negocio habla de **"interacción"**, no de "gestión".
- **Decisión:** La entidad núcleo de captura se llama **`sanic_cm_tbl_interaccion`**.
- **Porqué:** La tabla debe **gritar** su propósito en el lenguaje del negocio (screaming architecture).
- **Implicaciones:** Renombre en todo el modelo y los docs.

---

## DEC-010 — El pago es una entidad separada `sanic_cm_tbl_pago` (1:N con la interacción)

- **Estado:** Aceptada · 2026-06-21 *(reemplaza el "pago = atributos" de DEC-003)*
- **Contexto:** Un cliente puede pagar **parte en NIO y parte en USD** en una misma visita, y el pago
  merece historia propia.
- **Decisión:** `sanic_cm_tbl_pago` como **entidad separada**, relación **1:N** con `sanic_cm_tbl_interaccion` (una
  interacción puede tener **varios** pagos). Campos: monto, **moneda (NIO/USD)**, número de recibo físico,
  fecha.
- **Porqué:** El pago mixto multimoneda **solo** se modela limpio con una entidad hija. Los atributos en la
  interacción no lo permitían.
- **Implicaciones:** La interacción mantiene un booleano **"¿aplica pago?"** (lo marca el localizador); los
  montos viven en `sanic_cm_tbl_pago`. Recibo sigue siendo **físico** (DEC-003).

---

## DEC-011 — Gamificación: estadísticas calculadas, sin metas todavía

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Por ahora se quiere **estadística + competencia + visibilidad del trabajo**, no objetivos.
- **Decisión:** **No hay tabla de metas** por ahora. Las estadísticas se **calculan** (agregaciones de
  interacciones/pagos): **plata recuperada por moneda + dolarizada total**, **nº de interacciones**, y otras
  métricas. Diseño **expandible** a objetivos en una fase futura.
- **Porqué:** Entregar valor temprano (ranking/visibilidad) sin el peso de un módulo de objetivos.
- **Implicaciones:** Vistas/consultas de agregación; nada que mantener manualmente. La conversión a USD usa
  el `sanic_cm_ct_totalsaldovencido`/lógica de dolarización ya presente en los datos.

---

## DEC-012 — `sanic_cm_tbl_asignacion`: 3 tipos, ciclo de vida y política de import

- **Estado:** Aceptada · 2026-06-21 *(expande DEC-005)*
- **Contexto:** Las asignaciones son de 3 tipos con comportamientos muy distintos: **Diaria**, **Incobrable**,
  **Especial**.
- **Decisión:** `sanic_cm_tbl_asignacion` es una **tabla operativa viva** (con `sanic_cm_as_tipo` y `sanic_cm_as_estado` activo/cerrada),
  **no** un snapshot diario.
  - **Diaria:** el import diario la renueva.
  - **Incobrable:** persiste semanas con el mismo agente, **no viene en el CSV diario**; el import **NO la
    borra** por no verla; se **cierra manualmente** (backoffice/supervisor) con motivo.
  - **Especial:** nace por **flujo en la app** (DEC-013), no por CSV.
  - **Política de import:** corre **1 vez al día** (mañana). La **reasignación manual** posterior se
    **respeta hasta el próximo import**; al día siguiente el CSV es la nueva foto **solo para las Diarias**.
    El import **no pisa** Incobrables ni Especiales.
- **Porqué:** Cada tipo tiene su ciclo; tratar a todas como snapshot diario rompería las Incobrables y las
  reasignaciones manuales.
- **Implicaciones:** `sanic_cm_tbl_asignacion` con `sanic_cm_as_tipo`, `sanic_cm_as_estado`, fecha, agente, cuenta (requerida), orden del
  localizador, motivo de cierre. El import puebla Diaria/Incobrable; nunca borra.

---

## DEC-013 — Flujo de asignación Especial + el Solicitante escribe datos maestros

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Las asignaciones Especiales son pedidos puntuales que **normalmente no vienen por CSV**
  (si vinieran, se crean como `Pendiente` → ver **DEC-022**).
- **Decisión:** Nacen en la app de oficina, creadas por el rol **Solicitante** (DEC-014):
  1. El **Solicitante** (con permiso) **selecciona o CREA** el cliente, y **edita** sus datos maestros si
     hace falta (teléfonos, direcciones, etc.).
  2. Escribe la asignación especial (**siempre con número de cuenta**).
  3. Va a aprobación del **Supervisor** (DEC-014), que **aprueba/rechaza** y **asigna el localizador**.
  - **Estados:** `Borrador` → `Pendiente aprobación` → `Aprobada (asignada)` / `Rechazada (con motivo)`.
- **Porqué:** Canal controlado para encargos especiales con **segregación de funciones**: el Solicitante pide,
  el Supervisor aprueba y asigna. Ninguno es el Backoffice por defecto.
- **Implicaciones — IMPORTANTE:** las tablas del import (`cliente`, `telefono`, `direccion`, `cuenta`)
  **dejan de ser solo-lectura**: el **Solicitante** las **crea/edita**. Cuidar la **colisión de CIF** (un
  cliente creado a mano con CIF que luego llega por CSV se **fusiona** por upsert — bien si el CIF es
  correcto; duplica si está mal). El **Supervisor**, no el Solicitante, define el localizador final.

---

## DEC-014 — Roles de seguridad del sistema (5 roles)

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Hay que fijar el mapa de roles. **Quien pide** una asignación especial, **quien la aprueba**
  y **quien transcribe** al core son responsabilidades **distintas**.
- **Decisión:** **Cinco roles**:
  - **Localizador** — app **Canvas** (offline). Ve **solo su cartera**. Registra interacciones y pagos.
  - **Backoffice** — app **Model-driven**. Revisa interacciones y **transcribe al core** (asigna código,
    marca `Grabada en core`).
  - **Solicitante** — app **Model-driven**. **Crea** asignaciones **Especiales** (selecciona/crea cliente,
    **edita datos maestros**) y las envía a aprobación. **No** es necesariamente un Backoffice.
  - **Supervisor de localizadores** — app **Model-driven**. **Aprueba/rechaza** las Especiales y **asigna**
    el localizador.
  - **Admin** — app **Model-driven**. Dispara/monitorea el import, gestiona catálogos y configuración, ve todo.
- **Porqué:** Separación de responsabilidades. **Solicitante ≠ Backoffice ≠ Supervisor**: pedir, transcribir
  y aprobar son controles independientes (segregación de funciones).
- **Implicaciones:** La app Model-driven se diferencia por **roles de seguridad** (no por apps separadas).
  Seguridad row-level para el localizador (DEC-006). Un mismo usuario podría tener más de un rol si el
  negocio lo permite, pero **por defecto van separados**.

---

## DEC-015 — Seguridad row-level: estrategia "Moderado"

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** Cada localizador debe ver "solo su cartera", pero un `cliente` (1:N con `cuenta`) puede
  tener cuentas en **carteras distintas** → no puede tener un owner único. Hay que decidir el nivel de
  aislamiento de los **datos maestros**.
- **Decisión:** Estrategia **Moderado**:
  - **Tablas operativas** (`asignacion`, `interaccion`, `pago`): propiedad **User/Team**, acceso **User-level**
    → cada localizador ve y gestiona **solo lo suyo**. (Aislamiento real de la cartera.)
  - **Datos maestros** (`cliente`, `cuenta`, `direccion`, `telefono`, catálogos): **Read a nivel
    Organización** → el localizador **lee todo el padrón**, pero la **app filtra** su cartera.
  - El **owner de la asignación** lo setea el import vía el vínculo `agente → systemuser` (DEC-006); en las
    Especiales, lo setea el Supervisor al asignar el localizador.
- **Porqué:** Son **empleados internos** (DEC-004) → riesgo bajo. El "Estricto" exigiría *sharing*
  automático (compartir/descompartir cliente+dir+tel por asignación), hace **crecer la tabla POA** y
  reasignar owner de maestros en cada import → costo de performance/mantenimiento alto a escala (~100k
  cuentas) para mitigar un riesgo menor. **No se paga complejidad por un riesgo que no se tiene.**
- **Implicaciones:**
  - Un localizador podría, vía API/Advanced Find, consultar maestros fuera de su cartera (la **app** solo le
    muestra la suya). Aceptado y documentado.
  - **No** se usa *sharing* → la tabla POA **no crece**.
  - Requiere un **plugin/flujo** que setee el owner de `sanic_cm_tbl_asignacion` desde `agente → systemuser`.
- **Alternativas descartadas:** *Estricto* (costo alto para riesgo bajo); *Por zona/Business Units* (válido
  si los localizadores se organizaran por región; no es el caso hoy).

---

## DEC-016 — `sanic_cm_tbl_asignacion`: estado actual (1 fila por cuenta+tipo) + auditoría para historial

- **Estado:** Aceptada · 2026-06-21 *(a confirmar en revisión)*
- **Contexto:** La asignación es una **tabla viva** (DEC-012). Necesitamos: (a) upsert del import por clave de
  negocio, (b) que una cuenta pueda tener a la vez una **Diaria** y una **Especial**, y (c) **trazabilidad**
  de quién la tuvo y cuándo.
- **Decisión:**
  - **Alt key compuesta:** `sanic_cm_as_numerocuenta + sanic_cm_as_tipo`. Así una cuenta tiene **a lo sumo una asignación
    activa por tipo** (puede coexistir Diaria + Especial), y el import hace **upsert** limpio.
  - **Modelo de estado actual:** **una fila por (cuenta, tipo)** que se **actualiza** (no se acumulan filas).
  - **Historial de reasignaciones:** vía **auditoría de columna de Dataverse** sobre `sanic_cm_as_agenteid`/`owner`/`sanic_cm_as_estado`
    (no una tabla de historial paralela). El histórico **financiero** diario sigue en `cargadiaria`.
- **Porqué:** Evita una tabla de eventos paralela; el upsert del import exige clave única estable; la
  auditoría nativa ya responde *"¿quién la tenía el día X?"* sin duplicar datos.
- **Implicaciones:** Habilitar **auditoría** en `sanic_cm_tbl_asignacion` (campos `sanic_cm_as_agenteid`, `owner`, `sanic_cm_as_tipo`,
  `sanic_cm_as_estado`). El campo `sanic_cm_as_orden` del localizador **no se mapea** en el import → el upsert no lo pisa.
- **Alternativas descartadas:** *Tabla append de eventos de asignación* (más fiel al historial, pero complica
  "asignación actual" y el upsert); *alt key solo por `numerocuenta`* (no deja coexistir Diaria + Especial).

---

## DEC-017 — La tabla de cuentas se llama `sanic_cm_tbl_cuenta` (no `sanic_cuentamora`)

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** `cuentamora` no convencía y, además, es **impreciso**: con las asignaciones **Especiales**
  (DEC-013) puede haber cuentas que **no están en mora estricta**.
- **Decisión:** Renombrar la tabla a **`sanic_cm_tbl_cuenta`**. La clave primaria ya era genérica (`sanic_cm_tbl_cuentaid`) y no
  cambia; el número de cuenta `sanic_cm_ct_numerodecuenta` tampoco.
- **Porqué:** Nombre simple y natural ("mis cuentas" en la app), correcto también para Especiales, y fiel al
  lenguaje del localizador.
- **Implicaciones:** Renombre del componente (`08-tabla-cuenta.md`) y de todas las referencias en docs e
  import config. El concepto de negocio "cuenta en mora" se conserva en prosa.
- **Alternativas descartadas:** `sanic_cuentacobranza` (más largo), `sanic_caso` (cambia el modelo mental),
  `sanic_obligacion` (más abstracto).

---

## DEC-018 — Reconciliación de asignaciones Diaria desaparecidas (función del Admin, no automática)

- **Estado:** Aceptada · 2026-06-22 *(revisada: NO automática — la ejecuta el Admin)*
- **Contexto:** El import es **upsert-only** (nunca borra, a propósito: las Incobrables no vienen a diario y
  un archivo parcial no debe destruir datos). Consecuencia: una cuenta **Diaria** que **deja de aparecer** en
  la carga queda con su asignación `Activa` **colgada** del localizador para siempre.
- **Decisión:** **Reconciliación de snapshot completo, solo para `Diaria`**, ejecutada **manualmente por el
  Admin** como una **funcionalidad dentro de la PowerApp** (NO un flujo automático):
  1. Campo **sanic_cm_as_ultimacarga** (DateTime) en `sanic_cm_tbl_asignacion` = la `sanic_cm_cd_fechacarga` en que la cuenta vino.
     El import lo setea en **cada upsert** y pone `sanic_cm_as_estado = Activa`.
  2. El **Admin abre la función de reconciliación**. Esta calcula las candidatas: `sanic_cm_as_tipo = Diaria` +
     `sanic_cm_as_estado = Activa` + `sanic_cm_as_ultimacarga < la última carga`, y muestra un **preview**: cantidad, **% sobre las
     Diarias activas** y la fecha de carga.
  3. **Umbral de cordura (visible):** si el % supera el umbral configurado (ej. >20–30%), la función lo marca
     en **rojo** ("archivo posiblemente truncado") — el Admin decide igual. El humano es el control final.
  4. El Admin **confirma** → cierra las candidatas (`sanic_cm_as_estado = Cerrada`, `sanic_cm_as_motivo` *"fuera de cartera"*,
     sanic_cm_as_fechacierre). La app filtra `sanic_cm_as_estado = Activa` → salen del localizador.
  5. **Precondición:** la función solo habilita la ejecución si el **último import quedó Completado**.
  6. **Reporte:** vista *"Cerradas en esta corrida"* para auditoría.
  7. **Reaparición:** si la cuenta vuelve en una carga futura, el upsert la **re-activa** sola. El Admin/
     Supervisor también puede **reabrir** manualmente.
- **Porqué:** Patrón estándar de reconciliación de snapshot, pero con el **disparo en manos del Admin**
  (control humano explícito) en vez de automático.
- **Implicaciones:** `Incobrable` (cierre manual) y `Especial` (flujo de aprobación) **NO** se barren.
  Requiere el campo sanic_cm_as_ultimacarga, la **función en la app** (rol Admin) con preview + umbral + confirmación,
  y la vista de reporte. Es **lógica de negocio / app**, no Smelter.

---

## DEC-019 — Retención de `cargadiaria` vía Bulk Deletion

- **Estado:** Aceptada · 2026-06-21
- **Contexto:** `cargadiaria` crece **1 fila/cuenta/día** → a escala (100k cuentas) son **decenas de millones
  al año**. (`sanic_cm_tbl_asignacion` crece **lento** — upsert por `sanic_cm_as_numerocuenta+sanic_cm_as_tipo`, no es el problema.)
- **Decisión:** Política de **retención con Bulk Deletion** (job recurrente nativo de Dataverse): borrar de
  `sanic_cm_tbl_cargadiaria` las filas con `sanic_cm_cd_fechacarga < hoy − N` (ventana **N a definir**, ej. 6–12 meses). Para
  `sanic_cm_tbl_asignacion`: bulk-delete opcional de las `Cerradas` viejas (>1 año); el rastro de reasignaciones queda en
  la **auditoría** (DEC-016).
- **Porqué:** Simple y barato; el histórico reciente cubre la operación.
- **Tradeoff:** el histórico **más viejo que la ventana se pierde** (no se archiva). Aceptado por ahora; si
  luego se necesita histórico largo, se agrega **Long-Term Retention / Synapse Link** (offload a data lake).
- **Implicaciones:** definir la ventana `N` y el job recurrente.

---

## DEC-020 — Ciclo de vida de la asignación Incobrable

- **Estado:** Aceptada · 2026-06-22
- **Contexto:** A diferencia de la Diaria (source-driven, volátil, se reconcilia), la **Incobrable** es
  **persistente y human-managed**: vive meses con el mismo agente y viene en el CSV solo 1–2 veces/mes.
- **Decisión:**
  - **Nacimiento:** **solo del CSV** (`TipoAsignacion = Incobrable`). El core es el único que la clasifica;
    no se crea a mano (las creaciones manuales son exclusivas de las Especiales).
  - **Vida:** persiste `Activa`; **NO se reconcilia** (su ausencia en la carga es normal). El upsert la
    refresca cuando reaparece.
  - **Reasignación:** la hace el **Admin**. Conflicto con el CSV → **gana el CSV** (el import pisa el agente;
    lo manual es **interino**). No requiere marca especial → **consistente con la Diaria**. *Caveat:* un agente
    que renuncia se corrige en el **core**; mitigación en la app: `sanic_cm_tbl_agente.sanic_cm_ag_activo = No` + vista de
    control *"asignaciones de agentes inactivos"*.
  - **Cierre:** **manual, por el Admin**, vía función en la app **con motivo** (`Recuperada` /
    `Baja-castigada`). El **localizador NUNCA** cierra (segregación de funciones).
- **Porqué:** la Incobrable la gobierna el humano (Admin), salvo el dato base que viene del core.
- **Implicaciones:** función de cierre en la app (rol Admin), vista de agentes inactivos, set de motivos de
  cierre estándar (podrían formalizarse como Choice más adelante para reportería).

---

## DEC-021 — Reclasificación de tipo: Diaria e Incobrable son mutuamente excluyentes

- **Estado:** Aceptada · 2026-06-22
- **Contexto:** La alt key `sanic_cm_as_numerocuenta + sanic_cm_as_tipo` (DEC-016) permite que coexistan filas de distinto tipo. La
  **Especial** **debe** poder coexistir (es ortogonal, un encargo aparte). Pero **Diaria** e **Incobrable**
  son la **clasificación del core** → **mutuamente excluyentes**. Sin regla, un Incobrable→Diaria deja **dos
  asignaciones activas** de la misma cuenta.
- **Decisión:** **Regla de reclasificación automática en el import:** cuando una cuenta aparece con un
  core-type (`Diaria`/`Incobrable`), se **cierra su otro core-type activo** (`sanic_cm_as_estado = Cerrada`,
  `sanic_cm_as_motivo` *"Reclasificada a {tipo}"*). Resuelve **ambas direcciones al instante**. **Nunca** toca una `Especial`.
- **Porqué:** Es **presence-driven** (la cuenta está en el archivo con tipo nuevo) → **sin riesgo de
  truncamiento** → seguro automatizar. (A diferencia de la reconciliación de Diarias, DEC-018, que es
  **absence-driven** y por eso es manual del Admin.)
- **Implicaciones:** Lógica en el **post-import** (junto al plugin de owner). Distingue core-types
  (Diaria/Incobrable) de la Especial. Refina la coexistencia de DEC-016: coexisten **{Diaria|Incobrable} +
  Especial**, nunca Diaria + Incobrable.

---

## DEC-022 — Ciclo de vida de la asignación Especial

- **Estado:** Aceptada · 2026-06-22 *(extiende DEC-013)*
- **Contexto:** La Especial es el único tipo con **workflow de aprobación** y el único **ortogonal** (convive
  con Diaria/Incobrable, DEC-021).
- **Decisión:**
  - **Nacimiento (dos orígenes, ambos pasan por la aprobación del Supervisor):**
    - **App:** el **Solicitante** la crea (`Borrador → Pendiente`).
    - **CSV:** si el CSV trae `TipoAsignacion = Especial`, el import la crea como **`Pendiente`** (NO
      pre-aprobada) → va al Supervisor. **Resuelve el caso borde 8.1.** `sanic_cm_as_solicitadopor` = servicio de import
      (origen "CSV").
  - **Aprobación:** el **Supervisor** aprueba (asigna localizador → owner, `sanic_cm_as_estado = Activa`,
    `sanic_cm_as_estadoaprobacion = Aprobada`) o **rechaza** (`Rechazada` + motivo, terminal).
  - **Modelo de 2 estados:** `sanic_cm_as_estadoaprobacion` gobierna la fase de aprobación; `sanic_cm_as_estado` (Activa/Cerrada)
    **recién importa al aprobar**. **Cartera del localizador** = suya + `sanic_cm_as_estado = Activa` +
    `sanic_cm_as_estadoaprobacion = Aprobada` (antes de aprobar no tiene owner localizador → no aparece).
  - **Vida:** **sin vencimiento** (vive hasta cierre manual). Mitigación: vista *"Especiales activas antiguas"*.
  - **Reasignación:** la hace el **Supervisor** (él eligió al localizador). Sin conflicto con el CSV → **persiste**.
  - **Cancelación:** el **Solicitante retira** su `Borrador`/`Pendiente` (terminal `Cancelada`); cancelar una
    **Activa** = cierre por el **Admin** (motivo `Cancelada`).
  - **Cierre:** **Admin**, con motivo (`Recuperada` / `No recuperada` / `Cancelada`).
- **Guarda (re-import):** un CSV que **reenvía** una Especial ya existente **NO** debe revertir
  `sanic_cm_as_estadoaprobacion` (preserva la decisión del Supervisor). `sanic_cm_as_estadoaprobacion = Pendiente` se setea **solo al
  crear**, nunca al re-upsertear (mismo patrón que el `sanic_cm_as_orden`).
- **Implicaciones:** el import setea `sanic_cm_as_estadoaprobacion = Pendiente` solo para `sanic_cm_as_tipo = Especial` **nuevas**;
  conviene un indicador de **origen** (App/CSV); reasignación por Supervisor; cierre por Admin.

---

## DEC-023 — Borrado de maestros: `Cascade` en datos, `Restrict` en historia operativa

- **Estado:** Aceptada · 2026-06-22 *(revisada: Cascade en datos, no Restrict en todo)*
- **Contexto:** Los maestros son referenciados por el histórico operativo (interacciones, pagos…) = rastro de
  auditoría. Surgió la preocupación del **huérfano** al borrar. El negocio confirma que **no hay política de
  borrado**, pero **sí** se quiere poder borrar un cliente y arrastrar **sus datos** (direcciones, cuentas,
  teléfonos), **excepto** cuando hay **cobranza registrada**.
- **Decisión — comportamiento referencial MIXTO:**
  - **`Cascade`** en la **cadena de datos/contacto (posesión)**: `cliente → direccion`, `cliente → telefono`,
    `cliente → cuenta`. Borrar el cliente **borra todos sus datos relacionados**.
  - **`Restrict`** en las **referencias de catálogo**: `producto → cuenta`, `agente → cuenta`,
    `fiador → cuenta`. Son catálogos **compartidos** por muchas cuentas → **jamás** se cascadea (borrar un
    producto NO borra sus cuentas); se **bloquea** si está en uso. `agente` además **nunca** se hard-delete
    (soft-delete vía `sanic_cm_ag_activo`).
  - **`Restrict`** en el **único candado**: `cuenta → interaccion` **(CONFIRMADO)**. Una cuenta con
    interacciones **no se puede borrar** → protege toda la cadena `cliente → cuenta → interaccion`.
  - **`Cascade`** en **todo lo demás aguas abajo de la cuenta**: `cuenta → cargadiaria`, `cuenta → asignacion`,
    e `interaccion → pago` e `interaccion → actualizaciondatos`. Borrar arrastra; **solo** las `interaccion`
    bloquean. (El rastro de reasignaciones vive en la auditoría, DEC-016.)
    *(Intención del negocio; se **confirma** al revisar la tabla `interaccion`.)*
  - **Efecto neto:** borrar un cliente **cascadea** sus datos, **pero se bloquea** si alguna de sus cuentas
    tiene **interacciones** (la `Restrict` aguas abajo **detiene la cascada** — Dataverse hace rollback). En
    criollo: *"borrás todo, salvo que haya historia de cobranza"*.
  - **No hay hard-delete de rutina** (no hay política): el fin de vida normal es **soft-deactivate**
    (`sanic_cm_tbl_agente.sanic_cm_ag_activo = No`; cuenta "fuera de cobranza" = sin asignación activa). El borrado es
    **deliberado** (ej. eliminación de datos legal).
  - `direccion`/`telefono` = **dato actual** (upsert por `cif+tipo`); el histórico de cambios vive en
    `actualizaciondatos`. El **correo** **NO** se guarda en `cliente`: vive **solo** en `actualizaciondatos`.
- **Porqué:** Permite limpiar un cliente y su data cuando corresponde, pero la `Restrict` en la historia
  **protege la auditoría** y hace el huérfano **imposible**.
- **Implicaciones:** Configurar `Cascade` en cliente→hijos de datos y `Restrict` en cuenta→interaccion (a
  confirmar). Sin jobs de borrado masivo de maestros (a diferencia de `cargadiaria`, DEC-019).

---

## Plantilla para nuevas decisiones

```
## DEC-00X — <título corto>
- **Estado:** Propuesta | Aceptada | Reemplazada por DEC-xxx · <fecha>
- **Contexto:** <qué problema/disyuntiva motivó la decisión>
- **Decisión:** <qué se decidió>
- **Porqué:** <razón técnica/de negocio>
- **Implicaciones:** <qué cambia en datos/apps/seguridad/proceso>
- **Alternativas descartadas:** <opciones consideradas y por qué no>
```
