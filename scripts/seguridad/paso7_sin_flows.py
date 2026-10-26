"""Los roles CM operativos no pueden crear ni editar flows.

Un flow propio corre con la identidad de su autor: "Dataverse → listar filas → OneDrive/correo/HTTP" saca todo lo que esa
persona lee sin que se note. Las apps de CM no llaman a ningún flow (el orquestador de Smelter corre con su propia conexión).
Se quitan los privilegios de crear, editar, borrar, asignar, compartir, vincular y activar procesos, y prvFlow, a Localizador,
Solicitante, Supervisor y BackOffice. Se conservan prvReadWorkflow y prvWorkflowExecution (reglas de negocio y procesos del
sistema). Admin y 'Servicio - Cobranza' no cambian. Exportar a Excel viene del rol de plataforma 'Basic User' (compartido con
otras soluciones del entorno), la política DLP y el TDS endpoint son del centro de administración: fuera de este script.
Uso: python3 paso7_sin_flows.py [--aplicar | --revertir]"""
import sys
from roles_cm import aplicar, modo

ROLES = ['SR | CM | Localizador', 'SR | CM | Solicitante', 'SR | CM | Supervisor', 'SR | CM | BackOffice']
# (privilegio, profundidad original en DEV: igual en los cuatro roles)
PRIVILEGIOS = [('prvCreateWorkflow', 'Basic'), ('prvWriteWorkflow', 'Basic'), ('prvDeleteWorkflow', 'Basic'),
               ('prvAssignWorkflow', 'Basic'), ('prvShareWorkflow', 'Basic'), ('prvAppendWorkflow', 'Basic'),
               ('prvAppendToWorkflow', 'Global'), ('prvActivateSynchronousWorkflow', 'Basic'), ('prvFlow', 'Global')]
CAMBIOS = [(r, p, d) for r in ROLES for p, d in PRIVILEGIOS]

m = modo(sys.argv)
n = aplicar(CAMBIOS, m)
print(f'{n} cambio(s)' + (' (plan; aplicar con --aplicar)' if m == 'plan' else '') + '.' if n else 'sin cambios: ya está así.')
