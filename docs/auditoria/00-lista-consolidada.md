# Auditoría Cobranza Móvil: lista consolidada de bugs y riesgos

> Fecha: 2026-10-03 · Entorno: DEV · Alcance: app del cobrador ("App - Cobranza Movil", versión **publicada**), backoffice canvas, Dataverse (roles, perfiles offline, metadata), flows.
> Método: pruebas en la **app nativa de Power Apps** (Android sobre Redroid, User5) y en un teléfono físico (User6, del usuario), más tres revisiones de solo lectura: seguridad, código y contrato de datos.
> Detalle y evidencia completa en:
> - [localizador-seguridad.md](localizador-seguridad.md) (SEC-01…22)
> - [localizador-bugs-codigo.md](localizador-bugs-codigo.md) (APP-01…33)
> - [contrato-de-datos.md](contrato-de-datos.md) (DAT-01…32)
>
> Los 87 hallazgos se agrupan en **32 ítems** sin duplicados. Confianza: **V** = verificado (dispositivo, datos o metadata), **C** = verificado en código, **P** = probable, **H** = hipótesis.

## Verificaciones independientes del consolidador

Revisé por mi cuenta, fuera de los agentes, las afirmaciones más graves:

| Afirmación | Resultado |
|---|---|
| El teléfono del cobrador guarda el padrón completo | Base SQLite de User5: cuentas 1004/1004, clientes 959/959, direcciones 2697/2697, teléfonos 1059/1059 (pantalla: 2 cuentas) |
| Corte silencioso en 500 filas | User1 (lectura Global): **500 de 1006** asignaciones activas, observado en el teléfono del usuario. Data row limit = 500 en el `.msapp` **publicado** |
| Pedido Especial lee la cuenta | Teléfono del usuario (User6, CTA-0004): Retiro C$ 0.00, nota vacía; la asignación tiene C$ 750 y nota |
| Pagos sin "Fecha del pago" | 3 de 3 pagos en DEV |
| Sin seguridad de columna | 0 columnas `IsSecured` en las 8 tablas CM |
| Auditoría | Encendida en cuenta/cliente/teléfono/dirección; **apagada en asignación, interacción, pago y agente** |
| Flow de owner | Dueño = cuenta personal `a***@` (System Administrator); conexión personal, no referencia de la solución |

---

## P0: bloqueantes de seguridad (cerrar ANTES de cargar cualquier dato real)

| # | Sev. | Ítem | Hallazgos | Conf. |
|---|---|---|---|---|
| S-1 | 🔴 Crítica | **Cada teléfono descarga el padrón completo del banco** (todas las filas y columnas de cuentas, clientes, direcciones, teléfonos y fiadores). La app usa el perfil offline **autogenerado** ("All records") y no el diseñado | SEC-01, DAT-06, APP-01 | V |
| S-2 | 🔴 Crítica | **El rol Localizador tiene lectura Global del padrón.** Aunque se corrija el perfil, el cobrador puede extraer todo por la Web API. Solicitante y Supervisor también leen de más | SEC-02, SEC-12 | V |
| S-3 | 🔴 Crítica | **Sin control del dispositivo.** Los datos offline quedan en el teléfono hasta desinstalar, sin borrado remoto, Intune/MAM ni Conditional Access demostrables; los timeouts de sesión no aplican a canvas | SEC-03 | H (falta acceso a Intune/Entra) |
| S-4 | 🟠 Alta | **Canales de exfiltración abiertos**: todos los roles CM pueden crear flows; exportar a Excel (hasta 100k filas); endpoint TDS; **0 políticas DLP** | SEC-04, SEC-22 | V / P (DLP) |
| S-5 | 🟠 Alta | **Datos de tarjeta en claro y sin seguridad de columna.** Número de cuenta completo de 13 dígitos (443 tarjetas); CIF con formato de cédula. Si en PROD es el PAN real, aplica **PCI DSS** | SEC-05, SEC-15, DAT-11 | V (formato) / P (PCI) |
| S-6 | 🟠 Alta | **Se puede saltar la aprobación de una Especial.** El Solicitante puede crear la asignación con el agente cargado, el flow mueve el owner sin mirar el estado de aprobación y el teléfono muestra Especiales en Borrador o Pendiente | SEC-06, APP-08, DAT-09 | P / V (teléfono) |
| S-7 | 🟠 Alta | **El flow de owner corre como System Administrator con conexión personal** y da Read/Write/Append a quien figure en "Solicitado por", un campo editable por Solicitante, Supervisor y Localizador | SEC-07, SEC-21, DAT-25 | V (config) / P (explotación) |
| S-8 | 🟠 Alta | **Auditoría apagada justo en las tablas transaccionales** (asignación, interacción, pago, agente): no hay rastro de quién cambió un pago | SEC-09, DAT-12 | V |
| S-9 | 🟠 Alta | **El cobrador puede alterar lo ya registrado**: Write sobre interacciones y pagos propios, Assign de pagos; monto, recibo, GPS, fecha y agente los pone el cliente; los "bloqueos" son reglas de formulario que no corren en servidor | SEC-08, SEC-10, SEC-16 | V (privilegios) / P (explotación) |
| S-10 | 🟡 Media | **Segregación de funciones rota**: la app del cobrador está compartida con User1 (BackOffice, que además es agente de 66 asignaciones) y User9 (sin rol CM) | SEC-11 | V |
| S-11 | 🟡 Media | El Supervisor puede escribir todas las columnas de cualquier asignación | SEC-13 | V |
| S-12 | 🟡 Media | **El entorno no tiene controles de plataforma**: no es Managed, no tiene grupo de seguridad, firewall por IP ni cookie binding, y hay 15 System Administrators | SEC-14 | V |
| S-13 | 🟢 Baja | Usuarios de prueba con roles de otras soluciones; dirección del cliente enviada a Google Maps; trace log de plugins en "All" legible por todos; todos los roles listan usuarios | SEC-17…20 | V |

