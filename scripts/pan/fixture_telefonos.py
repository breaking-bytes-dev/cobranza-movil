"""Fixtures de las carteras de prueba de la app nativa (Power Apps en el teléfono), con el modelo de la Fase 2.
- User5 (Android del VPS): QA-E2E-LOC, 1 cliente, 2 cuentas (Diaria + Especial).
- User6 (teléfono del dueño): QA-E2E-LOC6, 2 clientes, 4 cuentas (Diaria, Incobrable, Diaria, Especial).
Cada cuenta lleva un número de tarjeta FICTICIO (BIN 9999, Luhn válido) que vive solo en la bóveda (cp_pan); la cuenta
tiene ct_clavepan = HMAC(K, número), ct_ultimos4 y, como nombre principal, una etiqueta QA sin dígitos de tarjeta.
Las asignaciones van al agente del usuario; el flow de owner las asigna y comparte cuenta → bóveda en cascada.
Los números nunca se imprimen (solo los últimos 4). Idempotente: lo que ya existe (por CIF) no se recrea.
La limpieza de tests/backoffice-canvas excluye QA-E2E-LOC* (el 2026-10-04 una corrida se los llevó).
Uso: python3 scripts/pan/fixture_telefonos.py [--aplicar | --borrar]"""
import hashlib, hmac, os, random, sys, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
from dvlib import rq, get

APLICAR, BORRAR = '--aplicar' in sys.argv, '--borrar' in sys.argv
K = bytes.fromhex(open(os.path.expanduser(os.environ.get('PAN_HMAC_KEY_FILE', '~/.config/cobranza-movil/pan_hmac_dev.key'))).read().strip())
P = 'sanic_cm_tbl_'
def b(s, i): return f'/{P}{s}({i})'
def luhn(base):
    d = [int(c) for c in base][::-1]
    s = sum(x if i % 2 else (x * 2 - 9 if x * 2 > 9 else x * 2) for i, x in enumerate(d))
    return base + str((10 - s % 10) % 10)
def crear(s, cuerpo):
    st, r = rq('POST', P + s, cuerpo, {'Prefer': 'return=representation'})
    if st >= 300: raise SystemExit(f'✗ {s}: {st} {str(r)[:300]}')
    return r

# (prefijo, email, clientes [(n, nombre, dirección, teléfono)], cuentas [(cliente, n, tipo, nio, usd, mora, especial)])
FIXTURES = [
    ('QA-E2E-LOC', 'User5@', [('001', 'CLIENTE MOVIL', 'Del semáforo 2c al lago, Managua', '2222-1111'),
                              ('002', 'CLIENTE DOS', 'Bello Horizonte, de la rotonda 1c al sur', '2222-2222')],
     [('001', '0001', 1, 2350.75, 0, 45, None), ('001', '0002', 3, 0, 120.5, 95, (500, 'Retiro urgente — prueba app nativa')),
      ('002', '0003', 1, 980.00, 0, 15, None)]),
    ('QA-E2E-LOC6', 'User6@', [('001', 'MARIA QA LOPEZ', 'Barrio Monseñor Lezcano, de la iglesia 2c al sur', '8888-1111'),
                               ('002', 'JOSE QA MARTINEZ', 'Altamira, del BDF 1c abajo', '8888-2222')],
     [('001', '0001', 1, 1850.40, 0, 35, None), ('001', '0002', 2, 12500, 0, 190, None), ('002', '0003', 1, 0, 85.75, 20, None),
      ('002', '0004', 3, 3200, 0, 60, (750, 'Retiro urgente — cliente pidió visita en la tarde'))]),
]

def borrar(pre):
    for c in get(f"{P}clientes?$select={P}clienteid&$filter=startswith(sanic_cm_cl_cif,'{pre}-CIF-')")['value']:
        for ct in get(f"{P}cuentas?$select={P}cuentaid&$filter=_sanic_cm_ct_clienteid_value eq {c[P + 'clienteid']}")['value']:
            for a in get(f"{P}asignacions?$select={P}asignacionid&$filter=_sanic_cm_as_cuentaid_value eq {ct[P + 'cuentaid']}")['value']:
                rq('DELETE', f"{P}asignacions({a[P + 'asignacionid']})")
        rq('DELETE', f"{P}clientes({c[P + 'clienteid']})")  # cascada: cuentas (y su bóveda), direcciones, teléfonos
    print(f'{pre}: borrado')

