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
| DAT-14 (resto) | Un Solicitante no ve las Especiales que pidió otro (lectura Basic): si la cuenta ya tiene una, al pedirla recibe el error de clave duplicada. Desde 2026-10-14 el mensaje explica qué pasa y Rechazar cierra la Especial (quien la pidió puede volver a pedirla) | Custom API `SolicitarEspecial` con el servicio del sistema: crea o reactiva la Especial y, si ya existe una activa, avisa quién la pidió. Exige agregar la fuente "Environment" a la app en Studio (un paso manual tuyo). Descartada: lectura Global para el Solicitante (ampliaba privilegios) | CM, más un paso tuyo en Studio | Mediano | **Decidido (a)**, 2026-10-14. Código listo (1.0.7.0, commit `00f3388`, CI 307/307); falta registrarlo en DEV, tu paso en Studio y el cambio en Nueva solicitud |
| DAT-20 | El backoffice no puede crear cuentas, así que no se puede pedir una Especial para un cliente nuevo | Decisión del dueño (2026-10-14): **el Admin** da de alta la cuenta, igual que hoy da de alta un cliente en Maestros. Choca con dos decisiones de la Fase 2: el Admin solo ve el PAN enmascarado (D3) y ningún plugin de CM conoce K (vive en la tabla protegida de Smelter). **Propuesta (recomendada):** el alta del Admin entra por el mismo camino que el CSV, como una importación de una fila con la plantilla de Smelter, que ya calcula el HMAC, llena la bóveda y enmascara. **Alternativa:** una Custom API de CM que calcule el HMAC, con una segunda copia de K (dos lugares que rotar y proteger). En las dos, el PAN pasa por el navegador del Admin | Dueño → CM | Grande | Esperando elección de camino |
| DAT-30 | Verificado el 2026-10-14. Huérfanos de verdad: la tabla `usuarioapp` (0 filas, sin privilegios, nadie la usa) y, en la cuenta, `ct_agenteid` y `ct_tipoasignacion` (la carga los llena en 1001 cuentas y nadie los lee). **No** son huérfanos: `ct_fiador` (el reparto y la reconciliación comparten el fiador con el cobrador), `ct_productoid` (vínculo con el maestro de productos) y `[AS] Resolutor` (lo muestra el Historial de Especiales) | Borrar `usuarioapp`. `ct_agenteid` y `ct_tipoasignacion`: dejarlos y documentarlos como "solo carga" (borrarlos exige quitar sus mapeos en la plantilla de Smelter y es irreversible), o quitarlos con tu OK | CM (+ Smelter config) | Chico | Esperando OK |
| Fiadores (origen) | La carga crea un fiador vacío por cada fila del CSV con `NombreFianza` en blanco (la clave alternativa por nombre no rige con NULL) | Corrección de origen en la plantilla de Smelter: no crear el fiador si viene vacío. Mientras tanto, el trabajo diario "CM - Fiadores sin nombre" los borra | Smelter (config) | Chico | Mitigado |
| Revocación offline | El teléfono pierde una cuenta revocada recién en la próxima sincronización | Inherente a offline. Se mitiga en PROD con Intune (borrado tras N días sin conexión, S-3) | Tenant | — | Propuesta: aceptar |
| Padrón viejo | Los teléfonos que tuvieron el padrón completo (antes de S-1) lo conservan hasta reiniciar la app | Borrar los datos de la app (o reinstalarla) en esos teléfonos: en DEV es el tuyo (User6). En PROD no aplica | Dueño | Chico | Pendiente (tu teléfono) |

## 3. Apps

| ID | Problema | Propuesta | Quién | Esfuerzo | Estado |
|---|---|---|---|---|---|
| APP-11 | El GPS con 6 decimales nunca se probó con GPS real (el Android de pruebas no tiene) | Una captura desde tu teléfono con el GPS encendido; yo verifico los decimales en Dataverse | Dueño (teléfono) | Chico | Pendiente (decisión: seguir esperando) |
| APP-04 | Nunca se probó qué pasa si falla el guardado de la interacción | Provocar el fallo desde el servidor en una prueba (por ejemplo, con un usuario QA sin agente vinculado, que `InteraccionDelCobrador` rechaza) y verificar que la app avise, no diga "✓ Guardado" y no deje huérfanos | CM | Mediano | Propuesta |
| APP-27 | El paquete base de las dos apps (`.msapr` del repo) dice `ParserErrorCount: 2` | El dato lo calcula Studio al guardar; el despliegue valida el YAML y el App Checker no muestra errores. Abrir cada app en Studio, guardar y volver a exportar el paquete base | Dueño (Studio) | Chico | Propuesta |
| S-7 tras reactivar | Si un Solicitante reactiva (con `SolicitarEspecial`) una Especial cerrada que creó otra persona, la pierde de vista cuando se aprueba: S-7 la comparte solo si `solicitadopor` = `createdby` | Como `solicitadopor` ahora lo pone el servidor, S-7 puede compartir por `solicitadopor` solo | CM | Chico | Propuesta (después de DAT-14) |
| F7 / versión servida | Las dos fallas de F7-03 mostraron la versión anterior de la app, una de ellas 1 minuto después de un despliegue correcto. Hipótesis: tras publicar, DEV sirve la versión vieja un rato | Estampar la versión (commit y hora) en las apps al desplegar, mostrarla en Sincronización y al pie del menú, y que la suite la compare antes de chequear (si no coincide, espera hasta 10 min y corta con un mensaje claro) | CM | Chico | Propuesta |
| Rol por pantalla | El backoffice decide qué pantallas mostrar con `gblRol`, del lado del cliente | Matriz rol × tabla × operación contra la API (suplantando a cada usuario QA). Ya hay pruebas por API de lo más sensible (S-9, S-11, DAT-11, DAT-13) | CM | Mediano | Propuesta |

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
