# Catálogo de Tablas — Definición, Lifecycle y Borrado

> Referencia **única y consolidada** de cada tabla de la solución `cm_core_cobranzamovil`: definición, campos y tipos,
> llaves, relaciones y cascadas, import, ciclo de vida, cómo borrar y consideraciones al borrar.
>
> **Última actualización: 2026-10-14** (plugins, privilegios, cascadas de DAT-18 y retención de datos; lo demás, 2026-10-06). Los campos, llaves, cascadas, pasos de plugin, privilegios, perfil offline y la
> plantilla de Smelter se **verificaron en vivo contra DEV** con consultas de solo lectura (`EntityDefinitions`,
> `sdkmessageprocessingsteps`, `RetrieveRolePrivilegesRole`, `mobileofflineprofileitems`, `sanic_tbl_smlt_*`).
> **Cuando la metadata y un documento discrepan, manda la metadata**, y la discrepancia queda anotada (§D).
>
> Fuentes cruzadas: `docs/auditoria/ciclo-de-vida-de-los-datos.md` (2026-10-06), `contrato-de-datos.md`,
> `fase2-pan-diseno.md`, `00-lista-consolidada.md`, `docs/guias/16-guia-por-rol.md`, `scripts/pan/*`,
> `scripts/seguridad/*`, `plugins/`. El detalle histórico de diseño sigue en `componentes/` y `decisiones/`.

**Cómo leer las columnas "Req."** `Sí` = `ApplicationRequired` en la metadata: lo exigen las apps y formularios, **el
servidor no lo hace cumplir**, salvo la cuenta y el agente de la interacción y la interacción del pago (DAT-13: plugins de C.2); `—` = opcional. Los montos son `Decimal`, no `Money`. Todas las tablas son
**de propiedad de usuario** (`UserOwned`). No se listan las columnas auxiliares `*name` / `*yominame` de los lookups.

## Convenciones de borrado (modelo, DEC-023, y estado real verificado)

**Intención de diseño (DEC-023):**

- **`Cascade`** en la cadena de **datos/contacto** (`cliente → direccion/telefono/cuenta`): borrar el padre borra sus datos.
- **`Restrict`** en el **límite de la historia operativa** (`cuenta → interaccion` y aguas abajo): si hay cobranza
  registrada, **bloquea** el borrado y detiene la cascada.
- Efecto buscado: *"se borra el cliente y toda su data, salvo que haya interacciones; ahí se bloquea todo"*.

**Estado real en DEV (2026-10-06, `OneToManyRelationships.CascadeConfiguration`; con el cambio de DAT-18 del 2026-10-14):**

- `Cascade` de borrado: `cliente → direccion / telefono / cuenta`, `cuenta → asignacion / cuentapan`.
- `Restrict` de borrado: los padres de catálogo → hijo: `agente → cuenta / asignacion / interaccion / cargadiaria`,
  `producto → cuenta`, `fiador → cuenta`; y, desde el 2026-10-14 (DAT-18), `cliente → cargadiaria`, `interaccion → pago` e
  `interaccion → actualizaciondatos`.
- `RemoveLink` (borra el padre y deja el hijo con el lookup vacío): `cuenta → interaccion`, `cuenta → pago`,
  `cuenta → cargadiaria`, y `systemuser → agente / asignacion (resolutor, solicitado por) / interaccion (grabado por)` (y `usuarioapp`, borrada el 2026-10-15).
- **El candado `cuenta → interaccion = Restrict` NO existe.** El catálogo anterior lo daba por "confirmado" y es falso:
  la metadata dice `RemoveLink`. Borrar una cuenta (o un cliente, que cascadea a sus cuentas) **no se bloquea** por tener
  interacciones: estas y sus pagos sobreviven **sin cuenta**. El comportamiento en ejecución **no se probó** (DEV es de
  solo lectura para esta revisión): es lectura de metadata.
- **Borrado de rutina: no existe** (no hay política ni job). Fin de vida normal = **cierre lógico**: asignación
  `Cerrada`, agente `Activo = No`, interacción `Grabado`; el histórico (carga diaria, pagos) se acumula.
- **Quién puede borrar:** solo el rol `SR | CM | Admin` (`Delete: Global`) y solo por la API; ninguna app borra
  (`ciclo-de-vida-de-los-datos.md`, "idea central"; no se recorrieron las apps para re-verificarlo). Los scripts de QA y de migración borran con ese rol. Smelter **nunca** borra
  (todas sus tablas son upsert: `createifnotexists` y `updateifexists` en `true`, sin mapeos de borrado).
- **Retención de `cargadiaria` (DEC-019), 2026-10-14:** un trabajo de Bulk Deletion **diario** borra las filas de más de **60 días**
  (decisión del dueño). Otro trabajo diario borra los fiadores sin nombre (ver `fiador`). Los crea
  `scripts/seguridad/paso10_borrado_programado.py`.

---

## A. Panorama de las 14 tablas

| # | Tabla | Nombre mostrado | Nombre principal | Auditoría de tabla | Perfil offline | Filas en DEV | Origen de las filas |
|---|-------|-----------------|------------------|:--:|:--:|-----:|---------------------|
| 1 | `sanic_cm_tbl_cliente` | CM \| TBL \| Cliente | `cl_nombre` | Sí | Sí | 963 | Smelter; Admin (Maestros) |
| 2 | `sanic_cm_tbl_direccion` | CM \| TBL \| Direccion | `dr_direccion` | Sí | Sí | 2701 | Smelter; Admin agrega |
| 3 | `sanic_cm_tbl_telefono` | CM \| TBL \| Telefono | `tl_numero` | Sí | Sí | 1063 | Smelter; Admin agrega |
| 4 | `sanic_cm_tbl_producto` | CM \| TBL \| Producto | `pr_codigo` | **No** | No | 7 | Smelter |
| 5 | `sanic_cm_tbl_agente` | CM \| TBL \| Agente | `ag_nombre` | Sí | Sí | 20 | Smelter (nombre); Admin (usuario, activo) |
| 6 | `sanic_cm_tbl_fiador` | CM \| TBL \| Fiador | `fi_nombre` | **No** | No | 2785 | Smelter |
| 7 | `sanic_cm_tbl_cuenta` | CM \| TBL \| Cuenta | `ct_numerodecuenta` (enmascarado) | Sí | Sí | 1010 | Solo Smelter |
| 8 | `sanic_cm_tbl_cargadiaria` | CM \| TBL \| Carga diaria | `cd_numerocuenta` (enmascarado) | Sí | No | 1000 | Solo Smelter |
| 9 | `sanic_cm_tbl_interaccion` | CM \| TBL \| Interaccion | `in_numerointeraccion` | Sí | Sí | 10 | App del cobrador |
| 10 | `sanic_cm_tbl_pago` | CM \| TBL \| Pago | `pg_numero` | Sí | Sí | 5 | App del cobrador |
| 11 | `sanic_cm_tbl_asignacion` | CM \| TBL \| Asignacion | `as_numero` | Sí | Sí | 1011 | Smelter; Solicitante; Supervisor/Admin cierran y reasignan |
| 12 | `sanic_cm_tbl_actualizaciondatos` | CM \| TBL \| Actualizacion de dato | `ad_numero` | Sí | Sí | 2 | App del cobrador |
| 13 | `sanic_cm_tbl_cuentapan` | CM \| TBL \| PAN de cuenta (**nueva**, bóveda) | `cp_name` (enmascarado) | Sí | Sí | 1010 | Solo Smelter |
| 14 | ~~`sanic_cm_tbl_usuarioapp`~~ | ~~CM \| TBL \| Usuario App~~ | ~~`ua_nombre`~~ | — | — | — | **Borrada el 2026-10-15 (DAT-30)**: era huérfana |

Notas:
- Filas de DEV al 2026-10-06 (`$count`, solo orden de magnitud: son datos ficticios y de QA). `fiador` tiene más filas que `cuenta`
  (2785 contra 1010): origen explicado el 2026-10-14, ver `fiador`.
- "Perfil offline" = lista de las 10 tablas del perfil `Cobranza Movil - Cobrador` (§C.5). Todas las tablas tienen `ChangeTracking`
  activo salvo `cargadiaria` (y `usuarioapp`, borrada el 2026-10-15).
- La numeración 1 a 12 se conserva respecto del catálogo anterior; `cuentapan` y `usuarioapp` se agregan al final como 13 y 14 (la 14 se borró el 2026-10-15, DAT-30).

## B. Relaciones y cascadas (verificado 2026-10-06; DAT-18 aplicado y verificado el 2026-10-14)

Cada fila es una relación 1:N real (`OneToManyRelationships` de las tablas CM). `NC` = `NoCascade`.

| Padre → Hijo | Lookup en el hijo | Delete | Assign | Share | Unshare | Reparent |
|--------------|-------------------|--------|--------|-------|---------|----------|
| cliente → direccion | `dr_clienteid` | Cascade | Cascade | Cascade | Cascade | Cascade |
| cliente → telefono | `tl_clienteid` | Cascade | Cascade | Cascade | Cascade | Cascade |
| cliente → cuenta | `ct_clienteid` | Cascade | Cascade | NC | NC | **NC** |
| cliente → cargadiaria | `cd_clienteid` | **Restrict** | Cascade | NC | NC | **NC** |
| cuenta → cuentapan | `cp_cuentaid` | Cascade | Cascade | Cascade | Cascade | Cascade |
| cuenta → asignacion | `as_cuentaid` | Cascade | **NC** | NC | NC | **NC** |
| cuenta → interaccion | `in_cuentaid` | **RemoveLink** | NC | NC | NC | NC |
| cuenta → pago | `pg_cuentaid` | **RemoveLink** | NC | NC | NC | NC |
| cuenta → cargadiaria | `cd_cuenta` | **RemoveLink** | NC | NC | NC | NC |
| interaccion → pago | `pg_interaccion` | **Restrict** | Cascade | Cascade | Cascade | Cascade |
| interaccion → actualizaciondatos | `ad_interaccionid` | **Restrict** | Cascade | Cascade | Cascade | Cascade |
| agente → cuenta / asignacion / interaccion / cargadiaria | `ct_agenteid`, `as_agenteid`, `in_agente`, `cd_agenteid` | **Restrict** | NC | NC | NC | NC |
| producto → cuenta | `ct_productoid` | **Restrict** | NC | NC | NC | NC |
| fiador → cuenta | `ct_fiador` | **Restrict** | NC | NC | NC | NC |
| systemuser → agente / asignacion (2) / interaccion / ~~usuarioapp~~ (borrada 2026-10-15) | `ag_usuarioid`, `as_resolutorid`, `as_solicitadopor`, `in_grabadopor`, ~~`ua_usuarioid`~~ | RemoveLink | NC | NC | NC | NC |

**Cómo se lee:**
- **Delete.** `Cascade` borra los hijos con el padre; `Restrict` impide borrar el padre mientras haya hijos; `RemoveLink` borra el
  padre y deja el lookup del hijo vacío (el hijo **no** se borra).
- **Share = Cascade** (`cliente → direccion/telefono`, `cuenta → cuentapan`, `interaccion → pago/AD`): compartir el padre comparte
  los hijos. Es el mecanismo por el que el cobrador, al recibir del reparto (`RepartoAsignacion`, C.3) la cuenta y el cliente, ve también la bóveda, las
  direcciones y los teléfonos. En `cliente → cuenta`, `cliente → cargadiaria` y `cuenta → asignacion` **no** se propaga (desde
  la Fase 1, 2026-10-03), para que compartir un cliente no comparta todas sus cuentas.
- **Reparent: cambio del 2026-10-06 (defecto 2 / S-14).** Estaba en `Cascade` en `cliente → cuenta`, `cliente → cargadiaria` y
  `cuenta → asignacion`. Cada vez que la carga de Smelter reescribía la cuenta, Dataverse daba **lectura heredada** de todas las
  cuentas de los clientes del cobrador, **con su bóveda (número completo)** y sus asignaciones, aunque no fueran suyas. Se pasó a
  `NoCascade` con `scripts/seguridad/paso1_cascadas_auditoria.py`. Sigue en `Cascade`, **a propósito**, en `cuenta → cuentapan`,
  `cliente → direccion/telefono` e `interaccion → pago/actualizacion`: ahí quien accede al padre es la misma audiencia que el
  diseño quiere. **Los accesos heredados que ya existían en DEV no se limpiaron** (datos ficticios); en PROD el cambio tiene
  que estar aplicado antes de la primera carga real.
- **DAT-18 (2026-10-14):** `interaccion → pago`, `interaccion → actualizaciondatos` y `cliente → cargadiaria` pasaron a `Delete =
  Restrict` (no se puede borrar una interacción con pagos ni un cliente con carga diaria), y `cuenta → asignacion` a `Assign =
  NoCascade`: reasignar la cuenta ya no pisa el dueño que puso `RepartoAsignacion`. Lo aplica
  `scripts/seguridad/paso1_cascadas_auditoria.py`.
- **Assign = Cascade** sigue vigente en `cliente → cuenta / cargadiaria` y `cuenta → cuentapan`: reasignar un cliente o una
  cuenta (por ejemplo, "reasignar los registros de un usuario deshabilitado") baja en cascada a esos hijos. Riesgo latente: hoy el
  dueño de cliente, cuenta y carga diaria es `SYSTEM` y nadie los reasigna.

## C. Automatización del servidor, auditoría, perfil offline y privilegios

### C.1 Cómo carga Smelter (resumen; el detalle por tabla está en cada sección)

