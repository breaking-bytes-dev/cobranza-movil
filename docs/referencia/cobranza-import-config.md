# Smelter — Import de Cobranza (CSV denormalizado → 7 tablas)

> Modelo totalmente normalizado. Cada fila del CSV alimenta 7 entidades. Decisiones: Cliente 1:N Cuenta;
> direcciones/teléfonos normalizados; Producto/Agente/Fiador como catálogos; Cliente = tabla custom.

> **Estado (verificado + corregido en Dev, 2026-07-28):** el template **`Cobranza movil` YA existe** en
> Smelter `smlt_core_smelter` **v1.1.0.0** con **12 table mappings** (incluye una `Asignacion` que este doc no
> tenía documentada — ver §2). Config: delimiter `,`, header sí, status Active, **chunk size = 50**
> (conservador; se puede subir a ~500).
>
> ⚠️ **Corrección importante:** una auditoría **columna por columna** encontró que faltaban **19 column
> mappings** — se agregaron todos (verificado):
> - **Cuenta (denormalizadas para el offline):** `cifcliente ← CIF`, `nombrecliente ← Cliente`,
>   `tipoproducto ← TipoProducto`, `nombrefiador ← NombreFianza`, `direccionfiador ← DireccionFianza`,
>   `nota ← Nota`, y el **lookup `fiador ← NombreFianza`**. Sin estas, la **canvas app offline se rompía**
>   (usa los campos denormalizados, no navega relaciones).
> - **Carga Diaria (snapshot):** los **10 montos/mora** (`saldovencidonio/usd`, `totalsaldovencido`,
>   `contagiadousd`, `pagominimo nio/usd`, `retiro nio/usd`, `diasmora`, `nivelmora`) + el **lookup
>   `cuenta ← NumeroCuenta`**. Sin estos el snapshot no tenía montos.
> - **Asignacion:** `ultimacarga ← FechaCarga` (**clave para la reconciliación DEC-018**). `estado` ya
>   defaultea `true` (Activa), no necesita mapping.
>
> **Ahora los mappings SÍ están completos.** El ajuste que queda es de **seguridad/team** (§5) — hoy el
> template es de un **usuario**, no de un team. Ver también §6 (disparar el import desde nuestras apps).

## 1. Estructura de tablas en Dataverse

> Prefijo `sanic_`. `Tipo` de Dirección/Teléfono va como **texto simple** (no Choice) para que el alt key
> y el default sean directos.

### `sanic_cm_tbl_cliente` — Cliente (deudor) · **Alt key: `sanic_cm_pk_cliente_cif`**
| Columna | Tipo | Del CSV |
|---------|------|---------|
| `sanic_cm_cl_nombre` | Text | Cliente |
| `sanic_cm_cl_cif` | Text | CIF |
| `sanic_cm_cl_departamento` | Text | Departamento |
| `sanic_cm_cl_distrito` | Text | Distrito |
| `sanic_cm_cl_trabajo` | Text | Trabajo |

### `sanic_cm_tbl_producto` — Producto · **Alt key: `sanic_cm_pk_producto_codigo`**
| `sanic_cm_pr_codigo` | Text | CodigoProducto |
| `sanic_cm_pr_tipo` | Text | TipoProducto |

### `sanic_cm_tbl_agente` — Agente · **Alt key: `sanic_cm_pk_agente_nombre`**
| `sanic_cm_ag_nombre` | Text | Agente |

### `sanic_cm_tbl_fiador` — Fiador · **Alt key: `sanic_cm_pk_fi_nombre`**
| `sanic_cm_fi_nombre` | Text | NombreFianza |
| `sanic_cm_fi_direccion` | Text | DireccionFianza |

### `sanic_cm_tbl_direccion` — Dirección · **Alt key compuesta: `sanic_cm_pk_direccion_cif_tipo`**
| `sanic_cm_dr_clienteid` | Lookup → Cliente | (resuelto por CIF) |
| `sanic_cm_dr_cif` | Text | CIF *(copiado para la alt key)* |
| `sanic_cm_dr_tipo` | Text | "Casa" / "EC" / "Trabajo" *(default por mapping)* |
| `sanic_cm_dr_direccion` | Text | DireccionCasa / EC / Trabajo |

### `sanic_cm_tbl_telefono` — Teléfono · **Alt key compuesta: `sanic_cm_pk_telefono_cif_tipo`**
| `sanic_cm_tl_clienteid` | Lookup → Cliente | (resuelto por CIF) |
| `sanic_cm_tl_cif` | Text | CIF *(copiado para la alt key)* |
| `sanic_cm_tl_tipo` | Text | "Celular" / "Casa" *(default)* |
| `sanic_cm_tl_numero` | Text | NumeroCelular / NumeroCasa |

