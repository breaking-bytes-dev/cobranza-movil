"""Fase 2 · Paso 3 (MIGRAR, aditivo): bóveda + claves HMAC desde los datos existentes.
- cuenta: crea su fila en sanic_cm_tbl_cuentapan (PAN completo, ****1234, HMAC) y completa ct_clavepan / ct_ultimos4.
- asignación: as_clavepan = clave de su cuenta.  carga diaria: cd_clavepan.  pago: pg_cuentaid (vía interacción o número).
La clave K se lee de PAN_HMAC_KEY_FILE (por defecto ~/.config/cobranza-movil/pan_hmac_dev.key) y NUNCA se imprime.
Nunca imprime PAN: solo conteos. Idempotente. Uso: python3 scripts/pan/paso3_backfill.py [--aplicar]"""
import hashlib, hmac, os, sys, collections
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
K = bytes.fromhex(open(os.path.expanduser(os.environ.get('PAN_HMAC_KEY_FILE', '~/.config/cobranza-movil/pan_hmac_dev.key'))).read().strip())
assert len(K) == 32, 'la clave K tiene que ser de 32 bytes'
def clave(pan): return hmac.new(K, pan.strip().encode(), hashlib.sha256).hexdigest()
def mascara(pan): return '****' + pan.strip()[-4:]
def todas(path):
    out, nxt = [], path
    while nxt:
        b = get(nxt); out += b['value']; nxt = b.get('@odata.nextLink')
    return out
def paralelo(fn, items):
    errores = []
    with ThreadPoolExecutor(8) as ex:
        for r in ex.map(fn, items):
            if r: errores.append(r)
    return errores

cuentas = todas('sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid,sanic_cm_ct_numerodecuenta,sanic_cm_ct_clavepan,sanic_cm_ct_ultimos4')
boveda = {b['_sanic_cm_cp_cuentaid_value'] for b in todas('sanic_cm_tbl_cuentapans?$select=_sanic_cm_cp_cuentaid_value')}
pend_c = [c for c in cuentas if c['sanic_cm_ct_numerodecuenta'] and not c['sanic_cm_ct_numerodecuenta'].startswith('****')]
clave_de = {c['sanic_cm_tbl_cuentaid']: clave(c['sanic_cm_ct_numerodecuenta']) for c in pend_c}
dup_c = [k for k, n in collections.Counter(clave_de.values()).items() if n > 1]
print(f'cuentas: {len(cuentas)} | con PAN a migrar: {len(pend_c)} | ya en bóveda: {len(boveda)} | claves duplicadas: {len(dup_c)}')

asig = todas('sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid,_sanic_cm_as_cuentaid_value,sanic_cm_as_numerocuenta,sanic_cm_as_tipo,sanic_cm_as_clavepan')
def clave_asig(a):
    if a['_sanic_cm_as_cuentaid_value'] in clave_de: return clave_de[a['_sanic_cm_as_cuentaid_value']]
    return clave(a['sanic_cm_as_numerocuenta']) if a.get('sanic_cm_as_numerocuenta') else None
ka = [(a, clave_asig(a)) for a in asig]
dup_a = [k for k, n in collections.Counter((k, a['sanic_cm_as_tipo']) for a, k in ka if k).items() if n > 1]
print(f'asignaciones: {len(asig)} | sin clave calculable: {sum(1 for _, k in ka if not k)} | duplicadas (clave, tipo): {len(dup_a)}')

carga = todas('sanic_cm_tbl_cargadiarias?$select=sanic_cm_tbl_cargadiariaid,sanic_cm_cd_numerocuenta,sanic_cm_cd_fechacarga,sanic_cm_cd_clavepan')
kc = [(c, clave(c['sanic_cm_cd_numerocuenta']) if c.get('sanic_cm_cd_numerocuenta') and not c['sanic_cm_cd_numerocuenta'].startswith('****') else c.get('sanic_cm_cd_clavepan')) for c in carga]
dup_cd = [k for k, n in collections.Counter((k, c['sanic_cm_cd_fechacarga']) for c, k in kc if k).items() if n > 1]
print(f'carga diaria: {len(carga)} | duplicadas (clave, fecha): {len(dup_cd)}')

