# Fase 2 — Configuración del Import (Smelter)

> **Objetivo:** configurar Smelter para poblar las tablas del modelo desde el **CSV diario**.
> Este doc cubre lo **nuevo y lo reconciliado** de la Fase 2; el detalle ya trabajado de los mappings de las
> tablas maestras está en [`../cobranza-import-config.md`](../referencia/cobranza-import-config.md) (base de referencia).
>
> Modelo de tablas: [`componentes/`](../referencia/componentes). Diccionario del CSV:
> [`01-import-y-diccionario-de-datos.md`](../desarrollo/01-import-y-diccionario-de-datos.md).

---

## 1. Qué puebla el import (y qué NO)

| Puebla por CSV (Smelter) | NO se importa (las crea la app) |
|--------------------------|----------------------------------|
| `cliente`, `producto`, `agente`, `fiador` | `interaccion` |
| `direccion` (x3), `telefono` (x2) | `pago` |
| `cuenta` | `actualizaciondatos` |
| `cargadiaria` (snapshot) | |
| **`asignacion`** (Diaria/Incobrable) ← **NUEVO** | |

> `agente.sanic_cm_ag_usuarioid` **no** lo pone el import (lo setea el Admin, Comp. 04). Las **asignaciones
> Especiales** **no** vienen por CSV (nacen en la app, DEC-013).

---

## 2. Estado: base + lo nuevo

- ✅ **Ya documentado** en `cobranza-import-config.md`: Table Mappings #1–#11 (maestras + `cuenta` +
  `cargadiaria`), sus Column/Relationship Mappings, alt keys y advertencias.
- 🟡 **Reconciliado:** rename `cuentamora` → `cuenta` (aplicado); `FechaCarga` confirmado como **columna del
  CSV** (no `createdon`).
- 🆕 **Nuevo (este doc):** Table Mapping de **`sanic_cm_tbl_asignacion`** + manejo del `tipo` Choice.

---

## 3. Nuevo Table Mapping #12 — `sanic_cm_tbl_asignacion`

Puebla la asignación operativa **Diaria/Incobrable** desde el CSV (las Especiales **no** pasan por acá).

| Propiedad | Valor |
|-----------|-------|
| Target Table | `sanic_cm_tbl_asignacion` |
| Processing Order | **4** (después de `cuenta` y `agente`; junto a `cargadiaria`) |
| Dedup / Alt key | **`sanic_cm_as_numerocuenta` + `sanic_cm_as_tipo`** |

### Column Mappings
| CSV | → Columna | Notas |
|-----|-----------|-------|
| `NumeroCuenta` | `sanic_cm_as_numerocuenta` | Copiado (texto) para la alt key. |
| `TipoAsignacion` | `sanic_cm_as_tipo` | **Texto → Choice** (ver §4). |
| — | `sanic_cm_as_estado` | **Default `Activa`** (no viene del CSV). El upsert la re-activa si reaparece (DEC-018). |
| `FechaCarga` | `sanic_cm_as_ultimacarga` | Marca la última carga en que la cuenta vino → base del **sweep** (DEC-018, §9). |
| — | `sanic_cm_as_fechasignacion` | Default = fecha del archivo (o `FechaCarga`), a definir en config. |
| `sanic_cm_as_orden` | — | **NO se mapea** (no pisar la prioridad del localizador, DEC-012). |

### Relationship Mappings (resuelven los lookups por valor de negocio)
| Parent | Lookup en asignacion | Dedup del parent (CSV) |
|--------|----------------------|------------------------|
| `cuenta` | `sanic_cm_as_cuentaid` | `NumeroCuenta` |
| `agente` | `sanic_cm_as_agenteid` | `Agente` |

> El **owner** de la asignación **NO** lo pone Smelter: lo setea un **plugin/flow** en Create/Update desde
> `agente.sanic_cm_ag_usuarioid` (Comp. 01 §6, Comp. 12). Es lógica de negocio, no import.

