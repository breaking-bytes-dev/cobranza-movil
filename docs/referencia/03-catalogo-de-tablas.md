# Catálogo de Tablas — Definición, Lifecycle y Borrado

> Referencia **única y consolidada** de cada tabla: definición, campos y tipos, llaves, import, ciclo de
> vida, cómo borrar y consideraciones al borrar. Se construye **tabla por tabla**.
>
> Reúne lo que estaba repartido entre los componentes (`componentes/`), la config de import
> (`02-import-configuracion.md`, `cobranza-import-config.md`) y las decisiones (`decisiones/`).

## Convenciones de borrado (modelo, DEC-023)

- **`Cascade`** en la cadena de **datos/contacto** (`cliente → direccion/telefono/cuenta`): borrar el padre
  borra sus datos.
- **`Restrict`** en el **límite de la historia operativa** (`cuenta → interaccion` y aguas abajo): si hay
  cobranza registrada, **bloquea** el borrado (y detiene la cascada).
- **Efecto neto:** *"se borra el cliente y toda su data, salvo que haya interacciones; ahí se bloquea todo".*
- Borrado de rutina: **no existe** (no hay política). Fin de vida normal = **soft-deactivate**.

---

## 1. `sanic_cm_tbl_cliente` — Cliente (deudor)

### Definición
El **cliente / deudor**: la persona a la que se le cobra. Es la **raíz** del subárbol de cliente — de ella
cuelgan cuentas, direcciones y teléfonos. Su identidad de negocio es el **CIF**.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_cl_nombre` | Text | ✔ | **Primary name.** Del CSV (`Cliente`). |
| CIF | `sanic_cm_cl_cif` | Text | ✔ | Identificador único interno del cliente. **Alt key.** |
| Departamento | `sanic_cm_cl_departamento` | Text | — | Departamento geográfico del cliente. |
| Distrito | `sanic_cm_cl_distrito` | Text | — | Distrito donde trabaja el cliente. |
| Trabajo | `sanic_cm_cl_trabajo` | Text | — | Nombre del trabajo del cliente. |

> **Sin correo:** el cliente **no** tiene campo de email. El correo que el cliente quiera actualizar vive
> **solo** en `actualizaciondatos` (decidido).

### Llaves
- **Alternate Key: `sanic_cm_pk_cliente_cif`** (sobre `sanic_cm_cl_cif`)**.** El import deduplica/upsertea por
  CIF. **Debe quedar `Active`** antes de importar (sin ella → error `TargetData`).

### Import
- **Origen:** CSV diario. **Table Mapping #1**, processing order **1** (es padre, va primero).
- **Column Mappings:** `Cliente → sanic_cm_cl_nombre` · `CIF → sanic_cm_cl_cif` · `Departamento → sanic_cm_cl_departamento` ·
  `Distrito → sanic_cm_cl_distrito` · `Trabajo → sanic_cm_cl_trabajo`.
- **Dedup:** `CIF`.

### Ciclo de vida
- **Nace:** del **CSV** (upsert por CIF) **o** en la **app** (el rol **Solicitante**, al crear una Especial —
  DEC-013).
- **Vive:** cada re-import **pisa** los campos mapeados (upsert por CIF). Es **rastro de auditoría**.
- **Fin de vida (normal):** **soft** — un cliente sin cuentas queda **dormido**, pero se conserva. No se
  borra de rutina (no hay política de borrado).

### Cómo borrar
- **Relaciones salientes en `Cascade`:** `cliente → direccion`, `cliente → telefono`, `cliente → cuenta`.
- Borrar un cliente **arrastra** sus direcciones, teléfonos y cuentas (DEC-023).
- El hard-delete es **deliberado** (no hay job de borrado).

### Consideraciones al borrar
- **La cascada se BLOQUEA si hay interacciones:** `cuenta → interaccion` es `Restrict` *(a confirmar al
  revisar `interaccion`)*. Si alguna cuenta del cliente tiene interacciones, Dataverse **rechaza** el borrado
  completo (rollback). En criollo: *no podés borrar un cliente que tiene cobranza registrada*.
- **Colisión de CIF (DEC-013):** un cliente creado a mano con un CIF que luego llega por CSV se **fusiona**
  (bien si el CIF es correcto; **duplica** si está mal).
- **Caso real de borrado:** un pedido **legal de eliminación de datos** ("derecho al olvido"). Ahí se borra
  deliberadamente; la `Restrict` aguas abajo obliga a resolver la historia primero, no de un saque.

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar · ✅ Consideraciones · ⏳ `Restrict` en `interaccion`
(a confirmar en su tabla).

---

## 2. `sanic_cm_tbl_direccion` — Dirección del cliente

### Definición
Las **direcciones** del cliente, **normalizadas por tipo** (`Casa` / `EC` / `Trabajo`). Cada fila del CSV
trae 3 direcciones → el import crea **una fila por tipo**. `EC` = dirección donde recibe el estado de cuenta.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Dirección | `sanic_cm_dr_direccion` | Text | ✔ | **Primary name.** `DireccionCasa` / `DireccionEC` / `DireccionTrabajo`. |
| Cliente | `sanic_cm_dr_clienteid` | Lookup → `sanic_cm_tbl_cliente` | ✔ | Resuelto por `CIF`. |
| CIF | `sanic_cm_dr_cif` | Text | ✔ | **Copiado** del cliente para la alt key compuesta. |
| Tipo | `sanic_cm_dr_tipo` | Text | ✔ | `"Casa"` / `"EC"` / `"Trabajo"` (lo fija el import por mapping). |

### Llaves
- **Alt key compuesta: `sanic_cm_pk_direccion_cif_tipo`** (`sanic_cm_dr_cif` + `sanic_cm_dr_tipo`)**.** Una
  dirección por (cliente, tipo). **Debe quedar `Active`.** Va sobre **texto** (CIF copiado), no sobre el lookup
  — el vínculo lo arma el Relationship Mapping.

### Import
- **Origen:** CSV. **3 Table Mappings** (#5 Casa, #6 EC, #7 Trabajo), processing order **2** (después de cliente).
- Cada uno: `DireccionX → sanic_cm_dr_direccion`, `CIF → sanic_cm_dr_cif`, `sanic_cm_dr_tipo` = **Default** ("Casa"/"EC"/"Trabajo").
- **Relationship Mapping:** `Cliente → Direccion` por `CIF`.

### Ciclo de vida
- **Nace:** del **CSV** (la explosión por tipo) **o** en la **app** (Solicitante, al editar datos del cliente
  para una Especial).
- **Vive:** guarda el **dato ACTUAL**. El upsert por `cif+tipo` **pisa** el valor anterior. El histórico de
  cambios **no** vive acá — lo captura `actualizaciondatos`.
- **Fin:** soft; se conserva mientras exista el cliente.

### Cómo borrar
- **Entrante:** `cliente → direccion` = **`Cascade`** — borrar el cliente borra sus direcciones (DEC-023).
- Es **hoja** (no tiene hijos) → no arrastra nada, no hay riesgo de huérfano aguas abajo.

### Consideraciones al borrar
- Se borra **junto con el cliente** (cascada). Por sí sola se puede borrar (es dato de contacto actual), pero
  no es lo habitual.
- **Tipos huérfanos (stale):** si el CSV deja de mandar un tipo (ej. el cliente ya no tiene "Trabajo"), la
  fila vieja **queda** (el import es upsert-only, no borra). Dato de contacto desactualizado, baja prioridad.

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar · ✅ Consideraciones.

---

## 3. `sanic_cm_tbl_telefono` — Teléfono del cliente

### Definición
Los **teléfonos** del cliente, **normalizados por tipo** (`Celular` / `Casa`). Cada fila del CSV trae 2
teléfonos → el import crea **una fila por tipo**.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_tl_numero` | Text | ✔ | **Primary name.** `NumeroCelular` / `NumeroCasa`. |
| Cliente | `sanic_cm_tl_clienteid` | Lookup → `sanic_cm_tbl_cliente` | ✔ | Resuelto por `CIF`. |
| CIF | `sanic_cm_tl_cif` | Text | ✔ | **Copiado** del cliente para la alt key compuesta. |
| Tipo | `sanic_cm_tl_tipo` | Text | ✔ | `"Celular"` / `"Casa"` (lo fija el import por mapping). |

