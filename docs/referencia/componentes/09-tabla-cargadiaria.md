# Componente: Tabla `sanic_cargadiaria`

> **Fase 1 — Modelo de datos · Componente 09 (maestra — snapshot histórico diario).**
> Guarda una **foto diaria** de cada cuenta en mora (montos, mora, agente del día). Mientras `sanic_cuenta`
> guarda el **estado actual** (se pisa), `cargadiaria` guarda la **historia**.
>
> Contexto: [`../01-import-y-diccionario-de-datos.md`](../../desarrollo/01-import-y-diccionario-de-datos.md) y
> [`../../cobranza-import-config.md`](../cobranza-import-config.md) §4.

---

## 1. Objetivo

Responder preguntas históricas: *"¿qué agente la tenía el día X?"*, *"¿cómo evolucionó la mora/saldo?"*,
*"¿cuántas veces se cargó la cuenta?"*. Un registro por **(cuenta, día)**.

---

## 2. Esquema de columnas (completo)

### Identidad y vínculos
| Nombre lógico | Nombre físico | Tipo | Req. | Del CSV / descripción |
|---------------|---------------|------|:----:|-----------------------|
| Número de cuenta | `sanic_numerocuenta` | Text | ✔ | **Primary name.** `NumeroCuenta` (copiado para la alt key). |
| Fecha de carga | `sanic_fechacarga` | DateTime (Date Only) | ✔ | **`FechaCarga`** — fecha del día. **Columna NUEVA en el CSV** (ver gotcha 8.1). |
| Cuenta | `sanic_cuentaid` | Lookup → `sanic_cuenta` | ✔ | Resuelto por `NumeroCuenta`. |
| Cliente | `sanic_clienteid` | Lookup → `sanic_cliente` | — | Resuelto por `CIF` (opcional, para reportes por cliente). |
| Agente | `sanic_agenteid` | Lookup → `sanic_agente` | — | Resuelto por `Agente` — **quién la tenía ese día**. |
| Tipo asignación | `sanic_tipoasignacion` | Text | — | `TipoAsignacion` del día (Diaria/Incobrable/Especial). |

### Montos del día (Decimal — NO Money, mismos 8 que `sanic_cuenta`)
| Nombre lógico | Nombre físico | Tipo | Del CSV |
|---------------|---------------|------|---------|
| Saldo vencido NIO | `sanic_saldovencidonio` | Decimal | `SaldoVencidoNIO` |
| Saldo vencido USD | `sanic_saldovencidousd` | Decimal | `SaldoVencidoUSD` |
| Total saldo vencido | `sanic_totalsaldovencido` | Decimal | `TotalSaldoVencido` |
| Contagiado USD | `sanic_contagiadousd` | Decimal | `ContagiadoUSD` |
| Pago mínimo NIO | `sanic_pagominimonio` | Decimal | `PagoMinimoNIO` |
| Pago mínimo USD | `sanic_pagominimousd` | Decimal | `PagoMinimoUSD` |
| Retiro NIO | `sanic_retironio` | Decimal | `RetiroNIO` |
| Retiro USD | `sanic_retirousd` | Decimal | `RetiroUSD` |

### Mora del día
| Nombre lógico | Nombre físico | Tipo | Del CSV |
|---------------|---------------|------|---------|
| Días de mora | `sanic_diasmora` | Whole Number | `DiasMora` |
| Nivel de mora | `sanic_nivelmora` | Whole Number | `NivelMora` |

> **Por qué un subconjunto de `sanic_cuenta` y no todo:** `cargadiaria` historia lo que **cambia día a día**
> (saldos, mora, agente, tipo). Los datos **estables** del cliente (nombre, direcciones, teléfonos, producto,
> fiador) **no** se duplican acá — se consultan por el lookup a `sanic_cuenta`/`sanic_cliente`.

---

## 3. Alternate Key

- **Compuesta: `sanic_numerocuenta` + `sanic_fechacarga`.** Un registro por cuenta por día; mismo día
  re-subido = upsert (no duplica). **Debe quedar `Active`**.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde |
|----------|:------------:|---------|-------|
| `sanic_cuenta` → `sanic_cargadiaria` | 1:N | `sanic_cuentaid` | **Acá.** |
| `sanic_cliente` → `sanic_cargadiaria` | 1:N | `sanic_clienteid` | **Acá** (opcional). |
| `sanic_agente` → `sanic_cargadiaria` | 1:N | `sanic_agenteid` | **Acá.** |

---

## 5. Proceso de carga diaria (cómo se puebla)

