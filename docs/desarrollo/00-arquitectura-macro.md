# Cobranza Móvil — Arquitectura Macro (Solución Power Platform)

> **Estado:** Borrador macro · Nivel: visión a gran escala (sin detalle de componentes todavía).
> **Objetivo de este documento:** fijar la forma general de la solución y sus capas antes de
> diseñar cada componente (tablas, apps, flujos) en su propio markdown.
>
> Documentos relacionados:
> - [`../smelter-overview.md`](../referencia/smelter-overview.md) — qué es Smelter (motor de import).
> - [`../cobranza-import-config.md`](../referencia/cobranza-import-config.md) — config concreta del import diario.
> - [`decisiones/registro-de-decisiones.md`](../referencia/decisiones/registro-de-decisiones.md) — bitácora de decisiones (ADR).

---

## 1. El problema (estado actual)

El departamento de cobranza asigna cuentas en mora a **localizadores externos** (personas que cobran
en moto). Hoy el proceso es manual y en papel:

1. A cada localizador le asignan, **en papelitos**, qué cuentas debe cobrar ese día.
2. En la calle, el localizador cobra, **emite un recibo a mano** y **anota en papelitos** cada
   interacción (pagó, no se localizó, promesa de pago, etc.).
3. De vuelta en la oficina, entrega los papelitos al **backoffice**.
4. El backoffice hace **data-entry manual** de cada interacción y pago al **software de cobro del banco**
   (el core), que usa **códigos de interacción específicos**.

**Dolores:** papel se pierde, doble carga de datos, sin trazabilidad, sin tiempo real, sin estadísticas,
sin priorización, recibos a mano.

---

## 2. La visión (estado objetivo)

Modernizar el ciclo completo con Power Platform, **sin reemplazar el core bancario** (sigue siendo la
fuente de verdad de la deuda). La solución es la **capa operativa móvil** entre el core y la calle.

El "papelito" cumple hoy DOS funciones; las separamos:

| Función del papelito | Cómo la cubre la solución |
|----------------------|---------------------------|
| (1) Decirle al localizador **qué cobrar** | **Import diario** (Smelter) — ya diseñado. |
| (2) Que él **anote lo que pasó** para recargar al core | **Captura en campo** (gestión/pago) + **write-back asistido** — lo nuevo. |

La función (1) ya está resuelta. **El corazón de este proyecto es la función (2):** capturar la gestión
y el pago en el celular del localizador y devolverlos al core eliminando el papel y minimizando la doble
carga.

---

## 3. Arquitectura macro (capas)

```
        CORE BANCARIO (fuente de verdad de la deuda)
          │  ① CSV diario de cuentas en mora           ▲  ④ backoffice graba gestiones/pagos
          ▼     (entrada)                               │     manualmente (con códigos del core)
┌──────────────────────────────────────────────────────────────────────┐
│  DATAVERSE  (prefijo sanic_)                                           │
│                                                                        │
│  ── CAPA DE IMPORT (Smelter) ───────────────────────── YA DISEÑADA     │
│       cliente · cuenta · producto · agente · fiador                │
│       direccion · telefono · cargadiaria (snapshot diario)             │
│                                                                        │
│  ── CAPA OPERATIVA (NUEVA, a diseñar) ──────────────────────────────   │
│       localizador · gestión · tipogestion(=códigos core) · pago/recibo │
│       meta (gamificación) · priorización del día                       │
│                                                                        │
│  ── SEGURIDAD: cada localizador ve SOLO su cartera (row-level)        │
└───────────────┬───────────────────────────────────────┬──────────────┘
        ② offline sync                          ③ revisión y export
                ▼                                         ▼
    ┌───────────────────────────┐         ┌──────────────────────────────┐
    │ APP DE CAMPO              │         │ APP DE OFICINA                 │
    │ Canvas · móvil · OFFLINE  │         │ Model-driven · escritorio      │
    │ rol: Localizador          │         │ roles: Backoffice·Solicitante· │
    │ - ver cartera del día     │         │        Supervisor·Admin        │
    │ - ordenar/priorizar       │         │ - revisar interacciones        │
    │ - registrar interacción   │         │ - asignar código + grabar core │
    │ - registrar pago(s)       │         │ - asignar/aprobar Especiales   │
    │ - stats / ranking         │         │ - import · dashboards · catáls.│
    └───────────────────────────┘         └──────────────────────────────┘
```

