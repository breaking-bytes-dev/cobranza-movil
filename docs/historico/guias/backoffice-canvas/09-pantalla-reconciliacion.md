# Guía 09 — scrReconciliacion

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrReconciliacion.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - Sin guard de rol (LED-20); sin `colCtasRec` ni `colDiariasRec`: cliente por `ThisRecord.'[AS] Cuenta'.'Nombre Cliente'`.
> - **Zona horaria**: corte `varCorteRec` = 00:00 UTC (receta 7.15) en el filtro y los conteos — con `'Última carga' < nfUltimaCarga` el 100% salía desaparecido (00 §4.4).
> - Conteos `varNDesapRec/varNDiariasRec` con `CountIf` (denominador del umbral 30% exacto); control NUEVO `lblTopeRec` ("Mostrando las primeras N…") y `conResumenRec.Height` variable.
> - Fecha de la fila con `DateAdd(…, TimeZoneOffset(…))` guardada por `IsBlank` (template vacío).
> - `btnCerrarRec`: UN `Patch` masivo con tabla de registros con PK (receta 7.17), sin `LookUp` por fila.
> - **Umbral 30 % = alerta, no bloqueo (2026-10-25):** card roja `conUmbralRec`/`lblUmbralAlertaRec`, casilla y botón con texto y color de alerta; `varRojoRec` ya no deshabilita la casilla ni el botón.
> - `chkConfirmarRec` sin `Wrap` (PA2108) y texto nuevo; `galDesapRec` en `DisplayMode.View`, sin overlay.

Reconciliación de Diarias (DEC-018): lista las asignaciones **Diaria activas** cuya `'Última carga'`
quedó atrás de la última carga registrada (= dejaron de venir en el CSV) y permite al **Admin**
cerrarlas **en lote** con motivo `"fuera de cartera"` — con umbral de cordura del 30 % y checkbox
de confirmación. Mockup: función `scrReconciliacion()` + `recCerrar()` de `backoffice-canvas.html`.

**Contrato de entrada** (lo setea `App.OnStart` / `App.Formulas`, guía 01 §3–4):
`gblRol` (debe ser `"Admin"` — guard abajo) · `gblC` / `gblE` (tokens) · `nfUltimaCarga`
(named formula, fecha/hora de la última carga) · `nfMovil` · `colNav` · `gblNavMovilAbierta`
(estado del drawer móvil — la setea cmpEncabezado/cmpNavLateral, 01 §8). No recibe `gvar*`:
la pantalla arma sus colecciones en su propio OnVisible.

## Árbol de controles

```
scrReconciliacion                    (ESTILO-Pantalla)
├── cmpEncabezadoRec                 (cmpEncabezado — receta 7.5)
├── conContenidoRec                  (Contenedor, transparente — columna de contenido)
│   ├── conBloqueoRec                (Contenedor — card de BLOQUEO DEC-018, roja)
│   │   └── lblBloqueoRec            (Etiqueta clásica)
│   ├── conResumenRec                (Contenedor — card resumen + meter)
│   │   ├── lblResumenRec            (Etiqueta clásica)
│   │   ├── conMeterRec              (Contenedor — pista del meter)
│   │   │   └── recMeterRec          (Rectángulo — relleno proporcional al %)
│   │   └── lblUmbralRec             (Etiqueta clásica — leyenda del umbral)
│   ├── conListaRec                  (Contenedor — card de la lista)
│   │   ├── galDesapRec              (Galería vertical en blanco — ESTÁTICA)
│   │   │   ├── lblClienteRec        (Etiqueta clásica)
│   │   │   ├── lblCuentaRec         (Etiqueta clásica)
│   │   │   ├── lblAgenteRec         (Etiqueta clásica)
│   │   │   ├── chpDiariaRec         (Botón clásico — chip receta 7.2)
│   │   │   └── recSepRec            (Rectángulo — receta 7.11)
│   │   └── cmpVacioRec              (cmpVacio — receta 7.12)
│   └── conAccionRec                 (Contenedor — card de acción)
│       ├── chkConfirmarRec          (Casilla clásica — receta 7.10)
│       └── btnCerrarRec             (Botón clásico — ESTILO-BotonPeligro)
└── cmpNavRec                        (cmpNavLateral — al FINAL del árbol, 01 §8.1)
```

Alturas documentadas de esta guía (los únicos literales fuera de tokens):
card bloqueo **64** · card resumen **116** · meter **8** · card acción **156** ·
`TemplateSize = gblE.AltoFila` (64).

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |

**OnVisible completo** (orden de la receta 7.7: guard → colecciones → estado de pantalla).
El join de cliente exige `colCtasRec` ANTES del `AddColumns` → cuentas fuera del `Concurrent`
(⚠ de la receta 7.7); los dos filtros sobre Asignacions sí corren en paralelo:

