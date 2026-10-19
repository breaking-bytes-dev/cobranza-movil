# Matriz de roles contra el servidor ("rol por pantalla")

> Corrida `U9V` del 2026-10-19 17:38 UTC en DEV, generada por `tests/integral/seguridad/matriz.js`.
> Lo esperado está en [`tests/integral/seguridad/esperado.js`](../../tests/integral/seguridad/esperado.js), con el porqué de cada fila.

**Cómo se lee.** Cada celda es lo que respondió el servidor cuando ese usuario, sin pasar por la app, intentó la operación: ✔ la hizo (código 2xx), ✘ la frenó (403 = sin privilegio, 400 = regla de un plugin, 404 = no la ve). Las filas son datos QA creados por la prueba y borrados al final.

| | Celdas |
|---|---|
| Probadas | 446 (413 de la matriz y 33 casos de columna) |
| Coinciden con lo esperado | 379 |
| **HUECO** (el servidor deja hacer algo que el rol no debería) | **0** |
| **DEMASIADO CERRADO** (el servidor frena algo que una pantalla necesita) | **0** |
| EXPECTATIVA DUDOSA (ninguna pantalla lo usa; decide el dueño) | 67 (67 permitidas hoy) |
| Limpieza | 0 restos |

Contexto: en DEV los usuarios de prueba también tienen roles de otras soluciones (SEC-17, riesgo aceptado). Lo que mide la matriz es el acceso **efectivo**; en esta corrida se verificó que ninguno de esos roles da privilegios sobre tablas de CM.

## Diferencias

### HUECO (0)

Ninguna.

### DEMASIADO CERRADO (0)

Ninguna.

### EXPECTATIVA DUDOSA (67)

| Rol | Qué intentó | Esperado | Servidor | Detalle |
|---|---|---|---|---|
| Admin (User4) | crear · cuenta | d | ✔ 201 |  |
| Admin (User4) | editar ajena · cuenta | d | ✔ 200 |  |
| Admin (User4) | editar la propia · cuenta | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · cuenta | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · cuenta | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · cuenta | d | ✔ 204 |  |
| Admin (User4) | borrar ajena · cliente | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · cliente | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · cliente | d | ✔ 204 |  |
| Admin (User4) | crear · asignacion | d | ✔ 201 |  |
| Admin (User4) | borrar ajena · asignacion | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · asignacion | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · asignacion | d | ✔ 204 |  |
| Admin (User4) | crear · interaccion | d | ✔ 201 |  |
| Admin (User4) | editar ajena · interaccion | d | ✔ 200 |  |
| Admin (User4) | editar la propia · interaccion | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · interaccion | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · interaccion | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · interaccion | d | ✔ 204 |  |
| Admin (User4) | crear · pago | d | ✔ 201 |  |
| Admin (User4) | editar ajena · pago | d | ✔ 200 |  |
| Admin (User4) | editar la propia · pago | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · pago | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · pago | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · pago | d | ✔ 204 |  |
| Admin (User4) | crear · actualizacion | d | ✔ 201 |  |
| Admin (User4) | editar ajena · actualizacion | d | ✔ 200 |  |
| Admin (User4) | editar la propia · actualizacion | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · actualizacion | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · actualizacion | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · actualizacion | d | ✔ 204 |  |
| Admin (User4) | borrar ajena · agente | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · agente | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · agente | d | ✔ 204 |  |
| Admin (User4) | crear · producto | d | ✔ 201 |  |
| Admin (User4) | editar ajena · producto | d | ✔ 200 |  |
| Admin (User4) | editar la propia · producto | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · producto | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · producto | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · producto | d | ✔ 204 |  |
| Admin (User4) | crear · fiador | d | ✔ 201 |  |
| Admin (User4) | leer ajena · fiador | d | ✔ 200 |  |
| Admin (User4) | editar ajena · fiador | d | ✔ 200 |  |
| Admin (User4) | editar la propia · fiador | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · fiador | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · fiador | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · fiador | d | ✔ 204 |  |
| Admin (User4) | borrar ajena · direccion | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · direccion | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · direccion | d | ✔ 204 |  |
| Admin (User4) | borrar ajena · telefono | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · telefono | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · telefono | d | ✔ 204 |  |
| Admin (User4) | crear · carga | d | ✔ 201 |  |
| Admin (User4) | editar ajena · carga | d | ✔ 200 |  |
| Admin (User4) | editar la propia · carga | d | ✔ 200 |  |
| Admin (User4) | borrar ajena · carga | d | ✔ 204 |  |
| Admin (User4) | reasignarse ajena · carga | d | ✔ 200 |  |
| Admin (User4) | compartir ajena · carga | d | ✔ 204 |  |
| BackOffice (User1) | leer ajena · fiador | d | ✔ 200 |  |
| Solicitante (User2) | leer ajena · fiador | d | ✔ 200 |  |
| Solicitante (User2) | leer ajena · direccion | d | ✔ 200 |  |
| Solicitante (User2) | leer ajena · telefono | d | ✔ 200 |  |
| Supervisor (User3) | leer ajena · fiador | d | ✔ 200 |  |
| Supervisor (User3) | leer ajena · direccion | d | ✔ 200 |  |
| Supervisor (User3) | leer ajena · telefono | d | ✔ 200 |  |
| Admin (User4) | COL-ADM-1: Admin cambia el retiro de una Especial aprobada | d | ✔ 200 |  |

