# Guía 02 — scrInicio

> ⚠ **Esta guía es el diseño original; el código vigente (validado en Studio) está en `apps/backoffice-canvas/Src/scrInicio.pa.yaml`.** Diferencias principales (ver 00 §2/§4 y 01 §7/§11):
> - KPIs sin colecciones: `CountIf` directo al datasource (`varNPendIni`, `varNPagoIni`, `varNTscIni`…); ya no existen `colPendIni/colTscIni/colSolIni/colAprobIni/colDesapIni/colAgIni` (00 §4.2, receta 7.16).
> - "Cobrado hoy": `Sum` delegable directo sobre `'CM | TBL | Pagos'` por moneda (`varCobNioIni/varCobUsdIni`), sin `colPagosHoyIni`.
> - Agentes sin usuario: `'Email Usuario' = Blank()` (texto) en vez de `IsBlank('[AG] Usuario')` (lookup, no delega).
> - Lookups comparados por PK (`'[IN] Grabado por'.User = gblUsuarioDV.User`, `'[AS] Solicitado por'.User = …`).
> - Desaparecidas con corte UTC `varCorteIni` (receta 7.15), no `'Última carga' < nfUltimaCarga`.
> - Accesos de las cards: overlay `btnAcc<Tag>Ini` (último hijo, `TabIndex 0`, foco visible, `Text = lblAcc<Tag>TitIni.Text`) — receta 7.3. Mensaje de "sin acceso" menciona rol `SR | CM | …` / team.

Pantalla de inicio de **todos los roles**: saludo + total cobrado hoy, fila de KPIs del rol y
cards de acceso rápido a las pantallas del rol. Es el `App.StartScreen` (guía 01 §4) y la única
pantalla **sin guard de navegación**: acá el control de acceso es visual (grupos por `gblRol`) y
el estado "sin acceso" para usuarios sin fila en `Usuario Apps`.

**Contrato de entrada** (todo lo setea App, guía 01 §3–4 — esta pantalla no depende de ninguna otra):

| Debe venir seteado | Quién lo setea |
|---|---|
| `gblC`, `gblE` (tokens) · `gblUsuarioDV` · `gblFilaRol` · `gblRol` · `colNav` · `gblNavMovilAbierta` | `App.OnStart` (01 §4) |
| `nfMovil` · `nfUltimaCarga` · `Moneda()` | `App.Formulas` (01 §3) |

Ruta de navegación de esta pantalla: `"ini"` (se pasa a `cmpNavLateral.RutaActiva`).

## Árbol de controles

