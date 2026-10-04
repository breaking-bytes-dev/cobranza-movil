"""Fase 2 · Paso 1 (EXPANDIR, aditivo y reversible): esquema de la bóveda del PAN y claves sustitutas.
Ver docs/auditoria/fase2-pan-diseno.md §2.2. Idempotente: lo que ya existe se saltea.
Uso: python3 scripts/pan/paso1_esquema.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
P = 'sanic_cm_tbl_'
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def existe_entidad(n): return rq('GET', f"EntityDefinitions(LogicalName='{n}')?$select=LogicalName")[0] == 200
def existe_attr(e, a): return rq('GET', f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')?$select=LogicalName")[0] == 200
def existe_clave(e, k): return any(x['SchemaName'].lower() == k.lower() for x in get(f"EntityDefinitions(LogicalName='{e}')/Keys?$select=SchemaName")['value'])
def hacer(desc, metodo, ruta, cuerpo):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
    if APLICAR:
        st, b = rq(metodo, ruta, cuerpo, SOL)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)
def texto(schema, etiqueta, largo, desc, audit=True, buscable=True):
    return {'@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata', 'SchemaName': schema, 'DisplayName': L(etiqueta),
            'Description': L(desc), 'RequiredLevel': {'Value': 'None'}, 'MaxLength': largo, 'FormatName': {'Value': 'Text'},
            'IsAuditEnabled': {'Value': audit}, 'IsValidForAdvancedFind': {'Value': buscable}}

BOVEDA = P + 'cuentapan'
# 1) Tabla bóveda (nombre principal = enmascarado)
if not existe_entidad(BOVEDA):
    hacer('tabla sanic_cm_tbl_cuentapan (bóveda del PAN, UserOwned, change tracking, auditoría)', 'POST', 'EntityDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.EntityMetadata', 'SchemaName': 'sanic_cm_tbl_cuentapan',
        'DisplayName': L('CM | TBL | PAN de cuenta'), 'DisplayCollectionName': L('CM | TBL | PAN de cuentas'),
        'Description': L('Bóveda del PAN completo (PCI DSS). 1:1 con la cuenta. Solo la leen el cobrador de la cartera (compartido) y BackOffice.'),
        'OwnershipType': 'UserOwned', 'HasActivities': False, 'HasNotes': False, 'IsActivity': False,
        'ChangeTrackingEnabled': True, 'IsAuditEnabled': {'Value': True},
        'Attributes': [{**texto('sanic_cm_cp_name', 'Número (enmascarado)', 20, 'Últimos 4 dígitos con máscara: ****1234'), 'IsPrimaryName': True}]})
else: print('YA EXISTE tabla', BOVEDA)
# 2) Columnas de la bóveda
for a, args in [('sanic_cm_cp_pan', ('PAN', 19, 'PAN completo. Sin auditoría de columna, fuera de búsqueda avanzada.', False, False)),
                ('sanic_cm_cp_clavepan', ('Clave PAN (HMAC)', 64, 'HMAC-SHA256 del PAN en hex minúsculas. Clave de unión.', True, True))]:
    if not (existe_entidad(BOVEDA) and existe_attr(BOVEDA, a)):
        hacer(f'columna {BOVEDA}.{a}', 'POST', f"EntityDefinitions(LogicalName='{BOVEDA}')/Attributes", texto(a, *args))
    else: print('YA EXISTE', a)
# 3) Relación PARENTAL cuenta → bóveda (comparte/descomparte/asigna/borra en cascada)
def relacion(schema, padre, hijo, lookup, etiqueta, cascada, requerido):
    if existe_entidad(hijo) and existe_attr(hijo, lookup.lower()): print('YA EXISTE lookup', hijo, lookup); return
    hacer(f'relación {padre} → {hijo} ({lookup}, Share={cascada["Share"]})', 'POST', 'RelationshipDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata', 'SchemaName': schema,
        'ReferencedEntity': padre, 'ReferencingEntity': hijo, 'CascadeConfiguration': cascada,
        'Lookup': {'@odata.type': 'Microsoft.Dynamics.CRM.LookupAttributeMetadata', 'SchemaName': lookup,
                   'DisplayName': L(etiqueta), 'RequiredLevel': {'Value': requerido}}})
PARENTAL = {'Assign': 'Cascade', 'Delete': 'Cascade', 'Merge': 'Cascade', 'Reparent': 'Cascade', 'Share': 'Cascade', 'Unshare': 'Cascade'}
REFERENCIAL = {'Assign': 'NoCascade', 'Delete': 'RemoveLink', 'Merge': 'NoCascade', 'Reparent': 'NoCascade', 'Share': 'NoCascade', 'Unshare': 'NoCascade'}
relacion('sanic_cm_tbl_cuentapan_cp_cuentaid_sanic_cm_tbl_cuenta', P + 'cuenta', BOVEDA, 'sanic_cm_cp_cuentaid', 'Cuenta', PARENTAL, 'ApplicationRequired')
# 4) Columnas nuevas en tablas existentes
for e, a, args in [(P + 'cuenta', 'sanic_cm_ct_clavepan', ('Clave PAN (HMAC)', 64, 'HMAC-SHA256 del PAN. Clave de la carga.')),
                   (P + 'cuenta', 'sanic_cm_ct_ultimos4', ('Últimos 4', 4, 'Últimos 4 dígitos del PAN (búsqueda).')),
                   (P + 'asignacion', 'sanic_cm_as_clavepan', ('Clave PAN (HMAC)', 64, 'HMAC-SHA256 del PAN de la cuenta.')),
                   (P + 'cargadiaria', 'sanic_cm_cd_clavepan', ('Clave PAN (HMAC)', 64, 'HMAC-SHA256 del PAN de la cuenta.'))]:
    if not existe_attr(e, a): hacer(f'columna {e}.{a}', 'POST', f"EntityDefinitions(LogicalName='{e}')/Attributes", texto(a, *args))
    else: print('YA EXISTE', a)
# 5) Lookup pago → cuenta (offline solo admite un nivel de lookup: R12)
relacion('sanic_cm_tbl_pago_pg_cuentaid_sanic_cm_tbl_cuenta', P + 'cuenta', P + 'pago', 'sanic_cm_pg_cuentaid', 'Cuenta', REFERENCIAL, 'None')
# 6) Claves alternativas NUEVAS de la bóveda (tabla vacía). Las de cuenta/asignación/carga se crean en el paso 3b,
#    DESPUÉS del backfill: una clave única sobre una columna vacía en todas las filas puede hacer fallar el índice.
#    Las claves viejas sobre el PAN se borran recién en el paso 8 (contraer).
for e, k, cols, etiqueta in [(BOVEDA, 'sanic_cm_ak_cp_clavepan', ['sanic_cm_cp_clavepan'], 'Clave PAN'),
                             (BOVEDA, 'sanic_cm_ak_cp_cuentaid', ['sanic_cm_cp_cuentaid'], 'Una bóveda por cuenta')]:
    if existe_entidad(e) and existe_clave(e, k): print('YA EXISTE clave', k); continue
    hacer(f'clave {e}.{k} = {cols}', 'POST', f"EntityDefinitions(LogicalName='{e}')/Keys",
          {'SchemaName': k, 'DisplayName': L(etiqueta), 'KeyAttributes': cols})
# 7) Auditoría de LECTURA solo en la bóveda
if existe_entidad(BOVEDA):
    m = get(f"EntityDefinitions(LogicalName='{BOVEDA}')")
    v = lambda x: x.get('Value') if isinstance(x, dict) else bool(x)  # en la metadata son booleanos simples
    if not (v(m.get('IsRetrieveAuditEnabled')) and v(m.get('IsRetrieveMultipleAuditEnabled'))):
        for k in [k for k in m if k.startswith('@odata')]: m.pop(k)
        m['@odata.type'] = 'Microsoft.Dynamics.CRM.EntityMetadata'
        m['IsRetrieveAuditEnabled'] = True; m['IsRetrieveMultipleAuditEnabled'] = True
        hacer('auditoría de lectura (Retrieve y RetrieveMultiple) en la bóveda', 'PUT', f"EntityDefinitions({m['MetadataId']})", m)
    else: print('YA ACTIVA auditoría de lectura en la bóveda')
# 8) Offline móvil ("Can be taken offline"): sin esto el perfil autogenerado de la app del cobrador no baja la bóveda
if existe_entidad(BOVEDA):
    m = get(f"EntityDefinitions(LogicalName='{BOVEDA}')")
    if not m['IsOfflineInMobileClient']['Value']:
        for k in [k for k in m if k.startswith('@odata')]: m.pop(k)
        m['@odata.type'] = 'Microsoft.Dynamics.CRM.EntityMetadata'
        m['IsOfflineInMobileClient']['Value'] = True
        hacer('offline móvil en la bóveda (IsOfflineInMobileClient)', 'PUT', f"EntityDefinitions({m['MetadataId']})", m)
    else: print('YA ACTIVO offline móvil en la bóveda')
if APLICAR:
    ents = [BOVEDA, P + 'cuenta', P + 'asignacion', P + 'cargadiaria', P + 'pago']
    st, b = rq('POST', 'PublishXml', {'ParameterXml': '<importexportxml><entities>' + ''.join(f'<entity>{x}</entity>' for x in ents) + '</entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)
