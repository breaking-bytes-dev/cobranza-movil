# Guía 03 — scrTranscripcion (estación de transcripción)

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrTranscripcion.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol en `OnVisible` (LED-20); los ⟨BLOQUE L/S/R⟩ quedan inlineados en cada `OnSelect`.
> - Sin `colCtasTrx`: `clienteTrx/cifTrx/productoTrx` por navegación `ThisRecord.'[IN] Cuenta'.…` (receta 7.7); `btnActualizarTrx` ya no recarga cuentas.
> - Fila "Todos" de `colLocsTrx`: `CountIf` directo al datasource, no `CountRows(colPendTrx)`.
> - Power Fx moderno: identificadores en `GroupBy/ShowColumns/RenameColumns`, `Sort` en vez de `SortByColumns`, LookUp de AD por PK (01 §11.1).
> - `galColaTrx.AllItemsCount` en contadores/vacío; `Index(colVistaTrx, n)` en vez de `Last(FirstN(…))` en grabar/saltar.
> - Accesibilidad: overlays `btnFilaLocsTrx`, `btnFilaLocsMovTrx`, `btnFilaColaTrx`; la card de localizador pasa a `conLocCardTrx`/`conLocCardMovTrx` (solo visual); hijos sin `OnSelect` (LED-19).

LA pantalla estrella del Backoffice: 3 columnas (localizadores → cola FIFO → detalle de trabajo)
para grabar en el core y seguir de corrido. Rol: **Backoffice** (guard en OnVisible).

**Contrato de entrada**: `gblC`, `gblE`, `gblRol`, `gblUsuarioDV` seteados por `App.OnStart`
(guía 01 §4); `nfMovil` y `Moneda()` definidos en `App.Formulas` (01 §3). La pantalla no
recibe variables de navegación: se autoabastece en su OnVisible.

**Literales de layout documentados de esta pantalla** (del grid `.ws3` del mockup): anchos de
columna `185` (locs) y `255` (cola) — con **breakpoint intermedio** `Parent.Width < 1080` que los
baja a `150` y `210` (hallazgo R1-12) · gap `12` · alto de la tira de locs móvil `56` ·
TemplateSize: locs `54` · cola `gblE.AltoFila` (64) · pagos `34` · cambios `52`. Todo lo demás
es relativo o token `gblE.*`.

## Árbol de controles

```
scrTranscripcion                       (ESTILO-Pantalla)
├── cmpEncTrx                          (cmpEncabezado, receta 7.5)
├── btnActualizarTrx                   "↻ Actualizar" (re-colecta explícita)
├── conContenidoTrx                    (Contenedor transparente — área de las 3 columnas)
│   ├── lblHeadLocsTrx                 colhead "LOCALIZADORES"
│   ├── galLocsTrx                     (Galería vertical en blanco — desktop)
│   │   ├── btnLocCardTrx              fondo-card clicable de la fila
│   │   ├── lblLocNomTrx               nombre del localizador / "Todos"
│   │   └── chpLocNTrx                 chip conteo (receta 7.2)
│   ├── galLocsMovTrx                  (Galería horizontal en blanco — móvil, misma plantilla)
│   │   ├── btnLocCardMovTrx ─ lblLocNomMovTrx ─ chpLocNMovTrx
│   ├── lblHeadColaTrx                 colhead "PENDIENTES · n · FIFO"
│   ├── galColaTrx                     (Galería vertical en blanco — la cola)
│   │   ├── lblColaNomTrx              cliente en Upper (LED-01)
│   │   ├── lblColaMetaTrx             cuenta · HH:mm · 💵 · ✏️ · agente
│   │   └── recSepColaTrx              separador (receta 7.11)
│   ├── cmpVacioColaTrx                (cmpVacio) 🎉 cola del loc vacía
│   ├── cmpVacioTodoTrx                (cmpVacio) sin pendientes globales (receta 7.12)
│   └── conDetalleTrx                  (Contenedor, ESTILO-Card — columna de trabajo)
│       ├── lblPosTrx                  "N DE M · loc · nº · agente · HH:mm"
│       ├── lblMapaTrx                 "📍 mapa" (si hay geo)
│       ├── conIdentidadTrx            (Contenedor, ESTILO-CardTeal)
│       │   ├── lblNombreTrx ─ chpCifTrx ─ lblCuentaTagTrx
│       │   ├── lblCuentaGrandeTrx     nº cuenta héroe Size 24 (LED-02)
│       │   └── lblProductoTrx
│       ├── lblComentTrx               comentario del localizador
│       ├── lblSecPagosTrx             sección "PAGOS (n)"
│       ├── galPagosTrx                (Galería vertical en blanco)
│       │   ├── lblPagoMontoTrx ─ lblPagoReciboTrx ─ recSepPagoTrx
│       ├── lblSecDatosTrx             sección violeta "✏️ ACTUALIZACIÓN DE DATOS…" (LED-09)
│       ├── conCambiosTrx              (Contenedor violeta espBg/espInk)
│       │   └── galCambiosTrx          (Galería vertical en blanco sobre colCambiosTrx)
│       │       ├── lblCamKTrx ─ lblCamAntesTrx ─ lblCamFlechaTrx
│       │       ├── chpCamNuevoTrx     chip "NUEVO" (receta 7.2)
│       │       └── lblCamNuevoTrx
│       ├── cmpVacioDetTrx             (cmpVacio) ✨ nada seleccionado
│       └── conCodigoTrx               (Contenedor — franja del código, al pie)
│           ├── recHairTrx ─ lblCodigoTrx ─ txtCodigoTrx
│           ├── btnGrabarTrx ─ btnSaltarTrx ─ lblHintTrx
└── cmpNavTrx                          (cmpNavLateral, AL FINAL del árbol — 01 §8.1)
```

