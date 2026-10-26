# Guía 10 — scrCierres

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrCierres.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol (LED-20); sin `colCtasCie`: `clienteCie/diasCie/nioCie/usdCie/montoCie` por navegación `ThisRecord.'[AS] Cuenta'.…` (receta 7.7).
> - Contadores de `btnTabIncobCie`/`btnTabEspCie` con `CountIf` directo al datasource (receta 7.16).
> - `cmpVacioCie.Visible = galCierresCie.AllItemsCount = 0`.
> - Overlay `btnFilaCie` reemplaza a `recFilaCie`; etiquetas y `chpTipoCie` sin `OnSelect` (receta 7.3); `AccessibleLabel` + foco en galería, scrim, cerrar, combo y detalle.

Cierres manuales del Admin: las asignaciones **Incobrables** y **Especiales** activas se cierran
a mano, con motivo obligatorio (DEC-020 / DEC-022). **Nunca las cierra el localizador** — las
Diarias las cierra la reconciliación (guía 09); esta pantalla no las toca.

**Contrato de entrada**: `gblRol`, `gblC`, `gblE`, `gblUsuarioDV` y `colNav` vienen del
`App.OnStart` (guía 01 §4); `Moneda()` y `nfMovil` de `App.Formulas` (guía 01 §3). La pantalla
no recibe variables de otra pantalla: colecta lo suyo en su propio OnVisible.

## Árbol de controles

```
scrCierres                          (ESTILO-Pantalla)
├── cmpEncabezadoCie                instancia de cmpEncabezado (receta 7.5)
├── conContenidoCie                 (Contenedor manual, transparente)
│   ├── conTabsCie                  (Contenedor manual)  segmento de tabs
│   │   ├── btnTabIncobCie         (Botón clásico)  "🟠 Incobrables (n)"
│   │   └── btnTabEspCie           (Botón clásico)  "🟣 Especiales (n)"
│   ├── btnActualizarCie           (Botón clásico)  "↻ Actualizar"
│   ├── galCierresCie              (Galería vertical en blanco)
│   │   ├── recFilaCie             (Rectángulo)  overlay clicable, receta 7.3
│   │   ├── lblNombreCie           (Etiqueta clásica)  cliente en MAYÚSCULAS
│   │   ├── lblMetaCie             (Etiqueta clásica)  nº cuenta · 🏍️ agente
│   │   ├── lblMoraCie             (Etiqueta clásica)  "N días de mora"
│   │   ├── lblMontoCie            (Etiqueta clásica)  monto (REGLA-MONTO-CIE)
│   │   ├── chpTipoCie             (Botón clásico)  chip Incobrable/Especial, receta 7.2
│   │   └── recSepCie              (Rectángulo)  separador, receta 7.11
│   └── cmpVacioCie                instancia de cmpVacio (receta 7.12)
├── recScrimCie                     (Rectángulo)  scrim del panel, receta 7.4
├── conPanelCie                     (Contenedor manual)  panel "Cerrar asignación", receta 7.4
│   ├── lblTituloPanCie            (Etiqueta clásica)  "Cerrar asignación"
│   ├── icoCerrarCie               (Icono, Cancel)
│   ├── lblK1Cie · lblV1Cie        (Etiquetas clásicas)  KV Cliente
│   ├── lblK2Cie · lblV2Cie        (Etiquetas clásicas)  KV Cuenta
│   ├── lblK3Cie · lblV3Cie        (Etiquetas clásicas)  KV Tipo
│   ├── lblK4Cie · lblV4Cie        (Etiquetas clásicas)  KV Saldo vencido
│   ├── recKvSep1Cie..recKvSep3Cie (Rectángulos)  hairlines entre KV
│   ├── lblSecMotivoCie            (Etiqueta clásica)  "MOTIVO DE CIERRE *"
│   ├── cmbMotivoCie               (Cuadro combinado)  motivos según tipo
│   ├── lblSecDetalleCie           (Etiqueta clásica)  "DETALLE"
│   ├── txtDetalleCie              (Entrada de texto clásica, multilínea)
│   ├── btnCancelarCie             (Botón clásico)  ESTILO-BotonFantasma
│   └── btnCerrarCie               (Botón clásico)  ESTILO-BotonPeligro
└── cmpNavCie                       instancia de cmpNavLateral (guía 01 §8.1 — al FINAL del árbol)
```

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | ↓ completo, listo para pegar |

