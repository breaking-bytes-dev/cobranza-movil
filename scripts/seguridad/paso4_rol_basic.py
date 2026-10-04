"""Fase 1 · Paso 4: rol 'SR | CM | Localizador' → Read Basic (en vez de Global) en maestros.
Uso: python3 paso4_rol_basic.py [--aplicar | --revertir]"""
import sys
from dvlib import rq, get

ROLE = get("roles?$select=roleid&$filter=name eq 'SR | CM | Localizador'")['value'][0]['roleid']
TABLAS = ['cuenta', 'cliente', 'direccion', 'telefono', 'fiador']
destino = 'Global' if '--revertir' in sys.argv else 'Basic'

actual = {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={ROLE})')['RolePrivileges']}
for t in TABLAS:
    pv = get(f"privileges?$select=privilegeid&$filter=name eq 'prvReadsanic_cm_tbl_{t}'")['value'][0]['privilegeid']
    print(f'Read {t}: {actual.get(pv, "—")} → {destino}')
    if ('--aplicar' in sys.argv or '--revertir' in sys.argv) and actual.get(pv) != destino:
        st, b = rq('POST', f'roles({ROLE})/Microsoft.Dynamics.CRM.RemovePrivilegeRole', {'Privilege': {'@odata.type': 'Microsoft.Dynamics.CRM.privilege', 'privilegeid': pv}})
        assert st < 300, b
        st, b = rq('POST', f'roles({ROLE})/Microsoft.Dynamics.CRM.AddPrivilegesRole',
                   {'Privileges': [{'PrivilegeId': pv, 'BusinessUnitId': None, 'Depth': destino}]})
        print('   ', 'ok' if st < 300 else b)
