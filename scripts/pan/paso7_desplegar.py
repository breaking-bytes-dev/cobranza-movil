"""Fase 2 · Paso 7: despliega las dos canvas apps (cobrador y backoffice) a DEV en UNA importación, desde el repo.
Por qué no alcanzan import-app / import-backoffice: esos workflows exportan la solución vigente y solo swapean el .msapp.
Las tablas que la app puede usar también están en customizations.xml (<DatabaseReferences> y <CdsDependencies> de cada
<CanvasApp>), que volvían viejas: la app publicada nunca consultaba una tabla nueva (la bóveda) aunque el .msapp la tuviera.

Pasos: pac canvas pack de cada app (con chequeo de YAML: con la carpeta mal nombrada pac empaqueta SIN fórmulas y no avisa)
y chequeo de la foto del esquema (paso7_esquema: una columna que usan las fórmulas y la foto no conoce deja la fórmula inerte)
→ pac solution export → swap de los .msapp → DatabaseReferences/CdsDependencies desde LocalDatabaseReferences de cada app
→ pac solution import --publish-changes → restaura el estado de los pasos de plugin de CM y de los flows de la solución
(verificado 2026-10-04: una importación con --activate-plugins ENCENDIÓ PanGuard y una sin él APAGÓ ClaveDesdeCuenta).
Sello de versión (F7): cada app lleva en App.Formulas `nfVersion = "dev";`. Se empaqueta una COPIA de la carpeta con
"<commit>[+dirty] <aaaa-mm-dd HH:MM UTC>" en lugar de "dev" (el repo no cambia), y tras importar se guarda en
local/version-desplegada.json: las suites lo comparan con lo que muestra la app servida antes de probar.
Usa el perfil activo de pac (SP de DEV). Después: paso3_reconciliar.py --aplicar.
--parche: exporta e importa la solución cm_parche (paso22_parche.py) en vez de cm_core, y solo swapea las apps que el
parche contiene. Mucho más rápido y no toca plugins, flows ni roles (no están en el parche). Sin paso3 después.
Uso: python3 scripts/pan/paso7_desplegar.py [--parche] [--aplicar]"""
import datetime, html, json, os, re, shutil, signal, subprocess, sys, tempfile, time, zipfile
import yaml
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get
import paso7_esquema

RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
PAC = os.path.expanduser('~/.dotnet/tools/pac')
APPS = [  # (carpeta, nombre de la canvas app, mínimo de .pa.yaml esperado)
    ('apps/localizador-canvas', 'sanic_appcobranzamovil_c4e67', 13),
    ('apps/backoffice-canvas', 'sanic_appcmbackofficecanvas_d0162', 17),
]
APLICAR = '--aplicar' in sys.argv
SOLUCION = 'cm_parche' if '--parche' in sys.argv else 'cm_core_cobranzamovil'
tmp = tempfile.mkdtemp(prefix='cm-deploy-')

def pac(*args):
    r = subprocess.run([PAC, *args], capture_output=True, text=True)
    salida = (r.stdout + r.stderr).strip().splitlines()
    if r.returncode != 0 or any('error' in l.lower() for l in salida[-3:]):
        raise SystemExit(f"pac {' '.join(args[:2])} falló:\n" + '\n'.join(salida[-8:]))
    return salida

# Propiedades que Studio rechaza (PA2108) pero `pac canvas pack` acepta sin avisar: la app publicada queda que NO abre en
# Studio ("Error opening file"). `pac canvas validate` ya no existe, así que se frena acá. Ampliar al encontrar otra.
PROHIBIDAS = {'Classic/Button': {'AccessibleLabel'}, 'Label': {'RadiusTopLeft', 'RadiusTopRight', 'RadiusBottomLeft', 'RadiusBottomRight'},
              'Classic/Icon': {'RadiusTopLeft', 'RadiusTopRight', 'RadiusBottomLeft', 'RadiusBottomRight'}}
