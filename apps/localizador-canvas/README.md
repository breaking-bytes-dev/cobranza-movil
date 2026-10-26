# App del localizador

*Canvas app* de teléfono que usa el localizador para ver su cartera y registrar interacciones, pagos y actualizaciones de datos, también sin señal (*offline profile* propio «Cobranza Movil - Cobrador»). Nombre interno: `sanic_appcobranzamovil_c4e67`.

La documentación completa (pantallas, sincronización, perfil *offline*, validaciones y trampas) está en [App del localizador](../../docs/tecnica/05-app-localizador.md).

## Contenido de la carpeta

| Pieza | Qué es |
|---|---|
| [`Src/`](Src/) | Fuentes de la app en formato *SourceCode* de `pac canvas`: `App.pa.yaml`, una `scr*.pa.yaml` por pantalla y `Components/` |
| [`sanic_appcobranzamovil_c4e67_DocumentUri.msapr`](sanic_appcobranzamovil_c4e67_DocumentUri.msapr) | Fuentes de datos, id del perfil *offline* y recursos. Se modifica solo con scripts (`scripts/pan/paso7_fuentes.py`, `scripts/pan/paso7_perfil_offline.py`) |
| [`icono/`](icono/) | Ícono de la app (`icono.svg` fuente, `icono.png` el que se despliega) |

⚠️ La carpeta de fuentes se llama `Src`, con mayúscula: con otro nombre, `pac canvas pack` empaqueta la app sin fórmulas y no avisa.

## Despliegue

Se despliega desde el repositorio con [`paso7_desplegar.py`](../../scripts/pan/paso7_desplegar.py) (sin `--aplicar` solo arma el paquete). No publicar desde Studio ni usar los *workflows* `import-app` / `import-backoffice`. Ver [Instalación y despliegue](../../docs/tecnica/09-instalacion-y-despliegue.md#apps-canal-habitual).
