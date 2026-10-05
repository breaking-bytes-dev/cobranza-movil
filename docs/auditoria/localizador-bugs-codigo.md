# Auditoría de código — App del cobrador "App - Cobranza Movil" (versión PUBLICADA)

> **Fuente revisada:** la versión publicada que corre en los teléfonos, desempaquetada del `.msapp`
> (`Src/*.pa.yaml`, `Properties.json`, `References/DataSources.json`, `AppCheckerResult.sarif`).
> `LastSavedDateTimeUTC` = 2026-07-27 23:34. Las referencias `archivo:línea` apuntan a esa versión
> (`Src/<pantalla>.pa.yaml`), **no** al repo.
>
> **Esquema y seguridad:** consultados en Dataverse **en solo lectura** (GET de `EntityDefinitions`,
> `mobileofflineprofile(item)s`, `RetrieveRolePrivilegesRole` y conteos agregados). Los datos de
> clientes y usuarios aparecen enmascarados.
>
> **Fecha:** 2026-10-03. **Alcance:** solo diagnóstico. No se corrigió nada.

## Cómo leer las columnas

- **Severidad**
  - **Crítica**: se pierden datos o se guardan datos mal, sin que el cobrador se entere.
  - **Alta**: el cobrador ve información equivocada o se ensucian datos.
  - **Media**: funciona mal, pero tiene arreglo manual o el impacto queda acotado.
  - **Baja**: cosmética, de mantenimiento o de calidad.
- **Confianza**
  - **Verificado en dispositivo**: se observó en un Android real.
  - **Verificado en código**: la fórmula publicada lo produce sin ambigüedad. Si dice "+ datos", también se ve en metadata o en datos de Dataverse.
  - **Probable**: depende de cómo se comporte el runtime.
  - **Hipótesis**: es una causa plausible que todavía hay que confirmar.

### Contexto verificado en Dataverse (de solo lectura) que usan varios hallazgos

| Hecho | Valor |
|---|---|
| Volumen por tabla | Cuentas 1008 · Asignaciones 1009 (1006 activas) · Clientes 961 · Teléfonos 1061 · Direcciones 2699 · Agentes 20 · Interacciones 11 · Pagos 3 |
| Rol del cobrador (`SR \| CM \| Localizador`, vía equipo) | Lectura **Global**: cuenta, cliente, teléfono, dirección y agente. Lectura **Básica** (solo las propias): asignación, interacción y pago. Append/AppendTo presentes en todas las tablas que hacen falta |
| Perfiles offline | Hay dos. `autogenProfile-sanic_appcobranzamovil_c4e67` usa **"All records" en TODAS las tablas**. `App - Cobranza Movil Profile` es el curado: Cuenta, Cliente, Dirección, Teléfono, Fiador y Pago bajan **solo las filas relacionadas**. El `.msapp` publicado tiene `"ManualOfflineProfileId": ""`, o sea que la app **no tiene asignado el perfil curado** |
| Precisión GPS | `sanic_cm_in_latitud` y `sanic_cm_in_longitud` son **Decimal con Precision = 2** |
| Autonuméricos | `[IN] Numero de Interaccion` = `IN-{SEQNUM:4}` · `[PG] Numero` = `PG-{SEQNUM:4}`. Los asigna el servidor |
| Interacciones creadas desde el teléfono | Hay 2 (IN-1035 e IN-1036, del usuario de campo). Las dos tienen `NombreAgente = "Sin nombre"`, **sin** lookup de Agente ni de Cuenta, `aplicapago = false` y `actualizainfo = false`. IN-1035 tiene **latitud null**. Las 9 interacciones del admin (web/Studio) tienen los dos lookups bien |
| Pagos | Los 3 tienen `[PG] Fecha del pago` vacío. 1 de 3 tiene `[PG] Numero Interaccion` vacío |
| Especiales activas (`[AS] Tipo = 3`, `Estado = true`) | 47 en Borrador · 1 Pendiente de aprobación · 1 Aprobada |
| `[AS] Orden` en asignaciones activas | 207 vacías y 799 con valor |
| `[AS] NumeroCuenta` repetido | 1 número de cuenta tiene **2 asignaciones activas de agentes distintos** |

---

## Resumen (ordenado por severidad)

