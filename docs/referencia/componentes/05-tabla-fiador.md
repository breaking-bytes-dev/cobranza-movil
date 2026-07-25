# Componente: Tabla `sanic_cm_tbl_fiador`

> **Fase 1 — Modelo de datos · Componente 05 (maestra padre / catálogo).**
> El **fiador** (fianza) del producto en mora. La puebla el import por `NombreFianza`.
>
> Significado: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) §2.

---

## 1. Objetivo

Registrar la fianza asociada a un producto en mora (nombre + dirección). Es **padre** de `cuenta`.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Nombre | `sanic_cm_fi_nombre` | Text | ✔ | **Primary name.** `NombreFianza` — nombre de la fianza. **Alt key.** |
| Dirección | `sanic_cm_fi_direccion` | Text | — | `DireccionFianza` — dirección de la fianza. |

---

## 3. Alternate Key

- **`sanic_cm_pk_fi_nombre`** (sobre `sanic_cm_fi_nombre`) — el import upsertea por nombre. **Debe quedar `Active`**.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cm_tbl_fiador` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_fiador` | Comp. 08 |

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = servicio del import.
- **Lectura:** **Org-level** para todos. **Escritura:** solo **Admin**. Ver Comp. 01 §5.

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_fiador` (User/Team owned), Primary name `sanic_cm_fi_nombre`.
2. Crear `sanic_cm_fi_direccion` (Text).
3. Crear la **Alternate Key** `sanic_cm_pk_fi_nombre` (sobre `sanic_cm_fi_nombre`) y dejarla **`Active`**.

---

## 7. Gotchas

- **7.1** Sin la alt key `sanic_cm_pk_fi_nombre` **Active**, el import del fiador falla (`TargetData`).
- **7.2** Si dos fianzas distintas comparten nombre exacto, el upsert las **colapsa** en una. Validar con el
  área si `NombreFianza` es único; si no, habría que repensar la clave.

---

## 8. Dependencias

- **Depende de:** nada (catálogo raíz).
- **Habilita:** cuenta (08).
