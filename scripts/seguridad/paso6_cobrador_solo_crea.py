"""Fase 4 · Paso 6 (S-9): el cobrador CREA interacciones, pagos y actualizaciones de datos, pero no los edita ni reasigna.

Antes el rol 'SR | CM | Localizador' tenía Write (Basic) en las tres tablas y Assign (Basic) en pago: podía bajar el monto o
cambiar el recibo de un pago después de que el BackOffice lo grabó en el core, marcar su interacción como grabada o pasarle el
pago a otro. La app del cobrador solo CREA (Patch con Defaults, scrInteraccion.pa.yaml): nunca usó ese Write.
Se conservan Create, Read, Append y AppendTo. El BackOffice conserva su Write Global en interacción (transcribe).
Uso: python3 paso6_cobrador_solo_crea.py [--aplicar | --revertir]"""
import sys
from roles_cm import aplicar, modo

ROL = 'SR | CM | Localizador'
CAMBIOS = [(ROL, 'prvWritesanic_cm_tbl_interaccion', 'Basic'),
           (ROL, 'prvWritesanic_cm_tbl_pago', 'Basic'),
           (ROL, 'prvWritesanic_cm_tbl_actualizaciondatos', 'Basic'),
           (ROL, 'prvAssignsanic_cm_tbl_pago', 'Basic')]

m = modo(sys.argv)
n = aplicar(CAMBIOS, m)
print(f'{n} cambio(s)' + (' (plan; aplicar con --aplicar)' if m == 'plan' else '') + '.' if n else 'sin cambios: ya está así.')
