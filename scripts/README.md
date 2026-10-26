# Scripts

Scripts de Python que instalan, despliegan y mantienen Cobranza Móvil por la Web API de Dataverse. Hay que leer la columna «Grupo» antes de correr cualquiera: muchos aplicaron una migración en el ambiente donde se construyó la solución y en una instalación nueva **no** hacen falta. La receta de instalación está en [Instalación y despliegue](../docs/tecnica/09-instalacion-y-despliegue.md) y la operación en [Mantenimiento](../docs/tecnica/11-mantenimiento.md).

## Uso

```bash
python3 scripts/<carpeta>/<script>.py            # muestra el plan (solo lecturas)
python3 scripts/<carpeta>/<script>.py --aplicar  # aplica
```

- Se corren **desde la raíz del repositorio** (algunos escriben respaldos en `local/_backups/` con ruta relativa).
- Excepciones a «plan por defecto»: los scripts de prueba de la sección 4 (`paso6_prueba.py`, `paso2_prueba.py`) crean datos apenas se corren; `paso7_esquema.py` y `paso7_fuentes.py` reciben rutas como argumentos.
- Credenciales: `DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET` y `PP_TENANT_ID` en `pp_secrets.env` (raíz, fuera de git) o en el archivo que indique `PP_SECRETS`. `paso5_plugins.py` y `paso12_custom_api.py` aceptan además variables de entorno; el resto, solo el archivo.
- La clave K se lee del archivo indicado en `PAN_HMAC_KEY_FILE`. Nunca se imprime.
- Los nombres `paso*` reflejan el orden en que se construyó la solución, no el orden de instalación.

## 1. Necesarios para instalar

