"""APP-11: latitud y longitud de la interacción pasan de 2 decimales (~1 km) a 6 (~10 cm) y se acotan a rangos geográficos.
Los datos existentes no cambian (los decimales que ya se perdieron no vuelven). Idempotente.
Después: refrescar la fuente de la app (paso7_fuentes.py --tablas sanic_cm_tbl_interaccion --aplicar); si no, el
.msapr guarda la precisión vieja.
Uso: python3 scripts/localizador/precision_gps.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
E = 'sanic_cm_tbl_interaccion'
OBJ = {'sanic_cm_in_latitud': (-90, 90), 'sanic_cm_in_longitud': (-180, 180)}
PRECISION = 6

fuera = get(f"sanic_cm_tbl_interaccions?$select=sanic_cm_tbl_interaccionid&$filter=sanic_cm_in_latitud lt -90 or sanic_cm_in_latitud gt 90 "
            f"or sanic_cm_in_longitud lt -180 or sanic_cm_in_longitud gt 180")['value']
if fuera: raise SystemExit(f'✗ {len(fuera)} interacciones con coordenadas fuera de rango: corregirlas antes de acotar')
cambio = False
for col, (mn, mx) in OBJ.items():
    ruta = f"EntityDefinitions(LogicalName='{E}')/Attributes(LogicalName='{col}')/Microsoft.Dynamics.CRM.DecimalAttributeMetadata"
    m = get(ruta)
    if (m['Precision'], m['MinValue'], m['MaxValue']) == (PRECISION, mn, mx): print('YA ESTÁ', col); continue
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f"{col}: precisión {m['Precision']} → {PRECISION}, rango [{m['MinValue']}, {m['MaxValue']}] → [{mn}, {mx}]")
    if not APLICAR: continue
    for k in [k for k in m if k.startswith('@')]: m.pop(k)
    m['@odata.type'] = 'Microsoft.Dynamics.CRM.DecimalAttributeMetadata'
    m['Precision'], m['MinValue'], m['MaxValue'] = PRECISION, mn, mx
    st, b = rq('PUT', f"EntityDefinitions(LogicalName='{E}')/Attributes({m['MetadataId']})", m,
               {'MSCRM.MergeLabels': 'true', 'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'})
    if st >= 300: raise SystemExit(f'✗ {col}: {st} {str(b)[:300]}')
    print('   ✓', st); cambio = True
if cambio:
    print('publicar', rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{E}</entity></entities></importexportxml>'})[0])
if not APLICAR: print('Nada aplicado. Repetir con --aplicar.')
