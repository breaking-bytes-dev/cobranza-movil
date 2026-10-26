# Glosario

Los términos de Cobranza Móvil, para quien usa las apps y para quien mantiene la solución. Cada término tiene una definición en lenguaje simple y, si es un concepto técnico, la tabla o el archivo donde vive. Las tablas se nombran sin el prefijo `sanic_cm_tbl_` (por ejemplo, `cuenta` es `sanic_cm_tbl_cuenta`); el detalle de cada una está en [Modelo de datos](tecnica/02-modelo-de-datos.md).

## Personas y roles

| Término | Qué es | Dónde vive |
|---|---|---|
| **Localizador** | La persona que sale a la calle a visitar clientes con deuda y registra cada visita y cada cobro en el teléfono. En el código histórico aparece como «cobrador» (por ejemplo, `InteraccionDelCobrador`, el equipo «Cartera sin cobrador» o el parámetro `--app cobrador`). | Rol `SR \| CM \| Localizador` |
| **Agente** | El registro que representa a un localizador dentro del sistema. Lo crea la carga diaria con el nombre que manda el banco; el Admin lo vincula después al usuario de la persona. | Tabla `agente` |
| **BackOffice** | La persona de la oficina que transcribe al *core* lo que registraron los localizadores. | Rol `SR \| CM \| BackOffice` |
| **Solicitante** | Quien pide asignaciones Especiales para una cuenta. | Rol `SR \| CM \| Solicitante` |
| **Supervisor** | Quien aprueba o rechaza las Especiales, elige el localizador y reasigna cuentas. | Rol `SR \| CM \| Supervisor` |
| **Admin** | Quien carga el archivo diario, reconcilia, cierra asignaciones, da de alta cuentas y administra localizadores, datos maestros y listas. | Rol `SR \| CM \| Admin` |
| **Servicio - Cobranza** | Identidad técnica (no una persona) con la que corren procesos automáticos. | Rol `SR \| CM \| Servicio - Cobranza` |

## Cartera y asignaciones

| Término | Qué es | Dónde vive |
|---|---|---|
| **Cartera** | El conjunto de cuentas que un localizador tiene que cobrar: sus asignaciones activas. | Tabla `asignacion` |
| **Asignación** | El vínculo entre una cuenta y un localizador, con un tipo y un estado. Una cuenta puede tener a la vez una Diaria o una Incobrable, y además una Especial. | Tabla `asignacion` |
| **Diaria** | Asignación que manda el banco todos los días en el archivo. Si deja de venir, se cierra en la reconciliación. | `as_tipo` = 1 |
| **Incobrable** | Asignación de deuda de difícil cobro. Llega en el archivo, pero no todos los días; no se cierra en la reconciliación sino a mano. | `as_tipo` = 2 |
| **Especial** | Asignación pedida para una gestión puntual (por ejemplo, traer un retiro). La pide un Solicitante o la manda el banco, y no llega a ningún localizador hasta que un Supervisor la aprueba. | `as_tipo` = 3 |
| **Estado de aprobación** | En qué punto está una Especial: Borrador, Pendiente aprobación, Aprobado o Rechazada. | `as_estadoaprobacion` |
| **Activa / Cerrada** | Si la asignación sigue vigente. Cerrar no borra: la asignación queda como historia y desaparece del teléfono. | `as_estado` |
| **Orden** | El orden de visita que el localizador elige para su cartera. Es lo único de la asignación que el localizador puede cambiar. | `as_orden` |
| **Reparto** | Lo que hace el servidor cuando una asignación cambia: la pone a nombre del usuario del localizador, le comparte la cuenta, el cliente y el fiador, y se los quita al localizador anterior. | [`RepartoAsignacion.cs`](../plugins/Sanic.CobranzaMovil.Plugins/RepartoAsignacion.cs) |
| **Reasignar** | Pasar una asignación a otro localizador. Lo hacen el Supervisor o el Admin; las Diarias no se reasignan a mano porque manda el archivo del banco. | Pantalla [`scrReasignar.pa.yaml`](../apps/backoffice-canvas/Src/scrReasignar.pa.yaml) |
| **Cartera sin cobrador** | Nombre histórico del equipo que queda como dueño de las asignaciones cuyo agente no tiene usuario. No tiene miembros: esas asignaciones no las ve ningún localizador. Su nombre en el ambiente es «CM · Cartera sin cobrador · \<entorno\>». | [`CarteraSinCobrador.cs`](../plugins/Sanic.CobranzaMovil.Plugins/CarteraSinCobrador.cs), [`paso5_cartera_sin_cobrador.py`](../scripts/seguridad/paso5_cartera_sin_cobrador.py) |
| **Cierre** | Terminar una asignación (pasarla a Cerrada) con un motivo: por ejemplo, «Recuperada» en una Incobrable o «Gestión completada» en una Especial. Lo hace el Admin. | `as_motivo`, `as_fechacierre`; pantalla [`scrCierres.pa.yaml`](../apps/backoffice-canvas/Src/scrCierres.pa.yaml) |
| **Reconciliación** | El paso diario en que el Admin cierra las Diarias que no vinieron en el último archivo del banco. La app avisa si son más del 30 %, porque suele ser un archivo incompleto. | Pantalla [`scrReconciliacion.pa.yaml`](../apps/backoffice-canvas/Src/scrReconciliacion.pa.yaml), Custom API [`CerrarFueraDeCartera.cs`](../plugins/Sanic.CobranzaMovil.Plugins/CerrarFueraDeCartera.cs) |
| **Fuera de cartera** | Una cuenta que el banco dejó de mandar. Es también el motivo de cierre que pone la reconciliación. Si la cuenta vuelve en un archivo posterior, la asignación se reabre sola. | Motivo `fuera de cartera`; [`CargaSobreAsignacion.cs`](../plugins/Sanic.CobranzaMovil.Plugins/CargaSobreAsignacion.cs) |
| **Reclasificación** | Cuando una cuenta pasa de Diaria a Incobrable o al revés: el sistema cierra la anterior con motivo «Reclasificada a …». | [`Reclasificacion.cs`](../plugins/Sanic.CobranzaMovil.Plugins/Reclasificacion.cs) |

