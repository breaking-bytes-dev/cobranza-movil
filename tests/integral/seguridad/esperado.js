// Matriz ESPERADA de "rol por pantalla": qué debería poder hacer cada rol de CM contra el servidor, por tabla y operación.
// Se deriva de lo que hacen sus pantallas (apps/backoffice-canvas/Src, apps/localizador-canvas/Src), no de los privilegios
// que hoy tiene el rol: la comparación con lo que responde el servidor es justamente la prueba.
//
// Valores:  's' = la operación tiene que funcionar · 'n' = el servidor la tiene que rechazar ·
//           'd' = dudosa (ninguna pantalla la usa pero el rol podría necesitarla: decide el dueño) · '-' = no se prueba.
// Operaciones:
//   C  crear una fila QA                 Ra leer una fila QA ajena (dueño: el servicio, sin compartir)
//   Rc leer una fila de SU cartera (solo el cobrador: lo que el reparto le comparte)
//   Ua editar una fila ajena             Up editar la fila que creó él mismo (solo si C funcionó)
//   D  borrar una fila ajena             A  reasignarse una fila ajena (Assign a sí mismo)
//   S  compartir una fila ajena con otro usuario (GrantAccess)
// Pantallas por rol (cmpNavLateral.pa.yaml):
//   BackOffice: Inicio, Transcripción, Transcritas · Solicitante: Inicio, Nueva solicitud, Mis solicitudes ·
//   Supervisor: Inicio, Aprobaciones, Reasignar, Cartera por localizador, Historial de Especiales ·
//   Admin: Inicio, Reasignar, Monitoreo, Cargar archivo, Alta de cuenta, Reconciliación, Cierres, Agentes, Datos maestros ·
//   Localizador: la app del cobrador (cartera, detalle, interacción con pago y actualización de datos, sincronización).

const ROLES = { adm: 'Admin (User4)', bo: 'BackOffice (User1)', sol: 'Solicitante (User2)', sup: 'Supervisor (User3)', cob: 'Localizador (User5)' };
const TABLAS = ['cuenta', 'cliente', 'asignacion', 'interaccion', 'pago', 'actualizacion', 'agente', 'producto', 'fiador', 'direccion', 'telefono', 'carga', 'boveda'];
const OPS = ['C', 'Ra', 'Rc', 'Ua', 'Up', 'D', 'A', 'S'];

// Abreviatura: f('C Ra Ua D A S', 'nsnnnn', 'por qué') → { C:'n', Ra:'s', ... }
const f = (ops, vals, por, extra = {}) => {
  const o = { por, ...extra };
  ops.split(' ').forEach((op, i) => { o[op] = vals[i]; });
  return o;
};
const SOLO_LEE = (por) => f('C Ra Ua D A S', 'nsnnnn', por);
const NADA = (por) => f('C Ra Ua D A S', 'nnnnnn', por);

// Admin: ninguna pantalla borra, reasigna por Assign (Reasignar cambia el agente y el plugin mueve el dueño) ni comparte.
// Es el administrador funcional: si eso le corresponde o no lo decide el dueño (por eso 'd').
const ADM_DUDA = 'Ninguna pantalla del Admin borra, reasigna por Assign ni comparte; ¿el Admin funcional debe poder hacerlo por fuera de la app?';

