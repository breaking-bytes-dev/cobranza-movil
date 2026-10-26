# Auditoría del contrato de datos — Cobranza Móvil

> **Qué es esto.** Una auditoría de **integración** entre las piezas de la solución (carga diaria Smelter, app del
> cobrador, backoffice canvas, flow de owner, business rules, seguridad y perfil offline) mirando **el dato que
> viaja entre ellas**: quién lo escribe, quién lo lee, con qué columna, y qué se rompe cuando no coinciden.
>
> - **Fecha:** 2026-10-03/04 · **Entorno:** DEV (`org36e60d9d`) · **Modo:** solo lectura (GET sobre Dataverse + lectura de código).
> - **No se corrigió nada.** Todo lo que figura como "corrección propuesta" es una propuesta con tradeoffs, no un cambio aplicado.
> - **Smelter:** solo se leyó (template `Cobranza movil` en DEV: 12 table mappings, column mappings, alt keys). No se tocó.
> - **Cierres posteriores:** el texto de cada hallazgo es la evidencia del 2026-10-03/04 y no se reescribió. Lo que se corrigió después lleva al final de su sección una nota **Estado (fecha)**.

## 0. Cómo leer este documento

| Sección | Contenido |
|---------|-----------|
| 1 | Resumen ejecutivo + lista ordenada por severidad |
| 2 | Fuentes y método (y qué NO pude verificar) |
| 3 | Matriz productor/consumidor por tabla y columna clave |
| 4 | Bugs `DAT-NN` en detalle (evidencia, impacto, corrección, confianza) |
| 5 | Ciclo de vida de la asignación con puntos de ruptura |
| 6 | Business rules y flow: qué hacen de verdad y dónde corren |
| 7 | Respuestas puntuales (cierre / reasignación en el teléfono, cascadas, numeración, AD) |
| 8 | Lo que está bien (verificado OK) |
| 9 | Drift documentación vs build |
| 10 | Orden de ataque sugerido y verificaciones pendientes |
| Apéndice | Consultas usadas |

**Escala de severidad:** *Crítica* = el dato que ve/graba un humano con plata o gestión de campo es incorrecto hoy ·
*Alta* = se rompe el aislamiento, la trazabilidad o el ciclo de vida · *Media* = se rompe en un caso borde razonable o escala mal ·
*Baja* = deuda, huérfanos, drift.
**Confianza:** *Verificado* = lo vi en metadata/datos/código y reproduce · *Probable* = la lógica del código/metadata lo implica pero no lo ejecuté en un dispositivo ·
*Hipótesis* = plausible, necesita una prueba que indico.

**Convención de rutas:** `LOC/…` = app del cobrador **publicada** (msapp extraído en el scratchpad de la sesión:
`…/scratchpad/loc-publicada/msapp/Src/`), `BO/…` = `apps/backoffice-canvas/Src/`, `WF/…` = `solutions/unpacked/Workflows/`.

---

## 1. Resumen ejecutivo

El modelo está bien armado en lo estructural (alt keys activas, choices consistentes entre apps y metadata, CIF/número de cuenta
sin divergencias en los datos de DEV). El problema de fondo es **otro**: las piezas se comunican por **columnas distintas del mismo concepto**
(texto denormalizado vs lookup, asignación vs cuenta) y por **reglas que viven solo en el cliente** (obligatoriedad, bloqueos, filtros de aprobación).
Cuando el cliente es un teléfono offline, un canvas Patch o un import, esas reglas no existen para el servidor.

Los cuatro hallazgos que hoy te dejan datos incorrectos delante de una persona:

1. **DAT-01** — la Especial creada en backoffice guarda el retiro en la *asignación*; el teléfono lo lee de la *cuenta*. El cobrador ve `C$ 0.00` **o, peor, el retiro de la última carga CSV** (en DEV: asignación pide 1.000, cuenta dice 46.048,91).
2. **DAT-02** — el backoffice calcula "Cobrado hoy" y el arqueo con `Fecha del pago`, que **ningún productor escribe** (3 de 3 pagos con `pg_fecha = null`). Esos totales dan 0 siempre.
3. **DAT-03** — la transcripción une pagos con interacciones por un **texto** (`Numero Interaccion`) que el teléfono copia de un autonumber que **offline todavía no existe**.
4. **DAT-04** — las interacciones del teléfono llegan sin `[IN] Agente` ni `[IN] Cuenta`; el backoffice navega justamente ese lookup para mostrar cliente/CIF/producto y el "antes" de la actualización de datos.

Y tres de ciclo de vida que van a morder apenas haya más de un cobrador real: **DAT-07/08** (el flow de owner no cubre agentes sin usuario ni re-vinculaciones;
en DEV 871 de 1.008 asignaciones apuntan a agentes sin usuario y 870 están owned por SYSTEM), **DAT-06** (el perfil offline es el *autogenerado "All records"*, no el acotado de la guía 13:
cada teléfono baja *todas* las cuentas/clientes/direcciones/teléfonos que el rol puede leer) y **DAT-10** (una Diaria reconciliada que reaparece en el CSV queda `Cerrada` para siempre).

### Lista ordenada por severidad

| ID | Sev. | Título | Confianza |
|----|:----:|--------|-----------|
| DAT-01 | Crítica | Especial: retiro/nota/tipo se escriben en la asignación y el teléfono los lee de la cuenta (0.00 o monto ajeno) | Verificado |
| DAT-02 | Crítica | "Cobrado hoy" y arqueo del backoffice filtran por `pg_fecha`, que nadie escribe | Verificado |
| DAT-03 | Crítica | Pagos creados offline no aparecen al transcribir (join por texto autonumber) | Probable |
| DAT-04 | Crítica | Interacciones del teléfono sin lookup `Agente`/`Cuenta` rompen la transcripción | Verificado (síntoma) / Hipótesis (causa) |
| DAT-05 | Alta | Latitud/longitud con precisión 2 (≈1,1 km) y geo nula sin bloqueo | Verificado |
| DAT-06 | Alta | Perfil offline autogenerado "All records" + Read Global: todo el padrón en cada teléfono | Verificado |
| DAT-07 | Alta | Flow de owner no actúa si el agente no tiene usuario (ni al reasignar) | Verificado |
| DAT-08 | Alta | Vincular/cambiar el usuario del agente no propaga el owner a sus asignaciones | Verificado (1 caso en DEV) |
| DAT-09 | Alta | El teléfono no filtra aprobación: Especial Pendiente/Borrador/Rechazada visible (CSV y reactivación) | Verificado |
| DAT-10 | Alta | Diaria reconciliada que reaparece en el CSV no se reactiva (Smelter no mapea `estado`) | Probable |
| DAT-11 | Alta | Sin FLS y con bloqueos solo de formulario: integridad y doble transcripción sin defensa del servidor | Verificado |
| DAT-12 | Alta | Auditoría de tabla desactivada (asignación, interacción, pago, agente): 0 filas de auditoría | Verificado |
| DAT-13 | Media | `ApplicationRequired` no se aplica en servidor: asignaciones sin `numerocuenta` y duplicados de Especial | Verificado |
| DAT-14 | Media | Solicitante (Read Basic): anti-duplicado ciego y Rechazada que bloquea la re-solicitud | Probable |
| DAT-15 | Media | Grano asignación vs cuenta: totales duplicados e interacciones dobles; reclasificación DEC-021 sin implementar | Probable |
| DAT-16 | Media | `Orden`: el import no lo inicializa, la BR "Orden inicial" está en borrador y el swap se rompe con empates | Verificado |
| DAT-17 | Media | `Email Usuario` (texto) vs `[AG] Usuario` (lookup): dos fuentes de verdad para "agente con usuario" | Probable |
| DAT-18 | Media | Cascadas peligrosas: Cliente→Cuenta→Asignación (assign/share/delete) e Interacción→Pago (delete) | Verificado (metadata) / Hipótesis (impacto) |
| DAT-19 | Media | Actualización de datos: ciclo sin cierre (sin estado, nadie la aplica a los maestros) | Verificado |
| DAT-20 | Media | Backoffice crea Cliente/Tel/Dir pero no hay forma de crear Cuenta: Especial para cliente nuevo imposible | Verificado |
| DAT-21 | Media | Teléfono sin `MaxLength`: rechazo diferido en el sync (recibo 30, tel 20, correo 100) | Probable |
| DAT-22 | Media | Pago con recibo vacío se persiste; recibo sin unicidad | Verificado |
| DAT-23 | Media | Refresco del teléfono no recarga cuentas/direcciones: asignación nueva sin cuenta | Probable |
| DAT-24 | Media | Asignaciones cerradas nunca salen del dispositivo (perfil sin filtro de estado, sin desactivar) | Probable |
| DAT-25 | Media | Flow de owner: conexión personal, sin Share en el rol de servicio y una corrida por fila por día | Verificado |
| DAT-26 | Media | Nombre/CIF del cliente duplicado (cliente vs cuenta): Maestros edita el que nadie lee; `fechapago` texto mixto | Verificado |
| DAT-27 | Media | "Hoy" del teléfono por `Created On` (hora de sync) en vez de fecha de captura; filtro `= Today()` imposible | Verificado |
| DAT-28 | Baja | `estadoaprobacion` default = Pendiente: las Diarias/Incobrables nacen "pendientes de aprobación" | Verificado |
| DAT-29 | Baja | Reasignación manual sin UI en canvas; la BR de bloqueo la impide en el formulario MDA | Verificado |
| DAT-30 | Baja | Huérfanos: `usuarioapp` (borrada el 2026-10-15), `producto`/`fiador` (solo import), `ct_agenteid`; dirección EC no se muestra | Verificado |
| DAT-31 | Baja | Números autonumber usados como clave de negocio sin alt key; `fechasignacion` pisada por el import | Verificado |
| DAT-32 | Baja | Drift de documentación vs build (formatos, precisión, roles, cascadas) | Verificado |

---

## 2. Fuentes y método

- **Metadata viva** (`EntityDefinitions` + atributos + relaciones + keys + roles/privilegios + profiles offline + workflows) volcada a archivo y analizada offline.
- **Datos de DEV** (solo conteos/ids truncados; sin datos de clientes): 1.009 asignaciones, 1.008 cuentas, 961 clientes, 2.699 direcciones, 1.061 teléfonos, 11 interacciones, 3 pagos, 2 actualizaciones de datos, 20 agentes, 1.000 filas de carga diaria (todas `2026-06-20`).
- **Código:** app del cobrador *publicada* (7 pantallas + 4 componentes), backoffice canvas (11 pantallas + 4 componentes), flow `Cambio de Owner en Asignaciones` (JSON), 3 business rules (XAML), template Smelter `Cobranza movil` (leído de las tablas `sanic_tbl_smlt_*`).
- **Docs:** `docs/desarrollo`, `docs/guias`, `docs/referencia` (componentes 08–15, divergencias, import config, Smelter).

**No pude verificar (y lo marco donde corresponde):**
- La *causa raíz* de los lookups vacíos del teléfono (DAT-04): no hay Monitor/trace; solo el síntoma en datos.
- Comportamiento del sync offline de Power Apps al perder acceso a un registro (DAT-24, sección 7): lo deduzco de cómo funciona el perfil, sin dispositivo.
- Si Smelter, al hacer upsert, escribe *solo* las columnas mapeadas (DAT-10). Es la semántica estándar de `UpsertRequest`, pero no leí el código del motor.
- `PrincipalObjectAccess` (shares vigentes): no es consultable por Web API de forma directa.

---

