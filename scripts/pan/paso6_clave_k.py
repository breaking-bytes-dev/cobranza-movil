"""Carga K en la tabla protegida de Smelter sanic_tbl_smlt_hmackey bajo el nombre "cm-pan".
K vive en esa tabla en TODOS los ambientes, producción incluida (no se usa Azure Key Vault): Smelter calcula el HMAC
y lee la clave de su propia tabla.
- Le quita a System Customizer los privilegios automáticos sobre esa tabla (la plataforma se los da a toda tabla nueva).
  Quedan System Administrator y roles de Microsoft que no se pueden editar: Service Reader/Writer y Support User
  ("The Support User Role cannot be modified"; lo usa el soporte de Microsoft con consentimiento).
- K se lee de PAN_HMAC_KEY_FILE (por defecto ~/.config/cobranza-movil/pan_hmac_dev.key; en cada ambiente apuntar al
  archivo de SU K, 32 bytes en hexadecimal). Tiene que ser la misma con la que se calcularon las claves ya guardadas.
  Nunca se imprime: la verificación compara en memoria y solo informa "coincide".
Uso: python3 scripts/pan/paso6_clave_k.py [--aplicar]"""
import hmac, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
TABLA, SET, NOMBRE = 'sanic_tbl_smlt_hmackey', 'sanic_tbl_smlt_hmackeies', 'cm-pan'
K = open(os.path.expanduser(os.environ.get('PAN_HMAC_KEY_FILE', '~/.config/cobranza-movil/pan_hmac_dev.key'))).read().strip().lower()
assert len(bytes.fromhex(K)) == 32, 'la clave K tiene que ser de 32 bytes'

# 1) Privilegios sobrantes
for rol in ['System Customizer']:
    r = get(f"roles?$select=roleid&$filter=name eq '{rol}' and _parentroleid_value eq null")['value'][0]
    for p in get(f"roles({r['roleid']})/roleprivileges_association?$select=name,privilegeid&$filter=contains(name,'{TABLA}')")['value']:
        print(('APLICAR ' if APLICAR else 'PLAN    ') + f"{rol}: quitar {p['name']}")
        if APLICAR:
            st, b = rq('POST', f"roles({r['roleid']})/Microsoft.Dynamics.CRM.RemovePrivilegeRole",
                       {'Privilege': {'@odata.type': 'Microsoft.Dynamics.CRM.privilege', 'privilegeid': p['privilegeid']}})
            if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
# 2) K
filas = get(f"{SET}?$select=sanic_tbl_smlt_hmackeyid,sanic_smlt_hk_value&$filter=sanic_smlt_hk_name eq '{NOMBRE}' and statecode eq 0")['value']
if len(filas) > 1: raise SystemExit(f'hay {len(filas)} filas activas "{NOMBRE}": Smelter fallaría. Dejá una sola')
if filas and hmac.compare_digest((filas[0]['sanic_smlt_hk_value'] or '').strip().lower(), K):
    print(f'K "{NOMBRE}": ya cargada y coincide con el archivo')
else:
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f'K "{NOMBRE}": ' + ('reemplazar (no coincide)' if filas else 'crear'))
    if APLICAR:
        cuerpo = {'sanic_smlt_hk_name': NOMBRE, 'sanic_smlt_hk_value': K}
        st, b = rq('PATCH', f"{SET}({filas[0]['sanic_tbl_smlt_hmackeyid']})", cuerpo) if filas else rq('POST', SET, cuerpo)
        if st >= 300: raise SystemExit(f'   ✗ {st}')  # sin el cuerpo: podría repetir el valor
        guardada = get(f"{SET}?$select=sanic_smlt_hk_value&$filter=sanic_smlt_hk_name eq '{NOMBRE}' and statecode eq 0")['value'][0]['sanic_smlt_hk_value']
        print('   ✓ verificación:', 'coincide' if hmac.compare_digest(guardada.strip().lower(), K) else '✗ NO coincide')
