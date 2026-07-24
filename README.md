# Cobranza Móvil

Solución **Power Platform** para modernizar la cobranza en campo de un banco: reemplaza las boletas de
papel de los **localizadores externos** (en moto, a veces sin señal) por una app móvil offline, y le da a
la oficina una app para transcribir las gestiones al core bancario.

El banco exporta un **CSV diario** → **Smelter** lo normaliza a **12 tablas Dataverse** (prefijo `sanic_`) →
dos apps lo consumen.

## Componentes

| Componente | Tecnología | Diseño / Guía |
|------------|------------|---------------|
| **App Localizador** (campo) | Canvas app **offline-first**, player móvil nativo | [desarrollo/04](docs/desarrollo/04-app-localizador-canvas.md) · [guía paso a paso](docs/guias/05-guia-paso-a-paso-localizador.md) |
| **App Oficina** (backoffice) | Model-driven app (vistas, formularios, comandos) | [desarrollo/07](docs/desarrollo/07-app-backoffice-modeldriven.md) · [guía incremental](docs/guias/09-guia-construccion-backoffice.md) |
| **Import diario** | Smelter (CSV → 12 tablas normalizadas) | [desarrollo/01](docs/desarrollo/01-import-y-diccionario-de-datos.md) · [guía de configuración](docs/guias/02-import-configuracion.md) |
| **Seguridad** | Row-level (owner por localizador) + 6 roles + field security | [referencia/componentes/01](docs/referencia/componentes/01-seguridad-row-level.md) · [15-roles](docs/referencia/componentes/15-roles-de-seguridad.md) |
| **Automatización** | Power Automate (owner de asignación) + flujo de auditoría | [referencia/componentes/14](docs/referencia/componentes/14-automatizacion-owner-asignacion.md) |

## Estructura del repositorio

```
docs/
├── guias/         Guías paso a paso para CONSTRUIR (por audiencia: maker)
├── desarrollo/    Diseño y profundidad técnica (arquitectura, apps, pruebas)
├── referencia/    Fuente única de verdad (tablas, roles, decisiones, Smelter)
│   ├── componentes/   Una ficha por tabla/pieza (01–15)
│   └── decisiones/    Registro de decisiones (DEC-001…)
└── mockups/       Prototipos HTML/SVG de la app de campo
test-data/         CSVs mock (⚠️ jamás datos reales del banco) + generador
```

## Por dónde empezar

1. **Entender el sistema** → [docs/desarrollo/00-arquitectura-macro.md](docs/desarrollo/00-arquitectura-macro.md)
2. **Ver el modelo de datos** → [docs/referencia/03-catalogo-de-tablas.md](docs/referencia/03-catalogo-de-tablas.md)
3. **Construir la app de campo** → [docs/guias/05-guia-paso-a-paso-localizador.md](docs/guias/05-guia-paso-a-paso-localizador.md)
4. **Construir la app de oficina** → [docs/guias/09-guia-construccion-backoffice.md](docs/guias/09-guia-construccion-backoffice.md)
5. **Por qué se decidió cada cosa** → [docs/referencia/decisiones/registro-de-decisiones.md](docs/referencia/decisiones/registro-de-decisiones.md)

## Convenciones

- **Idioma:** documentación en español; nombres de esquema Dataverse con prefijo `sanic_`.
- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`…).
- **Versionado:** versión de la solución Dataverse en 4 dígitos `X.Y.Z.B`; tags de release `sol-vX.Y.Z.B`
  (ver [docs/desarrollo/10-estructura-del-repositorio.md](docs/desarrollo/10-estructura-del-repositorio.md)).
- **Regla de oro de los docs:** siempre con los **campos reales** de las tablas `sanic_*`, nunca datos inventados.