```
scrInicio                                  ESTILO-Pantalla
├── cmpEncIni                              cmpEncabezado — saludo + cobrado hoy
├── conContenidoIni                        Contenedor — todo el contenido con rol
│   ├── conKpisBoIni                       Contenedor — KPIs Backoffice
│   │   ├── cmpKpiColaIni                  cmpKpi hero "EN COLA"
│   │   ├── cmpKpiTscIni                   cmpKpi "TRANSCRITAS HOY"
│   │   └── cmpKpiPagoIni                  cmpKpi "CON PAGO"
│   ├── conKpisSolIni                      Contenedor — KPIs Solicitante
│   │   ├── cmpKpiSpenIni                  cmpKpi hero "PENDIENTES"
│   │   ├── cmpKpiSaprIni                  cmpKpi "APROBADAS"
│   │   └── cmpKpiSrecIni                  cmpKpi "RECHAZADAS"
│   ├── conKpisSupIni                      Contenedor — KPIs Supervisor
│   │   └── cmpKpiAprbIni                  cmpKpi hero "POR APROBAR"
│   ├── conKpisAdmIni                      Contenedor — KPIs Admin
│   │   ├── cmpKpiCargaIni                 cmpKpi hero "CARGA DE HOY"
│   │   ├── cmpKpiDesapIni                 cmpKpi "DIARIAS FUERA DE CARTERA"
│   │   └── cmpKpiAgeIni                   cmpKpi "AGENTES SIN USUARIO"
│   ├── lblSecAccIni                       Etiqueta clásica "ACCESOS"
│   ├── conAccBoIni                        Contenedor — accesos Backoffice
│   │   ├── conAccColaIni                  card → lblAccColaIcoIni · lblAccColaTitIni · lblAccColaDesIni · btnAccColaIni
│   │   └── conAccArqIni                   card → lblAccArqIcoIni · lblAccArqTitIni · lblAccArqDesIni · btnAccArqIni
│   ├── conAccSolIni                       Contenedor — accesos Solicitante
│   │   ├── conAccNvaIni                   card → lblAccNvaIcoIni · lblAccNvaTitIni · lblAccNvaDesIni · btnAccNvaIni
│   │   └── conAccMisIni                   card → lblAccMisIcoIni · lblAccMisTitIni · lblAccMisDesIni · btnAccMisIni
│   ├── conAccSupIni                       Contenedor — accesos Supervisor
│   │   └── conAccAprbIni                  card → lblAccAprbIcoIni · lblAccAprbTitIni · lblAccAprbDesIni · btnAccAprbIni
│   └── conAccAdmIni                       Contenedor — accesos Admin
│       ├── conAccMonIni                   card → lblAccMonIcoIni · lblAccMonTitIni · lblAccMonDesIni · btnAccMonIni
│       ├── conAccRecIni                   card → lblAccRecIcoIni · lblAccRecTitIni · lblAccRecDesIni · btnAccRecIni
│       └── conAccAgeIni                   card → lblAccAgeIcoIni · lblAccAgeTitIni · lblAccAgeDesIni · btnAccAgeIni
├── cmpVacioIni                            cmpVacio — estado "sin acceso"
└── cmpNavIni                              cmpNavLateral (al FINAL del árbol — 01 §8.1)
```

Alturas documentadas de esta guía (únicos literales además de los tokens `gblE.*`): **96** (alto
fijo de `cmpKpi`, 01 §8.3) · **104** (alto de card de acceso) · **84** (encabezado, receta 7.5) ·
alturas internas de etiqueta de card (28/22/36) · **200/420** (caja de `cmpVacioIni`).

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | fórmula §1.1 completa |

### 1.1 scrInicio.OnVisible (pegar completo)

Orden de la receta 7.7: colecciones → estado. Sin guard de rol (pantalla de todos). Solo se
cargan las colecciones del **rol activo**; el `If` va SIEMPRE POR FUERA del `ClearCollect`
(gotcha de 00 §4.3 — `ClearCollect(col, If(cond, ...))` sin else revienta).

```powerfx
// 1) Cobrado hoy por los localizadores (subtítulo del encabezado) — común a todos los roles.
//    Filtro delegable por fecha (00 §4.2); cota 2000 declarada (00 §4.1).
If(!IsBlank(gblRol),
    ClearCollect(colPagosHoyIni,
        Filter('CM | TBL | Pagos', 'Fecha del pago' >= Today()));
    // Los Sum van sobre la colección, EN MEMORIA — jamás Sum directo al datasource (00 §4.3).
    // Por eso NO van en Concurrent con el ClearCollect: dependen de él (secuencial con ;).
    Set(varCobNioIni,
        Sum(Filter(colPagosHoyIni, '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.NIO), '[PG] Monto'));
    Set(varCobUsdIni,
        Sum(Filter(colPagosHoyIni, '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.USD), '[PG] Monto'))
);
// 2) Backoffice: cola pendiente + transcritas hoy por mí (independientes → Concurrent).
If("Backoffice" in gblRol,
    Concurrent(
        // Delegable: igualdad de choice (00 §4.2). Cota 2000 declarada (00 §4.1).
        ClearCollect(colPendIni,
            Filter('CM | TBL | Interaccions',
                '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Pendiente)),
        // Delegable: choice + fecha + lookup por registro (00 §4.2).
        ClearCollect(colTscIni,
            Filter('CM | TBL | Interaccions',
                '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Grabado
                && '[IN] Fecha grabacion' >= Today()
                && '[IN] Grabado por' = gblUsuarioDV))
    )
);
// 3) Solicitante: TODAS mis Especiales (los 3 KPIs se cuentan en memoria sobre esta colección).
If("Solicitante" in gblRol,
    ClearCollect(colSolIni,
        Filter('CM | TBL | Asignacions',
            '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
            && '[AS] Solicitado por' = gblUsuarioDV))
);
// 4) Supervisor: Especiales pendientes de aprobación (cota real: unidades — 00 §4.1).
If("Supervisor" in gblRol,
    ClearCollect(colAprobIni,
        Filter('CM | TBL | Asignacions',
            '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Especial
            && '[AS] Estado de aprobacion' = 'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion'))
);
// 5) Admin: diarias fuera de cartera + agentes (independientes → Concurrent).
If("Admin" in gblRol,
    Concurrent(
        // Delegable: choice + booleano + comparación de fecha (00 §4.2, patrón colDesap de 00 §4.1).
        ClearCollect(colDesapIni,
            Filter('CM | TBL | Asignacions',
                '[AS] Tipo' = 'Choice | CM | Tipo asignacion'.Diaria
                && '[AS] Estado' = true
                && 'Última carga' < nfUltimaCarga)),
        // Tabla chica (decenas de filas, org completa): se trae entera.
        ClearCollect(colAgIni, 'CM | TBL | Agentes')
    )
)
```

