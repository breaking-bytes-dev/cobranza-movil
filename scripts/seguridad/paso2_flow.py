"""Fase 1 · Paso 2: el flow de owner comparte (lectura) cuenta/cliente/fiador con el cobrador y revoca al anterior/al cerrar.
Una Especial sin aprobar no se comparte ni cambia de dueño (S-6).
Uso: python3 paso2_flow.py [--aplicar]   (sin --aplicar guarda el JSON y muestra el resumen)"""
import copy, json, os, sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
WF = 'workflows(3b897aa2-0077-f111-ab0e-70a8a5b226fd)'
live = get(WF + '?$select=clientdata,statecode')
json.dump(json.loads(live['clientdata']), open('flow-vigente-respaldo.json', 'w'), indent=1)  # lo que está desplegado ahora
# BASE FIJA: la versión validada antes de la Fase 1 (B-10/B-16/B-17). Construir siempre desde acá, nunca desde lo vivo.
cd = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'flow-owner-base.json')))
d = cd['properties']['definition']
trig = list(d['triggers'].values())[0]
conn = trig['inputs']['host']['connectionName']
HOST = {'connectionName': conn, 'apiId': '/providers/Microsoft.PowerApps/apis/shared_commondataserviceforapps'}
AUTH = "@parameters('$authentication')"

T = lambda f: f"triggerOutputs()?['body/{f}']"
ASIG = T('sanic_cm_tbl_asignacionid'); CUENTA = T('_sanic_cm_as_cuentaid_value'); DUENO = T('_ownerid_value')
USR = "outputs('Obtener_datos_del_Agente')?['body/_sanic_cm_ag_usuarioid_value']"

def op(operation, params, after=None):
    return {'runAfter': after or {}, 'type': 'OpenApiConnection',
            'inputs': {'host': {**HOST, 'operationId': operation}, 'parameters': params, 'authentication': AUTH}}

def principal(u):
    return {'@@odata.type': 'Microsoft.Dynamics.CRM.systemuser', 'systemuserid': f'@{{{u}}}'}

def grant(nombre, entset, idexpr, user, after):
    return nombre, op('PerformUnboundAction', {'actionName': 'GrantAccess',
        'item/Target': f"{entset}(@{{{idexpr}}})",
        'item/PrincipalAccess': {'Principal': principal(user), 'AccessMask': 'ReadAccess'}}, after)

def revoke(nombre, entset, idexpr, user, after):
    return nombre, op('PerformUnboundAction', {'actionName': 'RevokeAccess',
        'item/Target': f"{entset}(@{{{idexpr}}})", 'item/Revokee': principal(user)}, after)

def filtro_otras(user, campo_cuenta=None, valor=None):
    """OData $filter: ¿el usuario tiene OTRA asignación activa (de la misma cuenta, o cuya cuenta tenga ese cliente/fiador)?
    Antes era FetchXML con link-entity: el conector 'List rows' fallaba con esa variante aunque la Web API la aceptara."""
    base = f"_ownerid_value eq @{{{user}}} and sanic_cm_as_estado eq true and sanic_cm_tbl_asignacionid ne @{{{ASIG}}}"
    if campo_cuenta is None:
        return base + f" and _sanic_cm_as_cuentaid_value eq @{{{CUENTA}}}"
    return base + f" and sanic_cm_as_cuentaid/{campo_cuenta} eq @{{{valor}}}"

def listar(filtro, after=None):
    return op('ListRecords', {'entityName': 'sanic_cm_tbl_asignacions', '$select': 'sanic_cm_tbl_asignacionid', '$filter': filtro, '$top': 1}, after)

def bloque_revocar(p, user, cuenta_out):
    """Revoca a `user` cuenta/cliente/fiador si no tiene otra asignación activa que los justifique.
    Las consultas de cliente y fiador corren DENTRO de su condición de no-nulo (un FetchXML con GUID vacío falla)."""
    cli = f"outputs('{cuenta_out}')?['body/_sanic_cm_ct_clienteid_value']"
    fia = f"outputs('{cuenta_out}')?['body/_sanic_cm_ct_fiador_value']"
    def guardado(nombre, cond_val, link_attr, entset, after):
        lista = f'{p}Otras_{nombre}'
        return {'runAfter': after, 'type': 'If', 'expression': {'not': {'equals': [f'@{cond_val}', None]}},
                'actions': {
                    lista: listar(filtro_otras(user, '_' + link_attr + '_value', cond_val)),
                    f'{p}Revocar_{nombre}_si_corresponde': {'runAfter': {lista: ['Succeeded']}, 'type': 'If',
                        'expression': {'equals': [f"@length(outputs('{lista}')?['body/value'])", 0]},
                        'actions': dict([revoke(f'{p}Revocar_{nombre}', entset, cond_val, user, {})]), 'else': {'actions': {}}}},
                'else': {'actions': {}}}
    a = {}
    a[f'{p}Otras_de_la_cuenta'] = listar(filtro_otras(user))
    a[f'{p}Revocar_cuenta_si_corresponde'] = {'runAfter': {f'{p}Otras_de_la_cuenta': ['Succeeded']}, 'type': 'If',
        'expression': {'equals': [f"@length(outputs('{p}Otras_de_la_cuenta')?['body/value'])", 0]},
        'actions': dict([revoke(f'{p}Revocar_cuenta', 'sanic_cm_tbl_cuentas', CUENTA, user, {})]), 'else': {'actions': {}}}
    a[f'{p}Cliente'] = guardado('cliente', cli, 'sanic_cm_ct_clienteid', 'sanic_cm_tbl_clientes', {f'{p}Revocar_cuenta_si_corresponde': ['Succeeded']})
    a[f'{p}Fiador'] = guardado('fiador', fia, 'sanic_cm_ct_fiador', 'sanic_cm_tbl_fiadors', {f'{p}Cliente': ['Succeeded']})
    return a

