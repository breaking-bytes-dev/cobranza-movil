"""Fase 2 · Paso 8 (CONTRAER): el número de tarjeta deja de existir fuera de la bóveda. Ver docs/auditoria/fase2-pan-diseno.md §4.
Orden (cada etapa es idempotente; por defecto solo muestra el plan, aplica con --aplicar):
  a) borra las claves alternativas viejas sobre el número (con la clave única viva, ****1234 choca: 81 cuentas en 40 grupos)
  b) plantilla Smelter: ct_numerodecuenta y cd_numerocuenta pasan a CONCAT("****", RIGHT(TRIM(NumeroCuenta), 4));
     se quita el mapeo a as_numerocuenta
  c) enmascara los datos: ct_numerodecuenta y cd_numerocuenta → ****1234 (la auditoría de esas columnas está apagada: paso 2)
  d) vacía as_numerocuenta, in_numerocuenta y pg_numerocuenta
  e) borra esas 3 columnas (nada depende de ellas: RetrieveDependenciesForDelete vacío; las apps ya no las usan)
Después: paso5_plugins.py --activar-panguard; refrescar las fuentes de las apps (paso7_fuentes.py) y paso7_desplegar.py.
Requisito: backup del entorno (pac admin backup). Nunca imprime números: solo conteos.
Uso: python3 scripts/pan/paso8_contraer.py [--aplicar] [--solo a,b,c,d,e]"""
import os, re, sys
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
SOLO = set(sys.argv[sys.argv.index('--solo') + 1].split(',')) if '--solo' in sys.argv else set('abcde')
P = 'sanic_cm_tbl_'
MASCARA_TX = 'CONCAT("****", RIGHT(TRIM(NumeroCuenta), 4))'
def plan(desc): print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
def todas(p):
    out, n = [], p
    while n:
        b = get(n); out += b['value']; n = b.get('@odata.nextLink')
    return out
def paralelo(fn, items):
    with ThreadPoolExecutor(8) as ex: return [r for r in ex.map(fn, items) if r]
def mascara(v): v = (v or '').strip(); return '****' + v[-4:] if v else v

if 'a' in SOLO:
    print('== a) claves alternativas viejas')
    for e, k in [(P + 'cuenta', 'sanic_cm_pk_ct_numerodecuenta'), (P + 'asignacion', 'sanic_cm_altkey_NumeroCuentaxTipo'),
                 (P + 'cargadiaria', 'sanic_cm_pk_cd_numerocuenta_fechacarga')]:
        ks = [x for x in get(f"EntityDefinitions(LogicalName='{e}')/Keys?$select=SchemaName,LogicalName")['value'] if x['SchemaName'].lower() == k.lower()]
        if not ks: print('YA BORRADA', k); continue
        plan(f'borrar clave {e}.{k}')
        if APLICAR:
            st, b = rq('DELETE', f"EntityDefinitions(LogicalName='{e}')/Keys(LogicalName='{ks[0]['LogicalName']}')")
            print('   ', '✓' if st < 300 else f'✗ {st} {b}')

if 'b' in SOLO:
    print('== b) plantilla Smelter "Cobranza movil"')
    tid = get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'")['value'][0]['sanic_tbl_smlt_importtemplateid']
    for tm in get(f"sanic_tbl_smlt_tablemappings?$select=sanic_smlt_tm_targettable&$filter=_sanic_smlt_tm_importtemplateid_value eq {tid}")['value']:
        for c in get(f"sanic_tbl_smlt_columnmappings?$select=sanic_smlt_cm_targetcolumn,sanic_smlt_cm_transformexpression&$filter=_sanic_smlt_cm_tablemappingid_value eq {tm['sanic_tbl_smlt_tablemappingid']}")['value']:
            col, cid = c['sanic_smlt_cm_targetcolumn'], c['sanic_tbl_smlt_columnmappingid']
            if col in ('sanic_cm_ct_numerodecuenta', 'sanic_cm_cd_numerocuenta') and c['sanic_smlt_cm_transformexpression'] != MASCARA_TX:
                plan(f'{col}: = {MASCARA_TX}')
                if APLICAR: print('   ', rq('PATCH', f'sanic_tbl_smlt_columnmappings({cid})', {'sanic_smlt_cm_transformexpression': MASCARA_TX})[0])
            elif col == 'sanic_cm_as_numerocuenta':
                plan(f'quitar mapeo {col}')
                if APLICAR: print('   ', rq('DELETE', f'sanic_tbl_smlt_columnmappings({cid})')[0])