## 2. Estado "sin acceso" — `cmpVacioIni`

Si el usuario no tiene fila activa en `'CM | TBL | Usuario Apps'`, `gblRol` queda en blanco
(01 §4) y la pantalla entera se reduce a este componente. Instancia de `cmpVacio` (01 §8.4):

| Propiedad | Valor |
|---|---|
| Icono | `"🔐"` |
| Titulo | `"Sin acceso"` |
| Mensaje | `"Tu usuario no tiene rol asignado en Cobranza Móvil. Pedile al Admin que te agregue en Usuario App."` |
| X / Y | `(Parent.Width - Self.Width) / 2` / `(Parent.Height - Self.Height) / 2` |
| Width / Height | `Min(420, Parent.Width - 2*gblE.Pad)` / `200` |
| Visible | `IsBlank(gblRol)` |

## 3. Encabezado — `cmpEncIni`

Instancia de `cmpEncabezado` según receta 7.5 (X/Y/Width/Height exactos de la receta), más:

| Propiedad | Valor |
|---|---|
| Titulo | `"Hola, " & First(Split(User().FullName, " ")).Value` |
| Subtitulo | `"Cobrado hoy por los localizadores: " & Moneda(varCobNioIni, varCobUsdIni)` |
| Visible | `!IsBlank(gblRol)` |

La barra móvil con hamburguesa vive DENTRO del componente (01 §8.2) — al ocultarse el encabezado
en el estado sin acceso, tampoco hay hamburguesa: correcto, no hay nada que navegar.

## 4. Contenido — `conContenidoIni`

`Contenedor` transparente que agrupa KPIs y accesos (todo lo que exige rol):

| Propiedad | Valor |
|---|---|
| X / Y | `If(nfMovil, 0, gblE.AnchoNav)` / `cmpEncIni.Y + cmpEncIni.Height` |
| Width / Height | `Parent.Width - Self.X` / `Parent.Height - Self.Y` |
| Fill | `Color.Transparent` |
| Visible | `!IsBlank(gblRol)` |

### 4.1 Contenedores de KPIs por rol — ⚠ ADENDA MULTI-ROL: se APILAN, ya no se superponen

**La tabla `Usuario App` real permite VARIOS roles por persona** (00 §3.1): un usuario con
`"Backoffice|Admin"` ve LOS DOS grupos de KPIs, uno debajo del otro. Los cuatro contenedores
comparten X/Width/Height/Fill, pero la **Y se encadena** (cada grupo empieza donde terminó el
anterior visible; un grupo invisible aporta 0):