### Llaves
- **Alt key compuesta: `sanic_cm_pk_telefono_cif_tipo`** (`sanic_cm_tl_cif` + `sanic_cm_tl_tipo`)**.** Un
  teléfono por (cliente, tipo). **Debe quedar `Active`.**

### Import
- **Origen:** CSV. **2 Table Mappings** (#8 Celular, #9 Casa), processing order **2**.
- Cada uno: `NumeroX → sanic_cm_tl_numero`, `CIF → sanic_cm_tl_cif`, `sanic_cm_tl_tipo` = **Default** ("Celular"/"Casa").
- **Relationship Mapping:** `Cliente → Telefono` por `CIF`.

### Ciclo de vida
- **Nace:** del **CSV** (la explosión por tipo) **o** en la **app** (Solicitante).
- **Vive:** dato **ACTUAL**; el upsert por `cif+tipo` **pisa**. Histórico de cambios → `actualizaciondatos`.
- **Fin:** soft; se conserva mientras exista el cliente.

### Cómo borrar
- **Entrante:** `cliente → telefono` = **`Cascade`** (DEC-023). Es **hoja**, no arrastra nada.

### Consideraciones al borrar
- `sanic_cm_tl_numero` es **texto** (puede traer ceros a la izquierda, guiones, prefijos).
- Mismas consideraciones de cascada y tipos huérfanos que `direccion`.
- **Teléfono de trabajo = capture-only (DECIDIDO):** el master `telefono` tiene **solo** `Celular`/`Casa`
  (= el CSV). El "teléfono de trabajo" que captura `actualizaciondatos` **vive solo ahí** (no se propaga al
  master); se transcribe al core como las demás interacciones. **Mismo patrón que el correo.**

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar · ✅ Consideraciones.

---

## 4. `sanic_cm_tbl_producto` — Producto (catálogo)

### Definición
Catálogo del **producto en mora** (tarjeta o préstamo). **Clasifica** la cuenta. Es **padre** de `cuenta`.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Código | `sanic_cm_pr_codigo` | Text | ✔ | **Primary name.** `CodigoProducto`. **Alt key.** |
| Tipo | `sanic_cm_pr_tipo` | Text | — | `TipoProducto` — tipo de producto. |

### Llaves
- **Alt key: `sanic_cm_pk_producto_codigo`** (sobre `sanic_cm_pr_codigo`)**.** Upsert por código. **Debe quedar `Active`.**

### Import
- **Table Mapping #2**, processing order **1** (catálogo raíz). `CodigoProducto → sanic_cm_pr_codigo` ·
  `TipoProducto → sanic_cm_pr_tipo`. Dedup `CodigoProducto`.

### Ciclo de vida
- **Nace:** del **CSV** (upsert por código). **Vive:** casi **estático**; un código discontinuado se queda
  (cuentas históricas lo referencian). **Fin:** no se borra.

### Cómo borrar
- **`producto → cuenta` = `Restrict`** (catálogo compartido). **Nunca** se cascadea; se **bloquea** si hay
  cuentas que lo referencian. No hay job de borrado.

### Consideraciones al borrar
- **Nunca hard-delete** (referenciado por cuentas históricas). Discontinuar = simplemente **dejar de usarlo**.
- No tiene flag de "activo" (si se necesitara distinguir vigentes, se agregaría — hoy no hace falta).

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar · ✅ Consideraciones.

---

## 5. `sanic_cm_tbl_agente` — Agente / Localizador (catálogo + extensión operativa)

### Definición
El **Agente** del CSV **es el localizador** (DEC-006). Catálogo poblado por el import **+** la columna
operativa que lo liga a su identidad AAD. Es **padre** de `cuenta`, `asignacion`, `interaccion`, `cargadiaria`.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_ag_nombre` | Text | ✔ | **Primary name.** Del CSV (`Agente`). **Alt key.** |
| Usuario | `sanic_cm_ag_usuarioid` | Lookup → `systemuser` | — | **Operativo.** Identidad AAD; lo setea el **Admin** (no viene del CSV). |
| Activo | `sanic_cm_ag_activo` | Yes/No (def. Sí) | — | **Soft-delete:** marca si el localizador está operativo. |

### Llaves
- **Alt key: `sanic_cm_pk_agente_nombre`** (sobre `sanic_cm_ag_nombre`)**.** Upsert por nombre. **Debe quedar
  `Active`.** El vínculo `sanic_cm_ag_usuarioid` **NO** va en la alt key (lookup frágil).

### Import
- **Table Mapping #3**, processing order **1**. `Agente → sanic_cm_ag_nombre`. Dedup `Agente`.
- `sanic_cm_ag_usuarioid` y `sanic_cm_ag_activo` **NO** vienen del CSV: el **Admin** los gestiona.

### Ciclo de vida
- **Nace:** del **CSV** (upsert por nombre). El **Admin lo vincula** a un `systemuser` (una vez, alta del localizador).
- **Vive:** persiste. Ancla la **seguridad row-level** (el localizador ve su cartera vía su `systemuser`).
- **Fin:** cuando el localizador se va → **`sanic_cm_ag_activo = No`** (soft-delete). **NUNCA** se hard-delete.

### Cómo borrar
- **NUNCA hard-delete:** referenciado por cuentas, asignaciones, interacciones y cargas = historia + seguridad.
- `agente → cuenta / asignacion / interaccion / cargadiaria` = **`Restrict`**.
- **Fin de vida = soft-deactivate** (`sanic_cm_ag_activo = No`).

### Consideraciones al borrar
- **Nombre cambiante:** si el `Agente` cambia en el origen, el import crea **otro** agente (no renombra) →
  hay que **re-vincular** `sanic_cm_ag_usuarioid`. Acordar que el `Agente` del CSV sea **estable**.
- **Localizador que renuncia:** `sanic_cm_ag_activo = No` + vista de control *"asignaciones de agentes inactivos"*
  (el CSV puede insistir en mandarle cuentas — gana el CSV, DEC-020; se corrige en el core).
- Borrarlo rompería **historia** y **seguridad row-level**.

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar · ✅ Consideraciones.

---

## 6. `sanic_cm_tbl_fiador` — Fiador / Fianza (catálogo)

### Definición
El **fiador** (fianza) del producto en mora. Es **padre** de `cuenta`. ~50% de las cuentas **no** tienen
fiador (`NombreFianza` vacío → no se crea fiador).

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_fi_nombre` | Text | ✔ | **Primary name.** `NombreFianza`. **Alt key.** |
| Dirección | `sanic_cm_fi_direccion` | Text | — | `DireccionFianza`. |

### Llaves
- **Alt key: `sanic_cm_pk_fi_nombre`** (sobre `sanic_cm_fi_nombre`)**.** Upsert por nombre. **Debe quedar `Active`.**

### Import
- **Table Mapping #4**, processing order **1**. `NombreFianza → sanic_cm_fi_nombre` ·
  `DireccionFianza → sanic_cm_fi_direccion`. Dedup `NombreFianza`.

### Ciclo de vida
- **Nace:** del **CSV** (upsert por nombre). **Vive:** persiste; crece poco. **Fin:** no se borra.

### Cómo borrar
- **`fiador → cuenta` = `Restrict`** (catálogo). No se cascadea; se bloquea si está en uso.

### Consideraciones al borrar
- ⚠️ **Unicidad de `NombreFianza` (a VALIDAR con el área):** la alt key es por **nombre**. Si dos fianzas
  **distintas** comparten nombre exacto, el upsert las **colapsa en una**. Si `NombreFianza` no es único en el
  origen, hay que **repensar la clave** (ej. agregar un identificador de fianza). **Pregunta abierta al negocio.**

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar · ⚠️ Consideraciones (validar unicidad de `NombreFianza`).

---

## 7. `sanic_cm_tbl_cuenta` — Cuenta en mora (el grano)

### Definición
La **cuenta en mora** (tarjeta o préstamo) que se cobra. **El grano del modelo.** Guarda el **estado ACTUAL**
(el upsert la pisa cada día; la historia financiera va a `cargadiaria`). Cuelga de `cliente`/`producto`/
`agente`/`fiador`; es **padre** de `interaccion`, `asignacion`, `cargadiaria`.

### Campos

**Identidad y vínculos**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número de cuenta | `sanic_cm_ct_numerodecuenta` | Text | ✔ | **Primary name.** `NumeroCuenta` (tarjeta o préstamo). **Alt key.** |
| Cliente | `sanic_cm_ct_clienteid` | Lookup → `sanic_cm_tbl_cliente` | ✔ | Resuelto por `CIF`. |
| Producto | `sanic_cm_ct_productoid` | Lookup → `sanic_cm_tbl_producto` | — | Resuelto por `CodigoProducto`. |
| Agente | `sanic_cm_ct_agenteid` | Lookup → `sanic_cm_tbl_agente` | — | Resuelto por `Agente`. |
| Fiador | `sanic_cm_ct_fiador` | Lookup → `sanic_cm_tbl_fiador` | — | Resuelto por `NombreFianza`. |

**Montos** (Decimal — NO Money)
| Campo lógico | Físico | Del CSV |
|--------------|--------|---------|
| Saldo vencido NIO | `sanic_cm_ct_saldovencidonio` | `SaldoVencidoNIO` |
| Saldo vencido USD | `sanic_cm_ct_saldovencidousd` | `SaldoVencidoUSD` |
| Total saldo vencido | `sanic_cm_ct_totalsaldovencido` | NIO dolarizado + USD |
| Contagiado USD | `sanic_cm_ct_contagiadousd` | saldo total en riesgo |
| Pago mínimo NIO | `sanic_cm_ct_pagominimonio` | `PagoMinimoNIO` |
| Pago mínimo USD | `sanic_cm_ct_pagominimousd` | `PagoMinimoUSD` |
| Retiro NIO | `sanic_cm_ct_retironio` | **dinero a TRAER** (objetivo de cobro) ⭐ |
| Retiro USD | `sanic_cm_ct_retirousd` | **dinero a TRAER** USD ⭐ |

**Mora, fechas y atributos**
| Campo lógico | Físico | Tipo | Del CSV |
|--------------|--------|------|---------|
| Días de mora | `sanic_cm_ct_diasmora` | Whole Number | `DiasMora` |
| Nivel de mora | `sanic_cm_ct_nivelmora` | Whole Number | `NivelMora` |
| Fecha último pago | `sanic_cm_ct_fechaultimopago` | DateTime | `FechaUltimoPago` |
| Fecha de pago | `sanic_cm_ct_fechapago` | DateTime | `FechaPago` (cuándo le toca pagar) |
| Ciclo | `sanic_cm_ct_ciclo` | Text | `Ciclo` |
| Tipo de asignación | `sanic_cm_ct_tipoasignacion` | Text | `Diaria`/`Incobrable`/`Especial` (también alimenta `asignacion`) |
| Tipo especial | `sanic_cm_ct_tipoespecial` | Text | `TipoEspecial` |
| Nota | `sanic_cm_ct_nota` | Text | `Nota` |

### Llaves
- **Alt key: `sanic_cm_pk_ct_numerodecuenta`** (sobre `sanic_cm_ct_numerodecuenta`)**.** Upsert por número de cuenta (estado actual). **Debe quedar `Active`.**
- ⚠️ Caracteres prohibidos (`/ < > * % & : \ ? +`) en `NumeroCuenta` **rompen el upsert** — validar.

### Import
- **Table Mapping #10**, processing order **3** (después de los 4 padres).
- **Column Mappings:** `NumeroCuenta`, los **8 montos** (Decimal), `DiasMora`/`NivelMora` (Whole Number),
  `FechaUltimoPago`/`FechaPago` (DateTime — `DATEPARSE` si no es ISO), `Ciclo`/`TipoAsignacion`/`TipoEspecial`/
  `Nota` (Text).
- **Relationship Mappings:** `Cliente` (CIF) · `Producto` (CodigoProducto) · `Agente` (Agente) · `Fiador` (NombreFianza).

### Ciclo de vida
- **Nace:** del **CSV** (upsert por `numerocuenta`) **o** en la **app** (Solicitante, al crear una Especial —
  crea la cuenta si no existe).
- **Vive:** el upsert **PISA** cada día (estado actual). La **historia financiera** → `cargadiaria`. El
  `tipoasignacion` alimenta la `asignacion` (reclasificación, DEC-021).
- **Fin:** **soft.** "Fuera de cobranza" = sin asignación activa (estado explícito **opcional**). No se borra
  de rutina.

### Cómo borrar
- **Entrante:** `cliente → cuenta` = **`Cascade`** (borrar el cliente borra la cuenta).
- **Catálogos:** `producto/agente/fiador → cuenta` = **`Restrict`** (no se borra el catálogo si está en uso).
- **Salientes:**
  - `cuenta → cargadiaria` = **`Cascade`** (historia descartable).
  - `cuenta → asignacion` = **`Cascade`** (operativa; no es el límite de protección; el rastro de
    reasignaciones vive en la auditoría, DEC-016).
  - `cuenta → interaccion` = **`Restrict`** ⏳ (el límite de protección; a confirmar al revisar `interaccion`).
- **Efecto neto:** borrar una cuenta **cascadea** sus `cargadiaria` y `asignacion`, **pero se bloquea** si
  tiene `interaccion` (cobranza registrada).

### Consideraciones al borrar
- El `Restrict` en `interaccion` es lo que **protege la cobranza registrada** de un borrado accidental.
- **Creación manual (Especial):** una cuenta creada a mano puede **colisionar** con el CSV → el upsert por
  `numerocuenta` las **fusiona** (bien si el número es correcto).
- **Estado actual vs histórico:** la cuenta se pisa; la evolución vive en `cargadiaria` (no se pierde al pisar).

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar (`cargadiaria`/`asignacion` Cascade; `interaccion`
Restrict ⏳ a confirmar) · ✅ Consideraciones.

---

## 8. `sanic_cm_tbl_cargadiaria` — Snapshot histórico diario

### Definición
Una **foto diaria** de cada cuenta en mora (montos, mora, agente del día). Mientras `cuenta` guarda el
**estado actual** (se pisa), `cargadiaria` guarda la **historia** — un registro por **(cuenta, día)**.
Guarda un **subconjunto** de `cuenta`: solo lo que **cambia día a día** (lo estable se consulta por lookup).

### Campos

**Identidad y vínculos**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número de cuenta | `sanic_cm_cd_numerocuenta` | Text | ✔ | **Primary name.** Copiado para la alt key. |
| Fecha de carga | `sanic_cm_cd_fechacarga` | DateTime (Date Only) | ✔ | **`FechaCarga`** — columna NUEVA del CSV (no `createdon`). |
| Cuenta | `sanic_cm_cd_cuenta` | Lookup → `sanic_cm_tbl_cuenta` | ✔ | Resuelto por `NumeroCuenta`. |
| Cliente | `sanic_cm_cd_clienteid` | Lookup → `sanic_cm_tbl_cliente` | — | Por `CIF` (opcional, para reportes). |
| Agente | `sanic_cm_cd_agenteid` | Lookup → `sanic_cm_tbl_agente` | — | Por `Agente` — **quién la tenía ese día**. |
| Tipo de asignación | `sanic_cm_cd_tipoasignacion` | Text | — | `TipoAsignacion` del día. |

**Montos del día** (Decimal) — los mismos 8 que `cuenta`: `sanic_cm_cd_saldovencidonio`, `sanic_cm_cd_saldovencidousd`,
`sanic_cm_cd_totalsaldovencido`, `sanic_cm_cd_contagiadousd`, `sanic_cm_cd_pagominimonio`, `sanic_cm_cd_pagominimousd`,
`sanic_cm_cd_retironio`, `sanic_cm_cd_retirousd`.

**Mora del día** (Whole Number): `sanic_cm_cd_diasmora`, `sanic_cm_cd_nivelmora`.

### Llaves
- **Alt key compuesta: `sanic_cm_pk_cd_numerocuenta_fechacarga`** (`sanic_cm_cd_numerocuenta` +
  `sanic_cm_cd_fechacarga`)**.** Un registro por cuenta por día; mismo día re-subido = upsert (no duplica).
  **Debe quedar `Active`.**

### Import
- **Table Mapping #11**, processing order **4** (después de `cuenta`). Es **el mismo import diario** (no un
  proceso aparte).
