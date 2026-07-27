# App Localizador (Canvas) — fuentes

Fuentes YAML (layout SourceCode) de la app Canvas del localizador, rediseñada
(modo día/noche + accesibilidad + tokens `gblC`/`gblT`/`gblScale`).

## Estructura
- `src/App.pa.yaml` — OnStart: temas Día/Noche (`gblDay`/`gblNight`), `gblScale`, carga de colecciones, `Moneda()`.
- `src/scr*.pa.yaml` — pantallas. Cartera (`scrCartera`) rediseñada al mockup.
- `src/Components/` — `cmpSheet` (Ajustes: tema + tamaño), `cmpHeaderBack`, `cmpNav`, `cmpSyncPill`.

## Build + deploy (Dev)
```bash
# 1) empaquetar
pac canvas pack --sources apps/localizador-canvas --msapp app.msapp --overwrite
# 2) swap del .msapp dentro del zip de solución (CanvasApps/sanic_appcobranzamovil_c4e67_DocumentUri.msapp)
# 3) subir asset:  gh release upload sol-v1.0.0.0 app-offline-fixed.zip --clobber
# 4) importar:     gh workflow run import-app.yml --ref <rama>
#    (el workflow exporta la solución vigente y swapea el .msapp para no chocar con el perfil offline)
```

## Checks pre-deploy (pac NO los valida; Studio sí)
- colon-space `: ` dentro de valores inline (usar bloque `|-`)
- nombres de control únicos app-wide (PA2110)
- `Radius*` solo en GroupContainer, NO en Label/Icon (PA2108)

> Nota: `pac canvas pack` requiere `--overwrite` si el destino existe.
