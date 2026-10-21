"""Borrado por salida de cartera · identidad en la interacción (aditivo e idempotente).
La cuenta y el cliente se borran 14 días después de dejar de venir en la carga (scripts/seguridad/paso10_borrado_programado.py)
y lo que crea la app vive 2 años. Al borrarse la cuenta, la interacción pierde el vínculo: por eso guarda con qué identificar
la cuenta en el core (decisión del dueño, 2026-10-21): los últimos 4, el ID del cliente (CIF) y la clave HMAC del número.
Nunca el número completo. Los pagos y las actualizaciones la heredan por su interacción.

Crea en sanic_cm_tbl_interaccion (mismo largo que la columna de la cuenta):
  sanic_cm_in_ultimos4   "[IN] Últimos 4"   ← sanic_cm_ct_ultimos4
  sanic_cm_in_cifcliente "[IN] CIF cliente" ← sanic_cm_ct_cifcliente
  sanic_cm_in_clavepan   "[IN] Clave PAN"   ← sanic_cm_ct_clavepan (HMAC)
Va ANTES de paso5 (1.0.9.2: IdentidadEnInteraccion las completa al crear). Con --rellenar completa las interacciones que ya
existen (solo lo vacío). Imprime solo conteos, nunca valores.
Uso: python3 scripts/pan/paso17_identidad_interaccion.py [--aplicar] [--rellenar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
RELLENAR = '--rellenar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
P = 'sanic_cm_tbl_'
COLUMNAS = [  # (columna de la interacción, etiqueta, columna de la cuenta)
    ('sanic_cm_in_ultimos4', '[IN] Últimos 4', 'sanic_cm_ct_ultimos4'),
    ('sanic_cm_in_cifcliente', '[IN] CIF cliente', 'sanic_cm_ct_cifcliente'),
    ('sanic_cm_in_clavepan', '[IN] Clave PAN', 'sanic_cm_ct_clavepan'),
]
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def atributo(e, a):
    st, b = rq('GET', f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')/Microsoft.Dynamics.CRM.StringAttributeMetadata?$select=LogicalName,MaxLength")
    return b if st == 200 else None

for col, etiqueta, origen in COLUMNAS:
    if atributo(P + 'interaccion', col):
        print('YA EXISTE columna', col)
        continue
    largo = atributo(P + 'cuenta', origen)['MaxLength']
    print(('APLICAR ' if APLICAR else 'PLAN    ') + f'columna {P}interaccion.{col} (texto {largo})')
    if APLICAR:
        st, b = rq('POST', f"EntityDefinitions(LogicalName='{P}interaccion')/Attributes", {
            '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata', 'SchemaName': col, 'MaxLength': largo,
            'FormatName': {'Value': 'Text'}, 'DisplayName': L(etiqueta), 'RequiredLevel': {'Value': 'None'},
            'Description': L(f'Copia de {origen} al crear la interacción (IdentidadEnInteraccion): identifica la cuenta en el core cuando la cuenta ya se borró.')}, SOL)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)
if APLICAR:
    st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{P}interaccion</entity></entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)

if RELLENAR:
    sel = ','.join(c for c, _, _ in COLUMNAS)
    filas = get(f"{P}interaccions?$select=_sanic_cm_in_cuentaid_value,{sel}&$filter=_sanic_cm_in_cuentaid_value ne null")['value']
    cuentas, completadas, sin_cuenta = {}, 0, 0
    for f in filas:
        faltan = [(c, o) for c, _, o in COLUMNAS if not (f.get(c) or '').strip()]
        if not faltan: continue
        cid = f['_sanic_cm_in_cuentaid_value']
        if cid not in cuentas:
            r = get(f"{P}cuentas?$select={','.join(o for _, _, o in COLUMNAS)}&$filter=sanic_cm_tbl_cuentaid eq {cid}")['value']
            cuentas[cid] = r[0] if r else None
        cta = cuentas[cid]
        if cta is None: sin_cuenta += 1; continue
        cambio = {c: cta[o] for c, o in faltan if (cta.get(o) or '').strip()}
        if not cambio: continue
        if APLICAR:
            st, b = rq('PATCH', f"{P}interaccions({f['sanic_cm_tbl_interaccionid']})", cambio)
            if st >= 300: raise SystemExit(f'✗ PATCH {st}: {str(b)[:200]}')
        completadas += 1
    print(f"relleno: {len(filas)} interacciones con cuenta · {'completadas' if APLICAR else 'a completar'}: {completadas} · cuenta no encontrada: {sin_cuenta}")
