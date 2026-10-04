"""Prueba E2E del flow de compartir/revocar con datos QA-E2E-F1 (todo se borra al final)."""
import time, json
from dvlib import rq, get

P = 'QA-E2E-F1'
creados = []
def crear(es, pk, data):
    st, b = rq('POST', es, data, {'Prefer': 'return=representation'})
    assert st < 300, (es, st, b); creados.append((es, b[pk])); return b
bind = lambda es, i: f'/{es}({i})'
uid = lambda u: get(f"systemusers?$select=systemuserid&$filter=startswith(internalemailaddress,'{u}@')")['value'][0]['systemuserid']
U5, U7 = uid('User5'), uid('User7')
ag5 = get(f"sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid&$filter=_sanic_cm_ag_usuarioid_value eq {U5}")['value'][0]['sanic_cm_tbl_agenteid']
ag7 = crear('sanic_cm_tbl_agentes', 'sanic_cm_tbl_agenteid', {'sanic_cm_ag_nombre': P + ' Agente prueba User7', 'sanic_cm_ag_activo': True,
      'sanic_cm_ag_emailusuario': 'qa-e2e-f1@example.com', 'sanic_cm_ag_usuarioid@odata.bind': bind('systemusers', U7)})['sanic_cm_tbl_agenteid']
