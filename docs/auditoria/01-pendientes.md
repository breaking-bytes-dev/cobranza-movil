# Pendientes de Cobranza Móvil: lista de seguimiento

> **Fuente única** de lo que falta cerrar. Reemplaza la búsqueda por los demás documentos de auditoría, que siguen siendo
> la evidencia de cada hallazgo:
> - [00-lista-consolidada.md](00-lista-consolidada.md): S, F y Q.
> - [contrato-de-datos.md](contrato-de-datos.md): DAT.
> - [localizador-bugs-codigo.md](localizador-bugs-codigo.md): APP.
> - [localizador-seguridad.md](localizador-seguridad.md): SEC.
>
> Creada el 2026-10-14 a partir de esos documentos y de consultas a DEV (solo lectura).

**Cómo se usa**
- Al cerrar un ítem, se mueve a [Cerrados](#cerrados) con fecha, commit y cómo se verificó.
- Cada fila dice **quién** lo puede resolver:
  - **CM:** nosotros, con plugins, scripts, apps o documentación.
  - **Dueño:** necesita una decisión del dueño del producto antes de construir.
  - **Smelter:** configuración de la carga (el código de Smelter no se toca sin autorización).
  - **Tenant:** administrador del tenant o del entorno.
- **Esfuerzo:** Chico (unas horas), Mediano (uno o dos días), Grande (más).

---

## 1. Seguridad

| ID | Problema | Propuesta | Quién | Esfuerzo | Estado |
|---|---|---|---|---|---|
| S-4 (resto) | `Basic User` y roles de otras soluciones dan crear flows y exportar a Excel; no hay ninguna política DLP; el TDS endpoint está encendido | Riesgo aceptado hasta tener la DLP (ver [Riesgos aceptados](#riesgos-aceptados)) | Dueño / Tenant | — | Riesgo aceptado |

## 2. Datos e integridad

| ID | Problema | Propuesta | Quién | Esfuerzo | Estado |
|---|---|---|---|---|---|
| DAT-14 (resto) | Un Solicitante no ve las Especiales que pidió otro (lectura Basic): si la cuenta ya tiene una, al pedirla recibe el error de clave duplicada. Desde 2026-10-14 el mensaje explica qué pasa y Rechazar cierra la Especial (quien la pidió puede volver a pedirla) | Custom API `SolicitarEspecial` con el servicio del sistema: crea o reactiva la Especial y, si ya existe una activa, avisa quién la pidió. Exige agregar la fuente "Environment" a la app en Studio (un paso manual tuyo). Descartada: lectura Global para el Solicitante (ampliaba privilegios) | CM, más un paso tuyo en Studio | Mediano | **Decidido (a)**, 2026-10-14. API registrada en DEV (ensamblado 1.0.8.1) y probada en vivo 8/8 (`tests/integral/apis/solicitar-especial.js`). Falta tu paso en Studio y el cambio en Nueva solicitud |
| DAT-20 | Decisión del dueño (2026-10-14): **el Admin** da de alta la cuenta, **camino A** (una importación de una fila con la plantilla de Smelter: K y bóveda siguen en un solo lugar) | Pantalla "Alta de cuenta" (solo Admin) + Custom API de CM que arma la fila del lado del servidor y crea el job de Smelter como lo hace "Cargar archivo" (prueba en seco y después real). El servidor: usa la **fecha de la última carga** (con la de hoy, la reconciliación vería ~800 Diarias fuera de cartera); exige agente y producto existentes (el upsert por nombre crearía uno nuevo con un error de tipeo); completa los datos de un cliente existente (el upsert por CIF los pisaría); rechaza si el cliente ya tiene una cuenta con esos últimos 4 (el upsert pisaría la cuenta); valida Luhn; nunca guarda el PAN en tablas de CM. Comparte con DAT-14 el paso en Studio (fuente "Environment") | CM + paso tuyo en Studio | Grande | **Cualquier tipo** (dueño, 2026-10-14; con Diaria o Incobrable el formulario avisa que la reconciliación cierra la Diaria si el banco no la trae). API `AltaCuenta` registrada en DEV (1.0.8.1) y probada en vivo 19/19 (`tests/integral/apis/alta-cuenta.js`: rechazos, prueba en seco, real, clave = HMAC, bóveda, Especial Pendiente, cliente existente sin pisar, misma cuenta rechazada, reconciliación sin cambios, trazas sin PAN). Falta tu paso en Studio y la pantalla "Alta de cuenta" |
| DAT-30 (resto) | En la cuenta, `ct_agenteid` y `ct_tipoasignacion` los llena la carga (1001 cuentas) y nadie los lee | Dejarlos y documentarlos como "solo carga" (borrarlos exige quitar sus mapeos en la plantilla de Smelter y es irreversible) | CM | Chico | Decidido (tu OK del 2026-10-15): se documentan |
| Fiadores (origen) | La carga crea un fiador vacío por cada fila del CSV con `NombreFianza` en blanco (la clave alternativa por nombre no rige con NULL) | Corrección de origen en la plantilla de Smelter: no crear el fiador si viene vacío. Mientras tanto, el trabajo diario "CM - Fiadores sin nombre" los borra | Smelter (config) | Chico | Mitigado |
| Revocación offline | El teléfono pierde una cuenta revocada recién en la próxima sincronización | Inherente a offline. Se mitiga en PROD con Intune (borrado tras N días sin conexión, S-3) | Tenant | — | Propuesta: aceptar |
| Padrón viejo | Los teléfonos que tuvieron el padrón completo (antes de S-1) lo conservan hasta reiniciar la app | Borrar los datos de la app (o reinstalarla) en esos teléfonos: en DEV es el tuyo (User6). En PROD no aplica | Dueño | Chico | Pendiente (tu teléfono) |

## 3. Apps

| ID | Problema | Propuesta | Quién | Esfuerzo | Estado |
|---|---|---|---|---|---|
| APP-11 | El GPS con 6 decimales nunca se probó con GPS real (el Android de pruebas no tiene) | Una captura desde tu teléfono con el GPS encendido; yo verifico los decimales en Dataverse | Dueño (teléfono) | Chico | Pendiente (decisión: seguir esperando) |
| APP-04 | Nunca se probó qué pasa si falla el guardado de la interacción | Provocar el fallo desde el servidor en una prueba (por ejemplo, con un usuario QA sin agente vinculado, que `InteraccionDelCobrador` rechaza) y verificar que la app avise, no diga "✓ Guardado" y no deje huérfanos | CM | Mediano | Aprobado (2026-10-15), en curso |
| APP-27 | El paquete base de las dos apps (`.msapr` del repo) dice `ParserErrorCount: 2` | **Verificado el 2026-10-15:** el backoffice guardado y publicado por vos en Studio **sigue diciendo 2**, y su App Checker no tiene ningún error de fórmula (352 avisos: accesibilidad y rendimiento). O sea, guardar en Studio no lo cambia y no es un error nuestro que Studio vea. El paquete base del backoffice ya es el de Studio (con Environment, `402a473`). La app del cobrador no se guardó en Studio (último guardado 27/07) | CM | — | Propuesta: cerrar sin más acción |
| F7 / versión servida | Las dos fallas de F7-03 mostraron la versión anterior de la app, una de ellas 1 minuto después de un despliegue correcto. Hipótesis: tras publicar, DEV sirve la versión vieja un rato | Estampar la versión (commit y hora) en las apps al desplegar, mostrarla en Sincronización y al pie del menú, y que la suite la compare antes de chequear (si no coincide, espera hasta 10 min y corta con un mensaje claro) | CM | Chico | Aprobado (2026-10-15), en curso |
| Rol por pantalla | El backoffice decide qué pantallas mostrar con `gblRol`, del lado del cliente | Matriz rol × tabla × operación contra la API (suplantando a cada usuario QA). Ya hay pruebas por API de lo más sensible (S-9, S-11, DAT-11, DAT-13) | CM | Mediano | Propuesta (pediste que lo explique mejor) |

## 4. Documentación

Sin pendientes abiertos (ver Cerrados).

## 5. Antes de PROD (entorno y tenant)

Conviene juntarlos en un **runbook de pasaje a PROD** (`docs/guias/`), en orden y con un check por paso.

| ID | Qué | Quién |
|---|---|---|
| S-3 | Intune MDM/MAM sobre Power Apps y Conditional Access que exija app protegida | Tenant |
| S-4 | Política DLP del entorno; TDS endpoint apagado o restringido; usuarios de CM con un rol base propio en lugar de `Basic User` | Tenant / Dueño |
| S-10 | Compartir la app del cobrador solo con el equipo "CM · Localizadores" (hoy no se puede verificar sin la API de administración) | Tenant |
| SEC-21 | El dueño de la app es una cuenta personal: pasarla a una cuenta de servicio | Tenant |
| S-12 | Entorno Managed, grupo de seguridad, restricción por IP y menos System Administrators (hoy son 15) | Tenant |
| S-13 | Trazas de plugins en "Exception" (en DEV los roles operativos ya no las leen) | CM |
| S-14 | Cascadas en NoCascade **antes** de la primera carga real (`paso1_cascadas_auditoria.py`) | CM |
| Fase 2 | Pasos 2 y 8: el PAN fuera de las columnas auditadas; la clave K en Key Vault con identidad administrada; borrado automático del CSV de carga | CM + Tenant |
| PCI | La rama `feat/hmac-transform` de Smelter tiene que llegar a `main`; validación de Luhn; revisión de un QSA; la factura de papel con el PAN completo, a cumplimiento del banco | Smelter / Dueño |
| Despliegue | En orden: `paso1_cascadas_auditoria` (antes de la primera carga real); `paso5_cartera_sin_cobrador --entorno PRD`; registrar los plugins 1.0.6.1 y apagar el flow de owner; `paso6`, `paso7` y `paso8`; `paso9` (no hace falta si el entorno nace limpio); `paso10` (borrado programado); `paso7_desplegar`; `paso3_reconciliar`; sincronizar `solutions/unpacked` (rol nuevo, privilegios, regla de negocio borrada, cascadas). `paso11` no hace falta en PROD | CM |
| Offline | Documentar el perfil offline y cómo forzar una sincronización completa | CM |

---

## Riesgos aceptados

| ID | Qué se acepta | Decidido | Se revisa cuando |
|---|---|---|---|
| S-4 | En DEV, `Basic User` y los roles de otras soluciones siguen dando crear flows y exportar a Excel; no hay DLP; el TDS endpoint está encendido. Los roles CM ya no crean flows (`paso7_sin_flows.py`) | Dueño, 2026-10-14 | Al definir la política DLP del entorno o antes de PROD |
| SEC-18 | La app del cobrador manda la dirección del cliente a Google Maps al abrir el mapa | Dueño, 2026-10-14 | Si cumplimiento del banco lo objeta |
| SEC-17 | En DEV los usuarios de prueba tienen roles de otras soluciones. En PROD solo tendrán roles CM | Dueño, 2026-10-14 | Al asignar roles en PROD |
| SEC-20 | Todos los roles pueden listar usuarios (`prvReadUser`): lo necesita la plataforma | Dueño, 2026-10-14 | — |

## Cerrados

Cerrados en DEV antes del 2026-10-14 (detalle en [00-lista-consolidada.md](00-lista-consolidada.md)):
- S-1, S-2 (Localizador), S-5, S-6, S-7, S-8, S-9 y S-14.
- DAT-07, DAT-08 y DAT-12.
- Los APP corregidos y verificados están en las secciones de cierre de [localizador-bugs-codigo.md](localizador-bugs-codigo.md) (2026-10-05 a 2026-10-07).

Cerrados el 2026-10-15:

| ID | Qué se hizo | Verificación |
|---|---|---|
| S-7 tras reactivar | `RepartoAsignacion` comparte la Especial aprobada con "Solicitado por", sin exigir que sea quien la creó. Para que esa columna no regale acceso a un tercero, una persona de CM solo puede ponerse a sí misma: `AprobacionEspecial` lo frena en el alta y `ColumnasAsignacion` en la edición (Admin, Servicio y SYSTEM no se frenan). Plugins 1.0.8.2 (`5034adf`), CI 386/386, 24 pasos activos | `tests/integral/apis/s7-reactivada.js` 8/8: el Admin la crea, User2 la reactiva, User3 la aprueba, el dueño pasa a User5 y User2 la sigue leyendo; User2 poniendo a otro en "Solicitado por" → 400 al editar y al crear. Regresión `solicitar-especial.js` 8/8 |

Cerrados el 2026-10-14 (plugins 1.0.6.1, 24 pasos activos; prueba en vivo 17/17 y suite integral):

| ID | Qué se hizo | Verificación |
|---|---|---|
| S-9 | El cobrador crea pero no edita ni reasigna (`paso6_cobrador_solo_crea.py`, `0ca6427`) | User5 recibe 403 al editar o reasignar; F7 16/16 |
| S-7, DAT-07, DAT-08 | `RepartoAsignacion` con equipo sin cobrador y `RepartoPorAgente` (`906b20e`, `40acf65`) | Pruebas en vivo 17/17 |
| S-11 | Plugin `ColumnasAsignacion`: cada rol escribe solo las columnas de su pantalla | Supervisor cambiando el retiro → 400; cobrador cambiando el orden → 200, cerrando → 400 |
| S-13 | Los roles operativos no leen las trazas de plugins (`paso8_sin_trazas.py`) | 4 privilegios quitados; en PROD falta el nivel "Exception" (sección 5) |
| DAT-11 | Plugin `GrabacionInteraccion`; Transcripción ya no manda fecha ni usuario | Grabar → 200 con fecha y usuario del servidor; volver a grabar → 400; cambiar el comentario → 400. Límite: dos grabaciones en el mismo milisegundo |
| DAT-13, SEC-08 | Plugin `InteraccionDelCobrador`; `CuentaDesdeInteraccion` exige interacción | Sin cuenta → 400; el agente lo pone el servidor; nace Pendiente; pago sin interacción → 400 |
| DAT-10 | Plugin `CargaSobreAsignacion`: la carga reabre lo que cerró el sistema | "Fuera de cartera" que vuelve → reabierta; "Pagada" → sigue cerrada |
| DAT-15 | Plugin `Reclasificacion` (DEC-021) y saldo por cuenta distinta en la Cartera del cobrador | Diaria → Incobrable y vuelta, en los dos sentidos |
| DAT-31 | La carga no pisa la fecha de asignación; Aprobaciones, Cierres y Transcripción escriben por el id | Fecha de asignación conservada |
| DAT-28 | Diarias e Incobrables nacen en Borrador (`AprobacionEspecial`); 6 filas corregidas (`paso9`) | Incobrable nueva → Borrador |
| DAT-16 | Regla de negocio "Orden inicial" borrada (`paso9`); el orden 1..n lo arma la app | — |
| DAT-14 (parte) | Rechazar cierra la Especial; mensaje claro si la cuenta ya tiene una de otro | Suite F5/F10 |
| DAT-18 | Delete = Restrict en interacción→pago, interacción→actualización y cliente→carga diaria; Assign = NoCascade en cuenta→asignación (`paso1`) | Metadata aplicada y publicada |
| DAT-19 | **Por decisión:** la actualización de datos termina en el core del banco; no hace falta más estado | — |
| DAT-26 (parte) | En Maestros, el nombre del cliente es de solo lectura (lo pisa la carga) | — |
| DAT-30 (tabla) | Tabla `usuarioapp` borrada en DEV con tu OK: 0 filas, 0 dependencias para borrar, solo en la solución de CM, ninguna app la usa | `DELETE` 204 y después 404 (2026-10-15) |
| DAT-26 (resto) | La fecha de pago del Detalle de la cuenta se arma como texto (`aaaa-mm-dd` → `dd/mm/aaaa`) sin depender del idioma del teléfono. Sin columna nueva ni cambio en Smelter: en DEV 1001 de 1010 cuentas vienen en `aaaa-mm-dd` y nadie ordena ni filtra por esa fecha | Suite hasta F7 99/99, check nuevo F7-06b |
| DAT-29 | Pantalla "Reasignar" del backoffice: Supervisor (Especiales aprobadas) y Admin (también Incobrables) | Prueba de UI de la pantalla |
| DAT-17, DAT-23, DAT-24 | Una sola fuente del correo del agente (el backoffice solo escribe el usuario); "↻" recarga los maestros; el perfil offline filtra las activas | Revisión de código y perfil |
| DEC-019 | Borrado diario de la carga diaria con más de 60 días (`paso10`) | Trabajo programado |
| Fiadores | Causa: fiadores vacíos por `NombreFianza` en blanco. Borrado diario de los fiadores sin nombre (`paso10`) | 2777 huérfanos, 8 reales |
| Reparto | 825 asignaciones con dueño SYSTEM repartidas (`paso11`) | 0 restantes, 0 trabajos fallidos |
| Reconciliación | `paso3_reconciliar.py` lista y, con `--revocar`, quita compartidos sobrantes | 0 sobrantes en DEV |
| Backoffice: UPN | El backoffice identifica al usuario por su id de Entra | Suite (todos los roles entran) |
| Suite | Esperas estables en los contadores de Inicio (F3, F5, F8) y en F7 | Suite integral |
| Smelter DIAX | Los 2 trabajos DIAX borrados (con tu OK) | — |
| Docs | "El flow reparte", README de plugins, sincronización completa (el botón existe; guía 05 corregida), catálogo, ciclo de vida, contrato de datos, guía por rol | — |
