"""APP-09/APP-10 (respaldo del lado del servidor): clave alternativa en Pagos sobre (N° de recibo, cuenta, moneda).
La app ya bloquea el doble toque y los recibos repetidos, pero esta clave es la última barrera para un duplicado que
llegue por otro camino (otro dispositivo, un reintento de sincronización): la sincronización de ese pago falla y el
cobrador lo ve en los errores de sincronización. La moneda es parte de la clave para permitir que un mismo recibo
cubra un pago en córdobas y otro en dólares de la misma cuenta (la app valida la misma regla).
Antes de crear la clave verifica que los datos no tengan duplicados (si los hay, el índice falla).
Uso: python3 scripts/localizador/clave_recibo_pago.py [--aplicar]"""
import os, sys, time
from collections import Counter
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
E, K = 'sanic_cm_tbl_pago', 'sanic_cm_ak_pg_recibo_cuenta_moneda'
COLS = ['sanic_cm_pg_numerorecibo', 'sanic_cm_pg_cuentaid', 'sanic_cm_pg_moneda']
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}

claves = get(f"EntityDefinitions(LogicalName='{E}')/Keys?$select=SchemaName,EntityKeyIndexStatus")['value']
ya = [k for k in claves if k['SchemaName'].lower() == K.lower()]
if ya: print(f"YA EXISTE {K} (índice: {ya[0]['EntityKeyIndexStatus']})"); sys.exit()

filas, n = [], f"sanic_cm_tbl_pagos?$select=sanic_cm_pg_numerorecibo,_sanic_cm_pg_cuentaid_value,sanic_cm_pg_moneda"
while n:
    b = get(n); filas += b['value']; n = b.get('@odata.nextLink')
rep = [k for k, v in Counter((f['sanic_cm_pg_numerorecibo'], f['_sanic_cm_pg_cuentaid_value'], f['sanic_cm_pg_moneda']) for f in filas).items() if v > 1]
print(f'pagos: {len(filas)} | combinaciones repetidas (recibo, cuenta, moneda): {len(rep)}')
if rep: raise SystemExit('✗ hay duplicados: limpiarlos antes de crear la clave')

print(('APLICAR ' if APLICAR else 'PLAN    ') + f'clave {E}.{K} = {COLS}')
if not APLICAR: print('Nada aplicado. Repetir con --aplicar.'); sys.exit()
st, b = rq('POST', f"EntityDefinitions(LogicalName='{E}')/Keys",
           {'SchemaName': K, 'DisplayName': {'LocalizedLabels': [{'Label': 'Recibo único por cuenta y moneda', 'LanguageCode': 1033}]},
            'KeyAttributes': COLS}, SOL)
if st >= 300: raise SystemExit(f'✗ {st}: {b}')
for _ in range(24):  # el índice se crea en segundo plano
    k = [x for x in get(f"EntityDefinitions(LogicalName='{E}')/Keys?$select=SchemaName,EntityKeyIndexStatus")['value'] if x['SchemaName'].lower() == K.lower()]
    estado = k[0]['EntityKeyIndexStatus'] if k else '?'
    if estado in ('Active', 'Failed'): break
    time.sleep(10)
print('índice:', estado)
if estado != 'Active': raise SystemExit('✗ el índice no quedó activo')
