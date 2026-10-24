"""O3 · Relleno del tipo de producto de las cuentas que ya existen (idempotente).
La carga resuelve el producto de la cuenta por CodigoProducto (lookup ct_productoid) pero no llena ct_tipoproducto, que es
lo que muestra el cobrador ("Tarjeta Oro · •••• 1234"): en la prueba del 2026-10-23 las 6.242 cuentas lo tenían vacío.
Desde 1.0.10.0 el plugin TipoDesdeProducto lo copia al crear la cuenta o al cambiar su producto; esto completa las que ya
estaban, con la misma regla: ct_tipoproducto = tipo del producto (vacío si el producto no tiene tipo). Solo escribe donde
difiere. Corre con el Service Principal. No imprime valores, solo conteos.
Va DESPUÉS de paso5 (plugins 1.0.10.0).
Uso: python3 scripts/pan/paso21_tipo_producto.py [--aplicar]"""
import os, sys, collections, concurrent.futures
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv

tipos = {p['sanic_cm_tbl_productoid']: (p['sanic_cm_pr_tipo'] or '').strip() or None
         for p in get('sanic_cm_tbl_productos?$select=sanic_cm_tbl_productoid,sanic_cm_pr_tipo')['value']}

filas, n = [], 'sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid,_sanic_cm_ct_productoid_value,sanic_cm_ct_tipoproducto'
while n:
    st, b = rq('GET', n, headers={'Prefer': 'odata.maxpagesize=5000'})
    if st >= 300: raise SystemExit(f'✗ {st}: {b}')
    filas += b['value']; n = b.get('@odata.nextLink')

cambios, motivo = [], collections.Counter()
for c in filas:
    prod = c['_sanic_cm_ct_productoid_value']
    esperado = tipos.get(prod) if prod else None
    actual = (c['sanic_cm_ct_tipoproducto'] or '').strip() or None
    if actual != esperado:
        cambios.append((c['sanic_cm_tbl_cuentaid'], esperado))
        motivo['vacío → tipo' if actual is None else ('tipo → vacío' if esperado is None else 'tipo distinto')] += 1

print(f'productos: {len(tipos)} | cuentas: {len(filas)} | a corregir: {len(cambios)} {dict(motivo)}')
if not APLICAR:
    if cambios: print('Nada aplicado. Repetir con --aplicar.')
    raise SystemExit(0)

def aplicar(par):
    cid, tipo = par
    st, b = rq('PATCH', f'sanic_cm_tbl_cuentas({cid})', {'sanic_cm_ct_tipoproducto': tipo})
    return st < 300

ok = 0
with concurrent.futures.ThreadPoolExecutor(8) as ex:
    for i, bien in enumerate(ex.map(aplicar, cambios), 1):
        ok += bien
        if i % 500 == 0: print(f'   {i}/{len(cambios)}')
print(f'aplicadas: {ok}/{len(cambios)}')
if ok != len(cambios): raise SystemExit('✗ hubo errores; repetir (es idempotente).')
