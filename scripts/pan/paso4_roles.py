"""Privilegios sobre la bóveda sanic_cm_tbl_cuentapan.
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
# System Customizer recibe privilegios automáticos sobre toda tabla nueva (incluido Read de la bóveda) y lo tienen
# identidades de servicio y equipos de desarrollo: se le quita todo sobre la bóveda.
# Support User también los recibe, pero la plataforma no deja modificar ese rol (lo usa el soporte de Microsoft).
sc = get("roles?$select=roleid&$filter=name eq 'System Customizer' and _parentroleid_value eq null")['value'][0]['roleid']
for p in get(f"roles({sc})/roleprivileges_association?$select=name,privilegeid&$filter=contains(name,'sanic_cm_tbl_cuentapan')")['value']:
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f"System Customizer: quitar {p['name']}")
    if APLICAR:
        st, b = rq('POST', f'roles({sc})/Microsoft.Dynamics.CRM.RemovePrivilegeRole', {'Privilege': {'@odata.type': 'Microsoft.Dynamics.CRM.privilege', 'privilegeid': p['privilegeid']}})
        print('   ', '✓' if st < 300 else f'✗ {st} {b}')
