"""Ayuda común de los pasos 6 y 7: quitar o devolver privilegios a roles CM (unidad raíz), con plan por defecto."""
from dvlib import rq, get

_priv = {}

def privilegio(nombre):
    if nombre not in _priv:
        v = get(f"privileges?$select=privilegeid&$filter=name eq '{nombre}'")['value']
        if not v: raise SystemExit(f'no existe el privilegio {nombre}')
        _priv[nombre] = v[0]['privilegeid']
    return _priv[nombre]

def rol(nombre):
    v = get(f"roles?$select=roleid&$filter=name eq '{nombre}' and _parentroleid_value eq null")['value']
    if not v: raise SystemExit(f'no existe el rol {nombre}')
    return v[0]['roleid']

def aplicar(cambios, modo):
    """cambios: [(rol, privilegio, profundidad_original)]. modo: 'plan' | 'aplicar' | 'revertir'.
    aplicar = quitar el privilegio; revertir = devolverlo con su profundidad original. Devuelve cuántos cambió."""
    n = 0
    for nombre_rol, nombre_priv, original in cambios:
        rid, pid = rol(nombre_rol), privilegio(nombre_priv)
        actual = {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={rid})')['RolePrivileges']}.get(pid)
        if modo == 'revertir':
            if actual == original: continue
            print(f'  {nombre_rol} · {nombre_priv}: {actual or "—"} → {original}')
            st, b = rq('POST', f'roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole',
                       {'Privileges': [{'PrivilegeId': pid, 'BusinessUnitId': None, 'Depth': original}]})
        else:
            if actual is None: continue
            print(f'  {nombre_rol} · {nombre_priv}: {actual} → —')
            if modo == 'plan': n += 1; continue
            st, b = rq('POST', f'roles({rid})/Microsoft.Dynamics.CRM.RemovePrivilegeRole',
                       {'Privilege': {'@odata.type': 'Microsoft.Dynamics.CRM.privilege', 'privilegeid': pid}})
        if st >= 300: raise SystemExit(f'error HTTP {st}: {b}')
        n += 1
    return n

def modo(argv):
    return 'revertir' if '--revertir' in argv else 'aplicar' if '--aplicar' in argv else 'plan'
