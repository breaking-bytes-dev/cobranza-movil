# Sanic.CobranzaMovil.Plugins

Ensamblado de plugins de la **Fase 2 PAN/PCI** (diseño: [fase2-pan-diseno.md](../../docs/historico/auditoria/fase2-pan-diseno.md) §2.6 y §4, filas 5 y 8).
.NET Framework 4.6.2, sandbox de Dataverse, firmado con nombre seguro. **Ningún plugin conoce ni necesita la clave K del HMAC.**

## Qué hace cada plugin

Todos son **síncronos, PreOperation**, corren con los permisos del **usuario que llama** y nunca ponen un valor de columna en un mensaje de error ni en una traza (solo el nombre de la columna). Las excepciones son `RepartoAsignacion` y `RepartoPorAgente`: **asíncronos, PostOperation**, con el servicio del sistema (ver abajo).

| Plugin | Mensaje y tabla | Filtro | Qué hace |
|---|---|---|---|
| `PanGuard` | Create y Update de `sanic_cm_tbl_cuenta`, `sanic_cm_tbl_cargadiaria`, `sanic_cm_tbl_cuentapan` | Update: `sanic_cm_ct_numerodecuenta` / `sanic_cm_cd_numerocuenta` / `sanic_cm_cp_name` | Rechaza un valor con **5 o más dígitos seguidos** (tolera un espacio, guion o espacio duro entre dígitos: `4111 1111 1111 1111` también se rechaza). `****1234`, vacío y nulo pasan. |
| `PanGuard` | Create y Update de `sanic_cm_tbl_asignacion`, `sanic_cm_tbl_interaccion`, `sanic_cm_tbl_pago` | Update: `sanic_cm_as_numerocuenta` / `sanic_cm_in_numerocuenta` / `sanic_cm_pg_numerocuenta` | Rechaza **cualquier valor no vacío** en esas columnas denormalizadas viejas (el paso 8d las elimina; el script retira solo esos pasos cuando la columna ya no existe). |
| `ClaveDesdeCuenta` | Create y Update de `sanic_cm_tbl_asignacion` | Update: `sanic_cm_as_cuentaid`, `sanic_cm_as_clavepan` (+ imagen previa `PreImage` con `sanic_cm_as_cuentaid`) | Si hay `as_cuentaid`: lee `sanic_cm_ct_clavepan` de la cuenta (un solo `Retrieve`, una sola columna; **1.0.9.6:** no lee si escribe SYSTEM con la cuenta referenciada por su clave alternativa: usa ese valor); sin `as_clavepan` en el Target la completa; si es distinta, rechaza. Si `as_cuentaid` se borra, no toca nada. Si la cuenta aún no tiene clave, no bloquea. |
| `CuentaDesdeInteraccion` | Create de `sanic_cm_tbl_pago` | — | Rechaza un pago sin interacción (DAT-13). Si `sanic_cm_pg_cuentaid` viene vacío y hay `sanic_cm_pg_interaccion`, lo completa con `sanic_cm_in_cuentaid` de la interacción (un solo `Retrieve`, una columna). Hace falta porque el modo offline solo admite un nivel de lookup. |
| `AprobacionEspecial` | Create y Update de `sanic_cm_tbl_asignacion` | Update: `sanic_cm_as_agenteid`, `sanic_cm_as_estadoaprobacion`, `sanic_cm_as_tipo` (+ `PreImage`) | S-6. Sobre una Especial, solo Supervisor, Admin o Servicio aprueban, rechazan, ponen o cambian el agente, o la convierten en Especial con agente. S-7: en el alta, una persona de CM solo puede poner "Solicitado por" a sí misma. Lee los roles directos y por equipo. Sin roles de CM (la carga) no se frena. Además (DAT-28), en el Create una Diaria o Incobrable sin estado de aprobación (o en Pendiente) nace en **Borrador**. **H2 (1.0.8.4):** crear una Diaria o Incobrable, o convertir una asignación en una, solo lo hace la carga (SYSTEM: Smelter, también el alta de `AltaCuenta`) o el Servicio; cualquier persona con roles de CM, Admin incluido, recibe un error. |
| `ActualizacionConInteraccion` | Create y Update de `sanic_cm_tbl_actualizaciondatos` | Update: `sanic_cm_ad_interaccionid` | APP-24. Una actualización de datos cuelga de una interacción con cuenta. **F7 (1.0.8.6/1.0.8.8):** en el teléfono (offline) llega sin interacción (y sin fecha de captura); con `sanic_cm_ad_cuentaid` (y `sanic_cm_ad_fechacaptura` si llega) la vincula a la interacción del mismo cobrador (`createdby`) y cuenta con actualiza info: la de esa captura (±2 s), o sin fecha la más vieja de las últimas 24 h sin actualización. **F8 (1.0.9.0):** si no la encuentra (o es ambigua) **no rechaza** —el teléfono descarta lo rechazado y no lo reintenta—: la acepta con `sanic_cm_ad_sinvincular` = Sí. Con interacción, completa la cuenta si falta, rechaza si es otra o no tiene cuenta, y deja la marca en No; un Update que quita la interacción se rechaza. |
| `EmailDesdeUsuario` | Create y Update de `sanic_cm_tbl_agente` | Update: `sanic_cm_ag_usuarioid` | DAT-17. El correo del agente (`ag_emailusuario`, con el que la app del cobrador encuentra su agente) es siempre el UPN en minúsculas del usuario vinculado; desvincular lo vacía. Única fuente: el backoffice solo escribe el lookup. |
| `ColumnasAsignacion` | Update de `sanic_cm_tbl_asignacion` | — (todas) | S-11. Cada rol de CM escribe solo las columnas de la asignación que usa su pantalla: Localizador el orden; Supervisor aprobación, agente, resolutor, motivos, estado y fecha de cierre. Cualquier otro rol de CM, ninguna. Sin roles de CM, Admin y Servicio no se frenan (**1.0.9.6:** a SYSTEM, la carga, no se le consultan los roles). Solo mira columnas `sanic_cm_as_*` (el dueño cambia por Assign). **H1 (1.0.8.4):** el Solicitante no escribe ninguna columna (pide y vuelve a pedir con `SolicitarEspecial`). |
| `CargaSobreAsignacion` | Update de `sanic_cm_tbl_asignacion` | `sanic_cm_as_ultimacarga` (+ `PreImage` de estado, motivo, tipo y fecha de asignación) | Lo que hace la carga al volver a traer una asignación. DAT-10: una Diaria o Incobrable cerrada **por el sistema** ("fuera de cartera" o "Reclasificada a …") se reabre. DAT-31: no pisa la fecha de asignación si ya tenía una. No consulta nada. |
| `Reclasificacion` | Create y Update de `sanic_cm_tbl_asignacion`, **síncrono, PostOperation** | Update: `sanic_cm_as_ultimacarga`, `sanic_cm_as_estado`, `sanic_cm_as_tipo` (+ `PreImage` de tipo, estado y cuenta) | DAT-15 (DEC-021). Una sola activa entre Diaria e Incobrable por cuenta: si la cuenta llega con el otro tipo, cierra la anterior con motivo "Reclasificada a {tipo}" en la misma transacción. Las Especiales no se tocan. |
| `InteraccionDelCobrador` | Create de `sanic_cm_tbl_interaccion` | — | DAT-13 + SEC-08. Exige cuenta y agente. Si la crea un cobrador, el agente es el vinculado a su usuario (lo pone el servidor) y la interacción nace Pendiente de grabar, sin código, fecha ni usuario de grabación. **H3 (1.0.8.4):** el cobrador solo registra en cuentas de su cartera (una asignación activa de la cuenta cuyo dueño es su usuario); el pago hereda la cuenta de la interacción. **1.0.8.5:** tolerancia por fecha: si la cuenta ya no es suya pero la interacción trae fecha de captura (la del teléfono) de hasta 48 h atrás y no más de 5 min adelantada, entra si a esa fecha tenía una asignación activa suya, según la auditoría de dueño y estado (`RetrieveAttributeChangeHistory`, servicio del sistema); sin fecha, fuera de rango o sin poder leer la auditoría rige la regla estricta. |
| `VincularActualizacionPendiente` | Create de `sanic_cm_tbl_interaccion`, **asíncrono, PostOperation** | — | F8 (1.0.9.0). Si la interacción que llega tiene actualiza info y cuenta, cuelga de ella la actualización **sin vincular** más vieja (últimas 24 h) de esa cuenta y del mismo cobrador, y le quita la marca. Servicio del sistema. Asíncrono a propósito: si fallara síncrono se perdería la interacción del teléfono. |
| `ReciboRepetido` | Create de `sanic_cm_tbl_pago` | — | F6 (1.0.9.0). Si el número de recibo ya existe en otro pago (servicio del sistema: el cobrador no ve los de los demás), marca `sanic_cm_pg_reciborepetido` = Sí. **No rechaza** (F8): BackOffice lo revisa al transcribir. Nunca traza el número. |
| `EspecialDesdeCuenta` | Create de `sanic_cm_tbl_asignacion`; Update filtrado por `sanic_cm_as_ultimacarga` (PreImage) | Update: `sanic_cm_as_ultimacarga` | F3 (1.0.9.1). La carga escribe tipo, nota y retiro de la Especial en la **cuenta** y la app los lee de la **asignación**. En una Especial **sin solicitante** copia de la cuenta solo lo **vacío**: notas = "Tipo — nota", retiro NIO y USD. Nunca pisa lo que escribió una persona. Servicio del sistema; nunca traza valores. |
| `IdentidadEnInteraccion` | Create de `sanic_cm_tbl_interaccion` (PreOperation) | — | Borrado por salida de cartera (1.0.9.2). La cuenta y el cliente se borran 14 días después de dejar de venir en la carga, y la interacción vive 2 años: al crearse guarda los **últimos 4**, el **CIF** y la **clave HMAC** de su cuenta para identificarla en el core. Solo lo vacío; **nunca rechaza** (F8). Servicio del sistema; nunca traza valores. |
| `TramoCartera` | Create de `sanic_cm_tbl_asignacion` y de `sanic_cm_tbl_pago` (PreOperation); Create de `sanic_cm_tbl_cuenta` (PostOperation **asíncrono**) | — | Escala de la cartera (1.0.9.3). Tramo **0..19** (`sanic_cm_as_tramo`) calculado de la **cuenta**: estable y parejo. La app trae la cartera por tramos, cada uno bajo el límite de 2000 filas (hasta 40.000 asignaciones por cobrador). La **cuenta** recibe el mismo tramo (`sanic_cm_ct_tramo`, de su propio id) para que la app traiga también las cuentas por tramos (1.0.9.4). El **pago** recibe el tramo de su interacción (`sanic_cm_pg_tramo`, Create de `sanic_cm_tbl_pago` PreOperation): Transcritas trae los pagos del día por tramos (1.0.9.5). Solo lo vacío; nunca rechaza; no consulta. |
| `GrabacionInteraccion` | Update de `sanic_cm_tbl_interaccion` | — (todas) (+ `PreImage` de estado y código de grabación) | DAT-11. Una interacción Grabada no se vuelve a grabar ni cambia su código (dos personas del BackOffice ya no se pisan); pasar a Grabado exige el código; la fecha y el usuario de grabación los pone el servidor. El BackOffice escribe solo las columnas de grabación. |
| `RepartoAsignacion` | Create y Update de `sanic_cm_tbl_asignacion`, **asíncrono, PostOperation** | Update: `sanic_cm_as_agenteid`, `sanic_cm_as_estado`, `sanic_cm_as_estadoaprobacion` | Reemplaza al flow "Cambio de Owner en Asignaciones". Lee el estado **actual** de la asignación. Si está activa y no es una Especial sin aprobar (S-6): pasa el dueño al usuario del agente, le comparte en lectura cuenta, cliente y fiador y le revoca al dueño anterior lo que ya no justifique otra asignación activa suya. Sin agente o con un agente sin usuario, el dueño pasa al equipo **"CM · Cartera sin cobrador · &lt;entorno&gt;"** (sin miembros, DAT-07) y no comparte nada. La Especial aprobada se le comparte a **"Solicitado por"** (S-7), solo en lectura desde 1.0.8.4 (H1; `ModifyAccess` baja a lectura los compartidos anteriores). Desde 1.0.8.2 ya no se exige que sea quien la creó: una persona de CM solo puede ponerse a sí misma en esa columna (`AprobacionEspecial` en el alta, `ColumnasAsignacion` en la edición), así que quien reactiva con `SolicitarEspecial` una Especial creada por otro la sigue viendo al aprobarse. Si está cerrada, revoca al dueño actual. Cada paso corre aunque falle otro; el compartido concurrente se reintenta con espera y, si algo queda sin hacer, la ejecución termina en error (con reintento del servicio asíncrono si es transitorio). Corre con el servicio del **sistema** (dar y quitar acceso no lo tiene ningún rol de persona). |
| `RepartoPorAgente` | Update de `sanic_cm_tbl_agente`, **asíncrono, PostOperation** | `sanic_cm_ag_usuarioid` | DAT-08. Al vincular, cambiar o quitar el usuario del agente, busca sus asignaciones activas cuyo dueño no es el que corresponde (el usuario o el equipo Cartera sin cobrador), sin las Especiales por aprobar, y les vuelve a escribir el **mismo** agente: eso dispara `RepartoAsignacion` en cada una. Hasta 1000 por ejecución; si quedan más o alguna falla, pide reintento. |
| `SolicitarEspecial` | **Custom API** global `sanic_cm_SolicitarEspecial` (acción, sin entidad, etapa principal 30) | — | DAT-14 (a). Reemplaza el Patch de scrNuevaSolicitud. Entrada: `CuentaId` (Guid), `Notas` (texto), `RetiroNio` y `RetiroUsd` (decimal, opcionales). Salida: `AsignacionId`, `Resultado` ("creada", "reactivada" o "ya existe"), `SolicitadoPor` (nombre, si ya existe) y `EstadoAprobacion`. Valida al llamador (rol Solicitante, Admin o Servicio, o sin roles de CM; y que pueda leer la cuenta con sus permisos) y rechaza notas con 13 o más dígitos seguidos. Ve las Especiales de la cuenta con el servicio del sistema: si hay una activa no la toca y avisa quién la pidió; si hay una cerrada la reactiva (servicio del sistema, dueño = llamador) con las mismas columnas que la app y, desde 1.0.8.3, le quita el compartido de la asignación a quien la había pedido antes (S-7 b1; si falla, la reactivación sigue y se traza el código); si no hay, la crea **con los permisos del llamador** (dueño y `createdby` = Solicitante, para S-7). Se registra con `scripts/pan/paso12_custom_api.py`. |
| `AltaCuenta` | **Custom API** global `sanic_cm_AltaCuenta` (acción, sin entidad, etapa principal 30) | — | DAT-20 (camino A). El Admin da de alta una cuenta de cualquier tipo (Diaria, Incobrable o Especial) como una importación de **una fila** con la plantilla "Cobranza movil" de Smelter: el HMAC, la bóveda y el enmascarado los hace Smelter, K sigue en un solo lugar y ninguna tabla de CM guarda el PAN. Entrada: las columnas del archivo del banco (texto, decimal o entero; fechas como texto `aaaa-mm-dd`), `ProductoId` y `AgenteId` (Guid), `TipoAsignacion` y `EsPrueba` (prueba en seco). Salida: `JobId` y `Advertencias`. Solo Admin, Servicio o sin roles de CM. Valida todo antes de crear nada (13 a 19 dígitos y Luhn, agente activo, producto, sin comillas ni saltos de línea, ningún campo libre con 13 dígitos o más) y evita las trampas del upsert de la plantilla: **fecha de carga** = la más nueva de la carga diaria (si no hay, la "última carga" más vieja de las Diarias activas; si tampoco, hoy UTC), nunca la de hoy, para no mover el corte de la reconciliación; **agente y producto** con el nombre y el código de Dataverse; **cliente existente**: nombre, direcciones, teléfonos, departamento, distrito y trabajo los que ya tiene (y la dirección de un fiador existente); **cuenta existente**: rechaza si el cliente ya tiene una con esos últimos 4 y avisa si otro cliente la tiene. Crea el Import Job **con los permisos del llamador** y sube el CSV (encabezado exacto + una fila, RFC 4180, UTF-8 sin BOM) con `InitializeFileBlocksUpload`/`UploadBlock`/`CommitFileBlocksUpload`. No pone el estado: la app lo pone En cola después de la espera, como "Cargar archivo". Se registra con `scripts/pan/paso12_custom_api.py`. |
| `PartirCarga` | **Custom API** global `sanic_cm_PartirCarga` (acción, sin entidad, etapa principal 30) | — | Carga paralela (1.0.9.7). La carga real de 6.000 filas tardaba ~100 min en UN job (lotes de 100 filas de ~100 s, en serie). Entrada: `JobId` (el Import Job de la carga real que creó "Cargar archivo": plantilla "Cobranza movil", **sin estado**, no prueba en seco, con archivo) y `Partes` (1 a 4; con menos de 300 filas se hace una). Convierte ese job en el de **padres**: archivo con la **última** fila de cada producto, fiador y agente, en orden original (para cada registro la última escritura es su última aparición: igual que en serie), plantilla "Cobranza movil · padres" y nombre terminado en " · padres". Crea N jobs de **partes** sin estado (plantilla "Cobranza movil · partes", "&lt;nombre&gt; · parte i/N") con todas las filas: las que escriben un mismo registro de cualquier tabla de partes (mismo CIF, misma cuenta…) van juntas (union-find sobre las claves de dedup, normalizadas de más). Las columnas salen de la plantilla principal (delimitador, dedup y su columna fuente; el CIF es la que mapea a `sanic_cm_cl_cif`) y exige que las plantillas derivadas sean copia exacta (si no, pide correr `paso20`). El CSV se parte a nivel de bytes (`ArchivoCsv`: comillas, comas y saltos dentro de comillas, CRLF/LF/CR, BOM) y cada fila se copia tal cual. Salida: `PadresJobId`, `PartesJobIds`, `Partes`, `FilasArchivo`, `FilasPadres`, `FilasPartes` (solo conteos). Solo Admin, Servicio o sin roles de CM (como `AltaCuenta`), y el llamador tiene que ver el job y las plantillas (equipo de carga): los jobs se crean y cambian **con sus permisos** (dueño = equipo, "Creado por" = la persona). Atómica: primero las partes, después el archivo del original y al final su nombre y plantilla; si algo falla borra las partes (servicio del sistema: el rol Operador de Smelter no tiene Delete) y devuelve el archivo original. Lee y sube en bloques de 4 MB; tope 50 MB. Nunca traza valores. Se registra con `scripts/pan/paso12_custom_api.py`. |

