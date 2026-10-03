# Guía 11 — scrAgentes (Agentes y usuarios · Admin)

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrAgentes.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol (LED-20).
> - Alerta de agentes sin usuario: `CountIf('CM | TBL | Agentes', '[AG] Activo' && 'Email Usuario' = Blank())` (no `IsBlank('[AG] Usuario')`, lookup no delega). ⚠ `lblAlertaDetalleAge` aún lista desde `colAgentesAge` con `IsBlank('[AG] Usuario')` — puede no cuadrar con el conteo.
> - `cmbUsuarioAge`: `DefaultSelectedItems = Filter(Users, 'Primary Email' = ThisItem.'Email Usuario')` y el vínculo actual se MUESTRA en `InputTextPlaceholder` (01 §11.1 #9); `DisplayFields/SearchFields` lógicos.
> - `Parent.TemplateHeight` en vez de `Parent.TemplateSize` en los Y del template.
> - `AccessibleLabel` en galería y combo, foco visible; `ShowScrollbar = false`. Sin overlay (filas no clicables).

> **Por qué esta pantalla importa (leelo antes de construir):** la app del localizador identifica
> al agente que entra por **`'Email Usuario'`** (`LookUp` contra `User().Email`) y el Cloud Flow
> **Comp-14** asigna el *owner* de cada asignación por **`'[AG] Usuario'`**. Un agente activo SIN
> usuario vinculado = **no entra a la app del celular** Y **sus asignaciones no se aíslan** (quedan
> visibles para otros). La vista "sin usuario" de esta pantalla debe estar **VACÍA** antes de operar.

**Propósito:** el Admin liga cada localizador (`'CM | TBL | Agentes'`) a su usuario de Entra
(`Users`) y lo activa/desactiva. Filas **NO clicables** — toda la acción vive en el combo y el
botón de cada fila (igual que el mockup: `row static`).

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
│   │   │   ├── cmbUsuarioAge       Cuadro combinado (usuario de Entra)
│   │   │   ├── chpVinculoAge       Botón clásico — chip receta 7.2
│   │   │   ├── chpEstadoAge        Botón clásico — chip receta 7.2
│   │   │   ├── btnToggleAge        Botón clásico "Desactivar"/"Activar"
│   │   │   └── recSepAge           Rectángulo — separador receta 7.11
│   │   └── cmpVacioAge             cmpVacio (receta 7.12)
│   └── lblAdvertenciaAge           Etiqueta clásica — advertencia bajo la galería
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
| `cmbUsuarioAge` (Cuadro combinado) | X/Y/W/H | `If(nfMovil, gblE.Pad, Parent.TemplateWidth*32%)` / `If(nfMovil, 54, (Parent.TemplateSize - gblE.AltoInput)/2)` / `If(nfMovil, Parent.TemplateWidth - 2*gblE.Pad, Parent.TemplateWidth*30%)` / `gblE.AltoInput` |
| | resto | §4.1 |
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

### 4.1 cmbUsuarioAge — el vínculo con Entra (el corazón de la pantalla)

| Propiedad | Valor |
|---|---|
| Items | `Users` |
| Campos (panel Propiedades → Editar) | primario `'Full Name'`, secundario `'Primary Email'` (diseño "Doble") |
| SearchFields | `["fullname", "internalemailaddress"]` *(lógicos de 'Full Name' y 'Primary Email'; si el panel los escribe distinto al tildearlos, aceptá lo que genere Studio)* |
| SelectMultiple | `false` |
| IsSearchable | `true` — el combo delega la búsqueda como `StartsWith` (solo StartsWith: buscar "García" encuentra "García, Ana" pero no "Ana García") |
| InputTextPlaceholder | `"— Sin usuario —"` |
| DefaultSelectedItems | `If(!IsBlank(ThisItem.'[AG] Usuario'), Table(ThisItem.'[AG] Usuario'))` |
| OnChange | fórmula abajo |

> ⚠ **VERIFICÁ `DefaultSelectedItems` con IntelliSense**: la forma `Table(<registro lookup>)` es
> la esperada; si tu versión de Studio la subraya, usá la equivalente
> `Filter(Users, 'Primary Email' = ThisItem.'Email Usuario')` y anotá cuál quedó.

**OnChange (pegar completo):**

```powerfx
If(!IsBlank(Self.Selected),
    Patch('CM | TBL | Agentes',
        LookUp('CM | TBL | Agentes', '[AG] Nombre' = ThisItem.'[AG] Nombre'),
        {
            '[AG] Usuario': Self.Selected,
            'Email Usuario': Lower(Self.Selected.'Primary Email')
        }
    );
    Notify("✓ " & ThisItem.'[AG] Nombre' & " vinculado", NotificationType.Success, 2500),

    Patch('CM | TBL | Agentes',
        LookUp('CM | TBL | Agentes', '[AG] Nombre' = ThisItem.'[AG] Nombre'),
        {
            '[AG] Usuario': Blank(),
            'Email Usuario': Blank()
        }
    );
    Notify("Vínculo quitado", NotificationType.Warning)
);
ClearCollect(colAgentesAge, Sort('CM | TBL | Agentes', '[AG] Nombre'))
```

> ⚠ **Se escriben las DOS columnas, siempre juntas**: `'[AG] Usuario'` (lookup — lo usa Comp-14
> para asignar el owner) y `'Email Usuario'` (texto en minúsculas — lo usa la canvas del
> localizador para el login). Patchear una sola deja al agente en un estado inconsistente:
> entra a la app pero sus asignaciones no se aíslan, o al revés. Si tocás esta fórmula,
> mantené las dos.
>
> El `LookUp` por `'[AG] Nombre'` delega (igualdad sobre la columna primaria, que el import
> mantiene única). El `ClearCollect` final refresca chips y alerta; borrar el texto del combo
> y salir dispara la rama "quitar vínculo" (OnChange solo corre por interacción del usuario,
> no en la carga).

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

- **Mockup → canvas**: el `<select>` de `ligar()` se implementa como `Cuadro combinado` con
  búsqueda (mismo contrato funcional: opción vacía = quitar vínculo). Filas `row static`,
  chips y textos del mockup (`scrAgentes()`, `toggleAg()`) se conservan 1:1. Sin entradas LED
  nuevas. Toasts → `Notify` (LED-05).
- El combo dentro del template se instancia por fila: con la nómina de localizadores de la
  empresa (decenas) el costo es irrelevante; `Users` solo se consulta al abrir/buscar.
- Si IntelliSense difiere de un display name (`'[AG] Usuario'`, `'Email Usuario'`,
  `'Primary Email'`…), **el IntelliSense manda** — anotá y avisá (guía 01, encabezado).

## Checklist de salida

- [ ] Entrar con rol ≠ Admin rebota a `scrInicio` sin parpadeo de datos.
- [ ] Con ≥1 agente activo sin usuario: la card warn muestra el conteo y los nombres; al
      vincular el último, la card desaparece sola (sin tocar Actualizar).
- [ ] Elegir un usuario en el combo → Notify "✓ … vinculado", chip pasa a "✓ Vinculado" y
      `lblEmailAge` muestra el correo en minúsculas (verificá en Dataverse que se escribieron
      `'[AG] Usuario'` **y** `'Email Usuario'`).
- [ ] Borrar la selección del combo → "Vínculo quitado", chip "Sin usuario", ambas columnas Blank.
- [ ] Con ese vínculo hecho, el usuario entra a la app del localizador (login por
      `'Email Usuario'`) — la prueba de humo cruzada de las dos apps.
- [ ] Desactivar/Activar alterna chip y texto del botón; el registro cambia en Dataverse.
- [ ] "↻ Actualizar" re-colecta y refleja cambios hechos por otro Admin.
- [ ] Buscar en el combo por nombre o correo filtra (StartsWith) sin warning de delegación;
      cero subrayados azules en toda la pantalla.
- [ ] En ventana angosta (`nfMovil`): filas apiladas de 148, combo a ancho completo, nada
      recortado; nav colapsa a hamburguesa.