| ID | Sev. | Pantalla / control | Archivo:línea | Título | Confianza |
|---|---|---|---|---|---|
| APP-01 | Crítica | App.OnStart | App.pa.yaml:84-88 · Properties.json | Colecciones maestras truncadas a 500: el perfil offline activo baja **todo el banco** | Verificado en dispositivo + datos |
| APP-02 | Crítica | scrInteraccion · btnRegistrar_1 | scrInteraccion.pa.yaml:24-29, 1058-1064, 1101-1116 | "Guardar en 0 productos": no se guarda nada y aparece "✓ Guardado" | Verificado en código |
| APP-03 | Crítica | scrInteraccion · btnRegistrar_1 / App.OnStart | scrInteraccion.pa.yaml:1102-1113 · App.pa.yaml:91 | Interacciones del teléfono sin Agente, sin Cuenta y con "Sin nombre" (causa explicada) | Síntoma verificado en dispositivo · causa: Probable/Hipótesis |
| APP-04 | Crítica | scrInteraccion · btnRegistrar_1 | scrInteraccion.pa.yaml:1070-1116 | Guardado sin manejo de errores: siempre muestra "✓ Guardado" y quedan pagos/datos huérfanos | Verificado en código |
| APP-05 | Crítica | Cliente/Detalle · lblTagPago | scrInteraccion.pa.yaml:1087 · scrCliente.pa.yaml:482-484 · scrDetalleCuenta.pa.yaml:741-743 | El pago se vincula por un autonumérico que offline está vacío y nunca aparece en el historial | Verificado en código + datos |
| APP-06 | Alta | scrDetalleCuenta · Container16_3 | scrDetalleCuenta.pa.yaml:520-640 | "PEDIDO ESPECIAL" lee la Cuenta, pero el dato vive en la Asignación | Verificado en dispositivo |
| APP-07 | Alta | Inicio → Detalle; scrPagos | scrInicio.pa.yaml:800 · scrDetalleCuenta.pa.yaml:84, 520 · scrInteraccion.pa.yaml:103 · scrPagos.pa.yaml:133-253 | `varAsig` queda sucio: el tipo y el Pedido Especial que se muestran son de OTRA cuenta | Verificado en código |
| APP-08 | Alta | scrCartera.OnVisible | scrCartera.pa.yaml:23 | Se muestran Especiales sin aprobar (Borrador/Pendiente/Rechazada) | Verificado en código + datos |
| APP-09 | Alta | scrInteraccion · btnRegistrar_1 | scrInteraccion.pa.yaml:1058-1116 | Doble tap duplica interacciones y pagos | Verificado en código (efecto Probable) |
| APP-10 | Alta | scrInteraccion · pagos | scrInteraccion.pa.yaml:546-600, 1060, 1084-1091 | Se guardan pagos sin recibo; no hay tope de monto, ni control de recibo duplicado, ni largo máximo | Verificado en código |
| APP-11 | Alta | scrInteraccion · cardGeo / Patch | scrInteraccion.pa.yaml:1042, 1051, 1079-1080 | GPS con 2 decimales (~1 km), se guarda null sin avisar y el cartel dice "capturada" igual | Verificado en dispositivo + metadata |
| APP-12 | Alta | scrCartera.OnVisible / App.OnStart | scrCartera.pa.yaml:21, 47 · App.pa.yaml:84-88 | Cartera y colecciones maestras no se refrescan nunca después de un delta sync | Verificado en código |
| APP-13 | Alta | scrCartera · OnHidden / ↻ | scrCartera.pa.yaml:13-19, 143 | Escrituras masivas de `[AS] Orden` por NumeroCuenta: tocan asignaciones ajenas o inactivas y marcan "limpio" aunque falle | Verificado en código + datos |
| APP-14 | Alta | scrSincronizacion / cmpSyncPill | scrSincronizacion.pa.yaml:13-17, 244, 294 · cmpSyncPill.pa.yaml:61 | La píldora dice "Sincronizado" con solo tener red; no hay sync real ni fecha de última sincronización | Verificado en dispositivo + código |
| APP-15 | Media | Cartera / Inicio · agrupación | scrCartera.pa.yaml:24-45 · scrInicio.pa.yaml:406, 508-519 | Se agrupa por CIF/nombre sacado de `colCuentas` truncada: grupo "vacío" único y clientes que se fusionan | Verificado en dispositivo + código |
| APP-16 | Media | Cliente/Detalle · Interacciones | scrCliente.pa.yaml:128, 386, 482-484 · scrDetalleCuenta.pa.yaml:143, 646, 741-743 | Conteo por CIF contra lista por cuenta, filtros no delegables y 2 consultas a Pagos por tarjeta | Verificado en código (+ App Checker) |
| APP-17 | Media | scrCliente / scrDetalleCuenta · pestañas | scrDetalleCuenta.pa.yaml:117, 137 · scrCliente.pa.yaml:702 · scrInicio.pa.yaml:800 | Las dos pantallas comparten `varCliTab` y `varDetTab`/`varDetSrc` nunca se leen: la pestaña que abre es la equivocada | Verificado en código |
| APP-18 | Media | scrInteraccion · txtComentario / chips | scrInteraccion.pa.yaml:13, 202, 236, 1114 | Un chip pisa lo tipeado y `varCom` arrastra el comentario al cliente siguiente | Verificado en dispositivo + código |
| APP-19 | Media | App.OnStart / scrCliente | App.pa.yaml:88 · scrCartera.pa.yaml:35 · scrCliente.pa.yaml:49 | `colClientes` tiene dos esquemas: se carga y se pisa, y el título del cliente sale vacío si se entra desde Inicio | Verificado en código |
| APP-20 | Media | scrSincronizacion · galCap | scrSincronizacion.pa.yaml:17 | "Capturas de hoy" siempre está vacía (`'Created On' = Today()`) | Verificado en código |
| APP-21 | Media | scrInicio / scrPagos · "hoy" | scrInicio.pa.yaml:15-18 · scrInteraccion.pa.yaml:1085-1091 | "Hoy" se calcula con el `Created On` del servidor; `[PG] Fecha del pago` nunca se escribe | Probable + datos |
| APP-22 | Media | scrCartera · txtBuscar | scrCartera.pa.yaml:451, 456 | El buscador arranca con el placeholder como texto real | Verificado en código |
| APP-23 | Media | scrCartera · icoUp/icoDown | scrCartera.pa.yaml:830-853 | Las flechas de orden no hacen nada si hay empates o blancos y mueven bloques enteros | Verificado en código + datos |
| APP-24 | Media | scrInteraccion · secDatos | scrInteraccion.pa.yaml:747-979, 1092-1100 | Actualización de datos sin validar (largo, tipo, email) y sin vínculo a cliente/CIF | Verificado en código + metadata |
| APP-25 | Media | Toda la app · accesibilidad | todas (ver detalle) | 0 AccessibleLabel, contraste 2,6:1 en 41 textos y blancos de toque de ~19 dp | Verificado en código |
| APP-26 | Media | Detalle / Cartera / Inicio · formatos | scrDetalleCuenta.pa.yaml:209, 220, 360 · scrCartera.pa.yaml:110, 274, 315 · scrInicio.pa.yaml:117 | Formatos crudos o inconsistentes, que dependen del idioma; "A COBRAR HOY" está mal rotulado | Verificado en dispositivo + código |
| APP-27 | Media | Properties.json / App.OnStart | Properties.json · App.pa.yaml:91 | Se publicó con `ParserErrorCount: 2` y con OnStart no bloqueante: `gblAgente` se calcula una sola vez | Probable |
| APP-28 | Media | scrCliente.OnVisible | scrCliente.pa.yaml:12 | Filtro no delegable sobre Asignacions que se vuelve a ejecutar en cada visita | Verificado en código |
| APP-29 | Baja | scrSincronizacion / scrInteraccion · textos | scrSincronizacion.pa.yaml:439 · scrInteraccion.pa.yaml:1115, 1166, 1177 | Nota de desarrollador visible y mensajes técnicos o falsos | Verificado en dispositivo + código |
| APP-30 | Baja | cmpNav / cmpHeaderBack | cmpNav.pa.yaml:55, 94 · cmpHeaderBack.pa.yaml:66 | `Navigate` apila pantallas y `Back()` vuelve a pantallas inesperadas | Verificado en código |
| APP-31 | Baja | Varias · alturas fijas | ver detalle | Textos recortados con `gblScale` > 1 ("Espec/ial", teléfono, "Ubicación capturada") | Verificado en dispositivo |
| APP-32 | Baja | scrInteraccion · pagos | scrInteraccion.pa.yaml:546, 556-565, 614 | Monto en 0 por defecto y moneda sin relación con la cuenta | Verificado en código |
| APP-33 | Alta (proceso) | Repo `apps/localizador-canvas/src` | ver §Divergencia | El repo no refleja lo publicado (5 de 7 pantallas difieren) | Verificado |

---

## Detalle por hallazgo

### APP-01 · Crítica · Colecciones maestras truncadas a 500: el perfil activo baja todo el banco
- **Dónde:** `App.pa.yaml:84-88`, `Properties.json` (`DefaultConnectedDataSourceMaxGetRowsCount: 500`, `ManualOfflineProfileId: ""`).
- **Descripción:** ya está verificado en el dispositivo que las colecciones se cortan en 500 filas. Lo nuevo es **por qué le pega a un cobrador que tiene solo 2 asignaciones**. La app no tiene asignado el perfil curado (`ManualOfflineProfileId` está vacío), así que usa `autogenProfile-…`, que tiene **"All records" en todas las tablas**. Además, el rol `SR | CM | Localizador` da lectura **Global** sobre cuenta, cliente, teléfono y dirección. El resultado es que el teléfono baja las 1008 cuentas, los 961 clientes, los 1061 teléfonos y las 2699 direcciones del banco, y `ClearCollect` se queda con las primeras 500 de cada tabla. La guía 13 da por hecho que "la seguridad recorta", y eso es falso para esas cuatro tablas. El comentario del OnStart sobre Direcciones, "Filter DELEGABLE (… sin tope 500)", también es falso: `ClearCollect` respeta el *data row limit* aunque el filtro se delegue, y App Checker lo marca (`app-CollectDelegatableDataSource` ×4).
- **Evidencia:** `ClearCollect(colCuentas, 'CM | TBL | Cuentas')` y las 4 líneas siguientes. Los conteos, el perfil y los privilegios están en la tabla de contexto de arriba.
- **Impacto:** tarjetas vacías (nombre, saldo, mora, dirección), "Sin teléfonos registrados" o "Sin dirección" en clientes que sí los tienen, agrupación errónea (APP-15), interacciones sin cuenta (APP-03) y un arranque más lento que crece con el tamaño del banco.
- **Corrección propuesta:**
  1. Asignar el perfil curado `App - Cobranza Movil Profile` a la app y verificar que la cadena de relaciones funcione: Asignación → Cuenta → Cliente → Dirección/Teléfono. Alternativa: bajar la lectura de cuenta, cliente, teléfono y dirección a nivel BU o Básico, con owner = localizador.
  2. Dejar de copiar las tablas maestras a colecciones. Leer del origen offline con LookUp delegable, por ejemplo `LookUp('CM | TBL | Cuentas', '[CT] Numero de cuenta' = …)`, que no tiene tope de 500.
  3. Si hace falta mantener colecciones, cargarlas filtradas a la cartera: `ClearCollect(colCuentas, Filter('CM | TBL | Cuentas', '[CT] Numero de cuenta' in colAsigRaw.'[AS] NumeroCuenta'))`. Subir el límite a 2000 solo como red de seguridad.
- **Confianza:** Verificado en dispositivo (truncado). El perfil activo es Probable (se deduce de `ManualOfflineProfileId` vacío y del nombre del perfil autogenerado). Los privilegios y volúmenes están verificados en Dataverse.