| Propiedad | Valor |
|---|---|
| X (los 4) | `Max(gblE.Pad, (Parent.Width - gblE.AnchoMax) / 2)` |
| Width (los 4) | `Min(Parent.Width - 2*gblE.Pad, gblE.AnchoMax)` |
| Height (los 4) | `If(nfMovil, 2*96 + gblE.PadSm, 96)` |
| Fill (los 4) | `Color.Transparent` |
| Y `conKpisBoIni` | `gblE.PadSm` |
| Y `conKpisSolIni` | `conKpisBoIni.Y + If(conKpisBoIni.Visible, conKpisBoIni.Height + gblE.PadSm, 0)` |
| Y `conKpisSupIni` | `conKpisSolIni.Y + If(conKpisSolIni.Visible, conKpisSolIni.Height + gblE.PadSm, 0)` |
| Y `conKpisAdmIni` | `conKpisSupIni.Y + If(conKpisSupIni.Visible, conKpisSupIni.Height + gblE.PadSm, 0)` |
| Visible | Bo: `"Backoffice" in gblRol` · Sol: `"Solicitante" in gblRol` · Sup: `"Supervisor" in gblRol` · Adm: `"Admin" in gblRol` |

> En móvil el contenedor mide 2 filas aunque el rol tenga ≤ 2 KPIs (Supervisor): el espacio
> sobrante es invisible y a cambio TODOS los grupos comparten fórmula — decisión deliberada.
> **La sección 5 (Accesos) hereda el mismo patrón**: `lblSecAccIni.Y` cuelga de
> `conKpisAdmIni.Y + If(conKpisAdmIni.Visible, conKpisAdmIni.Height + gblE.PadSm, 0)`, y los
> contenedores de accesos por rol (§5.2) encadenan sus Y igual que estos cuatro. Aplica también
> al `Height` del scroll de `conContenidoIni` si la guía lo fija.

### 4.2 Grid responsive de KPIs — slots P0/P1/P2 (4 col escritorio → 2 col móvil)

Cada instancia de `cmpKpi` toma **Height `96`**, este Width común, y el X/Y de su slot:

```powerfx
// Width (común a todos los KPIs):
If(nfMovil, (Parent.Width - gblE.PadSm) / 2, (Parent.Width - 3*gblE.PadSm) / 4)
```

| Slot | X | Y |
|---|---|---|
| **P0** | `0` | `0` |
| **P1** | `If(nfMovil, (Parent.Width - gblE.PadSm)/2 + gblE.PadSm, (Parent.Width - 3*gblE.PadSm)/4 + gblE.PadSm)` | `0` |
| **P2** | `If(nfMovil, 0, 2*((Parent.Width - 3*gblE.PadSm)/4 + gblE.PadSm))` | `If(nfMovil, 96 + gblE.PadSm, 0)` |

En escritorio la 4ª columna queda vacía (ningún rol tiene 4 KPIs en v1 — ver Notas).

### 4.3 Instancias de `cmpKpi` (todas: Height `96`, Width §4.2, X/Y del slot)

