# Componente: Tabla `sanic_asignacion`

> **Fase 1 — Modelo de datos · Componente 12 (operativa).**
> Qué cuenta se asignó a qué localizador, de qué **tipo**, en qué **estado**, y con qué **orden**. Es una
> **tabla viva** (no un snapshot), separada del histórico financiero `cargadiaria`.
>
> Materializa: **DEC-005** (tabla dedicada), **DEC-012** (3 tipos + ciclo de vida + política import),
> **DEC-013** (flujo Especial), **DEC-015** (owner = localizador), **DEC-016** (alt key + auditoría).

---

## 1. Objetivo

Ser la **fuente operativa** de la cartera: lo que cada localizador tiene que cobrar **hoy y de forma
persistente** (Incobrables), con su **orden** de trabajo y, para las Especiales, su **flujo de aprobación**.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Origen / descripción |
|---------------|---------------|------|:----:|----------------------|
| Número | `sanic_numero` | Autonumber (`ASG-{SEQNUM:6}`) | auto | **Primary name.** |
| Número de cuenta | `sanic_numerocuenta` | Text | ✔ | Copiado para la **alt key** (patrón del import). |
| Cuenta | `sanic_cuentaid` | Lookup → `sanic_cuenta` | ✔ | Resuelto por `NumeroCuenta`. **Toda asignación tiene cuenta** (DEC-013). |
| Agente | `sanic_agenteid` | Lookup → `sanic_agente` | — | El localizador asignado. En Especiales lo define el **Supervisor**. |
| Tipo | `sanic_tipo` | Choice (ver §5) | ✔ | Diaria / Incobrable / Especial. |
| Estado | `sanic_estado` | Choice (ver §5) | ✔ | Activa / Cerrada. Default `Activa`. |
| Orden | `sanic_orden` | Whole Number | — | **Prioridad del localizador.** **NO** se mapea en el import (no se pisa). |
| Fecha de asignación | `sanic_fechaasignacion` | DateTime | — | Cuándo se asignó. |
| Última carga | `sanic_ultimacarga` | DateTime | — | = `FechaCarga` en que la cuenta vino. La setea el import en cada upsert. Base del **sweep** (DEC-018). |
| Motivo de cierre | `sanic_motivocierre` | Text | — | Al cerrar (manual en Incobrables DEC-012; *"fuera de cartera"* en el sweep DEC-018). |
| **— Solo Especiales —** | | | | |
| Estado de aprobación | `sanic_estadoaprobacion` | Choice (ver §5) | — | Borrador / Pendiente / Aprobada / Rechazada. |
| Supervisor | `sanic_supervisorid` | Lookup → `systemuser` | — | Quién aprueba/rechaza. |
| Creada por (Solicitante) | `sanic_creadaporid` | Lookup → `systemuser` | — | El rol **Solicitante** (DEC-014). |
| Motivo de rechazo | `sanic_motivorechazo` | Text | — | Si el Supervisor la rechaza. |
| Propietario | `ownerid` | Owner (system) | ✔ | = el **localizador** (seteado por plugin/flow, Comp. 01 §6). |

---

## 3. Alternate Key (DEC-016)

- **Compuesta: `sanic_numerocuenta` + `sanic_tipo`.**
- Garantiza **una asignación activa por (cuenta, tipo)** → una cuenta puede tener Diaria **y** Especial a la
  vez, pero no dos Diarias.
- El import hace **upsert** por esta clave (Diaria/Incobrable). El `tipo` es un **Choice** válido en alt key
  (Picklist soportado); el import **traduce el texto del CSV al valor** (Diaria→1, etc.) — ver
  [`../02-import-configuracion.md`](../../guias/02-import-configuracion.md) §4.

> Como en el import, la alt key va sobre **texto** (`numerocuenta`), no sobre el lookup. El vínculo
> `sanic_cuentaid` lo resuelve el Relationship Mapping aparte.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna |
|----------|:------------:|---------|
| `sanic_cuenta` → `sanic_asignacion` | 1:N | `sanic_cuentaid` |
| `sanic_agente` → `sanic_asignacion` | 1:N | `sanic_agenteid` |
| `systemuser` → `sanic_asignacion` (supervisor) | 1:N | `sanic_supervisorid` |
| `systemuser` → `sanic_asignacion` (creada por) | 1:N | `sanic_creadaporid` |

---

## 5. Choices

**`sanic_tipo`** (DEC-012):
| Valor | Código | Comportamiento |
|-------|:------:|----------------|
| Diaria | 1 | El import diario la renueva. |
| Incobrable | 2 | Persiste; no viene a diario; **cierre manual**. |
| Especial | 3 | Nace en la app; **flujo de aprobación**. |

**`sanic_estado`:**
| Valor | Código |
|-------|:------:|
| Activa | 1 |
| Cerrada | 2 |

**`sanic_estadoaprobacion`** (solo Especiales, DEC-013):
| Valor | Código |
|-------|:------:|
| Borrador | 1 |
| Pendiente aprobación | 2 |
| Aprobada | 3 |
| Rechazada | 4 |

---

## 6. Ciclo de vida por tipo (DEC-012)

```
 DIARIA      ── import 1×/día ──► upsert (agente/saldo cambian). Manual interino (gana el CSV). Si desaparece
                                  → la RECONCILIA el Admin (DEC-018).
 INCOBRABLE  ── import 1–2×/mes ─► upsert; NO se reconcilia por no venir. Reasignación = Admin (gana el CSV).
                                  CIERRE manual del ADMIN con motivo (Recuperada/Baja-castigada) (DEC-020).
 ESPECIAL    ── app (Solicitante) o CSV ─► Borrador → Pendiente → [Supervisor] Aprobada (asigna localizador,
                                  owner, estado=Activa) / Rechazada (motivo). Reasigna el SUPERVISOR; cierra el
                                  ADMIN; sin vencimiento. Ortogonal (convive con Diaria/Incob.). (DEC-022)
```