### APP-02 · Crítica · "Guardar en 0 productos": no guarda nada y avisa "✓ Guardado"
- **Dónde:** `scrInteraccion.pa.yaml:24-29` (OnVisible), `1058-1064` (DisplayMode), `1101-1116` (rama MULTI).
- **Descripción:** cuando no se activa ni pago ni datos, el botón ejecuta `ForAll(Filter(colProdInt, sel) As P, Patch(...))`. `colProdInt` se arma a partir de `colAsig`, y `colAsig` **solo existe si antes se pasó por Cartera** (se crea en `scrCartera.OnVisible`). El `DisplayMode` del botón **no exige** `CountRows(Filter(colProdInt, sel)) > 0`.
- **Evidencia:** hay dos caminos reproducibles.
  - (a) Abrir la app (StartScreen = scrInicio) → tocar una cuenta en "Interacciones de hoy" → Detalle → "＋ Registrar interacción". Como `colAsig` está vacía, `colProdInt` también, y el botón dice "Guardar en 0 producto". Al tocarlo aparece `Notify("✓ Guardado offline…")` y se ejecuta `Back()` **sin haber escrito nada**.
  - (b) Cuenta fuera del corte de 500 (APP-01). `varCuenta` queda en blanco, así que `sel = '[AS] NumeroCuenta' = Blank()` es falso para todas las filas y no hay ningún producto seleccionado.
- **Impacto:** el cobrador cree que registró la gestión, pero en Dataverse no hay nada.
- **Corrección propuesta:**
  - Armar `colProdInt` desde el origen y no desde `colAsig`: `Filter('CM | TBL | Asignacions', '[AS] Estado' = true && '[AS] Cuenta'.'CIF Cliente' = varCuenta.'CIF Cliente')`. Esto sale bien si el filtro por lookup de un nivel está soportado offline; si no, pasar el CIF a la asignación.
  - Agregar `&& CountRows(Filter(colProdInt, sel)) > 0` al `DisplayMode`.
  - Antes de navegar, verificar que se escribió al menos una fila (ver APP-04).
- **Confianza:** Verificado en código.

### APP-03 · Crítica · Interacciones del teléfono sin Agente, sin Cuenta y con "Sin nombre"
- **Dónde:** `scrInteraccion.pa.yaml:1102-1113` (rama MULTI), `1073-1083` (rama única), `App.pa.yaml:84-91`.
- **Hechos:** IN-1035 e IN-1036 son las dos únicas interacciones creadas desde el teléfono. Las dos tienen pago y datos en false, o sea que pasaron por la **rama MULTI**. En esa rama:
  - `'[IN] Cuenta': P.cta`, donde `P.cta = LookUp(colCuentas, …)`.
  - `'[IN] Numerocuenta': P.'[AS] NumeroCuenta'`: el número sale **de la asignación**, no de la cuenta.

  Lo que se descartó con datos:
  - **Permisos:** el agente del usuario existe, su email coincide con el UPN, el rol tiene lectura Global de agente y AppendTo sobre agente y cuenta.
  - **Mayúsculas o `User().Email`:** la prueba A/B con el email literal también falló.
  - **La fórmula en sí:** las 9 interacciones del admin (web/Studio) tienen ambos lookups.
- **Hipótesis A (Cuenta, confianza alta):** `P.cta` está en blanco porque la cuenta del cobrador quedó fuera del corte de 500 de `colCuentas` (APP-01). Las tarjetas vacías de ese mismo usuario lo confirman. `Numerocuenta` igual se llena porque sale de la asignación, y eso explica exactamente "solo numerocuenta texto". Para que aparezca en `colProdInt` con `varCuenta` vacío, el filtro `cid = Blank()` trae las asignaciones del cobrador, que tienen el `cid` vacío, y él las tilda a mano: `btnRow` está habilitado porque `esActual` es falso.
- **Hipótesis B (Agente, confianza media):** en el teléfono, `gblAgente` está vacío o incompleto. Hay dos causas compatibles con el A/B:
  - (B1) `usenonblockingonstartrule: true` más un cálculo **único** en OnStart. Si `ClearCollect(colAgentes, …)` corre antes de que el almacén offline tenga filas (primer arranque, re-descarga del perfil o sync inicial en curso), `gblAgente` queda Blank toda la sesión y nada lo recalcula.
  - (B2) La selección explícita de columnas offline (mobile-only, activa desde el player 3.24094) pierde el linaje de columnas al pasar por colecciones y variables. Microsoft lo documenta en *fast-app-page-load*: "column lineage is occasionally lost when the data moves through collections and variables". En ese caso `'[AG] Nombre'` y/o la clave primaria no llegan a `colAgentes`, lo que da "Sin nombre" y Patch descarta el lookup en silencio. En web/Studio no pasa.
- **Diagnóstico sugerido (sin cambiar lógica):** poner en scrSincronizacion una etiqueta oculta con:
  ```
  CountRows(colAgentes) & "|" & CountRows(Filter('CM | TBL | Agentes', 'Email Usuario' = User().Email)) & "|" & IsBlank(gblAgente) & "|" & gblAgente.'[AG] Nombre' & "|" & IsBlank(varCuenta)
  ```
  Si el segundo valor es 1 y el primero es 0, se confirma B1. Si `IsBlank(gblAgente)` es falso pero el nombre está vacío, se confirma B2.
- **Impacto:** las gestiones no se pueden atribuir a un agente ni a una cuenta. Rompen reportes, el BackOffice y la auditoría.
- **Corrección propuesta:** resolver los dos lookups **en el momento del Patch y desde el origen**, no desde colecciones:
  ```
  With({ ag: LookUp('CM | TBL | Agentes', '[AG] Usuario'.'Primary Email' = User().Email),
         ct: LookUp('CM | TBL | Cuentas', '[CT] Numero de cuenta' = P.'[AS] NumeroCuenta') },
    If(IsBlank(ag) || IsBlank(ct), Notify("No se pudo identificar agente/cuenta. No se guardó.", NotificationType.Error),
       Patch(..., {'[IN] Agente': ag, '[IN] Cuenta': ct, '[IN] NombreAgente': ag.'[AG] Nombre', ...})))
  ```
  Así quedan bloqueados los guardados con lookups vacíos, que hoy pasan porque `ApplicationRequired` no se valida del lado del servidor. Como alternativa, usar el lookup `[AS] Cuenta` de la asignación en lugar del texto.
- **Confianza:** síntoma Verificado en dispositivo + datos. A: Probable (alta). B: Hipótesis.

### APP-04 · Crítica · Guardado sin manejo de errores
- **Dónde:** `scrInteraccion.pa.yaml:1070-1116`.
- **Descripción:** no hay `IfError`, ni `Errors()`, ni `App.OnError`: un `rg` de `OnError|Errors(` da 0 resultados. Si falla el Patch de la interacción (validación, largo, permisos o conflicto de sync), `varInt` queda Blank y aun así:
  - se crean los Pagos con `'[PG] Interaccion': varInt` vacío, que quedan huérfanos;
  - se crea la Actualización de datos sin interacción;
  - se muestra siempre `Notify("✓ Guardado offline. Se sincroniza al volver la señal.")`, también cuando hay red;
  - se ejecuta `Back()`.
- **Impacto:** quedan pagos sin interacción, se pierden datos y el cobrador nunca se entera de los errores.
- **Corrección propuesta:**
  ```
  IfError(
    Set(varInt, Patch(...));
    If(IsBlank(varInt), Error({Kind: ErrorKind.Custom, Message: "Interacción no creada"}));
    ... pagos ... ;
    Notify(If(Connection.Connected, "✓ Guardado", "✓ Guardado en el teléfono. Se sube al sincronizar."), NotificationType.Success);
    Back(),
    Notify("No se pudo guardar: " & FirstError.Message, NotificationType.Error)
  )
  ```
  Sumar además un `App.OnError` que registre el error y muestre un mensaje entendible para el cobrador.
- **Confianza:** Verificado en código.