> Coordenadas de template: X dentro de la plantilla de una galería = X deseado en la FILA
> (no se resta nada: las galerías arrancan en 0). Regla enunciada una vez, vale para toda la guía.

## 1. Bloques reutilizables de fórmula (convención de esta guía)

Donde una fórmula de abajo diga `⟨BLOQUE L⟩`, `⟨BLOQUE S⟩` o `⟨BLOQUE R⟩`, pegá el bloque
LITERAL completo en ese punto. El resultado pegado es la fórmula final — nada queda "descrito".

**BLOQUE L — construir/refrescar `colLocsTrx`** ("Todos" primero, resto por conteo desc; LED-10):

```powerfx
ClearCollect(colLocsTrx, Table({locTrx: "Todos", nTrx: CountRows(colPendTrx)}));
Collect(colLocsTrx,
    RenameColumns(
        ShowColumns(
            SortByColumns(
                AddColumns(
                    GroupBy(AddColumns(colPendTrx, agenteTrx, '[IN] NombreAgente'),
                        "agenteTrx", "grpTrx"),
                    nTrx, CountRows(grpTrx)
                ),
                "nTrx", SortOrder.Descending
            ),
            "agenteTrx", "nTrx"
        ),
        "agenteTrx", "locTrx"
    )
)
```

> Todo en MEMORIA (colPendTrx) — cero delegación. Si tu versión de Studio exige comillas en los
> nombres de columna nuevos (`"agenteTrx"`, `"nTrx"`) o al revés los quiere como identificador
> pelado, **el IntelliSense manda** (01, encabezado). El orden queda horneado en la colección:
> la galería NO re-ordena, así "Todos" siempre encabeza.

**BLOQUE S — cargar el contexto del registro actual** (pegar inmediatamente después de CADA
`Set(varActualTrx, …)` de esta pantalla; LED-09):

```powerfx
If(!IsBlank(varActualTrx) && varActualTrx.'[IN] Actualiza info',
    Set(varADTrx, LookUp('CM | TBL | Actualizacion de datos',
        '[AD] Interaccion' = varActualTrx));
    ClearCollect(colTelAntesTrx, Filter('CM | TBL | Telefonos', '[TL] CIF' = varActualTrx.cifTrx));
    ClearCollect(colDirAntesTrx, Filter('CM | TBL | Direccions', '[DR] CIF' = varActualTrx.cifTrx));
    ClearCollect(colCambiosTrx, Filter(Table(
        {campoTrx: "TELÉFONO CASA",     antesTrx: LookUp(colTelAntesTrx, '[TL] Tipo' = "Casa").'[TL] Numero',    nuevoTrx: varADTrx.'[AD] Telefono Casa'},
        {campoTrx: "TELÉFONO CELULAR",  antesTrx: LookUp(colTelAntesTrx, '[TL] Tipo' = "Celular").'[TL] Numero', nuevoTrx: varADTrx.'[AD] Telefono Celular'},
        {campoTrx: "TELÉFONO TRABAJO",  antesTrx: LookUp(colTelAntesTrx, '[TL] Tipo' = "Trabajo").'[TL] Numero', nuevoTrx: varADTrx.'[AD] Telefono trabajo'},
        {campoTrx: "DIRECCIÓN CASA",    antesTrx: LookUp(colDirAntesTrx, '[DR] Tipo' = "Casa").'[DR] Direccion', nuevoTrx: varADTrx.'[AD] Direccion casa'},
        {campoTrx: "DIRECCIÓN EC",      antesTrx: LookUp(colDirAntesTrx, '[DR] Tipo' = "EC").'[DR] Direccion',   nuevoTrx: varADTrx.'[AD] Direccion EC'},
        {campoTrx: "DIRECCIÓN TRABAJO", antesTrx: LookUp(colDirAntesTrx, '[DR] Tipo' = "Trabajo").'[DR] Direccion', nuevoTrx: varADTrx.'[AD] Direccion trabajo'},
        {campoTrx: "CORREO ELECTRÓNICO", antesTrx: Blank(), nuevoTrx: varADTrx.'[AD] Correo electronico'}
    ), !IsBlank(nuevoTrx))),
    Set(varADTrx, Blank()); Clear(colCambiosTrx)
)
```

> - El LookUp de AD compara el lookup contra el REGISTRO (`'[AD] Interaccion' = varActualTrx`):
>   delegable y sin navegar el lookup en el predicado. Fallback si IntelliSense rechaza la
>   comparación de registro: `'[AD] Interaccion'.'CM | TBL | Interaccion' = varActualTrx.'CM | TBL | Interaccion'`
>   (patrón PK del proyecto) — el suyo manda.
> - Los dos Filter por CIF son delegables (igualdad de texto, 00 §4.2); traen pocas filas.
> - Si `'[TL] Tipo'`/`'[DR] Tipo'` resultan CHOICE y no texto, compará con
>   `Text('[TL] Tipo') = "Casa"` — es sobre colección en memoria, no delega nada.
> - El correo no tiene "antes" en maestros → siempre sale con chip NUEVO (así lo pinta el mockup).

**BLOQUE R — reselección común** (varActualTrx en blanco o ya fuera de la cola filtrada →
First de la cola filtrada; Blank() si quedó vacía):

```powerfx
Set(varActualTrx,
    First(SortByColumns(
        Filter(colPendTrx, varLocSelTrx = "Todos" || '[IN] NombreAgente' = varLocSelTrx),
        "Fecha de captura", SortOrder.Ascending)));
⟨BLOQUE S⟩
```

