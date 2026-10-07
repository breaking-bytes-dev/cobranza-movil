# Guía 17: prueba integral de 0 a 100 (todos los roles)

Es un recorrido completo, de punta a punta, que ejecutan **personas**:

1. el CSV del banco entra por Smelter;
2. se reparte a los cobradores;
3. el Solicitante y el Supervisor gestionan Especiales;
4. el cobrador cobra en la calle, con y sin señal;
5. el BackOffice transcribe al core;
6. el Admin cierra.

Cada paso dice **quién** lo hace, **qué** hace y **qué tiene que ver**.

- **Versión automática:** `tests/integral/` corre lo mismo en el navegador. Lo que exige un **teléfono real** (sin señal,
  GPS, "Ver número" en el dispositivo) está **solo** en esta guía y va marcado con 📱.
- **Guías relacionadas:**
  - [Guía 16](16-guia-por-rol.md): qué puede hacer cada rol.
  - [Ciclo de vida de los datos](../auditoria/ciclo-de-vida-de-los-datos.md): qué le pasa a cada tabla en cada paso.

---

## 0. Preparación (una vez por corrida)

| Qué | Detalle |
|---|---|
| Entorno | DEV (`org36e60d9d`). Nunca en PROD |
| Personas y usuarios | Admin y carga: **User4** (rol Admin + "Smelter Operador"). Solicitante: **User2**. Supervisor: **User3**. BackOffice: **User1**. Cobrador: **User5** en un **teléfono real** con Power Apps Mobile |
| Agente del CSV | "QA-E2E Localizador User5" (ya vinculado a User5). **No** crear ni tocar agentes |
| Prefijo | Todo lo de la prueba lleva **QA-E2E-INT** (cliente, CIF, notas, recibos, códigos) |
| Duración | 60 a 90 minutos con todos los roles |

### 0.1 El CSV de la prueba

- Una fila por cuenta, 33 columnas, separadas por coma, UTF-8, fechas `yyyy-MM-dd`.
- El encabezado tiene que ser **exacto** (el de `test-data/cobranza-mock-1000.csv`).
- La forma más segura de armarlo es dejar que lo genere la suite: `node run.js --hasta F0` (ver §10).
- Si se arma a mano, se necesitan **3 filas** del mismo cliente QA:

| Fila | Cuenta | TipoAsignacion | Notas |
|---|---|---|---|
| A | número QA 1 | `Diaria` | La del cobro del día |
| B | número QA 2 | `Incobrable` | Para cerrarla al final |
| C | número QA 3 | `Especial` | Con `RetiroNIO` > 0, `TipoEspecial` y `Nota`. Nace **Pendiente** de aprobación |

- `Agente` = `QA-E2E Localizador User5`; `FechaCarga` = **hoy**.
- **Números de cuenta:** solo números de prueba con prefijo **9999**, 16 dígitos, con dígito verificador Luhn válido.
  **Nunca** un número real.
- El archivo tiene números completos: no se manda por correo ni chat. Se borra al terminar.

> Efecto conocido: al cargar un CSV con fecha de hoy, las ~800 Diarias viejas de DEV (carga mock de junio) aparecen como
> "desaparecidas" en Reconciliación. La app **bloquea** el cierre en lote (más del 30%). **No pulsarlo.** Todo vuelve a la
> normalidad al borrar la carga QA en la limpieza.

---

## 1. Carga del día: Admin y carga (User4) en la app de Smelter

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 1.1 Abrir "MDA - SMLT - Import Configurationd and Monitoring" (así se llama, con la errata) → **Import Jobs** → Nuevo | Formulario del trabajo de importación |
| ☐ | 1.2 Template = **Cobranza movil**; subir el CSV en **Source File**; marcar **Is Dry Run**; Guardar → ▶ **Ejecutar** | Termina sin filas fallidas. No escribe nada en las tablas de CM (es la prueba en seco) |
| ☐ | 1.3 Nuevo job igual, **sin** Dry Run → ▶ Ejecutar | Estado **Completed** y **Failed = 0** |
| ☐ | 1.4 Revisar **Import Job Logs** del job | Sin errores. Ningún mensaje muestra un número completo |