## Clientes y cuentas

| Término | Qué es | Dónde vive |
|---|---|---|
| **Cliente** | La persona que tiene la deuda. | Tabla `cliente` |
| **CIF** | El identificador del cliente en el banco. | `cl_cif`; copia en `ct_cifcliente` e `in_cifcliente` |
| **Cuenta** | Una tarjeta o un préstamo en mora de un cliente. Es lo que se cobra. | Tabla `cuenta` |
| **Mora** | Atraso en los pagos. Se mide en días de mora y nivel de mora. | `ct_diasmora`, `ct_nivelmora` |
| **Saldo vencido** | Lo que el cliente debe y ya venció, en córdobas (NIO) y en dólares (USD). | `ct_saldovencidonio`, `ct_saldovencidousd` |
| **Retiro** | Monto que el localizador tiene que traer en una gestión Especial. | `ct_retironio`/`usd` (archivo), `as_retironio`/`usd` (Especial) |
| **Producto** | El tipo de tarjeta o préstamo. | Tabla `producto` |
| **Fiador** | La persona que garantiza la deuda. | Tabla `fiador` |
| **Datos maestros** | Clientes, direcciones, teléfonos, productos y agentes. Los trae la carga diaria; el Admin puede agregar en la pantalla de maestros, pero la carga siguiente pisa lo que traiga el banco. | Pantalla [`scrMaestros.pa.yaml`](../apps/backoffice-canvas/Src/scrMaestros.pa.yaml) |
| **Alta de cuenta** | Crear a mano una cuenta que no vino en el archivo. El Admin la carga en un formulario y el sistema la importa como si fuera una fila más del archivo del banco. | Pantalla [`scrAltaCuenta.pa.yaml`](../apps/backoffice-canvas/Src/scrAltaCuenta.pa.yaml), Custom API [`AltaCuenta.cs`](../plugins/Sanic.CobranzaMovil.Plugins/AltaCuenta.cs) |

## Número de tarjeta

| Término | Qué es | Dónde vive |
|---|---|---|
| **PAN** | El número completo de la tarjeta. Es un dato protegido: solo se guarda en la bóveda y nunca se muestra entero salvo cuando la persona lo pide. | `cp_pan` |
| **Bóveda** | La tabla que guarda el número completo, una fila por cuenta. Solo la leen el BackOffice y el localizador que tiene la cuenta asignada (botón «Ver número»). | Tabla `cuentapan` |
| **Número enmascarado** | El número con todos los dígitos ocultos menos los últimos 4: `****1234`. Es lo que muestran las pantallas. | `ct_numerodecuenta`, `cd_numerocuenta`, `cp_name` |
| **Últimos 4** | Los cuatro dígitos finales del número, para buscar y mostrar. Dos cuentas distintas pueden compartirlos. | `ct_ultimos4`, `in_ultimos4` |
| **Clave PAN (HMAC)** | Un código calculado a partir del número que identifica la cuenta sin guardar el número. Con el mismo número siempre da el mismo código, pero del código no se puede volver al número. | `ct_clavepan`, `as_clavepan`, `cd_clavepan`, `cp_clavepan`, `in_clavepan` |
| **Clave K** | La clave secreta con la que Smelter calcula la Clave PAN. Ningún plugin la conoce. | Tabla protegida de Smelter; ver [Seguridad](tecnica/03-seguridad.md) |