En el orden en que se corren, después de importar la *solution*. Todos son idempotentes. Los pasos manuales intercalados (crear los equipos, crear la plantilla principal y asignarla al equipo de carga) están en la receta de [Instalación y despliegue](../docs/tecnica/09-instalacion-y-despliegue.md#instalación-paso-a-paso).

| Script | Qué hace |
|---|---|
| [`pan/paso5_plugins.py`](pan/paso5_plugins.py) `--dll … --version … --public-key-token … --activar-panguard` | Registra o actualiza el ensamblado de *plugins* y sus pasos; con `--activar-panguard` deja todos activos. Requiere la dll firmada con la misma `.snk` |
| [`pan/paso12_custom_api.py`](pan/paso12_custom_api.py) | Registra o verifica las cuatro *Custom APIs*. Después de `paso5_plugins.py` |
| [`seguridad/paso5_cartera_sin_cobrador.py`](seguridad/paso5_cartera_sin_cobrador.py) `--entorno <ENV>` | Equipo «CM · Cartera sin cobrador · `<ENV>`» y su rol. Antes de la primera carga. Los demás equipos se crean a mano en este momento |
| [`pan/paso6_clave_k.py`](pan/paso6_clave_k.py) | Carga K (del archivo de `PAN_HMAC_KEY_FILE`) en la tabla protegida de Smelter y le quita privilegios sobre ella a System Customizer. Se usa en todos los ambientes, producción incluida: su comentario inicial, que habla de Key Vault, está desactualizado |
| [`pan/paso4_roles.py`](pan/paso4_roles.py) | Privilegios de la bóveda del número de tarjeta y quita los que System Customizer recibe sobre ella |
| [`pan/paso23_listas.py`](pan/paso23_listas.py) | Tabla de listas editables, sus privilegios y las opciones iniciales. Antes del *offline profile* |
| [`pan/paso13_fiador_sin_nombre.py`](pan/paso13_fiador_sin_nombre.py) | Ajuste de la plantilla principal de Smelter: fiador vacío → «(sin fiador)». La plantilla principal tiene que existir |
| [`pan/paso19_agente_sin_update.py`](pan/paso19_agente_sin_update.py) | Ajuste de la plantilla principal: la carga no reescribe agentes existentes |
| [`pan/paso20_plantillas_paralelas.py`](pan/paso20_plantillas_paralelas.py) | Genera las plantillas «Cobranza movil · padres» y «· partes» a partir de la principal, con su mismo dueño (el equipo de carga) |
| [`pan/paso7_perfil_offline.py`](pan/paso7_perfil_offline.py) | Crea o verifica y publica el *offline profile* de la app del localizador |
| [`seguridad/paso10_borrado_programado.py`](seguridad/paso10_borrado_programado.py) | Programa los trabajos diarios de *bulk delete* |

## 2. Despliegue y mantenimiento

| Script | Cuándo |
|---|---|
| [`pan/paso7_desplegar.py`](pan/paso7_desplegar.py) | Publicar las apps. `--parche` despliega solo la *solution* `cm_parche` |
| [`pan/paso22_parche.py`](pan/paso22_parche.py) | Elegir qué componentes van en `cm_parche` (`--app cobrador` es la app del localizador, por su nombre histórico) |
| [`pan/paso7_fuentes.py`](pan/paso7_fuentes.py) | Agregar o refrescar una tabla en las fuentes de datos de una app (`.msapr`) |
| [`pan/paso7_esquema.py`](pan/paso7_esquema.py) | Chequeo de columnas de las fórmulas. Lo llama `paso7_desplegar.py`; se puede correr suelto |
| [`seguridad/paso3_reconciliar.py`](seguridad/paso3_reconciliar.py) | Después de cada despliegue |
| [`pan/paso10_purgar_archivos.py`](pan/paso10_purgar_archivos.py) | Después de cada carga: borra el CSV fuente, que trae números de tarjeta completos. ⚠️ Hoy solo revisa la plantilla principal (ver [Mantenimiento](../docs/tecnica/11-mantenimiento.md#tareas-recurrentes)) |
| [`pan/paso5_plugins.py`](pan/paso5_plugins.py), [`pan/paso12_custom_api.py`](pan/paso12_custom_api.py) | Al publicar una versión nueva de los *plugins* o una *Custom API* nueva |
| [`pan/paso20_plantillas_paralelas.py`](pan/paso20_plantillas_paralelas.py) | Después de cada cambio en la plantilla principal de Smelter |
| [`pan/paso7_perfil_offline.py`](pan/paso7_perfil_offline.py) | Cuando la app del localizador usa una tabla nueva |
| [`seguridad/paso10_borrado_programado.py`](seguridad/paso10_borrado_programado.py) | Al cambiar un plazo de borrado |
| [`seguridad/paso1_cascadas_auditoria.py`](seguridad/paso1_cascadas_auditoria.py), [`seguridad/paso4_rol_basic.py`](seguridad/paso4_rol_basic.py), [`seguridad/paso6_cobrador_solo_crea.py`](seguridad/paso6_cobrador_solo_crea.py), [`seguridad/paso7_sin_flows.py`](seguridad/paso7_sin_flows.py), [`seguridad/paso8_sin_trazas.py`](seguridad/paso8_sin_trazas.py), [`pan/paso4_roles.py`](pan/paso4_roles.py) | Verificación: sin `--aplicar` confirman que roles, cascadas y auditoría siguen como deben |
| [`seguridad/dvlib.py`](seguridad/dvlib.py), [`seguridad/roles_cm.py`](seguridad/roles_cm.py) | Bibliotecas comunes. No se corren solas |

## 3. Migraciones ya aplicadas, solo de referencia

Aplicaron un cambio de esquema o un arreglo de datos en el ambiente donde se construyó la solución. Su resultado ya está en la *solution* o fue una corrección de datos existentes. **En una instalación nueva no se corren.** Sirven para entender por qué el esquema es como es.

| Script | Qué cambió |
|---|---|
| [`pan/paso1_esquema.py`](pan/paso1_esquema.py) | Bóveda del número de tarjeta y claves sustitutas (en la *solution*) |
| [`pan/paso2_auditoria.py`](pan/paso2_auditoria.py) | Auditoría de columna apagada en columnas que guardaban el número |
| [`pan/paso3_backfill.py`](pan/paso3_backfill.py) | Pasó los números existentes a la bóveda y calculó sus HMAC |
| [`pan/paso3b_claves.py`](pan/paso3b_claves.py) | Claves alternativas sobre el HMAC (en la *solution*) |
| [`pan/paso6_plantilla.py`](pan/paso6_plantilla.py) | Pasó la plantilla de Smelter a claves HMAC |
| [`pan/paso7_opcionales.py`](pan/paso7_opcionales.py) | Columnas viejas de número pasadas a opcionales |
| [`pan/paso8_contraer.py`](pan/paso8_contraer.py) | Enmascaró los números fuera de la bóveda y borró las columnas viejas |
| [`pan/paso14_actualizacion_offline.py`](pan/paso14_actualizacion_offline.py) | Columnas para vincular actualizaciones de datos hechas sin señal |
| [`pan/paso15_marcas_offline.py`](pan/paso15_marcas_offline.py) | Marcas «Sin vincular» y «Recibo repetido»; quitó la clave de recibo |
| [`pan/paso16_especial_desde_cuenta.py`](pan/paso16_especial_desde_cuenta.py) | Completó datos de Especiales existentes |
| [`pan/paso17_identidad_interaccion.py`](pan/paso17_identidad_interaccion.py) | Identidad de la cuenta en la interacción (`--rellenar` completó las existentes) |
| [`pan/paso18_tramo_cartera.py`](pan/paso18_tramo_cartera.py) | Columnas de tramo (`--rellenar` completó las existentes) |
| [`pan/paso21_tipo_producto.py`](pan/paso21_tipo_producto.py) | Completó el tipo de producto de cuentas existentes |
| [`localizador/backfill_fechas_numeros.py`](localizador/backfill_fechas_numeros.py) | Completó fechas y números vacíos de pagos e interacciones |
| [`localizador/precision_gps.py`](localizador/precision_gps.py) | Latitud y longitud con 6 decimales (en la *solution*) |
| [`seguridad/paso1_cascadas_auditoria.py`](seguridad/paso1_cascadas_auditoria.py), [`seguridad/paso1c_auditar_historia_especial.py`](seguridad/paso1c_auditar_historia_especial.py) | Cascadas y auditoría (en la *solution*; útiles como verificación) |
| [`seguridad/paso4_rol_basic.py`](seguridad/paso4_rol_basic.py), [`seguridad/paso6_cobrador_solo_crea.py`](seguridad/paso6_cobrador_solo_crea.py), [`seguridad/paso7_sin_flows.py`](seguridad/paso7_sin_flows.py), [`seguridad/paso8_sin_trazas.py`](seguridad/paso8_sin_trazas.py) | Privilegios de los roles (en la *solution*; útiles como verificación) |
| [`seguridad/paso9_limpieza_asignacion.py`](seguridad/paso9_limpieza_asignacion.py) | Corrigió el estado de aprobación de asignaciones viejas y borró una regla de negocio en borrador |
| [`seguridad/paso11_reparto_pendiente.py`](seguridad/paso11_reparto_pendiente.py) | Repartió asignaciones que habían quedado con dueño SYSTEM |

⚠️ Si se instala desde un export de la *solution* anterior a alguno de estos cambios, faltarán columnas o tablas. La solución es exportar un Release nuevo, no correr estos scripts en otro ambiente.

## 4. Pruebas y retirados

| Script | Estado |
|---|---|
| [`pan/fixture_telefonos.py`](pan/fixture_telefonos.py) | Datos de prueba para los teléfonos. Solo desarrollo |
| [`pan/paso6_prueba.py`](pan/paso6_prueba.py) | Prueba de la carga con HMAC. Solo desarrollo |
| [`seguridad/paso14_limpieza_total.py`](seguridad/paso14_limpieza_total.py) | ⚠️ Borra todos los datos de negocio. Solo desarrollo, nunca producción |
| [`localizador/clave_recibo_pago.py`](localizador/clave_recibo_pago.py) | **Retirado. No correr**: la clave que crea hace perder pagos sin señal |
| [`seguridad/paso2_flow.py`](seguridad/paso2_flow.py), [`seguridad/paso2_prueba.py`](seguridad/paso2_prueba.py), [`seguridad/flow-owner-base.json`](seguridad/flow-owner-base.json) | **Retirados**: el *flow* de dueños lo reemplazó el *plugin* `RepartoAsignacion` |