> ⚠ Si `SortByColumns` sobre la colección rechaza `"Fecha de captura"` (algunas versiones exigen
> el nombre lógico entre comillas), usá la alternativa equivalente en TODA la guía:
> `Sort(Filter(colPendTrx, …), 'Fecha de captura', SortOrder.Ascending)`.

## 2. Pantalla y OnVisible

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |

**OnVisible completo — orden EXACTO y SECUENCIAL** (hay join: cuentas ANTES que pendientes,
por eso acá NO va `Concurrent` — receta 7.7, advertencia final):

```powerfx
// 1) guard de rol — SIEMPRE primera línea
If(!("Backoffice" in gblRol), Navigate(scrInicio));
// 2) cuentas para el join (cota 2000 declarada — ver nota)
ClearCollect(colCtasTrx, ShowColumns('CM | TBL | Cuentas',
    '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto'));
// 3) pendientes del día + join horneado UNA vez (receta 7.7); nombre en Upper (LED-01)
ClearCollect(colPendTrx,
    AddColumns(
        Filter('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Pendiente),
        clienteTrx, Upper(Coalesce(
            LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Nombre Cliente', "—")),
        cifTrx, Coalesce(
            LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'CIF Cliente', "—"),
        productoTrx, Coalesce(
            LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Tipo Producto', "—")
    )
);
// 4) columna de localizadores materializada
⟨BLOQUE L⟩;
// 5) estado inicial
Set(varLocSelTrx, "Todos");
Set(varActualTrx, Blank());
// 6) selección inicial (First de la cola "Todos")
⟨BLOQUE R⟩
```

> - **Cota 2000**: `colCtasTrx` y `colPendTrx` cargan como máximo 2000 filas (límite de la app,
>   00 §4.1). Si `Cuentas` superara 2000, las interacciones de cuentas fuera de la página
>   muestran "—" como cliente — aceptado y anotado; el import hoy no llega a esa cota.
> - Abrir la pantalla = 2 llamadas a Dataverse (cuentas + pendientes); el resto es memoria.

## 3. Encabezado y botón Actualizar

| Control | Propiedad | Valor |
|---|---|---|
| `cmpEncTrx` (cmpEncabezado) | — | receta 7.5: `X = If(nfMovil, 0, gblE.AnchoNav)` · `Y = If(nfMovil, gblE.AltoBarra, 0)` · `Width = Parent.Width - Self.X` · `Height = 84` |
| | Titulo / Subtitulo | `"Por transcribir"` / `"Localizador → pendientes → detalle. Grabá en el core y seguí de corrido."` |
| `btnActualizarTrx` (Botón clásico) | Estilo | ESTILO-BotonFantasma · `Size = 9` · `Height = 36` |
| | X/Y/W | `Parent.Width - gblE.Pad - Self.Width` / `If(nfMovil, gblE.AltoBarra + 8, 24)` / `130` |
| | Text | `"↻ Actualizar"` |
| | OnSelect | fórmula completa abajo — recolecta explícita (00 §5) SIN tocar `varLocSelTrx`; si el loc seleccionado ya no tiene pendientes, la reselección devuelve Blank y aparecen los vacíos |

**btnActualizarTrx.OnSelect — COMPLETO** (mismas fórmulas de los pasos 2–4 del OnVisible,
más la reselección; los `⟨BLOQUE⟩` son los bloques literales de §1):

```powerfx
// 1) cuentas para el join (idéntico al paso 2 del OnVisible)
ClearCollect(colCtasTrx, ShowColumns('CM | TBL | Cuentas',
    '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto'));
// 2) pendientes del día + join horneado (idéntico al paso 3 del OnVisible)
ClearCollect(colPendTrx,
    AddColumns(
        Filter('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Pendiente),
        clienteTrx, Upper(Coalesce(
            LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Nombre Cliente', "—")),
        cifTrx, Coalesce(
            LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'CIF Cliente', "—"),
        productoTrx, Coalesce(
            LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Tipo Producto', "—")
    )
);
// 3) columna de localizadores
⟨BLOQUE L⟩;
// 4) reselección sobre la cola refrescada (varLocSelTrx queda como estaba)
⟨BLOQUE R⟩
```

## 4. conContenidoTrx — el grid de 3 columnas

| Propiedad | Valor |
|---|---|
| Fill | `Transparent` · sin borde |
| X | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* |
| Y | `cmpEncTrx.Y + cmpEncTrx.Height` |
| Width / Height | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* / `Parent.Height - Self.Y - gblE.Pad` |

Hijos (X/Y relativos a conContenidoTrx; **ambas fórmulas, desktop y móvil, vía `If(nfMovil,…)`**):

