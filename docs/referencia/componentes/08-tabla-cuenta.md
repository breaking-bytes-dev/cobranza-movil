# Componente: Tabla `sanic_cm_tbl_cuenta`

> **Fase 1 — Modelo de datos · Componente 08 (maestra hija — el "grano" del modelo).**
> La **cuenta en mora** que se cobra (tarjeta o préstamo). Es la entidad central del cobro: cuelga de
> cliente/producto/agente/fiador y es **padre** de interacciones, asignaciones y cargas diarias.
>
> Significado de cada columna: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) §2.

---

## 1. Objetivo

Mantener el **estado ACTUAL** de cada cuenta en mora (upsert por `NumeroCuenta` → pisa lo anterior). El
**histórico** financiero diario vive en `sanic_cm_tbl_cargadiaria` (Comp. 09).

---

## 2. Esquema de columnas

### Identidad y vínculos
| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Número de cuenta | `sanic_cm_ct_numerodecuenta` | Text | ✔ | **Primary name.** `NumeroCuenta` (tarjeta o préstamo). **Alt key.** |
| Cliente | `sanic_cm_ct_clienteid` | Lookup → `sanic_cm_tbl_cliente` | ✔ | Resuelto por `CIF`. |
| Producto | `sanic_cm_ct_productoid` | Lookup → `sanic_cm_tbl_producto` | — | Resuelto por `CodigoProducto`. |
| Agente | `sanic_cm_ct_agenteid` | Lookup → `sanic_cm_tbl_agente` | — | Resuelto por `Agente`. |
| Fiador | `sanic_cm_ct_fiador` | Lookup → `sanic_cm_tbl_fiador` | — | Resuelto por `NombreFianza`. |

### Montos (Decimal — NO Money, ver gotcha 7.1)
| Nombre físico | Del CSV / descripción |
|---------------|-----------------------|
| `sanic_cm_ct_saldovencidonio` | `SaldoVencidoNIO` — saldo vencido en córdobas. |
| `sanic_cm_ct_saldovencidousd` | `SaldoVencidoUSD` — saldo vencido en dólares. |
| `sanic_cm_ct_totalsaldovencido` | `TotalSaldoVencido` — NIO dolarizado + USD. |
| `sanic_cm_ct_contagiadousd` | `ContagiadoUSD` — saldo total en riesgo de la cuenta. |
| `sanic_cm_ct_pagominimonio` | `PagoMinimoNIO` — pago mínimo en córdobas. |
| `sanic_cm_ct_pagominimousd` | `PagoMinimoUSD` — pago mínimo en dólares. |
| `sanic_cm_ct_retironio` | `RetiroNIO` — **dinero a TRAER** del cliente, en córdobas. ⭐ |
| `sanic_cm_ct_retirousd` | `RetiroUSD` — **dinero a TRAER** del cliente, en dólares. ⭐ |

> ⭐ Los **Retiro** son el **objetivo de cobro** del día para la app de campo.

### Mora, fechas y atributos
| Nombre físico | Tipo | Del CSV / descripción |
|---------------|------|-----------------------|
| `sanic_cm_ct_diasmora` | Whole Number | `DiasMora` — días en mora. |
| `sanic_cm_ct_nivelmora` | Whole Number | `NivelMora` — nivel moratorio. |
| `sanic_cm_ct_fechaultimopago` | DateTime | `FechaUltimoPago` — última vez que pagó. |
| `sanic_cm_ct_fechapago` | DateTime | `FechaPago` — fecha en que le toca pagar. |
| `sanic_cm_ct_ciclo` | Text | `Ciclo` — ciclo de pago. |
| `sanic_cm_ct_tipoasignacion` | Text | `TipoAsignacion` — Diaria / Incobrable / Especial. |
| `sanic_cm_ct_tipoespecial` | Text | `TipoEspecial` — tipo de encargo especial. |
| `sanic_cm_ct_nota` | Text | `Nota` — nota aclaratoria del producto. |

---

## 3. Alternate Key

- **`sanic_cm_pk_ct_numerodecuenta`** (sobre `sanic_cm_ct_numerodecuenta`) — el import upsertea por número de cuenta (estado actual). **Debe quedar `Active`**.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cm_tbl_cliente` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_clienteid` | **Acá.** |
| `sanic_cm_tbl_producto` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_productoid` | **Acá.** |
| `sanic_cm_tbl_agente` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_agenteid` | **Acá.** |
| `sanic_cm_tbl_fiador` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_fiador` | **Acá.** |
| `sanic_cm_tbl_cuenta` → `sanic_cm_tbl_interaccion` | 1:N | `sanic_cm_in_cuentaid` | Comp. 10 |
| `sanic_cm_tbl_cuenta` → `sanic_cm_tbl_asignacion` | 1:N | `sanic_cm_as_cuentaid` | Comp. 12 |
| `sanic_cm_tbl_cuenta` → `sanic_cm_tbl_cargadiaria` | 1:N | `sanic_cm_cd_cuenta` | Comp. 09 |

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = servicio del import.
- **Lectura:** **Org-level** para todos (el padrón se lee completo; la app filtra por asignación).
- **Escritura:** **Solicitante** (crea/edita cuentas para Especiales, DEC-013) y **Admin**. Ver Comp. 01 §5.

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_cuenta` (User/Team owned), Primary name `sanic_cm_ct_numerodecuenta`.
2. Crear los **4 lookups** (cliente, producto, agente, fiador).
3. Crear los **8 montos** como **Decimal** (precisión 2).
4. Crear `diasmora`/`nivelmora` (Whole Number), las 2 fechas (DateTime) y los 4 textos.
5. Crear la **Alternate Key** `sanic_cm_pk_ct_numerodecuenta` (sobre `sanic_cm_ct_numerodecuenta`) y dejarla **`Active`**.
6. Aplicar la seguridad de §5.

---

## 7. Gotchas

- **7.1 Decimal, no Money:** los montos NIO/USD van como **columnas decimales separadas**, no como tipo
  Money, para matchear el CSV sin lógica de currency (decisión del import, `cobranza-import-config.md` §3.4).
- **7.2 Estado actual:** el upsert **pisa** la cuenta cada día. La historia (evolución de mora/saldos) la
  guarda `cargadiaria` (Comp. 09), no esta tabla.
- **7.3 Fechas:** `FechaUltimoPago`/`FechaPago` vienen como texto en el CSV; si el formato no es ISO, el
  import usa `DATEPARSE` (detalle en Fase 2).

---

## 8. Lifecycle / borrado (DEC-023)

- La `cuenta` es referenciada por el histórico operativo (interaccion, pago, asignacion, cargadiaria).
- **Entrante:** `cliente → cuenta` = **`Cascade`** — borrar el cliente borra sus cuentas (DEC-023).
- **Saliente:** `cuenta → interaccion` = **`Restrict`** *(intención, a confirmar al revisar `interaccion`)* —
  si la cuenta tiene interacciones, **bloquea** el borrado (y por ende detiene la cascada desde el cliente).
- **Fin de vida normal = soft:** "fuera de cobranza" se infiere de **no tener asignación activa** (un estado
  explícito en la cuenta es **opcional**, solo si la reportería lo pide). Hard-delete solo deliberado.

---

## 9. Dependencias

- **Depende de:** `cliente` (02), `producto` (03), `agente` (04), `fiador` (05).
- **Habilita:** `interaccion` (10), `asignacion` (12), `cargadiaria` (09) y la **cartera** de la app de campo.