cta_por_num = {c['sanic_cm_ct_numerodecuenta']: c['sanic_cm_tbl_cuentaid'] for c in pend_c}
pagos = todas('sanic_cm_tbl_pagos?$select=sanic_cm_tbl_pagoid,_sanic_cm_pg_cuentaid_value,sanic_cm_pg_numerocuenta,_sanic_cm_pg_interaccion_value')
print(f'pagos: {len(pagos)} | sin cuenta: {sum(1 for p in pagos if not p["_sanic_cm_pg_cuentaid_value"])}')
if dup_a: print('  ⚠ asignaciones duplicadas por (clave, tipo): la clave sanic_cm_ak_as_clavepan_tipo fallará hasta resolverlas (DAT-13/14)')

if not APLICAR: sys.exit()
# 1) bóveda + cuenta
def mig_cuenta(c):
    i, pan = c['sanic_cm_tbl_cuentaid'], c['sanic_cm_ct_numerodecuenta']
    if i not in boveda:
        st, b = rq('POST', 'sanic_cm_tbl_cuentapans', {'sanic_cm_cp_name': mascara(pan), 'sanic_cm_cp_pan': pan.strip(), 'sanic_cm_cp_clavepan': clave_de[i],
                                                       'sanic_cm_cp_cuentaid@odata.bind': f'/sanic_cm_tbl_cuentas({i})'})
        if st >= 300: return f'bóveda {i[:8]}: {st} {str(b)[:150]}'
    if c.get('sanic_cm_ct_clavepan') != clave_de[i] or not c.get('sanic_cm_ct_ultimos4'):
        st, b = rq('PATCH', f'sanic_cm_tbl_cuentas({i})', {'sanic_cm_ct_clavepan': clave_de[i], 'sanic_cm_ct_ultimos4': pan.strip()[-4:]})
        if st >= 300: return f'cuenta {i[:8]}: {st} {str(b)[:150]}'
e = paralelo(mig_cuenta, pend_c); print('cuentas + bóveda: errores', len(e), e[:3])
# 2) asignaciones
def mig_asig(t):
    a, k = t
    if not k or a.get('sanic_cm_as_clavepan') == k: return
    st, b = rq('PATCH', f"sanic_cm_tbl_asignacions({a['sanic_cm_tbl_asignacionid']})", {'sanic_cm_as_clavepan': k})
    if st >= 300: return f"asig {a['sanic_cm_tbl_asignacionid'][:8]}: {st} {str(b)[:150]}"
e = paralelo(mig_asig, ka); print('asignaciones: errores', len(e), e[:3])
# 3) carga diaria
def mig_carga(t):
    c, k = t
    if not k or c.get('sanic_cm_cd_clavepan') == k: return
    st, b = rq('PATCH', f"sanic_cm_tbl_cargadiarias({c['sanic_cm_tbl_cargadiariaid']})", {'sanic_cm_cd_clavepan': k})
    if st >= 300: return f"carga {c['sanic_cm_tbl_cargadiariaid'][:8]}: {st} {str(b)[:150]}"
e = paralelo(mig_carga, kc); print('carga diaria: errores', len(e), e[:3])
# 4) pagos → cuenta
def mig_pago(p):
    if p['_sanic_cm_pg_cuentaid_value']: return
    cid = None
    if p['_sanic_cm_pg_interaccion_value']:
        cid = get(f"sanic_cm_tbl_interaccions({p['_sanic_cm_pg_interaccion_value']})?$select=_sanic_cm_in_cuentaid_value").get('_sanic_cm_in_cuentaid_value')
    cid = cid or cta_por_num.get((p.get('sanic_cm_pg_numerocuenta') or '').strip())
    if not cid: return f"pago {p['sanic_cm_tbl_pagoid'][:8]}: sin cuenta resoluble"
    st, b = rq('PATCH', f"sanic_cm_tbl_pagos({p['sanic_cm_tbl_pagoid']})", {'sanic_cm_pg_cuentaid@odata.bind': f'/sanic_cm_tbl_cuentas({cid})'})
    if st >= 300: return f"pago {p['sanic_cm_tbl_pagoid'][:8]}: {st} {str(b)[:150]}"
e = paralelo(mig_pago, pagos); print('pagos: errores', len(e), e[:3])