---

## 4. Manejo del `tipo` (Choice) en el import

`sanic_cm_as_tipo` es un **Choice** (Diaria=1 / Incobrable=2 / Especial=3), válido en la alt key
(`PicklistAttributeMetadata` está soportado — [doc MS](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/define-alternate-keys-entity)).
El CSV trae **texto**, así que el upsert necesita el **valor**:

- **Traducción `TipoAsignacion` → valor:** `Diaria→1`, `Incobrable→2`, `Especial→3`.
- Implementación: mapeo por **label** del Choice si Smelter lo soporta, o un **Transform Expression** que
  devuelva el valor. **A verificar la capacidad de Smelter** con choices (no hay precedente en la config
  actual, que usa Text para los `tipo` de direccion/telefono).
- **Fallback si Smelter no mapea choices:** mantener una columna técnica o, en última instancia, evaluar
  `tipo` como Text (revierte parte de Comp. 12). Solo si la traducción no fuera viable.

---

## 5. `FechaCarga` para `cargadiaria`

- **Columna del CSV** (decidido): el sistema origen agrega `FechaCarga` por fila. **No** se usa `createdon`
  (rompe idempotencia y desfecha en reproceso/backfill).
- Si el formato no es ISO, usar `DATEPARSE(FechaCarga, "dd/MM/yyyy")` en el Column Mapping.

---

## 6. Orden de procesamiento (DAG) — actualizado

```
 Orden 1 (padres):   cliente · producto · agente · fiador
 Orden 2 (hijas):    direccion (x3) · telefono (x2)
 Orden 3:            cuenta
 Orden 4:            cargadiaria · asignacion   ← ambas dependen de cuenta/agente
```

---

## 7. Checklist de Alternate Keys (todas en `Active` antes de importar)

| Tabla | Alt key |
|-------|---------|
| `cliente` | `sanic_cm_pk_cliente_cif` |
| `producto` | `sanic_cm_pk_producto_codigo` |
| `agente` | `sanic_cm_pk_agente_nombre` |
| `fiador` | `sanic_cm_pk_fi_nombre` |
| `direccion` | `sanic_cm_pk_direccion_cif_tipo` |
| `telefono` | `sanic_cm_pk_telefono_cif_tipo` |
| `cuenta` | `sanic_cm_pk_ct_numerodecuenta` |
| `cargadiaria` | `sanic_cm_pk_cd_numerocuenta_fechacarga` |
| **`asignacion`** | **`sanic_cm_as_numerocuenta` + `sanic_cm_as_tipo`** ← nueva |

> ⚠️ **Regla de oro:** alt key faltante o no-`Active` → el import de esa tabla falla con `TargetData`.
> Máximo **10 alt keys por tabla** y el valor **no** debe contener `/ < > * % & : \ ? +` (rompe el upsert).

---

## 8. Casos borde a resolver

- **8.1 `Especial` en el CSV — RESUELTO (DEC-022):** si el CSV trae `TipoAsignacion = Especial`, el import la
  crea como **`Pendiente`** (NO pre-aprobada) → va a la aprobación del **Supervisor**. `creadaporid` =
  servicio de import (origen "CSV"). El import setea `estadoaprobacion = Pendiente` **solo al crear**; un
  **re-import NO** revierte una ya aprobada. *(Por eso, con el mock, `asignacion` = 1000: ~47 quedan `Pendiente`.)*
- **8.2 Capacidad de choice en Smelter (§4):** confirmar antes de cerrar el mapping de `tipo`.
- **8.3 Caracteres en alt keys (§7):** validar `NumeroCuenta` / `CIF` / códigos.

---

## 9. Procesos post-import (reconciliación, reclasificación y retención)

> El import **upsertea, nunca borra**. Estos procesos completan el ciclo de vida del dato.

### 9.0 Reclasificación de tipo — automática (DEC-021)