**Flujos numerados:**
- **① Entrada:** el core exporta el CSV diario → Smelter normaliza a las tablas de import. *(Ya diseñado.)*
- **② Sync offline:** la app de campo trabaja contra un store local y sincroniza gestiones/pagos a
  Dataverse al recuperar señal.
- **③ Revisión:** backoffice/admin ven en Dataverse lo capturado en campo.
- **④ Write-back manual asistido:** backoffice transcribe al core, **le asigna el código** del core que
  corresponde, y marca como `Grabada en core` (con auditoría) para que nadie la cargue dos veces.

### 3.1 El Import (entrada de datos) — explícito

Cada día el core bancario **exporta un CSV** con las cuentas en mora a cobrar. Ese archivo lo procesa
**Smelter** (motor declarativo sobre Dataverse) y lo **normaliza** a las tablas de import. En resumen:

- **Frecuencia:** **1 vez al día** (mañana). **Idempotente** (reprocesar no duplica; upsert por clave de negocio).
- **Formato:** CSV de **32 columnas**, denormalizado — cada fila mezcla **cliente + cuenta + producto +
  agente + fiador + 3 direcciones + 2 teléfonos**. Diccionario de negocio completo en
  [`01-import-y-diccionario-de-datos.md`](01-import-y-diccionario-de-datos.md).
- **Qué genera:** `cliente`, `cuenta`, `producto`, `agente`, `fiador`, `direccion` (Casa/EC/Trabajo),
  `telefono` (Celular/Casa) y el snapshot histórico `cargadiaria`.
- **Dato clave para campo:** `RetiroNIO`/`RetiroUSD` = la **plata que el localizador va a ir a traer** al cliente.
- **Asignación:** el CSV trae `TipoAsignacion` (Diaria/Incobrable/Especial) y `Agente` → de ahí se puebla
  `sanic_cm_tbl_asignacion` para **Diaria/Incobrable**. Las **Especiales NO vienen por CSV** (nacen en la app, DEC-013).
- **Detalle técnico** (mappings, alt keys, orden DAG): [`../cobranza-import-config.md`](../referencia/cobranza-import-config.md)
  y [`../smelter-overview.md`](../referencia/smelter-overview.md).

---

## 4. Decisiones fundacionales (resumen)

Detalle y porqué en [`decisiones/registro-de-decisiones.md`](../referencia/decisiones/registro-de-decisiones.md).