- **Prerrequisito:** el CSV **debe traer `FechaCarga`** (sin ella no hay forma de fechar el snapshot; `DATEPARSE`
  si no es ISO).
- **Relationship Mappings:** `Cuenta` (NumeroCuenta) · `Agente` (Agente) · `Cliente` (CIF, opcional).

### Ciclo de vida
- **Nace:** del **CSV** (una fila por cuenta por día).
- **Vive:** **ACUMULA** (alt key `numerocuenta+fechacarga` → fila nueva cada día). **No se pisa** como `cuenta`.
- **Cobertura por tipo:** `Diaria` **todos los días**; `Incobrable` **solo los días que aparece** (esparso);
  `Especial` **del CSV** → sí tiene snapshot el día que viene; `Especial` **de la app** → **no** (no está en el CSV).
- **Fin:** se **borra por retención** (es el único maestro que se borra de rutina).

### Cómo borrar
- **Entrante:** `cuenta → cargadiaria` = **`Cascade`** (borrar la cuenta borra sus snapshots).
- **Retención (DEC-019):** **Bulk Deletion** recurrente → borrar `fechacarga < hoy − N` (ventana **N a definir**).
- Es **hoja** (no tiene hijos) → borrado seguro.

### Consideraciones al borrar
- **Es el ÚNICO que se borra de rutina** (a diferencia del resto de maestros) — porque es **descartable**: el
  estado actual vive en `cuenta`, la cartera en `asignacion`. Borrarlo **no afecta la operación**, solo el histórico.
