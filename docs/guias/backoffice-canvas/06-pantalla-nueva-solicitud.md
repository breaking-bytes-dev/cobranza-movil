# Guía 06 — scrNuevaSolicitud

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrNuevaSolicitud.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol en `OnVisible` (LED-20).
> - **B-04: el selector de cuenta YA NO es un Cuadro combinado.** El ComboBox clásico no pinta registros de Dataverse en esta app (opciones con texto vacío, también en Studio Preview). Se reemplazó por **buscador (`txtBuscarCtaNva`) + galería de resultados (`galCtaNva`) + tarjeta de la cuenta elegida (`conPreviewNva`, variable `varCtaNva`)** — ver §3.1–§3.2 y 01 §11.
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
│   ├── txtBuscarCtaNva               Entrada de texto clásica (buscador, DelayOutput) — visible sin cuenta elegida
│   ├── icoBuscarCtaNva               Ícono lupa (dentro del buscador)
│   ├── galCtaNva                     Galería vertical (resultados, 2 líneas, alto acotado)
│   │   ├── lblNumFilaCtaNva          número de cuenta
│   │   ├── lblCliFilaCtaNva          nombre del cliente
│   │   ├── lblSaldoFilaCtaNva        saldo vencido (derecha)
│   │   ├── lblMoraFilaCtaNva         mora (derecha)
│   │   ├── recSepCtaNva              separador
│   │   └── btnFilaCtaNva             overlay de fila (un tab stop por fila)
│   ├── lblSinCtaNva                  "Ninguna cuenta ni cliente empieza con «…»"
│   ├── conPreviewNva                 Contenedor · tarjeta de la cuenta ELEGIDA (reemplaza al buscador)
│   │   ├── lblCtaSelNva              número · cliente
│   │   ├── btnCambiarCtaNva          "Cambiar" (limpia varCtaNva y el buscador)
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
vuelve, el borrador se conserva **incluida la cuenta elegida (`varCtaNva`)** (decisión deliberada;
la limpieza — `Set(varCtaNva, Blank())` + `Reset` del buscador y de los inputs — ocurre solo tras
enviar con éxito, §5; si el Patch falla no se limpia nada).

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

### 3.1 Cuenta — buscador `txtBuscarCtaNva` + galería `galCtaNva`

> **B-04 (causa real):** el `Classic/ComboBox@2.4.0` de esta app **no pinta registros de
> Dataverse** — renderiza 15 `<li role=option>` con el `<span>` de texto vacío (Cuentas, Agentes y
> hasta Users), también en Studio Preview. Con tablas estáticas (`["…"]`, `DisplayFields ["Value"]`)
> sí funciona. El parche intermedio (registros planos con `ForAll(Filter(…))`) tampoco sirvió y además
> **no delega** (bajaba la tabla entera). Patrón definitivo: buscador + galería + variable.