Un paso mal registrado (otra tabla, otra etapa, otro mensaje) **falla cerrado** con un error claro en vez de ignorar el control en silencio.

## Regla de activación de PanGuard (paso 8)

Hasta el paso 8 la carga de Smelter **todavía escribe el PAN** en `ct_numerodecuenta`, `cd_numerocuenta`, `cp_name` y `as_numerocuenta`. Activar `PanGuard` antes rompe la carga diaria. Por eso:

- Los 12 pasos `PanGuard` se **crean desactivados** (`statecode = 1`) y conservan su estado en cada re-ejecución.
- Se activan **solo en el paso 8**, después de cambiar la plantilla de Smelter para que enmascare, con `activar_panguard = true`.
- `desactivar_panguard = true` es el rollback.
- `ClaveDesdeCuenta` y `CuentaDesdeInteraccion` quedan **activos** desde el registro (paso 5).
- Después de la activación, la prueba negativa del plan: crear una cuenta con 13 dígitos en el nombre principal debe devolver error.

## Por qué `RepartoAsignacion` es asíncrono (y nunca debe ser síncrono)

Smelter escribe las filas del CSV con la Custom API `processchunk`: un solo mensaje por trozo de **100 filas** (la plantilla dice 50, pero Smelter fuerza un mínimo de 100). Un plugin síncrono sobre la asignación correría **dentro** de ese mensaje, y Dataverse le da al mensaje entero **2 minutos**: si se pasa, deshace el trozo completo. Medido en DEV (2026-10-12): unos 80 s por trozo de 100 filas sin el reparto; el reparto suma de 8 a 12 operaciones por asignación. Asíncrono, cada asignación es un trabajo del sistema aparte con sus propios 2 minutos. El plugin **falla cerrado** si se lo registra síncrono.