| # | Decisión | Implicación arquitectónica principal |
|---|----------|--------------------------------------|
| DEC-001 | **App de campo offline-first** | Canvas con perfil offline + store local + sync. Define toda la app. |
| DEC-002 | **Write-back manual; el código lo asigna el backoffice** | La gestión es comentario libre; el backoffice asigna el código del core al transcribir + estado de transcripción + auditoría anti-doble-carga. El código se guarda para futuro auto-clasificador. |
| DEC-003 | **Pago en papel; recibo digital on/off futuro** | El pago son atributos de la interacción (monto, moneda NIO/USD, nº recibo físico). Sin entidad de pago. Recibo digital diferido. |
| DEC-004 | **Localizadores = empleados internos (AAD)** | Identidad Entra ID, licencia Power Apps, seguridad row-level por ownership/BU. |
| DEC-005 | **Tabla de asignación diaria dedicada** | `sanic_cm_tbl_asignacion` separada del snapshot financiero: trazabilidad + orden del localizador no-pisable. |
| DEC-006 | **El "Agente" del import ES el localizador** | Lookup `sanic_cm_tbl_agente → systemuser` (AAD). No se crea entidad localizador aparte. Ancla la seguridad row-level. |
| DEC-007 | **Numeración anti-colisión por prefijo de localizador** *(propuesta)* | `{codigoLocalizador}-{yyyymmdd}-{secuenciaLocal}`, sin coordinación. Para el recibo digital futuro. |
| DEC-008 | **Geo-ubicación (lat/long) en cada interacción** | Trazabilidad de campo. Capturada por el dispositivo, viaja en el sync offline. |
| DEC-009 | **La tabla de captura se llama `sanic_cm_tbl_interaccion`** | Renombre desde `sanic_gestion`. El negocio habla de "interacción". |
| DEC-010 | **Pago como entidad separada `sanic_cm_tbl_pago` (1:N)** | Una interacción puede tener varios pagos (mixto NIO+USD). Reemplaza el "pago = atributos" de DEC-003. |
| DEC-011 | **Gamificación = estadísticas calculadas, sin metas aún** | Sin tabla de metas. Recuperado por moneda + dolarizado, nº interacciones. Expandible a objetivos. |
| DEC-012 | **`sanic_cm_tbl_asignacion`: 3 tipos + ciclo de vida + política import** | Diaria/Incobrable/Especial; tabla viva con estado; import 1x/día, manual manda, no pisa incobrables/especiales. |
| DEC-013 | **Flujo de asignación Especial + el Solicitante escribe datos maestros** | El **Solicitante** crea/edita cliente/tel/dir/cuenta; la especial va a aprobación del Supervisor. |
| DEC-014 | **5 roles: Localizador, Backoffice, Solicitante, Supervisor, Admin** | 1 Canvas (localizador) + 1 Model-driven diferenciada por roles. Segregación de funciones. |
| DEC-015 | **Seguridad row-level "Moderado"** | Operativas User-level (solo lo suyo); maestros Read Org-level (lee el padrón, la app filtra). Sin sharing. |
| DEC-016 | **`sanic_cm_tbl_asignacion`: estado actual + auditoría** | Alt key `sanic_cm_as_numerocuenta+sanic_cm_as_tipo` (Diaria+Especial coexisten); historial de reasignación vía auditoría, no tabla aparte. |
| DEC-017 | **Tabla de cuentas = `sanic_cm_tbl_cuenta`** | Renombre desde `cuentamora` (impreciso por las Especiales). |
| DEC-018 | **Sweep de Diarias desaparecidas** | sanic_cm_as_ultimacarga + flujo post-import que cierra las `Diaria` no vistas en la última carga. Solo Diaria; guarda de import completo. |
| DEC-019 | **Retención de `cargadiaria` (Bulk Delete)** | Borrado recurrente `fechacarga < hoy−N`. `cargadiaria` crece rápido; `asignacion` no. Offload a data lake si se necesita histórico largo. |
| DEC-020 | **Ciclo de vida Incobrable** | Nace solo del CSV; persiste (no se reconcilia); reasigna el Admin (gana el CSV); cierre manual del Admin con motivo. |
| DEC-021 | **Reclasificación Diaria↔Incobrable** | Mutuamente excluyentes; al llegar un core-type, el import cierra el otro activo (automático, presence-driven). Especial es ortogonal. |
| DEC-022 | **Ciclo de vida Especial** | Nace en app o CSV (→ Pendiente); aprueba/reasigna el Supervisor; cierra el Admin; sin vencimiento. Aprobación = única compuerta. |
| DEC-023 | **Lifecycle de maestros: Restrict + soft-delete** | Los maestros tienen dependientes (auditoría). `Restrict` (huérfanos imposibles); no hard-delete (no hay política); fin de vida = desactivar. |

---

## 5. Modelo de datos macro (capa operativa nueva)

> Nivel conceptual. El detalle de columnas, alt keys y relaciones va en los markdowns por componente.

| Entidad | Qué representa | Notas de diseño macro |
|---------|----------------|------------------------|
| `sanic_cm_tbl_agente` (= localizador) | El cobrador de campo. **Es el "Agente" del import** (DEC-006). | Lookup a `systemuser` (AAD). Ancla la seguridad row-level. **No** se crea entidad localizador aparte. |
| `sanic_cm_tbl_interaccion` | Una interacción con el cliente, en **texto libre** (pagó, no contesta, promesa, etc.) (DEC-009). | **Núcleo de la captura.** Campos abajo. La "no localización" es **solo un comentario**, no una entidad. |
| `sanic_cm_tbl_pago` | Un pago cobrado al cliente (DEC-010). | **1:N** con la interacción (una interacción → varios pagos: cubre NIO + USD). Recibo **físico**. |
| `sanic_cm_tbl_asignacion` | Qué cuenta se asignó a qué localizador, de qué **tipo**, en qué estado (DEC-005, DEC-012). | Tabla operativa **viva** (no snapshot). 3 tipos: **Diaria / Incobrable / Especial**. Trazabilidad + orden del localizador. |

