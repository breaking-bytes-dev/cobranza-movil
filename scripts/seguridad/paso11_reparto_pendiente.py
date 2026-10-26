"""Reparte las asignaciones activas que siguen con dueño SYSTEM (se corre una sola vez).

Son asignaciones cuyo agente no tenía usuario al crearse. El plugin RepartoAsignacion las pasa al equipo "Cartera sin
cobrador" (o al localizador) la próxima vez que se toca su agente. Este script no espera a la próxima carga: a cada una le
vuelve a escribir el MISMO agente (o el mismo vacío), igual que RepartoPorAgente, y eso dispara RepartoAsignacion.
No cambia ningún dato de negocio. Excluye las Especiales sin aprobar (el reparto no las toca).
En un entorno nuevo no hace falta: la primera carga ya nace repartida por el plugin.

Por defecto solo muestra el plan. Los trabajos asíncronos del reparto se ven en asyncoperations.
Uso: python3 paso11_reparto_pendiente.py [--aplicar]"""
import sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
ESPECIAL, APROBADO = 3, 3

def todas(path):
    out = []; nxt = path
    while nxt:
        b = get(nxt); out += b['value']; nxt = b.get('@odata.nextLink')
    return out

system = get("systemusers?$select=systemuserid&$filter=fullname eq 'SYSTEM'")['value']
if len(system) != 1:
    raise SystemExit('no se encontró un único usuario SYSTEM')
sid = system[0]['systemuserid']
filas = todas('sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid,_sanic_cm_as_agenteid_value'
              f'&$filter=sanic_cm_as_estado eq true and _ownerid_value eq {sid}'
              f' and (sanic_cm_as_tipo ne {ESPECIAL} or sanic_cm_as_estadoaprobacion eq {APROBADO})')
con, sin = sum(1 for f in filas if f['_sanic_cm_as_agenteid_value']), sum(1 for f in filas if not f['_sanic_cm_as_agenteid_value'])
print(f'asignaciones activas con dueño SYSTEM: {len(filas)} (con agente {con}, sin agente {sin})')
err = 0
if APLICAR:
    for i, f in enumerate(filas, 1):
        ag = f['_sanic_cm_as_agenteid_value']
        cuerpo = {'sanic_cm_as_agenteid@odata.bind': f'/sanic_cm_tbl_agentes({ag})'} if ag else {'sanic_cm_as_agenteid@odata.bind': None}
        st, b = rq('PATCH', f"sanic_cm_tbl_asignacions({f['sanic_cm_tbl_asignacionid']})", cuerpo)
        if st >= 300: err += 1; print('  error HTTP', st, str(b)[:200])
        if i % 200 == 0: print(f'  {i}/{len(filas)}')
    print(f'tocadas: {len(filas) - err} · errores: {err}. El reparto corre asíncrono: volver a correr el plan en unos minutos (debe dar 0).')
else:
    print('plan (sin cambios). Aplicar con --aplicar.')
sys.exit(1 if err else 0)
