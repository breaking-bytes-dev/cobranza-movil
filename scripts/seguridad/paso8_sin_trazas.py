"""Los roles operativos de CM no leen las trazas de plugins (plugintracelog).

Nuestros plugins nunca trazan valores, pero los de otras soluciones del entorno pueden hacerlo: la operación no tiene por
qué leerlas. Conservan el privilegio Admin y Servicio - Cobranza (diagnóstico).
El nivel de trazas es de toda la organización y no se toca acá; va en "Exception". Para diagnosticar, subirlo a "All" un
rato (organizations.plugintracelogsetting = 2) y volver a 1.
Uso: python3 paso8_sin_trazas.py [--aplicar | --revertir]"""
import sys
from roles_cm import aplicar, modo

ROLES = ['SR | CM | Localizador', 'SR | CM | Solicitante', 'SR | CM | Supervisor', 'SR | CM | BackOffice']
CAMBIOS = [(r, 'prvReadPluginTraceLog', 'Global') for r in ROLES]

m = modo(sys.argv)
n = aplicar(CAMBIOS, m)
print(f'{n} cambio(s)' + (' (plan; aplicar con --aplicar)' if m == 'plan' else '') + '.' if n else 'sin cambios: ya está así.')