## 3. Matriz productor / consumidor

Leyenda: **SM** = Smelter (import CSV diario) · **LOC** = app del cobrador · **BO** = backoffice canvas (a: Admin, b: BackOffice, s: Supervisor, sol: Solicitante) ·
**FL** = flow de owner · **—** = nadie. Una columna "escrita pero nunca leída" o "leída pero nunca escrita" va marcada con ⚠.

### 3.1 `sanic_cm_tbl_cliente` (alt key `cl_cif`)

| Columna | Escribe | Lee | Notas |
|---------|---------|-----|-------|
| `cl_cif` (20) | SM, BO-a (Maestros: crear) | BO-a (Maestros) | Alt key. Copiado como texto a `dr_cif`, `tl_cif`, `ct_cifcliente`. LOC **no** lo lee: agrupa por `cuenta.cifcliente`. |
| `cl_nombre` (200) | SM, BO-a (Maestros editar) | BO-a (Maestros) | ⚠ Las demás pantallas leen `ct_nombrecliente` (DAT-26). |
| `cl_departamento/distrito/trabajo` | SM, BO-a | BO-a | LOC no los muestra. |

### 3.2 `sanic_cm_tbl_cuenta` (alt key `ct_numerodecuenta`, len 20)

| Columna | Escribe | Lee | Notas |
|---------|---------|-----|-------|
| `ct_numerodecuenta` | SM | LOC (26 usos, clave de join por texto), BO | Clave de join de **todo**: LOC la usa para unir con `as_numerocuenta` e `in_numerocuenta`. |
| `ct_clienteid` / `ct_productoid` / `ct_fiador` / `ct_agenteid` (lookups) | SM | BO (1 uso) | ⚠ `ct_agenteid`, `ct_productoid`, `ct_fiador`: escritas y nunca leídas. `ct_agenteid` diverge de `as_agenteid` tras cualquier reasignación manual (3 casos en DEV, todos de QA). |
| `ct_cifcliente`, `ct_nombrecliente`, `ct_tipoproducto`, `ct_nombrefiador`, `ct_direccionfiador` | SM | LOC, BO | Denormalizados *a propósito* para el offline. Las apps leen **estos**, no los maestros. |
| `ct_saldovencido*`, `ct_pagominimo*`, `ct_contagiadousd`, `ct_totalsaldovencido`, `ct_diasmora`, `ct_nivelmora`, `ct_ciclo` | SM | LOC (detalle), BO (Cierres/Aprob/Reconciliación) | Decimal(2). |
| `ct_retironio/usd`, `ct_tipoespecial`, `ct_nota` | SM (solo la carga CSV) | LOC (detalle de cuenta, solo si asignación es Especial) | **BO nunca las escribe** → DAT-01. En DEV 1.001 de 1.008 cuentas tienen retiro > 0 (la CSV lo trae para todas). |
| `ct_fechapago` (texto 100) | SM | LOC | Formatos mezclados (`20/07/2026` y `2026-07-17`) → DAT-26. |
| `ct_tipoasignacion` (texto) | SM | — | ⚠ Escrita, nunca leída (y 7 cuentas la tienen vacía). |

### 3.3 `sanic_cm_tbl_asignacion` (alt key `as_tipo` + `as_numerocuenta`)

| Columna | Escribe | Lee | Notas |
|---------|---------|-----|-------|
| `as_tipo` (1/2/3) | SM (opt-set map `Diaria:1…`), BO-sol (crea Especial=3) | LOC, BO (todas) | **Default de columna = 3 (Especial)**: cualquier alta sin tipo explícito nace Especial. |
| `as_numerocuenta` (20) | SM, BO-sol | LOC (21 usos: join con cuenta), BO | `ApplicationRequired` pero no se aplica en servidor (DAT-13). |
| `as_cuentaid` (lookup) | SM, BO-sol | BO (navega el lookup en 8 pantallas) | LOC **no** lo usa; une por texto. Dos fuentes de verdad (texto vs lookup); 0 divergencias hoy. |
| `as_agenteid` | SM, BO-s (aprobar) | FL (trigger), BO | Dispara el flow de owner. |
| `ownerid` | SM/BO (= creador), **FL** (→ usuario del agente) | LOC (seguridad Basic = "mi cartera") | Ver DAT-07/08. |
| `as_estado` (true=Activa, false=Cerrada; default true) | SM (solo en create por default), BO-s/a (cierre/reconciliación/reapertura) | LOC (`Estado = true`), BO | SM **no** lo mapea → DAT-10. |
| `as_estadoaprobacion` (1 Borrador, 2 Pendiente, 3 Aprobado, 4 Rechazada; **default 2**) | BO-sol, BO-s | BO (Aprobaciones/Mis solicitudes/Inicio) | LOC **nunca** lo lee → DAT-09. |
| `as_retironio/usd` (Decimal 2), `as_notas` (memo) | BO-sol (Nueva solicitud) | BO (Aprobaciones, Cierres, Mis solicitudes) | LOC no las lee → DAT-01. `notas` = `"{tipo} — {motivo}"` concatenado (el tipo de solicitud no es consultable). |
| `as_solicitadopor`, `as_resolutorid` | BO-sol, BO-s | BO; FL (`solicitadopor` para GrantAccess) | |
| `as_motivo`, `as_motivorechazo`, `as_fechacierre` | BO-a (cierre/reconc.), BO-s (rechazo) | BO (Mis solicitudes lee `motivorechazo`) | |
| `as_orden` | **LOC** (UpdateIf masivo al salir de Cartera) | LOC | SM no lo toca (correcto). BO no lo ve. DAT-16. |
| `as_ultimacarga` | SM (`FechaCarga`) | BO (Reconciliación, Inicio) | `UserLocal`: SM la graba a 00:00Z. 3 Diarias de QA con `null` → nunca se reconcilian. |
| `as_fechasignacion` | SM (**pisa con `FechaCarga` en cada upsert**), BO-sol (`Now()`) | BO (orden de Mis solicitudes) | DAT-31. |

### 3.4 `sanic_cm_tbl_interaccion` (sin alt key; autonumber `IN-{SEQNUM:4}` seed 1000)

| Columna | Escribe | Lee | Notas |
|---------|---------|-----|-------|
| `in_numerointeraccion` | **Servidor** (autonumber al insertar) | LOC (9), BO (16) | Offline vacío hasta el sync. Es la clave de join con pagos (texto) → DAT-03. |
| `in_cuentaid` (lookup, ApplicationRequired) | LOC (`varCuenta`) | **BO** (14 usos: nombre, CIF, producto) | Llega vacío desde el teléfono → DAT-04. |
| `in_agente` (lookup, ApplicationRequired) | LOC (`gblAgente`) | — | ⚠ BO **no** lo lee (agrupa por `in_nombreagente`). |
| `in_nombreagente` (200) | LOC (`"Sin nombre"` si `gblAgente` vacío) | BO (16: filtro "localizador") | Dos de las 11 interacciones: `Sin nombre`. |
| `in_numerocuenta` (20) | LOC | LOC (19), BO (4) | Es lo que realmente sostiene al teléfono (LOC lista interacciones por este texto). |
| `in_comentario`, `cd_aplicapago`, `in_actualizainfo`, `in_fechacaptura` | LOC | BO | |
| `in_latitud/longitud` (**Decimal 2**) | LOC (`Location.*`) | BO (2) | DAT-05. |
| `in_estadograbacion` (1 Pendiente, 2 Grabado), `in_codigograbacion` (10), `in_fechagrabacion`, `in_grabadopor` | BO-b (Transcripción) | BO (Transcritas, Inicio) | `fechagrabacion` y `grabadopor` los pone **el cliente** (`Now()`/`gblUsuarioDV`) → DAT-11. |

### 3.5 `sanic_cm_tbl_pago` (autonumber `PG-{SEQNUM:4}`)

| Columna | Escribe | Lee | Notas |
|---------|---------|-----|-------|
| `pg_interaccion` (lookup, ApplicationRequired) | LOC (`varInt`) | LOC (1 uso), **BO: 0** | El vínculo "bueno" que nadie usa. |
| `pg_numerointeraccion` (texto 100) | LOC (copia `varInt.'Numero de Interaccion'`) | LOC (9), **BO (8: join)** | Vacío si el padre se creó offline → DAT-03. |
| `pg_numerocuenta` (texto 100) | LOC | LOC (4) | |
| `pg_monto` (Decimal 2), `pg_moneda` (1 NIO/2 USD), `pg_numerorecibo` (30) | LOC | LOC, BO | Recibo sin unicidad ni obligatoriedad real → DAT-22. |
| `pg_fecha` | **— (nadie)** | BO (4: Inicio, Transcritas) | ⚠ Leída y nunca escrita → DAT-02. |
| `pg_numero` | Servidor | LOC (pendientes de sync) | |

### 3.6 `sanic_cm_tbl_actualizaciondatos` (autonumber `AD-{SEQNUM:4}`)

| Columna | Escribe | Lee | Notas |
|---------|---------|-----|-------|
| `ad_interaccionid` | LOC (`varInt`) | BO (Transcripción: `'[AD] Interaccion'.'…Interaccion' = actual`) | Sin este lookup el backoffice no ve cambios aunque `actualizainfo = true`. |
| `ad_telefono*`, `ad_direccion*`, `ad_correoelectronico` | LOC | BO (muestra "antes" vs "nuevo") | "Antes" sale de `tl_cif`/`dr_cif` = CIF obtenido por **`in_cuentaid`** → depende de DAT-04. Dirección AD 1000 vs `dr_direccion` 200. |
| (sin estado) | — | — | No hay marca de "aplicada" ni escritura a maestros → DAT-19. |

### 3.7 Resto

| Tabla | Escribe | Lee | Notas |
|-------|---------|-----|-------|
| `agente` (alt key `ag_nombre`) | SM (solo nombre), BO-a (`[AG] Usuario` + `Email Usuario` + `Activo`) | LOC (`Email Usuario` → `gblAgente`), BO (`Email Usuario` en aprobaciones/KPI), FL (`[AG] Usuario`) | 16 de 20 agentes sin usuario en DEV. DAT-17. |
| `direccion` (alt key `dr_tipo`+`dr_cif`), `telefono` (alt key `tl_cif`+`tl_tipo`) | SM, BO-a (Maestros: **solo agrega**) | LOC (OnStart filtra Casa/Trabajo), BO (Transcripción "antes", Maestros) | `EC` no se carga en el teléfono. |
| `cargadiaria` (alt key `cd_numerocuenta`+`cd_fechacarga`, DateOnly) | SM | BO (Monitoreo: `CountIf` y `nfUltimaCarga`) | Resto del snapshot: solo histórico (nadie lo lee). Todas las filas de DEV: `2026-06-20`. |
| `producto`, `fiador` | SM | — | ⚠ Declaradas como data source en LOC pero sin fórmula que las lea; fuera del perfil offline autogenerado. |
| `usuarioapp` | — | — | ⚠ Tabla huérfana: 0 referencias en ambas apps, 0 privilegios en todos los roles. El rol se deriva de roles/teams (`gblRol`). **Borrada el 2026-10-15 (DAT-30).** |

---

## 4. Bugs en detalle

### DAT-01 — Especial: el dato vive en la asignación y el teléfono lo lee de la cuenta
**Severidad:** Crítica · **Confianza:** Verificado