```powerfx
If(!("Admin" in gblRol), Navigate(scrInicio));
// 1) Cuentas para el join en memoria (2 columnas, cota 2000 declarada — 00 §4.1)
ClearCollect(colCtasRec,
    ShowColumns('CM | TBL | Cuentas', '[CT] Numero de cuenta', 'Nombre Cliente')
);
// 2) Desaparecidas + denominador, en paralelo (ambos filtros delegables: choice por
//    valor + booleano + comparación de fecha — 00 §4.2)
Concurrent(
    ClearCollect(colDesapRec,
        AddColumns(
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Diaria
                && '[AS] Estado' = true
                && 'Última carga' < nfUltimaCarga
            ),
            clienteRec,
            Upper(Coalesce(
                LookUp(colCtasRec, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente',
                "—"
            ))
        )
    ),
    ClearCollect(colDiariasRec,
        ShowColumns(
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Diaria
                && '[AS] Estado' = true
            ),
            '[AS] Numero'
        )
    )
);
// 3) Estado de pantalla
Set(varPctRec, Round(100 * CountRows(colDesapRec) / Max(1, CountRows(colDiariasRec)), 0));
Set(varRojoRec, varPctRec > 30);
Reset(chkConfirmarRec)
```

> - `colDiariasRec` existe SOLO como denominador del %: 1 columna, cota **2000 declarada**
>   (00 §4.1). Si la cartera de Diarias activas superara 2000, el % se calcula sobre las 2000
>   primeras — aceptado: el umbral es de cordura, no contabilidad.
> - El `Upper()` corre en el `AddColumns` (memoria), jamás en el Filter (00 §4.3). Nombre
>   completo en mayúsculas, no shortname: **LED-01**.
> - `Reset(chkConfirmarRec)` es obligatorio: el estado de la casilla PERSISTE entre visitas
>   (gotcha de 01) — sin reset, un cierre confirmado ayer deja el botón armado hoy.

`cmpEncabezadoRec` — receta 7.5: `X = If(nfMovil, 0, gblE.AnchoNav)` · `Y = If(nfMovil, gblE.AltoBarra, 0)`
· `Width = Parent.Width - Self.X` · `Height = 84` · `Titulo = "Reconciliación de Diarias"` ·
`Subtitulo = "Cuentas Diarias activas que NO vinieron en la última carga."`

`cmpNavRec` — instancia de `cmpNavLateral` según 01 §8.1: `RutaActiva = "rec"` ·
`Visible = !nfMovil || gblNavMovilAbierta` · `X = 0` · `Y = 0` · `Height = Parent.Height` — va al
FINAL del árbol para flotar encima en móvil.

## 2. Columna de contenido

| Control | Propiedad | Valor |
|---|---|---|
| `conContenidoRec` (Contenedor) | X / Y | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* / `cmpEncabezadoRec.Y + cmpEncabezadoRec.Height` |
| | Width / Height | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* / `Parent.Height - Self.Y` |
| | Fill | `Transparent` |

Todas las cards hijas comparten: `X = gblE.Pad` · `Width = Min(gblE.AnchoMax, Parent.Width - 2*gblE.Pad)`.

## 3. Card de BLOQUEO — precondición DEC-018

La reconciliación compara contra la carga de HOY: correrla ANTES del import marcaría TODA la
cartera como desaparecida. Si `nfUltimaCarga` es anterior a hoy, la pantalla se bloquea.

| Control | Propiedad | Valor |
|---|---|---|
| `conBloqueoRec` (Contenedor) | X / Y / Width | regla §2 · `Y = gblE.PadSm` |
| | Height | `64` |
| | Fill / BorderColor / BorderThickness | `gblC.redBg` / `gblC.redInk` / `1` |
| | Radius (los 4) | `gblE.Radio` |
| | **Visible** | `nfUltimaCarga < Today()` |
| `lblBloqueoRec` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `0` / `Parent.Width - 2*gblE.Pad` / `Parent.Height` |
| | Text | `"⛔ Todavía no llegó la carga de HOY — la reconciliación se corre DESPUÉS del import."` |
| | Color / FontWeight / Wrap | `gblC.redInk` / `Semibold` / `true` |

Cuando esta card está visible, `chkConfirmarRec` y `btnCerrarRec` quedan **Disabled** — la
condición `nfUltimaCarga < Today()` está sumada a sus DisplayMode (§6), no es solo visual.

## 4. Card RESUMEN — conteo, meter y umbral

