# Guía 11 — scrAgentes (Agentes y usuarios · Admin)

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrAgentes.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol (LED-20).
> - Alerta de agentes sin usuario: `CountIf('CM | TBL | Agentes', '[AG] Activo' && 'Email Usuario' = Blank())` (no `IsBlank('[AG] Usuario')`, lookup no delega). ⚠ `lblAlertaDetalleAge` aún lista desde `colAgentesAge` con `IsBlank('[AG] Usuario')` — puede no cuadrar con el conteo.
> - **B-04: la fila YA NO tiene Cuadro combinado.** El ComboBox clásico no pinta registros de Dataverse en esta app (Users incluido) y además disparaba escrituras al abrir (B-08). La fila muestra el vínculo actual en texto (`lblUsrAge`) + botón **`btnVincularAge`** ("Vincular"/"Cambiar") que abre UN panel (`conPanelAge`, receta 7.4) con **buscador `txtBuscarUsrAge` + galería `galUsrAge` sobre `Users`**; tocar un usuario confirma el vínculo. Agente objetivo en `varAgenteAge`, visibilidad en `varPanelAge`. Ver §4.1 y 01 §11.
> - Desvincular es el botón aparte `btnDesvincularAge` (B-08); todas las escrituras con `IfError` + `Notify` del detalle (B-03).
> - `Parent.TemplateHeight` en vez de `Parent.TemplateSize` en los Y del template.
> - `AccessibleLabel` en galerías y buscador, foco visible; `ShowScrollbar = false`. Filas de agentes sin overlay (no clicables); filas de `galUsrAge` con overlay `btnFilaUsrAge`.

> **Por qué esta pantalla importa (leelo antes de construir):** la app del localizador identifica
> al agente que entra por **`'Email Usuario'`** (`LookUp` contra `User().Email`) y el plugin
> `RepartoAsignacion` asigna el *owner* de cada asignación por **`'[AG] Usuario'`**. Un agente activo SIN
> usuario vinculado = **no entra a la app del celular** y **sus asignaciones van al equipo "CM · Cartera sin cobrador"** (sin miembros: nadie las ve). La vista "sin usuario" de esta pantalla debe estar **VACÍA** antes de operar.

**Propósito:** el Admin liga cada localizador (`'CM | TBL | Agentes'`) a su usuario de Entra
(`Users`) y lo activa/desactiva. Filas **NO clicables** — toda la acción vive en los botones de
cada fila ("Vincular/Cambiar", "Desvincular", "Desactivar/Activar"); el vínculo se elige en el
panel `conPanelAge` (igual que el mockup: `row static`).

**Contrato de entrada:** `"Admin" in gblRol` (App.OnStart, 00 §3.1) · tokens `gblC`/`gblE`
(App.OnStart) · `nfMovil` (App.Formulas) · `colNav` (App.OnStart, la consume `cmpNavAge`) ·
`gblNavMovilAbierta` (estado del drawer móvil — la setea cmpEncabezado/cmpNavLateral, 01 §8).
Datasources: `'CM | TBL | Agentes'` y `Users`. Esta pantalla crea `colAgentesAge` en su OnVisible.

## Árbol de controles

