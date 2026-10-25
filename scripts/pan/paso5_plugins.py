"""Fase 2 · Paso 5: registrar (o actualizar) el ensamblado Sanic.CobranzaMovil.Plugins y sus pasos en DEV.
Ver docs/auditoria/fase2-pan-diseno.md §2.6 y §4 (filas 5 y 8) y plugins/Sanic.CobranzaMovil.Plugins/README.md.

Idempotente: el ensamblado y cada paso se buscan por nombre; lo que ya existe se actualiza, nunca se duplica.
Por defecto solo MUESTRA el plan (solo hace GET). Aplica con --aplicar.

REGLA DE PanGuard (paso 8 del plan):
  Hasta el paso 8 la carga de Smelter todavia escribe el PAN en las columnas que PanGuard vigila, asi que sus
  pasos se crean DESACTIVADOS. Un paso PanGuard que ya existe conserva su estado, salvo que se pida:
    --activar-panguard      activa los pasos PanGuard (paso 8)
    --desactivar-panguard   los desactiva (rollback)
  Los pasos de ClaveDesdeCuenta, CuentaDesdeInteraccion, AprobacionEspecial, ActualizacionConInteraccion, EmailDesdeUsuario,
  RepartoAsignacion, RepartoPorAgente, ColumnasAsignacion, CargaSobreAsignacion, Reclasificacion, InteraccionDelCobrador,
  GrabacionInteraccion, VincularActualizacionPendiente, ReciboRepetido, EspecialDesdeCuenta, IdentidadEnInteraccion,
  TramoCartera y TipoDesdeProducto quedan activos.

Las Custom APIs (SolicitarEspecial, AltaCuenta y, desde 1.0.9.7, PartirCarga de la carga paralela) no tienen pasos: sus tipos
vienen en la misma dll y las define scripts/pan/paso12_custom_api.py DESPUES de este paso. PartirCarga necesita ademas las
plantillas de Smelter "Cobranza movil · padres" y "· partes" (scripts/pan/paso20_plantillas_paralelas.py --aplicar).

RepartoAsignacion y RepartoPorAgente son ASINCRONOS (PostOperation, borran el trabajo del sistema si termina bien).
RepartoAsignacion reemplaza al flow "Cambio de Owner en Asignaciones"; nunca sincrono: correria dentro del trozo de 100 filas
de Smelter (2 minutos por mensaje). RepartoPorAgente (DAT-08) re-reparte las asignaciones de un agente al cambiar su usuario.
Ambos necesitan el equipo "Cartera sin cobrador" (scripts/seguridad/paso5_cartera_sin_cobrador.py).
Reclasificacion es PostOperation SINCRONO: cierra la asignacion del otro tipo en la misma transaccion que la fila de la carga.

Credenciales: variables de entorno DATAVERSE_URL, PP_CLIENT_ID, PP_CLIENT_SECRET, PP_TENANT_ID (las mismas
secrets del repo). En local, si faltan, se leen de pp_secrets.env (gitignored). Nunca se imprimen.

Uso (CI):
  python3 scripts/pan/paso5_plugins.py --dll Sanic.CobranzaMovil.Plugins.dll --version 1.0.0.0 \
      --public-key-token <16 hex> --aplicar [--activar-panguard]
"""
import argparse, base64, json, os, sys, time
import urllib.error, urllib.parse, urllib.request

ENSAMBLADO = 'Sanic.CobranzaMovil.Plugins'
NS = ENSAMBLADO + '.'
SOLUCION_POR_DEFECTO = 'cm_core_cobranzamovil'
P = 'sanic_cm_tbl_'
COMPONENTE_ENSAMBLADO, COMPONENTE_PASO = 91, 92
ETAPA_PREOPERATION, MODO_SINCRONO = 20, 0
ETAPA_POSTOPERATION, MODO_ASINCRONO = 40, 1
IMAGEN_PREVIA = 'PreImage'