| Control | Propiedad | Valor |
|---|---|---|
| `conResumenRec` (Contenedor) | X / Width | regla §2 |
| | Y | `conBloqueoRec.Y + If(conBloqueoRec.Visible, conBloqueoRec.Height + gblE.PadSm, 0)` |
| | Height | `116` |
| | ESTILO-Card, salvo: BorderColor | `If(varRojoRec, gblC.redInk, gblC.borde)` *(mockup: clase `alertcard` en rojo)* |
| `lblResumenRec` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `24` |
| | Text | `CountRows(colDesapRec) & " de " & CountRows(colDiariasRec) & " Diarias activas (" & varPctRec & "%) quedaron fuera de la cartera"` |
| | Size / FontWeight | `11` / `Bold` |
| `conMeterRec` (Contenedor) | X / Y / Width / Height | `gblE.Pad` / `lblResumenRec.Y + lblResumenRec.Height + gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `8` |
| | Fill / Radius (los 4) | `gblC.hairline` / `99` |
| `recMeterRec` (Rectángulo) | X / Y / Height | `0` / `0` / `8` |
| | **Width** | `Parent.Width * Min(varPctRec, 100) / 100` |
| | Fill / Radius (los 4) | `If(varRojoRec, gblC.redInk, gblC.teal)` / `99` |
| `lblUmbralRec` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `conMeterRec.Y + conMeterRec.Height + gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `34` |
| | Text | `If(varRojoRec, "⛔ Umbral superado (>30%). Esto huele a archivo truncado — NO cierres en lote. Verificá la carga primero.", "Umbral de cordura: si superara el 30%, se bloquea el cierre en lote.")` |
| | Color / Size / Wrap | `If(varRojoRec, gblC.redInk, gblC.muted)` / `9` / `true` |

## 5. Card LISTA — Diarias desaparecidas

| Control | Propiedad | Valor |
|---|---|---|
| `conListaRec` (Contenedor) | X / Width | regla §2 · ESTILO-Card |
| | Y | `conResumenRec.Y + conResumenRec.Height + gblE.PadSm` |
| | Height | `Parent.Height - Self.Y - If(CountRows(colDesapRec) > 0, 156 + gblE.PadSm, 0) - gblE.Pad` |

### galDesapRec (Galería vertical en blanco)

Lista **estática** (receta 7.3 sin acción): acá no se navega a nada — la fila informa, el cierre
es en lote. `DisplayMode = View` mata el hover/prensa de fila clicable.

| Propiedad | Valor |
|---|---|
| X / Y / Width / Height | `0` / `0` / `Parent.Width` / `Parent.Height` |
| Items | `colDesapRec` |
| TemplateSize | `gblE.AltoFila` |
| **DisplayMode** | `DisplayMode.View` |
| TemplatePadding | `0` |

Template (coordenadas relativas al template):

| Control | Propiedad | Valor |
|---|---|---|
| `lblClienteRec` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `10` / `Parent.TemplateWidth * 0.4` / `22` |
| | Text / FontWeight | `ThisItem.clienteRec` / `Semibold` |
| `lblCuentaRec` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad` / `32` / `Parent.TemplateWidth * 0.4` / `20` |
| | Text / Size / Color | `ThisItem.'[AS] NumeroCuenta'` / `9` / `gblC.muted` |
| `lblAgenteRec` (Etiqueta clásica) | X / Y / Width / Height | `gblE.Pad + Parent.TemplateWidth * 0.4` / `10` / `Parent.TemplateWidth * 0.4` / `44` |
| | Text | `"🏍️ " & Coalesce(ThisItem.'[AS] Agente'.'[AG] Nombre', "sin localizador") & " · última carga " & Text(ThisItem.'Última carga', "dd/mm")` |
| | Size / Color / Wrap | `9` / `gblC.ink2` / `true` |
| `chpDiariaRec` (Botón clásico — receta 7.2, variante Diaria) | X / Y | `Parent.TemplateWidth - Self.Width - gblE.Pad` / `(Parent.TemplateHeight - Self.Height) / 2` |
| | Text / Fill / Color | `"Diaria"` / `gblC.diariaBg` / `gblC.diariaInk` |
| `recSepRec` (Rectángulo — receta 7.11) | X / Y / Width / Height | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · Fill `gblC.hairline` |

> - Fecha corta `dd/mm` en vez del "hace 2 días" del mockup: **LED-06**.
> - `ThisItem.'[AS] Agente'.'[AG] Nombre'`: la colección conserva el lookup expandido con su
>   campo primario. Si al probar viniera en blanco (el player no siempre hidrata lookups
>   coleccionados), hornealo en el `AddColumns` del OnVisible como columna `agenteRec` —
>   mismo patrón que `clienteRec` — y cambiá este Text a `ThisItem.agenteRec`.

### Estado vacío

`cmpVacioRec` — instancia de `cmpVacio` (receta 7.12), centrado en `conListaRec`:
`Icono = "✨"` · `Titulo = "Cartera cuadrada"` · `Mensaje = "Todas las Diarias activas vinieron en la carga."`
· `Visible = CountRows(colDesapRec) = 0`.

## 6. Card de ACCIÓN — confirmar y cerrar en lote

| Control | Propiedad | Valor |
|---|---|---|
| `conAccionRec` (Contenedor) | X / Width | regla §2 · ESTILO-Card |
| | Y | `conListaRec.Y + conListaRec.Height + gblE.PadSm` |
| | Height | `156` |
| | **Visible** | `CountRows(colDesapRec) > 0` |
| `chkConfirmarRec` (Casilla clásica — receta 7.10) | X / Y / Width | `gblE.Pad` / `gblE.PadSm` / `Parent.Width - 2*gblE.Pad` |
| | Height / Wrap / Size | `48` / `true` / `10` |
| | Text | `"Confirmo que revisé la lista y que la carga de hoy fue correcta. Cerrar estas asignaciones con motivo ""fuera de cartera""."` |
| | **DisplayMode** | `If(varRojoRec \|\| nfUltimaCarga < Today(), DisplayMode.Disabled, DisplayMode.Edit)` |
| `btnCerrarRec` (Botón clásico — ESTILO-BotonPeligro) | X / Y / Width / Height | `gblE.Pad` / `chkConfirmarRec.Y + chkConfirmarRec.Height + gblE.PadSm` / `Parent.Width - 2*gblE.Pad` / `gblE.AltoBoton` |
| | Text | `"🧹 Cerrar " & CountRows(colDesapRec) & " asignaciones en lote"` |
| | **DisplayMode** | `If(chkConfirmarRec.Value && !varRojoRec && nfUltimaCarga >= Today(), DisplayMode.Edit, DisplayMode.Disabled)` |

**btnCerrarRec.OnSelect completo** (el `Notify` va ANTES de recargar — usa el conteo previo):

```powerfx
ForAll(colDesapRec As D,
    Patch('CM | TBL | Asignacions',
        LookUp('CM | TBL | Asignacions', '[AS] Numero' = D.'[AS] Numero'),
        {
            '[AS] Estado': false,
            '[AS] Motivo': "fuera de cartera",
            'Fecha de cierre': Now()
        }
    )
);
Notify("🧹 " & CountRows(colDesapRec) & " Diarias cerradas — motivo: fuera de cartera",
    NotificationType.Success, 2500);
