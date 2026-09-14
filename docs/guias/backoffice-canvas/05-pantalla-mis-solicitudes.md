# Guía 05 — scrMisSolicitudes

Seguimiento de las asignaciones **Especiales** creadas por el Solicitante: lista con estado de
aprobación + panel de detalle en **solo lectura** (la resolución es del Supervisor, guía 07).
Mockup: `backoffice-canvas.html` → `scrMisSolicitudes()` y `openSol(id, ro=true)`.

**Contrato de entrada** — seteado ANTES de llegar acá:
- `gblRol`, `gblUsuarioDV`, `gblC`, `gblE`, `colNav` → los setea `App.OnStart` (guía 01 §4).
- `gblNavMovilAbierta` → estado del drawer móvil — la setea cmpEncabezado/cmpNavLateral (01 §8).
- `nfMovil` y `Moneda()` → `App.Formulas` (guía 01 §3).
- Esta pantalla no depende de variables de otras pantallas. `varPanelSol`, `colCtasSol` y
  `colSolSol` son suyas y las inicializa su `OnVisible` (§1.1).

## Árbol de controles

```
scrMisSolicitudes                 (ESTILO-Pantalla)
├── cmpEncSol                     cmpEncabezado (receta 7.5)
├── btnActualizarSol              "↻ Actualizar"
├── btnNuevaSol                   "✨ Nueva solicitud" (visible SIEMPRE)
├── conContenidoSol               Contenedor (transparente)
│   ├── conListaSol               Contenedor (ESTILO-Card, receta 7.1)
│   │   └── galSolSol             Galería vertical en blanco (recetas 7.3 + 7.11)
│   │       ├── recFilaSol        overlay clicable (7.3 — PRIMERO: al fondo del z-order)
│   │       ├── lblClienteFilaSol    cliente en MAYÚSCULAS (LED-01)
│   │       ├── lblCuentaFilaSol     nº de cuenta
│   │       ├── lblMetaFilaSol       "<AS Numero> · dd/mm HH:mm"
│   │       ├── lblNotasFilaSol      Notas truncadas (tipo + motivo, LED-11)
│   │       ├── lblMontoFilaSol      Moneda(retiros)
│   │       ├── chpEstadoFilaSol     chip de estado (7.2 — Switch en §2.1)
│   │       ├── chpAgenteFilaSol     chip "🏍️ <localizador>" (solo si ya tiene)
│   │       └── recSepSol         separador (7.11)
│   └── cmpVacioSol               cmpVacio (receta 7.12)
├── recScrimSol                   Rectángulo (receta 7.4)
├── conPanelSol                   Contenedor (receta 7.4 — detalle SOLO LECTURA)
│   ├── lblTituloPanSol           "Solicitud <AS Numero>"
│   ├── icoCerrarSol              Icono ✕
│   ├── lblSecCtaSol              "CUENTA"
│   ├── lblKCliSol / lblVCliSol   Cliente
│   ├── lblKCtaSol / lblVCtaSol   Cuenta · producto
│   ├── lblKSaldoSol / lblVSaldoSol  Saldo vencido
│   ├── lblKMoraSol / lblVMoraSol Días de mora
│   ├── lblSecSolSol              "SOLICITUD"
│   ├── lblKRetiroSol / lblVRetiroSol  Retiro pedido
│   ├── lblKFechaSol / lblVFechaSol    Solicitada
│   ├── conNotasPanSol            Contenedor card gris ── lblNotasPanSol (Notas completa)
│   ├── conRechSol                Contenedor card roja ── lblRechSol (solo Rechazada)
│   ├── lblKAgenteSol / lblVAgenteSol  Localizador (solo Aprobada)
│   └── btnCerrarPanSol           "Cerrar"
└── cmpNavSol                     cmpNavLateral (AL FINAL del árbol — 01 §8.1)
```

> Coordenadas dentro del template de galería: relativas al template (X template = X encabezado −
> X galería). Las Y internas del panel (§3) son la pila vertical documentada de esta guía.

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | fórmula §1.1 COMPLETA |

### 1.1 OnVisible (pegar completo)