## Propuestas (no aplicadas: decide el dueño)

Verificado en la corrida: ninguno de los roles de otras soluciones que tienen los usuarios QA (SEC-17) da privilegios sobre tablas de CM, así que todo lo de abajo sale de los roles y plugins de CM.

| # | Hueco | Causa | Propuesta | Estado |
|---|---|---|---|---|
| H1 | Solicitante edita la Especial: la propia Pendiente (retiro, notas) y, ya aprobada y compartida por S-7, el retiro, las notas y hasta la cierra (Up asignación, COL-SOL-1, 4, 5 y 6) | El rol Solicitante tiene Write Basic en asignación; `ColumnasAsignacion` le deja retiro, notas, estado y motivo (era para volver a pedir una Especial cerrada desde la pantalla, que desde DAT-14 hace `SolicitarEspecial` con el servicio); `RepartoAsignacion` le comparte la aprobada con lectura, escritura y anexar | (1) Dejar al Solicitante sin columnas en `ColumnasAsignacion` (falla cerrado); (2) que el compartido de S-7 sea **solo lectura** (Mis solicitudes solo lee); (3) opcional: quitarle Write en asignación al rol. `SolicitarEspecial` no se rompe: crea con Create y reactiva con el servicio | Cerrado el 2026-10-19 (plugins 1.0.8.4) |
| H2 | Solicitante crea una Diaria activa con un agente: el reparto se la manda al cobrador sin pasar por Aprobaciones (COL-SOL-7) | Create Global en asignación; `AprobacionEspecial` solo controla Especiales | En el alta, `AprobacionEspecial` exige que una persona de CM que no sea Supervisor, Admin o Servicio solo cree **Especiales Pendientes y sin agente** | Cerrado el 2026-10-19 (plugins 1.0.8.4) |
| H3 | El cobrador registra una visita y un pago en una cuenta que **no es de su cartera** (COL-COB-6 y 7) | El rol Localizador tiene AppendTo Global en cuenta (lo exige el lookup); `InteraccionDelCobrador` pone el agente pero no mira la cartera | En `InteraccionDelCobrador`, si llama un Localizador: exigir que la cuenta tenga una asignación **activa de su cartera** (dueño = el llamador). El pago hereda la cuenta de la interacción (`CuentaDesdeInteraccion`), así que alcanza con frenar la interacción. Bajar AppendTo a Basic no sirve: el cobrador no es dueño de las cuentas, solo las tiene compartidas | Cerrado el 2026-10-19 (plugins 1.0.8.4) |

**Dudosas: una decisión cada una.**

- **Admin (la mayoría de las dudosas):** tiene Global en todo CM, así que puede borrar, reasignar y compartir cualquier fila, y crear o editar interacciones, pagos, cargas diarias y cuentas, aunque ninguna pantalla lo haga. Opciones: aceptarlo como administrador funcional (riesgo aceptado), o mínimo privilegio: quitar Delete, Assign y Share en todas las tablas y Create/Write en interacción, pago, actualización de datos, carga diaria y cuenta (Alta de cuenta va por Smelter, no lo necesita).
- **Lecturas de fiador, dirección y teléfono** para BackOffice, Solicitante y Supervisor: ninguna pantalla suya las muestra (el BackOffice sí lee direcciones y teléfonos en Transcripción). Opciones: quitar la lectura de fiador a los tres y la de dirección y teléfono a Solicitante y Supervisor, o aceptarlas.

## Matriz completa

### Admin (User4)

