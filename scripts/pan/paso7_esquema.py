"""Fase 2 · Paso 7 (chequeo previo): la foto del esquema de la app tiene que conocer las columnas que usan sus fórmulas.
La app no lee Dataverse para resolver nombres: usa la copia de las columnas guardada en msapp/References/DataSources.json
(NativeCDSDataSourceInfoNameMapping de cada tabla), tomada cuando se agregó o refrescó la fuente. Si una fórmula usa una
columna creada después, Studio la marcaría en rojo; con `pac canvas pack` la fórmula entera queda inerte y la app no avisa
(2026-10-21: la Cartera mostraba 0 asignaciones porque la foto no tenía '[AS] Tramo'). Esto lo corta antes de importar.

Error (corta): un nombre entre comillas simples de las fórmulas que existe como columna en Dataverse, en alguna tabla que
usa la app, y no está en la foto de NINGUNA tabla → falta refrescar la fuente con paso7_fuentes.py.
Aviso (no corta): un nombre que está en la foto pero ya no existe en Dataverse (columna borrada o renombrada).
No es un compilador de Power Fx: solo mira nombres entre comillas simples, fuera de textos "..." y comentarios //.

Uso suelto: python3 scripts/pan/paso7_esquema.py apps/localizador-canvas [archivo.msapr]"""
import json, os, re, sys, urllib.parse, zipfile
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, U

def _etiqueta(x): return ((x or {}).get('UserLocalizedLabel') or {}).get('Label') or ''

def _vivas(tabla):
    """Nombres visibles de las columnas de la tabla hoy en Dataverse (mismo criterio que la foto: sin columnas AttributeOf)."""
    st, b = rq('GET', U + f"/api/data/v9.2/EntityDefinitions(LogicalName='{tabla}')/Attributes?"
               + urllib.parse.quote("$select=LogicalName,DisplayName,AttributeOf", safe='$=,&'))
    if st >= 300: raise SystemExit(f'paso7_esquema: no pude leer las columnas de {tabla} ({st})')
    return {_etiqueta(a['DisplayName']): a['LogicalName'] for a in b['value'] if not a.get('AttributeOf') and _etiqueta(a['DisplayName'])}

def _relaciones(tabla):
    """SchemaName de las relaciones vivas: la foto también mapea relaciones (1:N, N:N) a nombres visibles."""
    nombres = set()
    for tipo in ('OneToManyRelationships', 'ManyToManyRelationships'):
        st, b = rq('GET', U + f"/api/data/v9.2/EntityDefinitions(LogicalName='{tabla}')/{tipo}?$select=SchemaName")
        if st >= 300: raise SystemExit(f'paso7_esquema: no pude leer las relaciones de {tabla} ({st})')
        nombres.update(r['SchemaName'] for r in b['value'])
    return nombres

def _nombres_en_formulas(textos):
    nombres = set()
    for t in textos:
        for linea in t.splitlines():
            linea = re.sub(r'"(?:[^"]|"")*"', '""', linea)   # textos primero: "https://…" no es un comentario
            linea = linea.split('//', 1)[0]
            nombres.update(m.replace("''", "'") for m in re.findall(r"'((?:[^']|'')+)'", linea))
    return nombres

def revisar(nombre_app, datasources_json, textos_yaml):
    fuentes = [d for d in json.loads(datasources_json)['DataSources'] if d.get('Type') == 'NativeCDSDataSourceInfo']
    foto = {d['LogicalName']: d.get('NativeCDSDataSourceInfoNameMapping') or {} for d in fuentes}   # nombre lógico → visible
    en_foto = {v for m in foto.values() for v in m.values()}
    usados = _nombres_en_formulas(textos_yaml)
    errores, avisos = [], []
    for tabla, columnas_foto in sorted(foto.items()):
        vivas = _vivas(tabla)
        for n in sorted(usados & set(vivas) - en_foto):
            errores.append(f"'{n}' ({tabla}.{vivas[n]}) está en las fórmulas y en Dataverse, pero no en la foto del esquema")
        existen = set(vivas.values()) | _relaciones(tabla) | {'_ownerid_value'}
        for clave, n in sorted(columnas_foto.items()):
            if n in usados and clave not in existen:
                avisos.append(f"'{n}' ({tabla}.{clave}) está en la foto y en las fórmulas, pero ya no existe en Dataverse")
    for a in avisos: print(f'  ⚠ {nombre_app}: {a}')
    if errores:
        tablas = sorted({e.split('(')[1].split('.')[0] for e in errores})
        raise SystemExit(f'{nombre_app}: la foto del esquema no conoce columnas que usan las fórmulas (la fórmula quedaría inerte, sin aviso):\n  '
                         + '\n  '.join(errores)
                         + f"\n  Refrescar: python3 scripts/pan/paso7_fuentes.py <.msapr de {nombre_app}> --tablas {','.join(tablas)} --aplicar")
    print(f'  {nombre_app}: esquema OK ({len(usados)} nombres entre comillas, {len(foto)} tablas)')

if __name__ == '__main__':
    carpeta = sys.argv[1]
    msapr = sys.argv[2] if len(sys.argv) > 2 else next(os.path.join(carpeta, a) for a in os.listdir(carpeta) if a.endswith('.msapr'))
    textos = [open(os.path.join(r, a), encoding='utf-8').read()
              for r, _, archivos in os.walk(os.path.join(carpeta, 'Src')) for a in archivos if a.endswith('.pa.yaml')]
    revisar(os.path.basename(carpeta.rstrip('/')), zipfile.ZipFile(msapr).read('msapp/References/DataSources.json'), textos)