**Evidencia**
- Productor: `BO/scrNuevaSolicitud.pa.yaml:595-625` — `Patch('CM | TBL | Asignacions', …)` con `'Retiro NIO'`, `'Retiro USD'`, `Notas: cmbTipoNva.Selected.Value & " — " & motivo`, `'[AS] Solicitado por'`.
- Consumidor: `LOC/scrDetalleCuenta.pa.yaml:568` (`varCuenta.'[CT] Tipo Especial'`), `:604` (`Moneda(varCuenta.'[CT] Retiro NIO', varCuenta.'[CT] Retiro USD')`), `:640` (`varCuenta.'[CT] Nota'`); visibilidad de la sección en `:520` (`Text(varAsig.'[AS] Tipo') = "Especial"`).
- Smelter mapea `RetiroNIO/USD`, `TipoEspecial`, `Nota` **solo a la cuenta** (`sanic_tbl_smlt_columnmapping`, TM 3 `Cuenta`); nadie sincroniza asignación → cuenta.
- Datos DEV: `AS-2041` (Especial creada para QA, retiro 500): `cuenta.ct_retironio = null` → el teléfono muestra `C$ 0.00` (`Moneda()` devuelve `"C$ 0.00"` si ambos son 0/blank). `AS-2001` (Aprobada, `as_retironio = 1000`): `cuenta.ct_retironio = 46.048,91` → el teléfono mostraría **46.048,91**, un monto ajeno.

**Impacto:** el cobrador sale a "traer" un monto equivocado o ninguno. Es el único campo que el cobrador lee que viene de la solicitud; es dato de plata.

**Corrección propuesta**
- **A (recomendada):** que el teléfono lea retiro/nota/tipo **de la asignación** cuando `[AS] Tipo = Especial` (`varAsig.'Retiro NIO'`, `varAsig.Notas`) y de la cuenta solo como respaldo para el flujo CSV. La asignación es el grano correcto (una cuenta puede tener Diaria + Especial) y las columnas ya viajan en el mismo registro offline. Costo: cambio de app.
- **B:** un flow al aprobar que copie a la cuenta. Costo: la cuenta es compartida por todas las asignaciones, la próxima carga CSV la pisa, y con dos solicitudes sucesivas se pisan entre sí.
- Persistir el "tipo de solicitud" en columna propia (hoy va dentro de `Notas` con `" — "`).

---

### DAT-02 — "Cobrado hoy" y arqueo siempre en cero
**Severidad:** Crítica · **Confianza:** Verificado

**Evidencia**
- Consumidores: `BO/scrInicio.pa.yaml:20-23` (`Sum(Filter(Pagos, 'Fecha del pago' >= Today() …))`), `BO/scrTranscritas.pa.yaml:17-18` y `:104` (`colPagosHoyTsc = Filter(Pagos, 'Fecha del pago' >= Today())` y de ahí `colPagosTsc`/`pagosNioTsc`/`pagosUsdTsc`).
- Productor: `LOC/scrInteraccion.pa.yaml:1084-1091` — el `Patch` de `Pagos` escribe `Interaccion`, `Numero Interaccion`, `Numero Cuenta`, `Monto`, `Moneda`, `Numero recibo`. **No** escribe `Fecha del pago`.
- DEV: los 3 pagos tienen `sanic_cm_pg_fecha = null`.
- Los docs lo daban por hecho: `docs/referencia/divergencias-diseno-vs-build.md` ("`sanic_cm_pg_fecha` … la setea la app al crear (`Now()`)") y componente 11 (`Req. ✔`, "auto al registrar").

**Impacto:** el KPI de Inicio y el arqueo de Transcritas (lo que el oficinista concilia con el efectivo) muestran 0 o vacío, sin error alguno.

**Corrección propuesta**
1. Teléfono: agregar `'Fecha del pago': Now()` al `Patch` (es la hora de campo; `Created On` offline es hora de sync).
2. Defensa de servidor: flow/plug-in en `Create` de pago que rellene `pg_fecha = coalesce(pg_fecha, createdon)`. Así un cliente viejo no rompe el arqueo.
3. Backfill de los pagos existentes con `createdon`.
4. Poner la columna en `Required` real (no `ApplicationRequired`, que el canvas no respeta — ver DAT-13) o no depender de ella.

---

### DAT-03 — Pagos creados offline no se ven al transcribir
**Severidad:** Crítica · **Confianza:** Probable (código y diseño lo implican; no probado en dispositivo)

**Evidencia**
- Productor: `LOC/scrInteraccion.pa.yaml:1085-1087` — `'[PG] Numero Interaccion': varInt.'[IN] Numero de Interaccion'`. Ese valor es el autonumber `IN-{SEQNUM:4}`, que el **servidor** asigna al insertar (docs/componentes 10 §7.1 y 11 §7.3: "offline se asigna al sincronizar"). Offline `varInt` no lo tiene → queda vacío.
- Consumidor: `BO/scrTranscripcion.pa.yaml:758-759` (`Filter('CM | TBL | Pagos', '[PG] Numero Interaccion' = varActualTrx.'[IN] Numero de Interaccion')`) y `BO/scrTranscritas.pa.yaml:42-46` (join por el mismo texto). El lookup `[PG] Interaccion` (que sí se escribe) **no se usa nunca en BO** (0 referencias).
- Datos: `PG-1000` tiene `pg_interaccion` seteado y `pg_numerointeraccion = null` (el texto puede faltar con el lookup presente). El teléfono mismo lista pagos por este texto (`LOC/scrCliente.pa.yaml:482-484`, `scrDetalleCuenta.pa.yaml:741-743`) → tampoco los ve.

**Impacto:** la sección "PAGOS (n)" de la transcripción queda vacía para pagos capturados sin señal; el backoffice podría grabar en el core una interacción "con pago" sin ver monto ni recibo.

**Corrección propuesta**
- Join por lookup: `Filter(Pagos, '[PG] Interaccion'.'CM | TBL | Interaccion' = varActualTrx.'CM | TBL | Interaccion')` (misma técnica que ya usan para `Actualizacion de datos`).
- Flow/plug-in en `Create` de pago: `pg_numerointeraccion := interaccion.in_numerointeraccion` (backfill del texto desde el lookup) para los lectores que sigan usando el texto.
- Probar offline con Monitor antes de cerrar (ver sección 10).

---

### DAT-04 — Interacciones del teléfono sin lookup de Agente ni de Cuenta
**Severidad:** Crítica · **Confianza:** Verificado el síntoma; la causa raíz es Hipótesis

**Evidencia**
- DEV: `IN-1035` e `IN-1036` (creadas por el usuario QA "User 5" el 2026-10-04): `in_agente = null`, `in_cuentaid = null`, `in_nombreagente = "Sin nombre"`, `in_numerocuenta` OK. Las 9 anteriores (junio/julio) sí tienen ambos lookups. `IN-1035` además sin lat/long.
- `"Sin nombre"` se genera solo si `gblAgente` está vacío (`LOC/scrInteraccion.pa.yaml:1083`, `Coalesce(gblAgente.'[AG] Nombre', "Sin nombre")`) → **el agente no se resolvió en el dispositivo** (la parte "agente" queda demostrada por los datos).
- `gblAgente` se calcula una vez: `LOC/App.pa.yaml:91` `LookUp(colAgentes, 'Email Usuario' = User().Email)`. Sin guarda: el botón Registrar no exige `gblAgente` (`DisplayMode` solo mira comentario/pago/datos).
- El `Patch` pasa `'[IN] Cuenta': varCuenta`, donde `varCuenta = ThisItem.cta` (`LOC/scrCliente.pa.yaml:702`, `scrInicio.pa.yaml:800`): un **registro anidado dentro de `colAsig`** (creado con `AddColumns(..., cta, LookUp(colCuentas, …))`). Es una copia en colección, no una fila del data source.
- Consumidor: `BO/scrTranscripcion.pa.yaml:17-27` y `BO/scrTranscritas.pa.yaml:29-37` → `Coalesce(ThisRecord.'[IN] Cuenta'.'Nombre Cliente', "—")`, `…'CIF Cliente'`, `…'Tipo Producto'`. Sin lookup: cliente "—", CIF "—", producto "—", y el "antes" de la actualización de datos se calcula con `Filter(Telefonos, '[TL] CIF' = "—")` → vacío (`scrTranscripcion.pa.yaml:59-60`). El agrupamiento "por localizador" usa `in_nombreagente` → bucket "Sin nombre".

**Causas candidatas (en orden de probabilidad)**
1. **Offline + registros de colección:** en modo offline el `Patch` de un lookup con un registro que viene de una colección (copia) no resuelve la referencia; online el mismo código funcionó (las 9 interacciones de julio, mismo patrón en `apps/localizador-canvas/src/scrInteraccion.pa.yaml:922-935`). *Hipótesis.*
2. `gblAgente` vacío por `User().Email` ≠ `Email Usuario` (UPN vs correo principal, cambio de correo, invitado B2B) o por `colAgentes` incompleto al arrancar. Los datos de "User 5" coinciden hoy (verificado), así que no es el caso simple.
3. `OnStart` evaluado antes de que el sync deje `Agente` disponible.

**Impacto:** el flujo de transcripción pierde cliente/CIF/producto y el control de localizador; la actualización de datos se ve sin contexto. `ApplicationRequired` en ambos lookups **no** frenó nada (el canvas no lo respeta; ver DAT-13).

**Corrección propuesta**
- **Server-side (robusta, independiente de la causa):** flow o plug-in síncrono pre-create en `interaccion`: si `in_agente` es null → resolverlo desde `ownerid` (agente cuyo `ag_usuarioid = ownerid`); si `in_cuentaid` es null → resolverlo por `in_numerocuenta` (alt key de cuenta); completar `in_nombreagente`. Cubre también interacciones de clientes viejos.
- Teléfono: pasar `LookUp('CM | TBL | Cuentas', '[CT] Numero de cuenta' = …)` (fila del data source) en vez de `varCuenta`; bloquear Registrar si `IsBlank(gblAgente)`; no reducir a `"Sin nombre"` en silencio.
- Backoffice (defensa): si `'[IN] Cuenta'` es blank, resolver cuenta por `in_numerocuenta` (`LookUp(Cuentas, '[CT] Numero de cuenta' = …)`).

---

### DAT-05 — Latitud/longitud con 2 decimales
**Severidad:** Alta · **Confianza:** Verificado

**Evidencia:** `sanic_cm_in_latitud` y `sanic_cm_in_longitud` = Decimal, **Precision = 2** (metadata). `IN-1036`: `12.14 / -86.25` (el GPS entrega 6+ decimales). Los docs prometen 5 (`componentes/10-tabla-interaccion.md`: "Decimal (5 dec.)"). 2 decimales ≈ 1,1 km de error: la evidencia geográfica de la visita no sirve.
Además `IN-1035` quedó sin geo (el `Patch` no espera ni valida `Location`; docs gotcha 7.3 lo admite como opcional).

**Corrección propuesta:** subir `Precision` a 6 (Decimal admite hasta 10; aumentar es seguro, reducir no). Las filas existentes no se recuperan (ya redondeadas). Para las nulas, decidir si se exige geo o se guarda motivo ("sin GPS"). Un cambio de esquema exige re-sync de los dispositivos.

---

### DAT-06 — Perfil offline: el autogenerado "All records", no el acotado
**Severidad:** Alta · **Confianza:** Verificado

