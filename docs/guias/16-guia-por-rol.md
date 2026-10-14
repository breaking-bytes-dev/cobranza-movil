# Guía 16: qué puede hacer cada rol y cómo probarlo

Para cada rol de Cobranza Móvil, esta guía cuenta tres cosas:

- **qué ve y qué puede hacer** en cada app;
- **qué le impide el servidor**, aunque la pantalla lo deje intentar;
- **cómo probarlo**, a mano y con las suites automáticas.

Estado verificado en DEV el **2026-10-06**:

- **App del cobrador:** suite web 23/23 y prueba en el teléfono.
- **Backoffice:** corrida E2E 6, 47/47.
- **Privilegios:** consultados a la API de Dataverse.

> Regla de oro: la app **no** es la seguridad. Las pantallas esconden o deshabilitan lo que un rol no debe hacer, pero el
> que manda es Dataverse: roles, compartidos y plugins. Por eso cada rol tiene dos tipos de prueba: "lo que la app muestra"
> y "lo que el servidor rechaza".

---

## 1. Roles, equipos y usuarios de prueba

En DEV los roles se asignan **por equipo**, no directo al usuario. Un usuario tiene el rol porque es miembro del equipo.

| Rol (Dataverse) | Equipo en DEV | Usuario de prueba | App que usa | Sesión web |
|---|---|---|---|---|
| `SR \| CM \| Localizador` | CM · Localizadores · DEV | **User5** (pruebas automáticas); **User6** (del dueño del producto, no se usa en pruebas automáticas) | App del cobrador (teléfono) | `local/sesion-user5.json` |
| `SR \| CM \| Solicitante` | CM · Solicitantes · DEV | **User2** | Backoffice | `local/sesion-user2.json` |
| `SR \| CM \| Supervisor` | CM · Supervisores · DEV | **User3** | Backoffice | `local/sesion-user3.json` |
| `SR \| CM \| BackOffice` | CM · Backoffice · DEV | **User1** | Backoffice | `local/sesion-user1.json` |
| `SR \| CM \| Admin` | CM · Admin y Carga · DEV (con "Smelter Operador") | **User4** (User10 también está, no se toca) | Backoffice | `local/sesion-user4.json` |
| `SR \| CM \| Servicio - Cobranza` | (identidad de servicio) | — | Ninguna: flows y procesos | — |

Agentes vinculados a usuarios en DEV:

| Agente | Usuario |
|---|---|
| "QA-E2E Localizador User5" | User5 |
| "QA-E2E Localizador User6" | User6 |
| "Carlos Mendoza" | User1 (ver S-10 en la auditoría) |
| "Álvaro Zeledón" | admin |

**Apps (DEV):**
- **Cobrador:** `https://apps.powerapps.com/play/e/9c8994b8-b1bc-ee23-9184-2f6346502613/a/dc5e56bb-9598-4f68-baa5-c04dc56ccba4?tenantId=7bcfee03-896e-4ad3-947b-8a2d9ce17974`
- **Backoffice:** `https://apps.powerapps.com/play/e/9c8994b8-b1bc-ee23-9184-2f6346502613/a/87fff00d-b086-4655-9166-7a7450015fe5?tenantId=7bcfee03-896e-4ad3-947b-8a2d9ce17974`

---

## 2. Antes de probar

### 2.1 Sesiones web (para las suites automáticas)

Cada sesión se genera **en tu máquina**, nunca en un servidor compartido:

```bash
npx playwright codegen --save-storage=sesion-userN.json "<URL de la app>"
# iniciás sesión como UserN, esperás a que cargue la app y cerrás el navegador
mv sesion-userN.json <repo>/local/ && chmod 600 <repo>/local/sesion-userN.json
```

`local/` está en `.gitignore`. Las sesiones vencen: si una suite no abre la app, regenerá la sesión de ese usuario.

### 2.2 Teléfono (app del cobrador)

- **Teléfono real:** Power Apps Mobile → iniciar sesión como el cobrador → "App - Cobranza Movil". La primera vez descarga
  los datos para trabajar sin conexión (avisa "This app may now be used without an internet connection").
