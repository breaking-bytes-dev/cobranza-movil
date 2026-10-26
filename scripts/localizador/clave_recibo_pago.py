"""RETIRADO: NO CORRER. scripts/pan/paso15_marcas_offline.py quita esta clave: offline, el pago que la clave rechaza
se pierde en silencio (el teléfono no reintenta). El recibo repetido entra marcado por el plugin ReciboRepetido.

Crea la clave alternativa de Pagos sobre (N° de recibo, cuenta, moneda), como barrera del servidor contra duplicados que
lleguen por otro camino (otro dispositivo, un reintento de sincronización). La moneda forma parte de la clave para que un
mismo recibo cubra un pago en córdobas y otro en dólares de la misma cuenta.
Antes de crearla verifica que no haya duplicados (si los hay, el índice falla).
Uso: python3 scripts/localizador/clave_recibo_pago.py [--aplicar]"""
import os, sys, time
from collections import Counter
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

raise SystemExit('✗ retirado: ver scripts/pan/paso15_marcas_offline.py')
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