**Gamificación = estadísticas calculadas** (DEC-011): **no hay tabla de metas** todavía. Las estadísticas se
**agregan** sobre interacciones/pagos (recuperado por moneda + dolarizado, nº de interacciones, etc.).

**No hay catálogo que espeje los códigos del core** (DEC-002): el código lo **asigna el backoffice** al
transcribir, y se guarda en la interacción para un futuro auto-clasificador.

### Campos de la interacción (`sanic_cm_tbl_interaccion`) — conceptual

| Campo | Tipo | Quién lo llena |
|-------|------|----------------|
| Localizador / agente | Lookup → `sanic_cm_tbl_agente` | localizador (auto) |
| Cuenta | Lookup → `sanic_cm_tbl_cuenta` | localizador |
| Comentario | Texto largo | localizador |
| Fecha de la interacción | DateTime | localizador (auto, en campo) |
| ¿Actualiza info del cliente? | Boolean | localizador |
| ¿Aplica pago? | Boolean | localizador |
| Geo-ubicación (lat/long) | Decimal x2 (DEC-008) | localizador (auto) |
| Estado de transcripción | Choice Pendiente / Grabada en core | sistema / backoffice |
| Código del core asignado | Texto *(futuro: catálogo)* | backoffice |
| Fecha grabada en core | DateTime | backoffice |
| Grabada por | Lookup → `systemuser` | backoffice |

> Los **montos** ya no están aquí: viven en `sanic_cm_tbl_pago` (DEC-010).

### Campos del pago (`sanic_cm_tbl_pago`) — conceptual

| Campo | Tipo | Quién lo llena |
|-------|------|----------------|
| Interacción | Lookup → `sanic_cm_tbl_interaccion` | localizador |
| Monto | Decimal | localizador |
| Moneda | Choice NIO / USD | localizador |
| Número de recibo (físico) | Texto | localizador |
| Fecha del pago | DateTime | localizador |

### Campos de la asignación (`sanic_cm_tbl_asignacion`) — conceptual

| Campo | Tipo | Notas |
|-------|------|-------|
| Cuenta | Lookup → `sanic_cm_tbl_cuenta` | **Requerida** (toda asignación tiene cuenta). |
| Localizador / agente | Lookup → `sanic_cm_tbl_agente` | En Especiales lo define el **Supervisor**. |
| Tipo | Choice Diaria / Incobrable / Especial | Ciclo de vida por tipo (DEC-012). |
| Estado | Choice Activa / Cerrada (+ flujo de aprobación para Especiales) | Cierre **manual** para Incobrables. |
| Orden / prioridad | Whole Number | Lo ordena el **localizador**; no lo pisa el import. |
| Fecha de asignación | DateTime | — |
| Motivo de cierre | Texto | Al cerrar manualmente. |
| *(Especiales)* Estado de aprobación, supervisor, creada por, motivo de rechazo | varios | Flujo DEC-013. |

**Relación con lo existente:** la asignación se vincula con `sanic_cm_tbl_cuenta` (y el snapshot
`sanic_cm_tbl_cargadiaria` da el histórico financiero diario). `sanic_cm_tbl_asignacion` es **operativa y viva**, separada
del snapshot, para que el import no pise el orden ni las reasignaciones manuales (DEC-005, DEC-012).

---

## 6. Las aplicaciones

| App | Tecnología | Rol | Responsabilidad |
|-----|-----------|-----|------------------|
| **App de Campo** | Canvas, móvil, **offline-first** | **Localizador** | Ver cartera del día (incl. `RetiroNIO/USD` a cobrar), ordenar/priorizar, registrar interacción, registrar pago(s), ver stats/ranking. Solo su cartera. |
| **App de Oficina** | Model-driven | **Backoffice** | Revisar interacciones, **transcribir al core** (asignar código) y marcar `Grabada en core`. |
| ″ | ″ | **Solicitante** | **Crear** asignaciones **Especiales** (selecciona/crea cliente, **edita datos maestros**) y enviarlas a aprobación. |
| ″ | ″ | **Supervisor** | **Aprobar/rechazar** asignaciones Especiales y **asignar** el localizador. |
| ″ | ″ | **Admin** | Disparar/monitorear el import, gestionar catálogos y configuración, dashboards globales. |

