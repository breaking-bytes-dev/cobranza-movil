"""F7 · Actualización de datos offline (opción B del dueño, aditivo e idempotente).
En el teléfono el lookup de la actualización a la interacción recién creada llega vacío y el plugin
ActualizacionConInteraccion la rechazaba. La app manda además la cuenta (ya existe en el servidor) y la misma
fecha de captura de la interacción; el plugin (1.0.8.6) la vincula a la interacción del mismo cobrador, cuenta y captura.
Crea: lookup sanic_cm_ad_cuentaid (→ cuenta, referencial) y sanic_cm_ad_fechacaptura (fecha y hora, como sanic_cm_in_fechacaptura).
Uso: python3 scripts/pan/paso14_actualizacion_offline.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq

APLICAR = '--aplicar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
P = 'sanic_cm_tbl_'
AD = P + 'actualizaciondatos'
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def existe_attr(e, a): return rq('GET', f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')?$select=LogicalName")[0] == 200
def hacer(desc, metodo, ruta, cuerpo):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
    if APLICAR:
        st, b = rq(metodo, ruta, cuerpo, SOL)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)

REFERENCIAL = {'Assign': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade', 'Reparent': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade'}
# 1) Lookup actualización → cuenta (un solo nivel de lookup, soportado offline: R12)
if existe_attr(AD, 'sanic_cm_ad_cuentaid'): print('YA EXISTE lookup sanic_cm_ad_cuentaid')
else:
    hacer(f'relación {P}cuenta → {AD} (sanic_cm_ad_cuentaid, referencial)', 'POST', 'RelationshipDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata',
        'SchemaName': 'sanic_cm_tbl_actualizaciondatos_ad_cuentaid_sanic_cm_tbl_cuenta',
        'ReferencedEntity': P + 'cuenta', 'ReferencingEntity': AD, 'CascadeConfiguration': REFERENCIAL,
        'Lookup': {'@odata.type': 'Microsoft.Dynamics.CRM.LookupAttributeMetadata', 'SchemaName': 'sanic_cm_ad_cuentaid',
                   'DisplayName': L('[AD] Cuenta'), 'RequiredLevel': {'Value': 'None'},
                   'Description': L('Cuenta de la visita. La pone el teléfono; el servidor la usa para vincular la interacción (F7).')}})
# 2) Fecha de captura (mismo formato y comportamiento que sanic_cm_in_fechacaptura)
if existe_attr(AD, 'sanic_cm_ad_fechacaptura'): print('YA EXISTE columna sanic_cm_ad_fechacaptura')
else:
    hacer(f'columna {AD}.sanic_cm_ad_fechacaptura', 'POST', f"EntityDefinitions(LogicalName='{AD}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.DateTimeAttributeMetadata', 'SchemaName': 'sanic_cm_ad_fechacaptura',
        'DisplayName': L('[AD] Fecha de captura'), 'RequiredLevel': {'Value': 'None'},
        'Description': L('Misma fecha de captura que la interacción (la pone el teléfono). El servidor la usa para vincular la interacción (F7).'),
        'Format': 'DateAndTime', 'DateTimeBehavior': {'Value': 'UserLocal'}, 'IsAuditEnabled': {'Value': True}})
if APLICAR:
    st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{AD}</entity><entity>{P}cuenta</entity></entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)
