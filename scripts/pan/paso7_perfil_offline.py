"""Fase 2 · Paso 7: perfil offline PERSONALIZADO para la app del cobrador (reemplaza al autogenerado).
Por qué: el autogenerado lo rearma el servicio con cada publicación de la app y el 2026-10-04 quedó trabado
("The auto-generated offline profile ... failed to publish. Error: 0x80071151"), la app no abría en el teléfono.
Además no soporta ALM (Microsoft lo documenta) y no viaja en la solución: para PROD hace falta uno propio.

Qué hace (idempotente; por defecto solo muestra el plan, aplica con --aplicar):
  1) crea el perfil PERFIL (si no existe) con las 10 tablas que la app consulta (las mismas del autogenerado)
     y la regla "Todas las filas" (1): el dispositivo baja lo que el usuario puede leer; la seguridad por fila
     (dueño + compartidos en cascada a la bóveda) es la que recorta. Las tablas de FILTROS usan además un filtro
     propio (S-1): asignaciones activas e historial de los últimos 90 días.
  2) lo publica (PublishXml <mobileofflineprofiles>) y verifica publishedon
  3) lo agrega a la solución cm_core_cobranzamovil (componente 161)
  4) escribe su id en ManualOfflineProfileId del .msapr de la app (paso7_desplegar lo lleva a CdsDependencies)
Después: paso7_desplegar.py --aplicar y paso3_reconciliar.py --aplicar.
Uso: python3 scripts/pan/paso7_perfil_offline.py [--aplicar]"""
import json, os, sys, time, zipfile
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
MSAPR = os.path.join(RAIZ, 'apps/localizador-canvas/sanic_appcobranzamovil_c4e67_DocumentUri.msapr')
PERFIL = 'Cobranza Movil - Cobrador'
TABLAS = ['cliente', 'actualizaciondatos', 'agente', 'asignacion', 'cuenta', 'pago', 'direccion', 'interaccion', 'telefono', 'cuentapan',
          'opcionlista']  # APP-49: opciones de lista (mensajes rápidos); tabla chica, baja completa
# S-1: además de la seguridad por fila, se acota lo que baja al teléfono ("Custom data filter", criterio 3, en FetchXML).
# Asignaciones: solo activas. Historial (interacciones, pagos, actualizaciones de datos): modificado en los últimos 90 días;
# lo más viejo se ve en la app solo con señal. Decisión del dueño del producto (2026-10-05).
ULTIMOS_90 = '<fetch version="1.0" mapping="logical"><entity name="{t}"><filter type="and"><condition attribute="modifiedon" operator="last-x-days" value="90" /></filter></entity></fetch>'
FILTROS = {
    'asignacion': '<fetch version="1.0" mapping="logical"><entity name="{t}"><filter type="and"><condition attribute="sanic_cm_as_estado" operator="eq" value="1" /></filter></entity></fetch>',
    'interaccion': ULTIMOS_90, 'pago': ULTIMOS_90, 'actualizaciondatos': ULTIMOS_90,
}
def plan(desc): print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
def ok(r, que):
    if r[0] >= 300: raise SystemExit(f'✗ {que}: {r[0]} {str(r[1])[:400]}')
    return r[1]

# 1) perfil + tablas
p = get(f"mobileofflineprofiles?$select=mobileofflineprofileid,publishedon&$filter=name eq '{PERFIL}'")['value']
if p: pid = p[0]['mobileofflineprofileid']; print(f'perfil existente {pid}, publicado {p[0]["publishedon"]}')
else:
    plan(f"crear perfil '{PERFIL}'"); pid = None
    if APLICAR:
        pid = ok(rq('POST', 'mobileofflineprofiles', {'name': PERFIL, 'description': 'App del cobrador (canvas). Generado por scripts/pan/paso7_perfil_offline.py'},
                    {'Prefer': 'return=representation'}), 'crear perfil')['mobileofflineprofileid']
