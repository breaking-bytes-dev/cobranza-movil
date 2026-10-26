# Datos de prueba

Archivos de carga de ejemplo con el formato del archivo diario del banco, para probar la plantilla de Smelter y la pantalla **Cargar archivo** sin datos reales. El formato completo (columnas, separador, codificación) y cómo se carga están en [Carga diaria](../docs/tecnica/07-carga-diaria.md).

⚠️ Este directorio **nunca** lleva datos reales: ni números de tarjeta, ni CIF, ni nombres, ni teléfonos de clientes. Todo lo que hay aquí es sintético y se regenera con el script.

## Archivos

| Archivo | `FechaCarga` | Qué es |
|---|---|---|
| [`cobranza-mock-1000.csv`](cobranza-mock-1000.csv) | `2026-06-20` | Día 1: 1.000 cuentas, 33 columnas |
| [`cobranza-mock-1000-dia2.csv`](cobranza-mock-1000-dia2.csv) | `2026-06-21` | Día 2: las mismas cuentas al día siguiente |
| [`generar-mock.py`](generar-mock.py) | — | Generador de los dos archivos |

Formato: UTF-8 sin BOM, coma como separador, CRLF, encabezado en la primera fila, fechas `aaaa-mm-dd` y decimales con punto.

## Qué ejercitan

| Caso | Cómo |
|---|---|
| Cliente con varias cuentas | 528 clientes para 1.000 cuentas (hasta 6 por cliente) |
| Catálogos con dedup | 7 productos, 18 agentes, 8 fiadores |
| Fila sin fiador | 262 filas con `NombreFianza` vacío |
| Dos monedas | ~70 % en NIO, ~30 % en USD |
| Tres tipos de asignación | 801 Diarias, 152 Incobrables, 47 Especiales |
| *Upsert* de la cuenta | El día 2 trae las mismas cuentas con `DiasMora + 1` y saldos nuevos: la cuenta se actualiza, no se duplica |
| Carga diaria acumulada | Una foto por cuenta y día: después de los dos días hay 2.000 filas de carga diaria |
| Pagos parciales | ~15 % de las cuentas del día 2 con saldo reducido y `FechaUltimoPago` = día 1 |
| Reasignación | ~10 % de las Diarias del día 2 cambian de agente |
| Idempotencia | Volver a cargar el mismo archivo no cambia los conteos |

## Cómo se generan

Desde la raíz del repositorio (el script escribe en `test-data/` con ruta relativa):

```bash
python3 test-data/generar-mock.py         # 1.000 filas por archivo
python3 test-data/generar-mock.py 5000    # otra cantidad
```

La semilla es fija: el mismo comando produce los mismos archivos, así que la prueba de idempotencia se puede repetir. Los nombres de salida son siempre `cobranza-mock-1000*.csv`, aunque se pida otra cantidad.

Si cambia el formato del archivo del banco, actualizar `COLS` en el generador junto con `FilaDeCarga.Encabezado` (ver [Carga diaria](../docs/tecnica/07-carga-diaria.md#agregar-o-cambiar-una-columna-del-archivo)).
