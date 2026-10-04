"""Fase 1 · Paso 1: cortar cascadas de compartido peligrosas + auditoría en tablas transaccionales.
Uso: python3 paso1_cascadas_auditoria.py [--aplicar]   (sin --aplicar solo muestra el plan)"""
import sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
P = 'sanic_cm_tbl_'
# (padre, hijo): compartir el padre NO debe compartir el hijo
CORTAR = [('cliente', 'cuenta'), ('cliente', 'cargadiaria'), ('cuenta', 'asignacion')]
AUDITAR = ['asignacion', 'interaccion', 'pago', 'agente', 'actualizaciondatos', 'cargadiaria']

rels = get("RelationshipDefinitions/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata?$select=SchemaName,ReferencedEntity,ReferencingEntity,MetadataId")['value']
for padre, hijo in CORTAR:
    r = [x for x in rels if x['ReferencedEntity'] == P + padre and x['ReferencingEntity'] == P + hijo]
    assert len(r) == 1, (padre, hijo, r)
    mid = r[0]['MetadataId']
    full = get(f"RelationshipDefinitions({mid})/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata")
    c = full['CascadeConfiguration']
    print(f"{padre}→{hijo} [{full['SchemaName']}] Share {c['Share']}→NoCascade · Unshare {c['Unshare']}→NoCascade (Assign {c['Assign']} y Delete {c['Delete']} sin cambio)")
    if APLICAR and (c['Share'] != 'NoCascade' or c['Unshare'] != 'NoCascade'):
        c['Share'] = 'NoCascade'; c['Unshare'] = 'NoCascade'
        full['@odata.type'] = 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata'
        for k in [k for k in full if k.startswith('@odata') and k != '@odata.type']: full.pop(k)
        st, b = rq('PUT', f"RelationshipDefinitions({mid})", full, {'MSCRM.MergeLabels': 'true'})
        print('   PUT →', st, '' if st < 300 else b)

for t in AUDITAR:
    e = get(f"EntityDefinitions(LogicalName='{P}{t}')")
    print(f"auditoría {t}: {e['IsAuditEnabled']['Value']} → True")
    if APLICAR and not e['IsAuditEnabled']['Value']:
        e['IsAuditEnabled']['Value'] = True
        for k in [k for k in e if k.startswith('@odata')]: e.pop(k)
        e['@odata.type'] = 'Microsoft.Dynamics.CRM.EntityMetadata'
        st, b = rq('PUT', f"EntityDefinitions({e['MetadataId']})", e, {'MSCRM.MergeLabels': 'true'})
        print('   PUT →', st, '' if st < 300 else b)

if APLICAR:
    ents = sorted({P + x for par in CORTAR for x in par} | {P + t for t in AUDITAR})
    xml = '<importexportxml><entities>' + ''.join(f'<entity>{x}</entity>' for x in ents) + '</entities></importexportxml>'
    st, b = rq('POST', 'PublishXml', {'ParameterXml': xml})
    print('PublishXml (solo estas tablas) →', st, '' if st < 300 else b)
