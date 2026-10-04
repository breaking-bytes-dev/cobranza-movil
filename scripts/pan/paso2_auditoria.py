"""Fase 2 · Paso 2: apaga la auditoría de COLUMNA de los campos que hoy guardan el PAN en claro, para que el
enmascarado del paso 8 no deje el PAN como oldValue en la auditoría (R19). La auditoría de TABLA sigue encendida.
Paso 11: --revertir la vuelve a encender en ct_numerodecuenta y cd_numerocuenta (ya enmascaradas). cp_pan NUNCA.
Uso: python3 scripts/pan/paso2_auditoria.py [--aplicar | --revertir]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

REVERTIR = '--revertir' in sys.argv; APLICAR = '--aplicar' in sys.argv or REVERTIR
P = 'sanic_cm_tbl_'
COLS = [(P + 'cuenta', 'sanic_cm_ct_numerodecuenta'), (P + 'cargadiaria', 'sanic_cm_cd_numerocuenta'),
        (P + 'asignacion', 'sanic_cm_as_numerocuenta'), (P + 'interaccion', 'sanic_cm_in_numerocuenta'),
        (P + 'pago', 'sanic_cm_pg_numerocuenta')]
if REVERTIR: COLS = COLS[:2]  # solo las que quedan enmascaradas; las demás se eliminan en el paso 8
destino = REVERTIR
for e, a in COLS:
    m = get(f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')/Microsoft.Dynamics.CRM.StringAttributeMetadata")
    actual = m['IsAuditEnabled']['Value']
    print(f'{e}.{a}: auditoría de columna {actual} → {destino}')
    if APLICAR and actual != destino:
        if not m['IsAuditEnabled'].get('CanBeChanged', True): print('   ✗ no se puede cambiar (managed property)'); continue
        m['IsAuditEnabled']['Value'] = destino
        for k in [k for k in m if k.startswith('@odata')]: m.pop(k)
        m['@odata.type'] = 'Microsoft.Dynamics.CRM.StringAttributeMetadata'
        st, b = rq('PUT', f"EntityDefinitions(LogicalName='{e}')/Attributes({m['MetadataId']})", m, {'MSCRM.MergeLabels': 'true'})
        print('   ', '✓' if st < 300 else f'✗ {st} {b}')
if APLICAR:
    ents = sorted({e for e, _ in COLS})
    st, b = rq('POST', 'PublishXml', {'ParameterXml': '<importexportxml><entities>' + ''.join(f'<entity>{x}</entity>' for x in ents) + '</entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)