Los trabajos fallidos quedan en `asyncoperations` (los exitosos se borran solos) y se leen con el Service Principal:
`asyncoperations?$filter=operationtype eq 1 and statuscode eq 31 and startswith(name,'Sanic.CobranzaMovil.Plugins.Reparto')`.

Ambos necesitan el equipo "CM · Cartera sin cobrador · &lt;entorno&gt;" y su rol mínimo (`scripts/seguridad/paso5_cartera_sin_cobrador.py`);
sin él, la asignación sin cobrador falla sin tocar nada (sin reintento).

## Registro (qué crea el workflow)

Versión 1.0.6.0: **24 pasos activos** más los 12 de `PanGuard`, que siguen **desactivados** hasta el paso 8 de la Fase 2 (ver arriba).
Los activos son síncronos PreOperation (20), salvo `Reclasificacion` (síncrono PostOperation, 40) y `RepartoAsignacion` /
`RepartoPorAgente` (asíncronos PostOperation). Todos rank 1, despliegue "solo servidor", ejecutar como el usuario que llama. Nombre de
cada paso: `<Tipo>: <Mensaje> of <tabla>` (ese nombre es la clave de idempotencia). El ensamblado y los pasos se agregan a la
solución `cm_core_cobranzamovil` (componentes 91 y 92). Los roles los leen `RolesCm` (directos y por equipo, con el servicio del
sistema): lo usan `AprobacionEspecial`, `ColumnasAsignacion`, `GrabacionInteraccion`, `InteraccionDelCobrador`, `SolicitarEspecial`, `AltaCuenta` y `PartirCarga`.

