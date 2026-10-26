"""Corrige en el ambiente las descripciones de metadatos para que queden iguales a las de los scripts que los crean.
Los scripts de creación no tocan lo que ya existe; este sí, pero solo la descripción. Los textos no se repiten aquí:
  - Tablas y columnas: constantes DESC_* de paso23_listas.py, paso15_marcas_offline.py y paso14_actualizacion_offline.py
    (se leen del código fuente sin ejecutarlo).
  - Equipo "CM · Cartera sin cobrador · <entorno>": DESC_EQUIPO de ../seguridad/paso5_cartera_sin_cobrador.py.
  - Custom APIs, sus parámetros y sus propiedades de respuesta: la lista APIS de paso12_custom_api.py.
Plan (por defecto, solo GET): muestra antes y después de cada descripción que cambia.
--aplicar: escribe solo las que cambian. Tablas y columnas con PUT de EntityDefinitions / Attributes y
MSCRM.MergeLabels: true (solo reemplaza la etiqueta 1033), y después PublishXml de las tablas tocadas. Equipo y
Custom APIs con PATCH del registro.
Además recorre las descripciones de las tablas y columnas sanic_cm_* y de las Custom APIs sanic_cm_* (con parámetros y
respuestas) y reporta las que tengan un código de seguimiento, aunque no estén arriba. Esas solo se reportan.
Uso: python3 scripts/pan/paso24_descripciones.py [--aplicar]"""
import ast, os, re, sys
AQUI = os.path.dirname(os.path.abspath(__file__))
SEGURIDAD = os.path.join(AQUI, '..', 'seguridad')
sys.path.insert(0, SEGURIDAD)
sys.path.insert(0, AQUI)
from dvlib import rq, get
from paso12_custom_api import APIS  # sin efectos al importar: su main() solo corre como script

APLICAR = '--aplicar' in sys.argv
P = 'sanic_cm_tbl_'
IDIOMA = 1033
PATRON = re.compile(r'\b(APP|DAT|DEC|SEC|REL)-\d+|\(F\d+\)|\bF\d+\b')


def constante(archivo, nombre):
    """Valor literal de una constante de módulo, leído del fuente (los scripts de creación corren al importarlos)."""
    arbol = ast.parse(open(archivo, encoding='utf-8').read())
    for n in arbol.body:
        if isinstance(n, ast.Assign) and any(isinstance(t, ast.Name) and t.id == nombre for t in n.targets):
            return ast.literal_eval(n.value)
    raise SystemExit(f'no está la constante {nombre} en {archivo}')


def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': IDIOMA}]}


def etiqueta(desc):
    """Texto de la etiqueta en el idioma base de una descripción de metadatos."""
    desc = desc or {}
    for x in desc.get('LocalizedLabels') or []:
        if x.get('LanguageCode') == IDIOMA: return x.get('Label') or ''
    return ((desc.get('UserLocalizedLabel') or {}).get('Label')) or ''


def mostrar(que, antes, despues):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + que)
    print('   antes:   ' + (antes or '(vacía)'))
    print('   después: ' + despues)


def escribir(metodo, ruta, cuerpo, headers=None):
    st, b = rq(metodo, ruta, cuerpo, headers)
    if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
    print('   ✓', st)


f23 = os.path.join(AQUI, 'paso23_listas.py')
f15 = os.path.join(AQUI, 'paso15_marcas_offline.py')
f14 = os.path.join(AQUI, 'paso14_actualizacion_offline.py')
f5 = os.path.join(SEGURIDAD, 'paso5_cartera_sin_cobrador.py')

# (tabla, columna o None para la tabla, descripción nueva)
METADATOS = [
    (P + 'opcionlista', None, constante(f23, 'DESC_TABLA')),
    (P + 'actualizaciondatos', 'sanic_cm_ad_sinvincular', constante(f15, 'DESC_SINVINCULAR')),
    (P + 'pago', 'sanic_cm_pg_reciborepetido', constante(f15, 'DESC_RECIBOREPETIDO')),
    (P + 'actualizaciondatos', 'sanic_cm_ad_cuentaid', constante(f14, 'DESC_CUENTAID')),
    (P + 'actualizaciondatos', 'sanic_cm_ad_fechacaptura', constante(f14, 'DESC_FECHACAPTURA')),
]
DESC_EQUIPO = constante(f5, 'DESC_EQUIPO')
cambios = 0
publicar = set()

# 1) Tablas y columnas ---------------------------------------------------------------------------------------------
for tabla, columna, nueva in METADATOS:
    ruta = f"EntityDefinitions(LogicalName='{tabla}')" + (f"/Attributes(LogicalName='{columna}')" if columna else '')
    nombre = f'{tabla}.{columna}' if columna else f'tabla {tabla}'
    st, b = rq('GET', ruta + '?$select=LogicalName,Description')
    if st == 404: print(f'NO EXISTE {nombre} (lo crea su script)'); continue
    if st >= 300: raise SystemExit(f'GET {nombre} → {st}: {b}')
    actual = etiqueta(b.get('Description'))
    if actual == nueva: print(f'ok      {nombre}'); continue
    cambios += 1
    mostrar(nombre, actual, nueva)
    if not APLICAR: continue
    if columna:
        # El PUT de una columna necesita el cuerpo completo de su tipo derivado (Boolean, Lookup, DateTime...).
        tipo = b['@odata.type'].lstrip('#')
        ruta_put, cuerpo = ruta, get(f'{ruta}/{tipo}')
    else:
        ruta_put, cuerpo = ruta, get(ruta)
    cuerpo = {k: v for k, v in cuerpo.items() if not k.startswith('@odata.context')}
    # Sin el tipo derivado explícito, el PUT de una columna devuelve 0x80048d19.
    if columna: cuerpo['@odata.type'] = '#' + tipo
    cuerpo['Description'] = L(nueva)
    escribir('PUT', ruta_put, cuerpo, {'MSCRM.MergeLabels': 'true'})
    publicar.add(tabla)

