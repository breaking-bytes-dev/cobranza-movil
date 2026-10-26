"""Claves alternativas nuevas sobre la clave HMAC (después del backfill).
Los nulos no se cuentan para la unicidad (doc "Define alternate keys"), pero un duplicado real hace fallar el índice:
la de asignación espera a que se resuelvan los duplicados por (clave, tipo).
Uso: python3 scripts/pan/paso3b_claves.py [--aplicar] [--incluir-asignacion]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get
APLICAR = '--aplicar' in sys.argv
P = 'sanic_cm_tbl_'
L = lambda t: {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
CLAVES = [(P + 'cuenta', 'sanic_cm_ak_ct_clavepan', ['sanic_cm_ct_clavepan'], 'Clave PAN'),
          (P + 'cargadiaria', 'sanic_cm_ak_cd_clavepan_fecha', ['sanic_cm_cd_clavepan', 'sanic_cm_cd_fechacarga'], 'Clave PAN + fecha de carga')]
if '--incluir-asignacion' in sys.argv:
    CLAVES.append((P + 'asignacion', 'sanic_cm_ak_as_clavepan_tipo', ['sanic_cm_as_clavepan', 'sanic_cm_as_tipo'], 'Clave PAN + tipo'))
for e, k, cols, et in CLAVES + [(P + 'cuentapan', 'sanic_cm_ak_cp_clavepan', None, None), (P + 'cuentapan', 'sanic_cm_ak_cp_cuentaid', None, None)]:
    ex = {x['SchemaName'].lower(): x['EntityKeyIndexStatus'] for x in get(f"EntityDefinitions(LogicalName='{e}')/Keys?$select=SchemaName,EntityKeyIndexStatus")['value']}
    if k.lower() in ex: print(f'{k}: {ex[k.lower()]}'); continue
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f'clave {e}.{k} = {cols}')
    if APLICAR:
        st, b = rq('POST', f"EntityDefinitions(LogicalName='{e}')/Keys", {'SchemaName': k, 'DisplayName': L(et), 'KeyAttributes': cols},
                   {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'})
        print('   ', '✓' if st < 300 else f'✗ {st} {b}')
