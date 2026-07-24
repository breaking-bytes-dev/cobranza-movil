# Changelog

Todos los cambios notables de la solución **Cobranza Móvil** se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). La versión es la de la
**solución Dataverse**, en 4 dígitos `X.Y.Z.B`; los releases se etiquetan `sol-vX.Y.Z.B`
(ver [docs/desarrollo/10](docs/desarrollo/10-estructura-del-repositorio.md)).

> **Regla:** cada cambio funcional hecho en el portal (tabla, vista, formulario, rol, flujo, app) suma una
> línea en `[Unreleased]` en el momento de hacerlo. Al exportar un release, `[Unreleased]` se convierte en
> la sección de esa versión.

## [Unreleased]

### Added
- Modelo de datos: 12 tablas `sanic_*` (cliente, producto, agente, fiador, dirección, teléfono, cuenta,
  carga diaria, interacción, pago, asignación, actualización de datos) — fichas en
  [docs/referencia/componentes/](docs/referencia/componentes/).
- Import diario configurado con Smelter (CSV denormalizado → 7 entidades, idempotente) —
  [docs/referencia/cobranza-import-config.md](docs/referencia/cobranza-import-config.md).
- Solución `Cobranza Móvil` creada en el entorno Dev (doc 09, Parte 0).
- App Canvas del localizador **en construcción**: pantalla Mi Cartera con reorden optimizado
  (colección local + optimistic UI, guard flags, dirty tracking) según
  [docs/guias/05](docs/guias/05-guia-paso-a-paso-localizador.md).

### Pendiente de construir (diseñado, sin implementar)
- App model-driven de oficina (Entrega 1 del [doc 09](docs/guias/09-guia-construccion-backoffice.md) en curso, con errores por resolver).
- Flujo Power Automate de owner de asignación ([componente 14](docs/referencia/componentes/14-automatizacion-owner-asignacion.md)).
- Roles de seguridad ([componente 15](docs/referencia/componentes/15-roles-de-seguridad.md)) y flujo de auditoría de transcripción.

<!--
Al publicar el primer release (sol-v1.0.0.0), agregar acá:

## [1.0.0.0] - AAAA-MM-DD
### Added
- ...

[Unreleased]: enlace-al-compare-cuando-haya-remoto
[1.0.0.0]: enlace-al-tag
-->
