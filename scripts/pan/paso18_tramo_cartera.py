"""Escala de la cartera · tramo de la asignación (aditivo e idempotente).
La app copia la cartera a colecciones y cada copia se corta en el límite de filas (2000): con 2.340 asignaciones el
teléfono armaba la Cartera con trozos distintos cada vez (prueba de estrés del 2026-10-21). Con el tramo, la app la trae
en partes: Filter(Asignaciones, Tramo = n), n = 0..19, cada una muy por debajo del límite.

Crea sanic_cm_tbl_asignacion.sanic_cm_as_tramo "[AS] Tramo" (entero 0..19). Lo pone TramoCartera (plugins 1.0.9.3) al
crear; con --rellenar completa las asignaciones que ya existen con la MISMA cuenta → mismo tramo que el plugin:
  tramo = FNV-1a de 32 bits de los 16 bytes del id de la cuenta (orden .NET: uuid.UUID(id).bytes_le) % 20
  (los ids de Dataverse son secuenciales: tomar sus primeros bytes repartía de 228 a 410 por tramo)
Va ANTES de paso5. Imprime solo conteos.
Uso: python3 scripts/pan/paso18_tramo_cartera.py [--aplicar] [--rellenar]"""
import os, sys, collections, concurrent.futures, uuid
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq

APLICAR = '--aplicar' in sys.argv
RELLENAR = '--rellenar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
TABLA, COL, TRAMOS = 'sanic_cm_tbl_asignacion', 'sanic_cm_as_tramo', 20
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def tramo(cuenta_id):
    h = 2166136261
    for b in uuid.UUID(cuenta_id).bytes_le:
        h = ((h ^ b) * 16777619) & 0xFFFFFFFF
    return h % TRAMOS

st, _ = rq('GET', f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='{COL}')?$select=LogicalName")
if st == 200:
    print('YA EXISTE columna', COL)
else:
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f'columna {TABLA}.{COL} (entero 0..{TRAMOS - 1})')
    if APLICAR:
        st, b = rq('POST', f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", {
            '@odata.type': 'Microsoft.Dynamics.CRM.IntegerAttributeMetadata', 'SchemaName': COL,
            'MinValue': 0, 'MaxValue': TRAMOS - 1, 'Format': 'None', 'DisplayName': L('[AS] Tramo'),
            'RequiredLevel': {'Value': 'None'},
            'Description': L('Tramo 0..19 de la cartera, calculado de la cuenta (TramoCartera). La app trae las asignaciones por tramos, cada uno bajo el límite de 2000 filas.')}, SOL)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)
        st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{TABLA}</entity></entities></importexportxml>'})
        print('PublishXml →', st, '' if st < 300 else b)

if RELLENAR:
    filas, url = [], f"{TABLA}s?$select=_sanic_cm_as_cuentaid_value,{COL}&$filter=_sanic_cm_as_cuentaid_value ne null"
    while url:
        st, b = rq('GET', url, headers={'Prefer': 'odata.maxpagesize=5000'})
        if st >= 300: raise SystemExit(f'✗ GET {st}')
        filas += b['value']; url = b.get('@odata.nextLink')
    faltan = [f for f in filas if f.get(COL) is None]
    print(f'relleno: {len(filas)} asignaciones con cuenta · sin tramo: {len(faltan)}')
    if APLICAR and faltan:
        def uno(f):
            return rq('PATCH', f"{TABLA}s({f['sanic_cm_tbl_asignacionid']})", {COL: tramo(f['_sanic_cm_as_cuentaid_value'])})[0]
        with concurrent.futures.ThreadPoolExecutor(8) as ex:
            res = collections.Counter(s < 300 for s in ex.map(uno, faltan))
        print(f'   completadas: {res[True]} · errores: {res[False]}')
    reparto = collections.Counter(tramo(f['_sanic_cm_as_cuentaid_value']) for f in filas)
    print('   por tramo: mín', min(reparto.values()), '· máx', max(reparto.values()))