- **Crece rápido** (1 fila/cuenta/día → decenas de millones/año a escala) → es la tabla a vigilar.
- **No confundir con `asignacion`:** `cargadiaria` = histórico financiero; `asignacion` = cartera viva.

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar (Cascade desde cuenta + Bulk Delete por retención) · ✅ Consideraciones.

---

## 9. `sanic_cm_tbl_interaccion` — Interacción (núcleo de la captura)

### Definición
Cada **interacción** del localizador con un cliente: **comentario libre**, geo, si hubo pago, si hay datos a
actualizar, y el **write-back al core**. Se crea **en la app de campo (offline)**, es **append-only**, y es
**el rastro de auditoría** de la cobranza — la pieza **más protegida** del modelo.

### Campos

**Captura del localizador (en campo, offline)**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_in_numerointeraccion` | Autonumber (`INT-{SEQNUM:6}`) | auto | **Primary name.** Server-assigned al sincronizar. |
| Cuenta | `sanic_cm_in_cuentaid` | Lookup → `sanic_cm_tbl_cuenta` | ✔ | La cuenta gestionada. |
| Agente | `sanic_cm_in_agente` | Lookup → `sanic_cm_tbl_agente` | ✔ | El localizador (= él mismo). |
| Comentario | `sanic_cm_in_comentario` | Multiline Text | ✔ | **Texto libre** de la interacción. |
| Fecha de interacción | `⚠️TODO(sanic_fechainteraccion)` | DateTime | ✔ | Auto al registrar, en campo. **Sin equivalente en el esquema real.** |
| ¿Actualiza datos? | `sanic_cm_in_actualizainfo` | Yes/No (def. No) | — | Marca que hay datos a actualizar (detalle en `actualizaciondatos`). |
| ¿Aplica pago? | `sanic_cm_cd_aplicapago` | Yes/No (def. No) | — | Si hubo cobro (montos en `pago`). **(Prefijo `cd` en el esquema real, no `in`.)** |
| Latitud | `sanic_cm_in_latitud` | Decimal (5) | — | GPS (DEC-008). |
| Longitud | `sanic_cm_in_longitud` | Decimal (5) | — | GPS. |
| Propietario | `ownerid` | Owner | ✔ | = el **localizador** (User-level). |

**Write-back al core (backoffice)**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Estado de transcripción | `sanic_cm_in_estadograbacion` | Choice (`Pendiente`/`Grabada en core`) | ✔ | Default `Pendiente`. Choice: `sanic_cm_choice_estadograbacioninteraccion`. |
| Código del core | `sanic_cm_in_codigograbacion` | Text | — | El código con que el backoffice la grabó (DEC-002). |
| Fecha grabada en core | `sanic_cm_in_fechagrabacion` | DateTime | — | Cuándo la transcribió. |
| Grabada por | `sanic_cm_in_grabadopor` | Lookup → `systemuser` | — | Qué backoffice. |

### Llaves
- **Ninguna.** Se crea en la app (no por import) → usa su **GUID**. Es un **hecho fechado** (append-only),
  no se deduplica.

### Import
- **N/A** — **no viene del CSV.** La crea la **app de campo** (offline); el `sanic_cm_in_numerointeraccion` lo asigna el
  servidor al **sincronizar**.

### Ciclo de vida
- **Nace:** en la **app de campo** (offline), por el localizador. Owner = él.
- **Vive:** **append-only.** Solo se completa el **write-back**: el backoffice asigna `codigocore`, marca
  `estadotranscripcion = Grabada en core`, sella fecha y usuario. No se edita una vez sincronizada.
- **Fin:** **NO se borra.** Es el rastro de auditoría — lo más sagrado del modelo.

### Cómo borrar
- **Entrante:** `cuenta → interaccion` = **`Restrict`** ✅ **(CONFIRMADO)** — **el candado**. Una cuenta con
  interacciones **no se puede borrar** → bloquea la cascada desde el cliente. *Esto protege toda la cadena*
  `cliente → cuenta → interaccion`.
- **Salientes:**
  - `interaccion → actualizaciondatos` = **`Cascade`** (es parte de la interacción; se va con ella).
  - `interaccion → pago` = **`Cascade`** (el pago es parte del registro de la interacción; la barrera ya está
    arriba en `cuenta → interaccion`).
- **Efecto neto:** las interacciones **no se borran de rutina**; un borrado deliberado (ej. erasure legal)
  obliga a resolverlas primero — esa es la fricción que protege la cobranza registrada.

### Consideraciones al borrar
- Es **el candado de auditoría** de todo el modelo de borrado.
- **Offline:** `sanic_cm_in_numerointeraccion` server-assigned al sync; geo opcional (no bloquear si el GPS falla).
- **Anti-doble-carga (write-back):** el filtro `estadotranscripcion = Pendiente` evita que dos backoffice la
  graben dos veces.

### Estado de la revisión
✅ Import (N/A) · ✅ Llaves (ninguna) · ✅ Lifecycle · ✅ Cómo borrar (`cuenta→interaccion` Restrict;
`→ pago` y `→ actualizaciondatos` Cascade) · ✅ Consideraciones.

---

## 10. `sanic_cm_tbl_pago` — Pago cobrado

### Definición
Un **pago** cobrado al cliente, **hijo de una interacción**. La relación **1:N** permite el **pago mixto**
(una interacción con un pago en NIO **y** otro en USD). El **recibo sigue siendo físico** (DEC-003); acá solo
se **anota** monto, moneda y número de recibo.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_pg_numero` | Autonumber (`PAG-{SEQNUM:6}`) | auto | **Primary name.** Server-assigned al sincronizar. |
| Interacción | `sanic_cm_pg_interaccion` | Lookup → `sanic_cm_tbl_interaccion` | ✔ | El pago **pertenece** a una interacción. |
| Monto | `sanic_cm_pg_monto` | Decimal (2) | ✔ | Monto cobrado. |
| Moneda | `sanic_cm_pg_moneda` | Choice (`NIO`/`USD`) | ✔ | Moneda del pago. Choice: `sanic_choice_cm_monedaenpago`. |
| Número de recibo | `sanic_cm_pg_numerorecibo` | Text | ✔ | **Número del recibo físico** (talonario). |
| Fecha del pago | `⚠️TODO(sanic_fechapago)` | DateTime | ✔ | Auto al registrar. **Sin equivalente en el esquema real de `pago`.** |
| Propietario | `ownerid` | Owner | ✔ | = el **localizador** (User-level). |

