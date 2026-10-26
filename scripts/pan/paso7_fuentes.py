"""Agrega o refresca fuentes de datos de Dataverse dentro de un .msapr (formato SourceCode de pac canvas),
como hace Studio con "Agregar datos" / "Actualizar". Hace falta porque las fuentes NO están en los .pa.yaml: viven en
msapp/References/DataSources.json, msapp/Properties.json (LocalDatabaseReferences) y msapp/References/QualifiedValues.json.

TableDefinition guarda las respuestas crudas de la Web API v9.0 que pidió Studio (cada parte trae su @odata.context);
este script repite esas mismas consultas. Para validar la réplica: --comparar <tabla> regenera una fuente existente y muestra
las diferencias con lo que generó Studio (solo deberían aparecer columnas y relaciones nuevas).

Uso:
  python3 scripts/pan/paso7_fuentes.py <archivo.msapr> --comparar sanic_cm_tbl_cuenta
  python3 scripts/pan/paso7_fuentes.py <archivo.msapr> --tablas sanic_cm_tbl_cuentapan,sanic_cm_tbl_cuenta [--aplicar]"""
import json, os, sys, urllib.parse, zipfile
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
import dvlib
from dvlib import rq

V9 = dvlib.U + '/api/data/v9.0/'
EXCLUIR_1N = ('_DeletedItemReferences', '_UserEntityInstanceDatas')  # Studio no las mapea (verificado contra cuenta)

def crudo(ruta):
    st, b = rq('GET', V9 + urllib.parse.quote(ruta, safe="/?&=$(),@'.:-_"), raw=True)
    if st >= 300: raise SystemExit(f'GET {ruta[:90]} → {st}: {b}')
    return b.decode('utf-8')

def definicion(t):
    em = crudo(f"EntityDefinitions(LogicalName='{t}')?$select=MetadataId,LogicalName,DisplayCollectionName,EntitySetName,Description,"
               "HasNotes,IsActivity,IsIntersect,IsManaged,TableType,IsPrivate,IsLogicalEntity,PrimaryIdAttribute,PrimaryNameAttribute,"
               "ObjectTypeCode,OwnershipType,Privileges,IsAvailableOffline,IsOfflineInMobileClient"
               "&$expand=Attributes,ManyToOneRelationships,OneToManyRelationships,ManyToManyRelationships")
    td = {'TableName': t, 'EntityMetadata': em}
    for clave, tipo in [('PicklistOptionSetAttribute', 'PicklistAttributeMetadata'), ('MultiSelectPicklistOptionSetAttribute', 'MultiSelectPicklistAttributeMetadata'),
                        ('StateOptionSetAttribute', 'StateAttributeMetadata'), ('StatusOptionSetAttribute', 'StatusAttributeMetadata'),
                        ('BooleanOptionSetAttribute', 'BooleanAttributeMetadata'), ('EntityNameOptionSetAttribute', 'EntityNameAttributeMetadata')]:
        td[clave] = crudo(f"EntityDefinitions(LogicalName='{t}')/Attributes/Microsoft.Dynamics.CRM.{tipo}?$select=MetadataId,LogicalName,SchemaName,DisplayName&$expand=OptionSet")
    td['Views'] = crudo(f"savedqueries?$select=savedqueryid,name,querytype&$filter=returnedtypecode eq '{t}' and statecode eq 0 and "
                        "(querytype eq 0 or querytype eq 1 or querytype eq 2 or querytype eq 64)")
    td['DefaultPublicView'] = crudo(f"savedqueries?$select=layoutxml&$filter=returnedtypecode eq '{t}' and isdefault eq true and querytype eq 0")
    td['IconUrl'] = ''
    td['Forms'] = crudo(f"systemforms?$select=objecttypecode,formid,name,type&$filter=objecttypecode eq '{t}' and (type eq 2 or type eq 6 or type eq 11)")
    return td

def etiqueta(x): return ((x or {}).get('UserLocalizedLabel') or {}).get('Label') or ''

def mapa_nombres(em):
    m = {}
    for a in em['Attributes']:
        if a['LogicalName'] == 'ownerid': m['_ownerid_value'] = etiqueta(a['DisplayName'])
        elif not a.get('AttributeOf'): m[a['LogicalName']] = etiqueta(a['DisplayName'])
    for r in em['OneToManyRelationships']:
        if r['SchemaName'].endswith(EXCLUIR_1N): continue
        m[r['SchemaName']] = etiqueta(json.loads(crudo(f"EntityDefinitions(LogicalName='{r['ReferencingEntity']}')?$select=DisplayCollectionName"))['DisplayCollectionName'])
    return m

