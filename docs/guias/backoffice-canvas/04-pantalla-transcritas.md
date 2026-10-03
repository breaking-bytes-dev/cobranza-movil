# Guía 04 — scrTranscritas

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrTranscritas.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol (LED-20); filtro por PK: `'[IN] Grabado por'.User = gblUsuarioDV.User`.
> - Sin `colCtasTsc`: `clienteTsc/cifTsc/prodTsc` por navegación `ThisRecord.'[IN] Cuenta'.…` (receta 7.7).
> - KPI "TRANSCRITAS" = `varNTscTsc` con `CountIf` directo (receta 7.16), no `CountRows(colTscTsc)`.
> - Horneado único `colTscTmpTsc` → `AddColumns` → `colTscTsc`; se quitó el paso puente que daba "column already exists" (01 §11.1 #7).
> - `galPagosPanelTsc.AllItemsCount`; overlay `btnFilaTsc` reemplaza a `recFilaTsc` (receta 7.3); scrim/cerrar con foco visible.

**Propósito:** el arqueo del día del transcriptor (rol Backoffice): qué interacciones grabó HOY en el
core, con su código, y cuánto dinero transcribió. Todo es **LECTURA** — la evidencia de campo es
inmutable después de la grabación (mockup: `scrTranscritas()` + `rowInt()` + `openTrans()` rama `grab=true`).

**Contrato de entrada** (lo setea `App.OnStart`, guía 01 §4 — esta pantalla no recibe nada de otras):
`gblRol` = "Backoffice" · `gblUsuarioDV` (registro de `Users`) · `gblC`/`gblE` (tokens) · `colNav` ·
`gblNavMovilAbierta` (estado del drawer móvil — la setea cmpEncabezado/cmpNavLateral, 01 §8).
Las colecciones `colCtasTsc`, `colPagosHoyTsc`, `colTscTsc`, `colPagosTsc` (y la puente
`colTscTmpTsc`) y las variables `varSelTsc`/`varPanelTsc` las crea el propio `OnVisible` (§1).

**Convención de coordenadas:** dentro del template de una galería, X/Y son relativos al template (X 0 = borde izquierdo de la fila).
Literales documentados de ESTA guía (además de `gblE.*`): TemplateSize `gblE.AltoFila` (64) y `36` (pagos) · columna `k` del panel `110`.

## Árbol de controles

```
scrTranscritas                       ESTILO-Pantalla
├── cmpEncTsc                        cmpEncabezado (receta 7.5)
├── btnActualizarTsc                 Botón clásico "↻ Actualizar"
├── conKpisTsc                       Contenedor (transparente)
│   ├── cmpKpiTransTsc               cmpKpi "TRANSCRITAS"
│   └── cmpKpiPagosTsc               cmpKpi "PAGOS TRANSCRITOS"
├── conListaTsc                      Contenedor ESTILO-Card (receta 7.1)
│   ├── galTscTsc                    Galería vertical en blanco (recetas 7.3 + 7.11)
│   │   ├── recFilaTsc               Rectángulo — overlay clicable (7.3, al FONDO)
│   │   ├── lblClienteTsc            Etiqueta clásica
│   │   ├── lblCuentaTsc             Etiqueta clásica
│   │   ├── lblAgenteTsc             Etiqueta clásica
│   │   ├── lblComentTsc             Etiqueta clásica
│   │   ├── chpDatosTsc              Botón clásico — chip "✏️ Datos nuevos" (7.2 Especial)
│   │   ├── chpPagoTsc               Botón clásico — chip "💵 <monto>" (7.2 OK)
│   │   ├── chpGrabTsc               Botón clásico — chip "✓ Grabada · código" (7.2 OK)
│   │   └── recSepTsc                Rectángulo — separador (7.11)
│   └── cmpVacioTsc                  cmpVacio (7.12)
├── recScrimTsc                      Rectángulo — scrim (receta 7.4)
├── conPanelTsc                      Contenedor — panel de detalle LECTURA (7.4)
│   ├── lblTituloPanelTsc            Etiqueta clásica
│   ├── icoCerrarTsc                 Icono (Cancel)
│   ├── chpRoTsc                     Botón clásico — chip "solo lectura" (7.2 Neutro)
│   ├── lblSecCampoTsc               Etiqueta clásica ESTILO-SecHead
│   ├── lblKClienteTsc ─ lblVClienteTsc   Etiquetas clásicas (fila kv)
│   ├── lblKCifTsc ─ lblVCifTsc           Etiquetas clásicas (fila kv)
│   ├── lblKCuentaTsc ─ lblVCuentaTsc     Etiquetas clásicas (fila kv)
│   ├── lblKLocTsc ─ lblVLocTsc           Etiquetas clásicas (fila kv)
│   ├── lblKCapTsc ─ lblVCapTsc           Etiquetas clásicas (fila kv)
│   ├── lblSecComentTsc              Etiqueta clásica ESTILO-SecHead
│   ├── lblComentPanelTsc            Etiqueta clásica (Wrap)
│   ├── chpDatosPanelTsc             Botón clásico — chip ✏️ datos nuevos (7.2 Especial)
│   ├── lblSecPagosTsc               Etiqueta clásica ESTILO-SecHead
│   ├── galPagosPanelTsc             Galería vertical en blanco
│   │   ├── lblMontoPagoTsc          Etiqueta clásica
│   │   ├── lblReciboPagoTsc         Etiqueta clásica
│   │   └── recSepPagoTsc            Rectángulo — separador (7.11)
│   ├── lblSecTransTsc               Etiqueta clásica ESTILO-SecHead
│   ├── lblKCodigoTsc ─ lblVCodigoTsc     Etiquetas clásicas (fila kv)
│   ├── lblKGrabTsc ─ lblVGrabTsc         Etiquetas clásicas (fila kv)
│   ├── lblNotaInmutableTsc          Etiqueta clásica (Wrap)
│   └── btnCerrarPanelTsc            Botón clásico ESTILO-BotonFantasma
└── cmpNavTsc                        cmpNavLateral (§8.1 de la guía 01 — al FINAL del árbol)
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | fórmula §1.1 completa |

### 1.1 OnVisible (pegar completo)

```powerfx
// 1) Guard de rol — primera línea SIEMPRE (guía 01 §4)
If(!("Backoffice" in gblRol), Navigate(scrInicio));
// 2) Fuentes independientes en paralelo (ambas delegables)
Concurrent(
    ClearCollect(colCtasTsc, ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto')),
    ClearCollect(colPagosHoyTsc,
        Filter('CM | TBL | Pagos', 'Fecha del pago' >= Today()))
);
// 3) Arqueo del día: SOLO lo grabado HOY por MÍ.
//    choice por valor + comparación de fecha + lookup por registro = delegable (00 §4.2).
//    El join del cliente se hornea acá (7.7) → va DESPUÉS de colCtasTsc, NUNCA en el Concurrent.
ClearCollect(colTscTsc,
    AddColumns(
        Filter('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Grabado
            && '[IN] Fecha grabacion' >= Today()
            && '[IN] Grabado por' = gblUsuarioDV),
        clienteTsc, Upper(Coalesce(LookUp(colCtasTsc, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Nombre Cliente', "—")),   // LED-01
        cifTsc, Coalesce(LookUp(colCtasTsc, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'CIF Cliente', "—"),
        prodTsc, Coalesce(LookUp(colCtasTsc, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Tipo Producto', "—")
    )
);
// 4) Pagos del arqueo = pagos de HOY (ya en memoria) ∩ mis transcritas.
//    Filter sobre COLECCIÓN → cero delegación involucrada: permitido (00 §4.1).
ClearCollect(colPagosTsc,
    Filter(colPagosHoyTsc,
        !IsBlank(LookUp(colTscTsc, '[IN] Numero de Interaccion' = '[PG] Numero Interaccion'))));
// 5) Chip 💵 de la fila: montos por interacción HORNEADOS una vez (todo sobre colecciones,
//    en memoria — R1-02). Puente colTscTmpTsc obligatorio: ClearCollect(colTscTsc,
//    AddColumns(colTscTsc, …)) se vaciaría a sí misma (el Clear corre antes de evaluar).
ClearCollect(colTscTmpTsc,
    AddColumns(colTscTsc,
        pagosNioTsc, Sum(Filter(colPagosTsc,
            '[PG] Numero Interaccion' = '[IN] Numero de Interaccion'
            && '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.NIO), '[PG] Monto'),
        pagosUsdTsc, Sum(Filter(colPagosTsc,
            '[PG] Numero Interaccion' = '[IN] Numero de Interaccion'
            && '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.USD), '[PG] Monto')
    )
);
ClearCollect(colTscTsc, colTscTmpTsc);
// 6) Estado del panel
Set(varSelTsc, Blank());
Set(varPanelTsc, false)
```

## 2. Encabezado y botón Actualizar

`cmpEncTsc` — instancia de `cmpEncabezado` con la receta 7.5 exacta (X/Y/W/H fijados ahí):

| Propiedad | Valor |
|---|---|
| Titulo | `"Transcritas hoy"` |
| Subtitulo | `"Tu arqueo del día — código grabado y auditoría sellada."` |

| Control | Propiedad | Valor |
|---|---|---|
| `btnActualizarTsc` (Botón clásico) | Estilo | ESTILO-BotonFantasma |
| | X / Y / W / H | `conKpisTsc.X + conKpisTsc.Width - Self.Width` / `cmpEncTsc.Y + (cmpEncTsc.Height - Self.Height)/2` / `140` / `gblE.AltoInput` |
| | Text | `"↻ Actualizar"` |
| | OnSelect | fórmula §2.1 |

### 2.1 btnActualizarTsc.OnSelect (pegar completo — es el OnVisible §1.1 sin guard ni estado de panel)

```powerfx
Concurrent(
    ClearCollect(colCtasTsc, ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto')),
    ClearCollect(colPagosHoyTsc,
        Filter('CM | TBL | Pagos', 'Fecha del pago' >= Today()))
);
ClearCollect(colTscTsc,
    AddColumns(
        Filter('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Grabado
            && '[IN] Fecha grabacion' >= Today()
            && '[IN] Grabado por' = gblUsuarioDV),
        clienteTsc, Upper(Coalesce(LookUp(colCtasTsc, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Nombre Cliente', "—")),
        cifTsc, Coalesce(LookUp(colCtasTsc, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'CIF Cliente', "—"),
        prodTsc, Coalesce(LookUp(colCtasTsc, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Tipo Producto', "—")
    )
);
ClearCollect(colPagosTsc,
    Filter(colPagosHoyTsc,
        !IsBlank(LookUp(colTscTsc, '[IN] Numero de Interaccion' = '[PG] Numero Interaccion'))));
ClearCollect(colTscTmpTsc,
    AddColumns(colTscTsc,
        pagosNioTsc, Sum(Filter(colPagosTsc,
            '[PG] Numero Interaccion' = '[IN] Numero de Interaccion'
            && '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.NIO), '[PG] Monto'),
        pagosUsdTsc, Sum(Filter(colPagosTsc,
            '[PG] Numero Interaccion' = '[IN] Numero de Interaccion'
            && '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.USD), '[PG] Monto')
    )
);
ClearCollect(colTscTsc, colTscTmpTsc);
Notify("✓ Arqueo actualizado", NotificationType.Success, 2500)   // LED-05
```

## 3. KPIs

| Control | Propiedad | Valor |
|---|---|---|
| `conKpisTsc` (Contenedor, transparente) | X / Y | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* / `cmpEncTsc.Y + cmpEncTsc.Height` |
| | Width | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* |
| | Height | `If(nfMovil, 2*96 + gblE.PadSm, 96)` (96 = altura fija de `cmpKpi`, guía 01 §8.3) |
| `cmpKpiTransTsc` (cmpKpi) | X / Y / H | `0` / `0` / `96` |
| | Width | `If(nfMovil, Parent.Width, (Parent.Width - gblE.PadSm)/2)` |
| | Etiqueta / Valor | `"TRANSCRITAS"` / `Text(CountRows(colTscTsc))` |
| | Detalle / EsHero | `""` / `false` |
| `cmpKpiPagosTsc` (cmpKpi) | X / Y | `If(nfMovil, 0, cmpKpiTransTsc.Width + gblE.PadSm)` / `If(nfMovil, 96 + gblE.PadSm, 0)` |
| | Width / H | `If(nfMovil, Parent.Width, (Parent.Width - gblE.PadSm)/2)` / `96` |
| | Etiqueta | `"PAGOS TRANSCRITOS"` |
| | Valor | `Moneda(Sum(Filter(colPagosTsc, '[PG] Moneda' = 'Choice \| CM \| Monedas en Pago'.NIO), '[PG] Monto'), Sum(Filter(colPagosTsc, '[PG] Moneda' = 'Choice \| CM \| Monedas en Pago'.USD), '[PG] Monto'))` — `Moneda()` es la named formula de la guía 01 §3; el Sum corre sobre COLECCIÓN |
| | Detalle / EsHero | `""` / `false` |

## 4. Lista del arqueo

| Control | Propiedad | Valor |
|---|---|---|
| `conListaTsc` (Contenedor) | Estilo | ESTILO-Card (receta 7.1) |
| | X / Y | `conKpisTsc.X` / `conKpisTsc.Y + conKpisTsc.Height + gblE.Pad` |
| | Width / Height | `conKpisTsc.Width` / `Parent.Height - Self.Y - gblE.Pad` |
| `galTscTsc` (Galería vertical en blanco) | X / Y | `gblE.Pad` / `gblE.PadSm` |
| | Width / Height | `Parent.Width - 2*gblE.Pad` / `Parent.Height - 2*gblE.PadSm` |
| | Items | `Sort(colTscTsc, '[IN] Fecha grabacion', SortOrder.Descending)` |
| | TemplateSize / TemplatePadding | `gblE.AltoFila` / `0` |
| | TemplateFill | `If(ThisItem.IsSelected, gblC.tealSuave, gblC.card)` (7.3) |
| | OnSelect | `Set(varSelTsc, ThisItem); Set(varPanelTsc, true)` |
| `cmpVacioTsc` (cmpVacio) | X / Y / W / H | `0` / `(Parent.Height - Self.Height)/2` / `Parent.Width` / `140` |
| | Icono / Titulo | `"🗒️"` / `"Nada todavía."` |
| | Mensaje | `"Lo que transcribas hoy aparece acá — tu arqueo del día."` |
| | Visible | `CountRows(colTscTsc) = 0` (receta 7.12) |

Dentro del template — **TODO hijo lleva `OnSelect = Select(Parent)`** (receta 7.3), chips incluidos
(excepción de la receta 7.2: chip dentro de fila clicable va **SIN** `DisplayMode.View`, o se come el clic):

| Control | Propiedad | Valor |
|---|---|---|
| `recFilaTsc` (Rectángulo, PRIMERO = al fondo) | X / Y / W / H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` |
| | Fill / BorderThickness | `Transparent` / `0` |
| `lblClienteTsc` (Etiqueta clásica) | X / Y / H | `gblE.PadSm` / `6` / `20` |
| | Width | `If(nfMovil, Parent.TemplateWidth*0.55, Parent.TemplateWidth*0.32)` |
| | Text / FontWeight | `ThisItem.clienteTsc` / `FontWeight.Semibold` |
| `lblCuentaTsc` (Etiqueta clásica) | X / Y / W / H | `gblE.PadSm` / `26` / `lblClienteTsc.Width` / `18` |
| | Text | `ThisItem.'[IN] Numerocuenta' & " · " & ThisItem.prodTsc` · ESTILO-Subtitulo |
| `lblAgenteTsc` (Etiqueta clásica) | X / Y / W / H | `gblE.PadSm` / `44` / `lblClienteTsc.Width` / `16` |
| | Text | `"🏍️ " & ThisItem.'[IN] NombreAgente' & " · " & Text(ThisItem.'Fecha de captura', "HH:mm")` |
| | Size / Color | `8` / `gblC.muted2` |
| `lblComentTsc` (Etiqueta clásica) | X / Y / H | `Parent.TemplateWidth*0.34` / `0` / `Parent.TemplateHeight` |
| | Width | `Parent.TemplateWidth*0.32` |
| | Text / VerticalAlign | `ThisItem.'[IN] Comentario'` / `VerticalAlign.Middle` |
| | Size / Color / Wrap | `9` / `gblC.ink2` / `false` (truncado en una línea) |
| | Visible | `!nfMovil` (en móvil la fila queda cliente + chips) |
| `chpGrabTsc` (Botón clásico, receta 7.2 variante OK) | X / Y | `Parent.TemplateWidth - Self.Width - gblE.PadSm` / `(Parent.TemplateHeight - Self.Height)/2` |
| | Text | `"✓ Grabada · " & ThisItem.'[IN] Codigo Grabacion'` |
| `chpPagoTsc` (Botón clásico, 7.2 OK) | X / Y | `chpGrabTsc.X - Self.Width - 6` / `chpGrabTsc.Y` |
| | W | `14 + 7*Len(Self.Text)` *(ancho por texto de la receta 7.2 — nada de W fija)* |
| | Text / Visible | `"💵 " & Moneda(ThisItem.pagosNioTsc, ThisItem.pagosUsdTsc)` *(montos horneados en §1.1 paso 5 — R1-02)* / `ThisItem.'[IN] Aplica pago'` |
| `chpDatosTsc` (Botón clásico, 7.2 Especial) | X | `chpGrabTsc.X - If(chpPagoTsc.Visible, chpPagoTsc.Width + 6, 0) - Self.Width - 6` |
| | Y / W | `chpGrabTsc.Y` / `14 + 7*Len(Self.Text)` *(receta 7.2)* |
| | Text / Visible | `"✏️ Datos nuevos"` / `ThisItem.'[IN] Actualiza info'` |
| `recSepTsc` (Rectángulo) | — | receta 7.11 exacta: `X 0 · Width Parent.TemplateWidth · Height 1 · Y = Parent.TemplateHeight - 1 · Fill gblC.hairline` |

## 5. Panel de detalle (LECTURA)

`recScrimTsc` y `conPanelTsc` siguen la receta 7.4 **exacta** con la variable `varPanelTsc`
(scrim: pantalla completa, `Visible = varPanelTsc`, `OnSelect = Set(varPanelTsc, false)`; panel:
ESTILO-Card `Radius* = 0`, `Width = If(nfMovil, Parent.Width, Min(480, Parent.Width*0.45))`,
`X = Parent.Width - Self.Width`, `Y = 0`, `Height = Parent.Height`, `Visible = varPanelTsc`).

Interno del panel — **layout fijo**: filas kv = etiqueta `k` (X `gblE.Pad`, Width `110`,
ESTILO-Subtitulo) + etiqueta `v` (X `gblE.Pad + 110`, Width `Parent.Width - gblE.Pad - Self.X`,
Size 10), Height `24`. Todas las etiquetas de sección: ESTILO-SecHead, X `gblE.Pad`,
Width `Parent.Width - 2*gblE.Pad`, Height `18`. Ningún control es editable: no hay inputs.

| Control | Propiedad | Valor |
|---|---|---|
| `lblTituloPanelTsc` (Etiqueta clásica) | X / Y / W / H | `gblE.Pad` / `16` / `Parent.Width - 2*gblE.Pad - 32` / `24` |
| | Text | `varSelTsc.'[IN] Numero de Interaccion'` · Size `13` · `FontWeight.Bold` (7.4) |
| `icoCerrarTsc` (Icono Cancel) | X / Y / W / H | `Parent.Width - 40` / `16` / `24` / `24` |
| | Color / OnSelect | `gblC.muted` / `Set(varPanelTsc, false)` |
| `chpRoTsc` (Botón clásico, 7.2 Neutro) | X / Y | `gblE.Pad` / `48` |
| | Text | `"solo lectura"` |
| `lblSecCampoTsc` | Y / Text | `84` / `"GESTIÓN DE CAMPO"` |
| `lblKClienteTsc` / `lblVClienteTsc` | Y / Text | `104` / `"Cliente"` — `varSelTsc.clienteTsc` |
| `lblKCifTsc` / `lblVCifTsc` | Y / Text | `128` / `"CIF"` — `varSelTsc.cifTsc` |
| `lblKCuentaTsc` / `lblVCuentaTsc` | Y / Text | `152` / `"Cuenta"` — `varSelTsc.'[IN] Numerocuenta' & " · " & varSelTsc.prodTsc` |
| `lblKLocTsc` / `lblVLocTsc` | Y / Text | `176` / `"Localizador"` — `"🏍️ " & varSelTsc.'[IN] NombreAgente'` |
| `lblKCapTsc` / `lblVCapTsc` | Y / Text | `200` / `"Capturada"` — `Text(varSelTsc.'Fecha de captura', "dd/mm HH:mm")` |
| `lblSecComentTsc` | Y / Text | `236` / `"COMENTARIO"` |
| `lblComentPanelTsc` (Etiqueta clásica) | X / Y / W / H | `gblE.Pad` / `256` / `Parent.Width - 2*gblE.Pad` / `76` |
| | Text / Wrap | `varSelTsc.'[IN] Comentario'` / `true` (Height explícito: gotcha de labels largos) |
| | Fill / PaddingLeft / PaddingTop | `gblC.pantalla` / `gblE.PadSm` / `gblE.PadSm` |
| `chpDatosPanelTsc` (Botón clásico, 7.2 Especial) | X / Y | `gblE.Pad` / `344` |
| | Text | `"✏️ Trae datos nuevos del cliente — revisalos en el core"` |
| | Visible | `varSelTsc.'[IN] Actualiza info'` |
| `lblSecPagosTsc` | Y | `384` |
| | Text | `"PAGOS (" & CountRows(galPagosPanelTsc.AllItems) & ")"` |
| | Visible | `varSelTsc.'[IN] Aplica pago'` |
| `galPagosPanelTsc` (Galería vertical en blanco) | X / Y / W / H | `gblE.Pad` / `404` / `Parent.Width - 2*gblE.Pad` / `108` |
| | Items | `Filter('CM \| TBL \| Pagos', '[PG] Numero Interaccion' = varSelTsc.'[IN] Numero de Interaccion')` (igualdad de texto exacto: delegable, 00 §4.2) |
| | TemplateSize / Visible | `36` / `varSelTsc.'[IN] Aplica pago'` |
| `lblMontoPagoTsc` (Etiqueta clásica) | X / Y / W / H | `0` / `8` / `Parent.TemplateWidth*0.55` / `20` |
| | Text | `If(varSelTsc.'[IN] Aplica pago', If(ThisItem.'[PG] Moneda' = 'Choice \| CM \| Monedas en Pago'.USD, "US$ ", "C$ ") & Text(ThisItem.'[PG] Monto', "#,##0.00"))` · `FontWeight.Semibold` · Color `gblC.okInk` |
| `lblReciboPagoTsc` (Etiqueta clásica) | X / Y / H | `Parent.TemplateWidth*0.55` / `8` / `20` |
| | Width / Align | `Parent.TemplateWidth*0.45` / `Align.Right` |
| | Text | `"Recibo " & ThisItem.'[PG] Numero recibo'` · ESTILO-Subtitulo |
| `recSepPagoTsc` (Rectángulo) | — | receta 7.11 exacta |
| `lblSecTransTsc` | Y / Text | `528` / `"TRANSCRIPCIÓN AL CORE"` |
| `lblKCodigoTsc` / `lblVCodigoTsc` | Y / Text | `548` / `"Código"` — `varSelTsc.'[IN] Codigo Grabacion' & " 🔒"` (`lblVCodigoTsc` con `FontWeight.Bold`) |
| `lblKGrabTsc` / `lblVGrabTsc` | Y / Text | `572` / `"Grabada"` — `Text(varSelTsc.'[IN] Fecha grabacion', "dd/mm HH:mm") & " · por vos"` |
| `lblNotaInmutableTsc` (Etiqueta clásica) | X / Y / W / H | `gblE.Pad` / `600` / `Parent.Width - 2*gblE.Pad` / `36` |
| | Text | `"Bloqueada tras la grabación — la evidencia de campo es inmutable."` |
| | Estilo / Wrap | ESTILO-Subtitulo / `true` |
| `btnCerrarPanelTsc` (Botón clásico) | Estilo | ESTILO-BotonFantasma |
| | X / Y | `gblE.Pad` / `Parent.Height - gblE.AltoBoton - gblE.Pad` |
| | Width / Text | `Parent.Width - 2*gblE.Pad` / `"Cerrar"` |
| | OnSelect | `Set(varPanelTsc, false)` |

## 6. Navegación

`cmpNavTsc` — instancia de `cmpNavLateral` con la receta de instanciación de la guía 01 §8.1 (`RutaActiva = "tsc"` ·
`Visible = !nfMovil || gblNavMovilAbierta` · `X 0 · Y 0 · Height = Parent.Height`). Va al **FINAL** del árbol, después de `conPanelTsc`.

> **Notas y divergencias declaradas**
> - **LED-01**: nombre del cliente `Upper(...)` horneado en `clienteTsc`. **LED-05**: el toast de
>   "arqueo actualizado" es `Notify(...)`.
> - **Filtro "hoy"**: `'[IN] Fecha grabacion' >= Today()` = desde la medianoche local — la semántica exacta de "arqueo del día".
> - **Ubicación (📍 mapa)** del panel del mockup: NO se incluye — las columnas `'[IN] Latitud'` /
>   `'[IN] Longitud'` SÍ existen; la omisión está declarada en **LED-16** (valor marginal
>   post-transcripción; la estación — guía 03 — sí muestra el mapa, que es donde importa).
>   Quick-add si el UAT lo pide: fila kv bajo "Capturada" con el patrón de la guía 03.
> - **LED-09**: el "antes/después" de la actualización de datos vive en la estación (guía 03); acá el
>   panel solo lo señala con `chpDatosPanelTsc`, igual que la rama `grab=true` de `openTrans()`.
> - **La fila CIF del panel es un agregado deliberado (no está en el mockup)**: el CIF es la clave
>   que el transcriptor usa en el core (hallazgo R1-07).
> - **Layout fijo del panel**: las secciones ocultas (datos/pagos) dejan hueco vertical — aceptado:
>   panel de lectura, cero cascadas condicionales de Y.
> - **Auditoría (00 §3.2)**: `'[IN] Grabado por'`/`'[IN] Fecha grabacion'` hoy los escribe el Patch de
>   la guía 03 (receta 7.8). Cuando el flujo server-side se active, esta pantalla NO cambia: solo lee.
> - Si IntelliSense difiere de algún display name, **el IntelliSense manda** — anotalo y avisá.

## 7. Checklist de salida

- [ ] Entrar con un usuario que NO es Backoffice → rebota a `scrInicio` sin parpadeo de datos.
- [ ] El KPI "Transcritas" coincide con el número de filas de `galTscTsc`.
- [ ] El KPI "Pagos transcritos" muestra `C$ … · US$ …` y omite la moneda en cero (named formula `Moneda`).
- [ ] Solo aparecen interacciones grabadas HOY por MI usuario (con otro transcriptor: arqueo distinto).
- [ ] La lista viene ordenada por `'[IN] Fecha grabacion'` descendente (la última grabada, arriba).
- [ ] Clic en CUALQUIER punto de la fila (texto, chips, espacio vacío) abre el panel de esa interacción.
- [ ] El panel es 100 % lectura: código con 🔒, "· por vos", nota de inmutabilidad — y ningún input.
- [ ] Fila con `'[IN] Aplica pago'` muestra el chip "💵 C$ … · US$ …" con el monto horneado
      (`Moneda`) y su panel lista los pagos con monto y recibo.
- [ ] "↻ Actualizar" re-colecta y notifica; lo recién marcado en `scrTranscripcion` aparece al volver acá.
- [ ] Sin transcripciones hoy: `cmpVacioTsc` "Nada todavía." centrado.
- [ ] Horas en formato 24 h (`HH:mm`) en fila y panel — nada de reloj de 12 h sin AM/PM.
- [ ] Studio no muestra NINGÚN subrayado azul de delegación en esta pantalla.
- [ ] Móvil (< 920): nav en hamburguesa, KPIs apilados, comentario oculto en la fila, panel a pantalla completa.