Versión 1.0.7.0: suma el plugin de la Custom API `SolicitarEspecial`. No agrega pasos: la API se define (con sus parámetros) con `scripts/pan/paso12_custom_api.py --aplicar`, después de registrar el ensamblado con `paso5_plugins.py`.

Versión 1.0.8.0: suma el plugin de la Custom API `AltaCuenta` (DAT-20). Tampoco agrega pasos: `paso12_custom_api.py --aplicar` define las dos APIs.

Versión 1.0.8.3: `SolicitarEspecial` le quita el compartido de la asignación al solicitante anterior al reactivar (S-7 b1). Sin pasos nuevos.

Versión 1.0.8.4: huecos H1, H2 y H3 de la matriz de roles (`docs/historico/auditoria/matriz-roles.md`). Sin pasos nuevos.

Versión 1.0.8.7: `ActualizacionConInteraccion` traza los NOMBRES (nunca los valores) de las columnas que recibe, para diagnosticar qué descarta el runtime offline del teléfono (F7).

Versión 1.0.8.8: la traza mostró que el teléfono manda `sanic_cm_ad_cuentaid` pero descarta `sanic_cm_ad_fechacaptura` y `sanic_cm_ad_interaccionid`. Sin fecha, `ActualizacionConInteraccion` vincula la actualización a la interacción más vieja (últimas 24 h) del mismo cobrador y cuenta, con actualiza info y sin actualización colgada (F7, B2). Con fecha, sigue buscando por captura.

