"""Completa datos que la app no escribe (o no puede escribir offline) en los registros que ya existen.
  - Pago.Fecha del pago vacía      → su createdon.
  - Interacción.Fecha de captura   → su createdon, si está vacía.
  - Pago.N° de interacción vacío   → el número de su interacción (en los pagos nuevos lo completa el plugin
                                     CuentaDesdeInteraccion).
Idempotente: solo toca lo vacío. Estas tablas no guardan números de tarjeta.
Uso: python3 scripts/localizador/backfill_fechas_numeros.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
def todas(p):
    out, n = [], p
    while n:
        b = get(n); out += b['value']; n = b.get('@odata.nextLink')
    return out
def plan(desc): print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
def patch(es, i, cuerpo):
    st, b = rq('PATCH', f'{es}({i})', cuerpo)
    if st >= 300: print('   ✗', i[:8], st, str(b)[:200]); return False
    return True

pg = todas("sanic_cm_tbl_pagos?$select=sanic_cm_tbl_pagoid,createdon&$filter=sanic_cm_pg_fecha eq null")
plan(f'pagos sin fecha del pago: {len(pg)} → createdon')
if APLICAR: print('   ok', sum(patch('sanic_cm_tbl_pagos', p['sanic_cm_tbl_pagoid'], {'sanic_cm_pg_fecha': p['createdon']}) for p in pg))

it = todas("sanic_cm_tbl_interaccions?$select=sanic_cm_tbl_interaccionid,createdon&$filter=sanic_cm_in_fechacaptura eq null")
plan(f'interacciones sin fecha de captura: {len(it)} → createdon')
if APLICAR: print('   ok', sum(patch('sanic_cm_tbl_interaccions', x['sanic_cm_tbl_interaccionid'], {'sanic_cm_in_fechacaptura': x['createdon']}) for x in it))

sn = todas("sanic_cm_tbl_pagos?$select=sanic_cm_tbl_pagoid,_sanic_cm_pg_interaccion_value&$filter=sanic_cm_pg_numerointeraccion eq null and _sanic_cm_pg_interaccion_value ne null")
plan(f'pagos sin N° de interacción (con interacción): {len(sn)} → número de su interacción')
if APLICAR:
    n = 0
    for p in sn:
        num = get(f"sanic_cm_tbl_interaccions({p['_sanic_cm_pg_interaccion_value']})?$select=sanic_cm_in_numerointeraccion").get('sanic_cm_in_numerointeraccion')
        if num and patch('sanic_cm_tbl_pagos', p['sanic_cm_tbl_pagoid'], {'sanic_cm_pg_numerointeraccion': num}): n += 1
    print('   ok', n)
if not APLICAR: print('Nada aplicado. Repetir con --aplicar.')