## P1: integridad de datos y operación (cerrar antes de PRD)

| # | Sev. | Ítem | Hallazgos | Conf. |
|---|---|---|---|---|
| F-1 | 🔴 Crítica | **Colecciones truncadas a 500 filas sin aviso**: tarjetas vacías (nombre, saldo, mora, dirección), agrupación errónea, direcciones faltantes. Las filas que llegan por delta sync quedan fuera del corte | APP-01, APP-12, APP-15, DAT-23 | V |
| F-2 | 🔴 Crítica | **Interacciones del teléfono sin lookup de Agente ni de Cuenta** ("Sin nombre"): el backoffice no puede atribuirlas ni transcribirlas bien | APP-03, DAT-04 | V (síntoma) / H (causa del agente) |
| F-3 | 🔴 Crítica | **"✓ Guardado" falso**: "Guardar en 0 productos" no guarda nada; el guardado no maneja errores; doble tap duplica; quedan pagos huérfanos | APP-02, APP-04, APP-09 | C |
| F-4 | 🔴 Crítica | **Pagos offline invisibles al transcribir**: se vinculan por un autonumérico que offline está vacío, y el backoffice une por ese texto | APP-05, DAT-03 | C / P |
| F-5 | 🔴 Crítica | **"Fecha del pago" nunca se escribe**: "Cobrado hoy" y el arqueo del backoffice dan 0; "hoy" del teléfono usa la fecha de creación del servidor | DAT-02, APP-20, APP-21, DAT-27 | V |
| F-6 | 🟠 Alta | **El Pedido Especial muestra datos de la cuenta y no de la solicitud**: C$ 0.00, o incluso un monto ajeno (AS-2001: C$ 46.048,91 en lugar de C$ 1.000) | APP-06, DAT-01 | V |
| F-7 | 🟠 Alta | **Huecos del flow de owner**: con agente sin usuario no hace nada (871 de 1008 asignaciones); vincular el usuario después no propaga el owner; las asignaciones cerradas o reasignadas no salen del teléfono | DAT-07, DAT-08, DAT-24 | V / P |
| F-8 | 🟠 Alta | **Sin validaciones de entrada**: pagos sin recibo, sin tope de monto, recibos duplicados; actualización de datos sin largo ni formato (el rechazo llega diferido en el sync) | APP-10, APP-24, APP-32, DAT-21, DAT-22 | C |
| F-9 | 🟠 Alta | **Estado global sucio**: `varAsig` arrastra el tipo y la Especial de otra cuenta; las pestañas comparten variable; `colClientes` tiene dos esquemas | APP-07, APP-17, APP-19 | C |
| F-10 | 🟠 Alta | **Escrituras masivas de `Orden`** sobre todas las asignaciones (incluso ajenas o inactivas), que marcan "limpio" aunque fallen; `Orden` sin inicializar (la regla de negocio quedó en borrador) | APP-13, APP-23, DAT-16 | V |
| F-11 | 🟠 Alta | **Sincronización opaca**: "Sincronizado" con solo tener red; última sincronización fija; el botón no trae cartera; no hay `ShowHostInfo`/`Connection.Sync` | APP-14, L-7 | V |
| F-12 | 🟡 Media | **GPS con 2 decimales (~1 km)**, null sin aviso, "capturada" siempre fijo | APP-11, DAT-05 | V |
| F-13 | 🟡 Media | **Ciclo de vida de datos incompleto**: una Diaria reconciliada que reaparece no se reactiva; la actualización de datos no tiene cierre; el backoffice no puede crear cuentas; nombre y CIF duplicados en cuenta y cliente; email en texto contra lookup; `ApplicationRequired` no se exige en servidor; el anti-duplicado del Solicitante no ve todo; grano asignación contra cuenta; cascadas | DAT-10, 13…20, 26 | V / P |
| F-14 | 🟡 Media | **Las respuestas rápidas pisan lo tipeado**; el comentario pasa al cliente siguiente | APP-18 | V |

