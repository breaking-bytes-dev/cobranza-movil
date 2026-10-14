"""Fase 4 · Paso 8 (S-13): los roles operativos de CM no leen las trazas de plugins.

Las trazas de plugins (plugintracelog) del entorno están en "All" y todos los roles CM tenían prvReadPluginTraceLog Global.
Nuestros plugins nunca trazan valores, pero los de otras soluciones del entorno no se sabe: una persona de la operación no
tiene por qué leerlas. Conservan el privilegio Admin y Servicio - Cobranza (diagnóstico).
El nivel de trazas ("All") es de toda la organización y lo usan otras soluciones de DEV: no se toca acá. En PROD va en
"Exception" (lista de PROD en docs/auditoria/01-pendientes.md).
Uso: python3 paso8_sin_trazas.py [--aplicar | --revertir]"""
import sys
from roles_cm import aplicar, modo

ROLES = ['SR | CM | Localizador', 'SR | CM | Solicitante', 'SR | CM | Supervisor', 'SR | CM | BackOffice']
CAMBIOS = [(r, 'prvReadPluginTraceLog', 'Global') for r in ROLES]

m = modo(sys.argv)
n = aplicar(CAMBIOS, m)
print(f'{n} cambio(s)' + (' (plan; aplicar con --aplicar)' if m == 'plan' else '') + '.' if n else 'sin cambios: ya está así.')