> **2 apps confirmado:** 1 Canvas (campo) + 1 Model-driven (Backoffice + Solicitante + Supervisor + Admin en
> la **misma** app, diferenciados por **roles de seguridad**) (DEC-014).

---

## 7. Asuntos transversales (cross-cutting)

1. **Sync offline (DEC-001):** qué tablas van al perfil offline, resolución de conflictos, qué pasa si dos
   sync chocan. La cartera del día es **mayormente lectura**; lo que se escribe offline es gestión y pago.
2. **Recibo:** hoy es **físico** (número del talonario, lo anota el localizador). La numeración anti-colisión
   por prefijo de localizador (DEC-007) aplica **solo al recibo digital futuro**, no es bloqueante hoy.
3. **Write-back anti-doble-carga (DEC-002):** el backoffice **asigna el código del core** al transcribir +
   estado de transcripción + auditoría (quién/cuándo grabó en core).
4. **Seguridad row-level (DEC-015, "Moderado"):** operativas (asignación/interacción/pago) **User-level**
   (solo lo suyo, vía ownership); maestros **Read Org-level** (lee el padrón, la app filtra la cartera).
   Detalle y pasos en [`componentes/01-seguridad-row-level.md`](../referencia/componentes/01-seguridad-row-level.md).
5. **Licenciamiento:** Power Apps para localizadores y backoffice; medir costo por usuario.
6. **Histórico vs estado actual:** el import ya distingue `cuenta` (actual) de `cargadiaria` (histórico).
   La capa operativa (gestiones/pagos) es **append-only** por naturaleza (cada gestión es un hecho fechado).

---

## 8. Roadmap por fases (tentativo)

| Fase | Foco | Entregable |
|------|------|-----------|
| 1 | **Modelo de datos** | **Las 12 tablas desde 0** (maestras: cliente, producto, agente, fiador, direccion, telefono, cuenta, cargadiaria · operativas: interaccion, pago, asignacion) + **seguridad row-level**. Componentes en [`componentes/`](../referencia/componentes). |
| 2 | **Configurar el import** | Templates/mappings de Smelter + alt keys activas + poblar `asignacion` (Diaria/Incobrable). Validar en ambiente. |
| 3 | **App de Oficina (Backoffice/Admin)** | Model-driven: revisión, asignación de código del core, write-back, flujo de Especiales (Solicitante/Supervisor), monitoreo de import. |
| 4 | **App de Campo (online)** | Canvas funcional online: ver cartera (incl. `Retiro`), ordenar, registrar interacción + pago(s). |
| 5 | **Offline + sync** | Perfil offline, captura sin señal, sincronización al recuperar conexión. |
| 6 | **Gamificación + recibo digital** | Estadísticas/ranking calculados + recibo digital encendible/apagable con numeración anti-colisión (DEC-007). |

---

## 9. Preguntas — estado

**Resueltas:**
- **P-1 ✅** `sanic_cm_tbl_agente` **ES** el localizador; lookup a `systemuser` (DEC-006).
- **P-3 ✅** Una sola app Model-driven con **roles** (backoffice + admin juntos).
- **P-4 ✅** No se necesita el catálogo de códigos del core por ahora; se **guarda el código que el
  backoffice asigna** a cada interacción (para futuro auto-clasificador) (DEC-002).
- **P-5 ✅** Recibo digital **básico** y **on/off a demanda**, diferido (DEC-003).
- **P-6 ✅** Numeración anti-colisión por **prefijo de localizador** (DEC-007), para el recibo digital futuro.

- **P-2 ✅** `sanic_cm_tbl_asignacion` confirmada como **tabla dedicada** (DEC-005).
- **Geo-ubicación ✅** Se captura **lat/long** en cada interacción (DEC-008).
- **P-7 ✅** Promesa de pago = **comentario libre**, **fuera del modelo** (sin entidad ni alertas). Reabrir si
  se quiere seguimiento de incumplimiento.

**No quedan preguntas abiertas a nivel macro.** ✅ Macro firme — listo para diseñar componentes.

---

## 10. Lo que NO cambia

- El **core bancario** sigue siendo la fuente de verdad de la deuda.
- La **capa de import (Smelter)** queda como está (ver docs de import).
- El **write-back al core es manual** (asistido), no automatizado (DEC-002).