```powerfx
If(!("Admin" in gblRol), Navigate(scrInicio));
// 1) Cuentas PRIMERO: el join de abajo la necesita — NO usar Concurrent acá (gotcha receta 7.7)
ClearCollect(colCtasCie,
    ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', '[CT] Dias Mora',
        '[CT] Saldo Vencido NIO', '[CT] Saldo Vencido USD'));
// 2) Asignaciones activas Incobrable/Especial + joins horneados una vez (receta 7.7)
//    Filtro DELEGABLE: OR de igualdades de choice por valor + booleano (00 §4.2)
ClearCollect(colCierresCie,
    AddColumns(
        Filter('CM | TBL | Asignacions',
            ('[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Incobrable
              || '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial)
            && '[AS] Estado' = true),
        clienteCie, Upper(Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente', "—")),
        diasCie, Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Dias Mora', 0),
        nioCie, Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido NIO', 0),
        usdCie, Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido USD', 0),
        montoCie, If('[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
                && (Coalesce('Retiro NIO', 0) > 0 || Coalesce('Retiro USD', 0) > 0),
            Moneda('Retiro NIO', 'Retiro USD'),
            Moneda(
                Coalesce(LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido NIO', 0),
                Coalesce(LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido USD', 0)))
    )
);
// 3) Estado de pantalla (resetea tab/panel entre visitas — gotcha de persistencia)
Set(varTabCie, "incob");
Set(varSelCie, Blank());
Set(varPanelCie, false)
```

> - **REGLA-MONTO-CIE (regla única, declarada en LED-15)**: para **Especiales** con retiro registrado
>   (`'Retiro NIO'` o `'Retiro USD'` > 0) la fila muestra el **monto del retiro**; en cualquier
>   otro caso (Especial sin retiro, toda Incobrable) muestra el **saldo vencido de la cuenta**.
>   Se hornea UNA vez en `montoCie` — ni la fila ni el panel recalculan. El panel siempre
>   muestra "Saldo vencido" de la cuenta, igual que el mockup.
> - `Upper()` del nombre completo: LED-01 (no hay shortname).
> - `Moneda()` es la UDF de App.Formulas — usable dentro de `AddColumns` sin problema.
> - Cota 2000 declarada (00 §4.1): `colCierresCie` son cientos como máximo; `colCtasCie` es la
>   cartera — si supera 2000 cuentas, los joins de las filas fuera de página muestran "—"/0
>   (mismo límite aceptado que la receta 7.7).

Encabezado — **receta 7.5**: `cmpEncabezadoCie` con `Titulo = "Cierres"` ·
`Subtitulo = "Incobrables y Especiales se cierran a mano, con motivo. Nunca el localizador."`.
Nav — **guía 01 §8.1**: `cmpNavCie` con `RutaActiva = "cie"`, al FINAL del árbol.

## 2. Contenido: tabs + actualizar

| Control | Propiedad | Valor |
|---|---|---|
| `conContenidoCie` (Contenedor manual) | X / Y | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* / `cmpEncabezadoCie.Y + cmpEncabezadoCie.Height` |
| | Width / Height | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* / `Parent.Height - Self.Y` |
| | Fill | `Transparent` |
| `conTabsCie` (Contenedor manual) | X / Y / W / H | `gblE.Pad` / `gblE.PadSm` / `356` / `44` |
| | Fill | `gblC.hairline` |
| | RadiusTopLeft/TopRight/BottomLeft/BottomRight | `11` (literal documentado — calca `.tabs` del mockup) |
| `btnActualizarCie` (Botón clásico) | X / Y / W / H | `Parent.Width - Self.Width - gblE.Pad` / `gblE.PadSm` / `140` / `44` |
| | Text / estilo | `"↻ Actualizar"` · ESTILO-BotonFantasma |
| | OnSelect | ↓ (misma recarga que el OnVisible, sin el guard) |

```powerfx
// OnSelect de btnActualizarCie — re-colecta TODO (secuencial, cuentas primero) y cierra el panel
ClearCollect(colCtasCie,
    ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', '[CT] Dias Mora',
        '[CT] Saldo Vencido NIO', '[CT] Saldo Vencido USD'));
ClearCollect(colCierresCie,
    AddColumns(
        Filter('CM | TBL | Asignacions',
            ('[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Incobrable
              || '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial)
            && '[AS] Estado' = true),
        clienteCie, Upper(Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente', "—")),
        diasCie, Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Dias Mora', 0),
        nioCie, Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido NIO', 0),
        usdCie, Coalesce(
            LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido USD', 0),
        montoCie, If('[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
                && (Coalesce('Retiro NIO', 0) > 0 || Coalesce('Retiro USD', 0) > 0),
            Moneda('Retiro NIO', 'Retiro USD'),
            Moneda(
                Coalesce(LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido NIO', 0),
                Coalesce(LookUp(colCtasCie, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido USD', 0)))
    )
);
Set(varSelCie, Blank());
Set(varPanelCie, false)
```