# ---------------------------------------------------------------------------------------------- definicion
def definir_pasos():
    """Lista declarativa de pasos. 'legacy' = columna en desuso que el paso 8d elimina: si ya no existe,
    el paso se retira en lugar de registrarse."""
    pasos = []
    # PanGuard: (tabla, columna vigilada, es_legacy)
    for tabla, columna, legacy in [
        (P + 'cuenta', 'sanic_cm_ct_numerodecuenta', False),
        (P + 'cargadiaria', 'sanic_cm_cd_numerocuenta', False),
        (P + 'cuentapan', 'sanic_cm_cp_name', False),
        (P + 'asignacion', 'sanic_cm_as_numerocuenta', True),
        (P + 'interaccion', 'sanic_cm_in_numerocuenta', True),
        (P + 'pago', 'sanic_cm_pg_numerocuenta', True),
    ]:
        for mensaje in ('Create', 'Update'):
            pasos.append(dict(
                tipo=NS + 'PanGuard', mensaje=mensaje, entidad=tabla, panguard=True,
                # Create no admite filtro de atributos; Update solo dispara si viene la columna vigilada.
                filtro=[columna] if mensaje == 'Update' else [], columnas=[columna],
                legacy=legacy, imagen=None,
                descripcion='PCI: rechaza un PAN en claro en ' + columna if not legacy
                else 'PCI: la columna en desuso ' + columna + ' debe quedar vacia'))
    # ClaveDesdeCuenta: Create y Update de asignacion
    pasos.append(dict(
        tipo=NS + 'ClaveDesdeCuenta', mensaje='Create', entidad=P + 'asignacion', panguard=False, filtro=[],
        columnas=['sanic_cm_as_cuentaid', 'sanic_cm_as_clavepan'], legacy=False, imagen=None,
        descripcion='Completa as_clavepan desde la cuenta (o rechaza si no coincide)'))
    pasos.append(dict(
        tipo=NS + 'ClaveDesdeCuenta', mensaje='Update', entidad=P + 'asignacion', panguard=False,
        filtro=['sanic_cm_as_cuentaid', 'sanic_cm_as_clavepan'],
        columnas=['sanic_cm_as_cuentaid', 'sanic_cm_as_clavepan'], legacy=False,
        imagen=dict(nombre=IMAGEN_PREVIA, atributos=['sanic_cm_as_cuentaid']),
        descripcion='Completa as_clavepan desde la cuenta (o rechaza si no coincide)'))
    # CuentaDesdeInteraccion: Create de pago
    pasos.append(dict(
        tipo=NS + 'CuentaDesdeInteraccion', mensaje='Create', entidad=P + 'pago', panguard=False, filtro=[],
        columnas=['sanic_cm_pg_cuentaid', 'sanic_cm_pg_interaccion'], legacy=False, imagen=None,
        descripcion='Completa pg_cuentaid desde la interaccion'))
    # AprobacionEspecial (S-6): solo Supervisor/Admin aprueban, rechazan o ponen agente a una Especial
    for mensaje in ('Create', 'Update'):
        cols = ['sanic_cm_as_agenteid', 'sanic_cm_as_estadoaprobacion', 'sanic_cm_as_tipo']
        pasos.append(dict(
            tipo=NS + 'AprobacionEspecial', mensaje=mensaje, entidad=P + 'asignacion', panguard=False,
            filtro=cols if mensaje == 'Update' else [], columnas=cols, legacy=False,
            imagen=dict(nombre=IMAGEN_PREVIA, atributos=cols) if mensaje == 'Update' else None,
            descripcion='S-6: solo Supervisor/Admin aprueban, rechazan o asignan agente a una Especial'))
    # ActualizacionConInteraccion (APP-24): la actualizacion de datos cuelga de una interaccion con cuenta
    for mensaje in ('Create', 'Update'):
        pasos.append(dict(
            tipo=NS + 'ActualizacionConInteraccion', mensaje=mensaje, entidad=P + 'actualizaciondatos', panguard=False,
            filtro=['sanic_cm_ad_interaccionid'] if mensaje == 'Update' else [],
            columnas=['sanic_cm_ad_interaccionid'], legacy=False, imagen=None,
            descripcion='APP-24: exige interaccion con cuenta'))
    # EmailDesdeUsuario: el correo del agente es el UPN en minusculas del usuario vinculado (gblAgente de la app)
    for mensaje in ('Create', 'Update'):
        pasos.append(dict(
            tipo=NS + 'EmailDesdeUsuario', mensaje=mensaje, entidad=P + 'agente', panguard=False,
            filtro=['sanic_cm_ag_usuarioid'] if mensaje == 'Update' else [],
            columnas=['sanic_cm_ag_usuarioid', 'sanic_cm_ag_emailusuario'], legacy=False, imagen=None,
            descripcion='Correo del agente = UPN (minusculas) del usuario vinculado'))
    # RepartoAsignacion: dueno al cobrador, compartir cuenta/cliente/fiador y revocar al anterior. ASINCRONO.
    for mensaje in ('Create', 'Update'):
        cols = ['sanic_cm_as_agenteid', 'sanic_cm_as_estado', 'sanic_cm_as_estadoaprobacion']
        pasos.append(dict(
            tipo=NS + 'RepartoAsignacion', mensaje=mensaje, entidad=P + 'asignacion', panguard=False,
            filtro=cols if mensaje == 'Update' else [], columnas=cols + ['sanic_cm_as_solicitadopor'], legacy=False,
            imagen=None, etapa=ETAPA_POSTOPERATION, modo=MODO_ASINCRONO,
            descripcion='Reparto: dueno al cobrador, compartidos de cuenta/cliente/fiador y revocacion al anterior (asincrono)'))
    # RepartoPorAgente (DAT-08): al vincular, cambiar o quitar el usuario del agente, re-reparte sus asignaciones. ASINCRONO.
    pasos.append(dict(
        tipo=NS + 'RepartoPorAgente', mensaje='Update', entidad=P + 'agente', panguard=False,
        filtro=['sanic_cm_ag_usuarioid'], columnas=['sanic_cm_ag_usuarioid'], legacy=False,
        imagen=None, etapa=ETAPA_POSTOPERATION, modo=MODO_ASINCRONO,
        descripcion='DAT-08: al cambiar el usuario del agente, re-reparte sus asignaciones activas (asincrono)'))
    # ColumnasAsignacion (S-11): cada rol de CM escribe solo las columnas de la asignacion que usa su pantalla. Sin filtro
    # A PROPOSITO: controla cualquier columna sanic_cm_as_* (un filtro dejaria pasar las que no lista). Desde 1.0.9.6 no le
    # consulta roles a SYSTEM (la carga), asi que en cada fila de la carga ya no cuesta consultas.
    pasos.append(dict(
        tipo=NS + 'ColumnasAsignacion', mensaje='Update', entidad=P + 'asignacion', panguard=False, filtro=[],
        columnas=['sanic_cm_as_orden', 'sanic_cm_as_estadoaprobacion'], legacy=False, imagen=None,
        descripcion='S-11: cada rol de CM escribe solo las columnas de la asignacion que usa su pantalla'))
    # CargaSobreAsignacion (DAT-10, DAT-31): la carga reabre lo que cerro el sistema y no pisa la fecha de asignacion.
    cols = ['sanic_cm_as_estado', 'sanic_cm_as_motivo', 'sanic_cm_as_tipo', 'sanic_cm_as_fechaasignada']
    pasos.append(dict(
        tipo=NS + 'CargaSobreAsignacion', mensaje='Update', entidad=P + 'asignacion', panguard=False,
        filtro=['sanic_cm_as_ultimacarga'], columnas=cols + ['sanic_cm_as_ultimacarga', 'sanic_cm_as_fechacierre'], legacy=False,
        imagen=dict(nombre=IMAGEN_PREVIA, atributos=cols),
        descripcion='DAT-10/31: la carga reabre la Diaria/Incobrable que cerro el sistema y no pisa la fecha de asignacion'))
    # Reclasificacion (DAT-15): una sola activa entre Diaria e Incobrable por cuenta. PostOperation SINCRONO.
    for mensaje in ('Create', 'Update'):
        cols = ['sanic_cm_as_tipo', 'sanic_cm_as_estado', 'sanic_cm_as_cuentaid']
        pasos.append(dict(
            tipo=NS + 'Reclasificacion', mensaje=mensaje, entidad=P + 'asignacion', panguard=False,
            filtro=['sanic_cm_as_ultimacarga', 'sanic_cm_as_estado', 'sanic_cm_as_tipo'] if mensaje == 'Update' else [],
            columnas=cols + ['sanic_cm_as_motivo', 'sanic_cm_as_fechacierre'], legacy=False,
            imagen=dict(nombre=IMAGEN_PREVIA, atributos=cols) if mensaje == 'Update' else None,
            etapa=ETAPA_POSTOPERATION, modo=MODO_SINCRONO,
            descripcion='DAT-15: al traer la cuenta con el otro tipo, cierra la Diaria/Incobrable anterior (sincrono)'))
    # InteraccionDelCobrador (DAT-13, SEC-08): cuenta y agente obligatorios; el agente del cobrador lo pone el servidor.
    pasos.append(dict(
        tipo=NS + 'InteraccionDelCobrador', mensaje='Create', entidad=P + 'interaccion', panguard=False, filtro=[],
        columnas=['sanic_cm_in_cuentaid', 'sanic_cm_in_agente', 'sanic_cm_in_nombreagente', 'sanic_cm_in_estadograbacion'],
        legacy=False, imagen=None,
        descripcion='DAT-13/SEC-08: cuenta y agente obligatorios; el agente del cobrador lo pone el servidor'))
    # GrabacionInteraccion (DAT-11): la grabacion en el CORE la controla el servidor. Sin filtro.
    cols = ['sanic_cm_in_estadograbacion', 'sanic_cm_in_codigograbacion']
    pasos.append(dict(
        tipo=NS + 'GrabacionInteraccion', mensaje='Update', entidad=P + 'interaccion', panguard=False, filtro=[],
        columnas=cols + ['sanic_cm_in_fechagrabacion', 'sanic_cm_in_grabadopor'], legacy=False,
        imagen=dict(nombre=IMAGEN_PREVIA, atributos=cols),
        descripcion='DAT-11: no se vuelve a grabar una interaccion grabada; fecha y usuario de grabacion del servidor'))
    # VincularActualizacionPendiente (F8): al llegar la interaccion, cuelga la actualizacion que entro sin vincular. ASINCRONO:
    # si fallara sincrono, se perderia la interaccion del telefono.
    pasos.append(dict(
        tipo=NS + 'VincularActualizacionPendiente', mensaje='Create', entidad=P + 'interaccion', panguard=False, filtro=[],
        columnas=['sanic_cm_in_actualizainfo', 'sanic_cm_in_cuentaid'], legacy=False,
        imagen=None, etapa=ETAPA_POSTOPERATION, modo=MODO_ASINCRONO,
        descripcion='F8: cuelga de la interaccion que llega la actualizacion de datos que entro sin vincular (asincrono)'))
    # ReciboRepetido (F6): el recibo repetido no se rechaza, se marca para BackOffice
    pasos.append(dict(
        tipo=NS + 'ReciboRepetido', mensaje='Create', entidad=P + 'pago', panguard=False, filtro=[],
        columnas=['sanic_cm_pg_numerorecibo', 'sanic_cm_pg_reciborepetido'], legacy=False, imagen=None,
        descripcion='F6: marca el pago cuyo numero de recibo ya existe (no lo rechaza)'))
    # EspecialDesdeCuenta (F3): la Especial de la carga toma tipo, nota y retiro de la cuenta (solo lo vacio, sin solicitante)
    cols = ['sanic_cm_as_tipo', 'sanic_cm_as_cuentaid', 'sanic_cm_as_solicitadopor', 'sanic_cm_as_notas',
            'sanic_cm_as_retironio', 'sanic_cm_as_retirousd']
    for mensaje in ('Create', 'Update'):
        pasos.append(dict(
            tipo=NS + 'EspecialDesdeCuenta', mensaje=mensaje, entidad=P + 'asignacion', panguard=False,
            filtro=['sanic_cm_as_ultimacarga'] if mensaje == 'Update' else [],
            columnas=cols + ['sanic_cm_as_ultimacarga'], legacy=False,
            imagen=dict(nombre=IMAGEN_PREVIA, atributos=cols) if mensaje == 'Update' else None,
            descripcion='F3: la Especial de la carga toma tipo, nota y retiro de la cuenta (solo lo vacio)'))
    # TipoDesdeProducto (O3): la cuenta toma el tipo de su producto (la carga solo resuelve el lookup por CodigoProducto)
    for mensaje in ('Create', 'Update'):
        pasos.append(dict(
            tipo=NS + 'TipoDesdeProducto', mensaje=mensaje, entidad=P + 'cuenta', panguard=False,
            filtro=['sanic_cm_ct_productoid'] if mensaje == 'Update' else [],
            columnas=['sanic_cm_ct_productoid', 'sanic_cm_ct_tipoproducto'], legacy=False,
            imagen=dict(nombre=IMAGEN_PREVIA, atributos=['sanic_cm_ct_productoid', 'sanic_cm_ct_tipoproducto'])
            if mensaje == 'Update' else None,
            descripcion='O3: la cuenta toma el tipo de su producto (lo que muestra el cobrador)'))
    # IdentidadEnInteraccion (borrado por salida de cartera): la interaccion guarda ultimos 4, CIF y clave HMAC de su cuenta,
    # porque la cuenta se borra 14 dias despues de salir y la interaccion vive 2 anos. Nunca rechaza.
    pasos.append(dict(
        tipo=NS + 'IdentidadEnInteraccion', mensaje='Create', entidad=P + 'interaccion', panguard=False, filtro=[],
        columnas=['sanic_cm_in_cuentaid', 'sanic_cm_in_ultimos4', 'sanic_cm_in_cifcliente', 'sanic_cm_in_clavepan'],
        legacy=False, imagen=None,
        descripcion='Borrado por salida: la interaccion guarda ultimos 4, CIF y clave de su cuenta (no rechaza)'))
    # TramoCartera (escala de la cartera): tramo 0..19 de la asignacion, sale de su cuenta. La app la trae por tramos.
    pasos.append(dict(
        tipo=NS + 'TramoCartera', mensaje='Create', entidad=P + 'asignacion', panguard=False, filtro=[],
        columnas=['sanic_cm_as_cuentaid', 'sanic_cm_as_tramo'], legacy=False, imagen=None,
        descripcion='Escala: tramo 0..19 de la asignacion para que la app la traiga en partes bajo el limite de 2000'))
    # La cuenta nueva recibe el mismo tramo (sale de su id, que recien existe despues de crearla). ASINCRONO: no frena la carga.
    pasos.append(dict(
        tipo=NS + 'TramoCartera', mensaje='Create', entidad=P + 'cuenta', panguard=False, filtro=[],
        columnas=['sanic_cm_ct_tramo'], legacy=False, imagen=None, etapa=ETAPA_POSTOPERATION, modo=MODO_ASINCRONO,
        descripcion='Escala: la cuenta nueva recibe el mismo tramo que sus asignaciones (asincrono)'))
    # El pago lleva el tramo de su interaccion: Transcritas trae los pagos del dia por tramos.
    pasos.append(dict(
        tipo=NS + 'TramoCartera', mensaje='Create', entidad=P + 'pago', panguard=False, filtro=[],
        columnas=['sanic_cm_pg_interaccion', 'sanic_cm_pg_tramo'], legacy=False, imagen=None,
        descripcion='Escala: tramo 0..19 del pago (de su interaccion) para que el backoffice traiga los pagos del dia en partes'))
    for p in pasos:
        p.setdefault('etapa', ETAPA_PREOPERATION)
        p.setdefault('modo', MODO_SINCRONO)
        p['nombre'] = '%s: %s of %s' % (p['tipo'], p['mensaje'], p['entidad'])
    return pasos


