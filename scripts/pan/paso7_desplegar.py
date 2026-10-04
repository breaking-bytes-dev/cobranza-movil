"""Fase 2 · Paso 7: despliega las dos canvas apps (cobrador y backoffice) a DEV en UNA importación, desde el repo.
Por qué no alcanzan import-app / import-backoffice: esos workflows exportan la solución vigente y solo swapean el .msapp.
Las tablas que la app puede usar también están en customizations.xml (<DatabaseReferences> y <CdsDependencies> de cada
<CanvasApp>), que volvían viejas: la app publicada nunca consultaba una tabla nueva (la bóveda) aunque el .msapp la tuviera.

Pasos: pac canvas pack de cada app (con chequeo de YAML: con la carpeta mal nombrada pac empaqueta SIN fórmulas y no avisa)
→ pac solution export → swap de los .msapp → DatabaseReferences/CdsDependencies desde LocalDatabaseReferences de cada app
→ pac solution import --publish-changes. Usa el perfil activo de pac (SP de DEV). Después: paso3_reconciliar.py --aplicar.
Uso: python3 scripts/pan/paso7_desplegar.py [--aplicar]"""
import html, json, os, re, subprocess, sys, tempfile, zipfile

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
    ldr = json.loads(json.loads(z.read('Properties.json'))['LocalDatabaseReferences'])['default.cds']['dataSources']
    print(f"{nombre}: {len(yamls)} .pa.yaml, {len(ldr)} fuentes de Dataverse, usa la bóveda en fórmulas: {b'PAN de cuentas' in texto}")
    if len(yamls) < minimo: raise SystemExit(f'{nombre}: el .msapp tiene {len(yamls)} .pa.yaml (mínimo {minimo}). ¿Carpeta Src mal nombrada?')
    msapps[nombre] = (open(destino, 'rb').read(), ldr)

fresco = os.path.join(tmp, 'fresh.zip')
pac('solution', 'export', '--name', 'cm_core_cobranzamovil', '--path', fresco, '--managed', 'false', '--overwrite')
zin = zipfile.ZipFile(fresco)
cust = zin.read('customizations.xml').decode('utf-8')

def actualizar_app(cust, nombre, ldr):
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
    bloque = bloque.replace(m2.group(0), '<CdsDependencies>' + html.escape(json.dumps(dep, separators=(',', ':')), quote=False) + '</CdsDependencies>')
    print(f"  {nombre}: DatabaseReferences + {sorted(set(db['default.cds']['dataSources']) - antes) or 'sin cambios'}")
    return cust[:ini] + bloque + cust[fin:]

for nombre, (_, ldr) in msapps.items(): cust = actualizar_app(cust, nombre, ldr)
deploy = os.path.join(tmp, 'deploy.zip')
with zipfile.ZipFile(deploy, 'w') as zout:
    for it in zin.infolist():
        data = zin.read(it.filename)
        if it.filename == 'customizations.xml': data = cust.encode('utf-8')
        for nombre, (bytes_app, _) in msapps.items():
            if it.filename == f'CanvasApps/{nombre}_DocumentUri.msapp': data = bytes_app
        zi = zipfile.ZipInfo(it.filename, date_time=it.date_time); zi.compress_type = it.compress_type; zi.external_attr = it.external_attr
        zout.writestr(zi, data)
print('deploy.zip listo en', deploy)
if not APLICAR: print('Nada importado. Repetir con --aplicar.'); sys.exit()
for l in pac('solution', 'import', '--path', deploy, '--force-overwrite', '--publish-changes')[-3:]: print(' ', l)
print('Importado. Falta: python3 scripts/seguridad/paso3_reconciliar.py --aplicar')
