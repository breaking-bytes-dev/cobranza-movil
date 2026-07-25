# Estructura del Repositorio, Releases y CI/CD

> **Fase — Infraestructura del proyecto.** Define cómo se organiza este repositorio, cómo se versiona y
> releasea la solución Dataverse, y qué automatización (CI/CD) aplica. Basado en el análisis del repo
> [`bitbreaker01/sigil`](https://github.com/bitbreaker01/sigil) (2026-07-24) — un proyecto Power Platform
> del **mismo publisher `sanic_`**, cuyo layout y estrategia ALM adoptamos adaptados a este proyecto.

---

## 1. Por qué sigil como referencia

Sigil (firma digital sobre Power Platform: plugins C# + Code App React + solución Dataverse) resuelve los
mismos problemas de ALM que tenemos acá — con el mismo publisher — y ya tiene funcionando:

- **README router** corto en español: estructura, "por dónde empezar", tabla de componentes.
- **Docs en 3 capas**: `guias/` (por audiencia) · `desarrollo/` (profundidad técnica) · `referencia/`
  (fuente única de verdad).
- **CHANGELOG** formato *Keep a Changelog* con versión de solución de 4 dígitos `[X.Y.Z.B]`.
- **ALM de dos planos**: zips de la solución (managed + unmanaged) como assets de **GitHub Releases** +
  `solutions/unpacked/` en git con la **metadata no-código** para diffs históricos.
- **4 workflows** de GitHub Actions: `ci` (tests), `playwright-e2e`, `solution-release`, `solution-sync`.

## 2. Qué traduce a este proyecto — y qué no

| Pieza de sigil | ¿Aplica acá? | Motivo |
|---|:---:|---|
| README router · docs 3 capas · CHANGELOG · Releases · `solutions/unpacked/` · workflows de solución | ✅ | Independientes del tipo de app. |
| CI de tests de código (`dotnet test`, `npm test`) | ❌ hoy | No hay pro-code: la lógica son business rules, flujos y roles **configurados** en el portal. |
| E2E Playwright | ❌ | La canvas app corre en el player **móvil nativo offline**; Playwright no llega. Las pruebas son el plan manual del [doc 06](06-plan-de-pruebas-localizador.md). |
| Round-trip de la solución desde `unpacked/` | ⚠️ | Igual que sigil documenta para su Code App: las **Canvas Apps no soportan round-trip** del Solution Packager. `unpacked/` es **histórico/diff**, no fuente de reconstrucción. La fuente de verdad de Dev es el zip *unmanaged* del último Release. |

## 3. Estructura objetivo (Fases 0–1 · ✅ aplicadas)

```
cobranza-movil/
├── README.md                  # Router: qué es, componentes, por dónde empezar, convenciones
├── .gitignore                 # Secretos, zips (van a Releases), datos reales del banco
├── CHANGELOG.md               # (Fase 2) Keep a Changelog, versiones [X.Y.Z.B]
├── docs/
│   ├── guias/                 # Paso a paso para CONSTRUIR
│   │   ├── 02-import-configuracion.md
│   │   ├── 05-guia-paso-a-paso-localizador.md
│   │   └── 09-guia-construccion-backoffice.md
│   ├── desarrollo/            # Diseño y profundidad técnica
│   │   ├── 00-arquitectura-macro.md
│   │   ├── 01-import-y-diccionario-de-datos.md
│   │   ├── 04-app-localizador-canvas.md
│   │   ├── 06-plan-de-pruebas-localizador.md
│   │   ├── 07-app-backoffice-modeldriven.md
│   │   ├── 08-backoffice-transcripcion.md
│   │   └── 10-estructura-del-repositorio.md   ← este doc
│   ├── referencia/            # Fuente única de verdad
│   │   ├── 03-catalogo-de-tablas.md
│   │   ├── cobranza-import-config.md          # (ex raíz) config concreta del import
│   │   ├── smelter-overview.md                # (ex raíz) qué es Smelter
│   │   ├── componentes/                       # 01–15: fichas de tablas, seguridad, roles, owner
│   │   └── decisiones/registro-de-decisiones.md
│   └── mockups/               # Prototipos HTML/SVG (sin cambios)
├── solutions/                 # (Fase 3)
│   ├── README.md              #   estrategia de dos planos
│   └── unpacked/              #   metadata no-código de la solución (diffs)
├── .github/workflows/         # (Fase 4) solution-release.yml · solution-sync.yml
└── test-data/                 # CSVs MOCK + generador (jamás datos reales)
```

> **Numeración:** se conserva la numeración histórica global (doc 05, doc 09…) aunque los archivos vivan
> en carpetas distintas — así las referencias cruzadas y la conversación del proyecto siguen valiendo.

## 4. Plan por fases

### ✅ Fase 0 — Fundación *(aplicada)*
`git init` · `.gitignore` con secciones comentadas · `README.md` router · raíz limpia (los 2 MDs sueltos
pasaron a `docs/referencia/`).

### ✅ Fase 1 — Docs en 3 capas *(aplicada)*
Reorganización de `docs/` en `guias/` · `desarrollo/` · `referencia/`. **43 links relativos** reescritos y
verificados (0 rotos), incluidos los de `test-data/README.md`.

### ✅ Fase 2 — CHANGELOG + versionado *(aplicada)*
- `CHANGELOG.md` en formato **Keep a Changelog** (ES): `[Unreleased]` + una sección por versión con
  **Added / Changed / Fixed**.
- La versión es la **de la solución Dataverse**, 4 dígitos `X.Y.Z.B` (ej. `1.0.0.1`).
- Tags de release: **`sol-vX.Y.Z.B`**.
- Se mantiene **a mano** (como sigil): cada cambio funcional en el portal suma una línea en `[Unreleased]`.

### ⬜ Fase 3 — ALM de dos planos
1. **Plano Releases (desplegables):** exportar la solución con `pac solution export` en dos sabores —
   `unmanaged` (fuente de verdad de Dev, re-importable) y `managed` (sellado, para Test/Prod) — y subirlos
   como **assets del GitHub Release** `sol-vX.Y.Z.B`. Los zips **no** entran al árbol (`.gitignore`).
2. **Plano git (`solutions/unpacked/`):** `pac solution unpack` del zip unmanaged, **podando** los
   componentes que no son metadata pura (`CanvasApps/` — la app del localizador no hace round-trip), y
   commit del resultado. Cada cambio de vista/formulario/rol/flujo hecho en el portal queda **diffeable**
   en el historial.
- Requisitos: solución única `Cobranza Móvil` en Dev (guía [doc 09 Parte 0](../guias/09-guia-construccion-backoffice.md))
  · Power Platform CLI (`pac`) · repo publicado en GitHub.

### ✅ Fase 4 — Workflows (GitHub Actions) *(escritos, inertes hasta configurar)*
- **`.github/workflows/solution-release.yml`** — manual (`workflow_dispatch`, input `version`):
  `pac auth create` con **Service Principal** → fija la versión → `pac solution export` (unmanaged +
  managed) → `gh release create sol-v{version}` con ambos zips (`--clobber` si el tag existe) → encadena
  el sync.
- **`.github/workflows/solution-sync.yml`** — `release: published` + `workflow_dispatch` / `workflow_call`:
  descarga el zip unmanaged → `pac solution unpack` (offline) → poda `CanvasApps/` → reescribe
  `solutions/unpacked/` → commit `chore(solution): sync de metadata no-código desde el release {tag}`.
- **Secrets**: **org secrets** de `breaking-bytes-dev` (`DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET`,
  `PP_TENANT_ID`) con visibilidad *Selected repositories* → `cobranza-movil` ([doc 11](../guias/11-service-principal-alm.md)).

> **Gotcha de diseño (por qué el release llama al sync a mano):** un release creado por el `GITHUB_TOKEN`
> **no dispara** el evento `release: published` (protección anti-recursión de Actions). Por eso
> `solution-release` invoca a `solution-sync` vía `workflow_call` (`needs`), en vez de confiar en el evento.
> El trigger `release: published` queda igual, para cubrir releases publicados **a mano por un humano**.

> **⚠️ Antes de la primera corrida:** `SOLUTION_NAME` ya está seteado en `solution-release.yml`
> (`cm_core_cobranzamovil`). Falta solo tener los 4 secrets cargados (hecho, repo secrets) y probar con
> `workflow_dispatch` (input `version`, ej. `1.0.0.0`), revisando el log del primer run.

- Requisito previo: **App Registration** en Entra ID + application user en el entorno Dev con rol
  suficiente para exportar soluciones ([doc 11](../guias/11-service-principal-alm.md)).

## 5. Convenciones adoptadas

- **Branching:** trunk-based sobre `main` (como sigil). Ramas cortas por cambio si hace falta.
- **Commits:** Conventional Commits (`docs:`, `feat:`, `fix:`, `chore(solution):`…).
- **Idioma:** docs en español; nombres de esquema Dataverse en el estándar ya definido
  ([referencia/03](../referencia/03-catalogo-de-tablas.md)).
- **Naming de sigil NO adoptado:** sigil usa marcador de app en el schema name (`sanic_sigil_tbl_*`).
  Nuestras 12 tablas ya existen como `sanic_*` liso — **renombrar esquemas publicados rompe todo**
  (formularios, vistas, flujos, offline profile) y no paga el costo. Queda documentado como convención
  para **futuros proyectos** del mismo entorno, no para este.

## 6. Trazabilidad

- **Origen:** pedido del 2026-07-24 — adoptar la estructura de `bitbreaker01/sigil` (proyecto,
  documentación, releases, CI/CD).
- **Habilita:** historial diffeable de lo construido en el portal ("llevar rastro de todo" a nivel
  solución) · releases reproducibles Dev→Test→Prod · onboarding por README router.
- **Próximo paso concreto:** Fase 2 (CHANGELOG) puede arrancar ya; Fases 3–4 cuando la solución en Dev
  tenga la Entrega 1 del backoffice ([doc 09](../guias/09-guia-construccion-backoffice.md)) verificada y
  el repo esté publicado en GitHub.