// Recarga — mismo bloque 2)+3) del OnVisible (colCtasRec sigue viva, no se re-pide)
Concurrent(
    ClearCollect(colDesapRec,
        AddColumns(
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Diaria
                && '[AS] Estado' = true
                && 'Última carga' < nfUltimaCarga
            ),
            clienteRec,
            Upper(Coalesce(
                LookUp(colCtasRec, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente',
                "—"
            ))
        )
    ),
    ClearCollect(colDiariasRec,
        ShowColumns(
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Diaria
                && '[AS] Estado' = true
            ),
            '[AS] Numero'
        )
    )
);
Set(varPctRec, Round(100 * CountRows(colDesapRec) / Max(1, CountRows(colDiariasRec)), 0));
Set(varRojoRec, varPctRec > 30);
Reset(chkConfirmarRec)
```

> **Costo declarado**: son **N `Patch` secuenciales** (uno por Diaria desaparecida) — aceptable
> para las decenas que el umbral del 30 % permite pasar. Si algún día el lote legítimo fuera de
> CIENTOS, esto se muda a un flow/acción server-side que reciba la lista y cierre en una
> transacción — no se "optimiza" en canvas con trucos.

## 7. Checklist de salida

- [ ] Entrar como no-Admin (o forzar `gblRol`): rebota a `scrInicio` sin destello de datos.
- [ ] Con `nfUltimaCarga` de HOY y desaparecidas > 0: card ⛔ oculta, resumen con `N de M (pct%)`,
      meter proporcional, casilla habilitada; marcarla enciende el botón peligro.
- [ ] Con `nfUltimaCarga` de AYER (probar antes del import): card ⛔ visible y casilla + botón
      **Disabled** aunque el % sea bajo.
- [ ] Simular % > 30 (achicar el denominador en Studio): borde y leyenda en rojo, meter rojo,
      casilla Disabled → el botón no se puede armar.
- [ ] Cerrar en lote: Notify con el conteo, la lista queda vacía → `cmpVacioRec` "✨ Cartera
      cuadrada" y la card de acción desaparece; en Dataverse las filas quedan con
      `'[AS] Estado' = false`, `'[AS] Motivo' = "fuera de cartera"` y `'Fecha de cierre'` de hoy.
- [ ] Volver a entrar a la pantalla: la casilla arranca SIEMPRE desmarcada (`Reset` del OnVisible).
- [ ] Cero subrayado azul de delegación en el OnVisible y el OnSelect.
- [ ] Ventana < 920 px: nav colapsa a hamburguesa y las cards ocupan el ancho completo.