### `sanic_cm_tbl_cuenta` — Cuenta en Mora (el grano) · **Alt key: `sanic_cm_pk_ct_numerodecuenta`**
| Columna | Tipo | Del CSV |
|---------|------|---------|
| `sanic_cm_ct_numerodecuenta` | Text | NumeroCuenta |
| `sanic_cm_ct_clienteid` | Lookup → Cliente | (por CIF) |
| `sanic_cm_ct_productoid` | Lookup → Producto | (por CodigoProducto) |
| `sanic_cm_ct_agenteid` | Lookup → Agente | (por Agente) |
| `sanic_cm_ct_fiador` | Lookup → Fiador | (por NombreFianza) |
| `sanic_cm_ct_saldovencidonio` `sanic_cm_ct_saldovencidousd` `sanic_cm_ct_totalsaldovencido` `sanic_cm_ct_contagiadousd` `sanic_cm_ct_pagominimonio` `sanic_cm_ct_pagominimousd` `sanic_cm_ct_retironio` `sanic_cm_ct_retirousd` | Decimal | montos |
| `sanic_cm_ct_diasmora` `sanic_cm_ct_nivelmora` | Whole Number | DiasMora, NivelMora |
| `sanic_cm_ct_fechaultimopago` `sanic_cm_ct_fechapago` | DateTime | FechaUltimoPago, FechaPago |
| `sanic_cm_ct_ciclo` `sanic_cm_ct_tipoasignacion` `sanic_cm_ct_tipoespecial` `sanic_cm_ct_nota` | Text | Ciclo, TipoAsignacion, TipoEspecial, Nota |

### Relaciones (1:N)
`Cliente → Direccion` · `Cliente → Telefono` · `Cliente → CuentaMora` · `Producto → CuentaMora` ·
`Agente → CuentaMora` · `Fiador → CuentaMora`

### Orden de procesamiento (DAG)
1. **Padres** (sin dependencias): Cliente, Producto, Agente, Fiador.
2. **Hijas**: Direccion, Telefono, CuentaMora.

---

## 2. Config del Import Template

**Template**: Source Type = CSV, Has Header = Sí, Delimiter = `,`, Chunk Size = 1000.

### Table Mappings (10 — sí, 10: las direcciones/teléfonos se desdoblan)

| # | Target Table | Processing Order | Dedup Columns |
|---|--------------|:---:|---------------|
| 1 | `sanic_cm_tbl_cliente` | 1 | `sanic_cm_cl_cif` |
| 2 | `sanic_cm_tbl_producto` | 1 | `sanic_cm_pr_codigo` |
| 3 | `sanic_cm_tbl_agente` | 1 | `sanic_cm_ag_nombre` |
| 4 | `sanic_cm_tbl_fiador` | 1 | `sanic_cm_fi_nombre` |
| 5 | `sanic_cm_tbl_direccion` (Casa) | 2 | `sanic_cm_dr_cif,sanic_cm_dr_tipo` |
| 6 | `sanic_cm_tbl_direccion` (EC) | 2 | `sanic_cm_dr_cif,sanic_cm_dr_tipo` |
| 7 | `sanic_cm_tbl_direccion` (Trabajo) | 2 | `sanic_cm_dr_cif,sanic_cm_dr_tipo` |
| 8 | `sanic_cm_tbl_telefono` (Celular) | 2 | `sanic_cm_tl_cif,sanic_cm_tl_tipo` |
| 9 | `sanic_cm_tbl_telefono` (Casa) | 2 | `sanic_cm_tl_cif,sanic_cm_tl_tipo` |
| 10 | `sanic_cm_tbl_cuenta` | 3 | `sanic_cm_ct_numerodecuenta` |
| 11 | `sanic_cm_tbl_asignacion` | 4 | `sanic_cm_as_numerocuenta,sanic_cm_as_tipo` |