## P2: calidad, accesibilidad y proceso

| # | Sev. | Ítem | Hallazgos | Conf. |
|---|---|---|---|---|
| Q-1 | 🟠 Alta | **El repo no refleja lo publicado** (5 de 7 pantallas difieren; se perdería el multi-producto) y la documentación está desfasada. Hay que fijar lo publicado como línea base **antes** de corregir | APP-33, DAT-32 | V |
| Q-2 | 🟡 Media | **Accesibilidad**: 0 etiquetas accesibles, contraste 2,58:1 en 41 textos, toques de ~19 dp; textos recortados al agrandar la letra | APP-25, APP-31 | C / V |
| Q-3 | 🟡 Media | **Formatos**: "U$" sin formato, fechas según el idioma del dispositivo, rótulos engañosos, buscador que arranca con el placeholder como texto, conteo de interacciones por CIF | APP-16, APP-22, APP-26 | V / C |
| Q-4 | 🟢 Baja | Nota de desarrollador visible, mensajes técnicos, navegación que apila pantallas, la app publicada tiene 2 errores de análisis | APP-27, APP-29, APP-30 | V / P |
| Q-5 | 🟢 Baja | Tablas y columnas huérfanas, autonumérico como clave de negocio, default de `estadoaprobacion` | DAT-28…31 | V |

---

## Avance

