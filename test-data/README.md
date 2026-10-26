# Datos de prueba

Archivos de carga de ejemplo con el formato del archivo diario del banco, para probar la plantilla de Smelter y la pantalla **Cargar archivo** sin datos reales. El formato completo (columnas, separador, codificación) y cómo se carga están en [Carga diaria](../docs/tecnica/07-carga-diaria.md).

⚠️ Este directorio **nunca** lleva datos reales: ni números de tarjeta, ni CIF, ni nombres, ni teléfonos de clientes. Todo lo que hay aquí es **sintético**: los números de tarjeta son ficticios y los nombres, CIF, direcciones y teléfonos, inventados. Nunca cargar estos archivos en producción.

## Archivos

| Archivo | `FechaCarga` | Qué es |
|---|---|---|
| [`cobranza-mock-1000.csv`](cobranza-mock-1000.csv) | `2026-06-20` | Día 1: 1.000 cuentas, 33 columnas |
| [`cobranza-mock-1000-dia2.csv`](cobranza-mock-1000-dia2.csv) | `2026-06-21` | Día 2: las mismas cuentas al día siguiente |

Formato: UTF-8 sin BOM, coma como separador, CRLF, encabezado en la primera fila, fechas `aaaa-mm-dd` y decimales con punto.

La `FechaCarga` es fija. Para que una carga cuente como «de hoy» (Monitoreo y Reconciliación comparan contra la fecha de hoy), reemplazar esa columna en todas las filas con un editor de texto. ⚠️ No abrir ni guardar estos archivos con Excel: convierte los números largos a notación científica, cambia las fechas al formato regional y puede cambiar la codificación o el separador.

## Qué ejercitan

| Caso | Cómo |
|---|---|
| Cliente con varias cuentas | 528 clientes para 1.000 cuentas (hasta 6 por cliente) |
| Catálogos con dedup | 7 productos, 18 agentes y 8 fiadores con nombre |
| Fila sin fiador | 262 filas con `NombreFianza` vacío: la tabla de fiadores recibe un único registro «(sin fiador)» (valor por defecto del mapeo) y esas cuentas quedan sin fiador |
| Dos monedas | ~70 % en NIO, ~30 % en USD |
| Tres tipos de asignación | 801 Diarias, 152 Incobrables, 47 Especiales (las Especiales entran Pendientes de aprobación) |
| *Upsert* de la cuenta | El día 2 trae las mismas cuentas con `DiasMora + 1` y saldos nuevos: la cuenta se actualiza, no se duplica |
| Carga diaria acumulada | Una foto por cuenta y día: después de los dos días hay 2.000 filas de carga diaria |
| Pagos parciales | ~15 % de las cuentas del día 2 con saldo reducido y `FechaUltimoPago` = día 1 |
| Reasignación | ~10 % de las Diarias del día 2 cambian de agente |
| Idempotencia | Volver a cargar el mismo archivo no cambia los conteos |

Sirven para pruebas de **volumen** e idempotencia. Sus agentes no están vinculados a ningún usuario de prueba, así que no sirven para recorrer los flujos de las apps.

## Armar un archivo a mano

Para los flujos de las apps conviene un archivo chico, armado a mano con un editor de texto: cinco filas alcanzan. La guía de [Pruebas](../docs/tecnica/10-pruebas.md#datos-de-prueba-el-archivo-de-carga-sintético) trae dos archivos listos para copiar y las reglas para armar uno propio:

- números de tarjeta ficticios que pasen el dígito verificador Luhn, con un prefijo que no emita ningún banco (los de ejemplo empiezan con `9999`);
- `FechaCarga` igual a la fecha de hoy en la zona horaria del negocio;
- `Agente` con el nombre exacto de un agente vinculado a un usuario de prueba;
- CIF y nombres inventados.

Si cambia el formato del archivo del banco (ver [Carga diaria](../docs/tecnica/07-carga-diaria.md#agregar-o-cambiar-una-columna-del-archivo)), estos archivos de ejemplo quedan desactualizados: hay que agregarles la columna nueva o armar uno nuevo.
