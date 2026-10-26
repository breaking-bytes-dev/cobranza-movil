"""El rol 'SR | CM | Localizador' CREA interacciones, pagos y actualizaciones de datos, pero no los edita ni reasigna.

Quita Write (Basic) en las tres tablas y Assign (Basic) en pago: con ellos el localizador podía bajar el monto o cambiar
el recibo de un pago ya grabado en el core, marcar su interacción como grabada o pasarle el pago a otro. La app del
localizador solo crea registros (Patch con Defaults), así que nunca los necesitó.
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
