"""Plantillas de Smelter para la CARGA PARALELA.
Con la plantilla "Cobranza movil" sola, la carga corre en UN Import Job con lotes en serie. La Custom API
sanic_cm_PartirCarga (plugins/.../PartirCarga.cs) convierte ese job en el de PADRES y crea N jobs de PARTES. Cada uno usa
una plantilla derivada de la principal, que genera ESTE script:
  - "Cobranza movil · padres": SOLO los Table Mappings de sanic_cm_tbl_producto, sanic_cm_tbl_fiador y sanic_cm_tbl_agente
    (con sus Column Mappings, flags, dedup y orden). Corre primero y solo: producto y fiador se siguen actualizando.
  - "Cobranza movil · partes": TODOS los demas. Sus lookups a producto, fiador y agente se resuelven por clave alterna en
    Dataverse (NormalizationEngine.cs:183-200 de Smelter): solo LEEN esas tablas.
Mismo delimitador, encabezado, tamano de lote, origen y estado (Activo: PrepareImportPlugin.cs:154-161 lo exige) que la
principal, y el MISMO dueno (el equipo "CM · Admin y Carga"): AssignJobOwner le da al job el dueno de su plantilla y
PrepareImport exige que quien lo pone en cola pueda leerla.

La principal es la UNICA que se edita a mano. Al reaplicar, re-sincroniza: crea, corrige y BORRA lo que haga falta para que
cada derivada quede igual a su parte de la principal (Table Mappings por tabla + nombre, Column Mappings por columna destino).
PartirCarga compara las tres y se niega a partir si no coinciden: despues de tocar la principal, correr este script.
Los Relationship Mappings no se copian: si la principal llega a tener alguno, el script se detiene (un mapping de relacion
con el padre en otra plantilla deja el lookup vacio en silencio: NormalizationEngine.cs:235-264).

Es SOLO CONFIGURACION de Smelter (registros de sus tablas de plantillas); no toca codigo ni flows de Smelter.
Respaldo previo (principal y derivadas, si existen) en local/_backups/. Idempotente. Por defecto solo muestra el PLAN.
Uso: python3 scripts/pan/paso20_plantillas_paralelas.py [--aplicar]"""
import json, os, sys, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
PRINCIPAL = 'Cobranza movil'
PADRES, PARTES = 'Cobranza movil · padres', 'Cobranza movil · partes'
TABLAS_PADRES = {'sanic_cm_tbl_producto', 'sanic_cm_tbl_fiador', 'sanic_cm_tbl_agente'}
CAMPOS_IT = ['sanic_smlt_it_delimiter', 'sanic_smlt_it_hasheaderrow', 'sanic_smlt_it_chunksize', 'sanic_smlt_it_sourcetype', 'sanic_smlt_it_status']
CAMPOS_TM = ['sanic_smlt_tm_name', 'sanic_smlt_tm_targettable', 'sanic_smlt_tm_dedupcolumns', 'sanic_smlt_tm_processingorder',
             'sanic_smlt_tm_createifnotexists', 'sanic_smlt_tm_updateifexists', 'sanic_smlt_tm_isjunctiontable']
CAMPOS_CM = ['sanic_smlt_cm_name', 'sanic_smlt_cm_targetcolumn', 'sanic_smlt_cm_sourcecolumn', 'sanic_smlt_cm_datatype',
             'sanic_smlt_cm_isrequired', 'sanic_smlt_cm_defaultvalue', 'sanic_smlt_cm_lookuptable', 'sanic_smlt_cm_lookupmatchcolumn',
             'sanic_smlt_cm_optionsetmapping', 'sanic_smlt_cm_transformexpression']
DESCRIPCION = ('Generada por scripts/pan/paso20_plantillas_paralelas.py desde "Cobranza movil" (carga paralela). '
               'NO editar a mano: editar la principal y volver a correr el script.')
cambios = 0


def plantilla(nombre):
    r = get("sanic_tbl_smlt_importtemplates?$filter=sanic_smlt_it_name eq '%s'" % nombre)['value']
    if len(r) > 1: raise SystemExit(f'Hay {len(r)} plantillas "{nombre}": dejar una sola antes de seguir')
    return r[0] if r else None


def mapeos(tid):
    tms = get(f"sanic_tbl_smlt_tablemappings?$filter=_sanic_smlt_tm_importtemplateid_value eq {tid}")['value']
    for tm in tms:
        tm['_cms'] = get(f"sanic_tbl_smlt_columnmappings?$filter=_sanic_smlt_cm_tablemappingid_value eq {tm['sanic_tbl_smlt_tablemappingid']}")['value']
    return tms


def clave_tm(tm): return (tm['sanic_smlt_tm_targettable'], tm['sanic_smlt_tm_name'])


def hacer(desc, metodo, ruta, cuerpo=None):
    global cambios
    cambios += 1
    print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
    if not APLICAR: return {}
    st, b = rq(metodo, ruta, cuerpo, {'Prefer': 'return=representation'} if metodo == 'POST' else None)
    if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
    return b or {}


