# Guía 07 — scrAprobaciones (Supervisor)

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrAprobaciones.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol (LED-20); sin `Concurrent` ni `colCtasAprb`.
> - Cola horneada por navegación `ThisRecord.'[AS] Cuenta'.…` (`clienteAprb`, `prodAprb`, `nioAprb`, `usdAprb`); el panel lee esas columnas en vez de `LookUp(colCtasAprb…)` (receta 7.7).
> - Overlay `btnFilaAprb` (último hijo, `TabIndex 0`, hover translúcido) reemplaza a `recFilaAprb`; etiquetas sin `OnSelect` (receta 7.3).
> - **B-04: el localizador YA NO se elige con un Cuadro combinado** (el ComboBox clásico no pinta registros de Dataverse en esta app). Se reemplazó por **buscador `txtBuscarAgeAprb` + galería `galAgeAprb` (Filter directo delegado) + tarjeta `conAgeSelAprb` con variable `varAgeAprb`** — sin `colAgentesAprb` (§3 y 01 §11). `AccessibleLabel` + foco visible en galerías, scrim, cerrar, buscador y motivo.
> - `lblFilaNotasAprb` sin `Wrap`, `VerticalAlign.Top`.

Cola FIFO de solicitudes **Especiales pendientes de aprobación**. El Supervisor abre una,
la revisa (cuenta + retiro pedido + motivo) y resuelve: **aprueba asignando localizador**
o **rechaza con motivo**. Una llamada de red por resolución; la cola vive en memoria (00 §4.1).

**Contrato de entrada**: `gblRol`, `gblUsuarioDV`, `gblC`, `gblE`, `colNav` — seteadas por
`App.OnStart` (01 §4). `nfMovil` y `Moneda()` — fórmulas con nombre (01 §3). `gblNavMovilAbierta`
(estado del drawer móvil — la setea cmpEncabezado/cmpNavLateral, 01 §8). Esta pantalla
NO recibe variables de otras pantallas; todo lo suyo nace en su OnVisible.

**Literales documentados de esta guía** (permitidos por 01 §0.6): TemplateSize `84` ·
alto de fila KV del panel `24` · ancho de columna K del panel `130` · `btnActualizarAprb`
140×36 · alturas internas de fila de galería (8/28/46) anotadas en sus tablas.

## Árbol de controles