`cargadiaria` **NO** es un proceso aparte: se puebla con el **mismo import diario** (Smelter), como **una
tabla destino más** (Table Mapping #11). El detalle fino de los mappings va en la **Fase 2**; acá el proceso:

```
 Admin sube CSV (con FechaCarga) ─► Import Job = Queued ─► Smelter (cloud flow)
        │
        ├─ orden 1: cliente, producto, agente, fiador   (padres)
        ├─ orden 2: direccion, telefono                  (hijas de cliente)
        ├─ orden 3: sanic_cuenta                          (resuelve sus lookups)
        └─ orden 4: sanic_cargadiaria  ◄── ACÁ           (necesita que la cuenta ya exista)
                     │
                     └─ por cada fila del CSV → 1 registro (clave numerocuenta + fechacarga)
                        copiando el snapshot del día (8 montos, mora, agente, tipo)
```

**Secuencia:**
1. **Prerrequisito:** el CSV del día **debe traer la columna `FechaCarga`** (la fecha del archivo, puesta por
   el sistema origen). Sin ella, no hay forma de fechar el snapshot.
2. **Disparo:** el Admin sube el CSV y crea un Import Job (→ `Queued`); el flujo de Smelter arranca, igual que
   para las demás tablas. **Es el mismo job**, no uno separado.
3. **Orden (DAG):** se procesa **después de `sanic_cuenta`** (orden 4), porque su lookup `sanic_cuentaid` se
   resuelve por `NumeroCuenta` y la cuenta ya tiene que existir.
4. **Por cada fila del CSV:** Smelter **crea o actualiza** un registro con clave `numerocuenta + fechacarga`,
   copiando el snapshot financiero del día.
5. **Idempotencia:** resubir el **mismo** archivo (misma `FechaCarga`) → upsert, no duplica. Un archivo de
   **otro día** (nueva `FechaCarga`) → filas nuevas; la historia **se acumula**.

**Resolución de vínculos** (por valor de negocio, no por GUID):
`sanic_cuentaid` ← `NumeroCuenta` · `sanic_agenteid` ← `Agente` · `sanic_clienteid` ← `CIF` (opcional).

**Cobertura por tipo de asignación** (sutileza clave):
| Tipo | ¿Genera snapshot diario? |
|------|--------------------------|
| **Diaria** | **Sí, todos los días** (viene siempre en el CSV). |
| **Incobrable** | **Solo los días que aparece** en el CSV (1–2 veces/mes) → historia **esparsa**. Esperado. |
| **Especial** | **No** — no viene por CSV. Si se quisiera historiar, sería otro mecanismo (a decidir, no hoy). |

---

## 6. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = servicio del import.
- **Lectura:** **Org-level** (lo usan dashboards/reportes de oficina). **Escritura:** solo **Admin/import**.
  Ver Comp. 01 §5.

---

## 7. Pasos de construcción

1. Crear la tabla `sanic_cargadiaria` (User/Team owned), Primary name `sanic_numerocuenta`.
2. Crear `sanic_fechacarga` (DateTime, **Date Only**).
3. Crear los lookups (cuenta, cliente opcional, agente) y `sanic_tipoasignacion` (Text).
4. Crear los **8 montos** (Decimal, precisión 2) y `sanic_diasmora` / `sanic_nivelmora` (Whole Number).
5. Crear la **Alternate Key** compuesta `sanic_numerocuenta + sanic_fechacarga` y dejarla **`Active`**.
6. *(Fase 2)* configurar el Table Mapping #11 de Smelter (orden 4) — ver §5.

---

## 8. Gotchas

- **8.1 Requiere `FechaCarga` en el CSV:** sin esa columna (puesta por el sistema origen) **no hay forma de
  fechar el snapshot**. Es un **requisito al área que genera el archivo** (`cobranza-import-config.md` §4). Si
  el formato no es ISO, el import usa `DATEPARSE` en el Column Mapping (Fase 2).
- **8.2 Snapshot completo, no condicional:** en cobranza `DiasMora` sube +1 cada día y los saldos varían →
  casi nunca dos días traen lo mismo. Por eso se guarda el snapshot **completo** diario (no "solo si cambió").
- **8.3 No confundir con `asignacion`:** `cargadiaria` es el **histórico financiero** (se acumula por día);
  `sanic_asignacion` (Comp. 12) es la **asignación viva** (estado actual + orden del localizador). Son cosas
  distintas a propósito (DEC-005, DEC-016).
- **8.4 Especiales y el snapshot:** una Especial **del CSV** (DEC-022) **sí** genera snapshot el día que viene;
  una Especial **creada en la app** (Solicitante) **no** tiene snapshot (no está en el CSV). Tenerlo presente
  en los reportes de evolución de mora.
- **8.5 Crecimiento — el que más crece:** 1 fila/cuenta/día → a escala, decenas de millones/año. Retención
  por **Bulk Deletion** (`fechacarga < hoy − N`, ventana a definir), DEC-019. Esta es la tabla a vigilar.

---

## 9. Dependencias

- **Depende de:** `sanic_cuenta` (08), `sanic_cliente` (02), `sanic_agente` (04).
- **Habilita:** dashboards históricos (evolución de mora, agente por día) y la gamificación (DEC-011).