# ---------- nueva estructura ----------
acciones = d['actions']
original_rama_usuario = copy.deepcopy(acciones['Verificar_que_el_agente_tenga_asignado_un_usuario'])

# 1) cuenta de la asignación (cliente y fiador para compartir/revocar)
obtener_cuenta = op('GetItem', {'entityName': 'sanic_cm_tbl_cuentas', 'recordId': f'@{CUENTA}',
                                '$select': 'sanic_cm_tbl_cuentaid,_sanic_cm_ct_clienteid_value,_sanic_cm_ct_fiador_value'},
                    {'Obtener_datos_del_Agente': ['Succeeded']})

# 2) rama ACTIVA: lógica original + compartir con el usuario del agente + revocar al dueño anterior
rama = original_rama_usuario
rama['runAfter'] = {}
else_acts = rama['else']['actions']
ultimo = 'Compartir_la_Especial_con_el_solicitante'
cli = "outputs('Obtener_cuenta')?['body/_sanic_cm_ct_clienteid_value']"
fia = "outputs('Obtener_cuenta')?['body/_sanic_cm_ct_fiador_value']"
n, a = grant('Compartir_cuenta_con_el_cobrador', 'sanic_cm_tbl_cuentas', CUENTA, USR, {ultimo: ['Succeeded']}); else_acts[n] = a
else_acts['Compartir_cliente_con_el_cobrador'] = {'runAfter': {n: ['Succeeded']}, 'type': 'If',
    'expression': {'not': {'equals': [f'@{cli}', None]}},
    'actions': dict([grant('Grant_cliente', 'sanic_cm_tbl_clientes', cli, USR, {})]), 'else': {'actions': {}}}
else_acts['Compartir_fiador_con_el_cobrador'] = {'runAfter': {'Compartir_cliente_con_el_cobrador': ['Succeeded']}, 'type': 'If',
    'expression': {'not': {'equals': [f'@{fia}', None]}},
    'actions': dict([grant('Grant_fiador', 'sanic_cm_tbl_fiadors', fia, USR, {})]), 'else': {'actions': {}}}
rev_ant = bloque_revocar('Ant_', DUENO, 'Obtener_cuenta')
primero = next(iter(rev_ant)); rev_ant[primero]['runAfter'] = {}
else_acts['Revocar_al_duenio_anterior'] = {'runAfter': {'Compartir_fiador_con_el_cobrador': ['Succeeded']}, 'type': 'If',
    'expression': {'not': {'equals': [f'@{DUENO}', f'@{USR}']}}, 'actions': rev_ant, 'else': {'actions': {}}}

# 3) rama CERRADA: revocar al dueño actual
rev_cierre = bloque_revocar('Cierre_', DUENO, 'Obtener_cuenta')
primero = next(iter(rev_cierre)); rev_cierre[primero]['runAfter'] = {}

d['actions'] = {
    'Obtener_datos_del_Agente': acciones['Obtener_datos_del_Agente'],
    'Obtener_cuenta': obtener_cuenta,
    'Segun_estado_de_la_asignacion': {'runAfter': {'Obtener_cuenta': ['Succeeded']}, 'type': 'If',
        'expression': {'equals': [f"@{T('sanic_cm_as_estado')}", True]},
        # S-6 / APP-08: una Especial sin aprobar (Borrador, Pendiente o Rechazada) no se comparte ni cambia de dueño,
        # venga del backoffice o del CSV de Smelter. Al aprobarla, el Supervisor pone el agente y el flow corre.
        'actions': {'Solo_si_no_es_Especial_sin_aprobar': {'runAfter': {}, 'type': 'If',
            'expression': {'or': [{'not': {'equals': [f"@{T('sanic_cm_as_tipo')}", 3]}},
                                  {'equals': [f"@{T('sanic_cm_as_estadoaprobacion')}", 3]}]},
            'actions': {'Verificar_que_el_agente_tenga_asignado_un_usuario': rama},
            'else': {'actions': {}}}},
        'else': {'actions': rev_cierre}},
}
trig['inputs']['parameters']['subscriptionRequest/filteringattributes'] = 'sanic_cm_as_agenteid,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion'
json.dump(cd, open('flow-nuevo.json', 'w'), indent=1)
print('acciones raíz:', list(d['actions']))
print('rama activa → else (agente con usuario):', list(else_acts))
print('rama cerrada:', list(rev_cierre))
print('disparador:', trig['inputs']['parameters']['subscriptionRequest/filteringattributes'], '| condición:', trig.get('conditions'))

if APLICAR:
    print('off', rq('PATCH', WF, {'statecode': 0, 'statuscode': 1})[0])
    st, b = rq('PATCH', WF, {'clientdata': json.dumps(cd)})
    print('clientdata', st, '' if st < 300 else b)
    print('on', rq('PATCH', WF, {'statecode': 1, 'statuscode': 2})[:2])