Versión 1.0.9.0 (F8 y F6): lo que viene del teléfono ya no se rechaza, se marca. `ActualizacionConInteraccion` acepta la actualización sin interacción con `sanic_cm_ad_sinvincular`; `VincularActualizacionPendiente` (nuevo, asíncrono) la cuelga cuando llega la interacción; `ReciboRepetido` (nuevo) marca `sanic_cm_pg_reciborepetido`. Columnas: `scripts/pan/paso15_marcas_offline.py`, antes de paso5. Pasos: 26 (+2 implementaciones de Custom API).

Versión 1.0.9.1 (F3): `EspecialDesdeCuenta` (nuevo) completa la Especial de la carga con el tipo, la nota y el retiro de la cuenta. Pasos: 28 (+2 implementaciones de Custom API).

Versión 1.0.9.2 (borrado por salida de cartera): `IdentidadEnInteraccion` (nuevo) guarda en la interacción los últimos 4, el CIF y la clave HMAC de su cuenta. Pasos: 29 (+2 implementaciones de Custom API).

Versión 1.0.9.3 (escala de la cartera): `TramoCartera` (nuevo) pone el tramo de la asignación. Pasos: 30 (+2 implementaciones de Custom API).

Versión 1.0.9.4: `TramoCartera` también pone el tramo de la cuenta nueva (asíncrono). Pasos: 31 (+2 implementaciones de Custom API).

