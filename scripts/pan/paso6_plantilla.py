"""Fase 2 · Paso 6: plantilla Smelter "Cobranza movil" con claves HMAC (D1 + D1.1). Ver docs/auditoria/fase2-pan-diseno.md §4.
- Dedup y lookups pasan de la columna con el PAN a *_clavepan = HMAC(NumeroCuenta, "cm-pan").
- Tabla de bóveda nueva (orden 4); asignación y carga diaria pasan al orden 5 para que la bóveda exista antes que
  el flow de owner comparta la cuenta (igual, una fila creada después hereda los compartidos del padre).
- NO enmascara: las claves únicas viejas siguen vivas y ****1234 choca (81 de 1.008 cuentas). Eso es el paso 8.
Requiere Smelter con la transformación HMAC() y K cargada en su tabla protegida; sin eso, la carga falla con TransformError.
Respaldo previo: local/_backups/smelter-plantilla-cobranza_*.json (sanic_capi_smelter_exporttemplate).
Uso: python3 scripts/pan/paso6_plantilla.py [--aplicar --smelter-con-hmac]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
if APLICAR and '--smelter-con-hmac' not in sys.argv:
    raise SystemExit('Falta --smelter-con-hmac: confirmá que Smelter con HMAC() está desplegado y que K está cargada')
HMAC = 'HMAC(NumeroCuenta, "cm-pan")'
STRING, LOOKUP = 0, 5
P = 'sanic_cm_tbl_'

tid = get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'")['value'][0]['sanic_tbl_smlt_importtemplateid']
tms = get(f"sanic_tbl_smlt_tablemappings?$filter=_sanic_smlt_tm_importtemplateid_value eq {tid}")['value']
def tabla(destino): return [t for t in tms if t['sanic_smlt_tm_targettable'] == destino]
def columnas(tm): return get(f"sanic_tbl_smlt_columnmappings?$filter=_sanic_smlt_cm_tablemappingid_value eq {tm['sanic_tbl_smlt_tablemappingid']}")['value']
def hacer(desc, metodo, ruta, cuerpo=None):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
    if not APLICAR: return {}
    st, b = rq(metodo, ruta, cuerpo)
    if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
    return b or {}
def cambiar_tm(tm, **campos):
    campos = {f'sanic_smlt_tm_{k}': v for k, v in campos.items() if tm.get(f'sanic_smlt_tm_{k}') != v}
    if campos: hacer(f"{tm['sanic_smlt_tm_targettable']}: {campos}", 'PATCH', f"sanic_tbl_smlt_tablemappings({tm['sanic_tbl_smlt_tablemappingid']})", campos)
def asegurar_cm(tm, destino, tx, tipo=STRING, lookup=None, match=None):
    """Crea o corrige el mapeo de columna `destino` (idempotente)."""
    cuerpo = {'sanic_smlt_cm_name': f'NumeroCuenta → {destino}', 'sanic_smlt_cm_sourcecolumn': 'NumeroCuenta',
              'sanic_smlt_cm_targetcolumn': destino, 'sanic_smlt_cm_datatype': tipo, 'sanic_smlt_cm_isrequired': False,
              'sanic_smlt_cm_transformexpression': tx, 'sanic_smlt_cm_lookuptable': lookup, 'sanic_smlt_cm_lookupmatchcolumn': match}
    actual = [c for c in columnas(tm) if c['sanic_smlt_cm_targetcolumn'] == destino] if tm.get('sanic_tbl_smlt_tablemappingid') else []
    if not actual:
        hacer(f"{tm['sanic_smlt_tm_targettable']}: + {destino} = {tx}", 'POST', 'sanic_tbl_smlt_columnmappings',
              {**cuerpo, 'sanic_smlt_cm_tablemappingid@odata.bind': f"/sanic_tbl_smlt_tablemappings({tm.get('sanic_tbl_smlt_tablemappingid', '<nueva>')})"})
        return
    dif = {k: v for k, v in cuerpo.items() if k != 'sanic_smlt_cm_name' and actual[0].get(k) != v}
    if dif: hacer(f"{tm['sanic_smlt_tm_targettable']}: ~ {destino} {dif}", 'PATCH', f"sanic_tbl_smlt_columnmappings({actual[0]['sanic_tbl_smlt_columnmappingid']})", dif)
    else: print('YA ESTÁ  ', tm['sanic_smlt_tm_targettable'], destino)

# 1) Cuenta: dedup por la clave HMAC + últimos 4. ct_numerodecuenta sigue igual hasta el paso 8.
cuenta = tabla(P + 'cuenta')[0]
asegurar_cm(cuenta, 'sanic_cm_ct_clavepan', HMAC)
asegurar_cm(cuenta, 'sanic_cm_ct_ultimos4', 'RIGHT(TRIM(NumeroCuenta), 4)')
cambiar_tm(cuenta, dedupcolumns='sanic_cm_ct_clavepan')
# 2) Bóveda (orden 4)
boveda = tabla(P + 'cuentapan')
if boveda: boveda = boveda[0]
else:
    b = hacer('+ tabla sanic_cm_tbl_cuentapan (orden 4, dedup sanic_cm_cp_clavepan)', 'POST', 'sanic_tbl_smlt_tablemappings?$select=sanic_tbl_smlt_tablemappingid', {
        'sanic_smlt_tm_name': 'Cuenta PAN (bóveda)', 'sanic_smlt_tm_targettable': P + 'cuentapan', 'sanic_smlt_tm_dedupcolumns': 'sanic_cm_cp_clavepan',
        'sanic_smlt_tm_processingorder': 4, 'sanic_smlt_tm_createifnotexists': True, 'sanic_smlt_tm_updateifexists': True,
        'sanic_smlt_tm_isjunctiontable': False, 'sanic_smlt_tm_importtemplateid@odata.bind': f'/sanic_tbl_smlt_importtemplates({tid})'})
    boveda = {'sanic_smlt_tm_targettable': P + 'cuentapan', **({'sanic_tbl_smlt_tablemappingid': b['sanic_tbl_smlt_tablemappingid']} if b else {})}
asegurar_cm(boveda, 'sanic_cm_cp_clavepan', HMAC)
asegurar_cm(boveda, 'sanic_cm_cp_pan', 'TRIM(NumeroCuenta)')
asegurar_cm(boveda, 'sanic_cm_cp_name', 'CONCAT("****", RIGHT(TRIM(NumeroCuenta), 4))')
asegurar_cm(boveda, 'sanic_cm_cp_cuentaid', HMAC, LOOKUP, P + 'cuenta', 'sanic_cm_ct_clavepan')
# 3) Asignación (orden 5): dedup (clavepan, tipo) y lookup a la cuenta por la clave HMAC
asig = tabla(P + 'asignacion')[0]
asegurar_cm(asig, 'sanic_cm_as_clavepan', HMAC)
asegurar_cm(asig, 'sanic_cm_as_cuentaid', HMAC, LOOKUP, P + 'cuenta', 'sanic_cm_ct_clavepan')
cambiar_tm(asig, dedupcolumns='sanic_cm_as_clavepan,sanic_cm_as_tipo', processingorder=5)
# 4) Carga diaria (orden 5): dedup (clavepan, fecha) y lookup nuevo a la cuenta
carga = tabla(P + 'cargadiaria')[0]
asegurar_cm(carga, 'sanic_cm_cd_clavepan', HMAC)
asegurar_cm(carga, 'sanic_cm_cd_cuenta', HMAC, LOOKUP, P + 'cuenta', 'sanic_cm_ct_clavepan')
cambiar_tm(carga, dedupcolumns='sanic_cm_cd_clavepan,sanic_cm_cd_fechacarga', processingorder=5)