const ESPERADO = {
  adm: {
    cuenta: f('C Ra Ua D A S', 'dsdddd', `Monitoreo, Cierres y Reconciliación la leen. Las cuentas solo las crea la carga (Alta de cuenta va por Smelter) y Datos maestros no la edita. ${ADM_DUDA}`),
    cliente: f('C Ra Ua D A S', 'sssddd', `Datos maestros crea clientes y edita departamento y dirección. ${ADM_DUDA}`),
    asignacion: f('C Ra Ua D A S', 'dssddd', `Cierres, Reconciliación y Reasignar la editan (estado, motivo, agente). La crea la carga o SolicitarEspecial. ${ADM_DUDA}`),
    interaccion: f('C Ra Ua D A S', 'dsdddd', `Inicio cuenta interacciones. Ninguna pantalla del Admin las crea ni edita (crear una sería inventar una visita). ${ADM_DUDA}`),
    pago: f('C Ra Ua D A S', 'dsdddd', `Inicio cuenta pagos. Ninguna pantalla del Admin crea ni edita pagos (sería inventar un cobro). ${ADM_DUDA}`),
    actualizacion: f('C Ra Ua D A S', 'dsdddd', `Ninguna pantalla del Admin la usa; es lo que captura el cobrador. ${ADM_DUDA}`),
    agente: f('C Ra Ua D A S', 'sssddd', `Agentes: "＋ Nuevo agente", activar y vincular el usuario. ${ADM_DUDA}`),
    producto: f('C Ra Ua D A S', 'dsdddd', `Alta de cuenta elige un producto existente; los productos los crea la carga. ${ADM_DUDA}`),
    fiador: f('C Ra Ua D A S', 'dddddd', `Ninguna pantalla del Admin muestra fiadores (dato personal: nombre y dirección). ${ADM_DUDA}`),
    direccion: f('C Ra Ua D A S', 'sssddd', `Datos maestros agrega y edita direcciones. ${ADM_DUDA}`),
    telefono: f('C Ra Ua D A S', 'sssddd', `Datos maestros agrega y edita teléfonos. ${ADM_DUDA}`),
    carga: f('C Ra Ua D A S', 'dsdddd', `Monitoreo y la "última carga" de App.Formulas la leen; la escribe solo Smelter. ${ADM_DUDA}`),
    boveda: NADA('Fase 2 D3: el Admin ve el número de tarjeta solo enmascarado; nunca la bóveda.'),
  },
  bo: {
    cuenta: SOLO_LEE('Transcripción y Transcritas muestran la cuenta de la interacción.'),
    cliente: SOLO_LEE('Transcripción arma el "antes" del cliente (direcciones y teléfonos por CIF).'),
    asignacion: SOLO_LEE('Inicio muestra contadores de asignaciones.'),
    interaccion: f('C Ra Ua D A S', 'nsnnnn', 'Transcripción la graba en el CORE: solo las columnas de grabación (caso aparte, COL-BO-*). Cualquier otra columna, no (DAT-11).'),
    pago: SOLO_LEE('Transcripción y Transcritas muestran el pago de la interacción.'),
    actualizacion: SOLO_LEE('Transcripción muestra la actualización de datos que capturó el cobrador.'),
    agente: SOLO_LEE('Inicio y Transcripción muestran el nombre del agente.'),
    producto: SOLO_LEE('Dato de referencia del catálogo (no sensible).'),
    fiador: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del BackOffice muestra fiadores (dato personal).'),
    direccion: SOLO_LEE('Transcripción muestra las direcciones "antes" del cliente.'),
    telefono: SOLO_LEE('Transcripción muestra los teléfonos "antes" del cliente.'),
    carga: SOLO_LEE('La "última carga" (App.Formulas) se calcula para todos los roles.'),
    boveda: SOLO_LEE('Fase 2 D3: el BackOffice lee el número completo (lectura Global de la bóveda) para grabar en el CORE.'),
  },
  sol: {
    cuenta: SOLO_LEE('Nueva solicitud busca cualquier cuenta por sus últimos 4 (lectura Global a propósito).'),
    cliente: SOLO_LEE('Nueva solicitud muestra el cliente de la cuenta.'),
    asignacion: f('C Ra Ua Up D A S', 'snnnnnn', 'Nueva solicitud crea la Especial con los permisos del Solicitante (SolicitarEspecial). Desde DAT-14 la reactivación la hace el servicio del sistema: ninguna pantalla del Solicitante edita una asignación. Mis solicitudes lee solo las suyas.'),
    interaccion: NADA('Ninguna pantalla del Solicitante muestra interacciones.'),
    pago: NADA('Ninguna pantalla del Solicitante muestra pagos.'),
    actualizacion: NADA('Ninguna pantalla del Solicitante muestra actualizaciones de datos.'),
    agente: SOLO_LEE('Mis solicitudes muestra el agente asignado.'),
    producto: SOLO_LEE('Nueva solicitud muestra el producto de la cuenta.'),
    fiador: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del Solicitante muestra fiadores (dato personal).'),
    direccion: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del Solicitante muestra direcciones; el rol tiene lectura Global.'),
    telefono: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del Solicitante muestra teléfonos; el rol tiene lectura Global.'),
    carga: NADA('La "última carga" se queda en blanco para el Solicitante; ninguna pantalla suya la usa.'),
    boveda: NADA('Fase 2 D3: solo el BackOffice (y el cobrador, de su cartera) leen la bóveda.'),
  },
  sup: {
    cuenta: SOLO_LEE('Aprobaciones, Cartera por localizador e Historial muestran la cuenta.'),
    cliente: SOLO_LEE('Aprobaciones muestra el cliente de la cuenta.'),
    asignacion: f('C Ra Ua D A S', 'nssnnn', 'Aprobaciones, Reasignar y el rechazo editan columnas de la asignación (casos COL-SUP-*). No la crea ni la borra; Reasignar cambia el agente y el plugin mueve el dueño (sin Assign directo).'),
    interaccion: SOLO_LEE('Inicio y Cartera por localizador cuentan interacciones.'),
    pago: SOLO_LEE('Inicio cuenta pagos.'),
    actualizacion: NADA('Ninguna pantalla del Supervisor muestra actualizaciones de datos.'),
    agente: SOLO_LEE('Aprobaciones y Reasignar eligen el agente.'),
    producto: SOLO_LEE('Dato de referencia del catálogo (no sensible).'),
    fiador: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del Supervisor muestra fiadores (dato personal).'),
    direccion: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del Supervisor muestra direcciones; el rol tiene lectura Global.'),
    telefono: f('C Ra Ua D A S', 'ndnnnn', 'Ninguna pantalla del Supervisor muestra teléfonos; el rol tiene lectura Global.'),
    carga: NADA('La "última carga" se queda en blanco para el Supervisor; ninguna pantalla suya la usa.'),
    boveda: NADA('Fase 2 D3: solo el BackOffice (y el cobrador, de su cartera) leen la bóveda.'),
  },
  cob: {
    cuenta: f('C Ra Rc Ua D A S', 'nnsnnnn', 'S-1: el cobrador lee solo las cuentas de su cartera (compartidas por el reparto).'),
    cliente: f('C Ra Rc Ua D A S', 'nnsnnnn', 'S-1: solo los clientes de su cartera.'),
    asignacion: f('C Ra Rc Ua D A S', 'nnsnnnn', 'Lee sus asignaciones; solo cambia el orden de su cartera (caso COL-COB-*).'),
    interaccion: f('C Ra Ua Up D A S', 'snnnnnn', 'Captura interacciones de su cartera; S-9: no las edita ni las reasigna. Lee solo las suyas.'),
    pago: f('C Ra Ua Up D A S', 'snnnnnn', 'Captura el pago de su interacción; S-9: no lo edita. Lee solo los suyos.'),
    actualizacion: f('C Ra Ua Up D A S', 'snnnnnn', 'Captura la actualización de datos de su interacción; S-9: no la edita.'),
    agente: SOLO_LEE('La app resuelve el agente del cobrador (lectura Global del catálogo de agentes).'),
    producto: SOLO_LEE('Dato de referencia del catálogo (lectura Global).'),
    fiador: f('C Ra Rc Ua D A S', 'nnsnnnn', 'Solo el fiador de su cartera (compartido por el reparto).'),
    direccion: f('C Ra Rc Ua D A S', 'nnsnnnn', 'Solo las direcciones de los clientes de su cartera; los cambios van por actualización de datos.'),
    telefono: f('C Ra Rc Ua D A S', 'nnsnnnn', 'Solo los teléfonos de los clientes de su cartera; los cambios van por actualización de datos.'),
    carga: NADA('La app del cobrador no usa la carga diaria.'),
    boveda: f('C Ra Rc Ua D A S', 'nnsnnnn', '"Ver número" del detalle y de la interacción: el número completo solo de su cartera.'),
  },
};

// Up no declarado = igual que Ua; Rc no declarado = no se prueba.
for (const r of Object.keys(ESPERADO)) for (const t of TABLAS) {
  const e = ESPERADO[r][t];
  if (!e) throw new Error(`falta la fila ${r}/${t}`);
  if (e.Up === undefined) e.Up = e.Ua;
  if (e.Rc === undefined) e.Rc = '-';
  for (const op of OPS) if (!['s', 'n', 'd', '-'].includes(e[op])) throw new Error(`valor inválido ${r}/${t}/${op}`);
}

module.exports = { ROLES, TABLAS, OPS, ESPERADO };