Versión 1.0.9.5: `TramoCartera` también pone el tramo del pago (sale de su interacción). Columna: `paso18_tramo_cartera.py`, antes de paso5. Pasos: 32 (+2 implementaciones de Custom API).

Versión 1.0.9.6 (costo de la carga diaria, sin cambiar reglas): la carga de Smelter escribe como **SYSTEM** (R28), que no tiene roles de CM. `AprobacionEspecial` y `ColumnasAsignacion` ya no le consultan los roles: eran dos `RetrieveMultiple` por fila (en `ColumnasAsignacion`, en **cada** Update de la carga; en `AprobacionEspecial`, en cada Diaria o Incobrable nueva) que siempre devolvían "sin roles de CM". SYSTEM se reconoce con un `WhoAmI` del servicio del sistema (`CreateOrganizationService(null)` es SYSTEM según la documentación), **una vez por organización** y proceso del sandbox (`UsuarioSistema`); si no responde, se consultan los roles como antes. A las personas se les consultan como siempre: S-6, S-7, S-11, H1 y H2 no cambian. `ClaveDesdeCuenta`: cuando escribe SYSTEM y la cuenta llega por su clave alternativa `sanic_cm_ct_clavepan` (R26) ya resuelta a un Id, usa el valor de la referencia en lugar de releer la cuenta (la clave es única: es la de la cuenta resuelta); a cualquier otro llamador con Id se le lee la cuenta como siempre. Sin cambios de pasos, filtros ni imágenes: solo actualizar el ensamblado. Pasos: 32 (+2 implementaciones de Custom API).