def revisar_yaml(carpeta):
    errores = []
    for raiz, _, archivos in os.walk(os.path.join(RAIZ, carpeta, 'Src')):
        for a in archivos:
            if not a.endswith('.pa.yaml'): continue
            # YAML válido: pac empaqueta igual un archivo que no parsea (p. ej. ": " dentro de una fórmula inline) y
            # Studio no abre la app; el servicio sigue sirviendo la versión anterior sin avisar (2026-10-05).
            try: yaml.compose(open(os.path.join(raiz, a), encoding='utf-8'), Loader=yaml.SafeLoader)  # solo estructura ('=' no es un tag)
            except yaml.YAMLError as e:
                errores.append(f'{a}: YAML inválido ({str(e).splitlines()[0]}; una fórmula con ": " va en bloque |-)'); continue
            ctl = None
            for n, l in enumerate(open(os.path.join(raiz, a), encoding='utf-8'), 1):
                m = re.match(r'^\s+Control: (\S+?)@', l)
                if m: ctl = m.group(1); continue
                m = re.match(r'^\s+(\w+): ', l)
                if m and ctl and m.group(1) in PROHIBIDAS.get(ctl, ()):
                    errores.append(f'{a}:{n} {ctl} no admite {m.group(1)}')
    if errores: raise SystemExit('YAML que Studio rechaza (la app no abriría):\n  ' + '\n  '.join(errores))

def git(*args): return subprocess.run(['git', '-C', RAIZ, *args], capture_output=True, text=True).stdout.strip()
SELLO_CUANDO = datetime.datetime.now(datetime.timezone.utc)
VERSION = (git('rev-parse', '--short', 'HEAD') + ('+dirty' if git('status', '--porcelain', '--untracked-files=no') else '')
           + ' ' + SELLO_CUANDO.strftime('%Y-%m-%d %H:%M'))
MARCA = 'nfVersion = "dev";'
def copia_sellada(carpeta, nombre):
    copia = os.path.join(tmp, nombre + '-src'); shutil.copytree(os.path.join(RAIZ, carpeta), copia)
    app = os.path.join(copia, 'Src', 'App.pa.yaml'); txt = open(app, encoding='utf-8').read()
    if txt.count(MARCA) != 1: raise SystemExit(f'{nombre}: App.pa.yaml tiene que tener UNA vez `{MARCA}` (sello de versión, F7)')
    open(app, 'w', encoding='utf-8').write(txt.replace(MARCA, f'nfVersion = "{VERSION}";'))
    return copia
print('versión que se sella en las apps:', VERSION)

msapps = {}
for carpeta, nombre, minimo in APPS:
    fuentes = copia_sellada(carpeta, nombre)
    revisar_yaml(fuentes)
    destino = os.path.join(tmp, nombre + '.msapp')
    pac('canvas', 'pack', '--sources', fuentes, '--msapp', destino)
    z = zipfile.ZipFile(destino)
    yamls = [n for n in z.namelist() if n.endswith('.pa.yaml')]
    texto = b''.join(z.read(n) for n in yamls)
    props = json.loads(z.read('Properties.json'))
    ldr = json.loads(props['LocalDatabaseReferences'])['default.cds']['dataSources']
    perfil = props.get('ManualOfflineProfileId') or None  # perfil offline personalizado (paso7_perfil_offline.py)
    print(f"{nombre}: {len(yamls)} .pa.yaml, {len(ldr)} fuentes de Dataverse, usa la bóveda en fórmulas: {b'PAN de cuentas' in texto}, "
          f"perfil offline: {perfil or 'autogenerado'}")
    if len(yamls) < minimo: raise SystemExit(f'{nombre}: el .msapp tiene {len(yamls)} .pa.yaml (mínimo {minimo}). ¿Carpeta Src mal nombrada?')
    paso7_esquema.revisar(nombre, z.read('References/DataSources.json'), [z.read(n).decode('utf-8') for n in yamls])
    msapps[nombre] = (open(destino, 'rb').read(), ldr, perfil)