## Visitas, pagos y transcripción

| Término | Qué es | Dónde vive |
|---|---|---|
| **Visita** | El encuentro (o intento) del localizador con un cliente. Cada visita se registra como una interacción. | Pantalla [`scrInteraccion.pa.yaml`](../apps/localizador-canvas/Src/scrInteraccion.pa.yaml) |
| **Interacción** | Lo que el localizador registra en una visita: comentario, ubicación, si hubo pago y si el cliente pidió cambiar sus datos. Recibe un número `IN-xxxx` cuando llega al servidor. | Tabla `interaccion` |
| **Mensaje rápido** | Texto predefinido que el localizador elige para no escribir el comentario a mano (por ejemplo, «Promesa de pago»). | Tabla `opcionlista` (lista 4) |
| **Pago** | Un cobro registrado en una visita: monto, moneda y número de recibo. Una visita puede tener varios pagos. Recibe un número `PG-xxxx`. | Tabla `pago` |
| **Recibo** | El comprobante en papel que el localizador entrega al cliente. En el sistema se anota su número. | `pg_numerorecibo` |
| **Recibo repetido** | Un pago cuyo número de recibo ya existía en otro pago. No se rechaza: entra marcado para que la oficina lo revise. | `pg_reciborepetido`; [`ReciboRepetido.cs`](../plugins/Sanic.CobranzaMovil.Plugins/ReciboRepetido.cs) |
| **Actualización de datos** | Los datos de contacto nuevos que el cliente pidió cambiar (teléfono, dirección, correo). Es un pedido para la oficina: no cambia los datos del cliente en este sistema. | Tabla `actualizaciondatos` |
| **Sin vincular** | Una actualización de datos que llegó al servidor antes que su interacción. Se vincula sola cuando llega la interacción. | `ad_sinvincular`; [`VincularActualizacionPendiente.cs`](../plugins/Sanic.CobranzaMovil.Plugins/VincularActualizacionPendiente.cs) |
| **Core** | El sistema central del banco, donde vive la información oficial de los clientes y las cuentas. Cobranza Móvil no se conecta a él: una persona copia los datos. | — |
| **Transcribir** | Pasar a mano una interacción (y sus pagos y actualizaciones) al *core*, y anotar el código que devuelve. | Pantalla [`scrTranscripcion.pa.yaml`](../apps/backoffice-canvas/Src/scrTranscripcion.pa.yaml) |
| **Grabada** | Una interacción que ya se transcribió al *core*. No se puede volver a grabar ni cambiar su código. La contraria es **Pendiente**. | `in_estadograbacion` = 2 (Grabado); [`GrabacionInteraccion.cs`](../plugins/Sanic.CobranzaMovil.Plugins/GrabacionInteraccion.cs) |
| **Código de grabación** | El código que da el *core* al registrar la interacción. Es obligatorio para marcarla como grabada. | `in_codigograbacion` |
| **Transcritas** | La pantalla donde la oficina revisa lo ya transcrito y los pagos del día. | Pantalla [`scrTranscritas.pa.yaml`](../apps/backoffice-canvas/Src/scrTranscritas.pa.yaml) |

## Carga diaria

| Término | Qué es | Dónde vive |
|---|---|---|
| **Archivo diario** | El CSV que manda el banco cada día con todas las cuentas en mora y su asignación. | Formato en [Carga diaria](tecnica/07-carga-diaria.md) |
| **Carga diaria** | El proceso de importar ese archivo. También se llama así la tabla que guarda una foto por cuenta y por día, con los montos de esa fecha. | Pantalla [`scrCargaArchivo.pa.yaml`](../apps/backoffice-canvas/Src/scrCargaArchivo.pa.yaml); tabla `cargadiaria` |
| **Fecha de carga** | La fecha que trae el archivo. La reconciliación compara contra la más reciente. | `cd_fechacarga`, `as_ultimacarga` |
| **Smelter** | Herramienta de importación de terceros instalada en el ambiente. Lee el archivo, calcula el número enmascarado y la Clave PAN, y crea o actualiza las tablas según una plantilla. No se modifica su código, solo su configuración. | Tablas `sanic_tbl_smlt_*` |
| **Plantilla** | La configuración de Smelter que dice qué columna del archivo va a qué tabla y columna. La principal se llama «Cobranza movil». | Ver [Carga diaria](tecnica/07-carga-diaria.md) |
| **Import Job** | Una ejecución de Smelter sobre un archivo. | Tabla `sanic_tbl_smlt_importjob` |
| **Carga paralela (padres y partes)** | Forma de cargar un archivo grande en varios trabajos a la vez: primero los «padres» (productos, fiadores y agentes) y después las «partes», repartidas por cliente. | Custom API [`PartirCarga.cs`](../plugins/Sanic.CobranzaMovil.Plugins/PartirCarga.cs) |
| **Monitoreo** | La pantalla donde el Admin sigue el estado de la carga. | [`scrMonitoreo.pa.yaml`](../apps/backoffice-canvas/Src/scrMonitoreo.pa.yaml) |