| Control | Propiedad | Valor |
|---|---|---|
| (lblCuentaTitNva) | Y | `gblE.Pad` |
| `txtBuscarCtaNva` (Entrada de texto clásica) | X / Y / Width / Height | `gblE.Pad` / `lblCuentaTitNva.Y + lblCuentaTitNva.Height + 4` / `Parent.Width - 2*gblE.Pad` / `gblE.AltoInput` |
| | Estilo / PaddingLeft / Default | ESTILO-Input / `34` (lugar de la lupa) / `""` |
| | DelayOutput | `true` (la consulta sale al terminar de tipear, B-13) |
| | HintText | `"Buscar por número de cuenta o cliente (mín. 2)"` |
| | Visible | `IsBlank(varCtaNva)` |
| `icoBuscarCtaNva` (Ícono) | Icon / X / Y | `Icon.Search` / `txtBuscarCtaNva.X + 8` / `txtBuscarCtaNva.Y + (txtBuscarCtaNva.Height - Self.Height)/2` · `20×20` · Visible `txtBuscarCtaNva.Visible` |
| `galCtaNva` (Galería vertical) | X / Y / Width | `gblE.Pad` / `txtBuscarCtaNva.Y + txtBuscarCtaNva.Height + 4` / `Parent.Width - 2*gblE.Pad` |
| | TemplateSize / TemplatePadding | `52` / `0` |
| | Height | `Min(Max(Self.AllItemsCount, 1), 4) * Self.TemplateHeight` (alto acotado: hasta 4 filas) |
| | Fill / BorderColor / BorderThickness | `gblC.card` / `gblC.borde` / `1` |
| | Visible | `IsBlank(varCtaNva) && Len(Trim(txtBuscarCtaNva.Text)) >= 2` |
| | Items | fórmula ↓ |
| | OnSelect | `Set(varCtaNva, ThisItem)` |
| `lblNumFilaCtaNva` / `lblCliFilaCtaNva` | Text | `ThisItem.'[CT] Numero de cuenta'` (Semibold, 10) / `ThisItem.'Nombre Cliente'` (muted, 9) |
| `lblSaldoFilaCtaNva` / `lblMoraFilaCtaNva` | Text | `Moneda(ThisItem.'[CT] Saldo Vencido NIO', ThisItem.'[CT] Saldo Vencido USD')` / `"Mora " & ThisItem.'[CT] Dias Mora' & " d · nivel " & ThisItem.'[CT] Nivel Mora'` (alineadas a la derecha, `Width = Min(170, Parent.TemplateWidth * 40%)`) |
| `btnFilaCtaNva` (Botón clásico, overlay) | Fill / Color | `Color.Transparent` (patrón de fila de la app: `OnSelect = Select(Parent)`, `TabIndex 0`, `Height = Parent.TemplateHeight`) |
| | Text | `"Elegir cuenta " & ThisItem.'[CT] Numero de cuenta' & " · " & ThisItem.'Nombre Cliente'` |
| `lblSinCtaNva` (Etiqueta clásica) | Visible | `galCtaNva.Visible && galCtaNva.AllItemsCount = 0` (encima de la galería, mismo X/Y/Width/Height) |

```powerfx
// galCtaNva.Items — con menos de 2 caracteres, centinela "§§": no se consulta la tabla entera
// (B-19) y SIEMPRE hay tabla (nada de If sin rama falsa → "expected a table", B-06).
With({t: If(Len(Trim(txtBuscarCtaNva.Text)) < 2, "§§", Trim(txtBuscarCtaNva.Text))},
    Filter('CM | TBL | Cuentas',
        StartsWith('[CT] Numero de cuenta', t) || StartsWith('Nombre Cliente', t))
)
```

> `StartsWith` delega en Dataverse (receta 7.9) y `t` es constante por consulta. **Nada de
> `ForAll`/`AddColumns`/colecciones** sobre Dataverse para esta lista.
> **Por qué saldo y mora se pintan en la fila:** la selección explícita de columnas trae solo lo que
> los controles usan; la doc oficial advierte que el linaje de columnas puede perderse al pasar por
> variables. Al usarlas en la fila, viajan en ESTA consulta y `varCtaNva` las tiene completas.

### 3.2 Cuenta elegida — tarjeta `conPreviewNva`

`Contenedor` (manual) en el MISMO lugar que el buscador (lo reemplaza mientras hay cuenta elegida).

| Control | Propiedad | Valor |
|---|---|---|
| `conPreviewNva` | X / Y / Width | `gblE.Pad` / `txtBuscarCtaNva.Y` / `Parent.Width - 2*gblE.Pad` |
| | Height | `lblPrevMoraNva.Y + lblPrevMoraNva.Height + gblE.PadSm` |
| | Fill / BorderColor / BorderThickness | `gblC.tealSuave` / `gblC.teal` / `1` · Radius* `gblE.RadioSm` |
| | Visible | `!IsBlank(varCtaNva)` |
| `lblCtaSelNva` (Etiqueta clásica) | X / Y / Height | `gblE.PadSm` / `gblE.PadSm` / `22` · Bold 10 |
| | Width | `Parent.Width - 3*gblE.PadSm - btnCambiarCtaNva.Width` |
| | Text | `varCtaNva.'[CT] Numero de cuenta' & "  ·  " & varCtaNva.'Nombre Cliente'` |
| `btnCambiarCtaNva` (Botón clásico, secundario) | X / Y / Width / Height | `Parent.Width - Self.Width - gblE.PadSm` / `gblE.PadSm` / `92` / `28` |
| | OnSelect | `Set(varCtaNva, Blank()); Reset(txtBuscarCtaNva)` |
| `lblPrevSaldoNva` | Y / Text | `lblCtaSelNva.Y + lblCtaSelNva.Height + 4` / `"Saldo vencido: " & Moneda(varCtaNva.'[CT] Saldo Vencido NIO', varCtaNva.'[CT] Saldo Vencido USD')` |
| `lblPrevMoraNva` | Y / Text | `lblPrevSaldoNva.Y + lblPrevSaldoNva.Height` / `"Mora: " & varCtaNva.'[CT] Dias Mora' & " días · nivel " & varCtaNva.'[CT] Nivel Mora'` |

