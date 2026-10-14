"""DAT-14 (a): registrar (o verificar) la Custom API global sanic_cm_SolicitarEspecial en DEV.
Ver plugins/Sanic.CobranzaMovil.Plugins/SolicitarEspecial.cs (comportamiento) y docs/auditoria/01-pendientes.md (DAT-14).

Requisito: el ensamblado Sanic.CobranzaMovil.Plugins 1.0.7.0 o posterior ya registrado con paso5_plugins.py (trae el tipo
SolicitarEspecial). Si el tipo de plugin no existe en el ensamblado, este script lo registra.

Idempotente: la API, sus parametros y sus propiedades de respuesta se buscan por nombre unico; lo que ya existe no se
duplica. Dataverse no deja cambiar el tipo de un parametro, si es opcional, ni el enlace o si es funcion de la API: si lo
registrado no coincide con lo definido aca, el script se detiene y lo dice (hay que borrar y recrear a mano).
Por defecto solo MUESTRA el plan (solo hace GET). Aplica con --aplicar.

La app (scrNuevaSolicitud) la llama con Environment.sanic_cm_SolicitarEspecial({...}): hay que agregar la fuente
"Environment" en Studio. Sin privilegio de ejecucion propio: quien puede pedirla lo decide el plugin (rol Solicitante,
Admin o Servicio, y que el llamador pueda leer la cuenta).

Credenciales: las mismas que paso5_plugins.py (variables de entorno o pp_secrets.env). Nunca se imprimen.
Uso: python3 scripts/pan/paso12_custom_api.py [--aplicar] [--solucion cm_core_cobranzamovil]
"""
import argparse, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from paso5_plugins import Dataverse, cargar_config, ENSAMBLADO, NS, SOLUCION_POR_DEFECTO  # noqa: E402

API = 'sanic_cm_SolicitarEspecial'
TIPO_PLUGIN = NS + 'SolicitarEspecial'
ENLACE_GLOBAL, PASOS_NINGUNO = 0, 0
# Tipos de parametro de Custom API (customapifieldtype)
DECIMAL, ENTERO, TEXTO, GUID = 2, 7, 10, 12

DEFINICION = dict(
    uniquename=API, name=API, displayname='CM - Solicitar Especial',
    description='DAT-14: crea, reactiva o avisa que ya existe la asignacion Especial de una cuenta. '
                'Valida el rol del llamador y que pueda leer la cuenta.',
    bindingtype=ENLACE_GLOBAL, isfunction=False, isprivate=False, allowedcustomprocessingsteptype=PASOS_NINGUNO)