### Llaves
- **Ninguna.** Se crea en la app (GUID). El `sanic_cm_pg_numerorecibo` es del **talonario físico**, sin constraint
  de unicidad (ver consideraciones).

### Import
- **N/A** — **no viene del CSV.** La crea la **app de campo** (offline) cuando el localizador marca
  `aplicapago` en una interacción. `sanic_cm_pg_numero` lo asigna el servidor al **sincronizar**.

### Ciclo de vida
- **Nace:** en la **app de campo** (offline), hijo de una interacción. Owner = el localizador.
- **Vive:** **append-only** (registro financiero del cobro). No se edita.
- **Fin:** **NO se borra** de rutina (registro financiero).

### Cómo borrar
- **Entrante:** `interaccion → pago` = **`Cascade`** — se va con su interacción (la barrera está arriba, en
  `cuenta → interaccion = Restrict`).
- Es **hoja** (no tiene hijos). En la práctica **nunca se borra solo** (solo si se borra deliberadamente su interacción).

### Consideraciones al borrar
- **Decimal, no Money** — consistente con el import; cada pago lleva **su** moneda (`sanic_cm_pg_moneda`).
- **Unicidad del recibo:** el `sanic_cm_pg_numerorecibo` viene del **talonario físico**. Hoy es texto sin
  constraint. Si se quisiera evitar duplicados, la clave correcta sería **`localizador + numerorecibo`** (dos
  talonarios distintos pueden repetir número). **No** se implementa ahora; queda anotado.