def familia(t):
    """Entradas de DataSources.json para la tabla t: (tabla, [option sets de estado], vistas), más forms y la referencia."""
    td = definicion(t); em = json.loads(td['EntityMetadata']); nombre = etiqueta(em['DisplayCollectionName'])
    tabla = {'Name': nombre, 'IsSampleData': False, 'IsWritable': True, 'Type': 'NativeCDSDataSourceInfo', 'DatasetName': 'default.cds',
             'EntitySetName': em['EntitySetName'], 'LogicalName': t, 'PreferredName': nombre, 'IsHidden': False, 'MaxGetRowsCount': 500,
             'CdsFCBContext': '{"FileType":true,"ImageExternalStorage":true}', 'TableDefinition': json.dumps(td, ensure_ascii=False, separators=(',', ':')),
             'NativeCDSDataSourceInfoNameMapping': mapa_nombres(em)}
    opciones = []
    for col, clave, tipo, desc in [('statecode', 'StateOptionSetAttribute', 'StateType', 'Status'), ('statuscode', 'StatusOptionSetAttribute', 'StatusType', 'Status Reason')]:
        os_ = json.loads(td[clave])['value'][0]['OptionSet']['Options']
        opciones.append({'Type': 'OptionSetInfo', 'Name': f'{t}_{col}', 'DatasetName': 'default.cds', 'DisplayName': f'{desc} ({nombre})',
                         'RelatedEntityName': nombre, 'RelatedColumnInvariantName': col,
                         'OptionSetInfoNameMapping': {str(o['Value']): etiqueta(o['Label']) for o in os_}, 'OptionSetIsGlobal': False,
                         'OptionSetIsBooleanValued': False, 'OptionSetTypeKey': tipo,
                         'OptionSetReference': {'OptionSetReferenceItem0': {'OptionSetReferenceEntityName': nombre, 'OptionSetReferenceColumnName': col}}})
    # Picklist y Boolean de la tabla (Studio los registra como OptionSetInfo; los de estado ya están arriba). Nombre local = '<tabla>_<columna>';
    # los globales ya existen si otra tabla los usa (se actualiza su referencia sin duplicar la entrada).
    for clave, tipo, booleano in [('PicklistOptionSetAttribute', 'PicklistType', False), ('BooleanOptionSetAttribute', 'BooleanType', True)]:
        for a in json.loads(td[clave])['value']:
            os_ = a['OptionSet']; col = a['LogicalName']
            if not os_: continue
            nombre_os = os_.get('Name') or f'{t}_{col}'
            if os_.get('IsGlobal'): visible = etiqueta(os_.get('DisplayName'))
            else: visible = f"{etiqueta(a['DisplayName'])} ({nombre})"
            mapa = ({'1': etiqueta(os_['TrueOption']['Label']), '0': etiqueta(os_['FalseOption']['Label'])} if booleano
                    else {str(o['Value']): etiqueta(o['Label']) for o in os_['Options']})
            opciones.append({'Type': 'OptionSetInfo', 'Name': nombre_os, 'DatasetName': 'default.cds', 'DisplayName': visible,
                             'RelatedEntityName': nombre, 'RelatedColumnInvariantName': col, 'OptionSetInfoNameMapping': mapa,
                             'OptionSetIsGlobal': bool(os_.get('IsGlobal')), 'OptionSetIsBooleanValued': booleano, 'OptionSetTypeKey': tipo,
                             'OptionSetReference': {'OptionSetReferenceItem0': {'OptionSetReferenceEntityName': nombre, 'OptionSetReferenceColumnName': col}}})
    vistas = {'Type': 'ViewInfo', 'Name': f'default.cds_{t}_views', 'DisplayName': f'{nombre} (Views)', 'RelatedEntityName': nombre,
              'ViewInfoNameMapping': {v['savedqueryid']: v['name'] for v in json.loads(td['Views'])['value']}}
    principal = [f for f in json.loads(td['Forms'])['value'] if f['type'] == 2]
    forms = {'Namespace': f'{nombre} (Forms)', 'Kind': f'default.cds_{t}_forms', 'Values': {f['name']: f['formid'] for f in principal}}
    return tabla, opciones, vistas, forms, em