### Especial — los dos campos de estado (DEC-022)

`estadoaprobacion` gobierna la **aprobación** (`Borrador → Pendiente → Aprobada/Rechazada`); `estado`
(Activa/Cerrada) **recién importa al aprobar**. La **cartera del localizador** = suya + `estado = Activa` +
`estadoaprobacion = Aprobada`. Origen App (Solicitante) **o** CSV (import → `Pendiente`, nunca pre-aprobada).
**Guarda:** un re-import **no** revierte `estadoaprobacion` (solo se setea `Pendiente` al crear).

> **Reclasificación (DEC-021):** Diaria e Incobrable son **mutuamente excluyentes** (clasificación del core).
> Cuando una cuenta aparece con un core-type, el import **cierra su otro core-type activo** (motivo
> *"Reclasificada a {tipo}"*) — automático, **presence-driven** (seguro). **Nunca** toca una `Especial`.

### Reconciliación — la cuenta Diaria que desaparece (función del Admin, DEC-018)

El import es **upsert-only**: nunca borra. Si una cuenta **Diaria** deja de venir, su asignación quedaría
`Activa` colgada. La saca el **Admin** con una **función ejecutable en la PowerApp** (NO automática):

1. El import setea **`sanic_ultimacarga = FechaCarga`** y `estado = Activa` en **cada upsert**.
2. El Admin abre la función → **preview** de candidatas (`Diaria` + `Activa` + `ultimacarga < última carga`):
   cantidad, **% sobre Diarias activas**, fecha de carga.
3. **Umbral de cordura visible:** si el % es anómalo (ej. >20–30%) lo marca en rojo. El Admin decide.
4. El Admin **confirma** → `estado = Cerrada`, `motivocierre`, `fechacierre`. **Precondición:** último import
   `Completado`.
5. **Reaparición:** si la cuenta vuelve, el upsert la **re-activa**; el Admin/Supervisor puede reabrir.

> Solo se reconcilian las **`Diaria`**. `Incobrable` = cierre **manual** (DEC-012); `Especial` = flujo de aprobación.

---

## 7. Pasos de construcción

1. Crear la tabla `sanic_asignacion`, **User/Team owned**. Primary name autonumber `ASG-{SEQNUM:6}`.
2. Crear columnas de §2 (incluido `sanic_numerocuenta` texto para la alt key).
3. Crear los **Choices** de §5 con sus defaults (`estado = Activa`).
4. Crear la **Alternate Key** `sanic_numerocuenta + sanic_tipo` y dejarla **Active** (DEC-016).
5. Crear los lookups (cuenta, agente, supervisor, creada por).
6. **Habilitar auditoría** en `sanic_agenteid`, `ownerid`, `sanic_tipo`, `sanic_estado` → historial de
   reasignaciones (DEC-016, reemplaza una tabla de historial).
7. **Owner por plugin/flow** (Comp. 01 §6, paso 10): en `Create`/`Update`, setear `ownerid` =
   `sanic_agenteid.sanic_usuario`. Para Especiales, al **Aprobar**.
8. **Import (Fase 2):** Table Mapping de Smelter → `sanic_asignacion` para Diaria/Incobrable; **no mapear**
   `sanic_orden` (para no pisar la prioridad del localizador).
9. **Flujo de Especiales (su md):** estados, transición de aprobación, asignación de agente por el Supervisor.

---

## 8. Gotchas

- **8.1 El orden NO se pisa:** si `sanic_orden` se mapeara en el import, el upsert lo borraría. **No mapearlo**
  (DEC-012). El localizador reordena y sobrevive a las recargas.
- **8.2 Owner al reasignar:** cambiar `sanic_agenteid` debe **re-setear** `ownerid` (mismo plugin del paso 7),
  o el localizador nuevo no la verá y el viejo la seguiría viendo.
- **8.3 Especial sin agente:** mientras está `Pendiente`, `sanic_agenteid` puede estar vacío → sin owner
  visible para un localizador (correcto: todavía no se asignó). El owner se setea **al Aprobar**.
- **8.4 Coexistencia:** una cuenta con Diaria + Especial son **dos filas** (alt key por tipo). La app de campo
  debe mostrar ambas sin confundirlas.

---

## 9. Checklist

- [ ] Tabla User/Team owned, primary name autonumber.
- [ ] Alt key `numerocuenta+tipo` **Active**.
- [ ] Choices `tipo`, `estado`, `estadoaprobacion` con defaults.
- [ ] Auditoría activa en agente/owner/tipo/estado.
- [ ] El plugin/flow setea `ownerid` desde `agente.sanic_usuario` (y al reasignar).
- [ ] `sanic_orden` editable por el localizador y **no** mapeado en el import.
- [ ] Especial: Solicitante crea → Supervisor aprueba/asigna → owner queda en el localizador.

---

## 10. Dependencias

- **Depende de:** `sanic_cuenta` (Comp. 08) y `sanic_agente` (Comp. 04) — **tablas que se construyen en
  esta solución** — y la seguridad (Comp. 01).
- **Pendiente (otra fase):** el **plugin/flow de owner** y el **flujo de aprobación de Especiales** tendrán su
  propio markdown de lógica de negocio. La **config del import** de esta tabla va en la Fase 2.
