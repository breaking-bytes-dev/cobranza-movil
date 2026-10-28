"""Solución «parche» (cm_parche) para desplegar rápido SOLO lo que se está cambiando.
La solución completa (cm_core_cobranzamovil) trae plugins, pasos, flows, roles y Custom APIs: exportarla e importarla
tarda y obliga a apagar y restaurar los pasos de plugin. cm_parche es una solución no administrada del mismo editor que
contiene únicamente los componentes que se le agregan (por ejemplo, la app del localizador y una tabla que cambió). Un
componente puede estar a la vez en cm_core y en cm_parche: no se mueve, solo se lista también acá.

Despliegue con el parche: python3 scripts/pan/paso7_desplegar.py --parche --aplicar (exporta e importa cm_parche).
La fuente de verdad sigue siendo cm_core: lo que se agrega al parche ya existe en DEV (este script no crea componentes).

Uso:
  python3 scripts/pan/paso22_parche.py                          lista lo que tiene el parche
  python3 scripts/pan/paso22_parche.py --app cobrador [--aplicar]  agrega la app del localizador (o backoffice)
  python3 scripts/pan/paso22_parche.py --tabla sanic_cm_tbl_pago [--aplicar]
  python3 scripts/pan/paso22_parche.py --plugins [--aplicar]     agrega el ensamblado de plugins (sin sus pasos)
  python3 scripts/pan/paso22_parche.py --quitar --app backoffice [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

PARCHE, BASE = 'cm_parche', 'cm_core_cobranzamovil'
APPS = {'cobrador': 'sanic_appcobranzamovil_c4e67', 'backoffice': 'sanic_appcmbackofficecanvas_d0162'}
TIPOS = {1: 'tabla', 91: 'ensamblado', 300: 'canvas app'}
ENSAMBLADO = 'Sanic.CobranzaMovil.Plugins'
APLICAR, QUITAR = '--aplicar' in sys.argv, '--quitar' in sys.argv

def arg(nombre):
    return [sys.argv[i + 1] for i, a in enumerate(sys.argv[:-1]) if a == nombre]

def ok(r, que):
    st, b = r[0], r[1]
    if st >= 300: raise SystemExit(f'✗ {que}: {st} {b}')
    print(f'  ✓ {que}')

def solucion(nombre):
    v = get(f"solutions?$select=solutionid,_publisherid_value&$filter=uniquename eq '{nombre}'")['value']
    return v[0] if v else None

# 1) La solución, con el editor de cm_core (mismo prefijo sanic_).
base, parche = solucion(BASE), solucion(PARCHE)
if not parche:
    print(f'{PARCHE} no existe: se crea (editor de {BASE})')
    if APLICAR:
        ok(rq('POST', 'solutions', {'uniquename': PARCHE, 'friendlyname': 'CM · Parche (despliegue rápido)', 'version': '1.0.0.0',
                                     'description': 'Solo los componentes que se están cambiando. Ver scripts/pan/paso22_parche.py',
                                     'publisherid@odata.bind': f"/publishers({base['_publisherid_value']})"}), 'crear la solución')
        parche = solucion(PARCHE)

# 2) Componentes pedidos.
pedidos = []
for a in arg('--app'):
    if a not in APPS: raise SystemExit(f'--app {a}: usar {" o ".join(APPS)}')
    v = get(f"canvasapps?$select=canvasappid,name&$filter=name eq '{APPS[a]}'")['value']
    if not v: raise SystemExit(f'no existe la canvas app {APPS[a]}')
    pedidos.append((300, v[0]['canvasappid'], APPS[a]))
for t in arg('--tabla'):
    v = get(f"EntityDefinitions(LogicalName='{t}')?$select=MetadataId")
    pedidos.append((1, v['MetadataId'], t))

if '--plugins' in sys.argv:
    v = get(f"pluginassemblies?$select=pluginassemblyid,version&$filter=name eq '{ENSAMBLADO}'")['value']
    if not v: raise SystemExit(f'no existe el ensamblado {ENSAMBLADO}')
    pedidos.append((91, v[0]['pluginassemblyid'], f"{ENSAMBLADO} {v[0]['version']}"))

for tipo, cid, nombre in pedidos:
    if QUITAR:
        print(f'quitar {TIPOS[tipo]} {nombre} de {PARCHE}')
        if APLICAR:
            # La Web API no recibe ComponentId: el id del componente va como solutioncomponentid de una referencia.
            ok(rq('POST', 'RemoveSolutionComponent', {'ComponentType': tipo, 'SolutionUniqueName': PARCHE, 'SolutionComponent': {
                '@odata.type': 'Microsoft.Dynamics.CRM.solutioncomponent', 'solutioncomponentid': cid}}), 'quitado')
    else:
        print(f'agregar {TIPOS[tipo]} {nombre} a {PARCHE} (sin componentes requeridos)')
        if APLICAR: ok(rq('POST', 'AddSolutionComponent', {'ComponentId': cid, 'ComponentType': tipo, 'SolutionUniqueName': PARCHE,
                                                           'AddRequiredComponents': False}), 'agregado')

# 3) Lo que tiene el parche ahora.
if parche:
    comps = get(f"solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq {parche['solutionid']}")['value']
    nombres = {c['canvasappid']: c['name'] for c in get('canvasapps?$select=canvasappid,name')['value']}
    nombres.update({a['pluginassemblyid']: f"{a['name']} {a['version']}" for a in get('pluginassemblies?$select=pluginassemblyid,name,version')['value']})
    print(f'{PARCHE}: {len(comps)} componente(s)')
    for c in comps:
        print(f"  - {TIPOS.get(c['componenttype'], c['componenttype'])}: {nombres.get(c['objectid'], c['objectid'])}")
if not APLICAR and pedidos: print('Nada cambiado. Repetir con --aplicar.')
