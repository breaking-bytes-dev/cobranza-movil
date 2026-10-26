# Scripts de seguridad y ciclo de vida

Scripts que configuran el modelo de seguridad (roles, equipo «Cartera sin cobrador», cascadas, auditoría) y el borrado programado de Cobranza Móvil. El diseño que aplican está en [Seguridad](../../docs/tecnica/03-seguridad.md) y [Ciclo de vida de los datos](../../docs/tecnica/08-ciclo-de-vida.md). El mapa de todos los scripts y su clasificación está en [`scripts/README.md`](../README.md).

## Uso

```bash
python3 scripts/seguridad/<script>.py            # muestra el plan; no cambia nada
python3 scripts/seguridad/<script>.py --aplicar  # aplica
```

- Se corren desde la raíz del repositorio.
- Las credenciales salen de `pp_secrets.env` en la raíz (o del archivo indicado en `PP_SECRETS`), con `DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET` y `PP_TENANT_ID`. [`dvlib.py`](dvlib.py) **no** lee variables de entorno.
- `paso4_rol_basic.py`, `paso6_cobrador_solo_crea.py`, `paso7_sin_flows.py` y `paso8_sin_trazas.py` aceptan `--revertir` (devuelve los privilegios originales). `paso5_cartera_sin_cobrador.py` no lo acepta y exige `--entorno <ENV>`.
- `paso2_prueba.py` (retirado) no tiene modo plan: crea datos al correr.

## Scripts

| Script | Cuándo | Qué hace |
|---|---|---|
| [`paso5_cartera_sin_cobrador.py`](paso5_cartera_sin_cobrador.py) `--entorno <ENV>` | Instalación | Crea el equipo «CM · Cartera sin cobrador · `<ENV>`» (sin miembros) y su rol mínimo. Termina con error si el equipo tiene miembros |
| [`paso10_borrado_programado.py`](paso10_borrado_programado.py) | Instalación y al cambiar un plazo | Programa los trabajos diarios de *bulk delete*. Reconoce los trabajos por nombre: cambiar un plazo exige retirar el trabajo viejo (ver [Mantenimiento](../../docs/tecnica/11-mantenimiento.md#cambiar-un-plazo-de-borrado)) |
| [`paso3_reconciliar.py`](paso3_reconciliar.py) | Después de cada despliegue | Agrega los compartidos de cuenta, cliente y fiador que falten. `--revocar` quita los sobrantes |
| [`paso1_cascadas_auditoria.py`](paso1_cascadas_auditoria.py), [`paso1c_auditar_historia_especial.py`](paso1c_auditar_historia_especial.py), [`paso4_rol_basic.py`](paso4_rol_basic.py), [`paso6_cobrador_solo_crea.py`](paso6_cobrador_solo_crea.py), [`paso7_sin_flows.py`](paso7_sin_flows.py), [`paso8_sin_trazas.py`](paso8_sin_trazas.py) | Verificación (sin `--aplicar`) | Ya aplicados. Su resultado viaja en la *solution*. Sin `--aplicar` confirman que la configuración sigue igual |
| [`paso9_limpieza_asignacion.py`](paso9_limpieza_asignacion.py), [`paso11_reparto_pendiente.py`](paso11_reparto_pendiente.py) | No hacen falta | Arreglos de datos de una sola vez. En una instalación nueva no aplican |
| [`paso14_limpieza_total.py`](paso14_limpieza_total.py) | Solo desarrollo | ⚠️ Borra **todos** los datos de negocio y las cargas de Smelter. Nunca en producción |
| [`paso2_flow.py`](paso2_flow.py), [`flow-owner-base.json`](flow-owner-base.json), [`paso2_prueba.py`](paso2_prueba.py) | Retirados | El *flow* de dueños lo reemplazó el *plugin* `RepartoAsignacion`. No correr |
| [`dvlib.py`](dvlib.py), [`roles_cm.py`](roles_cm.py) | Bibliotecas | Cliente de la Web API y ayuda para quitar o devolver privilegios. Los usan los demás scripts |

## Advertencias

- ⚠️ **Durante una importación de la *solution* el reparto no corre**: los pasos de *plugin* quedan apagados. Por eso, después de cada despliegue, `paso3_reconciliar.py --aplicar`.
- ⚠️ **Quitar un permiso no limpia los teléfonos.** La sincronización incremental baja solo registros modificados. Un teléfono que ya bajó datos los conserva hasta una sincronización completa o hasta que se borren los datos de la app.
- Asignar como dueño a un usuario sin lectura sobre la tabla falla: el localizador necesita el rol Localizador antes de que se le reparta la cartera.
