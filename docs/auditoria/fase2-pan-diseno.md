# Fase 2 · PAN / PCI: diseño con restricciones verificadas

> Fecha: 2026-10-04 · Entorno: DEV · Estado: **propuesta para aprobar** (nada aplicado).
> Base: decisiones del dueño del producto en [00-lista-consolidada.md](00-lista-consolidada.md) ("Diseño PCI acordado") y la Fase 1 en [scripts/seguridad](../../scripts/seguridad/README.md).
> Método:
> - Documentación oficial de Microsoft Learn (URL en cada fila).
> - Metadata y datos de Dataverse DEV leídos solo con GET (la consulta OData se cita como evidencia).
> - Lectura del repo de esta solución y del código de Smelter (`/home/gmaker/projects/smelter`), solo lectura.
>
> No se imprime ningún PAN. Los ejemplos van enmascarados.
> Confianza: **V** = verificado (la documentación lo dice explícito, o hay metadata o datos que lo prueban). **P** = probable (inferido, o la documentación no lo dice).

## TL;DR

1. **El diseño acordado no se puede aplicar tal cual.** Hay tres restricciones verificadas que lo rompen:
   - **El nombre principal no admite seguridad de columna.** Así que el PAN tiene que salir de `ct_numerodecuenta`.
   - **Una clave alternativa no puede incluir una columna con seguridad de columna.** Smelter hace **UpsertRequest por clave alternativa**, así que no puede encontrar la cuenta por un PAN protegido.
   - **El modo offline no respeta la seguridad de columna.** Microsoft lo declara "no soportado". Que el cobrador vea el PAN sin señal pasaría a depender de un comportamiento sin soporte.
2. **Diseño ajustado (recomendado):**
   - **PAN en una tabla bóveda** `sanic_cm_tbl_cuentapan`, 1:1 con cuenta. Se protege con **seguridad de fila y de tabla**, que el offline sí respeta, y no con seguridad de columna.
   - La bóveda se comparte con el cobrador **en cascada** desde la cuenta. Así reutiliza el flow de la Fase 1.
   - Solicitante y Supervisor no tienen ningún privilegio sobre esa tabla.
3. **Clave sustituta = HMAC-SHA256(PAN) con clave secreta.** Es determinística, no reversible sin la clave y cumple PCI 3.5.1.1 (hash con clave).
   - Pasa a ser la clave alternativa de cuenta, bóveda, asignación (+ tipo) y carga diaria (+ fecha), y el campo por el que Smelter resuelve los lookups.
   - Smelter **no tiene función de hash**: sus transformaciones son `CONCAT`, `RIGHT`, `REPLACE`, etc. Tampoco se puede registrar un plugin en el mensaje `Upsert` de tablas custom (verificado).
   - **Quién calcula el HMAC en la carga es la decisión D1**: Smelter (recomendado), el extracto del banco, o una tabla de aterrizaje con un plugin de CM.
4. **Enmascarado:**
   - `ct_numerodecuenta` (nombre principal) pasa a guardar `****1234`. Lo calcula la propia plantilla con `CONCAT("****", RIGHT(NumeroCuenta, 4))`.
   - Se agrega `ct_ultimos4` para buscar.
   - No se usa una formula column, porque no muestra valores offline (V).
5. **Hallazgos PCI nuevos** que la Fase 2 tiene que limpiar:
   - **El historial de auditoría tiene PAN en claro.** Los 58 *Create* de cuenta tienen `newValue` de `ct_numerodecuenta`, con retención de 365 días.
   - **Smelter guarda el CSV fuente** (archivo con los PAN) en `sanic_smlt_ij_sourcefile` de cada job, sin vencimiento.
   - **El historial de corridas del flow de owner** guarda `as_numerocuenta`: el disparador devuelve la fila completa.

---

## Acta de decisiones (2026-10-04)

| # | Decisión del dueño del producto |
|---|---|
| **D1** | **P1: función `HMAC()` en Smelter.** El dueño lo autoriza solo para este fin. El cambio exacto en Smelter se presenta para aprobación antes de aplicarse |
| **D2** | **A: bóveda `sanic_cm_tbl_cuentapan`** con seguridad de fila |
| **D4** | **No se purga la auditoría de DEV** (datos sintéticos). Requisito para PROD: el PAN nunca entra en columnas auditadas fuera de la bóveda (pasos 2 y 8 antes de la primera carga real) |
| **D5** | **Últimos 4 + CIF o nombre** para que el Solicitante identifique la cuenta |
| D3, D6–D10 | Se aplican las **recomendaciones** de la tabla de decisiones, a confirmar por el dueño: K en Key Vault con managed identity (en DEV, en un archivo fuera del repo); BackOffice con lectura Global de la bóveda y Admin solo enmascarado; CSV de Smelter borrado al terminar; `Copy()` solo en BackOffice; Luhn en PROD |
| **D1.1** | El dueño **aprobó el cambio exacto en Smelter**: transformación `HMAC(valor, "nombreClave")` (HMAC-SHA256, hex minúsculas, sobre el valor sin espacios) + `IHmacKeyProvider` + pruebas + documentación del DSL |
| **D3.1** | **Cómo le llega K a Smelter.** Verificado en Microsoft Learn: un Custom API (ProcessChunk) **no recibe configuración segura**, y las variables de entorno con secreto de Key Vault **solo llegan a flows, Copilot Studio y conectores personalizados**, no a plugins. El único camino GA es la **identidad administrada de Power Platform** (ensamblado firmado + credencial federada + `IManagedIdentityService`). **DEV:** K en una tabla protegida de Smelter (sin privilegios para ningún rol, la lee SYSTEM). Tiene que ser la misma K del archivo que usó el backfill. **PROD:** identidad administrada + Key Vault; la tabla de DEV **no sirve para PROD** (los administradores pueden leer K y queda junto a los datos) |
| **D11** | **AS-2002 borrada** (2026-10-04). Era una Especial duplicada y sin agente sobre la misma cuenta que AS-2001, creada sin `as_numerocuenta` por una versión anterior del backoffice. No tenía registros dependientes. Respaldo en `local/_backups/` (fuera del repo). Se mantiene la regla de **una asignación por cuenta y tipo**, que el backoffice ya aplica |

### Estado de ejecución en DEV (2026-10-04)

| Paso | Estado | Evidencia |
|---|---|---|
| 1–4 | ✅ | Commits `a470e7e`, `41444b1`. 6 claves nuevas `Active` (la de asignación, después de D11) |
| 5 | ✅ registrado | Ensamblado `Sanic.CobranzaMovil.Plugins` 1.0.0.0 (sandbox, firmado; la clave `.snk` está fuera del repo y en el secret `PLUGIN_SNK_BASE64`). 15 pasos: **12 PanGuard desactivados** (se activan en el paso 8), `ClaveDesdeCuenta` ×2 y `CuentaDesdeInteraccion` activos. 129 pruebas en verde en CI. `ClaveDesdeCuenta` usa la clave de la referencia cuando la cuenta llega por clave alternativa sin Id (upsert de Smelter) |
| 6 | ✅ | **Smelter** rama `feat/hmac-transform` (HMAC + tabla `sanic_tbl_smlt_hmackey`, `Smelter.Plugins` 1.0.6) desplegada en DEV. K cargada con `paso6_clave_k.py` (coincide con la del backfill). System Customizer sin acceso a la tabla; quedan System Administrator y roles de Microsoft que no se pueden editar. Plantilla con `paso6_plantilla.py` (respaldo previo en `local/_backups/`). **`paso6_prueba.py`: OK dos veces** (3 filas QA, 2 importaciones cada una: 0 fallos, 4 claves = HMAC, bóveda 1:1 enmascarada, sin duplicados, logs sin PAN, limpieza completa) |
| 7–12 | Pendiente | — |

**Lección del despliegue de Smelter:** sin subir la versión de `Smelter.Plugins`, algunos workers del sandbox siguieron con la versión anterior y la carga fallaba de forma intermitente con `Unknown function: HMAC`. Cada cambio de código de un plugin tiene que subir la versión.

