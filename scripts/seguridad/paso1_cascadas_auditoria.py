"""Corta cascadas peligrosas en las relaciones de CM y enciende la auditoría en las tablas transaccionales.
- Share, Unshare y Reparent dejan de propagarse del padre al hijo: con Reparent = Cascade, cada vez que la carga reescribe
  la cuenta, Dataverse da lectura heredada de la cuenta, su bóveda y sus asignaciones a quien tenga acceso al cliente.
- Delete = Restrict: borrar una interacción no borra sus pagos ni sus actualizaciones de datos, y borrar un cliente no
  borra su historial de carga diaria (hay que borrar los hijos antes).
- Assign = NoCascade en cuenta → asignación: la asignación tiene su propio dueño, el que pone RepartoAsignacion.
Uso: python3 paso1_cascadas_auditoria.py [--aplicar]   (sin --aplicar solo muestra el plan)"""
import sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
P = 'sanic_cm_tbl_'
# (padre, hijo): compartir el padre NO debe compartir el hijo, ni darle acceso heredado al re-escribirlo (Reparent)
CORTAR = [('cliente', 'cuenta'), ('cliente', 'cargadiaria'), ('cuenta', 'asignacion')]
AUDITAR = ['asignacion', 'interaccion', 'pago', 'agente', 'actualizaciondatos']
# La carga diaria no se audita: es la foto histórica que escribe Smelter (~6.000 altas por carga) y la auditoría
# duplicaría cada alta. Contra: un borrado de historia (solo Admin) no queda registrado en la auditoría.
SIN_AUDITAR = ['cargadiaria']
# (padre, hijo, {acción: valor})
OTRAS = [('interaccion', 'pago', {'Delete': 'Restrict'}),
         ('interaccion', 'actualizaciondatos', {'Delete': 'Restrict'}),
         ('cliente', 'cargadiaria', {'Delete': 'Restrict'}),
         ('cuenta', 'asignacion', {'Assign': 'NoCascade'})]

rels = get("RelationshipDefinitions/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata?$select=SchemaName,ReferencedEntity,ReferencingEntity,MetadataId")['value']
for padre, hijo in CORTAR:
    r = [x for x in rels if x['ReferencedEntity'] == P + padre and x['ReferencingEntity'] == P + hijo]
    assert len(r) == 1, (padre, hijo, r)
    mid = r[0]['MetadataId']
    full = get(f"RelationshipDefinitions({mid})/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata")
    c = full['CascadeConfiguration']
    print(f"{padre}→{hijo} [{full['SchemaName']}] Share {c['Share']}→NoCascade · Unshare {c['Unshare']}→NoCascade · Reparent {c['Reparent']}→NoCascade (Assign {c['Assign']} y Delete {c['Delete']} sin cambio)")
    if APLICAR and (c['Share'] != 'NoCascade' or c['Unshare'] != 'NoCascade' or c['Reparent'] != 'NoCascade'):
        c['Share'] = 'NoCascade'; c['Unshare'] = 'NoCascade'; c['Reparent'] = 'NoCascade'
        full['@odata.type'] = 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata'
        for k in [k for k in full if k.startswith('@odata') and k != '@odata.type']: full.pop(k)
        st, b = rq('PUT', f"RelationshipDefinitions({mid})", full, {'MSCRM.MergeLabels': 'true'})
        print('   PUT →', st, '' if st < 300 else b)

for padre, hijo, objetivo in OTRAS:
    r = [x for x in rels if x['ReferencedEntity'] == P + padre and x['ReferencingEntity'] == P + hijo]
    assert len(r) == 1, (padre, hijo, r)
    mid = r[0]['MetadataId']
    full = get(f"RelationshipDefinitions({mid})/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata")
    c = full['CascadeConfiguration']
    cambios = {k: v for k, v in objetivo.items() if c[k] != v}
    print(f"{padre}→{hijo} [{full['SchemaName']}] " + ' · '.join(f"{k} {c[k]}→{v}" for k, v in objetivo.items())
          + ('' if cambios else ' (ya está)'))
    if APLICAR and cambios:
        c.update(cambios)
        full['@odata.type'] = 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata'
        for k in [k for k in full if k.startswith('@odata') and k != '@odata.type']: full.pop(k)
        st, b = rq('PUT', f"RelationshipDefinitions({mid})", full, {'MSCRM.MergeLabels': 'true'})
        print('   PUT →', st, '' if st < 300 else b)

for t, destino in [(t, True) for t in AUDITAR] + [(t, False) for t in SIN_AUDITAR]:
    e = get(f"EntityDefinitions(LogicalName='{P}{t}')")
    print(f"auditoría {t}: {e['IsAuditEnabled']['Value']} → {destino}")
    if APLICAR and e['IsAuditEnabled']['Value'] != destino:
        e['IsAuditEnabled']['Value'] = destino
        for k in [k for k in e if k.startswith('@odata')]: e.pop(k)
        e['@odata.type'] = 'Microsoft.Dynamics.CRM.EntityMetadata'
        st, b = rq('PUT', f"EntityDefinitions({e['MetadataId']})", e, {'MSCRM.MergeLabels': 'true'})
        print('   PUT →', st, '' if st < 300 else b)

if APLICAR:
    ents = sorted({P + x for par in CORTAR for x in par} | {P + x for o in OTRAS for x in o[:2]} | {P + t for t in AUDITAR + SIN_AUDITAR})
    xml = '<importexportxml><entities>' + ''.join(f'<entity>{x}</entity>' for x in ents) + '</entities></importexportxml>'
    st, b = rq('POST', 'PublishXml', {'ParameterXml': xml})
    print('PublishXml (solo estas tablas) →', st, '' if st < 300 else b)
