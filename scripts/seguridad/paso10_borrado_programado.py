"""Configura los trabajos diarios de borrado en masa (Bulk Deletion) de Cobranza Móvil.
Retención:
  - Los datos del cliente viven mientras tenga una cuenta en la app, más 14 días de gracia (7 de la carga diaria + 7).
  - Lo que crea la app (interacciones, pagos, actualizaciones) vive 2 años. La interacción guarda últimos 4, CIF y clave
    HMAC de su cuenta (plugin IdentidadEnInteraccion), así sigue identificable en el core.

Trabajos (centro de administración → Borrado de registros en masa):
  1. Carga diaria de más de 7 días.
  2. Cuentas fuera de cartera hace más de 14 días: sin cambios en 14 días, sin asignación viva (activa, o que vino en la
     carga o se cerró en los últimos 14 días) y sin interacciones sin grabar. Por cascada se borran su bóveda (número
     completo) y sus asignaciones; sus interacciones y pagos quedan sin el vínculo.
  3. Clientes sin cuentas: por cascada, sus direcciones y teléfonos. Mientras tenga carga diaria (≤ 7 días) Dataverse no
     deja borrarlo (Restrict).
  4. Fiadores sin cuentas. Un fiador con cuentas no se borra (Restrict).
  5. Pagos y actualizaciones de más de 730 días y, después, interacciones de más de 735 días: la interacción no se puede
     borrar mientras tenga pagos o actualizaciones (Restrict), por eso va unos días después.
Los "sin registros relacionados" son uniones externas con la condición "nulo"; Dataverse las respeta en el borrado en masa.

Cada trabajo se crea como Data Life Cycle Config (retentionconfig, tipo borrado) dentro de la solución, para que viaje
con ella a otros ambientes. Retira los trabajos de borrado sueltos (fuera de la solución) con los mismos nombres y los
obsoletos "CM - Carga diaria de mas de 60 dias" y "CM - Fiadores sin nombre".
Corre todos los días a las 09:00 UTC. Idempotente por nombre. Imprime solo conteos.
Uso: python3 paso10_borrado_programado.py [--aplicar]"""
import sys, datetime, json
from xml.sax.saxutils import quoteattr
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
SOLUCION = 'cm_core_cobranzamovil'
TIPO_BORRADO = 1
CARGA_DIAS, GRACIA_DIAS, APP_DIAS, INTERACCION_DIAS, RECIEN_DIAS = 7, 14, 730, 735, 2
OPERACION_BULK_DELETE, TERMINADO, CANCELADO = 13, 3, 32
RETIRADOS = ['CM - Carga diaria de mas de 60 dias', 'CM - Fiadores sin nombre']
P = 'sanic_cm_tbl_'

def c(attr, op, valor=None, alias=None):
    return dict(attr=attr, op=op, valor=valor, alias=alias)
def union(entidad, desde, hacia, alias, vivas=()):
    """Unión externa a 'entidad' (entidad.hacia = principal.desde); con 'vivas' (OR) solo cuentan esas filas."""
    return dict(entidad=entidad, desde=desde, hacia=hacia, alias=alias, vivas=list(vivas))

TRABAJOS = [
    (f'CM - Carga diaria de mas de {CARGA_DIAS} dias', P + 'cargadiaria',
     [c('sanic_cm_cd_fechacarga', 'OlderThanXDays', CARGA_DIAS)], []),
    (f'CM - Cuentas fuera de cartera hace mas de {GRACIA_DIAS} dias', P + 'cuenta',
     [c('modifiedon', 'OlderThanXDays', GRACIA_DIAS),
      c(P + 'asignacionid', 'Null', alias='viva'), c(P + 'interaccionid', 'Null', alias='pend')],
     [union(P + 'asignacion', P + 'cuentaid', 'sanic_cm_as_cuentaid', 'viva',
            [c('sanic_cm_as_estado', 'Equal', True), c('sanic_cm_as_ultimacarga', 'LastXDays', GRACIA_DIAS),
             c('sanic_cm_as_fechacierre', 'LastXDays', GRACIA_DIAS)]),
      union(P + 'interaccion', P + 'cuentaid', 'sanic_cm_in_cuentaid', 'pend',
            [c('sanic_cm_in_estadograbacion', 'Equal', 1)])]),
    ('CM - Clientes sin cuentas', P + 'cliente',
     [c('modifiedon', 'OlderThanXDays', RECIEN_DIAS), c(P + 'cuentaid', 'Null', alias='cta')],
     [union(P + 'cuenta', P + 'clienteid', 'sanic_cm_ct_clienteid', 'cta')]),
    ('CM - Fiadores sin cuentas', P + 'fiador',
     [c('modifiedon', 'OlderThanXDays', RECIEN_DIAS), c(P + 'cuentaid', 'Null', alias='cta')],
     [union(P + 'cuenta', P + 'fiadorid', 'sanic_cm_ct_fiador', 'cta')]),
    ('CM - Pagos de mas de 2 anos', P + 'pago', [c('createdon', 'OlderThanXDays', APP_DIAS)], []),
    ('CM - Actualizaciones de datos de mas de 2 anos', P + 'actualizaciondatos', [c('createdon', 'OlderThanXDays', APP_DIAS)], []),
    ('CM - Interacciones de mas de 2 anos', P + 'interaccion', [c('createdon', 'OlderThanXDays', INTERACCION_DIAS)], []),
]

