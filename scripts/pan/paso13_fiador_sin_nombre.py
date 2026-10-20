"""Fase 2 · Paso 13: fiador sin nombre -> "(sin fiador)" (opcion 2a-2 de docs/auditoria/01-pendientes.md, fila "Fiadores (origen)").
Pone DefaultValue = "(sin fiador)" en el Column Mapping NombreFianza -> sanic_cm_fi_nombre del Table Mapping de la tabla
sanic_cm_tbl_fiador de la plantilla Smelter "Cobranza movil". Smelter aplica el default ANTES del chequeo de vacio
(NormalizationEngine) y UpsertWriter arma la clave alterna solo si el valor no es nulo: asi las filas sin fiador dejan de
crear un fiador vacio por fila y comparten UN solo fiador "(sin fiador)" (upsert por sanic_cm_fi_nombre).
NO toca el Column Mapping de la Cuenta (sanic_cm_ct_fiador: lookup a fiador por nombre, fuente NombreFianza): con la fila vacia
el lookup no encuentra nada y la cuenta queda sin fiador. Es solo CONFIGURACION de la plantilla; no toca codigo de Smelter.
Busca el mapeo por plantilla + tabla destino + columna fuente/destino (no por id), asi sirve igual en PROD.
Respaldo previo: local/_backups/smelter-plantilla-cobranza_*.json. Revertir: --revertir (deja el default en null).
Idempotente. Uso: python3 scripts/pan/paso13_fiador_sin_nombre.py [--aplicar] [--revertir]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
VALOR = None if '--revertir' in sys.argv else '(sin fiador)'
TABLA, FUENTE, DESTINO = 'sanic_cm_tbl_fiador', 'NombreFianza', 'sanic_cm_fi_nombre'

tpl = get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'")['value']
if len(tpl) != 1: raise SystemExit(f'Se esperaba 1 plantilla "Cobranza movil", hay {len(tpl)}')
tid = tpl[0]['sanic_tbl_smlt_importtemplateid']
tms = [t for t in get(f"sanic_tbl_smlt_tablemappings?$filter=_sanic_smlt_tm_importtemplateid_value eq {tid}")['value']
       if t['sanic_smlt_tm_targettable'] == TABLA]
if len(tms) != 1: raise SystemExit(f'Se esperaba 1 Table Mapping de {TABLA}, hay {len(tms)}')
cms = [c for c in get(f"sanic_tbl_smlt_columnmappings?$filter=_sanic_smlt_cm_tablemappingid_value eq {tms[0]['sanic_tbl_smlt_tablemappingid']}")['value']
       if c['sanic_smlt_cm_sourcecolumn'] == FUENTE and c['sanic_smlt_cm_targetcolumn'] == DESTINO]
if len(cms) != 1: raise SystemExit(f'Se esperaba 1 Column Mapping {FUENTE} -> {DESTINO}, hay {len(cms)}')
cm = cms[0]; cid = cm['sanic_tbl_smlt_columnmappingid']
print(f"Column Mapping {cid} ({tms[0]['sanic_smlt_tm_name']}: {FUENTE} -> {DESTINO}) · default actual: {cm.get('sanic_smlt_cm_defaultvalue')!r} · requerido: {cm.get('sanic_smlt_cm_isrequired')}")
if cm.get('sanic_smlt_cm_defaultvalue') == VALOR:
    print('YA ESTA'); sys.exit(0)
print(('APLICAR ' if APLICAR else 'PLAN    ') + f'sanic_smlt_cm_defaultvalue = {VALOR!r}')
if APLICAR:
    st, b = rq('PATCH', f'sanic_tbl_smlt_columnmappings({cid})', {'sanic_smlt_cm_defaultvalue': VALOR})
    if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
    ahora = get(f'sanic_tbl_smlt_columnmappings({cid})?$select=sanic_smlt_cm_defaultvalue')['sanic_smlt_cm_defaultvalue']
    print('Verificado por GET, default =', repr(ahora))
    if ahora != VALOR: raise SystemExit('✗ el GET no devuelve el valor esperado')