### APP-05 · Crítica · El pago se vincula por un autonumérico vacío offline
- **Dónde:** `scrInteraccion.pa.yaml:1087`, `scrCliente.pa.yaml:482-484`, `scrDetalleCuenta.pa.yaml:741-743`.
- **Descripción:** `'[PG] Numero Interaccion': varInt.'[IN] Numero de Interaccion'` copia un autonumérico (`IN-{SEQNUM:4}`) que **asigna el servidor**. Offline vale Blank, y una vez sincronizado ya nadie lo vuelve a escribir en el pago. El historial busca los pagos de una interacción por ese texto, con `Not(IsBlank('[PG] Numero Interaccion')) && '[PG] Numero Interaccion' = ThisItem.'[IN] Numero de Interaccion'`, así que **un pago capturado offline no aparece nunca**.
- **Evidencia:** 1 de los 3 pagos en Dataverse ya tiene ese campo vacío. Pasó incluso con un pago creado desde web (IN-1001), porque el registro que devuelve Patch no siempre trae el autonumérico. La propia nota de scrSincronizacion admite "INT-/PAG- los asigna el servidor".
- **Impacto:** el historial no muestra los pagos y el cobrador puede volver a cobrar o a registrar el mismo pago.
- **Corrección propuesta:** vincular por el lookup `'[PG] Interaccion'`. En la tarjeta: `Filter('CM | TBL | Pagos', '[PG] Interaccion'.'CM | TBL | Interaccion' = ThisItem.'CM | TBL | Interaccion')`, o `ThisItem.'CM | TBL | Pagos'` si existe la relación 1:N. Eliminar el campo de texto o rellenarlo con un plugin post-create.
- **Confianza:** Verificado en código + datos.

### APP-06 · Alta · "PEDIDO ESPECIAL" lee la Cuenta en vez de la Asignación
- **Dónde:** `scrDetalleCuenta.pa.yaml:568, 604, 640`. El mismo patrón está en `scrPagos.pa.yaml:181, 217, 253`.
- **Evidencia:** las fórmulas leen `varCuenta.'[CT] Tipo Especial'`, `'[CT] Retiro NIO/USD'` y `'[CT] Nota'`. En la metadata, la Asignación tiene `sanic_cm_as_retironio/usd`, `Notas` y `[AS] Motivo`, que es donde graba el BackOffice.
- **Impacto:** el cobrador ve C$ 0.00 y una nota vacía (confirmado en el dispositivo).
- **Corrección propuesta:** leer `varAsig.'Retiro NIO'`, `varAsig.'Retiro USD'`, `varAsig.Notas` y `varAsig.'[AS] Motivo'`. Antes hay que arreglar APP-07 para que `varAsig` sea siempre el correcto.
- **Confianza:** Verificado en dispositivo.

### APP-07 · Alta · `varAsig` queda sucio entre clientes
- **Dónde:** `scrInicio.pa.yaml:800` solo hace `Set(varCuenta, …)` y no setea `varAsig`. Lo leen `scrDetalleCuenta.pa.yaml:84` (badge de tipo), `:520` (visibilidad de Especial), `scrInteraccion.pa.yaml:103` y `scrPagos.pa.yaml:133` (sección "PEDIDO ESPECIAL" en el resumen de pagos del día).
- **Descripción:** al entrar a Detalle desde Inicio, el badge de tipo y la sección Especial salen de la **última asignación abierta desde Cliente**, que puede ser de otro cliente. En scrPagos, que es un resumen de todo el día, aparece el Pedido Especial de la última cuenta vista.
- **Impacto:** se muestra un tipo de gestión equivocado y un retiro especial ajeno.
- **Corrección propuesta:** en `scrInicio` Button4, setear `Set(varAsig, LookUp('CM | TBL | Asignacions', '[AS] NumeroCuenta' = ThisItem.cta && '[AS] Estado'))`. Eliminar `Container16_7` de scrPagos. Mejor todavía: navegar con un único contexto, `Navigate(scrDetalleCuenta, …, {ctxAsig: …, ctxCuenta: …})`.
- **Confianza:** Verificado en código.

### APP-08 · Alta · Se muestran Especiales sin aprobar
- **Dónde:** `scrCartera.pa.yaml:23` (`Filter('CM | TBL | Asignacions', '[AS] Estado' = true)`).
- **Descripción:** el diseño (`docs/desarrollo/04-app-localizador-canvas.md:243`) pide "Especial: + `estadoaprobacion = Aprobada`", y la app no lo filtra. Hoy hay 47 Especiales activas en Borrador y 1 en Pendiente de aprobación.
- **Impacto:** el cobrador sale a gestionar un pedido especial que todavía no se aprobó o que se rechazó.
- **Corrección propuesta:**
  ```
  Filter('CM | TBL | Asignacions', '[AS] Estado' = true && ('[AS] Tipo' <> 'Tipo asignacion'.Especial || '[AS] Estado de aprobacion' = '…'.Aprobado))
  ```
- **Confianza:** Verificado en código + datos.

### APP-09 · Alta · Doble tap duplica interacciones y pagos
- **Dónde:** `scrInteraccion.pa.yaml:1058-1116`.
- **Descripción:** no hay ninguna bandera de "guardando". El `DisplayMode` solo depende del formulario, y la secuencia es Patch → ForAll Patch → Back. Un segundo tap mientras el primero escribe vuelve a ejecutar todo.
- **Impacto:** interacciones y pagos duplicados, que inflan lo cobrado.
- **Corrección propuesta:** `Set(varGuardando, true)` al inicio y `false` en `IfError` y al final. Agregar `&& !varGuardando` al `DisplayMode`. Del lado del servidor, una clave alternativa sobre (`[PG] Numero recibo`, `[PG] Numero Cuenta`).
- **Confianza:** Verificado en código. El efecto es Probable.

### APP-10 · Alta · Validación de pagos insuficiente
- **Dónde:** `scrInteraccion.pa.yaml:1060` (DisplayMode), `1084` (`Filter(colLineasPago, monto > 0)`), `546-551`, `584-589`.
- **Descripción:**
  - El botón exige que **al menos una** línea tenga monto > 0 y recibo. El Patch guarda **todas** las líneas con monto > 0, **aunque no tengan recibo**. `lblHint` lo avisa, pero el botón sigue habilitado.
  - No hay tope de monto: la columna admite hasta 1e11. No hay comparación contra el saldo o el pago mínimo, ni control de recibo duplicado (entre líneas o contra pagos existentes).
  - `txtRecibo` no tiene `MaxLength`, pero la columna admite 30 caracteres. Un recibo más largo hace fallar el Patch, y ese error no se ve (APP-04).
- **Corrección propuesta:**
  - `ForAll(Filter(colLineasPago, monto > 0 && !IsBlank(Trim(recibo))) …)`.
  - `DisplayMode` con `CountRows(Filter(colLineasPago, monto <= 0 || IsBlank(Trim(recibo)))) = 0`.
  - `MaxLength = 30` en el recibo y validación de unicidad: `CountRows(colLineasPago) = CountRows(Distinct(colLineasPago, recibo))` y `IsBlank(LookUp('CM | TBL | Pagos', '[PG] Numero recibo' = …))`.
  - Pedir confirmación si el monto supera el saldo vencido.
- **Confianza:** Verificado en código.

### APP-11 · Alta · GPS de baja precisión y null silencioso
- **Dónde:** `scrInteraccion.pa.yaml:1079-1080` (Patch), `1042` (texto fijo "Ubicación capturada"), `1051`. También `scrCliente.pa.yaml:517` y `scrDetalleCuenta.pa.yaml:777` ("📍 Ubicación" se muestra siempre).
- **Evidencia:**
  - Metadata: Latitud y Longitud tienen `Precision = 2`, o sea ~1,1 km. IN-1036 se grabó con lat = 12.14.
  - IN-1035 se grabó con **lat null**, por permiso denegado o GPS todavía sin fijar, y la pantalla dijo "Ubicación capturada".
  - El historial muestra "📍 Ubicación" incluso cuando no hay coordenadas.
- **Corrección propuesta:**
  - Cambiar la columna a Decimal o Float con precisión 6, o usar la columna de geolocalización.
  - Bloquear el guardado o pedir confirmación si `IsBlank(Location.Latitude)`.
  - Texto: `If(IsBlank(Location.Latitude), "Sin ubicación (activá el GPS)", "Ubicación capturada")`.
  - En el historial: `Visible = !IsBlank(ThisItem.'[IN] Latitud')`.
- **Confianza:** Verificado en dispositivo + metadata.

### APP-12 · Alta · Cartera y maestras sin refresco tras el delta sync
- **Dónde:** `scrCartera.pa.yaml:21` (`If(!varCarteraLista || varRecargarCartera, …)`), `:47`. `varRecargarCartera` **no se pone en true en ninguna parte** (un `rg` lo encuentra solo en esas dos líneas). `colCuentas`, `colTelefonos`, `colDirecciones`, `colClientes` y `colAgentes` se cargan **solo** en `App.OnStart`.
- **Impacto:** las asignaciones nuevas, los saldos actualizados y los teléfonos nuevos que llegan por sync no aparecen hasta cerrar y abrir la app. El ↻ los trae, pero dispara las escrituras masivas de APP-13.
- **Corrección propuesta:** sacar las maestras de colecciones (APP-01). Si se mantienen, agregar un "Recargar datos" que solo haga `ClearCollect` sin escribir, y ejecutarlo al volver de la pantalla de sincronización.
- **Confianza:** Verificado en código.

