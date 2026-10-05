# App Localizador (Canvas) — fuentes

Fuentes YAML (layout SourceCode) de la app Canvas del localizador, rediseñada
(modo día/noche + accesibilidad + tokens `gblC`/`gblT`/`gblScale`).

## Estructura
- `src/App.pa.yaml` — OnStart: temas Día/Noche (`gblDay`/`gblNight`), `gblScale`, carga de colecciones, `Moneda()`.
- `src/scr*.pa.yaml` — pantallas. Cartera (`scrCartera`) rediseñada al mockup.
- `src/Components/` — `cmpSheet` (Ajustes: tema + tamaño), `cmpHeaderBack`, `cmpNav`, `cmpSyncPill`.

## Fuentes de datos (`*.msapr`)
Las fuentes de datos NO están en los `.pa.yaml`: viven en `sanic_appcobranzamovil_c4e67_DocumentUri.msapr`
(`References/DataSources.json`, `Properties.json` y `References/QualifiedValues.json`). Para agregar una tabla o refrescar
columnas nuevas sin Studio: `python3 scripts/pan/paso7_fuentes.py <msapr> --tablas <logicalname,...> --aplicar`
(replica las consultas de Studio; `--comparar <tabla>` valida la réplica contra una fuente existente).

## ⚠ Despliegue: usar `scripts/pan/paso7_desplegar.py`
Despliega cobrador + backoffice en una importación, sincroniza `DatabaseReferences`/`CdsDependencies` (sin eso la app no consulta
tablas nuevas) y **restaura el estado de los pasos de plugin y de los flows**. Los workflows `import-app` / `import-backoffice`
(en `main`) importan con `--activate-plugins`: el 2026-10-04 eso **encendió PanGuard** (debe seguir apagado hasta el paso 8).
No usarlos mientras exista PanGuard apagado.

## Perfil offline: personalizado "Cobranza Movil - Cobrador" (desde 2026-10-04)
La app usa un perfil offline **propio** (`ManualOfflineProfileId` en `Properties.json` del `.msapr`), no el autogenerado.
El autogenerado lo rearma el servicio en cada publicación de la app. El 2026-10-04 se trabó
("The auto-generated offline profile ... failed to publish. Error: 0x80071151") y la app no abría en el teléfono.
Además no soporta ALM.
- Lo crea/publica `python3 scripts/pan/paso7_perfil_offline.py --aplicar` (idempotente). El script también lo agrega a la
  solución y escribe el id en el `.msapr`. `paso7_desplegar.py` lleva ese id a la dependencia 161 de `CdsDependencies`.
- Tablas: las 10 que la app consulta. Regla "Todas las filas": el teléfono baja lo que el usuario **puede leer**; lo que
  recorta es la seguridad por fila (rol Basic + compartidos), no el perfil. Pendiente (S-1): filtrar por las asignaciones
  del cobrador ("filas relacionadas").
- Si la app usa una tabla nueva, **agregarla al perfil** (sumarla a `TABLAS` en el script y correrlo). Con perfil
  personalizado el servicio ya no la agrega solo.
- Publicar el perfil: `publishedon` no cambia (un perfil nuevo nace publicado). La prueba de que se publicó es que se
  regeneran sus `mobileofflineprofileitemfilters`; el script lo verifica.

## Build + deploy (Dev) — histórico
```bash
# 1) empaquetar
pac canvas pack --sources apps/localizador-canvas --msapp app.msapp --overwrite
# 2) swap del .msapp dentro del zip de solución (CanvasApps/sanic_appcobranzamovil_c4e67_DocumentUri.msapp)
# 3) subir asset:  gh release upload sol-v1.0.0.0 app-offline-fixed.zip --clobber
# 4) importar:     gh workflow run import-app.yml --ref <rama>
#    (el workflow exporta la solución vigente y swapea el .msapp para no chocar con el perfil offline)
```

## ⚠ La carpeta se llama `Src` (con mayúscula)
`pac canvas pack` en Linux busca `Src` y, si no la encuentra, **empaqueta igual sin ningún `.pa.yaml` y sin avisar**
(`packed.json` dice `LoadFromYaml: true` pero no hay YAML). Esa app publicada no tiene fórmulas y el servicio regenera
el perfil offline autogenerado con **0 tablas** (pasó el 2026-10-04). Verificar siempre antes de subir:
`python3 -c "import zipfile,sys;print(sum(n.endswith('.pa.yaml') for n in zipfile.ZipFile(sys.argv[1]).namelist()))" app.msapp` → 13.

## Checks pre-deploy (pac NO los valida; Studio sí)
- colon-space `: ` dentro de valores inline (usar bloque `|-`)
- nombres de control únicos app-wide (PA2110)
- `Radius*` solo en GroupContainer, NO en Label/Icon (PA2108)

> Nota: `pac canvas pack` requiere `--overwrite` si el destino existe.