if 'c' in SOLO:
    print('== c) enmascarar datos')
    # La cuenta usa ct_ultimos4 (coincide con la bóveda); si falta, los últimos 4 del valor actual.
    for s, pk, col, ult in [('cuentas', P + 'cuentaid', 'sanic_cm_ct_numerodecuenta', 'sanic_cm_ct_ultimos4'),
                            ('cargadiarias', P + 'cargadiariaid', 'sanic_cm_cd_numerocuenta', None)]:
        filas = [x for x in todas(f"{P}{s}?$select={pk},{col}" + (f',{ult}' if ult else '')) if x[col] and not x[col].startswith('****')]
        plan(f'{s}.{col}: {len(filas)} filas → ****1234')
        if APLICAR:
            def f(x):
                nuevo = '****' + x[ult] if ult and x.get(ult) else mascara(x[col])
                st, b = rq('PATCH', f'{P}{s}({x[pk]})', {col: nuevo})
                if st >= 300: return f'{x[pk][:8]}: {st} {str(b)[:150]}'
            e = paralelo(f, filas); print('    errores', len(e), e[:3])

if 'd' in SOLO:
    print('== d) vaciar columnas viejas')
    for s, pk, col in [('asignacions', P + 'asignacionid', 'sanic_cm_as_numerocuenta'), ('interaccions', P + 'interaccionid', 'sanic_cm_in_numerocuenta'),
                       ('pagos', P + 'pagoid', 'sanic_cm_pg_numerocuenta')]:
        if rq('GET', f"EntityDefinitions(LogicalName='{P}{s[:-1]}')/Attributes(LogicalName='{col}')?$select=LogicalName")[0] != 200:
            print('YA NO EXISTE', col); continue
        filas = todas(f"{P}{s}?$select={pk}&$filter={col} ne null")
        plan(f'{s}.{col}: vaciar {len(filas)} filas')
        if APLICAR:
            def f(x):
                st, b = rq('PATCH', f'{P}{s}({x[pk]})', {col: None})
                if st >= 300: return f'{x[pk][:8]}: {st} {str(b)[:150]}'
            e = paralelo(f, filas); print('    errores', len(e), e[:3])

if 'e' in SOLO:
    print('== e) borrar columnas viejas')
    for e, col in [(P + 'asignacion', 'sanic_cm_as_numerocuenta'), (P + 'interaccion', 'sanic_cm_in_numerocuenta'), (P + 'pago', 'sanic_cm_pg_numerocuenta')]:
        st, m = rq('GET', f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{col}')?$select=MetadataId")
        if st != 200: print('YA BORRADA', col); continue
        # Dependencias: pasos PanGuard que filtran por la columna, vistas y formularios. Cualquier otro tipo frena.
        for d in get(f"RetrieveDependenciesForDelete(ComponentType=2,ObjectId={m['MetadataId']})")['value']:
            t, oid = d['dependentcomponenttype'], d['dependentcomponentobjectid']
            if t == 92:
                nombre = get(f'sdkmessageprocessingsteps({oid})?$select=name')['name']
                if not nombre.startswith('Sanic.CobranzaMovil.Plugins.PanGuard'): raise SystemExit(f'paso ajeno depende de {col}: {nombre}')
                plan(f'  retirar paso {nombre} (vigila una columna que se borra)')
                if APLICAR: print('     ', rq('DELETE', f'sdkmessageprocessingsteps({oid})')[0])
            elif t == 26:
                v = get(f'savedqueries({oid})?$select=name,fetchxml,layoutxml')
                fx = re.sub(rf'\s*<attribute name="{col}"\s*/>', '', v['fetchxml'])
                lx = re.sub(rf'\s*<cell name="{col}"[^>]*/>', '', v['layoutxml'])
                plan(f"  quitar {col} de la vista '{v['name']}'")
                if APLICAR: print('     ', rq('PATCH', f'savedqueries({oid})', {'fetchxml': fx, 'layoutxml': lx})[0])
            elif t == 60:
                import xml.etree.ElementTree as ET
                fm = get(f'systemforms({oid})?$select=name,formxml'); raiz = ET.fromstring(fm['formxml'])
                quitadas = 0
                for fila in raiz.iter('row'):
                    for celda in list(fila):
                        if any(c.get('datafieldname') == col for c in celda.iter('control')): fila.remove(celda); quitadas += 1
                plan(f"  quitar {col} del formulario '{fm['name']}' ({quitadas} celda/s)")
                if APLICAR: print('     ', rq('PATCH', f'systemforms({oid})', {'formxml': ET.tostring(raiz, encoding='unicode')})[0])
            else: raise SystemExit(f'dependencia no prevista de {col}: tipo {t} {oid}')
        if APLICAR: rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{e}</entity></entities></importexportxml>'})
        plan(f'borrar columna {e}.{col}')
        if APLICAR:
            st, b = rq('DELETE', f"EntityDefinitions(LogicalName='{e}')/Attributes({m['MetadataId']})")
            print('   ', '✓' if st < 300 else f'✗ {st} {str(b)[:300]}')
