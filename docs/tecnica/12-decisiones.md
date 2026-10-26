# Decisiones de diseño

Las decisiones que conviene conocer **antes** de cambiar algo en Cobranza Móvil. Cada una dice qué se decidió, por qué, qué alternativa se descartó y qué se rompe si se cambia. Está escrito para desarrolladores y administradores que van a modificar la solución; el funcionamiento general está en [Arquitectura](01-arquitectura.md).

## Cómo usar este documento

Antes de tocar una pieza, buscarla en el [índice](#índice-por-pieza) y leer las decisiones que la afectan. Si un cambio contradice una decisión, primero hay que resolver el «qué se rompe»; no alcanza con que la pantalla funcione.

Las decisiones están agrupadas por área:

1. [Plataforma y offline](#1-plataforma-y-offline)
2. [Lógica en el servidor](#2-lógica-en-el-servidor)
3. [Número de tarjeta (PAN)](#3-número-de-tarjeta-pan)
4. [Acceso y reparto](#4-acceso-y-reparto)
5. [Asignaciones](#5-asignaciones)
6. [Carga diaria](#6-carga-diaria)
7. [Escala](#7-escala)
8. [Ciclo de vida](#8-ciclo-de-vida)

## 1. Plataforma y offline

### 1.1 La app del teléfono es canvas con perfil offline propio

- **Decisión:** la app del localizador es una *canvas app* con un *offline profile* propio («Cobranza Movil - Cobrador»), que baja solo lo que el usuario puede leer, más un filtro: asignaciones activas y 90 días de interacciones, pagos y actualizaciones.
- **Por qué:** el localizador trabaja sin señal. El perfil autogenerado por Power Apps se rearma en cada publicación, puede quedar trabado («failed to publish») y no viaja en la *solution*.
- **Alternativa descartada:** app solo online (deja al localizador sin herramienta en la calle) y perfil autogenerado (sin ALM y con fallas de publicación).
- **Qué se rompe si se cambia:** sin el filtro, el teléfono baja todo el historial legible; con «todas las filas» sin seguridad por fila, baja el padrón. El perfil viaja en la *solution*; sus filtros se revisan como indica [Instalación, paso 3](09-instalacion-y-despliegue.md#3-revisar-lo-importado).

### 1.2 Lo que viene del teléfono no se rechaza: se marca

- **Decisión:** los plugins que procesan lo que crea el teléfono completan o marcan en lugar de rechazar: recibo repetido (`sanic_cm_pg_reciborepetido`), actualización sin vincular (`sanic_cm_ad_sinvincular`), identidad de la cuenta en la interacción, tramo, número de tarjeta en un texto libre (se enmascara, ver [3.4](#34-en-los-textos-libres-del-teléfono-el-pan-se-enmascara-no-se-rechaza)). El pago **no tiene** *alternate key* sobre recibo, cuenta y moneda.
- **Por qué:** el runtime offline descarta en silencio lo que el servidor rechaza y no lo reintenta. Un rechazo es un pago perdido sin aviso. Una *alternate key* se valida antes que los plugins, así que tampoco sirve.
- **Alternativa descartada:** clave alternativa `(número de recibo, cuenta, moneda)` en el pago. No se crea: si un ambiente la tuviera, hay que borrarla de la tabla Pago (make.powerapps.com → **Tables** → Pago → **Keys**).
- **Qué se rompe si se cambia:** cualquier `throw` nuevo en un plugin de Create de interacción, pago o actualización de datos convierte un dato dudoso en un dato perdido.
- **Excepciones vigentes (rechazos deliberados):**

  | Plugin | Qué rechaza | Por qué se acepta perder la captura |
  |---|---|---|
  | [`InteraccionDelCobrador.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/InteraccionDelCobrador.cs) (PreOperation, Create de interacción) | Para cualquier rol: interacción sin cuenta o sin agente. Si la crea un localizador: cuenta fuera de su cartera pasada la tolerancia, usuario sin agente vinculado, usuario con dos agentes (y la app no mandó uno de ellos), agente desactivado | Es una regla de seguridad: nadie registra a nombre de otro ni en cuentas ajenas. Ver [2.3](#23-el-localizador-solo-registra-en-su-cartera). Los pagos de esa visita cuelgan de la interacción rechazada y también se pierden |
  | [`CuentaDesdeInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CuentaDesdeInteraccion.cs) (PreOperation, Create de pago) | Pago sin interacción | Un pago suelto no se puede transcribir ni ubicar en una cuenta; la app siempre crea el pago desde una interacción |
  | [`ActualizacionConInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ActualizacionConInteraccion.cs) (PreOperation, Create y Update de actualización de datos) | Actualización que **trae** interacción, cuando esa interacción no tiene cuenta o es de otra cuenta. En Update, quitarle la interacción | Es un dato inconsistente, no dudoso: no se sabe de qué cliente es. Si llega **sin** interacción no rechaza: la marca `sanic_cm_ad_sinvincular` |

  Además, [`PluginBase.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PluginBase.cs) convierte cualquier error inesperado en un rechazo genérico: un plugin nuevo de Create sobre estas tablas tiene que tolerar datos faltantes (dejar pasar y trazar, como [`IdentidadEnInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/IdentidadEnInteraccion.cs)).

### 1.3 Un solo nivel de lookup en lo que se filtra offline

- **Decisión:** el pago tiene su propio *lookup* a la cuenta (`sanic_cm_pg_cuentaid`) y la actualización de datos también (`sanic_cm_ad_cuentaid`), aunque se podrían deducir de la interacción. La app los manda; si faltan, el servidor los completa desde la interacción.
- **Por qué:** offline, `Filter` y `LookUp` solo resuelven un nivel de lookup; pago → interacción → cuenta no funciona sin señal. Además, offline el lookup a una interacción recién creada llega vacío.
- **Alternativa descartada:** llegar a la cuenta navegando desde la interacción.
- **Qué se rompe si se cambia:** las pantallas de pagos del teléfono dejan de encontrar la cuenta sin señal, y las actualizaciones capturadas offline quedan sin vincular. Ver [`CuentaDesdeInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CuentaDesdeInteraccion.cs) y [`ActualizacionConInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ActualizacionConInteraccion.cs).

### 1.4 El agente se identifica por el UPN en minúsculas

- **Decisión:** el teléfono encuentra su agente con `Lower(User().Email)` contra `sanic_cm_ag_emailusuario`, y ese correo lo deriva siempre el servidor del UPN del usuario vinculado ([`EmailDesdeUsuario.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/EmailDesdeUsuario.cs)). Nadie lo escribe a mano.
- **Por qué:** `User().Email` devuelve el UPN, que puede no coincidir con el correo principal, y offline la comparación la hace la base local del teléfono, sensible a mayúsculas.
- **Alternativa descartada:** correo escrito por el Admin; buscar por id de Entra (la app de la oficina sí lo hace, pero offline la tabla de usuarios no está en el perfil).
- **Qué se rompe si se cambia:** el localizador abre la app y no ve su cartera ni puede registrar. Ver [`App.pa.yaml`](../../apps/localizador-canvas/Src/App.pa.yaml) (`gblAgente`).

## 2. Lógica en el servidor

### 2.1 Las reglas críticas viven en plugins

- **Decisión:** aprobación de Especiales, columnas que puede escribir cada rol, grabación en el core, agente de la interacción, reparto y reconciliación se imponen en plugins y Custom APIs. Las pantallas solo ayudan.
- **Por qué:** el servidor es la única fuente confiable. Las reglas de negocio de formulario no corren en canvas ni en la Web API, y cualquiera con un rol puede escribir por la API sin pasar por la pantalla.
- **Alternativa descartada:** reglas de negocio de formulario y validación en la app; un *flow* de Power Automate para el dueño de la asignación (correría con una identidad personal y su historial de ejecuciones guardaría la fila completa).
- **Qué se rompe si se cambia:** mover una validación a la pantalla la deja sin efecto para la API, Smelter y la otra app. Ver [Plugins y Custom APIs](04-plugins-y-custom-apis.md).

### 2.2 Restricción de columnas por plugin, no por seguridad de columna

- **Decisión:** [`ColumnasAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ColumnasAsignacion.cs) y [`GrabacionInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/GrabacionInteraccion.cs) limitan qué columnas escribe cada rol: el localizador solo el orden de su cartera; el Supervisor aprobación, agente y cierre; el BackOffice solo las columnas de grabación.
- **Por qué:** la seguridad de columna (FLS) también aplicaría a la carga de Smelter, y el modo offline no la respeta.
- **Alternativa descartada:** perfiles de seguridad de columna.
- **Qué se rompe si se cambia:** una pantalla nueva que escriba otra columna de la asignación falla con «tu rol no puede cambiar …» hasta que se agregue a la lista `Permitidas` del plugin.

### 2.3 El localizador solo registra en su cartera

- **Decisión:** una interacción de un localizador entra solo si la cuenta tiene una asignación activa cuyo dueño es su usuario, o si lo era a la fecha de captura (hasta 48 h atrás y no más de 5 min adelantada), según la auditoría de dueño y estado de la asignación (si la auditoría no se puede leer, rige la regla estricta). El servidor pone el agente vinculado al usuario (con dos agentes vinculados, vale el que mandó la app si es uno de ellos) y rechaza si el usuario no tiene agente o si el agente está desactivado. También fuerza el estado de grabación a Pendiente y descarta código, fecha y usuario de grabación. Aplica a quien tiene el rol Localizador sin Admin ni Servicio ([`InteraccionDelCobrador.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/InteraccionDelCobrador.cs)).
- **Por qué:** el rol Localizador tiene `AppendTo` global sobre la cuenta (lo exige el lookup), así que ver la cuenta no prueba que sea suya. La tolerancia cubre la cuenta reasignada mientras el teléfono estaba sin señal.
- **Alternativa descartada:** confiar en el agente que manda la app.
- **Qué se rompe si se cambia:** apagar la auditoría de `ownerid` y `sanic_cm_as_estado` en la asignación anula la tolerancia y las capturas offline de cuentas reasignadas se pierden. Este rechazo es una excepción deliberada a [1.2](#12-lo-que-viene-del-teléfono-no-se-rechaza-se-marca).

### 2.4 Sin roles de CM, sin restricción

- **Decisión:** los plugins leen los roles `SR | CM | ` del llamador, directos y por equipo ([`RolesCm.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RolesCm.cs)). Quien no tiene ninguno se trata como SYSTEM (la carga) o administrador del entorno y no se frena.
- **Por qué:** Smelter escribe como SYSTEM, sin roles, y no se puede frenar la carga. Para no consultar roles en cada fila, SYSTEM se reconoce una vez por organización ([`UsuarioSistema.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/UsuarioSistema.cs)).
- **Alternativa descartada:** una lista de identidades permitidas.
- **Qué se rompe si se cambia:** renombrar los roles o quitar el prefijo `SR | CM | ` deja a todas las personas «sin restricción». La app de la oficina, además, arma su menú con el nombre del rol sin el prefijo (`sanic_cm_MisRoles`, [`MisRoles.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/MisRoles.cs)).

### 2.5 La interacción grabada es terminal y la controla el servidor

- **Decisión:** una interacción **grabada** no se vuelve a grabar ni cambia su código; pasar a grabada exige el código del core; la fecha y el usuario de grabación los pone el servidor ([`GrabacionInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/GrabacionInteraccion.cs)). El localizador crea interacciones, pagos y actualizaciones pero no los edita: el rol `SR | CM | Localizador` no tiene Write sobre esas tablas (viaja con el rol en la *solution*; ver la [matriz de privilegios](03-seguridad.md#matriz-de-privilegios-por-tabla)).
- **Por qué:** sin esto, dos personas del BackOffice con la misma interacción abierta se pisarían el código; y un localizador con permiso de escritura podría bajar el monto de un pago ya grabado.
- **Alternativa descartada:** controlarlo en la pantalla de transcripción.
- **Qué se rompe si se cambia:** devolverle `Write` al localizador abre la edición de pagos ya transcritos. **Límite conocido:** dos grabaciones en el mismo milisegundo pueden pasar las dos.

## 3. Número de tarjeta (PAN)

### 3.1 El PAN vive en una bóveda con seguridad por fila

- **Decisión:** el número completo solo está en `sanic_cm_tbl_cuentapan` («CM | TBL | PAN de cuenta»), 1:1 con la cuenta, sin seguridad de columna. El localizador la ve por el compartido en *cascade* desde la cuenta; el BackOffice la lee entera; Admin, Solicitante y Supervisor no tienen privilegios, y ningún rol la crea ni la edita: la escribe solo la carga. Los privilegios viajan con los roles; después de importar se revisan como indica [Instalación, paso 4.2](09-instalacion-y-despliegue.md#42-revisar-los-privilegios-de-la-bóveda).
- **Por qué:** tres límites de la plataforma: el nombre principal no admite seguridad de columna; una *alternate key* no puede incluir una columna protegida; el modo offline no respeta la seguridad de columna. La seguridad por fila sí funciona offline.
- **Alternativa descartada:** una columna con seguridad de columna en la cuenta.
- **Qué se rompe si se cambia:** cambiar a «sin cascada» la relación cuenta → bóveda deja al localizador sin «Ver número»; darle lectura de la bóveda a otro rol amplía el alcance PCI. Ver [Seguridad](03-seguridad.md).

### 3.2 Clave sustituta HMAC, calculada por Smelter

- **Decisión:** las uniones y los upserts usan `HMAC-SHA256(K, PAN sin espacios ni guiones)` en hexadecimal (`sanic_cm_ct_clavepan`, `sanic_cm_as_clavepan`, `sanic_cm_cd_clavepan`, `sanic_cm_cp_clavepan`). Lo calcula la transformación `HMAC(NumeroCuenta, "cm-pan")` de la plantilla de Smelter ([Instalación, paso 8](09-instalacion-y-despliegue.md#8-plantillas-de-smelter)). Ningún plugin conoce K; [`ClaveDesdeCuenta.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ClaveDesdeCuenta.cs) solo copia la clave de la cuenta a la asignación.
- **Por qué:** la carga trae solo el PAN y tiene que encontrar la cuenta existente: hace falta una función determinística. Un hash sin clave se revierte por fuerza bruta (un PAN de 13 dígitos con BIN conocido tiene unas 10⁹ combinaciones). No se pueden registrar plugins en `Upsert` de tablas propias, así que la clave tiene que salir de la propia fila.
- **Alternativa descartada:** SHA-256 sin clave, GUID o autonumérico (no determinísticos) y una tabla de aterrizaje con un plugin que hiciera los upserts.
- **Qué se rompe si se cambia:** cambiar K o la canonización hace que la siguiente carga no encuentre ninguna cuenta y duplique todo; rotar K exige recalcular las cuatro claves desde la bóveda. K vive en una tabla protegida de Smelter; esa custodia es un riesgo aceptado y se documenta en [Seguridad](03-seguridad.md).

### 3.3 El nombre principal guarda el número enmascarado

- **Decisión:** `sanic_cm_ct_numerodecuenta`, `sanic_cm_cd_numerocuenta` y `sanic_cm_cp_name` guardan `****1234`, calculado por la plantilla; `sanic_cm_ct_ultimos4` sirve para buscar. [`PanGuard.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PanGuard.cs) rechaza 5 o más dígitos seguidos en esas columnas.
- **Por qué:** todos los lookups, vistas y búsquedas muestran el nombre principal; enmascararlo corrige todos a la vez. *Formula columns* no muestran valores offline y las reglas de enmascarado nativas están en versión preliminar y no sirven en canvas.
- **Alternativa descartada:** columna calculada o de fórmula; enmascarar en cada pantalla.
- **Qué se rompe si se cambia:** una plantilla que vuelva a escribir el número completo hace fallar la carga fila por fila (PanGuard lo rechaza). Es el comportamiento buscado: no desactivar PanGuard para «arreglar» la carga.

### 3.4 En los textos libres del teléfono el PAN se enmascara, no se rechaza

- **Decisión:** [`PanEnTextoLibre.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PanEnTextoLibre.cs) (PreOperation síncrono, Create y Update) reemplaza en el comentario de la interacción y en las tres direcciones de la actualización de datos cada secuencia de 13 a 19 dígitos que pasa Luhn por `•••• ` y sus últimos 4. Nunca rechaza. La app del localizador muestra un aviso que no bloquea ([`scrInteraccion.pa.yaml`](../../apps/localizador-canvas/Src/scrInteraccion.pa.yaml), `lblAvisoTarjeta`).
- **Por qué:** esos textos llegan del teléfono, muchas veces sin señal, y el runtime offline descarta en silencio un registro rechazado: rechazar perdería la visita entera (y sus pagos) por un dato que se puede corregir en el servidor ([1.2](#12-lo-que-viene-del-teléfono-no-se-rechaza-se-marca)). Exigir Luhn y un largo de 13 a 19 evita enmascarar teléfonos, recibos o importes, porque aquí un falso positivo modifica el texto guardado.
- **Alternativa descartada:** una regla más de `PanGuard` (rechaza: perdería la visita); bloquear el guardado en la app (sin el servidor no protege lo que llega por la API y el aviso de la app no valida Luhn); no revisar los textos libres (un PAN quedaría guardado en claro, fuera de la bóveda).
- **Qué se rompe si se cambia:** convertirlo en rechazo pierde capturas sin aviso. Ampliar la detección (por ejemplo, sin Luhn o dentro de tiras de dígitos sin separadores) enmascara datos buenos que el BackOffice necesita al transcribir.

## 4. Acceso y reparto

### 4.1 «Solo su cartera» por dueño y compartidos, en un plugin asíncrono

- **Decisión:** [`RepartoAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RepartoAsignacion.cs) pone como dueño de la asignación al usuario del agente, le comparte en lectura cuenta, cliente y fiador, y revoca al dueño anterior lo que ninguna otra asignación activa suya justifique. Corre asíncrono, en PostOperation, con el servicio del sistema. El rol Localizador tiene lectura de alcance usuario.
- **Por qué:** un cliente puede tener cuentas en carteras distintas, así que no puede tener un dueño único; el compartido da acceso fila por fila. Asíncrono porque, síncrono, correría dentro del trozo de 100 filas de Smelter con su límite de 2 minutos y un exceso deshace el trozo entero.
- **Alternativa descartada:** lectura global de los datos maestros con filtro en la app (el teléfono bajaría el padrón completo) y el *flow* de Power Automate.
- **Qué se rompe si se cambia:** registrarlo síncrono hace fallar el plugin a propósito. Un trabajo fallido deja accesos de más o de menos. No hay una pantalla que reconcilie todo de una vez: para un agente, quitar y volver a poner su usuario en **Agentes y usuarios** re-reparte sus asignaciones ([Mantenimiento](11-mantenimiento.md#reconciliar-compartidos)). Los compartidos hacen crecer la tabla de accesos (POA); es un costo aceptado.

### 4.2 Cambiar el usuario de un agente reescribe sus asignaciones

- **Decisión:** al vincular, cambiar o quitar el usuario de un agente, [`RepartoPorAgente.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RepartoPorAgente.cs) vuelve a escribir el **mismo** agente en sus asignaciones activas mal repartidas, hasta 1.000 por ejecución, y pide reintento si quedan más.
- **Por qué:** el reparto solo corre al tocar la asignación; sin esto, las Incobrables y Especiales nunca cambiarían de dueño. Así la lógica del reparto vive en un solo lugar.
- **Alternativa descartada:** duplicar la lógica de dueño y compartidos en el plugin del agente.
- **Qué se rompe si se cambia:** si `RepartoAsignacion` deja de dispararse por el Update del agente de la asignación, este mecanismo deja de funcionar en silencio.

### 4.3 Equipo dueño «Cartera sin cobrador», sin miembros

- **Decisión:** una asignación activa sin agente, o cuyo agente no tiene usuario, pasa al *owner team* «CM · Cartera sin cobrador · <entorno>», sin miembros y con un rol que solo tiene lectura de asignaciones ([`CarteraSinCobrador.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CarteraSinCobrador.cs); se crea a mano según [Instalación, paso 5](09-instalacion-y-despliegue.md#5-equipo-cartera-sin-cobrador)).
- **Por qué:** el localizador anterior tiene que perderla y nadie tiene que verla en el teléfono. Dataverse no deja asignar un registro a un dueño sin lectura sobre la tabla.
- **Alternativa descartada:** dejarla con el dueño anterior o con SYSTEM.
- **Qué se rompe si se cambia:** si el equipo no existe o hay más de uno con ese prefijo, el reparto falla sin tocar nada. Agregarle un miembro le da a esa persona toda la cartera huérfana.

### 4.4 Cascadas cortadas donde filtraban acceso

- **Decisión:** *Share*, *Unshare* y *Reparent* en `NoCascade` en cliente → cuenta, cliente → carga diaria y cuenta → asignación; *Assign* en `NoCascade` en cuenta → asignación. Siguen en *cascade* cuenta → bóveda y cliente → dirección y teléfono. *Delete* en `Restrict` en interacción → pago, interacción → actualización de datos y cliente → carga diaria. El comportamiento de cada relación viaja en la *solution* (make.powerapps.com → **Tables** → tabla → **Relationships** → relación → **Advanced options**).
- **Por qué:** con *Reparent* en cascada, cada carga que reescribe una cuenta le daría a quien tiene acceso al cliente lectura heredada de la cuenta, su bóveda y sus asignaciones: el localizador vería todas las cuentas de sus clientes, con su número completo, aunque no fueran suyas. Donde la audiencia es la misma (bóveda, direcciones, teléfonos) la cascada es justo lo que se quiere.
- **Alternativa descartada:** las cascadas por defecto de Dataverse.
- **Qué se rompe si se cambia:** volver a *Cascade* en esas relaciones expone PAN ajenos al localizador, y el acceso heredado no se pierde al cerrar la asignación. En un ambiente nuevo hay que comprobarlo **antes** de la primera carga.

## 5. Asignaciones

### 5.1 Tres tipos con ciclos de vida distintos

- **Decisión:** `sanic_cm_tbl_asignacion` es una tabla viva, una fila por cuenta y tipo (*alternate key* `sanic_cm_ak_as_clavepan_tipo`). Diaria e Incobrable las trae solo la carga; la Especial pasa por aprobación del Supervisor ([`AprobacionEspecial.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/AprobacionEspecial.cs)). Una Diaria o Incobrable nace en Borrador; el valor por defecto del estado de aprobación sigue en Pendiente porque lo necesitan las Especiales del CSV.
- **Por qué:** el core es el único que clasifica Diaria e Incobrable; la Especial es un encargo puntual con segregación de funciones (el Solicitante pide, el Supervisor aprueba y elige localizador).
- **Alternativa descartada:** una fila por día (tabla de eventos) y que las personas creen cualquier tipo.
- **Qué se rompe si se cambia:** cambiar el valor por defecto a Borrador deja las Especiales del CSV fuera de la cola del Supervisor. Una persona con roles de CM, Admin incluido, no puede crear Diarias ni Incobrables: el alta manual pasa por [6.3](#63-el-alta-manual-de-una-cuenta-es-una-importación-de-una-fila).

### 5.2 Diaria e Incobrable son excluyentes; la Especial convive

- **Decisión:** cuando la carga trae una cuenta con el otro tipo, [`Reclasificacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/Reclasificacion.cs) cierra la anterior con motivo «Reclasificada a {tipo}», en la misma transacción (PostOperation síncrono).
- **Por qué:** sin la regla quedarían dos asignaciones activas de la misma cuenta. Se automatiza porque depende de la **presencia** de la cuenta en el archivo, sin riesgo de archivo truncado.
- **Alternativa descartada:** cierre manual por el Admin.
- **Qué se rompe si se cambia:** pasarlo a asíncrono deja ventanas con dos activas y el teléfono las muestra ambas.

### 5.3 Reconciliación de Diarias: manual, en el servidor y con freno

- **Decisión:** las Diarias que no vinieron en la última carga las cierra el Admin con «fuera de cartera» desde [`scrReconciliacion.pa.yaml`](../../apps/backoffice-canvas/Src/scrReconciliacion.pa.yaml), que llama por lotes a [`CerrarFueraDeCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CerrarFueraDeCartera.cs). La API exige el día de corte que vio la pantalla y la cantidad confirmada. Si superan el 30 %, la pantalla muestra una alerta roja y pide escribir la cantidad.
- **Por qué:** depende de la **ausencia** en el archivo: un archivo truncado cerraría carteras enteras. Se hace en el servidor porque una lista de canvas se corta en 2.000 filas.
- **Alternativa descartada:** cierre automático tras cada carga; `Patch` masivo desde la app.
- **Qué se rompe si se cambia:** quitar el control de corte o de cantidad permite cerrar con una pantalla desactualizada.

### 5.4 La carga reabre solo lo que cerró el sistema, reconocido por el motivo

- **Decisión:** [`CargaSobreAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CargaSobreAsignacion.cs) reabre una Diaria o Incobrable que vuelve en el archivo si su motivo es «fuera de cartera» o empieza con «Reclasificada a ». Los cierres de una persona y las Especiales no se reabren. Tampoco pisa la fecha de asignación.
- **Por qué:** la plantilla no mapea el estado; sin esto, una cuenta que vuelve quedaría cerrada.
- **Alternativa descartada:** una columna de origen del cierre.
- **Qué se rompe si se cambia:** los motivos son texto comparado en el plugin ([`Esquema.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/Esquema.cs)): cambiar ese texto en la reconciliación o en la reclasificación deja de reabrir. Por la misma razón, «fuera de cartera» no debe ofrecerse como motivo de cierre manual.

### 5.5 Las Diarias no se reasignan a mano

- **Decisión:** [`scrReasignar.pa.yaml`](../../apps/backoffice-canvas/Src/scrReasignar.pa.yaml) solo reasigna Especiales aprobadas (Supervisor y Admin) e Incobrables (Admin). Solo escribe el agente; el reparto mueve el dueño.
- **Por qué:** la carga diaria reescribe el agente de la Diaria: un cambio manual duraría hasta la carga siguiente.
- **Alternativa descartada:** reasignación manual de cualquier tipo.
- **Qué se rompe si se cambia:** habilitar Diarias en la pantalla produce reasignaciones que se deshacen solas al día siguiente.

### 5.6 Las listas editables se guardan como texto

- **Decisión:** tipos de Especial, motivos de cierre y mensajes rápidos salen de `sanic_cm_tbl_opcionlista`, que el Admin edita; lo elegido se guarda como texto en la asignación o la interacción. Las opciones no se borran: se desactivan ([`scrListas.pa.yaml`](../../apps/backoffice-canvas/Src/scrListas.pa.yaml); opciones iniciales en [Instalación, paso 10](09-instalacion-y-despliegue.md#10-opciones-iniciales-de-las-listas)).
- **Por qué:** renombrar una opción no cambia el historial, y el teléfono la lee offline.
- **Alternativa descartada:** columnas *choice* fijas.
- **Qué se rompe si se cambia:** convertirlo en lookup ata el historial a la opción actual. El tipo de Especial que manda el banco entra tal como viene, sin pasar por la lista.

## 6. Carga diaria

### 6.1 Smelter carga; CM solo configura plantillas

- **Decisión:** la carga la hace Smelter con la plantilla «Cobranza movil». La app de la oficina crea el *Import Job* sin estado, sube el archivo y recién después lo pone En cola, que es lo que dispara el orquestador de Smelter ([`scrCargaArchivo.pa.yaml`](../../apps/backoffice-canvas/Src/scrCargaArchivo.pa.yaml)).
- **Por qué:** Smelter es una herramienta compartida de otro equipo; CM solo tiene permiso de operador (por el equipo de carga) y no modifica su código.
- **Alternativa descartada:** un importador propio en plugins.
- **Qué se rompe si se cambia:** el estado En cola se pone unos 20 s después de subir el archivo. El disparador del orquestador evalúa el registro como está cuando arranca la corrida: con los tres cambios seguidos, el alta y la subida del archivo también verían «En cola» y el job se procesaría tres veces en paralelo. Detalle en [Carga diaria](07-carga-diaria.md).

### 6.2 Carga paralela: padres primero, partes por cliente

- **Decisión:** [`PartirCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PartirCarga.cs) convierte el job en uno de «padres» (última fila de cada producto, fiador y agente) y crea hasta 4 jobs de «partes» donde las filas que escriben un mismo registro van juntas ([`ParticionCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ParticionCarga.cs)). La app usa 3 partes (`nfPartesCarga`). El archivo se parte a nivel de bytes ([`ArchivoCsv.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ArchivoCsv.cs)).
- **Por qué:** en un solo job las filas se procesan en serie, en lotes que rozan el límite de 120 s por operación, y una carga grande tarda mucho. 3 y no 4 por el límite de protección del servicio de Dataverse (1.200 s de ejecución cada 5 minutos por usuario): cada parte consume unos 100 s por lote y con 4 se queda al borde ([`App.pa.yaml`](../../apps/backoffice-canvas/Src/App.pa.yaml)). Con menos de 300 filas la API hace una sola parte. Dos partes escribiendo el mismo registro a la vez chocarían.
- **Alternativa descartada:** un solo job; partir por número de fila.
- **Qué se rompe si se cambia:** las plantillas «Cobranza movil · padres» y «· partes» son copia exacta de su parte de la principal ([Instalación, paso 8.2](09-instalacion-y-despliegue.md#82-plantillas-derivadas)): si difieren en algo, `PartirCarga` se niega a partir. Un *Relationship Mapping* en la principal no se puede repartir entre las derivadas. Las tres plantillas se reconocen por nombre también en [`PurgaArchivoCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PurgaArchivoCarga.cs): renombrarlas deja los archivos de carga sin purgar (ver [8.2](#82-el-archivo-de-carga-se-purga-con-un-plugin-asíncrono-no-con-un-flow)).

### 6.3 El alta manual de una cuenta es una importación de una fila

- **Decisión:** [`AltaCuenta.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/AltaCuenta.cs) arma en el servidor un CSV de una fila con el formato del banco ([`FilaDeCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/FilaDeCarga.cs)) y crea un Import Job con la plantilla principal.
- **Por qué:** así el HMAC, la bóveda y el enmascarado los hace Smelter y K sigue en un solo lugar. La fila la arma el servidor para evitar trampas del upsert: la fecha de carga es la más nueva de la carga diaria (con la de hoy, todas las Diarias del banco quedarían «fuera de cartera»), y agente y producto se escriben con sus valores de Dataverse (un error de tipeo crearía uno nuevo).
- **Alternativa descartada:** crear la cuenta con `Patch` desde la app (tendría que conocer K o dejar la cuenta sin clave).
- **Qué se rompe si se cambia:** agregar una columna al archivo del banco exige agregarla al `Encabezado` de `FilaDeCarga`.

## 7. Escala

### 7.1 Tramos por hash del id de la cuenta

- **Decisión:** [`TramoCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/TramoCartera.cs) pone un tramo de 0 a 19 en asignación y cuenta (calculado del id de la cuenta) y en pago (del id de la interacción). Las apps traen la cartera, la reasignación y los pagos del día con 20 consultas, una por tramo.
- **Por qué:** cada copia a una colección de canvas se corta en 2.000 filas (⚠️ A confirmar: el paquete de la app del localizador declara 500 por fuente y 2.000 para la app; no está confirmado cuál rige). El tramo sale de la cuenta para que sea estable aunque la carga vuelva a crear la asignación, y usa FNV-1a porque los ids de Dataverse son secuenciales y sus primeros bytes reparten mal.
- **Alternativa descartada:** paginar por fecha u orden; tramo por id de la asignación.
- **Qué se rompe si se cambia:** cambiar `TramosCartera` o la función de hash deja las filas existentes en tramos que la app ya no consulta; hay que recalcularlas. Las apps también consultan el tramo vacío para lo que todavía no lo tiene.

### 7.2 Las fechas del banco son días, no instantes

- **Decisión:** `sanic_cm_as_ultimacarga` y `sanic_cm_as_fechaasignada` son columnas de solo fecha; `sanic_cm_cd_fechacarga` es independiente de la zona horaria. Las tres guardan el día tal como viene del banco.
- **Por qué:** guardadas como medianoche UTC se verían como el día anterior en la zona horaria local, y la reconciliación compara días.
- **Alternativa descartada:** fecha y hora en UTC.
- **Qué se rompe si se cambia:** el comportamiento de fecha de una columna no se puede revertir. En consultas de *bulk delete*, `last-x-hours` no vale en columnas de solo fecha.

## 8. Ciclo de vida

### 8.1 Borrado programado con identidad conservada en la interacción

- **Decisión:** trabajos diarios de *bulk delete* (se crean a mano según [Instalación, paso 11](09-instalacion-y-despliegue.md#11-borrado-programado)): carga diaria a los 7 días; cuentas 14 días después de salir de cartera y sin interacciones pendientes (por cascada, su bóveda y asignaciones); clientes y fiadores sin cuentas; pagos y actualizaciones a los 730 días; interacciones a los 735. Al crearse, la interacción copia últimos 4, CIF y clave HMAC de su cuenta ([`IdentidadEnInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/IdentidadEnInteraccion.cs)).
- **Por qué:** los datos del cliente no deben vivir más que la gestión; lo que capturó el localizador se conserva 2 años y sigue identificable en el core sin el número completo. La interacción va 5 días después porque `Restrict` impide borrarla mientras tenga pagos o actualizaciones de datos.
- **Alternativa descartada:** no borrar nunca; borrar la interacción junto con la cuenta.
- **Qué se rompe si se cambia:** borrar una cuenta con interacciones sin grabar deja al BackOffice sin la bóveda para transcribirlas. El nombre del cliente no se copia a la interacción, por decisión del dueño del producto. Detalle en [Ciclo de vida](08-ciclo-de-vida.md).

### 8.2 El archivo de carga se purga con un plugin asíncrono, no con un flow

- **Decisión:** [`PurgaArchivoCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PurgaArchivoCarga.cs), PostOperation **asíncrono** sobre el Update de `sanic_smlt_ij_status` de los trabajos de Smelter, borra con `DeleteFile` el CSV de los trabajos de las tres plantillas de CM cuando llegan a un estado terminal (2, 3 o 4) y barre hasta 50 trabajos de más de 7 días que todavía tengan archivo. Conserva el trabajo y sus registros.
- **Por qué:** el CSV trae los PAN completos y, una vez terminado el trabajo, nadie lo vuelve a leer. Un *plugin* viaja dentro del ensamblado (se instala y versiona con el resto), no necesita conexiones (*connection references*) ni un dueño de *flow* con licencia, y su lógica se prueba en la CI con las pruebas unitarias. Es asíncrono para no frenar al orquestador de Smelter, que cambia el estado dentro de su propia operación; el barrido cubre lo que dejó un paso fallido.
- **Alternativa descartada:** un *flow* de Power Automate disparado por el cambio de estado (necesita conexión, identidad dueña y licencia, no se prueba en la CI); un *bulk delete* (borra filas, no vacía una columna de archivo, y se perderían los registros del trabajo); vaciar el archivo a mano después de cada carga (depende de que alguien se acuerde).
- **Qué se rompe si se cambia:** borrar el archivo de un trabajo **sin** estado puede romper una carga: lo leen `PartirCarga`, Smelter y «Relanzar partes». Registrarlo síncrono lo deja sin efecto (el *plugin* no hace nada en ese modo). Renombrar una plantilla la deja fuera de la purga.

## Índice por pieza

| Si se va a cambiar… | Leer |
|---|---|
| Un plugin de interacción, pago o actualización de datos | [1.2](#12-lo-que-viene-del-teléfono-no-se-rechaza-se-marca), [1.3](#13-un-solo-nivel-de-lookup-en-lo-que-se-filtra-offline), [2.3](#23-el-localizador-solo-registra-en-su-cartera) |
| El perfil offline o la app del teléfono | [1.1](#11-la-app-del-teléfono-es-canvas-con-perfil-offline-propio), [1.4](#14-el-agente-se-identifica-por-el-upn-en-minúsculas), [7.1](#71-tramos-por-hash-del-id-de-la-cuenta) |
| Roles, equipos o privilegios | [2.2](#22-restricción-de-columnas-por-plugin-no-por-seguridad-de-columna), [2.4](#24-sin-roles-de-cm-sin-restricción), [4.3](#43-equipo-dueño-cartera-sin-cobrador-sin-miembros) |
| Textos libres que llegan del teléfono | [1.2](#12-lo-que-viene-del-teléfono-no-se-rechaza-se-marca), [3.4](#34-en-los-textos-libres-del-teléfono-el-pan-se-enmascara-no-se-rechaza) |
| Relaciones o cascadas | [3.1](#31-el-pan-vive-en-una-bóveda-con-seguridad-por-fila), [4.4](#44-cascadas-cortadas-donde-filtraban-acceso), [8.1](#81-borrado-programado-con-identidad-conservada-en-la-interacción) |
| La plantilla de Smelter o el archivo del banco | [3.2](#32-clave-sustituta-hmac-calculada-por-smelter), [3.3](#33-el-nombre-principal-guarda-el-número-enmascarado), [6.2](#62-carga-paralela-padres-primero-partes-por-cliente), [6.3](#63-el-alta-manual-de-una-cuenta-es-una-importación-de-una-fila), [8.2](#82-el-archivo-de-carga-se-purga-con-un-plugin-asíncrono-no-con-un-flow) |
| Motivos de cierre o la reconciliación | [5.3](#53-reconciliación-de-diarias-manual-en-el-servidor-y-con-freno), [5.4](#54-la-carga-reabre-solo-lo-que-cerró-el-sistema-reconocido-por-el-motivo), [5.6](#56-las-listas-editables-se-guardan-como-texto) |
| El reparto de asignaciones | [4.1](#41-solo-su-cartera-por-dueño-y-compartidos-en-un-plugin-asíncrono), [4.2](#42-cambiar-el-usuario-de-un-agente-reescribe-sus-asignaciones) |

## Cómo se agrega o cambia una decisión

1. Agregar una sección en el área que corresponda, con los cuatro puntos: decisión, por qué, alternativa descartada, qué se rompe si se cambia.
2. Enlazar el código que la implementa; si no hay código que la imponga, decirlo.
3. Si reemplaza a otra, reescribir la anterior para que describa lo vigente: este documento describe lo que la solución **es**, no su historia.
4. Sumarla al [índice por pieza](#índice-por-pieza).

## Trampas y errores conocidos

- ⚠️ **Varias decisiones fallan en silencio si se rompen:** un rechazo nuevo sobre lo que sube el teléfono ([1.2](#12-lo-que-viene-del-teléfono-no-se-rechaza-se-marca)), un cambio de texto en los motivos de cierre ([5.4](#54-la-carga-reabre-solo-lo-que-cerró-el-sistema-reconocido-por-el-motivo)), una cascada vuelta a *Cascade* ([4.4](#44-cascadas-cortadas-donde-filtraban-acceso)) o una persona sin rol de CM ([2.4](#24-sin-roles-de-cm-sin-restricción)). Ninguna produce un error visible.
- ⚠️ **K y la canonización del PAN son parte del esquema.** Cambiarlas equivale a cambiar la clave primaria de negocio de cuatro tablas ([3.2](#32-clave-sustituta-hmac-calculada-por-smelter)).
- **Los plugins asíncronos fallan fuera de la vista del usuario.** Los trabajos del reparto o de la purga del archivo de carga que fallan quedan en las operaciones del sistema; se diagnostican como se explica en [Mantenimiento](11-mantenimiento.md).
- **Los interruptores de la app de la oficina** (`nfCargaParalela`, `nfCierreServidor`, `nfValidarLargo` en [`App.pa.yaml`](../../apps/backoffice-canvas/Src/App.pa.yaml)) solo funcionan después de refrescar la fuente «Environment» en Studio; con la fuente desactualizada, la fórmula que llama a la Custom API queda inerte.
