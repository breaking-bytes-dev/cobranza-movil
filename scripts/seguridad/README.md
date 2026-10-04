# Seguridad · Fase 1: el cobrador solo lee su cartera

Scripts que aplicaron la Fase 1 de [docs/auditoria/00-lista-consolidada.md](../../docs/auditoria/00-lista-consolidada.md) (S-1, S-2, S-8) en DEV. Usan el Service Principal de `pp_secrets.env` (no versionado). Para otra ruta, definir `PP_SECRETS`.

Todos muestran el plan sin cambiar nada; aplican solo con `--aplicar`.

| Paso | Script | Qué hace |
|---|---|---|
| 1 | `paso1_cascadas_auditoria.py` | Pone **Share/Unshare = NoCascade** en cliente→cuenta, cliente→carga diaria y cuenta→asignación. Así, compartir un cliente no comparte todas sus tarjetas, y compartir una cuenta no comparte las asignaciones de otros cobradores. Cliente→dirección y cliente→teléfono siguen en cascada (es lo que se busca). Enciende la **auditoría** en asignación, interacción, pago, agente, actualización de datos y carga diaria. Publica solo esas tablas |
| 2 | `paso2_flow.py` | Redefine el flow "Cambio de Owner en Asignaciones" a partir de `flow-owner-base.json`. Con asignación activa y agente con usuario: owner + compartido de la Especial con el solicitante + **GrantAccess Read** de cuenta, cliente y fiador al cobrador + revoca al dueño anterior. Al cerrar la asignación: revoca al dueño. Solo revoca si el usuario no tiene **otra** asignación activa que justifique ese registro |
| — | `paso2_prueba.py` | Prueba E2E (T1–T4) con datos `QA-E2E-F1` que se borran al final |
| 3 | `paso3_reconciliar.py` | **Reconciliación**: asegura los compartidos de todas las asignaciones activas cuyo owner es el usuario de su agente. Es idempotente |
| 4 | `paso4_rol_basic.py` | El rol `SR \| CM \| Localizador` pasa de lectura **Global** a **Basic** en cuenta, cliente, dirección, teléfono y fiador. `--revertir` lo devuelve a Global |

## Orden obligatorio

1 → 2 → 3 → 4. Si se baja el rol (4) antes de compartir (2 y 3), los teléfonos se quedan sin cartera.

## Regla operativa: correr la reconciliación después de cada despliegue del flow

Al desactivar y reactivar el flow (por ejemplo, en un import de solución), Dataverse tarda unos minutos en volver a registrar la suscripción del disparador. **Los cambios de asignaciones que ocurren en ese intervalo no disparan el flow y se pierden** (comprobado en DEV: el alta T1 se perdió). Por eso, **después de cada despliegue**:

```bash
python3 scripts/seguridad/paso3_reconciliar.py --aplicar
```

La reconciliación solo **agrega** los compartidos que falten. Los compartidos sobrantes de un cierre o reasignación ocurridos durante ese intervalo quedan sin revocar; eso queda pendiente para una reconciliación completa (Fase 4).

## Lecciones (verificadas)

- El conector "List rows" del flow **falló con FetchXML que tiene `link-entity`**, aunque la Web API lo aceptaba. Se reemplazó por `$filter` OData con navegación: `sanic_cm_as_cuentaid/_sanic_cm_ct_clienteid_value eq …`.
- Un FetchXML con un GUID vacío (`value=""`) falla: hay que verificar el nulo **antes** de consultar.
- En `principalobjectaccessset`, filtrar por `principalid` devuelve vacío. Hay que filtrar por `objectid` y comparar el principal del lado del cliente.
- Asignar como owner a un usuario sin lectura sobre la tabla falla: el cobrador tiene que tener el rol Localizador.
- `RemovePrivilegeRole` recibe `Privilege` (referencia a la entidad), no `PrivilegeId`.
- La tabla `flowruns` de Dataverse se actualiza con atraso: no sirve para verificar en tiempo real.
