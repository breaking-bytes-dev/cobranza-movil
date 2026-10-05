"""Fase 2 · Paso 7: despliega las dos canvas apps (cobrador y backoffice) a DEV en UNA importación, desde el repo.
Por qué no alcanzan import-app / import-backoffice: esos workflows exportan la solución vigente y solo swapean el .msapp.
Las tablas que la app puede usar también están en customizations.xml (<DatabaseReferences> y <CdsDependencies> de cada
<CanvasApp>), que volvían viejas: la app publicada nunca consultaba una tabla nueva (la bóveda) aunque el .msapp la tuviera.

Pasos: pac canvas pack de cada app (con chequeo de YAML: con la carpeta mal nombrada pac empaqueta SIN fórmulas y no avisa)
→ pac solution export → swap de los .msapp → DatabaseReferences/CdsDependencies desde LocalDatabaseReferences de cada app
→ pac solution import --publish-changes → restaura el estado de los pasos de plugin de CM y de los flows de la solución
(verificado 2026-10-04: una importación con --activate-plugins ENCENDIÓ PanGuard y una sin él APAGÓ ClaveDesdeCuenta).
Usa el perfil activo de pac (SP de DEV). Después: paso3_reconciliar.py --aplicar.
Uso: python3 scripts/pan/paso7_desplegar.py [--aplicar]"""
import html, json, os, re, subprocess, sys, tempfile, time, zipfile
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
PAC = os.path.expanduser('~/.dotnet/tools/pac')
APPS = [  # (carpeta, nombre de la canvas app, mínimo de .pa.yaml esperado)
    ('apps/localizador-canvas', 'sanic_appcobranzamovil_c4e67', 13),
    ('apps/backoffice-canvas', 'sanic_appcmbackofficecanvas_d0162', 17),
]
APLICAR = '--aplicar' in sys.argv
tmp = tempfile.mkdtemp(prefix='cm-deploy-')

def pac(*args):
    r = subprocess.run([PAC, *args], capture_output=True, text=True)
    salida = (r.stdout + r.stderr).strip().splitlines()
    if r.returncode != 0 or any('error' in l.lower() for l in salida[-3:]):
        raise SystemExit(f"pac {' '.join(args[:2])} falló:\n" + '\n'.join(salida[-8:]))
    return salida

msapps = {}
for carpeta, nombre, minimo in APPS:
    destino = os.path.join(tmp, nombre + '.msapp')
    pac('canvas', 'pack', '--sources', os.path.join(RAIZ, carpeta), '--msapp', destino)
    z = zipfile.ZipFile(destino)
    yamls = [n for n in z.namelist() if n.endswith('.pa.yaml')]
    texto = b''.join(z.read(n) for n in yamls)
    props = json.loads(z.read('Properties.json'))
    ldr = json.loads(props['LocalDatabaseReferences'])['default.cds']['dataSources']
    perfil = props.get('ManualOfflineProfileId') or None  # perfil offline personalizado (paso7_perfil_offline.py)
    print(f"{nombre}: {len(yamls)} .pa.yaml, {len(ldr)} fuentes de Dataverse, usa la bóveda en fórmulas: {b'PAN de cuentas' in texto}, "
          f"perfil offline: {perfil or 'autogenerado'}")
    if len(yamls) < minimo: raise SystemExit(f'{nombre}: el .msapp tiene {len(yamls)} .pa.yaml (mínimo {minimo}). ¿Carpeta Src mal nombrada?')
    msapps[nombre] = (open(destino, 'rb').read(), ldr, perfil)

fresco = os.path.join(tmp, 'fresh.zip')
pac('solution', 'export', '--name', 'cm_core_cobranzamovil', '--path', fresco, '--managed', 'false', '--overwrite')
zin = zipfile.ZipFile(fresco)
cust = zin.read('customizations.xml').decode('utf-8')

