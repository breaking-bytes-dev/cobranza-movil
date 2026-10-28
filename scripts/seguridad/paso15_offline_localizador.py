"""El rol 'SR | CM | Localizador' lee el offline profile, para que el teléfono pueda bajar la app.

Una canvas app offline exige que el rol del usuario lea Mobile Offline Profile y Sync Error
(https://learn.microsoft.com/power-apps/mobile/canvas-mobile-offline-setup). Sync Error ya estaba; los ítems del perfil
no tienen privilegio propio. Sin el del perfil, el teléfono falla con «No tiene permiso para descargar actualizaciones» (falta prvReadMobileOfflineProfile).
Basic User y System Administrator lo dan: un usuario con solo los roles de CM no lo tenía.
Es una tabla de la organización: la profundidad es Global (la misma que da Basic User).
Uso: python3 paso15_offline_localizador.py [--aplicar]"""
import sys
from dvlib import rq, get
from roles_cm import privilegio, rol

APLICAR = '--aplicar' in sys.argv
ROL = 'SR | CM | Localizador'
PRIVILEGIOS = ['prvReadMobileOfflineProfile']
GLOBAL = 'Global'

rid = rol(ROL)
actuales = {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={rid})')['RolePrivileges']}
faltan = [n for n in PRIVILEGIOS if actuales.get(privilegio(n)) != GLOBAL]
for n in faltan:
    print(f'  {ROL} · {n}: {actuales.get(privilegio(n)) or "—"} → {GLOBAL}')
if faltan and APLICAR:
    st, b = rq('POST', f'roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole',
               {'Privileges': [{'PrivilegeId': privilegio(n), 'BusinessUnitId': None, 'Depth': GLOBAL} for n in faltan]})
    if st >= 300: raise SystemExit(f'error HTTP {st}: {b}')
print(f'{len(faltan)} cambio(s)' + ('' if APLICAR else ' (plan; aplicar con --aplicar)') + '.' if faltan else 'sin cambios: ya está así.')