**Evidencia**
- `LOC/Properties.json`: `"ManualOfflineProfileId": ""` y `"enabledataverseoffline": true` → la app usa el perfil **autogenerado** `autogenProfile-sanic_appcobranzamovil_c4e67`.
- `mobileofflineprofileitems` de ese perfil: `cliente, actualizaciondatos, agente, asignacion, cuenta, pago, direccion, interaccion, telefono` — **todas** con `recorddistributioncriteria = 1` (*All records*).
- Rol `SR | CM | Localizador` (privilegios vivos): `cuenta/cliente/direccion/telefono/agente` → **Read = Global**. "All records" + Read Global = cada teléfono baja *todas* las cuentas, clientes, direcciones y teléfonos de la organización (DEV: 1.008 / 961 / 2.699 / 1.061; en producción decenas de miles) — datos personales de clientes que no son de su cartera.
- Existe un perfil manual `App - Cobranza Movil Profile` con el diseño de la guía 13 (`Related rows only` para cuenta/cliente/dir/tel/pago), pero **no está asociado** a la app y además le **falta** `actualizaciondatos`, y trae `producto`/`fiador` que la app no lee.
- La guía `14-estrategia-carga-datos.md` y `13-perfil-offline-rendimiento.md` presentan el perfil acotado como "la solución real"; el entorno no lo usa.

**Impacto:** privacidad (PII ajena en el dispositivo; un teléfono perdido filtra todo el padrón), volumen de sync y arranque lento al escalar, y la defensa de la guía 14 no existe.

**Corrección propuesta**
- Asociar el perfil manual a la app (Studio → Configuración → Offline), agregar `actualizaciondatos`, quitar `producto`/`fiador` si no se usan, y filtrar `asignacion` por `sanic_cm_as_estado eq true` (ver DAT-24). Validar el perfil.
- Alternativa/complemento: bajar Read de `cuenta/cliente/dirección/teléfono` a un alcance que no sea Global — pero `AppendTo Global` en cuenta/agente es obligatorio para crear la interacción (lookup), y bajar Read rompe `LookUp` en backoffice. Tradeoff: el perfil acotado es más simple.

---

### DAT-07 — El flow de owner no hace nada si el agente no tiene usuario
**Severidad:** Alta · **Confianza:** Verificado

**Evidencia**
- `WF/CloudFlowCMCambiodeOwnerenAsignaciones-….json`: la condición `Verificar_que_el_agente_tenga_asignado_un_usuario` tiene `actions: {}` en la rama "usuario vacío": **no loguea, no alerta, no cambia el owner**. (El comp. 14 prometía "alertar al Admin".)
- DEV: de **1.008** asignaciones con agente, **871** apuntan a un agente sin `[AG] Usuario` (16 de 20 agentes están sin vincular) y **870** están owned por `SYSTEM`. Nadie las ve en el teléfono.
- Reasignación: si una asignación pasa del agente A (con usuario) al agente B (sin usuario), el trigger dispara y el flow sale sin hacer nada → el **owner sigue siendo el cobrador A**, que conserva la asignación en su teléfono. Mismo efecto con un Diario reasignado por la carga CSV.
- El único guardia contra esto está **en una pantalla**: `BO/scrAprobaciones.pa.yaml:635-638` filtra agentes con `'Email Usuario' <> Blank()` (otra columna, ver DAT-17). No protege a la CSV ni a otras vías.

**Impacto:** carteras sin dueño visible; cobrador anterior con acceso indebido; cero trazabilidad del fallo.

**Corrección propuesta:** en la rama sin usuario, (a) asignar a un *team* "Cartera sin cobrador" para quitarle el acceso al anterior, y (b) registrar el caso (tabla de log o correo). Prevenir en origen: validación server-side (plug-in/Custom API) que impida setear `as_agenteid` de un agente sin usuario, o KPI/alerta diaria de "asignaciones con agente sin usuario" (hoy `scrAgentes` cuenta *agentes* sin usuario, no asignaciones afectadas).

**Estado (2026-10-14): cerrado en DEV.** El plugin asíncrono `RepartoAsignacion` le pasa la asignación activa sin agente o con
agente sin usuario al equipo "CM · Cartera sin cobrador · DEV" (sin miembros; rol mínimo `SR | CM | Cartera sin cobrador`) y le
revoca al cobrador anterior lo que no justifique otra asignación suya. Si el equipo no existe, falla sin tocar nada (trabajo
del sistema fallido). Ver [catálogo C.3](../referencia/03-catalogo-de-tablas.md).

---

### DAT-08 — Vincular o cambiar el usuario del agente no propaga el owner
**Severidad:** Alta · **Confianza:** Verificado (1 caso real en DEV; mecanismo por el JSON del flow)

**Evidencia**
- El trigger del flow es `sanic_cm_tbl_asignacion`, `filteringattributes: sanic_cm_as_agenteid`, `message: 4` (create/update). Cambiar `sanic_cm_tbl_agente.sanic_cm_ag_usuarioid` **no dispara nada**.
- `BO/scrAgentes.pa.yaml:572-576` (vincular/cambiar) y `:268-274` (desvincular) solo hacen `Patch` del agente.
- DEV: `AS-2000` — agente "Álvaro Zeledón" (hoy vinculado al usuario `8bccbe…`) pero `owner = cf7b6d…` (el creador): el vínculo llegó después y el owner nunca se movió.
- Para asignaciones que SM re-upserta a diario (Diarias), el trigger sí se vuelve a disparar al día siguiente (SM manda `as_agenteid`, aunque no cambie), así que se "cura" solo en 24 h. Las **Incobrables** (no vienen en el CSV diario) y las Especiales **no se curan nunca**.

**Impacto:** el onboarding real de un cobrador (crear agente en la carga → vincular usuario → entra a la app) deja su cartera vacía hasta la próxima carga; el reemplazo de un usuario deja la cartera con el usuario anterior.

**Corrección propuesta:** segundo flow (o ampliar este) disparado por `ag_usuarioid` en `agente`: recorrer las asignaciones `Activa` del agente y re-ejecutar la lógica de owner (+ `GrantAccess` de las Especiales). Para desvincular: devolver las asignaciones al team "Cartera sin cobrador".

**Estado (2026-10-14): cerrado en DEV.** El plugin asíncrono `RepartoPorAgente` (Update de `ag_usuarioid`) re-escribe el mismo
agente en sus asignaciones activas mal repartidas, lo que dispara `RepartoAsignacion` en cada una: al vincular pasan al usuario,
al desvincular vuelven al equipo "Cartera sin cobrador".

---

### DAT-09 — El teléfono no filtra aprobación: Especial Pendiente/Borrador/Rechazada visible
**Severidad:** Alta · **Confianza:** Verificado

**Evidencia**
- `LOC/scrCartera.pa.yaml:23` — `ClearCollect(colAsigRaw, Filter('CM | TBL | Asignacions', '[AS] Estado' = true))`. `[AS] Estado de aprobacion` aparece **0** veces en toda la app. La cartera "oficial" según docs es `suya + Activa + Aprobada` (componente 12 §6).
- **Vía CSV:** el CSV puede traer `TipoAsignacion = Especial`. SM la crea con `agente` (mapeado) → el flow mueve el owner al cobrador de inmediato, **sin pasar por el Supervisor**. `estadoaprobacion` no se mapea: nace con el default (hoy 2 = Pendiente) pero el teléfono la muestra igual. DEV: 47 Especiales de la carga con `apr = 1 (Borrador)` y `Estado = true`; `AS-2041`/`AS-2045` (QA) en Borrador, activas y owned por los cobradores QA.
- **Vía reactivación:** `BO/scrNuevaSolicitud.pa.yaml:595-607` reactiva una Especial cerrada: pone `Pendiente aprobación`, `'[AS] Agente': Blank()`, **pero no toca el owner**. Como `agente = null`, el flow no corre (`condition: agenteid != null`). El **cobrador anterior sigue siendo owner** de una Especial *Pendiente* con retiro nuevo y la ve en su teléfono antes de que nadie la apruebe. Lo mismo si luego el Supervisor la **rechaza**.

**Impacto:** el control de aprobación (DEC-013/022) se saltea por dos vías; el cobrador opera sobre solicitudes no aprobadas.

**Corrección propuesta:** teléfono: `Filter(Asignacions, Estado = true && (Tipo <> Especial || 'Estado de aprobacion' = Aprobado))`. Backend: al reactivar/rechazar, devolver el owner al solicitante (flow: si `agenteid` pasa a null → `ownerid := solicitadopor`). CSV: decidir explícitamente si una Especial por CSV nace aprobada; si no, el flow de owner debe esperar `estadoaprobacion = Aprobado` para tipo 3.

---

### DAT-10 — Una Diaria reconciliada que reaparece no se reactiva
**Severidad:** Alta · **Confianza:** Probable

**Evidencia**
- Template Smelter `Cobranza movil`, TM 4 `Asignacion`: columnas mapeadas `NumeroCuenta`, `TipoAsignacion`, `FechaCarga→fechasignacion`, `NumeroCuenta→cuenta`, `Agente→agente`, `FechaCarga→ultimacarga`. **Sin `estado`**. El doc (`cobranza-import-config.md`) afirma que "estado ya defaultea true, no necesita mapping" — el default de columna solo aplica al **crear**; en un upsert que *actualiza* una fila existente no se vuelve a aplicar.
- Reconciliación (`BO/scrReconciliacion.pa.yaml:381-392`) cierra con `Estado=false`, `Motivo="fuera de cartera"`. El doc de la tabla (componente 12 §6) promete: "Reaparición: si la cuenta vuelve, el upsert la **re-activa**".
- Efecto: la cuenta vuelve en el CSV, `ultimacarga` se actualiza, pero `as_estado` sigue `false`. El teléfono (`Estado = true`) no la muestra. Y el backoffice tampoco la marca como anomalía (Reconciliación solo mira `Estado = true`).

**Corrección propuesta:** mapear `estado` en Smelter con valor fijo `true` *solo para* Diaria/Incobrable (cambio de configuración de la carga: pedirlo a quien administra Smelter, yo no lo toqué), o flow sobre `as_ultimacarga` que reabra Diarias cerradas con motivo "fuera de cartera". Test mínimo: cerrar una Diaria, volver a cargar su CSV y mirar `as_estado`.

**Estado (2026-10-14): cerrado en DEV.** El plugin `CargaSobreAsignacion` (PreOperation, Update de asignación filtrado por
`as_ultimacarga`) reabre una Diaria o Incobrable que cerró el sistema (motivo "fuera de cartera" o "Reclasificada a …") cuando la
carga la trae de vuelta. No reabre los cierres hechos por una persona ni las Especiales. No se tocó Smelter. Verificado en vivo,
incluido que una asignación "Pagada" no se reabre. Ver [catálogo C.2](../referencia/03-catalogo-de-tablas.md).

---

### DAT-11 — Sin seguridad de columnas ni invariantes en servidor
**Severidad:** Alta · **Confianza:** Verificado

**Evidencia**
- `IsSecured` = false en todas las columnas de `asignacion` e `interaccion`; no existe ningún *Field Security Profile* de la solución. Los docs lo daban por hecho (componente 15 nota 1: "Localizador: escribe solo `as_orden`" y §4 "Backoffice escribe solo los campos de transcripción; comentario y geo inmutables").
- Privilegios vivos: `Localizador` → `asignacion` Write = Basic (**todas** las columnas de las suyas: estado, agente, retiro…); `BackOffice` → `interaccion` Write = Global (**todas**: comentario, geo, aplicapago, agente…).
- Las 3 business rules solo hacen `SetDisplayMode` (lock): según Microsoft Learn, *Lock/Unlock* aplica solo a **model-driven**; "las acciones que solo aplican a model-driven se ignoran cuando la regla corre en servidor". Ver sección 6.
- La transcripción (`BO/scrTranscripcion.pa.yaml:1050-1058`) hace `Patch` directo y no verifica que la fila siga `Pendiente`: dos backoffice pueden grabar la misma interacción (gana el último, el primero pierde su código) — y con la auditoría apagada (DAT-12) no queda rastro. `fechagrabacion = Now()` y `grabadopor = gblUsuarioDV` los fija el cliente (el propio comentario del código dice "quitar estas 2 líneas cuando el flujo de auditoría esté activo").
- Existe una función low-code `sanic_PlugInCMMarcarinteraccioncomograbadaenCORE` (Custom API, *Async only*) que **ninguna app usa**.