| Control | Padre | Slot | EsHero | Etiqueta | Valor | Detalle |
|---|---|---|---|---|---|---|
| `cmpKpiColaIni` | `conKpisBoIni` | P0 | `true` | `"EN COLA"` | `Text(CountRows(colPendIni))` | `"interacciones por transcribir"` |
| `cmpKpiTscIni` | `conKpisBoIni` | P1 | `false` | `"TRANSCRITAS HOY"` | `Text(CountRows(colTscIni))` | `"por vos"` |
| `cmpKpiPagoIni` | `conKpisBoIni` | P2 | `false` | `"CON PAGO"` | `Text(CountRows(Filter(colPendIni, '[IN] Aplica pago')))` | `"priorizá estas"` |
| `cmpKpiSpenIni` | `conKpisSolIni` | P0 | `true` | `"PENDIENTES"` | `Text(CountRows(Filter(colSolIni, '[AS] Estado de aprobacion' = 'Choice | CM | Estado aprobacion especial'.'Pendiente aprobacion')))` | `"esperando aprobación"` |
| `cmpKpiSaprIni` | `conKpisSolIni` | P1 | `false` | `"APROBADAS"` | `Text(CountRows(Filter(colSolIni, '[AS] Estado de aprobacion' = 'Choice | CM | Estado aprobacion especial'.Aprobado)))` | `"en gestión de campo"` |
| `cmpKpiSrecIni` | `conKpisSolIni` | P2 | `false` | `"RECHAZADAS"` | `Text(CountRows(Filter(colSolIni, '[AS] Estado de aprobacion' = 'Choice | CM | Estado aprobacion especial'.Rechazada)))` | `"revisá el motivo"` |
| `cmpKpiAprbIni` | `conKpisSupIni` | P0 | `true` | `"POR APROBAR"` | `Text(CountRows(colAprobIni))` | `"solicitudes Especiales"` |
| `cmpKpiCargaIni` | `conKpisAdmIni` | P0 | `true` | `"CARGA DE HOY"` | `If(IsBlank(nfUltimaCarga) || nfUltimaCarga < Today(), "sin carga", Text(nfUltimaCarga, "dd/mm HH:mm"))` | `"última corrida del import — detalle en Monitoreo"` |
| `cmpKpiDesapIni` | `conKpisAdmIni` | P1 | `false` | `"DIARIAS FUERA DE CARTERA"` | `Text(CountRows(colDesapIni))` | `"salieron del archivo — ver Reconciliación"` |
| `cmpKpiAgeIni` | `conKpisAdmIni` | P2 | `false` | `"AGENTES SIN USUARIO"` | `Text(CountRows(Filter(colAgIni, '[AG] Activo' && IsBlank('[AG] Usuario'))))` | `"no pueden entrar a la app"` |

Los `Filter`/`CountRows` de la columna Valor van SIEMPRE sobre colecciones — cero delegación
(00 §4.3). El formato de fecha del KPI de carga usa `HH` (24 h), regla 0.10.

## 5. Sección "Accesos"

### 5.1 `lblSecAccIni` (Etiqueta clásica)

| Propiedad | Valor |
|---|---|
| Text | `"ACCESOS"` — ESTILO-SecHead |
| X / Y | `conKpisBoIni.X` / `conKpisBoIni.Y + conKpisBoIni.Height + gblE.Pad` |
| Width / Height | `200` / `18` |

### 5.2 Contenedores de accesos por rol

Igual patrón que §4.1: `conAccBoIni` fija todo; `conAccSolIni`, `conAccSupIni` y `conAccAdmIni`
idénticos salvo `Visible` (mismos valores por rol que §4.1):

| Propiedad | `conAccBoIni` |
|---|---|
| X / Y | `conKpisBoIni.X` / `lblSecAccIni.Y + lblSecAccIni.Height + gblE.PadSm` |
| Width | `conKpisBoIni.Width` |
| Height | `If(nfMovil, 3*104 + 2*gblE.PadSm, 104)` |
| Fill | `Color.Transparent` |
| Visible | `"Backoffice" in gblRol` (Sol/Sup/Adm: su rol) |

### 5.3 Receta local: card de acceso (grid 3 col escritorio → apilado móvil)

Cada card es un `Contenedor` ESTILO-Card con **Height `104`** y este Width común:

```powerfx
If(nfMovil, Parent.Width, (Parent.Width - 2*gblE.PadSm) / 3)
```

Slots de posición dentro de su contenedor de rol:

| Slot | X | Y |
|---|---|---|
| **A0** | `0` | `0` |
| **A1** | `If(nfMovil, 0, (Parent.Width - 2*gblE.PadSm)/3 + gblE.PadSm)` | `If(nfMovil, 104 + gblE.PadSm, 0)` |
| **A2** | `If(nfMovil, 0, 2*((Parent.Width - 2*gblE.PadSm)/3 + gblE.PadSm))` | `If(nfMovil, 2*(104 + gblE.PadSm), 0)` |