Los dos tabs son **Botones clásicos** dentro de `conTabsCie` (activo = card blanca + teal;
inactivo = transparente + muted, calcando `.tab`/`.tab.on` del mockup):

| Control | Propiedad | Valor |
|---|---|---|
| `btnTabIncobCie` (Botón clásico) | X / Y / W / H | `4` / `4` / `170` / `36` |
| | Text | `"🟠 Incobrables (" & CountRows(Filter(colCierresCie, '[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable)) & ")"` |
| | Fill / Color | `If(varTabCie = "incob", gblC.card, Color.Transparent)` / `If(varTabCie = "incob", gblC.teal, gblC.muted)` |
| | HoverFill / FontWeight / Size | `Self.Fill` / `Bold` / `9` |
| | RadiusTopLeft…BottomRight / BorderThickness | `8` / `0` |
| | OnSelect | `Set(varTabCie, "incob")` |
| `btnTabEspCie` (Botón clásico) | X / Y / W / H | `178` / `4` / `170` / `36` |
| | Text | `"🟣 Especiales (" & CountRows(Filter(colCierresCie, '[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Especial)) & ")"` |
| | resto | igual que `btnTabIncobCie`, cambiando `"incob"` por `"esp"` en Fill/Color/OnSelect |

> Los conteos corren sobre la colección — cero delegación. El estado del tab NO persiste entre
> visitas: el OnVisible lo repone a `"incob"`.

## 3. galCierresCie — la lista (recetas 7.3 + 7.11)

| Propiedad | Valor |
|---|---|
| X / Y | `gblE.Pad` / `conTabsCie.Y + conTabsCie.Height + gblE.PadSm` |
| Width / Height | `Min(gblE.AnchoMax, Parent.Width - 2*gblE.Pad)` / `Parent.Height - Self.Y - gblE.Pad` |
| Items | `Filter(colCierresCie, If(varTabCie = "incob", '[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable, '[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Especial))` *(en memoria)* |
| TemplateSize | `gblE.AltoFila` (64) |
| TemplatePadding | `0` |
| TemplateFill | `If(ThisItem.IsSelected && varPanelCie, gblC.tealSuave, gblC.card)` |
| OnSelect | `Set(varSelCie, ThisItem); Set(varPanelCie, true); Reset(cmbMotivoCie); Reset(txtDetalleCie)` |

> - Divergencia visual declarada: el mockup pinta cada fila como card suelta con gap; en canvas
>   es lista continua con separador 7.11 — mismo contenido, cero costo.
> - Los `Reset()` en el OnSelect limpian motivo/detalle del cierre ANTERIOR (el estado de los
>   inputs persiste entre aperturas del panel — gotcha del proyecto).

Template — coordenadas RELATIVAS al template; TODO hijo lleva `OnSelect = Select(Parent)` (7.3):