- **Plantilla `Cobranza movil`** (`sanic_tbl_smlt_importtemplate`): CSV delimitado por `,`, con encabezado, lotes de 50 filas. Smelter
  corre como **SYSTEM** (sin roles de CM). **13 Table Mappings** y **0 Relationship Mappings**: los lookups se resuelven con
  *column mappings* de tipo lookup (`lookuptable` + `lookupmatchcolumn`), no con relationship mappings (el catálogo anterior
  hablaba de "Relationship Mapping": ya no aplica).
- **Orden de proceso:**

| Orden | Mapeos | Dedup (clave) |
|:--:|--------|---------------|
| 1 | Cliente · Producto · Agente · Fiador | `cl_cif` · `pr_codigo` · `ag_nombre` · `fi_nombre` |
| 2 | Direccion (casa / EC / Trabajo) · Telefono (Celular / Numero casa) | `dr_cif + dr_tipo` · `tl_cif + tl_tipo` |
| 3 | Cuenta | `ct_clavepan` |
| 4 | Cuenta PAN (bóveda) | `cp_clavepan` |
| 5 | Asignacion · Carga Diaria | `as_clavepan + as_tipo` · `cd_clavepan + cd_fechacarga` |

- **Todos** hacen `createifnotexists` + `updateifexists`: son **upsert**, nunca borran ni cierran nada.
- **El número de tarjeta (PAN).** La plantilla calcula en la propia fila:
  `clavepan = HMAC(NumeroCuenta, <clave K>)` (HMAC-SHA256 en hex, 64 caracteres; la clave K la lee Smelter de su tabla de claves
  `sanic_tbl_smlt_hmackey`, que **no se leyó** en esta revisión), `ultimos4 = RIGHT(TRIM(NumeroCuenta), 4)` y el enmascarado
  `CONCAT("****", RIGHT(TRIM(NumeroCuenta), 4))`. El número completo (`TRIM(NumeroCuenta)`) se escribe **solo** en
  `cuentapan.cp_pan`. Los lookups a cuenta (bóveda, asignación, carga diaria) se resuelven calculando el mismo HMAC contra
  `ct_clavepan`.
- **Columnas del CSV** que usa la plantilla: `Cliente, CIF, Departamento, Distrito, Trabajo, CodigoProducto, TipoProducto, Agente,
  NombreFianza, DireccionFianza, DireccionCasa/EC/Trabajo, NumeroCelular, NumeroCasa, NumeroCuenta, TotalSaldoVencido,
  SaldoVencidoNIO/USD, ContagiadoUSD, PagoMinimoNIO/USD, RetiroNIO/USD, DiasMora, NivelMora, FechaPago, FechaUltimoPago, Ciclo,
  TipoAsignacion, TipoEspecial, Nota, FechaCarga`.
- **Qué Smelter NO escribe** (queda en el default de la columna o en manos de las apps): `as_estado` (default Activa),
  `as_estadoaprobacion` (default Pendiente aprobación), `as_orden`, `as_motivo`, `as_fechacierre`, `as_notas`, `as_retironio/usd`,
  `as_solicitadopor`, `as_resolutorid`, `ag_usuarioid`, `ag_activo`, `ag_emailusuario`, `ct_tipoproducto`.
- Después de cada carga, los **archivos CSV fuente** del job conservan el número completo: `scripts/pan/paso10_purgar_archivos.py`
  los borra a mano en DEV; en PROD tiene que ser automático (pendiente).

### C.2 Plugins (ensamblado `Sanic.CobranzaMovil.Plugins`)

**24 pasos, todos activos en DEV** (verificado `statecode = 0`, 2026-10-14, ensamblado 1.0.6.1). 19 son **PreOperation (20)**,
**síncronos (0)** y ejecutan como el usuario que llama; los 2 de `Reclasificacion` son **PostOperation (40) síncronos**; los 2 de
`RepartoAsignacion` y el de `RepartoPorAgente` son **PostOperation (40)**, **asíncronos (1)** y usan el servicio del sistema (ver
C.3). Los que dependen del rol (`AprobacionEspecial`, `ColumnasAsignacion`, `GrabacionInteraccion`, `InteraccionDelCobrador`) leen los
roles de CM directos y por equipo; quien no tiene roles de CM (SYSTEM / Smelter, administradores) y Admin/Servicio no se frenan.
Ninguno conoce la clave K ni escribe valores en trazas o mensajes de error.

| Plugin | Tabla y mensaje | Filtro de Update | Qué hace |
|--------|-----------------|------------------|----------|
| `PanGuard` (6 pasos) | `cuenta`, `cargadiaria`, `cuentapan`: Create y Update | `ct_numerodecuenta`, `cd_numerocuenta`, `cp_name` | Rechaza un valor con **5 o más dígitos seguidos** (tolera espacio, guion o espacio duro entre dígitos). `****1234`, vacío y nulo pasan. Red de seguridad si una plantilla vuelve a escribir el número en claro |
| `ClaveDesdeCuenta` (2) | `asignacion`: Create y Update | `as_cuentaid`, `as_clavepan` (+ `PreImage` con `as_cuentaid`) | Si hay cuenta, completa `as_clavepan` con la `ct_clavepan` de la cuenta; si viene otra distinta, **rechaza**. Así las apps no escriben la clave |
| `CuentaDesdeInteraccion` (1) | `pago`: Create | — | DAT-13: rechaza un pago **sin interacción**. Completa `pg_cuentaid` desde `in_cuentaid` de la interacción y `pg_numerointeraccion` desde `in_numerointeraccion` (el modo offline solo admite un nivel de lookup y no conoce el autonumérico) |
| `AprobacionEspecial` (2) | `asignacion`: Create y Update | `as_agenteid`, `as_estadoaprobacion`, `as_tipo` (+ `PreImage`) | S-6. Sobre una **Especial**, solo Supervisor, Admin o `Servicio - Cobranza` pueden: pasarla a Aprobado o Rechazada, poner o cambiar el agente, o convertir en Especial una asignación que ya tiene agente. Frena a las **personas con algún rol `SR \| CM \|`** que no sean esos; no frena a SYSTEM (Smelter) ni a administradores sin roles de CM. DAT-28: en el Create, una Diaria o Incobrable nace en **Borrador** (no "Pendiente aprobacion") |
| `ActualizacionConInteraccion` (2) | `actualizaciondatos`: Create y Update | `ad_interaccionid` | APP-24. Exige una interacción **con cuenta** |
| `EmailDesdeUsuario` (2) | `agente`: Create y Update | `ag_usuarioid` | `ag_emailusuario` = UPN del usuario vinculado en minúsculas; vacío si se desvincula |
| `RepartoAsignacion` (2) | `asignacion`: Create y Update, **asíncrono** | `as_agenteid`, `as_estado`, `as_estadoaprobacion` | Reparto de la asignación al cobrador (o a la cartera sin cobrador): dueño, compartidos y revocación al anterior (C.3) |
| `RepartoPorAgente` (1) | `agente`: Update, **asíncrono** | `ag_usuarioid` | DAT-08. Al vincular, cambiar o quitar el usuario, vuelve a escribir el mismo agente en sus asignaciones activas mal repartidas: eso dispara `RepartoAsignacion` en cada una (C.3) |
| `ColumnasAsignacion` (1) | `asignacion`: Update | — (todas) | S-11. Cada rol escribe solo las columnas `as_*` de su pantalla: Localizador `as_orden`; Supervisor aprobación, agente, resolutor, motivo de rechazo, estado, motivo y fecha de cierre; Solicitante las de volver a pedir una Especial. Otro rol de CM: ninguna. El mensaje cita la columna, nunca el valor |
| `CargaSobreAsignacion` (1) | `asignacion`: Update | `as_ultimacarga` (+ `PreImage`) | DAT-10: la carga **reabre** una Diaria o Incobrable cerrada por el sistema ("fuera de cartera" o "Reclasificada a …"); no reabre cierres de personas ni Especiales. DAT-31: no pisa `as_fechasignacion` si ya tenía valor |
| `Reclasificacion` (2) | `asignacion`: Create y Update, **PostOperation síncrono** | `as_ultimacarga`, `as_estado`, `as_tipo` (+ `PreImage`) | DAT-15 (DEC-021): una sola activa entre Diaria e Incobrable por cuenta; al llegar el otro tipo cierra la anterior con motivo "Reclasificada a {tipo}". Las Especiales conviven |
| `InteraccionDelCobrador` (1) | `interaccion`: Create | — | DAT-13 + SEC-08: exige cuenta y agente; si la crea un cobrador, el agente es el **vinculado a su usuario** (lo pone el servidor) y nace Pendiente de grabar |
| `GrabacionInteraccion` (1) | `interaccion`: Update | — (todas) (+ `PreImage`) | DAT-11: una Grabada no se vuelve a grabar ni cambia su código; Grabado exige código; fecha y usuario de grabación los pone el servidor; el BackOffice escribe solo las columnas de grabación |

Los pasos `PanGuard` de las columnas viejas (`as_`, `in_`, `pg_numerocuenta`) se retiraron cuando esas columnas se eliminaron
(Fase 2, paso 8). Un paso mal registrado **falla cerrado**. Registro: `scripts/pan/paso5_plugins.py`; detalle en el README del ensamblado.

### C.3 Reparto de asignaciones: plugin `RepartoAsignacion` (reemplaza al flow de owner)