def actualizar_app(cust, nombre, ldr, perfil):
    i = cust.index(f'<Name>{nombre}</Name>'); ini = cust.rindex('<CanvasApp>', 0, i); fin = cust.index('</CanvasApp>', i)
    bloque = cust[ini:fin]
    m = re.search(r'<DatabaseReferences>(.*?)</DatabaseReferences>', bloque, re.S)
    db = json.loads(html.unescape(m.group(1)))
    antes = set(db['default.cds']['dataSources'])
    for k, v in ldr.items(): db['default.cds']['dataSources'][k] = {'entitySetName': v['entitySetName'], 'logicalName': v['logicalName']}
    bloque = bloque.replace(m.group(0), '<DatabaseReferences>' + html.escape(json.dumps(db, ensure_ascii=False, separators=(',', ':')), quote=False) + '</DatabaseReferences>')
    m2 = re.search(r'<CdsDependencies>(.*?)</CdsDependencies>', bloque, re.S)
    dep = json.loads(html.unescape(m2.group(1))); tablas = {d['logicalname'] for d in dep['cdsdependencies'] if d.get('componenttype') == 1}
    for v in ldr.values():
        if v['logicalName'] not in tablas: dep['cdsdependencies'].append({'componenttype': 1, 'logicalname': v['logicalName']})
    if perfil:  # la dependencia 161 (perfil offline) apunta al personalizado, no al autogenerado
        dep['cdsdependencies'] = [d for d in dep['cdsdependencies'] if d.get('componenttype') != 161] + [{'dependencyid': perfil, 'componenttype': 161}]
    bloque = bloque.replace(m2.group(0), '<CdsDependencies>' + html.escape(json.dumps(dep, separators=(',', ':')), quote=False) + '</CdsDependencies>')
    print(f"  {nombre}: DatabaseReferences + {sorted(set(db['default.cds']['dataSources']) - antes) or 'sin cambios'}")
    return cust[:ini] + bloque + cust[fin:]

for nombre, (_, ldr, perfil) in msapps.items(): cust = actualizar_app(cust, nombre, ldr, perfil)
deploy = os.path.join(tmp, 'deploy.zip')
with zipfile.ZipFile(deploy, 'w') as zout:
    for it in zin.infolist():
        data = zin.read(it.filename)
        if it.filename == 'customizations.xml': data = cust.encode('utf-8')
        for nombre, (bytes_app, _, _) in msapps.items():
            if it.filename == f'CanvasApps/{nombre}_DocumentUri.msapp': data = bytes_app
        zi = zipfile.ZipInfo(it.filename, date_time=it.date_time); zi.compress_type = it.compress_type; zi.external_attr = it.external_attr
        zout.writestr(zi, data)
print('deploy.zip listo en', deploy)
if not APLICAR: print('Nada importado. Repetir con --aplicar.'); sys.exit()
def estados():
    pasos = {x['sdkmessageprocessingstepid']: (x['name'], x['statecode']) for x in
             get("sdkmessageprocessingsteps?$select=name,statecode&$filter=startswith(name,'Sanic.CobranzaMovil.Plugins.')")['value']}
    sol = get("solutions?$select=solutionid&$filter=uniquename eq 'cm_core_cobranzamovil'")['value'][0]['solutionid']
    ids = {c['objectid'] for c in get(f"solutioncomponents?$select=objectid&$filter=_solutionid_value eq {sol} and componenttype eq 29")['value']}
    flows = {w['workflowid']: (w['name'], w['statecode']) for w in get("workflows?$select=name,statecode&$filter=category eq 5")['value'] if w['workflowid'] in ids}
    return pasos, flows
pasos_antes, flows_antes = estados()
print(f'estado previo: {sum(v[1] == 0 for v in pasos_antes.values())} pasos activos de {len(pasos_antes)}, '
      f'{sum(v[1] == 1 for v in flows_antes.values())} flows encendidos de {len(flows_antes)}')
try:
    for intento in range(10):
        try:
            for l in pac('solution', 'import', '--path', deploy, '--force-overwrite', '--publish-changes')[-3:]: print(' ', l)
            break
        except SystemExit as e:
            # Studio (también una sesión de solo lectura abierta en modo edición) deja la app "locked for editing" unos minutos.
            if 'AppLeaseConflict' in str(e) and intento < 9:
                print('  la app está tomada en Studio (AppLeaseConflict); reintento en 60 s'); time.sleep(60); continue
            raise
finally:  # aunque la importación falle, los estados de pasos y flows vuelven a como estaban
    pasos_desp, flows_desp = estados()
    for i, (nombre, st) in pasos_antes.items():
        if pasos_desp.get(i, (nombre, st))[1] != st:
            r = rq('PATCH', f'sdkmessageprocessingsteps({i})', {'statecode': st, 'statuscode': 1 if st == 0 else 2})
            print(f"  restaurado paso {nombre}: {'activo' if st == 0 else 'desactivado'} → {r[0]}")
    for i, (nombre, st) in flows_antes.items():
        if flows_desp.get(i, (nombre, st))[1] != st:
            r = rq('PATCH', f'workflows({i})', {'statecode': st, 'statuscode': 2 if st == 1 else 1})
            print(f"  restaurado flow {nombre}: {'encendido' if st == 1 else 'apagado'} → {r[0]}")
print('Importado. Falta: python3 scripts/seguridad/paso3_reconciliar.py --aplicar')
