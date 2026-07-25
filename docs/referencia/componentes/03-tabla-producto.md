# Componente: Tabla `sanic_cm_tbl_producto`

> **Fase 1 — Modelo de datos · Componente 03 (maestra padre / catálogo).**
> Catálogo del **producto en mora** (tarjeta o préstamo). La puebla el import por `CodigoProducto`.
>
> Significado: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) §2.

---

## 1. Objetivo

Clasificar el producto que está en mora, para reportes y agrupaciones. Es **padre** de `cuenta`.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Código | `sanic_cm_pr_codigo` | Text | ✔ | **Primary name.** `CodigoProducto` — código del producto. **Alt key.** |
| Tipo | `sanic_cm_pr_tipo` | Text | — | `TipoProducto` — tipo de producto en mora. |

---

## 3. Alternate Key

- **`sanic_cm_pk_producto_codigo`** (sobre `sanic_cm_pr_codigo`) — el import upsertea por código. **Debe quedar `Active`**.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cm_tbl_producto` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_productoid` | Comp. 08 |

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = servicio del import.
- **Lectura:** **Org-level** para todos. **Escritura:** solo **Admin** (es un catálogo). Ver Comp. 01 §5.

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_producto` (User/Team owned), Primary name `sanic_cm_pr_codigo`.
2. Crear `sanic_cm_pr_tipo` (Text).
3. Crear la **Alternate Key** `sanic_cm_pk_producto_codigo` (sobre `sanic_cm_pr_codigo`) y dejarla **`Active`**.

---

## 7. Gotchas

- **7.1** Sin la alt key `sanic_cm_pk_producto_codigo` **Active**, el import del producto falla (`TargetData`).

---

## 8. Dependencias

- **Depende de:** nada (catálogo raíz).
- **Habilita:** cuenta (08).