### Estado de la revisión
✅ Import (N/A) · ✅ Llaves (ninguna) · ✅ Lifecycle · ✅ Cómo borrar (Cascade desde interacción) · ✅ Consideraciones.

---

## 11. `sanic_cm_tbl_asignacion` — Asignación (la cartera viva)

### Definición
**Qué cuenta se asignó a qué localizador**, de qué **tipo**, en qué **estado** y con qué **orden**. Es una
tabla **operativa viva** (no un snapshot), separada del histórico financiero `cargadiaria`. Es lo que cada
localizador **tiene que cobrar**.

### Campos

**Base / operativo**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_as_numero` | Autonumber (`ASG-{SEQNUM:6}`) | auto | **Primary name.** |
| Número de cuenta | `sanic_cm_as_numerocuenta` | Text | ✔ | Copiado para la alt key. |
| Cuenta | `sanic_cm_as_cuentaid` | Lookup → `sanic_cm_tbl_cuenta` | ✔ | Resuelto por `NumeroCuenta`. **Toda asignación tiene cuenta.** |
| Agente | `sanic_cm_as_agenteid` | Lookup → `sanic_cm_tbl_agente` | — | El localizador. En Especiales lo define el **Supervisor**. |
| Tipo | `sanic_cm_as_tipo` | Choice (`Diaria`/`Incobrable`/`Especial`) | ✔ | Ciclo de vida por tipo. Choice: `sanic_choice_cm_tipoasignacion`. |
| Estado | `sanic_cm_as_estado` | Choice (`Activa`/`Cerrada`) | ✔ | Default `Activa`. |
| Orden | `sanic_cm_as_orden` | Whole Number | — | Prioridad del localizador. **NO se mapea en el import** (no se pisa). |
| Fecha de asignación | `sanic_cm_as_fechasignacion` | DateTime | — | — |
| Última carga | `⚠️TODO(sanic_ultimacarga)` | DateTime | — | = `FechaCarga`; la setea el import en cada upsert. Base del **sweep** (DEC-018). **Sin equivalente en el esquema real.** |
| Motivo de cierre | `sanic_cm_as_motivo` | Text | — | Al cerrar (sweep, manual, reclasificación). |

**Solo Especiales (workflow de aprobación)**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Estado de aprobación | `sanic_cm_as_estadoaprobacion` | Choice (`Borrador`/`Pendiente`/`Aprobada`/`Rechazada`) | — | Gobierna la aprobación. Choice: `sanic_choice_cm_estadoaprobacionespecial`. |
| Supervisor | `sanic_cm_as_resolutorid` | Lookup → `systemuser` | — | Quién aprueba/rechaza (resolutor). |
| Creada por | `sanic_cm_as_solicitadopor` | Lookup → `systemuser` | — | El **Solicitante** (o servicio de import si vino por CSV). |
| Motivo de rechazo | `⚠️TODO(sanic_motivorechazo)` | Text | — | Si el Supervisor la rechaza. **Sin equivalente en el esquema real** (solo existe `sanic_cm_as_motivo` genérico). |
| Propietario | `ownerid` | Owner | ✔ | = el **localizador** (lo setea un plugin desde `agente.sanic_cm_ag_usuarioid`). |

### Llaves
- **Alt key compuesta: `sanic_cm_as_numerocuenta` + `sanic_cm_as_tipo`** (DEC-016). Una asignación activa por
  (cuenta, tipo) → coexisten **`{Diaria|Incobrable}` + `Especial`**, **nunca** Diaria + Incobrable (DEC-021). **Active.**

### Import
- **Table Mapping #12**, processing order **4**. Puebla **Diaria/Incobrable** (y **Especial → `Pendiente`**, DEC-022).
- **Mapea:** `NumeroCuenta → numerocuenta`, `TipoAsignacion → tipo` (texto→Choice), `FechaCarga → ultimacarga`,
  `estado` = default `Activa`. **NO mapea** `orden` (no pisar la prioridad).
- **Relationship Mappings:** `Cuenta` (NumeroCuenta) · `Agente` (Agente).
- **Owner:** lo setea un **plugin/flow** (no Smelter), desde `agente.sanic_cm_ag_usuarioid` (Comp. 01 §6).
- **Reclasificación (DEC-021):** al llegar un core-type, el import **cierra el otro core-type activo**.
- **Guarda Especial (DEC-022):** `estadoaprobacion = Pendiente` **solo al crear**; un re-import **no** revierte una ya aprobada.

### Ciclo de vida (por tipo)
| Tipo | Nace | Reasigna | Cierra | Notas |
|------|------|----------|--------|-------|
| **Diaria** | CSV diario | — (gana el CSV; manual interino) | **Admin** (reconciliación, función en app, DEC-018) | Si desaparece → la reconcilia el Admin. |
| **Incobrable** | CSV (1–2/mes) | **Admin** (gana el CSV) | **Admin** (manual + motivo, DEC-020) | Persiste; NO se reconcilia. |
| **Especial** | App (Solicitante) o CSV→`Pendiente` | **Supervisor** | **Admin** (manual; sin vencimiento, DEC-022) | Workflow: Borrador→Pendiente→[Supervisor] Aprobada/Rechazada. 2 campos de estado. |

- **Estados:** `estado` (Activa/Cerrada) gobierna la vigencia; `estadoaprobacion` gobierna la aprobación de
  Especiales. Cartera del localizador = suya + `estado = Activa` (+ `estadoaprobacion = Aprobada` si Especial).
- **Auditoría:** habilitada en `agente`/`owner`/`tipo`/`estado` → historial de reasignaciones (DEC-016).

### Cómo borrar
- **Entrante:** `cuenta → asignacion` = **`Cascade`** — borrar la cuenta borra sus asignaciones.
- Es **hoja** (no tiene hijos). El historial de reasignaciones **no se pierde** (vive en la auditoría, DEC-016).

### Consideraciones al borrar
- **CERRAR ≠ BORRAR:** el fin de vida operativo es **`estado = Cerrada`** (sweep/manual/reclasificación), **no**
  un borrado. Las cerradas **se conservan** como rastro.
- **Borrado físico** (opcional, muy posterior): bulk-delete de `Cerradas` viejas (>1 año), si hiciera falta;
  no es rutina. La tabla **crece lento** (upsert por `numerocuenta+tipo`, no por día).
- **Owner:** si el `agente` no tiene `sanic_cm_ag_usuarioid` vinculado, la asignación queda sin owner correcto → el
  localizador no la ve (vista de control "agentes sin usuario vinculado").

### Estado de la revisión
✅ Import · ✅ Llaves · ✅ Lifecycle · ✅ Cómo borrar (Cascade desde cuenta) · ✅ Consideraciones.

---

## 12. `sanic_cm_tbl_actualizaciondatos` — Cambios de contacto pedidos

### Definición
Captura los **nuevos valores de contacto** que el cliente pide actualizar **durante una interacción**
(teléfonos, direcciones, correo). Es una **bandeja de cambios pedidos** que la **oficina procesa** — el
localizador no edita los datos maestros (no tiene permiso). Tabla **aparte** (no columnas en `interaccion`,
para no ensuciar la tabla núcleo).

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_ad_numero` | Autonumber (`ACT-{SEQNUM:6}`) | auto | **Primary name.** Server-assigned al sincronizar. |
| Interacción | `sanic_cm_ad_interaccionid` | Lookup → `sanic_cm_tbl_interaccion` | ✔ | El vínculo (1:N, en la práctica 1:1). |
| Teléfono casa | `sanic_cm_ad_telefonocasa` | Text | — | Nuevo número de casa → aplica a `telefono` (Casa). |
| Teléfono celular | `sanic_cm_ad_telefonocelular` | Text | — | Nuevo celular → aplica a `telefono` (Celular). |
| Teléfono trabajo | `sanic_cm_ad_telefonotrabajo` | Text | — | Nuevo trabajo → **capture-only** (no se aplica al master). |
| Dirección casa | `sanic_cm_ad_direccioncasa` | Text | — | → aplica a `direccion` (Casa). |
| Dirección EC | `sanic_cm_ad_direccionec` | Text | — | → aplica a `direccion` (EC). |
| Dirección trabajo | `sanic_cm_ad_direcciontrabajo` | Text | — | → aplica a `direccion` (Trabajo). |
| Correo | `sanic_cm_ad_correoelectronico` | Text (formato Email) | — | Nuevo correo → **capture-only** (no se aplica al master). |
| Propietario | `ownerid` | Owner | ✔ | = el **localizador** (User-level). |

