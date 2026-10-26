# Ciclo de vida de los datos

Cuánto vive cada dato de Cobranza Móvil, qué lo cierra o lo borra y por qué. Cubre los siete trabajos de borrado programado, la purga del archivo de carga de Smelter, las *cascades* y *Restrict* que deciden qué arrastra cada borrado, lo que conserva la interacción cuando su cuenta desaparece, y los cierres de asignaciones (Reconciliación y Cierres). Está escrito para quien mantiene la solución y tiene que cambiar un plazo o diagnosticar por qué algo se borró (o no).

## Cómo funciona

La política tiene dos reglas:

- **Los datos del banco viven mientras la cuenta esté en cartera, más 14 días de gracia** (7 de la carga diaria + 7). Así una cuenta que el banco deja de mandar un par de días no pierde su historia.
- **Lo que crea la app (interacciones, pagos, actualizaciones de datos) vive 2 años.** Para que la interacción siga identificable en el core cuando su cuenta ya no existe, guarda los últimos 4 dígitos, el CIF y la clave HMAC de la cuenta.

Nada se borra desde las apps. Las asignaciones se **cierran** (estado inactivo, motivo y fecha de cierre) y el borrado lo hacen trabajos de *bulk delete* (trabajos nativos de Dataverse que borran en masa las filas que cumplen una consulta), recurrentes, uno por día.

```mermaid
flowchart TD
    CSV[/"Carga diaria del banco"/] --> AS["Asignación activa"]
    AS -->|"no vino en la carga<br/>(Reconciliación)"| CE["Asignación cerrada<br/>«fuera de cartera»"]
    AS -->|"Incobrable / Especial<br/>(Cierres)"| CE
    CE -->|"vuelve en el CSV y la cerró el sistema"| AS
    CE -->|"14 días sin asignación viva,<br/>sin cambios y sin interacción Pendiente"| BC["Bulk delete: cuenta"]
    BC -->|cascade| PAN["Bóveda y asignaciones"]
    BC -->|"quita el vínculo"| IN["Interacción / pago<br/>(conservan identidad)"]
    BC --> CL["Cliente sin cuentas<br/>(2 días)"]
    CL -->|cascade| DT["Direcciones y teléfonos"]
    IN -->|"730 días: pagos y actualizaciones<br/>735 días: interacción"| FIN(["Borrado"])
```

### Los siete trabajos de borrado