`Diaria` e `Incobrable` son **mutuamente excluyentes** (clasificación del core); `Especial` es ortogonal.
Como la alt key es `numerocuenta + tipo`, un cambio de core-type crearía una fila nueva y dejaría la vieja
activa. Por eso, al procesar el import, una lógica **automática** (junto al plugin de owner): cuando una
cuenta llega con un core-type, **cierra su otro core-type activo** (`estado = Cerrada`,
motivo *"Reclasificada a {tipo}"*). **Automática y segura** porque es **presence-driven** (no la engaña un
archivo truncado). **Nunca** toca una `Especial`.

### 9.1 Reconciliación de Diarias desaparecidas — función del Admin (DEC-018)

Una cuenta **`Diaria`** que deja de venir en la carga quedaría `Activa` colgada del localizador. Para sacarla,
**el Admin ejecuta una función en la PowerApp** (NO es automática):

1. El import setea **`sanic_cm_as_ultimacarga = FechaCarga`** y `estado = Activa` en **cada upsert** de `asignacion`.
2. El **Admin abre la función**. Calcula candidatas (`Diaria` + `Activa` + `ultimacarga < última carga`) y
   muestra **preview**: cantidad, **% sobre Diarias activas** y fecha de carga.
3. **Umbral de cordura visible:** si el % supera el umbral (ej. >20–30%), lo marca en rojo ("archivo
   posiblemente truncado"). El Admin decide.
4. El Admin **confirma** → `estado = Cerrada`, `motivocierre = "Fuera de cartera..."`, `fechacierre`.
5. **Precondición:** solo habilita si el **último import quedó Completado**.
6. **Solo `Diaria`.** `Incobrable` = cierre manual (DEC-012); `Especial` = flujo de aprobación.
7. **Reaparición:** si la cuenta vuelve, el upsert la **re-activa**; el Admin/Supervisor también puede reabrir.

### 9.2 Retención y limpieza (DEC-019)

- **`cargadiaria`** crece **1 fila/cuenta/día** (el que crece rápido). **`asignacion`** crece lento (upsert).
- **Política:** **Bulk Deletion** recurrente (nativo) → borrar `cargadiaria` con `fechacarga < hoy − N`
  (ventana **N a definir**, ej. 6–12 meses). Opcional: bulk-delete de `asignacion` `Cerradas` >1 año.
- **Tradeoff:** el histórico más viejo que `N` **se pierde** (no se archiva). Si luego se necesita histórico
  largo → agregar **Long-Term Retention / Synapse Link** (offload a data lake).

---

## 10. Validación en ambiente (pasos)

1. Activar **todas** las alt keys (§7) y confirmar estado `Active`.
2. Cargar el **día 1** (`test-data/cobranza-mock-1000.csv`).
3. Verificar: maestras pobladas, `cuenta` con sus lookups, `cargadiaria` con 1 fila/cuenta, `asignacion`
   con `tipo` correcto, `estado = Activa` y `ultimacarga = 2026-06-20`.
4. Verificar el **owner** de `asignacion` (lo setea el plugin/flow) = el localizador esperado.
5. **Reimportar el día 1** → confirmar **idempotencia** (no duplica; mismo conteo).
6. Cargar el **día 2** (`...-dia2.csv`) → `cuenta` sigue en 1000 (pisa), `cargadiaria` en 2000 (acumula),
   ~75 reasignaciones. Correr el **sweep** → las cuentas del día 1 ausentes en el día 2 quedan `Cerrada`.
7. Revisar el **Import Job Log** por filas fallidas y su categoría.

---

## 11. Dependencias y siguiente

- **Depende de:** el modelo de datos (Fase 1) construido y las alt keys activas.
- **Pendiente (lógica de negocio, otra fase):** el **plugin/flow de owner** de `asignacion` y el **flujo de
  aprobación de Especiales**.
- **Siguiente fase:** App de Oficina (Backoffice/Admin) — Fase 3.
