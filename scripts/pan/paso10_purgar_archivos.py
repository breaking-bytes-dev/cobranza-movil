"""Borra el CSV fuente (sanic_smlt_ij_sourcefile) de los jobs de Smelter de las plantillas de CM ("Cobranza movil",
"Cobranza movil · padres" y "Cobranza movil · partes"): esos archivos traen los números de tarjeta completos. Los jobs
(contadores y logs) se conservan. Solo toca jobs de ESAS plantillas (Smelter es un producto compartido). Irreversible.

Misma regla que el plugin PurgaArchivoCarga, que lo hace solo al terminar cada carga (y barre lo viejo):
  - se borra el archivo de un job en estado terminal (2 Completed, 3 CompletedWithErrors, 4 Failed), sea prueba en
    seco o carga real: un job terminado no se vuelve a leer (cada envío crea un job nuevo con su archivo);
  - además, las pruebas en seco SIN estado de más de 7 días (abandonadas: la pantalla nunca relanza una prueba).
Nunca toca un job sin estado (salvo esas pruebas viejas), En cola ni Procesando: lo puede leer PartirCarga, Smelter o
el «Relanzar» de las partes.
Sirve para limpiar lo que ya existía antes del plugin y en instalaciones nuevas.

Uso: python3 scripts/pan/paso10_purgar_archivos.py [--aplicar]   (por defecto solo muestra el plan)"""
import os, sys
from datetime import datetime, timedelta, timezone
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
PLANTILLAS = ['Cobranza movil', 'Cobranza movil · padres', 'Cobranza movil · partes']
TERMINALES = (2, 3, 4)
DIAS_PRUEBA_SIN_ESTADO = 7


def odata(texto):
    return texto.replace("'", "''")


filtro_nombres = ' or '.join(f"sanic_smlt_it_name eq '{odata(n)}'" for n in PLANTILLAS)
plantillas = get(f"sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter={filtro_nombres}")['value']
for n in PLANTILLAS:
    print(('✓ ' if any(p['sanic_smlt_it_name'] == n for p in plantillas) else '· no existe ') + f'plantilla "{n}"')
if not plantillas:
    raise SystemExit('No hay plantillas de CM: nada que purgar.')
nombre_plantilla = {p['sanic_tbl_smlt_importtemplateid']: p['sanic_smlt_it_name'] for p in plantillas}

corte = (datetime.now(timezone.utc) - timedelta(days=DIAS_PRUEBA_SIN_ESTADO)).strftime('%Y-%m-%dT%H:%M:%SZ')
filtro_plantilla = ' or '.join(f'_sanic_smlt_ij_importtemplateid_value eq {i}' for i in nombre_plantilla)
filtro_regla = (' or '.join(f'sanic_smlt_ij_status eq {e}' for e in TERMINALES)
                + f' or (sanic_smlt_ij_isdryrun eq true and sanic_smlt_ij_status eq null and createdon lt {corte})')
CONSULTA = ('sanic_tbl_smlt_importjobs?$select=sanic_smlt_ij_name,sanic_smlt_ij_sourcefile_name,sanic_smlt_ij_status,'
            '_sanic_smlt_ij_importtemplateid_value,sanic_smlt_ij_isdryrun'
            f'&$filter=({filtro_plantilla}) and sanic_smlt_ij_sourcefile_name ne null and ({filtro_regla})')


def con_archivo(consulta):
    filas, url = [], consulta
    while url:
        r = get(url)
        filas += r['value']
        url = r.get('@odata.nextLink')
    return filas


jobs = con_archivo(CONSULTA)
print(f'jobs de CM con archivo que ya no se lee: {len(jobs)}')
for j in jobs:
    estado = j.get('sanic_smlt_ij_status')
    detalle = f"plantilla «{nombre_plantilla.get(j['_sanic_smlt_ij_importtemplateid_value'], '?')}», estado {estado if estado is not None else 'vacío'}" \
              + (', prueba en seco' if j.get('sanic_smlt_ij_isdryrun') else '')
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f"borrar archivo de '{j['sanic_smlt_ij_name']}' ({detalle})")
    if APLICAR:
        print('   ', rq('DELETE', f"sanic_tbl_smlt_importjobs({j['sanic_tbl_smlt_importjobid']})/sanic_smlt_ij_sourcefile")[0])
if APLICAR:
    print('quedan con archivo (con la misma regla):', len(con_archivo(CONSULTA)))
else:
    print('Nada aplicado. Repetir con --aplicar.')