### APP-13 · Alta · Escrituras masivas de `[AS] Orden`
- **Dónde:** `scrCartera.pa.yaml:13-19` (OnHidden) y `:143` (↻).
- **Descripción:** ya se sabía que ↻ escribe todas las asignaciones (es lo que reporta App Checker como `app-ForAllWithMutation`). Lo nuevo:
  - (a) `UpdateIf('CM | TBL | Asignacions', '[AS] NumeroCuenta' = A.'[AS] NumeroCuenta', …)` filtra **solo por número de cuenta**. No mira agente ni estado, así que también apunta a asignaciones inactivas o de otro agente. Hoy hay 1 cuenta con 2 asignaciones activas de agentes distintos. Con Write Básico, online da error y offline queda un conflicto en la cola de sync.
  - (b) `UpdateIf(colClientes, dirty, {dirty: false})` corre aunque las escrituras hayan fallado, y el orden se pierde.
  - (c) ↻ escribe aunque no haya cambios (no filtra por `dirty`).
- **Corrección propuesta:** `Patch('CM | TBL | Asignacions', A, {'[AS] Orden': C.orden})` con el registro (por clave primaria), solo para `dirty`, dentro de `IfError`, y limpiar `dirty` solo si salió bien. Como alternativa, guardar el orden en una tabla de preferencias del agente.
- **Confianza:** Verificado en código + datos.

### APP-14 · Alta · Estado de sincronización engañoso
- **Dónde:** `cmpSyncPill.pa.yaml:61` (`If(Connection.Connected, "Sincronizado", "Sin conexión")`), `scrSincronizacion.pa.yaml:13-17`, `244` (`"—"` fijo), `294`.
- **Descripción:** ya se sabía lo del botón deshabilitado, que no hay `ShowHostInfo` y que no se usa `Connection.Sync`. Lo nuevo:
  - La píldora dice "Sincronizado" con solo tener red, aunque haya capturas pendientes.
  - "Última sincronización" es un literal.
  - `varEstado` se calcula una sola vez en OnVisible y no se actualiza si cambia la conexión.
- **Corrección propuesta:**
  - Píldora: `If(!Connection.Connected, "Sin conexión", Connection.Sync = ConnectionSync.Synced, "Sincronizado", "Pendiente")`. Ajustar los valores a los que expone el player.
  - `OnSelect = ShowHostInfo(HostInfo.OfflineSync)`.
  - Mostrar la fecha real de la última sincronización si el host la expone. Si no, sacar la fila.
- **Confianza:** Verificado en dispositivo + código.

### APP-15 · Media · Agrupación por CIF/nombre sacados de `colCuentas`
- **Dónde:** `scrCartera.pa.yaml:24-45`, `scrInicio.pa.yaml:406, 508-519, 655-672`.
- **Descripción:** ya se sabía que, con la cuenta fuera del corte, el CIF queda vacío. Lo nuevo:
  - Todas las cuentas sin CIF se juntan en **un solo grupo vacío**: una tarjeta sin nombre con N cuentas ajenas entre sí. `lblTotalMeta` lo cuenta como un cliente más.
  - En Inicio, "Clientes" se cuenta con `Distinct` sobre el **nombre**: dos clientes homónimos cuentan como uno y los nombres vacíos suman 1.
  - Desde ese grupo, `Navigate(scrCliente)` llega con `varCliente` vacío.
- **Corrección propuesta:** agrupar por el lookup `[AS] Cuenta`.`[CT] Cliente` (GUID), y descartar o señalar las filas sin cuenta resuelta (`Filter(colAsig, !IsBlank(cta))`) con un aviso "datos incompletos, sincronizá".
- **Confianza:** Verificado en dispositivo + código.

### APP-16 · Media · Interacciones: conteo inconsistente, no delegable y N+1
- **Dónde:** `scrCliente.pa.yaml:128, 386, 482-484`, `scrDetalleCuenta.pa.yaml:143, 646, 741-743`.
- **Descripción:**
  - Ya se sabía que la pestaña cuenta por CIF y la galería filtra por número de cuenta.
  - Los `Filter('CM | TBL | Interaccions', LookUp(colCuentas, …).'CIF Cliente' = …)` **no son delegables** (lookup a una colección dentro del predicado). Evalúan solo las primeras 500 interacciones y App Checker los marca (`app-SuggestRemoteExecutionHint` ×12).
  - Cada tarjeta lanza **2 consultas** a Pagos: `Concat` y `CountRows`.
  - Como interacción y pago tienen lectura Básica, el cobrador solo ve **sus** gestiones, no las de otros localizadores sobre la misma cuenta. Hay que confirmar si eso es lo que se quiere.
- **Corrección propuesta:** filtrar por el lookup `'[IN] Cuenta'` (delegable). Para el CIF, `'[IN] Cuenta'.'CIF Cliente' = varCliente` (un nivel, soportado offline). Para los pagos, usar la relación (APP-05) y calcular `With({p: …}, …)` una sola vez por tarjeta.
- **Confianza:** Verificado en código (+ App Checker).

### APP-17 · Media · Pestañas: variable compartida y variables muertas
- **Dónde:**
  - `scrDetalleCuenta.pa.yaml:117, 137, 165, 240, 416, 649` usan `varCliTab`, que es la misma variable de scrCliente.
  - `scrCliente.pa.yaml:702` y `scrInicio.pa.yaml:800` setean `varDetTab` y `varDetSrc`, que **no se leen** (App Checker `app-UnusedVariables`). `varSyncBack` tampoco.
- **Impacto:** desde Inicio, el Detalle abre en la pestaña que haya quedado de antes. Si se cambia de pestaña en Detalle y se vuelve, scrCliente aparece en "Interacciones" en lugar de "Cuentas".
- **Corrección propuesta:** usar `varDetTab` en Detalle y setearla siempre al navegar, o usar variables de contexto (`Navigate(…, {tab: "detalle"})`).
- **Confianza:** Verificado en código.

### APP-18 · Media · Comentario: los chips pisan el texto y arrastran estado
- **Dónde:** `scrInteraccion.pa.yaml:202` (`Default: =varCom`), `236`, `13` (`Reset(txtComentario)` en OnHidden), `1114` (`Set(varCom, "")` **solo** si se guarda).
- **Descripción:** ya se sabía que el chip descarta lo tipeado. Lo nuevo: si el cobrador toca un chip y vuelve **sin guardar**, `varCom` conserva el texto y el próximo cliente abre con el comentario anterior ya cargado.
- **Corrección propuesta:** el chip debe concatenar sobre el texto actual: `Set(varCom, Trim(txtComentario.Text & ". " & ThisItem.Value))`. Agregar `Set(varCom, "")` en `OnHidden`.
- **Confianza:** Verificado en dispositivo (chips) + código (arrastre).

### APP-19 · Media · `colClientes` con dos esquemas
- **Dónde:** `App.pa.yaml:88` (`ClearCollect(colClientes, 'CM | TBL | Clientes')`), `scrCartera.pa.yaml:35` (la pisa con un `GroupBy` cuyas columnas son `cid` y `cliNom`), `scrCliente.pa.yaml:49`.
- **Impacto:** se carga la tabla Clientes (truncada a 500) para nada, lo que hace el arranque más lento. Si se entra a Cliente desde Inicio antes de visitar Cartera, `LookUp(colClientes, cid = varCliente).cliNom` sale en blanco y el encabezado queda sin nombre.
- **Corrección propuesta:** renombrar la colección de Cartera (`colCarteraCli`) y obtener el nombre del cliente desde el origen: `LookUp('CM | TBL | Clientes', '[CL] CIF' = varCliente).'[CL] Nombre'`.
- **Confianza:** Verificado en código.