```powerfx
If(!("Solicitante" in gblRol), Navigate(scrInicio));
// SIN Concurrent: el join de colSolSol necesita colCtasSol ya cargada (gotcha 7.7)
ClearCollect(colCtasSol, ShowColumns('CM | TBL | Cuentas',
    '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto',
    '[CT] Saldo Vencido NIO', '[CT] Saldo Vencido USD', '[CT] Dias Mora'));
// Mis Especiales — Filter delegable (choice por valor + lookup por registro, 00 §4.2);
// AddColumns y Sort corren EN MEMORIA sobre el resultado (cota 2000, 00 §4.1)
ClearCollect(colSolSol,
    Sort(
        AddColumns(
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
                && '[AS] Solicitado por' = gblUsuarioDV),
            clienteSol, Upper(Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente', "—")),
            prodSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Tipo Producto', "—"),
            saldoNioSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido NIO', 0),
            saldoUsdSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido USD', 0),
            moraSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Dias Mora', 0),
            agenteSol, '[AS] Agente'.'[AG] Nombre'
        ),
        '[AS] Fecha de Asignacion', SortOrder.Descending
    )
);
Set(varPanelSol, false)
```

### 1.2 Encabezado y botones de acción

`cmpEncSol` (cmpEncabezado): receta 7.5 tal cual — `X = If(nfMovil, 0, gblE.AnchoNav)` ·
`Y = If(nfMovil, gblE.AltoBarra, 0)` · `Width = Parent.Width - Self.X` · `Height = 84` ·
`Titulo = "Mis solicitudes"` · `Subtitulo = "Estado de tus asignaciones Especiales."`.

| Control | Propiedad | Valor |
|---|---|---|
| `btnNuevaSol` (Botón clásico) | Estilo | ESTILO-BotonPri |
| | Text / Width / Height | `"✨ Nueva solicitud"` / `170` / `gblE.AltoBoton` |
| | X / Y | `Parent.Width - Self.Width - gblE.Pad` / `cmpEncSol.Y + (84 - Self.Height)/2` |
| | OnSelect | `Navigate(scrNuevaSolicitud)` |
| | Visible | `true` (siempre — también con lista vacía) |
| `btnActualizarSol` (Botón clásico) | Estilo | ESTILO-BotonFantasma |
| | Text / Width / Height | `"↻ Actualizar"` / `130` / `gblE.AltoBoton` |
| | X / Y | `btnNuevaSol.X - Self.Width - gblE.PadSm` / `btnNuevaSol.Y` |
| | OnSelect | fórmula §1.3 COMPLETA |

### 1.3 btnActualizarSol.OnSelect (pegar completo — es el §1.1 sin guard ni estado de panel)

```powerfx
ClearCollect(colCtasSol, ShowColumns('CM | TBL | Cuentas',
    '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto',
    '[CT] Saldo Vencido NIO', '[CT] Saldo Vencido USD', '[CT] Dias Mora'));
ClearCollect(colSolSol,
    Sort(
        AddColumns(
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
                && '[AS] Solicitado por' = gblUsuarioDV),
            clienteSol, Upper(Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Nombre Cliente', "—")),
            prodSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'Tipo Producto', "—"),
            saldoNioSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido NIO', 0),
            saldoUsdSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Saldo Vencido USD', 0),
            moraSol, Coalesce(
                LookUp(colCtasSol, '[CT] Numero de cuenta' = '[AS] NumeroCuenta').'[CT] Dias Mora', 0),
            agenteSol, '[AS] Agente'.'[AG] Nombre'
        ),
        '[AS] Fecha de Asignacion', SortOrder.Descending
    )
);
Notify("✓ Lista actualizada", NotificationType.Success, 2500)
```

## 2. Lista de solicitudes

| Control | Propiedad | Valor |
|---|---|---|
| `conContenidoSol` (Contenedor) | X / Y | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado)* / `cmpEncSol.Y + 84` |
| | Width / Height | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* / `Parent.Height - Self.Y` |
| | Fill | `Transparent` |
| `conListaSol` (Contenedor) | Estilo | ESTILO-Card (receta 7.1) |
| | X / Y | `gblE.Pad` / `gblE.Pad` |
| | Width / Height | `Min(Parent.Width - 2*gblE.Pad, gblE.AnchoMax)` / `Parent.Height - 2*gblE.Pad` |
| `cmpVacioSol` (cmpVacio) | Visible | `CountRows(colSolSol) = 0` (receta 7.12) |
| | Icono / Titulo | `"🗂️"` / `"Sin solicitudes todavía"` |
| | Mensaje | `"Creá la primera con el botón «✨ Nueva solicitud», arriba a la derecha."` |
| | X / Y / Width | centrado en `conContenidoSol` (receta 7.12) |