- **Android de pruebas (redroid):** `~/android/and.py` para capturas y toques. Para simular "señal sin datos":
  - cortar la salida a internet:
    ```bash
    docker exec redroid sh -c "ip route del default via 172.17.0.1 dev eth0 table eth0"
    ```
  - restaurarla:
    ```bash
    docker exec redroid sh -c "ip route add default via 172.17.0.1 dev eth0 table eth0"
    ```
- El Android de pruebas **no tiene GPS**. La prueba de coordenadas reales (APP-11) se hace en un teléfono físico.

### 2.3 Reglas de datos de prueba

- Todo lo que crees lleva el prefijo **QA-E2E** (o "QA" en el comentario) y se borra al terminar. Las suites ya limpian lo
  que crean.
- **Nunca** vincular ni desvincular agentes reales, ni pulsar el **cierre en lote** de Reconciliación con datos reales.
- Datos fijos que **no** se borran (las suites dependen de ellos):

  | Dato fijo | Qué es | Lo usa |
  |---|---|---|
  | QA-R1 (en IN-1065) | Pago | APP-05 |
  | AS-2077 | Especial aprobada, cuenta 5498 | APP-07 |
  | AS-2134 | Especial en Borrador cuyo dueño es User5 | APP-08 |

- Nunca escribir ni copiar un número de tarjeta completo en capturas, mensajes o tickets.

### 2.4 Cómo leer la matriz de privilegios

C = crear, R = leer, W = escribir, D = borrar, A = vincular (Append), T = ser vinculado (Append To), G = asignar dueño, S = compartir.
Alcance: **U** = solo lo propio o lo compartido con el usuario; **G** = toda la organización.

---

## 3. Matriz de privilegios (Dataverse, verificada el 2026-10-06)