| Control | Propiedad | Valor |
|---|---|---|
| `lblHeadLocsTrx` (Etiqueta clásica) | Estilo | ESTILO-SecHead · Text `"LOCALIZADORES"` |
| | X/Y/W/H | `0` / `0` / `If(Parent.Width < 1080, 150, 185)` / `18` · `Visible = !nfMovil` |
| `galLocsTrx` | — | §5 (desktop) |
| `galLocsMovTrx` | — | §5 (móvil) |
| `lblHeadColaTrx` (Etiqueta clásica) | Estilo | ESTILO-SecHead |
| | Text | `"PENDIENTES" & If(varLocSelTrx <> "Todos", " · " & Upper(First(Split(varLocSelTrx, " ")).Value), "") & " · " & CountRows(galColaTrx.AllItems) & " · FIFO"` |
| | X | `If(nfMovil, 0, If(Parent.Width < 1080, 150, 185) + 12)`  *(ancho col 1 + gap 12 — breakpoint R1-12)* |
| | Y | `If(nfMovil, conDetalleTrx.Y + conDetalleTrx.Height + 8, 0)` |
| | W/H | `If(Parent.Width < 1080, 210, 255)` / `18` |
| `galColaTrx` | — | §6 |
| `cmpVacioColaTrx` (cmpVacio) | Icono/Titulo/Mensaje | `"🎉"` / `"Cola vacía"` / `"Pasá al siguiente localizador."` |
| | X/Y/W/H | los mismos de `galColaTrx` (misma fórmula, H `160`) |
| | Visible | `CountRows(colPendTrx) > 0 && CountRows(galColaTrx.AllItems) = 0` |
| `cmpVacioTodoTrx` (cmpVacio, receta 7.12) | Icono/Titulo/Mensaje | `"🎉"` / `"Sin pendientes"` / `"No hay nada por transcribir. Volvé más tarde o tocá ↻ Actualizar."` |
| | X/Y/W/H | `0` / `Parent.Height/2 - 90` / `Parent.Width` / `180` |
| | Visible | `CountRows(colPendTrx) = 0` |
| `conDetalleTrx` | — | §7 |

## 5. Columna 1 — localizadores (galería doble: vertical desktop, horizontal móvil)

**`galLocsTrx` (Galería vertical en blanco)** — `Visible = !nfMovil`:

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `0` / `22` / `If(Parent.Width < 1080, 150, 185)` / `Parent.Height - 22` |
| Items | `colLocsTrx` *(materializada por ⟨BLOQUE L⟩ — orden ya horneado)* |
| TemplateSize / TemplatePadding | `54` / `0` |
| ShowScrollbar | `false` |
| OnSelect | `Set(varLocSelTrx, ThisItem.locTrx); Set(varActualTrx, Blank()); ⟨BLOQUE R⟩; If(!nfMovil, SetFocus(txtCodigoTrx))` *(el foco vuelve al input — R1-04)* |

Plantilla (receta 7.3: la acción vive en la galería, TODO hijo `OnSelect = Select(Parent)`):

| Control | Propiedad | Valor |
|---|---|---|
| `btnLocCardTrx` (Botón clásico, al FONDO del z-order) | X/Y/W/H | `0` / `0` / `Parent.TemplateWidth` / `46` |
| | Fill | `If(ThisItem.locTrx = varLocSelTrx, gblC.teal, gblC.card)` *(fila activa = versión "on" teal del mockup)* |
| | BorderColor/Thickness | `gblC.borde` / `1` · `Radius* = 10` · Text `""` · `HoverFill = ColorFade(Self.Fill, 5%)` |
| | OnSelect | `Select(Parent)` |
| `lblLocNomTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.PadSm` / `10` / `Parent.TemplateWidth - 66` / `26` |
| | Text | `If(ThisItem.locTrx = "Todos", "Todos", "🏍️ " & ThisItem.locTrx)` |
| | FontWeight / Size | `Bold` / `9` · `Color = If(ThisItem.locTrx = varLocSelTrx, Color.White, gblC.ink)` |
| | OnSelect | `Select(Parent)` |
| `chpLocNTrx` (Botón clásico, chip receta 7.2) | X/Y | `Parent.TemplateWidth - Self.Width - gblE.PadSm` / `10` |
| | Text | `Text(ThisItem.nTrx)` |
| | Fill / Color | `If(ThisItem.locTrx = varLocSelTrx, gblC.blancoTr, gblC.hairline)` / `If(ThisItem.locTrx = varLocSelTrx, Color.White, gblC.ink2)` |
| | OnSelect | `Select(Parent)` |

**`galLocsMovTrx` (Galería horizontal en blanco)** — `Visible = nfMovil` · mismos `Items`,
mismo `OnSelect` de galería **pero SIN la línea final `If(!nfMovil, SetFocus(txtCodigoTrx))`**
(en móvil no se roba el foco: dispararía el teclado en pantalla).
`X/Y/H = 0 / 0 / 56` · `Width = Parent.Width` · `TemplateSize = 150`.
Plantilla idéntica con sufijo `Mov` (`btnLocCardMovTrx` H `46` W `Parent.TemplateWidth - 8`;
`lblLocNomMovTrx` W `Parent.TemplateWidth - 60`; `chpLocNMovTrx` X `Parent.TemplateWidth - Self.Width - 14`)
— mismas fórmulas de Fill/Color/Text/OnSelect que sus gemelos de arriba, control por control.

## 6. Columna 2 — galColaTrx (la cola FIFO)

| Propiedad | Valor |
|---|---|
| X | `If(nfMovil, 0, If(Parent.Width < 1080, 150, 185) + 12)` *(ancho col 1 + gap 12 — breakpoint R1-12)* |
| Y | `lblHeadColaTrx.Y + 22` |
| Width | `If(nfMovil, Parent.Width, If(Parent.Width < 1080, 210, 255))` |
| Height | `Parent.Height - Self.Y` |
| Items | `SortByColumns(Filter(colPendTrx, varLocSelTrx = "Todos" || '[IN] NombreAgente' = varLocSelTrx), "Fecha de captura", SortOrder.Ascending)` *(alternativa `Sort(…)` — nota del ⟨BLOQUE R⟩)* |
| TemplateSize | `gblE.AltoFila` |
| TemplateFill | `If(!IsBlank(varActualTrx) && ThisItem.'[IN] Numero de Interaccion' = varActualTrx.'[IN] Numero de Interaccion', gblC.tealSuave, gblC.card)` |
| OnSelect | `Set(varActualTrx, ThisItem); ⟨BLOQUE S⟩; If(!nfMovil, SetFocus(txtCodigoTrx))` *(el foco vuelve al input — R1-04)* |

