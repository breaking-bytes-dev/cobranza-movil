# Solution de Dataverse

Cobranza Móvil se distribuye como la *solution* `cm_core_cobranzamovil` («CM | Core | Cobranza Movil», prefijo `sanic`). Una *solution* es el paquete de Dataverse que agrupa tablas, roles, *plugins*, apps y demás componentes para moverlos entre ambientes.

## Dónde está cada cosa

| Qué | Dónde | Para qué |
|---|---|---|
| Zips instalables (`*_unmanaged.zip` y `*_managed.zip`) | *Assets* de los GitHub Releases `sol-vX.Y.Z.B` | Instalar o actualizar un ambiente. No se guardan en git |
| [`unpacked/`](unpacked/) | Esta carpeta | Historial de cambios de metadatos, para ver diferencias entre versiones |

- **No administrada** (*unmanaged*): para el ambiente de desarrollo. Se puede editar y volver a exportar.
- **Administrada** (*managed*): para producción. Está sellada y no se edita.

## Cómo se genera

El *workflow* [`solution-release.yml`](../.github/workflows/solution-release.yml) (manual, con la versión de 4 dígitos) fija la versión en el ambiente de desarrollo, exporta las dos variantes y las publica en el Release. Al terminar encadena [`solution-sync.yml`](../.github/workflows/solution-sync.yml), que desempaqueta la no administrada en `unpacked/`, quita `CanvasApps/` y hace *commit*.

## Reglas

- ⚠️ **`unpacked/` no se instala ni se edita.** Es solo un registro para comparar versiones, y puede estar atrasada respecto del ambiente. Un cambio hecho acá no vuelve a Dataverse.
- **El código fuente de las apps está en [`apps/`](../apps/)**, no en la *solution*: `CanvasApps/` se excluye porque las *canvas apps* no admiten ida y vuelta con el *Solution Packager*.
- **Los cambios se hacen en el ambiente de desarrollo, dentro de la *solution*.** Después se exporta un Release.
- Lo que no viaja en la *solution* (equipos, clave K, plantillas de Smelter, trabajos de borrado) lo crean los scripts de [`scripts/`](../scripts/README.md).

Instalación completa y publicación de cambios: [Instalación y despliegue](../docs/tecnica/09-instalacion-y-despliegue.md).