Versión 1.0.9.9: `PartirCarga` suma el parámetro opcional `SoloValidar` (booleano). Con `true` solo mide el largo de los valores (misma regla que 1.0.9.8, mapeos de la plantilla principal) y no escribe nada; acepta la prueba en seco, no pide `Partes`, ni las plantillas derivadas, ni el CIF. "Cargar archivo" lo llama antes de poner en cola la prueba en seco y la carga en serie. Sin pasos nuevos; `paso12_custom_api.py --aplicar` registra el parámetro.

Versión 1.0.9.8: `PartirCarga` rechaza el archivo, antes de crear o cambiar nada, si algún valor es más largo que su columna de texto (MaxLength de la metadata de Dataverse; una consulta por tabla; solo columnas con fuente y sin transformación). La prueba en seco de Smelter no lo detecta y la carga real fallaba en los padres. El error cita fila, columna y largos (hasta 5 casos), nunca el valor. Sin pasos nuevos.

Versión 1.0.9.7 (carga paralela): suma el plugin de la Custom API `PartirCarga` y la lógica pura `ArchivoCsv` y `ParticionCarga`. Orden de despliegue: `scripts/pan/paso20_plantillas_paralelas.py --aplicar` (plantillas de Smelter "· padres" y "· partes"), `paso5_plugins.py` con la dll 1.0.9.7 y `paso12_custom_api.py --aplicar` (define la tercera API). Sin pasos nuevos. Pasos: 32 (+3 implementaciones de Custom API).