| Fase | Estado | Evidencia |
|---|---|---|
| 0 · Línea base | ✅ | Commits `49275f0`, `0f4d491` |
| 1 · Exposición (S-1, S-2, S-8) | ✅ en DEV | [scripts/seguridad](../../scripts/seguridad/README.md): cascadas cortadas, auditoría transaccional, el flow comparte y revoca, backfill de 282 compartidos, Localizador en lectura Basic. Teléfono de User5 con primera sincronización limpia: 2 cuentas en vez de 1.004, y tarjetas completas (también resuelve F-1 para cobradores). Pendiente: los teléfonos que ya tenían el padrón lo conservan hasta reiniciarlos (ver README). 2026-10-05: perfil offline propio con asignaciones activas e historial de 90 días |
| 4 · Integridad: S-6 | ✅ en DEV | Plugin `AprobacionEspecial` (solo Supervisor/Admin aprueban, rechazan o asignan agente a una Especial; la carga de Smelter, sin roles de CM, no se frena), flow que no comparte una Especial sin aprobar y app que no la muestra. Ver [localizador-bugs-codigo.md](localizador-bugs-codigo.md#cierre-app-25-24-16-08s-6-y-s-1-2026-10-05) |

## Estado verificado y recomendaciones de seguridad (2026-10-06)

Revisado contra DEV el 2026-10-06 con consultas a la API: privilegios de los roles, auditoría por tabla, configuración de
trazas y definición del flow.

| # | Estado en DEV | Qué queda y por qué importa | Recomendación |
|---|---|---|---|
| S-1 | ✅ Cerrado | — | Perfil offline propio: solo lo legible por el cobrador, asignaciones activas y 90 días de historial |
| S-2 | ✅ Cerrado (Localizador) | Solicitante y Supervisor leen todas las cuentas y clientes (Global): lo necesitan para buscar cualquier cuenta al crear y aprobar Especiales. **Decisión de diseño**, no hueco | Mantener; revisarlo si cambia el proceso |
| S-3 | ❌ Abierto (tenant) | Los datos offline quedan en el teléfono hasta desinstalar: sin borrado remoto, PIN ni cifrado exigido | Intune/MAM + Conditional Access en Entra antes de PROD. Lo hace un administrador del tenant |
| S-4 | ❌ Abierto | **Todos los roles CM pueden crear flows** (verificado): un flow puede copiar lo que el usuario lee a su OneDrive o correo sin que se note. Los roles CM no dan "Exportar a Excel", pero los usuarios tienen otros roles ("Basic User") que pueden darlo. DLP no verificable con la cuenta de servicio | Quitar `prvCreateWorkflow` a los roles CM operativos; revisar la exportación en **todos** los roles de cada usuario; política DLP del entorno |
| S-5 | ✅ Cerrado | — | Fase 2 de PAN: el número completo solo vive en la bóveda |
| S-6 | ✅ Cerrado | — | Plugin `AprobacionEspecial` + compuerta del flow + filtro de la app |
| S-7 | ❌ Abierto | El flow corre con la **cuenta personal** de su dueño (con permisos de administrador): si esa cuenta cambia, se cae la asignación de Especiales. Además da **lectura, escritura y vínculo** a quien figure en "Solicitado por", un campo que editan Solicitante, Supervisor y Localizador: se puede regalar acceso a un tercero | **Prioridad 1.** Identidad de servicio (SP con rol "Servicio - Cobranza") y compartir con `createdby` (no editable) en lugar de "Solicitado por" |
| S-8 | ✅ Cerrado | — | Auditoría activa en asignación, interacción, pago y agente |
| S-9 | ❌ Abierto | El cobrador tiene **escritura sobre sus interacciones y pagos** y puede **reasignar pagos** (verificado): puede cambiar un monto, la fecha o el GPS después de grabar | **Prioridad 2.** Que el rol cree pero no edite, o un plugin que bloquee los campos clave después de grabar. Diseñarlo con la sincronización offline en mente (también actualiza registros) |
| S-10 | ⚠️ Sin re-verificar | En la auditoría, la app del cobrador estaba compartida con User1 (BackOffice y agente de 66 asignaciones) y User9 (sin rol CM). Ver los compartidos requiere la API de administración de Power Apps | Compartir la app solo con el equipo "CM · Localizadores" |
| S-11 | ❌ Abierto (mitigado) | El Supervisor escribe **cualquier columna de cualquier asignación** (Global). Desde S-6 lo que aprueba queda controlado, pero puede tocar cuenta, saldo, orden y tipo | Seguridad de columna sobre lo que no le corresponde |
| S-12 | ❌ Abierto (tenant) | El entorno no es Managed, no tiene grupo de seguridad ni restricción por IP, y tiene 15 System Administrators | Centro de administración de Power Platform antes de PROD |
| S-13 | ❌ Abierto | Trazas de plugins en "All" (`plugintracelogsetting = 2`) y **todos los roles CM las pueden leer**. Nuestros plugins nunca trazan valores; los de otras soluciones del entorno, no se sabe | Trazas en "Exception" en PROD y quitar `prvReadPluginTraceLog` a los roles operativos |

**Orden recomendado:** S-7 (además de riesgo, es un punto de falla operativo), S-9 (toca la plata), S-4. S-3, S-10 y S-12
hay que pedírselos al administrador del tenant antes de PROD.

## Orden de corrección propuesto

1. **Línea base (Q-1):** desempaquetar la versión publicada al repo y commitearla. Sin esto cualquier corrección pisa el multi-producto.
2. **Cerrar la exposición (S-1, S-2, S-10):**
   - asociar el perfil offline diseñado (solo lo relacionado con las asignaciones del cobrador);
   - bajar la lectura del Localizador a lo que le corresponde (ver alternativas en [localizador-seguridad.md](localizador-seguridad.md));
   - limpiar con quién está compartida la app.
3. **Plataforma (S-3, S-4, S-12):** Intune/MAM y Conditional Access para la app de Power Apps; políticas DLP; grupo de seguridad del entorno; quitar la creación de flows y la exportación a Excel a los roles CM.
4. **Integridad en servidor (S-6, S-7, S-8, S-9, F-7):**
   - flow con identidad de servicio;
   - validar la aprobación antes de mover el owner;
   - auditoría en las tablas transaccionales;
   - bloquear los cambios posteriores a la grabación con lógica de servidor, no con reglas de formulario.
5. **App del cobrador (F-1…F-6, F-8…F-12, F-14):** leer por las relaciones de la asignación en lugar de colecciones de tablas enteras; guardado transaccional con manejo de errores; vínculo pago↔interacción por lookup; escribir "Fecha del pago"; Especial desde la asignación.
6. **Calidad (Q-2…Q-5).**

## Decisiones del dueño del producto (2026-10-03)

| Tema | Decisión | Consecuencia |
|---|---|---|
| ¿El número de cuenta es el PAN? | **Sí, es el PAN real** | Alcance **PCI DSS** |
| ¿Qué usa el backoffice para grabar en el core? | **El PAN completo** | El PAN tiene que existir en Dataverse |
| ¿Qué trae la carga diaria? | **Solo el PAN** | No hay otra clave natural; la plantilla de Cobranza Móvil lo recibe |
| ¿El cobrador necesita el PAN? | **Sí, lo escribe a mano en la factura de papel**, también **sin señal** | Visible completo para el cobrador, offline, solo de su cartera |
| Modelo "solo su cartera" | **A: compartir desde el flow** (GrantAccess y RevokeAccess) + lectura Basic en el rol Localizador | Hay que mantener el revoke al reasignar o cerrar; crece la tabla POA |
| Dispositivos | **Corporativos con Intune** | MDM + MAM: Power Apps (`com.microsoft.msapps`) está en la lista oficial de [apps protegidas por Intune](https://learn.microsoft.com/intune/app-management/ref-protected-apps) |
| Identidad del flow | **Se mantiene la cuenta actual en DEV** | Pendiente obligatorio antes de PRD (S-7) |

### Diseño PCI acordado (Fase 2)

PCI DSS distingue **mostrar** el PAN (req. 3.4.1: enmascarado salvo para quien tiene necesidad legítima) de **guardarlo** (req. 3.5.1: ilegible donde se almacene). El cobrador tiene necesidad legítima de verlo; el control está en cuántos PAN hay en el teléfono y cómo se protegen:

1. **Fila:** el cobrador lee solo su cartera (opción A). En el teléfono quedan pocas tarjetas, no el padrón.
2. **Columna:** el PAN pasa a una columna con **seguridad de columna**, legible por Localizador, BackOffice y Admin. Solicitante, Supervisor y las exportaciones no la ven. El número visible por defecto es **enmascarado**.
3. **Pantalla:** enmascarado por defecto, con un botón "Ver número" para escribir la factura.
4. **Dispositivo (Intune, lo configura TI del banco):**
   - MDM con cifrado del dispositivo obligatorio por cumplimiento;
   - MAM sobre Power Apps: cifrado de datos de la app, PIN o biometría, bloqueo de capturas y de copiar/pegar hacia apps no administradas, borrado selectivo y borrado tras N días sin conexión;
   - Conditional Access que exija app protegida.
5. **Servidor:** auditoría de lecturas y cambios en las tablas sensibles, DLP y sin exportación masiva. El acceso se revoca al cerrar o reasignar, y la sincronización saca el registro del teléfono.
6. **Claves de unión:** asignación, interacción, pago y carga dejan de usar el PAN como texto de unión. Pasan a usar el lookup de cuenta y una clave sustituta calculada en servidor.

**A verificar antes de implementar** (restricciones de plataforma, no asumidas):
- La columna de nombre principal no admite seguridad de columna.
- Restricciones de las claves alternativas sobre columnas protegidas, en relación con el upsert de la carga.
- Que el cifrado MAM cubra la base offline de Power Apps (se pide confirmación a Microsoft o a TI).

**Riesgo a elevar a cumplimiento del banco:** la **factura de papel con el PAN completo** también es almacenamiento de datos de tarjeta en medio físico (PCI req. 9: custodia y destrucción). Conviene evaluar si la factura puede llevar solo los últimos 4 dígitos.
