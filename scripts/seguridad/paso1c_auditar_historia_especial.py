"""Historia de la Especial: audita las columnas "Solicitado por" y "Resolutor" de la asignación.
Al reactivar una Especial se reescribe la misma fila (clave alternativa NumeroCuentaxTipo) y sin auditoría de columna se
pierde quién la pidió y quién la resolvió antes. Solo metadatos de CM. Idempotente.
Uso: python3 paso1c_auditar_historia_especial.py [--aplicar]   (sin --aplicar solo muestra el plan)"""
import sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
E = 'sanic_cm_tbl_asignacion'
for a in ('sanic_cm_as_solicitadopor', 'sanic_cm_as_resolutorid'):
    m = get(f"EntityDefinitions(LogicalName='{E}')/Attributes(LogicalName='{a}')/Microsoft.Dynamics.CRM.LookupAttributeMetadata")
    print(f"{a}: auditoría de columna {m['IsAuditEnabled']['Value']} → True")
    if APLICAR and not m['IsAuditEnabled']['Value']:
        if not m['IsAuditEnabled'].get('CanBeChanged', True): print('   ✗ no se puede cambiar (managed property)'); continue
        m['IsAuditEnabled']['Value'] = True
        for k in [k for k in m if k.startswith('@odata')]: m.pop(k)
        m['@odata.type'] = 'Microsoft.Dynamics.CRM.LookupAttributeMetadata'
        st, b = rq('PUT', f"EntityDefinitions(LogicalName='{E}')/Attributes({m['MetadataId']})", m, {'MSCRM.MergeLabels': 'true'})
        print('   ', '✓' if st < 300 else f'✗ {st} {b}')
if APLICAR:
    st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{E}</entity></entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)