**Impacto:** la integridad de la evidencia de campo y el anti-doble-carga dependen de que nadie use otra herramienta (Advanced Find, API, otra app).

**Corrección propuesta:** (1) FLS para `as_*` (todo menos `orden`) y `in_*` de captura; (2) un plug-in/real-time workflow síncrono en `interaccion` que rechace pasar a `Grabado` si ya lo está o sin `codigograbacion`, y estampe `fechagrabacion/grabadopor` con `InitiatingUserId`/`UtcNow`; (3) chequeo de concurrencia en el Patch (re-leer estado antes de escribir). Tradeoff: FLS rompe cualquier flujo que hoy escriba esas columnas con el rol equivocado — probar cada pantalla.

**Estado (2026-10-14): cerrado en DEV.** En lugar de seguridad de columnas, dos plugins del servidor. `GrabacionInteraccion`
(Update de interacción): una interacción Grabada no se vuelve a grabar ni cambia su código (la segunda persona del BackOffice
recibe un error en vez de pisar la grabación), pasar a Grabado exige el código, la fecha y el usuario de grabación los pone el
servidor y el BackOffice solo puede escribir las columnas de grabación. La transcripción del backoffice ya no manda fecha ni
usuario y busca la interacción por id. `ColumnasAsignacion` (Update de asignación, S-11): cada rol escribe solo las columnas de
su pantalla. Verificado en vivo: grabar da 200 con fecha y usuario del servidor, volver a grabar da 400, el BackOffice editando el
comentario da 400, el Supervisor cambiando el retiro da 400 y el cobrador cambiando el orden da 200. **Límite:** dos grabaciones
en el mismo milisegundo podrían pasar las dos, porque el plugin lee el estado previo antes de que la fila quede bloqueada.

---

### DAT-12 — Auditoría de tabla desactivada
**Severidad:** Alta · **Confianza:** Verificado

**Evidencia:** `EntityDefinitions.IsAuditEnabled` = **false** en `asignacion`, `interaccion`, `pago`, `agente`, `actualizaciondatos`, `fiador`, `producto`, `cargadiaria`. Solo `cliente`, `cuenta`, `direccion`, `telefono` = true. Los atributos clave (`as_agenteid`, `as_tipo`, `as_estado`, `in_estadograbacion`, `in_codigograbacion`…) sí tienen `IsAuditEnabled = true`, pero **sin auditoría a nivel tabla no se escribe nada**. Consulta a `audits`: **0 filas** para asignación, interacción, pago y agente. La organización sí tiene auditoría activa (retención 365 días).

Los diseños dependen de ella: DEC-016 ("historial de reasignaciones"), componente 10 ("auditoría del write-back"), el comentario de `BO/scrNuevaSolicitud.pa.yaml` ("el historial queda en la auditoría" al reactivar una Especial), y el subtítulo de Transcritas ("auditoría sellada").

**Corrección propuesta:** activar auditoría a nivel tabla en esas entidades (costo: almacenamiento de auditoría; filtrar columnas ya está hecho a nivel atributo).

---

### DAT-13 — `ApplicationRequired` no se aplica en servidor
**Severidad:** Media · **Confianza:** Verificado

**Evidencia:** columnas `ApplicationRequired` (`as_numerocuenta`, `in_agente`, `in_cuentaid`, `pg_interaccion`, `pg_numerorecibo`, `cd_*`, …) aceptan `null` desde canvas, flows e import. DEV: `AS-2000` (tipo **null**, numerocuenta null), `AS-2001` y `AS-2002` (ambas **Especial** sobre la *misma* cuenta, `numerocuenta = null`) — la alt key `(tipo, numerocuenta)` no las detecta como duplicadas porque la parte de texto es nula. Y `IN-1035/1036` sin los dos lookups requeridos. Además el anti-duplicado de Nueva solicitud busca por `'[AS] NumeroCuenta'` → no las ve.

**Corrección propuesta:** pasar a `SystemRequired`/validación server-side (plug-in o Custom API) lo que de verdad es invariante (`as_numerocuenta` + `as_cuentaid` + `as_tipo`; `in_cuentaid`; `pg_interaccion`); limpiar `AS-2000/2001/2002`. Mientras tanto, completar `as_numerocuenta` desde `as_cuentaid` por flow.

**Estado (2026-10-14): cerrado en DEV.** El plugin `InteraccionDelCobrador` (Create de interacción) exige cuenta y agente; cuando
la crea un cobrador, el agente es el vinculado a su usuario (lo pone el servidor, aunque la app mande otro) y la interacción nace
Pendiente, sin código, fecha ni usuario de grabación (también cierra SEC-08). `CuentaDesdeInteraccion` rechaza un pago sin
interacción. Verificado en vivo: sin cuenta da 400, con otro agente se reemplaza por el del usuario, con Grabado se guarda
Pendiente y un pago sin interacción da 400.

---

### DAT-14 — Solicitante: anti-duplicado ciego y re-solicitud bloqueada
**Severidad:** Media · **Confianza:** Probable

**Evidencia:** el rol Solicitante tiene `asignacion` **Read = Basic** (verificado). `BO/scrNuevaSolicitud.pa.yaml:553-607` decide con dos `LookUp(Asignacions, …)` (Especial activa / Especial cerrada). Un Solicitante B no ve la Especial activa o cerrada creada por A (owner = A o el cobrador; solo hay share con el `solicitadopor` original). Resultado: no aparece el aviso amable, el `LookUp(_cerrada)` da `blank` → intenta `Create` → falla con `412` por la alt key (el error B-11 que se quería evitar).
Además `Rechazar` (`scrAprobaciones.pa.yaml:836-844`) deja `Estado = true`: la Especial rechazada sigue "ACTIVA" y el aviso dice "esperá a que el Admin la cierre" (Cierres sí la lista, `scrCierres.pa.yaml:18-22`), así que el Solicitante queda bloqueado hasta que el Admin intervenga.

**Corrección propuesta:** hacer el anti-duplicado con una Custom API/flow que corra con permisos de servicio y devuelva estado ("existe activa/cerrada/rechazada"); al rechazar, poner `Estado=false` (o permitir reabrir una Rechazada propia).

**Estado (2026-10-14): parcial.** Hecho: Rechazar cierra la Especial (estado inactivo, motivo "Rechazada" y fecha de cierre), así
que el Solicitante original puede volver a pedirla; Nueva solicitud muestra un mensaje claro cuando la cuenta ya tiene una Especial
pedida por otra persona. **Sigue abierto:** un Solicitante no ve la Especial de otro (lectura Basic) y hace falta una decisión del
dueño: una Custom API que corra con el servicio del sistema o una lectura más amplia.

---

### DAT-15 — Grano asignación vs cuenta
**Severidad:** Media · **Confianza:** Probable

**Evidencia**
- `LOC/scrCartera.pa.yaml:38-42` — `totNio = Sum(Asigs, saldoNio)`, `nCuentas = CountRows(Asigs)` sobre asignaciones; cada una trae el saldo de su cuenta. Una cuenta con Diaria **y** Especial activas (permitido por la alt key) cuenta **dos veces** en el total del cliente. Idem `scrCliente.pa.yaml:172`.
- `LOC/scrInteraccion.pa.yaml:25-28` y `:375` — `colProdInt` se arma desde `colAsig`; `sel` se pone por `'[AS] NumeroCuenta'`: con dos asignaciones de la misma cuenta, **ambas** quedan `sel = true` y el `ForAll(Filter(colProdInt, sel))` (`:1102`) crea **dos interacciones** idénticas.
- DEC-021 (reclasificación: al aparecer como Incobrable, cerrar la Diaria) **no está implementada**: no hay mapping ni flow. Una cuenta que cambia de Diaria a Incobrable queda con dos asignaciones activas en el teléfono.

**Corrección propuesta:** agrupar por cuenta en el teléfono (`GroupBy` por `NumeroCuenta`) antes de sumar/crear; implementar DEC-021 como flow sobre `asignacion` (al crear/actualizar Diaria/Incobrable activa, cerrar la otra con `Motivo = "Reclasificada a {tipo}"`, sin tocar Especiales).

**Estado (2026-10-14): cerrado en DEV.** El plugin `Reclasificacion` (PostOperation síncrono, Create y Update de asignación) deja
una sola activa entre Diaria e Incobrable por cuenta: al llegar el otro tipo, cierra la anterior con motivo "Reclasificada a
{tipo}". Las Especiales conviven (DEC-021). En la app del cobrador, el saldo total de la ficha del cliente suma por cuenta
distinta, así que una cuenta con Diaria más Especial aprobada ya no cuenta su saldo dos veces. Verificado en vivo en las dos
direcciones.

---

### DAT-16 — `Orden`: sin inicializar, BR inactiva, swap con empates
**Severidad:** Media · **Confianza:** Verificado

**Evidencia**
- SM no mapea `as_orden` (correcto, no se pisa) pero tampoco lo inicializa. DEV: `orden = null` en 163 Diarias, 33 Incobrables y 10 Especiales; `orden = 0` en 22.
- La BR `Bussiness Rule | CM | AS | Orden inicial` (`WF/…OrdeninicialD6F8…xaml`): si `orden` es 0 o null → `orden = 1`. Estado live: **Draft (`statecode 0`)**. No ejecuta. Si se activara: pone *todo* en 1 → empate masivo.
- El reordenamiento (`LOC/scrCartera.pa.yaml:832-853`) intercambia valores entre vecinos: `obj = Max(Filter(colClientes, orden < ThisItem.orden), orden)`. Con `orden` nulo o empatado, `obj` es blank → el botón no hace nada.
- `OnHidden` (`scrCartera.pa.yaml:14-19`) hace `UpdateIf(Asignacions, '[AS] NumeroCuenta' = A.NumeroCuenta, {Orden})`: pisa `Orden` en **todas** las asignaciones con ese número (incluyendo Cerradas y otros tipos), no solo la de la cartera.

**Corrección propuesta:** inicializar `orden` secuencial por cobrador (flow tras el owner o en la carga), único; que la BR (o mejor, el flow) lo haga sin empates; el teléfono debe actualizar por `[AS] Numero`/GUID y solo `Estado = true`.

**Estado (2026-10-14): cerrado.** La app numera de 1 a n sin empates (APP-23) y la regla de negocio en borrador "Orden inicial" se
eliminó, así que ya no puede activarse y poner todo en 1.

---

### DAT-17 — `Email Usuario` vs `[AG] Usuario`
**Severidad:** Media · **Confianza:** Probable

