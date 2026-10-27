# App de la oficina

Este documento describe la *canvas app* web que usa la oficina: BackOffice, Solicitante, Supervisor y Admin trabajan en la misma app y cada rol ve solo su menú. Cubre cómo se determina el rol, qué hace cada pantalla y qué *Custom APIs* (mensajes propios de Dataverse que la app llama como una función) y *plugins* (código C# que corre en el servidor ante cada operación) intervienen, los componentes reutilizables y los patrones que permiten trabajar con más de 2.000 filas. Está pensado para quien tiene que modificarla o diagnosticarla. La seguridad real (qué puede leer y escribir cada rol) la imponen Dataverse y los *plugins*: ver [Seguridad](03-seguridad.md) y [Plugins y Custom APIs](04-plugins-y-custom-apis.md).

## Cómo funciona

### Propósito

| Rol | Para qué usa la app |
|---|---|
| BackOffice | Transcribir al *core* las interacciones pendientes y revisar las ya transcritas (arqueo del día) |
| Solicitante | Pedir asignaciones Especiales y seguir sus solicitudes |
| Supervisor | Aprobar o rechazar Especiales, reasignar, mirar la cartera de cada localizador y el historial de Especiales |
| Admin | Carga diaria, monitoreo, reconciliación, alta de cuentas, cierres, agentes, datos maestros y listas |

La app es de escritorio (1366 × 768) y se adapta a pantallas angostas: `nfMovil = App.ActiveScreen.Width < 920` oculta el menú lateral y muestra una barra con botón de menú. Con el menú abierto (`gblNavMovilAbierta`), cada pantalla muestra una capa transparente `recCierraNav…` a pantalla completa, justo antes de `cmpNav…` en el orden de controles (debajo del menú y encima del resto): tocarla cierra el menú. Su nombre interno es `sanic_appcmbackofficecanvas_d0162` y su nombre visible «App - CM - Backoffice Canvas». No usa perfil *offline*: trabaja siempre conectada. Todas sus fuentes de Dataverse tienen límite de 2.000 filas (`MaxGetRowsCount` en el `.msapr`).

### Cómo se determina el rol del usuario

El rol sale de la seguridad real de Dataverse, sin tabla propia ni nombres de equipos:

1. `gblUsuarioDV` (fórmula con nombre en [`App.pa.yaml`](../../apps/backoffice-canvas/Src/App.pa.yaml)) busca al usuario en `Users` por `'Azure AD Object ID' = User().EntraObjectId`. Se usa el id de Entra y no el correo porque `User().Email` devuelve el UPN, que puede no coincidir con el correo principal.
2. `gblRol` es una variable que pone el `OnVisible` de la pantalla inicial [`scrCarga`](../../apps/backoffice-canvas/Src/scrCarga.pa.yaml) con la *Custom API* [`sanic_cm_MisRoles`](04-plugins-y-custom-apis.md#sanic_cm_misroles): los roles efectivos del usuario (directos y de cualquier equipo, también de grupo de Entra ID) sin el prefijo `SR | CM | ` y separados por `|`, por ejemplo `Admin|BackOffice`. `Servicio - Cobranza` no cuenta como perfil. La misma llamada devuelve `Version` (versión de la *solution*), que va a `gblVersion` para el pie del menú.
3. Las comprobaciones usan `"Admin" in gblRol`, que no distingue mayúsculas.

Mientras la API no responde, `gblRolListo` es falso: `scrCarga` muestra «Cargando tus permisos…» y el temporizador `tmrCarga` (cada 0,5 s) pasa a Inicio recién cuando resolvió, así el `OnVisible` de [`scrInicio`](../../apps/backoffice-canvas/Src/scrInicio.pa.yaml) calcula los KPIs con el rol. Como tope, a los 30 s entra igual con error. En Inicio, «Cargando tus permisos…» mientras no resolvió; «Sin acceso» si resolvió vacío; «No se pudo verificar tu acceso» con el detalle (`gblRolError`) si la API falló. El menú muestra «Cargando…» en el lugar del rol.

### Menú por rol

El menú vive en [`cmpNavLateral`](../../apps/backoffice-canvas/Src/Components/cmpNavLateral.pa.yaml): una tabla literal con `Orden`, `Icono`, `Titulo`, `Ruta` y `Roles`. Una fila se muestra si alguno de sus `Roles` está en `gblRol`. El catálogo es local al componente porque leer una fórmula con nombre de la app desde el componente falla en la primera evaluación.

| Orden | Opción | Pantalla | Roles |
|---|---|---|---|
| 1 | Inicio | `scrInicio` | Backoffice, Solicitante, Supervisor, Admin |
| 2 | Por transcribir | `scrTranscripcion` | Backoffice |
| 3 | Transcritas | `scrTranscritas` | Backoffice |
| 4 | Nueva solicitud | `scrNuevaSolicitud` | Solicitante |
| 5 | Mis solicitudes | `scrMisSolicitudes` | Solicitante |
| 6 | Aprobaciones | `scrAprobaciones` | Supervisor |
| 7 | Reasignar | `scrReasignar` | Supervisor, Admin |
| 8 | Cartera por localizador | `scrCarteraLocalizador` | Supervisor |
| 9 | Historial de Especiales | `scrHistorialEspeciales` | Supervisor |
| 10 | Monitoreo de carga | `scrMonitoreo` | Admin |
| 11 | Cargar archivo | `scrCargaArchivo` | Admin |
| 12 | Alta de cuenta | `scrAltaCuenta` | Admin |
| 13 | Reconciliación | `scrReconciliacion` | Admin |
| 14 | Cierres | `scrCierres` | Admin |
| 15 | Localizadores y usuarios | `scrAgentes` | Admin |
| 16 | Datos maestros | `scrMaestros` | Admin |
| 17 | Listas | `scrListas` | Admin |

⚠️ El menú decide qué se **ve**, no qué se **puede hacer**: las pantallas no repiten el control de rol. Lo que protege los datos son los privilegios de cada rol y los *plugins* ([Seguridad](03-seguridad.md)).

### Pantallas

#### Inicio — [`scrInicio`](../../apps/backoffice-canvas/Src/scrInicio.pa.yaml)

Indicadores y accesos según el rol (`cmpKpi`). Los conteos van directo al origen con `CountIf` y `Sum`, que delegan en Dataverse y son exactos hasta 50.000 filas; una colección con `CountRows` se cortaría en el límite de filas.

| Rol | Indicadores |
|---|---|
| BackOffice | En cola (pendientes de transcribir), con pago, transcritas hoy por el usuario |
| Solicitante | Sus Especiales pendientes, aprobadas activas (mismo criterio que el filtro «Aprobada» de Mis solicitudes) y rechazadas |
| Supervisor | Especiales pendientes de aprobación |
| Admin | Carga de hoy, Diarias fuera de cartera, agentes activos sin usuario (sin correo derivado) |

El cobrado de hoy solo se calcula para BackOffice, Admin y Supervisor: el Solicitante no tiene lectura de Pagos y la consulta daría error 403.

#### Por transcribir — [`scrTranscripcion`](../../apps/backoffice-canvas/Src/scrTranscripcion.pa.yaml)

Cola FIFO de interacciones pendientes de grabar en el *core*, con filtro por localizador. Por cada interacción muestra cliente, CIF, producto, número de tarjeta, comentario, ubicación, pagos (con aviso «⚠ REPETIDO» si `sanic_cm_pg_reciborepetido`) y la actualización de datos con el «antes» (teléfonos y direcciones actuales) y el «nuevo».

- La persona escribe el código de grabación del *core* y toca «✓ y siguiente» (`btnGrabarTrx`). El único `Patch` pone Estado de grabación = Grabado y el código en mayúsculas. Si falla, la interacción no sale de la cola.
- [`GrabacionInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/GrabacionInteraccion.cs) pone la fecha y el usuario de grabación, exige el código y rechaza grabar una interacción que otra persona ya grabó.
- La actualización de datos se busca primero vinculada a la interacción y, si no está, entre las que entraron sin vincular (`sanic_cm_ad_sinvincular`) de la misma cuenta. Si tampoco, avisa que los datos no llegaron.
- Enter en el código no graba: Power Apps no distingue Enter, Tab ni clic afuera, así que el `OnChange` solo pasa el foco al botón «✓ y siguiente». La ayuda de teclado (`lblHintTrx`) nombra el botón con su texto real.

#### Transcritas — [`scrTranscritas`](../../apps/backoffice-canvas/Src/scrTranscritas.pa.yaml)

Arqueo de un día: lo grabado por cualquier BackOffice, por defecto hoy y el usuario actual. Filtros por quién grabó y búsqueda por CIF, cliente, localizador, últimos dígitos o número de interacción (en memoria). Los pagos del día se traen por tramos (ver [Patrones](#patrones)); las interacciones grabadas del día, en una sola consulta. Solo lectura.

#### Nueva solicitud — [`scrNuevaSolicitud`](../../apps/backoffice-canvas/Src/scrNuevaSolicitud.pa.yaml)

El Solicitante busca la cuenta (últimos 4 exactos, o CIF o nombre que empiezan con el texto), elige el tipo de Especial (lista administrable «Tipo de Especial»), los montos de retiro y el motivo. Envía con la *Custom API* `sanic_cm_SolicitarEspecial` ([`SolicitarEspecial.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/SolicitarEspecial.cs)), que crea la Especial pendiente, reactiva una cerrada o avisa quién pidió la activa. Se usa una API y no un `Patch` porque el Solicitante no ve las Especiales de otros y la clave (cuenta, tipo) lo frenaría sin explicación.

#### Mis solicitudes — [`scrMisSolicitudes`](../../apps/backoffice-canvas/Src/scrMisSolicitudes.pa.yaml)

Las Especiales que pidió el usuario, con filtros por fechas (último mes por defecto), estado, localizador, CIF y cuenta. Solo lectura.

- Las fechas filtran por `'[AS] Fecha asignada'` (*DateOnly*, la que se muestra como «Solicitada»), no por `Created On`: al reactivar una Especial, [`SolicitarEspecial.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/SolicitarEspecial.cs) le pone la fecha de hoy y así cae en el rango de su fecha nueva. La comparación de una columna de solo fecha contra fechas delega en Dataverse.

#### Aprobaciones — [`scrAprobaciones`](../../apps/backoffice-canvas/Src/scrAprobaciones.pa.yaml)

Cola FIFO de Especiales pendientes, con filtros por fecha, solicitante y búsqueda. El Supervisor aprueba eligiendo el localizador o rechaza con motivo.

- Aprobar escribe estado Aprobado, agente y resolutor. El botón se deshabilita si hay texto en «Motivo de rechazo» (`lblAyudaAprobarAprb` pide borrarlo), para que ese texto no se pierda sin aviso. [`AprobacionEspecial.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/AprobacionEspecial.cs) exige Supervisor o Admin y [`RepartoAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RepartoAsignacion.cs) (asíncrono) pasa el dueño al usuario del agente: la asignación baja al teléfono en la próxima sincronización.
- Rechazar escribe estado Rechazada, motivo, resolutor y además **cierra** la asignación; si quedara activa, bloquearía volver a pedirla.

#### Reasignar — [`scrReasignar`](../../apps/backoffice-canvas/Src/scrReasignar.pa.yaml)

Cambia el localizador de una asignación. El Supervisor ve Especiales activas y aprobadas; el Admin, además, Incobrables activas. Las Diarias no se reasignan: las define el archivo diario. Solo escribe `'[AS] Agente'`, porque [`ColumnasAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ColumnasAsignacion.cs) le rechaza al Supervisor cualquier otra columna. El buscador de localizadores solo trae agentes activos con usuario vinculado. Carga por tramos.

#### Cartera por localizador — [`scrCarteraLocalizador`](../../apps/backoffice-canvas/Src/scrCarteraLocalizador.pa.yaml)

Lista de localizadores con conteos por tipo (`CountIf`) y una alerta roja si superan `nfUmbralCarga` asignaciones activas (500, criterio de operación definido en `App.Formulas`). Al tocar uno, carga su cartera por tramos. «Incluir inactivos» muestra localizadores desactivados que todavía tienen cartera, para reasignarla. Solo lectura. El aviso «Sin localizadores» solo sale si la galería está vacía y además el conteo delegable `varNAgCpl` (mismo criterio que la galería, recalculado al cambiar «Incluir inactivos» y en ↻ Actualizar) es 0, o hay texto de búsqueda: así no aparece mientras carga o si falla la consulta. La carga de la cartera va envuelta en `IfError`; si falla, `colAsigCpl` queda vacía y el panel dice que no se pudo cargar (lo distingue de «sin asignaciones» con el `CountIf` previo `varNAsigCpl`).

#### Historial de Especiales — [`scrHistorialEspeciales`](../../apps/backoffice-canvas/Src/scrHistorialEspeciales.pa.yaml)

Especiales resueltas (aprobadas o rechazadas), con filtros por estado, vigencia, días, localizador, solicitante y búsqueda. Cada filtro entra a la consulta como un par de constantes («Todas» = los dos valores; «Aprobadas» = el mismo valor dos veces), así la consulta tiene siempre la misma forma y delega entera. Solo lectura.

#### Monitoreo de carga — [`scrMonitoreo`](../../apps/backoffice-canvas/Src/scrMonitoreo.pa.yaml)

Si hay carga de hoy, cuántas filas de carga, cuentas y clientes hay. Hace `Refresh` de la carga diaria al entrar: la fórmula `nfUltimaCarga` lee la tabla en memoria y, sin `Refresh`, seguiría mostrando la carga anterior.

#### Cargar archivo — [`scrCargaArchivo`](../../apps/backoffice-canvas/Src/scrCargaArchivo.pa.yaml)

El Admin sube el archivo del banco (un `.csv` de hasta 50 MB, el mismo tope que `sanic_cm_PartirCarga`) y la pantalla crea y sigue los trabajos de importación de Smelter (herramienta de importación de terceros que corre en el ambiente): prueba en seco obligatoria, después carga real. Con `nfCargaParalela`, la *Custom API* `sanic_cm_PartirCarga` ([`PartirCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PartirCarga.cs)) parte el trabajo en uno de padres y `nfPartesCarga` (3) partes por CIF; con `nfValidarLargo`, la misma API mide antes el largo de cada valor. El temporizador `tmrAvanceCar` pone los trabajos en cola y sigue su avance. Cuando cada trabajo termina, el *plugin* [`PurgaArchivoCarga`](../../plugins/Sanic.CobranzaMovil.Plugins/PurgaArchivoCarga.cs) borra su archivo. El detalle del proceso está en [Carga diaria](07-carga-diaria.md).

#### Alta de cuenta — [`scrAltaCuenta`](../../apps/backoffice-canvas/Src/scrAltaCuenta.pa.yaml)

El Admin da de alta una cuenta como una importación de una fila de Smelter, con la *Custom API* `sanic_cm_AltaCuenta` ([`AltaCuenta.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/AltaCuenta.cs)): prueba en seco y después carga real, con el temporizador `tmrAvanceAlt`. La app controla en vivo los dígitos y el dígito verificador (`LuhnOk()`); el servidor vuelve a validar todo y arma la fila. El número de tarjeta se escribe en un cuadro en modo contraseña («Mostrar» lo descubre), va directo a la API y no pasa por ninguna variable.

#### Reconciliación — [`scrReconciliacion`](../../apps/backoffice-canvas/Src/scrReconciliacion.pa.yaml)

Cierra las Diarias activas que no vinieron en la última carga («fuera de cartera»). El corte es el **día** de la última carga. El cierre se deshabilita mientras haya una carga en curso (un trabajo real de Smelter en cola, procesando o sin estado en las últimas 12 h) y falla cerrado si no puede leer los trabajos. Si se cerraría más del 30 % de las Diarias, hay que escribir la cantidad para confirmar. Con `nfCierreServidor` cierra por lotes con la *Custom API* `sanic_cm_CerrarFueraDeCartera` ([`CerrarFueraDeCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CerrarFueraDeCartera.cs)); ver [Patrones](#patrones). El porqué del ciclo está en [Ciclo de vida de los datos](08-ciclo-de-vida.md).

#### Cierres — [`scrCierres`](../../apps/backoffice-canvas/Src/scrCierres.pa.yaml)

El Admin cierra manualmente Incobrables y Especiales aprobadas activas, con un motivo de la lista administrable («Motivo de cierre Incobrable» o «Motivo de cierre Especial») y un detalle opcional. Un `Patch` por asignación: estado inactivo, motivo y fecha de cierre. Carga por tramos.

#### Localizadores y usuarios — [`scrAgentes`](../../apps/backoffice-canvas/Src/scrAgentes.pa.yaml)

Alta de agentes, activar o desactivar, y vincular o desvincular el usuario de Entra. La app solo escribe el lookup `'[AG] Usuario'`; el correo que usa el teléfono para identificar al agente lo deriva [`EmailDesdeUsuario.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/EmailDesdeUsuario.cs). Al vincular o desvincular, [`RepartoPorAgente.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RepartoPorAgente.cs) mueve sus asignaciones al nuevo usuario o al equipo «Cartera sin cobrador». Desvincular es un botón y no el `OnChange` de un combo, porque ese evento se dispara al abrir la pantalla.

#### Datos maestros — [`scrMaestros`](../../apps/backoffice-canvas/Src/scrMaestros.pa.yaml)

Búsqueda de clientes, ficha con teléfonos, direcciones y cuentas, edición de departamento, distrito y trabajo, alta de teléfonos y direcciones, y alta de clientes con control de CIF duplicado. El nombre es de solo lectura: lo escribe la carga diaria y lo pisaría.

#### Listas — [`scrListas`](../../apps/backoffice-canvas/Src/scrListas.pa.yaml)

Ver [Listas administrables](#listas-administrables).

### Componentes reutilizables

| Componente | Propiedades de entrada | Qué hace |
|---|---|---|
| [`cmpNavLateral`](../../apps/backoffice-canvas/Src/Components/cmpNavLateral.pa.yaml) | `RutaActiva` | Menú por rol, marca de la app, usuario, rol y versión desplegada (`gblVersion`) |
| [`cmpEncabezado`](../../apps/backoffice-canvas/Src/Components/cmpEncabezado.pa.yaml) | `Titulo`, `Subtitulo` | Título de la pantalla; en pantallas angostas, barra con botón de menú |
| [`cmpKpi`](../../apps/backoffice-canvas/Src/Components/cmpKpi.pa.yaml) | `Etiqueta`, `Valor`, `Detalle`, `EsHero` | Tarjeta de indicador |
| [`cmpVacio`](../../apps/backoffice-canvas/Src/Components/cmpVacio.pa.yaml) | `Icono`, `Titulo`, `Mensaje` | Estado vacío (cola vacía, sin resultados, sin acceso) |

Los cuatro tienen `AccessAppScope: true`: leen los tokens de la app (`gblC` colores, `gblE` medidas). La versión llega al menú por la variable `gblVersion`, que pone el `OnVisible` de [`scrCarga`](../../apps/backoffice-canvas/Src/scrCarga.pa.yaml) con la propiedad `Version` de la respuesta de `sanic_cm_MisRoles`: es el número de versión de la *solution* `cm_core_cobranzamovil` instalada, leído en el servidor. Si la API no la devuelve o falla, el menú muestra «—». Por eso la versión no se escribe a mano en la app: cambia sola al importar una *solution* con número mayor.

[`App.Formulas`](../../apps/backoffice-canvas/Src/App.pa.yaml) también define funciones compartidas: `Moneda()`, `CuentaMasc()`, `TextoError()` (extrae el `message` del JSON de error de una *Custom API*), `RetiroOk()`, `SoloDigitos()` y `LuhnOk()`.

### Cómo se ve el PAN

Todas las pantallas muestran `•••• 1234` (`CuentaMasc('Últimos 4')`). El número completo solo aparece en dos lugares, para el rol BackOffice, que lo necesita para transcribir al *core*:

| Pantalla | Control | Comportamiento |
|---|---|---|
| Por transcribir | `lblCuentaGrandeTrx` | PAN completo de la interacción seleccionada; tocarlo lo copia al portapapeles (`Copy()`, el único de la app) |
| Transcritas | `lblVCuentaTsc` (panel de detalle) | PAN completo de la interacción seleccionada |

La fórmula es la misma en los dos:

```
If("Backoffice" in gblRol,
    Coalesce(
        IfError(If(IsBlank(cuentaId), Blank(),
            LookUp('CM | TBL | PAN de cuentas', Cuenta.'CM | TBL | Cuenta' = cuentaId).PAN), Blank()),
        CuentaMasc(ult4)),
    CuentaMasc(ult4))
```

- Un `LookUp` a la bóveda por registro mostrado, nunca una colección con PAN.
- Sin rol, sin permiso de lectura o sin fila en la bóveda: muestra el enmascarado; nunca recurre a otra columna.
- Sin cuenta (borrada por el ciclo de vida) no consulta: una clave vacía buscaría «cuenta vacía».

La comprobación de rol en la pantalla es de presentación. La barrera real es que, entre los roles de la oficina, solo BackOffice tiene lectura sobre la bóveda ([Seguridad](03-seguridad.md)).

### Listas administrables

La pantalla [`scrListas`](../../apps/backoffice-canvas/Src/scrListas.pa.yaml) edita `sanic_cm_tbl_opcionlista` («CM | TBL | Opciones de lista»). Cada opción tiene lista, texto (hasta 100 caracteres), orden y activa.

| Lista | Dónde se usa | Respaldo si está vacía |
|---|---|---|
| Tipo de Especial | Nueva solicitud; la primera activa viene elegida | Sí, en el `OnVisible` |
| Motivo de cierre Incobrable | Cierres | Sí |
| Motivo de cierre Especial | Cierres | Sí |
| Mensaje rápido | Teléfono, Registrar visita ([App del localizador](05-app-localizador.md)); llega en la próxima sincronización | Sí |

- **No hay borrado, se desactiva.** Lo elegido se guarda como texto en la asignación o la interacción: renombrar o desactivar no cambia el historial. Nadie tiene privilegio Delete sobre la tabla.
- No deja una lista sin ninguna activa: la app mostraría los valores de respaldo y confundiría.
- Subir y bajar intercambian el `[OL] Orden` con la vecina; una opción nueva va al final (máximo + 10).
- Solo Admin y Servicio tienen Create y Write (privilegios de los roles, que viajan en la *solution*; ver [Seguridad](03-seguridad.md#matriz-de-privilegios-por-tabla)).

### Patrones

#### Carga por tramos para pasar el límite de 2.000 filas

Cada consulta que se copia a una colección se corta en el límite de filas de la app (2.000). Las pantallas que pueden superar ese número traen los datos en 20 consultas, una por tramo, más una para los registros sin tramo. El tramo (0..19) lo pone [`TramoCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/TramoCartera.cs).

```
ForAll(Sequence(20, 0) As T, Collect(colCierresCie,
    AddColumns(Filter('CM | TBL | Asignacions', … && '[AS] Tramo' = T.Value), …)));
```

| Pantalla | Qué trae por tramos |
|---|---|
| Transcritas | Pagos del día (hasta unos 30.000) |
| Reasignar | Asignaciones reasignables |
| Cartera por localizador | La cartera activa del localizador elegido |
| Cierres | Incobrables y Especiales aprobadas activas |

⚠️ El tramo va **constante** en cada consulta (`T.Value`). Un `If`, un `DateAdd` o un campo de colección dentro del `Filter` lo vuelve no delegable y Power Apps trae una sola página cortada, sin error.

Cuando no se usan tramos, la pantalla ordena en el servidor para que el corte deje afuera lo menos importante y avisa con un conteo exacto. Por transcribir ordena por fecha de captura ascendente (entran las más antiguas) y compara la colección contra `CountIf`; si la cola vino cortada, muestra «se muestran las N más antiguas».

#### Delegación

*Delegación* es que Power Apps traduzca la fórmula a una consulta que resuelve Dataverse; lo que no delega se evalúa sobre la primera página. Reglas que sigue la app:

- `CountIf` y `Sum` directo al origen para indicadores exactos; nunca `CountRows` sobre una colección. `CountIf(tabla, true)` en lugar de `CountRows(tabla)`, que usa un valor en caché.
- Choice y booleanos por valor, fechas y lookups contra variables. Las partes `IsBlank(var) ||` o `var = "x" &&` no tocan columnas y se resuelven antes de enviar.
- **Sin `With` dentro del predicado**: una variable de `With` corta la delegación y la consulta sale sin filtro.
- `IsBlank()` sobre un lookup o una opción dentro de un `Filter` rompe la delegación de toda la consulta.
- Buscadores: con menos de 2 caracteres se busca un centinela (`"§§"`) que no coincide con nada, para no traer la tabla entera.
- Búsquedas por CIF, cliente o últimos 4 de una tabla relacionada se hacen en dos pasos: primero las cuentas que coinciden (hasta 50) y después una consulta por cuenta (Mis solicitudes, Aprobaciones, Historial de Especiales).
- Los filtros se aplican en el servidor y cada cambio vuelve a consultar: el límite de 2.000 filas aplica a lo que coincide, no a todo el historial.
- Los datos de la cuenta se leen navegando el lookup (`ThisRecord.'[IN] Cuenta'.'Nombre Cliente'`), que resuelve el servidor, en lugar de un catálogo de cuentas en memoria que se cortaría.

#### Temporizadores para procesos por lotes

| Temporizador | Pantalla | Qué hace |
|---|---|---|
| `tmrCierreRec` | Reconciliación | Cada 300 ms, si no hay un llamado en vuelo (`varEnVueloRec`), llama a `sanic_cm_CerrarFueraDeCartera` con `Lote: 250`, el corte y las esperadas. Suma las cerradas y sigue mientras `Restantes > 0` |
| `tmrAvanceCar` | Cargar archivo | Espera 20 s y pone el trabajo de Smelter en cola; después sondea cada 3 s los primeros 20 min y cada 30 s después (tope ≈ 1 h 30 min). Lanza las partes solo si los padres terminan bien |
| `tmrAvanceAlt` | Alta de cuenta | Igual que el anterior para un trabajo de una fila, cada 3 s |
| `tmrCarga` | Carga (arranque) | Espera a que resuelva el rol |

Por qué lotes de 250: un *plugin* tiene un límite de 2 minutos por operación. La API cierra hasta 500 por llamado (250 por defecto) y la app repite. La API además valida que el corte y la cantidad esperada coincidan con lo que vio la pantalla: si llegó otra carga o aparecieron más, no cierra nada.

Por qué la espera de 20 s antes de poner un trabajo en cola: el disparador de Smelter evalúa «estado = En cola» sobre el registro como está cuando arranca la corrida. Con el alta, el archivo y el estado escritos seguidos, los tres eventos verían el estado y el trabajo se procesaría tres veces en paralelo.

Los temporizadores de proceso tienen `AutoPause = false`: siguen andando si el usuario cambia de pantalla. Cerrar o recargar la app los corta.

#### Estados de carga visibles

- Cada pantalla con lectura lenta tiene una variable `varCargandoX` que deshabilita filtros y botones y muestra «⏳ Cargando…».
- Los procesos largos muestran un velo («⏳ Cerrando…») y deshabilitan el botón mientras duran.
- Los botones Actualizar avisan «✓ … actualizado» solo si la lectura salió bien.
- La lógica de carga vive en botones ocultos (`Visible: =false`, por ejemplo `btnCargarTsc`) que `OnVisible`, los filtros y Actualizar invocan con `Select()`, para tener un solo lugar con esa fórmula.

#### Manejo de errores

En una cadena de acciones separadas por `;`, Power Fx sigue después de un error e `IfError` solo ve el último paso. Por eso cada acción que puede fallar guarda su resultado en una variable y el error se lee aparte:

```
Set(varResp, Environment.sanic_cm_SolicitarEspecial({ … }));
Set(varErr, IfError(If(IsBlank(varResp.Resultado), "el servidor no devolvió resultado", Left(Text(varResp.Resultado), 0)),
    Coalesce(FirstError.Message, "sin detalle")));
If(IsBlank(varErr), /* éxito */, Notify("No se pudo …: " & varErr, NotificationType.Error))
```

`""` significa que salió bien. Los `Patch` simples usan `IfError(Patch(…), Notify(error), /* éxito */)` y, si fallan, no sacan el registro de la cola ni limpian el formulario. Después de una *Custom API* que escribe por fuera de las fuentes de la app, la pantalla hace `Refresh` de la tabla afectada.

#### Interruptores de funciones del servidor

`App.Formulas` tiene tres interruptores: `nfCargaParalela`, `nfCierreServidor` y `nfValidarLargo`, los tres en `true`. Cada uno habilita una fórmula que llama a una *Custom API* o a un parámetro nuevo. Existen porque la fuente `Environment` del `.msapr` guarda una foto de las *Custom APIs*: si no conoce la API, la fórmula queda inerte sin aviso. Con el interruptor en `false`, la pantalla usa el camino sin API: la carga real en un solo trabajo, el cierre con un `Patch` masivo desde la app (cortado en 2.000 filas) o la carga sin medir antes el largo de los valores.

## Piezas y archivos

| Pieza | Archivo |
|---|---|
| Fuentes de la app | [`apps/backoffice-canvas/Src/`](../../apps/backoffice-canvas/Src/) |
| Fuentes de datos y recursos | [`sanic_appcmbackofficecanvas_d0162_DocumentUri.msapr`](../../apps/backoffice-canvas/sanic_appcmbackofficecanvas_d0162_DocumentUri.msapr) |
| Rol, tokens, funciones e interruptores | [`App.pa.yaml`](../../apps/backoffice-canvas/Src/App.pa.yaml) |
| Menú por rol | [`cmpNavLateral.pa.yaml`](../../apps/backoffice-canvas/Src/Components/cmpNavLateral.pa.yaml) |
| *Custom APIs* que llama la app (`sanic_cm_MisRoles` en la pantalla de carga, `sanic_cm_SolicitarEspecial` en Nueva solicitud, `sanic_cm_AltaCuenta` en Alta de cuenta, `sanic_cm_PartirCarga` en Cargar archivo, `sanic_cm_CerrarFueraDeCartera` en Reconciliación) | [`MisRoles.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/MisRoles.cs), [`SolicitarEspecial.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/SolicitarEspecial.cs), [`AltaCuenta.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/AltaCuenta.cs), [`PartirCarga.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/PartirCarga.cs), [`CerrarFueraDeCartera.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/CerrarFueraDeCartera.cs) |
| *Plugins* que reaccionan a sus escrituras | [`GrabacionInteraccion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/GrabacionInteraccion.cs), [`AprobacionEspecial.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/AprobacionEspecial.cs), [`ColumnasAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/ColumnasAsignacion.cs), [`RepartoAsignacion.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RepartoAsignacion.cs), [`RepartoPorAgente.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/RepartoPorAgente.cs), [`EmailDesdeUsuario.cs`](../../plugins/Sanic.CobranzaMovil.Plugins/EmailDesdeUsuario.cs) |
| Tabla de listas | `sanic_cm_tbl_opcionlista`, en la *solution* ([Modelo de datos](02-modelo-de-datos.md#sanic_cm_tbl_opcionlista--cm--tbl--opción-de-lista)); opciones iniciales en [Instalación, paso 10](09-instalacion-y-despliegue.md#10-opciones-iniciales-de-las-listas) |
| Despliegue | Importar la *solution* ([Instalación y despliegue](09-instalacion-y-despliegue.md#actualizar-a-una-versión-nueva)) |
| Pruebas de punta a punta | [`tests/backoffice-canvas/`](../../tests/backoffice-canvas/) ([Pruebas](10-pruebas.md)) |

## Cómo se modifica

### Cómo modificar la app

Igual que la app del localizador: se edita en **Power Apps Studio** dentro de la *solution* `cm_core_cobranzamovil` del ambiente de desarrollo y llega a los demás ambientes con una versión nueva de la *solution* ([Instalación y despliegue](09-instalacion-y-despliegue.md#actualizar-a-una-versión-nueva)). Pasos:

1. `make.powerapps.com`, ambiente de desarrollo → **Solutions** → `cm_core_cobranzamovil` → **Apps** → «App - CM - Backoffice Canvas» → **Edit**.
2. Hacer el cambio. Respetar: nombres de control únicos en toda la app, `Radius*` solo en `GroupContainer`, sin `AccessibleLabel` en `Classic/Button`. No publicar con fórmulas marcadas en rojo.
3. **Save** y **Publish**.
4. Exportar la *solution* **subiendo su número de versión** en **Export this solution** (es el número que muestra la app; no hay que tocar nada en ella) e importarla en el destino ([Instalación, pasos 1 y 2](09-instalacion-y-despliegue.md#1-conseguir-el-paquete-de-la-solution)).
5. En el destino, verificar en el pie del menú que «Versión» muestre la versión nueva de la *solution* ([Pruebas](10-pruebas.md#0-verificación-previa-pre), PRE-07).

6. Actualizar [`apps/backoffice-canvas/Src/`](../../apps/backoffice-canvas/Src/) en el repositorio con los `*.pa.yaml` de una copia descargada de la app, como explica [Mantener `Src/` del repositorio al día](05-app-localizador.md#mantener-src-del-repositorio-al-día) (incluye por qué hay que revisar la diferencia antes de hacer *commit*).

El ejemplo de YAML para pegar en Studio está en [App del localizador](05-app-localizador.md#cómo-modificar-la-app). Al agregar una fuente de datos, revisar que tome el límite de la app (2.000): **Settings** → **General** → **Data row limit**.

### Agregar una columna o una tabla

1. Crear la columna o tabla en Dataverse, dentro de la *solution* ([Modelo de datos](02-modelo-de-datos.md#agregar-una-columna)).
2. En Studio, panel **Data** → la tabla → **⋯** → **Refresh** para una columna nueva, o **Add data** para una tabla nueva. Sin este paso, la fórmula que nombra la columna no la encuentra.
3. Usarla, guardar y publicar.

Las pantallas de esta app cuentan con un límite de 2.000 filas por consulta; en Studio se ve en **Settings** → **General** → **Data row limit** (Límite de filas de datos).

### Agregar o cambiar una Custom API

Las *Custom APIs* se llaman a través de la fuente `Environment`, que guarda una foto de las APIs y sus parámetros. Para que la app conozca una API nueva o un parámetro nuevo, en Studio, **Data** → `Environment` → **⋯** → **Refresh**, guardar y publicar. Mientras la API no esté instalada en todos los ambientes, proteger la fórmula con un interruptor en `App.Formulas`, como `nfCierreServidor`. Los id van como texto: la fuente `Environment` declara los `Guid` de la API como texto (`Text(varCtaNva.'CM | TBL | Cuenta')`).

### Agregar una pantalla al menú

1. Crear `scrNueva.pa.yaml` con `cmpEncabezado` y `cmpNavLateral` (`RutaActiva` con la ruta nueva; `Visible: =!nfMovil || gblNavMovilAbierta`).
2. Agregar una fila a la tabla de `galNavCmp.Items` en `cmpNavLateral` con `Orden`, `Icono`, `Titulo`, `Ruta` y `Roles`.
3. Agregar la ruta al `Switch` de `galNavCmp.OnSelect`.
4. Dar los privilegios en el rol de Dataverse: el menú no protege nada.

### Agregar un rol

Crear el rol `SR | CM | <perfil>` y sumar `<perfil>` a la fila del menú (`Roles`). `sanic_cm_MisRoles` lo devuelve solo, sea directo o por equipo. Ver [Seguridad](03-seguridad.md#agregar-un-rol-nuevo).

### Agregar una lista administrable

Agregar el valor al *choice* `[OL] Lista` de la tabla `sanic_cm_tbl_opcionlista` (en la *solution*: **Tables** → la tabla → **Columns** → `[OL] Lista` → agregar la opción), refrescar la fuente en las dos apps desde Studio (**Data** → la tabla → **⋯** → **Refresh**), sumar la entrada a `colListasLst` y al `Switch` de `varEnumLst` en `scrListas`, y leerla en la pantalla que la usa con valores de respaldo.

## Trampas y errores conocidos

- ⚠️ **El ComboBox clásico no muestra registros de Dataverse en esta app** (filas en blanco, también con `Users`) y al abrir puede disparar su `OnChange`. Para elegir un registro se usa buscador + galería; los `Classic/ComboBox` y `Classic/DropDown` que hay se usan solo sobre tablas de textos (tipo de Especial, motivos de cierre, tipos de teléfono y dirección).
- ⚠️ **Fórmula inerte sin aviso.** Una columna que la foto del esquema no conoce, una *Custom API* que la fuente `Environment` no conoce o una comparación de tipos incompatibles (por ejemplo, un lookup contra un registro guardado en una variable) dejan inerte la fórmula, y a veces todo el `OnVisible`, sin error visible. Studio la marca en rojo: revisar que no quede ninguna antes de publicar.
- **Límite de filas:** 2.000 por consulta copiada a una colección. `AllItemsCount` de una galería cuenta solo la página cargada; sirve para saber si hay cero resultados, no para totales.
- **Fechas DateOnly:** `'Última carga'` se compara día contra día. `[CD] FechaCarga` comparada contra `Today()` viaja como `ge <hoy>T06:00Z` y da 0 el día de la carga; Monitoreo usa `DateAdd(Today(), -TimeZoneOffset(Today()), TimeUnit.Minutes)`.
- **Corte por día, no por fecha y hora:** en Reconciliación, una Diaria cargada segundos antes del último registro no está fuera de cartera.
- **Escrituras por fuera de la app:** lo que escriben las *Custom APIs* o Smelter no aparece hasta hacer `Refresh` de la fuente.
- **Los temporizadores mueren al cerrar la app.** Si el Admin cierra Cargar archivo antes de que terminen los padres, las partes quedan sin estado; el historial de la pantalla ofrece relanzarlas o descartarlas, y mientras tanto conservan su archivo (con los números completos) y Reconciliación las cuenta como carga en curso durante 12 h.
- **El nombre de los roles es parte del contrato** con `gblRol` (prefijo `SR | CM | ` y perfil; ver [Cómo se determina el rol](#cómo-se-determina-el-rol-del-usuario)). El nombre de los equipos no.
- **`DropDown` aplica `Default` una sola vez:** si sus opciones se cargan después, hay que hacer `Reset()` del control.
- Editar solo en el ambiente de desarrollo, dentro de la *solution*, y llevar el cambio con una versión nueva de la *solution*: ver [App del localizador](05-app-localizador.md#despliegue) e [Instalación y despliegue](09-instalacion-y-despliegue.md#actualizar-a-una-versión-nueva).
