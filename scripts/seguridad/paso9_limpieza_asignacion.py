"""Limpieza de la asignación:
- El estado de aprobación es solo de las Especiales. Las Diarias e Incobrables que quedaron en "Pendiente aprobacion"
  (el valor por defecto de la columna) pasan a Borrador; el plugin AprobacionEspecial hace que las nuevas nazcan así.
  El valor por defecto NO se cambia: lo necesitan las Especiales del CSV para entrar a la cola del Supervisor.
- Borra la regla de negocio "Bussiness Rule | CM | AS | Orden inicial" solo si sigue en borrador. Activada pondría todas
  las asignaciones en orden 1 (empate masivo); el orden 1..n lo arma la app del localizador.

Idempotente. Por defecto solo muestra el plan. Aplica con --aplicar.
Uso: python3 paso9_limpieza_asignacion.py [--aplicar]"""
import sys
from dvlib import rq, get

APLICAR = '--aplicar' in sys.argv
DIARIA, INCOBRABLE, PENDIENTE, BORRADOR = 1, 2, 2, 1
REGLA = 'Bussiness Rule | CM | AS | Orden inicial'

filas = get('sanic_cm_tbl_asignacions?$select=sanic_cm_tbl_asignacionid'
            f'&$filter=(sanic_cm_as_tipo eq {DIARIA} or sanic_cm_as_tipo eq {INCOBRABLE}) and sanic_cm_as_estadoaprobacion eq {PENDIENTE}')['value']
print(f'DAT-28: Diarias/Incobrables en "Pendiente aprobacion": {len(filas)} → Borrador')
err = 0
if APLICAR:
    for f in filas:
        st, b = rq('PATCH', f"sanic_cm_tbl_asignacions({f['sanic_cm_tbl_asignacionid']})", {'sanic_cm_as_estadoaprobacion': BORRADOR})
        if st >= 300: err += 1; print('  error HTTP', st, str(b)[:200])
    print(f'  corregidas: {len(filas) - err} · errores: {err}')

reglas = get(f"workflows?$select=workflowid,statecode,category&$filter=name eq '{REGLA}' and type eq 1")['value']
if not reglas:
    print(f'DAT-16: la regla "{REGLA}" ya no existe.')
for r in reglas:
    if r['statecode'] != 0:
        print(f'DAT-16: la regla "{REGLA}" está ACTIVA (statecode {r["statecode"]}): no se borra sola. Revisar a mano.'); err += 1
        continue
    print(f'DAT-16: borrar la regla "{REGLA}" (en borrador)')
    if APLICAR:
        st, b = rq('DELETE', f"workflows({r['workflowid']})")
        print('  DELETE →', st, '' if st < 300 else str(b)[:200])
        if st >= 300: err += 1
print('listo.' if APLICAR else 'plan (sin cambios). Aplicar con --aplicar.')
sys.exit(1 if err else 0)