## 2. Reparto automático (lo hace el flow, en segundos)

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 2.1 📱 Cobrador (User5): abrir la app → Sincronización → "Sincronización completa" | La píldora vuelve a "Sincronizado" |
| ☐ | 2.2 📱 Cartera | Aparece el cliente **QA-E2E-INT** con las cuentas **A (Diaria)** y **B (Incobrable)**. **No** aparece la C (Especial Pendiente) |
| ☐ | 2.3 📱 Abrir la cuenta A → **Ver número** | Muestra el número completo unos segundos y vuelve a `•••• NNNN` |
| ☐ | 2.4 Admin y carga: repetir 1.3 con el **mismo CSV** (como la carga del día siguiente) | Completed, **sin registros nuevos**: la carga es idempotente |
| ☐ | 2.5 📱 Cobrador: Sincronización completa → Cliente QA-E2E-INT | Sigue viendo **solo A y B**: la C (Especial Pendiente) **no** aparece. Antes del 2026-10-06, la segunda carga le daba acceso heredado a la C, con el número completo (defecto 2) |

## 3. Admin (User4) en el backoffice

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 3.1 Inicio | "Carga de hoy" con las filas QA |
| ☐ | 3.2 Monitoreo de carga | Filas de carga de hoy = las del CSV; fecha de la última carga = hoy |
| ☐ | 3.3 Reconciliación | Card de **bloqueo** (más del 30% desaparecidas) y botón de cierre **deshabilitado**. **No pulsar nada** |
| ☐ | 3.4 Agentes y usuarios | "QA-E2E Localizador User5" vinculado a User5, con su correo en minúsculas |

## 4. Solicitante (User2) en el backoffice

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 4.1 Nueva solicitud → buscar la cuenta **A** por sus últimos 4 → tipo, retiro y motivo "QA-E2E-INT pedido" → Enviar | "Mis solicitudes": la Especial de A **Pendiente**, sin agente |
| ☐ | 4.2 Repetir 4.1 con la misma cuenta | Aviso "Ya existe una Especial ACTIVA…"; no crea otra |
| ☐ | 4.3 Nueva solicitud sobre la cuenta **B** (motivo "QA-E2E-INT rechazar") | Otra Especial Pendiente |
| ☐ | 4.4 📱 Cobrador: sincronizar → Cartera | **No** aparecen las Especiales de A ni de B: todavía no están aprobadas |

## 5. Supervisor (User3) en el backoffice

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 5.1 Inicio | "POR APROBAR" incluye las 3 Especiales QA: C (del CSV), A y B (de User2) |
| ☐ | 5.2 Aprobaciones → la Especial **C** (del CSV) → buscar "QA-E2E Localizador User5" → **Aprobar** | Sale de la cola |
| ☐ | 5.3 Igual con la Especial de **A** → Aprobar con User5 | Sale de la cola |
| ☐ | 5.4 La Especial de **B** → Rechazar **sin** motivo | Botón deshabilitado |
| ☐ | 5.5 Escribir el motivo "QA-E2E-INT no corresponde" → Rechazar | Sale de la cola como Rechazada |
| ☐ | 5.6 📱 Cobrador: sincronizar → Cartera → filtro **Especial** | Aparecen C y la Especial de A, con su retiro y su nota. **No** aparece la de B |

## 6. Cobrador (User5) en la calle 📱

> ⚠️ **Defecto conocido (F7-07, 2026-10-06).** La cuenta **A** queda con dos asignaciones activas: la Diaria y su Especial
> aprobada en el paso 5.3. En ese caso la app **no deja aplicar pago ni actualizar datos**: los dos productos quedan marcados
> y bloqueados. Hasta que se corrija, hacé el paso 6.1 en la cuenta **B**, y anotá el bloqueo en A como hallazgo.

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 6.1 Con señal: cuenta A → Registrar interacción → comentario "QA-E2E-INT visita 1" → Aplica pago: monto 25, NIO, recibo "QA-E2E-INT-R1" → Guardar | "✓ Guardado". El pago aparece en el historial de la cuenta y suma a "Cobrado hoy" |
| ☐ | 6.2 Repetir con el mismo recibo | El botón queda bloqueado: recibo repetido |
| ☐ | 6.3 Monto mayor que el saldo vencido | El botón cambia a "Confirmar pago"; el segundo toque guarda |
| ☐ | 6.4 **Modo avión**. Cuenta C → interacción "QA-E2E-INT sin señal" con pago (recibo "QA-E2E-INT-R2") y **Actualizar datos** (teléfono celular "8888-0000") → Guardar | "✓ Guardado. Se sube en la próxima sincronización". Píldora **Pendiente de subir**; "Capturas de hoy" la muestra "pendiente de subir" |
| ☐ | 6.5 📱 Con GPS activado, en otra interacción | Tarjeta "Ubicación capturada" con coordenadas de 5 decimales |
| ☐ | 6.6 Sin GPS | Tarjeta ámbar; "Guardar sin ubicación" pide un segundo toque |
| ☐ | 6.7 Quitar el modo avión | La píldora pasa a "Sincronizado"; "Capturas de hoy" marca "subida" |
| ☐ | 6.8 Cartera → reordenar la ruta con las flechas → salir y volver | Se mantiene el orden |

