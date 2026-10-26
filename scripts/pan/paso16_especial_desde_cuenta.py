"""Relleno de las Especiales de la carga que ya existen (idempotente).
La carga escribe tipo, nota y retiro de la Especial en la CUENTA y la app los lee de la ASIGNACIÓN. El plugin
EspecialDesdeCuenta los copia al crear la Especial o en cada carga; esto completa las que ya estaban, con la misma regla:
solo Especiales SIN solicitante y solo lo vacío (notas = "Tipo — nota", retiro NIO y USD). Nunca pisa lo de una persona.
Corre con el Service Principal (sin roles de CM: ColumnasAsignacion no lo frena). No imprime valores, solo conteos.
Va DESPUÉS de paso5 (con EspecialDesdeCuenta registrado).
Uso: python3 scripts/pan/paso16_especial_desde_cuenta.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
ESPECIAL, SEP, LARGO = 3, ' — ', 2000

def notas(tipo, nota):
    tipo, nota = (tipo or '').strip(), (nota or '').strip()
    if not tipo and not nota: return None
    return (nota if not tipo else tipo + SEP + nota)[:LARGO]

filas, n = [], (f"sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid,sanic_cm_as_notas,sanic_cm_as_retironio,sanic_cm_as_retirousd"
                f"&$filter=sanic_cm_as_tipo eq {ESPECIAL} and _sanic_cm_as_solicitadopor_value eq null and _sanic_cm_as_cuentaid_value ne null"
                f"&$expand=sanic_cm_as_cuentaid($select=sanic_cm_ct_tipoespecial,sanic_cm_ct_nota,sanic_cm_ct_retironio,sanic_cm_ct_retirousd)")
while n:
    b = get(n); filas += b['value']; n = b.get('@odata.nextLink')

cambios = []
for a in filas:
    c, cambio = a.get('sanic_cm_as_cuentaid') or {}, {}
    if not (a['sanic_cm_as_notas'] or '').strip():
        t = notas(c.get('sanic_cm_ct_tipoespecial'), c.get('sanic_cm_ct_nota'))
        if t is not None: cambio['sanic_cm_as_notas'] = t
    for de, a_ in (('sanic_cm_ct_retironio', 'sanic_cm_as_retironio'), ('sanic_cm_ct_retirousd', 'sanic_cm_as_retirousd')):
        if a[a_] is None and c.get(de) is not None: cambio[a_] = c[de]
    if cambio: cambios.append((a['sanic_cm_tbl_asignacionid'], cambio))

print(f'Especiales sin solicitante: {len(filas)} | a completar: {len(cambios)}')
for i, (aid, cambio) in enumerate(cambios, 1):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f'asignación #{i}: {", ".join(sorted(cambio))}')
    if APLICAR:
        st, b = rq('PATCH', f'sanic_cm_tbl_asignacions({aid})', cambio)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
if not APLICAR and cambios: print('Nada aplicado. Repetir con --aplicar.')