fresco = os.path.join(tmp, 'fresh.zip')
pac('solution', 'export', '--name', SOLUCION, '--path', fresco, '--managed', 'false', '--overwrite')
zin = zipfile.ZipFile(fresco)
cust = zin.read('customizations.xml').decode('utf-8')
# En el parche solo van las apps que contiene (paso22_parche.py --app …); las demás no se tocan.
msapps = {n: v for n, v in msapps.items() if f'<Name>{n}</Name>' in cust}
if not msapps: raise SystemExit(f'{SOLUCION} no contiene ninguna de las apps: agregarla con paso22_parche.py --app … --aplicar')
print(f'solución: {SOLUCION} · apps que se despliegan: {", ".join(msapps)}')

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
        # La fuente "Environment" (Custom APIs) va en DatabaseReferences pero no es una tabla: Studio no la pone en CdsDependencies.
        if v['entitySetName'] == 'Environment': continue
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
# El import apaga los pasos de plugin y el `finally` de abajo los vuelve a su estado. Si el proceso muere a mitad
# (2026-10-12: un `timeout` le mandó SIGTERM durante el import), el `finally` no corre y los 15 pasos de CM (PanGuard
# incluido) quedan APAGADOS; el deploy siguiente tomaba ese estado como "el de antes" y los dejaba así.
# Defensa doble: SIGTERM/SIGHUP pasan por el `finally`, y el estado previo queda en disco hasta restaurarlo; si al
# arrancar hay uno guardado, es de un deploy interrumpido y manda ese.
for sig in (signal.SIGTERM, signal.SIGHUP): signal.signal(sig, lambda s, f: sys.exit(f'deploy interrumpido por la señal {s}'))
ESTADO = os.path.join(RAIZ, 'local', 'deploy-estado-previo.json')
if os.path.exists(ESTADO):
    guardado = json.load(open(ESTADO))
    pasos_antes = {k: tuple(v) for k, v in guardado['pasos'].items()}; flows_antes = {k: tuple(v) for k, v in guardado['flows'].items()}
    print(f'⚠ hay un estado previo de un deploy interrumpido ({guardado["cuando"]}): se restaura ESE, no el actual')
else:
    pasos_antes, flows_antes = estados()
    json.dump({'cuando': time.strftime('%Y-%m-%d %H:%M:%S'), 'pasos': pasos_antes, 'flows': flows_antes}, open(ESTADO, 'w'), indent=1)
print(f'estado previo: {sum(v[1] == 0 for v in pasos_antes.values())} pasos activos de {len(pasos_antes)}, '
      f'{sum(v[1] == 1 for v in flows_antes.values())} flows encendidos de {len(flows_antes)}')
try:
    for intento in range(10):
        try:
            for l in pac('solution', 'import', '--path', deploy, '--force-overwrite', '--publish-changes')[-3:]: print(' ', l)
            # Versión por app (cobrador / backoffice): con --parche solo cambia la que va en el parche; la otra conserva la suya.
            arch = os.path.join(RAIZ, 'local', 'version-desplegada.json')
            try: previo = json.load(open(arch))
            except (OSError, ValueError): previo = {}
            ahora = datetime.datetime.now(datetime.timezone.utc).isoformat(timespec='seconds')
            por_app = previo.get('por_app') or {c: {'version': previo['version'], 'importado': previo['importado']}
                                                 for c in ('cobrador', 'backoffice') if previo.get('version')}
            for carpeta, n, _ in APPS:
                if n in msapps: por_app['cobrador' if 'localizador' in carpeta else 'backoffice'] = {'version': VERSION, 'importado': ahora}
            json.dump({'version': VERSION, 'importado': ahora, 'apps': list(msapps), 'por_app': por_app}, open(arch, 'w'), indent=1)
            print('  versión desplegada:', VERSION, '(local/version-desplegada.json)')
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
    pasos_fin, flows_fin = estados()
    if all(pasos_fin.get(i, v)[1] == v[1] for i, v in pasos_antes.items()) and all(flows_fin.get(i, v)[1] == v[1] for i, v in flows_antes.items()):
        os.remove(ESTADO)
    else:
        print(f'⚠ quedaron pasos o flows sin restaurar: el estado previo sigue en {ESTADO} y el próximo deploy lo usa')
print('Importado.' if SOLUCION == 'cm_parche' else 'Importado. Falta: python3 scripts/seguridad/paso3_reconciliar.py --aplicar')
