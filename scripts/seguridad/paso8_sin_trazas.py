"""Fase 4 · Paso 8 (S-13): los roles operativos de CM no leen las trazas de plugins.

Las trazas de plugins (plugintracelog) del entorno están en "All" y todos los roles CM tenían prvReadPluginTraceLog Global.
Nuestros plugins nunca trazan valores, pero los de otras soluciones del entorno no se sabe: una persona de la operación no
tiene por qué leerlas. Conservan el privilegio Admin y Servicio - Cobranza (diagnóstico).
El nivel de trazas es de toda la organización: no se toca acá. 2026-10-22: DEV pasó de "All" a "Exception" (velocidad de
la carga, propuesta B aprobada por el dueño aunque lo compartan otras soluciones). Para diagnosticar, subirlo a "All" un rato
(organizations.plugintracelogsetting = 2) y volver a 1. En PROD también va en "Exception".
Uso: python3 paso8_sin_trazas.py [--aplicar | --revertir]"""
import sys
from roles_cm import aplicar, modo

ROLES = ['SR | CM | Localizador', 'SR | CM | Solicitante', 'SR | CM | Supervisor', 'SR | CM | BackOffice']
CAMBIOS = [(r, 'prvReadPluginTraceLog', 'Global') for r in ROLES]

m = modo(sys.argv)
n = aplicar(CAMBIOS, m)
print(f'{n} cambio(s)' + (' (plan; aplicar con --aplicar)' if m == 'plan' else '') + '.' if n else 'sin cambios: ya está así.')
