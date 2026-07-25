# Nomenclatura y Esquema Real (fuente de verdad)

> **Extraído del entorno Dev** (`cm_core_cobranzamovil` v1.0.0.0, publisher *Sistemas Abiertos Nicaragua*)
> vía `pac solution export` + `unpack` (2026-07-25). **Esto es lo que EXISTE**, no lo que dicen los docs
> viejos. Ante cualquier conflicto con otro MD, **gana este archivo** hasta que se reconcilien todos.

## 1. La convención (estilo sigil, marcador `cm`)

| Elemento | Patrón | Ejemplo |
|----------|--------|---------|
| Tabla | `sanic_cm_tbl_<nombre>` | `sanic_cm_tbl_interaccion` |
| Columna | `sanic_cm_<abrev-tabla>_<nombre>` | `sanic_cm_in_estadograbacion` |
| Clave alterna | `sanic_cm_pk_<...>` | `sanic_cm_pk_cliente_cif` |
| Choice global | `sanic_cm_choice_*` **o** `sanic_choice_cm_*` ⚠️ | `sanic_cm_choice_estadograbacioninteraccion` |
| Rol | `SR \| CM \| <rol>` | `SR \| CM \| Backoffice` |
| Plugin / Custom API | `PlugIn \| CM \| <acción>` | `PlugIn \| CM \| Marcar interaccion...` |

### Abreviaturas de columna por tabla

| Tabla | Abrev | Tabla | Abrev |
|-------|:-----:|-------|:-----:|
| cliente | `cl` | cuenta | `ct` |
| producto | `pr` | cargadiaria | `cd` |
| agente | `ag` | interaccion | `in` |
| fiador | `fi` | pago | `pg` |
| direccion | `dr` | asignacion | `as` |
| telefono | `tl` | actualizaciondatos | `ad` |

## 2. Traducción docs viejos → esquema real (columnas clave)

**`sanic_cm_tbl_interaccion`** (la más usada en la app):

| Doc viejo | Real |
|-----------|------|
| `sanic_numero` (autonumber) | `sanic_cm_in_numerointeraccion` |
| `sanic_codigocore` | `sanic_cm_in_codigograbacion` |
| `sanic_estadotranscripcion` | `sanic_cm_in_estadograbacion` |
| `sanic_fechagrabacore` | `sanic_cm_in_fechagrabacion` |
| `sanic_grabadaporid` | `sanic_cm_in_grabadopor` |
| `sanic_comentario` | `sanic_cm_in_comentario` |
| `sanic_aplicapago` | `sanic_cm_cd_aplicapago` ⚠️ (ver §4) |
| `sanic_cuentaid` | `sanic_cm_in_cuentaid` |
| (agente / nombre) | `sanic_cm_in_agente` · `sanic_cm_in_nombreagente` |
| (lat/long) | `sanic_cm_in_latitud` · `sanic_cm_in_longitud` |
| `sanic_actualizainfo` | `sanic_cm_in_actualizainfo` |

**`sanic_cm_tbl_cuenta`:**

| Doc viejo | Real |
|-----------|------|
| `sanic_totalsaldovencido` | `sanic_cm_ct_totalsaldovencido` |
| `sanic_saldovencidonio` / `usd` | `sanic_cm_ct_saldovencidonio` / `_usd` |
| `sanic_retironio` / `usd` | `sanic_cm_ct_retironio` / `_usd` |
| `sanic_pagominimonio` / `usd` | `sanic_cm_ct_pagominimonio` / `_usd` |
| `sanic_contagiadousd` | `sanic_cm_ct_contagiadousd` |
| `sanic_diasmora` · `sanic_nivelmora` | `sanic_cm_ct_diasmora` · `sanic_cm_ct_nivelmora` |
| `sanic_clienteid` | `sanic_cm_ct_clienteid` |

**`sanic_cm_tbl_asignacion`:**

| Doc viejo | Real |
|-----------|------|
| `sanic_orden` | `sanic_cm_as_orden` |
| `sanic_estado` | `sanic_cm_as_estado` |
| `sanic_estadoaprobacion` | `sanic_cm_as_estadoaprobacion` |
| `sanic_agenteid` | `sanic_cm_as_agenteid` |
| `sanic_cuentaid` | `sanic_cm_as_cuentaid` |

**`sanic_cm_tbl_cliente`:** `sanic_cif`→`sanic_cm_cl_cif` · `sanic_nombre`→`sanic_cm_cl_nombre` ·
`sanic_departamento`→`sanic_cm_cl_departamento` · `sanic_distrito`→`sanic_cm_cl_distrito` ·
`sanic_trabajo`→`sanic_cm_cl_trabajo`.

**`sanic_cm_tbl_agente`:** `sanic_nombre`→`sanic_cm_ag_nombre` · `sanic_usuario`→`sanic_cm_ag_usuarioid` ·
`sanic_activo`→`sanic_cm_ag_activo`.

### Navegación (impacta las fórmulas Power Fx de la app)

Para llegar del cliente desde una asignación, la cadena real es:
```
sanic_cm_as_cuentaid . sanic_cm_ct_clienteid . sanic_cm_cl_cif
```
(antes en los docs: `sanic_cuentaid.sanic_clienteid.sanic_cif`).

