# Datos de prueba — Mock CSV para Smelter

> CSVs sintéticos para **probar la configuración del import** (Fase 2). Generados por
> [`generar-mock.py`](generar-mock.py). Reproducible (seed fija). **Fechas en ISO `yyyy-mm-dd`**.

## Archivos

| Archivo | FechaCarga | Qué es |
|---------|-----------|--------|
| **`cobranza-mock-1000.csv`** | `2026-06-20` | Día 1 — 1000 cuentas, 33 columnas. |
| **`cobranza-mock-1000-dia2.csv`** | `2026-06-21` | Día 2 — el "día siguiente" del día 1 (mismas cuentas, actualizadas). |

Las 32 columnas del diccionario ([`../docs/01-import-y-diccionario-de-datos.md`](../docs/desarrollo/01-import-y-diccionario-de-datos.md)) **+ `FechaCarga`**.

## Qué EJERCITA

### Día 1 (carga inicial)
| Aspecto | Cómo lo prueba |
|---------|----------------|
| Cliente **1:N** Cuenta | 528 clientes únicos para 1000 cuentas (hasta 6 por cliente). |
| Dedup de catálogos | Solo 7 productos, 18 agentes, 8 fiadores repetidos. |
| Re-upsert direcciones/teléfonos | Mismo CIF → mismos datos en todas sus filas. |
| Dos monedas | ~70% NIO / ~30% USD. |
| 3 TipoAsignacion | ~800 Diaria, ~150 Incobrable, ~47 Especial. |
| Fiador opcional | ~30% sin fianza. |
| Fechas **ISO** | `yyyy-mm-dd` → Smelter parsea **sin `DATEPARSE`**. |

### Día 2 (el día siguiente) — lo más jugoso
| Aspecto | Cómo lo prueba |
|---------|----------------|
| **`cuenta` se PISA** (upsert) | Mismas cuentas con `DiasMora +1`, saldos actualizados → tras importar, `cuenta` sigue en **1000** filas, no 2000. |
| **`cargadiaria` ACUMULA** | Alt key `NumeroCuenta + FechaCarga` → tras importar ambos días, `cargadiaria` tiene **2000** snapshots. |
| Pagos parciales | ~150 cuentas con saldo reducido + `FechaUltimoPago = 2026-06-20`. |
| Reasignaciones | ~75 Diarias cambian de agente → prueba el cambio de **owner** en `asignacion`. |

## Cómo probar el ciclo completo

1. Activar **todas las alt keys** ([`../docs/02-import-configuracion.md`](../docs/guias/02-import-configuracion.md) §7).
2. Template: Source = CSV, Has Header = Sí, Delimiter = `,`, Chunk Size = 1000.
3. **Importar día 1** → verificar: maestras pobladas, `cuenta`=1000, `cargadiaria`=1000, `asignacion` ok.
4. **Reimportar día 1** (mismo archivo) → **idempotencia**: nada se duplica (conteos iguales).
5. **Importar día 2** → verificar:
   - `cuenta` sigue en **1000** (se pisó con los valores nuevos: DiasMora+1, saldos, agente).
   - `cargadiaria` ahora en **2000** (acumuló el snapshot del 21).
   - Las ~75 cuentas reasignadas cambiaron de agente (y su owner, vía el plugin/flow).
6. Revisar **Import Job Log** por filas fallidas.

## Notas

- **Fechas ISO:** con `yyyy-mm-dd` **no** hace falta `DATEPARSE` en los Column Mappings.
- **`Especial`** (~47 filas): el import las crea como **`Pendiente`** (DEC-022) → van a la aprobación del
  Supervisor. Por eso `asignacion` = **1000** (953 Diaria/Incobrable activas + ~47 Especial pendientes).
- **Alt keys:** los CIF usan guiones (`-`, permitido). No se generan los caracteres prohibidos
  (`/ < > * % & : \ ? +`).

## Regenerar / ajustar

```bash
python3 test-data/generar-mock.py        # día 1 + día 2, 1000 filas c/u
python3 test-data/generar-mock.py 5000   # otra cantidad
```

Seed fija → mismo comando, mismos archivos (útil para repetir la prueba de idempotencia).
