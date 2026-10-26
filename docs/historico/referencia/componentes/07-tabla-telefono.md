# Componente: Tabla `sanic_cm_tbl_telefono`

> **Fase 1 — Modelo de datos · Componente 07 (maestra hija de `cliente`).**
> Los teléfonos del cliente, **normalizados** por tipo (Celular / Casa). Cada fila del CSV trae 2 teléfonos
> → el import crea **una fila por tipo** (ver Fase 2).
>
> Significado: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) §2.

---

## 1. Objetivo

Guardar los teléfonos del cliente de forma normalizada, diferenciados por **tipo**, para que el localizador
pueda contactarlo.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Número | `sanic_cm_tl_numero` | Text | ✔ | **Primary name.** `NumeroCelular` / `NumeroCasa`. |
| Cliente | `sanic_cm_tl_clienteid` | Lookup → `sanic_cm_tbl_cliente` | ✔ | Resuelto por `CIF`. |
| CIF | `sanic_cm_tl_cif` | Text | ✔ | **Copiado** del cliente para la **alt key compuesta**. |
| Tipo | `sanic_cm_tl_tipo` | Text | ✔ | `"Celular"` / `"Casa"` (lo fija el import por mapping). |

---

## 3. Alternate Key

- **Compuesta: `sanic_cm_pk_telefono_cif_tipo`** (sobre `sanic_cm_tl_cif` + `sanic_cm_tl_tipo`). Un teléfono por (cliente, tipo). **Debe quedar `Active`**.

> Mismo patrón que `direccion`: CIF copiado como texto, alt key sobre texto (no sobre lookup).

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cm_tbl_cliente` → `sanic_cm_tbl_telefono` | 1:N | `sanic_cm_tl_clienteid` | **Acá** (lookup). |

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = servicio del import.
- **Lectura:** **Org-level** para todos. **Escritura:** **Solicitante** (Especiales, DEC-013) y **Admin**.
  Ver Comp. 01 §5.

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_telefono` (User/Team owned), Primary name `sanic_cm_tl_numero`.
2. Crear `sanic_cm_tl_clienteid` (Lookup → `sanic_cm_tbl_cliente`), `sanic_cm_tl_cif` (Text), `sanic_cm_tl_tipo` (Text).
3. Crear la **Alternate Key** compuesta `sanic_cm_pk_telefono_cif_tipo` (sobre `sanic_cm_tl_cif` + `sanic_cm_tl_tipo`) y dejarla **`Active`**.

---

## 7. Gotchas

- **7.1** `sanic_cm_tl_numero` es **texto** (no número): puede traer ceros a la izquierda, guiones o prefijos.
- **7.2** El `sanic_cm_tl_tipo` es texto (no Choice), por el mismo motivo que en `direccion` (default por mapping).

---

## 8. Dependencias

- **Depende de:** `sanic_cm_tbl_cliente` (Comp. 02).
- **Habilita:** el contacto del cliente desde la app de campo.