```
scrAgentes                          ESTILO-Pantalla
├── cmpEncabezadoAge                cmpEncabezado (receta 7.5)
├── conContenidoAge                 Contenedor (transparente, área de trabajo)
│   ├── btnActualizarAge            Botón clásico "↻ Actualizar" (Fantasma)
│   ├── conAlertaAge                Contenedor — card de alerta warn (agentes sin usuario)
│   │   ├── recBordeAlertaAge       Rectángulo — borde izquierdo pendInk
│   │   ├── lblAlertaTituloAge      Etiqueta clásica
│   │   └── lblAlertaDetalleAge     Etiqueta clásica
│   ├── conListaAge                 Contenedor ESTILO-Card (receta 7.1)
│   │   ├── galAgentesAge           Galería vertical en blanco
│   │   │   ├── lblNombreAge        Etiqueta clásica
│   │   │   ├── lblEmailAge         Etiqueta clásica
│   │   │   ├── lblUsrAge           Etiqueta clásica — usuario vinculado actual (nombre o email)
│   │   │   ├── btnVincularAge      Botón clásico "Vincular"/"Cambiar" → abre conPanelAge
│   │   │   ├── btnDesvincularAge   Botón clásico "Desvincular" (B-08)
│   │   │   ├── chpVinculoAge       Botón clásico — chip receta 7.2
│   │   │   ├── chpEstadoAge        Botón clásico — chip receta 7.2
│   │   │   ├── btnToggleAge        Botón clásico "Desactivar"/"Activar"
│   │   │   └── recSepAge           Rectángulo — separador receta 7.11
│   │   └── cmpVacioAge             cmpVacio (receta 7.12)
│   └── lblAdvertenciaAge           Etiqueta clásica — advertencia bajo la galería
├── recScrimAge                     Rectángulo — scrim del panel (receta 7.4)
├── conPanelAge                     Contenedor — panel lateral "Vincular usuario" (receta 7.4)
│   ├── lblTituloPanelAge           "Vincular usuario — <agente>"
│   ├── icoCerrarPanelAge           Ícono Cancel
│   ├── lblActualPanelAge           "Vínculo actual: …"
│   ├── lblBuscarKAge               "Usuario de Entra (tocá uno para vincularlo)"
│   ├── txtBuscarUsrAge             Entrada de texto clásica (buscador, DelayOutput)
│   ├── icoBuscarUsrAge             Ícono lupa
│   ├── galUsrAge                   Galería vertical — usuarios (nombre + correo)
│   │   ├── lblNomFilaUsrAge / lblMailFilaUsrAge / recSepUsrAge
│   │   └── btnFilaUsrAge           overlay de fila (tocar = CONFIRMAR vínculo)
│   ├── lblSinUsrAge                "Ningún usuario empieza con «…»"
│   └── btnCancelarPanelAge         "Cancelar" (cierra sin escribir)
└── cmpNavAge                       cmpNavLateral (AL FINAL del árbol — flota en móvil)
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | fórmula §1.1 |

### 1.1 OnVisible (pegar completo — orden receta 7.7: guard → colección)

```powerfx
If(!("Admin" in gblRol), Navigate(scrInicio));
ClearCollect(colAgentesAge, Sort('CM | TBL | Agentes', '[AG] Nombre'))
```

> Tabla chica y de alcance organizacional (los localizadores de la empresa): la cota de 2 000
> filas no delegables (00 §4.1) es trivial acá. `Sort` sobre columna escalar delega; una sola
> llamada a Dataverse por visita.

## 2. Encabezado y navegación

| Control | Propiedad | Valor |
|---|---|---|
| `cmpEncabezadoAge` (cmpEncabezado) | X/Y/W/H | receta 7.5 exacta: `If(nfMovil, 0, gblE.AnchoNav)` / `If(nfMovil, gblE.AltoBarra, 0)` / `Parent.Width - Self.X` / `84` |
| | Titulo | `"Agentes y usuarios"` |
| | Subtitulo | `"Ligá cada localizador a su usuario. Sin usuario = no entra a la app del celular."` |
| `cmpNavAge` (cmpNavLateral) | — | instanciación estándar de la guía 01 §8.1: `RutaActiva = "age"` · `Visible = !nfMovil \|\| gblNavMovilAbierta` · `X=0 · Y=0 · Height=Parent.Height` · al FINAL del árbol |

## 3. Área de contenido

| Control | Propiedad | Valor |
|---|---|---|
| `conContenidoAge` (Contenedor) | X | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* |
| | Y | `cmpEncabezadoAge.Y + cmpEncabezadoAge.Height` |
| | Width | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* |
| | Height | `Parent.Height - Self.Y - gblE.Pad` |
| | Fill | `Color.Transparent` |
| `btnActualizarAge` (Botón clásico) | X/Y/W/H | `Parent.Width - Self.Width` / `0` / `150` / `gblE.AltoBoton` *(150 = ancho fijo documentado del botón)* |
| | Text | `"↻ Actualizar"` |
| | Estilo | ESTILO-BotonFantasma |
| | OnSelect | `ClearCollect(colAgentesAge, Sort('CM | TBL | Agentes', '[AG] Nombre')); Notify("Lista actualizada", NotificationType.Success, 2000)` |

### 3.1 Card de alerta — agentes activos sin usuario

| Control | Propiedad | Valor |
|---|---|---|
| `conAlertaAge` (Contenedor) | X/Y/W/H | `0` / `btnActualizarAge.Y + btnActualizarAge.Height + gblE.PadSm` / `Parent.Width` / `96` *(96 = alto fijo documentado de la card)* |
| | Fill / Radius* | `gblC.pendBg` / `gblE.Radio` |
| | Visible | `CountRows(Filter(colAgentesAge, '[AG] Activo' && IsBlank('[AG] Usuario'))) > 0` |
| `recBordeAlertaAge` (Rectángulo) | X/Y/W/H | `0` / `0` / `4` / `Parent.Height` *(4 = grosor fijo del borde de acento)* |
| | Fill | `gblC.pendInk` |
| `lblAlertaTituloAge` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `22` |
| | Text | `"⚠ " & CountRows(Filter(colAgentesAge, '[AG] Activo' && IsBlank('[AG] Usuario'))) & " agente(s) activo(s) sin usuario vinculado"` |
| | FontWeight / Color / Size | `Bold` / `gblC.pendInk` / `11` |
| `lblAlertaDetalleAge` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `lblAlertaTituloAge.Y + lblAlertaTituloAge.Height + 4` / `Parent.Width - 2*gblE.Pad` / `Parent.Height - Self.Y - gblE.PadSm` |
| | Text | `Concat(Filter(colAgentesAge, '[AG] Activo' && IsBlank('[AG] Usuario')), '[AG] Nombre', ", ") & ". Esta lista debe estar VACÍA antes de operar: sin usuario no entran a la app del celular y Comp-14 no aísla sus asignaciones."` |
| | Color / Size / Wrap | `gblC.pendInk` / `9` / `true` |

## 4. Lista de agentes

| Control | Propiedad | Valor |
|---|---|---|
| `conListaAge` (Contenedor) | X/Y/W/H | `0` / `If(conAlertaAge.Visible, conAlertaAge.Y + conAlertaAge.Height + gblE.Pad, conAlertaAge.Y)` / `Parent.Width` / `Parent.Height - Self.Y - 28 - gblE.PadSm` *(28 = alto reservado a la advertencia §5)* |
| | Estilo | ESTILO-Card (receta 7.1) |
| `cmpVacioAge` (cmpVacio) | — | receta 7.12: `Visible = CountRows(colAgentesAge) = 0`, centrado en `conListaAge`. `Icono = "🏍️"` · `Titulo = "Sin agentes"` · `Mensaje = "El import diario todavía no creó localizadores."` |

### galAgentesAge (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X/Y/W/H | `gblE.PadSm` / `gblE.PadSm` / `Parent.Width - 2*gblE.PadSm` / `Parent.Height - 2*gblE.PadSm` |
| Items | `colAgentesAge` |
| TemplateSize / TemplatePadding | `If(nfMovil, 148, 72)` / `0` |
| TemplateFill | `gblC.card` *(SIN resaltado de selección — filas no clicables, receta 7.3 NO aplica)* |
| ShowScrollbar | `true` |

Dentro del template (coordenadas relativas al template; desktop = 1 fila de 72,
móvil = 3 renglones apilados dentro de 148):

| Control | Propiedad | Valor |
|---|---|---|
| `lblNombreAge` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `If(nfMovil, 8, 12)` / `If(nfMovil, Parent.TemplateWidth - 2*gblE.Pad, Parent.TemplateWidth*32% - gblE.Pad)` / `22` |
| | Text | `ThisItem.'[AG] Nombre'` |
| | FontWeight / Size | `Bold` / `11` |
| `lblEmailAge` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `lblNombreAge.Y + lblNombreAge.Height` / `lblNombreAge.Width` / `18` |
| | Text | `If(IsBlank(ThisItem.'Email Usuario'), "sin correo", ThisItem.'Email Usuario')` |
| | Color / Size | `gblC.muted` / `9` |
| `lblUsrAge` (Etiqueta clásica) | X/Y/H | `If(nfMovil, gblE.Pad, Parent.TemplateWidth*32%)` / `If(nfMovil, 54, (Parent.TemplateHeight - Self.Height)/2)` / `gblE.AltoInput` |
| | Width | `If(nfMovil, Parent.TemplateWidth - 2*gblE.Pad, Parent.TemplateWidth*30%) - btnVincularAge.Width - gblE.PadSm` |
| | Text | `If(IsBlank(ThisItem.'Email Usuario'), "— Sin usuario —", Coalesce(ThisItem.'[AG] Usuario'.'Full Name', ThisItem.'Email Usuario'))` |
| `btnVincularAge` (Botón clásico) | X/Y/W/H | `If(nfMovil, Parent.TemplateWidth - Self.Width - gblE.Pad, Parent.TemplateWidth*62% - Self.Width)` / `lblUsrAge.Y + (lblUsrAge.Height - Self.Height)/2` / `96` / `34` |
| | Text | `If(IsBlank(ThisItem.'Email Usuario'), "Vincular", "Cambiar")` (sin usuario → ESTILO-BotonPri; con usuario → secundario) |
| | OnSelect | `Set(varAgenteAge, ThisItem); Reset(txtBuscarUsrAge); Set(varPanelAge, true)` — NO escribe nada (§4.1) |
| `chpVinculoAge` (Botón clásico, chip 7.2) | X/Y/W/H | `If(nfMovil, gblE.Pad, Parent.TemplateWidth*64% + gblE.PadSm)` / `If(nfMovil, 106, (Parent.TemplateSize - Self.Height)/2)` / `14 + 7*Len(Self.Text)` / `26` |
| | Text | `If(IsBlank(ThisItem.'[AG] Usuario'), "Sin usuario", "✓ Vinculado")` |
| | Fill / Color | `If(IsBlank(ThisItem.'[AG] Usuario'), gblC.redBg, gblC.okBg)` / `If(IsBlank(ThisItem.'[AG] Usuario'), gblC.redInk, gblC.okInk)` |
| | DisplayMode | `DisplayMode.View` (resto: receta 7.2) |
| `chpEstadoAge` (Botón clásico, chip 7.2) | X/Y/W/H | `chpVinculoAge.X + chpVinculoAge.Width + gblE.PadSm` / `chpVinculoAge.Y` / `14 + 7*Len(Self.Text)` / `26` |
| | Text | `If(ThisItem.'[AG] Activo', "Activo", "Inactivo")` |
| | Fill / Color | `If(ThisItem.'[AG] Activo', gblC.hairline, gblC.redBg)` / `If(ThisItem.'[AG] Activo', gblC.ink2, gblC.redInk)` *(variante neutro/error de 7.2)* |
| | DisplayMode | `DisplayMode.View` (resto: receta 7.2) |
| `btnToggleAge` (Botón clásico) | X/Y/W/H | `Parent.TemplateWidth - Self.Width - gblE.Pad` / `If(nfMovil, 102, (Parent.TemplateSize - Self.Height)/2)` / `110` / `34` *(110/34 = tamaño fijo documentado del botón de fila)* |
| | Text | `If(ThisItem.'[AG] Activo', "Desactivar", "Activar")` |
| | Estilo | activo → ESTILO-BotonFantasma · inactivo → ESTILO-BotonSec, en fórmulas: `Fill = If(ThisItem.'[AG] Activo', gblC.card, gblC.tealSuave)` · `Color = If(ThisItem.'[AG] Activo', gblC.ink2, gblC.teal)` · `BorderColor = gblC.borde` · `BorderThickness = If(ThisItem.'[AG] Activo', 1, 0)` · `Radius* = 10` |
| | OnSelect | fórmula §4.2 |
| `recSepAge` (Rectángulo, receta 7.11) | X/Y/W/H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · `Fill = gblC.hairline` |

> Las filas NO son clicables: la galería queda sin `OnSelect`, ningún hijo lleva `Select(Parent)`
> y `TemplateFill` es plano. (Las galerías auto-seleccionan la 1ª fila — acá no tiene consecuencia
> visual ni de estado, y así se deja.)

### 4.1 Panel `conPanelAge` — el vínculo con Entra (el corazón de la pantalla)

> **B-04:** el Cuadro combinado de la fila (`Items = Users`) renderizaba opciones con el texto
> vacío (el ComboBox clásico no pinta registros de Dataverse en esta app) y su `OnChange` se
> disparaba con la preselección al abrir (B-08). Reemplazo: botón por fila + UN panel con
> buscador + galería. No se anidan galerías (el panel vive fuera de `galAgentesAge`).

`recScrimAge` y `conPanelAge` van al final del árbol (antes de `cmpNavAge`) con la receta 7.4
(`varPanelAge`). El scrim, `icoCerrarPanelAge` y `btnCancelarPanelAge` cierran con
`Set(varPanelAge, false); Set(varAgenteAge, Blank()); Reset(txtBuscarUsrAge)` — sin escribir.
El `OnVisible` de la pantalla deja el panel cerrado y `varAgenteAge` en blanco.

| Control | Propiedad | Valor |
|---|---|---|
| `txtBuscarUsrAge` (Entrada de texto clásica) | Y/H | `lblBuscarKAge.Y + lblBuscarKAge.Height + 4` / `gblE.AltoInput` · ESTILO-Input · PaddingLeft `34` · DelayOutput `true` |
| | HintText | `"Buscar por nombre o correo (mín. 2)"` |
| `galUsrAge` (Galería vertical) | Y | `txtBuscarUsrAge.Y + txtBuscarUsrAge.Height + 4` |
| | TemplateSize / Height | `48` / `Min(Max(Self.AllItemsCount, 1) * Self.TemplateHeight, btnCancelarPanelAge.Y - gblE.PadSm - Self.Y)` |
| | Visible | `Len(Trim(txtBuscarUsrAge.Text)) >= 2` |
| | Items | fórmula ↓ |
| | OnSelect | fórmula ↓ (tocar una fila = CONFIRMAR) |
| `lblNomFilaUsrAge` / `lblMailFilaUsrAge` | Text | `ThisItem.'Full Name'` / `If(IsBlank(ThisItem.'Primary Email'), "sin correo principal", ThisItem.'Primary Email')` |
| `btnFilaUsrAge` (overlay) | Text | `"Vincular a " & ThisItem.'Full Name' & " · " & ThisItem.'Primary Email'` · transparente, `Select(Parent)`, `TabIndex 0` |
| `lblSinUsrAge` | Visible | `galUsrAge.Visible && galUsrAge.AllItemsCount = 0` |

```powerfx
// galUsrAge.Items — delegable (StartsWith en ambas columnas); con < 2 caracteres, centinela
// "§§": no se consulta la tabla entera (B-19) y siempre hay tabla (B-06).
With({t: If(Len(Trim(txtBuscarUsrAge.Text)) < 2, "§§", Trim(txtBuscarUsrAge.Text))},
    Filter(Users, StartsWith('Full Name', t) || StartsWith('Primary Email', t))
)
```

**galUsrAge.OnSelect (pegar completo):**

```powerfx
If(
    IsBlank(ThisItem.'Primary Email'),
    Notify("El usuario " & ThisItem.'Full Name' & " no tiene correo principal: no podría entrar a la app del celular. Elegí otro.",
        NotificationType.Warning),
    Lower(ThisItem.'Primary Email') = Lower(Coalesce(varAgenteAge.'Email Usuario', "")),
    Notify(varAgenteAge.'[AG] Nombre' & " ya está vinculado a " & Lower(ThisItem.'Primary Email'),
        NotificationType.Information, 2500),
    IfError(
        Patch('CM | TBL | Agentes',
            LookUp('CM | TBL | Agentes', '[AG] Nombre' = varAgenteAge.'[AG] Nombre'),
            {
                '[AG] Usuario': ThisItem,
                'Email Usuario': Lower(ThisItem.'Primary Email')
            }
        ),
        Notify("No se pudo vincular " & varAgenteAge.'[AG] Nombre' & ": " & FirstError.Message,
            NotificationType.Error),
        Notify("✓ " & varAgenteAge.'[AG] Nombre' & " vinculado a " & Lower(ThisItem.'Primary Email'),
            NotificationType.Success, 2500);
        Set(varPanelAge, false);
        Set(varAgenteAge, Blank());
        Reset(txtBuscarUsrAge);
        ClearCollect(colAgentesAge, Sort('CM | TBL | Agentes', '[AG] Nombre'))
    )
)
```

> ⚠ **Se escriben las DOS columnas, siempre juntas**: `'[AG] Usuario'` (lookup — lo usa Comp-14
> para asignar el owner) y `'Email Usuario'` (texto en minúsculas — lo usa la canvas del
> localizador para el login). Patchear una sola deja al agente en un estado inconsistente:
> entra a la app pero sus asignaciones no se aíslan, o al revés. Si tocás esta fórmula,
> mantené las dos.
>
> Misma lógica que el viejo `OnChange` (B-08): SOLO vincula y SOLO si el usuario elegido es
> distinto del vínculo actual; un usuario sin correo principal no se vincula (no podría hacer
> login en el celular). Si el Patch falla, Notify con el detalle y el panel queda ABIERTO.
> Desvincular es `btnDesvincularAge` (fuera del panel). El `LookUp` por `'[AG] Nombre'` delega
> (igualdad sobre la columna primaria, que el import mantiene única).

### 4.2 btnToggleAge.OnSelect (pegar completo)

```powerfx
Patch('CM | TBL | Agentes',
    LookUp('CM | TBL | Agentes', '[AG] Nombre' = ThisItem.'[AG] Nombre'),
    { '[AG] Activo': !ThisItem.'[AG] Activo' }
);
Notify(ThisItem.'[AG] Nombre' & If(ThisItem.'[AG] Activo', " desactivado", " activado"),
    NotificationType.Success, 2500);