## Teléfono y sincronización

| Término | Qué es | Dónde vive |
|---|---|---|
| **Offline (sin señal)** | La app del localizador funciona sin conexión: guarda lo que se registra en el teléfono y lo envía después. | [App del localizador](tecnica/05-app-localizador.md) |
| **Sincronización** | El intercambio entre el teléfono y el servidor cuando hay señal: sube lo registrado y baja la cartera actualizada. | Pantalla [`scrSincronizacion.pa.yaml`](../apps/localizador-canvas/Src/scrSincronizacion.pa.yaml) |
| **Perfil offline** | La configuración de Dataverse que define qué tablas y qué filas bajan al teléfono: las asignaciones activas, lo compartido con el localizador y la historia de los últimos 90 días. | [`paso7_perfil_offline.py`](../scripts/pan/paso7_perfil_offline.py) |
| **Fecha de captura** | El momento en que el localizador registró la visita en el teléfono, que puede ser horas antes de que llegue al servidor. | `in_fechacaptura`, `ad_fechacaptura` |
| **Tramo** | Número de 0 a 19 que divide la cartera en partes, porque una app trae como máximo 2.000 filas por consulta. | `as_tramo`, `ct_tramo`, `pg_tramo`; [`TramoCartera.cs`](../plugins/Sanic.CobranzaMovil.Plugins/TramoCartera.cs) |

## Términos de la plataforma

| Término | Qué es | Dónde vive |
|---|---|---|
| **Dataverse** | La base de datos de Power Platform donde viven las 14 tablas. | [Modelo de datos](tecnica/02-modelo-de-datos.md) |
| **Canvas app** | El tipo de app de Power Apps que usan el teléfono y la oficina. | [`apps/`](../apps/) |
| **Plugin** | Código C# que corre en el servidor de Dataverse cuando se crea o modifica un registro, para validar o completar datos. | [`plugins/`](../plugins/Sanic.CobranzaMovil.Plugins/) |
| **Custom API** | Una operación propia del servidor que las apps llaman como una función (por ejemplo, la reconciliación o el alta de cuenta). | [Plugins y Custom APIs](tecnica/04-plugins-y-custom-apis.md) |
| **Lookup** | Una columna que apunta a un registro de otra tabla (por ejemplo, la cuenta de una interacción). | — |
| **Clave alternativa** (*alternate key*) | Una columna o combinación de columnas que no se puede repetir en la tabla y sirve para encontrar un registro sin su identificador interno. | [Modelo de datos](tecnica/02-modelo-de-datos.md#claves-alternativas) |
| **Cascada** (*cascade*) | Lo que le pasa a los registros hijos cuando se borra, comparte o reasigna el padre. | [Modelo de datos](tecnica/02-modelo-de-datos.md#relaciones-y-cascadas) |
| **Dueño** (*owner*) | El usuario o equipo al que pertenece un registro. Decide quién lo ve. | `ownerid` |
| **Compartir** (*share*) | Darle a un usuario acceso a un registro que no es suyo. Así el localizador ve la cuenta y el cliente de su cartera. | [Seguridad](tecnica/03-seguridad.md) |
| **Bulk delete** | Borrado en masa programado que Dataverse corre todos los días para eliminar datos vencidos. | [`paso10_borrado_programado.py`](../scripts/seguridad/paso10_borrado_programado.py); [Ciclo de vida](tecnica/08-ciclo-de-vida.md) |
| **Solution** | El paquete que contiene todos los componentes de la solución para instalarla en otro ambiente. La principal es `cm_core_cobranzamovil`. | [`solutions/`](../solutions/); [Instalación](tecnica/09-instalacion-y-despliegue.md) |
| **Opciones de lista** | Los valores de las listas desplegables (tipo de Especial, motivos de cierre, mensajes rápidos) que el Admin puede editar. | Tabla `opcionlista`; pantalla [`scrListas.pa.yaml`](../apps/backoffice-canvas/Src/scrListas.pa.yaml) |