### galSolSol (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X / Y | `gblE.PadSm` / `gblE.PadSm` |
| Width / Height | `Parent.Width - 2*gblE.PadSm` / `Parent.Height - 2*gblE.PadSm` |
| Items | `colSolSol` (ya viene ordenada por fecha descendente — §1.1) |
| TemplateSize / TemplatePadding | `If(nfMovil, 132, 84)` / `0` |
| TemplateFill | `If(ThisItem.IsSelected, gblC.tealSuave, gblC.card)` (receta 7.3) |
| OnSelect | `Set(varPanelSol, true)` |
| Visible | `CountRows(colSolSol) > 0` |

Template — TODO hijo lleva `OnSelect = Select(Parent)` (receta 7.3, sin excepción; los chips van
**SIN** `DisplayMode.View` — excepción de la receta 7.2 para chips dentro de fila clicable). Desktop en
3 bloques (izquierda / centro / derecha); móvil apilado con las fórmulas `If(nfMovil, …)` dadas:

| Control | Propiedad | Valor |
|---|---|---|
| `recFilaSol` (Rectángulo, PRIMERO: al fondo) | X/Y/W/H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` — `Fill = Transparent` · `OnSelect = Select(Parent)` (receta 7.3) |
| `lblClienteFilaSol` (Etiqueta clásica) | X / Y / W / H | `gblE.PadSm` / `6` / `If(nfMovil, Parent.TemplateWidth - 2*gblE.PadSm, (Parent.TemplateWidth - 2*gblE.PadSm) * 0.34)` / `18` |
| | Text / FontWeight | `ThisItem.clienteSol` / `FontWeight.Semibold` |
| `lblCuentaFilaSol` (Etiqueta clásica) | X / Y / W / H | `gblE.PadSm` / `26` / `lblClienteFilaSol.Width` / `16` |
| | Text / Size / Color | `ThisItem.'[AS] NumeroCuenta'` / `9` / `gblC.muted` |
| `lblMetaFilaSol` (Etiqueta clásica) | X / Y / W / H | `gblE.PadSm` / `44` / `lblClienteFilaSol.Width` / `16` |
| | Text | `ThisItem.'[AS] Numero' & " · " & Text(ThisItem.'[AS] Fecha de Asignacion', "dd/mm HH:mm")` |
| | Size / Color | `8` / `gblC.muted2` |
| `lblNotasFilaSol` (Etiqueta clásica) | X | `If(nfMovil, gblE.PadSm, gblE.PadSm + (Parent.TemplateWidth - 2*gblE.PadSm) * 0.36)` |
| | Y / W | `If(nfMovil, 64, 6)` / `(Parent.TemplateWidth - 2*gblE.PadSm) * If(nfMovil, 1, 0.32)` |
| | H / Wrap | `If(nfMovil, 34, Parent.TemplateHeight - 14)` / `true` |
| | Text | `If(Len(ThisItem.Notas) > 90, Left(ThisItem.Notas, 90) & "…", ThisItem.Notas)` |
| | Size / Color | `9` / `gblC.ink2` |
| `lblMontoFilaSol` (Etiqueta clásica) | X / Y | `If(nfMovil, gblE.PadSm, Parent.TemplateWidth - Self.Width - gblE.PadSm)` / `If(nfMovil, 100, 8)` |
| | W / H / Align | `220` / `18` / `If(nfMovil, Align.Left, Align.Right)` |
| | Text / FontWeight | `Moneda(ThisItem.'Retiro NIO', ThisItem.'Retiro USD')` / `FontWeight.Semibold` |
| `chpEstadoFilaSol` (Botón clásico) | Receta | 7.2 **SIN `DisplayMode.View`** (excepción 7.2 — chip dentro de fila clicable; Radius 8, Size 8, Bold, Height 26, `Width = 14 + 7*Len(Self.Text)`) |
| | X / Y | `Parent.TemplateWidth - Self.Width - gblE.PadSm` / `If(nfMovil, 98, 32)` |
| | Text / Fill / Color | fórmulas §2.1 |
| | OnSelect | `Select(Parent)` |
| `chpAgenteFilaSol` (Botón clásico) | Receta | 7.2, variante neutro (`Fill = gblC.hairline`, `Color = gblC.ink2`) — **SIN `DisplayMode.View`** (excepción 7.2) |
| | X / Y | `chpEstadoFilaSol.X - Self.Width - gblE.PadSm` / `chpEstadoFilaSol.Y` |
| | Text | `"🏍️ " & ThisItem.agenteSol` |
| | Visible | `!IsBlank(ThisItem.agenteSol)` |
| | OnSelect | `Select(Parent)` |
| `recSepSol` (Rectángulo) | X/Y/W/H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · `Fill = gblC.hairline` (receta 7.11) |

### 2.1 Chip de estado — fórmulas EXACTAS

El cierre (`'[AS] Estado' = false`) **precede** al estado de aprobación:

```powerfx
// chpEstadoFilaSol.Text
If(ThisItem.'[AS] Estado' = false, "Cerrada",
    Switch(ThisItem.'[AS] Estado de aprobacion',
        'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion', "Pendiente aprobación",
        'Choice | CM | Estado aprobacion especial'.Aprobado, "Aprobada",
        'Choice | CM | Estado aprobacion especial'.Rechazada, "Rechazada",
        'Choice | CM | Estado aprobacion especial'.Borrador, "Borrador",
        "—"))

