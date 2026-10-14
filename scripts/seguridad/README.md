# Seguridad · Fase 1: el cobrador solo lee su cartera

Scripts que aplicaron la Fase 1 de [docs/auditoria/00-lista-consolidada.md](../../docs/auditoria/00-lista-consolidada.md) (S-1, S-2, S-8) en DEV. Usan el Service Principal de `pp_secrets.env` (no versionado). Para otra ruta, definir `PP_SECRETS`.

Todos muestran el plan sin cambiar nada; aplican solo con `--aplicar`.

| Paso | Script | Qué hace |
|---|---|---|
| 1 | `paso1_cascadas_auditoria.py` | Pone **Share/Unshare = NoCascade** en cliente→cuenta, cliente→carga diaria y cuenta→asignación. Así, compartir un cliente no comparte todas sus tarjetas, y compartir una cuenta no comparte las asignaciones de otros cobradores. Cliente→dirección y cliente→teléfono siguen en cascada (es lo que se busca). Enciende la **auditoría** en asignación, interacción, pago, agente, actualización de datos y carga diaria. Publica solo esas tablas |
| 2 | `paso2_flow.py` | **Retirado el 2026-10-13.** Definía el flow "Cambio de Owner en Asignaciones", que quedó **apagado** y lo reemplaza el plugin asíncrono `RepartoAsignacion` ([README de plugins](../../plugins/Sanic.CobranzaMovil.Plugins/README.md)), con la misma regla: asignación activa con agente con usuario → owner + compartido de la Especial con el solicitante + **GrantAccess Read** de cuenta, cliente y fiador al cobrador + revoca al dueño anterior; al cerrar, revoca al dueño; solo revoca si el usuario no tiene **otra** asignación activa que justifique ese registro. El flow fallaba en ~40% de las corridas y, cuando fallaba un compartido, se salteaba la revocación. El script ahora se niega a aplicar salvo con `--reactivar-flow-retirado` (rollback: antes desactivar los pasos de `RepartoAsignacion`) |
| — | `paso2_prueba.py` | Prueba E2E (T1–T4) con datos `QA-E2E-F1` que se borran al final |
| 3 | `paso3_reconciliar.py` | **Reconciliación**: asegura los compartidos de todas las asignaciones activas cuyo owner es el usuario de su agente. Es idempotente |
| 4 | `paso4_rol_basic.py` | El rol `SR \| CM \| Localizador` pasa de lectura **Global** a **Basic** en cuenta, cliente, dirección, teléfono y fiador. `--revertir` lo devuelve a Global |
| 5 | `paso5_cartera_sin_cobrador.py --entorno DEV` | Equipo dueño **"CM · Cartera sin cobrador · DEV"** (unidad raíz, **sin miembros**) y su rol `SR \| CM \| Cartera sin cobrador` (solo Read Basic de la asignación, más los 4 de SharePoint que Dataverse le pone a todo rol; se agrega a la solución). `RepartoAsignacion` le pasa las asignaciones activas cuyo agente no tiene usuario (DAT-07) y `RepartoPorAgente` las mueve al vincular o quitar el usuario (DAT-08). Termina con error si el equipo tiene miembros u otros roles. **Antes de registrar los plugins 1.0.5.0 en un entorno nuevo** |
| 6 | `paso6_cobrador_solo_crea.py` | **S-9.** El rol `SR \| CM \| Localizador` pierde Write (Basic) en interacción, pago y actualización de datos, y Assign (Basic) en pago: **crea pero no edita ni reasigna** lo capturado. La app del cobrador solo crea (`Patch` con `Defaults`). `--revertir` devuelve los 4 privilegios |
| 7 | `paso7_sin_flows.py` | **S-4 (parte de CM).** Localizador, Solicitante, Supervisor y BackOffice pierden los privilegios de crear, editar, borrar, asignar, compartir, vincular y activar procesos, y `prvFlow`; conservan leer y ejecutar. Las apps no llaman a flows. **No alcanza solo:** `Basic User` y roles de otras soluciones siguen dando crear flows y exportar a Excel (ver S-4). `--revertir` devuelve los 36 privilegios |

## Orden obligatorio

1 → 2 → 3 → 4. Si se baja el rol (4) antes de compartir (2 y 3), los teléfonos se quedan sin cartera.

## Regla operativa: correr la reconciliación después de cada despliegue

El import de la solución apaga los pasos de plugin (y apagaba el flow) mientras dura, y `paso7_desplegar.py` los vuelve a su estado al terminar. **Los cambios de asignaciones que ocurren en ese intervalo no disparan el reparto y se pierden** (comprobado en DEV con el flow: el alta T1 se perdió). Por eso, **después de cada despliegue**:

```bash
python3 scripts/seguridad/paso3_reconciliar.py --aplicar
```

La reconciliación solo **agrega** los compartidos que falten. Los compartidos sobrantes de un cierre o reasignación ocurridos durante ese intervalo quedan sin revocar; eso queda pendiente para una reconciliación completa (Fase 4).

## Resultado verificado en el dispositivo (Android real con Power Apps, User5)

**Primera sincronización limpia** (con los datos de la app borrados): el teléfono bajó **solo su cartera**.

| Tabla | Antes de la Fase 1 | Después |
|---|---|---|
| Cuentas | 1.004 (todo el banco) | 2 |
| Clientes | 959 | 1 |
| Direcciones | 2.697 | 1 |
| Teléfonos | 1.059 | 1 |

Efecto colateral: las colecciones del cobrador ya no superan el límite de 500 filas, así que **las tarjetas salen completas** (nombre, CIF, dirección y saldos). F-1 queda resuelto para el rol Localizador.

### ⚠ Los teléfonos que ya bajaron el padrón lo conservan

La sincronización incremental solo baja registros que **cambiaron** ([doc](https://learn.microsoft.com/power-apps/mobile/offline-sync-icon#types-of-offline-syncs)). Quitar un permiso no cambia el registro, así que el teléfono no se entera. Hacen falta una **sincronización completa** ("Refresh" en Device status, que esta app todavía no expone: L-7) o un **reinicio** (cerrar sesión, reinstalar o borrado selectivo de Intune).

- **Antes del go-live:** si la Fase 1 entra antes de cargar datos reales, ningún teléfono llega a tener el padrón.
- **En DEV:** hay que reiniciar los teléfonos que ya tenían datos (User1, User6 y el Android del VPS, que ya está limpio).
- **Al reasignar o cerrar una asignación:** el teléfono del cobrador anterior conserva la fila hasta la próxima sincronización completa. Mitigación prevista: un botón de sincronización completa con `ShowHostInfo(HostInfo.OfflineSync)` y, en PROD, la política de Intune.

## Lecciones (verificadas)

- El conector "List rows" del flow **falló con FetchXML que tiene `link-entity`**, aunque la Web API lo aceptaba. Se reemplazó por `$filter` OData con navegación: `sanic_cm_as_cuentaid/_sanic_cm_ct_clienteid_value eq …`.
- Un FetchXML con un GUID vacío (`value=""`) falla: hay que verificar el nulo **antes** de consultar.
- En `principalobjectaccessset`, filtrar por `principalid` devuelve vacío. Hay que filtrar por `objectid` y comparar el principal del lado del cliente.
- Asignar como owner a un usuario sin lectura sobre la tabla falla: el cobrador tiene que tener el rol Localizador.
- `RemovePrivilegeRole` recibe `Privilege` (referencia a la entidad), no `PrivilegeId`.
- La tabla `flowruns` de Dataverse se actualiza con atraso: no sirve para verificar en tiempo real.
