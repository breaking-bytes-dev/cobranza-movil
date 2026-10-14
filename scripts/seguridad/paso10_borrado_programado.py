"""Fase 4 · Paso 10: trabajos de borrado programado (Bulk Deletion) de Cobranza Móvil, diarios.

- DEC-019: la carga diaria (sanic_cm_tbl_cargadiaria) guarda una foto por cuenta y por día y nada la borraba (~1000 filas por
  día en DEV). Se conservan los últimos 60 días (decisión del dueño, 2026-10-14). Monitoreo solo lee la fecha de la última
  carga. Las cuentas y clientes no dependen de estas filas.
- Fiadores sin nombre: cada fila del CSV con NombreFianza vacío hace que la carga cree un fiador vacío nuevo (la clave
  alternativa por nombre no rige con NULL). Ninguna cuenta los referencia (2777 en DEV, todos huérfanos). Se borran a diario.
  Un fiador referenciado no se puede borrar (Fiador→Cuenta es Restrict): el trabajo nunca rompe una cuenta.
  La corrección de origen (que la plantilla de Smelter no cree el fiador si viene vacío) es configuración de Smelter.

El primer ciclo corre al aplicar y borra lo acumulado; después, una vez por día a la misma hora.
Idempotente: un trabajo con el mismo nombre que no esté terminado no se vuelve a crear.
Uso: python3 paso10_borrado_programado.py [--aplicar]"""
import sys, datetime
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
DIAS_CARGA = 60
OPERACION_BULK_DELETE, TERMINADO = 13, 3

TRABAJOS = [
    ('CM - Carga diaria de mas de %d dias' % DIAS_CARGA, 'sanic_cm_tbl_cargadiaria',
     {'AttributeName': 'sanic_cm_cd_fechacarga', 'Operator': 'OlderThanXDays',
      'Values': [{'Type': 'System.Int32', 'Value': str(DIAS_CARGA)}]},
     f"sanic_cm_tbl_cargadiarias?$select=sanic_cm_tbl_cargadiariaid&$count=true&$top=1"
     f"&$filter=Microsoft.Dynamics.CRM.OlderThanXDays(PropertyName='sanic_cm_cd_fechacarga',PropertyValue={DIAS_CARGA})"),
    ('CM - Fiadores sin nombre', 'sanic_cm_tbl_fiador',
     {'AttributeName': 'sanic_cm_fi_nombre', 'Operator': 'Null', 'Values': []},
     "sanic_cm_tbl_fiadors?$select=sanic_cm_tbl_fiadorid&$count=true&$top=1&$filter=sanic_cm_fi_nombre eq null"),
]

err = 0
for nombre, tabla, condicion, consulta in TRABAJOS:
    hoy = get(consulta).get('@odata.count')
    vivos = get(f"asyncoperations?$select=asyncoperationid,statecode,recurrencepattern"
                f"&$filter=operationtype eq {OPERACION_BULK_DELETE} and name eq '{nombre}' and statecode ne {TERMINADO}")['value']
    if vivos:
        print(f'"{nombre}": ya programado ({len(vivos)} trabajo(s), recurrencia {vivos[0].get("recurrencepattern") or "-"}). Hoy cumplen: {hoy}')
        continue
    print(f'"{nombre}": se programa (diario). Hoy cumplen: {hoy}')
    if not APLICAR:
        continue
    cuerpo = {
        'QuerySet': [{'EntityName': tabla,
                      'Criteria': {'FilterOperator': 'And', 'Conditions': [condicion], 'Filters': []}}],
        'JobName': nombre, 'SendEmailNotification': False, 'ToRecipients': [], 'CCRecipients': [],
        'RecurrencePattern': 'FREQ=DAILY;INTERVAL=1;',
        'StartDateTime': (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(minutes=1)).strftime('%Y-%m-%dT%H:%M:%SZ'),
    }
    st, b = rq('POST', 'BulkDelete', cuerpo)
    if st >= 300:
        err += 1; print('  error HTTP', st, str(b)[:300])
    else:
        print('  programado. JobId', b.get('JobId'))
print('listo.' if APLICAR else 'plan (sin cambios). Aplicar con --aplicar.')
sys.exit(1 if err else 0)
