"""Listas editables por el Admin (en vez de fijas en las apps).
Crea la tabla sanic_cm_tbl_opcionlista ("CM | TBL | Opciones de lista"), sus privilegios y las opciones iniciales (los valores
que las apps tenían escritos), todo idempotente. Por defecto solo muestra el plan; aplica con --aplicar.
  - Lista (choice local): 1 Tipo de Especial (Nueva solicitud), 2 Motivo de cierre Incobrable y 3 Motivo de cierre Especial
    (Cierres), 4 Mensaje rápido (Registrar visita del localizador). El TipoEspecial que manda el banco en el archivo NO
    pasa por esta lista: entra tal como viene.
  - Texto (nombre principal, 100), Orden (la primera activa es la opción por defecto), Activa (no se borra: se desactiva;
    lo elegido se guarda como texto en la asignación o la interacción, así que renombrar no cambia el historial).
  - Tabla de la organización (OrganizationOwned): la leen todos los roles de CM; Create y Write solo Admin y Servicio; nadie
    tiene Delete. Auditoría encendida.
Después: paso7_perfil_offline.py --aplicar (el teléfono la baja completa) y paso7_fuentes.py en las dos apps.
Uso: python3 scripts/pan/paso23_listas.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
T = 'sanic_cm_tbl_opcionlista'
ES = 'sanic_cm_tbl_opcionlistas'
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def existe_entidad(n): return rq('GET', f"EntityDefinitions(LogicalName='{n}')?$select=LogicalName")[0] == 200
def existe_attr(e, a): return rq('GET', f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')?$select=LogicalName")[0] == 200
def hacer(desc, metodo, ruta, cuerpo, headers=SOL):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
    if APLICAR:
        st, b = rq(metodo, ruta, cuerpo, headers)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)

LISTAS = {1: 'Tipo de Especial', 2: 'Motivo de cierre Incobrable', 3: 'Motivo de cierre Especial', 4: 'Mensaje rápido'}
INICIALES = {
    1: ['Retiro urgente', 'Cobro especial', 'Verificación'],
    2: ['Recuperada', 'Baja / castigada', 'Fuera de cartera'],
    3: ['Gestión completada', 'Ya no aplica', 'Solicitud errónea'],
    4: ['Promesa de pago', 'No localizado', 'Cliente pagó', 'No tiene dinero hoy', 'Negocio cerrado', 'Pidió arreglo'],
}

# 1) Tabla
if not existe_entidad(T):
    hacer(f'tabla {T} (OrganizationOwned, auditoría, change tracking)', 'POST', 'EntityDefinitions', {
        '@odata.type': 'Microsoft.Dynamics.CRM.EntityMetadata', 'SchemaName': T,
        'DisplayName': L('CM | TBL | Opción de lista'), 'DisplayCollectionName': L('CM | TBL | Opciones de lista'),
        'Description': L('APP-49: opciones de las listas de las apps (tipo de Especial, motivos de cierre, mensajes rápidos). Las edita el Admin.'),
        'OwnershipType': 'OrganizationOwned', 'HasActivities': False, 'HasNotes': False, 'IsActivity': False,
        'ChangeTrackingEnabled': True, 'IsAuditEnabled': {'Value': True},
        'Attributes': [{'@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata', 'SchemaName': 'sanic_cm_ol_texto',
                        'DisplayName': L('[OL] Texto'), 'Description': L('Texto que muestra la app (hasta 100 caracteres).'),
                        'RequiredLevel': {'Value': 'ApplicationRequired'}, 'MaxLength': 100, 'FormatName': {'Value': 'Text'},
                        'IsAuditEnabled': {'Value': True}, 'IsPrimaryName': True}]})
else: print('YA EXISTE tabla', T)

# 2) Columnas
COLS = [
    ('sanic_cm_ol_lista', {'@odata.type': 'Microsoft.Dynamics.CRM.PicklistAttributeMetadata', 'SchemaName': 'sanic_cm_ol_lista',
        'DisplayName': L('[OL] Lista'), 'Description': L('A qué lista pertenece la opción.'), 'RequiredLevel': {'Value': 'ApplicationRequired'},
        'IsAuditEnabled': {'Value': True},
        'OptionSet': {'@odata.type': 'Microsoft.Dynamics.CRM.OptionSetMetadata', 'IsGlobal': False, 'OptionSetType': 'Picklist',
                      'Options': [{'Value': v, 'Label': L(n)} for v, n in LISTAS.items()]}}),
    ('sanic_cm_ol_orden', {'@odata.type': 'Microsoft.Dynamics.CRM.IntegerAttributeMetadata', 'SchemaName': 'sanic_cm_ol_orden',
        'DisplayName': L('[OL] Orden'), 'Description': L('Orden en la lista; la primera activa es la opción por defecto.'),
        'RequiredLevel': {'Value': 'None'}, 'MinValue': 0, 'MaxValue': 9999, 'Format': 'None', 'IsAuditEnabled': {'Value': True}}),
    ('sanic_cm_ol_activa', {'@odata.type': 'Microsoft.Dynamics.CRM.BooleanAttributeMetadata', 'SchemaName': 'sanic_cm_ol_activa',
        'DisplayName': L('[OL] Activa'), 'Description': L('Las apps muestran solo las activas. No se borra: se desactiva.'),
        'RequiredLevel': {'Value': 'None'}, 'DefaultValue': True, 'IsAuditEnabled': {'Value': True},
        'OptionSet': {'@odata.type': 'Microsoft.Dynamics.CRM.BooleanOptionSetMetadata',
                      'TrueOption': {'Value': 1, 'Label': L('Sí')}, 'FalseOption': {'Value': 0, 'Label': L('No')}}}),
]
for a, cuerpo in COLS:
    if existe_entidad(T) and existe_attr(T, a): print('YA EXISTE', a); continue
    hacer(f'columna {a}', 'POST', f"EntityDefinitions(LogicalName='{T}')/Attributes", cuerpo)
if APLICAR:
    st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{T}</entity></entities></importexportxml>'})
    print('publicar', '✓' if st < 300 else f'✗ {st} {b}')

# 3) Privilegios: Read para todos los roles de CM; Create/Write solo Admin y Servicio; Delete nadie.
LEEN = ['SR | CM | Admin', 'SR | CM | Servicio - Cobranza', 'SR | CM | Localizador', 'SR | CM | BackOffice',
        'SR | CM | Solicitante', 'SR | CM | Supervisor']
ESCRIBEN = ['SR | CM | Admin', 'SR | CM | Servicio - Cobranza']
def priv(n):
    v = get(f"privileges?$select=privilegeid&$filter=name eq '{n}'")['value']
    return v[0]['privilegeid'] if v else None
if existe_entidad(T):
    pr = {k: priv(f'prv{k}{T}') for k in ('Read', 'Create', 'Write', 'Append', 'AppendTo')}
    for rol in LEEN:
        rid = get(f"roles?$select=roleid&$filter=name eq '{rol}' and _parentroleid_value eq null")['value'][0]['roleid']
        actual = {p['PrivilegeId']: p['Depth'] for p in get(f'RetrieveRolePrivilegesRole(RoleId={rid})')['RolePrivileges']}
        quiere = ['Read'] + (['Create', 'Write', 'Append', 'AppendTo'] if rol in ESCRIBEN else [])
        falta = [k for k in quiere if actual.get(pr[k]) != 'Global']
        if not falta: print(f'ok      {rol}: {", ".join(quiere)}'); continue
        hacer(f'{rol}: {", ".join(falta)} (Global)', 'POST', f'roles({rid})/Microsoft.Dynamics.CRM.AddPrivilegesRole',
              {'Privileges': [{'PrivilegeId': pr[k], 'BusinessUnitId': None, 'Depth': 'Global'} for k in falta]}, {})
else: print('PLAN    privilegios (después de crear la tabla)')

# 4) Opciones iniciales (por lista y texto; las que ya existen no se tocan)
hay = set()
if existe_entidad(T):
    hay = {(r['sanic_cm_ol_lista'], r['sanic_cm_ol_texto']) for r in get(f'{ES}?$select=sanic_cm_ol_lista,sanic_cm_ol_texto')['value']}
for lista, textos in INICIALES.items():
    for i, tx in enumerate(textos):
        if (lista, tx) in hay: continue
        hacer(f'opción {LISTAS[lista]} · {tx}', 'POST', ES,
              {'sanic_cm_ol_lista': lista, 'sanic_cm_ol_texto': tx, 'sanic_cm_ol_orden': (i + 1) * 10, 'sanic_cm_ol_activa': True}, {})
print('listo.' if APLICAR else 'plan (sin cambios). Aplicar con --aplicar.')