```
scrAprobaciones                     (ESTILO-Pantalla)
├── cmpEncAprb                      instancia cmpEncabezado (receta 7.5)
├── conContenidoAprb                (Contenedor — área de la cola)
│   ├── btnActualizarAprb           «↻ Actualizar»
│   ├── galAprobAprb                (Galería vertical en blanco — recetas 7.3 + 7.11)
│   │   ├── recFilaAprb             overlay clicable (7.3, al FONDO del z-order)
│   │   ├── lblFilaClienteAprb      cliente en MAYÚSCULAS
│   │   ├── lblFilaCuentaAprb       nº de cuenta
│   │   ├── lblFilaPideAprb         "Pide: <solicitante> · dd/mm HH:mm"
│   │   ├── lblFilaNotasAprb        notas truncadas (solo desktop)
│   │   ├── lblFilaMontoAprb        Moneda(retiros)
│   │   ├── chpFilaPendAprb         chip "Pendiente" (7.2)
│   │   └── recSepAprb              separador (7.11)
│   └── cmpVacioAprb                instancia cmpVacio (7.12) — "🎉 Nada por aprobar"
├── recScrimAprb                    scrim del panel (7.4)
├── conPanelAprb                    panel lateral de detalle (7.4)
│   ├── lblPanelTituloAprb          "Solicitud <nº>"
│   ├── icoCerrarAprb
│   ├── lblSecCtaAprb               "CUENTA"
│   ├── lblCtaK1Aprb / lblCtaV1Aprb    Cliente
│   ├── lblCtaK2Aprb / lblCtaV2Aprb    Cuenta (nº · producto)
│   ├── lblCtaK3Aprb / lblCtaV3Aprb    Saldo vencido
│   ├── lblSecSolAprb               "SOLICITUD"
│   ├── lblSolK1Aprb / lblSolV1Aprb    Retiro pedido
│   ├── lblSolK2Aprb / lblSolV2Aprb    Solicitante · fecha
│   ├── conNotasAprb                card gris con las Notas completas
│   │   └── lblNotasAprb
│   ├── lblSecResAprb               "RESOLUCIÓN"
│   ├── lblLocKAprb                 label del localizador
│   ├── txtBuscarAgeAprb            Entrada de texto clásica (buscador, DelayOutput) — visible sin localizador elegido
│   ├── icoBuscarAgeAprb            Ícono lupa
│   ├── galAgeAprb                  Galería vertical (resultados: nombre + email, hasta 3 filas)
│   │   ├── lblNomFilaAgeAprb / lblMailFilaAgeAprb / recSepAgeAprb
│   │   └── btnFilaAgeAprb          overlay de fila
│   ├── lblSinAgeAprb               "Ningún localizador activo con usuario empieza con «…»"
│   ├── conAgeSelAprb               Contenedor · tarjeta del localizador ELEGIDO (reemplaza al buscador)
│   │   ├── lblAgeSelAprb           nombre · email
│   │   └── btnCambiarAgeAprb       "Cambiar"
│   ├── lblMotKAprb                 label del motivo de rechazo
│   ├── txtMotRechAprb              (Entrada de texto clásica)
│   ├── btnRechazarAprb             (ESTILO-BotonPeligro)
│   └── btnAprobarAprb              "✓ Aprobar y asignar" (ESTILO-BotonPri)
└── cmpNavAprb                      instancia cmpNavLateral (8.1 — al FINAL del árbol)
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | bloque completo ↓ |

```powerfx
// 1) Guard de rol (01 §4) — SIEMPRE primera línea
If(!("Supervisor" in gblRol), Navigate(scrInicio));
// 2) Catálogos en paralelo (no dependen entre sí)
Concurrent(
    // (Sin catálogo de agentes: el localizador se busca en galAgeAprb con Filter directo, §3 — B-04.)
    // Cuentas: SOLO las 6 columnas que usa esta pantalla
    ClearCollect(colCtasAprb, ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto',
        '[CT] Saldo Vencido NIO', '[CT] Saldo Vencido USD'))
);
// 3) La cola — DESPUÉS del Concurrent: el join hornea contra colCtasAprb (gotcha 7.7)
ClearCollect(colAprobAprb,
    AddColumns(
        Filter('CM | TBL | Asignacions',
            '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial &&
            '[AS] Estado de aprobacion' =
                'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion'),
        clienteAprb,
        Upper(Coalesce(
            LookUp(colCtasAprb,
                '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente',
            "—")),
        pideAprb, Coalesce('[AS] Solicitado por'.'Full Name', "—")
    )
);
// 4) Estado de pantalla
Set(varSelAprb, Blank());
Set(varPanelAprb, false)
```

> - El `Filter` de la cola es 100 % delegable (choice por valor + `&&`, 00 §4.2); `AddColumns`
>   y `Upper` corren en memoria sobre la colección ya traída (cota 2000, 00 §4.1).
> - `Upper()` del nombre completo: divergencia declarada **LED-01**.
> - `pideAprb` se hornea en el `AddColumns` porque el player no siempre hidrata lookups
>   coleccionados (mismo patrón que la guía 09): si `'[AS] Solicitado por'.'Full Name'`
>   viniera en blanco al coleccionar, vale el fallback `"—"` — la fila y el panel leen
>   la columna horneada, nunca navegan el lookup por fila.

## 2. Encabezado y cola

| Control | Propiedad | Valor |
|---|---|---|
| `cmpEncAprb` (cmpEncabezado) | X/Y/W/H | receta 7.5 exacta (`X = If(nfMovil, 0, gblE.AnchoNav)` · `Y = If(nfMovil, gblE.AltoBarra, 0)` · `Width = Parent.Width - Self.X` · `Height = 84`) |
| | Titulo | `"Aprobaciones pendientes"` |
| | Subtitulo | `"FIFO — asigná localizador y aprobá, o rechazá con motivo."` |
| `conContenidoAprb` (Contenedor) | X / Y | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* / `cmpEncAprb.Y + cmpEncAprb.Height` |
| | W / H | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* / `Parent.Height - Self.Y` |
| | Fill | `Color.Transparent` · sin borde |
| `btnActualizarAprb` (Botón clásico) | Estilo | ESTILO-BotonFantasma · Text `"↻ Actualizar"` · Size `9` |
| | X/Y/W/H | `Parent.Width - Self.Width - gblE.Pad` / `0` / `140` / `36` |
| | OnSelect | fórmula completa abajo — recarga catálogos y cola, y cierra el panel |
| `cmpVacioAprb` (cmpVacio) | Icono / Titulo | `"🎉"` / `"Nada por aprobar"` |
| | Mensaje | `"Las solicitudes Especiales nuevas aparecen acá apenas el Solicitante las envía."` |
| | Visible | `CountRows(colAprobAprb) = 0` (receta 7.12) |
| | X / Y | `(Parent.Width - Self.Width) / 2` / `galAprobAprb.Y + 80` |

**btnActualizarAprb.OnSelect — COMPLETO** (pasos 2→4 del OnVisible §1, sin el guard —
recarga catálogos y cola, y cierra el panel):

```powerfx
// (Sin catálogo de agentes — galAgeAprb, §3, B-04. Código vigente: también limpia varAgeAprb.)
Concurrent(
    ClearCollect(colCtasAprb, ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto',
        '[CT] Saldo Vencido NIO', '[CT] Saldo Vencido USD'))
);
ClearCollect(colAprobAprb,
    AddColumns(
        Filter('CM | TBL | Asignacions',
            '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial &&
            '[AS] Estado de aprobacion' =
                'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion'),
        clienteAprb,
        Upper(Coalesce(
            LookUp(colCtasAprb,
                '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente',
            "—")),
        pideAprb, Coalesce('[AS] Solicitado por'.'Full Name', "—")
    )
);
Set(varSelAprb, Blank());
Set(varPanelAprb, false)
```

### galAprobAprb (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X / Y | `gblE.Pad` / `btnActualizarAprb.Y + btnActualizarAprb.Height + gblE.PadSm` |
| W / H | `Parent.Width - 2*gblE.Pad` / `Parent.Height - Self.Y - gblE.Pad` |
| Items | `Sort(colAprobAprb, '[AS] Fecha de Asignacion', SortOrder.Ascending)` — **FIFO: la más vieja arriba** (orden en memoria, cero delegación) |
| TemplateSize / TemplatePadding | `84` / `0` |
| TemplateFill | `If(ThisItem.IsSelected, gblC.tealSuave, gblC.card)` (7.3) |
| OnSelect | `Set(varSelAprb, ThisItem); Set(varAgeAprb, Blank()); Reset(txtBuscarAgeAprb); Reset(txtMotRechAprb); Set(varPanelAprb, true)` |

Template — regla 7.3: **TODO hijo lleva `OnSelect = Select(Parent)`** (no se repite por fila).
El chip va **SIN** `DisplayMode.View` (excepción de la receta 7.2 para chips dentro de fila
clicable — en View se come el clic). X del template = X en pantalla − X de la galería.

| Control | Propiedad | Valor |
|---|---|---|
| `recFilaAprb` (Rectángulo, PRIMERO = fondo del z-order) | X/Y/W/H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` · Fill `Color.Transparent` (7.3) |
| `lblFilaClienteAprb` (Etiqueta clásica) | X/Y/H | `gblE.Pad` / `8` / `20` · W `If(nfMovil, Parent.TemplateWidth - 2*gblE.Pad - 170, Parent.TemplateWidth*0.32)` |
| | Text | `ThisItem.clienteAprb` · FontWeight `Semibold` |
| `lblFilaCuentaAprb` (Etiqueta clásica) | X/Y/W/H | como cliente pero Y `28` · H `16` · Size `9` · Color `gblC.muted` |
| | Text | `ThisItem.'[AS] NumeroCuenta'` |
| `lblFilaPideAprb` (Etiqueta clásica) | X/Y/W/H | como cliente pero Y `46` · H `16` · Size `8` · Color `gblC.muted2` |
| | Text | `"Pide: " & ThisItem.pideAprb & " · " & Text(ThisItem.'[AS] Fecha de Asignacion', "dd/mm HH:mm")` *(columna horneada en el OnVisible — no navegar el lookup por fila)* |
| `lblFilaNotasAprb` (Etiqueta clásica) | X/Y/H | `Parent.TemplateWidth*0.34` / `12` / `60` · W `Parent.TemplateWidth*0.36` |
| | Text / Wrap | `ThisItem.Notas` / `true` — el label corta el sobrante (truncado visual; el texto completo vive en el panel) |
| | Size / Color / Visible | `9` / `gblC.ink2` / `!nfMovil` |
| `lblFilaMontoAprb` (Etiqueta clásica) | X/Y/W/H | `Parent.TemplateWidth - 150 - gblE.Pad` / `14` / `150` / `20` · Align `Align.Right` · FontWeight `Semibold` |
| | Text | `Moneda(ThisItem.'Retiro NIO', ThisItem.'Retiro USD')` |
| `chpFilaPendAprb` (Botón clásico, receta 7.2 variante **Pendiente**) | Text | `"Pendiente"` · Fill `gblC.pendBg` · Color `gblC.pendInk` |
| | X / Y | `Parent.TemplateWidth - Self.Width - gblE.Pad` / `44` |
| `recSepAprb` (Rectángulo) | receta 7.11 | `X=0 · W=Parent.TemplateWidth · H=1 · Y = Parent.TemplateHeight - 1 · Fill = gblC.hairline` |

> - La 2ª línea del mockup era `nº · tipo`: acá va SOLO el número — el "tipo de especial"
>   no es columna, viaja antepuesto en `Notas` (**LED-11**) y se ve en la columna de notas.
> - La galería clásica auto-selecciona la 1ª fila: no importa — el panel solo abre con
>   `varPanelAprb`, que arranca en `false`.

## 3. Panel de detalle y resolución (receta 7.4, sufijo Aprb)

`recScrimAprb` y `conPanelAprb` van al final del árbol (antes de `cmpNavAprb`), con TODAS
las propiedades de la receta 7.4 y `varPanelAprb` como variable de visibilidad. Interior
(hijos de `conPanelAprb`; X `gblE.Pad`, W `Parent.Width - 2*gblE.Pad` salvo indicación):

| Control | Propiedad | Valor |
|---|---|---|
| `lblPanelTituloAprb` (Etiqueta clásica) | Y/H | `gblE.Pad` / `28` · ESTILO-TituloPagina con Size `13` (7.4) |
| | Text | `"Solicitud " & varSelAprb.'[AS] Numero'` |
| `icoCerrarAprb` (Icono, Cancel) | X/Y/W/H | `Parent.Width - 40` / `gblE.Pad` / `28` / `28` · Color `gblC.muted` |
| | OnSelect | `Set(varPanelAprb, false); Set(varAgeAprb, Blank()); Reset(txtBuscarAgeAprb)` (igual en `recScrimAprb`) |
| `lblSecCtaAprb` (Etiqueta clásica) | Y/H | `64` / `18` · ESTILO-SecHead · Text `"CUENTA"` |

Pares K/V — filas de alto `24`: K (`lblCtaKnAprb`/`lblSolKnAprb`, Etiqueta clásica, X `gblE.Pad`,
W `130`, Size `9`, Color `gblC.muted`) y V (`lblCtaVnAprb`/`lblSolVnAprb`, Etiqueta clásica,
X `gblE.Pad + 130`, W `Parent.Width - 2*gblE.Pad - 130`, Align `Align.Right`):

| Fila | Y | K (Text) | V (Text) |
|---|---|---|---|
| Cta1 | `86` | `"Cliente"` | `varSelAprb.clienteAprb` |
| Cta2 | `110` | `"Cuenta"` | `varSelAprb.'[AS] NumeroCuenta' & " · " & Coalesce(LookUp(colCtasAprb, '[CT] Numero de cuenta' = varSelAprb.'[AS] NumeroCuenta').'Tipo Producto', "—")` |
| Cta3 | `134` | `"Saldo vencido"` | `With({_c: LookUp(colCtasAprb, '[CT] Numero de cuenta' = varSelAprb.'[AS] NumeroCuenta')}, Moneda(_c.'[CT] Saldo Vencido NIO', _c.'[CT] Saldo Vencido USD'))` — FontWeight `Semibold` |
| — | `166` | `lblSecSolAprb`: ESTILO-SecHead · Text `"SOLICITUD"` · H `18` |
| Sol1 | `188` | `"Retiro pedido"` | `Moneda(varSelAprb.'Retiro NIO', varSelAprb.'Retiro USD')` — FontWeight `Semibold` |
| Sol2 | `212` | `"Solicitante"` | `varSelAprb.pideAprb & " · " & Text(varSelAprb.'[AS] Fecha de Asignacion', "dd/mm HH:mm")` *(columna horneada)* |

> El panel NO tiene fila "Tipo" (el mockup sí): **LED-11** — el tipo viene antepuesto en
> las Notas de acá abajo. Los `LookUp` de cuenta pegan contra `colCtasAprb` en memoria.

Notas completas y sección de resolución (desde acá, posiciones RELATIVAS — el card crece):

| Control | Propiedad | Valor |
|---|---|---|
| `conNotasAprb` (Contenedor) | Y / H | `244` / `lblNotasAprb.Height + 2*gblE.PadSm` |
| | Fill / Radius* | `gblC.pantalla` / `gblE.RadioSm` · sin borde |
| `lblNotasAprb` (Etiqueta clásica, hijo) | X/Y/W | `gblE.PadSm` / `gblE.PadSm` / `Parent.Width - 2*gblE.PadSm` |
| | Text / Wrap / AutoHeight | `varSelAprb.Notas` / `true` / `true` — la solicitud completa, sin truncar |
| `lblSecResAprb` (Etiqueta clásica) | Y/H | `conNotasAprb.Y + conNotasAprb.Height + gblE.Pad` / `18` · ESTILO-SecHead · Text `"RESOLUCIÓN"` |
| `lblLocKAprb` (Etiqueta clásica) | Y/H | `lblSecResAprb.Y + 24` / `18` · Size `9` · FontWeight `Semibold` |
| | Text | `"Localizador * (requerido para aprobar)"` |
| `txtBuscarAgeAprb` (Entrada de texto clásica) | Y/H | `lblLocKAprb.Y + 20` / `gblE.AltoInput` · ESTILO-Input · PaddingLeft `34` · DelayOutput `true` |
| | HintText / Default | `"Buscar localizador por nombre (mín. 2)"` / `""` |
| | Visible | `IsBlank(varAgeAprb)` |
| `icoBuscarAgeAprb` (Ícono) | Icon / X / Y | `Icon.Search` / `txtBuscarAgeAprb.X + 8` / centrado en el buscador · Visible `txtBuscarAgeAprb.Visible` |
| `galAgeAprb` (Galería vertical) | Y | `txtBuscarAgeAprb.Y + txtBuscarAgeAprb.Height + 4` |
| | TemplateSize / Height | `48` / `Min(Max(Self.AllItemsCount, 1), 3) * Self.TemplateHeight` |
| | Fill / BorderColor / BorderThickness | `gblC.card` / `gblC.borde` / `1` |
| | Visible | `IsBlank(varAgeAprb) && Len(Trim(txtBuscarAgeAprb.Text)) >= 2` |
| | Items | `With({t: If(Len(Trim(txtBuscarAgeAprb.Text)) < 2, "§§", Trim(txtBuscarAgeAprb.Text))}, Filter('CM \| TBL \| Agentes', '[AG] Activo' = true && 'Email Usuario' <> Blank() && StartsWith('[AG] Nombre', t)))` — todo delegable; solo agentes activos CON usuario (sin usuario, `RepartoAsignacion` las deja en el equipo "Cartera sin cobrador"); centinela `"§§"` con < 2 caracteres (B-19, B-06) |
| | OnSelect | `Set(varAgeAprb, ThisItem)` |
| `lblNomFilaAgeAprb` / `lblMailFilaAgeAprb` | Text | `ThisItem.'[AG] Nombre'` / `ThisItem.'Email Usuario'` |
| `btnFilaAgeAprb` (overlay) | Text | `"Asignar a " & ThisItem.'[AG] Nombre'` · transparente, `Select(Parent)`, `TabIndex 0` |
| `lblSinAgeAprb` (Etiqueta clásica) | Visible | `galAgeAprb.Visible && galAgeAprb.AllItemsCount = 0` (sobre la galería) |
| `conAgeSelAprb` (Contenedor) | Y/H | `txtBuscarAgeAprb.Y` / `gblE.AltoInput` · Fill `gblC.tealSuave` · borde `gblC.teal` |
| | Visible | `!IsBlank(varAgeAprb)` |
| `lblAgeSelAprb` (hijo) | Text | `varAgeAprb.'[AG] Nombre' & "  ·  " & varAgeAprb.'Email Usuario'` |
| `btnCambiarAgeAprb` (hijo) | OnSelect | `Set(varAgeAprb, Blank()); Reset(txtBuscarAgeAprb)` |
| `lblMotKAprb` (Etiqueta clásica) | Y/H | `If(galAgeAprb.Visible, galAgeAprb.Y + galAgeAprb.Height, txtBuscarAgeAprb.Y + txtBuscarAgeAprb.Height) + gblE.PadSm` / `18` · Size `9` · FontWeight `Semibold` |
| | Text | `"Motivo de rechazo (requerido para rechazar)"` |
| `txtMotRechAprb` (Entrada de texto clásica) | Y/H | `lblMotKAprb.Y + 20` / `gblE.AltoInput` · ESTILO-Input |
| | HintText / Default | `"¿Por qué se rechaza?"` / `""` |

Pie del panel — dos botones anclados abajo, mitad y mitad:

| Control | Propiedad | Valor |
|---|---|---|
| `btnRechazarAprb` (Botón clásico) | Estilo | ESTILO-BotonPeligro · Text `"Rechazar"` |
| | X/Y/W/H | `gblE.Pad` / `Parent.Height - gblE.AltoBoton - gblE.Pad` / `(Parent.Width - 3*gblE.Pad)/2` / `gblE.AltoBoton` |
| | DisplayMode | `If(!IsBlank(Trim(txtMotRechAprb.Text)), DisplayMode.Edit, DisplayMode.Disabled)` |
| `btnAprobarAprb` (Botón clásico) | Estilo | ESTILO-BotonPri · Text `"✓ Aprobar y asignar"` |
| | X/Y/W/H | `btnRechazarAprb.X + btnRechazarAprb.Width + gblE.Pad` / igual Y / igual W / igual H |
| | DisplayMode | `If(!IsBlank(varAgeAprb), DisplayMode.Edit, DisplayMode.Disabled)` |

**btnAprobarAprb.OnSelect** (completo):

```powerfx
Patch('CM | TBL | Asignacions',
    LookUp('CM | TBL | Asignacions', '[AS] Numero' = varSelAprb.'[AS] Numero'),
    {
        '[AS] Estado de aprobacion': 'Choice | CM | Estado aprobacion especial'.Aprobado,
        '[AS] Agente': varAgeAprb,
        '[AS] Resolutor': gblUsuarioDV
    }
);
// (código vigente: todo envuelto en IfError — B-03; el Notify va ANTES de limpiar varAgeAprb)
Notify("✓ Aprobada — asignada a " & varAgeAprb.'[AG] Nombre' &
    ". El reparto la baja al celular del localizador.",
    NotificationType.Success, 2500);
Remove(colAprobAprb,
    LookUp(colAprobAprb, '[AS] Numero' = varSelAprb.'[AS] Numero'));
Set(varPanelAprb, false);
Set(varSelAprb, Blank());
Set(varAgeAprb, Blank());
Reset(txtBuscarAgeAprb)
```

> Al setear `'[AS] Agente'`, el plugin asíncrono `RepartoAsignacion` (catálogo, C.3) cambia el OWNER del registro al usuario
> del agente → eso dispara el aislamiento por propietario y la sincronización offline: la
> asignación aparece en el celular del localizador sin que esta app haga nada más. Por eso la
> notificación lo anuncia — la app solo aprueba; el reparto es del backend.

**btnRechazarAprb.OnSelect** (completo):

```powerfx
Patch('CM | TBL | Asignacions',
    LookUp('CM | TBL | Asignacions', '[AS] Numero' = varSelAprb.'[AS] Numero'),
    {
        '[AS] Estado de aprobacion': 'Choice | CM | Estado aprobacion especial'.Rechazada,
        'Motivo de rechazo': Trim(txtMotRechAprb.Text),
        '[AS] Resolutor': gblUsuarioDV
    }
);
Remove(colAprobAprb,
    LookUp(colAprobAprb, '[AS] Numero' = varSelAprb.'[AS] Numero'));
Set(varPanelAprb, false);
Set(varSelAprb, Blank());
Notify("Solicitud rechazada", NotificationType.Warning)
```

## 4. Navegación

`cmpNavAprb` (instancia de `cmpNavLateral`): receta de instanciación 8.1 exacta —
`RutaActiva = "aprb"` · `Visible = !nfMovil || gblNavMovilAbierta` · `X=0 · Y=0 ·
Width = gblE.AnchoNav · Height = Parent.Height` · **último control del árbol** (en móvil
flota encima del panel).

> Divergencias de esta pantalla ya declaradas en 00 §2: **LED-01** (Upper del nombre),
> **LED-05** (toasts → `Notify`), **LED-11** (tipo de especial dentro de `Notas`).

## 5. Checklist de salida

- [ ] Un usuario con rol ≠ Supervisor que navegue a la pantalla rebota a `scrInicio`.
- [ ] La cola ordena FIFO: la solicitud MÁS VIEJA arriba (`Fecha de Asignacion` ascendente).
- [ ] Clic en CUALQUIER punto de la fila (cliente, notas, monto) abre el panel — sin cazar píxeles.
- [ ] Al cambiar de solicitud (o cerrar el panel), el localizador (`varAgeAprb` + buscador) y el motivo de rechazo llegan LIMPIOS.
- [ ] Con 0–1 caracteres la galería de localizadores no aparece; con ≥ 2 ofrece SOLO agentes activos CON `Email Usuario` cuyo nombre empieza así, con el texto visible (B-04). Tocar uno muestra la tarjeta con "Cambiar".
- [ ] "✓ Aprobar y asignar" está deshabilitado sin localizador; "Rechazar" sin motivo (espacios no cuentan).
- [ ] Aprobar: la fila desaparece de la cola SIN recargar, Notify verde, y en Dataverse el registro
      queda `Aprobado` + `'[AS] Agente'` + `'[AS] Resolutor'` (y el owner cambia solo — `RepartoAsignacion`).
- [ ] Rechazar: registro `Rechazada` con `'Motivo de rechazo'` y `'[AS] Resolutor'` grabados.
- [ ] Vaciar la cola muestra "🎉 Nada por aprobar"; «↻ Actualizar» re-colecta y cierra el panel.
- [ ] Cero banda azul de delegación en Studio (filtro de cola y `galAgeAprb.Items` delegables; el resto en memoria).
- [ ] Móvil (`nfMovil`): panel a ancho completo, columna de notas oculta, nav por hamburguesa.
