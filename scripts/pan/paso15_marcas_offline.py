"""F8 y F6 · Marcas en lugar de rechazos (aditivo e idempotente).
El runtime offline del teléfono DESCARTA en silencio lo que el servidor rechaza y no lo reintenta. Por eso el servidor
ya no rechaza lo que viene del teléfono: lo acepta marcado.
Crea:
  sanic_cm_ad_sinvincular   "[AD] Sin vincular" (Sí/No, por defecto No): la actualización entró sin interacción
                            (plugin ActualizacionConInteraccion 1.0.9.0); VincularActualizacionPendiente la cuelga después.
  sanic_cm_pg_reciborepetido "[PG] Recibo repetido" (Sí/No, por defecto No): el número de recibo ya existía (plugin
                            ReciboRepetido); BackOffice lo revisa al transcribir.
Quita la clave alternativa sanic_cm_ak_pg_recibo_cuenta_moneda (APP-10, scripts/localizador/clave_recibo_pago.py): la
plataforma la aplica ANTES de los plugins, y offline un pago con el mismo recibo, cuenta y moneda se rechazaba y se
perdía en silencio. El repetido ahora entra marcado (ReciboRepetido).
Va ANTES de paso5 (paso5 exige que las columnas de cada paso existan).
Uso: python3 scripts/pan/paso15_marcas_offline.py [--aplicar]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq

APLICAR = '--aplicar' in sys.argv
SOL = {'MSCRM.SolutionUniqueName': 'cm_core_cobranzamovil'}
P = 'sanic_cm_tbl_'
def L(t): return {'LocalizedLabels': [{'Label': t, 'LanguageCode': 1033}]}
def existe_attr(e, a): return rq('GET', f"EntityDefinitions(LogicalName='{e}')/Attributes(LogicalName='{a}')?$select=LogicalName")[0] == 200
def hacer(desc, metodo, ruta, cuerpo):
    print(('APLICAR ' if APLICAR else 'PLAN    ') + desc)
    if APLICAR:
        st, b = rq(metodo, ruta, cuerpo, SOL)
        if st >= 300: raise SystemExit(f'   ✗ {st}: {b}')
        print('   ✓', st)

def booleana(entidad, schema, etiqueta, descripcion):
    if existe_attr(entidad, schema):
        print('YA EXISTE columna', schema)
        return
    hacer(f'columna {entidad}.{schema}', 'POST', f"EntityDefinitions(LogicalName='{entidad}')/Attributes", {
        '@odata.type': 'Microsoft.Dynamics.CRM.BooleanAttributeMetadata', 'SchemaName': schema,
        'DisplayName': L(etiqueta), 'Description': L(descripcion), 'RequiredLevel': {'Value': 'None'},
        'DefaultValue': False, 'IsAuditEnabled': {'Value': True},
        'OptionSet': {'TrueOption': {'Value': 1, 'Label': L('Sí')}, 'FalseOption': {'Value': 0, 'Label': L('No')}}})

booleana(P + 'actualizaciondatos', 'sanic_cm_ad_sinvincular', '[AD] Sin vincular',
         'La actualización llegó del teléfono sin su interacción. La pone el servidor (F8); se vincula sola al llegar la interacción.')
booleana(P + 'pago', 'sanic_cm_pg_reciborepetido', '[PG] Recibo repetido',
         'El número de recibo ya existía en otro pago. La pone el servidor (F6); BackOffice lo revisa.')
CLAVE = 'sanic_cm_ak_pg_recibo_cuenta_moneda'
st, b = rq('GET', f"EntityDefinitions(LogicalName='{P}pago')/Keys?$select=LogicalName,SchemaName")
if st >= 300: raise SystemExit(f'✗ {st}: {b}')
if any(k['LogicalName'] == CLAVE for k in b['value']):
    hacer(f'quitar clave {P}pago.{CLAVE}', 'DELETE', f"EntityDefinitions(LogicalName='{P}pago')/Keys(LogicalName='{CLAVE}')", None)
else:
    print('YA NO EXISTE clave', CLAVE)
if APLICAR:
    st, b = rq('POST', 'PublishXml', {'ParameterXml': f'<importexportxml><entities><entity>{P}actualizaciondatos</entity><entity>{P}pago</entity></entities></importexportxml>'})
    print('PublishXml →', st, '' if st < 300 else b)