**Evidencia:** el login del teléfono usa el **texto** (`LOC/App.pa.yaml:91`), el filtro de agentes asignables, el KPI "agentes sin usuario" y las listas de `scrAgentes` usan el **texto** (`scrAprobaciones.pa.yaml:637`, `scrInicio.pa.yaml:92`, `scrAgentes.pa.yaml:74,101,119…`), pero el **flow de owner usa el lookup** `[AG] Usuario`. Hoy se escriben juntos (`scrAgentes.pa.yaml:572-576`), y en DEV los 4 agentes vinculados están consistentes (`Email Usuario` = correo principal = UPN, minúsculas). Se desacoplan si: cambia el correo principal / UPN del usuario, alguien edita el agente desde otra vía (MDA, flow, API), o el correo `User().Email` del dispositivo difiere del `internalemailaddress` (invitados B2B: p. ej. el usuario "Playwright Dev User" tiene correo `@gmail.com`).

**Corrección propuesta:** una sola fuente. Que `Email Usuario` sea una columna calculada/mantenida por flow desde `[AG] Usuario` (o eliminar el texto y resolver `gblAgente` por la relación `systemuser`). Mientras tanto, KPI/filtros por `[AG] Usuario <> Blank()`.

**Estado (2026-10-14): cerrado en DEV.** Una sola fuente: el backoffice escribe solo el lookup `[AG] Usuario` y el plugin
`EmailDesdeUsuario` completa `Email Usuario` con el UPN en minúsculas. Además el backoffice identifica al usuario por el Entra
object id (`'Azure AD Object ID' = User().EntraObjectId`) y no por correo o UPN.

---

### DAT-18 — Cascadas de relación
**Severidad:** Media · **Confianza:** Verificado (metadata) / Hipótesis (impacto)

**Evidencia** (`OneToManyRelationships.CascadeConfiguration`, DEV):

| Padre → Hijo | Delete | Assign | Share | Reparent |
|--------------|:------:|:------:|:-----:|:--------:|
| Cliente → Cuenta / Teléfono / Dirección / CargaDiaria | Cascade | Cascade | Cascade | Cascade |
| **Cuenta → Asignación** | Cascade | Cascade | Cascade | Cascade |
| Cuenta → Interacción / CargaDiaria | RemoveLink | NoCascade | NoCascade | NoCascade |
| **Interacción → Pago / ActualizaciónDatos** | Cascade | Cascade | Cascade | Cascade |
| Agente → Asignación / Interacción / Cuenta / CargaDiaria | Restrict | NoCascade | NoCascade | NoCascade |
| Producto/Fiador → Cuenta | Restrict | NoCascade | NoCascade | NoCascade |

**Riesgos:**
- Reasignar (`Assign`) un **Cliente** o una **Cuenta** (p. ej. "reasignar registros de un usuario deshabilitado") baja en cascada a *todas* las asignaciones y **pisa el owner que puso el flow** (aislamiento por cobrador roto). Hoy el owner de cliente/cuenta es `SYSTEM` (1.958 de 1.969) y nadie los reasigna — riesgo latente.
- Compartir una cuenta se propaga a todas sus asignaciones (de todos los cobradores y tipos).
- Borrar una Interacción borra sus **Pagos** (dato financiero). El componente 11 recomendaba relación *referencial* "para no perder pagos"; el build la dejó *Parental*. Borrar un Cliente borra cuentas, asignaciones y **su histórico de carga diaria**. Solo `Admin` tiene Delete.

**Corrección propuesta:** pasar `Interacción→Pago` y `Cliente→CargaDiaria` a *Referential, Restrict Delete*; en `Cuenta→Asignación` poner `Assign/Share/Reparent = NoCascade` (la asignación tiene su propio owner por diseño). Tradeoff: tocar relaciones en una solución con datos requiere ventana de cambio.

**Estado (2026-10-14): cerrado en DEV.** `paso1_cascadas_auditoria.py` pasó `Interacción→Pago`, `Interacción→ActualizaciónDatos` y
`Cliente→CargaDiaria` a Delete = Restrict, y `Cuenta→Asignación` a Assign = NoCascade (Share y Reparent ya estaban cortados desde
el 2026-10-03 y el 2026-10-06). Ya no se puede borrar una interacción con pagos ni un cliente con carga diaria, y reasignar la
cuenta no baja a sus asignaciones.

---

### DAT-19 — Actualización de datos: el ciclo no cierra
**Severidad:** Media · **Confianza:** Verificado

**Evidencia** (flujo completo cobrador → backoffice → core):
1. LOC (`scrInteraccion.pa.yaml:1092-1100`) crea `AD` con 3 teléfonos, 3 direcciones y correo + lookup a la interacción; marca `in_actualizainfo = true`.
2. BO (`scrTranscripcion.pa.yaml:51-70`) busca el AD por `'[AD] Interaccion'`, trae los datos "antes" de `telefono`/`direccion` por **CIF** (obtenido de `in_cuentaid`, DAT-04) y arma la tabla de cambios; el oficinista los **tipea a mano en el core**.
3. Cierra marcando la interacción `Grabado`. **No hay estado propio en AD** ni acción de "aplicada a maestros".
- Los maestros (`telefono`/`direccion`) los puede escribir solo `Admin` (Maestros) y esa pantalla **solo agrega** (alt key `cif+tipo` rechaza duplicados) — no hay "editar teléfono" ni "aplicar AD". El rol BackOffice tiene Write = `—` sobre tel/dir.
- Roles vs docs: componente 13 §5 dice que Backoffice **y Solicitante** leen AD; en vivo `Solicitante` y `Supervisor` no tienen Read (`Rea:-`).
- Offline: el perfil **manual** no incluye `actualizaciondatos` (el autogenerado sí) — si se corrige DAT-06 sin agregarla, las altas de AD fallarían offline.
- Campos capture-only: correo y teléfono de trabajo no tienen destino; direcciones AD (1000) > `dr_direccion` (200).
- Se asume que el core es verdad y el CSV de mañana trae el cambio; mientras tanto, el teléfono muestra el dato viejo (que el cobrador acaba de corregir).

**Corrección propuesta:** columna de estado/resolución en AD (`Pendiente/Aplicada/Descartada`) llenada en la transcripción; decidir si se aplica a maestros (acción Admin "aplicar") o si el core es el único destino; alinear longitudes; alinear lectura de roles con el doc o corregir el doc.

**Estado (2026-10-14): cerrado por decisión del dueño.** La actualización de datos termina en el core del banco, así que no hacen
falta más estados ni una acción de aplicar a los maestros: la opción "el core es el único destino" de la corrección propuesta.

---

### DAT-20 — No hay forma de crear una Cuenta desde el backoffice
**Severidad:** Media · **Confianza:** Verificado

**Evidencia:** no existe ningún `Patch` sobre `Cuentas` en BO. Maestros (`scrMaestros.pa.yaml:1427-1470`, solo Admin) crea `Cliente` + `Teléfono` + `Dirección`; Nueva solicitud exige elegir una cuenta existente ("Si el cliente no existe, pedile al Admin que lo cree"). El cliente nuevo queda sin cuenta → no se le puede pedir una Especial. DEC-013/014 prometían que el Solicitante crearía "datos maestros si faltan"; en vivo el Solicitante tiene Create = `—` sobre cliente/cuenta/dirección/teléfono.

**Corrección propuesta:** decidir el flujo: (a) formulario de cuenta mínimo (Admin) en Maestros, o (b) aceptar que toda cuenta entra por CSV y retirar la promesa del doc. Si se crea cuenta a mano, rellenar `ct_cifcliente/ct_nombrecliente/ct_tipoproducto` (los que leen las apps) y producto (ApplicationRequired).

---

### DAT-21 — Teléfono sin `MaxLength`
**Severidad:** Media · **Confianza:** Probable

**Evidencia:** ningún input de `scrInteraccion` define `MaxLength` (recibo → `pg_numerorecibo` 30; teléfonos AD → 20; correo → 100; direcciones AD → 1000). Offline, el `Patch` local siempre "funciona" ("✓ Guardado offline") y el rechazo llega **después**, en el sync, como error de sincronización: la interacción/pago queda trabado y el cobrador ya se fue de la pantalla. (BO sí limita: `txtCodigoTrx.MaxLength = 10` = `codigograbacion` len 10.)

**Corrección propuesta:** `MaxLength` por control igual a la columna; formato de teléfono; validación previa al Registrar.

---

### DAT-22 — Recibo vacío y sin unicidad
**Severidad:** Media · **Confianza:** Verificado

**Evidencia:** `LOC/scrInteraccion.pa.yaml` — el botón se habilita con `CountRows(Filter(colLineasPago, monto>0 && !IsBlank(Trim(recibo)))) > 0` (alcanza **una** línea válida), pero el `ForAll(Filter(colLineasPago, monto > 0) …)` de `:1084` persiste **todas** las líneas con monto, también las de recibo vacío (`pg_numerorecibo` es solo `ApplicationRequired`). `lblHint` sí marca ese estado como "Falta", pero no deshabilita. No existe alt key ni validación de duplicados de recibo (el comp. 11 §7.2 lo deja como pendiente; un talonario por cobrador haría falta `agente + recibo`).

**Corrección propuesta:** `DisplayMode` = ninguna línea con `monto>0` y recibo vacío; opcional alt key `(owner/agente, recibo)` o un flow de aviso de duplicados.

---

### DAT-23 — Refresco incompleto en el teléfono
**Severidad:** Media · **Confianza:** Probable

**Evidencia:** las colecciones maestras (`colCuentas`, `colClientes`, `colDirecciones`, `colTelefonos`, `colAgentes`) se cargan **solo** en `App.OnStart` (`LOC/App.pa.yaml:84-91`). El "↻" de Cartera (`scrCartera.pa.yaml:143`) y "Buscar actualizaciones" (`scrSincronizacion.pa.yaml:294`, solo `Refresh` de Interacciones/Pagos/AD) no las recargan; `varRecargarCartera` no se pone en `true` en ningún lado. Una asignación nueva (Especial aprobada o Diaria de la mañana) cuyo `cta = LookUp(colCuentas, …)` no estaba en la foto del arranque queda con `cta`, `cid` y `cliNom` vacíos hasta que el cobrador cierre y reabra la app.

**Corrección propuesta:** en el refresco, recargar las colecciones maestras (o resolverlas con `LookUp` directo al data source en vez de a colecciones).

**Estado (2026-10-14): cerrado en DEV.** El "↻" recarga los maestros (cuentas, clientes, direcciones, teléfonos y agentes).

---

### DAT-24 — Asignaciones cerradas nunca salen del dispositivo
**Severidad:** Media · **Confianza:** Probable

**Evidencia:** el perfil autogenerado trae **todas** las asignaciones legibles (Basic = las propias) sin filtro por `as_estado`, y cerrar una asignación solo cambia `as_estado` (`statecode` sigue Activo, el owner sigue siendo el cobrador). La app descarta las cerradas con `Filter(Estado = true)` en memoria, pero siguen sincronizándose y creciendo: la reconciliación cierra Diarias a diario, así que cada cobrador acumula históricos para siempre (más las `Cuentas` en cascada de DAT-06).

**Corrección propuesta:** perfil con filtro `sanic_cm_as_estado eq true` en `asignacion` (y `Related rows only` para el resto), o desactivar (`statecode = 1`) al cerrar, o archivar.

**Estado (2026-10-14): cerrado en DEV.** El perfil offline filtra solo las asignaciones activas (`as_estado = 1`), así que las
cerradas salen del teléfono en la próxima sincronización.

---

### DAT-25 — Flow de owner: conexión personal, sin Share en el rol de servicio y una corrida por fila
**Severidad:** Media · **Confianza:** Verificado