def comparar(ds, t):
    tabla, *_ = familia(t)
    viejo = next(d for d in ds if d.get('LogicalName') == t and d['Type'] == 'NativeCDSDataSourceInfo')
    a, b = viejo['NativeCDSDataSourceInfoNameMapping'], tabla['NativeCDSDataSourceInfoNameMapping']
    print(f'{t}: mapa de nombres  solo en Studio: {sorted(set(a) - set(b))}  solo nuevo: {sorted(set(b) - set(a))}  '
          f'valores distintos: {[k for k in set(a) & set(b) if a[k] != b[k]]}')
    tv, tn = json.loads(viejo['TableDefinition']), json.loads(tabla['TableDefinition'])
    print('  partes de TableDefinition iguales en claves:', list(tv) == list(tn))
    ev, en = json.loads(tv['EntityMetadata']), json.loads(tn['EntityMetadata'])
    print('  claves de EntityMetadata:', sorted(set(ev) ^ set(en)) or 'iguales', '| columnas', len(ev['Attributes']), '→', len(en['Attributes']),
          '| claves de columna distintas:', sorted(set().union(*map(set, ev['Attributes'])) ^ set().union(*map(set, en['Attributes']))) or 'ninguna')
    for k in ['Views', 'DefaultPublicView', 'Forms']:
        sin_etag = lambda xs: sorted(json.dumps({c: v for c, v in x.items() if c != '@odata.etag'}, sort_keys=True) for x in xs)
        vv, vn = json.loads(tv[k])['value'], json.loads(tn[k])['value']
        print(f'  {k}: {len(vv)} → {len(vn)}', 'iguales (sin etag)' if sin_etag(vv) == sin_etag(vn) else 'DISTINTAS')

def main():
    ruta = sys.argv[1]; z = zipfile.ZipFile(ruta)
    archivos = {n: z.read(n) for n in z.namelist()}; infos = {i.filename: i for i in z.infolist()}; z.close()
    DS, PR, QV = 'msapp/References/DataSources.json', 'msapp/Properties.json', 'msapp/References/QualifiedValues.json'
    dsj = json.loads(archivos[DS]); ds = dsj['DataSources']
    if '--comparar' in sys.argv:
        for t in sys.argv[sys.argv.index('--comparar') + 1].split(','): comparar(ds, t)
        return
    pr = json.loads(archivos[PR]); ldr = json.loads(pr['LocalDatabaseReferences']); qv = json.loads(archivos[QV])
    for t in sys.argv[sys.argv.index('--tablas') + 1].split(','):
        tabla, opciones, vistas, forms, em = familia(t)
        existe = any(d.get('LogicalName') == t and d['Type'] == 'NativeCDSDataSourceInfo' for d in ds)
        print(('refrescar ' if existe else 'agregar   ') + f"{tabla['Name']} ({t}): {len(em['Attributes'])} columnas, {len(tabla['NativeCDSDataSourceInfoNameMapping'])} nombres")
        def poner(nueva, antes_de_tipo):
            i = next((k for k, d in enumerate(ds) if d['Name'] == nueva['Name']), None)
            if i is not None: ds[i] = nueva; return
            ultimo = max((k for k, d in enumerate(ds) if d['Type'] == nueva['Type']), default=None)
            ds.insert(ultimo + 1 if ultimo is not None else len(ds), nueva)
        for o in opciones: poner(o, 'OptionSetInfo')
        poner(vistas, 'ViewInfo')
        # MaxGetRowsCount es el límite de filas de la app (Configuración → Límite de filas): se CONSERVA al refrescar; una tabla
        # nueva toma el mayor de la app. Fijarlo en un valor menor rompe pantallas que dependen de un límite mayor.
        i = next((k for k, d in enumerate(ds) if d.get('LogicalName') == t and d['Type'] == 'NativeCDSDataSourceInfo'), None)
        tabla['MaxGetRowsCount'] = ds[i].get('MaxGetRowsCount', 500) if i is not None else \
            max((d.get('MaxGetRowsCount', 500) for d in ds if d['Type'] == 'NativeCDSDataSourceInfo'), default=500)
        if i is not None: ds[i] = tabla
        else: ds.insert(next(k for k, d in enumerate(ds) if d['Name'] == 'Users'), tabla)
        ldr['default.cds']['dataSources'][tabla['Name']] = {'entitySetName': em['EntitySetName'], 'logicalName': t}
        j = next((k for k, x in enumerate(qv['QualifiedValues']) if x['Kind'] == forms['Kind']), None)
        if j is not None: qv['QualifiedValues'][j] = forms
        else: qv['QualifiedValues'].append(forms)
    if '--aplicar' not in sys.argv: print('Nada escrito. Repetir con --aplicar.'); return
    pr['LocalDatabaseReferences'] = json.dumps(ldr, ensure_ascii=False, separators=(',', ':'))
    archivos[DS] = json.dumps(dsj, ensure_ascii=False, indent=2).encode('utf-8')
    archivos[PR] = json.dumps(pr, ensure_ascii=False, indent=2).encode('utf-8')
    archivos[QV] = json.dumps(qv, ensure_ascii=False, indent=2).encode('utf-8')
    with zipfile.ZipFile(ruta + '.tmp', 'w', zipfile.ZIP_DEFLATED) as out:
        for n, data in archivos.items(): out.writestr(infos[n], data, compress_type=zipfile.ZIP_DEFLATED)
    os.replace(ruta + '.tmp', ruta); print('escrito', ruta)

main()