| Tabla | Localizador | Solicitante | Supervisor | BackOffice | Admin | Servicio |
|---|---|---|---|---|---|---|
| Asignación | R/W/A/T: U | **C: G**, R/W: U, A/T: G | R/W/A/T: **G** | R: G | todo: G | C/R/W/A/T/G: G |
| Cuenta | R: U | R/A/T: G | R: G | R: G | todo: G | C/R/W/A/T: G |
| Cliente | R: U | R/A/T: G | R: G | R: G | todo: G | C/R/W/A/T: G |
| PAN de cuentas (bóveda) | R: U | — | — | **R: G** | — | — |
| Interacción | C/R/W/A/T: U | — | R: G | R/**W**/A: G | todo: G | — |
| Pago | C/R/W/A/T/**G**: U | — | R: G | R: G | todo: G | — |
| Actualización de datos | C/R/W/A/T: U | — | — | R: G | todo: G | — |
| Agente | R/T: G | R/T: G | R/T: G | R: G | todo: G | C/R/W/A/T: G |
| Carga diaria | — | — | — | R: G | todo: G | C/R/W/A/T: G |
| Dirección / Teléfono | R: U | R/A/T: G | R: G | R: G | todo: G | C/R/W/A/T: G |

Lo que dice la matriz en palabras:
- **Localizador:** lee solo lo que el reparto le comparte (sus cuentas asignadas) y crea interacciones, pagos y
  actualizaciones de datos. **No puede editar ni reasignar lo que ya capturó** (S-9, cerrado 2026-10-14).
- **Solicitante y Supervisor:** leen todas las cuentas y clientes porque necesitan buscar cualquiera. Es una decisión de
  diseño.
- **BackOffice:** el **único** rol de persona que lee la bóveda: ve el número completo para transcribirlo al core.
- **Admin:** puede todo, incluso borrar.

---

## 4. Localizador (cobrador): app del cobrador, en el teléfono

### 4.1 Qué ve y qué puede hacer

| Pantalla | Qué ve | Qué puede hacer |
|---|---|---|
| **Inicio** | "Cobrado hoy" (C$ y US$), interacciones, cuentas y clientes de hoy; lista por cliente o por cuenta | Abrir un cliente o una cuenta; "Ver detalle" del cobrado |
| **Cartera** | Sus clientes con asignación activa, saldo vencido total, "N cuentas · M clientes" | Filtrar (Todos, Diaria, Incobrable, Especial), buscar, **ordenar la ruta** con las flechas, refrescar (↻) |
| **Cliente** | Cuentas del cliente, teléfonos, direcciones, historial de **sus** interacciones con pagos | Llamar, abrir el mapa, abrir una cuenta |
| **Detalle de cuenta** | Tipo (Diaria, Incobrable, Especial), saldos, mora; si es Especial: retiro y nota; historial | **Ver el número completo** (se oculta solo), registrar una interacción |
| **Interacción** | Datos de la cuenta, productos a los que aplica, ubicación (GPS) | Comentario (con respuestas rápidas), **pagos** (monto, moneda, recibo), **actualización de datos** (teléfonos, direcciones, correo); guardar |
| **Pagos del día** | Lo cobrado hoy, por moneda | Solo ver |
| **Sincronización** | Estado real de la sincronización, capturas de hoy ("pendiente de subir" o "subida") | "Buscar actualizaciones", "Sincronización completa" |

Ajustes ("Aa"): tema día o noche y tamaño de letra.

### 4.2 Reglas que aplica la app

**Guardar una interacción:**
- El comentario es obligatorio.
- Cada línea de pago necesita monto mayor que 0 y recibo (hasta 30 caracteres, sin repetir).
- Si el monto supera el saldo vencido, pide confirmación con un segundo toque ("Confirmar pago").
- Sin GPS, la tarjeta de ubicación se pone ámbar y guardar pide confirmación ("Guardar sin ubicación").
- El botón se bloquea mientras guarda (no hay doble toque).

**Actualización de datos:**
- Teléfonos de 7 a 20 caracteres (dígitos, espacio, `+ - ( )`).
- Correo válido.
- Largos máximos en todos los campos.

**Sin conexión:**
- Todo se guarda en el teléfono y sube al sincronizar.
- La píldora pasa a "Pendiente de subir" y vuelve a "Sincronizado" cuando sube.

**Qué asignaciones muestra:**
- Solo **activas**.
- Una **Especial sin aprobar** (Borrador, Pendiente o Rechazada) no se muestra.

### 4.3 Qué le impide el servidor

| Intento | Quién lo frena | Resultado |
|---|---|---|
| Ver cuentas, clientes, interacciones o pagos de otro cobrador | Rol en "U" + compartidos del flow | No llegan, ni a la app ni a la API |
| Ver una Especial antes de que se apruebe | Flow (no comparte) + app (filtro) | No aparece |
| Aprobar o asignarse una Especial (por la API) | Plugin `AprobacionEspecial` | Error "solo un Supervisor puede aprobar, rechazar o asignar el agente…" |
| Crear una actualización de datos sin interacción, o con una interacción sin cuenta | Plugin `ActualizacionConInteraccion` | Error "…tiene que estar vinculada a una interaccion" |
| Leer el número de tarjeta de una cuenta que no es suya | Bóveda con lectura "U" | No llega |
| Que el cobrador edite o reasigne un pago, una interacción o una actualización de datos ya capturados | Rol Localizador sin Write ni Assign (S-9) | Error de privilegio (`prvWritesanic_cm_tbl_pago`, …) |

**Al sincronizar**, el servidor completa:
- la cuenta del pago;
- el N° de interacción del pago (plugin `CuentaDesdeInteraccion`);
- el agente: la app lo busca por el correo del agente, que el plugin `EmailDesdeUsuario` mantiene igual al UPN del
  usuario, en minúsculas.

### 4.4 Cómo probarlo a mano (teléfono)

| # | Paso | Resultado esperado |
|---|---|---|
| L-1 | Abrir la app con el cobrador | Inicio con su nombre, píldora "Sincronizado" |
| L-2 | Cartera | Solo sus clientes. El conteo "N cuentas" coincide con sus asignaciones activas, sin contar Especiales sin aprobar |
| L-3 | Filtro "Especial" | Solo Especiales **aprobadas** |
| L-4 | Abrir una cuenta → "Ver número" | Número completo unos segundos; después vuelve a `•••• 1234` |
| L-5 | Registrar interacción con pago (monto y recibo) → Guardar | "✓ Guardado". El pago aparece en el historial de la cuenta y en "Cobrado hoy" |
| L-6 | Mismo paso, pero monto mayor que el saldo vencido | El botón dice "Confirmar pago"; el segundo toque guarda |
| L-7 | Recibo repetido, o pago sin recibo | El botón queda bloqueado y el aviso dice qué falta |
| L-8 | Actualizar datos: teléfono "12ab" | Bloquea con aviso. Con "8888-1234" habilita |
| L-9 | **Modo avión** (o sin datos): registrar interacción con pago | Guarda; píldora "Pendiente de subir"; el pago se ve en el historial y suma a "Cobrado hoy" |
| L-10 | Volver a la conexión | Píldora "Sincronizado". En el backoffice (User1) la interacción aparece en "Por transcribir", con agente y N° de interacción |
| L-11 | Con GPS activado: registrar | Tarjeta "Ubicación capturada" con 5 decimales. En Dataverse, latitud y longitud con 5 o 6 decimales (**APP-11, pendiente en teléfono real**) |
| L-12 | Sin GPS: registrar | Tarjeta ámbar; "Guardar sin ubicación" pide un segundo toque |
| L-13 | Sincronización → "Capturas de hoy" | Las del día, la más reciente arriba, con "pendiente de subir" o "subida" |

### 4.5 Pruebas automáticas

**Web:** `tests/localizador-canvas`:

```bash
NODE_PATH=../backoffice-canvas/node_modules node grupo2.js
```

- Corre como User5, 23 chequeos.
- Crea sus datos del día y los borra.
- Lo offline **no** se prueba en web: el navegador corre siempre en línea.

**Servidor:** pruebas de los plugins en CI (`.github/workflows/pan-plugin-cm.yml`, al hacer push a `feat/pan-plugin-cm`): 169 tests.

---

## 5. Solicitante: backoffice

### 5.1 Qué ve y qué puede hacer

| Pantalla | Qué puede hacer |
|---|---|
| **Inicio** | Ver PENDIENTES, APROBADAS y RECHAZADAS de **sus** Especiales ("Tus solicitudes Especiales"). No ve "Cobrado hoy" |
| **Nueva solicitud** | Buscar una cuenta (por últimos 4, CIF o nombre), elegir el tipo, el retiro NIO/USD y el motivo, y **enviar** la Especial. Queda **Pendiente de aprobación**, **sin agente** |
| **Mis solicitudes** | Seguir sus Especiales: estado, motivo de rechazo y agente asignado. Solo lectura |

**Reglas de "Enviar":**
- Exige cuenta, tipo, retiro mayor que 0 y motivo.
- Si la cuenta ya tiene una Especial **activa**, avisa y no crea nada.
- Si tiene una **cerrada**, la **reactiva** (el mismo registro, con los datos nuevos).

### 5.2 Qué le impide el servidor

| Intento | Quién lo frena | Resultado |
|---|---|---|
| Crear una Especial **con agente** o **ya aprobada** (por la API) | Plugin `AprobacionEspecial` | Error "solo un Supervisor…" |
| Aprobar, rechazar o cambiar el agente de una Especial | Plugin `AprobacionEspecial` | Error "solo un Supervisor…" |
| Leer pagos, carga diaria o el número completo | Rol sin esos privilegios | 403 o vacío |
| Ver Especiales de otros solicitantes | Rol en "U" (solo las suyas o compartidas) | No aparecen |

Después de que se aprueba (el dueño pasa al cobrador), el Solicitante **sigue viendo** su Especial: el flow se la comparte.

### 5.3 Cómo probarlo a mano (User2)

| # | Paso | Resultado esperado |
|---|---|---|
| SO-1 | Abrir el backoffice | Menú con solo Inicio, Nueva solicitud y Mis solicitudes |
| SO-2 | Nueva solicitud → buscar una cuenta QA por sus últimos 4 | Aparece en la galería con número enmascarado y cliente |
| SO-3 | Completar tipo, retiro y motivo → Enviar | Pasa a "Mis solicitudes" como **Pendiente**, sin agente |
| SO-4 | Repetir sobre la misma cuenta | Aviso "Ya existe una Especial ACTIVA…", no crea otra |
| SO-5 | En el teléfono del cobrador | La Especial **no** aparece (todavía no está aprobada) |
| SO-6 | Después de que el Supervisor la apruebe (SU-3) | En "Mis solicitudes" figura **Aprobada** con el agente |

### 5.4 Pruebas automáticas

`tests/backoffice-canvas`, fases B y G: R-03, E-01b, D-02e, D-04, F-02a, F-02, F-02b, X-02, F-02d, D-05, F-02e y F-02c.

---

## 6. Supervisor: backoffice

### 6.1 Qué ve y qué puede hacer

| Pantalla | Qué puede hacer |
|---|---|
| **Inicio** | Ver POR APROBAR y "Cobrado hoy" |
| **Aprobaciones** | Ver la cola de Especiales **pendientes y activas de todos los solicitantes**. Elegir una y: **aprobar eligiendo el localizador** (buscador de agentes activos con usuario) o **rechazar con motivo** |
| **Cartera por localizador** (desde 2026-10-09) | Ver cada localizador con sus asignaciones **activas** por tipo (Diaria / Incobrable / Especial). Al elegir uno se abre el detalle: cliente, cuenta enmascarada, tipo, estado de aprobación, mora y saldo. Solo lectura. Las Especiales en Borrador o Pendiente cuentan acá aunque el cobrador no las vea en el teléfono |
| **Historial de Especiales** (desde 2026-10-09) | Ver las Especiales **aprobadas y rechazadas**: quién la pidió, quién la resolvió, a qué localizador fue, motivo de rechazo, retiro y si sigue activa o cerrada. Filtros por resultado, por los últimos 7/30/90 días (último cambio) y por activas/cerradas. Solo lectura |

Al **aprobar** se guardan, en un solo cambio: estado Aprobado, el agente y el Supervisor como resolutor. Después, el flow:
- pasa el dueño de la asignación al usuario del agente;
- le comparte la cuenta y el cliente al cobrador.

En unos segundos la Especial le aparece al cobrador en la próxima sincronización.

### 6.2 Qué le impide el servidor

| Intento | Resultado |
|---|---|
| Aprobar o rechazar | **Permitido** (es su función). El plugin lo reconoce aunque el rol le llegue por equipo |
| Crear una solicitud | No tiene privilegio de creación |
| **Escribir cualquier otra columna de cualquier asignación** (por la API) | **Hoy se puede** (S-11, abierto: escritura Global) |

### 6.3 Cómo probarlo a mano (User3)

| # | Paso | Resultado esperado |
|---|---|---|
| SU-1 | Abrir el backoffice | Menú con solo Inicio y Aprobaciones |
| SU-2 | Aprobaciones | La Especial QA del Solicitante (SO-3) está en la cola. "Aprobar" deshabilitado hasta elegir un localizador |
| SU-3 | Buscar el localizador QA (User5) → Aprobar | La fila sale de la cola. En pocos segundos, el dueño de la asignación es User5 |
| SU-4 | Teléfono de User5 → Cartera (sincronizar) | La Especial aparece, con su retiro y nota |
| SU-5 | Otra Especial QA → Rechazar sin motivo | Deshabilitado. Con motivo: sale de la cola como **Rechazada** y nunca llega al teléfono |

### 6.4 Pruebas automáticas

Fase C: R-04, E-01c, D-02f, F-03, F-03c (el flow mueve el dueño) y F-03b.

---

## 7. BackOffice: backoffice

### 7.1 Qué ve y qué puede hacer

| Pantalla | Qué puede hacer |
|---|---|
| **Inicio** | Ver EN COLA, CON PAGO, TRANSCRITAS HOY y "Cobrado hoy" |
| **Por transcribir** | Cola de las interacciones **pendientes** de todos los cobradores (la más vieja primero). Para cada una ve: cliente, cuenta con el **número completo** (bóveda; el único rol que lo ve), comentario, pagos y la actualización de datos (antes y después). Escribe el **código del core**: **Enter (o Tab) lleva el foco a "Grabar" y Enter otra vez graba** (desde 2026-10-09: Power Apps no distingue Enter de Tab ni de un clic afuera, y antes cualquiera de los tres grababa) |
| **Transcritas hoy** | Arqueo del día: lo que **él** grabó hoy, con sus pagos. Solo lectura |

**Al grabar** se guarda: estado Grabado, código (en mayúsculas, hasta 10), fecha y "grabado por". Después avanza a la
siguiente. Si falla, avisa y la interacción **queda** en la cola.

### 7.2 Qué le impide el servidor

| Intento | Resultado |
|---|---|
| Escribir pagos, clientes, teléfonos o direcciones | Sin privilegio |
| Crear o aprobar asignaciones | Sin privilegio |
| Leer el número completo | **Permitido** (es su función). Cualquier otro rol de persona recibe 403 o vacío |

### 7.3 Cómo probarlo a mano (User1)

| # | Paso | Resultado esperado |
|---|---|---|
| BO-1 | Abrir el backoffice | Menú con solo Inicio, Por transcribir y Transcritas hoy |
| BO-2 | Por transcribir | La interacción QA del cobrador (L-5 o L-9) está en la cola, con agente, cuenta, número completo y su pago |
| BO-3 | Escribir un código QA → Enter | "✓ IN-xxxx grabada — siguiente…"; sale de la cola |
| BO-4 | Transcritas hoy | Aparece con su pago; el total coincide con lo grabado |

### 7.4 Pruebas automáticas

Fase D: R-02, E-01d, D-02g, D-02h, F-01, F-01b, H-03 y R-06a/b (multirol).

---

## 8. Admin: backoffice

### 8.1 Qué ve y qué puede hacer

| Pantalla | Qué puede hacer |
|---|---|
| **Inicio** | Ver CARGA DE HOY, DIARIAS FUERA DE CARTERA, AGENTES SIN USUARIO y "Cobrado hoy" |
| **Monitoreo de carga** | Ver si llegó la carga del día (filas, cuentas, clientes, última carga). Solo lectura |
| **Reconciliación** | Ver las Diarias que **desaparecieron** de la última carga y **cerrarlas en lote** ("fuera de cartera"). Freno: no deja cerrar si superan el 30% o si no hay carga de hoy |
| **Cierres** | Cerrar a mano Incobrables (Recuperada, Baja/castigada, Fuera de cartera) y Especiales (Gestión completada, Ya no aplica, Solicitud errónea), con detalle opcional |
| **Agentes y usuarios** | **Vincular** un agente a un usuario, cambiarlo, **desvincularlo**, activarlo o desactivarlo. **＋ Nuevo agente** (desde 2026-10-09): nombre (tiene que ser **exacto** al que manda el banco en el CSV, o la próxima carga crea otro agente) y usuario opcional; bloquea nombres repetidos |
| **Cargar archivo** | Subir el CSV del día sin entrar a Smelter: **＋ Elegir el archivo .csv** → **Validar (prueba en seco)** → si sale *Terminada* con 0 fallos se habilita **Cargar** → confirmar. Muestra solo contadores (filas, procesadas, éxitos, fallos, omitidas); si hay fallos dice *Avisá a soporte con el nombre de la carga*. Abajo, **Mis últimas cargas** (por *Creado por*: el job queda a nombre del team «CM · Admin y Carga», porque el rol Smelter Operador llega por el team) |
| **Datos maestros** | Buscar clientes por CIF o nombre; **crear** un cliente (con anti-duplicado de CIF), editar nombre y datos, agregar teléfono o dirección |

**Al vincular un agente**, el servidor (plugin `EmailDesdeUsuario`) deja el correo del agente igual al **UPN** del usuario,
en minúsculas. Es lo que la app del cobrador usa para saber quién es.

### 8.2 Qué le impide el servidor

- Es el rol con más poder: **crea, edita y borra todo**. No lee la bóveda.
- Aprueba Especiales por la API: el plugin lo permite.
- Las acciones peligrosas (cierre en lote) se frenan **en la app**, no en el servidor.

### 8.3 Cómo probarlo a mano (User4)

| # | Paso | Resultado esperado |
|---|---|---|
| AD-1 | Abrir el backoffice | Menú Admin (6 pantallas) |
| AD-2 | Monitoreo | Los conteos coinciden con la carga del día |
| AD-3 | Reconciliación sin carga de hoy | Card de bloqueo y "Cerrar" **deshabilitado**. **No pulsar el cierre en lote con datos reales** |
| AD-4 | Cierres → la Especial QA aprobada en SU-3 → "Gestión completada" | Se cierra. En la próxima sincronización desaparece del teléfono y el cobrador deja de ver su cuenta (el flow revoca el compartido) |
| AD-5 | Agentes → vincular un agente QA a un usuario QA | El correo del agente queda igual al UPN del usuario, en minúsculas |
| AD-6 | Datos maestros → crear un cliente QA con un CIF existente | Aviso de duplicado; no crea |

### 8.4 Pruebas automáticas

Fases A, F y X: R-05, E-01a, D-02a–d, F-05, F-04c, D-03, F-04b, F-04, D-07, F-05b y X-01.

```bash
cd tests/backoffice-canvas
BO_RESULTADOS_DIR=$PWD/resultados/corrida-N node run.js            # todas las fases, ~15 min
node run.js --fases A,X                                           # algunas (B→C→F→G van en ese orden)
node run.js --solo-limpieza                                       # borra restos QA-E2E
```

El cierre en lote de Reconciliación **nunca** se pulsa en las pruebas: solo se verifica que esté deshabilitado.

---

## 9. Recorrido completo entre roles (prueba de punta a punta)

Es la prueba que mejor muestra que los roles encajan. Usa una cuenta QA del cobrador User5.

1. **Solicitante (User2):** pide una Especial (SO-3). Queda Pendiente, sin agente.
2. **Cobrador (User5):** no la ve (SO-5).
3. **Supervisor (User3):** la aprueba asignando a User5 (SU-3). El dueño pasa a User5.
4. **Cobrador:** sincroniza y la ve (SU-4). Registra una interacción con pago, primero en modo avión y después con conexión (L-9, L-10).
5. **BackOffice (User1):** la transcribe (BO-2 y BO-3) y la ve en el arqueo (BO-4).
6. **Admin (User4):** cierra la Especial (AD-4). El cobrador deja de verla.
7. **Solicitante:** en "Mis solicitudes" sigue viendo su Especial (F-02e).

Limpieza: borrar la interacción, el pago y la Especial QA, o correr `node run.js --solo-limpieza` para lo del backoffice.

---

## 10. Pruebas de "lo que no debe poder" (por la API)

La app esconde; el servidor tiene que **rechazar**. Estas pruebas se hacen por la Web API suplantando al usuario con el
encabezado `MSCRMCallerID: <systemuserid>`, usando la cuenta de servicio de DEV. Siempre sobre registros QA.

| Como | Intento | Esperado |
|---|---|---|
| User5 (Localizador) | `PATCH` de su Especial QA a `estadoaprobacion = 3` | 400 "solo un Supervisor…" |
| User5 | `POST` de una actualización de datos sin interacción | 400 "…vinculada a una interaccion" |
| User2 (Solicitante) | `POST` de una Especial con agente | 400 "solo un Supervisor…" |
| User2 / User3 / User4 | `GET` de la bóveda `sanic_cm_tbl_cuentapans` | 403 o vacío (H-01) |
| Sin roles de CM (carga de Smelter) | `POST` de una Especial en Borrador con agente | Se permite. El flow **no** la comparte hasta que se aprueba |

---

## 11. Riesgos conocidos al probar

**El backoffice no controla el rol en cada pantalla.**
- El menú decide qué se ve. Si alguien llega a una pantalla ajena, solo lo frena Dataverse.
- El rol se deduce del **nombre del equipo** ("CM · Solicitantes · …"): renombrar un equipo deja al usuario "Sin acceso".

**Identificación del usuario en el backoffice.**
- La app compara `'Primary Email' = Lower(User().Email)`, pero `User().Email` es el **UPN**.
- En DEV el UPN y el correo coinciden en todos los usuarios, así que no falla.
- Si en PROD un usuario tiene un correo distinto de su cuenta de inicio de sesión, verá "Sin acceso". Mismo problema que
  se corrigió en la app del cobrador el 2026-10-06; está pendiente en el backoffice.

**Huecos de seguridad abiertos**, que una prueba puede "lograr" y conviene registrar como hallazgo, no como falla de la
prueba (detalle en `docs/auditoria/00-lista-consolidada.md`):
- **S-11:** el Supervisor escribe cualquier columna.
- **S-4:** los roles CM ya no crean flows, pero `Basic User` y roles de otras soluciones del entorno sí (y exportan a Excel).

**Cobrador:**
- El historial offline es de **90 días**; lo más viejo se ve solo con señal.
- Una cartera de más de **2000** filas por colección muestra un aviso de que pueden faltar datos.