Los controles siguientes se corren solos según lo visible en el bloque Cuenta (§3.3).

### 3.3 Tipo de especial — `cmbTipoNva` (Cuadro combinado)

| Propiedad | Valor |
|---|---|
| (lblTipoTitNva) Y | `If(conPreviewNva.Visible, conPreviewNva.Y + conPreviewNva.Height, galCtaNva.Visible, galCtaNva.Y + galCtaNva.Height, txtBuscarCtaNva.Y + txtBuscarCtaNva.Height) + gblE.PadSm` |
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
                {t: If(IsBlank(varCtaNva), "cuenta")},
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
| DisplayMode | `If(!IsBlank(varCtaNva) && (Value(txtRetiroNioNva.Text) > 0 || Value(txtRetiroUsdNva.Text) > 0) && !IsBlank(Trim(txtMotivoNva.Text)), DisplayMode.Edit, DisplayMode.Disabled)` |
| OnSelect | fórmula ↓ (pegar COMPLETA) |

```powerfx
If(
    !IsBlank(LookUp('CM | TBL | Asignacions',
        '[AS] NumeroCuenta' = varCtaNva.'[CT] Numero de cuenta'
        && '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
        && '[AS] Estado' = true)),

    Notify("Ya existe una Especial ACTIVA para esa cuenta. Revisá Mis solicitudes o esperá a que el Admin la cierre.",
        NotificationType.Warning),

    Patch('CM | TBL | Asignacions', Defaults('CM | TBL | Asignacions'),
        {
            '[AS] Cuenta': varCtaNva,
            '[AS] NumeroCuenta': varCtaNva.'[CT] Numero de cuenta',
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
    Set(varCtaNva, Blank());
    Reset(txtBuscarCtaNva);
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
- [ ] Con 0–1 caracteres en `txtBuscarCtaNva` la galería NO aparece (y no se trae la tabla).
- [ ] Con ≥ 2 caracteres, `galCtaNva` filtra por Nº de cuenta Y por nombre de cliente, **sin
      subrayado azul de delegación**; cada fila muestra número + cliente (izq.) y saldo + mora (der.)
      **con texto visible** (B-04). Sin coincidencias → `lblSinCtaNva`.
- [ ] Tocar una fila oculta buscador y galería y muestra la tarjeta `conPreviewNva` (número ·
      cliente, saldo bimoneda con `Moneda()` y mora); el resto del form se acomoda sin encimarse.
      "Cambiar" vuelve al buscador vacío.
- [ ] `lblHintNva` arranca en "Falta: cuenta · monto · motivo" (ámbar) y va quitando piezas al
      completar; con todo lleno pasa a "✓ Todo listo" (verde) y el botón se habilita.
- [ ] Solo NIO **o** solo USD con monto > 0 alcanza para habilitar (no exige ambas).
- [ ] Motivo con solo espacios NO habilita el botón (`Trim`).
- [ ] Enviar sobre cuenta con Especial activa → Notify ámbar y NO crea registro.
- [ ] Enviar válido → crea la fila (verificar en la tabla: Tipo Especial, Estado activo,
      `Pendiente aprobacion`, Notas con prefijo de tipo, sin Agente), notifica éxito, limpia
      `varCtaNva` + buscador + los 4 inputs y navega a scrMisSolicitudes donde aparece la solicitud.
- [ ] Si el Patch falla → Notify rojo con el detalle y el formulario queda INTACTO (tarjeta incluida).
- [ ] En ventana angosta (< 920): NIO/USD apilados, card a ancho completo menos padding,
      hamburguesa abre `cmpNavNva`.