## 7. BackOffice (User1) en el backoffice

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 7.1 Inicio | "EN COLA" y "CON PAGO" incluyen las interacciones QA |
| ☐ | 7.2 Por transcribir → la interacción "QA-E2E-INT visita 1" | Cliente QA, cuenta con el **número completo** (solo este rol lo ve), agente "QA-E2E Localizador User5", pago C$ 25 recibo R1 |
| ☐ | 7.3 La interacción "QA-E2E-INT sin señal" | Su pago (R2) y la **actualización de datos** con el antes y el después del teléfono celular |
| ☐ | 7.4 Escribir el código "QAINT1" + **Enter** | "✓ … grabada — siguiente". Sale de la cola |
| ☐ | 7.5 Grabar la otra con "QAINT2" | Sale de la cola |
| ☐ | 7.6 Transcritas hoy | Las dos, con sus pagos; el total coincide |

## 8. Admin (User4): cierre

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 8.1 Cierres → pestaña Incobrables → la cuenta **B** → motivo "Recuperada" → Cerrar | Sale de la lista |
| ☐ | 8.2 Pestaña Especiales → la **C** → "Gestión completada", detalle "QA-E2E-INT" → Cerrar | Sale de la lista |
| ☐ | 8.3 📱 Cobrador: Sincronización completa → Cartera | Ya **no** aparecen B ni la Especial C. Siguen A (Diaria) y la Especial de A |

## 9. Solicitante (User2): seguimiento

| ☐ | Paso | Qué tiene que ver |
|---|---|---|
| ☐ | 9.1 Mis solicitudes | Especial de A **Aprobada**, con el agente; Especial de B **Rechazada**, con el motivo |
| ☐ | 9.2 Inicio | APROBADAS y RECHAZADAS cuentan las QA |

## 10. Limpieza (obligatoria)

- La suite lo hace sola: `cd tests/integral && NODE_PATH=../backoffice-canvas/node_modules node run.js --solo-limpieza`.
  Borra todo lo que tenga el prefijo **QA-E2E-INT**: interacciones (con sus pagos y actualizaciones), asignaciones,
  carga diaria, bóveda, cuentas, direcciones, teléfonos, el cliente y los Import Jobs de la corrida.
- Borrar el CSV de la máquina donde se armó.
- 📱 En el teléfono: "Sincronización completa", para que desaparezca todo lo QA.
- **Nunca** borrar los datos fijos de las suites (QA-R1, AS-2077, AS-2134) ni el agente QA.

---

## Qué cubre cada paso

| Paso | Rol | Funcionalidad | Regla del servidor que se ejercita |
|---|---|---|---|
| 1 | Admin y carga | Carga diaria por Smelter, prueba en seco, idempotencia | PanGuard, clave HMAC, bóveda, `ClaveDesdeCuenta` |
| 2 | (flow) | Reparto al cobrador; perfil offline | Flow de owner y compartidos, cascada a la bóveda |
| 3 | Admin | Monitoreo, freno de Reconciliación, Agentes | — |
| 4 | Solicitante | Nueva solicitud, anti-duplicado | `AprobacionEspecial` (nace sin agente), clave cuenta + tipo |
| 5 | Supervisor | Aprobar con localizador, rechazar con motivo | `AprobacionEspecial` (solo Supervisor), compuerta S-6 del flow |
| 6 | Cobrador | Cobro, validaciones, sin señal, GPS, actualización de datos, ruta | `CuentaDesdeInteraccion`, `ActualizacionConInteraccion`, clave alterna del recibo |
| 7 | BackOffice | Transcripción con el número completo, arqueo | Lectura de la bóveda solo para BackOffice |
| 8 | Admin | Cierre de Incobrable y Especial | Flow: revoca el compartido |
| 9 | Solicitante | Seguimiento | Compartido al solicitante |