# 2) Equipo ----------------------------------------------------------------------------------------------------------
equipos = get("teams?$select=teamid,name,description&$filter=startswith(name,'CM · Cartera sin cobrador')")['value']
if len(equipos) > 1: raise SystemExit(f'hay {len(equipos)} equipos "CM · Cartera sin cobrador…": debe haber uno solo.')
if not equipos:
    print('NO EXISTE equipo "CM · Cartera sin cobrador" (lo crea paso5_cartera_sin_cobrador.py)')
elif (equipos[0].get('description') or '') == DESC_EQUIPO:
    print(f'ok      equipo {equipos[0]["name"]}')
else:
    cambios += 1
    mostrar(f'equipo {equipos[0]["name"]}', equipos[0].get('description'), DESC_EQUIPO)
    if APLICAR: escribir('PATCH', f"teams({equipos[0]['teamid']})", {'description': DESC_EQUIPO})

# 3) Custom APIs, parámetros y respuestas ---------------------------------------------------------------------------
def revisar(coleccion, id_campo, registro, nueva, nombre):
    global cambios
    actual = registro.get('description') or ''
    if actual == nueva: print(f'ok      {nombre}'); return
    cambios += 1
    mostrar(nombre, actual, nueva)
    if APLICAR: escribir('PATCH', f'{coleccion}({registro[id_campo]})', {'description': nueva})

for x in APIS:
    api = x['api']
    c = get(f"customapis?$select=customapiid,description&$filter=uniquename eq '{api}'")['value']
    if not c: print(f'NO EXISTE Custom API {api} (la crea paso12_custom_api.py)'); continue
    revisar('customapis', 'customapiid', c[0], x['definicion']['description'], f'Custom API {api}')
    api_id = c[0]['customapiid']
    for coleccion, id_campo, lista in (('customapirequestparameters', 'customapirequestparameterid', [(p[0], p[3]) for p in x['parametros']]),
                                       ('customapiresponseproperties', 'customapiresponsepropertyid', [(r[0], r[2]) for r in x['respuesta']])):
        hay = {r['uniquename']: r for r in get(f"{coleccion}?$select={id_campo},uniquename,description&$filter=_customapiid_value eq {api_id}")['value']}
        for nombre, nueva in lista:
            if nombre not in hay: print(f'NO EXISTE {api}.{nombre} (lo crea paso12_custom_api.py)'); continue
            revisar(coleccion, id_campo, hay[nombre], nueva, f'{coleccion} {api}.{nombre}')

if APLICAR and publicar:
    xml = ''.join(f'<entity>{t}</entity>' for t in sorted(publicar))
    st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities>{xml}</entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)

# 4) Reporte: cualquier descripción con un código de seguimiento (solo se informa) ------------------------------------
print('--- descripciones con códigos de seguimiento en el ambiente (solo reporte)')
hallados = 0
def reportar(que, texto):
    global hallados
    if texto and PATRON.search(texto):
        hallados += 1
        print(f'REPORTE {que}: {texto}')

tablas = [t for t in get('EntityDefinitions?$select=LogicalName,Description')['value'] if t['LogicalName'].startswith('sanic_cm_')]
for t in sorted(tablas, key=lambda t: t['LogicalName']):
    reportar(f"tabla {t['LogicalName']}", etiqueta(t.get('Description')))
    for a in get(f"EntityDefinitions(LogicalName='{t['LogicalName']}')/Attributes?$select=LogicalName,Description")['value']:
        if a['LogicalName'].startswith('sanic_cm_'):
            reportar(f"{t['LogicalName']}.{a['LogicalName']}", etiqueta(a.get('Description')))
for c in get("customapis?$select=customapiid,uniquename,description&$filter=startswith(uniquename,'sanic_cm_')")['value']:
    reportar(f"Custom API {c['uniquename']}", c.get('description'))
    for coleccion in ('customapirequestparameters', 'customapiresponseproperties'):
        for r in get(f"{coleccion}?$select=uniquename,description&$filter=_customapiid_value eq {c['customapiid']}")['value']:
            reportar(f"{coleccion} {c['uniquename']}.{r['uniquename']}", r.get('description'))
print(f'{hallados} con código de seguimiento' + (' (en modo plan incluye los que este script corregiría).' if hallados and not APLICAR else '.'))

print(('listo. ' if APLICAR else 'plan (sin cambios). Aplicar con --aplicar. ') + f'{cambios} descripción(es) a cambiar.')
