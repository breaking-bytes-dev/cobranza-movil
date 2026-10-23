"""Registrar (o verificar) las Custom APIs globales de Cobranza Movil en DEV:
- DAT-14 (a): sanic_cm_SolicitarEspecial (plugins/Sanic.CobranzaMovil.Plugins/SolicitarEspecial.cs), desde 1.0.7.0.
- DAT-20 (camino A): sanic_cm_AltaCuenta (plugins/Sanic.CobranzaMovil.Plugins/AltaCuenta.cs), desde 1.0.8.0.
- Carga paralela: sanic_cm_PartirCarga (plugins/Sanic.CobranzaMovil.Plugins/PartirCarga.cs), desde 1.0.9.7. Requiere las
  plantillas de Smelter "Cobranza movil · padres" y "· partes" (scripts/pan/paso20_plantillas_paralelas.py --aplicar).
Ver docs/auditoria/01-pendientes.md.

Requisito: el ensamblado Sanic.CobranzaMovil.Plugins ya registrado con paso5_plugins.py en la version que trae el tipo de
cada API. Si el tipo de plugin no existe en el ensamblado, este script lo registra.

Idempotente: la API, sus parametros y sus propiedades de respuesta se buscan por nombre unico; lo que ya existe no se
duplica. Dataverse no deja cambiar el tipo de un parametro, si es opcional, ni el enlace o si es funcion de la API: si lo
registrado no coincide con lo definido aca, el script se detiene y lo dice (hay que borrar y recrear a mano).
Por defecto solo MUESTRA el plan (solo hace GET). Aplica con --aplicar.

Las apps las llaman con Environment.sanic_cm_<API>({...}): hay que agregar la fuente "Environment" en Studio. Sin
privilegio de ejecucion propio: quien puede llamarlas lo decide cada plugin (SolicitarEspecial: Solicitante, Admin o Servicio
y que pueda leer la cuenta; AltaCuenta y PartirCarga: solo Admin, Servicio o sin roles de CM, y ademas el llamador tiene que
ver el job y las plantillas de Smelter, o sea estar en el equipo de carga "CM · Admin y Carga").

Credenciales: las mismas que paso5_plugins.py (variables de entorno o pp_secrets.env). Nunca se imprimen.
Uso: python3 scripts/pan/paso12_custom_api.py [--aplicar] [--solucion cm_core_cobranzamovil] [--api sanic_cm_AltaCuenta]
"""
import argparse, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from paso5_plugins import Dataverse, cargar_config, ENSAMBLADO, NS, SOLUCION_POR_DEFECTO  # noqa: E402

ENLACE_GLOBAL, PASOS_NINGUNO = 0, 0
# Tipos de parametro de Custom API (customapifieldtype)
BOOLEANO, DECIMAL, ENTERO, TEXTO, GUID = 0, 2, 7, 10, 12


def definicion(api, nombre_visible, descripcion):
    return dict(uniquename=api, name=api, displayname=nombre_visible, description=descripcion,
                bindingtype=ENLACE_GLOBAL, isfunction=False, isprivate=False, allowedcustomprocessingsteptype=PASOS_NINGUNO)


