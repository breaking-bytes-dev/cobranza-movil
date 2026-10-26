"""Las columnas viejas con el número de cuenta que las apps ya no escriben pasan a opcionales.
as_numerocuenta e in_numerocuenta eran ApplicationRequired: Power Apps lo valida en el Patch del cliente (también offline),
así que la Especial del backoffice y la interacción del localizador fallaban ("Field ... is required"). Se borran en el paso 8.
Después hay que refrescar asignación e interacción en los .msapr (paso7_fuentes.py): la obligatoriedad viaja en la metadata.
Uso: python3 scripts/pan/paso7_opcionales.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get
APLICAR = '--aplicar' in sys.argv
ents = []
for e, a in [('sanic_cm_tbl_asignacion', 'sanic_cm_as_numerocuenta'), ('sanic_cm_tbl_interaccion', 'sanic_cm_in_numerocuenta')]:
    m = get(f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')/Microsoft.Dynamics.CRM.StringAttributeMetadata")
    print(f"{a}: {m['RequiredLevel']['Value']} → None")
    if APLICAR and m['RequiredLevel']['Value'] != 'None':
        m['RequiredLevel']['Value'] = 'None'
        for k in [k for k in m if k.startswith('@odata')]: m.pop(k)
        m['@odata.type'] = 'Microsoft.Dynamics.CRM.StringAttributeMetadata'
        st, b = rq('PUT', f"EntityDefinitions(LogicalName='{e}')/Attributes({m['MetadataId']})", m, {'MSCRM.MergeLabels': 'true'})
        print('   ', '✓' if st < 300 else f'✗ {st} {b}'); ents.append(e)
if ents:
    st, b = rq('POST', 'PublishXml', {'ParameterXml': '<importexportxml><entities>' + ''.join(f'<entity>{x}</entity>' for x in ents) + '</entities></importexportxml>'})
    print('PublishXml →', st)