# ---- FetchXML del borrado (y la misma consulta agregada, para contar lo que cumple hoy)
FX_OP = {'OlderThanXDays': 'olderthan-x-days', 'LastXDays': 'last-x-days', 'Null': 'null', 'Equal': 'eq'}
def fx_cond(x):
    v = '' if x['valor'] is None else ' value=' + quoteattr(str(int(x['valor'])) if isinstance(x['valor'], bool) else str(x['valor']))
    e = f' entityname="{x["alias"]}"' if x['alias'] else ''
    return f'<condition{e} attribute="{x["attr"]}" operator="{FX_OP[x["op"]]}"{v}/>'
def fx(tabla, conds, uniones, contar=True):
    links = ''.join(f'<link-entity name="{u["entidad"]}" from="{u["hacia"]}" to="{u["desde"]}" link-type="outer" alias="{u["alias"]}">'
                    + (f'<filter type="or">{"".join(fx_cond(x) for x in u["vivas"])}</filter>' if u['vivas'] else '')
                    + '</link-entity>' for u in uniones)
    filtro = f'<filter type="and">{"".join(fx_cond(x) for x in conds)}</filter>'
    if not contar:
        return (f'<fetch version="1.0" mapping="logical" distinct="true"><entity name="{tabla}"><attribute name="{tabla}id"/>'
                f'{links}{filtro}</entity></fetch>')
    return (f'<fetch aggregate="true"><entity name="{tabla}"><attribute name="{tabla}id" alias="n" aggregate="countcolumn" distinct="true"/>'
            f'{links}{filtro}</entity></fetch>')
def conjunto(tabla):
    st, b = rq('GET', f"EntityDefinitions(LogicalName='{tabla}')?$select=EntitySetName")
    return b['EntitySetName']
def cumplen(tabla, conds, uniones):
    import urllib.parse
    st, b = rq('GET', f"{conjunto(tabla)}?fetchXml=" + urllib.parse.quote(fx(tabla, conds, uniones)))
    if st >= 300: return f'? ({st})'
    return b['value'][0]['n']

def sueltos(nombre):
    """Trabajos de borrado en curso con ese nombre que no son de una Data Life Cycle Config."""
    propios = {r['asyncoperationid'] for r in get("retentionconfigs?$select=asyncoperationid")['value']}
    return [j for j in get(f"asyncoperations?$select=asyncoperationid&$filter=operationtype eq {OPERACION_BULK_DELETE} "
                           f"and name eq '{nombre}' and statecode ne {TERMINADO}")['value'] if j['asyncoperationid'] not in propios]
def configurado(nombre):
    return get(f"retentionconfigs?$select=retentionconfigid&$filter=name eq '{nombre}' and type eq {TIPO_BORRADO}")['value']
def cancelar(nombre, motivo):
    global err
    for j in sueltos(nombre):
        print(f'"{nombre}": se retira el trabajo suelto ({motivo}).')
        if APLICAR:
            st, b = rq('PATCH', f"asyncoperations({j['asyncoperationid']})", {'statecode': TERMINADO, 'statuscode': CANCELADO})
            print('   ✓' if st < 300 else f'   ✗ {st} {str(b)[:200]}'); err += st >= 300

err = 0
for nombre in RETIRADOS: cancelar(nombre, 'obsoleto')
for nombre, tabla, conds, uniones in TRABAJOS:
    hoy = cumplen(tabla, conds, uniones)
    if configurado(nombre):
        print(f'"{nombre}": ya está en la solución. Hoy cumplen: {hoy}')
    else:
        print(f'"{nombre}": se crea en la solución (diario). Hoy cumplen: {hoy}')
        if APLICAR:
            inicio = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=1)).strftime('%Y-%m-%dT09:00:00Z')
            criterio = {'QuerySet': [fx(tabla, conds, uniones, contar=False)], 'SendEmailNotification': False,
                        'ToRecipients': [], 'CCRecipients': []}
            cuerpo = {'name': nombre, 'uniquename': nombre, 'type': TIPO_BORRADO, 'entitylogicalname': tabla,
                      'recurrence': 'FREQ=DAILY;INTERVAL=1', 'starttime': inicio, 'criteria': json.dumps(criterio)}
            st, b = rq('POST', 'retentionconfigs', cuerpo, {'MSCRM.SolutionUniqueName': SOLUCION})
            if st >= 300:
                err += 1; print('  error HTTP', st, str(b)[:300]); continue
            print('  creado.')
    cancelar(nombre, 'lo reemplaza el de la solución')
print('listo.' if APLICAR else 'plan (sin cambios). Aplicar con --aplicar.')
sys.exit(1 if err else 0)