def dueno_bind(t):
    if t.get('_owningteam_value'): return {'ownerid@odata.bind': f"/teams({t['_owningteam_value']})"}
    return {'ownerid@odata.bind': f"/systemusers({t['_ownerid_value']})"}


def sincronizar(nombre, principal, tms_principal):
    print(f'--- "{nombre}": {len(tms_principal)} Table Mappings '
          f"({', '.join(sorted({t['sanic_smlt_tm_targettable'] for t in tms_principal}))})")
    derivada = plantilla(nombre)
    cuerpo = {c: principal.get(c) for c in CAMPOS_IT}
    if derivada is None:
        b = hacer(f'crear la plantilla "{nombre}" (estado {principal.get("sanic_smlt_it_status")}, dueno = el de la principal)',
                  'POST', 'sanic_tbl_smlt_importtemplates?$select=sanic_tbl_smlt_importtemplateid',
                  {'sanic_smlt_it_name': nombre, 'sanic_smlt_it_description': DESCRIPCION, **cuerpo, **dueno_bind(principal)})
        tid, actuales = b.get('sanic_tbl_smlt_importtemplateid', '<nueva>'), []
    else:
        tid = derivada['sanic_tbl_smlt_importtemplateid']
        dif = {c: v for c, v in cuerpo.items() if derivada.get(c) != v}
        if derivada.get('sanic_smlt_it_description') != DESCRIPCION: dif['sanic_smlt_it_description'] = DESCRIPCION
        if dif: hacer(f'"{nombre}": {dif}', 'PATCH', f'sanic_tbl_smlt_importtemplates({tid})', dif)
        if derivada.get('_ownerid_value') != principal.get('_ownerid_value'):
            hacer(f'"{nombre}": dueno = el de la principal', 'PATCH', f'sanic_tbl_smlt_importtemplates({tid})', dueno_bind(principal))
        if derivada.get('statecode') != 0:
            hacer(f'"{nombre}": activar el registro', 'PATCH', f'sanic_tbl_smlt_importtemplates({tid})', {'statecode': 0, 'statuscode': 1})
        actuales = mapeos(tid)

    por_clave = {}
    for tm in actuales:
        por_clave.setdefault(clave_tm(tm), []).append(tm)
    for k, tm in [(clave_tm(t), t) for t in tms_principal]:
        hay = por_clave.pop(k, [])
        for sobra in hay[1:]: borrar_tm(nombre, sobra)
        destino = hay[0] if hay else None
        campos = {c: tm.get(c) for c in CAMPOS_TM}
        if destino is None:
            b = hacer(f'"{nombre}": + Table Mapping {k[0]} / {k[1]}', 'POST', 'sanic_tbl_smlt_tablemappings?$select=sanic_tbl_smlt_tablemappingid',
                      {**campos, 'sanic_smlt_tm_importtemplateid@odata.bind': f'/sanic_tbl_smlt_importtemplates({tid})'})
            mid, cms_actuales = b.get('sanic_tbl_smlt_tablemappingid', '<nuevo>'), []
        else:
            mid = destino['sanic_tbl_smlt_tablemappingid']
            dif = {c: v for c, v in campos.items() if destino.get(c) != v}
            if dif: hacer(f'"{nombre}": ~ {k[0]} / {k[1]} {dif}', 'PATCH', f'sanic_tbl_smlt_tablemappings({mid})', dif)
            cms_actuales = destino['_cms']
        sincronizar_cms(nombre, k, mid, tm['_cms'], cms_actuales)
    for hay in por_clave.values():
        for sobra in hay: borrar_tm(nombre, sobra)


def sincronizar_cms(nombre, k, mid, esperadas, actuales):
    por_destino = {}
    for c in actuales: por_destino.setdefault(c['sanic_smlt_cm_targetcolumn'], []).append(c)
    for cm in esperadas:
        campos = {c: cm.get(c) for c in CAMPOS_CM}
        hay = por_destino.pop(cm['sanic_smlt_cm_targetcolumn'], [])
        for sobra in hay[1:]:
            hacer(f'"{nombre}": - Column Mapping repetido {k[0]}.{sobra["sanic_smlt_cm_targetcolumn"]}', 'DELETE',
                  f"sanic_tbl_smlt_columnmappings({sobra['sanic_tbl_smlt_columnmappingid']})")
        if not hay:
            hacer(f'"{nombre}": + Column Mapping {k[0]}.{cm["sanic_smlt_cm_targetcolumn"]}', 'POST', 'sanic_tbl_smlt_columnmappings',
                  {**campos, 'sanic_smlt_cm_tablemappingid@odata.bind': f'/sanic_tbl_smlt_tablemappings({mid})'})
            continue
        dif = {c: v for c, v in campos.items() if hay[0].get(c) != v}
        if dif:
            hacer(f'"{nombre}": ~ Column Mapping {k[0]}.{cm["sanic_smlt_cm_targetcolumn"]} {sorted(dif)}', 'PATCH',
                  f"sanic_tbl_smlt_columnmappings({hay[0]['sanic_tbl_smlt_columnmappingid']})", dif)
    for hay in por_destino.values():
        for sobra in hay:
            hacer(f'"{nombre}": - Column Mapping {k[0]}.{sobra["sanic_smlt_cm_targetcolumn"]} (ya no esta en la principal)', 'DELETE',
                  f"sanic_tbl_smlt_columnmappings({sobra['sanic_tbl_smlt_columnmappingid']})")