Plantilla (receta 7.3 — fila clicable completa):

| Control | Propiedad | Valor |
|---|---|---|
| `lblColaNomTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.PadSm` / `8` / `Parent.TemplateWidth - 2*gblE.PadSm` / `20` |
| | Text | `ThisItem.clienteTrx` *(ya viene en Upper — LED-01)* · `FontWeight = Bold` · `Size = 9` |
| | OnSelect | `Select(Parent)` |
| `lblColaMetaTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.PadSm` / `28` / `Parent.TemplateWidth - 2*gblE.PadSm` / `30` |
| | Text | `ThisItem.'[IN] Numerocuenta' & " · " & Text(ThisItem.'Fecha de captura', "HH:mm") & If(ThisItem.'[IN] Aplica pago', " · 💵", "") & If(ThisItem.'[IN] Actualiza info', " · ✏️", "") & If(varLocSelTrx = "Todos", " · 🏍️ " & First(Split(ThisItem.'[IN] NombreAgente', " ")).Value, "")` |
| | Size / Color | `8` / `gblC.muted` · `Wrap = true` · `OnSelect = Select(Parent)` |
| `recSepColaTrx` (Rectángulo, receta 7.11) | X/Y/W/H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · `Fill = gblC.hairline` · `OnSelect = Select(Parent)` |

## 7. Columna 3 — conDetalleTrx (la mesa de trabajo)

| Propiedad | Valor |
|---|---|
| Estilo | ESTILO-Card |
| X | `If(nfMovil, 0, If(Parent.Width < 1080, 384, 464))`  *(384 = 150+12+210+12 · 464 = 185+12+255+12 — breakpoint R1-12)* |
| Y | `If(nfMovil, 64, 0)`  *(64 = tira de locs 56 + 8)* |
| Width | `If(nfMovil, Parent.Width, Parent.Width - If(Parent.Width < 1080, 384, 464))` |
| Height | `If(nfMovil, (Parent.Height - 64) * 0.55, Parent.Height)` |

> Móvil apila: locs (horizontal, arriba) → detalle (55% del alto restante) → cola (el resto)
> — orden decidido en **LED-18** (el DOM del mockup ordenaba distinto por descuido del CSS).
> Los contenedores manuales NO scrollean: si el detalle no entra, las galerías internas
> (pagos/cambios) scrollean solas — aceptado.

Hijos (X/Y relativos a conDetalleTrx). Todos con `Visible = !IsBlank(varActualTrx)` salvo
indicación (los de pagos/datos ya lo incluyen en su conjunción):

| Control | Propiedad | Valor |
|---|---|---|
| `lblPosTrx` (Etiqueta clásica) | Estilo | ESTILO-SecHead · X/Y/W/H `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad - 90` / `16` |
| | Text | `With({_cola: SortByColumns(Filter(colPendTrx, varLocSelTrx = "Todos" || '[IN] NombreAgente' = varLocSelTrx), "Fecha de captura", SortOrder.Ascending)}, (CountRows(Filter(_cola, 'Fecha de captura' < varActualTrx.'Fecha de captura')) + 1) & " DE " & CountRows(_cola)) & If(varLocSelTrx <> "Todos", " · " & Upper(varLocSelTrx), "") & " · " & varActualTrx.'[IN] Numero de Interaccion' & " · 🏍️ " & Upper(varActualTrx.'[IN] NombreAgente') & " · " & Text(varActualTrx.'Fecha de captura', "HH:mm")` |
| `lblMapaTrx` (Etiqueta clásica) | X/Y/W/H | `Parent.Width - gblE.Pad - 80` / `gblE.PadSm` / `80` / `16` · `Size = 8` · `FontWeight = Bold` · `Color = gblC.teal` · `Align = Align.Right` |
| | Text / Visible | `"📍 mapa"` / `!IsBlank(varActualTrx) && !IsBlank(varActualTrx.'[IN] Latitud')` |
| | OnSelect | `Launch("https://www.google.com/maps?q=" & varActualTrx.'[IN] Latitud' & "," & varActualTrx.'[IN] Longitud')` |
| `conIdentidadTrx` (Contenedor) | Estilo | ESTILO-CardTeal · X/Y/W/H `gblE.Pad` / `34` / `Parent.Width - 2*gblE.Pad` / `132` |
| `lblComentTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `conIdentidadTrx.Y + conIdentidadTrx.Height + gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `96` |
| | Text | `varActualTrx.'[IN] Comentario'` · `Wrap = true` · `Size = 11` · `Fill = gblC.pantalla` · Padding interno `12` |
| `lblSecPagosTrx` (Etiqueta clásica) | Estilo | ESTILO-SecHead · X/W `gblE.Pad` / `Parent.Width - 2*gblE.Pad` · H `18` |
| | Y | `lblComentTrx.Y + lblComentTrx.Height + gblE.PadSm` |
| | Text / Visible | `"PAGOS (" & CountRows(galPagosTrx.AllItems) & ")"` / `!IsBlank(varActualTrx) && varActualTrx.'[IN] Aplica pago'` |
| `galPagosTrx` | — | tabla siguiente |
| `lblSecDatosTrx` (Etiqueta clásica) | Estilo | ESTILO-SecHead con `Color = gblC.espInk` · X/W como lblSecPagosTrx · H `18` |
| | Y | `If(!IsBlank(varActualTrx) && varActualTrx.'[IN] Aplica pago', galPagosTrx.Y + galPagosTrx.Height + gblE.PadSm, lblSecPagosTrx.Y)` |
| | Text / Visible | `"✏️ ACTUALIZACIÓN DE DATOS — GRABALA TAMBIÉN EN EL CORE"` / `!IsBlank(varActualTrx) && varActualTrx.'[IN] Actualiza info'` · `Wrap = true` |
| `conCambiosTrx` (Contenedor) | — | tabla de §7.2 |
| `cmpVacioDetTrx` (cmpVacio) | Icono/Titulo/Mensaje | `"✨"` / `"Nada seleccionado"` / `"Elegí un localizador con pendientes."` |
| | X/Y/W/H | `gblE.Pad` / `Parent.Height/2 - 80` / `Parent.Width - 2*gblE.Pad` / `160` |
| | Visible | `IsBlank(varActualTrx) && CountRows(colPendTrx) > 0` |
| `conCodigoTrx` (Contenedor) | — | §7.3 |

