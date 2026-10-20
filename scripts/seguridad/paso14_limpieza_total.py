"""Limpieza total de DEV antes de una prueba guiada "como un día normal" (autorizada por el dueño, 2026-10-19).

Borra TODOS los registros de negocio de Cobranza Móvil y los import jobs de Smelter de la plantilla "Cobranza movil".
NO es para PROD. Orden (por las reglas de borrado: cuenta→interacción/pago/carga = RemoveLink, interacción→pago/AD = Restrict,
agente→cuenta/asignación/interacción/carga = Restrict, cliente→carga = Restrict):
  1 pago y actualización de datos → 2 interacción → 3 carga diaria → 4 cuenta (arrastra asignación y cuentapan/bóveda)
  → 5 fiador (excepto el placeholder "(sin fiador)") → 6 cliente (arrastra dirección y teléfono)
  → 7 agentes que no estén en CONSERVAR_AGENTES (por nombre) → 8 logs y jobs de Smelter de la plantilla (filtra por plantilla,
  NO por nombre del job).
SE CONSERVAN: productos, equipos, roles, plantilla/mappings de Smelter, la tabla de la clave K (no se lee ni se toca), jobs de otras
plantillas, usuarios (systemuser). Idempotente.

Sesión: el Service Principal de pp_secrets.env, la misma que usan las suites (tests/integral/lib/datos.js limpiar()) para borrar.
Nunca se imprime un PAN ni la K: toda salida pasa por sin_pan() (enmascara 13-19 dígitos).

Por defecto solo muestra el plan (conteos por tabla). Aplica con --aplicar.
  --cliente CIF   ensayo: restringe los pasos 1-6 a ese cliente (con sus cuentas, interacciones, pagos...) y salta 7 y 8
Uso: python3 paso14_limpieza_total.py [--aplicar] [--cliente CIF]"""
import json, re, sys, time, urllib.request, urllib.error, urllib.parse, uuid
import dvlib
from dvlib import get, A, U

APLICAR = '--aplicar' in sys.argv
CIF = sys.argv[sys.argv.index('--cliente') + 1] if '--cliente' in sys.argv else None

CONSERVAR_AGENTES = ['QA-E2E Localizador User5', 'QA-E2E Localizador User6']
PLACEHOLDER_FIADOR = '(sin fiador)'
PLANTILLA = 'Cobranza movil'
LOTE = 50

sin_pan = lambda s: re.sub(r'\d{13,19}', lambda m: '****' + m.group()[-4:], str(s))
# (clave, entityset, pk)
T = {
    'pago': ('sanic_cm_tbl_pagos', 'sanic_cm_tbl_pagoid'),
    'actualizaciondatos': ('sanic_cm_tbl_actualizaciondatoses', 'sanic_cm_tbl_actualizaciondatosid'),
    'interaccion': ('sanic_cm_tbl_interaccions', 'sanic_cm_tbl_interaccionid'),
    'cargadiaria': ('sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_cargadiariaid'),
    'cuenta': ('sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuentaid'),
    'asignacion': ('sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid'),
    'cuentapan': ('sanic_cm_tbl_cuentapans', 'sanic_cm_tbl_cuentapanid'),
    'fiador': ('sanic_cm_tbl_fiadors', 'sanic_cm_tbl_fiadorid'),
    'cliente': ('sanic_cm_tbl_clientes', 'sanic_cm_tbl_clienteid'),
    'direccion': ('sanic_cm_tbl_direccions', 'sanic_cm_tbl_direccionid'),
    'telefono': ('sanic_cm_tbl_telefonos', 'sanic_cm_tbl_telefonoid'),
    'producto': ('sanic_cm_tbl_productos', 'sanic_cm_tbl_productoid'),
    'agente': ('sanic_cm_tbl_agentes', 'sanic_cm_tbl_agenteid'),
    'joblog': ('sanic_tbl_smlt_importjoblogs', 'sanic_tbl_smlt_importjoblogid'),
    'job': ('sanic_tbl_smlt_importjobs', 'sanic_tbl_smlt_importjobid'),
}
ES = lambda k: T[k][0]
PK = lambda k: T[k][1]


