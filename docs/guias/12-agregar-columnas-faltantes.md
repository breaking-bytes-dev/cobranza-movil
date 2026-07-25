# Guía — Agregar las columnas faltantes (5)

> ✅ **YA CREADAS (2026-07-25)** vía el workflow `add-columns-oneshot` (Web API + Service Principal),
> verificadas por GET. Este doc queda como **referencia del método manual (portal)** y del **cableado
> pendiente** en la app/sweep. Respetá el naming EXACTO — los docs y fórmulas ya lo usan.

## 📋 Precondición
- [ ] Estás en **make.powerapps.com**, entorno correcto, **solución `cm_core_cobranzamovil` abierta**.

## 🧱 Las 5 columnas

| # | Tabla | Nombre (Schema/Logical) | Tipo | Requerida | Notas |
|---|-------|-------------------------|------|:--------:|-------|
| 1 | `sanic_cm_tbl_interaccion` | `sanic_cm_in_fechacaptura` | **Fecha y hora** | No | Fecha/hora de la gestión en campo |
| 2 | `sanic_cm_tbl_pago` | `sanic_cm_pg_fecha` | **Fecha y hora** | No | Fecha real del pago |
| 3 | `sanic_cm_tbl_asignacion` | `sanic_cm_as_ultimacarga` | **Fecha y hora** | No | Ancla del sweep DEC-018 |
| 4 | `sanic_cm_tbl_asignacion` | `sanic_cm_as_fechacierre` | **Fecha y hora** | No | Cuándo se cerró |
| 5 | `sanic_cm_tbl_asignacion` | `sanic_cm_as_motivorechazo` | **Texto** (multilínea) | No | Motivo de rechazo de Especial |

> **Formato de fecha:** para 1 y 2 usá **"Fecha y hora"** (no solo fecha) — importa la hora. Zona horaria:
> **"Solo fecha"/"Independiente de zona"** según cómo manejes el resto; sé consistente con las fechas ya
> existentes de la solución.

## 🖱️ Pasos (por cada columna)

1. **Tablas** → abrí la tabla del cuadro → pestaña **Columnas** → **+ Nueva columna**.
2. **Nombre para mostrar:** poné algo legible (ej. *"Fecha de captura"*, *"Motivo de rechazo"*).
3. **⚠️ Nombre (Schema):** expandí **Opciones avanzadas** y **forzá el nombre lógico EXACTO** de la tabla
   (ej. `sanic_cm_in_fechacaptura`). El portal propone uno por el display — **corregilo** para que sea el de
   arriba, sin sufijos raros. El prefijo del publisher es `sanic_`; el resto (`cm_in_fechacaptura`) lo
   tipeás vos.
4. **Tipo de datos:** el de la tabla (Fecha y hora / Texto).
5. **Guardar.**
6. Repetí para las 5. **Publicar todo** al final.

## 🔌 Después de crearlas — cablearlas

- **App Canvas (localizador):** en el `Patch` de creación de interacción, agregá
  `sanic_cm_in_fechacaptura: Now()`; en el de pago, `sanic_cm_pg_fecha: Now()`. **Así se captura la hora de
  campo aunque esté offline** (a diferencia de `createdon`, que es la hora de sync).
- **Sweep DEC-018 (servidor):** el proceso de import setea `sanic_cm_as_ultimacarga` cada vez que la Diaria
  aparece en el CSV; el sweep marca `sanic_cm_as_fechacierre` cuando cierra las que no vinieron.
- **`sanic_cm_as_motivorechazo`:** lo llena el Supervisor al rechazar una Especial (campo del formulario).

## ✅ Verificar
- [ ] Las 5 columnas aparecen en sus tablas con el **nombre lógico exacto** (revisá que no sea
      `sanic_cm_in_fechacaptura1` ni similar).
- [ ] Un `pac solution export` + `unpack` (o el workflow `solution-release`) las muestra en
      `solutions/unpacked/Entities/...`.
- [ ] Ninguna fórmula/flujo que ya las referencia tira "columna no encontrada".

> Cuando estén creadas, actualizá [nomenclatura-real §6](../referencia/nomenclatura-real.md) (movelas de
> "a agregar" al esquema real) y [divergencias](../referencia/divergencias-diseno-vs-build.md).