**Hijos de conIdentidadTrx** (relativos a él; textos blancos por ESTILO-CardTeal):

| Control | Propiedad | Valor |
|---|---|---|
| `lblNombreTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad - 110` / `24` |
| | Text | `varActualTrx.clienteTrx` · `Size = 13` · `FontWeight = Bold` · `Color = Color.White` *(LED-01)* |
| `chpCifTrx` (Botón clásico, receta 7.2) | X/Y | `Parent.Width - gblE.Pad - Self.Width` / `gblE.PadSm` |
| | Text / Fill / Color | `varActualTrx.cifTrx` / `gblC.blancoTr` / `Color.White` |
| `lblCuentaTagTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `40` / `Parent.Width - 2*gblE.Pad` / `14` |
| | Text | `"N° TARJETA / PRÉSTAMO"` · `Size = 8` · `FontWeight = Bold` · `Color = RGBA(255,255,255,0.75)` |
| `lblCuentaGrandeTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `56` / `Parent.Width - 2*gblE.Pad` / `38` |
| | Text | `varActualTrx.'[IN] Numerocuenta'` · `Size = 24` · `FontWeight = Bold` · `Color = Color.White` |
| | OnSelect | `Copy(Self.Text)` — **LED-02**: si `Copy` no existe en tu versión de Studio, dejá el OnSelect vacío y el número queda solo visual |
| `lblProductoTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `98` / `Parent.Width - 2*gblE.Pad` / `20` |
| | Text | `varActualTrx.productoTrx` · `Color = RGBA(255,255,255,0.85)` |

> Gotcha de labels largos: `lblComentTrx` tiene `Wrap=true` con Height FIJO generoso (96). Si un
> comentario excede ~6 líneas, se corta — decisión aceptada (los comentarios del localizador son
> cortos por diseño de esa app). Si molesta, subí el Height fijo; NO uses alturas auto.

### 7.1 galPagosTrx (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `gblE.Pad` / `lblSecPagosTrx.Y + 22` / `Parent.Width - 2*gblE.Pad` / `74` |
| Visible | `!IsBlank(varActualTrx) && varActualTrx.'[IN] Aplica pago'` |
| Items | `Filter('CM | TBL | Pagos', '[PG] Numero Interaccion' = varActualTrx.'[IN] Numero de Interaccion')` *(delegable, igualdad — y chica: 1–3 filas)* |
| TemplateSize | `34` |

| Control | Propiedad | Valor |
|---|---|---|
| `lblPagoMontoTrx` (Etiqueta clásica) | X/Y/W/H | `0` / `6` / `Parent.TemplateWidth * 0.55` / `22` |
| | Text | `If(ThisItem.'[PG] Moneda' = 'Choice | CM | Monedas en Pago'.USD, "US$ ", "C$ ") & Text(ThisItem.'[PG] Monto', "#,##0.00")` · `FontWeight = Bold` |
| `lblPagoReciboTrx` (Etiqueta clásica) | X/Y/W/H | `Parent.TemplateWidth * 0.55` / `6` / `Parent.TemplateWidth * 0.45` / `22` |
| | Text | `"Recibo " & ThisItem.'[PG] Numero recibo'` · `Color = gblC.muted` · `Align = Align.Right` |
| `recSepPagoTrx` (Rectángulo, receta 7.11) | X/Y/W/H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · `Fill = gblC.hairline` |

### 7.2 conCambiosTrx + galCambiosTrx — el bloque violeta (LED-09)

| Control | Propiedad | Valor |
|---|---|---|
| `conCambiosTrx` (Contenedor) | X/W | `gblE.Pad` / `Parent.Width - 2*gblE.Pad` |
| | Y | `lblSecDatosTrx.Y + 22` |
| | Height | `Max(0, Min(Min(CountRows(colCambiosTrx), 4) * 52 + 10, conCodigoTrx.Y - lblSecDatosTrx.Y - 32))` |
| | Fill / BorderColor / BorderThickness | `gblC.espBg` / `gblC.espInk` / `2` · `Radius* = gblE.RadioSm` |
| | Visible | `!IsBlank(varActualTrx) && varActualTrx.'[IN] Actualiza info'` |
| `galCambiosTrx` (Galería vertical en blanco) | X/Y/W/H | `4` / `5` / `Parent.Width - 8` / `Parent.Height - 10` |
| | Items / TemplateSize | `colCambiosTrx` *(la arma ⟨BLOQUE S⟩)* / `52` |

Plantilla:

| Control | Propiedad | Valor |
|---|---|---|
| `lblCamKTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.PadSm` / `4` / `Parent.TemplateWidth - 2*gblE.PadSm` / `14` |
| | Text | `ThisItem.campoTrx` · ESTILO-SecHead con `Color = gblC.espInk` |
| `lblCamAntesTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.PadSm` / `20` / `Parent.TemplateWidth * 0.38` / `28` |
| | Text | `ThisItem.antesTrx` · `Strikethrough = true` · `Color = gblC.muted` · `Size = 9` · `Wrap = true` |
| | Visible | `!IsBlank(ThisItem.antesTrx)` |
| `lblCamFlechaTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.PadSm + Parent.TemplateWidth * 0.38` / `20` / `20` / `28` |
| | Text / Color / FontWeight | `"→"` / `gblC.espInk` / `Bold` · `Visible = !IsBlank(ThisItem.antesTrx)` |
| `chpCamNuevoTrx` (Botón clásico, chip 7.2) | X/Y | `gblE.PadSm` / `22` · `Height = 22` |
| | Text / Fill / Color | `"NUEVO"` / `gblC.espInk` / `Color.White` · `Visible = IsBlank(ThisItem.antesTrx)` |
| `lblCamNuevoTrx` (Etiqueta clásica) | X | `If(IsBlank(ThisItem.antesTrx), gblE.PadSm + 64, gblE.PadSm + Parent.TemplateWidth * 0.38 + 24)` |
| | Y/W/H | `20` / `Parent.TemplateWidth * 0.5` / `28` · `Text = ThisItem.nuevoTrx` · `FontWeight = Bold` · `Wrap = true` |

> **Divergencia declarada** (mecánica, no visual): el mockup pinta 7 filas fijas; acá son las
> MISMAS 7 filas posibles como galería sobre `colCambiosTrx`, que ⟨BLOQUE S⟩ ya filtró a las que
> tienen valor nuevo (`Visible = !IsBlank(<campo AD>)` del mockup ≡ el `Filter` del bloque).
> Motivo: 7 filas fijas con Visible condicional dejan HUECOS de layout en canvas; la galería
> compacta sola. Con más de 4 cambios, el bloque scrollea. Contenido y estilo: idénticos al mockup.

### 7.3 conCodigoTrx — la franja del código (marcar y seguir)

| Control | Propiedad | Valor |
|---|---|---|
| `conCodigoTrx` (Contenedor) | X/Y/W/H | `0` / `Parent.Height - 64` / `Parent.Width` / `64` · `Fill = gblC.card` · sin borde |
| `recHairTrx` (Rectángulo) | X/Y/W/H | `0` / `0` / `Parent.Width` / `1` · `Fill = gblC.hairline` *(borde superior hairline)* |
| `lblCodigoTrx` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `20` / `104` / `24` · Text `"Código del core"` · `FontWeight = Semibold` · `Color = gblC.muted` · `Size = 9` |
| `txtCodigoTrx` (Entrada de texto clásica) | Estilo | ESTILO-Input · X/Y `gblE.Pad + 110` / `12` · `Width = Max(110, Parent.Width * 0.15)` · `MaxLength = 10` |
| | HintText | `"PAG01"` · `FontWeight = Bold` |
| | OnChange | `If(!IsBlank(Trim(Self.Text)) && !IsBlank(varActualTrx), Select(btnGrabarTrx))` — **LED-03**: Enter dispara OnChange → marca y avanza. Efecto colateral ACEPTADO: salir del campo por clic/Tab con código escrito también marca (es el mismo gesto de intención; documentado, no accidental). |
| `btnGrabarTrx` (Botón clásico) | Estilo | ESTILO-BotonPri · `Size = 9` · `Height = 40` · X/Y `txtCodigoTrx.X + txtCodigoTrx.Width + 8` / `12` · `Width = 120` |
| | Text | `"✓ y siguiente"` |
| | DisplayMode | `If(!IsBlank(Trim(txtCodigoTrx.Text)) && !IsBlank(varActualTrx), DisplayMode.Edit, DisplayMode.Disabled)` |
| | OnSelect | fórmula completa abajo |
| `btnSaltarTrx` (Botón clásico) | Estilo | ESTILO-BotonFantasma · `Size = 9` · `Height = 40` · X/Y `btnGrabarTrx.X + btnGrabarTrx.Width + 8` / `12` · `Width = 96` |
| | Text / OnSelect | `"Saltar →"` / fórmula completa abajo |
| `lblHintTrx` (Etiqueta clásica) | X/Y/W/H | `btnSaltarTrx.X + btnSaltarTrx.Width + 8` / `20` / `Parent.Width - Self.X - gblE.Pad` / `28` |
| | Text | `"Enter marca y avanza · la cola se navega con clic"` · `Size = 8` · `Color = gblC.muted2` · `Wrap = true` *(LED-03: sin ↑/↓ — canvas no expone teclado)* |

**btnGrabarTrx.OnSelect — COMPLETO**:

```powerfx
// 1) foto de la cola filtrada y posición del actual (colVistaTrx materializada)
ClearCollect(colVistaTrx,
    SortByColumns(
        Filter(colPendTrx, varLocSelTrx = "Todos" || '[IN] NombreAgente' = varLocSelTrx),
        "Fecha de captura", SortOrder.Ascending));