def borrar_tm(nombre, tm):
    for c in tm['_cms']:
        hacer(f'"{nombre}": - Column Mapping {tm["sanic_smlt_tm_targettable"]}.{c["sanic_smlt_cm_targetcolumn"]}', 'DELETE',
              f"sanic_tbl_smlt_columnmappings({c['sanic_tbl_smlt_columnmappingid']})")
    hacer(f'"{nombre}": - Table Mapping {tm["sanic_smlt_tm_targettable"]} / {tm["sanic_smlt_tm_name"]} (ya no corresponde)', 'DELETE',
          f"sanic_tbl_smlt_tablemappings({tm['sanic_tbl_smlt_tablemappingid']})")


def firma(tms):
    return sorted((tuple(tm.get(c) for c in CAMPOS_TM), tuple(sorted(tuple(str(cm.get(c)) for c in CAMPOS_CM) for cm in tm['_cms'])))
                  for tm in tms)


def main():
    principal = plantilla(PRINCIPAL)
    if principal is None: raise SystemExit(f'No existe la plantilla "{PRINCIPAL}"')
    tid = principal['sanic_tbl_smlt_importtemplateid']
    tms = mapeos(tid)
    rms = get(f"sanic_tbl_smlt_relationshipmappings?$select=sanic_smlt_rm_name&$filter=_sanic_smlt_rm_importtemplateid_value eq {tid}")['value']
    if rms: raise SystemExit(f'La principal tiene {len(rms)} Relationship Mappings: este script no los sabe copiar. Revisar antes.')
    claves = [clave_tm(t) for t in tms]
    if len(claves) != len(set(claves)): raise SystemExit('La principal tiene dos Table Mappings con la misma tabla y nombre: renombrar uno.')
    for t in tms:
        destinos = [c['sanic_smlt_cm_targetcolumn'] for c in t['_cms']]
        if len(destinos) != len(set(destinos)): raise SystemExit(f'{t["sanic_smlt_tm_name"]}: dos Column Mappings con la misma columna destino.')
    padres = [t for t in tms if t['sanic_smlt_tm_targettable'] in TABLAS_PADRES]
    partes = [t for t in tms if t['sanic_smlt_tm_targettable'] not in TABLAS_PADRES]
    faltan = TABLAS_PADRES - {t['sanic_smlt_tm_targettable'] for t in padres}
    if faltan: raise SystemExit(f'La principal no carga {sorted(faltan)}')
    print('MODO:', 'APLICAR' if APLICAR else 'PLAN (solo lectura; usar --aplicar)')
    print(f'principal "{PRINCIPAL}": {len(tms)} Table Mappings, {sum(len(t["_cms"]) for t in tms)} Column Mappings, '
          f'estado {principal.get("sanic_smlt_it_status")}, dueno {"equipo" if principal.get("_owningteam_value") else "usuario"}')

    if APLICAR:
        os.makedirs('local/_backups', exist_ok=True)
        ruta = f"local/_backups/smelter-plantillas-paralelas_{time.strftime('%Y%m%d-%H%M%S')}.json"
        respaldo = {'principal': {'plantilla': principal, 'tablemappings': tms}}
        for n in (PADRES, PARTES):
            d = plantilla(n)
            respaldo[n] = {'plantilla': d, 'tablemappings': mapeos(d['sanic_tbl_smlt_importtemplateid']) if d else []}
        json.dump(respaldo, open(ruta, 'w'), indent=1, ensure_ascii=False)
        print('respaldo:', ruta)

    sincronizar(PADRES, principal, padres)
    sincronizar(PARTES, principal, partes)

    if not cambios:
        print('YA ESTA: las dos plantillas son copia exacta de la principal.')
    elif not APLICAR:
        print(f'plan: {cambios} cambio(s). Aplicar con --aplicar.')
    else:
        # Verificacion por GET: cada derivada igual a su parte de la principal (lo mismo que exige PartirCarga).
        for n, esperado in ((PADRES, padres), (PARTES, partes)):
            d = plantilla(n)
            ok = d is not None and firma(mapeos(d['sanic_tbl_smlt_importtemplateid'])) == firma(esperado) \
                and all(d.get(c) == principal.get(c) for c in CAMPOS_IT) and d.get('_ownerid_value') == principal.get('_ownerid_value')
            print(('Verificado por GET: ' if ok else '✗ NO coincide: ') + f'"{n}"')
            if not ok: raise SystemExit(1)


if __name__ == '__main__':
    main()