def todas(path):
    out, nxt = [], path
    while nxt:
        b = get(nxt); out += b['value']; nxt = b.get('@odata.nextLink')
    return out


def ids(k, filtro=None, extra=''):
    f = f'&$filter={filtro}' if filtro else ''
    return [x[PK(k)] for x in todas(f'{ES(k)}?$select={PK(k)}{extra}{f}')]


def por(k, campo, lista):
    out = []
    for i in range(0, len(lista), 40):
        out += ids(k, ' or '.join(f'{campo} eq {x}' for x in lista[i:i + 40]))
    return out


# ---- selección por paso -------------------------------------------------------------------------------------------------
def seleccion():
    s = {}
    if CIF:
        cli = ids('cliente', f"sanic_cm_cl_cif eq '{CIF}'")
        cue = por('cuenta', '_sanic_cm_ct_clienteid_value', cli)
        inte = por('interaccion', '_sanic_cm_in_cuentaid_value', cue)
        s['pago'] = sorted(set(por('pago', '_sanic_cm_pg_cuentaid_value', cue) + por('pago', '_sanic_cm_pg_interaccion_value', inte)))
        s['actualizaciondatos'] = por('actualizaciondatos', '_sanic_cm_ad_interaccionid_value', inte)
        s['interaccion'] = inte
        s['cargadiaria'] = sorted(set(por('cargadiaria', '_sanic_cm_cd_cuenta_value', cue) + por('cargadiaria', '_sanic_cm_cd_clienteid_value', cli)))
        s['cuenta'] = cue; s['fiador'] = []; s['cliente'] = cli
        s['agente'] = []; s['joblog'] = []; s['job'] = []
    else:
        for k in ('pago', 'actualizaciondatos', 'interaccion', 'cargadiaria', 'cuenta', 'cliente'):
            s[k] = ids(k)
        s['fiador'] = ids('fiador', f"sanic_cm_fi_nombre ne '{PLACEHOLDER_FIADOR}'")
        s['agente'] = [a[PK('agente')] for a in todas(f"{ES('agente')}?$select={PK('agente')},sanic_cm_ag_nombre")
                       if a['sanic_cm_ag_nombre'] not in CONSERVAR_AGENTES]
        plant = [p['sanic_tbl_smlt_importtemplateid'] for p in todas(
            f"sanic_tbl_smlt_importtemplates?$select=sanic_tbl_smlt_importtemplateid&$filter=sanic_smlt_it_name eq '{PLANTILLA}'")]
        if len(plant) != 1:
            sys.exit(f'PARO: la plantilla "{PLANTILLA}" devolvió {len(plant)} resultados (se esperaba 1).')
        s['job'] = por('job', '_sanic_smlt_ij_importtemplateid_value', plant)
        s['joblog'] = por('joblog', '_sanic_smlt_ijl_importjobid_value', s['job'])
    return s


def conteos_ref():
    """Tablas que caen por cascada o se conservan: solo se cuentan (cuando es total)."""
    r = {k: len(ids(k)) for k in ('asignacion', 'cuentapan', 'direccion', 'telefono', 'producto')}
    r['fiador_total'] = len(ids('fiador'))
    r['agente_total'] = len(ids('agente'))
    r['job_total'] = len(ids('job'))
    return r