| Tabla | crear | leer ajena | leer de su cartera | editar ajena | editar la propia | borrar ajena | reasignarse ajena | compartir ajena |
|---|---|---|---|---|---|---|---|---|
| cuenta | ✔ 201 (d) | ✔ 200 |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| cliente | ✔ 201 | ✔ 200 |  | ✔ 200 | ✔ 200 | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| asignacion | ✔ 201 (d) | ✔ 200 |  | ✔ 200 | ✔ 200 | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| interaccion | ✔ 201 (d) | ✔ 200 |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| pago | ✔ 201 (d) | ✔ 200 |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| actualizacion | ✔ 201 (d) | ✔ 200 |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| agente | ✔ 201 | ✔ 200 |  | ✔ 200 | ✔ 200 | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| producto | ✔ 201 (d) | ✔ 200 |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| fiador | ✔ 201 (d) | ✔ 200 (d) |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| direccion | ✔ 201 | ✔ 200 |  | ✔ 200 | ✔ 200 | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| telefono | ✔ 201 | ✔ 200 |  | ✔ 200 | ✔ 200 | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| carga | ✔ 201 (d) | ✔ 200 |  | ✔ 200 (d) | ✔ 200 (d) | ✔ 204 (d) | ✔ 200 (d) | ✔ 204 (d) |
| boveda | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |

### BackOffice (User1)

| Tabla | crear | leer ajena | leer de su cartera | editar ajena | editar la propia | borrar ajena | reasignarse ajena | compartir ajena |
|---|---|---|---|---|---|---|---|---|
| cuenta | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| cliente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| asignacion | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| interaccion | ✘ 403 | ✔ 200 |  | ✘ 400 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| pago | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| actualizacion | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| agente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| producto | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| fiador | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| direccion | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| telefono | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| carga | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| boveda | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |

### Solicitante (User2)

| Tabla | crear | leer ajena | leer de su cartera | editar ajena | editar la propia | borrar ajena | reasignarse ajena | compartir ajena |
|---|---|---|---|---|---|---|---|---|
| cuenta | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| cliente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| asignacion | ✔ 201 | ✘ 403 |  | ✘ 403 | ✘ 400 | ✘ 403 | ✘ 403 | ✘ 403 |
| interaccion | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| pago | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| actualizacion | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| agente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| producto | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| fiador | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| direccion | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| telefono | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| carga | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| boveda | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |

### Supervisor (User3)

| Tabla | crear | leer ajena | leer de su cartera | editar ajena | editar la propia | borrar ajena | reasignarse ajena | compartir ajena |
|---|---|---|---|---|---|---|---|---|
| cuenta | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| cliente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| asignacion | ✘ 403 | ✔ 200 |  | ✔ 200 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| interaccion | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| pago | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| actualizacion | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| agente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| producto | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| fiador | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| direccion | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| telefono | ✘ 403 | ✔ 200 (d) |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| carga | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| boveda | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |

### Localizador (User5)

| Tabla | crear | leer ajena | leer de su cartera | editar ajena | editar la propia | borrar ajena | reasignarse ajena | compartir ajena |
|---|---|---|---|---|---|---|---|---|
| cuenta | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| cliente | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| asignacion | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| interaccion | ✔ 201 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 | ✘ 403 | ✘ 403 |
| pago | ✔ 201 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 | ✘ 403 | ✘ 403 |
| actualizacion | ✔ 201 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 | ✘ 403 | ✘ 403 |
| agente | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| producto | ✘ 403 | ✔ 200 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| fiador | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| direccion | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| telefono | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| carga | ✘ 403 | ✘ 403 |  | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |
| boveda | ✘ 403 | ✘ 403 | ✔ 200 | ✘ 403 |  | ✘ 403 | ✘ 403 | ✘ 403 |

## Casos de columna y de flujo