APIS = [
    dict(
        api='sanic_cm_SolicitarEspecial', tipo='SolicitarEspecial', version=(1, 0, 7),
        definicion=definicion('sanic_cm_SolicitarEspecial', 'CM - Solicitar Especial',
                              'DAT-14: crea, reactiva o avisa que ya existe la asignacion Especial de una cuenta. '
                              'Valida el rol del llamador y que pueda leer la cuenta.'),
        parametros=[  # (nombre, tipo, opcional, descripcion)
            ('CuentaId', GUID, False, 'Id de la cuenta (sanic_cm_tbl_cuenta)'),
            ('Notas', TEXTO, False, 'Tipo y motivo de la solicitud (sin numeros de tarjeta; hasta 2000 caracteres)'),
            ('RetiroNio', DECIMAL, True, 'Retiro en cordobas (0 a 1.000.000.000)'),
            ('RetiroUsd', DECIMAL, True, 'Retiro en dolares (0 a 1.000.000.000)'),
        ],
        respuesta=[  # (nombre, tipo, descripcion)
            ('AsignacionId', GUID, 'Id de la asignacion Especial creada, reactivada o existente'),
            ('Resultado', TEXTO, '"creada", "reactivada" o "ya existe"'),
            ('SolicitadoPor', TEXTO, 'Si ya existe: nombre de quien la pidio; si no, vacio'),
            ('EstadoAprobacion', ENTERO, 'Estado de aprobacion (2 = Pendiente, 3 = Aprobado, 4 = Rechazada)'),
        ]),
    dict(
        api='sanic_cm_AltaCuenta', tipo='AltaCuenta', version=(1, 0, 8),
        definicion=definicion('sanic_cm_AltaCuenta', 'CM - Alta de cuenta',
                              'DAT-20: el Admin da de alta una cuenta como una importacion de una fila con la plantilla '
                              '"Cobranza movil" de Smelter. Devuelve el Import Job sin estado (la app lo pone En cola).'),
        parametros=[
            ('CIF', TEXTO, False, 'CIF del cliente (hasta 20 caracteres)'),
            ('Cliente', TEXTO, True, 'Nombre del cliente; obligatorio si el cliente es nuevo (si existe, se usa el de Dataverse)'),
            ('DireccionCasa', TEXTO, True, 'Direccion de la casa (cliente nuevo)'),
            ('DireccionEC', TEXTO, True, 'Direccion del estado de cuenta (cliente nuevo)'),
            ('DireccionTrabajo', TEXTO, True, 'Direccion del trabajo (cliente nuevo)'),
            ('Departamento', TEXTO, True, 'Departamento (cliente nuevo)'),
            ('Distrito', TEXTO, True, 'Distrito (cliente nuevo)'),
            ('Trabajo', TEXTO, True, 'Empleador (cliente nuevo)'),
            ('NumeroCelular', TEXTO, True, 'Celular (cliente nuevo)'),
            ('NumeroCasa', TEXTO, True, 'Telefono de la casa (cliente nuevo)'),
            ('NumeroCuenta', TEXTO, False, 'Numero de tarjeta: 13 a 19 digitos, Luhn. Nunca se guarda en tablas de CM'),
            ('SaldoVencidoNIO', DECIMAL, False, 'Saldo vencido en cordobas'),
            ('SaldoVencidoUSD', DECIMAL, False, 'Saldo vencido en dolares'),
            ('TotalSaldoVencido', DECIMAL, False, 'Total del saldo vencido'),
            ('ContagiadoUSD', DECIMAL, True, 'Contagiado en dolares'),
            ('PagoMinimoNIO', DECIMAL, True, 'Pago minimo en cordobas'),
            ('PagoMinimoUSD', DECIMAL, True, 'Pago minimo en dolares'),
            ('DiasMora', ENTERO, False, 'Dias de mora'),
            ('NivelMora', ENTERO, False, 'Nivel de mora'),
            ('FechaUltimoPago', TEXTO, True, 'Fecha del ultimo pago, aaaa-mm-dd'),
            ('FechaPago', TEXTO, True, 'Fecha de pago, aaaa-mm-dd'),
            ('Ciclo', TEXTO, True, 'Ciclo'),
            ('ProductoId', GUID, False, 'Id del producto (sanic_cm_tbl_producto) existente'),
            ('AgenteId', GUID, False, 'Id del agente (sanic_cm_tbl_agente) existente y activo'),
            ('NombreFianza', TEXTO, True, 'Nombre del fiador'),
            ('DireccionFianza', TEXTO, True, 'Direccion del fiador (si existe, se usa la de Dataverse)'),
            ('RetiroNIO', DECIMAL, True, 'Retiro en cordobas'),
            ('RetiroUSD', DECIMAL, True, 'Retiro en dolares'),
            ('TipoEspecial', TEXTO, True, 'Tipo de Especial'),
            ('Nota', TEXTO, True, 'Nota (sin numeros de tarjeta; hasta 2000 caracteres)'),
            ('TipoAsignacion', TEXTO, False, 'Diaria, Incobrable o Especial'),
            ('EsPrueba', BOOLEANO, False, 'true = prueba en seco de Smelter; false = carga real'),
        ],
        respuesta=[
            ('JobId', GUID, 'Id del Import Job de Smelter (sin estado: la app lo pone En cola despues de la espera)'),
            ('Advertencias', TEXTO, 'Avisos para el Admin, uno por linea'),
        ]),
    dict(
        api='sanic_cm_PartirCarga', tipo='PartirCarga', version=(1, 0, 9, 7),
        definicion=definicion('sanic_cm_PartirCarga', 'CM - Partir carga',
                              'Carga paralela: convierte el Import Job de la carga real (sin estado, plantilla "Cobranza movil") '
                              'en el de padres (producto, fiador y agente) y crea N jobs de partes por CIF, sin estado.'),
        parametros=[
            ('JobId', GUID, False, 'Id del Import Job (sanic_tbl_smlt_importjob) de la carga real, sin estado y con archivo'),
            ('Partes', ENTERO, False, 'Cantidad de partes pedidas (1 a 4; con menos de 300 filas se hace una sola)'),
            ('SoloValidar', BOOLEANO, True, 'Opcional (1.0.9.9): true = solo mide el largo de los valores, sin escribir (prueba en seco y carga en serie)'),
        ],
        respuesta=[
            ('PadresJobId', GUID, 'Id del job de padres (el mismo JobId, ya convertido)'),
            ('PartesJobIds', TEXTO, 'Ids de los jobs de partes, separados por coma, en orden (parte 1, 2, ...)'),
            ('Partes', ENTERO, 'Cantidad de partes creadas'),
            ('FilasArchivo', ENTERO, 'Filas de datos del archivo original'),
            ('FilasPadres', ENTERO, 'Filas del archivo de padres'),
            ('FilasPartes', TEXTO, 'Filas de cada parte, separadas por coma'),
        ]),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--solucion', default=SOLUCION_POR_DEFECTO)
    ap.add_argument('--aplicar', action='store_true')
    ap.add_argument('--api', help='registrar solo esta API (nombre unico); por defecto, todas')
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

    apis = [x for x in APIS if not a.api or x['api'] == a.api]
    if not apis:
        raise SystemExit('No hay una API llamada %s' % a.api)
    for x in apis:
        registrar(dv, accion, sol, a.solucion, mod, x)

    print('listo.' if mod else 'plan (sin cambios). Aplicar con --aplicar.')


def registrar(dv, accion, sol, solucion, mod, x):
    API, TIPO_PLUGIN, DEFINICION = x['api'], NS + x['tipo'], x['definicion']
    PARAMETROS, RESPUESTA, minima = x['parametros'], x['respuesta'], x['version']
    print('--- ' + API)
    # 1) Ensamblado y tipo de plugin ------------------------------------------------------------
    r = dv.get("pluginassemblies?$select=pluginassemblyid,version&$filter=name eq '%s'" % ENSAMBLADO)['value']
    if not r:
        raise SystemExit('No esta registrado el ensamblado %s: correr antes paso5_plugins.py' % ENSAMBLADO)
    ensamblado_id, version = r[0]['pluginassemblyid'], r[0].get('version') or '?'
    print('ensamblado %s %s' % (ENSAMBLADO, version))
    if tuple(int(n) for n in version.split('.')[:4] if n.isdigit()) < minima:
        print('AVISO   el ensamblado registrado es anterior a %s: no trae %s. Registrar la dll nueva antes de aplicar.'
              % ('.'.join(str(n) for n in (minima + (0, 0, 0, 0))[:4]), x['tipo']))
    t = dv.get("plugintypes?$select=plugintypeid&$filter=_pluginassemblyid_value eq %s and typename eq '%s'"
               % (ensamblado_id, TIPO_PLUGIN))['value']
    tipo_id = t[0]['plugintypeid'] if t else None
    if not tipo_id:
        b = accion('registrar tipo de plugin ' + TIPO_PLUGIN, 'POST', 'plugintypes',
                   {'typename': TIPO_PLUGIN, 'name': TIPO_PLUGIN, 'friendlyname': x['tipo'],
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
        b = accion('crear Custom API %s (global, accion, sin pasos personalizados) en la solucion %s' % (API, solucion),
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


if __name__ == '__main__':
    main()