# ---- $batch con reintentos ante 429/503 ---------------------------------------------------------------------------------
def post_batch(ops):
    """ops: lista de (entityset, id). Devuelve lista de (status, mensaje) en el mismo orden."""
    for intento in range(8):
        bnd = 'batch_' + uuid.uuid4().hex
        cs = 'changeset_' + uuid.uuid4().hex  # no se usa: cada DELETE va suelto para que uno que falle no tumbe a los demás
        partes = []
        for n, (es, i) in enumerate(ops, 1):
            partes.append(f'--{bnd}\r\nContent-Type: application/http\r\nContent-Transfer-Encoding: binary\r\nContent-ID: {n}\r\n\r\n'
                          f'DELETE {A}{es}({i}) HTTP/1.1\r\nAccept: application/json\r\n\r\n')
        body = ''.join(partes) + f'--{bnd}--\r\n'
        req = urllib.request.Request(A + '$batch', data=body.encode(), method='POST', headers={
            'Authorization': 'Bearer ' + dvlib.T, 'Content-Type': f'multipart/mixed; boundary={bnd}',
            'OData-MaxVersion': '4.0', 'OData-Version': '4.0', 'Accept': 'application/json'})
        try:
            with urllib.request.urlopen(req, timeout=600) as x:
                txt = x.read().decode('utf-8', 'replace')
            res = []
            for blk in txt.split('HTTP/1.1')[1:]:
                m = re.match(r'\s*(\d{3})', blk)
                msg = ''
                mm = re.search(r'"message"\s*:\s*"([^"]*)"', blk)
                if mm: msg = sin_pan(mm.group(1))[:160]
                res.append((int(m.group(1)), msg))
            if len(res) != len(ops):
                raise RuntimeError(f'respuesta batch con {len(res)} partes para {len(ops)} operaciones')
            # reintenta solo las que devolvieron 429/503 dentro del batch
            pend = [n for n, (s, _) in enumerate(res) if s in (429, 503)]
            if pend:
                time.sleep(10)
                sub = post_batch([ops[n] for n in pend])
                for n, r in zip(pend, sub): res[n] = r
            return res
        except urllib.error.HTTPError as e:
            if e.code in (429, 503):
                time.sleep(int(e.headers.get('Retry-After', '15')) + 2 * intento); continue
            if e.code == 401:
                dvlib.T = dvlib.token(); continue
            raise
    raise RuntimeError('batch: reintentos agotados')


def borrar(k, lista):
    ok = nf = 0; fallos = []
    for i in range(0, len(lista), LOTE):
        lote = lista[i:i + LOTE]
        for (s, msg), idr in zip(post_batch([(ES(k), x) for x in lote]), lote):
            if s in (200, 204): ok += 1
            elif s == 404: nf += 1
            else: fallos.append((idr, s, msg))
        print(f'  {k}: {min(i + LOTE, len(lista))}/{len(lista)}', end='\r', flush=True)
    print(f'  {k}: borrados {ok} · ya no existían {nf} · errores {len(fallos)}' + ' ' * 10)
    for f in fallos[:5]: print('    ERROR', f)
    return fallos


# ---- main ----------------------------------------------------------------------------------------------------------------
ORDEN = ['pago', 'actualizaciondatos', 'interaccion', 'cargadiaria', 'cuenta', 'fiador', 'cliente', 'agente', 'joblog', 'job']
sel = seleccion()
ref = None if CIF else conteos_ref()
print(('ENSAYO cliente ' + CIF if CIF else 'TOTAL') + ' · modo ' + ('APLICAR' if APLICAR else 'plan'))
print('A borrar (directo):')
for k in ORDEN: print(f'  {k:20s} {len(sel[k])}')
if ref:
    print('Arrastradas por cascada (cuenta→asignación y cuentapan; cliente→dirección y teléfono) / conservadas:')
    for k, v in ref.items(): print(f'  {k:20s} {v}')
    print('Agentes conservados por nombre:', CONSERVAR_AGENTES)
if not APLICAR:
    print('plan (sin cambios). Aplicar con --aplicar.'); sys.exit(0)

errores = 0
for k in ORDEN:
    if sel[k]:
        errores += len(borrar(k, sel[k]))
print('listo con errores.' if errores else 'listo.')
sys.exit(1 if errores else 0)
