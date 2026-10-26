# Componente: Tabla `sanic_cm_tbl_cliente`

> **Fase 1 — Modelo de datos · Componente 02 (maestra padre).**
> Tabla que se **construye desde cero**. Representa al **cliente (deudor)**. La puebla el **import** por el
> campo `CIF`. Es **padre** de cuentas, direcciones y teléfonos.
>
> Significado de negocio: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) §2.
> Config del import (mappings) → Fase 2 / [`../../cobranza-import-config.md`](../cobranza-import-config.md).

---

## 1. Objetivo

Centralizar la identidad del **cliente** y sus datos generales. Todo lo demás (cuentas, direcciones,
teléfonos) cuelga de acá por relación 1:N.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Nombre | `sanic_cm_cl_nombre` | Text | ✔ | **Primary name.** `Cliente` — nombre del cliente. |
| CIF | `sanic_cm_cl_cif` | Text | ✔ | `CIF` — **identificador único interno**. **Alt key.** |
| Departamento | `sanic_cm_cl_departamento` | Text | — | `Departamento` — ubicación geográfica del cliente. |
| Distrito | `sanic_cm_cl_distrito` | Text | — | `Distrito` — distrito donde **trabaja** el cliente. |
| Trabajo | `sanic_cm_cl_trabajo` | Text | — | `Trabajo` — nombre del trabajo del cliente. |

---

## 3. Alternate Key

- **`sanic_cm_pk_cliente_cif`** (sobre `sanic_cm_cl_cif`) — clave de negocio. El import upsertea el cliente por CIF. **Debe quedar `Active`** antes
  de importar (sin ella, error `TargetData` — regla de oro del import).

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cm_tbl_cliente` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_clienteid` | Comp. 08 |
| `sanic_cm_tbl_cliente` → `sanic_cm_tbl_direccion` | 1:N | `sanic_cm_dr_clienteid` | Comp. 06 |
| `sanic_cm_tbl_cliente` → `sanic_cm_tbl_telefono` | 1:N | `sanic_cm_tl_clienteid` | Comp. 07 |
| `sanic_cm_tbl_cliente` → `sanic_cm_tbl_cargadiaria` | 1:N (opcional) | `sanic_cm_cd_clienteid` | Comp. 09 |

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = **cuenta de servicio del import** (irrelevante para la lectura).
- **Lectura:** **Org-level** para todos los roles (el padrón se lee completo; la app filtra la cartera).
- **Escritura:** solo **Solicitante** (para Especiales, DEC-013) y **Admin**. Ver matriz Comp. 01 §5.

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_cliente` (User/Team owned), Primary name `sanic_cm_cl_nombre`.
2. Crear las columnas de §2 (todas Text).
3. Crear la **Alternate Key** `sanic_cm_pk_cliente_cif` (sobre `sanic_cm_cl_cif`) y dejarla **`Active`**.
4. Aplicar la seguridad de §5 (al cargar la matriz en Comp. 01).

---

## 7. Gotchas

- **7.1** Sin la alt key `sanic_cm_pk_cliente_cif` **Active**, el import de cliente falla (`TargetData`).
- **7.2** El **Solicitante** puede crear clientes a mano (Especiales). Cuidar **colisión de CIF** con el
  upsert del import (DEC-013): un cliente creado con CIF correcto se **fusiona**; con CIF mal, **duplica**.

---

## 8. Dependencias

- **Depende de:** nada (tabla raíz).
- **Habilita:** cuenta (08), `sanic_cm_tbl_direccion` (06), `sanic_cm_tbl_telefono` (07), cargadiaria (09).