# ---------------------------------------------------------------------------------------------- Web API
def cargar_config():
    claves = ['DATAVERSE_URL', 'PP_CLIENT_ID', 'PP_CLIENT_SECRET', 'PP_TENANT_ID']
    cfg = {k: os.environ.get(k, '').strip() for k in claves}
    if not all(cfg.values()):
        ruta = os.environ.get('PP_SECRETS') or os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'pp_secrets.env')
        if os.path.exists(ruta):
            for linea in open(ruta, encoding='utf-8').read().replace('\r', '').split('\n'):
                if '=' in linea and not linea.startswith('#'):
                    k, v = linea.split('=', 1)
                    if k.strip() in cfg and not cfg[k.strip()]:
                        cfg[k.strip()] = v.strip().strip('"')
    faltan = [k for k, v in cfg.items() if not v]
    if faltan:
        raise SystemExit('Faltan credenciales (solo nombres): ' + ', '.join(faltan))
    return cfg


class Dataverse:
    def __init__(self, cfg):
        self.base = cfg['DATAVERSE_URL'].rstrip('/')
        self.api = self.base + '/api/data/v9.2/'
        datos = urllib.parse.urlencode({
            'client_id': cfg['PP_CLIENT_ID'], 'client_secret': cfg['PP_CLIENT_SECRET'],
            'grant_type': 'client_credentials', 'scope': self.base + '/.default'}).encode()
        r = urllib.request.Request('https://login.microsoftonline.com/%s/oauth2/v2.0/token' % cfg['PP_TENANT_ID'], data=datos)
        self.token = json.load(urllib.request.urlopen(r))['access_token']

    def rq(self, metodo, ruta, cuerpo=None, headers=None):
        url = ruta if ruta.startswith('http') else self.api + urllib.parse.quote(ruta, safe="/?&=$(),@'.:-_")
        h = {'Authorization': 'Bearer ' + self.token, 'Accept': 'application/json', 'Content-Type': 'application/json',
             'OData-MaxVersion': '4.0', 'OData-Version': '4.0', **(headers or {})}
        datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
        for intento in range(4):
            try:
                with urllib.request.urlopen(urllib.request.Request(url, method=metodo, data=datos, headers=h)) as x:
                    s = x.read()
                    return x.status, (json.loads(s) if s else None)
            except urllib.error.HTTPError as e:
                texto = e.read().decode('utf-8', 'replace')
                if e.code in (429, 503) and intento < 3:
                    time.sleep(2 ** intento * 3)
                    continue
                return e.code, texto[:1200]
        return 599, 'sin respuesta'

    def get(self, ruta):
        st, b = self.rq('GET', ruta)
        if st >= 300:
            raise SystemExit('GET %s -> %s: %s' % (ruta[:120], st, b))
        return b

    def existe(self, ruta):
        st, _ = self.rq('GET', ruta)
        if st == 200: return True
        if st == 404: return False
        raise SystemExit('GET %s -> %s' % (ruta[:120], st))