Viajan **dentro de la *solution*** `cm_core_cobranzamovil` como componentes **Data Life Cycle Config**: los trabajos de *bulk delete* son *solution-aware*, así que su definición (consulta completa y recurrencia) se exporta e importa como cualquier otro componente. Después de importar solo se verifican ([Instalación, paso 11](09-instalacion-y-despliegue.md#11-borrado-programado)). Cada uno es una consulta sobre una tabla que se repite cada 1 día, sin aviso por correo.

⚠️ Un trabajo importado **corre inmediatamente al importarse** ([documentación de Microsoft](https://learn.microsoft.com/power-platform/admin/delete-bulk-records#solution-aware-bulk-deletion-jobs)): cada importación de la *solution* dispara una limpieza en ese momento. Se planifica la importación (sin cargas en curso, fuera del horario de los localizadores), y solo un administrador puede importar una *solution* que contiene estos trabajos.

| # | Nombre del trabajo | Tabla | Borra | Plazo |
|---|---|---|---|---|
| 1 | `CM - Carga diaria de mas de 7 dias` | `sanic_cm_tbl_cargadiaria` («CM \| TBL \| Carga diaria») | `sanic_cm_cd_fechacarga` con más de 7 días | 7 días |
| 2 | `CM - Cuentas fuera de cartera hace mas de 14 dias` | `sanic_cm_tbl_cuenta` («CM \| TBL \| Cuenta») | `modifiedon` con más de 14 días **y** sin asignación viva **y** sin interacción Pendiente (ver abajo) | 14 días de gracia |
| 3 | `CM - Clientes sin cuentas` | `sanic_cm_tbl_cliente` («CM \| TBL \| Cliente») | Sin ninguna cuenta y `modifiedon` con más de 2 días | 2 días |
| 4 | `CM - Fiadores sin cuentas` | `sanic_cm_tbl_fiador` («CM \| TBL \| Fiador») | Sin ninguna cuenta y `modifiedon` con más de 2 días | 2 días |
| 5 | `CM - Pagos de mas de 2 anos` | `sanic_cm_tbl_pago` («CM \| TBL \| Pago») | `createdon` con más de 730 días | 730 días |
| 6 | `CM - Actualizaciones de datos de mas de 2 anos` | `sanic_cm_tbl_actualizaciondatos` («CM \| TBL \| Actualizacion de dato») | `createdon` con más de 730 días | 730 días |
| 7 | `CM - Interacciones de mas de 2 anos` | `sanic_cm_tbl_interaccion` («CM \| TBL \| Interaccion») | `createdon` con más de 735 días | 735 días |

**Asignación viva** (trabajo 2): una asignación de la cuenta (unida por `sanic_cm_as_cuentaid`) con `sanic_cm_as_estado` = activa, **o** con `sanic_cm_as_ultimacarga` en los últimos 14 días, **o** con `sanic_cm_as_fechacierre` en los últimos 14 días. **Interacción Pendiente**: una interacción de la cuenta con `sanic_cm_in_estadograbacion` = 1 (todavía no transcrita al core). El `modifiedon` de la cuenta cuenta porque cada carga la reescribe.

Los «sin registros relacionados» son, en la consulta, una unión con la tabla relacionada del tipo **Does Not Contain Data** (No contiene datos), que Dataverse resuelve como una unión externa sin filas del otro lado. En el trabajo 2, las condiciones de «asignación viva» e «interacción Pendiente» van dentro de esas uniones: la consulta completa está en el trabajo del ambiente de desarrollo y viaja así en la *solution*.

Agente, producto y opciones de lista no se borran nunca: se desactivan.

### Cascades y Restrict

Al borrar un registro padre, cada relación decide qué pasa con los hijos: **Cascade** los borra, **Restrict** impide borrar el padre mientras tenga hijos y **RemoveLink** deja los hijos con el *lookup* vacío. La configuración completa de las relaciones está en [Modelo de datos](02-modelo-de-datos.md); estas son las que mueven el ciclo de vida.

| Padre → hijo | Al borrar el padre | Efecto en el ciclo de vida |
|---|---|---|
| Cuenta → bóveda (`sanic_cm_tbl_cuentapan`) | Cascade | El número completo desaparece con la cuenta |
| Cuenta → asignación | Cascade | Las asignaciones se van con la cuenta |
| Cuenta → interacción, pago, actualización de datos | RemoveLink | Quedan sin cuenta; la interacción conserva su identidad (ver abajo) |
| Cuenta → carga diaria | RemoveLink | La foto queda sin vínculo hasta que su propio trabajo la borra |
| Cliente → dirección, teléfono | Cascade | Se van con el cliente |
| Cliente → cuenta | Cascade | Ver Trampas: el trabajo 3 solo borra clientes sin cuentas |
| Cliente → carga diaria | **Restrict** | Un cliente con carga diaria (≤ 7 días) no se puede borrar |
| Interacción → pago, actualización de datos | **Restrict** | La interacción no se borra mientras tenga pagos o actualizaciones |
| Fiador → cuenta | **Restrict** | Un fiador con cuentas no se borra |
| Producto → cuenta | **Restrict** | El producto no se borra mientras tenga cuentas |
| Agente → asignación, interacción, cuenta, carga diaria | **Restrict** | El agente no se borra; se desactiva |

El comportamiento de cada relación viaja en la *solution*. Se revisa en `make.powerapps.com` → **Tables** → la tabla padre → **Relationships** (Relaciones) → la relación → opciones avanzadas (**Delete**, Eliminar).

**Por qué el orden importa.** Los siete trabajos son independientes y Dataverse no garantiza en qué orden corren dentro del día. Los plazos están escalonados para que el resultado no dependa de ese orden:

- **Interacción 5 días después que sus pagos.** Con Restrict, una interacción de 731 días con pagos no se puede borrar. Si los trabajos 5 y 6 borran los pagos y actualizaciones a los 730 días, el 7 la encuentra libre a los 735.
- **Cliente después que su carga diaria.** La carga diaria se borra a los 7 días; la cuenta, como mínimo 14 días después de su última carga. Cuando el cliente se queda sin cuentas, ya no tiene carga diaria que lo frene. Si igual la tiene (por ejemplo, una cuenta borrada a mano), el borrado del cliente falla por Restrict ese día y vuelve a intentarse al siguiente.
- **Cliente y fiador 2 días después de su último cambio.** Evita borrar uno que una carga en curso acaba de crear y todavía no tiene la cuenta escrita.

Un registro que hoy no se puede borrar no es un error: queda para la próxima corrida. Los fallos se ven en el detalle del trabajo (ver Cómo se modifica).

### Qué conserva la interacción

El *plugin* [`IdentidadEnInteraccion`](../../plugins/Sanic.CobranzaMovil.Plugins/IdentidadEnInteraccion.cs) (PreOperation síncrono, solo Create de la interacción) copia de la cuenta:

| Columna de la interacción | Origen en la cuenta | Para qué |
|---|---|---|
| `sanic_cm_in_ultimos4` («[IN] Últimos 4») | `sanic_cm_ct_ultimos4` | Mostrar `•••• 1234` cuando la cuenta ya no existe |
| `sanic_cm_in_cifcliente` («[IN] CIF cliente») | `sanic_cm_ct_cifcliente` | Encontrar al cliente en el core |
| `sanic_cm_in_clavepan` («[IN] Clave PAN») | `sanic_cm_ct_clavepan` (HMAC) | La misma clave que calcula la carga para ese número: permite reconocer la cuenta si el banco la vuelve a mandar. Nunca el número completo |

Solo completa lo vacío y **nunca rechaza**: el teléfono descarta lo que el servidor rechaza, y perder una visita es peor que guardarla sin identidad. El nombre del cliente no se guarda, a propósito.

Cómo lo muestran las apps:

| Pantalla | Con la cuenta borrada |
|---|---|
| Transcripción ([`scrTranscripcion.pa.yaml`](../../apps/backoffice-canvas/Src/scrTranscripcion.pa.yaml)) | Cliente «(cuenta fuera de cartera)»; CIF y últimos 4 de la interacción. Sin cuenta no consulta la bóveda, así que no hay número completo |
| Transcritas ([`scrTranscritas.pa.yaml`](../../apps/backoffice-canvas/Src/scrTranscritas.pa.yaml)) | Cliente «(cuenta fuera de cartera)»; CIF y últimos 4 de la interacción |
| Sincronización del localizador ([`scrSincronizacion.pa.yaml`](../../apps/localizador-canvas/Src/scrSincronizacion.pa.yaml)) | «(cuenta ya no asignada) · •••• 1234» |
| Pagos del localizador ([`scrPagos.pa.yaml`](../../apps/localizador-canvas/Src/scrPagos.pa.yaml)) | «(cuenta ya no asignada)» y los últimos 4 de la interacción |

### Cierres de asignaciones

Antes de borrarse, una cuenta tiene que quedar sin asignación viva. Las asignaciones se cierran por cuatro caminos:

| Camino | Qué cierra | Motivo | Se reabre sola |
|---|---|---|---|
| **Reconciliación** ([`scrReconciliacion.pa.yaml`](../../apps/backoffice-canvas/Src/scrReconciliacion.pa.yaml) + [`CerrarFueraDeCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CerrarFueraDeCartera.cs)) | Diarias activas cuya «Última carga» es anterior al día de la última carga | `fuera de cartera` | Sí, si vuelve en el CSV (`CargaSobreAsignacion`) |
| **Reclasificación** (plugin `Reclasificacion`, durante la carga) | La Diaria o Incobrable anterior cuando llega el otro tipo | `Reclasificada a {tipo}` | Sí, si vuelve con el tipo anterior |
| **Cierres** ([`scrCierres.pa.yaml`](../../apps/backoffice-canvas/Src/scrCierres.pa.yaml)) | Incobrables activas y Especiales aprobadas activas, una por una | Lista «Motivo de cierre Incobrable» o «Motivo de cierre Especial» de Opciones de lista, más un detalle opcional | No (cierre de una persona) |
| **Rechazo** de una Especial (Aprobaciones) | La Especial rechazada | Rechazada | No |

Al cerrarse, `RepartoAsignacion` le revoca al localizador la cuenta, el cliente y el fiador si ninguna otra asignación activa suya los justifica. Detalle en [Plugins y Custom APIs](04-plugins-y-custom-apis.md).

**Reconciliación**, paso a paso:

1. **Corte.** El día de la última carga (`nfUltimaCarga`, la `FechaCarga` más nueva). «Última carga» es *DateOnly*: se compara día contra día, sin zona.
2. **Frenos de la pantalla.** No deja cerrar si no hay carga de hoy, ni si hay una carga **en curso**: un *Import Job* real (no prueba en seco) de cualquiera de las tres plantillas en cola, procesando, o sin estado y creado en las últimas 12 h. Mientras corren las partes, las Diarias que faltan se verían como «fuera de cartera». Si no puede leer los jobs, también bloquea (falla cerrado).
3. **Umbral de cordura.** Si las desaparecidas superan el 30 % de las Diarias activas, muestra una alerta roja y exige escribir la cantidad para confirmar. Es una alerta, no un bloqueo: el banco puede haber sacado media cartera.
4. **Cierre en el servidor.** Con `nfCierreServidor = true`, un temporizador llama a `sanic_cm_CerrarFueraDeCartera` en lotes de 250 (máximo 500) hasta que `Restantes` sea 0. La Custom API se niega a cerrar si la última carga cambió desde que se abrió la pantalla (`Corte`) o si hay más desaparecidas que las confirmadas (`Esperadas`). Cierra con los permisos del llamador (el Admin), para que la auditoría lo muestre a él; solo la aceptan Admin, Servicio o un usuario sin roles de CM. `SoloContar` devuelve el conteo sin escribir.

Con `nfCierreServidor = false` la pantalla hace un `Patch` masivo desde la app, limitado a las 2.000 filas que puede tener una colección.

### Archivo de carga de Smelter

Cada envío a Smelter (prueba en seco, carga en serie, carga paralela y alta de cuenta) crea un trabajo de importación (`sanic_tbl_smlt_importjob`) con el CSV del banco en la columna de archivo `sanic_smlt_ij_sourcefile`, con los números de tarjeta completos. Ese archivo no lo borra un *bulk delete* sino el *plugin* [`PurgaArchivoCarga`](../../plugins/Sanic.CobranzaMovil.Plugins/PurgaArchivoCarga.cs), asíncrono, que corre cada vez que cambia `sanic_smlt_ij_status` de un trabajo:

| Caso | Qué hace | Por qué |
|---|---|---|
| El trabajo que cambió de estado, si es de una de las tres plantillas de CM («Cobranza movil», «· padres», «· partes») y quedó en estado terminal (2 *Completed*, 3 *CompletedWithErrors*, 4 *Failed*) | Borra su archivo con `DeleteFile`, sea prueba en seco o carga real | Un trabajo terminado no se vuelve a leer: cada envío crea un trabajo nuevo con su propio archivo |
| Barrido en la misma corrida: hasta 50 trabajos de esas plantillas, con archivo, de más de 7 días, en estado terminal o pruebas en seco sin estado | Borra sus archivos, los más viejos primero | Recupera lo que un paso fallido dejó y los trabajos anteriores a la instalación del *plugin* |
| Trabajo sin estado (salvo esas pruebas viejas), en cola o procesando | No lo toca | Todavía lo pueden leer `PartirCarga`, Smelter o «Relanzar partes» |

El trabajo se conserva con sus contadores y sus registros; solo se borra el archivo. Los archivos que ya existían antes de instalar el *plugin* los recoge el barrido, hasta 50 por corrida, cada vez que algún trabajo cambia de estado. ⚠️ A confirmar un procedimiento gráfico para borrar a mano el archivo de un trabajo puntual (por ejemplo, desde el trabajo en las pantallas de Smelter o desde la fila de «TBL | SMLT | Import Job» en `make.powerapps.com`). Ficha del *plugin*: [Plugins y Custom APIs](04-plugins-y-custom-apis.md#purgaarchivocarga).

## Piezas y archivos

| Pieza | Archivo |
|---|---|
| Trabajos de borrado programado | En la *solution*, como componentes **Data Life Cycle Config**; se ven en admin center → **Bulk deletion** ([Instalación, paso 11](09-instalacion-y-despliegue.md#11-borrado-programado)) |
| Restrict y cascades | Relaciones de las tablas, en la *solution* ([Modelo de datos](02-modelo-de-datos.md#relaciones-y-cascadas)) |
| Borrado del archivo de los jobs de Smelter | [`PurgaArchivoCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PurgaArchivoCarga.cs), [`PurgaArchivoCargaTests.cs`](../../plugins/Sanic.CobranzaMovil.Plugins.Tests/PurgaArchivoCargaTests.cs) |
| Identidad en la interacción | [`IdentidadEnInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/IdentidadEnInteraccion.cs), [`IdentidadEnInteraccionTests.cs`](../../plugins/Sanic.CobranzaMovil.Plugins.Tests/IdentidadEnInteraccionTests.cs) |
| Reapertura y reclasificación | [`CargaSobreAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CargaSobreAsignacion.cs), [`Reclasificacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/Reclasificacion.cs) |
| Cierre en lote | [`CerrarFueraDeCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CerrarFueraDeCartera.cs), [`CerrarFueraDeCarteraTests.cs`](../../plugins/Sanic.CobranzaMovil.Plugins.Tests/CerrarFueraDeCarteraTests.cs) |
| Pantallas | [`scrReconciliacion.pa.yaml`](../../apps/backoffice-canvas/Src/scrReconciliacion.pa.yaml), [`scrCierres.pa.yaml`](../../apps/backoffice-canvas/Src/scrCierres.pa.yaml) |
| Interruptor del cierre en servidor | `nfCierreServidor` en [`App.pa.yaml`](../../apps/backoffice-canvas/Src/App.pa.yaml) |

## Cómo se modifica

### Ver, pausar o cancelar los trabajos

En el Power Platform admin center: **Entornos** → el ambiente → **Configuración** → **Administración de datos** → **Eliminación masiva** (*Bulk deletion*). Cada trabajo `CM - …` aparece como un trabajo del sistema recurrente.

| Para | Hacer |
|---|---|
| Ver si corrió y cuánto borró | Abrir el trabajo; el estado está en *Status Reason* |
| Ver qué no pudo borrar (por ejemplo, por Restrict) | Abrir el trabajo → **Related** → **Failures** |
| Ver la consulta | Abrir el trabajo → **Information** → **Properties** |
| Pausar, posponer, reanudar o cancelar | Menú **Actions** del trabajo |

⚠️ Antes de una migración o de una carga masiva de prueba, pausar los trabajos 2 a 4: una carga que no toca ciertas cuentas durante 14 días las deja elegibles.

### Cambiar un plazo

Los trabajos se cambian **solo en el ambiente de desarrollo** y llegan a los demás ambientes con la *solution*. Un trabajo de *bulk delete* no se edita: se cancela y se crea otro.

1. En el ambiente de desarrollo: admin center → ambiente → **Settings** → **Data management** → **Bulk deletion**: abrir el trabajo y cancelarlo desde el menú **Actions** (Acciones).
2. Crear el trabajo nuevo con **New** (se abre **Bulk Deletion Wizard**), con la misma consulta y el plazo nuevo; **Run this job after every** **1** día; hora de inicio fuera del horario de carga; sin correo. Si el nombre incluye el plazo (trabajos 1 y 2), ajustar también el nombre. Antes de enviarlo, revisar con **Preview Records** (Vista previa de registros) cuántas filas cumplen hoy.
3. Mantener el escalonamiento: el plazo de las interacciones mayor que el de pagos y actualizaciones, y la gracia de la cuenta mayor que el plazo de la carga diaria.
4. En `make.powerapps.com` → **Solutions** → `cm_core_cobranzamovil` → **Add existing** (Agregar existente) → **More** (Más) → **Other** (Otros) → **Data Life Cycle Config** → elegir el trabajo nuevo. Quitar de la *solution* el trabajo cancelado.
5. Exportar la *solution* con un número de versión mayor ([Instalación, paso 1](09-instalacion-y-despliegue.md#1-conseguir-el-paquete-de-la-solution)) e importarla en los demás ambientes, recordando que el trabajo corre al importarse.
6. En cada ambiente, comprobar en **Bulk deletion** que quede el trabajo nuevo y no el viejo; si el viejo sigue recurrente, cancelarlo desde **Actions**.
7. Actualizar la tabla de trabajos de [Instalación, paso 11](09-instalacion-y-despliegue.md#11-borrado-programado) y la de este documento.

### Agregar una tabla nueva al ciclo de vida

Igual que cambiar un plazo, pero sin cancelar nada: en el ambiente de desarrollo, crear el trabajo con **New** (tabla en **Look for**, condiciones y, para «sin registros relacionados», la tabla relacionada con **Does Not Contain Data**), agregarlo a la *solution* con **Add existing** → **More** → **Other** → **Data Life Cycle Config**, exportar e importar. Agregarlo a la tabla de trabajos de la instalación. Revisar antes las relaciones de la tabla: una Restrict con un hijo que vive más hace que el borrado falle todos los días.

### Operadores que no valen con *DateOnly*

`sanic_cm_cd_fechacarga` y `sanic_cm_as_ultimacarga` son *DateOnly*. Con esas columnas Dataverse rechaza los operadores de horas y minutos (error `0x80048415`):

| Vale | No vale |
|---|---|
| `olderthan-x-days` (`OlderThanXDays`), `last-x-days` (`LastXDays`), `on`, `lt`, `ge`… | `olderthan-x-hours`, `olderthan-x-minutes`, `last-x-hours`, `next-x-hours` |

Para probar el borrado con plazos cortos, usar días o columnas que no sean *DateOnly* (`createdon`, `modifiedon`).

## Trampas y errores conocidos

- ⚠️ **Archivos de carga que quedan.** Una parte de la carga paralela sin estado que nadie relanza ni descarta, o un trabajo real sin estado que nadie cierra, conserva su archivo con los números de tarjeta completos: `PurgaArchivoCarga` no lo toca, porque todavía se podría leer. Se resuelve con «▶ Relanzar partes», «✕ Descartar partes» o «✕ Descartar carga» en Cargar archivo; al llegar a un estado terminal, el *plugin* borra el archivo. Los trabajos y sus registros no se borran nunca.
- **El paso de `PurgaArchivoCarga` tiene que estar activo y asíncrono.** Registrado síncrono no hace nada (no frena a Smelter). Si el paso falta o está apagado, los archivos se acumulan; al reactivarlo, el barrido los borra de a 50 por corrida ([Instalación, paso 3](09-instalacion-y-despliegue.md#3-revisar-lo-importado)).
- ⚠️ **Borrar un cliente a mano borra sus cuentas** (Cascade), con su bóveda y sus asignaciones. El trabajo 3 solo elige clientes sin cuentas, pero un Admin con la API no tiene ese freno.
- **Una cuenta borrada vuelve como nueva.** Si el banco la manda otra vez, la carga crea una cuenta nueva con la misma clave HMAC; las interacciones viejas siguen sin vínculo (conservan su `Clave PAN`).
- **La interacción creada sin cuenta no guarda identidad.** `IdentidadEnInteraccion` solo actúa en el Create y solo si la cuenta existe.
- **«Fuera de cartera» en Cierres.** `CargaSobreAsignacion` reconoce un cierre del sistema por el motivo exacto `fuera de cartera` (sin distinguir mayúsculas). Una Incobrable cerrada a mano con ese motivo y **sin** detalle se reabre si el banco la vuelve a mandar; con detalle («Fuera de cartera — …») no.
- **El teléfono pierde lo cerrado recién al sincronizar.** El cierre y la revocación son inmediatos en el servidor; el dispositivo los ve en la próxima sincronización (ver [App del localizador](05-app-localizador.md)).
- **Reconciliación con una carga en serie colgada.** Un job sin estado de menos de 12 h bloquea Reconciliación. Se libera con «✕ Descartar carga» en Cargar archivo (ver [Carga diaria](07-carga-diaria.md)).
- **`solutions/unpacked` puede estar atrasada.** En el repositorio, las relaciones cliente → carga diaria, interacción → pago e interacción → actualización de datos figuran todavía como Cascade, y la relación cuenta → bóveda no figura. Lo que vale es lo que tiene el ambiente (y el `.zip` exportado de él); se revisa como se indica en [Cascades y Restrict](#cascades-y-restrict).