> **La `Asignacion` (mapping #11, orden 4) — la bisagra.** Crea la asignación que liga **cuenta + agente +
> tipo** (Diaria/Incobrable/Especial). Es lo que le arma la **cartera al localizador** (y sobre lo que la
> **comp 14** setea el owner). Dedup por `numerocuenta+tipo` → una asignación por cuenta y tipo. Column
> mappings: `NumeroCuenta→sanic_cm_as_numerocuenta`, `TipoAsignacion→sanic_cm_as_tipo`; Relationship Mappings:
> `Cuenta → Asignacion` (`sanic_cm_as_cuentaid`, por NumeroCuenta) y `Agente → Asignacion`
> (`sanic_cm_as_agenteid`, por Agente).

> 💡 **La sutileza clave:** Smelter genera **1 registro por Table Mapping por fila**. Como cada fila trae
> 3 direcciones y 2 teléfonos, creás **un Table Mapping por cada una**, todos apuntando a la misma tabla,
> diferenciados por el **DefaultValue del campo tipo** (`sanic_cm_dr_tipo` / `sanic_cm_tl_tipo`) y por qué columna del CSV mapean.

### Column Mappings por Table Mapping

**Cliente:** Cliente→`sanic_cm_cl_nombre`, CIF→`sanic_cm_cl_cif`, Departamento→`sanic_cm_cl_departamento`, Distrito→`sanic_cm_cl_distrito`, Trabajo→`sanic_cm_cl_trabajo`.

**Producto:** CodigoProducto→`sanic_cm_pr_codigo`, TipoProducto→`sanic_cm_pr_tipo`.

**Agente:** Agente→`sanic_cm_ag_nombre`.

**Fiador:** NombreFianza→`sanic_cm_fi_nombre` con **DefaultValue `"(sin fiador)"`** (2026-10-19, `scripts/pan/paso13_fiador_sin_nombre.py`), DireccionFianza→`sanic_cm_fi_direccion`. Smelter aplica el default antes de revisar si el valor está vacío: las filas sin fiador comparten UN solo fiador "(sin fiador)" (placeholder permanente; no se borra) en vez de crear un fiador vacío por fila. Eso es solo del mapeo de la tabla Fiador: el enlace de la cuenta no lleva default y con el nombre vacío la cuenta queda sin fiador.

**Direccion (Casa):** DireccionCasa→`sanic_cm_dr_direccion`, CIF→`sanic_cm_dr_cif`, `sanic_cm_dr_tipo` = **Default "Casa"** (Source Column vacío).
**Direccion (EC):** DireccionEC→`sanic_cm_dr_direccion`, CIF→`sanic_cm_dr_cif`, `sanic_cm_dr_tipo` = Default "EC".
**Direccion (Trabajo):** DireccionTrabajo→`sanic_cm_dr_direccion`, CIF→`sanic_cm_dr_cif`, `sanic_cm_dr_tipo` = Default "Trabajo".

**Telefono (Celular):** NumeroCelular→`sanic_cm_tl_numero`, CIF→`sanic_cm_tl_cif`, `sanic_cm_tl_tipo` = Default "Celular".
**Telefono (Casa):** NumeroCasa→`sanic_cm_tl_numero`, CIF→`sanic_cm_tl_cif`, `sanic_cm_tl_tipo` = Default "Casa".

**CuentaMora:** NumeroCuenta→`sanic_cm_ct_numerodecuenta`; los 8 montos→sus decimales (Data Type **Decimal**); DiasMora/NivelMora→**Whole Number**; FechaUltimoPago/FechaPago→**DateTime**; Ciclo/TipoAsignacion/TipoEspecial/Nota→Text. (CIF, CodigoProducto y Agente los lee el Relationship Mapping; no hace falta column mapping propio salvo que quieras guardarlos. **El fiador es distinto:** el enlace `sanic_cm_ct_fiador` es un **Column Mapping de la Cuenta** de tipo Lookup — `lookuptable = sanic_cm_tbl_fiador`, `lookupmatchcolumn = sanic_cm_fi_nombre`, `sourcecolumn = NombreFianza` — y no un Relationship Mapping; la plantilla real no tiene Relationship Mappings. Ese Column Mapping NO lleva default: con `NombreFianza` vacío no encuentra fiador y deja el lookup en blanco.)

### Relationship Mappings (resuelven los lookups por valor de negocio)

| Parent | Child | Lookup Column On Child | Parent Dedup Columns (del CSV) |
|--------|-------|------------------------|--------------------------------|
| Cliente | Direccion (x3) | `sanic_cm_dr_clienteid` | CIF |
| Cliente | Telefono (x2) | `sanic_cm_tl_clienteid` | CIF |
| Cliente | CuentaMora | `sanic_cm_ct_clienteid` | CIF |
| Producto | CuentaMora | `sanic_cm_ct_productoid` | CodigoProducto |
| Agente | CuentaMora | `sanic_cm_ct_agenteid` | Agente |
| ~~Fiador~~ | ~~CuentaMora~~ | ~~`sanic_cm_ct_fiador`~~ | ~~NombreFianza~~ — **no es un Relationship Mapping**: es un Column Mapping Lookup de la Cuenta (ver arriba) |

---

## 3. Advertencias (las que te van a morder si no las cuidás)

1. **TODAS las alt keys deben estar `Active`** antes de importar: `sanic_cm_pk_cliente_cif`, `sanic_cm_pk_producto_codigo`,
   `sanic_cm_pk_agente_nombre` (agente) y `sanic_cm_pk_fi_nombre` (fiador), `sanic_cm_pk_ct_numerodecuenta`, y las **compuestas** `sanic_cm_pk_direccion_cif_tipo`
   y `sanic_cm_pk_telefono_cif_tipo` (dirección y teléfono). Sin ellas → `TargetData` (lo que ya viviste con `contact`).
2. **El truco de la alt key compuesta:** copiamos el `CIF` como texto dentro de Dirección/Teléfono y la
   alt key es `cif+tipo` (texto), NO sobre el lookup. Así el upsert es directo; el lookup al Cliente lo
   setea el Relationship Mapping aparte. (Una alt key sobre un lookup es más frágil — la evitamos.)
3. **Fechas:** `FechaUltimoPago` y `FechaPago` vienen como texto. Si el formato NO es ISO
   (ej. `dd/MM/yyyy`), usá un Transform Expression `DATEPARSE(FechaPago, "dd/MM/yyyy")` en ese column
   mapping. Si fallan, vas a verlo como `TypeError` en el log.
4. **NIO/USD** quedan como **columnas decimales separadas** (no Money) — matchea el CSV sin currency.
5. **Idempotencia:** reprocesar el mismo archivo no duplica nada (todo upsert por su alt key). Un cliente
   con 3 cuentas (3 filas) crea 1 cliente + 3 direcciones + 2 teléfonos (se re-upsertan iguales) + 3 cuentas.

---

## 4. Histórico diario (el archivo se sube cada día)

`sanic_cm_tbl_cuenta` guarda el **estado ACTUAL** (upsert por NumeroCuenta → pisa lo anterior). Para la
**historia** (cuántas veces se cargó, qué agente cada día) se agrega una tabla snapshot diaria.

> **Requiere una columna nueva en el CSV: `FechaCarga`** (la fecha del día, puesta por el sistema origen).
> Sin esa columna no hay forma de fechar el snapshot.

### `sanic_cm_tbl_cargadiaria` — Snapshot diario · **Alt key: `sanic_cm_pk_cd_numerocuenta_fechacarga`**
| Columna | Tipo | Del CSV |
|---------|------|---------|
| `sanic_cm_cd_numerocuenta` | Text | NumeroCuenta *(copiado para la alt key)* |
| `sanic_cm_cd_fechacarga` | DateTime (Date Only) | **FechaCarga** *(columna nueva)* |
| `sanic_cm_cd_cuenta` | Lookup → CuentaMora | (por NumeroCuenta) |
| `sanic_cm_cd_clienteid` | Lookup → Cliente | (por CIF) — opcional, para reportes por cliente |
| `sanic_cm_cd_agenteid` | Lookup → Agente | (por Agente) |
| `sanic_cm_cd_tipoasignacion` | Text | TipoAsignacion |
| `sanic_cm_cd_saldovencidonio` … `sanic_cm_cd_retirousd` | Decimal | snapshot de montos del día |
| `sanic_cm_cd_diasmora` `sanic_cm_cd_nivelmora` | Whole Number | mora del día |

### Cómo encaja en el template
- **Table Mapping #11**: target `sanic_cm_tbl_cargadiaria`, Processing Order **4**, dedup `sanic_cm_cd_numerocuenta,sanic_cm_cd_fechacarga`.
- **Column Mappings**: NumeroCuenta→`sanic_cm_cd_numerocuenta`, FechaCarga→`sanic_cm_cd_fechacarga` (DateTime; si el formato no es ISO, `DATEPARSE(FechaCarga,"dd/MM/yyyy")`), Agente→lookup, TipoAsignacion, + los 8 montos (Decimal) y DiasMora/NivelMora (Whole Number).
- **Relationship Mappings**: `CuentaMora → CargaDiaria` (`sanic_cm_cd_cuenta`, por NumeroCuenta) · `Agente → CargaDiaria` (`sanic_cm_cd_agenteid`, por Agente) · `Cliente → CargaDiaria` (`sanic_cm_cd_clienteid`, por CIF, opcional).

### Por qué NO "guardar solo si cambió"
En cobranza `DiasMora` sube +1 cada día y los saldos varían → casi nunca dos días traen lo mismo, así que
el ahorro de un snapshot "liviano condicional" no se activa. Y detectar el cambio exigiría CDC (hash o
lógica custom en el plugin) que Smelter no tiene. Snapshot completo diario = simple y sin desperdicio real.

### Consultas que habilita
- *"¿Cuántas veces se cargó la cuenta X?"* → `count` de `cargadiaria` por `sanic_cm_cd_cuenta`.
- *"¿Qué agente la tenía el día X?"* → `cargadiaria` filtrada por cuenta + `sanic_cm_cd_fechacarga` = X.
- *"Evolución de la mora"* → serie de `sanic_cm_cd_diasmora` / saldos por `sanic_cm_cd_fechacarga`.

---

## 5. Seguridad y Team (Smelter v1.1) — **el ajuste pendiente**

Desde `smelter-security` v2, **el acceso a un template va por Team**. Dos roles (los provisiona
`Smelter.Deploy`, ya están en Dev):
- **`Smelter Configurador`** — crea/edita templates y mappings, **ve TODO** (Org-level). Los que arman la config.
- **`Smelter Operador`** — solo **corre imports** y ve logs, y **ve solo los templates de sus teams**
  (Read User/Basic). Los que suben el CSV cada día.

**El cascade Template→Import Job** (Share=Cascade, ya provisionado) hace que los jobs creados bajo el
template **hereden el acceso del team** — el operador ve los jobs de su team sin compartir cada uno.

### Estado actual (DEV, 2026-07-28) — ✅ hecho por API
Se crearon los **5 teams de DEV** (uno por rol), cada uno con su rol de seguridad. El template
`Cobranza movil` **ya es owner del team `CM · Admin y Carga · DEV`** (antes era de un usuario):

| Team DEV | Rol(es) |
|----------|---------|
| `CM · Localizadores · DEV` | `SR \| CM \| Localizador` |
| `CM · Backoffice · DEV` | `SR \| CM \| Backoffice` |
| `CM · Solicitantes · DEV` | `SR \| CM \| Solicitante` |
| `CM · Supervisores · DEV` | `SR \| CM \| Supervisor` |
| **`CM · Admin y Carga · DEV`** | `SR \| CM \| Admin` + **`Smelter Operador`** · **owner del template** |

**Falta (manual, depende de vos):**
1. **Agregar los usuarios** (operadores/localizadores/etc.) como **miembros** de cada team.
2. **Compartir la app** con los teams.
3. Cuando exista PRD, **replicar los 5 teams** con sufijo `· PRD`.
4. Si Admin y Carga va a **editar** la estructura del template (no solo correrlo), sumarle también
   **`Smelter Configurador`** (hoy tiene solo Operador, que alcanza para correr la carga).

> Los **Configuradores** (Org-level) ven todos los templates sin depender del team.

> ⚠️ **Rename de Custom API (v1.1):** si algo invoca la API por nombre, es `sanic_capi_smelter_processchunk`
> (se corrigió el typo `anic_...` histórico). `prepareimport` no cambió.

---

## 6. Disparar el import desde NUESTRAS apps (Smelter v1.1)

Smelter v1.1 permite **correr el import desde cualquier canvas o model-driven app** (no solo la MDA de
Smelter), porque el motor son **Custom APIs**. El flujo mínimo:

1. Crear un **Import Job** con `Import Template = Cobranza movil` y el **CSV adjunto**.
2. Invocar **`sanic_capi_smelter_prepareimport`** (valida headers, cuenta filas) → el Cloud Flow orquesta
   **`sanic_capi_smelter_processchunk`** en lote hasta terminar.

**Opciones para Cobranza:**
- **Simple:** el Admin usa la **MDA de Smelter** (botón ▶ Ejecutar) — cero desarrollo.
- **Integrado:** un botón en NUESTRO backoffice (área "Monitoreo de carga") que dispare un **Power Automate**
  que crea el Job + llama `prepareimport`. Requiere que quien dispara tenga **Create en Import Job**
  (rol Operador) + **Read en el template** (por el team) — el `ExecutePrivilegeName` y el check de runtime
  lo validan.

> Para el flujo diario típico (un CSV por día), la opción **simple** (MDA de Smelter) alcanza. El botón
> integrado se justifica solo si querés que el oficinista dispare la carga sin salir del backoffice.