prod = get("sanic_cm_tbl_productos?$select=sanic_cm_pr_tipo&$filter=sanic_cm_pr_codigo eq 'TC001'")['value'][0]
cli = crear('sanic_cm_tbl_clientes', 'sanic_cm_tbl_clienteid', {'sanic_cm_cl_cif': P + '-CIF', 'sanic_cm_cl_nombre': P + ' CLIENTE'})['sanic_cm_tbl_clienteid']
tel = crear('sanic_cm_tbl_telefonos', 'sanic_cm_tbl_telefonoid', {'sanic_cm_tl_cif': P + '-CIF', 'sanic_cm_tl_numero': '2222-9999', 'sanic_cm_tl_tipo': 'Casa',
      'sanic_cm_tl_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cli)})['sanic_cm_tbl_telefonoid']
dirc = crear('sanic_cm_tbl_direccions', 'sanic_cm_tbl_direccionid', {'sanic_cm_dr_cif': P + '-CIF', 'sanic_cm_dr_tipo': 'Casa', 'sanic_cm_dr_direccion': P + ' dir',
      'sanic_cm_dr_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cli)})['sanic_cm_tbl_direccionid']
cta = {}
for k in 'AB':
    cta[k] = crear('sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuentaid', {'sanic_cm_ct_numerodecuenta': f'{P}-CTA-{k}', 'sanic_cm_ct_cifcliente': P + '-CIF',
        'sanic_cm_ct_tipoproducto': prod['sanic_cm_pr_tipo'], 'sanic_cm_ct_clienteid@odata.bind': bind('sanic_cm_tbl_clientes', cli),
        'sanic_cm_ct_productoid@odata.bind': bind('sanic_cm_tbl_productos', prod['sanic_cm_tbl_productoid'])})['sanic_cm_tbl_cuentaid']

def share(obj, user):
    # Filtrar por principalid en el servidor devuelve vacío: se filtra por objectid y se compara acá.
    r = [x for x in get(f"principalobjectaccessset?$select=principalid,accessrightsmask,inheritedaccessrightsmask&$filter=objectid eq {obj}")['value'] if x['principalid'] == user]
    return (r[0]['accessrightsmask'] | r[0]['inheritedaccessrightsmask']) if r else 0
def estado(user, nombre):
    s = {'cuentaA': share(cta['A'], user), 'cuentaB': share(cta['B'], user), 'cliente': share(cli, user), 'tel': share(tel, user), 'dir': share(dirc, user)}
    return nombre + ' ' + ' '.join(f"{k}={'R' if v & 1 else '-'}" for k, v in s.items())
def esperar(cond, desc, seg=480):
    t0 = time.time()
    while time.time() - t0 < seg:
        if cond(): print(f'  ✅ {desc} ({int(time.time()-t0)} s)'); return True
        time.sleep(10)
    print(f'  ❌ {desc} (no ocurrió en {seg} s)'); return False
R = lambda obj, u: bool(share(obj, u) & 1)
TEAM = get("teams?$select=teamid&$filter=name eq 'CM · Localizadores · DEV'")['value'][0]['teamid']
st_t = rq('POST', f'teams({TEAM})/teammembership_association/$ref', {'@odata.id': 'https://org36e60d9d.crm.dynamics.com/api/data/v9.2/' + f'systemusers({U7})'})[0]
print('User7 agregado temporalmente al team Localizadores:', st_t)
ok = []
INICIO = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
try:
    print('T1: X(cuenta A) → agente User5')
    X = crear('sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid', {'sanic_cm_as_numerocuenta': f'{P}-CTA-A', 'sanic_cm_as_tipo': 1, 'sanic_cm_as_estado': True,
        'sanic_cm_as_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', cta['A']), 'sanic_cm_as_agenteid@odata.bind': bind('sanic_cm_tbl_agentes', ag5)})['sanic_cm_tbl_asignacionid']
    ok.append(esperar(lambda: R(cta['A'], U5) and R(cli, U5) and R(tel, U5) and R(dirc, U5), 'User5 lee cuenta A + cliente + tel + dir'))
    ok.append(not R(cta['B'], U5)); print('  ', '✅' if ok[-1] else '❌', 'User5 NO lee cuenta B'); print('  ', estado(U5, 'User5:'))
    print('T2: Y(cuenta B) → agente User5')
    Y = crear('sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid', {'sanic_cm_as_numerocuenta': f'{P}-CTA-B', 'sanic_cm_as_tipo': 1, 'sanic_cm_as_estado': True,
        'sanic_cm_as_cuentaid@odata.bind': bind('sanic_cm_tbl_cuentas', cta['B']), 'sanic_cm_as_agenteid@odata.bind': bind('sanic_cm_tbl_agentes', ag5)})['sanic_cm_tbl_asignacionid']
    ok.append(esperar(lambda: R(cta['B'], U5), 'User5 lee cuenta B'))
    print('T3: X pasa al agente User7')
    rq('PATCH', f'sanic_cm_tbl_asignacions({X})', {'sanic_cm_as_agenteid@odata.bind': bind('sanic_cm_tbl_agentes', ag7)})
    ok.append(esperar(lambda: R(cta['A'], U7) and R(cli, U7) and not R(cta['A'], U5), 'User7 lee A+cliente; User5 pierde A'))
    ok.append(R(cli, U5) and R(cta['B'], U5)); print('  ', '✅' if ok[-1] else '❌', 'User5 conserva cliente y cuenta B (le queda Y)')
    print('  ', estado(U5, 'User5:')); print('  ', estado(U7, 'User7:'))
    print('T4: se cierra Y')
    rq('PATCH', f'sanic_cm_tbl_asignacions({Y})', {'sanic_cm_as_estado': False})
    ok.append(esperar(lambda: not R(cta['B'], U5) and not R(cli, U5) and not R(tel, U5), 'User5 pierde cuenta B + cliente + tel/dir'))
    ok.append(R(cli, U7)); print('  ', '✅' if ok[-1] else '❌', 'User7 conserva el cliente (su X sigue activa)')
    print('  ', estado(U5, 'User5:')); print('  ', estado(U7, 'User7:'))
    runs = get(f"flowruns?$select=status,starttime&$filter=_workflow_value eq 3b897aa2-0077-f111-ab0e-70a8a5b226fd and starttime ge {INICIO}&$orderby=starttime")['value']
    print('runs del flow DURANTE la prueba:', [r['status'] for r in runs]); ok.append(bool(runs) and all(r['status'] == 'Succeeded' for r in runs))
finally:
    for es, i in reversed(creados):
        st = rq('DELETE', f'{es}({i})')[0]
        if st >= 300 and st != 404: print('  no se pudo borrar', es, i, st)
    print('User7 sale del team Localizadores:', rq('DELETE', f'teams({TEAM})/teammembership_association({U7})/$ref')[0])
    print('limpieza: borrados', len(creados), 'registros QA-E2E-F1 (las asignaciones caen en cascada con la cuenta)')
print('RESULTADO:', 'TODO OK' if all(ok) else f'{sum(ok)}/{len(ok)} OK')