Set(varPosTrx,
    CountRows(Filter(colVistaTrx, 'Fecha de captura' < varActualTrx.'Fecha de captura')) + 1);
Set(varNumTrx, varActualTrx.'[IN] Numero de Interaccion');
// 2) patch (receta 7.8) — LA única llamada de escritura
Patch('CM | TBL | Interaccions',
    LookUp('CM | TBL | Interaccions',
        '[IN] Numero de Interaccion' = varActualTrx.'[IN] Numero de Interaccion'),
    {
        '[IN] Estado grabacion': 'Choice | CM | Estado grabacion Interaccio'.Grabado,
        '[IN] Codigo Grabacion': Upper(Trim(txtCodigoTrx.Text)),
        '[IN] Fecha grabacion': Now(),          // ← quitar estas 2 líneas cuando el flujo
        '[IN] Grabado por': gblUsuarioDV        //    de auditoría esté activo (00 §3.2)
    });
// 3) sacar de la cola local (00 §5: cero re-consulta)
Remove(colPendTrx, LookUp(colPendTrx,
    '[IN] Numero de Interaccion' = varNumTrx));
// 4) refrescar la columna de localizadores
⟨BLOQUE L⟩;
// 5) reselección: siguiente de la MISMA cola filtrada en la MISMA posición
//    (la anterior si era la última; Blank si quedó vacía)
ClearCollect(colVistaTrx,
    SortByColumns(
        Filter(colPendTrx, varLocSelTrx = "Todos" || '[IN] NombreAgente' = varLocSelTrx),
        "Fecha de captura", SortOrder.Ascending));
