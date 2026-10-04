"""Fase 2 · Paso 4: privilegios sobre la bóveda sanic_cm_tbl_cuentapan (diseño §2.3 + D6).
Localizador: Read Basic (la ve solo por el compartido en cascada desde la cuenta). BackOffice: Read Global (graba en el core).
Admin, Solicitante, Supervisor: ninguno. Nadie tiene Create/Write/Delete: la bóveda la escribe solo la carga (SYSTEM).
Uso: python3 scripts/pan/paso4_roles.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get
APLICAR = '--aplicar' in sys.argv
pv = get("privileges?$select=privilegeid&$filter=name eq 'prvReadsanic_cm_tbl_cuentapan'")['value'][0]['privilegeid']
for rol, depth in [('SR | CM | Localizador', 'Basic'), ('SR | CM | BackOffice', 'Global')]:
    rid = get(f"roles?$select=roleid&$filter=name eq '{rol}'")['value'][0]['roleid']
    actual = {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={rid})')['RolePrivileges']}.get(pv)
    print(f'{rol}: Read bóveda {actual or "—"} → {depth}')
    if APLICAR and actual != depth:
        if actual: rq('POST', f'roles({rid})/Microsoft.Dynamics.CRM.RemovePrivilegeRole', {'Privilege': {'@odata.type': 'Microsoft.Dynamics.CRM.privilege', 'privilegeid': pv}})
        st, b = rq('POST', f'roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole', {'Privileges': [{'PrivilegeId': pv, 'BusinessUnitId': None, 'Depth': depth}]})
        print('   ', '✓' if st < 300 else f'✗ {st} {b}')
for rol in ['SR | CM | Admin', 'SR | CM | Solicitante', 'SR | CM | Supervisor']:
    rid = get(f"roles?$select=roleid&$filter=name eq '{rol}'")['value'][0]['roleid']
    print(f'{rol}: Read bóveda', {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={rid})')['RolePrivileges']}.get(pv, '— (ninguno, correcto)'))