# ---------------------------------------------------------------------------------------------- logica
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--dll', required=True)
    ap.add_argument('--version', required=True, help='AssemblyVersion de la dll (ej. 1.0.0.0)')
    ap.add_argument('--public-key-token', required=True, help='16 hex de la clave de nombre seguro')
    ap.add_argument('--culture', default='neutral')
    ap.add_argument('--solucion', default=SOLUCION_POR_DEFECTO)
    ap.add_argument('--aplicar', action='store_true')
    ap.add_argument('--activar-panguard', action='store_true')
    ap.add_argument('--desactivar-panguard', action='store_true')
    a = ap.parse_args()
    if a.activar_panguard and a.desactivar_panguard:
        raise SystemExit('--activar-panguard y --desactivar-panguard son excluyentes')
    if len(a.public_key_token) != 16 or any(c not in '0123456789abcdefABCDEF' for c in a.public_key_token):
        raise SystemExit('public-key-token invalido: la dll no esta firmada (se esperan 16 hex)')
    contenido = base64.b64encode(open(a.dll, 'rb').read()).decode()

    dv = Dataverse(cargar_config())
    mod = a.aplicar
    print('MODO:', 'APLICAR' if mod else 'PLAN (solo lectura; usar --aplicar para registrar)')

    def accion(desc, metodo, ruta, cuerpo=None, headers=None, tolerar=()):
        print(('APLICAR ' if mod else 'PLAN    ') + desc)
        if not mod: return None
        st, b = dv.rq(metodo, ruta, cuerpo, headers)
        if st >= 300 and st not in tolerar:
            raise SystemExit('   FALLO %s: %s' % (st, b))
        print('   ok', st)
        return b

    sol = {'MSCRM.SolutionUniqueName': a.solucion}
    if not dv.get("solutions?$select=uniquename&$filter=uniquename eq '%s'" % a.solucion)['value']:
        raise SystemExit('No existe la solucion ' + a.solucion)

    # 1) Ensamblado ---------------------------------------------------------------------------
    r = dv.get("pluginassemblies?$select=pluginassemblyid,version,publickeytoken,isolationmode,sourcetype&$filter=name eq '%s'" % ENSAMBLADO)['value']
    ensamblado_id = None
    if r:
        ensamblado_id = r[0]['pluginassemblyid']
        if r[0].get('publickeytoken') and r[0]['publickeytoken'].lower() != a.public_key_token.lower():
            raise SystemExit('La clave de nombre seguro de la dll (%s) no coincide con la registrada (%s). '
                             'Dataverse no permite cambiarla: use el MISMO .snk.' % (a.public_key_token.lower(), r[0]['publickeytoken']))
        if r[0]['isolationmode'] != 2 or r[0]['sourcetype'] != 0:
            raise SystemExit('El ensamblado existente no es sandbox/base de datos (isolationmode=%s, sourcetype=%s)' % (r[0]['isolationmode'], r[0]['sourcetype']))
        accion('actualizar contenido del ensamblado %s (registrado %s -> dll %s)' % (ENSAMBLADO, r[0].get('version'), a.version),
               'PATCH', 'pluginassemblies(%s)' % ensamblado_id, {'content': contenido, 'version': a.version})
    else:
        b = accion('crear ensamblado %s %s (sandbox, base de datos) en la solucion %s' % (ENSAMBLADO, a.version, a.solucion),
                   'POST', 'pluginassemblies',
                   {'name': ENSAMBLADO, 'content': contenido, 'isolationmode': 2, 'sourcetype': 0,
                    'version': a.version, 'culture': a.culture, 'publickeytoken': a.public_key_token.lower()},
                   {**sol, 'Prefer': 'return=representation'})
        if b: ensamblado_id = b['pluginassemblyid']
    if ensamblado_id:
        accion('asegurar el ensamblado en la solucion', 'POST', 'AddSolutionComponent',
               {'ComponentId': ensamblado_id, 'ComponentType': COMPONENTE_ENSAMBLADO,
                'SolutionUniqueName': a.solucion, 'AddRequiredComponents': False})

    # 2) Tipos de plugin (Dataverse los crea al subir el ensamblado; si falta alguno se registra) -------------
    tipos = {}
    if ensamblado_id:
        for t in dv.get("plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq %s" % ensamblado_id)['value']:
            tipos[t['typename']] = t['plugintypeid']
    for tipo in sorted({p['tipo'] for p in definir_pasos()}):
        if tipo in tipos: continue
        b = accion('registrar tipo de plugin ' + tipo, 'POST', 'plugintypes',
                   {'typename': tipo, 'name': tipo, 'friendlyname': tipo.split('.')[-1],
                    'pluginassemblyid@odata.bind': '/pluginassemblies(%s)' % ensamblado_id}, {'Prefer': 'return=representation'}) if ensamblado_id else None
        if b: tipos[tipo] = b['plugintypeid']

    # 3) Mensajes y filtros ---------------------------------------------------------------------
    mensajes = {}
    for m in ('Create', 'Update'):
        mensajes[m] = dv.get("sdkmessages?$select=sdkmessageid&$filter=name eq '%s'" % m)['value'][0]['sdkmessageid']

    def filtro_id(mensaje, entidad):
        v = dv.get("sdkmessagefilters?$select=sdkmessagefilterid&$filter=primaryobjecttypecode eq '%s' and _sdkmessageid_value eq %s"
                   % (entidad, mensajes[mensaje]))['value']
        if not v: raise SystemExit('No hay filtro de mensaje %s para %s (la tabla no admite plugins en ese mensaje)' % (mensaje, entidad))
        return v[0]['sdkmessagefilterid']

    # 4) Pasos ---------------------------------------------------------------------------------
    resumen = {'creados': 0, 'actualizados': 0, 'sin cambios': 0, 'retirados': 0, 'recreados': 0}
    for p in definir_pasos():
        faltantes = [c for c in p['columnas'] if not dv.existe("EntityDefinitions(LogicalName='%s')/Attributes(LogicalName='%s')?$select=LogicalName" % (p['entidad'], c))]
        tipo_id = tipos.get(p['tipo'])
        existentes = []
        if tipo_id:
            existentes = dv.get("sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,filteringattributes,rank,stage,mode,asyncautodelete,statecode,description,_sdkmessagefilterid_value,_sdkmessageid_value"
                                "&$filter=name eq '%s' and _eventhandler_value eq %s" % (p['nombre'].replace("'", "''"), tipo_id))['value']
        if len(existentes) > 1:
            raise SystemExit('Hay %d pasos con el nombre "%s": resolver a mano antes de seguir' % (len(existentes), p['nombre']))
        existente = existentes[0] if existentes else None

        if faltantes:
            if p['legacy']:
                # Paso 8d ya borro la columna en desuso: este paso ya no tiene razon de ser.
                if existente:
                    accion('retirar paso (la columna en desuso %s ya no existe): %s' % (', '.join(faltantes), p['nombre']),
                           'DELETE', 'sdkmessageprocessingsteps(%s)' % existente['sdkmessageprocessingstepid'])
                    resumen['retirados'] += 1
                else:
                    print('OMITIDO  %s (columna en desuso %s ya eliminada)' % (p['nombre'], ', '.join(faltantes)))
                continue
            raise SystemExit('Faltan columnas en %s: %s. Aplicar antes scripts/pan/paso1_esquema.py' % (p['entidad'], ', '.join(faltantes)))

        filtro = ','.join(p['filtro'])
        mensaje_id, filtro_msg_id = mensajes[p['mensaje']], filtro_id(p['mensaje'], p['entidad'])
        # ClaveDesdeCuenta y CuentaDesdeInteraccion van SIEMPRE activos (una importación de la solución puede apagarlos);
        # PanGuard: None = conservar su estado (se activa en el paso 8).
        deseado_activo = None if p['panguard'] else True
        if p['panguard']:
            if a.activar_panguard: deseado_activo = True
            elif a.desactivar_panguard: deseado_activo = False

        asincrono = p['modo'] == MODO_ASINCRONO
        if existente and (existente['stage'] != p['etapa'] or existente['mode'] != p['modo']
                          or existente['_sdkmessagefilterid_value'] != filtro_msg_id or existente['_sdkmessageid_value'] != mensaje_id):
            # Etapa, modo, mensaje o entidad no se pueden editar: se vuelve a crear conservando el estado.
            estaba_activo = existente['statecode'] == 0
            accion('recrear (cambio de etapa/modo/mensaje/entidad): ' + p['nombre'], 'DELETE',
                   'sdkmessageprocessingsteps(%s)' % existente['sdkmessageprocessingstepid'])
            existente = None
            resumen['recreados'] += 1
            if deseado_activo is None and p['panguard']: deseado_activo = estaba_activo

        if existente is None:
            cuerpo = {
                'name': p['nombre'], 'description': p['descripcion'], 'stage': p['etapa'], 'mode': p['modo'],
                'rank': 1, 'supporteddeployment': 0, 'asyncautodelete': asincrono,
                'eventhandler_plugintype@odata.bind': '/plugintypes(%s)' % tipo_id,
                'sdkmessageid@odata.bind': '/sdkmessages(%s)' % mensaje_id,
                'sdkmessagefilterid@odata.bind': '/sdkmessagefilters(%s)' % filtro_msg_id}
            if filtro: cuerpo['filteringattributes'] = filtro
            b = accion('crear paso: %s [%s, filtro=%s]' % (p['nombre'], '%s, %s' % ('PostOperation' if p['etapa'] == ETAPA_POSTOPERATION else 'PreOperation', 'asincrono' if asincrono else 'sincrono'), filtro or '-'),
                       'POST', 'sdkmessageprocessingsteps', cuerpo, {**sol, 'Prefer': 'return=representation'})
            paso_id = b['sdkmessageprocessingstepid'] if b else None
            resumen['creados'] += 1
            # Un PanGuard nuevo nace DESACTIVADO salvo que se pida activarlo (regla del paso 8).
            if p['panguard'] and deseado_activo is not True:
                if paso_id:
                    accion('desactivar paso nuevo (PanGuard se activa en el paso 8): ' + p['nombre'], 'PATCH',
                           'sdkmessageprocessingsteps(%s)' % paso_id, {'statecode': 1, 'statuscode': 2})
                else:
                    print('PLAN     desactivar paso nuevo (PanGuard se activa en el paso 8)')
            elif p['panguard'] and deseado_activo is True:
                print('         queda ACTIVO (--activar-panguard)')
        else:
            paso_id = existente['sdkmessageprocessingstepid']
            cambios = {}
            if (existente.get('filteringattributes') or '') != filtro: cambios['filteringattributes'] = filtro or None
            if existente.get('rank') != 1: cambios['rank'] = 1
            if bool(existente.get('asyncautodelete')) != asincrono: cambios['asyncautodelete'] = asincrono
            if (existente.get('description') or '') != p['descripcion']: cambios['description'] = p['descripcion']
            if cambios:
                accion('actualizar paso %s: %s' % (p['nombre'], ', '.join(sorted(cambios))), 'PATCH', 'sdkmessageprocessingsteps(%s)' % paso_id, cambios)
                resumen['actualizados'] += 1
            else:
                resumen['sin cambios'] += 1
            activo = existente['statecode'] == 0
            if deseado_activo is not None and deseado_activo != activo:
                accion(('activar' if deseado_activo else 'desactivar') + ' paso: ' + p['nombre'], 'PATCH',
                       'sdkmessageprocessingsteps(%s)' % paso_id,
                       {'statecode': 0, 'statuscode': 1} if deseado_activo else {'statecode': 1, 'statuscode': 2})
            else:
                print('ESTADO   %s: %s (sin cambios)' % (p['nombre'], 'activo' if activo else 'desactivado'))

        if paso_id:
            accion('asegurar el paso en la solucion: ' + p['nombre'], 'POST', 'AddSolutionComponent',
                   {'ComponentId': paso_id, 'ComponentType': COMPONENTE_PASO, 'SolutionUniqueName': a.solucion, 'AddRequiredComponents': False})
            # Imagen previa (ClaveDesdeCuenta y AprobacionEspecial, en Update): una, con las columnas justas.
            if p['imagen']:
                imgs = dv.get("sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,attributes,entityalias,imagetype"
                              "&$filter=_sdkmessageprocessingstepid_value eq %s and name eq '%s'" % (paso_id, p['imagen']['nombre']))['value']
                atributos = ','.join(p['imagen']['atributos'])
                if not imgs:
                    accion('crear imagen previa %s (%s) en %s' % (p['imagen']['nombre'], atributos, p['nombre']),
                           'POST', 'sdkmessageprocessingstepimages',
                           {'name': p['imagen']['nombre'], 'entityalias': p['imagen']['nombre'], 'imagetype': 0,
                            'attributes': atributos, 'messagepropertyname': 'Target',
                            'sdkmessageprocessingstepid@odata.bind': '/sdkmessageprocessingsteps(%s)' % paso_id})
                elif imgs[0]['attributes'] != atributos or imgs[0]['entityalias'] != p['imagen']['nombre'] or imgs[0]['imagetype'] != 0:
                    accion('actualizar imagen previa de ' + p['nombre'], 'PATCH',
                           'sdkmessageprocessingstepimages(%s)' % imgs[0]['sdkmessageprocessingstepimageid'],
                           {'attributes': atributos, 'entityalias': p['imagen']['nombre'], 'imagetype': 0})

    print('\nRESUMEN pasos:', json.dumps(resumen, ensure_ascii=False))
    if not mod:
        print('Nada aplicado. Repetir con --aplicar.')


if __name__ == '__main__':
    main()