tiene = {i['selectedentitytypecode']: i for i in get(f"mobileofflineprofileitems?$select=mobileofflineprofileitemid,selectedentitytypecode,recorddistributioncriteria,profileitementityfilter&$filter=_regardingobjectid_value eq {pid}")['value']} if pid else {}
for t in TABLAS:
    lt = f'sanic_cm_tbl_{t}'
    criterio, fx = (3, FILTROS[t].format(t=lt)) if t in FILTROS else (1, None)
    cuerpo = {'recorddistributioncriteria': criterio, 'profileitementityfilter': fx}
    actual = tiene.get(lt)
    if actual and actual['recorddistributioncriteria'] == criterio and (actual.get('profileitementityfilter') or None) == fx: continue
    plan(f"  {'ajustar' if actual else 'agregar'} {lt}: " + ('todas las filas que el usuario puede leer' if criterio == 1 else 'filtro: ' + fx))
    if not APLICAR: continue
    if actual: ok(rq('PATCH', f"mobileofflineprofileitems({actual['mobileofflineprofileitemid']})", cuerpo), f'item {lt}')
    else: ok(rq('POST', 'mobileofflineprofileitems', {'name': lt, 'selectedentitytypecode': lt, **cuerpo,
                                                      'regardingobjectid@odata.bind': f'/mobileofflineprofiles({pid})'}), f'item {lt}')
if not APLICAR: print('Nada aplicado. Repetir con --aplicar.'); sys.exit()

# 2) publicar. publishedon NO sirve de prueba (un perfil nuevo nace con publishedon = createdon y PublishXml no lo mueve):
# la prueba es que la publicación regenera las consultas de sincronización (mobileofflineprofileitemfilter) de cada tabla.
items = [i['mobileofflineprofileitemid'] for i in get(f"mobileofflineprofileitems?$select=mobileofflineprofileitemid&$filter=_regardingobjectid_value eq {pid}")['value']]
def filtros_desde(t):
    fs = get(f"mobileofflineprofileitemfilters?$select=mobileofflineprofileitemid&$filter=modifiedon ge {t}")['value']
    return {f['mobileofflineprofileitemid'] for f in fs} & set(items)
inicio = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
ok(rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><mobileofflineprofiles><mobileofflineprofile>{pid}</mobileofflineprofile></mobileofflineprofiles></importexportxml>'}), 'publicar')
for _ in range(12):
    regenerados = filtros_desde(inicio)
    if len(regenerados) == len(items): break
    time.sleep(5)
print(f'publicado: consultas de sincronización regeneradas para {len(regenerados)} de {len(items)} tablas')
if len(regenerados) != len(items): raise SystemExit('✗ la publicación no regeneró todas las tablas')

# 3) solución
sol = 'cm_core_cobranzamovil'
sid = get(f"solutions?$select=solutionid&$filter=uniquename eq '{sol}'")['value'][0]['solutionid']
if not get(f'solutioncomponents?$select=objectid&$filter=_solutionid_value eq {sid} and objectid eq {pid}')['value']:
    ok(rq('POST', 'AddSolutionComponent', {'ComponentId': pid, 'ComponentType': 161, 'SolutionUniqueName': sol, 'AddRequiredComponents': False}), 'agregar a la solución')
    print(f'agregado a {sol}')

# 4) .msapr
z = zipfile.ZipFile(MSAPR); archivos = {n: z.read(n) for n in z.namelist()}; infos = {i.filename: i for i in z.infolist()}; z.close()
pr = json.loads(archivos['msapp/Properties.json'])
if pr.get('ManualOfflineProfileId') != pid:
    print(f"ManualOfflineProfileId: '{pr.get('ManualOfflineProfileId')}' → {pid}")
    pr['ManualOfflineProfileId'] = pid
    archivos['msapp/Properties.json'] = json.dumps(pr, ensure_ascii=False, indent=2).encode('utf-8')
    with zipfile.ZipFile(MSAPR + '.tmp', 'w', zipfile.ZIP_DEFLATED) as out:
        for n, data in archivos.items(): out.writestr(infos[n], data, compress_type=zipfile.ZIP_DEFLATED)
    os.replace(MSAPR + '.tmp', MSAPR)
print('Listo. Falta: paso7_desplegar.py --aplicar y paso3_reconciliar.py --aplicar')
