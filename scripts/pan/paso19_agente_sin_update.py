"""La carga no reescribe el Agente que ya existe: UpdateIfExists = false en el Table Mapping de sanic_cm_tbl_agente de la
plantilla Smelter "Cobranza movil".
El Agente tiene UNA sola columna mapeada (sanic_cm_ag_nombre), que es su clave de dedup: actualizarlo no cambia nada, pero
Smelter lo reescribiría en cada fila. Con el flag, el existente queda Skipped y el que falta se sigue creando
(CreateIfNotExists sigue en true). Requiere una versión de Smelter que honre el flag. Los "omitidos" del job suben ~1 por fila.
Es solo CONFIGURACION de la plantilla; no toca codigo de Smelter. Busca el mapeo por plantilla + tabla destino (no por id).
Respaldo previo en local/_backups/. Revertir: --revertir. Idempotente.
Uso: python3 scripts/pan/paso19_agente_sin_update.py [--aplicar] [--revertir]"""
import json, os, sys, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
VALOR = '--revertir' in sys.argv          # destino de updateifexists
TABLA = 'sanic_cm_tbl_agente'

tpl = get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'")['value']
if len(tpl) != 1: raise SystemExit(f'Se esperaba 1 plantilla "Cobranza movil", hay {len(tpl)}')
tid = tpl[0]['sanic_tbl_smlt_importtemplateid']
tms = [t for t in get(f"sanic_tbl_smlt_tablemappings?$filter=_sanic_smlt_tm_importtemplateid_value eq {tid}")['value']
       if t['sanic_smlt_tm_targettable'] == TABLA]
if len(tms) != 1: raise SystemExit(f'Se esperaba 1 Table Mapping de {TABLA}, hay {len(tms)}')
tm = tms[0]; mid = tm['sanic_tbl_smlt_tablemappingid']
cms = get(f"sanic_tbl_smlt_columnmappings?$filter=_sanic_smlt_cm_tablemappingid_value eq {mid}")['value']
destinos = sorted(c['sanic_smlt_cm_targetcolumn'] for c in cms)
print(f"Table Mapping {tm['sanic_smlt_tm_name']} · columnas {destinos} · dedup {tm['sanic_smlt_tm_dedupcolumns']}"
      f" · crear {tm['sanic_smlt_tm_createifnotexists']} · actualizar {tm['sanic_smlt_tm_updateifexists']}")
if not VALOR and destinos != [tm['sanic_smlt_tm_dedupcolumns']]:
    raise SystemExit('✗ el Agente ya mapea columnas además de la clave: sin update dejarían de actualizarse. Revisar antes.')
if not tm['sanic_smlt_tm_createifnotexists']: raise SystemExit('✗ CreateIfNotExists está en false: el agente nuevo no se crearía')
if tm['sanic_smlt_tm_updateifexists'] == VALOR:
    print('YA ESTA'); sys.exit(0)
print(('APLICAR ' if APLICAR else 'PLAN    ') + f'sanic_smlt_tm_updateifexists = {VALOR}')
if APLICAR:
    os.makedirs('local/_backups', exist_ok=True)
    ruta = f"local/_backups/smelter-tm-agente_{time.strftime('%Y%m%d-%H%M%S')}.json"
    json.dump({'tablemapping': tm, 'columnmappings': cms}, open(ruta, 'w'), indent=1)
    print('respaldo:', ruta)
    st, b = rq('PATCH', f'sanic_tbl_smlt_tablemappings({mid})', {'sanic_smlt_tm_updateifexists': VALOR})
    if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
    ahora = get(f'sanic_tbl_smlt_tablemappings({mid})?$select=sanic_smlt_tm_updateifexists')['sanic_smlt_tm_updateifexists']
    print('Verificado por GET, updateifexists =', ahora)
    if ahora != VALOR: raise SystemExit('✗ el GET no devuelve el valor esperado')
