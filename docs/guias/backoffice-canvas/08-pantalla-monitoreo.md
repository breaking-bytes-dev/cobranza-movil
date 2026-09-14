# Guía 08 — scrMonitoreo (Admin)

**Propósito**: el Admin verifica en un vistazo que la carga diaria del core LLEGÓ y que el volumen
cuadra. v1 acotada por **LED-07** (los errores de fila viven en Smelter — se menciona, no se duplica)
y **LED-12** (sin mini-tabla de últimas cargas: solo la carga de HOY; el historial por día exige
agregación no delegable → backlog `cargaresumen`). Pantalla corta y honesta, a propósito.

**Contrato de entrada** (los setea App.OnStart / App.Formulas — guía 01 §3–4):
`gblRol` (guard Admin) · `gblC`/`gblE` (tokens) · `nfMovil` · `nfUltimaCarga` (named formula,
01 §3 — se reevalúa sola en cada lectura) · `colNav` (para `cmpNavLateral`) ·
`gblNavMovilAbierta` (estado del drawer móvil — la setea cmpEncabezado/cmpNavLateral, 01 §8).
Datasources: `'CM | TBL | Carga diarias'` · `'CM | TBL | Cuentas'` · `'CM | TBL | Clientes'`.

## Árbol de controles

```
scrMonitoreo                       (ESTILO-Pantalla)
├── cmpEncabezadoMon               componente cmpEncabezado (receta 7.5)
├── conContenidoMon                Contenedor transparente — todo el contenido
│   ├── conEstadoOkMon             Contenedor — card estado OK (Visible = varHayCargaMon)
│   │   ├── recBordeOkMon          Rectángulo — borde izquierdo 4px okInk
│   │   ├── lblEstadoOkTituloMon   Etiqueta clásica
│   │   └── lblEstadoOkDetalleMon  Etiqueta clásica
│   ├── conEstadoAlertaMon         Contenedor — card ALERTA (Visible = !varHayCargaMon)
│   │   ├── recBordeAlertaMon      Rectángulo — borde izquierdo 4px redInk
│   │   ├── lblEstadoAlertaTituloMon   Etiqueta clásica
│   │   ├── lblEstadoAlertaUltimaMon   Etiqueta clásica
│   │   └── lblEstadoAlertaAccionMon   Etiqueta clásica
│   ├── cmpKpiFilasMon             componente cmpKpi — "FILAS DE CARGA HOY"
│   ├── cmpKpiCuentasMon           componente cmpKpi — "CUENTAS TOTALES"
│   ├── cmpKpiClientesMon          componente cmpKpi — "CLIENTES TOTALES"
│   ├── conInfoMon                 Contenedor ESTILO-Card — nota Smelter/historial
│   │   └── lblInfoMon             Etiqueta clásica
│   └── btnActualizarMon           Botón clásico ESTILO-BotonSec
└── cmpNavLateralMon               componente cmpNavLateral (receta 8.1 — al FINAL del árbol)
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | fórmula §1.1 completa |

### 1.1 OnVisible (pegar completo)

```powerfx
// Guard de rol (01 §4) → colecciones → estado de pantalla (receta 7.7).
If(!("Admin" in gblRol), Navigate(scrInicio));
Concurrent(
    // Filas de la carga de HOY. Filter por fecha = delegable (00 §4.2);
    // ShowColumns achica el payload a una sola columna.
    // ⚠ COTA 2000 DECLARADA (00 §4.1): si la cartera supera 2000 filas/día,
    //   este conteo SATURA en 2000 — el KPI lo delata (§4).
    ClearCollect(colCargaHoyMon,
        ShowColumns(
            Filter('CM | TBL | Carga diarias', '[CD] FechaCarga' >= Today()),
            '[CD] Numero cuenta'
        )
    ),
    ClearCollect(colCtasMon, ShowColumns('CM | TBL | Cuentas', '[CT] Numero de cuenta')),
    ClearCollect(colCliMon,  ShowColumns('CM | TBL | Clientes', '[CL] CIF'))
);
Set(varHayCargaMon, nfUltimaCarga >= Today())
```

> Las tres colecciones traen UNA columna cada una: existen solo para `CountRows` en memoria
> (los KPI jamás cuentan directo sobre el datasource — lista negra 00 §4.3).
> `colCtasMon`/`colCliMon` también saturan en 2000; hoy la cartera (~1 000 cuentas / ~950
> clientes) está lejos. Si algún KPI clava "2,000", esa es la señal de saturación, no el dato.

## 2. Nav y encabezado

- `cmpNavLateralMon` — instancia según receta 8.1 (guía 01), va al **final** del árbol:
  `RutaActiva = "mon"` · `Visible = !nfMovil || gblNavMovilAbierta` · `X = 0` · `Y = 0` ·
  `Width = gblE.AnchoNav` · `Height = Parent.Height`.
- `cmpEncabezadoMon` — receta 7.5: `X = If(nfMovil, 0, gblE.AnchoNav)` ·
  `Y = If(nfMovil, gblE.AltoBarra, 0)` · `Width = Parent.Width - Self.X` · `Height = 84` ·
  `Titulo = "Monitoreo de carga"` · `Subtitulo = "¿Llegó el archivo del core? ¿Cuadra el volumen?"`.

### conContenidoMon

| Propiedad | Valor |
|---|---|
| Tipo | `Contenedor` (manual), Fill transparente, sin borde |
| X | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* |
| Y | `cmpEncabezadoMon.Y + cmpEncabezadoMon.Height` |
| Width | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* |
| Height | `Parent.Height - Self.Y - gblE.Pad` |

Todo lo que sigue es hijo de `conContenidoMon` (posiciones relativas a él).

## 3. Card de estado (arriba, ancho completo)

Dos cards superpuestas en la misma posición; la variable `varHayCargaMon` decide cuál se ve.
El "borde izquierdo de acento" del mockup se hace con un `Rectángulo` interno de 4 de ancho
(los contenedores no tienen borde por lado). Sus esquinas quedan rectas contra el radio de la
card — aceptado, es una franja de 4px.

### 3.1 conEstadoOkMon — la carga de hoy llegó

| Control | Propiedad | Valor |
|---|---|---|
| `conEstadoOkMon` (Contenedor) | X / Y / Width / Height | `0` / `0` / `Parent.Width` / `84` |
| | Fill / Radius* | `gblC.okBg` / `gblE.Radio` (las 4 esquinas) |
| | BorderThickness / Visible | `0` / `varHayCargaMon` |
| `recBordeOkMon` (Rectángulo) | X / Y / Width / Height | `0` / `0` / `4` / `Parent.Height` |
| | Fill | `gblC.okInk` |
| `lblEstadoOkTituloMon` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `26` |
| | Text | `"✓ Carga de hoy completada · " & Text(nfUltimaCarga, "HH:mm")` |
| | Size / FontWeight / Color | `11` / `Bold` / `gblC.okInk` |
| `lblEstadoOkDetalleMon` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `lblEstadoOkTituloMon.Y + lblEstadoOkTituloMon.Height` / `Parent.Width - 2*gblE.Pad` / `24` |
| | Text | `Text(CountRows(colCargaHoyMon), "#,##0") & " filas de carga · " & Text(CountRows(colCtasMon), "#,##0") & " cuentas · " & Text(CountRows(colCliMon), "#,##0") & " clientes en maestros"` |
| | Size / Color | `9` / `gblC.okInk` |

> El mockup decía además "3 asignaciones nuevas · 0 errores de fila": **LED-07** — ambos datos
> viven en Smelter (la app de imports), acá NO se duplican. El detalle v1 son los tres conteos reales.

### 3.2 conEstadoAlertaMon — no llegó la carga

| Control | Propiedad | Valor |
|---|---|---|
| `conEstadoAlertaMon` (Contenedor) | X / Y / Width / Height | `0` / `0` / `Parent.Width` / `84` |
| | Fill / Radius* | `gblC.redBg` / `gblE.Radio` |
| | BorderThickness / Visible | `0` / `!varHayCargaMon` |
| `recBordeAlertaMon` (Rectángulo) | X / Y / Width / Height | `0` / `0` / `4` / `Parent.Height` — Fill `gblC.redInk` |
| `lblEstadoAlertaTituloMon` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `6` / `Parent.Width - 2*gblE.Pad` / `24` |
| | Text | `"⛔ No ha llegado la carga de hoy"` |
| | Size / FontWeight / Color | `11` / `Bold` / `gblC.redInk` |
| `lblEstadoAlertaUltimaMon` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `lblEstadoAlertaTituloMon.Y + lblEstadoAlertaTituloMon.Height` / `Parent.Width - 2*gblE.Pad` / `22` |
| | Text | `If(IsBlank(nfUltimaCarga), "Sin cargas registradas en el sistema", "Última carga: " & Text(nfUltimaCarga, "dd/mm HH:mm"))` |
| | Size / Color | `9` / `gblC.redInk` |
| `lblEstadoAlertaAccionMon` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `lblEstadoAlertaUltimaMon.Y + lblEstadoAlertaUltimaMon.Height` / `Parent.Width - 2*gblE.Pad` / `22` |
| | Text | `"Verificá el import en Smelter antes de operar la reconciliación."` |
| | Size / Color | `9` / `gblC.ink2` |

## 4. Fila de KPIs (cmpKpi ×3 — responsive 3→1 columnas)

Tres instancias de `cmpKpi` (guía 01 §8.3, altura fija 96). En desktop van lado a lado; en móvil
apiladas. `EsHero = false` en las tres.

| Control | Propiedad | Valor |
|---|---|---|
| `cmpKpiFilasMon` | X / Y | `0` / `conEstadoOkMon.Y + conEstadoOkMon.Height + gblE.Pad` |
| | Width / Height | `If(nfMovil, Parent.Width, (Parent.Width - 2*gblE.Pad) / 3)` / `96` |
| | Etiqueta / Valor | `"FILAS DE CARGA HOY"` / `Text(CountRows(colCargaHoyMon), "#,##0")` |
| | Detalle | `If(CountRows(colCargaHoyMon) >= 2000, "⚠ cota 2000 alcanzada — conteo saturado", "desde las 00:00")` |
| `cmpKpiCuentasMon` | X | `If(nfMovil, 0, cmpKpiFilasMon.X + cmpKpiFilasMon.Width + gblE.Pad)` |
| | Y | `If(nfMovil, cmpKpiFilasMon.Y + cmpKpiFilasMon.Height + gblE.PadSm, cmpKpiFilasMon.Y)` |
| | Width / Height | `cmpKpiFilasMon.Width` / `96` |
| | Etiqueta / Valor / Detalle | `"CUENTAS TOTALES"` / `Text(CountRows(colCtasMon), "#,##0")` / `"en maestros"` |
| `cmpKpiClientesMon` | X | `If(nfMovil, 0, cmpKpiCuentasMon.X + cmpKpiCuentasMon.Width + gblE.Pad)` |
| | Y | `If(nfMovil, cmpKpiCuentasMon.Y + cmpKpiCuentasMon.Height + gblE.PadSm, cmpKpiFilasMon.Y)` |
| | Width / Height | `cmpKpiFilasMon.Width` / `96` |
| | Etiqueta / Valor / Detalle | `"CLIENTES TOTALES"` / `Text(CountRows(colCliMon), "#,##0")` / `"en maestros"` |

> El mockup mostraba "ayer: N" como detalle de cada KPI: comparar contra ayer exige el historial
> por día que **LED-12** manda a backlog — el detalle v1 es estático.

## 5. Card informativa y botón Actualizar

| Control | Propiedad | Valor |
|---|---|---|
| `conInfoMon` (Contenedor) | Estilo | ESTILO-Card (01 §6) |
| | X / Y | `0` / `cmpKpiClientesMon.Y + cmpKpiClientesMon.Height + gblE.Pad` |
| | Width / Height | `Parent.Width` / `72` |
| `lblInfoMon` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `Parent.Height - 2*gblE.PadSm` |
| | Text | `"El detalle de errores por fila vive en Smelter (app de imports). El historial por día llega en una fase posterior."` |
| | Size / Color / Wrap | `9` / `gblC.muted` / `true` |
| `btnActualizarMon` (Botón clásico) | Estilo | ESTILO-BotonSec (01 §6) |
| | X / Y | `Parent.Width - Self.Width` / `conInfoMon.Y + conInfoMon.Height + gblE.Pad` |
| | Width / Height | `160` / `gblE.AltoBoton` |
| | Text | `"↻ Actualizar"` |
| | OnSelect | fórmula §5.1 |

> `lblInfoMon` documenta en pantalla las dos podas de v1: errores de fila → **LED-07** (viven en
> la tabla interna de Smelter; leerla desde acá acopla productos) · historial → **LED-12**
> (agregación no delegable sobre ~7 000 filas; backlog: tabla espejo `cargaresumen`).

### 5.1 btnActualizarMon.OnSelect (pegar completo — re-colecta TODO)

```powerfx
Concurrent(
    ClearCollect(colCargaHoyMon,
        ShowColumns(
            Filter('CM | TBL | Carga diarias', '[CD] FechaCarga' >= Today()),
            '[CD] Numero cuenta'
        )
    ),
    ClearCollect(colCtasMon, ShowColumns('CM | TBL | Cuentas', '[CT] Numero de cuenta')),
    ClearCollect(colCliMon,  ShowColumns('CM | TBL | Clientes', '[CL] CIF'))
);
Set(varHayCargaMon, nfUltimaCarga >= Today());
Notify("✓ Datos actualizados", NotificationType.Success, 2500)
```

> `nfUltimaCarga` es fórmula con nombre (01 §3): se reevalúa al leerla, no hay que "refrescarla".
> El `Set` sí es necesario porque `varHayCargaMon` es variable normal.

## 6. Qué NO tiene esta pantalla (divergencias declaradas)

- **Sin mini-tabla de últimas cargas** (mockup "Últimas cargas" con chips ✓/⚠) — **LED-12**.
- **Sin conteo de asignaciones nuevas ni errores de fila** — **LED-07**.
- Si mañana el negocio pide historial, el camino es la tabla espejo `cargaresumen` server-side
  (00 §2, LED-12) — NO una agregación en canvas.

## 7. Checklist de salida

- [ ] Entrar con rol ≠ Admin → rebota a `scrInicio` sin parpadeo de contenido.
- [ ] Con carga de hoy en Dev: card verde con "✓ Carga de hoy completada · HH:mm" (reloj 24 h)
      y detalle con los tres conteos; card roja invisible.
- [ ] Sin carga de hoy (probar con `Set(varHayCargaMon, false)` en la barra de fórmulas):
      card roja con última carga `dd/mm HH:mm` y la línea de Smelter; card verde invisible.
- [ ] Los 3 KPI muestran números con separador de miles y cuadran con los conteos del card verde.
- [ ] "↻ Actualizar" re-colecta (verificar en Monitor: 3 llamadas a Dataverse) y notifica éxito.
- [ ] Ventana < 920 px: nav colapsa a hamburguesa, KPIs apilados en 1 columna, nada se solapa.
- [ ] Studio sin NINGÚN subrayado azul de delegación en OnVisible ni en el botón.
- [ ] No existe ninguna galería/tabla de historial en la pantalla (LED-12).