| ID | Rol | Caso | Esperado | Servidor | Por qué |
|---|---|---|---|---|---|
| COL-SOL-1 | Solicitante (User2) | Solicitante cambia el retiro de su Especial Pendiente | n | ✘ 400 | Desde DAT-14 ninguna pantalla del Solicitante edita la asignación |
| COL-SOL-2 | Solicitante (User2) | Solicitante aprueba su propia Especial | n | ✘ 400 | S-6: solo Supervisor/Admin aprueban |
| COL-SOL-3 | Solicitante (User2) | Solicitante se asigna el agente de su Especial | n | ✘ 400 | S-6: el agente lo pone el Supervisor |
| COL-SUP-1 | Supervisor (User3) | Supervisor cambia el retiro | n | ✘ 400 | S-11: el Supervisor aprueba, no cambia montos |
| COL-SUP-2 | Supervisor (User3) | Supervisor cambia el orden | n | ✘ 400 | S-11: el orden es del cobrador |
| COL-SUP-3 | Supervisor (User3) | Supervisor se pone como "Solicitado por" | n | ✘ 400 | S-7 / S-11 |
| COL-BO-A | BackOffice (User1) | BackOffice cambia las notas de la Especial | n | ✘ 403 | Ninguna pantalla del BackOffice edita asignaciones |
| COL-COB-0 | Localizador (User5) | Cobrador cambia el orden de una Especial que no es suya | n | ✘ 403 | S-1: solo su cartera |
| COL-SUP-4 | Supervisor (User3) | Supervisor aprueba con el localizador (como Aprobaciones) | s | ✔ 200 | Aprobaciones |
| COL-SOL-4 | Solicitante (User2) | Solicitante cambia el retiro de la Especial YA APROBADA (compartida por S-7) | n | ✘ 403 | El monto aprobado no lo cambia quien la pidió |
| COL-SOL-5 | Solicitante (User2) | Solicitante cierra la Especial aprobada | n | ✘ 403 | Cerrar es de Cierres (Admin), del cobrador al cobrar o del Supervisor al rechazar |
| COL-SOL-6 | Solicitante (User2) | Solicitante cambia las notas de la Especial aprobada | n | ✘ 403 | Ninguna pantalla del Solicitante edita la asignación |
| COL-COB-1 | Localizador (User5) | Cobrador cambia el orden de su Especial | s | ✔ 200 | Cartera: reordenar |
| COL-COB-2 | Localizador (User5) | Cobrador cierra su Especial | n | ✘ 400 | S-11: el cobrador solo ordena |
| COL-COB-3 | Localizador (User5) | Cobrador cambia el retiro | n | ✘ 400 | S-11 |
| COL-COB-4 | Localizador (User5) | Cobrador se pone como "Solicitado por" | n | ✘ 400 | S-7 / S-11 |
| COL-COB-5 | Localizador (User5) | Cobrador se cambia el agente | n | ✘ 400 | S-6 / S-11 |
| COL-ADM-1 | Admin (User4) | Admin cambia el retiro de una Especial aprobada | d | ✔ 200 | Ninguna pantalla del Admin cambia montos |
| COL-SUP-5 | Supervisor (User3) | Supervisor reasigna el agente (como Reasignar) | s | ✔ 200 | Reasignar (DAT-29) |
| COL-SUP-6 | Supervisor (User3) | Supervisor rechaza y cierra | s | ✔ 200 | Aprobaciones: rechazar cierra (DAT-14) |
| COL-SOL-7 | Solicitante (User2) | Solicitante crea una Diaria activa con un agente (sin aprobación) | n | ✘ 400 | S-6: el Solicitante pide Especiales; las Diarias las trae la carga |
| COL-ADM-D | Admin (User4) | Admin crea una Diaria activa con un agente | n | ✘ 400 | H2: las Diarias solo llegan por la carga (dueño, 2026-10-19) |
| COL-SOL-8 | Solicitante (User2) | Solicitante crea una Especial a nombre de otro | n | ✘ 400 | S-7: "Solicitado por" solo a sí mismo |
| COL-BO-1 | BackOffice (User1) | BackOffice graba una interacción en el CORE | s | ✔ 200 | Transcripción |
| COL-BO-2 | BackOffice (User1) | BackOffice vuelve a grabar con otro código | n | ✘ 400 | DAT-11: una grabada no se regraba |
| COL-SUP-I | Supervisor (User3) | Supervisor graba una interacción | n | ✘ 403 | Solo el BackOffice graba |
| COL-COB-I | Localizador (User5) | Cobrador marca como grabada su interacción | n | ✘ 403 | S-9 / DAT-11 |
| COL-COB-6 | Localizador (User5) | Cobrador registra una visita en una cuenta que NO es de su cartera | n | ✘ 400 | S-1 / SEC-08: solo su cartera |
| COL-JOB-ADM | Admin (User4) | Admin (User4) crea un job de importación | s | ✔ 201 | Cargar archivo / Alta de cuenta |
| COL-JOB-BO | BackOffice (User1) | BackOffice (User1) crea un job de importación | n | ✘ 403 | Solo el Admin carga |
| COL-JOB-SOL | Solicitante (User2) | Solicitante (User2) crea un job de importación | n | ✘ 403 | Solo el Admin carga |
| COL-JOB-SUP | Supervisor (User3) | Supervisor (User3) crea un job de importación | n | ✘ 403 | Solo el Admin carga |
| COL-JOB-COB | Localizador (User5) | Localizador (User5) crea un job de importación | n | ✘ 403 | Solo el Admin carga |
