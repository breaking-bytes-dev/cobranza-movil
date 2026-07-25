# Componente: Tabla `sanic_cm_tbl_pago`

> **Fase 1 — Modelo de datos · Componente 11 (operativa).**
> Un pago cobrado al cliente, **hijo de una interacción**. Soporta pago **mixto** (NIO + USD) como dos pagos.
>
> Materializa: **DEC-003** (pago en papel: se anota monto + nº de recibo físico), **DEC-010** (entidad
> separada, 1:N con la interacción), **DEC-015** (User-level).

---

## 1. Objetivo

Registrar el/los **pago(s)** asociados a una interacción: **monto**, **moneda** (NIO/USD) y el **número del
recibo físico** que el localizador entrega del talonario. El recibo **sigue siendo de papel** (DEC-003); acá
solo se **anota**.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Quién lo llena / descripción |
|---------------|---------------|------|:----:|------------------------------|
| Número | `sanic_cm_pg_numero` | Autonumber (`PAG-{SEQNUM:6}`) | auto | **Primary name.** Server-assigned al sincronizar. |
| Interacción | `sanic_cm_pg_interaccion` | Lookup → `sanic_cm_tbl_interaccion` | ✔ | localizador — el pago **pertenece** a una interacción. |
| Monto | `sanic_cm_pg_monto` | Decimal (2 dec.) | ✔ | localizador — monto cobrado. |
| Moneda | `sanic_cm_pg_moneda` | Choice NIO / USD | ✔ | localizador — moneda del pago. |
| Número de recibo | `sanic_cm_pg_numerorecibo` | Text | ✔ | localizador — **número del recibo físico** (talonario). |
| Fecha del pago | `sanic_cm_pg_fecha` | DateTime | ✔ | localizador (auto al registrar). **Sin equivalente en el build.** |
| Propietario | `ownerid` | Owner (system) | ✔ | = el **localizador** (creador). User-level. |

> **Decimal, no Money** — consistente con la decisión del import de NO usar el tipo Money para NIO/USD
> (ver `cobranza-import-config.md` §3.4). Cada pago lleva **su** moneda en `sanic_cm_pg_moneda`.

---

## 3. Alternate Key

- **Ninguna** por ahora. Se crea en la app (GUID). El `sanic_cm_pg_numerorecibo` es del **talonario físico**; si el
  negocio garantiza su unicidad, se podría agregar una alt key/constraint más adelante (ver gotcha 6.2).

---

## 4. Relaciones

| Relación | Cardinalidad | Columna |
|----------|:------------:|---------|
| `sanic_cm_tbl_interaccion` → `sanic_cm_tbl_pago` | **1:N** | `sanic_cm_pg_interaccion` |

> El **1:N** es lo que permite el **pago mixto**: una interacción con un pago en NIO **y** otro en USD
> (DEC-010).

---

## 5. Choices

**`sanic_cm_pg_moneda`** — choice `sanic_choice_cm_monedaenpago`:
| Valor | Código |
|-------|:------:|
| NIO (Córdoba) | 1 |
| USD (Dólar) | 2 |

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_pago`, **User/Team owned**.
2. Primary name = **Autonumber** `PAG-{SEQNUM:6}`.
3. Crear columnas de §2; `sanic_cm_pg_monto` Decimal (precisión 2).
4. Crear el Choice `sanic_choice_cm_monedaenpago` (§5).
5. Crear el lookup `sanic_cm_pg_interaccion` (**requerido**) con comportamiento **Parental** o referencial según
   se decida el borrado en cascada (recomendado: referencial, para no perder pagos si se reabre la lógica).
6. **Regla de negocio** (su md): al crear un pago, marcar la interacción padre `sanic_cm_cd_aplicapago = Sí`.

---

## 7. Gotchas

- **6.1 / 7.1 Heredar el owner:** el pago lo crea el localizador → owner = él. Asegurar que en la app el
  owner del pago coincida con el de la interacción padre (ambos del mismo localizador).
- **7.2 Unicidad del recibo:** el `sanic_cm_pg_numerorecibo` viene del **talonario físico**. Hoy se guarda como
  texto sin constraint. Si se quisiera evitar duplicados, evaluar una alt key — pero ojo con talonarios por
  localizador (el mismo número puede repetirse entre talonarios distintos → alt key debería ser
  `localizador + numerorecibo`). **No** se implementa ahora; queda anotado.
- **7.3 Offline:** igual que la interacción, el `sanic_cm_pg_numero` se asigna al **sincronizar**.

---

## 8. Checklist

- [ ] Tabla creada User/Team owned, primary name autonumber.
- [ ] Lookup requerido a `sanic_cm_tbl_interaccion` (1:N).
- [ ] Choice `sanic_choice_cm_monedaenpago` (NIO/USD).
- [ ] Una interacción admite **varios** pagos (probar NIO + USD).
- [ ] El localizador solo ve **sus** pagos (User-level).

---

## 9. Dependencias

- **Depende de:** `sanic_cm_tbl_interaccion` (Comp. 10), seguridad (Comp. 01).
- **Habilita:** estadísticas de **plata recuperada** por moneda + dolarizada (DEC-011) y, a futuro, el recibo
  digital (DEC-003/007).