> **Todos los campos de dato son opcionales:** se completa **solo lo que cambió**.

### Llaves
- **Ninguna.** Se crea en la app (GUID), ligada a una interacción.

### Import
- **N/A** — **no viene del CSV.** La crea la **app de campo** (offline) cuando el localizador marca
  `actualizacliente` en una interacción.

### Ciclo de vida
- **Nace:** en la **app de campo** (offline), hija de una interacción. Owner = el localizador.
- **Vive:** la **oficina aplica** los cambios a los datos maestros:
  - **Aplicables al master:** `telefonocasa`/`celular` → `telefono`; `direccioncasa`/`ec`/`trabajo` → `direccion`.
  - **Capture-only** (no se aplican; se transcriben al core): `correo` y `telefonotrabajo`.
- **Fin:** se va con su interacción (no se borra de rutina).

### Cómo borrar
- **Entrante:** `interaccion → actualizaciondatos` = **`Cascade`** — se va con su interacción.
- Es **hoja** (no tiene hijos).

### Consideraciones al borrar
- **Es una SOLICITUD, no el dato vivo:** no modifica `cliente`/`telefono`/`direccion`; la **oficina** aplica
  el cambio (proceso de aplicación = lógica de negocio, su propio diseño).
- El **proceso de aplicación** de cambios al master todavía es **pendiente de diseño** (otra fase).

### Estado de la revisión
✅ Import (N/A) · ✅ Llaves (ninguna) · ✅ Lifecycle · ✅ Cómo borrar (Cascade desde interacción) · ✅ Consideraciones.

---

> **✅ Catálogo completo — las 12 tablas revisadas** (Import · Llaves · Lifecycle · Cómo borrar · Consideraciones).