Desde el 2026-10-13 el reparto lo hace el plugin **asíncrono** `RepartoAsignacion` (desde el 2026-10-14, versión 1.0.5.0 del
ensamblado, con la cartera sin cobrador, `RepartoPorAgente` y S-7). El flow
`Cloud Flow | CM | Cambio de Owner en Asignaciones` quedó **apagado** (borrador) y `scripts/seguridad/paso2_flow.py` se niega a
encenderlo salvo como rollback. El flow fallaba en ~40% de las corridas: (a) intentaba revocarle la cuenta a SYSTEM siendo SYSTEM
su dueño; (b) varias asignaciones del mismo cliente compartían el cliente a la vez ("More than one concurrent Share requests
detected") y, como las acciones iban encadenadas, **se salteaban el fiador y la revocación al cobrador anterior**.

El plugin lee el estado **actual** de la asignación (converge, no repite el evento) y corre con el servicio del sistema:

- **Asignación activa, y no es Especial sin aprobar, y el agente tiene usuario:** primero el dueño de la asignación pasa al
  usuario del agente; después `GrantAccess` (**solo lectura**) de **cuenta, cliente y fiador** al cobrador; por último
  `RevokeAccess` al dueño anterior (usuario o equipo) de lo que no justifique otra asignación activa suya. La bóveda, las
  direcciones y los teléfonos llegan por **Share en cascada**.
- **Especial aprobada:** además se le comparte (lectura, escritura y anexar) al **Solicitante**, pero **solo si
  `as_solicitadopor` es quien la creó** (`createdby`). "Solicitado por" lo editan Solicitante, Supervisor y Localizador;
  `createdby` no se puede editar. Si no coinciden, no se le comparte a nadie (S-7). Desde el backoffice siempre coinciden: la
  pantalla de nueva solicitud pone "Solicitado por" = quien la crea.
- **Asignación activa sin agente, o con un agente sin usuario (DAT-07):** el dueño pasa al equipo **"CM · Cartera sin
  cobrador · DEV"** y se le revoca al dueño anterior lo que no justifique otra asignación suya. No se comparte nada: el equipo
  **no tiene miembros**, así que nadie la ve en el teléfono. Su único rol, `SR | CM | Cartera sin cobrador`, solo da Read Basic
  de la asignación (Dataverse no deja pasarle un registro a un dueño sin lectura sobre la tabla), más los 4 de SharePoint que la
  plataforma le pone a todo rol. Equipo y rol los crea `scripts/seguridad/paso5_cartera_sin_cobrador.py`, que falla si el equipo
  tiene miembros. Si el equipo no existe, el plugin falla sin tocar nada (trabajo del sistema fallido, sin reintento).
- **Cambia el usuario de un agente (DAT-08):** el plugin `RepartoPorAgente` (asíncrono, Update de `ag_usuarioid`) busca las
  asignaciones activas del agente cuyo dueño no es el que corresponde (el usuario nuevo o, si se lo quitó, el equipo), sin las
  Especiales por aprobar, y les vuelve a escribir el **mismo** agente: eso dispara `RepartoAsignacion` en cada una. Hasta 1000
  por ejecución; si quedan más o alguna falla, pide reintento.
- **Asignación cerrada:** `RevokeAccess` al dueño actual si no tiene otra asignación activa que lo justifique; desaparece del
  teléfono en la próxima sincronización.
- **Especial Borrador, Pendiente o Rechazada:** no hace nada (S-6).
- **Fallas:** cada paso corre aunque falle otro; el compartido concurrente se reintenta con espera dentro del plugin; si algo queda
  sin hacer, la ejecución termina en error, con reintento del servicio asíncrono si es transitorio. Los trabajos fallidos quedan en
  `asyncoperations` y se leen con el Service Principal; los exitosos se borran solos. `paso3_reconciliar.py` sigue como red de
  seguridad (solo agrega compartidos).
- **Nunca síncrono:** correría dentro del trozo de 100 filas de Smelter, que tiene 2 minutos por mensaje; si se pasa, se deshace
  el trozo entero. El plugin falla cerrado si se lo registra síncrono.
- S-7 queda **cerrado**: no hay conexión personal (servicio del sistema) y el compartido al Solicitante exige que sea quien
  creó la Especial.
- Las asignaciones de agentes sin usuario que ya eran de SYSTEM pasan al equipo la próxima vez que algo las toque (la carga
  diaria re-escribe el agente de cada fila).

### C.4 Auditoría

- **Auditoría de tabla activa** en 11 de 14 tablas; **apagada** en `producto`, `fiador` y `usuarioapp` (borrada el 2026-10-15; hoy son 11 de 13 tablas). Se encendió en
  `asignacion`, `interaccion`, `pago`, `agente`, `actualizaciondatos` y `cargadiaria` el 2026-10-06 (S-8,
  `paso1_cascadas_auditoria.py`); el historial de reasignaciones (DEC-016) vive acá.
- **Columna `cp_pan`: auditoría apagada y fuera de búsqueda avanzada** (`IsAuditEnabled = false`, `IsValidForAdvancedFind = false`).
  La auditoría de columna de `ct_numerodecuenta` y `cd_numerocuenta` se apagó durante la migración (para que el enmascarado no dejara
  el número como valor viejo) y **hoy está otra vez encendida**, porque ya guardan `****1234`. `cuentapan` tiene además auditoría de
  lectura (`IsRetrieveAuditEnabled` e `IsRetrieveMultipleAuditEnabled` en `true`).
- El historial de auditoría **anterior** a la Fase 2 contenía el número en claro; en DEV no se purgó (decisión D4, datos ficticios).
  En PROD el número nunca debe entrar en columnas auditadas: pasos 2 y 8 antes de la primera carga real.

### C.5 Perfil offline `Cobranza Movil - Cobrador`

Perfil **propio** (reemplaza al autogenerado), publicado, creado por `scripts/pan/paso7_perfil_offline.py`. Verificado en vivo: 10 tablas.

| Criterio | Tablas |
|----------|--------|
| Todas las filas que el usuario puede leer (la seguridad por fila recorta) | `cliente`, `agente`, `cuenta`, `cuentapan`, `direccion`, `telefono` |
| Filtro propio: **solo activas** (`as_estado = 1`) | `asignacion` |
| Filtro propio: **modificado en los últimos 90 días** | `interaccion`, `pago`, `actualizaciondatos` |

Fuera del perfil: `producto`, `fiador`, `cargadiaria` (y `usuarioapp`, borrada el 2026-10-15). Con "todas las filas que el usuario puede leer", al teléfono
baja solo lo que es suyo o se le compartió.

### C.6 Privilegios por rol (verificado 2026-10-06, Localizador actualizado 2026-10-14; ver `docs/guias/16-guia-por-rol.md` §3)

C = crear, R = leer, W = escribir, D = borrar, A = vincular (Append), T = ser vinculado (Append To), G = asignar dueño, S = compartir.
Alcance: **U** = propio o compartido, **G** = toda la organización. Sin entrada = sin privilegios.

| Tabla | Localizador | Solicitante | Supervisor | BackOffice | Admin | Servicio - Cobranza |
|-------|-------------|-------------|------------|------------|-------|---------------------|
| cliente | R:U · T:G | R/A/T:G | R:G | R:G | todo:G | C/R/W/A/T:G |
| direccion, telefono | R:U | R/A/T:G | R:G | R:G | todo:G | C/R/W/A/T:G |
| producto | R/T:G | R/T:G | R/T:G | R:G | todo:G | C/R/W/A/T:G |
| fiador | R:U · T:G | R/T:G | R/T:G | R:G | todo:G | C/R/W/A/T:G |
| agente | R/T:G | R/T:G | R/T:G | R:G | todo:G | C/R/W/A/T:G |
| cuenta | R:U · T:G | R/A/T:G | R:G | R:G | todo:G | C/R/W/A/T:G |
| cuentapan (bóveda) | R:U | — | — | **R:G** | **—** | — |
| asignacion | R/W/A/T:U (W solo `as_orden`: S-11) | **C:G**, R/W:U, A/T:G (W solo columnas de re-solicitud) | R/W/A/T:G (W solo aprobación, agente y cierre) | R:G | todo:G | C/R/W/A/T/G:G |
| interaccion | C/R/A/T:U (sin W: S-9) | — | R:G | R/W/A:G (W solo grabación: DAT-11) | todo:G | — |
| pago | C/R/A/T:U (sin W ni G: S-9) | — | R:G | R:G | todo:G | — |
| actualizaciondatos | C/R/A/T:U (sin W: S-9) | — | — | R:G | todo:G | — |
| cargadiaria | — | — | — | R:G | todo:G | C/R/W/A/T:G |
| ~~usuarioapp~~ (borrada 2026-10-15) | — | — | — | — | — | — |

(`todo:G` = C, R, W, D, A, T, G y S en alcance Global.) **Solo Admin tiene Delete**, en las 12 tablas de datos de CM (no en `cuentapan` ni
`usuarioapp`, que no tenía ningún privilegio y se borró el 2026-10-15). `Solicitante` y `Supervisor` leen todas las cuentas y clientes a propósito (necesitan buscar
cualquiera: S-2, decisión de diseño).

Las restricciones "W solo …" las aplican los plugins `ColumnasAsignacion` y `GrabacionInteraccion` (C.2): el privilegio de la
tabla no distingue columnas. **Trazas de plugins (S-13, 2026-10-14):** solo Admin y Servicio leen `plugintracelog`
(`scripts/seguridad/paso8_sin_trazas.py`).

**Flows (S-4, 2026-10-14):** Localizador, Solicitante, Supervisor y BackOffice ya no tienen los privilegios de crear, editar,
borrar, asignar, compartir, vincular ni activar procesos, ni `prvFlow` (`scripts/seguridad/paso7_sin_flows.py`); conservan leer y
ejecutar procesos. **En DEV no alcanza:** los usuarios también tienen el rol de plataforma `Basic User` y roles de otras
soluciones del entorno que les dan crear flows y exportar a Excel. Ver S-4 en `docs/auditoria/00-lista-consolidada.md`.

---

## 1. `sanic_cm_tbl_cliente` — Cliente (deudor)

### Definición
El **cliente / deudor**: la persona a la que se le cobra. Es la **raíz** del subárbol de cliente — de ella cuelgan cuentas,
direcciones, teléfonos y la carga diaria. Su identidad de negocio es el **CIF**.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_cl_nombre` | Text(200) | Sí | **Nombre principal.** Del CSV (`Cliente`). |
| CIF | `sanic_cm_cl_cif` | Text(20) | Sí | Identificador único interno del cliente. **Alt key.** |
| Departamento | `sanic_cm_cl_departamento` | Text(100) | — | Departamento geográfico del cliente. |
| Distrito | `sanic_cm_cl_distrito` | Text(100) | — | Distrito donde trabaja el cliente. |
| Trabajo | `sanic_cm_cl_trabajo` | Text(200) | — | Nombre del trabajo del cliente. |
| Propietario | `ownerid` | Owner | Sí | `SYSTEM` para lo que crea Smelter (muestra de 500 filas, 2026-10-06). |

> **Sin correo:** el cliente **no** tiene campo de email. El correo que el cliente quiera actualizar vive **solo** en
> `actualizaciondatos` (capture-only).

### Llaves
- **Alternate Key `sanic_cm_pk_cliente_cif`** (`sanic_cm_cl_cif`). Estado **Active**. El import deduplica y upsertea por CIF.

### Relaciones
- Hijos: `direccion`, `telefono` (Delete/Assign/Share/Unshare/Reparent = Cascade); `cuenta` (Delete y Assign = Cascade;
  Share, Unshare y **Reparent = NoCascade**); `cargadiaria` (**Delete = Restrict** desde el 2026-10-14, Assign = Cascade; Share,
  Unshare y Reparent = NoCascade). Ver §B.

### Import
- **Origen:** CSV diario. Table Mapping `Cliente`, **orden 1** (es padre, va primero). Dedup `sanic_cm_cl_cif`.
- **Columnas:** `Cliente → cl_nombre` · `CIF → cl_cif` · `Departamento → cl_departamento` · `Distrito → cl_distrito` ·
  `Trabajo → cl_trabajo`. Sin lookups ni transformaciones.

### Ciclo de vida
- **Nace:** del **CSV** (upsert por CIF) **o** en **Maestros** de la app backoffice, por el **Admin** (con anti-duplicado de CIF).
  El **Solicitante no crea clientes**: tiene `Create` vacío sobre `cliente` (la promesa de DEC-013/014 no se cumplió; DAT-20).
- **Vive:** cada re-import **pisa** los campos mapeados. Lo que el Admin edita en Maestros (nombre y datos) **lo pisa la carga
  siguiente**, y las apps leen el nombre desde `cuenta.ct_nombrecliente`, no de acá (DAT-26). Es rastro de auditoría.
- **Lo ve el cobrador** solo si el reparto (`RepartoAsignacion`) se lo compartió (junto con la cuenta asignada) y baja al teléfono por el perfil offline.
- **Fin de vida (normal):** **soft** — un cliente sin cuentas queda dormido y se conserva. No se borra de rutina.

### Cómo borrar
- **Relaciones salientes en `Cascade` (Delete):** `cliente → direccion`, `cliente → telefono`, `cliente → cuenta`. Y por la
  cuenta, a su vez: `asignacion` y `cuentapan` (bóveda). `cliente → cargadiaria` es **`Restrict`** desde el 2026-10-14 (DAT-18).
- Borrar un cliente **arrastra**: direcciones, teléfonos, cuentas, **sus asignaciones y su bóveda**. **No se puede** mientras tenga
  filas de carga diaria (llegan por `cd_clienteid`). Solo el **Admin** puede, por la API.

### Consideraciones al borrar
- **La cascada NO se bloquea por interacciones** (corrección respecto del catálogo anterior): `cuenta → interaccion` es `RemoveLink`.
  Si el cliente tiene cuentas con cobranza registrada, el borrado **procede** y las interacciones y pagos quedan **sin cuenta**
  (y sin camino a su cliente: la interacción no apunta al cliente). Comportamiento en ejecución **no confirmado**.
- **`cliente → cargadiaria` es `Restrict`** (DAT-18, 2026-10-14): el histórico financiero del cliente ya no se borra arrastrado
  por el cliente; lo bloquea.
- **Colisión de CIF:** un cliente creado a mano con un CIF que luego llega por CSV se **fusiona** (bien si el CIF es correcto;
  **duplica** si está mal).
- **Caso real de borrado:** un pedido **legal de eliminación de datos** ("derecho al olvido"). Hoy no hay nada que obligue a
  resolver primero la cobranza registrada: hay que hacerlo a mano (o poner en `Restrict` la relación `cuenta → interaccion`). Con carga diaria, el borrado del cliente se bloquea (DAT-18).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: campos y longitudes, alt key activa, cascadas, mapeo de Smelter, privilegios. Corrección: el
bloqueo por interacciones no existe. No confirmado: comportamiento real del borrado en cascada (DEV de solo lectura).

---

## 2. `sanic_cm_tbl_direccion` — Dirección del cliente

### Definición
Las **direcciones** del cliente, **normalizadas por tipo** (`Casa` / `EC` / `Trabajo`). Cada fila del CSV trae 3 direcciones → el
import crea **una fila por tipo**. `EC` = dirección donde recibe el estado de cuenta.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Dirección | `sanic_cm_dr_direccion` | Text(200) | Sí | **Nombre principal.** `DireccionCasa` / `DireccionEC` / `DireccionTrabajo`. |
| Cliente | `sanic_cm_dr_clienteid` | Lookup → `cliente` | Sí | Resuelto por `CIF`. |
| CIF | `sanic_cm_dr_cif` | Text(20) | Sí | **Copiado** del cliente para la alt key compuesta. |
| Tipo | `sanic_cm_dr_tipo` | Text(100) | Sí | `"Casa"` / `"EC"` / `"Trabajo"` (lo fija el mapeo con valor por defecto). |

### Llaves
- **Alt key compuesta `sanic_cm_pk_direccion_cif_tipo`** (`dr_cif` + `dr_tipo`). **Active.** Una dirección por (cliente, tipo). Va sobre
  **texto** (CIF copiado), no sobre el lookup: el vínculo se arma con el lookup del mapeo.

### Relaciones
- Entrante `cliente → direccion`: Cascade en las cinco acciones (§B). Es **hoja**.

### Import
- **3 Table Mappings** (`Direccion (casa)`, `Direccion (EC)`, `Direccion (Trabajo)`), **orden 2**. Dedup `dr_cif + dr_tipo`.
- Cada uno: `DireccionX → dr_direccion`, `CIF → dr_cif`, `dr_tipo` = **valor por defecto** (`Casa` / `EC` / `Trabajo`), y el lookup
  `dr_clienteid` por `CIF → cl_cif`.

### Ciclo de vida
- **Nace:** del **CSV** (la explosión por tipo) **o** por el **Admin** en Maestros (agrega; **no hay editar ni borrar en las apps**).
- **Vive:** guarda el **dato ACTUAL**. El upsert por `cif + tipo` **pisa** el valor anterior. El histórico de cambios **no** vive acá:
  los cambios que pide el cobrador quedan en `actualizaciondatos`, que **nadie aplica** a esta tabla: la actualización termina en el core del banco, por decisión del dueño (DAT-19, 2026-10-14).
- **Teléfono del cobrador:** baja al teléfono por el Share en cascada del cliente. La app solo carga Casa y Trabajo; **EC** se importa pero
  no se muestra.
- **Fin:** soft; se conserva mientras exista el cliente.

### Cómo borrar
- **Entrante:** `cliente → direccion` = **Cascade** — borrar el cliente borra sus direcciones. Es **hoja**: no arrastra nada.

### Consideraciones al borrar
- Se borra **junto con el cliente**. Por sí sola se puede borrar (Admin, API), pero no es lo habitual.
- **Tipos huérfanos (stale):** si el CSV deja de mandar un tipo, la fila vieja **queda** (el import es upsert-only). Dato de contacto
  desactualizado, baja prioridad.

### Estado de la revisión
**2026-10-06.** Verificado en vivo: campos, alt key activa, cascadas, mapeos (3). Sin discrepancias salvo longitudes y nombres de los
mapeos, que ahora están documentados.

---

## 3. `sanic_cm_tbl_telefono` — Teléfono del cliente

### Definición
Los **teléfonos** del cliente, **normalizados por tipo** (`Celular` / `Casa`). Cada fila del CSV trae 2 teléfonos → el import crea
**una fila por tipo**.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_tl_numero` | Text(20) | Sí | **Nombre principal.** `NumeroCelular` / `NumeroCasa`. |
| Cliente | `sanic_cm_tl_clienteid` | Lookup → `cliente` | Sí | Resuelto por `CIF`. |
| CIF | `sanic_cm_tl_cif` | Text(20) | Sí | **Copiado** del cliente para la alt key compuesta. |
| Tipo | `sanic_cm_tl_tipo` | Text(20) | Sí | `"Celular"` / `"Casa"` (lo fija el mapeo). |

### Llaves
- **Alt key compuesta `sanic_cm_pk_telefono_cif_tipo`** (`tl_cif` + `tl_tipo`). **Active.** Un teléfono por (cliente, tipo).

### Relaciones
- Entrante `cliente → telefono`: Cascade en las cinco acciones (§B). Es **hoja**.

### Import
- **2 Table Mappings** (`Telefono (Celular)`, `Numero (casa)`), **orden 2**. Dedup `tl_cif + tl_tipo`.
- Cada uno: `NumeroX → tl_numero`, `CIF → tl_cif`, `tl_tipo` = valor por defecto (`Celular` / `Casa`), lookup `tl_clienteid` por `CIF → cl_cif`.

### Ciclo de vida
- **Nace:** del **CSV** o por el **Admin** (Maestros, solo agrega).
- **Vive:** dato **ACTUAL**; el upsert por `cif + tipo` **pisa**. Histórico de cambios → `actualizaciondatos`.
- **Fin:** soft; se conserva mientras exista el cliente.

### Cómo borrar
- **Entrante:** `cliente → telefono` = **Cascade**. Es **hoja**.

### Consideraciones al borrar
- `tl_numero` es **texto** de 20 caracteres (puede traer ceros a la izquierda, guiones, prefijos). La app valida 7 a 20 caracteres
  al capturar una actualización.
- Mismas consideraciones de cascada y tipos huérfanos que `direccion`.
- **Teléfono de trabajo = capture-only (decidido):** el master tiene **solo** `Celular` y `Casa`. El "teléfono de trabajo" que captura
  `actualizaciondatos` **vive solo ahí** (no se propaga al master); se transcribe al core como las demás interacciones. Mismo patrón
  que el correo.

### Estado de la revisión
**2026-10-06.** Verificado en vivo: campos, alt key activa, cascadas, mapeos (2).

---

## 4. `sanic_cm_tbl_producto` — Producto (catálogo)

### Definición
Catálogo del **producto en mora** (tarjeta o préstamo). **Clasifica** la cuenta. Es **padre** de `cuenta`.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Código | `sanic_cm_pr_codigo` | Text(20) | Sí | **Nombre principal.** `CodigoProducto`. **Alt key.** |
| Tipo | `sanic_cm_pr_tipo` | Text(20) | Sí | `TipoProducto`. |

### Llaves
- **Alt key `sanic_cm_pk_producto_codigo`** (`pr_codigo`). **Active.** Upsert por código.

### Relaciones
- `producto → cuenta`: **Restrict** de borrado; nada se propaga (§B).

### Import
- **Table Mapping `Producto`**, **orden 1** (catálogo raíz). `CodigoProducto → pr_codigo` · `TipoProducto → pr_tipo`. Dedup `pr_codigo`.

### Ciclo de vida
- **Nace:** del **CSV** (upsert por código). **Vive:** casi **estático**; un código discontinuado se queda (cuentas históricas lo
  referencian). **Fin:** no se borra.
- **No está en el perfil offline** y las apps no lo leen (usan el texto denormalizado de la cuenta); lo ve cualquier rol de persona (R:G),
  sin tener privilegio de escritura.
- **Sin auditoría de tabla.**

### Cómo borrar
- **`producto → cuenta` = Restrict** (catálogo compartido). **Nunca** se cascadea; se **bloquea** si hay cuentas que lo referencian.
  No hay job de borrado.

### Consideraciones al borrar
- **Nunca hard-delete** (referenciado por cuentas históricas). Discontinuar = dejar de usarlo.
- No tiene flag de "activo".
- Es la única de las relaciones de catálogo que además es **obligatoria** en la cuenta (`ct_productoid` = Sí).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: campos, alt key activa, `Restrict`, mapeo. `pr_tipo` figura como requerido en la metadata
(el catálogo anterior lo daba opcional).

---

## 5. `sanic_cm_tbl_agente` — Agente / Localizador (catálogo + extensión operativa)

### Definición
El **Agente** del CSV **es el localizador** (DEC-006). Catálogo poblado por el import **+** las columnas operativas que lo ligan a su
identidad de Entra. Es **padre** de `cuenta`, `asignacion`, `interaccion` y `cargadiaria`.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_ag_nombre` | Text(200) | Sí | **Nombre principal.** Del CSV (`Agente`). **Alt key.** |
| Usuario | `sanic_cm_ag_usuarioid` | Lookup → `systemuser` | Sí | **Operativo.** Identidad del cobrador; lo setea el **Admin** (no viene del CSV). Smelter crea agentes sin él, así que el servidor no lo exige. |
| Email Usuario | `sanic_cm_ag_emailusuario` | Text(250), formato Email | — | **Derivado por el plugin `EmailDesdeUsuario`**: UPN del usuario en minúsculas. La app del cobrador identifica a su agente con `Lower(User().Email)`. El backoffice solo escribe el lookup del usuario y el correo lo pone el plugin (DAT-17, cerrado 2026-10-14). |
| Activo | `sanic_cm_ag_activo` | Yes/No (def. **Sí**) | — | **Soft-delete:** marca si el localizador está operativo. |

### Llaves
- **Alt key `sanic_cm_pk_agente_nombre`** (`ag_nombre`). **Active.** Upsert por nombre. El vínculo `ag_usuarioid` **no** va en la clave.

### Relaciones
- Hijos con **Restrict** de borrado: `cuenta`, `asignacion`, `interaccion`, `cargadiaria`. Nada se propaga (§B).
- `systemuser → agente` (`ag_usuarioid`): `RemoveLink`.

### Import
- **Table Mapping `Agente`**, **orden 1**. `Agente → ag_nombre`. Dedup `ag_nombre`.
- `ag_usuarioid`, `ag_emailusuario` y `ag_activo` **no** vienen del CSV: el **Admin** los gestiona (el default de `ag_activo` es Sí).

### Ciclo de vida
- **Nace:** del **CSV** (solo el nombre). El **Admin lo vincula** a un usuario en *Agentes y usuarios* (el plugin pone el correo).
- **Estados:** `SinUsuario` → `ConUsuario` (vincula) → `Inactivo` (desactiva); el Admin puede cambiar o quitar el usuario (correo vacío).
- **Vive:** persiste. Ancla el reparto: `RepartoAsignacion` lee `ag_usuarioid` para dar el dueño y el acceso. **Sin usuario, sus asignaciones van
  al equipo "Cartera sin cobrador"** (sin miembros: nadie las ve) y el cobrador anterior las pierde (DAT-07, cerrado 2026-10-14).
- **Fin:** cuando el localizador se va → **`ag_activo = No`** (soft-delete). **Nunca** se hard-delete.

### Cómo borrar
- **NUNCA hard-delete:** referenciado por cuentas, asignaciones, interacciones y cargas = historia + seguridad.
- `agente → cuenta / asignacion / interaccion / cargadiaria` = **Restrict**.
- **Fin de vida = soft-deactivate** (`ag_activo = No`).

### Consideraciones al borrar
- **Nombre cambiante:** si el `Agente` cambia en el origen, el import crea **otro** agente (no renombra) → hay que **re-vincular** el usuario.
  Acordar que el `Agente` del CSV sea **estable**.
- **Localizador que renuncia:** `ag_activo = No` + control de "asignaciones de agentes inactivos" (el CSV puede insistir en mandarle
  cuentas; gana el CSV, DEC-020).
- **Vincular, cambiar o quitar el usuario re-reparte sus asignaciones activas** (`RepartoPorAgente`, DAT-08, cerrado 2026-10-14).
- Borrarlo rompería **historia** y **seguridad por fila**.
- `ag_usuarioid` y `ag_emailusuario` tienen auditoría de tabla activa (`agente` está auditada desde 2026-10-06).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: campos (incluida `ag_emailusuario`, que el catálogo anterior no tenía), alt key activa, `Restrict`,
plugin `EmailDesdeUsuario` activo, auditoría. No confirmado: cuántos agentes tienen hoy usuario vinculado.

---

## 6. `sanic_cm_tbl_fiador` — Fiador / Fianza (catálogo)

### Definición
El **fiador** (fianza) del producto en mora. Es **padre** de `cuenta`. Cerca de la mitad de las cuentas **no** tienen fiador
(`NombreFianza` vacío → no se crea fiador).

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_fi_nombre` | Text(200) | Sí | **Nombre principal.** `NombreFianza`. **Alt key.** |
| Dirección | `sanic_cm_fi_direccion` | Text(200) | — | `DireccionFianza`. |

### Llaves
- **Alt key `sanic_cm_pk_fi_nombre`** (`fi_nombre`). **Active.** Upsert por nombre.

### Relaciones
- `fiador → cuenta`: **Restrict** de borrado; nada se propaga (§B).

### Import
- **Table Mapping `Fiador`**, **orden 1**. `NombreFianza → fi_nombre` · `DireccionFianza → fi_direccion`. Dedup `fi_nombre`.
- La **cuenta** guarda además el nombre y la dirección del fiador **denormalizados** (`ct_nombrefiador`, `ct_direccionfiador`).

### Ciclo de vida
- **Nace:** del **CSV** (upsert por nombre). **Vive:** persiste. **Fin:** no se borra.
- **No está en el perfil offline**; el reparto (`RepartoAsignacion`) **sí le comparte el fiador** al cobrador (junto con cuenta y cliente) y lo revoca al cerrar,
  aunque la tabla no baje al teléfono. **Sin auditoría de tabla.**

### Cómo borrar
- **`fiador → cuenta` = Restrict** (catálogo). No se cascadea; se bloquea si está en uso.

### Consideraciones al borrar
- **Unicidad de `NombreFianza` (a VALIDAR con el área):** la alt key es por **nombre**. Si dos fianzas **distintas** comparten nombre
  exacto, el upsert las **colapsa en una**. Si no es único en el origen, hay que repensar la clave (agregar un identificador de fianza).
  Pregunta abierta al negocio.
- En DEV había **2785 fiadores para 1010 cuentas** y el origen no se conocía. Se explicó el 2026-10-14: cada fila del CSV con
  `NombreFianza` vacío hace que la carga cree un fiador sin nombre, porque la alt key no rige con NULL. Eran 2777 huérfanos y 8
  reales. Un trabajo de Bulk Deletion **diario** borra los fiadores sin nombre (DEC-019; un fiador referenciado no se puede
  borrar, porque `fiador → cuenta` es Restrict). **Pendiente:** la corrección de origen, que es configuración de Smelter (que la
  plantilla no cree el fiador cuando viene vacío).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: campos, alt key activa, `Restrict`, mapeo. Abierto: unicidad de `NombreFianza`.

---

## 7. `sanic_cm_tbl_cuenta` — Cuenta en mora (el grano)

### Definición
La **cuenta en mora** (tarjeta o préstamo) que se cobra. **El grano del modelo.** Guarda el **estado ACTUAL** (el upsert la pisa cada
día; la historia financiera va a `cargadiaria`). Cuelga de `cliente` / `producto` / `agente` / `fiador`; es **padre** de `asignacion`
y `cuentapan`, y referencia de `interaccion`, `pago` y `cargadiaria`.

**Desde la Fase 2 PAN (2026-10-04 al 06) la cuenta ya no guarda el número de tarjeta completo:** el nombre principal guarda el número
**enmascarado** `****1234`, la identidad de negocio es el **HMAC** `ct_clavepan`, y el número completo vive **solo** en la bóveda
(`cuentapan`, §13).

### Campos

**Identidad y vínculos**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número de cuenta (enmascarado) | `sanic_cm_ct_numerodecuenta` | Text(20) | Sí | **Nombre principal.** `****1234` (`CONCAT("****", RIGHT(TRIM(NumeroCuenta),4))`). `PanGuard` rechaza 5 o más dígitos. El nombre mostrado sigue siendo `[CT] Numero de cuenta`. |
| Clave PAN (HMAC) | `sanic_cm_ct_clavepan` | Text(64) | — | HMAC-SHA256 en hex minúsculas del número. **Alt key.** Lo calcula la plantilla. |
| Últimos 4 | `sanic_cm_ct_ultimos4` | Text(4) | — | Para buscar (Solicitante, búsqueda rápida). |
| Cliente | `sanic_cm_ct_clienteid` | Lookup → `cliente` | Sí | Resuelto por `CIF`. |
| Producto | `sanic_cm_ct_productoid` | Lookup → `producto` | Sí | Resuelto por `CodigoProducto`. |
| Agente | `sanic_cm_ct_agenteid` | Lookup → `agente` | — | Resuelto por `Agente`. Escrita por Smelter, **nunca leída** por las apps. **Solo carga** (decisión del dueño, 2026-10-15, DAT-30): se conserva porque borrarla exige quitar su mapeo en la plantilla de Smelter. |
| Fiador | `sanic_cm_ct_fiador` | Lookup → `fiador` | — | Resuelto por `NombreFianza`. |

**Denormalizados para las apps** (texto; las apps leen **estos**, no los de `cliente` ni `fiador`)
| Campo lógico | Físico | Tipo | Del CSV |
|--------------|--------|------|---------|
| CIF Cliente | `sanic_cm_ct_cifcliente` | Text(250) | `CIF` |
| Nombre Cliente | `sanic_cm_ct_nombrecliente` | Text(250) | `Cliente` |
| Nombre Fiador | `sanic_cm_ct_nombrefiador` | Text(250) | `NombreFianza` |
| Dirección Fiador | `sanic_cm_ct_direccionfiador` | Text(250) | `DireccionFianza` |
| Tipo Producto | `sanic_cm_ct_tipoproducto` | Text(250) | **Sin mapeo en la plantilla** (origen no confirmado) |

**Montos** (Decimal, precisión 2, rango 0 a 100 000 000 000)
| Campo lógico | Físico | Del CSV |
|--------------|--------|---------|
| Saldo vencido NIO | `sanic_cm_ct_saldovencidonio` | `SaldoVencidoNIO` |
| Saldo vencido USD | `sanic_cm_ct_saldovencidousd` | `SaldoVencidoUSD` |
| Total saldo vencido | `sanic_cm_ct_totalsaldovencido` | `TotalSaldoVencido` (viene del CSV, no se calcula acá) |
| Contagiado USD | `sanic_cm_ct_contagiadousd` | `ContagiadoUSD` |
| Pago mínimo NIO | `sanic_cm_ct_pagominimonio` | `PagoMinimoNIO` |
| Pago mínimo USD | `sanic_cm_ct_pagominimousd` | `PagoMinimoUSD` |
| Retiro NIO | `sanic_cm_ct_retironio` | `RetiroNIO` — dinero a **traer** (objetivo de cobro) |
| Retiro USD | `sanic_cm_ct_retirousd` | `RetiroUSD` |

**Mora, fechas y atributos**
| Campo lógico | Físico | Tipo | Del CSV |
|--------------|--------|------|---------|
| Días de mora | `sanic_cm_ct_diasmora` | Whole Number (>= 0) | `DiasMora` |
| Nivel de mora | `sanic_cm_ct_nivelmora` | Whole Number (>= 0) | `NivelMora` |
| Fecha último pago | `sanic_cm_ct_fechaultimopago` | DateTime (Date and Time, hora local) | `FechaUltimoPago` |
| Fecha de pago | `sanic_cm_ct_fechapago` | **Text(100)** | `FechaPago` — **es texto, no fecha**; en DEV mezcla `20/07/2026` y `2026-07-17` (DAT-26) |
| Ciclo | `sanic_cm_ct_ciclo` | Text(10) | `Ciclo` |
| Tipo de asignación | `sanic_cm_ct_tipoasignacion` | Text(200) | `TipoAsignacion` (`Diaria`/`Incobrable`/`Especial`); texto. La `asignacion` toma el tipo directo del CSV, no de acá. **Solo carga**, nadie la lee (DAT-30, 2026-10-15) |
| Tipo especial | `sanic_cm_ct_tipoespecial` | Text(200) | `TipoEspecial` |
| Nota | `sanic_cm_ct_nota` | Text(400) | `Nota` |

**Columnas eliminadas (Fase 2, paso 8):** el número completo ya no está en ninguna columna de la cuenta. Verificado que no existen las
columnas `sanic_cm_as_numerocuenta`, `sanic_cm_in_numerocuenta` ni `sanic_cm_pg_numerocuenta` (las denormalizaciones viejas
de asignación, interacción y pago).

### Llaves
- **Alt key `sanic_cm_ak_ct_clavepan`** (`ct_clavepan`). **Active.** Reemplaza a `sanic_cm_pk_ct_numerodecuenta`, **que ya no existe**
  (con la clave única viva, `****1234` colisionaba: 81 cuentas en 40 grupos). Upsert por el HMAC del número.
- El nombre principal **ya no es único** ni clave: dos cuentas distintas pueden compartir `****1234`.

### Relaciones
- Entrante: `cliente → cuenta` (Delete y Assign = Cascade; Share, Unshare y Reparent = NoCascade). Catálogos `producto/agente/fiador →
  cuenta` = Restrict.
- Saliente: `cuenta → cuentapan` (Cascade en todo), `cuenta → asignacion` (Delete = Cascade; Assign, Share, Unshare y Reparent = NoCascade, Assign desde el 2026-10-14 por DAT-18),
  `cuenta → interaccion / pago / cargadiaria` (**RemoveLink**). Ver §B.

### Import
- **Table Mapping `Cuenta`**, **orden 3** (después de los 4 padres y de direcciones/teléfonos). Dedup `ct_clavepan`.
- **Columnas:** los 8 montos (Decimal), `DiasMora`/`NivelMora` (Whole Number), `FechaUltimoPago` (DateTime), `FechaPago` (**texto**, sin
  transformación), `Ciclo`/`TipoAsignacion`/`TipoEspecial`/`Nota` (Text), los denormalizados (`CIF`, `Cliente`, `NombreFianza`,
  `DireccionFianza`), `NumeroCuenta` enmascarado → `ct_numerodecuenta`, `HMAC(NumeroCuenta)` → `ct_clavepan`, `RIGHT(.,4)` → `ct_ultimos4`.
- **Lookups:** `Cliente` (CIF → `cl_cif`) · `Producto` (`CodigoProducto` → `pr_codigo`) · `Agente` (`Agente` → `ag_nombre`) · `Fiador`
  (`NombreFianza` → `fi_nombre`).
- **Especiales del CSV (DAT-01):** `RetiroNIO/USD`, `TipoEspecial` y `Nota` se mapean **solo a la cuenta**. La `asignacion` tiene sus
  propias columnas de retiro y notas, que Smelter **no** llena. La cuenta es compartida por todas sus asignaciones y la próxima carga la
  pisa.
- **Caracteres prohibidos** (`/ < > * % & : \ ? +`): rompían el upsert cuando la clave era el número. Ahora la clave es el HMAC (hex), así que ya
  no deberían afectar; **no se re-probó**.

### Ciclo de vida
- **Nace:** **solo de Smelter** (upsert por `ct_clavepan`). No hay forma de crear una cuenta desde las apps: el Solicitante tiene `Create`
  vacío y el backoffice no tiene pantalla (DAT-20). La promesa original (DEC-013: el Solicitante crea la cuenta si no existe) no se cumple.
- **Vive:** el upsert **PISA** cada día (saldos, mora, datos del cliente). La **historia financiera** → `cargadiaria`. Lo que el
  cobrador ve de una cuenta es lo que el **reparto le comparte** (`RepartoAsignacion`) (con la asignación activa y, si es Especial, aprobada).
- **Dueño:** `SYSTEM` (muestra de 500 filas).
- **Fin:** **soft.** "Fuera de cobranza" = sin asignación activa. No se borra de rutina.

### Cómo borrar
- **Entrante:** `cliente → cuenta` = **Cascade** (borrar el cliente borra la cuenta). Catálogos `producto/agente/fiador → cuenta` = **Restrict**.
- **Salientes (Delete):**
  - `cuenta → cuentapan` = **Cascade** (la bóveda se va con la cuenta; nunca queda un número sin cuenta).
  - `cuenta → asignacion` = **Cascade** (operativa; el rastro de reasignaciones vive en la auditoría, DEC-016).
  - `cuenta → interaccion`, `cuenta → pago`, `cuenta → cargadiaria` = **RemoveLink** (quedan sin cuenta; no bloquean).
- **Efecto neto verificado:** borrar una cuenta **borra** sus asignaciones y su bóveda y **deja huérfanas** sus interacciones, pagos y
  filas de carga diaria. **No se bloquea** (el catálogo anterior decía que se bloqueaba por interacciones: era `Restrict` en el diseño, no en el build).

### Consideraciones al borrar
- Hoy **nada protege la cobranza registrada** de un borrado de cuenta: solo que el único que puede borrar es el Admin por la API.
  Si se quiere el candado de DEC-023, hay que pasar `cuenta → interaccion` (y `→ pago`) a `Restrict`. Tradeoff: con `Restrict` ya no se podría
  borrar un cliente con cobranza sin antes tratar las interacciones; requiere ventana de cambio.
- **Assign:** `cliente → cuenta` y `cuenta → cuentapan` siguen en Cascade; `cuenta → asignacion` es **NoCascade** desde el 2026-10-14
  (DAT-18): reasignar la cuenta ya no pisa el dueño que puso `RepartoAsignacion`.
- **Estado actual vs histórico:** la cuenta se pisa; la evolución vive en `cargadiaria`.
- **Lo que edita el Admin en Maestros sobre el cliente no cambia los denormalizados de la cuenta** (`ct_nombrecliente`, `ct_cifcliente`), y la
  próxima carga los vuelve a escribir (DAT-26).
- **Cambiar el número de tarjeta de una cuenta** (ej. reemisión) crea **otra** cuenta, porque la identidad es el HMAC del número.

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas (32 custom), alt key `sanic_cm_ak_ct_clavepan` activa y la vieja eliminada, cascadas,
mapeo de Smelter, plugins. Discrepancias: `ct_fechapago` es texto (el catálogo decía DateTime); Smelter no mapea `ct_tipoproducto`; el
bloqueo por interacciones no existe. No confirmado: origen de `ct_tipoproducto`.

---

## 8. `sanic_cm_tbl_cargadiaria` — Snapshot histórico diario

### Definición
Una **foto diaria** de cada cuenta en mora (montos, mora, agente del día). Mientras `cuenta` guarda el **estado actual** (se pisa),
`cargadiaria` guarda la **historia** — un registro por **(cuenta, día)**. Guarda un **subconjunto** de `cuenta`: solo lo que **cambia
día a día**.

### Campos

**Identidad y vínculos**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número cuenta (enmascarado) | `sanic_cm_cd_numerocuenta` | Text(20) | Sí | **Nombre principal.** `****1234`. `PanGuard` rechaza 5 o más dígitos. Ya **no** forma parte de la clave. |
| Clave PAN (HMAC) | `sanic_cm_cd_clavepan` | Text(64) | — | HMAC del número. Forma parte de la **alt key**. |
| Fecha de carga | `sanic_cm_cd_fechacarga` | DateTime (**Date Only**, sin zona horaria) | Sí | **`FechaCarga`** — columna del CSV (no `createdon`). |
| Cuenta | `sanic_cm_cd_cuenta` | Lookup → `cuenta` | — | Resuelto con el HMAC contra `ct_clavepan`. |
| Cliente | `sanic_cm_cd_clienteid` | Lookup → `cliente` | Sí | Por `CIF`. Es el que ata la carga al borrado en cascada del cliente. |
| Agente | `sanic_cm_cd_agenteid` | Lookup → `agente` | — | Por `Agente` — **quién la tenía ese día**. |
| Tipo de asignación | `sanic_cm_cd_tipoasignacion` | Text(100) | — | `TipoAsignacion` del día. |

**Montos del día** (Decimal, precisión 2, 0 a 100 000 000 000) — los mismos 8 que `cuenta`: `sanic_cm_cd_saldovencidonio`,
`sanic_cm_cd_saldovencidousd`, `sanic_cm_cd_totalsaldovencido`, `sanic_cm_cd_contagiadousd`, `sanic_cm_cd_pagominimonio`,
`sanic_cm_cd_pagominimousd`, `sanic_cm_cd_retironio`, `sanic_cm_cd_retirousd`.

**Mora del día** (Whole Number >= 0): `sanic_cm_cd_diasmora`, `sanic_cm_cd_nivelmora`.

### Llaves
- **Alt key compuesta `sanic_cm_ak_cd_clavepan_fecha`** (`cd_clavepan` + `cd_fechacarga`). **Active.** Reemplaza a
  `sanic_cm_pk_cd_numerocuenta_fechacarga` (eliminada). Un registro por cuenta por día; mismo día re-subido = upsert (no duplica).

### Relaciones
- Entrantes: `cliente → cargadiaria` (**Delete = Restrict** desde el 2026-10-14, DAT-18; Assign = Cascade; Share/Unshare/**Reparent = NoCascade**), `cuenta → cargadiaria`
  (**RemoveLink**), `agente → cargadiaria` (Restrict). Es **hoja**.

### Import
- **Table Mapping `Carga Diaria`**, **orden 5** (después de cuenta y bóveda). Dedup `cd_clavepan + cd_fechacarga`. Es **el mismo import
  diario**.
- **Prerrequisito:** el CSV debe traer `FechaCarga`.
- **Columnas:** los 8 montos, `DiasMora`/`NivelMora`, `TipoAsignacion`, `FechaCarga`, número enmascarado, HMAC. **Lookups:** `Cuenta` (HMAC →
  `ct_clavepan`), `Cliente` (CIF), `Agente`.

### Ciclo de vida
- **Nace:** del **CSV** (una fila por cuenta por día), solo Smelter. **Nadie la cambia.**
- **Vive:** **ACUMULA**. No se pisa como `cuenta`. Casi nadie la lee: *Monitoreo de carga* (Admin) cuenta las filas de hoy y la
  reconciliación compara contra la última carga; el resto del snapshot es histórico. No está en el perfil offline.
- **Cobertura por tipo:** `Diaria` todos los días; `Incobrable` solo los días que aparece (esparso); `Especial` del CSV tiene snapshot el
  día que viene; `Especial` pedida en la app **no** (no está en el CSV).
- **Fin:** **retención de 60 días** (DEC-019, decisión del dueño, 2026-10-14): un trabajo de Bulk Deletion diario borra las filas con
  `fechacarga` de más de 60 días. Sin él, la tabla crecería ~1 fila por cuenta por día.

### Cómo borrar
- **Entrante:** `cliente → cargadiaria` = **Restrict** desde el 2026-10-14 (DAT-18): no se puede borrar un cliente que tenga histórico. `cuenta → cargadiaria` = **RemoveLink** (borrar la
  cuenta deja la fila con `cd_cuenta` vacío, no la borra).
- **Retención (DEC-019):** hecha el 2026-10-14. Bulk Deletion diario de las filas de más de 60 días (`scripts/seguridad/paso10_borrado_programado.py`).
- Es **hoja**: borrado seguro (no arrastra nada).

### Consideraciones al borrar
- Es la **única tabla descartable** del modelo: el estado actual vive en `cuenta`, la cartera en `asignacion`. Borrarla **no afecta la operación**,
  solo el histórico.
- **Crece rápido** (decenas de millones por año a escala) → la tabla a vigilar. Sin `ChangeTracking` (no viaja al teléfono).
- **No confundir con `asignacion`:** `cargadiaria` = histórico financiero; `asignacion` = cartera viva.
- Un **cliente** con histórico financiero **no se puede borrar**: `cliente → cargadiaria` es `Restrict` desde el 2026-10-14 (DAT-18).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas (20), alt key `sanic_cm_ak_cd_clavepan_fecha` activa y la vieja eliminada, cascadas, mapeo, ausencia
de job de retención. Discrepancias: `cuenta → cargadiaria` es `RemoveLink` (el catálogo anterior decía Cascade); `cd_clienteid` figura como
requerido (el catálogo lo daba opcional).

---

## 9. `sanic_cm_tbl_interaccion` — Interacción (núcleo de la captura)

### Definición
Cada **interacción** del localizador con un cliente: **comentario libre**, GPS, si hubo pago, si hay datos a actualizar y el **write-back al
core**. Se crea **en la app de campo (offline)** y es **el rastro de auditoría** de la cobranza. El cobrador la crea pero no la edita (S-9, cerrado
2026-10-14) y el servidor controla su grabación (DAT-11).

### Campos

**Captura del localizador (en campo, offline)**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número de interacción | `sanic_cm_in_numerointeraccion` | Text(20), autonumérico **`IN-{SEQNUM:4}`** | auto | **Nombre principal.** Lo asigna el servidor al **sincronizar** (offline está vacío). Semilla 1000 según el contrato de datos; **no se pudo re-verificar** la semilla (es una acción). |
| Cuenta | `sanic_cm_in_cuentaid` | Lookup → `cuenta` | Sí | La cuenta gestionada. |
| Agente | `sanic_cm_in_agente` | Lookup → `agente` | Sí | El localizador (el de su correo). Si la crea un cobrador, lo pone el servidor: el agente vinculado a su usuario (DAT-13/SEC-08). |
| Nombre agente | `sanic_cm_in_nombreagente` | Text(200) | Sí | Nombre del agente copiado (texto). |
| Comentario | `sanic_cm_in_comentario` | Multiline Text(2000) | Sí | **Texto libre** de la interacción. |
| Fecha de captura | `sanic_cm_in_fechacaptura` | DateTime (Date and Time, hora local) | — | La pone el **teléfono** al registrar. |
| ¿Actualiza datos? | `sanic_cm_in_actualizainfo` | Yes/No (def. No) | — | Marca que hay datos a actualizar (detalle en `actualizaciondatos`). |
| ¿Aplica pago? | `sanic_cm_cd_aplicapago` | Yes/No (def. No) | — | Si hubo cobro (montos en `pago`). **Prefijo `cd` en el esquema real, no `in`.** |
| Latitud | `sanic_cm_in_latitud` | Decimal(**6**), -90 a 90 | — | GPS. Era Decimal(2) (~1 km); hoy **6 decimales** (DAT-05 corregido). |
| Longitud | `sanic_cm_in_longitud` | Decimal(**6**), -180 a 180 | — | GPS. |
| Propietario | `ownerid` | Owner | Sí | = el **localizador** (nivel usuario). |

**Write-back al core (backoffice)**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Estado de grabación | `sanic_cm_in_estadograbacion` | Choice `sanic_cm_choice_estadograbacioninteraccion`: **1 Pendiente** (def.), **2 Grabado** | — | Cola del backoffice: `Pendiente`. (El catálogo anterior decía "Grabada en core": el valor real es **Grabado**.) |
| Código del core | `sanic_cm_in_codigograbacion` | Text(10) | — | Código con que el backoffice la grabó (en mayúsculas). Es obligatorio para pasar a Grabado (DAT-11). |
| Fecha de grabación | `sanic_cm_in_fechagrabacion` | DateTime | — | Cuándo la transcribió. La pone el servidor (DAT-11). |
| Grabado por | `sanic_cm_in_grabadopor` | Lookup → `systemuser` | — | Qué backoffice. Lo pone el servidor (DAT-11). |

### Llaves
- **Ninguna.** Se crea en la app (no por import) → usa su **GUID**. Es un **hecho fechado**, no se deduplica.

### Relaciones
- Entrantes: `cuenta → interaccion` (**RemoveLink**; nada se propaga), `agente → interaccion` (Restrict), `systemuser → interaccion` (`grabado por`, RemoveLink).
- Salientes: `interaccion → pago` e `interaccion → actualizaciondatos` (**Delete = Restrict** desde el 2026-10-14, DAT-18; las otras cuatro acciones, Cascade: Share en cascada incluido).

### Import
- **N/A** — no viene del CSV. La crea la **app de campo**; el número lo asigna el servidor al sincronizar.

### Ciclo de vida
- **Nace:** en la **app del cobrador** (con o sin señal), solo el cobrador. Owner = él. Nada la crea fuera de la app. El plugin `InteraccionDelCobrador` exige cuenta y agente y, si la crea un cobrador, pone el agente vinculado a su usuario y la deja Pendiente, sin código, fecha ni usuario de grabación (DAT-13, SEC-08).
- **Estados:** `En el teléfono` (sin sincronizar) → **Pendiente** (el servidor asigna `IN-xxxx`) → **Grabado** (el BackOffice transcribe al core:
  código, fecha, "grabado por"; desde la cola *Por transcribir*).
- **Grabado es terminal:** el cobrador crea pero no edita la interacción (S-9, cerrado 2026-10-14) y el plugin `GrabacionInteraccion`
  impide volver a grabarla o cambiar su código (DAT-11, 2026-10-14): la segunda persona del BackOffice recibe un error en vez de
  pisar la grabación. La fecha y el usuario de grabación los pone el servidor, y el BackOffice solo escribe las columnas de
  grabación. **Límite:** dos grabaciones en el mismo milisegundo podrían pasar las dos (el plugin lee la versión previa antes de
  bloquear la fila).
- **En el teléfono:** últimos 90 días (por fecha de modificación).
- **Fin:** **no se borra.** Es el rastro de auditoría.

### Cómo borrar
- **Entrante:** `cuenta → interaccion` = **RemoveLink**, **no `Restrict`** (corrección del catálogo anterior, que lo marcaba "CONFIRMADO"). Una
  cuenta con interacciones **sí se puede borrar** y las interacciones quedan sin cuenta; **no existe el candado** que el modelo suponía.
- **Salientes:** `interaccion → pago` y `interaccion → actualizaciondatos` = **Restrict** desde el 2026-10-14 (DAT-18): el pago y la actualización son parte del registro y lo protegen.
- **Efecto neto:** una interacción con **pagos** (dato financiero) o con actualización de datos **no se puede borrar** (DAT-18,
  2026-10-14); antes arrastraba ambos.

### Consideraciones al borrar
- **El candado de auditoría del modelo de borrado está en la capa de privilegios** (solo Admin tiene Delete), **no en las relaciones**.
- **Offline:** el número es server-assigned al sync; GPS opcional (la app avisa y pide confirmación si falta).
- **Anti-doble-carga:** el filtro `estadograbacion = Pendiente` evita que dos backoffice la graben dos veces, y desde el 2026-10-14 el servidor también (`GrabacionInteraccion`, DAT-11).
- **Auditoría de tabla activa** (desde 2026-10-06).
- El plugin `CuentaDesdeInteraccion` lee de esta tabla al crear un pago (`in_cuentaid`, `in_numerointeraccion`).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas (21), sin alt key, cascadas, GPS con precisión 6, choice con valores, autonumérico `IN-{SEQNUM:4}`,
privilegios. Discrepancias: `RemoveLink` en lugar de `Restrict`; autonumérico `IN-{SEQNUM:4}` (el catálogo decía `INT-{SEQNUM:6}`); choice
`Grabado`; `in_estadograbacion` y `fechacaptura` figuran como opcionales; `in_nombreagente` es columna nueva. No confirmado: semilla del autonumérico.

---

## 10. `sanic_cm_tbl_pago` — Pago cobrado

### Definición
Un **pago** cobrado al cliente, **hijo de una interacción**. La relación **1:N** permite el **pago mixto** (una interacción con un pago en NIO
**y** otro en USD). El **recibo sigue siendo físico** (DEC-003); acá solo se **anota** monto, moneda y número de recibo.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_pg_numero` | Text(20), autonumérico **`PG-{SEQNUM:4}`** | auto | **Nombre principal.** Lo asigna el servidor al sincronizar. |
| Interacción | `sanic_cm_pg_interaccion` | Lookup → `interaccion` | Sí | El pago **pertenece** a una interacción. |
| Cuenta | `sanic_cm_pg_cuentaid` | Lookup → `cuenta` | — | **La completa el plugin** `CuentaDesdeInteraccion` desde la interacción (offline solo admite un nivel de lookup). Forma parte de la alt key. |
| Número de interacción | `sanic_cm_pg_numerointeraccion` | Text(100) | — | **La completa el plugin** con `in_numerointeraccion` (offline el autonumérico está vacío; el backoffice une por este texto). |
| Monto | `sanic_cm_pg_monto` | Decimal(2), 0 a 100 000 000 000 | — | Monto cobrado. La app exige > 0 y no tiene tope. |
| Moneda | `sanic_cm_pg_moneda` | Choice `sanic_choice_cm_monedaenpago`: **1 NIO**, **2 USD** (sin valor por defecto) | — | Moneda del pago. |
| Número de recibo | `sanic_cm_pg_numerorecibo` | Text(30) | Sí | **Número del recibo físico** (talonario). |
| Fecha del pago | `sanic_cm_pg_fecha` | DateTime (Date and Time, hora local) | — | La escribe la app al registrar. |
| Propietario | `ownerid` | Owner | Sí | = el **localizador** (nivel usuario). |

### Llaves
- **Alt key compuesta `sanic_cm_ak_pg_recibo_cuenta_moneda`** (`pg_moneda` + `pg_cuentaid` + `pg_numerorecibo`). **Active.** Impide registrar el mismo
  recibo dos veces en la misma cuenta y moneda (DAT-22; el catálogo anterior decía "sin clave").
- `pg_cuentaid` forma parte de la clave y lo completa el plugin `CuentaDesdeInteraccion` (PreOperation) cuando la app no lo manda (offline). No se probó el
  caso de un pago sin cuenta resoluble.

### Relaciones
- Entrantes: `interaccion → pago` (Delete = Restrict desde el 2026-10-14; el resto, Cascade) y `cuenta → pago` (**RemoveLink**).

### Import
- **N/A** — no viene del CSV. La crea la **app de campo** cuando el cobrador marca `aplicapago`.

### Ciclo de vida
- **Nace:** en la **app de campo**, hijo de una interacción. Owner = el localizador. El plugin `CuentaDesdeInteraccion` rechaza un pago sin interacción (DAT-13) y completa cuenta y N° de interacción al crear en el servidor.
- **Sin estados:** no se aprueba ni se cierra; es un hecho registrado. El BackOffice lo ve al transcribir y en el arqueo (*Transcritas hoy*).
- **Vive:** append-only para el cobrador: crea, pero no edita ni reasigna (S-9, cerrado 2026-10-14). Nadie fuera de Admin tiene `Write` sobre pagos.
- **En el teléfono:** últimos 90 días.
- **Fin:** **no se borra** de rutina (registro financiero).

### Cómo borrar
- **Entrante:** `interaccion → pago` = **Restrict** desde el 2026-10-14 (DAT-18): una interacción con pagos no se borra. `cuenta → pago` = **RemoveLink** (borrar la cuenta lo deja sin cuenta pero no lo borra).
- Es **hoja**. En la práctica nunca se borra solo.

### Consideraciones al borrar
- **Decimal, no Money:** cada pago lleva **su** moneda.
- **Unicidad del recibo:** la clave `recibo + cuenta + moneda` cubre el duplicado en una cuenta; **no** cubre el mismo recibo en cuentas distintas. La
  clave que se propuso (`localizador + numerorecibo`) no se implementó.
- El pago es el dato más sensible por plata: el cobrador lo crea pero no lo edita ni lo reasigna (S-9, cerrado 2026-10-14).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas (11), alt key activa, cascadas, plugin `CuentaDesdeInteraccion` activo, choice con valores.
Discrepancias: alt key **sí** existe; autonumérico `PG-{SEQNUM:4}` (el catálogo decía `PAG-{SEQNUM:6}`); `pg_cuentaid` y `pg_numerointeraccion` son columnas nuevas;
monto, moneda y fecha figuran como opcionales (el catálogo los daba requeridos).

---

## 11. `sanic_cm_tbl_asignacion` — Asignación (la cartera viva)

### Definición
**Qué cuenta se asignó a qué localizador**, de qué **tipo**, en qué **estado** y con qué **orden**. Tabla **operativa viva** (no un snapshot),
separada del histórico financiero `cargadiaria`. Es lo que cada localizador **tiene que cobrar**. Tiene **dos ejes independientes**: estado (Activa o Cerrada) y
aprobación (solo relevante para las Especiales).

### Campos

**Base / operativo**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_as_numero` | Text(20), autonumérico **`AS-{SEQNUM:4}`** | auto | **Nombre principal.** (El catálogo decía `ASG-{SEQNUM:6}`.) |
| Cuenta | `sanic_cm_as_cuentaid` | Lookup → `cuenta` | — | Resuelto con el HMAC contra `ct_clavepan`. |
| Clave PAN (HMAC) | `sanic_cm_as_clavepan` | Text(64) | — | HMAC de la cuenta. Forma parte de la **alt key**. Lo completa `ClaveDesdeCuenta` si viene vacío. **Reemplaza a `as_numerocuenta`** (eliminada). |
| Agente | `sanic_cm_as_agenteid` | Lookup → `agente` | — | El localizador. En Especiales lo define el **Supervisor**. |
| Tipo | `sanic_cm_as_tipo` | Choice `sanic_choice_cm_tipoasignacion`: **1 Diaria**, **2 Incobrable**, **3 Especial** (def. **3**) | — | Ciclo de vida por tipo. |
| Estado | `sanic_cm_as_estado` | **Yes/No** (Sí = **Activa**, No = **Cerrada**; def. Sí) | — | Vigencia. (No es un Choice como decía el catálogo anterior.) |
| Orden | `sanic_cm_as_orden` | Whole Number (-1 a 4000) | — | Prioridad del localizador; el cobrador la cambia desde la app, también sin conexión. **Smelter no la escribe.** |
| Fecha de asignación | `sanic_cm_as_fechasignacion` | DateTime | — | Smelter la mapea con `FechaCarga` en cada upsert, pero desde el 2026-10-14 el plugin `CargaSobreAsignacion` **no la pisa si ya tenía valor** (DAT-31): queda la fecha de alta, y la fecha de solicitud de una Especial del CSV ya no se pierde. |
| Última carga | `sanic_cm_as_ultimacarga` | DateTime | — | = `FechaCarga`; la setea el import en cada upsert. Base de la reconciliación (DEC-018). |
| Motivo | `sanic_cm_as_motivo` | Text(500) | — | Motivo de cierre (Reconciliación o Cierres). |
| Fecha de cierre | `sanic_cm_as_fechacierre` | DateTime | — | Cuándo se cerró. |
| Retiro NIO / USD | `sanic_cm_as_retironio` / `sanic_cm_as_retirousd` | Decimal(2), 0 a 1 000 000 000 | — | Retiro **de la solicitud Especial** (Solicitante). No los llena Smelter. |
| Notas | `sanic_cm_as_notas` | Multiline Text(2000) | — | Motivo de la solicitud; el tipo de solicitud se concatena acá con `" — "`. |

**Solo Especiales (workflow de aprobación)**
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Estado de aprobación | `sanic_cm_as_estadoaprobacion` | Choice `sanic_choice_cm_estadoaprobacionespecial`: **1 Borrador**, **2 Pendiente aprobación** (def. **2**), **3 Aprobado**, **4 Rechazada** | — | Gobierna la aprobación. Una Diaria o Incobrable nace en **Borrador** (DAT-28, plugin `AprobacionEspecial`); el default de la columna sigue en Pendiente porque las Especiales del CSV lo necesitan para entrar en la cola del Supervisor. |
| Resolutor | `sanic_cm_as_resolutorid` | Lookup → `systemuser` | — | Quién aprueba o rechaza. Escrita, nunca leída por las apps. |
| Solicitado por | `sanic_cm_as_solicitadopor` | Lookup → `systemuser` | — | El **Solicitante**. `RepartoAsignacion` le **comparte la Especial aprobada** solo si es quien la creó (`createdby`): el campo lo editan varios roles (S-7, cerrado). |
| Motivo de rechazo | `sanic_cm_as_motivorechazo` | Text(2000, área de texto) | — | Si el Supervisor rechaza. |
| Propietario | `ownerid` | Owner | Sí | Lo mueve el plugin `RepartoAsignacion` al usuario del agente (C.3). |

### Llaves
- **Alt key compuesta `sanic_cm_ak_as_clavepan_tipo`** (`as_tipo` + `as_clavepan`). **Active.** Reemplaza a `sanic_cm_altkey_NumeroCuentaxTipo` (eliminada). Una
  asignación por (cuenta, tipo): coexisten `{Diaria|Incobrable}` + `Especial`, nunca Diaria + Incobrable (DEC-021, regla de negocio, **no** una clave).

### Relaciones
- Entrante `cuenta → asignacion`: Delete = Cascade; Assign (desde el 2026-10-14, DAT-18), Share, Unshare y **Reparent = NoCascade** (§B). `agente → asignacion` = Restrict. `systemuser →`
  `as_resolutorid` y `as_solicitadopor` = RemoveLink. Es **hoja**.

### Import
- **Table Mapping `Asignacion`**, **orden 5**. Dedup `as_clavepan + as_tipo`.
- **Mapea:** `TipoAsignacion → as_tipo` (texto → Choice: Diaria 1, Incobrable 2, Especial 3), `FechaCarga → as_fechasignacion` y `→ as_ultimacarga`,
  `HMAC(NumeroCuenta) → as_clavepan`; lookups `Cuenta` (HMAC → `ct_clavepan`) y `Agente` (`ag_nombre`).
- **NO mapea** `as_estado` (nace Activa por el default), **`as_estadoaprobacion`** (nace **Pendiente aprobación** por el default de la columna: una Especial nueva
  del CSV entra sola en la cola del Supervisor y, como no se manda, **la carga diaria no pisa** una ya aprobada, DEC-022), `as_orden`, `as_motivo`, retiro ni notas.
  Una Especial por CSV con agente queda **sin compartir** hasta que el Supervisor la aprueba.
- **Owner:** lo mueve el plugin **`RepartoAsignacion`**, no Smelter. El **plugin `ClaveDesdeCuenta`** completa o valida la clave en cada Create/Update.
- **Reclasificación (DEC-021, Diaria ↔ Incobrable), DAT-15 (2026-10-14):** el plugin `Reclasificacion` deja una sola activa entre
  Diaria e Incobrable por cuenta; al llegar el otro tipo, cierra la anterior con motivo "Reclasificada a {tipo}" (verificado en las
  dos direcciones). Las Especiales conviven. En la app del cobrador, el saldo total de la ficha del cliente suma por cuenta
  distinta: una cuenta con Diaria más Especial aprobada ya no cuenta su saldo dos veces.
- **Reapertura (DAT-10, 2026-10-14):** una Diaria o Incobrable **cerrada por el sistema** ("fuera de cartera" o "Reclasificada a …") que
  **reaparece** en el CSV se **reabre** (`CargaSobreAsignacion`). No se reabre lo que cerró una persona (por ejemplo "Pagada") ni las Especiales.
- **DAT-28:** una Diaria o Incobrable que llega por CSV nace en Borrador (lo fuerza `AprobacionEspecial`); las Especiales del CSV siguen naciendo Pendiente.

### Ciclo de vida (por tipo)
| Tipo | Nace | Reasigna | Cierra | Notas |
|------|------|----------|--------|-------|
| **Diaria** | CSV diario (Smelter) | Gana el CSV; no se reasigna a mano (la pantalla *Reasignar* no incluye Diarias: DAT-29) | **Admin**: *Reconciliación* cierra en lote las que **no vinieron** en la última carga ("fuera de cartera"); freno de la app: no deja si superan el **30%** o si no hay carga de hoy | Es freno de la app, no del servidor. El sistema también la cierra al llegar el otro tipo (DAT-15) |
| **Incobrable** | CSV (1 a 2 por mes) | Admin, en la pantalla *Reasignar* (gana el CSV) | **Admin**: *Cierres* (Recuperada, Baja o castigada, Fuera de cartera) | Persiste; no se reconcilia. El sistema la cierra si llega como Diaria (DAT-15) |
| **Especial** | App del **Solicitante** (nace **Pendiente**, sin agente) o CSV (nace **Pendiente**) | **Supervisor** al aprobar; después, en la pantalla *Reasignar* (aprobadas activas) | **Admin**: *Cierres* (Gestión completada, Ya no aplica, Solicitud errónea) | Ver estados abajo |

**Estados de una Especial:** `Pendiente` → `Aprobado` (Supervisor, **eligiendo el cobrador**) o `Rechazada` (con motivo) → `Cerrada` (Admin). El Solicitante puede
volver a pedir una `Cerrada`: **reactiva el mismo registro**. **Rechazar cierra la Especial** (estado inactivo, motivo "Rechazada" y fecha de cierre), así que el
Solicitante original puede volver a pedirla (DAT-14, parcial, 2026-10-14). Nueva solicitud muestra un mensaje claro si la cuenta ya tiene una Especial pedida por otra
persona. **Sigue abierto:** un Solicitante no ve la Especial de otro (Read Basic); hace falta una decisión del dueño (Custom API o lectura más amplia).
En una Especial, `Borrador` solo se alcanza por la API (las Diarias e Incobrables nacen en Borrador: DAT-28; las 6 filas que había en DEV se corrigieron); las **47 Especiales en Borrador** de DEV son de la carga mock anterior al cambio del default y no aparecen en la cola de
Aprobaciones (que muestra solo Pendientes). Reglas del servidor: solo Supervisor, Admin o `Servicio - Cobranza` aprueban, rechazan o ponen agente (plugin `AprobacionEspecial`);
el reparto **no comparte** una Especial sin aprobar.

- **Lo que ve el cobrador:** asignaciones **activas** (el perfil offline filtra `as_estado = 1`) y, si es Especial, **aprobada**; baja por el reparto + perfil offline.
- **Lo que cambia el cobrador:** solo `as_orden`, también sin conexión. El servidor lo impone (`ColumnasAsignacion`, S-11).
- **Quién crea:** Smelter; el Solicitante (`C:G`). **Quién cambia:** Supervisor (`W:G`, pero solo aprobación, agente, resolutor, motivo de rechazo, estado, motivo y fecha de cierre: S-11, cerrado 2026-10-14), el Solicitante (solo las columnas de volver a pedir una Especial), Admin y `Servicio - Cobranza`. El agente también se cambia en la pantalla *Reasignar* del backoffice (DAT-29): el Supervisor sobre Especiales aprobadas activas, el Admin además sobre Incobrables activas; solo escribe el agente y `RepartoAsignacion` mueve el dueño y los compartidos.
- **Auditoría:** activa a nivel de tabla (historial de reasignaciones, DEC-016).

### Cómo borrar
- **Entrante:** `cuenta → asignacion` = **Cascade** — borrar la cuenta borra sus asignaciones (y borrar el cliente, por la cuenta). Es **hoja**. El historial de
  reasignaciones **no se pierde**: vive en la auditoría.
- Solo el Admin tiene `Delete`; ninguna app borra asignaciones.

### Consideraciones al borrar
- **CERRAR ≠ BORRAR:** el fin de vida es **`as_estado = No` (Cerrada)** (Reconciliación, Cierres, Rechazar o la reclasificación), no un borrado. Las cerradas **se conservan** como rastro.
  El teléfono las saca en la próxima sincronización (`RepartoAsignacion` revoca el acceso y el filtro del perfil las excluye).
- **Borrado físico** (opcional, muy posterior): bulk-delete de `Cerradas` viejas (más de 1 año), si hiciera falta; no hay job. La tabla crece lento.
- **Owner:** si el agente no tiene `ag_usuarioid`, la asignación pasa al equipo "Cartera sin cobrador" y ningún localizador la ve (DAT-07; vista de control "agentes sin usuario" en el inicio del Admin).
- **Assign:** desde el 2026-10-14 (DAT-18), `cuenta → asignacion` es `NoCascade`: reasignar la cuenta o el cliente ya no pisa el dueño que puso `RepartoAsignacion`.
- Antes del 2026-10-06 `Reparent` estaba en Cascade y el cobrador heredaba lectura de **todas las asignaciones** de sus clientes (defecto 2 / S-14).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas (27), alt key activa y la vieja eliminada, choices con valores y defaults, cascadas (incluido Reparent), mapeo, flow activo,
plugins `AprobacionEspecial` y `ClaveDesdeCuenta`, privilegios. Discrepancias: `as_estado` es Yes/No; autonumérico `AS-{SEQNUM:4}`; columnas nuevas (`clavepan`, `fechacierre`,
`notas`, `retironio/usd`); `as_numerocuenta` eliminada; Smelter mapea `FechaCarga` también a `fechasignacion` y **no** mapea `estado`. No confirmado: el disparador en vivo del flow
(se tomó del script).

---

## 12. `sanic_cm_tbl_actualizaciondatos` — Cambios de contacto pedidos

### Definición
Captura los **nuevos valores de contacto** que el cliente pide actualizar **durante una interacción** (teléfonos, direcciones, correo). Es una **bandeja de cambios
pedidos** que la **oficina transcribe al core** — el cobrador no edita los datos maestros. Tabla **aparte** (no columnas en `interaccion`).

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número | `sanic_cm_ad_numero` | Text(20), autonumérico **`AD-{SEQNUM:4}`** | auto | **Nombre principal.** Lo asigna el servidor al sincronizar. (El catálogo decía `ACT-{SEQNUM:6}`.) |
| Interacción | `sanic_cm_ad_interaccionid` | Lookup → `interaccion` | Sí | El vínculo (1:N, en la práctica 1:1). El plugin exige que la interacción **tenga cuenta**. |
| Teléfono casa | `sanic_cm_ad_telefonocasa` | Text(20) | — | Nuevo número de casa. |
| Teléfono celular | `sanic_cm_ad_telefonocelular` | Text(20) | — | Nuevo celular. |
| Teléfono trabajo | `sanic_cm_ad_telefonotrabajo` | Text(20) | — | Nuevo trabajo → **capture-only**. |
| Dirección casa | `sanic_cm_ad_direccioncasa` | Text(1000) | — | Nueva dirección de casa. |
| Dirección EC | `sanic_cm_ad_direccionec` | Text(1000) | — | Nueva dirección de estado de cuenta. |
| Dirección trabajo | `sanic_cm_ad_direcciontrabajo` | Text(1000) | — | Nueva dirección de trabajo. |
| Correo | `sanic_cm_ad_correoelectronico` | Text(100), formato Email | — | Nuevo correo → **capture-only**. |
| Propietario | `ownerid` | Owner | Sí | = el **localizador** (nivel usuario). |

> **Todos los campos de dato son opcionales:** se completa **solo lo que cambió**. El teléfono no impone `MaxLength` en el control: el rechazo por largo llega
> diferido, en la sincronización (DAT-21); la app actual valida 7 a 20 caracteres.

### Llaves
- **Ninguna.** Se crea en la app (GUID), ligada a una interacción.

### Relaciones
- Entrante `interaccion → actualizaciondatos`: Cascade en las cinco acciones. Es **hoja**.

### Import
- **N/A** — no viene del CSV. La crea la **app de campo** cuando el cobrador marca `actualizainfo` en una interacción.

### Ciclo de vida
- **Nace:** en la **app de campo** (offline), hija de una interacción. Owner = el localizador. El plugin `ActualizacionConInteraccion` rechaza una sin interacción o con una
  interacción sin cuenta.
- **Vive:** el BackOffice ve el "antes / después" (cruza con `telefono` y `direccion` por CIF) y **lo tipea a mano en el core**. **Nadie lo aplica** a los datos maestros de CM:
  se renuevan recién con la próxima carga, si el core los actualizó. **Sin estados ni cierre: termina en el core por decisión del dueño (DAT-19, cerrado 2026-10-14).**
- **Fin:** se va con su interacción; no se borra de rutina.

### Cómo borrar
- **Entrante:** `interaccion → actualizaciondatos` = **Cascade** — se va con su interacción. Es **hoja**.

### Consideraciones al borrar
- **Es una SOLICITUD, no el dato vivo:** no modifica `cliente`/`telefono`/`direccion`.
- Para saber de qué cliente es hay que seguir `actualización → interacción → cuenta → CIF`. Si se borra la cuenta (`RemoveLink`), ese camino se corta.
- Sin privilegio para Solicitante ni Supervisor; lo lee el BackOffice y lo edita el cobrador dueño.
- No hay proceso de aplicación al master, a propósito: la actualización termina en el core del banco y los maestros los renueva la carga (DAT-19, decisión del dueño, 2026-10-14).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas (10), sin alt key, cascada, plugin activo, privilegios. Discrepancia: autonumérico `AD-{SEQNUM:4}`.

---

## 13. `sanic_cm_tbl_cuentapan` — PAN de cuentas (la bóveda)

### Definición
**Tabla nueva de la Fase 2 PAN (2026-10-04).** Guarda el **número de tarjeta completo** (PAN) de cada cuenta, **una fila por cuenta**. Es el **único** lugar del modelo donde vive el
número; el resto de las tablas guarda `****1234` y un HMAC. Se eligió una tabla con seguridad **por fila** (no una columna con seguridad de columna) porque el modo offline
respeta la de fila y porque el nombre principal no puede protegerse por columna. Nombre mostrado: **CM | TBL | PAN de cuenta**.

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Número (enmascarado) | `sanic_cm_cp_name` | Text(850) | — | **Nombre principal.** `****1234`. `PanGuard` rechaza 5 o más dígitos. Inofensivo si se filtra por un lookup. |
| PAN | `sanic_cm_cp_pan` | Text(19) | — | **Número completo.** **Auditoría de columna apagada** y **fuera de búsqueda avanzada**; sin seguridad de columna. **Nunca se imprime ni se copia a otro lado.** |
| Clave PAN (HMAC) | `sanic_cm_cp_clavepan` | Text(64) | — | HMAC-SHA256 hex del número. **Alt key.** |
| Cuenta | `sanic_cm_cp_cuentaid` | Lookup → `cuenta` | Sí | La cuenta dueña del número. **Alt key** (garantiza 1:1). |
| Propietario | `ownerid` | Owner | Sí | En la muestra de DEV: el usuario de aplicación del servicio de despliegue (`# PowerPlatform-Dev`), no `SYSTEM`. |

### Llaves
- **`sanic_cm_ak_cp_cuentaid`** (`cp_cuentaid`): **una bóveda por cuenta.** **Active.**
- **`sanic_cm_ak_cp_clavepan`** (`cp_clavepan`): upsert de la carga. **Active.**

### Relaciones
- Entrante `cuenta → cuentapan`: **Cascade en las cinco acciones** (Delete, Assign, Share, Unshare, Reparent). Es **hoja**.
  El Share en cascada es la **única** vía por la que el cobrador la lee: ve el número solo de las cuentas que el reparto le compartió.

### Import
- **Table Mapping `Cuenta PAN (bóveda)`**, **orden 4** (después de `cuenta`). Dedup `cp_clavepan`. Mapea: `HMAC(NumeroCuenta) → cp_clavepan`, `TRIM(NumeroCuenta) →
  cp_pan` (**único destino del número completo**), `****1234 → cp_name`, y el lookup `cp_cuentaid` calculando el HMAC contra `ct_clavepan`.

### Ciclo de vida
- **Nace:** solo **Smelter**, una por cuenta (backfill inicial con `scripts/pan/paso3_backfill.py`). **Nadie la cambia** (inmutable fuera de la carga).
- **Quién la lee:** **BackOffice** (Global; ve el número completo para transcribir al core) y el **Localizador** (`R:U`, solo lo compartido; botón "Ver número", se oculta a los 30 s, un solo nivel de
  lookup). Solicitante, Supervisor y `Servicio - Cobranza` **no tienen privilegio**; Admin **tampoco** (ver discrepancia).
- **En el teléfono:** sí, dentro del perfil offline (criterio "todas las filas legibles").
- **Fin:** se borra **solo con la cuenta** (Cascade). Nunca se borra suelta.

### Cómo borrar
- **Entrante:** `cuenta → cuentapan` = **Cascade**. Borrar la cuenta (o el cliente) borra la bóveda.

### Consideraciones al borrar
- **Cadena de acceso:** compartir la cuenta comparte la bóveda; revocar la cuenta la revoca. Si `Reparent` o `Share` volvieran a ser Cascade más arriba (cliente → cuenta), se rompería
  el aislamiento por cobrador (S-14).
- **Admin sin privilegios sobre la tabla:** el Admin no puede leerla por rol (el diseño de Fase 2 decía `Read Global`; la guía 16 y la metadata dicen que no). Si el Admin borra una cuenta,
  si el Delete en cascada sobre la bóveda exige un privilegio que no tiene es **no confirmado** (no se probó).
- La **auditoría de tabla** está encendida (Create, Delete, Share y lectura), con la columna `cp_pan` fuera.
- **Rotar la clave K** obliga a recalcular los HMAC de cuenta, bóveda, asignación y carga diaria desde la bóveda (que tiene el número).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas, dos alt keys activas, Cascade completo, mapeo, `PanGuard` activo, auditoría de columna apagada en `cp_pan`, privilegios (R:G solo BackOffice; R:U Localizador).
Discrepancia: Admin sin privilegios. No confirmado: contenido de la bóveda (no se leyó, por diseño).

---

## 14. `sanic_cm_tbl_usuarioapp` — Usuario App (BORRADA el 2026-10-15)

> **Tabla borrada de DEV el 2026-10-15 (DAT-30), con aprobación del dueño, y quitada de `solutions/unpacked`.** Motivo: era huérfana (0 filas, 0 privilegios en todos los roles, 0 dependencias de borrado, ninguna app la usaba). El detalle de abajo queda como registro histórico de lo que era; ya no existe en el entorno ni en la solución.

### Definición
Tabla **sin uso**: 0 filas, 0 referencias en ambas apps, 0 privilegios en todos los roles CM (verificado). El rol de cada persona se deriva de los **roles y equipos** de Dataverse
(`gblRol`), no de esta tabla. Fue **eliminada** (DAT-30).

### Campos
| Campo lógico | Físico | Tipo | Req. | Descripción |
|--------------|--------|------|:----:|-------------|
| Nombre | `sanic_cm_ua_nombre` | Text(200) | — | **Nombre principal.** |
| Usuario | `sanic_cm_ua_usuarioid` | Lookup → `systemuser` | Sí | |
| Rol | `sanic_cm_ua_rol` | Multi-select Choice: **1 Backoffice**, **2 Solicitante**, **3 Supervisor**, **4 Admin** | Sí | |
| Activo | `sanic_cm_ua_activo` | Yes/No (def. **No**) | — | |

### Llaves, import, ciclo de vida, borrado
- **Sin llaves, sin import, sin ciclo de vida.** Sin auditoría de tabla, sin `ChangeTracking`, fuera del perfil offline.
- `systemuser → usuarioapp`: RemoveLink. Nada la referenciaba: se **borró** sin impacto (2026-10-15).

### Estado de la revisión
**2026-10-06.** Verificado en vivo: columnas, 0 filas, 0 privilegios en los 6 roles CM. No confirmado: referencias desde otras soluciones del entorno.

---

## D. Discrepancias entre la documentación y la metadata viva (2026-10-06)

Cuando metadata y documentos discrepan, manda la metadata. Lo que se corrigió en este catálogo:

| # | Qué decía el catálogo / otro doc | Qué dice la metadata en DEV |
|---|----------------------------------|------------------------------|
| 1 | `cuenta → interaccion` = **Restrict** ("confirmado", el candado de DEC-023) | **RemoveLink**; también `cuenta → pago` y `cuenta → cargadiaria`. El candado no existe |
| 2 | `cuenta → cargadiaria` = Cascade | RemoveLink (el borrado de carga diaria llega por `cliente → cargadiaria`) |
| 3 | Alt keys `pk_ct_numerodecuenta`, `altkey_NumeroCuentaxTipo`, `pk_cd_numerocuenta_fechacarga` | Eliminadas; hoy `ak_ct_clavepan`, `ak_as_clavepan_tipo`, `ak_cd_clavepan_fecha`, más `ak_cp_*` y `ak_pg_recibo_cuenta_moneda` |
| 4 | `pago` sin alt key | Alt key `sanic_cm_ak_pg_recibo_cuenta_moneda` activa |
| 5 | Autonumeración `INT-/PAG-/ASG-/ACT-{SEQNUM:6}` | `IN-/PG-/AS-/AD-{SEQNUM:4}` |
| 6 | `as_estado` es Choice (Activa/Cerrada) | Yes/No (Sí = Activa) |
| 7 | `in_estadograbacion`: `Pendiente` / `Grabada en core` | `1 Pendiente` / `2 Grabado` |
| 8 | `as_estadoaprobacion`: `Pendiente` / `Aprobada` | `2 Pendiente aprobación` / `3 Aprobado` |
| 9 | GPS Decimal(5) | Decimal(**6**) |
| 10 | `ct_fechapago` DateTime | **Text(100)**, con formatos mezclados |
| 11 | La cuenta guarda el número; `as_numerocuenta` / `in_numerocuenta` / `pg_numerocuenta` | Número solo en la bóveda; las 3 columnas viejas **no existen** |
| 12 | Relationship Mappings en Smelter | **0**; los lookups van en los column mappings |
| 13 | `as_estado` lo mapea Smelter con default Activa | No lo mapea; nace Activa por el default de la columna |
| 14 | Admin con `Read Global` sobre la bóveda (diseño Fase 2) | Sin ningún privilegio sobre `cuentapan` |
| 15 | README de los plugins: 12 `PanGuard` desactivados, 3 plugins | 15 pasos activos, 6 `PanGuard` (los de columnas viejas se retiraron), 6 plugins |
| 16 | Solicitante crea cliente y cuenta (DEC-013/014) | Solo el Admin crea clientes (Maestros); **nadie crea cuentas** fuera de Smelter |
| 17 | `DAT-18` (contrato de datos): Reparent y Share en Cascade en `cliente → cuenta` y `cuenta → asignacion` | Cortados (Share 2026-10-03, Reparent 2026-10-06); `Assign` de `cuenta → asignacion` y los `Restrict` de borrado, 2026-10-14 |

**No confirmado** en esta revisión: comportamiento real de los borrados en cascada y de `RemoveLink` sobre lookups requeridos (DEV de solo lectura); semillas de los autonuméricos;
disparador en vivo del flow; origen de `ct_tipoproducto` y del volumen de `fiador`; contenido de `sanic_tbl_smlt_hmackey` y de `cp_pan` (no se leen por diseño).
