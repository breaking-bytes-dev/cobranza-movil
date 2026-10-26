"""Prueba E2E de la carga de Smelter con claves HMAC. Solo crea registros QA-E2E-PAN y los borra al final.
1. Arma un CSV de N filas QA con PAN numéricos aleatorios (BIN 9999, Luhn válido; nunca se imprimen).
2. Lo importa DOS veces (PrepareImport + ProcessChunk directos; el job queda sin estado, así el flow orquestador no corre).
3. Verifica: 0 fallos; por cada PAN, ct/cp/as/cd_clavepan = HMAC(K, PAN) calculado acá; bóveda 1:1 con cp_pan = PAN y
   cp_name = ****1234; ct_ultimos4; lookups as_cuentaid y cd_cuenta; la 2.ª importación no crea cuentas ni bóvedas;
   los logs de los jobs no tienen secuencias de 13-19 dígitos.
Uso: python3 scripts/pan/paso6_prueba.py [--conservar]"""
import csv, hashlib, hmac, io, os, random, re, sys, urllib.request
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seguridad'))
import dvlib
from dvlib import rq, get

N, QA = 3, 'QA-E2E-PAN'
K = bytes.fromhex(open(os.path.expanduser(os.environ.get('PAN_HMAC_KEY_FILE', '~/.config/cobranza-movil/pan_hmac_dev.key'))).read().strip())
def clave(pan): return hmac.new(K, pan.strip().encode(), hashlib.sha256).hexdigest()
def luhn(base):
    d = [int(c) for c in base][::-1]
    s = sum(x if i % 2 else (x * 2 - 9 if x * 2 > 9 else x * 2) for i, x in enumerate(d))
    return base + str((10 - s % 10) % 10)
fallas = []
def check(cond, desc):
    print(('  ✓ ' if cond else '  ✗ ') + desc)
    if not cond: fallas.append(desc)
def todas(p):
    out, n = [], p
    while n:
        b = get(n); out += b['value']; n = b.get('@odata.nextLink')
    return out

# 1) CSV QA
plantilla = next(csv.DictReader(open(os.path.join(os.path.dirname(__file__), '..', '..', 'test-data', 'cobranza-mock-1000-dia2.csv'), encoding='utf-8-sig')))
pans = [luhn('9999' + ''.join(random.choice('0123456789') for _ in range(11))) for _ in range(N)]
filas = []
for i, pan in enumerate(pans, 1):
    f = dict(plantilla)
    f.update({'Cliente': f'{QA} Cliente {i}', 'CIF': f'{QA}-CIF-{i}', 'NumeroCelular': '00000000', 'NumeroCasa': '00000000',
              'NumeroCuenta': pan, 'CodigoProducto': f'{QA}-PR', 'TipoProducto': f'{QA} Producto', 'NombreFianza': f'{QA} Fiador',
              'DireccionFianza': f'{QA} dirección', 'Agente': f'{QA} Agente', 'TipoAsignacion': 'Diaria', 'Nota': '', 'TipoEspecial': ''})
    filas.append(f)
buf = io.StringIO(); w = csv.DictWriter(buf, fieldnames=list(plantilla)); w.writeheader(); w.writerows(filas)
contenido = buf.getvalue().encode('utf-8')

tid = get("sanic_tbl_smlt_importtemplates?$select=sanic_smlt_it_name&$filter=sanic_smlt_it_name eq 'Cobranza movil'")['value'][0]['sanic_tbl_smlt_importtemplateid']
def importar(n):
    st, b = rq('POST', 'sanic_tbl_smlt_importjobs', {'sanic_smlt_ij_name': f'{QA} job {n}',
              'sanic_smlt_ij_importtemplateid@odata.bind': f'/sanic_tbl_smlt_importtemplates({tid})'}, {'Prefer': 'return=representation'})
    assert st < 300, b
    jid = b['sanic_tbl_smlt_importjobid']
    r = urllib.request.Request(dvlib.A + f'sanic_tbl_smlt_importjobs({jid})/sanic_smlt_ij_sourcefile', method='PATCH', data=contenido,
                               headers={'Authorization': 'Bearer ' + dvlib.T, 'Content-Type': 'application/octet-stream', 'x-ms-file-name': f'{QA.lower()}-{n}.csv'})
    urllib.request.urlopen(r).read()
    st, b = rq('POST', 'sanic_capi_smelter_prepareimport', {'JobId': jid}); assert st < 300, f'prepare {st} {str(b)[:300]}'
    for _ in range(50):
        st, b = rq('POST', 'sanic_capi_smelter_processchunk', {'JobId': jid}); assert st < 300, f'chunk {st} {str(b)[:300]}'
        if not b.get('HasMore'): break
    j = get(f"sanic_tbl_smlt_importjobs({jid})?$select=sanic_smlt_ij_status,sanic_smlt_ij_failedcount,sanic_smlt_ij_successcount,sanic_smlt_ij_totalrows")
    logs = todas(f"sanic_tbl_smlt_importjoblogs?$select=sanic_smlt_ijl_errormessage,sanic_smlt_ijl_targettable&$filter=_sanic_smlt_ijl_importjobid_value eq {jid}")
    return jid, j, logs

def estado():
    claves = {clave(p) for p in pans}
    ct = [c for c in todas("sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid,sanic_cm_ct_clavepan,sanic_cm_ct_ultimos4,sanic_cm_ct_numerodecuenta&$filter=startswith(sanic_cm_ct_cifcliente,'" + QA + "')")]
    cp = todas("sanic_cm_tbl_cuentapans?$select=sanic_cm_cp_pan,sanic_cm_cp_name,sanic_cm_cp_clavepan,_sanic_cm_cp_cuentaid_value&$filter=" +
               ' or '.join(f"sanic_cm_cp_clavepan eq '{k}'" for k in claves))
    return ct, cp