| Control | Propiedad | Valor |
|---|---|---|
| `recFilaCie` (Rectángulo, al FONDO del z-order) | X / Y / W / H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` |
| | Fill / OnSelect | `Transparent` / `Select(Parent)` |
| `lblNombreCie` (Etiqueta clásica) | X / Y / W / H | `gblE.Pad` / `8` / `Parent.TemplateWidth * 0.38` / `22` |
| | Text / FontWeight / Size / OnSelect | `ThisItem.clienteCie` / `Bold` / `11` / `Select(Parent)` |
| `lblMetaCie` (Etiqueta clásica) | X / Y / W / H | `gblE.Pad` / `32` / `Parent.TemplateWidth * 0.38` / `20` |
| | Text | `ThisItem.'[AS] NumeroCuenta' & " · 🏍️ " & Coalesce(ThisItem.'[AS] Agente'.'[AG] Nombre', "—")` *(el lookup se navega hasta su campo primario — mismo patrón que la guía 09; R1-05)* |
| | Size / Color / OnSelect | `9` / `gblC.muted` / `Select(Parent)` |
| `lblMoraCie` (Etiqueta clásica) | X / Y / W / H | `Parent.TemplateWidth * 0.40` / `20` / `Parent.TemplateWidth * 0.22` / `24` |
| | Text / Color / OnSelect | `Text(ThisItem.diasCie) & " días de mora"` / `gblC.ink2` / `Select(Parent)` |
| `lblMontoCie` (Etiqueta clásica) | X / Y / W / H | `Parent.TemplateWidth - 260 - gblE.Pad` / `8` / `260` / `22` |
| | Text | `ThisItem.montoCie` *(REGLA-MONTO-CIE, horneado en §1)* |
| | Align / FontWeight / Color / OnSelect | `Align.Right` / `Bold` / `gblC.teal` / `Select(Parent)` |
| `chpTipoCie` (Botón clásico, receta 7.2) | X / Y / H | `Parent.TemplateWidth - Self.Width - gblE.Pad` / `32` / `26` |
| | Width | `14 + 7*Len(Self.Text)` |
| | Text | `If(ThisItem.'[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable, "Incobrable", "Especial")` |
| | Fill / Color | `If(ThisItem.'[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable, gblC.incobBg, gblC.espBg)` / `If(ThisItem.'[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable, gblC.incobInk, gblC.espInk)` |
| | OnSelect / resto | `Select(Parent)` — **SIN `DisplayMode.View`** (excepción de la receta 7.2: chip dentro de fila clicable) / receta 7.2 |
| `recSepCie` (Rectángulo, receta 7.11) | X / Y / W / H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` |
| | Fill | `gblC.hairline` |

Estado vacío por tab — **receta 7.12**, `cmpVacioCie` centrado sobre el área de la galería:

| Propiedad | Valor |
|---|---|
| Icono / Titulo | `"✨"` / `"Nada activo"` |
| Mensaje | `"No hay " & If(varTabCie = "incob", "Incobrables", "Especiales") & " activas para cerrar."` |
| Visible | `CountRows(galCierresCie.AllItems) = 0` |

## 4. Panel "Cerrar asignación" (receta 7.4, sufijo Cie)

`recScrimCie` y `conPanelCie` van como hijos directos de la pantalla, DESPUÉS de
`conContenidoCie` y ANTES de `cmpNavCie`. Receta 7.4 fija: scrim
(`Fill = gblC.scrim` · `Visible = varPanelCie` · `OnSelect = Set(varPanelCie, false)`) y panel
(`Width = If(nfMovil, Parent.Width, Min(480, Parent.Width*0.45))` · `Height = Parent.Height` ·
`X = Parent.Width - Self.Width` · `Y = 0` · `Visible = varPanelCie` · ESTILO-Card con `Radius* = 0`).

Interior (X interno = `gblE.Pad`, ancho útil `Parent.Width - 2*gblE.Pad` salvo indicación):

| Control | Propiedad | Valor |
|---|---|---|
| `lblTituloPanCie` (Etiqueta clásica) | Y / H | `gblE.Pad` / `28` · Text `"Cerrar asignación"` · ESTILO-TituloPagina con `Size = 13` |
| `icoCerrarCie` (Icono, Cancel) | X / Y / W / H | `Parent.Width - 40 - gblE.PadSm` / `gblE.Pad` / `40` / `28` |
| | Color / OnSelect | `gblC.muted` / `Set(varPanelCie, false)` |
| `lblK1Cie`..`lblK4Cie` (Etiquetas clásicas) | X / W / H | `gblE.Pad` / `120` / `36` · Color `gblC.muted` |
| `lblV1Cie`..`lblV4Cie` (Etiquetas clásicas) | X / W / H | `gblE.Pad + 120` / `Parent.Width - 3*gblE.Pad - 120` / `36` · Align `Align.Right` · FontWeight `Semibold` |
| `recKvSep1Cie`..`recKvSep3Cie` (Rectángulos) | X / W / H | `gblE.Pad` / `Parent.Width - 2*gblE.Pad` / `1` · Fill `gblC.hairline` · Y = fin de cada fila KV |

La fila N de KV va en `Y = 56 + 36*(N-1)` (K y V comparten Y; el separador N en `Y = 56 + 36*N - 1`):

| N | K (Text) | V (Text) |
|---|---|---|
| 1 | `"Cliente"` | `varSelCie.clienteCie` |
| 2 | `"Cuenta"` | `varSelCie.'[AS] NumeroCuenta'` |
| 3 | `"Tipo"` | `If(varSelCie.'[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable, "Incobrable", "Especial")` |
| 4 | `"Saldo vencido"` | `Moneda(varSelCie.nioCie, varSelCie.usdCie)` — además `Color = gblC.teal`, `FontWeight = Bold` |

Sección motivo + detalle:

| Control | Propiedad | Valor |
|---|---|---|
| `lblSecMotivoCie` (Etiqueta clásica) | Y / H | `212` / `20` · Text `"MOTIVO DE CIERRE *"` · ESTILO-SecHead |
| `cmbMotivoCie` (Cuadro combinado) | Y / H | `236` / `gblE.AltoInput` · ESTILO-Input |
| | Items | `If(varSelCie.'[AS] Tipo' = 'Choice \| CM \| Tipo asignacion'.Incobrable, ["Recuperada", "Baja / castigada", "Fuera de cartera"], ["Gestión completada", "Ya no aplica", "Solicitud errónea"])` |
| | SelectMultiple / IsSearchable | `false` / `false` |
| | InputTextPlaceholder | `"— Elegir —"` |
| `lblSecDetalleCie` (Etiqueta clásica) | Y / H | `292` / `20` · Text `"DETALLE"` · ESTILO-SecHead |
| `txtDetalleCie` (Entrada de texto clásica) | Y / H | `316` / `84` · ESTILO-Input |
| | Mode / HintText | `TextMode.MultiLine` / `"Contexto del cierre (opcional)"` |

Pie de panel (dos botones, mitades iguales):

| Control | Propiedad | Valor |
|---|---|---|
| `btnCancelarCie` (Botón clásico) | X / Y / W | `gblE.Pad` / `Parent.Height - gblE.AltoBoton - gblE.Pad` / `(Parent.Width - 3*gblE.Pad) / 2` |
| | Text / estilo / OnSelect | `"Cancelar"` · ESTILO-BotonFantasma · `Set(varPanelCie, false)` |
| `btnCerrarCie` (Botón clásico) | X / Y / W | `btnCancelarCie.X + btnCancelarCie.Width + gblE.Pad` / `= btnCancelarCie.Y` / `= btnCancelarCie.Width` |
| | Text / estilo | `"🔒 Cerrar asignación"` · ESTILO-BotonPeligro |
| | DisplayMode | `If(!IsBlank(cmbMotivoCie.Selected), DisplayMode.Edit, DisplayMode.Disabled)` |
| | OnSelect | ↓ completo |

```powerfx
// 1 Patch delegable (LookUp por '[AS] Numero', texto exacto — 00 §4.2) + limpieza local: cero recargas
Patch('CM | TBL | Asignacions',
    LookUp('CM | TBL | Asignacions', '[AS] Numero' = varSelCie.'[AS] Numero'),
    {
        '[AS] Estado': false,
        '[AS] Motivo': cmbMotivoCie.Selected.Value &
            If(!IsBlank(Trim(txtDetalleCie.Text)), " — " & Trim(txtDetalleCie.Text), ""),
        'Fecha de cierre': Now()
    }
);
Remove(colCierresCie, LookUp(colCierresCie, '[AS] Numero' = varSelCie.'[AS] Numero'));
Set(varPanelCie, false);
Set(varSelCie, Blank());
Notify("🔒 Asignación cerrada — " & cmbMotivoCie.Selected.Value, NotificationType.Success, 2500)
```

> - El motivo elegido y el detalle opcional viajan JUNTOS en `'[AS] Motivo'`
>   (`"Baja / castigada — se agotaron instancias"`): no existe columna de detalle propia —
>   misma técnica que LED-11 (texto compuesto, columna nueva = backlog).
> - `'Fecha de cierre': Now()` lo escribe la app (no hay sello server-side para cierres; si un
>   día existe flujo de auditoría, se quita esta línea — mismo criterio que 00 §3.2).
> - **Divergencia declarada** (R1-09): el mockup permite tocar «Cerrar» sin motivo y responde
>   con toast de error; la guía deshabilita el botón hasta elegir motivo (validación preventiva,
>   mismo resultado).

## 5. Checklist de salida

- [ ] Con rol distinto de Admin, entrar rebota a `scrInicio` sin parpadeo de datos.
- [ ] Los tabs muestran conteos correctos y al alternar NO hay llamadas a Dataverse (Monitor).
- [ ] Clic en CUALQUIER punto de la fila (nombre, monto, chip) abre el panel del registro correcto.
- [ ] Los motivos del combo cambian según el tipo: Incobrable ≠ Especial.
- [ ] `btnCerrarCie` deshabilitado hasta elegir motivo; el detalle es opcional.
- [ ] Cerrar = 1 Patch en Monitor; la fila desaparece de la lista sin recargar; Notify de éxito.
- [ ] En Dataverse: `'[AS] Estado' = false`, `'Fecha de cierre'` con sello, `'[AS] Motivo'` con
      formato `"Motivo — detalle"` cuando hubo detalle.
- [ ] Reabrir el panel con otra fila NO arrastra motivo/detalle del cierre anterior (Resets).
- [ ] Fila Especial con retiro muestra el monto del retiro; sin retiro, el saldo vencido (REGLA-MONTO-CIE).
- [ ] Tab sin filas muestra "✨ Nada activo"; `↻ Actualizar` re-colecta.
- [ ] Cero banda azul de delegación en TODA la pantalla.