**Evidencia:** el flow y su conexión (`wf_sharedcommondataserviceforapps_3bc81`) son del usuario humano "Randy Kauffman" (roles: Admin, System Administrator…). Si esa cuenta se deshabilita o cambia, deja de moverse el owner (y no hay alerta, DAT-07). El rol de servicio previsto `SR | CM | Servicio - Cobranza` tiene `asignacion`: Assign = Global pero **Share = —**, y el flow hace `GrantAccess` (requiere Share): con una conexión de servicio y ese rol, la rama de Especiales fallaría. SM manda `as_agenteid` en cada upsert, por lo que el trigger (filtrado por esa columna, "create or update") se dispara **una vez por fila de la carga, todos los días**, ejecute o no un cambio (≈1.000/día en DEV; decenas de miles en producción: consumo de requests de Power Platform).

**Corrección propuesta:** conexión con service principal/cuenta de servicio con Share en asignación; en el trigger, expresión de condición para que solo corra si `ownerid` ≠ `_ag_usuarioid` del agente (o `ownerid` es SYSTEM/team "sin cobrador"); o reemplazarlo por un plug-in síncrono (el comp. 14 ya lo dejaba como plan B).

---

### DAT-26 — Nombre/CIF duplicados y `fechapago` mixto
**Severidad:** Media · **Confianza:** Verificado

**Evidencia:** las dos apps muestran el nombre del cliente desde `cuenta.ct_nombrecliente` (`'Nombre Cliente'`: LOC 9 usos, BO 16), pero Maestros edita `cliente.cl_nombre` (`BO/scrMaestros.pa.yaml:808-814`) — los cambios no se ven en ningún lado, y la próxima carga los pisa (SM vuelve a escribir `cl_nombre` y `ct_nombrecliente` con el valor del CSV). Lo mismo para `ct_cifcliente`/`cl_cif` (hoy sin divergencias, 0 de 1.007). `ct_fechapago` es texto(100) con `20/07/2026` y `2026-07-17` mezclados (DEV), el doc dice DateTime. `ct_cifcliente` (250) vs `cl_cif` (20).

**Corrección propuesta:** quitar la edición de nombre en Maestros (o dejarla solo-lectura) o llevarla a ambas tablas; normalizar `fechapago` a Date en el mapping (`DATEPARSE`) o documentar que es texto.

---

### DAT-27 — "Hoy" por `Created On`
**Severidad:** Media · **Confianza:** Verificado

**Evidencia:** `LOC/scrInicio.pa.yaml:15-16` filtra interacciones y pagos por `'Created On' >= Today()` (hora de **sync**, no de captura; los docs explican por qué no usar `createdon`: `fechacaptura` y `pg_fecha` existen justamente para esto). `LOC/scrSincronizacion.pa.yaml:17` usa `'Created On' = Today()` (igualdad exacta con medianoche → casi nunca verdadera). El "cobrado hoy" del cobrador y el del backoffice quedan en bases distintas (y el del backoffice, en cero: DAT-02).

**Corrección propuesta:** usar `Fecha de captura` / `Fecha del pago` (con el relleno de DAT-02) en ambos lados.

---

### DAT-28 — `estadoaprobacion` default = Pendiente
**Severidad:** Baja · **Confianza:** Verificado

`DefaultFormValue = 2` (Pendiente aprobación) en `sanic_cm_as_estadoaprobacion`; `as_tipo` default = 3 (Especial). SM no mapea `estadoaprobacion`, así que las Diarias/Incobrables nuevas de la próxima carga nacerán "pendientes de aprobación" (las 801 Diarias actuales están en `1 = Borrador`; 3 en `2`). No rompe hoy porque las vistas/KPIs filtran por tipo = Especial, pero cualquier consulta sin ese filtro sobrecuenta. **Propuesta:** dejar `null`/sin default y setear Pendiente solo al crear una Especial.

**Estado (2026-10-14): cerrado en DEV.** El plugin `AprobacionEspecial` hace que una Diaria o Incobrable nazca en Borrador. El default
de la columna queda en Pendiente, porque las Especiales del CSV lo necesitan para entrar en la cola del Supervisor. Se corrigieron
las 6 filas existentes.

### DAT-29 — Reasignación manual sin UI
**Severidad:** Baja · **Confianza:** Verificado

DEC-012/022 dicen que el Supervisor reasigna Especiales y el Admin las Incobrables; en canvas **no existe** esa pantalla (el único `Patch` de `as_agenteid` es el de Aprobar y el de reactivación con `Blank()`). En el formulario MDA la BR `Bloqueo de asignacion aprobada o rechazada` bloquea `as_agenteid` cuando `estadoaprobacion ∈ {3,4}` (solo en formulario). Resultado: la única forma real de reasignar es editar por fuera. **Propuesta:** definir el camino (pantalla "Reasignar") y que dispare el flow de owner.

**Estado (2026-10-14): cerrado en DEV.** Pantalla *Reasignar* del backoffice (`scrReasignar`): el Supervisor reasigna las Especiales
aprobadas activas y el Admin también las Incobrables activas. Solo escribe el agente; el plugin de reparto mueve el dueño y los
compartidos. Las Diarias no se reasignan a mano: gana el CSV.

### DAT-30 — Huérfanos
**Severidad:** Baja · **Confianza:** Verificado

`sanic_cm_tbl_usuarioapp`: 0 usos y 0 privilegios (borrada el 2026-10-15, DAT-30). `producto`/`fiador`: solo import; las apps usan los textos denormalizados. `ct_agenteid`, `ct_tipoasignacion`, `ct_productoid`, `ct_fiador`: escritas por SM, nunca leídas. `cargadiaria`: snapshot solo de histórico (más 1.000 filas por día; Monitoreo lee solo `FechaCarga`). `[AS] Resolutor`: escrita, nunca leída. La dirección **EC** se importa pero el teléfono solo carga Casa/Trabajo (`LOC/App.pa.yaml:86`) — pero el cobrador puede capturar un cambio de EC sin ver el actual. `[CT] Total saldo vencido`/`Contagiado USD`: leídas, ok.

### DAT-31 — Autonumber como clave de negocio / `fechasignacion`
**Severidad:** Baja · **Confianza:** Verificado

`IN-/AS-/PG-/AD-{SEQNUM:4}` (seed 1000; en DEV 0 duplicados). Se usan como clave de `LookUp` en `Patch` (`'[IN] Numero de Interaccion' = …`, `'[AS] Numero' = …`) sin alt key. El ancho es mínimo, no máximo (al llegar a 10000 crece a 5 dígitos; si algo ordena como texto, se desordena). SM sobrescribe `as_fechasignacion` con `FechaCarga` en cada upsert, pisando la fecha de la solicitud de una Especial que viniera en el CSV. **Propuesta:** usar el GUID para parchear; alt key sobre los autonumber si se mantiene el patrón.

**Estado (2026-10-14): cerrado en DEV en lo de `fechasignacion`.** El plugin `CargaSobreAsignacion` ya no pisa `as_fechasignacion`
si tenía valor, así que queda la fecha de alta (y la de solicitud de una Especial del CSV) y `ultimacarga` sigue diciendo la última.

### DAT-32 — Drift documentación vs build
**Severidad:** Baja · **Confianza:** Verificado
Ver sección 9.

---

## 5. Ciclo de vida de la asignación y puntos de ruptura

```
CARGA DIARIA (Smelter, 1x/día, SYSTEM)                                   [R1: default tipo=3, estadoaprobacion=2]
  CSV ──► Cliente, Producto, Agente(nombre), Fiador ──► Dirección/Teléfono ──► Cuenta ──► Asignación + CargaDiaria
                                                     (cif+tipo)          (denormaliza)  (cuenta+tipo; no mapea estado/orden/aprobación)
                                                                                 │
                              Asignación Diaria/Incobrable: owner = SYSTEM       │  ◄── [R2: DAT-10 una Diaria cerrada que reaparece queda Cerrada]
                                                                                 ▼
FLOW "Cambio de Owner" (trigger: as_agenteid, create/update, Org)                 [R3: DAT-25 1 corrida por fila por día; conexión personal]
   ¿agente.usuario?  ── NO ──► (no hace nada) ──────────────────────────────► [R4: DAT-07/08 queda en SYSTEM / en el cobrador anterior]
        │ SÍ
        ▼
   owner := usuario del agente ──► si Especial + solicitadopor → GrantAccess(R/W/Append) al solicitante
                                                                                 │
ESPECIAL (backoffice)                                                            │
  Solicitante: Nueva solicitud ─ Patch Asignación(tipo=3, retiro/notas EN LA ASIGNACIÓN, Pendiente, SIN agente, owner=Solicitante)
        │         ▲ re-solicitud: reactiva la CERRADA (mismo registro, owner = cobrador anterior)  [R5: DAT-09/14]
        ▼
  Supervisor: Aprobaciones ─ Aprobar: estadoaprobacion=Aprobado + agente (solo agentes con Email Usuario)
        │                  ─ Rechazar: Rechazada + motivo (Estado sigue Activa)           [R6: DAT-14 bloquea re-solicitud]
        └─► dispara el FLOW ──► owner = cobrador ──► share al solicitante
                                                                                 │
TELÉFONO (offline, perfil autogenerado "All records")                            ▼
  Sync baja: asignaciones propias (todas, activas y cerradas)  + TODAS las cuentas/clientes/dir/tel      [R7: DAT-06/24]
  OnStart: carga colecciones UNA vez        ◄──── [R8: DAT-23 refresh no recarga maestros]
  Cartera: Filter(Estado=true) (SIN aprobación)                                    [R9: DAT-09]
  Detalle de cuenta: Especial → retiro/nota/tipo DE LA CUENTA                      [R10: DAT-01]
  Registrar interacción ─ Patch Interacción(+Pagos+AD)  · lookups agente/cuenta pueden quedar vacíos   [R11: DAT-04]
                                          · pg_fecha NO se escribe                 [R12: DAT-02]
                                          · pg.numerointeraccion vacío offline     [R13: DAT-03]
                                          · lat/long a 2 decimales                 [R14: DAT-05]
  Reordenar cartera ─ UpdateIf(Asignaciones por NumeroCuenta, Orden) ──► sin dispararse el flow (OK)    [R15: DAT-16]
                                                                                 │
SYNC ──► servidor asigna IN-/PG-/AD- (autonumber)                                ▼
BACKOFFICE
  Transcripción: Pendiente ─ navega '[IN] Cuenta' (cliente, CIF, producto; "antes" de AD) ◄─ depende de R11
                 pagos por TEXTO numerointeraccion ◄─ depende de R13
                 Patch Grabado + código + fecha/usuario (cliente) ── sin chequeo de concurrencia    [R16: DAT-11]
  Transcritas / Inicio: arqueo y "Cobrado hoy" por pg_fecha ◄─ vacío por R12
  AD: se transcribe a mano al core; sin estado, sin aplicación a maestros             [R17: DAT-19]
                                                                                 │
CIERRE / RECONCILIACIÓN (Admin)
  Reconciliación: Diarias Activas con ultimacarga < corte ─ Estado=false ("fuera de cartera")          [null ultimacarga nunca entra: DAT-13/16]
  Cierres: Incobrable/Especial a mano (Estado=false + motivo)  ·  Especial: owner y share NO se retiran
  Teléfono: desaparece en el próximo Filter/sync; el registro sigue en el dispositivo (R7, DAT-24)
```