Set(varActualTrx,
    If(CountRows(colVistaTrx) = 0, Blank(),
        Last(FirstN(colVistaTrx, Min(varPosTrx, CountRows(colVistaTrx))))));
⟨BLOQUE S⟩;
// 6) limpiar el input
Reset(txtCodigoTrx);
// 7) confirmar ANUNCIANDO el siguiente (la reselección del paso 5 ya corrió — R1-06)
Notify("✓ " & varNumTrx & " grabada" &
        If(IsBlank(varActualTrx), " — ¡cola vacía!",
            " — siguiente: " & varActualTrx.clienteTrx),
    NotificationType.Success, 2500);
// 8) devolver el foco al input — la fluidez de la estación (solo desktop; R1-04)
If(!nfMovil, SetFocus(txtCodigoTrx))
```

**btnSaltarTrx.OnSelect — COMPLETO** (avance circular, sin Patch):

```powerfx
ClearCollect(colVistaTrx,
    SortByColumns(
        Filter(colPendTrx, varLocSelTrx = "Todos" || '[IN] NombreAgente' = varLocSelTrx),
        "Fecha de captura", SortOrder.Ascending));
If(CountRows(colVistaTrx) >= 2,
    Set(varPosTrx,
        CountRows(Filter(colVistaTrx, 'Fecha de captura' < varActualTrx.'Fecha de captura')) + 1);
    Set(varActualTrx,
        Last(FirstN(colVistaTrx, Mod(varPosTrx, CountRows(colVistaTrx)) + 1)));
    ⟨BLOQUE S⟩
);
// devolver el foco al input — la fluidez de la estación (solo desktop; R1-04)
If(!nfMovil, SetFocus(txtCodigoTrx))
```

## 8. cmpNavTrx

Instancia de `cmpNavLateral` con la receta fija de 01 §8.1: `RutaActiva = "trx"` ·
`X = 0 · Y = 0 · Height = Parent.Height` · `Visible = !nfMovil || gblNavMovilAbierta` ·
**al FINAL del árbol** (en móvil flota encima de todo).

## Notas — divergencias y aproximaciones declaradas

- **LED-01/02/03/09/10** aplicadas donde se citan. **LED-03** amplía: el efecto colateral del
  `OnChange` (perder foco con texto = marcar) es decisión aceptada de esta guía.
- **Posición "N de M"**: N se calcula contando filas con `'Fecha de captura'` menor — si dos
  interacciones comparten timestamp exacto, comparten N (aproximación declarada; el orden FIFO
  visible no se ve afectado). La misma cuenta ancla la reselección de btnGrabar/btnSaltar.
- **Locs en cero**: el mockup atenúa (`.zero`) a los localizadores sin pendientes; acá la columna
  se arma desde `colPendTrx`, así que el loc que llega a 0 DESAPARECE de la lista (y si estaba
  seleccionado, su cola muestra el vacío 🎉). Coherente con "cola del día" — declarado.
- **Bloque de cambios como galería** (no 7 filas fijas): declarado en §7.2.
- **`'CM | TBL | Actualizacion de datos'`**: plural raro — confirmá el nombre del datasource en
  el panel de datos (01 §2); si IntelliSense difiere, el suyo manda.

## Checklist de salida

- [ ] Entrar con rol ≠ Backoffice: rebota a scrInicio (primera línea del OnVisible).
- [ ] Abrir la pantalla = 2 llamadas a Dataverse (Monitor); al seleccionar una interacción con
      ✏️ se suman las 3 lecturas de AD/teléfonos/direcciones; marcar = 1 Patch y nada más.
- [ ] "Todos" encabeza la columna 1 con el total; el resto ordenado por conteo descendente.
- [ ] Clic en un loc filtra la cola, auto-selecciona el primero FIFO y pinta la fila teal.
- [ ] Escribir código + Enter: marca, notifica "✓ N grabada — siguiente: <cliente>" (o
      "— ¡cola vacía!" si no queda nada), avanza al siguiente de la misma posición y el input
      queda limpio; el botón se deshabilita sin código.
- [ ] Tras marcar/saltar/seleccionar en desktop, el cursor vuelve al input de código sin clic
      (SetFocus; en móvil no aplica).
- [ ] Saltar → avanza circular sin escribir nada en Dataverse.
- [ ] Interacción con 💵 muestra sus pagos con símbolo por moneda y "Recibo N".
- [ ] Interacción con ✏️ muestra el bloque violeta: tachado → nuevo, chip NUEVO si no había dato.
- [ ] Cola de un loc vacía → 🎉 "Cola vacía"; sin pendientes globales → vacío global.
- [ ] Clic en el nº de cuenta grande copia (o no hace nada si no existe `Copy` — LED-02).
- [ ] Ventana < 920 px: locs pasan a tira horizontal arriba, detalle al medio, cola abajo.
- [ ] Cero subrayados azules de delegación en toda la pantalla.
