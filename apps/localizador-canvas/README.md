# App del localizador

*Canvas app* de teléfono que usa el localizador para ver su cartera y registrar interacciones, pagos y actualizaciones de datos, también sin señal (*offline profile* propio «Cobranza Movil - Cobrador»). Nombre interno: `sanic_appcobranzamovil_c4e67`.

La documentación completa (pantallas, sincronización, perfil *offline*, validaciones y trampas) está en [App del localizador](../../docs/tecnica/05-app-localizador.md).

## Contenido de la carpeta

| Pieza | Qué es |
|---|---|
| [`Src/`](Src/) | Código fuente de la app en formato `.pa.yaml`: `App.pa.yaml`, una `scr*.pa.yaml` por pantalla y `Components/`. Sirve para leer, revisar y comparar las fórmulas |
| [`sanic_appcobranzamovil_c4e67_DocumentUri.msapr`](sanic_appcobranzamovil_c4e67_DocumentUri.msapr) | Fuentes de datos, id del perfil *offline* y recursos de la app. No se edita a mano |
| [`icono/`](icono/) | Ícono de la app (`icono.svg` fuente, `icono.png` el que se usa) |

## Instalación y cambios

La app viaja dentro de la *solution* `cm_core_cobranzamovil` y se instala al importarla: [Instalación y despliegue](../../docs/tecnica/09-instalacion-y-despliegue.md). Se comparte **solo** con los localizadores ([paso 9](../../docs/tecnica/09-instalacion-y-despliegue.md#9-asignar-roles-a-las-personas-y-compartir-las-apps)).

Los cambios se hacen en el ambiente de desarrollo, abriendo la app en Power Apps Studio desde la *solution*, y llegan a los demás ambientes con una versión nueva de la *solution* ([Actualizar a una versión nueva](../../docs/tecnica/09-instalacion-y-despliegue.md#actualizar-a-una-versión-nueva)). Una tabla nueva que use el teléfono se agrega antes al *offline profile*: ver [Mantenimiento](../../docs/tecnica/11-mantenimiento.md#agregar-una-columna-y-mostrarla-en-una-app).

La fuente de verdad es la app publicada en la *solution* del ambiente de desarrollo; `Src/` es una copia legible para revisar cambios en el repositorio. Después de publicar en Studio, descargar una copia de la app (**Save** → **Download a copy**; en la interfaz clásica, **File** → **Save as** → **This computer**), abrir el `.msapp` como zip, copiar los `*.pa.yaml` de su carpeta `Src` a [`Src/`](Src/) y revisar la diferencia antes de hacer *commit*: Studio no conserva los comentarios YAML (`# …`) ni el orden de algunas propiedades, aunque sí los comentarios `//` de las fórmulas. El `.msapr` solo cambia si se agregan o refrescan fuentes de datos en Studio y no se mantiene a mano. Paso a paso en [Mantener `Src/` del repositorio al día](../../docs/tecnica/05-app-localizador.md#mantener-src-del-repositorio-al-día).
