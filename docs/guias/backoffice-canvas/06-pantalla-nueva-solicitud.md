# Guía 06 — scrNuevaSolicitud

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrNuevaSolicitud.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol en `OnVisible` (LED-20).
> - `cmbCuentaNva.DisplayFields` con nombres LÓGICOS (`["sanic_cm_ct_numerodecuenta","lineaNva"]`); sin `IsSearchable` (01 §11.1 #10).
> - `AccessibleLabel` en combos/inputs y `FocusedBorderThickness 2` en todo lo interactivo (LED-19).
> - `cmpEncabezadoNva`: sin `Y`, `Height = If(nfMovil, gblE.AltoBarra + 84, 84)` (receta 7.5). Patch, anti-duplicado y `Moneda()` iguales a la guía.

**Rol:** Solicitante. El Solicitante pide una asignación **Especial** sobre una cuenta existente:
elige cuenta, tipo de especial, montos de retiro y motivo. La solicitud nace `Pendiente aprobacion`
y **sin localizador** (lo asigna el Supervisor en scrAprobaciones). Mockup: función `scrNueva()`
(+ `nsPreview()`, `nsValida()`, `nsCrear()`) de `docs/mockups/backoffice-canvas.html`.

**Contrato de entrada** (todo lo setea App.OnStart / App.Formulas — guía 01 §3–4):
`gblRol` (guard), `gblUsuarioDV` (autor del Patch), `gblC`/`gblE` (tokens), `nfMovil`,
`gblNavMovilAbierta` (nav móvil). Esta pantalla **no llena colecciones**: la búsqueda de cuenta es
delegable directo al datasource (receta 7.9) y el resto es formulario puro.
Datasources usados: `'CM | TBL | Cuentas'` · `'CM | TBL | Asignacions'`.

> **NOTA BACKEND (bloqueante — 00 §3.3):** si el Patch de §5 falla con "campo requerido"
> sobre `'[AS] Agente'`, es que `sanic_cm_as_agenteid` sigue como *Application required*.
> Bajarlo a **Opcional** en el portal de Dev ANTES de probar esta pantalla: la solicitud nace
> sin localizador **a propósito** — el Supervisor lo asigna al aprobar. Es 1 minuto en el portal.

## Árbol de controles

```
scrNuevaSolicitud                     ESTILO-Pantalla
├── cmpEncabezadoNva                  cmpEncabezado (receta 7.5)
├── conFormNva                        Contenedor · ESTILO-Card (la card central del form)
│   ├── lblCuentaTitNva               "Cuenta *"
│   ├── cmbCuentaNva                  Cuadro combinado (búsqueda delegable, 2 líneas)
│   ├── conPreviewNva                 Contenedor · card gris (saldo/mora de la cuenta elegida)
│   │   ├── lblPrevSaldoNva
│   │   └── lblPrevMoraNva
│   ├── lblTipoTitNva                 "Tipo de especial *"
│   ├── cmbTipoNva                    Cuadro combinado (lista fija)
│   ├── lblRetiroNioTitNva            "Retiro C$ (NIO)"
│   ├── txtRetiroNioNva               Entrada de texto clásica
│   ├── lblRetiroUsdTitNva            "Retiro US$ (USD)"
│   ├── txtRetiroUsdNva               Entrada de texto clásica
│   ├── lblMonedaHintNva              hint fijo de monedas
│   ├── lblMotivoTitNva               "Motivo *"
│   ├── txtMotivoNva                  Entrada de texto clásica (multilínea)
│   ├── lblHintNva                    hint dinámico "Falta: …" / "✓ Todo listo"
│   └── btnEnviarNva                  Botón clásico · ESTILO-BotonPri
└── cmpNavNva                         cmpNavLateral (AL FINAL del árbol — §8.1 de la guía 01)
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | fórmula ↓ (guard de rol, primera línea SIEMPRE) |

```powerfx
If(!("Solicitante" in gblRol), Navigate(scrInicio));
```

Nada más en OnVisible: sin colecciones y sin Reset — si el Solicitante sale a mitad del form y
vuelve, el borrador se conserva (decisión deliberada; el Reset ocurre solo tras enviar, §5).

## 2. Encabezado y navegación

| Control | Propiedad | Valor |
|---|---|---|
| `cmpEncabezadoNva` (cmpEncabezado) | X / Y | `If(nfMovil, 0, gblE.AnchoNav)` / `If(nfMovil, gblE.AltoBarra, 0)` |
| | Width / Height | `Parent.Width - Self.X` / `84` (receta 7.5) |
| | Titulo | `"Nueva solicitud de Especial"` |
| | Subtitulo | `"Elegí una cuenta existente y definí el retiro. Si el cliente no existe, pedile al Admin que lo cree."` |
| `cmpNavNva` (cmpNavLateral) | RutaActiva | `"nva"` |
| | X / Y / Width / Height | `0` / `0` / `gblE.AnchoNav` / `Parent.Height` |
| | Visible | `!nfMovil \|\| gblNavMovilAbierta` |

> El subtítulo es gobernanza, no decoración: el Solicitante **NO crea maestros** (00 §1 y §3.4) —
> si el cliente/cuenta no existe, lo crea el Admin en scrMaestros.
> `cmpNavNva` va al FINAL del árbol para flotar encima del contenido en móvil (guía 01 §8.1).

## 3. Card del formulario — `conFormNva`

`Contenedor` (manual) con ESTILO-Card. Todos los hijos: `X = gblE.Pad` ·
`Width = Parent.Width - 2*gblE.Pad`, salvo el par NIO/USD (§4) y `conPreviewNva` interno.

| Propiedad | Valor |
|---|---|
| Width | `Min(640, Parent.Width - 2*gblE.Pad)` |
| X | `If(nfMovil, (Parent.Width - Self.Width)/2, gblE.AnchoNav + (Parent.Width - gblE.AnchoNav - Self.Width)/2)` |
| Y | `cmpEncabezadoNva.Y + cmpEncabezadoNva.Height + gblE.PadSm` |
| Height | `btnEnviarNva.Y + btnEnviarNva.Height + gblE.Pad` |

**Etiquetas de campo** (`lblCuentaTitNva` · `lblTipoTitNva` · `lblRetiroNioTitNva` ·
`lblRetiroUsdTitNva` · `lblMotivoTitNva`) — fila compartida, mismas propiedades para las cinco:
`Etiqueta clásica` · `Size = 9` · `FontWeight = FontWeight.Semibold` · `Color = gblC.ink2` ·
`Height = 18` · Text el del árbol (el `*` de requerido va EN el texto). X/Width del párrafo
inicial; las de NIO/USD usan X/Width del par (§4). Y de cada una, en §3.1–§4.

### 3.1 Cuenta — `cmbCuentaNva` (Cuadro combinado)

| Propiedad | Valor |
|---|---|
| (lblCuentaTitNva) Y | `gblE.Pad` |
| Y / Height | `lblCuentaTitNva.Y + lblCuentaTitNva.Height + 4` / `48` (layout de dos líneas) |
| Items | fórmula ↓ |
| SelectMultiple / IsSearchable | `false` / `true` |
| InputTextPlaceholder | `"— Buscar cuenta —"` |
| Campos (panel Propiedades → Editar) | **Primario** `'[CT] Numero de cuenta'` · **Secundario** `lineaNva` *(columna horneada en Items: `cliente · producto` — el layout "Doble" del combo solo admite 2 columnas, por eso el producto viaja concatenado en la línea secundaria; R1-08)* |
| Fill / BorderColor | `gblC.card` / `gblC.borde` (lo que el combo permita de ESTILO-Input) |

```powerfx
AddColumns(
    Filter('CM | TBL | Cuentas',
        StartsWith('[CT] Numero de cuenta', cmbCuentaNva.SearchText)
        || StartsWith('Nombre Cliente', cmbCuentaNva.SearchText)
    ),
    lineaNva, 'Nombre Cliente' & " · " & 'Tipo Producto'
)
```

> `StartsWith` es delegable en Dataverse (receta 7.9) — el combo busca contra ~7 000 cuentas sin
> warning azul; el `AddColumns` corre EN MEMORIA sobre la página ya traída (no rompe la
> delegación del `Filter` interno). Si tu versión de Studio expone **SearchFields** en vez del
> filtro manual sobre `Self.SearchText`, configurá esas MISMAS 2 columnas
> (`'[CT] Numero de cuenta'`, `'Nombre Cliente'`) y dejá
> `Items = AddColumns('CM | TBL | Cuentas', lineaNva, 'Nombre Cliente' & " · " & 'Tipo Producto')`.
> Cualquiera de las dos variantes cumple. Si IntelliSense marca `'Tipo Producto'` como choice,
> envolvelo en `Text('Tipo Producto')` y anotalo.

### 3.2 Preview de la cuenta — `conPreviewNva`

`Contenedor` (manual), la "card gris" del mockup (`nsPreview()`): sin borde, fondo pantalla.

| Control | Propiedad | Valor |
|---|---|---|
| `conPreviewNva` | X / Width | `gblE.Pad` / `Parent.Width - 2*gblE.Pad` |
| | Y / Height | `cmbCuentaNva.Y + cmbCuentaNva.Height + gblE.PadSm` / `62` |
| | Fill / BorderThickness | `gblC.pantalla` / `0` |
| | RadiusTopLeft/TopRight/BottomLeft/BottomRight | `gblE.RadioSm` |
| | Visible | `!IsBlank(cmbCuentaNva.Selected)` |
| `lblPrevSaldoNva` (Etiqueta clásica) | X / Y / Width / Height | `gblE.PadSm` / `gblE.PadSm` / `Parent.Width - 2*gblE.PadSm` / `20` |
| | Text | `"Saldo vencido: " & Moneda(cmbCuentaNva.Selected.'[CT] Saldo Vencido NIO', cmbCuentaNva.Selected.'[CT] Saldo Vencido USD')` |
| | FontWeight | `FontWeight.Semibold` |
| `lblPrevMoraNva` (Etiqueta clásica) | X / Y / Width / Height | `gblE.PadSm` / `32` / `Parent.Width - 2*gblE.PadSm` / `20` |
| | Text | `"Mora: " & cmbCuentaNva.Selected.'[CT] Dias Mora' & " días · nivel " & cmbCuentaNva.Selected.'[CT] Nivel Mora'` |
| | Color / Size | `gblC.ink2` / `9` |

Los controles siguientes se corren solos cuando el preview aparece: su Y arranca de
`conPreviewNva.Y + If(conPreviewNva.Visible, conPreviewNva.Height + gblE.PadSm, 0)` (§3.3).

### 3.3 Tipo de especial — `cmbTipoNva` (Cuadro combinado)

| Propiedad | Valor |
|---|---|
| (lblTipoTitNva) Y | `conPreviewNva.Y + If(conPreviewNva.Visible, conPreviewNva.Height + gblE.PadSm, 0)` |
| Y / Height | `lblTipoTitNva.Y + lblTipoTitNva.Height + 4` / `gblE.AltoInput` |
| Items | `["Retiro urgente", "Cobro especial", "Verificación"]` |
| DefaultSelectedItems | `["Retiro urgente"]` |
| SelectMultiple / IsSearchable | `false` / `false` |
| Fill / BorderColor | `gblC.card` / `gblC.borde` |

> **LED-11:** el "tipo de especial" NO es columna de `asignacion` — se antepone al texto de
> `'Notas'` en el Patch (§5): `"Retiro urgente — <motivo>"`. Columna choice propia = backlog.

## 4. Montos y motivo

Par NIO/USD lado a lado en escritorio, apilado en móvil. Ancho de cada mitad:
`If(nfMovil, Parent.Width - 2*gblE.Pad, (Parent.Width - 2*gblE.Pad - gblE.PadSm)/2)` —
llamalo "AnchoMitad" al leer la tabla (escribilo completo en cada propiedad).

| Control | Propiedad | Valor |
|---|---|---|
| `lblRetiroNioTitNva` | X / Y / Width | `gblE.Pad` / `cmbTipoNva.Y + cmbTipoNva.Height + gblE.PadSm` / AnchoMitad |
| `txtRetiroNioNva` (Entrada de texto clásica) | X / Y / Width / Height | `gblE.Pad` / `lblRetiroNioTitNva.Y + lblRetiroNioTitNva.Height + 4` / AnchoMitad / `gblE.AltoInput` |
| | Estilo / Format / HintText | ESTILO-Input / `TextFormat.Number` / `"0.00"` |
| | Default | `""` |
| `lblRetiroUsdTitNva` | X | `If(nfMovil, gblE.Pad, gblE.Pad + txtRetiroNioNva.Width + gblE.PadSm)` |
| | Y | `If(nfMovil, txtRetiroNioNva.Y + txtRetiroNioNva.Height + gblE.PadSm, lblRetiroNioTitNva.Y)` |
| | Width | AnchoMitad |
| `txtRetiroUsdNva` (Entrada de texto clásica) | X / Y / Width / Height | `lblRetiroUsdTitNva.X` / `lblRetiroUsdTitNva.Y + lblRetiroUsdTitNva.Height + 4` / AnchoMitad / `gblE.AltoInput` |
| | Estilo / Format / HintText / Default | ESTILO-Input / `TextFormat.Number` / `"0.00"` / `""` |
| `lblMonedaHintNva` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `txtRetiroUsdNva.Y + txtRetiroUsdNva.Height + 6` / `Parent.Width - 2*gblE.Pad` / `16` |
| | Text | `"Al menos una moneda con monto mayor a cero."` |
| | Size / Color | `8` / `gblC.muted` |
| `lblMotivoTitNva` | Y | `lblMonedaHintNva.Y + lblMonedaHintNva.Height + gblE.PadSm` |
| `txtMotivoNva` (Entrada de texto clásica) | X / Y / Width / Height | `gblE.Pad` / `lblMotivoTitNva.Y + lblMotivoTitNva.Height + 4` / `Parent.Width - 2*gblE.Pad` / `84` |
| | Estilo / Mode | ESTILO-Input / `TextMode.MultiLine` |
| | HintText | `"¿Por qué se necesita esta gestión especial?"` |
| | Default | `""` |

## 5. Validación y envío

### 5.1 `lblHintNva` — el hint dinámico de `nsValida()`

| Propiedad | Valor |
|---|---|
| X / Y / Width / Height | `gblE.Pad` / `txtMotivoNva.Y + txtMotivoNva.Height + gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `18` |
| Size / FontWeight | `9` / `FontWeight.Semibold` |
| Text | fórmula ↓ |
| Color | `If(StartsWith(Self.Text, "Falta"), gblC.pendInk, gblC.okInk)` |

```powerfx
With({_falta:
    Concat(
        Filter(
            Table(
                {t: If(IsBlank(cmbCuentaNva.Selected), "cuenta")},
                {t: If(!(Value(txtRetiroNioNva.Text) > 0 || Value(txtRetiroUsdNva.Text) > 0), "monto")},
                {t: If(IsBlank(Trim(txtMotivoNva.Text)), "motivo")}
            ),
            !IsBlank(t)
        ),
        t, " · "
    )},
    If(IsBlank(_falta), "✓ Todo listo", "Falta: " & _falta)
)
```

### 5.2 `btnEnviarNva` (Botón clásico, ESTILO-BotonPri)

| Propiedad | Valor |
|---|---|
| X / Y / Width / Height | `gblE.Pad` / `lblHintNva.Y + lblHintNva.Height + gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `gblE.AltoBoton` |
| Text | `"✨ Enviar solicitud"` |
| DisplayMode | `If(!IsBlank(cmbCuentaNva.Selected) && (Value(txtRetiroNioNva.Text) > 0 || Value(txtRetiroUsdNva.Text) > 0) && !IsBlank(Trim(txtMotivoNva.Text)), DisplayMode.Edit, DisplayMode.Disabled)` |
| OnSelect | fórmula ↓ (pegar COMPLETA) |

```powerfx
If(
    !IsBlank(LookUp('CM | TBL | Asignacions',
        '[AS] NumeroCuenta' = cmbCuentaNva.Selected.'[CT] Numero de cuenta'
        && '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
        && '[AS] Estado' = true)),

    Notify("Ya existe una Especial ACTIVA para esa cuenta. Revisá Mis solicitudes o esperá a que el Admin la cierre.",
        NotificationType.Warning),

    Patch('CM | TBL | Asignacions', Defaults('CM | TBL | Asignacions'),
        {
            '[AS] Cuenta': cmbCuentaNva.Selected,
            '[AS] NumeroCuenta': cmbCuentaNva.Selected.'[CT] Numero de cuenta',
            '[AS] Tipo': 'Choice | CM | Tipo asignacion'.Especial,
            '[AS] Estado': true,
            '[AS] Estado de aprobacion': 'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion',
            'Retiro NIO': Value(txtRetiroNioNva.Text),
            'Retiro USD': Value(txtRetiroUsdNva.Text),
            'Notas': cmbTipoNva.Selected.Value & " — " & Trim(txtMotivoNva.Text),
            '[AS] Solicitado por': gblUsuarioDV,
            '[AS] Fecha de Asignacion': Now()
        }
    );
    Notify("✨ Solicitud enviada — quedó Pendiente de aprobación", NotificationType.Success, 2500);
    Reset(cmbCuentaNva);
    Reset(cmbTipoNva);
    Reset(txtRetiroNioNva);
    Reset(txtRetiroUsdNva);
    Reset(txtMotivoNva);
    Navigate(scrMisSolicitudes)
)
```

> **Anti-duplicado:** el `LookUp` es delegable (igualdades + `&&` sobre texto/choice/booleano —
> 00 §4.2). La alt key `cuenta+tipo` de `asignacion` haría fallar el Patch igual; este check
> existe para dar el error AMABLE antes del error duro de Dataverse (que igual atraparía
> App.OnError). `'[AS] Estado' = true` acota a la Especial ACTIVA: una Especial cerrada no
> bloquea pedir otra.
> **`'Notas'`:** el prefijo `"<tipo> — "` es LED-11 (el tipo no tiene columna propia).
> **Sin `'[AS] Agente'` en el Patch:** deliberado — ver NOTA BACKEND del encabezado (00 §3.3).

## 6. Checklist de salida

- [ ] Entrar con rol ≠ Solicitante rebota a scrInicio (guard del OnVisible).
- [ ] Escribir en el combo de cuenta filtra por Nº de cuenta Y por nombre de cliente,
      **sin subrayado azul de delegación**; cada opción muestra el número arriba y
      "cliente · producto" en la línea secundaria (R1-08).
- [ ] Elegir cuenta muestra `conPreviewNva` con saldo bimoneda (`Moneda()`) y mora; el resto
      del form se desplaza hacia abajo sin encimarse. Deseleccionar lo oculta y el form sube.
- [ ] `lblHintNva` arranca en "Falta: cuenta · monto · motivo" (ámbar) y va quitando piezas al
      completar; con todo lleno pasa a "✓ Todo listo" (verde) y el botón se habilita.
- [ ] Solo NIO **o** solo USD con monto > 0 alcanza para habilitar (no exige ambas).
- [ ] Motivo con solo espacios NO habilita el botón (`Trim`).
- [ ] Enviar sobre cuenta con Especial activa → Notify ámbar y NO crea registro.
- [ ] Enviar válido → crea la fila (verificar en la tabla: Tipo Especial, Estado activo,
      `Pendiente aprobacion`, Notas con prefijo de tipo, sin Agente), notifica éxito, limpia los
      5 controles y navega a scrMisSolicitudes donde aparece la solicitud.
- [ ] En ventana angosta (< 920): NIO/USD apilados, card a ancho completo menos padding,
      hamburguesa abre `cmpNavNva`.