P = 'sanic_cm_tbl_'
def limpiar(jobs):
    ct, cp = estado()
    ids = [c['sanic_cm_tbl_cuentaid'] for c in ct]
    for i in ids:
        for e, col in [('cargadiarias', '_sanic_cm_cd_cuenta_value'), ('asignacions', '_sanic_cm_as_cuentaid_value')]:
            for r in todas(f"{P}{e}?$select={P}{e[:-1]}id&$filter={col} eq {i}"): rq('DELETE', f"{P}{e}({r[P + e[:-1] + 'id']})")
        rq('DELETE', f'{P}cuentas({i})')  # la bóveda se borra en cascada (relación parental)
    for e, col in [('clientes', 'sanic_cm_cl_cif'), ('agentes', 'sanic_cm_ag_nombre'), ('fiadors', 'sanic_cm_fi_nombre'), ('productos', 'sanic_cm_pr_codigo')]:
        for r in todas(f"{P}{e}?$select={P}{e[:-1]}id&$filter=startswith({col},'{QA}')"): rq('DELETE', f"{P}{e}({r[P + e[:-1] + 'id']})")
    for j in jobs: rq('DELETE', f'sanic_tbl_smlt_importjobs({j})')

jobs = []
try:
    print(f'Importación 1 ({N} filas QA)')
    jid, j, logs = importar(1); jobs.append(jid)
    check(j['sanic_smlt_ij_failedcount'] in (0, None) and not logs, f"sin fallos (estado {j['sanic_smlt_ij_status']}, éxitos {j['sanic_smlt_ij_successcount']}, logs {len(logs)})")
    for l in logs: print('    log:', l['sanic_smlt_ijl_targettable'], (l['sanic_smlt_ijl_errormessage'] or '')[:160])
    ct, cp = estado()
    check(len(ct) == N, f'cuentas QA = {len(ct)} (esperado {N})')
    check(len(cp) == N, f'bóveda QA = {len(cp)} (esperado {N})')
    por_clave = {c['sanic_cm_ct_clavepan']: c for c in ct}
    for i, pan in enumerate(pans, 1):
        k = clave(pan); c = por_clave.get(k); b = next((x for x in cp if x['sanic_cm_cp_clavepan'] == k), None)
        ok_c = c is not None and c['sanic_cm_ct_ultimos4'] == pan[-4:]
        ok_b = b is not None and hmac.compare_digest(b['sanic_cm_cp_pan'] or '', pan) and b['sanic_cm_cp_name'] == '****' + pan[-4:] and c is not None and b['_sanic_cm_cp_cuentaid_value'] == c['sanic_cm_tbl_cuentaid']
        a = todas(f"{P}asignacions?$select=sanic_cm_as_clavepan&$filter=_sanic_cm_as_cuentaid_value eq {c['sanic_cm_tbl_cuentaid']}") if c else []
        d = todas(f"{P}cargadiarias?$select=sanic_cm_cd_clavepan,sanic_cm_cd_numerocuenta&$filter=_sanic_cm_cd_cuenta_value eq {c['sanic_cm_tbl_cuentaid']}") if c else []
        # La carga escribe el nombre principal enmascarado (PanGuard rechaza el número completo)
        check(c is not None and c['sanic_cm_ct_numerodecuenta'] == '****' + pan[-4:] and len(d) == 1 and d[0]['sanic_cm_cd_numerocuenta'] == '****' + pan[-4:],
              f'fila {i}: ct_numerodecuenta y cd_numerocuenta enmascarados (****{pan[-4:]})')
        check(ok_c and ok_b and len(a) == 1 and a[0]['sanic_cm_as_clavepan'] == k and len(d) == 1 and d[0]['sanic_cm_cd_clavepan'] == k,
              f'fila {i}: ct_clavepan=HMAC, ultimos4, bóveda (pan, ****{pan[-4:]}, cuenta), asignación y carga diaria por clave')
    print('Importación 2 (mismo archivo: no debe duplicar)')
    jid, j, logs = importar(2); jobs.append(jid)
    check(j['sanic_smlt_ij_failedcount'] in (0, None) and not logs, f"sin fallos (éxitos {j['sanic_smlt_ij_successcount']}, logs {len(logs)})")
    for l in logs: print('    log:', l['sanic_smlt_ijl_targettable'], (l['sanic_smlt_ijl_errormessage'] or '')[:220])
    ct2, cp2 = estado()
    check(len(ct2) == N and len(cp2) == N, f'sin duplicados: cuentas {len(ct2)}, bóveda {len(cp2)}')
    todos = todas("sanic_tbl_smlt_importjoblogs?$select=sanic_smlt_ijl_errormessage&$filter=" + ' or '.join(f'_sanic_smlt_ijl_importjobid_value eq {x}' for x in jobs))
    check(not any(re.search(r'(?<!\d)\d{13,19}(?!\d)', l['sanic_smlt_ijl_errormessage'] or '') for l in todos), 'logs sin secuencias de 13-19 dígitos')
finally:
    if '--conservar' not in sys.argv:
        limpiar(jobs)
        ct, cp = estado()
        print(f'Limpieza: cuentas QA {len(ct)}, bóveda QA {len(cp)}, jobs borrados {len(jobs)}')
print('RESULTADO:', 'OK' if not fallas else f'{len(fallas)} fallas')