### APP-20 · Media · "Capturas de hoy" siempre vacía
- **Dónde:** `scrSincronizacion.pa.yaml:17` (`Filter('CM | TBL | Interaccions', 'Created On' = Today())`).
- **Descripción:** `Created On` es una fecha con hora. La igualdad contra `Today()` (medianoche) solo matchea registros creados exactamente a las 00:00:00.
- **Corrección propuesta:** `Filter(…, 'Fecha de captura' >= Today() && 'Fecha de captura' < DateAdd(Today(), 1))`.
- **Confianza:** Verificado en código.

### APP-21 · Media · "Hoy" calculado con `Created On`
- **Dónde:** `scrInicio.pa.yaml:15-16`, `scrPagos.pa.yaml:358`, `scrInteraccion.pa.yaml:1085-1091`.
- **Descripción:**
  - El `Created On` de un registro creado offline lo pone el servidor al sincronizar. IN-1035 se capturó a las 01:01:48 y se creó a las 01:02:40. Lo más probable es que, antes de sincronizar, el valor local esté vacío.
  - Lo cobrado offline no suma en "Cobrado hoy". Un pago de las 23:50 que sincroniza a las 00:10 cae en el día siguiente.
  - `[PG] Fecha del pago` existe pero **nunca se escribe** (3 de 3 pagos vacíos).
- **Corrección propuesta:** escribir `'Fecha del pago': Now()` en cada pago y filtrar "hoy" por `'Fecha de captura'` (interacciones) y `'Fecha del pago'` (pagos), con un rango `[Today(), Today()+1)`.
- **Confianza:** Probable + datos.

### APP-22 · Media · El buscador arranca con el placeholder como texto
- **Dónde:** `scrCartera.pa.yaml:451` (`Default: ="Buscar cliente o cuenta…"`) y `:456`.
- **Impacto:** si el cobrador toca y escribe sin borrar, busca "Buscar cliente o cuenta…juan" y no aparece nada. El color gris `muted2` hace que parezca un placeholder.
- **Corrección propuesta:** `Default = ""` y `HintText = "Buscar cliente o cuenta…"`.
- **Confianza:** Verificado en código.

### APP-23 · Media · Flechas de orden con empates
- **Dónde:** `scrCartera.pa.yaml:830-853`.
- **Descripción:**
  - `obj = Max(Filter(colClientes, orden < ThisItem.orden), orden)`. Con órdenes iguales o en blanco (207 asignaciones activas no tienen orden), no existe un "menor estricto" y la flecha no hace nada.
  - `UpdateIf(colClientes, orden = obj, …)` mueve **todos** los clientes que comparten ese orden.
  - Además, la flecha usa el orden global y no la lista filtrada que se ve en pantalla.
- **Corrección propuesta:** al cargar, renumerar `orden` con secuencia: `ForAll(Sequence(CountRows(…)), …)`. Hacer el intercambio por `cid` del vecino inmediato dentro de la vista ordenada.
- **Confianza:** Verificado en código + datos.

### APP-24 · Media · Actualización de datos sin validar
- **Dónde:** `scrInteraccion.pa.yaml:747-979`, `1092-1100`.
- **Descripción:**
  - Los teléfonos no tienen `MaxLength` (la columna admite 20) ni teclado numérico.
  - El correo no se valida (`IsMatch(…, Match.Email)`).
  - El registro de `Actualizacion de datos` no lleva CIF ni cliente: depende solo de `varInt`, que puede estar vacío (APP-04).
  - Si un texto excede el largo de la columna, el Patch falla en silencio.
- **Corrección propuesta:**
  - `MaxLength = 20`.
  - Validar con `IsMatch(inTelCel.Text, "^[0-9 +-]{8,20}$")` y `IsMatch(inCorreo.Text, Match.Email)`.
  - Guardar el CIF y el lookup de Cliente en la actualización.
- **Confianza:** Verificado en código + metadata.