## 3. Choices (global option sets)

| Choice | Uso |
|--------|-----|
| `sanic_cm_choice_estadograbacioninteraccion` | estado de grabación de la interacción (valor `'2'` = grabada) |
| `sanic_choice_cm_estadoaprobacionespecial` | aprobación de asignación Especial |
| `sanic_choice_cm_monedaenpago` | moneda del pago |
| `sanic_choice_cm_tipoasignacion` | tipo de asignación |

## 4. Inconsistencias conocidas (estilo `typos-conocidos` de sigil)

Documentadas **a propósito** — no las "arregles" sin evaluar el costo de renombrar esquemas publicados:

1. **Orden de `cm`/`choice` inconsistente:** `sanic_cm_choice_estadograbacioninteraccion` (cm→choice) vs.
   `sanic_choice_cm_*` (choice→cm) en las otras tres. Convención mixta.
2. **`sanic_cm_cd_aplicapago` vive en `interaccion`**, no en cargadiaria — arrastra el prefijo `cd`
   (probable copy de cargadiaria). El nombre engaña, la columna es de interacción.
3. **Comando "Marcar como grabada":** su `OnSelect` está en una *command component library* (tipo canvas)
   que el sync **poda** → la fórmula NO es diffeable. Ver [doc 10](../desarrollo/10-estructura-del-repositorio.md).
   Pendiente confirmar si invoca el Custom API `sanic_PlugInCMMarcarinteraccioncomograbadaenCORE` o hace
   `Patch` inline (define si el plugin se usa o está huérfano).

## 5. Tablas del modelo (12)

`sanic_cm_tbl_`: `cliente`, `producto`, `agente`, `fiador`, `direccion`, `telefono`, `cuenta`,
`cargadiaria`, `interaccion`, `pago`, `asignacion`, `actualizaciondatos`.

> El listado completo de columnas por tabla se obtiene de `solutions/unpacked/Entities/*/Entity.xml`
> (fuente viva). Este doc mapea lo que las guías/fórmulas referencian con más frecuencia.

## 6. Columnas agregadas el 2026-07-25 (✅ ya existen en el entorno)

Creadas para cerrar huecos de diseño (ver [divergencias-diseno-vs-build.md](divergencias-diseno-vs-build.md)),
vía el workflow `add-columns-oneshot` (Web API + Service Principal), verificadas por GET. **Ya son parte del
esquema real.**

| Tabla | Columna | Tipo | Quién la setea | Para qué |
|-------|---------|------|----------------|----------|
| `sanic_cm_tbl_interaccion` | `sanic_cm_in_fechacaptura` | DateTime | **la app** (al crear, `Now()`) | fecha/hora real de la gestión en campo (offline) — `createdon` sería la hora de sync |
| `sanic_cm_tbl_pago` | `sanic_cm_pg_fecha` | DateTime | **la app** (al crear) | fecha real del pago registrado en campo |
| `sanic_cm_tbl_asignacion` | `sanic_cm_as_ultimacarga` | DateTime | import/servidor | ancla del sweep DEC-018 (última vez que la Diaria vino en el CSV) |
| `sanic_cm_tbl_asignacion` | `sanic_cm_as_fechacierre` | DateTime | servidor/sweep | cuándo se cerró la asignación |
| `sanic_cm_tbl_asignacion` | `sanic_cm_as_motivorechazo` | Text (2000) | Supervisor | motivo de rechazo de Especial (separado del `sanic_cm_as_motivo` genérico) |

> Falta **cablearlas** en la app (`sanic_cm_in_fechacaptura: Now()` en el Patch de interacción,
> `sanic_cm_pg_fecha: Now()` en el de pago) y en el sweep. El próximo `solution-release` las va a reflejar en
> `solutions/unpacked/`.

### Columnas DENORMALIZADAS en `cuenta` (2026-07-25) — para offline sin navegación

La app del localizador **offline no resuelve navegación de relaciones** (ver
[app-offline](../desarrollo/13-rediseno-offline-canvas.md)). Estas 5 columnas escalares en
`sanic_cm_tbl_cuenta` **las llena Smelter del CSV** (que ya trae todo denormalizado), y la app las lee
directo (join local por número de cuenta), sin `.'[CT] Cliente'.'[CL] ...'`.

| Columna | Display | Tipo | Origen CSV |
|---------|---------|------|-----------|
| `sanic_cm_ct_nombrecliente` | `[CT] Nombre Cliente` | Text | `Cliente` |
| `sanic_cm_ct_cifcliente` | `[CT] CIF Cliente` | Text | `CIF` |
| `sanic_cm_ct_tipoproducto` | `[CT] Tipo Producto` | Text | `TipoProducto` |
| `sanic_cm_ct_nombrefiador` | `[CT] Nombre Fiador` | Text | `NombreFianza` |
| `sanic_cm_ct_direccionfiador` | `[CT] Direccion Fiador` | Text | `DireccionFianza` |

> **Pendiente:** agregar estos 5 mapeos al Table Mapping de `cuenta` en Smelter
> ([cobranza-import-config](cobranza-import-config.md)).