Versión 1.0.8.6: `ActualizacionConInteraccion` vincula en el servidor la actualización de datos que el teléfono sube sin interacción (F7, opción B del dueño). Metadatos nuevos: `sanic_cm_ad_cuentaid` y `sanic_cm_ad_fechacaptura` (`scripts/pan/paso14_actualizacion_offline.py`). Sin pasos nuevos.

Versión 1.0.8.5: `InteraccionDelCobrador` acepta la visita offline de una cuenta que le reasignaron al cobrador mientras no tenía señal (tolerancia por fecha de captura, opción 2 del dueño). Sin pasos nuevos ni metadatos: usa la auditoría de `ownerid` y de `sanic_cm_as_estado`, que ya están activas.

## Cómo correr el workflow

Actions → **pan-plugin-cm** → Run workflow.

| Input | Efecto |
|---|---|
| `registrar = false` (defecto) | Compila y corre las pruebas. **No toca Dataverse.** |
| `registrar = true` | Además compila firmado y registra/actualiza ensamblado y pasos en DEV. Re-ejecutable: los pasos se buscan por nombre y se actualizan, nunca se duplican. |
| `activar_panguard = true` | Activa los pasos PanGuard (**solo paso 8**). |
| `desactivar_panguard = true` | Los desactiva. Excluyente con el anterior. |

Secrets: los de siempre (`DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET`, `PP_TENANT_ID`) y, **solo para registrar**, `PLUGIN_SNK_BASE64`.

El Service Principal necesita permisos de escritura sobre `pluginassembly`, `plugintype`, `sdkmessageprocessingstep` y su imagen, y poder agregar componentes a la solución (rol System Customizer o System Administrator en DEV).

Localmente (plan, solo lectura, con `pp_secrets.env`): `python3 scripts/pan/paso5_plugins.py --dll <dll> --version 1.0.0.0 --public-key-token <16 hex>`; con `--aplicar` registra.

### Clave de nombre seguro (una sola vez)

Dataverse **rechaza actualizar** un ensamblado con otra clave: hay que usar siempre el mismo `.snk`. No se versiona (`*.snk` está en `.gitignore`) y tampoco se genera en cada corrida.

```powershell
# En Windows con el SDK de .NET Framework (Developer PowerShell):
sn -k 2048 Sanic.CobranzaMovil.Plugins.snk
[Convert]::ToBase64String([IO.File]::ReadAllBytes("Sanic.CobranzaMovil.Plugins.snk")) | gh secret set PLUGIN_SNK_BASE64
```

Guardar el `.snk` en el gestor de secretos del equipo (un respaldo): si se pierde, hay que desregistrar y volver a registrar el ensamblado.

## Pruebas

`plugins/Sanic.CobranzaMovil.Plugins.Tests` (xunit + Moq, net462, corren en un runner Windows). Cubren: cada rechazo (y que el mensaje y las trazas no contienen el valor), enmascarados `****1234` permitidos, vacíos y nulos, Update sin la columna (no-op), `ClaveDesdeCuenta` (completa / rechaza / sin cambios / cuenta borrada / PreImage), `CuentaDesdeInteraccion` (completa / ya trae cuenta / sin interacción) y que se lee una sola columna con el usuario que llama.

Se usó **Moq y no FakeXrmEasy** (la skill de pruebas del equipo propone FakeXrmEasy v3): v3 exige licencia comercial y una clave en CI que no está provista, y aquí hace falta afirmar *qué* columnas se consultan, cosa que Moq permite directamente. Si se prefiere FakeXrmEasy, la lógica está aislada y las pruebas se portan sin tocar los plugins.

## Estructura

```
plugins/
├── Sanic.CobranzaMovil.Plugins/        un archivo por plugin, PanDetector, CarteraSinCobrador, Esquema, PluginBase
└── Sanic.CobranzaMovil.Plugins.Tests/  xunit + Moq
scripts/pan/paso5_plugins.py            registro idempotente por Web API
.github/workflows/pan-plugin-cm.yml     compila, prueba y registra
```
