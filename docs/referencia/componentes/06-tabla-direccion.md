# Componente: Tabla `sanic_direccion`

> **Fase 1 — Modelo de datos · Componente 06 (maestra hija de `cliente`).**
> Las direcciones del cliente, **normalizadas** por tipo (Casa / EC / Trabajo). Cada fila del CSV trae 3
> direcciones → el import crea **una fila por tipo** (ver Fase 2).
>
> Significado: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) §2.

---

## 1. Objetivo

Guardar las direcciones del cliente de forma normalizada, diferenciadas por **tipo**, para que la app de
campo muestre dónde ubicarlo.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Dirección | `sanic_direccion` | Text | ✔ | **Primary name.** `DireccionCasa` / `DireccionEC` / `DireccionTrabajo`. |
| Cliente | `sanic_clienteid` | Lookup → `sanic_cliente` | ✔ | Resuelto por `CIF`. |
| CIF | `sanic_cif` | Text | ✔ | **Copiado** del cliente para la **alt key compuesta**. |
| Tipo | `sanic_tipo` | Text | ✔ | `"Casa"` / `"EC"` / `"Trabajo"` (lo fija el import por mapping). |

> **`EC`** = dirección donde el cliente quiere recibir el **estado de cuenta**.

---

## 3. Alternate Key

- **Compuesta: `sanic_cif` + `sanic_tipo`.** Una dirección por (cliente, tipo). **Debe quedar `Active`**.

> El CIF se **copia como texto** dentro de la dirección; la alt key va sobre **texto**, NO sobre el lookup
> (las alt keys sobre lookup son frágiles — `cobranza-import-config.md` §3). El vínculo real al cliente lo
> arma el Relationship Mapping aparte.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cliente` → `sanic_direccion` | 1:N | `sanic_clienteid` | **Acá** (lookup). |

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = servicio del import.
- **Lectura:** **Org-level** para todos. **Escritura:** **Solicitante** (edita datos del cliente en
  Especiales, DEC-013) y **Admin**. Ver Comp. 01 §5.

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_direccion` (User/Team owned), Primary name `sanic_direccion`.
2. Crear `sanic_clienteid` (Lookup → `sanic_cliente`), `sanic_cif` (Text), `sanic_tipo` (Text).
3. Crear la **Alternate Key** compuesta `sanic_cif + sanic_tipo` y dejarla **`Active`**.

---

## 7. Gotchas

- **7.1** El **`sanic_tipo`** es **texto** (no Choice), a propósito, para que el default por mapping y la alt
  key sean directos (decisión del import).
- **7.2** Si el cliente no existe aún, el import resuelve el lookup por CIF en el orden del DAG (padres
  primero). Fuera del import, el Solicitante debe elegir el cliente.

---

## 8. Dependencias

- **Depende de:** `sanic_cliente` (Comp. 02).
- **Habilita:** la vista de ubicación del cliente en la app de campo.