### APP-25 · Media · Accesibilidad
- **Dónde:** toda la app.
- **Evidencia:**
  - Hay 0 `AccessibleLabel` en todo `Src`. App Checker reporta `acc-AccessibleLabelNeeded` ×49, `acc-FocusBorderShouldBeVisible` ×159 y `acc-TabIndex…` ×181.
  - El color `muted2` (#98A2B3) se usa como texto en 41 controles, con contraste **2,58:1** sobre blanco y 2,34:1 sobre `screen`. No llega al mínimo AA de 4,5:1, y el cobrador trabaja a pleno sol. `hint` (#D97706) da 3,19:1.
  - Las flechas de orden miden 34 unidades sobre un lienzo de 640, unos 19 dp, por debajo del mínimo de 44-48 dp. Además están juntas, lo que lleva a tocar la equivocada.
  - Los filtros de Cartera (`lblOpt_*`) son etiquetas con `OnSelect` y no tienen rol de botón.
  - Se usan emojis como íconos sin texto alternativo.
- **Corrección propuesta:** `AccessibleLabel` en todos los controles interactivos, `muted2` → #667085 o más oscuro para texto, blancos de toque ≥ 80 unidades del lienzo y botones en lugar de etiquetas para los filtros.
- **Confianza:** Verificado en código.

### APP-26 · Media · Formatos crudos e inconsistentes
- **Dónde:**
  - `scrDetalleCuenta.pa.yaml:209, 220` y `scrInteraccion.pa.yaml:171, 182`: `"Total dolarizado: U$" & número` sin `Text()`, que muestra "U$1234.5" con el separador decimal del dispositivo. Además usa "U$" en lugar de "US$".
  - `scrDetalleCuenta.pa.yaml:360`: `[CT] Fecha Ultimo Pago`, que es fecha con hora, se convierte implícitamente con el formato del idioma del teléfono e incluye la hora.
  - `scrCartera.pa.yaml:110`: un DateTime crudo.
  - `scrCartera.pa.yaml:274, 315`: `"#,##0"` sin centavos.
  - `scrInicio.pa.yaml:117`: `Text(Today(), "mmmm, dddd dd")` muestra "octubre, sábado 03".
  - "A COBRAR HOY" (`scrCartera.pa.yaml:220`) suma el saldo vencido **total** de toda la cartera, no lo que vence hoy.
- **Corrección propuesta:**
  - Para montos, usar `Moneda()` o `Text(x, "#,##0.00", "es-NI")`.
  - Para fechas, `Text(f, "dd/mm/yyyy", "es-NI")`.
  - Renombrar a "Saldo vencido total".
- **Confianza:** Verificado en dispositivo (fechas) + código.

### APP-27 · Media · Publicación con errores de análisis y OnStart no bloqueante
- **Dónde:** `Properties.json` (`"ParserErrorCount": 2`, `"usenonblockingonstartrule": true`), `App.pa.yaml:91`.
- **Descripción:** la versión se guardó y publicó con 2 errores de análisis sin resolver; el `.msapp` no indica en qué fórmulas están. Con OnStart no bloqueante, scrInicio se pinta y se puede navegar antes de que terminen las colecciones y `gblAgente`, y no hay un `Named Formula` que los recalcule (ver APP-03 B1).
- **Corrección propuesta:**
  - Abrir en Studio y revisar App Checker → Fórmulas hasta tener 0 errores. Bloquear la publicación si hay errores.
  - Pasar `gblAgente` a una fórmula con nombre en `App.Formulas`: `gblAgente = LookUp('CM | TBL | Agentes', '[AG] Usuario'.'Primary Email' = User().Email);`, que se reevalúa cuando llegan los datos.
- **Confianza:** Probable.

### APP-28 · Media · `scrCliente.OnVisible` no delegable y repetido
- **Dónde:** `scrCliente.pa.yaml:12`.
- **Descripción:** `Filter('CM | TBL | Asignacions', LookUp(colCuentas, …).'CIF Cliente' = varCliente, …)` no es delegable y **vuelve a consultar en cada visita**, también al volver de Detalle o de Interacción. El cobrador tiene lectura Básica y le afecta poco. Para supervisores o el BackOffice con lectura amplia, se corta en 500. Si la cuenta está fuera del corte de `colCuentas`, las cuentas del cliente no aparecen ("Cuentas (0)").
- **Corrección propuesta:** `Filter(colAsig, cid = varCliente)`, que ya está en memoria, o filtrar por el lookup `'[AS] Cuenta'.'CIF Cliente'`.
- **Confianza:** Verificado en código.

### APP-29 · Baja · Textos técnicos o falsos
- **Dónde:**
  - `scrSincronizacion.pa.yaml:439`: la nota "INT-/PAG- los asigna el servidor… Device Status…" está visible (ya conocido).
  - `scrInteraccion.pa.yaml:1115`: dice "Guardado offline" aunque haya red o el guardado haya fallado.
  - `:1166, 1177`: "Informacion" sin tilde.
  - `scrSincronizacion.pa.yaml:407`: "sin Nº aún" es jerga.
- **Corrección propuesta:** eliminar la nota y usar mensajes condicionados al estado real, por ejemplo "Pendiente de subir" o "Subido".
- **Confianza:** Verificado en dispositivo + código.

### APP-30 · Baja · Navegación que apila pantallas
- **Dónde:** `cmpNav.pa.yaml:55, 67, 94, 106` (`Navigate(scrInicio)` / `Navigate(scrCartera)`), `cmpHeaderBack.pa.yaml:66` (`Back()`).
- **Impacto:** la pila crece. Después de alternar con la barra inferior, el "←" vuelve a pantallas inesperadas, por ejemplo a Interacción ya guardada.
- **Corrección propuesta:** pasar la barra a `Navigate(scr, ScreenTransition.None)` y que el encabezado navegue a una pantalla padre explícita en lugar de `Back()`, o fijar `varSyncBack` (hoy no se usa).
- **Confianza:** Verificado en código.

### APP-31 · Baja · Recortes con `gblScale`
- **Dónde:** controles con altura fija: scrInicio 14, scrCartera 13, scrInteraccion 12 (por ejemplo `btnRegistrar_1` `Height: =54` con `Size: gblT.fmd`).
- **Descripción:** ya se conocen "Espec/ial", el teléfono recortado y "Ubicación capturada" recortado. La causa es que la fuente escala con `gblScale` (hasta ×1,7) y la altura no.
- **Corrección propuesta:** usar `AutoHeight = true` o calcular las alturas en función de `gblScale`, igual que ya se hace con `icoUp`.
- **Confianza:** Verificado en dispositivo.

### APP-32 · Baja · Monto en 0 por defecto y moneda sin relación con la cuenta
- **Dónde:** `scrInteraccion.pa.yaml:614` (`{monto:0, moneda:"NIO"}`), `546`, `556-565`.
- **Descripción:** el cobrador tiene que borrar el 0 antes de escribir. Se puede registrar un pago en USD sobre una cuenta que solo tiene saldo en NIO, sin ningún aviso.
- **Corrección propuesta:** `monto: Blank()`. Elegir la moneda por defecto según el saldo de la cuenta y avisar si no coincide.
- **Confianza:** Verificado en código.

### APP-33 · Alta (proceso) · El repo no refleja lo publicado
Ver la sección siguiente.

---

## Divergencia repo vs publicada

- **Repo:** `apps/localizador-canvas/src`. Último commit `ba73b5a`, 2026-07-26 23:51 -0600 (= 2026-07-27 05:51 UTC).
- **Publicada:** `LastSavedDateTimeUTC` 2026-07-27 23:34 UTC, unas **18 h posterior**.
- **Situación:** se editó en Studio y no se volvió a desempaquetar al repo. Cualquier corrección que se haga sobre el repo **pisaría** funcionalidad publicada (por ejemplo, la multi-selección de productos).

| Archivo | Líneas distintas (diff) | Qué cambió (repo → publicada) |
|---|---|---|
| App.pa.yaml | 0 | Igual. |
| scrInicio | 30 | `Text(colCobN,"#,###.00")` → `Text(Coalesce(colCobN,0),"#,##0.00")` (lo mismo en USD). El resto es layout (alturas y padding). |
| scrCartera | 44 | Solo layout y alturas, más un ajuste del formato YAML de los textos de dirección. Sin cambios de lógica. |
| scrCliente | 258 | El encabezado pasó de `LookUp(colClientes,'[CL] CIF'=…).'[CL] Nombre'`, con subtítulo Departamento·Trabajo (esquema de la tabla Clientes), a `LookUp(colClientes, cid=…).cliNom` y "CIF " & varCliente (esquema de Cartera). Se agregaron `lblSinTel` y `lblSinDir`. La tarjeta de cuenta se rearmó (`conMiniBody`/`conMiniFoot`, etiqueta "SALDO VENCIDO", `moraBadge`) y se intercambiaron Tipo y NumeroCuenta. |
| scrDetalleCuenta | 27 | Solo layout. |
| scrInteraccion | 293 | Lo publicado **agrega**: `OnVisible` que arma `colProdInt`, la tarjeta "APLICAR A ESTOS PRODUCTOS" (`galProd`, `btnRow`, `lblSelNota`), `OnCheck` en `tpPago`/`tgDatos` y la **rama MULTI** del guardado (una interacción por producto tildado). En el repo hay un solo Patch con pago y datos. Origen de APP-02 y APP-03. |
| scrPagos | 284 | La publicada **quitó** los bloques MORA y FIADOR (copiados de Detalle) y **dejó** el bloque "PEDIDO ESPECIAL" (APP-07). Por eso la versión del repo tiene más líneas. |
| scrSincronizacion | 0 | Igual (incluida la nota de desarrollador). |
| Components | 7 (cmpSheet) | Solo formato de texto ("🌙 Noche"). |

**Recomendación:** antes de corregir, exportar el `.msapp` publicado, desempaquetarlo (`pac canvas unpack` o descarga del código fuente) sobre `apps/localizador-canvas/src`, commitearlo como línea base y recién después aplicar las correcciones. Agregar a la guía de release el paso "unpack + commit de lo publicado".

---

## Fuera de alcance pero relevante

- **Perfil offline:** el curado `App - Cobranza Movil Profile` pone Agente en "All records", y la guía 13 dice "Organization rows". Con lectura Global da lo mismo, pero conviene alinearlo. Tampoco incluye `Actualizacion de datos`, que la app sí escribe.
- **Agentes:** 16 de 20 no tienen `Email Usuario`. Cualquier cobrador nuevo va a caer en "Sin nombre" mientras `gblAgente` dependa de ese campo denormalizado y no del lookup `[AG] Usuario`.
- **Asignaciones:** 1 activa tiene `[AS] Tipo` vacío. Solo aparece con el filtro "Todos".

## Encontrados en la prueba en dispositivo (2026-10-04) — corregidos y verificados

Prueba en el Android del VPS con User5, con la app publicada después de la Fase 2. Cada corrección se verificó en el teléfono, y en Dataverse cuando corresponde.

| # | Bug | Corrección | Verificación |
|---|---|---|---|
| D-1 | El buscador de la Cartera traía "Buscar cliente o cuenta…" como **texto real** (`Default`) y filtraba recién al pulsar Enter o salir del campo (`OnChange`) | `HintText` + `Default` vacío; filtro en vivo sobre `txtBuscar.Text` (`DelayOutput`) | "3649" sin Enter → 1 cliente; vacío → todos |
| D-2 | El botón ↻ no recargaba `colCuentas`, `colDirecciones` ni `colTelefonos`: una asignación nueva aparecía como tarjeta vacía hasta reiniciar la app | El ↻ recarga esas tres colecciones | Cliente nuevo asignado con la app abierta → aparece completo tras ↻ |
| D-3 | Las flechas de reordenar no hacían nada con clientes sin `[AS] Orden` (207 de 1.006 asignaciones activas en DEV). `Min()` de órdenes vacíos devuelve 0 | Los clientes con orden 0 o vacío reciben un orden único al final (alfabético) y quedan `dirty` | Orden asignado; ↑ funciona y se persiste (DOS 4, TRES 3) |
| D-4 | Pantalla Sincronización: nota de desarrollo visible; la ayuda nombraba un botón inexistente ("Actualizar"); "Buscar actualizaciones" deshabilitado salvo que hubiera pendientes y solo refrescaba 3 tablas; "Última sincronización" fija en "—" | Texto para el cobrador; ayuda con "Buscar actualizaciones (Check for updates)"; botón activo en línea, refresca también asignaciones, cuentas y bóveda; la hora remite al estado del dispositivo (Power Fx no la expone) | Botón activo; bajó un cliente nuevo |
| D-5 | "Último pago" mostraba **un día menos** (medianoche UTC vista en Managua) y otro formato que "Fecha de pago" | `DateAdd(…, TimeZoneOffset(…))` y `dd/mm/yyyy` en las dos fechas | 15/09/2026 y 15/10/2026 |
| D-6 | "Total dolarizado: U$42.1" (sin espacio, 1 decimal) | `US$ 0.00`, y "—" si está vacío | "US$ 42.10", "US$ 15.50" |
| D-7 | En tamaño Máximo la insignia de tipo se partía ("Diari / a") y "Ver número" quedaba apretado | La insignia crece con la escala; "Ver número" va debajo de ella | Máximo: una línea, sin superposición |

Además: la **sincronización completa** ("Check for updates") **borra del teléfono lo que el cobrador ya no debe ver**. Al cerrar una asignación, la bóveda, las cuentas y los clientes bajaron de 3→2, 3→2 y 2→1. La sincronización automática tardó más de 5 minutos en traer un cambio del servidor; "Check for updates" lo trajo al instante.

## Parciales cerrados y verificados en el teléfono (2026-10-05)

Antes de esta prueba, la app no abría: el perfil offline **autogenerado** se trabó ("failed to publish. Error: 0x80071151").
Se reemplazó por el perfil personalizado "Cobranza Movil - Cobrador" (`scripts/pan/paso7_perfil_offline.py`, ver README de la app)
y la app abrió al primer intento.

| # | Verificación en el teléfono (User5) y en Dataverse |
|---|---|
| APP-12 | Se cambió una dirección en Dataverse → "Buscar actualizaciones" → la Cartera la mostró al volver (y la hora de actualización se movió) |
| APP-13 | Con el filtro "4" (MOVIL y DOS visibles, TRES oculto), ↓ en MOVIL intercambió con DOS. Dataverse: DOS 1, TRES 2 (sin tocar), MOVIL 3 |
| APP-23 | El orden viejo 2, 3, 4 (con hueco) se guardó como 1, 2, 3 al tocar ↻ |
| APP-26 | "SALDO VENCIDO TOTAL", montos con centavos, "04/10/2026 23:22", "domingo 04 de octubre" |
| APP-29 | Sincronización dice «pendiente de subir». Con señal, el guardado dice "Se sube en la próxima sincronización" (la app es offline-first); la interacción llegó una sola vez a Dataverse |
| APP-31 | En **Máximo**: fecha del Inicio sin cortar la "g" (al contenedor le faltaban los 16 px de relleno); "Ver número" entra en el encabezado (el encabezado crece hasta contenerlo); tarjeta de ubicación en una fila; "ACTUAL" pasó a la línea del número ("•••• 3440 · ACTUAL") porque la pastilla al costado le quitaba el ancho al nombre del producto |

Pendiente detectado: la tarjeta dice "Ubicación capturada" aunque el GPS no haya devuelto nada (", · automática"). Es **APP-11**, sigue abierto.

## Botón Guardar: APP-02, 04, 09 y 10 corregidos y verificados en el teléfono (2026-10-05)

| # | Corrección | Verificación en el teléfono (User5) y en Dataverse |
|---|---|---|
| APP-02 | Los productos salen del origen (`Cuentas` del cliente → sus `Asignacions` activas), no de `colAsig`. El botón exige al menos un producto | Inicio → cuenta → Registrar (el camino que daba "Guardar en 0 productos"): "Guardar en 1 producto" y se guardó. MOVIL muestra sus 2 cuentas |
| APP-04 | Guardado con `IfError`: si falla la interacción no se crean pagos ni datos; si falla un pago se avisa cuál y **el reintento solo guarda lo que falta** (el rol no puede borrar, así que no se puede deshacer) | Pago de 2e11 (> tope de la columna): "La interacción se guardó, pero no todos los pagos (Value must be between 0 and 100000000000)". Se corrigió el monto y se reintentó: **1 interacción y 2 pagos**, sin duplicar |
| APP-09 | Bandera `locGuardando`: el botón se deshabilita y dice "Guardando…" mientras escribe; el `OnSelect` no hace nada si ya está guardando | Triple toque → 1 interacción. Doble toque en "Guardar en 2 productos" → 2 interacciones (una por cuenta) |
| APP-10 | Cada línea necesita monto > 0 y recibo; recibos sin repetir (recibo + moneda); recibo ≤ 30; recibo ya usado en la cuenta → bloquea; monto mayor que el saldo vencido → hay que tocar «Confirmar pago». Las líneas tienen `id` (antes editar una pisaba la otra). Respaldo en el servidor: clave alternativa (recibo, cuenta, moneda) con `scripts/localizador/clave_recibo_pago.py` | "Falta: monto y recibo en cada pago", "Falta: recibos sin repetir", "Ese número de recibo ya está registrado en esta cuenta", aviso y «Confirmar pago» con monto > saldo. La segunda línea se editó sin tocar la primera |

No verificado: un fallo del Patch de la **interacción** (no hubo forma de provocarlo desde el teléfono; el comentario de 2.050 caracteres llegó con 1.254 y se guardó sin error). La rama usa el mismo `IfError` que sí se probó con los pagos.
Visto de paso: un chip de comentario (APP-18) **reemplaza** el texto escrito, y "Atrás" de más saca de la app (APP-30). Siguen abiertos.

## Grupo 2: corregidos y verificados en el navegador (2026-10-05)

Suite `tests/localizador-canvas/grupo2.js` (Playwright, User5, web player): **18/18**. El App Checker quedó sin errores
y sin las 9 advertencias de delegación de `scrCliente`.

| # | Corrección | Verificación |
|---|---|---|
| APP-07 | Detalle busca la asignación de SU cuenta si se entra desde Inicio. Se quitó "PEDIDO ESPECIAL" del resumen de pagos | Especial desde Cliente → después, otra cuenta desde Inicio, muestra "Diaria" |
| APP-15 | Las asignaciones sin cuenta resuelta no forman un grupo vacío (se avisa); "Clientes" del Inicio se cuenta por CIF | KPI = clientes distintos en Dataverse (2) |
| APP-16 | Interacciones del cliente: consulta delegable por cuenta; pagos por la relación con la interacción (1 consulta por tarjeta) | Pestaña "(5)" = Dataverse; 0 consultas sin `$filter`; el pago QA-R1 aparece |
| APP-17 | Detalle usa `varDetTab`; la pestaña "Interacciones" de Cliente tenía el color de la otra (no se leía) | Texto blanco sobre la pestaña activa, en Cliente y en Detalle |
| APP-18 | El chip suma al texto escrito; el comentario se limpia al salir | "Hola QA. Promesa de pago"; otra cuenta abre vacío |
| APP-19 | Ya no se carga la tabla Clientes al inicio; el título sale del origen | Desde Inicio, sin pasar por Cartera: "QA-E2E-LOC CLIENTE TRES" |
| APP-24 | Teléfonos 7–20 caracteres (dígitos, espacio, + - ( )), correo válido, largo máximo en todos los campos | "12ab" bloquea con aviso; "8888-1234" habilita |
| APP-25 (parcial) | `AccessibleLabel` en íconos, campos, interruptores y botones modernos; contraste de `muted2`/`hint` ≥ 4,5:1; flechas de orden de 56 en vez de 34 | Etiquetas presentes; texto secundario #5F6B7E |
| APP-27 | El agente es una fórmula con nombre (se recalcula sola) | La interacción guardada lleva el agente vinculado |
| APP-28 | Cuentas del cliente: consulta delegable, una vez por cliente (se invalida al sincronizar) | 0 consultas sin `$filter` |
| APP-30 (parcial) | Desde Cartera, la barra vuelve a Inicio con `Back()` (no apila) | Llega a Inicio; el Atrás físico se confirma en el teléfono |
| APP-32 | Monto vacío y moneda según el saldo de la cuenta | NIO en cuenta en córdobas, USD en cuenta en dólares |
| Nuevo | Montos con `Text(…, "en-US")`: con el dispositivo en es-NI salían con coma decimal ("160,00") | "C$ 160.00" con el navegador en es-NI |

Pendiente de APP-25: los filtros de Cartera son etiquetas con `OnSelect` (no botones) y los botones clásicos no admiten etiqueta accesible.
Pendiente de APP-30 y APP-05: confirmar en el teléfono (Atrás físico; pago creado offline visible en el historial).