PARAMETROS = [  # (nombre, tipo, opcional, descripcion)
    ('CuentaId', GUID, False, 'Id de la cuenta (sanic_cm_tbl_cuenta)'),
    ('Notas', TEXTO, False, 'Tipo y motivo de la solicitud (sin numeros de tarjeta; hasta 2000 caracteres)'),
    ('RetiroNio', DECIMAL, True, 'Retiro en cordobas (0 a 1.000.000.000)'),
    ('RetiroUsd', DECIMAL, True, 'Retiro en dolares (0 a 1.000.000.000)'),
]
RESPUESTA = [  # (nombre, tipo, descripcion)
    ('AsignacionId', GUID, 'Id de la asignacion Especial creada, reactivada o existente'),
    ('Resultado', TEXTO, '"creada", "reactivada" o "ya existe"'),
    ('SolicitadoPor', TEXTO, 'Si ya existe: nombre de quien la pidio; si no, vacio'),
    ('EstadoAprobacion', ENTERO, 'Estado de aprobacion (2 = Pendiente, 3 = Aprobado, 4 = Rechazada)'),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--solucion', default=SOLUCION_POR_DEFECTO)
    ap.add_argument('--aplicar', action='store_true')
    a = ap.parse_args()

    dv = Dataverse(cargar_config())
    mod = a.aplicar
    print('MODO:', 'APLICAR' if mod else 'PLAN (solo lectura; usar --aplicar para registrar)')
    sol = {'MSCRM.SolutionUniqueName': a.solucion, 'Prefer': 'return=representation'}

    def accion(desc, metodo, ruta, cuerpo=None, headers=None):
        print(('APLICAR ' if mod else 'PLAN    ') + desc)
        if not mod: return None
        st, b = dv.rq(metodo, ruta, cuerpo, headers)
        if st >= 300:
            raise SystemExit('   FALLO %s: %s' % (st, b))
        print('   ok', st)
        return b

    if not dv.get("solutions?$select=uniquename&$filter=uniquename eq '%s'" % a.solucion)['value']:
        raise SystemExit('No existe la solucion ' + a.solucion)

    # 1) Ensamblado y tipo de plugin ------------------------------------------------------------
    r = dv.get("pluginassemblies?$select=pluginassemblyid,version&$filter=name eq '%s'" % ENSAMBLADO)['value']
    if not r:
        raise SystemExit('No esta registrado el ensamblado %s: correr antes paso5_plugins.py con la dll 1.0.7.0' % ENSAMBLADO)
    ensamblado_id, version = r[0]['pluginassemblyid'], r[0].get('version') or '?'
    print('ensamblado %s %s' % (ENSAMBLADO, version))
    if tuple(int(x) for x in version.split('.')[:3] if x.isdigit()) < (1, 0, 7):
        print('AVISO   el ensamblado registrado es anterior a 1.0.7.0: no trae SolicitarEspecial. Registrar la dll nueva antes de aplicar.')
    t = dv.get("plugintypes?$select=plugintypeid&$filter=_pluginassemblyid_value eq %s and typename eq '%s'"
               % (ensamblado_id, TIPO_PLUGIN))['value']
    tipo_id = t[0]['plugintypeid'] if t else None
    if not tipo_id:
        b = accion('registrar tipo de plugin ' + TIPO_PLUGIN, 'POST', 'plugintypes',
                   {'typename': TIPO_PLUGIN, 'name': TIPO_PLUGIN, 'friendlyname': 'SolicitarEspecial',
                    'pluginassemblyid@odata.bind': '/pluginassemblies(%s)' % ensamblado_id}, {'Prefer': 'return=representation'})
        if b: tipo_id = b['plugintypeid']

    # 2) Custom API -----------------------------------------------------------------------------
    campos = ','.join(['customapiid', '_plugintypeid_value'] + [k for k in DEFINICION if k not in ('description',)])
    c = dv.get("customapis?$select=%s&$filter=uniquename eq '%s'" % (campos, API))['value']
    api_id = None
    if c:
        api_id = c[0]['customapiid']
        inmutables = [k for k in ('bindingtype', 'isfunction', 'allowedcustomprocessingsteptype') if c[0].get(k) != DEFINICION[k]]
        if inmutables:
            raise SystemExit('La Custom API %s ya existe con otro valor en %s (no se puede cambiar): borrarla y recrearla.'
                             % (API, ', '.join(inmutables)))
        if tipo_id and c[0].get('_plugintypeid_value') != tipo_id:
            accion('enlazar la Custom API %s al plugin %s' % (API, TIPO_PLUGIN), 'PATCH', 'customapis(%s)' % api_id,
                   {'PluginTypeId@odata.bind': '/plugintypes(%s)' % tipo_id})
        else:
            print('ok      Custom API %s ya existe y apunta al plugin' % API)
    else:
        cuerpo = dict(DEFINICION)
        if tipo_id: cuerpo['PluginTypeId@odata.bind'] = '/plugintypes(%s)' % tipo_id
        b = accion('crear Custom API %s (global, accion, sin pasos personalizados) en la solucion %s' % (API, a.solucion),
                   'POST', 'customapis', cuerpo, sol)
        if b: api_id = b['customapiid']

    # 3) Parametros de entrada y propiedades de respuesta ------------------------------------------
    def asegurar(coleccion, nombre, tipo, descripcion, opcional=None):
        existente = []
        if api_id:
            existente = dv.get("%s?$select=uniquename,type%s&$filter=_customapiid_value eq %s and uniquename eq '%s'"
                               % (coleccion, ',isoptional' if opcional is not None else '', api_id, nombre))['value']
        if existente:
            e = existente[0]
            if e['type'] != tipo or (opcional is not None and e['isoptional'] != opcional):
                raise SystemExit('%s %s ya existe con otro tipo u opcionalidad (no se puede cambiar): borrarlo y recrearlo.'
                                 % (coleccion, nombre))
            print('ok      %s %s' % (coleccion, nombre))
            return
        cuerpo = {'uniquename': nombre, 'name': '%s.%s' % (API, nombre), 'displayname': nombre,
                  'description': descripcion, 'type': tipo}
        if opcional is not None: cuerpo['isoptional'] = opcional
        if api_id: cuerpo['CustomAPIId@odata.bind'] = '/customapis(%s)' % api_id
        accion('crear %s %s (tipo %s%s)' % (coleccion, nombre, tipo, ', opcional' if opcional else ''), 'POST', coleccion, cuerpo, sol)

    for nombre, tipo, opcional, descripcion in PARAMETROS:
        asegurar('customapirequestparameters', nombre, tipo, descripcion, opcional)
    for nombre, tipo, descripcion in RESPUESTA:
        asegurar('customapiresponseproperties', nombre, tipo, descripcion)

    print('listo.' if mod else 'plan (sin cambios). Aplicar con --aplicar.')


if __name__ == '__main__':
    main()