**Qué pasa en el teléfono**
- **Cierre:** la fila sigue asignada al cobrador (mismo owner) y dentro del perfil; el sync actualiza `Estado=false` y la app deja de mostrarla *cuando recarga* `colAsigRaw` (solo al abrir la sesión o con "↻"). No sale del almacenamiento local (DAT-24).
- **Reasignación a otro agente con usuario:** el flow cambia el owner (async, segundos); en el próximo sync el cobrador viejo **pierde el acceso** (no hay shares a cobradores; el único share es al solicitante) y la fila se retira del dispositivo; cambios locales pendientes (`Orden`) pueden quedar en conflicto. *Probable, no probado en dispositivo.*
- **Reasignación a un agente sin usuario:** el cobrador viejo **conserva** la asignación (DAT-07).
- **Cuentas/clientes/direcciones/teléfonos:** el cobrador viejo los conserva igual porque el perfil trae *todo* (DAT-06).

---

## 6. Business rules y flow: qué hacen de verdad

| Regla | Estado live | Scope | Qué hace | ¿Aplica a canvas/servidor? |
|-------|:-----------:|:-----:|----------|----------------------------|
| `BR | CM | AS | Bloqueo de asignacion aprobada o echazada` | Activa | **Form** (`processtriggerscope = 1`, formulario `813336ce…`) | Si `estadoaprobacion ∈ {Aprobado, Rechazada}` → bloquea `as_agenteid`, `as_estadoaprobacion`, `as_motivorechazo` | **No.** Solo el formulario MDA; el canvas y la API ignoran el bloqueo (y el canvas reabre/reactiva). |
| `BR | CM | AS | Orden inicial` | **Borrador** (`statecode 0`) | Entity | Si `as_orden` es 0 o null → `as_orden = 1` (Set Field Value) | **No ejecuta** (inactiva). Si se activara, correría en servidor (esa acción sí aplica) y empataría todo en 1 (DAT-16). |
| `BR | CM | Bloqueo tras grabacion en CORE` | Activa | Entity | Si `in_estadograbacion = Grabado` → bloquea `in_codigograbacion` e `in_estadograbacion` (Lock) | **No.** Lock/Unlock es solo model-driven; en servidor se ignora (Microsoft Learn: "Actions that only apply to model-driven apps are ignored when the rule is run server-side"). El anti-doble-carga no existe en servidor (DAT-11). |
| `Cloud Flow | CM | Cambio de Owner en Asignaciones` | Activo | Org | Ver DAT-07/08/25 | Sí (cualquier cliente que escriba `as_agenteid`) |

Notas: (a) `workflows` de DEV acumula ~17 filas *type 2* en borrador de `Bloqueo tras grabacion en CORE` (versiones de edición de hoy); no afectan, pero ensucian la solución. (b) La BR `Orden inicial` fue modificada hoy (`2026-10-03T22:57Z`) y sigue en borrador: confirmar que es intencional.
Choice values: todas las comparaciones de las apps (`Diaria/Incobrable/Especial = 1/2/3`, `Pendiente aprobacion/Aprobado/Rechazada = 2/3/4`, `Pendiente/Grabado = 1/2`, `NIO/USD = 1/2`) coinciden con la metadata; las BR usan `3` y `4` correctamente.

---

## 7. Respuestas puntuales al pedido

1. **Numeración `INT-`/`PAG-`:** no son `INT-`/`PAG-`; los autonumber reales son `IN-{SEQNUM:4}`, `PG-{SEQNUM:4}`, `AS-{SEQNUM:4}`, `AD-{SEQNUM:4}` (seed 1000), asignados por **Dataverse al insertar** (en offline, al sincronizar). Los docs dicen `INT-{SEQNUM:6}`/`PAG-{SEQNUM:6}`/`ASG-{SEQNUM:6}`/`ACT-{SEQNUM:6}`. Sin duplicados en DEV; huecos por borrado de pruebas. Son el nexo entre pago e interacción en texto (DAT-03).
2. **¿Borrar una cuenta borra interacciones/pagos?** Borrar **cuenta**: borra *asignaciones* (Cascade), deja *interacciones* y *carga diaria* con el lookup en `null` (RemoveLink; el texto `in_numerocuenta` sobrevive). Borrar **interacción**: borra sus *pagos* y *AD* (Cascade). Borrar **cliente**: borra cuentas, direcciones, teléfonos y carga diaria. Borrar **agente**/producto/fiador: bloqueado (Restrict) si tienen hijos.
3. **¿Reasignar owner en cascada?** Sí, por `Assign = Cascade` en Cliente→Cuenta y Cuenta→Asignación (DAT-18). Interacción→Pago/AD también.
4. **Alt keys y reactivaciones:** `(tipo, numerocuenta)` en asignación impide una segunda Especial para la misma cuenta; por eso Nueva solicitud reactiva la cerrada (y falla con 412 si no la ve, DAT-14). La key de texto no cubre el `null` (DAT-13). La Diaria reaparecida no se reactiva (DAT-10). `(cif, tipo)` en teléfono/dirección rechaza el alta duplicada de Maestros. `cd_numerocuenta + cd_fechacarga` hace idempotente la recarga del día.
5. **Monedas:** `NIO/USD` en choice; `Decimal(2)` por moneda (sin Money); BO filtra por moneda para sumar, así que un pago con moneda en blanco (`default -1`) no entra en ningún total (el teléfono siempre manda NIO o USD).

---

## 8. Lo que está bien (verificado OK)

- Alt keys: todas **Active** (`cliente_cif`, `producto_codigo`, `agente_nombre`, `fiador_nombre`, `cuenta_numero`, `direccion_cif_tipo`, `telefono_cif_tipo`, `asignacion_tipo+numerocuenta`, `cargadiaria_numerocuenta+fechacarga`).
- Consistencia de datos en DEV: `ct_cifcliente = cl_cif` (0 divergencias), `ct_nombrecliente = cl_nombre`, `tipoproducto`, `nombrefiador`, `dr_cif/tl_cif = cl_cif`, `as_numerocuenta = ct_numerodecuenta` (0 divergencias).
- Owner del flow correcto para los agentes vinculados (1 desvío, DAT-08).
- Permisos Append/AppendTo del Localizador sobre cuenta/agente: correctos (la creación de la interacción no falla por seguridad).
- `codigograbacion` len 10 = `MaxLength 10` del input; `numerocuenta` len 20 consistente en cuenta/asignación/interacción/carga.
- Autonumber sin duplicados; `Pendiente/Grabado` y `NIO/USD` coherentes app↔metadata.
- Reglas de reconciliación con corte por fecha y manejo de `DateOnly`/`UserLocal` (comentado y consistente en Inicio/Reconciliación/Monitoreo).

---

## 9. Drift documentación vs build

| Doc | Dice | Build real |
|-----|------|------------|
| Comp. 10 | `INT-{SEQNUM:6}`; lat/long 5 decimales | `IN-{SEQNUM:4}`; **precisión 2** |
| Comp. 11 / divergencias | `pg_fecha` obligatoria, la setea la app | Opcional; **nadie la escribe** |
| Comp. 11 | Interacción→Pago "referencial" | **Parental/Cascade** |
| Comp. 12 | `ASG-{SEQNUM:6}`; `estado` Activa=1/Cerrada=2 choice | `AS-{SEQNUM:4}`; `estado` Booleano (true=Activa) |
| Comp. 12 §6 | "Reaparición: el upsert la re-activa" | SM no mapea `estado` |
| Comp. 13 / 15 | Solicitante lee AD; Backoffice aplica AD a maestros | Solicitante sin Read; Backoffice sin Write en tel/dir |
| Comp. 15 nota 1 | FLS: Localizador solo `orden`; Backoffice solo campos de transcripción | Sin FLS |
| Comp. 01/12 / DEC-016 | Auditoría de reasignaciones | Auditoría de tabla desactivada |
| Comp. 14 | Flow alerta al Admin si falta usuario | Rama vacía |
| Guías 13/14 | Perfil offline acotado ("la solución real") | Perfil autogenerado "All records" |
| DEC-013/014 | Solicitante crea/edita maestros | Create/Write = `—` en cliente/cuenta/dir/tel |
| `cobranza-import-config.md` §2 | "estado ya defaultea true, no necesita mapping" | Cierto solo para create |
| Vistas MDA de asignación | "Aprobaciones pendientes" muestra `as_motivo` y "Mis solicitudes" filtra por `createdby` | Canvas usa `Notas` y `Solicitado por` |

---

## 10. Orden de ataque sugerido y verificaciones pendientes

**Orden sugerido (de más barato/seguro a más invasivo):**
1. DAT-02 + DAT-27 (una línea en el teléfono + backfill y flow de relleno) — destraba el arqueo.
2. DAT-01 (leer de la asignación) y DAT-09 (filtro de aprobación) — cambio de app, sin tocar esquema.
3. DAT-03 y DAT-04 (join por lookup en BO + backfill server-side de lookups) — protege la transcripción aunque el teléfono falle.
4. DAT-05 (precisión de geo) y DAT-12 (auditoría) — cambios de metadata chicos.
5. DAT-06/24 (perfil offline acotado + filtro por estado + AD) — exige ventana, re-sync y prueba en dispositivo.
6. DAT-07/08/25 (flow robusto, segundo flow por agente, conexión de servicio) y DAT-10 (mapeo de `estado` en Smelter, a cargo de quien administre Smelter).
7. DAT-11/13/18 (FLS, validaciones server-side, cascadas) — el cambio más delicado; probar pantalla por pantalla.

**Verificaciones pendientes (no pude hacerlas desde lectura):**
- Reproducir DAT-04 con Monitor en un dispositivo/emulador real: ¿`gblAgente` vacío? ¿el `Patch` de `[IN] Cuenta` con `varCuenta` (registro anidado) llega al servidor? Probar variante con `LookUp(datasource, …)`.
- Reproducir DAT-03: crear una interacción + pago en modo avión, sincronizar y mirar `pg_numerointeraccion`.
- Reproducir DAT-10: cerrar una Diaria, volver a subir su fila en el CSV y leer `as_estado`.
- Confirmar el retiro de acceso al reasignar (DAT sección 5) con dos dispositivos.
- Contar corridas diarias del flow en el historial de Power Automate (DAT-25).

---

## Apéndice — Consultas usadas (solo GET)

- Metadata: `EntityDefinitions?$select=…` + `/Attributes`, `/Attributes/Microsoft.Dynamics.CRM.{String,Decimal,Double,Money,Lookup,DateTime,Picklist,Boolean}AttributeMetadata`, `/Keys`, `/OneToManyRelationships`, `/ManyToOneRelationships`; `IsAuditEnabled`/`IsSecured`; `RetrieveRolePrivilegesRole(RoleId=…)`.
- Seguridad: `roles`, `roleprivileges_association`, `fieldsecurityprofiles`, `systemusers`, `organizations` (auditoría).
- Procesos: `workflows` (BR y flow: `statecode`, `processtriggerscope`, `ownerid`), `connectionreferences`, `customapis`, `customapirequestparameters`.
- Offline: `mobileofflineprofiles`, `mobileofflineprofileitems`, `Properties.json` del msapp (`ManualOfflineProfileId`).
- Smelter (solo lectura): `sanic_tbl_smlt_importtemplates`, `…_tablemappings`, `…_columnmappings`.
- Datos: `sanic_cm_tbl_{asignacion,cuenta,cliente,interaccion,pago,actualizaciondatos,agente,cargadiaria,direccion,telefono,producto,fiador}` (conteos y comparaciones de columnas; sin volcar datos de clientes), `audits` (filtrado por `objecttypecode`).
