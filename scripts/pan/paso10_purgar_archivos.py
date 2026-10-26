"""Borra el CSV fuente (sanic_smlt_ij_sourcefile) de los jobs de Smelter de la plantilla
"Cobranza movil": esos archivos traen los números de tarjeta completos. Los jobs (contadores y logs) se conservan.
Solo toca jobs de ESA plantilla (Smelter es un producto compartido). Irreversible: correr después del backup.
En PROD esto tiene que ser automático al terminar cada carga (pendiente: flow o paso al final del orquestador).
Uso: python3 scripts/pan/paso10_purgar_archivos.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
tid = get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'")['value'][0]['sanic_tbl_smlt_importtemplateid']
jobs = get(f"sanic_tbl_smlt_importjobs?$select=sanic_smlt_ij_name,sanic_smlt_ij_sourcefile_name&$filter=_sanic_smlt_ij_importtemplateid_value eq {tid} and sanic_smlt_ij_sourcefile_name ne null")['value']
print(f'jobs de "Cobranza movil" con archivo: {len(jobs)}')
for j in jobs:
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f"borrar archivo de '{j['sanic_smlt_ij_name']}' ({j['sanic_smlt_ij_sourcefile_name']})")
    if APLICAR: print('   ', rq('DELETE', f"sanic_tbl_smlt_importjobs({j['sanic_tbl_smlt_importjobid']})/sanic_smlt_ij_sourcefile")[0])
if APLICAR:
    quedan = get(f"sanic_tbl_smlt_importjobs?$select=sanic_smlt_ij_name&$filter=_sanic_smlt_ij_importtemplateid_value eq {tid} and sanic_smlt_ij_sourcefile_name ne null")['value']
    print('quedan con archivo:', len(quedan))
