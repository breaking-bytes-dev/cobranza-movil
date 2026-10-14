"""Fase 1 · Paso 3: reconciliación de compartidos (red de seguridad del reparto).
Para cada asignación ACTIVA cuyo owner es el usuario de su agente: comparte en lectura cuenta, cliente y fiador.
Además lista los compartidos SOBRANTES: cuenta, cliente o fiador compartidos directamente con un usuario al que ninguna
asignación activa suya se lo justifica (por ejemplo, si un revoke del plugin falló). Con --revocar se quitan.
Ojo: el reparto es asíncrono. Correrlo con la carga o un re-reparto en curso puede marcar como sobrante algo que el plugin
está por compartir; en ese caso basta con volver a correr --aplicar.
Uso: python3 paso3_reconciliar.py [--aplicar] [--revocar]"""
import sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
REVOCAR = '--revocar' in sys.argv

def todas(path):
    out = []; nxt = path
    while nxt:
        b = get(nxt); out += b['value']; nxt = b.get('@odata.nextLink')
    return out

agentes = {a['sanic_cm_tbl_agenteid']: a['_sanic_cm_ag_usuarioid_value'] for a in
           todas('sanic_cm_tbl_agentes?$select=_sanic_cm_ag_usuarioid_value&$filter=_sanic_cm_ag_usuarioid_value ne null')}
asig = todas('sanic_cm_tbl_asignacions?$select=_ownerid_value,_sanic_cm_as_agenteid_value,_sanic_cm_as_cuentaid_value&$filter=sanic_cm_as_estado eq true and _sanic_cm_as_agenteid_value ne null')
objetivo = [a for a in asig if agentes.get(a['_sanic_cm_as_agenteid_value']) == a['_ownerid_value'] and a['_sanic_cm_as_cuentaid_value']]
print(f'asignaciones activas con agente: {len(asig)} | con owner = usuario del agente: {len(objetivo)}')

pares = set()  # (entityset, logical, id, user)
for a in objetivo:
    u = a['_ownerid_value']
    c = get(f"sanic_cm_tbl_cuentas({a['_sanic_cm_as_cuentaid_value']})?$select=_sanic_cm_ct_clienteid_value,_sanic_cm_ct_fiador_value")
    pares.add(('sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuenta', a['_sanic_cm_as_cuentaid_value'], u))
    if c.get('_sanic_cm_ct_clienteid_value'): pares.add(('sanic_cm_tbl_clientes', 'sanic_cm_tbl_cliente', c['_sanic_cm_ct_clienteid_value'], u))
    if c.get('_sanic_cm_ct_fiador_value'): pares.add(('sanic_cm_tbl_fiadors', 'sanic_cm_tbl_fiador', c['_sanic_cm_ct_fiador_value'], u))
print(f'compartidos a asegurar: {len(pares)} (por tabla: ' + ', '.join(f"{t.split('_')[-1]}={sum(1 for p in pares if p[1]==t)}" for t in sorted({p[1] for p in pares})) + ')')

err = 0
if APLICAR:
    for es, ln, i, u in sorted(pares):
        st, b = rq('POST', 'GrantAccess', {'Target': {'@odata.type': f'Microsoft.Dynamics.CRM.{ln}', f'{ln}id': i},
                                           'PrincipalAccess': {'Principal': {'@odata.type': 'Microsoft.Dynamics.CRM.systemuser', 'systemuserid': u}, 'AccessMask': 'ReadAccess'}})
        if st >= 300: err += 1; print('  error', es, i, st, b[:200])
    print('GrantAccess aplicados:', len(pares) - err, '| errores:', err)

# ---------------------------------------------------------------------------------------------- sobrantes
justificados = {(ln, i, u) for _, ln, i, u in pares}
sobrantes = []
for ln in ('sanic_cm_tbl_cuenta', 'sanic_cm_tbl_cliente', 'sanic_cm_tbl_fiador'):
    for poa in todas(f"principalobjectaccessset?$select=objectid,principalid,accessrightsmask"
                     f"&$filter=objecttypecode eq '{ln}' and principaltypecode eq 'systemuser' and accessrightsmask gt 0"):
        if (ln, poa['objectid'], poa['principalid']) not in justificados:
            sobrantes.append((ln, poa['objectid'], poa['principalid']))
print(f'compartidos sobrantes (sin asignación activa que los justifique): {len(sobrantes)} (por tabla: '
      + ', '.join(f"{t.split('_')[-1]}={sum(1 for x in sobrantes if x[0] == t)}" for t in sorted({x[0] for x in sobrantes})) + ')'
      + ('' if REVOCAR or not sobrantes else ' · quitarlos con --revocar'))
if REVOCAR:
    err = 0
    for ln, i, u in sobrantes:
        st, b = rq('POST', 'RevokeAccess', {'Target': {'@odata.type': f'Microsoft.Dynamics.CRM.{ln}', f'{ln}id': i},
                                            'Revokee': {'@odata.type': 'Microsoft.Dynamics.CRM.systemuser', 'systemuserid': u}})
        if st >= 300: err += 1; print('  error', ln, st, str(b)[:200])
    print('RevokeAccess aplicados:', len(sobrantes) - err, '| errores:', err)
