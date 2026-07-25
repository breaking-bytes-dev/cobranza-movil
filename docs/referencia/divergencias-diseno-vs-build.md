# Divergencias Diseño (docs) ↔ Build (entorno real)

> Surgió al reconciliar los docs con el esquema real (2026-07-25). Campos/comportamientos que **los docs
> asumían y el entorno NO tenía**. Estado tras la decisión "agregar columnas" (2026-07-25).

## ✅ Resueltos — se agregan columnas (siguiendo `sanic_cm_<abrev>_*`)

Los docs ya usan estos nombres nuevos. **Falta crearlas en el portal** →
[guías/12](../guias/12-agregar-columnas-faltantes.md).

| Hueco original | Columna nueva | Tabla | Tipo |
|----------------|---------------|-------|------|
| `sanic_fechainteraccion` (fecha de captura en campo) | `sanic_cm_in_fechacaptura` | interaccion | DateTime |
| `sanic_fechapago` (fecha del pago) | `sanic_cm_pg_fecha` | pago | DateTime |
| `sanic_ultimacarga` (ancla sweep DEC-018) | `sanic_cm_as_ultimacarga` | asignacion | DateTime |
| `fechacierre` (cierre de asignación) | `sanic_cm_as_fechacierre` | asignacion | DateTime |
| `sanic_motivorechazo` (rechazo de Especial) | `sanic_cm_as_motivorechazo` | asignacion | Text |

**Resueltos por reuso / mapeo** (sin columna nueva):
- `sanic_motivocierre` → `sanic_cm_as_motivo` (el campo genérico existente).
- `sanic_supervisorid` → `sanic_cm_as_resolutorid` (quien resuelve la aprobación = el supervisor).

> **Nota offline (el argumento que motivó agregarlas):** `sanic_cm_in_fechacaptura` y `sanic_cm_pg_fecha`
> las **setea la app al crear** (`Now()`), NO `createdon` — porque en offline `createdon` = hora de **sync**
> (servidor), no de captura real en campo.

## ❓ Abiertos

Marcados aún como `⚠️TODO(...)` en los docs. **Decisión 2026-07-25:** los 3 campos especulativos
(`escalavista`, `backofficeid`, `tomadapor`) se **difieren** — NO se agregan hasta que haya necesidad real
(evitar bloat de esquema). Quedan como TODO abierto para retomar si aparece el caso de uso.

| Hueco | Dónde | Pregunta |
|-------|-------|----------|
| `sanic_escalavista` | agente (doc 04/05) | ¿Se usa la escala de accesibilidad del localizador, o era una idea? Si va, sería `sanic_cm_ag_escalavista`. |
| `sanic_backofficeid` | doc 07 | Asignar backoffice a localizador. Estaba como "a futuro". ¿Lo activamos (`sanic_cm_ag_backofficeid`)? |
| `sanic_tomadapor` | doc 08 | ¿Registramos quién tomó la transcripción? Opcional. |
| Agrupar cartera por cliente | doc 04 | **No es columna** — asignacion llega al cliente solo vía `sanic_cm_as_cuentaid.sanic_cm_ct_clienteid`. Se resuelve con `AddColumns` en la app (como ya veníamos haciendo). |
| Valor "Pendiente" del choice | interaccion | **Verificar en el portal** el nombre/valor de la opción inicial en `sanic_cm_choice_estadograbacioninteraccion` (solo tenemos confirmado `'2'=grabada`). |

---

> Buscar `⚠️TODO(` en el repo lista los abiertos que faltan. Al resolver cada uno, quitar su marcador.
