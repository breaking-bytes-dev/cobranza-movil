"""Fase 1 · Paso 5: equipo "CM · Cartera sin cobrador · <entorno>" y su rol mínimo (DAT-07/08).

Los plugins RepartoAsignacion y RepartoPorAgente le pasan a este equipo las asignaciones activas cuyo agente no tiene
usuario (o que no tienen agente): así nadie las ve en el teléfono y el cobrador anterior las pierde.

- Rol "SR | CM | Cartera sin cobrador" (unidad raíz): SOLO Read Basic de la asignación (más los 4 de SharePoint que
  Dataverse le pone a todo rol). Dataverse no deja pasarle un
  registro a un dueño sin permiso de lectura sobre la tabla; nada más hace falta. Se agrega a la solución.
- Equipo dueño en la unidad raíz, con ese único rol y SIN miembros. Si alguien le agrega un miembro, el script lo
  reporta y termina con error (no lo saca solo: que lo decida una persona).

Idempotente. Por defecto solo muestra el plan (solo hace GET). Aplica con --aplicar.
Uso: python3 paso5_cartera_sin_cobrador.py --entorno DEV [--aplicar]"""
import argparse, sys
from dvlib import rq, get, A

SOLUCION = 'cm_core_cobranzamovil'
ROL = 'SR | CM | Cartera sin cobrador'
PRIVILEGIOS = [('prvReadsanic_cm_tbl_asignacion', 'Basic')]
COMPONENTE_ROL = 20

ap = argparse.ArgumentParser()
ap.add_argument('--entorno', required=True, help='sufijo del nombre del equipo, como los demás equipos de CM (DEV, PRD)')
ap.add_argument('--aplicar', action='store_true')
args = ap.parse_args()
EQUIPO = f'CM · Cartera sin cobrador · {args.entorno}'
APLICAR = args.aplicar

def ok(st, b, que):
    if st >= 300: raise SystemExit(f'{que}: HTTP {st} {b}')

raiz = get("businessunits?$select=businessunitid&$filter=parentbusinessunitid eq null")['value'][0]['businessunitid']
yo = get('WhoAmI')['UserId']

# ---------------------------------------------------------------------------------------------- rol
roles = get(f"roles?$select=roleid&$filter=name eq '{ROL}' and _businessunitid_value eq {raiz}")['value']
if roles:
    rol = roles[0]['roleid']; print(f'rol "{ROL}": existe')
elif APLICAR:
    st, b = rq('POST', 'roles', {'name': ROL, 'businessunitid@odata.bind': f'/businessunits({raiz})'},
               headers={'Prefer': 'return=representation'})
    ok(st, b, 'crear rol'); rol = b['roleid']; print(f'rol "{ROL}": creado')
else:
    rol = None; print(f'rol "{ROL}": se crea')

objetivo = {}
for nombre, prof in PRIVILEGIOS:
    objetivo[get(f"privileges?$select=privilegeid&$filter=name eq '{nombre}'")['value'][0]['privilegeid']] = (nombre, prof)
# Dataverse le agrega a todo rol los de SharePoint (Read/Write/Create SharePointData, ReadSharePointDocument) y los vuelve a
# poner aunque se reemplacen: no se cuentan. Con el equipo sin miembros no le dan acceso a nadie.
nombre_priv = lambda i: get(f"privileges({i})?$select=name")['name']
actual = {} if not rol else {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={rol})')['RolePrivileges']}
actual = {k: v for k, v in actual.items() if k in objetivo or 'SharePoint' not in nombre_priv(k)}
if actual != {k: v[1] for k, v in objetivo.items()}:
    print(f'  privilegios: {len(actual)} → solo ' + ', '.join(f'{n} {p}' for n, p in objetivo.values()))
    if APLICAR:
        st, b = rq('POST', f'roles({rol})/Microsoft.Dynamics.CRM.ReplacePrivilegesRole',
                   {'Privileges': [{'PrivilegeId': k, 'BusinessUnitId': raiz, 'Depth': v[1]} for k, v in objetivo.items()]})
        ok(st, b, 'privilegios del rol')
else:
    print('  privilegios: correctos (' + ', '.join(f'{n} {p}' for n, p in objetivo.values()) + ')')

sol = get(f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'")['value'][0]['solutionid']
en_sol = bool(rol) and bool(get(f"solutioncomponents?$select=solutioncomponentid&$filter=_solutionid_value eq {sol} and objectid eq {rol} and componenttype eq {COMPONENTE_ROL}")['value'])
if not en_sol:
    print(f'  solución {SOLUCION}: se agrega')
    if APLICAR:
        st, b = rq('POST', 'AddSolutionComponent', {'ComponentId': rol, 'ComponentType': COMPONENTE_ROL, 'SolutionUniqueName': SOLUCION,
                                                   'AddRequiredComponents': False, 'DoNotIncludeSubcomponents': False})
        ok(st, b, 'agregar el rol a la solución')
else:
    print(f'  solución {SOLUCION}: ya está')

# ---------------------------------------------------------------------------------------------- equipo
equipos = get(f"teams?$select=teamid,teamtype,_businessunitid_value&$filter=startswith(name,'CM · Cartera sin cobrador')")['value']
if len(equipos) > 1:
    raise SystemExit(f'hay {len(equipos)} equipos "CM · Cartera sin cobrador…": los plugins exigen uno solo.')
if equipos:
    eq = equipos[0]; equipo = eq['teamid']; print(f'equipo "{EQUIPO}": existe')
    if eq['teamtype'] != 0 or eq['_businessunitid_value'] != raiz:
        raise SystemExit('el equipo existe pero no es un equipo dueño de la unidad raíz.')
elif APLICAR:
    st, b = rq('POST', 'teams', {'name': EQUIPO, 'teamtype': 0, 'businessunitid@odata.bind': f'/businessunits({raiz})',
                                 'administratorid@odata.bind': f'/systemusers({yo})',
                                 'description': 'Dueño de las asignaciones cuyo agente no tiene usuario (DAT-07/08). SIN miembros.'},
               headers={'Prefer': 'return=representation'})
    ok(st, b, 'crear equipo'); equipo = b['teamid']; print(f'equipo "{EQUIPO}": creado')
else:
    equipo = None; print(f'equipo "{EQUIPO}": se crea')

roles_eq = [] if not equipo else [r['roleid'] for r in get(f'teams({equipo})/teamroles_association?$select=roleid')['value']]
if rol is None or rol not in roles_eq:
    print(f'  rol del equipo: se asigna "{ROL}"')
    if APLICAR:
        st, b = rq('POST', f'teams({equipo})/teamroles_association/$ref', {'@odata.id': f'{A}roles({rol})'})
        ok(st, b, 'asignar el rol al equipo')
otros = [r for r in roles_eq if r != rol]
if otros:
    raise SystemExit(f'el equipo tiene {len(otros)} rol(es) además de "{ROL}": quitarlos a mano (debe tener solo ese).')

miembros = [] if not equipo else get(f'teams({equipo})/teammembership_association?$select=fullname')['value']
if miembros:
    print(f'  ✗ el equipo tiene {len(miembros)} miembro(s): debe estar VACÍO. Sacarlos a mano y volver a correr.')
    sys.exit(1)
print('  miembros: 0 (correcto)' if equipo else '  miembros: 0')
print('listo.' if APLICAR else 'plan (sin cambios). Aplicar con --aplicar.')