for pre, email, clientes, cuentas in FIXTURES:
    if BORRAR: borrar(pre); continue
    # Idempotente por cliente: solo crea los clientes (y sus cuentas) que falten.
    existentes = {x['sanic_cm_cl_cif'] for x in get(f"{P}clientes?$select=sanic_cm_cl_cif&$filter=startswith(sanic_cm_cl_cif,'{pre}-CIF-')")['value']}
    clientes = [c for c in clientes if f'{pre}-CIF-{c[0]}' not in existentes]
    cuentas = [c for c in cuentas if any(c[0] == x[0] for x in clientes)]
    if not clientes: print(f'{pre}: completo ({len(existentes)} clientes); usar --borrar para recrear'); continue
    uid = get(f"systemusers?$select=systemuserid&$filter=startswith(internalemailaddress,'{email}')")['value'][0]['systemuserid']
    ag = get(f"{P}agentes?$select={P}agenteid,sanic_cm_ag_nombre&$filter=_sanic_cm_ag_usuarioid_value eq {uid}")['value'][0]
    prod = get(f"{P}productos?$select={P}productoid,sanic_cm_pr_tipo&$filter=sanic_cm_pr_codigo eq 'TC001'")['value'][0]
    print(f"{pre}: agente {ag['sanic_cm_ag_nombre']}, {len(clientes)} clientes, {len(cuentas)} cuentas")
    if not APLICAR: continue
    cli, asig = {}, []
    for n, nom, dire, tel in clientes:
        c = crear('clientes', {'sanic_cm_cl_cif': f'{pre}-CIF-{n}', 'sanic_cm_cl_nombre': f'{pre} {nom}', 'sanic_cm_cl_departamento': 'Managua'})
        cli[n] = c[P + 'clienteid']
        crear('telefonos', {'sanic_cm_tl_cif': f'{pre}-CIF-{n}', 'sanic_cm_tl_numero': tel, 'sanic_cm_tl_tipo': 'Celular',
                            'sanic_cm_tl_clienteid@odata.bind': b('clientes', cli[n])})
        crear('direccions', {'sanic_cm_dr_cif': f'{pre}-CIF-{n}', 'sanic_cm_dr_tipo': 'Casa', 'sanic_cm_dr_direccion': f'{pre} {dire}',
                             'sanic_cm_dr_clienteid@odata.bind': b('clientes', cli[n])})
    for cn, n, tipo, nio, usd, mora, esp in cuentas:
        pan = luhn('9999' + ''.join(random.choice('0123456789') for _ in range(11)))
        clave = hmac.new(K, pan.encode(), hashlib.sha256).hexdigest()
        nombre = next(x[1] for x in clientes if x[0] == cn)  # solo cuentas de clientes nuevos
        ct = crear('cuentas', {'sanic_cm_ct_numerodecuenta': '****' + pan[-4:],  # paso 8: el nombre principal va enmascarado
                                'sanic_cm_ct_clavepan': clave, 'sanic_cm_ct_ultimos4': pan[-4:],
                               'sanic_cm_ct_nombrecliente': f'{pre} {nombre}', 'sanic_cm_ct_cifcliente': f'{pre}-CIF-{cn}',
                               'sanic_cm_ct_tipoproducto': prod['sanic_cm_pr_tipo'], 'sanic_cm_ct_saldovencidonio': nio,
                               'sanic_cm_ct_saldovencidousd': usd, 'sanic_cm_ct_diasmora': mora,
                               'sanic_cm_ct_clienteid@odata.bind': b('clientes', cli[cn]), 'sanic_cm_ct_productoid@odata.bind': b('productos', prod[P + 'productoid'])})
        cid = ct[P + 'cuentaid']
        crear('cuentapans', {'sanic_cm_cp_name': '****' + pan[-4:], 'sanic_cm_cp_pan': pan, 'sanic_cm_cp_clavepan': clave,
                             'sanic_cm_cp_cuentaid@odata.bind': b('cuentas', cid)})
        extra = {'sanic_cm_as_estadoaprobacion': 1, 'sanic_cm_as_retironio': esp[0], 'sanic_cm_as_notas': esp[1]} if esp else {}
        a = crear('asignacions', {'sanic_cm_as_tipo': tipo, 'sanic_cm_as_estado': True, 'sanic_cm_as_fechasignacion': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
                                  'sanic_cm_as_cuentaid@odata.bind': b('cuentas', cid), 'sanic_cm_as_agenteid@odata.bind': b('agentes', ag[P + 'agenteid']), **extra})
        asig.append(a[P + 'asignacionid'])
        print(f'  cuenta {pre}-CTA-{n} (tarjeta ****{pan[-4:]}) tipo {tipo} → asignación creada')
    for i in range(18):  # el flow de owner asigna y comparte (cuenta → bóveda en cascada)
        time.sleep(10)
        dueños = [get(f"{P}asignacions({a})?$select=_ownerid_value")['_ownerid_value'] for a in asig]
        listos = sum(d == uid for d in dueños); print(f'  {(i + 1) * 10}s: owner = usuario en {listos} de {len(asig)}')
        if listos == len(asig): break
    else: print('  ⚠ el flow no asignó todo: correr python3 scripts/seguridad/paso3_reconciliar.py --aplicar')
if not (APLICAR or BORRAR): print('Nada creado. Repetir con --aplicar.')