Interior de CADA card (mismos 4 controles, sufijo del tag de la card — cubre a todos los hijos
del árbol). ⚠ El `Contenedor` NO tiene OnSelect: el clic vive en un **botón overlay transparente**,
ÚLTIMO dentro del contenedor (tope del z-order). No tapa texto porque todos sus Fill son
transparentes; el hover replica el borde teal del `.qbtn` del mockup:

| Control | Propiedades |
|---|---|
| `lblAcc<Tag>IcoIni` (Etiqueta clásica) | X `gblE.Pad` · Y `8` · Width `40` · Height `28` · Size `13` · Text = emoji de §5.4 |
| `lblAcc<Tag>TitIni` (Etiqueta clásica) | X `gblE.Pad` · Y `36` · Width `Parent.Width - 2*gblE.Pad` · Height `22` · Size `11` · FontWeight `Bold` · Text = título de §5.4 |
| `lblAcc<Tag>DesIni` (Etiqueta clásica) | X `gblE.Pad` · Y `58` · Width `Parent.Width - 2*gblE.Pad` · Height `36` · ESTILO-Subtitulo · Wrap `true` · Text = descripción de §5.4 |
| `btnAcc<Tag>Ini` (Botón clásico) | X `0` · Y `0` · Width `Parent.Width` · Height `Parent.Height` · Text `""` · Fill `Color.Transparent` · HoverFill `Color.Transparent` · PressedFill `Color.Transparent` · BorderThickness `1` · BorderColor `Color.Transparent` · HoverBorderColor `gblC.teal` · PressedBorderColor `gblC.teal` · RadiusTopLeft/TopRight/BottomLeft/BottomRight `gblE.Radio` · OnSelect = §5.4 · AccessibleLabel = título de §5.4 |

### 5.4 Las 8 cards (contenidos según `scrHome()` del mockup)

| Card (`con…`) | Tag | Padre | Slot | Emoji | Título | Descripción | btnAcc….OnSelect |
|---|---|---|---|---|---|---|---|
| `conAccColaIni` | `Cola` | `conAccBoIni` | A0 | 📥 | `"Ir a la cola"` | `"FIFO — la más vieja primero"` | `Navigate(scrTranscripcion)` |
| `conAccArqIni` | `Arq` | `conAccBoIni` | A1 | ✅ | `"Arqueo del día"` | `"lo que ya grabaste"` | `Navigate(scrTranscritas)` |
| `conAccNvaIni` | `Nva` | `conAccSolIni` | A0 | ✨ | `"Nueva solicitud"` | `"pedir una asignación Especial"` | `Navigate(scrNuevaSolicitud)` |
| `conAccMisIni` | `Mis` | `conAccSolIni` | A1 | 🗂️ | `"Mis solicitudes"` | `"seguimiento y estados"` | `Navigate(scrMisSolicitudes)` |
| `conAccAprbIni` | `Aprb` | `conAccSupIni` | A0 | ⚖️ | `"Aprobaciones"` | `"FIFO — la más vieja primero"` | `Navigate(scrAprobaciones)` |
| `conAccMonIni` | `Mon` | `conAccAdmIni` | A0 | 📡 | `"Monitoreo"` | `"¿llegó el archivo?"` | `Navigate(scrMonitoreo)` |
| `conAccRecIni` | `Rec` | `conAccAdmIni` | A1 | 🧹 | `"Reconciliación"` | `"Diarias desaparecidas"` | `Navigate(scrReconciliacion)` |
| `conAccAgeIni` | `Age` | `conAccAdmIni` | A2 | 🏍️ | `"Agentes"` | `"ligar usuarios"` | `Navigate(scrAgentes)` |

## 6. Navegación — `cmpNavIni`

Instancia de `cmpNavLateral` según 01 §8.1 (al FINAL del árbol para flotar sobre todo en móvil):

| Propiedad | Valor |
|---|---|
| RutaActiva | `"ini"` |
| X / Y / Height | `0` / `0` / `Parent.Height` |
| Visible | `(!nfMovil || gblNavMovilAbierta) && !IsBlank(gblRol)` |