ClearCollect(colAgentesAge, Sort('CM | TBL | Agentes', '[AG] Nombre'))
```

> El `If` del Notify lee el valor VIEJO de `ThisItem` (el Patch ya lo invirtió en Dataverse pero
> la colección aún no se refrescó) — por eso "activo → desactivado" queda correcto tal cual.

## 5. Advertencia al pie

| Control | Propiedad | Valor |
|---|---|---|
| `lblAdvertenciaAge` (Etiqueta clásica) | X/Y/W/H | `0` / `conListaAge.Y + conListaAge.Height + gblE.PadSm` / `Parent.Width` / `28` *(el alto que §4 reserva)* |
| | Text | `"Desactivar un agente NO cierra sus asignaciones — eso se hace en Cierres/Reconciliación."` |
| | Color / Size / Wrap | `gblC.muted` / `9` / `true` |

## Notas

- **Mockup → canvas**: el `<select>` de `ligar()` se implementa como botón "Vincular/Cambiar" +
  panel con buscador y galería de `Users` (B-04); quitar el vínculo es `btnDesvincularAge`. Filas `row static`,
  chips y textos del mockup (`scrAgentes()`, `toggleAg()`) se conservan 1:1. Sin entradas LED
  nuevas. Toasts → `Notify` (LED-05).
- `Users` solo se consulta desde el panel y con ≥ 2 caracteres. `lblUsrAge` navega el lookup
  `'[AG] Usuario'.'Full Name'` por fila (lectura perezosa): con decenas de localizadores es
  irrelevante; si no resuelve, muestra el email (texto plano).
- Si IntelliSense difiere de un display name (`'[AG] Usuario'`, `'Email Usuario'`,
  `'Primary Email'`…), **el IntelliSense manda** — anotá y avisá (guía 01, encabezado).

## Checklist de salida

- [ ] Entrar con rol ≠ Admin rebota a `scrInicio` sin parpadeo de datos.
- [ ] Con ≥1 agente activo sin usuario: la card warn muestra el conteo y los nombres; al
      vincular el último, la card desaparece sola (sin tocar Actualizar).
- [ ] "Vincular"/"Cambiar" abre el panel del agente de ESA fila sin escribir nada; con < 2
      caracteres no hay galería; con ≥ 2 filtra por nombre o correo (StartsWith) con texto visible.
- [ ] Tocar un usuario en `galUsrAge` → Notify "✓ … vinculado", el panel se cierra, chip pasa a "✓ Vinculado" y
      `lblEmailAge` muestra el correo en minúsculas (verificá en Dataverse que se escribieron
      `'[AG] Usuario'` **y** `'Email Usuario'`).
- [ ] "Cancelar" / scrim / ✕ cierran el panel SIN escribir. Si el Patch falla, Notify rojo y el panel sigue abierto.
- [ ] "Desvincular" → "Vínculo quitado", chip "Sin usuario", ambas columnas Blank.
- [ ] Con ese vínculo hecho, el usuario entra a la app del localizador (login por
      `'Email Usuario'`) — la prueba de humo cruzada de las dos apps.
- [ ] Desactivar/Activar alterna chip y texto del botón; el registro cambia en Dataverse.
- [ ] "↻ Actualizar" re-colecta y refleja cambios hechos por otro Admin.
- [ ] La búsqueda de `galUsrAge` no tiene warning de delegación;
      cero subrayados azules en toda la pantalla.
- [ ] En ventana angosta (`nfMovil`): filas apiladas de 148, vínculo + botón en el 2.º renglón, panel a ancho completo, nada
      recortado; nav colapsa a hamburguesa.
