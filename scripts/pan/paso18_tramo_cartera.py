"""Tramo de la asignación, la cuenta y el pago, para que la app traiga la cartera por partes (aditivo e idempotente).
La app copia la cartera a colecciones y cada copia se corta en el límite de filas (2000): por encima, el teléfono arma la
Cartera con trozos distintos cada vez. Con el tramo, la app la trae en partes: Filter(Asignaciones, Tramo = n) y
Filter(Cuentas, Tramo = n), n = 0..19, cada una muy por debajo del límite.

Crea "[AS] Tramo" (sanic_cm_as_tramo), "[CT] Tramo" (sanic_cm_ct_tramo) y "[PG] Tramo" (sanic_cm_pg_tramo), enteros
0..19. Los pone el plugin TramoCartera al crear; el pago usa el id de su INTERACCIÓN (Transcritas trae los pagos del día
por tramos); con --rellenar completa los que ya existen con el MISMO cálculo que el plugin:
  tramo = FNV-1a de 32 bits de los 16 bytes del id de la CUENTA (orden .NET: uuid.UUID(id).bytes_le) % 20
La asignación usa el id de su cuenta, así cuenta y asignaciones caen en el mismo tramo. Se hashea el id completo porque los
ids de Dataverse son secuenciales y sus primeros bytes reparten muy desparejo.
Va ANTES de paso5. Imprime solo conteos.
Uso: python3 scripts/pan/paso18_tramo_cartera.py [--aplicar] [--rellenar]"""
import os, sys, collections, concurrent.futures, uuid
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq

APLICAR = '--aplicar' in sys.argv
RELLENAR = '--rellenar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
TRAMOS = 20
# (tabla, columna, etiqueta, columna del id de la cuenta en la tabla)
COLUMNAS = [('sanic_cm_tbl_asignacion', 'sanic_cm_as_tramo', '[AS] Tramo', '_sanic_cm_as_cuentaid_value'),
            ('sanic_cm_tbl_cuenta', 'sanic_cm_ct_tramo', '[CT] Tramo', 'sanic_cm_tbl_cuentaid'),
            ('sanic_cm_tbl_pago', 'sanic_cm_pg_tramo', '[PG] Tramo', '_sanic_cm_pg_interaccion_value')]
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def tramo(cuenta_id):
    h = 2166136261
    for b in uuid.UUID(cuenta_id).bytes_le:
        h = ((h ^ b) * 16777619) & 0xFFFFFFFF
    return h % TRAMOS

for tabla, col, etiqueta, _ in COLUMNAS:
    st, _b = rq('GET', f"EntityDefinitions(LogicalName='{tabla}')/Attributes(LogicalName='{col}')?$select=LogicalName")
    if st == 200:
        print('YA EXISTE columna', col)
        continue
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f'columna {tabla}.{col} (entero 0..{TRAMOS - 1})')
    if APLICAR:
        st, b = rq('POST', f"EntityDefinitions(LogicalName='{tabla}')/Attributes", {
            '@odata.type': 'Microsoft.Dynamics.CRM.IntegerAttributeMetadata', 'SchemaName': col,
            'MinValue': 0, 'MaxValue': TRAMOS - 1, 'Format': 'None', 'DisplayName': L(etiqueta),
            'RequiredLevel': {'Value': 'None'},
            'Description': L('Tramo 0..19 calculado del id de la cuenta (o de la interacción, en el pago) por TramoCartera. La app trae los datos por tramos, cada uno bajo el límite de 2000 filas.')}, SOL)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)
        st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>'})
        print('PublishXml →', st, '' if st < 300 else b)

if RELLENAR:
    for tabla, col, _, cuenta in COLUMNAS:
        st, _b = rq('GET', f"EntityDefinitions(LogicalName='{tabla}')/Attributes(LogicalName='{col}')?$select=LogicalName")
        if st != 200:
            print(f'relleno {tabla}: falta la columna (correr con --aplicar)'); continue
        filas, url = [], f"{tabla}s?$select={cuenta},{col}&$filter={cuenta} ne null"
        while url:
            st, b = rq('GET', url, headers={'Prefer': 'odata.maxpagesize=5000'})
            if st >= 300: raise SystemExit(f'✗ GET {st}')
            filas += b['value']; url = b.get('@odata.nextLink')
        faltan = [f for f in filas if f.get(col) is None]
        print(f'relleno {tabla}: {len(filas)} filas · sin tramo: {len(faltan)}')
        if APLICAR and faltan:
            def uno(f):
                return rq('PATCH', f"{tabla}s({f[tabla + 'id']})", {col: tramo(f[cuenta])})[0]
            with concurrent.futures.ThreadPoolExecutor(8) as ex:
                res = collections.Counter(s < 300 for s in ex.map(uno, faltan))
            print(f'   completadas: {res[True]} · errores: {res[False]}')
        reparto = collections.Counter(tramo(f[cuenta]) for f in filas)
        print('   por tramo: mín', min(reparto.values()), '· máx', max(reparto.values()))