> ⚠ **Excepción declarada a la receta 8.1**: al `Visible` estándar se le agrega
> `&& !IsBlank(gblRol)` — en el estado "sin acceso" la nav desaparece por completo.
> Solo scrInicio lleva esta conjunción (las demás pantallas son inalcanzables sin rol
> por sus guards de 01 §4).

## Notas — divergencias y dependencias

- **Sin guard de OnVisible**: deliberado — scrInicio es la pantalla de aterrizaje de todos los
  roles y del estado sin-acceso. Las colecciones quedan protegidas por los `If(gblRol = ...)`.
- **KPI "Aprobadas hoy" del Supervisor (mockup) omitido**: no hay columna de fecha de resolución
  verificada en `asignacion` para filtrar "hoy" con honestidad (usar `Modified On` mezclaría
  cualquier edición). Divergencia declarada en **LED-14** (00 §2).
- **KPI "Cierres pendientes" del Admin (mockup) omitido**: exigiría traer `colCierres` (cientos
  de filas, 00 §4.1) solo para un número; contradice el OnVisible mínimo de 00 §5. El número
  vive en scrCierres (guía 10). Divergencia declarada en **LED-14** (00 §2).
- **Hero de carga sin "1,002 cuentas · sin errores"**: el conteo y los errores viven en
  Monitoreo/Smelter — LED-07 / LED-12. Detalle de "Diarias fuera de cartera" sin el
  "de N activas" del mockup: contar las activas totales no es delegable ni amerita colección
  (00 §4.3).
- **Barra de cambio de rol del mockup**: no existe — LED-04.
- **Divergencia semántica declarada (hallazgo R1-10)**: el mockup calculaba «Cobrado hoy» solo
  con pagos ya transcritos; la guía usa TODOS los pagos capturados hoy
  (`'Fecha del pago' >= Today()`) — «cobrado» es lo que entró en el día, esté transcrito o no.
  Semántica elegida a propósito.
- **Display names**: `'[IN] Aplica pago'`, `'[AS] Solicitado por'`, `'[AG] Usuario'` y los
  choices (`'Choice | CM | Estado aprobacion especial'.Aprobado/.Rechazada/.'Pendiente aprobacion'`)
  según la auditoría de 00; **si IntelliSense difiere, el suyo manda** (01, encabezado) — anotar y avisar.
- **Dependencia de backend**: sin la tabla `Usuario Apps` (00 §3.1) todo entra por el fallback
  `Set(gblRol, "Admin")` de 01 §4 — esta pantalla no cambia, solo se ve siempre el set Admin.

## Checklist de salida

- [ ] Usuario SIN fila activa en `Usuario Apps`: solo se ve `cmpVacioIni` (🔐 centrado);
      ni nav, ni encabezado, ni KPIs, ni accesos. OnVisible no dispara ninguna llamada.
- [ ] Backoffice: 3 KPIs (hero teal "EN COLA") + 2 cards; ningún control de otro rol visible.
- [ ] Solicitante: 3 KPIs + 2 cards · Supervisor: 1 KPI + 1 card · Admin: 3 KPIs + 3 cards.
- [ ] Subtítulo del encabezado muestra `C$ … · US$ …` coherente con los pagos de hoy
      (`"C$ 0.00"` si no hay pagos).
- [ ] Admin sin carga del día (probar antes de la corrida del import): hero muestra "sin carga".
- [ ] Clic en cada card navega a su pantalla; hover pinta el borde teal (overlay transparente,
      el texto de la card nunca se tapa).
- [ ] Ventana < 920 px: KPIs en 2 columnas (P2 baja de fila), cards apiladas a ancho completo,
      hamburguesa de la barra móvil abre la nav flotante por encima del contenido.
- [ ] Studio: cero subrayados azules de delegación en el OnVisible y en los Valor de los KPIs.
- [ ] Monitor: abrir scrInicio genera ≤ 3 llamadas a Dataverse (1 de pagos + máx. 2 del rol).