// chpEstadoFilaSol.Fill                                  // chpEstadoFilaSol.Color (misma estructura)
If(ThisItem.'[AS] Estado' = false, gblC.hairline,      // → gblC.ink2
    Switch(ThisItem.'[AS] Estado de aprobacion',
        'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion', gblC.pendBg,   // → gblC.pendInk
        'Choice | CM | Estado aprobacion especial'.Aprobado, gblC.okBg,                   // → gblC.okInk
        'Choice | CM | Estado aprobacion especial'.Rechazada, gblC.redBg,                 // → gblC.redInk
        gblC.hairline))                                                                   // → gblC.ink2
```

## 3. Panel de detalle (solo lectura)

`recScrimSol` y `conPanelSol` siguen la receta 7.4 **tal cual** (posiciones, tamaños, `Visible =
varPanelSol`, cierre por scrim/✕), al final del árbol antes de `cmpNavSol`. El registro mostrado es
`galSolSol.Selected`. Internos — todos con `X = gblE.Pad` · `Width = Parent.Width - 2*gblE.Pad`
salvo indicación; pares K/V comparten fila: K `Width = 110` `Size = 9` `Color = gblC.muted`, V
`X = gblE.Pad + 110` `Width = Parent.Width - 2*gblE.Pad - 110` `Align = Align.Right`; Height 22:

| Control | Y | Text / propiedades |
|---|---|---|
| `lblTituloPanSol` (Etiqueta clásica) | `16` | `"Solicitud " & galSolSol.Selected.'[AS] Numero'` · ESTILO-TituloPagina Size 13 (receta 7.4) |
| `icoCerrarSol` (Icono ✕) | `16` | `X = Parent.Width - Self.Width - gblE.Pad` · `OnSelect = Set(varPanelSol, false)` |
| `lblSecCtaSol` (Etiqueta clásica) | `58` | `"CUENTA"` · ESTILO-SecHead |
| `lblKCliSol` / `lblVCliSol` (Etiquetas clásicas) | `80` | `"Cliente"` / `galSolSol.Selected.clienteSol` |
| `lblKCtaSol` / `lblVCtaSol` (Etiquetas clásicas) | `104` | `"Cuenta"` / `galSolSol.Selected.'[AS] NumeroCuenta' & " · " & galSolSol.Selected.prodSol` |
| `lblKSaldoSol` / `lblVSaldoSol` (Etiquetas clásicas) | `128` | `"Saldo vencido"` / `Moneda(galSolSol.Selected.saldoNioSol, galSolSol.Selected.saldoUsdSol)` · V en `FontWeight.Semibold` |
| `lblKMoraSol` / `lblVMoraSol` (Etiquetas clásicas) | `152` | `"Mora"` / `galSolSol.Selected.moraSol & " días"` |
| `lblSecSolSol` (Etiqueta clásica) | `190` | `"SOLICITUD"` · ESTILO-SecHead |
| `lblKRetiroSol` / `lblVRetiroSol` (Etiquetas clásicas) | `212` | `"Retiro pedido"` / `Moneda(galSolSol.Selected.'Retiro NIO', galSolSol.Selected.'Retiro USD')` · V en `FontWeight.Semibold` |
| `lblKFechaSol` / `lblVFechaSol` (Etiquetas clásicas) | `236` | `"Solicitada"` / `Text(galSolSol.Selected.'[AS] Fecha de Asignacion', "dd/mm HH:mm")` |
| `conNotasPanSol` (Contenedor) | `266` | `Height = 96` · `Fill = gblC.pantalla` · `Radius* = gblE.RadioSm` · sin borde |
| `lblNotasPanSol` (Etiqueta clásica) | — | dentro de `conNotasPanSol`: `X/Y = gblE.PadSm` · `Width = Parent.Width - 2*gblE.PadSm` · `Height = Parent.Height - 2*gblE.PadSm` · `Text = galSolSol.Selected.Notas` (completa — el tipo va antepuesto, LED-11) · `Wrap = true` · `Size = 9` |
| `conRechSol` (Contenedor) | `374` | `Height = 72` · `Fill = gblC.redBg` · `Radius* = gblE.RadioSm` · `Visible = galSolSol.Selected.'[AS] Estado de aprobacion' = 'Choice \| CM \| Estado aprobacion especial'.Rechazada` |
| `lblRechSol` (Etiqueta clásica) | — | dentro de `conRechSol`: `X/Y = gblE.PadSm` · `Width/Height = Parent.Width - 2*gblE.PadSm` / `Parent.Height - 2*gblE.PadSm` · `Text = "Motivo de rechazo: " & Coalesce(galSolSol.Selected.'Motivo de rechazo', "—")` · `Wrap = true` · `Size = 9` · `Color = gblC.redInk` |
| `lblKAgenteSol` / `lblVAgenteSol` (Etiquetas clásicas) | `374` | `"Localizador"` / `"🏍️ " & Coalesce(galSolSol.Selected.agenteSol, "—")` · ambos `Visible = galSolSol.Selected.'[AS] Estado de aprobacion' = 'Choice \| CM \| Estado aprobacion especial'.Aprobado` (comparte la franja Y con `conRechSol`: estados mutuamente excluyentes) |
| `btnCerrarPanSol` (Botón clásico) | `Parent.Height - gblE.AltoBoton - gblE.Pad` | ESTILO-BotonFantasma · `Text = "Cerrar"` · `Width = 120` · `Height = gblE.AltoBoton` · `OnSelect = Set(varPanelSol, false)` |

`cmpNavSol` (cmpNavLateral): instanciación estándar de 01 §8.1 — `RutaActiva = "sol"` ·
`Visible = !nfMovil || gblNavMovilAbierta` · `X = 0` · `Y = 0` · `Height = Parent.Height` ·
AL FINAL del árbol (después del panel 7.4).

## Notas

- **LED-01**: el cliente va en `Upper()` del nombre completo (columna horneada `clienteSol`).
- **LED-11**: no existe columna "tipo de especial"; viaja antepuesto en `'Notas'`
  (`"Retiro urgente — <motivo>"`). Por eso la fila muestra Notas truncadas (cubre el
  `num · tipo` + motivo del mockup) y el panel la muestra completa sin campo "Tipo" aparte.
- Delegación: solo el `Filter` interno de §1.1 va al datasource (choice por valor + lookup por
  registro — 00 §4.2). `agenteSol` se hornea con `AddColumns` DESPUÉS del filtro (en memoria),
  nunca navegando la relación dentro del `Filter` (00 §4.3). Cota 2000 declarada (00 §4.1).
- La galería clásica auto-selecciona la 1ª fila: no importa — el panel solo se ve con
  `varPanelSol = true`, que el OnVisible resetea a `false` en cada visita.
- Solo lectura deliberada: esta pantalla no tiene `Patch`. Rama `ro=true` de `openSol()` en el
  mockup; la resolución (aprobar/rechazar) vive en scrAprobaciones (guía 07).

## Checklist de salida

- [ ] Entrar con rol ≠ Solicitante rebota a scrInicio (guard §1.1).
- [ ] La lista muestra SOLO Especiales del usuario logueado, la más reciente arriba.
- [ ] Chip por estado: Pendiente aprobación (ámbar) · Aprobada (verde) · Rechazada (roja) ·
      Cerrada (neutra, y gana aunque haya estado de aprobación) — y chip 🏍️ solo si hay agente.
- [ ] Clic en CUALQUIER punto de la fila (texto incluido) abre el panel de la fila clicada.
- [ ] Panel: card roja con motivo SOLO en Rechazada; fila Localizador SOLO en Aprobada.
- [ ] "✨ Nueva solicitud" navega a scrNuevaSolicitud, con y sin filas en la lista.
- [ ] "↻ Actualizar" re-colecta y avisa; el panel no queda abierto sobre datos viejos.
- [ ] Sin filas: `cmpVacioSol` visible con la invitación a crear.
- [ ] Cero subrayados azules de delegación en Studio en toda la pantalla.
- [ ] Fechas en `dd/mm HH:mm` (24 h) — ningún `hh`.