**⚠ Mientras la rama de Smelter no esté en `main`:** si alguien despliega Smelter desde `main` en DEV, `HMAC()` desaparece y **toda** carga de "Cobranza movil" falla por fila (no se escribe nada con clave vacía). Volver atrás: reimportar la plantilla del respaldo.

Verificaciones independientes del consolidador:
- `ct_numerodecuenta` es nombre principal con `CanBeSecuredForRead = false`.
- La única clave alternativa de cuenta es `sanic_cm_pk_ct_numerodecuenta` (el PAN).
- Microsoft Learn: *"Mobile offline honors the security model… except the field-level security"*.
- Hay 58 auditorías de alta de cuenta.

## 1. Restricciones verificadas

| # | Restricción | Resultado | Fuente / evidencia | Conf. |
|---|---|---|---|---|
| R1 | Seguridad de columna sobre el **nombre principal** | **No se puede.** "Columns that can't be secured include: … Primary name columns" | [field-level-security#which-columns-can-be-secured](https://learn.microsoft.com/power-platform/admin/field-level-security#which-columns-can-be-secured). Metadata: `EntityDefinitions(LogicalName='sanic_cm_tbl_cuenta')/Attributes(LogicalName='sanic_cm_ct_numerodecuenta')` → `IsPrimaryName=true`, `CanBeSecuredForRead/Create/Update=false`. Lo mismo con `cargadiaria.cd_numerocuenta` y `account.name` | V |
| R2 | Otras columnas que no se pueden asegurar | Lookups, formula columns, columnas de tablas virtuales y columnas de sistema | misma URL | V |
| R3 | **Clave alternativa** sobre una columna con seguridad | **No se puede.** "Attributes must not have field-level security applied" | [define-alternate-keys-entity](https://learn.microsoft.com/power-apps/developer/data-platform/define-alternate-keys-entity). Ojo: la metadata de `as_numerocuenta`, que está en una clave activa, igual dice `CanBeSecuredForRead=true`. La metadata **no** refleja esta regla; la regla viene de la documentación | V |
| R4 | Límites de las claves alternativas | Tipos permitidos: Decimal, Whole, texto de una línea, DateTime, **Lookup** y OptionSet. Hasta 16 columnas y 900 bytes por clave, y 10 claves por tabla | misma URL | V |
| R5 | **Offline y seguridad de columna** | "Mobile offline honors the security model … **except the field-level security and field sharing**". "Field level security and field sharing **aren't supported** in Mobile offline mode" | [best-practices-offline](https://learn.microsoft.com/power-apps/mobile/best-practices-offline#tips-on-mobile-offline-synchronization), [offline-limitations](https://learn.microsoft.com/power-apps/mobile/offline-limitations#feature-limitations). La segunda página es de model-driven. La de canvas ([limitations-canvas-apps](https://learn.microsoft.com/power-apps/mobile/limitations-canvas-apps)) no lo menciona, pero es el mismo motor offline | V (MDA) / P (canvas) |
| R6 | Offline y seguridad de fila | Sí la respeta: "honors the security model … and the hierarchical security model". Prueba propia: en la Fase 1, con lectura Basic, el teléfono bajó **2 cuentas de 1.004** | misma URL + [README Fase 1](../../scripts/seguridad/README.md) | V |
| R7 | ¿La columna protegida baja al teléfono? ¿Baja vacía? | **La documentación no lo dice.** "No soportado" puede significar cualquiera de las dos cosas | — | Sin dato. Hay que probarlo en el dispositivo (opcional, solo si se elige la alternativa B) |
| R8 | ¿Se cifra la caché offline? | La documentación no dice que la caché SQLite esté cifrada por la app. Intune: "Provide encryption at rest", restricción de copiar y pegar. Pero también: "Intune advanced app settings aren't currently supported with Power Apps Mobile" | [mobile/intune](https://learn.microsoft.com/power-apps/mobile/intune), [mobile-offline-works-overview](https://learn.microsoft.com/power-apps/mobile/mobile-offline-works-overview) | P. **Hay que confirmarlo con Microsoft o con TI** |
| R9 | Quitar un permiso no limpia el teléfono | "Any changes to a user's privilege to view a row won't take effect on the mobile device until the next synchronization cycle" | best-practices-offline | V |
| R10 | Formula column (Power Fx) offline | "Formula columns don't display values when the app is in mobile offline mode". Además no se pueden asegurar | [formula-columns](https://learn.microsoft.com/power-apps/maker/data-platform/formula-columns) | V |
| R11 | Calculated column que lee una columna protegida | Se calcula **sin** aplicar la seguridad de columna; la documentación recomienda asegurarla también ("consider securing the calculated column as well"). No hay `LEN` ni `RIGHT`, solo `TRIMLEFT(texto, n)`, que obliga a una longitud fija | [define-calculated-fields](https://learn.microsoft.com/power-apps/maker/data-platform/define-calculated-fields) | V |
| R12 | Lookups con seguridad de fila en canvas offline | Un solo nivel de lookup en `Filter`/`LookUp`. Pago → Interacción → Cuenta (dos niveles) **no** funciona offline | [limitations-canvas-apps](https://learn.microsoft.com/power-apps/mobile/limitations-canvas-apps#offline-runtime-limitations) | V |
| R13 | Dataverse search | Respeta la seguridad de columna. En DEV **ninguna tabla `sanic_cm` está indexada** (`SyncToExternalSearchIndex=false`; la solución solo incluye interacción, con `in_numerocuenta` en IsSearchable=0) | [relevance-search](https://learn.microsoft.com/power-apps/user/relevance-search#understand-search-results). Metadata en DEV | V |
| R14 | Vistas, Quick Find, Web API sin permiso | Devuelven null y no se distingue de un vacío real. Con la columna en el filtro, no encuentran nada | [column-level-security#display-masked-data](https://learn.microsoft.com/power-apps/developer/data-platform/column-level-security#display-masked-data) | V (retrieve) / P (filtro) |
| R15 | Exportar a Excel | Corre con la identidad del usuario. Con seguridad de fila, el usuario sin privilegio sobre la tabla no la puede exportar. En DEV: `allowwebexcelexport=true` y máximo de 100.000 filas | [export-data-excel](https://learn.microsoft.com/power-apps/user/export-data-excel); `organizations?$select=allowwebexcelexport,maxrecordsforexporttoexcel` | V |
| R16 | Auditoría de una columna con seguridad | En Purview se registra "*". El historial de Dataverse **no dice** si oculta el valor | [field-level-security#activity-logging-data](https://learn.microsoft.com/power-platform/admin/field-level-security#activity-logging-data) | V / sin dato |
| R17 | **El PAN ya está en la auditoría** | `audits?$filter=objecttypecode eq 'sanic_cm_tbl_cuenta' and action eq 1` → **58 creates con `newValue` de `ct_numerodecuenta`**. Retención: `auditretentionperiodv2` = 365 días. La carga diaria también se audita desde la Fase 1 | Dataverse DEV | V |
| R18 | Borrar el historial de auditoría | `DeleteRecordChangeHistory` borra **todo** el historial de un registro. Para algo más selectivo, `BulkDelete` sobre `audit` | [delete-audit-data](https://learn.microsoft.com/power-apps/developer/data-platform/auditing/delete-audit-data) | V |
| R19 | Desactivar la auditoría por columna | Existe `IsAuditEnabled` por columna. Al hacer un Update, la auditoría guarda el **valor anterior** (por eso hay que apagarla **antes** de enmascarar) | metadata `IsAuditEnabled` por atributo; comportamiento estándar de `changedata` (oldValue/newValue) visto en R17 | V |
| R20 | Detección de duplicados y rollups | La documentación no habla de su interacción con columnas protegidas. En DEV: **0 reglas de duplicados** en `sanic_cm` (`duplicaterules`) y no hay rollups sobre el PAN | — / Dataverse | V (DEV) |
| R21 | Power Automate y Service Principal | La seguridad de columna aplica a todas las llamadas a la API. Una app user que no es administrador deja de leer las columnas protegidas hasta que está en un perfil | [sample column-level-security](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/samples/column-level-security-powershell) | V (app user) / P (conexión del flow) |
| R22 | Plugins | `CreateOrganizationService(null)` corre como **SYSTEM** | [IOrganizationServiceFactory](https://learn.microsoft.com/dotnet/api/microsoft.xrm.sdk.iorganizationservicefactory.createorganizationservice) | V. Que SYSTEM saltee la seguridad de columna es P |
| R23 | Masking rules nativas (enmascarado + "Read unmasked") | **Preview**, exigen Managed Environment, el botón para desenmascarar solo lo ven los administradores, y no está documentado su uso en canvas ni offline | [column-level-security#display-masked-data](https://learn.microsoft.com/power-apps/developer/data-platform/column-level-security#display-masked-data) | V. **No sirven** para el cobrador |
| R24 | Administradores | "Column-level security doesn't apply for users who have the system administrator role". DEV tiene **15** administradores (S-12) | field-level-security | V |
| R25 | **Smelter hace upsert por clave alternativa** | `UpsertWriter.cs:12-13, 202-220`: arma `UpsertRequest` con `KeyAttributes` = `tm_dedupcolumns`. Sin columnas de dedup cae en Create/Update "NOT idempotent" (`:56-58`) | código de Smelter (lectura) | V |
| R26 | **Smelter resuelve los lookups por clave alternativa** | `NormalizationEngine.cs:174-177`: `new EntityReference(LookupTable, LookupMatchColumn, valor)`. `LookupMatchColumn` tiene que ser una clave alternativa de la tabla destino | código de Smelter | V |
| R27 | **Transformaciones de Smelter** | `TransformEngine.cs:69-110`: CONCAT, UPPER, LOWER, TRIM, LEFT, **RIGHT**, DATEPARSE, REPLACE, SPLIT, IF, COALESCE, STATIC, MAP. **No hay hash.** La plantilla "Cobranza movil" hoy no usa ninguna | código de Smelter; `sanic_tbl_smlt_columnmappings` | V |
| R28 | Smelter corre como SYSTEM | `ProcessChunkPlugin.cs:26` `CreateOrganizationService(null) // SYSTEM` | código de Smelter | V |
| R29 | **No hay plugins en `Upsert` para tablas custom** | `sdkmessagefilters` del mensaje Upsert: 48 filtros, ninguno para `sanic_cm_tbl_cuenta`. Create y Update sí (`iscustomprocessingstepallowed=true`). Así que **no se puede reescribir la clave del upsert** desde un plugin | Dataverse: `sdkmessages?$filter=name eq 'Upsert'` → `sdkmessagefilters?$filter=_sdkmessageid_value eq …` | V |
| R30 | El upsert busca por clave **antes** de llamar a Create o Update | Los plugins de Create/Update corren, pero **no pueden cambiar** si encuentra o no el registro | [use-upsert-insert-update-record](https://learn.microsoft.com/power-apps/developer/data-platform/use-upsert-insert-update-record#understand-the-upsert-process-for-standard-tables) | V |
| R31 | Secretos en variables de entorno (Key Vault) desde un plugin | **No se puede.** "limited for use with Power Automate flows, Copilot Studio agents, and custom connectors" | [environmentvariables-azure-key-vault-secrets](https://learn.microsoft.com/power-apps/maker/data-platform/environmentvariables-azure-key-vault-secrets) | V |
| R32 | Secreto para un plugin | (a) **Secure configuration** del paso: "only system administrators" la leen y no viaja en la solución. (b) **Managed identity** del plugin → Key Vault (GA) | [register-plug-in#set-configuration-data](https://learn.microsoft.com/power-apps/developer/data-platform/register-plug-in#set-configuration-data), [managed-identity-overview](https://learn.microsoft.com/power-platform/admin/managed-identity-overview) | V |
| R33 | **Smelter guarda el archivo fuente** | `sanic_tbl_smlt_importjob.sanic_smlt_ij_sourcefile` es de tipo File. Los 10 jobs más recientes lo tienen poblado. Esos archivos contienen la columna `NumeroCuenta` | Dataverse: `EntityDefinitions(LogicalName='sanic_tbl_smlt_importjob')/Attributes(LogicalName='sanic_smlt_ij_sourcefile')` → FileType; `sanic_tbl_smlt_importjobs?$select=sanic_smlt_ij_sourcefile_name` | V |
| R34 | El historial de corridas del flow de owner guarda el PAN | El disparador (`SubscribeWebhookTrigger` sobre asignación) devuelve la fila completa, incluida `as_numerocuenta`, y no tiene `secureData` | `scripts/seguridad/flow-owner-base.json:28-50` | V (definición) / P (contenido del historial) |
| R35 | Cumplimiento de la plataforma | Power Platform está en el AoC PCI DSS de Microsoft, pero los requisitos sobre los datos de tarjeta propios son responsabilidad del cliente | [offering-pci-dss](https://learn.microsoft.com/azure/compliance/offerings/offering-pci-dss) | V |

---

## 2. Diseño definitivo propuesto

### 2.1 Decisión de arquitectura: bóveda con seguridad de fila (en lugar de seguridad de columna)

| | **A. Tabla bóveda `cuentapan` (recomendada)** | B. Columna con seguridad en cuenta (lo acordado) |
|---|---|---|
| Offline del cobrador | Usa seguridad de fila, que el offline respeta (R6). La prueba de la Fase 1 lo demuestra | Depende de un comportamiento **sin soporte** (R5) y sin documentar (R7). Puede que el PAN no baje, y entonces el cobrador no lo ve sin señal, o puede que baje sin control |
| Solicitante y Supervisor | Sin privilegio sobre la tabla: no la ven en apps, Web API, Excel ni Advanced Find | Ven la columna vacía. Equivalente en los canales online |
| Clave alternativa y upsert | La bóveda tiene una clave sobre el HMAC. La columna `cp_pan` no necesita seguridad de columna. Ver también D2b | La clave nunca puede estar en el PAN (R3). Igual hace falta el HMAC |
| Auditoría | `cp_pan` con auditoría de columna apagada. Altas, bajas y shares quedan auditados | Igual, más la incertidumbre de R16 |
| Administración | No hay perfiles de seguridad de columna. Viaja con la tabla y los roles | Un perfil asignado a 3 equipos |
| Costo | Una tabla nueva, una relación y un `LookUp` en "Ver número" | Una columna. Las pantallas cambian igual |
| Alcance PCI | Toda la información de tarjeta queda en **una tabla delimitada**, con lo que el alcance de la auditoría es más claro | Queda mezclada con los saldos y datos de la cuenta |

Los administradores ven todo en los dos casos (R24). La recomendación es **A**. La alternativa B queda documentada en caso de que el dueño del producto prefiera no agregar una tabla, pero exige antes la prueba R7 en el dispositivo (paso 0.3).

### 2.2 Esquema

**Tabla nueva `sanic_cm_tbl_cuentapan`** ("CM | TBL | PAN de cuenta"; propiedad de usuario o equipo; prefijo `sanic_cm_cp_`)

| Columna | Tipo | Notas |
|---|---|---|
| `sanic_cm_cp_name` (nombre principal) | Texto 20 | Enmascarado `****1234`. Es inofensivo si se filtra en un lookup |
| `sanic_cm_cp_pan` | Texto 19 | **PAN completo** (13 a 19 dígitos). Sin seguridad de columna. **IsAuditEnabled=false** en la columna. IsValidForAdvancedFind=false, IsSearchable=false |
| `sanic_cm_cp_clavepan` | Texto 64 | HMAC-SHA256 en hex, minúsculas. **Clave `sanic_cm_ak_cp_clavepan`** |
| `sanic_cm_cp_cuentaid` | Lookup → cuenta | Relación **Parental** (Share, Unshare, Assign y Delete en cascada). **Clave `sanic_cm_ak_cp_cuentaid`**, que garantiza 1:1 |

Tabla: auditoría encendida para Create, Delete y Share. Auditoría de lectura (`IsRetrieveAuditEnabled` / `IsRetrieveMultipleAuditEnabled`) encendida **solo** en esta tabla; la de la organización ya está activa (`isreadauditenabled=true`). Queda **fuera** del índice de Dataverse search y fuera del sitemap del MDA.

**Cambios en tablas existentes**

| Tabla | Columna o clave | Cambio |
|---|---|---|
| cuenta | `sanic_cm_ct_numerodecuenta` (nombre principal) | Se queda, porque el nombre principal no se puede cambiar de columna. Pasa a guardar **`****1234`**. Se renombra a "Número (enmascarado)" y su auditoría de columna se apaga durante la migración |
| cuenta | **nueva** `sanic_cm_ct_clavepan` (texto 64) | HMAC. **Clave `sanic_cm_ak_ct_clavepan`** que reemplaza a `sanic_cm_pk_ct_numerodecuenta`, que se borra |
| cuenta | **nueva** `sanic_cm_ct_ultimos4` (texto 4) | Para buscar (Solicitante, Quick Find) |
| asignación | **nueva** `sanic_cm_as_clavepan` (texto 64) | **Clave `sanic_cm_ak_as_clavepan_tipo` = (clavepan, tipo)**, que reemplaza a `sanic_cm_altkey_NumeroCuentaxTipo`. Conserva la regla de DEC-016 / DAT-14: una de cada tipo por cuenta |
| asignación | `sanic_cm_as_numerocuenta` | Se vacía y **se elimina** |
| carga diaria | `sanic_cm_cd_numerocuenta` (nombre principal) | Pasa a `****1234` |
| carga diaria | **nueva** `sanic_cm_cd_clavepan` | **Clave = (cd_clavepan, cd_fechacarga)**, que reemplaza a `sanic_cm_pk_cd_numerocuenta_fechacarga`. `cd_cuenta` pasa a mapearse explícitamente |
| interacción | `sanic_cm_in_numerocuenta` | Se vacía y se elimina. La unión pasa a `in_cuentaid`, que ya existe. **Requisito previo: F-2** |
| pago | **nuevo lookup** `sanic_cm_pg_cuentaid` | Hace falta porque offline solo se admite un nivel de lookup (R12) |
| pago | `sanic_cm_pg_numerocuenta` | Se vacía y se elimina |
| actualizaciondatos | — | No tiene PAN. Sin cambios |

¿Por qué el nombre principal enmascarado arregla tanto? **Todos los lookups a cuenta muestran el nombre principal.** Hoy, las vistas "Especiales · Mis solicitudes" (Solicitante), "Aprobaciones" (Supervisor) y "Por transcribir" le muestran el PAN a todo el que ve una asignación o interacción. Al enmascarar el nombre principal se corrige en todas a la vez.

### 2.3 Seguridad: roles y equipos

Las tablas existentes quedan como en la Fase 1. Esto es lo nuevo:

| Rol (equipo DEV) | `cuentapan` | Comentario |
|---|---|---|
| SR \| CM \| Localizador (CM · Localizadores · DEV) | **Read Basic** | Sin privilegios propios: la ve solo por el share en cascada desde la cuenta que comparte el flow |
| SR \| CM \| BackOffice (CM · Backoffice · DEV) | **Read Global** (o BU, ver D6) | Necesita el PAN para grabar en el core |
| SR \| CM \| Admin (CM · Admin y Carga · DEV) | **Read Global** | Sin Write. La bóveda es inmutable fuera de la carga |
| SR \| CM \| Solicitante / Supervisor | **Ninguno** | — |
| SR \| CM \| Servicio - Cobranza | **Share** (sin Read) si pasa a ser la identidad del flow (S-7) | Hoy el flow corre como System Administrator. P: verificar si el share en cascada exige el privilegio Share sobre la tabla hija |
| Smelter | — | Corre como SYSTEM (R28) |

Quitar `prvExportToExcel` a los roles CM que no lo necesitan (S-4) sigue pendiente y es independiente de esta fase.

### 2.4 Clave sustituta (HMAC)

- **Definición canónica** (tiene que dar el mismo resultado byte a byte en C# y en Python):
  `clave = lowercase(hex(HMAC_SHA256(K, UTF8(canon(PAN)))))`, con `canon` = quitar espacios y guiones y aplicar trim.
  - Se acuerda un **vector de prueba** con una clave de prueba, nunca con la real.
- **Por qué con clave y no un SHA-256 simple:** con un BIN conocido, un PAN de 13 dígitos tiene unas 10⁹ combinaciones, que se recorren por fuerza bruta en segundos. PCI DSS 4.0 req. 3.5.1.1 exige **hash con clave** de todo el PAN.
  - La clave también impide correlacionar el HMAC con el `****1234`. El estándar exige ese control cuando conviven la versión hasheada y la truncada.
- **¿Por qué no un GUID ni un autonumérico?** La carga trae solo el PAN y tiene que encontrar la cuenta existente. Hace falta una función **determinística** del PAN, y ni el GUID ni el autonumérico lo son. Un token aleatorio exigiría buscar en la bóveda antes del upsert, algo que Smelter no hace (R25 y R26).
- **Dónde vive la clave K:** ver D3. Opciones verificadas: secure configuration del paso (R32a) o managed identity → Key Vault (R32b). **No** puede ir en una variable de entorno de tipo Secret si la lee un plugin (R31).
- **Rotación:** rotar K obliga a recalcular todas las claves (`ct_`, `cp_`, `as_`, `cd_`). Hay que preparar un script de re-clave desde la bóveda, que tiene el PAN.

### 2.5 Cómo se resuelve el upsert de la carga diaria

La carga trae solo `NumeroCuenta`. Hoy la plantilla hace:
- **cuenta:** dedup `ct_numerodecuenta`.
- **asignación:** dedup `[as_numerocuenta, as_tipo]` + lookup `as_cuentaid` por `ct_numerodecuenta`.
- **carga diaria:** dedup `[cd_numerocuenta, cd_fechacarga]`.

Como Smelter solo hace upsert y lookups por clave alternativa (R25 y R26), **el valor de la clave tiene que poder calcularse en la propia fila**. Hay tres caminos (decisión **D1**):

| Opción | Cómo | A favor | En contra |
|---|---|---|---|
| **P1 · Función `HMAC()` en Smelter (recomendada)** | Smelter agrega una transformación genérica `HMAC(col, "cm-pan")` y lee la clave en su paso (R32). La plantilla mapea: `HMAC(NumeroCuenta)` → `ct_clavepan`, `cp_clavepan`, `as_clavepan`, `cd_clavepan`, y lookups por `ct_clavepan`; `NumeroCuenta` → `cp_pan`; `CONCAT("****", RIGHT(NumeroCuenta,4))` → `ct_numerodecuenta`, `cp_name`, `cd_numerocuenta`; `RIGHT(NumeroCuenta,4)` → `ct_ultimos4` | Cambio chico y reutilizable: MPPP y CashHub también tienen números de cuenta. El upsert sigue siendo de un solo viaje e idempotente | **Hay que tocar Smelter**, que es otra solución. Requiere el acuerdo de su dueño |
| P2 · El extracto del banco trae el token | El archivo agrega una columna `ClaveCuenta` calculada en el origen con K. La plantilla solo cambia el mapeo | Es lo mejor para PCI: K nunca entra a Power Platform. Smelter no se toca | Cambia la decisión "la carga trae solo el PAN" y depende de TI del banco |
| P3 · Tabla de aterrizaje + plugin de CM | Smelter **crea** (sin dedup) filas en `sanic_cm_tbl_cargapan` con el PAN y los datos de la cuenta. Un plugin de CM (síncrono, PostOperation Create) calcula el HMAC, hace los upserts de cuenta, bóveda, asignación y carga diaria, y borra el PAN de la fila de aterrizaje | Smelter no se toca | Se reescribe en C# la lógica de mapeo de 3 tablas, que hoy es configuración. La idempotencia queda en el plugin. Más esfuerzo y más superficie de error |

Queda descartado, y verificado que no funciona:
- Clave sobre la columna protegida (R3).
- Plugin en `Upsert` que reescriba la clave (R29).
- Plugin PreCreate que "redirija" a un Update (R30).
- Transformación sin clave tipo `REPLACE` o `MAP`: es reversible y equivale al PAN.

Con P1 o P2, el orden de proceso de la plantilla queda así:
1. Cliente, Producto, Agente, Fiador, Dirección y Teléfono, sin cambios.
2. **Cuenta**: dedup `ct_clavepan`.
3. **Cuenta PAN**: dedup `cp_clavepan`, lookup `cp_cuentaid` por `ct_clavepan`.
4. **Asignación**: dedup `[as_clavepan, as_tipo]`, lookup `as_cuentaid` por `ct_clavepan`.
5. **Carga diaria**: dedup `[cd_clavepan, cd_fechacarga]`, más el lookup `cd_cuenta` por `ct_clavepan`.

### 2.6 Lógica de servidor de CM (plugin nuevo, **sin la clave K**)

Assembly `Sanic.CobranzaMovil.Plugins`, pasos síncronos en PreOperation:

| Paso | Qué hace |
|---|---|
| `PanGuard` en Create y Update de cuenta, carga diaria, bóveda, asignación, interacción y pago (filtrado por las columnas de texto) | **Rechaza** cualquier valor con 5 o más dígitos seguidos en `ct_numerodecuenta`, `cd_numerocuenta` y `cp_name`, y cualquier valor en las columnas denormalizadas viejas mientras existan. Es la red de seguridad por si una plantilla mal configurada vuelve a escribir el PAN en claro |
| `ClaveDesdeCuenta` en Create y Update de asignación (filtrado `as_cuentaid`) | Copia `as_clavepan` desde `as_cuentaid.ct_clavepan`, o rechaza si no coincide. Así el backoffice ("Nueva solicitud") no tiene que escribir la clave desde el cliente (S-9) |
| `CuentaDesdeInteraccion` en Create de pago | Completa `pg_cuentaid` desde `pg_interaccion.in_cuentaid` si viene vacío |

Los upserts de Smelter disparan estos pasos porque internamente llaman a Create o Update (R30).

### 2.7 Enmascarado

- **Lo calcula la plantilla** (`CONCAT` + `RIGHT`, R27) y el `PanGuard` lo valida.
- Se descartaron:
  - la formula column, que no muestra valores offline (R10);
  - la calculated column, que no tiene `RIGHT` para longitudes variables y lee sin aplicar la seguridad (R11);
  - las masking rules, que están en preview y no sirven ni en canvas ni offline (R23).
- Formato en pantalla: `•••• 1234`. Nunca se muestran los primeros 6 dígitos: PCI permite BIN + últimos 4, pero no hace falta.

### 2.8 "Ver número" en la app del cobrador

- Por defecto se muestra `'[AS] Cuenta'.'[CT] Número (enmascarado)'`.
- El botón "Ver número" hace `Set(locPanVisible, LookUp('CM | TBL | PAN de cuenta', '[CP] Cuenta'.'CM | TBL | Cuenta' = varCuenta.'CM | TBL | Cuenta').'[CP] PAN')`. Usa un solo nivel de lookup, que funciona offline (R12).
  - Se muestra en una variable **de contexto**, con un temporizador que lo oculta a los 30 s.
  - **No** se guarda en colecciones, `SaveData` ni variables globales.
- Perfil offline: se agrega `cuentapan` con "**All records**". Con lectura Basic + share solo baja la cartera; es el mismo mecanismo que se probó en la Fase 1 (R6).
- La lectura offline **no** queda auditada en el servidor. Opcional: registrar un evento "PAN visto" (fecha, usuario, cuenta) en una tabla que se sincroniza.

---

## 3. Inventario de impacto

### 3.1 Dataverse (solutions/unpacked/Entities)

| Componente | Cambio |
|---|---|
| `sanic_cm_tbl_cuenta/Entity.xml:1035` (`ct_numerodecuenta`) | Display "Número (enmascarado)", auditoría de columna apagada en la migración y encendida después |
| `sanic_cm_tbl_cuenta/Entity.xml:1749` clave `sanic_cm_pk_ct_numerodecuenta` | Borrar (paso 8). Crear `sanic_cm_ak_ct_clavepan` (paso 1) |
| `sanic_cm_tbl_asignacion/Entity.xml:899, :1445` | Nueva `as_clavepan` + clave `(as_clavepan, as_tipo)`. Borrar `altkey_NumeroCuentaxTipo` y la columna `as_numerocuenta` |
| `sanic_cm_tbl_cargadiaria/Entity.xml:767, :1365` | Nueva `cd_clavepan` + clave `(cd_clavepan, cd_fechacarga)`. Borrar la clave vieja |
| `sanic_cm_tbl_interaccion/Entity.xml:1046` | Borrar `in_numerocuenta` |
| `sanic_cm_tbl_pago/Entity.xml:686` | Nuevo lookup `pg_cuentaid`. Borrar `pg_numerocuenta` |
| Quick Find de cuenta `{b9e60746…}` y de carga diaria `{82f29b03…}` | Campos de búsqueda: `ct_ultimos4`, nombre del cliente y CIF. **Quitar** el número |
| Vistas de cuenta (`{2cd4888e}`, `{bb20f3d7}`, `{aa34b0bf}`, `{a756543b}`, `{cf3758a2}`) y de carga diaria (`{6480e01d}`, `{9ce3251b}`, `{eb0e59e2}`, `{29a17095}`, `{e81b971e}`, `{548679ba}`) | Siguen mostrando el nombre principal, que ya va enmascarado. Agregar `ultimos4` donde sirva |
| Vistas de pago `{79e571ab}` y de asignación `{4a8679ba}` | Quitar la columna denormalizada |
| Vistas que muestran el lookup a cuenta (`{7e3336ce}`, `{518679ba}`, `{4e8679ba}`, `{b7044010}`, `{d51e05e1}`, `{381db7c4}`, `{a8cae391}`) | Sin cambios: quedan enmascaradas por el nombre principal |
| Formularios: cuenta `{3cd8fdcc}`, `{8e6a2edd}`, `{361ece40}`; carga diaria `{c1f57c30}`, `{a5be34eb}`, `{a98be8b4}`; interacción `{f03f4c77}`; pago `{c95a0d82}`; asignación `{7b16391e}`, `{813336ce}`, `{de9f1851}` | Quitar las denormalizadas. **No** se agrega la bóveda al MDA |
| Perfil offline "App - Cobranza Movil Profile" | Agregar `cuentapan` (All records) y `pg_cuentaid`. **Confirmar** cuál perfil está vinculado a la app: también existe `autogenProfile-…` (S-1) |
| Roles | Ver 2.3 |
| Auditoría | Bóveda: tabla sí, `cp_pan` no, lecturas sí |
| Flow "Cambio de Owner en Asignaciones" (`scripts/seguridad/flow-owner-base.json`) | **Sin cambio funcional**: el share de cuenta llega a la bóveda en cascada. Agregar `runtimeConfiguration.secureData` (entradas y salidas) al disparador y a las acciones (R34) |
| `scripts/seguridad/paso3_reconciliar.py` | Tiene que volver a compartir las cuentas **que ya estaban compartidas**: la cascada solo actúa en el momento del share, no hacia atrás (P). Verificar que el `GrantAccess` repetido se propague en cascada |
| `scripts/seguridad/paso2_prueba.py:24,49,54` | Sembrar con `cp_pan` y `ct_clavepan`, no con `ct_numerodecuenta` |

### 3.2 Plantilla Smelter "Cobranza movil" (datos en `sanic_tbl_smlt_*`, se cambia más adelante)

| Mapeo | Hoy | Después |
|---|---|---|
| TM Cuenta | dedup `ct_numerodecuenta`; `NumeroCuenta→ct_numerodecuenta` | dedup `ct_clavepan`; `HMAC(NumeroCuenta)→ct_clavepan`; `CONCAT("****",RIGHT(NumeroCuenta,4))→ct_numerodecuenta`; `RIGHT(NumeroCuenta,4)→ct_ultimos4` |
| **TM nuevo** Cuenta PAN | — | dedup `cp_clavepan`; `NumeroCuenta→cp_pan`; enmascarado→`cp_name`; lookup `cp_cuentaid` por `ct_clavepan` = `HMAC(NumeroCuenta)` |
| TM Asignación | dedup `[as_numerocuenta, as_tipo]`; `NumeroCuenta→as_numerocuenta`; `as_cuentaid` por `ct_numerodecuenta` | dedup `[as_clavepan, as_tipo]`; `HMAC(NumeroCuenta)→as_clavepan`; `as_cuentaid` por `ct_clavepan` |
| TM Carga diaria | dedup `[cd_numerocuenta, cd_fechacarga]`; `NumeroCuenta→cd_numerocuenta`; **sin** `cd_cuenta` | dedup `[cd_clavepan, cd_fechacarga]`; enmascarado→`cd_numerocuenta`; `HMAC→cd_clavepan`; lookup `cd_cuenta` por `ct_clavepan` |
| Job (`ij_sourcefile`) | El CSV queda guardado | Borrar el archivo después de un job "Completed" (D7) |

Documentación a actualizar: `docs/referencia/cobranza-import-config.md` (17 usos) y `docs/guias/02-import-configuracion.md`.

### 3.3 App del cobrador (`apps/localizador-canvas/src`)

| Archivo:línea | Hoy | Cambio |
|---|---|---|
| `App.pa.yaml:83` | `ClearCollect(colCuentas, 'CM \| TBL \| Cuentas')` | Sin cambio: la cuenta ya no tiene el PAN. **No** se agrega la bóveda a ninguna colección |
| `scrCartera.pa.yaml:17, :143` | `UpdateIf(Asignacions, '[AS] NumeroCuenta' = …, {Orden})` | Filtrar por GUID de asignación (también es APP-13) |
| `scrCartera.pa.yaml:27, :143` | `LookUp(colCuentas, '[CT] Numero de cuenta' = '[AS] NumeroCuenta')` | `'[AS] Cuenta'` directo |
| `scrCartera.pa.yaml:556, :563, :569, :877` | buscador `varBuscar in '[AS] NumeroCuenta'` | `in '[AS] Cuenta'.'[CT] Últimos 4'` o el nombre del cliente |
| `scrCartera.pa.yaml:564, :878` | orden `sanic_cm_as_numerocuenta` | Ordenar por cliente u `Orden` |
| `scrInteraccion.pa.yaml:28-29` | `'[AS] NumeroCuenta' = varCuenta.'[CT] Numero de cuenta'` | Comparar el lookup con el registro |
| `scrInteraccion.pa.yaml:65` | Subtítulo con el PAN | Enmascarado + **"Ver número"** (2.8) |
| `scrInteraccion.pa.yaml:350` | Número de cada producto | Enmascarado |
| `scrInteraccion.pa.yaml:374-375` | `UpdateIf(colProdInt, '[AS] NumeroCuenta' = tn, …)` | Por id |
| `scrInteraccion.pa.yaml:1081, :1111` | Patch escribe `'[IN] Numerocuenta'` | Quitarlo. `'[IN] Cuenta'` ya se escribe (:1074, :1104) |
| `scrInteraccion.pa.yaml:1088` | Patch de pago escribe `'[PG] Numero Cuenta'` | Escribir `'[PG] Cuenta': varCuenta` |
| `scrDetalleCuenta.pa.yaml:46` | Subtítulo con el PAN | Enmascarado + **"Ver número"** |
| `scrDetalleCuenta.pa.yaml:143, :646` | `Filter(Interaccions, '[IN] Numerocuenta' = …)` | `'[IN] Cuenta'.'CM \| TBL \| Cuenta' = varCuenta.'CM \| TBL \| Cuenta'` |
| `scrDetalleCuenta.pa.yaml:716` | "Tipo · PAN" | Enmascarado |
| `scrCliente.pa.yaml:12, :128, :386` | Unión por texto | Por lookup |
| `scrCliente.pa.yaml:457, :604` | Muestra el PAN | Enmascarado |
| `scrInicio.pa.yaml:358` | `Distinct(colIntHoy, '[IN] Numerocuenta')` | `Distinct` por `'[IN] Cuenta'` (id) |
| `scrInicio.pa.yaml:406, :512, :514, :659, :663, :661, :671` | Unión y navegación por texto | Por registro o lookup |
| `scrInicio.pa.yaml:735` | `cliNom · cta` | Enmascarado |
| `scrPagos.pa.yaml:261-263` | Unión por `'[PG] Numero Cuenta'` | `'[PG] Cuenta'` (nuevo, un nivel) |
| `scrPagos.pa.yaml:306` | `prod · cta` | Enmascarado |
| `scrSincronizacion.pa.yaml:366` | Unión por `[IN] Numerocuenta` | Por lookup |
| Fuente de datos nueva | — | `'CM \| TBL \| PAN de cuenta'` |

### 3.4 Backoffice (`apps/backoffice-canvas/Src`)

| Archivo:línea | Rol | Cambio |
|---|---|---|
| `scrNuevaSolicitud.pa.yaml:125` | Solicitante | `StartsWith('[CT] Numero de cuenta', …)` pasa a `'[CT] Últimos 4' = txt` (delegable) más filtro por cliente o CIF (D5) |
| `scrNuevaSolicitud.pa.yaml:153, :230, :281` | Solicitante | Mostrar el nombre principal (enmascarado) |
| `scrNuevaSolicitud.pa.yaml:576-584` | Solicitante | Validar "Especial activa" por `'[AS] Cuenta' = varCtaNva` |
| `scrNuevaSolicitud.pa.yaml:615` | Solicitante | **Dejar de escribir** `'[AS] NumeroCuenta'`. `as_clavepan` lo pone el plugin `ClaveDesdeCuenta` |
| `scrMisSolicitudes.pa.yaml:183, :470` | Solicitante | `'[AS] Cuenta'.<nombre principal>` (enmascarado) |
| `scrAprobaciones.pa.yaml:143, :397` | Supervisor | Igual |
| `scrCierres.pa.yaml:229, :463`, `scrReconciliacion.pa.yaml:259` | Admin | Enmascarado, o PAN desde la bóveda según D6 |
| `scrTranscripcion.pa.yaml:482, :692` | BackOffice | `LookUp('CM \| TBL \| PAN de cuenta', '[CP] Cuenta' = ThisItem.'[IN] Cuenta').'[CP] PAN'`. Mantener o no `Copy(Self.Text)`: D8 |
| `scrTranscritas.pa.yaml:222, :540` | BackOffice | Igual |
| `scrMaestros.pa.yaml:582` | Varios | Enmascarado |

### 3.5 Pruebas y datos

| Archivo | Cambio |
|---|---|
| `tests/backoffice-canvas/lib/datosqa.js:54, :68, :79, :94, :108, :123-134` | Sembrar y limpiar por `ct_clavepan` / bóveda / lookups (el prefijo QA va en `cp_pan`) |
| `tests/backoffice-canvas/escenarios/B-solicitante.js:17, :45, :81-82, :151, :168` | Filtro por `_sanic_cm_as_cuentaid_value`. La request esperada pasa a `sanic_cm_ct_ultimos4 eq …` |
| `escenarios/G-reapertura.js:14, :28`, `C-supervisor.js:95` | Por id o clave |
| `run.js:97` | No imprimir `cp_pan` |
| `test-data/generar-mock.py:117` | Con P2: agregar la columna `ClaveCuenta`. Con P1 o P3: sin cambios |
| **Prueba nueva** `escenarios/H-pan.js` | Solicitante y Supervisor: `GET sanic_cm_tbl_cuentapans` → vacío o 403. BackOffice: lee. Ningún nombre principal con 5 o más dígitos |

### 3.6 Documentación

Actualizar: `docs/referencia/cobranza-import-config.md`, `03-catalogo-de-tablas.md`, guías del backoffice 03-07, 09, 10 y 14, `05-guia-paso-a-paso-localizador.md`, `registro-de-decisiones.md` (DEC-016 y la clave de la carga diaria), `localizador-seguridad.md` (SEC-05 y SEC-15) y `contrato-de-datos.md:271-273`, que proponía resolver `in_cuentaid` por `in_numerocuenta` y queda obsoleto.

---

## 4. Plan de migración en DEV

Patrón **expandir → migrar → contraer**. Hasta el paso 8 todo es aditivo y reversible. Los scripts propuestos van en `scripts/pan/`, con la misma convención que la Fase 1: muestran el plan y solo aplican con `--aplicar`. **Antes del paso 8: backup manual del entorno** (PPAC).

| Paso | Qué | Cómo | Verificación | Rollback |
|---|---|---|---|---|
| 0.1 | Decisiones D1-D3 cerradas | — | Acta en `00-lista-consolidada.md` | — |
| 0.2 | **F-2 resuelto**: toda interacción nueva con `in_cuentaid` | Corrección de la app (Fase 5) + `ApplicationRequired` exigido por plugin | `interaccions?$filter=_sanic_cm_in_cuentaid_value eq null` → 0 nuevas | — |
| 0.3 | *(Solo si se elige la alternativa B)* Prueba R7 | Columna de prueba con seguridad de columna, perfil para Localizadores, sincronización limpia en User5 e inspección de la SQLite | ¿Bajó con valor? ¿Bajó vacía? ¿Baja para un usuario sin perfil? | Borrar la columna de prueba |
| 1 | **Esquema (expandir)** | `paso1_esquema.py`: tabla `cuentapan`, columnas `ct_clavepan`, `ct_ultimos4`, `as_clavepan`, `cd_clavepan`, `pg_cuentaid`, relación Parental con cascada de Share, claves **nuevas**; auditoría de tabla y lecturas; `cp_pan` IsAuditEnabled=false | `EntityDefinitions(...)/Keys` → `EntityKeyIndexStatus=Active` en las 5 claves nuevas. `Attributes(LogicalName='sanic_cm_cp_pan')` → IsAuditEnabled=false, IsSecured=false. Relación con `CascadeConfiguration.Share=Cascade` | Borrar los componentes nuevos (sin datos) |
| 2 | **Apagar la auditoría de columna** de `ct_numerodecuenta`, `cd_numerocuenta`, `as_numerocuenta`, `in_numerocuenta` y `pg_numerocuenta` | `paso2_auditoria.py`. Hace falta para que el enmascarado del paso 8 no deje el PAN como `oldValue` (R19) | `IsAuditEnabled=false` en las 5 | Volver a encenderla |
| 3 | **Backfill** | `paso3_backfill.py`, que lee K de un archivo fuera del repo o de Key Vault y nunca lo imprime. Por cada cuenta: upsert de la bóveda (`cp_pan` = valor actual, `cp_clavepan`, `cp_name`, `cp_cuentaid`); `ct_clavepan` y `ct_ultimos4`. Asignación: `as_clavepan` desde el lookup (incluye las 3 que no tienen texto). Carga diaria: `cd_clavepan` desde `cd_cuenta`. Pago: `pg_cuentaid` desde `pg_interaccion` o por coincidencia con la cuenta. Idempotente (upsert por clave) | Bóveda = **1.008** = cuentas; `ct_clavepan` no nulo 1.008/1.008; `as_clavepan` 1.009/1.009; `cd_clavepan` 1.000/1.000; `pg_cuentaid` 3/3 o justificado. Para cada cuenta, `HMAC(cp_pan) = ct_clavepan`, calculado en el script e informado solo como conteo. Los 6 registros QA alfanuméricos y el de 8 dígitos se informan aparte | Vaciar las columnas nuevas y borrar las filas de la bóveda |
| 4 | **Roles y compartidos** | `paso4_roles.py` (privilegios de 2.3). `paso3_reconciliar.py --aplicar`, adaptado para volver a hacer `GrantAccess` de las cuentas compartidas y que llegue en cascada a la bóveda | Cada cuenta compartida con su cobrador tiene su fila de bóveda compartida con el mismo usuario (`RetrieveSharedPrincipalsAndAccess` por `objectid`; recordar que filtrar POA por `principalid` falla). Como Solicitante: `GET sanic_cm_tbl_cuentapans?$top=1` → 0 filas o 403 | Quitar privilegios (`--revertir`) |
| 5 | **Plugin de CM** (`PanGuard`, `ClaveDesdeCuenta`, `CuentaDesdeInteraccion`) | Registrar en la solución con pasos PreOperation síncronos | Asignación sin `as_clavepan` → la completa. **La regla de PanGuard que rechaza el PAN en el nombre principal se registra desactivada y se enciende en el paso 8**: si se encendiera antes, rompería la carga actual, que todavía escribe el PAN (ver paso 6). Prueba negativa en el paso 8: crear una cuenta con 13 dígitos en el nombre principal → error | Desactivar los pasos |
| 6 | **Plantilla Smelter** (según D1) | P1: versión de Smelter con `HMAC()` + plantilla nueva: dedup y lookups por `*_clavepan`, tabla de la bóveda y `ct_ultimos4`. **La plantilla todavía NO enmascara**: mientras vivan las claves únicas viejas, `****1234` choca (81 de 1.008 cuentas, en 40 grupos, comparten los últimos 4 dígitos; verificado el 2026-10-04). `ct_numerodecuenta`, `cd_numerocuenta` y `as_numerocuenta` siguen con el PAN hasta el paso 8 | Importar `test-data/cobranza-mock-1000-dia2.csv`: **0 cuentas nuevas duplicadas** (cuentas antes = después + nuevas reales), 0 errores de clave, bóveda 1:1, `HMAC(cp_pan) = ct_clavepan` | Volver a la plantilla anterior (exportada con `exporttemplate` antes del cambio). Las claves viejas siguen vivas hasta el paso 8 |
| 7 | **Apps** | Cambios de 3.3 y 3.4. Publicar backoffice y app del cobrador. Agregar `cuentapan` al perfil offline | E2E de `tests/backoffice-canvas` adaptados + `H-pan.js`. Prueba en el teléfono (abajo) | Republicar la versión anterior |
| 8 | **Contraer** | `paso8_contraer.py`: (a) borrar las claves viejas, **y después** cambiar en la plantilla `ct_numerodecuenta`, `cd_numerocuenta` y `cp_name` a `CONCAT("****", RIGHT(TRIM(NumeroCuenta), 4))` y quitar `as_numerocuenta`; encender la regla de PanGuard (`pk_ct_numerodecuenta`, `altkey_NumeroCuentaxTipo`, `pk_cd_numerocuenta_fechacarga`); (b) `ct_numerodecuenta` y `cd_numerocuenta` → `****1234`; (c) vaciar `as_numerocuenta`, `in_numerocuenta` y `pg_numerocuenta`; (d) borrar esas 3 columnas, después de sacarlas de vistas, formularios y apps | Escaneo con regex `\d{5,}` sobre todas las columnas de texto de las 8 tablas CM **excepto `cp_pan`** → **0 coincidencias** | (b) y (c): restaurar desde la bóveda con un script; (a): volver a crear las claves; (d): solo con backup |
| 9 | **Purgar el PAN de la auditoría** (según D4) | `paso9_purga_auditoria.py`: auditorías de cuenta, carga diaria, asignación, interacción y pago cuyo `changedata` incluya las columnas del PAN → `DeleteRecordChangeHistory` por registro, o `BulkDelete` sobre `audit` (R18) | `audits?$filter=objecttypecode eq '…'` → 0 registros con esas columnas en `changedata` | **Irreversible** |
| 10 | **Purgar otros almacenamientos** | Archivos `ij_sourcefile` de los jobs de Smelter (D7); historial de corridas del flow de owner (o esperar a su vencimiento) y `secureData` en el flow | `sanic_tbl_smlt_importjobs?$select=sanic_smlt_ij_sourcefile_name` → todos null | Irreversible |
| 11 | Volver a encender la auditoría de `ct_numerodecuenta` y `cd_numerocuenta` (ya enmascaradas). `cp_pan` **nunca** | `paso2_auditoria.py --revertir` (sin `cp_pan`) | `IsAuditEnabled=true` | — |
| 12 | **Reiniciar teléfonos** | Los teléfonos que ya sincronizaron conservan `in_`/`pg_numerocuenta` y el nombre principal con el PAN en su SQLite (R9). Hay que borrar los datos de la app o hacer un borrado selectivo con Intune | SQLite de User5: regex `\d{13}` solo en la tabla de la bóveda | — |

### Prueba en el teléfono (criterio de aceptación de la fase)

Se hace con Android sobre Redroid (User5, Localizador) y con un teléfono físico.

1. **Sincronización limpia.** La SQLite tiene filas en `sanic_cm_tbl_cuentapan` = cuentas de la cartera (por ejemplo, 2) y ninguna fila de otra cartera.
2. **Fuera de la bóveda no hay PAN.** Un regex `\d{13}` sobre el resto de las tablas de la SQLite da 0 coincidencias.
3. **Modo avión.**
   - En Detalle de cuenta e Interacción se ve `•••• 1234`.
   - Al tocar "Ver número" aparece el PAN completo, que coincide con el de la carga (se compara por HMAC o por los últimos 4, nunca se anota).
   - El número se oculta solo a los 30 s.
4. **Reasignación.** Se reasigna la cuenta a otro cobrador y se hace una sincronización completa (`ShowHostInfo(HostInfo.OfflineSync)` → Refresh): la fila de la bóveda desaparece del teléfono anterior.
5. **Solicitante (User de "CM · Solicitantes").**
   - En Nueva solicitud, Mis solicitudes y en el MDA solo ve `****1234`.
   - `GET /sanic_cm_tbl_cuentapans` con su token → 0 filas o 403.
   - Exportar a Excel desde la vista "Especiales · Mis solicitudes" no trae ningún número completo.
6. **Supervisor.** Lo mismo que el Solicitante, en Aprobaciones.
7. **BackOffice.** En Transcripción ve el PAN completo. La auditoría de la bóveda registra la lectura.

---

## 5. Riesgos abiertos y decisiones del dueño del producto

### Decisiones

| # | Decisión | Opciones | Recomendación |
|---|---|---|---|
| **D1** | Quién calcula el HMAC en la carga | P1: función en Smelter · P2: token en el extracto del banco · P3: tabla de aterrizaje + plugin de CM | **P1** si el dueño de Smelter lo acepta. **P2** si TI del banco puede, porque es lo mejor para PCI. P3 solo si no hay otra salida |
| **D2** | Dónde vive el PAN | A: tabla bóveda (seguridad de fila) · B: columna con seguridad en cuenta | **A**. B depende de un comportamiento offline sin soporte (R5 y R7) |
| D2b | Seguridad de columna **además** en `cp_pan` (defensa en profundidad para los canales online) | Sí, después de la prueba 0.3 · No | No por ahora: suma la incertidumbre de R5 y no frena a los 15 administradores |
| **D3** | Custodia de la clave K | Secure configuration del paso de Smelter o CM · Managed identity → Key Vault · Clave en el banco (P2) | Key Vault + managed identity. Rotación anual con script de re-clave. Definir quién custodia la clave (separación de funciones, PCI 3.6 y 3.7) |
| **D4** | Purga de la auditoría con PAN (58+ registros y la carga diaria) | `DeleteRecordChangeHistory` (borra **todo**, incluidos los Share de la Fase 1) · `BulkDelete` selectivo · esperar a que venza (365 días) | `BulkDelete` selectivo de los Create y Update que tienen columnas del PAN. Esperar el vencimiento no es aceptable para PCI |
| D5 | Cómo identifica el Solicitante la cuenta en Nueva solicitud | Últimos 4 + cliente o CIF · nombre del cliente | Últimos 4 + CIF |
| D6 | Alcance del BackOffice y del Admin sobre la bóveda | Global · BU · Admin solo enmascarado | BackOffice en Global (graba en el core) con auditoría de lectura. Admin solo enmascarado en Cierres y Reconciliación |
| D7 | Retención del CSV en Smelter (`ij_sourcefile`) | Borrar al terminar el job · retener N días · no subirlo | Borrar al terminar con éxito. Es otro cambio en Smelter, o un job de CM |
| D8 | `Copy()` del PAN al portapapeles en Transcripción | Mantener · quitar | Mantener solo en BackOffice (puesto de escritorio). Que DLP o Intune lo cubran |
| D9 | Factura de papel con el PAN completo (PCI req. 9) | PAN completo · solo últimos 4 | Elevarlo a cumplimiento del banco (ya estaba planteado) |
| D10 | Validar el PAN en la carga (13 a 19 dígitos, Luhn) | Rechazar · registrar como advertencia | Rechazar en PROD. En DEV no: los mocks no pasan Luhn (solo 97 de 1.001) |

### Riesgos

| Riesgo | Detalle | Mitigación |
|---|---|---|
| **El PAN se guarda en claro en Dataverse** (PCI 3.5.1) | Dataverse no tiene cifrado por campo para texto. El cobrador necesita el PAN en claro offline, así que no se puede cifrar a nivel de aplicación: no hay forma de descifrar en el teléfono sin poner la clave ahí. Lo único que queda es el cifrado de la plataforma (TDE) más el AoC de Microsoft (R35) | **Que lo valide un QSA**. Acotar el alcance a la tabla bóveda |
| Caché offline sin cifrado de la app confirmado | R8 | Confirmar con Microsoft o con TI que la protección de app de Intune cifra la base de Power Apps. Mientras tanto: MDM con cifrado del dispositivo obligatorio |
| La revocación offline llega tarde | R9. El PAN de una cuenta reasignada queda en el teléfono hasta la próxima sincronización completa | Botón de sincronización completa (L-7) y borrado por inactividad de Intune |
| 15 System Administrators | Ven todo, en cualquier diseño (R24) | S-12: reducirlos y auditar sus lecturas |
| La cascada de Share no actúa hacia atrás | Los compartidos de la Fase 1 no llegan solos a la bóveda (P) | Paso 4: volver a hacer el `GrantAccess` y verificar la POA |
| Smelter y la plantilla escriben el PAN en un campo equivocado | Por error de configuración | `PanGuard` en el servidor + escaneo con regex en cada despliegue |
| Historial de corridas del flow con PAN | R34 | Quitar `as_numerocuenta` (paso 8) + `secureData` |
| Mensajes de error de Smelter con valores | `NormalizationEngine.cs:181` incluye el valor en el error de lookup (`ijl_errormessage`). Con la clave HMAC, el valor que aparece es el HMAC y no el PAN, pero un error de **Dataverse** en el upsert podría citar el valor de un campo | Revisar `sanic_tbl_smlt_importjoblogs` después de la importación de prueba (paso 6) |
| F-2 sin resolver | Si se borra `in_numerocuenta` antes, las interacciones sin `in_cuentaid` quedan huérfanas | Paso 0.2 bloqueante |
| Perfil offline vinculado | Hay dos perfiles (el diseñado y `autogenProfile-…`) | Confirmar cuál usa la app antes del paso 7 |
| `RetrieveTotalRecordCount` desactualizado | Devuelve 0 en DEV | Contar paginando, como en este relevamiento |
