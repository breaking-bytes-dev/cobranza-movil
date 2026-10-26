# Guía Paso a Paso — App del Localizador (nivel kindergarten) 🍼

> **Qué es esto.** Un instructivo de IKEA para construir la app, **control por control**, asumiendo que nunca
> abriste Power Apps Studio. El [`04-app-localizador-canvas.md`](../desarrollo/04-app-localizador-canvas.md) es el *plano del
> arquitecto* (referencia). **Este** es el *instructivo de armado*.
>
> **Construimos con los DATOS REALES de Dataverse** (`sanic_*`). Lo único local es `colLineasPago` (borrador de
> las líneas de pago que el usuario tipea antes de guardar). **Requisito:** las tablas `sanic_*` ya existen y tu
> usuario está ligado a un `sanic_cm_tbl_agente`.
>
> **Layout:** contenedores (cajas dentro de cajas que se acomodan solas).

---

## 📖 Cómo leer esta guía (LEÉ ESTO PRIMERO)

Cada control se presenta como un **bloque**, así:

> **`nombreDelControl`** · *Tipo de control* · va dentro de → `contenedorPadre`
> ```
> Propiedad        = valor que escribís en la barra de fórmulas
> Fill             = gblC.white
> OnSelect         = acción al tocarlo
> ```

**Reglas del formato (valen para TODA la guía):**

| Cuando leas… | Hacé esto en Power Apps |
|--------------|--------------------------|
| `Ancho = Llenar` / `Alto = Llenar` | En el panel derecho activá **Anchura/Altura flexible** (ocupa el espacio disponible). |
| `Ancho = 360` / `Alto = 44` | Valor fijo en `Width` / `Height`. |
| `Alto = Auto` | Activá **Altura automática** (crece con el contenido — clave para accesibilidad). |
| `RadiusAll = 14` | Poné **14** en las 4 esquinas (panel: **Radio de borde**). |
| `Padding = 14,18,16,20` | `PaddingTop=14`, `PaddingBottom=18`, `PaddingLeft=16`, `PaddingRight=20`. |
| `Borde = gblC.cardBorder, 1` | `BorderColor=gblC.cardBorder`, `BorderThickness=1`, `BorderStyle=BorderStyle.Solid`. |
| `Gap = 8` | `LayoutGap=8` (separación entre hijos del contenedor). |
| `Justif = SpaceBetween` | `LayoutJustifyContent=LayoutJustifyContent.SpaceBetween`. |
| `Alinear = Center` | `LayoutAlignItems=LayoutAlignItems.Center`. |
| `Wrap = Sí` | `LayoutWrap=true` (los hijos bajan de línea si no entran — **no se rompen**). |
| `Scroll = Sí` | `LayoutOverflowY=LayoutOverflowY.Scroll`. |
| (no listo una propiedad) | Dejala en su **valor por defecto**. |

> 🅰️ **Tip que te ahorra escribir mil veces `Font`:** andá a **Configuración → Tema** y poné la fuente en
> **Segoe UI**. Así todos los textos ya salen con la fuente correcta y **no tenés que setear `Font` en cada
> etiqueta**. (En la guía omito `Font`; ya queda por el tema.)

---

## 📇 Hoja de referencia — tablas y campos REALES (verificados)

Lo que vas a escribir. Todo `sanic_*` sale de los componentes 02–13. **Tenelo a mano.**

**`sanic_cm_tbl_asignacion`** (base de la Cartera — 1 fila por cuenta asignada):
`sanic_cm_as_cuentaid`(→cuenta) · `sanic_cm_as_agenteid`(→agente) · `sanic_cm_as_tipo`(Choice: Diaria/Incobrable/Especial) ·
`sanic_cm_as_estado`(Choice: Activa/Cerrada) · `sanic_cm_as_orden`(Nº, prioridad) · `sanic_cm_as_estadoaprobacion`(Choice).
*(No tiene cliente directo → se llega por `sanic_cm_as_cuentaid.sanic_cm_ct_clienteid`.)*

**`sanic_cm_tbl_cuenta`:** `sanic_cm_ct_numerodecuenta`(nombre) · `sanic_cm_ct_clienteid` · `sanic_cm_ct_productoid` · `sanic_cm_ct_fiador` ·
`sanic_cm_ct_retironio` · `sanic_cm_ct_retirousd` *(⭐ a cobrar)* · `sanic_cm_ct_saldovencidonio` · `sanic_cm_ct_saldovencidousd` ·
`sanic_cm_ct_totalsaldovencido` *(el dolarizado)* · `sanic_cm_ct_contagiadousd` · `sanic_cm_ct_pagominimonio` ·
`sanic_cm_ct_pagominimousd` · `sanic_cm_ct_diasmora` · `sanic_cm_ct_nivelmora` · `sanic_cm_ct_fechapago` · `sanic_cm_ct_fechaultimopago` ·
`sanic_cm_ct_ciclo` · `sanic_cm_ct_nota`.

**`sanic_cm_tbl_cliente`:** `sanic_cm_cl_nombre` · `sanic_cm_cl_cif` · `sanic_cm_cl_departamento` · `sanic_cm_cl_distrito` · `sanic_cm_cl_trabajo`.
**`sanic_cm_tbl_telefono`:** `sanic_cm_tl_numero`(el teléfono) · `sanic_cm_tl_clienteid` · `sanic_cm_tl_tipo`(Celular/Casa).
**`sanic_cm_tbl_direccion`:** `sanic_cm_dr_direccion`(el texto) · `sanic_cm_dr_clienteid` · `sanic_cm_dr_tipo`(Casa/EC/Trabajo).
**`sanic_cm_tbl_producto`:** `sanic_cm_pr_codigo` · `sanic_cm_pr_tipo`(texto, ej. "Tarjeta Oro"). **`sanic_cm_tbl_fiador`:** `sanic_cm_fi_nombre`.
**`sanic_cm_tbl_agente`:** `sanic_cm_ag_nombre` · `sanic_cm_ag_usuarioid`(→systemuser) · `sanic_cm_ag_activo`.

**`sanic_cm_tbl_interaccion`:** `sanic_cm_in_numerointeraccion`(autonumber `INT-`, **vacío hasta sincronizar**) · `sanic_cm_in_cuentaid` ·
`sanic_cm_in_agente` · `sanic_cm_in_comentario` · `sanic_cm_in_fechacaptura` · `sanic_cm_cd_aplicapago`(Sí/No) ·
`sanic_cm_in_actualizainfo`(Sí/No) · `sanic_cm_in_latitud` · `sanic_cm_in_longitud` · `sanic_cm_in_estadograbacion`(Choice).
**`sanic_cm_tbl_pago`:** `sanic_cm_pg_numero`(`PAG-`) · `sanic_cm_pg_interaccion` · `sanic_cm_pg_monto` · `sanic_cm_pg_moneda`(Choice NIO/USD) ·
`sanic_cm_pg_numerorecibo` · `sanic_cm_pg_fecha`.
**`sanic_cm_tbl_actualizaciondatos`:** `sanic_cm_ad_interaccionid` + campos a actualizar (ver comp. 13).

> ⚠️ **Choices:** al comparar escribís `Campo.Value = "Texto"` (ej. `sanic_cm_as_tipo.Value="Diaria"`). Al **escribir**
> un Choice usás `'Nombre del conjunto'.Opcion` — Power Apps **autocompleta** el nombre exacto; usá lo que ofrezca.

---

## 🧭 Glosario express del Studio

- **Árbol de objetos** (izq.): pantallas y controles anidados. Tu mapa.
- **Lienzo** (centro): el teléfono.
- **Panel de propiedades** (der.): opciones del control seleccionado (Anchura flexible, Radio, Relleno…).
- **Barra de fórmulas** (arriba): desplegable de **propiedad** + campo de **fórmula**.
- **+ Insertar** / **+ Datos** (izq.): agregar controles / conectar tablas.

---

## 🔧 LAS 6 RECETAS (acciones que repetís)

1. **CONTENEDOR** — seleccioná el padre → **+ Insertar → Diseño → Contenedor vertical/horizontal** → renombrá.
2. **ETIQUETA** — **+ Insertar → Etiqueta de texto** → renombrá → seteá el bloque de propiedades.
3. **BOTÓN/ÍCONO** — **+ Insertar → Botón** (o **Iconos**) → renombrá → seteá + `OnSelect`.
4. **GALERÍA** — **+ Insertar → Galería → Galería vertical/horizontal en blanco** → renombrá → seteá `Items` →
   poné la plantilla **adentro**.
5. **SETEAR propiedad** — seleccioná el control → barra de fórmulas → elegí la propiedad → escribí el valor.
6. **ANIDAR** — en el Árbol, **arrastrá** el control encima del contenedor (queda con sangría = es su hijo).

---

## 🧱 CATÁLOGO DE COMPONENTES (definidos una vez, completos)

Estos bloques se repiten en varias pantallas. **Construilos una vez acá, entendé sus propiedades, y después las
pantallas te dicen "usá la Tarjeta (§C.1)".** Donde un componente tiene contenido variable (texto, datos), la
pantalla te da solo eso.

### §C.1 — Tarjeta blanca `cmpTarjeta`
> *Contenedor vertical* — la caja blanca base de casi todo.
```
Fill        = gblC.white
Borde       = gblC.cardBorder, 1
RadiusAll   = 14
Padding     = 14,14,16,16
Gap         = 0
Ancho       = Llenar
Alto        = Auto
```

### §C.2 — Tarjeta realce verde `cmpTarjetaVerde`
> *Contenedor vertical* — para "totales / a cobrar".
```
Fill        = gblC.accentBg
Borde       = gblC.accentBorder, 1
RadiusAll   = 14
Padding     = 14,14,16,16
Gap         = 2
Ancho       = Llenar
Alto        = Auto
```

### §C.3 — Badge de tipo `cmpBadge`
> *Etiqueta* — la pastillita Diaria/Incobrable/Especial. `<tipo>` = la expresión que te diga la pantalla.
```
Text        = <tipo>
Size        = gblT.fxs
FontWeight  = FontWeight.Bold
Align       = Align.Center
RadiusAll   = 11
Padding     = 3,3,10,10
Alto        = Auto
Fill        = Switch(<tipo>, "Diaria",gblC.diariaBg, "Incobrable",gblC.incobBg, gblC.espBg)
Color       = Switch(<tipo>, "Diaria",gblC.diariaInk,"Incobrable",gblC.incobInk, gblC.espInk)
```

### §C.4 — Pastilla de mora `cmpPillMora`
> *Etiqueta*. `<txt>` y `<alta>` (condición booleana de mora alta) los da la pantalla.
```
Text        = <txt>
Size        = gblT.fxs
Align       = Align.Center
RadiusAll   = 11
Padding     = 5,5,12,12
Alto        = Auto
Fill        = If(<alta>, gblC.redBg, gblC.screen)
Color       = If(<alta>, gblC.redInk, gblC.ink3)
FontWeight  = If(<alta>, FontWeight.Bold, FontWeight.Normal)
```

### §C.5 — Título de sección `cmpSecTitle`
> *Etiqueta* — "CONTACTO", "MORA", etc.
```
Text        = <texto en MAYÚSCULAS>
Size        = gblT.fxs
FontWeight  = FontWeight.Bold
Color       = gblC.muted2
Alto        = Auto
```

### §C.6 — Fila clave–valor `cmpKV`
> *Contenedor horizontal* con dos etiquetas. Una por cada dato (Días en mora, Ciclo, etc.).
```
[contenedor]  Justif=SpaceBetween · Alinear=Start · Gap=10 · Padding=6,6,0,0 · Ancho=Llenar · Alto=Auto · Wrap=Sí
```
> **`lblK`** *(Etiqueta)*: `Text=<clave>` · `Size=gblT.fbase` · `Color=gblC.muted` · `Alto=Auto`
> **`lblV`** *(Etiqueta)*: `Text=<valor>` · `Size=gblT.fbase` · `FontWeight=FontWeight.Semibold` ·
> `Color=gblC.ink` · `Align=Align.Right` · `Alto=Auto`

### §C.7 — Botones de fila de contacto
> **`btnLlamar`** *(Botón)* — primario:
```
Text=<"Llamar">  Fill=gblC.teal  Color=gblC.white  Size=gblT.fsm  FontWeight=FontWeight.Semibold
RadiusAll=9  Padding=7,7,13,13  Alto=Auto
OnSelect = Launch("tel:" & <numero>)
```
> **`btnMapa`** *(Botón)* — secundario: igual pero `Fill=gblC.chipBg`, `Color=gblC.teal`,
> `OnSelect = Launch("https://www.google.com/maps/search/?api=1&query=" & EncodeUrl(<direccion>))`.

### §C.8 — Header verde `cmpHeaderVerde` (Inicio / Cartera / Stats)
> *Contenedor vertical*:
```
Fill=gblC.teal  Padding=12,18,20,20  Gap=12  Ancho=Llenar  Alto=Auto
```
Hijos:
> **`conStatus`** *(Cont. horizontal)* · en header — `Justif=SpaceBetween · Ancho=Llenar · Alto=22`
> > **`lblHora`** *(Etiqueta)*: `Text="9:41"` · `Color=gblC.white` · `Size=gblT.fbase` · `FontWeight=FontWeight.Bold`
> > **`lblIconos`** *(Etiqueta)*: `Text="●●● 📶 🔋"` · `Color=gblC.white` · `Size=gblT.fbase` · `Align=Align.Right`
>
> **`conTitulo`** *(Cont. horizontal)* · en header — `Justif=SpaceBetween · Alinear=Start · Gap=8 · Wrap=Sí · Ancho=Llenar · Alto=Auto`
> > **`conTitIzq`** *(Cont. vertical)* · `Gap=2 · Alto=Auto`
> > > **`lblTitulo`** *(Etiqueta)*: `Text=<título>` · `Color=gblC.white` · `Size=gblT.fxl` · `FontWeight=FontWeight.Bold` · `Alto=Auto`
> > > **`lblSub`** *(Etiqueta)*: `Text=<subtítulo>` · `Color=gblC.tealSub` · `Size=gblT.fsm` · `Alto=Auto`
> > **`conHeaderBtns`** *(Cont. horizontal)* · `Gap=8 · Wrap=Sí · Justif=End · Alinear=Center · Alto=Auto`
> > > **`btnAa`** (§C.13) + **`cmpSyncPill`** (§C.11)

### §C.9 — Header con volver `cmpHeaderBack` (pantallas apiladas)
> *Contenedor horizontal*:
```
Fill=gblC.teal  Padding=14,16,16,18  Gap=12  Alinear=Center  Wrap=Sí  Ancho=Llenar  Alto=Auto
```
> **`btnBack`** *(Ícono)*: `Icon=Icon.Back` · `Color=gblC.white` · `Fill=RGBA(255,255,255,0.16)` ·
> `RadiusAll=10` · `Width=34` · `Height=34` · `OnSelect=Back()`
> **`conBackTit`** *(Cont. vertical)*: `Ancho=Llenar · Gap=2 · Alto=Auto`
> > **`lblDnm`** *(Etiqueta)*: `Text=<título>` · `Color=gblC.white` · `Size=gblT.flg` · `FontWeight=FontWeight.Bold` · `Alto=Auto`
> > **`lblDac`** *(Etiqueta)*: `Text=<subtítulo>` · `Color=gblC.tealSub` · `Size=gblT.fsm` · `Alto=Auto`
> *(opcional a la derecha: un `cmpBadge` y/o `btnAa`, según la pantalla.)*

### §C.10 — Barra inferior `cmpNav`
> *Contenedor horizontal*:
```
Fill=gblC.white  Borde=gblC.cardBorder,1  Padding=9,14,0,0  Justif=SpaceBetween  Ancho=Llenar  Height=64
```
3 ítems (Inicio / Cartera / Stats). Cada uno:
> **`conNav<X>`** *(Cont. vertical)*: `Alinear=Center · Gap=3 · Ancho=Llenar · Alto=Auto · OnSelect=Navigate(scr<X>)`
> > **`ico<X>`** *(Ícono)*: `Icon=Icon.Home|Icon.Items|Icon.Trending` · `Width=22` · `Height=22` ·
> > `Color=If(<es la pantalla actual>, gblC.teal, gblC.muted2)`
> > **`lbl<X>`** *(Etiqueta)*: `Text="Inicio"|"Cartera"|"Stats"` · `Size=gblT.f2xs` · `Align=Align.Center` ·
> > `Color=If(<actual>, gblC.teal, gblC.muted2)` · `FontWeight=If(<actual>, FontWeight.Bold, FontWeight.Normal)`

### §C.11 — Indicador de sync `cmpSyncPill`
> *Contenedor horizontal*:
```
Fill=RGBA(255,255,255,0.16)  RadiusAll=12  Padding=5,5,11,11  Gap=6  Alinear=Center  Height=28
OnSelect = Set(varSyncBack, <"cartera" o "inicio">); Navigate(scrSincronizacion)
```
> **`icoDot`** *(Ícono `Icon.CircleFill`)*: `Width=8` · `Height=8` ·
> `Color=If(Connection.Connected, gblC.green, gblC.amberOff)`
> **`lblSync`** *(Etiqueta)*: `Text=If(Connection.Connected,"Sincronizado","Sin conexión")` · `Color=gblC.white` · `Size=gblT.fxs`

### §C.12 — Pestañas `cmpTabBar`
> *Contenedor horizontal*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · Ancho=Llenar · Height=48`. Dos botones:
> **`tab<A>` / `tab<B>`** *(Botón)*: `Ancho=Llenar · Size=gblT.fsm · FontWeight=FontWeight.Semibold · Fill=gblC.white · BorderThickness=0`
> > `Text=<"Etiqueta (N)">` · `Color=If(<varTab>=<clave>, gblC.teal, gblC.muted)` · `OnSelect=Set(<varTab>, <clave>)`

### §C.13 — Botón "Aa" + Hoja de accesibilidad `cmpSheet`
> **`btnAa`** *(Botón)*: `Text="Aa"` · `Fill=RGBA(255,255,255,0.18)` · `Color=gblC.white` · `FontWeight=FontWeight.Bold` ·
> `RadiusAll=9` · `Padding=7,7,11,11` · `Height=34` · `OnSelect=Set(varSheetEscala, true)`
>
> **`conSheet`** *(Cont. vertical, último hijo de `conMaster`)*:
> ```
> Width=Parent.Width  Height=Parent.Height  X=0  Y=0
> Fill=RGBA(16,24,40,0.45)  Justif=End  Visible=varSheetEscala
> OnSelect = Set(varSheetEscala, false)
> ```
> > **`cardSheet`** *(Cont. vertical)*: `Fill=gblC.white · RadiusTopLeft=22 · RadiusTopRight=22 · Padding=14,16,16,16 · Ancho=Llenar`
> > > **`lblSheetT`** *(Etiqueta)*: `Text="TAMAÑO DE VISTA"` · `Size=gblT.fsm` · `FontWeight=FontWeight.Bold` · `Color=gblC.muted2`
> > > **4 filas** (Botón), una por nivel — `Text="Aa  Normal|Grande|Muy grande|Máximo"` · `Size=gblT.fmd` · `RadiusAll=12` · `Padding=14,14,14,14`. `OnSelect`:
> > > ```
> > > Set(gblScale, 1 | 1.2 | 1.45 | 1.7);
> > > Set(gblT, { f2xs:10*gblScale, fxs:11*gblScale, fsm:12.5*gblScale, fbase:13.5*gblScale,
> > >             fmd:15*gblScale, flg:17*gblScale, fxl:23*gblScale, fbig:24*gblScale });
> > > Set(varSheetEscala, false)
> > > ```

---

## 🟢 PARTE 0 — Crear la app vacía

1. **make.powerapps.com** → confirmá el **entorno** (donde están tus tablas).
2. **+ Crear → App en blanco → App de lienzo en blanco → Crear**.
3. Nombre `Cobranza Móvil - Localizador`, formato **Teléfono** → **Crear**. **Guardá** (`Ctrl+S`).
4. **Configuración → Pantalla:** Orientación **Vertical** + **Bloquear**; **Escalar para ajustar** On;
   **Bloquear relación de aspecto** On.
5. **Configuración → Tema:** fuente **Segoe UI** (así no repetís `Font`).

---

## 🔌 PARTE 1 — Conectar las tablas + perfil offline

1. **Datos → + Agregar datos → Dataverse** → agregá las **11 tablas**:
   `sanic_cm_tbl_asignacion`, `sanic_cm_tbl_cuenta`, `sanic_cm_tbl_cliente`, `sanic_cm_tbl_telefono`, `sanic_cm_tbl_direccion`, `sanic_cm_tbl_producto`,
   `sanic_cm_tbl_fiador`, `sanic_cm_tbl_agente`, `sanic_cm_tbl_interaccion`, `sanic_cm_tbl_pago`, `sanic_cm_tbl_actualizaciondatos`.
2. **Configuración → General → Sin conexión → Activar** y configurá el **perfil offline** según el
   [doc 04, §4](../desarrollo/04-app-localizador-canvas.md).

---

## 🧠 PARTE 2 — App.OnStart

En el árbol → **App** → propiedad **`OnStart`**:
```powerapps
Set(gblC, {
    teal:        ColorValue("#0F6B6B"),  tealSub:    ColorValue("#BFE3E3"),
    screen:      ColorValue("#F2F4F7"),  card:       ColorValue("#FFFFFF"),
    cardBorder:  ColorValue("#EAECF0"),  borderSoft: ColorValue("#E4E7EC"),
    hairline:    ColorValue("#F6F7F9"),  white:      ColorValue("#FFFFFF"),
    ink:         ColorValue("#101828"),  ink2:       ColorValue("#344054"),
    ink3:        ColorValue("#475467"),  muted:      ColorValue("#667085"),
    muted2:      ColorValue("#98A2B3"),  green:      ColorValue("#34D399"),
    accentBg:    ColorValue("#ECFDF5"),  accentBorder:ColorValue("#A7F3D0"),
    accentInk:   ColorValue("#065F46"),  accentInk2: ColorValue("#059669"),
    diariaBg:    ColorValue("#DBEAFE"),  diariaInk:  ColorValue("#2563EB"),
    incobBg:     ColorValue("#FEF3C7"),  incobInk:   ColorValue("#B45309"),
    espBg:       ColorValue("#EDE9FE"),  espInk:     ColorValue("#7C3AED"),
    redBg:       ColorValue("#FEE2E2"),  redInk:     ColorValue("#DC2626"),
    chipBg:      ColorValue("#EEF2F6"),  datosBg:    ColorValue("#EFF6FF"),
    amber:       ColorValue("#FBBF24"),  amberOff:   ColorValue("#F59E0B"),
    offGray:     ColorValue("#475467"),  phoneBorder:ColorValue("#CBD2D9"),
    noteBg:      ColorValue("#F8FAFC"),  hint:       ColorValue("#D97706")
});
Set(gblScale, 1);
Set(gblT, { f2xs:10*gblScale, fxs:11*gblScale, fsm:12.5*gblScale, fbase:13.5*gblScale,
            fmd:15*gblScale, flg:17*gblScale, fxl:23*gblScale, fbig:24*gblScale });
Set(gblAgente, LookUp(sanic_cm_tbl_agente, sanic_cm_ag_usuarioid.'Primary Email' = User().Email));
Set(varModo, "cliente"); Set(varFiltro, "Todos"); Set(varBuscar, "");
Set(varInicioModo, "cliente");
```
**Ejecutá OnStart** (clic derecho en App → Ejecutar OnStart). Si `gblAgente` queda vacío → falta tu fila en
`sanic_cm_tbl_agente`.

**Helper bimoneda** — App → propiedad **`Formulas`**:
```powerapps
Moneda(nio:Number, usd:Number): Text =
   With({ pn: If(nio>0, "C$ " & Text(nio, "#,##0.00"), ""),
          pu: If(usd>0, "US$ " & Text(usd, "#,##0.00"), "") },
      Coalesce(pn & If(!IsBlank(pn) && !IsBlank(pu), " · ", "") & pu, "C$ 0.00"));
```

---

## 🟦 PARTE 3 — Pantalla CARTERA

> **Árbol de la pantalla:**
> ```
> scrCartera
> └── conMaster
>     ├── conHeader      (cmpHeaderVerde + tarjeta total)
>     ├── conFiltros     (segmento + búsqueda + barra de resultado)
>     ├── galCartera     (la lista — scrollea)
>     └── conNav         (cmpNav)
> ```

### Paso 1 — Pantalla y raíz
> **`scrCartera`** *(Pantalla)* — renombrá `Screen1`.
> ```
> Fill     = gblC.screen
> OnVisible= Set(misAsig, Filter(sanic_cm_tbl_asignacion, sanic_cm_as_estado.Value = "Activa"))
> ```
> **`conMaster`** *(Cont. vertical)* · en `scrCartera`
> ```
> Width=Parent.Width  Height=Parent.Height  X=0  Y=0  Gap=0  Scroll=No
> ```

### Paso 2 — Header
Armá **`conHeader`** = **`cmpHeaderVerde`** (§C.8) dentro de `conMaster`, con:
- `lblTitulo.Text = "Mi Cartera"`
- `lblSub.Text = CountRows(Distinct(misAsig, sanic_cm_as_cuentaid.sanic_cm_ct_clienteid)) & " clientes · " & CountRows(misAsig) & " cuentas"`
- `cmpSyncPill.OnSelect` → `Set(varSyncBack,"cartera"); Navigate(scrSincronizacion)`

Y agregale la **tarjeta total** como último hijo del header:
> **`conTotal`** *(Cont. vertical)* · en `conHeader`
> ```
> Fill=RGBA(255,255,255,0.12)  RadiusAll=12  Padding=10,10,14,14  Gap=3  Ancho=Llenar  Alto=Auto
> ```
> > **`lblTotalL`** *(Etiqueta)*: `Text="A cobrar hoy"` · `Color=gblC.tealSub` · `Size=gblT.fxs` · `Alto=Auto`
> > **`lblTotalA`** *(Etiqueta)*:
> > ```
> > Text       = Moneda(Sum(misAsig, sanic_cm_as_cuentaid.sanic_cm_ct_retironio),
> >                     Sum(misAsig, sanic_cm_as_cuentaid.sanic_cm_ct_retirousd))
> > Color      = gblC.white
> > Size       = gblT.flg
> > FontWeight = FontWeight.Bold
> > Alto       = Auto
> > ```

### Paso 3 — Filtros
> **`conFiltros`** *(Cont. vertical)* · en `conMaster`
> ```
> Fill=gblC.screen  Padding=14,0,16,16  Gap=14  Ancho=Llenar  Alto=Auto
> ```

> **`galSeg`** *(Galería horizontal)* · en `conFiltros` — el segmento de tipo
> ```
> Items      = ["Todos","Diaria","Incobrable","Especial"]
> Fill       = gblC.borderSoft
> RadiusAll  = 18
> Height     = 40
> Ancho      = Llenar
> TemplatePadding = 0
> ```
> Plantilla (adentro de `galSeg`):
> > **`lblOpt`** *(Etiqueta)*:
> > ```
> > Text       = ThisItem.Value
> > Align      = Align.Center
> > Size       = gblT.fsm
> > RadiusAll  = 15
> > Alto       = Llenar
> > Fill       = If(varFiltro=ThisItem.Value, gblC.white, Color.Transparent)
> > Color      = If(varFiltro=ThisItem.Value, gblC.teal, gblC.ink3)
> > FontWeight = If(varFiltro=ThisItem.Value, FontWeight.Bold, FontWeight.Normal)
> > OnSelect   = Set(varFiltro, ThisItem.Value)
> > ```

> **`conSearch`** *(Cont. horizontal)* · en `conFiltros`
> ```
> Fill=gblC.white  Borde=gblC.borderSoft,1  RadiusAll=12  Padding=10,10,12,10  Gap=9  Alinear=Center  Ancho=Llenar  Height=44
> ```
> > **`icoBuscar`** *(Ícono `Icon.Search`)*: `Color=gblC.muted2` · `Width=18` · `Height=18`
> > **`txtBuscar`** *(Entrada de texto)*:
> > ```
> > HintText        = "Buscar cliente o cuenta…"
> > BorderThickness = 0
> > Fill            = Color.Transparent
> > Size            = gblT.fbase
> > Color           = gblC.ink
> > Ancho           = Llenar
> > OnChange        = Set(varBuscar, Self.Value)
> > ```

> **`conResult`** *(Cont. horizontal)* · en `conFiltros`
> ```
> Justif=SpaceBetween  Alinear=Center  Wrap=Sí  Ancho=Llenar  Alto=Auto
> ```
> > **`lblCount`** *(Etiqueta)*:
> > ```
> > Text       = CountRows(galCartera.AllItems) & If(varModo="cliente"," clientes"," cuentas")
> > Size       = gblT.fsm  ·  Color=gblC.muted  ·  FontWeight=FontWeight.Semibold  ·  Alto=Auto
> > ```
> > **`conToggle`** *(Cont. horizontal)*: `Fill=gblC.borderSoft · RadiusAll=10 · Padding=2,2,2,2 · Height=34 · Alto=Auto`
> > > **`btnModoCli`** *(Botón)*:
> > > ```
> > > Text=" 👤 Cliente"  RadiusAll=8  Size=gblT.fxs  BorderThickness=0
> > > Fill       = If(varModo="cliente", gblC.white, Color.Transparent)
> > > Color      = If(varModo="cliente", gblC.teal, gblC.ink3)
> > > FontWeight = If(varModo="cliente", FontWeight.Bold, FontWeight.Normal)
> > > OnSelect   = Set(varModo, "cliente")
> > > ```
> > > **`btnModoCta`** *(Botón)*: igual con `"▤ Cuenta"` y `varModo="cuenta"` · `OnSelect=Set(varModo,"cuenta")`

### Paso 4 — La lista `galCartera`
> **`galCartera`** *(Galería vertical)* · en `conMaster`
> ```
> Ancho      = Llenar
> Alto       = Llenar          (ocupa lo que sobra y SCROLLEA solo)
> Fill       = gblC.screen
> TemplatePadding = 12
> Items      =
>    If(varModo = "cuenta",
>       SortByColumns(
>         Filter(misAsig,
>            (varFiltro="Todos" || sanic_cm_as_tipo.Value = varFiltro) &&
>            (varBuscar="" || varBuscar in sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre
>                          || varBuscar in sanic_cm_as_numerocuenta)),
>         "sanic_cm_as_orden", SortOrder.Ascending),
>       SortByColumns(
>         AddColumns(
>           Filter(
>             GroupBy(
>               AddColumns(misAsig,
>                  "cliId",  sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_cif,
>                  "cliNom", sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre),
>               "cliId", "cliNom", "Asigs"),
>             (varFiltro="Todos" || CountRows(Filter(Asigs, sanic_cm_as_tipo.Value = varFiltro))>0) &&
>             (varBuscar="" || varBuscar in cliNom)),
>           "orden",    Min(Asigs, sanic_cm_as_orden),
>           "totNio",   Sum(Asigs, sanic_cm_as_cuentaid.sanic_cm_ct_retironio),
>           "totUsd",   Sum(Asigs, sanic_cm_as_cuentaid.sanic_cm_ct_retirousd),
>           "maxDias",  Max(Asigs, sanic_cm_as_cuentaid.sanic_cm_ct_diasmora),
>           "nCuentas", CountRows(Asigs)),
>         "orden", SortOrder.Ascending))
> ```
> 💡 El **punto** `sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre` = "seguí el vínculo": de la asignación → cuenta →
> cliente → nombre. Modo cuenta filtra; modo cliente **agrupa por cliente**. Datos offline = sin avisos de delegación.

**Plantilla de la fila** (con `galCartera` seleccionada, todo va adentro):

> **Árbol de la fila:**
> ```
> conCard (horizontal)
> ├── conCardMain (vertical)
> │   ├── conR1 (horizontal): lblNombre · lblCnt · lblBadge
> │   ├── lblBadgeCli
> │   ├── lblDiv
> │   └── conR2 (horizontal): conCobrar(lblCobL+lblCobA) · lblMora
> └── conArr (vertical): icoUp · icoDown
> ```

> **`conCard`** *(Cont. horizontal)* · la fila
> ```
> Fill=gblC.white  Borde=gblC.cardBorder,1  RadiusAll=14  Padding=14,14,16,12  Gap=6  Ancho=Llenar  Alto=Auto
> OnSelect =
>    If(varModo="cliente",
>       Set(varCliente, First(ThisItem.Asigs).sanic_cm_as_cuentaid.sanic_cm_ct_clienteid);
>          Set(varCliTab,"cuentas"); Navigate(scrCliente, ScreenTransition.Cover),
>       Set(varAsig, ThisItem); Set(varCuenta, ThisItem.sanic_cm_as_cuentaid);
>          Set(varDetTab,"detalle"); Set(varDetSrc,"cartera"); Navigate(scrDetalleCuenta, ScreenTransition.Cover))
> ```

> **`conCardMain`** *(Cont. vertical)* · en `conCard` — `Ancho=Llenar · Gap=0 · Alto=Auto`

> **`conR1`** *(Cont. horizontal)* · en `conCardMain` — `Justif=SpaceBetween · Wrap=Sí · Alinear=Center · Gap=8 · Ancho=Llenar · Alto=Auto`
> > **`lblNombre`** *(Etiqueta)*:
> > ```
> > Text       = If(varModo="cliente", ThisItem.cliNom, ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre)
> > Size       = gblT.fmd   ·   FontWeight=FontWeight.Bold   ·   Color=gblC.ink   ·   Alto=Auto
> > ```
> > **`lblCnt`** *(Etiqueta)* — solo modo cliente:
> > ```
> > Visible    = (varModo="cliente")
> > Text       = ThisItem.nCuentas & " cuenta(s)"
> > Size=gblT.fxs · Color=gblC.muted · Fill=gblC.screen · RadiusAll=10 · Padding=3,3,9,9 · Alto=Auto
> > ```
> > **`lblBadge`** *(Etiqueta = `cmpBadge` §C.3)* — solo modo cuenta:
> > ```
> > Visible    = (varModo="cuenta")
> > <tipo>     = ThisItem.sanic_cm_as_tipo.Value
> > ```

> **`lblBadgeCli`** *(Etiqueta = `cmpBadge` §C.3)* · en `conCardMain` — solo modo cliente:
> ```
> Visible = (varModo="cliente")
> <tipo>  = First(ThisItem.Asigs).sanic_cm_as_tipo.Value
> ```

> **`lblDiv`** *(Etiqueta vacía = divisor)* · en `conCardMain`
> ```
> Text="" · Height=1 · Fill=gblC.screen · Ancho=Llenar · Padding(arriba/abajo) con Gap del padre
> ```

> **`conR2`** *(Cont. horizontal)* · en `conCardMain` — `Justif=SpaceBetween · Wrap=Sí · Alinear=Center · Gap=10 · Ancho=Llenar · Alto=Auto`
> > **`conCobrar`** *(Cont. vertical)*: `Gap=1 · Alto=Auto`
> > > **`lblCobL`** *(Etiqueta)*: `Text=If(varModo="cliente","Total a cobrar","A cobrar")` · `Size=gblT.f2xs` · `Color=gblC.muted2` · `Alto=Auto`
> > > **`lblCobA`** *(Etiqueta)*:
> > > ```
> > > Text = If(varModo="cliente",
> > >           Moneda(ThisItem.totNio, ThisItem.totUsd),
> > >           Moneda(ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_retironio, ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_retirousd))
> > > Size=gblT.fmd · FontWeight=FontWeight.Bold · Color=gblC.teal · Alto=Auto
> > > ```
> > **`lblMora`** *(Etiqueta = `cmpPillMora` §C.4)*:
> > ```
> > <txt>  = If(varModo="cliente",
> >            "máx " & ThisItem.maxDias & " días",
> >            ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_diasmora & " días · Nivel " & ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_nivelmora)
> > <alta> = If(varModo="cliente",
> >            ThisItem.maxDias>90,
> >            Or(ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_diasmora>90, ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_nivelmora>=4))
> > ```

> **`conArr`** *(Cont. vertical)* · en `conCard` (a la derecha) — `Gap=14 · Alinear=Center · Justif=Center · Alto=Auto`
> > **`icoUp`** *(Ícono `Icon.ChevronUp`)*:
> > ```
> > Color=gblC.muted2 · Fill=gblC.screen · RadiusAll=6 · Width=28 · Height=24 · Padding=2,2,2,2
> > OnSelect =
> >    With({ arriba: First(Sort(Filter(galCartera.AllItems, sanic_cm_as_orden < ThisItem.sanic_cm_as_orden),
> >                              sanic_cm_as_orden, SortOrder.Descending)) },
> >       If(!IsBlank(arriba),
> >          With({ act: ThisItem.sanic_cm_as_orden },
> >             Patch(sanic_cm_tbl_asignacion, ThisItem, { sanic_cm_as_orden: arriba.sanic_cm_as_orden });
> >             Patch(sanic_cm_tbl_asignacion, arriba,   { sanic_cm_as_orden: act }))))
> > ```
> > **`icoDown`** *(Ícono `Icon.ChevronDown`)*: igual estilo. `OnSelect` = lo mismo pero
> > `sanic_cm_as_orden > ThisItem.sanic_cm_as_orden` y `SortOrder.Ascending`.
> > 🔁 El `Patch` es local (offline) y sincroniza al volver señal — lo único que escribís sobre la asignación.

### Paso 5 — Barra inferior
Armá **`conNav`** = **`cmpNav`** (§C.10) en `conMaster`, con **Cartera** como activo
(`<actual>` = `true` en Cartera, `false` en los otros).

> 🎉 **Probá** (`F5`): lista, Cliente/Cuenta, filtro, búsqueda, ▲▼. Si anda → guardaste lo más difícil. **Guardá.**

---

## 🟩 PARTE 4 — Pantalla CLIENTE

> **Árbol:**
> ```
> scrCliente → conMaster
>   ├── conHeaderBack (cmpHeaderBack + btnAa)
>   ├── conTabs (cmpTabBar)
>   └── conBody (vertical, Scroll=Sí)
>        ├── [tab Cuentas]  cardTotalCli · cardContacto · lblSecCtas · galMini
>        └── [tab Inter]    galInterCli
> ```

> **`scrCliente`** *(Pantalla)*: `Fill=gblC.screen` ·
> `OnVisible = Set(varCliCuentas, Filter(sanic_cm_tbl_asignacion, sanic_cm_as_cuentaid.sanic_cm_ct_clienteid = varCliente, sanic_cm_as_estado.Value="Activa"))`
> **`conMaster`** *(Cont. vertical)*: `Width=Parent.Width · Height=Parent.Height · Gap=0`

**Header:** **`conHeaderBack`** = **`cmpHeaderBack`** (§C.9) con
`lblDnm.Text=varCliente.sanic_cm_cl_nombre`, `lblDac.Text=varCliente.sanic_cm_cl_departamento & " · " & varCliente.sanic_cm_cl_trabajo`,
`btnBack.OnSelect=Back()`, + `btnAa` (§C.13).

**Pestañas:** **`conTabs`** = **`cmpTabBar`** (§C.12):
- `tabCuentas`: `Text="Cuentas (" & CountRows(varCliCuentas) & ")"` · `<varTab>=varCliTab` · clave `"cuentas"`.
- `tabInter`: `Text="Interacciones (" & CountRows(Filter(sanic_cm_tbl_interaccion, sanic_cm_in_cuentaid.sanic_cm_ct_clienteid = varCliente)) & ")"` · clave `"inter"`.

> **`conBody`** *(Cont. vertical)* · en `conMaster`
> ```
> Ancho=Llenar  Alto=Llenar  Scroll=Sí  Padding=16,16,16,16  Gap=12  Fill=gblC.screen
> ```

**TAB CUENTAS** (poné `Visible=(varCliTab="cuentas")` en cada tarjeta):

> **`cardTotalCli`** = **`cmpTarjetaVerde`** (§C.2):
> > **`lblTL`** *(Etiqueta)*: `Text="Total a cobrar del cliente"` · `Size=gblT.fxs` · `Color=gblC.muted2` · `Alto=Auto`
> > **`lblTB`** *(Etiqueta)*:
> > ```
> > Text = Moneda(Sum(varCliCuentas, sanic_cm_as_cuentaid.sanic_cm_ct_retironio),
> >               Sum(varCliCuentas, sanic_cm_as_cuentaid.sanic_cm_ct_retirousd))
> > Size=gblT.fbig · FontWeight=FontWeight.Bold · Color=gblC.teal · Alto=Auto
> > ```

> **`cardContacto`** = **`cmpTarjeta`** (§C.1):
> > **`lblSecTel`** = **`cmpSecTitle`** (§C.5): `Text="CONTACTO"`
> > **`galTels`** *(Galería vertical)*:
> > ```
> > Items  = Filter(sanic_cm_tbl_telefono, sanic_cm_tl_clienteid = varCliente)
> > Height = CountRows(Self.AllItems) * 52
> > Ancho  = Llenar
> > ```
> > > plantilla **`conFilaTel`** *(Cont. horizontal)*: `Justif=SpaceBetween · Alinear=Center · Padding=8,8,0,0 · Borde-abajo=gblC.hairline,1 · Ancho=Llenar · Alto=Auto`
> > > > **`lblTelTipo`** *(Etiqueta)*: `Text=ThisItem.sanic_cm_tl_tipo` · `Size=gblT.f2xs` · `Color=gblC.muted2`
> > > > **`lblTelVal`** *(Etiqueta)*: `Text=ThisItem.sanic_cm_tl_numero` · `Size=gblT.fbase` · `Color=gblC.ink`
> > > > **`btnLlamar`** = §C.7 con `<numero>=ThisItem.sanic_cm_tl_numero`
> > **`lblSecDir`** = **`cmpSecTitle`**: `Text="DIRECCIONES"` · `Padding=12,0,0,0`
> > **`galDirs`** *(Galería vertical)*: `Items=Filter(sanic_cm_tbl_direccion, sanic_cm_dr_clienteid = varCliente)` · `Height=CountRows(Self.AllItems)*52`
> > > plantilla igual, pero `lblDirVal.Text=ThisItem.sanic_cm_dr_direccion` y **`btnMapa`** = §C.7 con `<direccion>=ThisItem.sanic_cm_dr_direccion`

> **`lblSecCtas`** *(Etiqueta)*: `Text="Cuentas a cobrar (" & CountRows(varCliCuentas) & ")"` · `Size=gblT.fxs` · `FontWeight=FontWeight.Bold` · `Color=gblC.muted2`
> **`galMini`** *(Galería vertical)*: `Items=varCliCuentas` · `Ancho=Llenar` · `Alto=Auto`
> > plantilla **`conMini`** *(Cont. horizontal)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=12,12,14,14 · Justif=SpaceBetween · Wrap=Sí · Alinear=Center · Alto=Auto`
> > ```
> > OnSelect = Set(varAsig, ThisItem); Set(varCuenta, ThisItem.sanic_cm_as_cuentaid);
> >            Set(varDetTab,"detalle"); Set(varDetSrc,"cliente"); Navigate(scrDetalleCuenta, ScreenTransition.Cover)
> > ```
> > > **`conMiniIzq`** *(vertical)*: `lblMp`(`Text=ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_productoid.sanic_cm_pr_tipo`, `Size=gblT.fbase`, `FontWeight=Bold`) + `cmpBadge`(`<tipo>=ThisItem.sanic_cm_as_tipo.Value`) + `lblMc`(`Text=ThisItem.sanic_cm_as_numerocuenta`, `Size=gblT.fxs`, `Color=gblC.muted`)
> > > **`conMiniDer`** *(vertical, Alinear=End)*: `lblMr`(`Text=Moneda(ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_retironio, ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_retirousd)`, `Color=gblC.teal`, `FontWeight=Bold`) + `lblMm`(`Text=ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_diasmora & " días"`, `Size=gblT.fxs`, `Color=If(Or(ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_diasmora>90, ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_nivelmora>=4), gblC.redInk, gblC.muted2)`)

**TAB INTERACCIONES** (`Visible=(varCliTab="inter")`):
> **`galInterCli`** *(Galería vertical)*: `Items=Sort(Filter(sanic_cm_tbl_interaccion, sanic_cm_in_cuentaid.sanic_cm_ct_clienteid = varCliente), sanic_cm_in_fechacaptura, SortOrder.Descending)` · plantilla = **Tarjeta de interacción (§4.1)**.

### §4.1 — 🧩 Tarjeta de interacción (Cliente y Detalle)
> **`conICard`** *(Cont. vertical)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=12,12,14,14 · Gap=8 · Ancho=Llenar · Alto=Auto`
> > **`conIHead`** *(horizontal)*: `Justif=SpaceBetween · Wrap=Sí · Ancho=Llenar`
> > > **`lblITime`** *(Etiqueta)*: `Text="🕘 " & Text(ThisItem.sanic_cm_in_fechacaptura,"dd/mm · hh:mm")` · `Size=gblT.fxs` · `Color=gblC.muted` · `FontWeight=Semibold`
> > > **`lblIAcct`** *(Etiqueta)*: `Text=ThisItem.sanic_cm_in_cuentaid.sanic_cm_ct_productoid.sanic_cm_pr_tipo & " · " & ThisItem.sanic_cm_in_cuentaid.sanic_cm_ct_numerodecuenta` · `Size=gblT.fxs` · `Color=gblC.muted2` · *(en Detalle: `Visible=false`)*
> > **`lblICom`** *(Etiqueta)*: `Text=ThisItem.sanic_cm_in_comentario` · `Size=gblT.fbase` · `Color=gblC.ink` · `Alto=Auto`
> > **`conITags`** *(horizontal, Wrap=Sí, Gap=8, Alto=Auto)*:
> > > **`lblTagPago`** *(Etiqueta)*:
> > > ```
> > > Visible = CountRows(Filter(sanic_cm_tbl_pago, sanic_cm_pg_interaccion = ThisItem))>0
> > > Text    = "💵 " & Concat(Filter(sanic_cm_tbl_pago, sanic_cm_pg_interaccion = ThisItem),
> > >             If(sanic_cm_pg_moneda.Value="USD","US$ ","C$ ") & Text(sanic_cm_pg_monto,"#,##0.00") &
> > >             " (rec. " & sanic_cm_pg_numerorecibo & ")", " · ")
> > > Fill=gblC.accentBg · Color=gblC.accentInk · RadiusAll=10 · Padding=4,4,10,10 · Size=gblT.fxs · FontWeight=Semibold
> > > ```
> > > **`lblTagDatos`** *(Etiqueta)*: `Visible=ThisItem.sanic_cm_in_actualizainfo` · `Text="✏️ Datos actualizados"` · `Fill=gblC.datosBg` · `Color=gblC.diariaInk` · resto igual al tag pago
> > > **`lblTagGeo`** *(Etiqueta)*: `Text="📍 Ubicación"` · `Fill=gblC.screen` · `Color=gblC.muted` · resto igual

---

## 🟨 PARTE 5 — Pantalla DETALLE DE CUENTA

> **Árbol:**
> ```
> scrDetalleCuenta → conMaster
>   ├── conHeaderBack (cmpHeaderBack + cmpBadge + btnAa)
>   ├── conTabs (cmpTabBar)
>   ├── conBody (Scroll=Sí)
>   │    ├── [Detalle] cardRetiro · cardMora · cardContacto · cardProd
>   │    └── [Inter]   galInterDet
>   └── btnRegistrar (fijo abajo)
> ```

> **`scrDetalleCuenta`** *(Pantalla)*: `Fill=gblC.screen` · *(ya tenés `varAsig` y `varCuenta=varAsig.sanic_cm_as_cuentaid`)*
> **`conMaster`** *(vertical)*: `Width=Parent.Width · Height=Parent.Height · Gap=0`

**Header:** **`cmpHeaderBack`** (§C.9) con `lblDnm.Text=varCuenta.sanic_cm_ct_clienteid.sanic_cm_cl_nombre`,
`lblDac.Text=varCuenta.sanic_cm_ct_productoid.sanic_cm_pr_tipo & " · " & varCuenta.sanic_cm_ct_numerodecuenta`, `btnBack.OnSelect=Back()`,
+ a la derecha **`cmpBadge`** (`<tipo>=varAsig.sanic_cm_as_tipo.Value`) + `btnAa`.

**Pestañas:** **`cmpTabBar`**: `tabDetalle`(Text="Detalle", clave "detalle", `<varTab>=varDetTab`) ·
`tabInterDet`(Text=`"Interacciones (" & CountRows(Filter(sanic_cm_tbl_interaccion, sanic_cm_in_cuentaid = varCuenta)) & ")"`, clave "inter").

> **`conBody`** *(vertical)*: `Scroll=Sí · Padding=16,16,16,16 · Gap=12 · Ancho=Llenar · Alto=Llenar`

**TAB DETALLE** (`Visible=(varDetTab="detalle")`):

> **`cardRetiro`** = **`cmpTarjetaVerde`**:
> > **`lblRL`** *(Etiqueta)*: `Text="A cobrar (Retiro)"` · `Size=gblT.fxs` · `Color=gblC.muted2`
> > **`lblRB`** *(Etiqueta)*: `Text=Moneda(varCuenta.sanic_cm_ct_retironio, varCuenta.sanic_cm_ct_retirousd)` · `Size=gblT.fbig` · `FontWeight=Bold` · `Color=gblC.teal`
> > **`conGrid`** *(horizontal, Wrap=Sí, Gap=8, Padding=12,0,0,0, Ancho=Llenar)* — 3 celdas:
> > > cada **`celdaG<n>`** *(vertical)*: `Borde=gblC.cardBorder,1 · RadiusAll=10 · Padding=8,8,8,8 · Ancho=Llenar · Alinear=Center · Alto=Auto`
> > > > **`lblG<n>l`** *(Etiqueta)*: `Size=gblT.f2xs · Color=gblC.muted2 · Align=Center` — Text: `"Saldo vencido"` / `"Pago mínimo"` / `"Total (dolariz.)"`
> > > > **`lblG<n>v`** *(Etiqueta)*: `Size=gblT.fsm · FontWeight=Bold · Color=gblC.ink2 · Align=Center` — Text:
> > > > `Moneda(varCuenta.sanic_cm_ct_saldovencidonio, varCuenta.sanic_cm_ct_saldovencidousd)` /
> > > > `Moneda(varCuenta.sanic_cm_ct_pagominimonio, varCuenta.sanic_cm_ct_pagominimousd)` /
> > > > `"US$ " & Text(varCuenta.sanic_cm_ct_totalsaldovencido, "#,##0.00")`
> > **`lblRiesgo`** *(Etiqueta)*: `Text="En riesgo (contagiado): US$ " & Text(varCuenta.sanic_cm_ct_contagiadousd, "#,##0.00")` · `Size=gblT.fxs` · `Color=gblC.muted` · `Padding=10,0,0,0`

> **`cardMora`** = **`cmpTarjeta`**: **`lblSecMora`** (`cmpSecTitle`, Text="MORA") + 4 filas **`cmpKV`** (§C.6):
> > Días en mora → `<clave>="Días en mora"`, `<valor>=varCuenta.sanic_cm_ct_diasmora & " días · Nivel " & varCuenta.sanic_cm_ct_nivelmora` *(en `lblV` poné además `Color=If(Or(varCuenta.sanic_cm_ct_diasmora>90, varCuenta.sanic_cm_ct_nivelmora>=4), gblC.redInk, gblC.ink)`)*
> > Fecha de pago → `Text(varCuenta.sanic_cm_ct_fechapago,"dd/mm/yyyy")` · Último pago → `Text(varCuenta.sanic_cm_ct_fechaultimopago,"dd/mm/yyyy")` · Ciclo → `varCuenta.sanic_cm_ct_ciclo`

> **`cardContacto`** = igual que en Cliente, pero los filtros con `varCuenta.sanic_cm_ct_clienteid`:
> `galTels.Items=Filter(sanic_cm_tbl_telefono, sanic_cm_tl_clienteid = varCuenta.sanic_cm_ct_clienteid)` (idem `galDirs` con `sanic_cm_tbl_direccion`).

> **`cardProd`** = **`cmpTarjeta`** con 3 filas **`cmpKV`**: Producto → `varCuenta.sanic_cm_ct_productoid.sanic_cm_pr_tipo` ·
> Fiador → `varCuenta.sanic_cm_ct_fiador.sanic_cm_fi_nombre` · Nota → `varCuenta.sanic_cm_ct_nota`.

**TAB INTERACCIONES** (`Visible=(varDetTab="inter")`):
> **`galInterDet`** *(Galería vertical)*: `Items=Sort(Filter(sanic_cm_tbl_interaccion, sanic_cm_in_cuentaid = varCuenta), sanic_cm_in_fechacaptura, SortOrder.Descending)` · plantilla = **Tarjeta de interacción (§4.1)** con `lblIAcct.Visible=false`.

> **`btnRegistrar`** *(Botón)* · en `conMaster`, DESPUÉS de `conBody` (queda fijo abajo):
> ```
> Text="＋ Registrar interacción"  Fill=gblC.teal  Color=gblC.white  Size=gblT.fmd  FontWeight=FontWeight.Bold
> RadiusAll=14  Height=54  Ancho=Llenar
> OnSelect = Set(varDetSrc,"detalle"); Navigate(scrInteraccion, ScreenTransition.Cover)
> ```

---

## 🟧 PARTE 6 — Pantalla REGISTRAR INTERACCIÓN (¡acá se ESCRIBE en Dataverse!)

> **Árbol:**
> ```
> scrInteraccion → conMaster
>   ├── conHeaderBack
>   └── conBody (Scroll=Sí)
>        cardCtx · lblComL · txtComentario · galChips
>        · rowPago(tgPago) · secPago(galLineasPago · btnAddPago)
>        · rowDatos(tgDatos) · secDatos(7 inputs)
>        · cardGeo · lblMeta · btnGuardar · lblHint
> ```

> **`scrInteraccion`** *(Pantalla)*: `Fill=gblC.screen` · `OnVisible = Clear(colLineasPago)`
> **`conMaster`** *(vertical)*: `Width=Parent.Width · Height=Parent.Height · Gap=0`

**Header:** **`cmpHeaderBack`**: `lblDnm.Text="Registrar interacción"`, `lblDac.Text=varCuenta.sanic_cm_ct_clienteid.sanic_cm_cl_nombre`, `btnBack.OnSelect=Back()`.

> **`conBody`** *(vertical)*: `Scroll=Sí · Padding=16,16,16,16 · Gap=12 · Ancho=Llenar · Alto=Llenar`

> **`cardCtx`** *(Cont. horizontal)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=12,12,14,14 · Justif=SpaceBetween · Wrap=Sí · Alto=Auto`
> > **`conCtxIzq`** *(vertical)*: `lblCl`(`Text=varCuenta.sanic_cm_ct_productoid.sanic_cm_pr_tipo`, `FontWeight=Bold`, `Size=gblT.fbase`) + `lblCc2`(`Text=varCuenta.sanic_cm_ct_numerodecuenta`, `Size=gblT.fxs`, `Color=gblC.muted`)
> > **`conCtxDer`** *(vertical, Alinear=End)*: `lblCrl`(`Text="A cobrar"`, `Size=gblT.f2xs`, `Color=gblC.muted2`) + `lblCrv`(`Text=Moneda(varCuenta.sanic_cm_ct_retironio, varCuenta.sanic_cm_ct_retirousd)`, `Size=gblT.fmd`, `FontWeight=Bold`, `Color=gblC.teal`)

> **`lblComL`** *(Etiqueta)*: `Text="¿Qué pasó? *"` · `Size=gblT.fsm` · `FontWeight=Bold` · `Color=gblC.ink2`
> **`txtComentario`** *(Entrada de texto)*:
> ```
> Mode=TextMode.MultiLine  HintText="Escribí el comentario de la interacción…"  Default=varCom
> Height=96  Borde=gblC.borderSoft,1  RadiusAll=12  Size=gblT.fbase  Color=gblC.ink  Ancho=Llenar
> ```
> **`galChips`** *(Galería horizontal)*:
> ```
> Items=["Promesa de pago","No localizado","Cliente pagó","No tiene dinero hoy","Negocio cerrado","Pidió arreglo"]
> Height=40  Ancho=Llenar
> ```
> > plantilla **`btnChip`** *(Botón)*:
> > ```
> > Text=ThisItem.Value  Fill=gblC.chipBg  Color=gblC.teal  RadiusAll=16  Padding=7,7,13,13  Size=gblT.fxs
> > OnSelect = Set(varCom, If(IsBlank(varCom), ThisItem.Value, Trim(varCom) & ". " & ThisItem.Value))
> > ```

> **`rowPago`** *(Cont. horizontal)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=14,14,16,16 · Justif=SpaceBetween · Alinear=Center · Wrap=Sí · Alto=Auto`
> > **`conPagoIzq`** *(vertical)*: `lblPagoT`(`Text="💵 Aplica pago"`, `Size=gblT.fbase`, `FontWeight=Semibold`) + `lblPagoD`(`Text="El cliente pagó algo en esta visita"`, `Size=gblT.fxs`, `Color=gblC.muted2`)
> > **`tgPago`** *(Alternar)*: `Default=false`
> **`secPago`** *(Cont. vertical)*: `Visible=tgPago.Value · Padding=2,2,12,0 · Borde-izq=gblC.teal,3 · Gap=10 · Ancho=Llenar · Alto=Auto`
> > **`lblPagoTit`** *(Etiqueta)*: `Text="Pago(s)"` · `Size=gblT.fsm` · `FontWeight=Bold` · `Color=gblC.teal`
> > **`galLineasPago`** *(Galería vertical)*: `Items=colLineasPago · Ancho=Llenar · Alto=Auto`
> > > plantilla **`conLinea`** *(vertical)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=10,10,10,10 · Gap=8 · Alto=Auto`
> > > > fila 1 *(horizontal, Gap=8)*: **`txtMonto`**(`Format=TextFormat.Number`, `HintText="Monto"`, `Ancho=Llenar`, `OnChange=Patch(colLineasPago, ThisItem, {monto: Value(Self.Text)})`) + **`drpMoneda`**(`Items=["NIO","USD"]`, `OnChange=Patch(colLineasPago, ThisItem, {moneda: Self.Selected.Value})`)
> > > > fila 2 *(horizontal, Gap=8)*: **`txtRecibo`**(`HintText="N° de recibo"`, `Ancho=Llenar`, `OnChange=Patch(colLineasPago, ThisItem, {recibo: Self.Text})`) + **`btnQuitar`**(`Text="✕"`, `Fill=gblC.redBg`, `Color=gblC.redInk`, `RadiusAll=10`, `Width=42`, `OnSelect=Remove(colLineasPago, ThisItem)`)
> > **`btnAddPago`** *(Botón)*:
> > ```
> > Text="＋ Agregar pago"  Fill=gblC.chipBg  Color=gblC.teal  BorderStyle=BorderStyle.Dashed
> > BorderColor=ColorValue("#A7C4C4")  BorderThickness=1  RadiusAll=10  Ancho=Llenar  Padding=11,11,11,11
> > OnSelect = Collect(colLineasPago, {monto:0, moneda:"NIO", recibo:""})
> > ```

> **`rowDatos`** *(Cont. horizontal)*: igual que `rowPago` con `lblDatosT.Text="✏️ Actualizar datos del cliente"`, `lblDatosD.Text="Cambió un teléfono, dirección o correo"`, **`tgDatos`** *(Alternar, Default=false)*.
> **`secDatos`** *(Cont. vertical)*: `Visible=tgDatos.Value · Padding=2,2,12,0 · Borde-izq=gblC.teal,3 · Gap=10 · Ancho=Llenar`
> > **`lblDatosTit`** *(Etiqueta)*: `Text="Nuevos datos · dejá en blanco lo que no cambió"` · `Size=gblT.fsm` · `FontWeight=Bold` · `Color=gblC.teal`
> > **7 campos** — cada uno: label *(Etiqueta `Size=gblT.fxs · Color=gblC.muted`)* + **Entrada de texto** *(Ancho=Llenar)*. Nombres:
> > `inTelCasa` (Teléfono casa) · `inTelCel` (Teléfono celular) · `inTelTrab` (Teléfono trabajo) ·
> > `inDirCasa` (Dirección casa) · `inDirEC` (Dirección estado de cuenta) · `inDirTrab` (Dirección trabajo) · `inCorreo` (Correo electrónico)

> **`cardGeo`** *(Cont. horizontal)*: `Fill=gblC.accentBg · Borde=gblC.accentBorder,1 · RadiusAll=12 · Padding=12,12,14,14 · Gap=10 · Alinear=Center · Alto=Auto`
> > **`lblGeoIco`** *(Etiqueta)*: `Text="📍"` · `Size=20*gblScale`
> > **`conGeoTxt`** *(vertical)*: `lblGeoT`(`Text="Ubicación capturada"`, `Color=gblC.accentInk`, `FontWeight=Semibold`, `Size=gblT.fbase`) + `lblGeoC`(`Text=Text(Location.Latitude,"0.0000") & ", " & Text(Location.Longitude,"0.0000") & " · automática"`, `Size=gblT.fxs`, `Color=gblC.accentInk2`)

> **`lblMeta`** *(Etiqueta)*: `Text="Se registrará: " & Text(Now(),"dd/mm/yyyy hh:mm") & " · " & varCuenta.sanic_cm_ct_clienteid.sanic_cm_cl_nombre` · `Size=gblT.fxs` · `Color=gblC.muted2` · `Align=Center`

> **`btnGuardar`** *(Botón)*:
> ```
> Text = "Guardar interacción" & If(tgPago.Value," + pago","") & If(tgDatos.Value," + datos","")
> Fill=gblC.teal  Color=gblC.white  RadiusAll=14  Height=54  Size=gblT.fmd  FontWeight=FontWeight.Bold  Ancho=Llenar
> DisplayMode =
>    If(!IsBlank(txtComentario.Text)
>       && (!tgPago.Value || CountRows(Filter(colLineasPago, monto>0 && !IsBlank(Trim(recibo))))>0)
>       && (!tgDatos.Value || Or(!IsBlank(inTelCasa.Text), !IsBlank(inTelCel.Text), !IsBlank(inTelTrab.Text),
>                                !IsBlank(inDirCasa.Text), !IsBlank(inDirEC.Text), !IsBlank(inDirTrab.Text),
>                                !IsBlank(inCorreo.Text))),
>       DisplayMode.Edit, DisplayMode.Disabled)
> OnSelect =
>    Set(varInt, Patch(sanic_cm_tbl_interaccion, Defaults(sanic_cm_tbl_interaccion), {
>       sanic_cm_in_cuentaid: varCuenta, sanic_cm_in_agente: gblAgente, sanic_cm_in_comentario: txtComentario.Text,
>       sanic_cm_in_fechacaptura: Now(), sanic_cm_cd_aplicapago: tgPago.Value, sanic_cm_in_actualizainfo: tgDatos.Value,
>       sanic_cm_in_latitud: Location.Latitude, sanic_cm_in_longitud: Location.Longitude,
>       sanic_cm_in_estadograbacion: 'Estado grabación interacción (sanic_cm_choice_estadograbacioninteraccion)'.Pendiente }));
>    If(tgPago.Value,
>       ForAll(Filter(colLineasPago, monto>0) As L,
>          Patch(sanic_cm_tbl_pago, Defaults(sanic_cm_tbl_pago), {
>             sanic_cm_pg_interaccion: varInt, sanic_cm_pg_monto: L.monto,
>             sanic_cm_pg_moneda: Switch(L.moneda,"USD",'Moneda en pago (sanic_choice_cm_monedaenpago)'.USD,'Moneda en pago (sanic_choice_cm_monedaenpago)'.NIO),
>             sanic_cm_pg_numerorecibo: L.recibo, sanic_cm_pg_fecha: Now() })));
>    If(tgDatos.Value,
>       Patch(sanic_cm_tbl_actualizaciondatos, Defaults(sanic_cm_tbl_actualizaciondatos), {
>          sanic_cm_ad_interaccionid: varInt, sanic_cm_ad_telefonocasa: inTelCasa.Text, sanic_cm_ad_telefonocelular: inTelCel.Text,
>          sanic_cm_ad_telefonotrabajo: inTelTrab.Text, sanic_cm_ad_direccioncasa: inDirCasa.Text,
>          sanic_cm_ad_direccionec: inDirEC.Text, sanic_cm_ad_direcciontrabajo: inDirTrab.Text, sanic_cm_ad_correoelectronico: inCorreo.Text }));
>    Set(varCom, "");
>    Notify("✓ Guardado offline. Se sincroniza al volver la señal.", NotificationType.Success);
>    Back()
> ```
> ⚠️ Los nombres de Choice (`'Estado grabación interacción (sanic_cm_choice_estadograbacioninteraccion)'.Pendiente`, `'Moneda en pago (sanic_choice_cm_monedaenpago)'.NIO/.USD`)
> los **autocompleta** Power Apps. Los campos de `sanic_cm_tbl_actualizaciondatos` confirmalos en el
> [componente 13](../referencia/componentes/13-tabla-actualizacion-datos.md).

> **`lblHint`** *(Etiqueta)*: `Text=If(IsBlank(txtComentario.Text),"Falta: comentario","")` · `Color=gblC.hint` · `Size=gblT.fxs` · `Align=Center`

---

## 🟪 PARTE 7 — Pantalla INICIO

> **`scrInicio`** *(Pantalla)*: `Fill=gblC.screen`
> ```
> OnVisible =
>    Set(varIntHoy, Filter(sanic_cm_tbl_interaccion, DateValue(sanic_cm_in_fechacaptura) = Today()));
>    Set(varPagHoy, Filter(sanic_cm_tbl_pago, DateValue(sanic_cm_pg_fecha) = Today()));
>    Set(varCobN, Sum(Filter(varPagHoy, sanic_cm_pg_moneda.Value="NIO"), sanic_cm_pg_monto));
>    Set(varCobU, Sum(Filter(varPagHoy, sanic_cm_pg_moneda.Value="USD"), sanic_cm_pg_monto))
> ```
> **`conMaster`** *(vertical)*: `Width=Parent.Width · Height=Parent.Height · Gap=0`

**Header:** **`cmpHeaderVerde`** (§C.8) **sin** tarjeta total: `lblTitulo.Text="Hola, " & gblAgente.sanic_cm_ag_nombre`,
`lblSub.Text=Text(Today(),"dddd dd ""de"" mmmm")`, `cmpSyncPill.OnSelect=Set(varSyncBack,"inicio"); Navigate(scrSincronizacion)`.

> **`conBody`** *(vertical)*: `Scroll=Sí · Padding=16,16,16,16 · Gap=12 · Ancho=Llenar · Alto=Llenar`

> **`cardHero`** *(Cont. vertical)*: `Fill=gblC.teal · RadiusAll=16 · Padding=16,16,16,16 · Gap=4 · Ancho=Llenar · Alto=Auto`
> ```
> OnSelect = Navigate(scrPagos, ScreenTransition.Cover)
> ```
> > **`lblHL`** *(Etiqueta)*: `Text="Cobrado hoy"` · `Size=gblT.fsm` · `Color=gblC.tealSub`
> > **`lblHA`** *(Etiqueta)*: `Text=Moneda(varCobN, varCobU)` · `Size=gblT.fbig` · `FontWeight=Bold` · `Color=gblC.white`
> > **`lblHS`** *(Etiqueta)*: `Text=CountRows(varPagHoy) & " pago(s) · " & CountRows(varIntHoy) & " interacción(es) · Ver detalle ›"` · `Size=gblT.fxs` · `Color=gblC.tealSub`

> **`conStats`** *(Cont. horizontal)*: `Wrap=Sí · Gap=10 · Ancho=Llenar · Alto=Auto` — 3 cajitas:
> > cada **`statc<n>`** *(vertical)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=12,12,12,12 · Ancho=Llenar · Alinear=Center · Alto=Auto`
> > > **`lblSv`** *(Etiqueta)*: `Size=gblT.flg · FontWeight=Bold · Color=gblC.ink · Align=Center` — Text: `CountRows(varIntHoy)` / `CountRows(Distinct(varIntHoy, sanic_cm_in_cuentaid))` / `CountRows(Distinct(varIntHoy, sanic_cm_in_cuentaid.sanic_cm_ct_clienteid))`
> > > **`lblSl`** *(Etiqueta)*: `Size=gblT.fxs · Color=gblC.muted2 · Align=Center` — Text: `"Interacciones"` / `"Cuentas"` / `"Clientes"`

> **`conResult`** *(horizontal, Justif=SpaceBetween, Wrap=Sí, Alinear=Center)*: `lblResL`(`Text="Interacciones de hoy"`, `Size=gblT.fsm`, `Color=gblC.muted`, `FontWeight=Semibold`) + `conToggle` (igual al de Cartera, pero `Set(varInicioModo, …)`)

> **`galDesglose`** *(Galería vertical)*:
> ```
> Items = If(varInicioModo="cliente",
>           GroupBy(AddColumns(varIntHoy, "cliNom", sanic_cm_in_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre,
>                              "cliRec", sanic_cm_in_cuentaid.sanic_cm_ct_clienteid), "cliNom","cliRec","Items"),
>           GroupBy(AddColumns(varIntHoy, "prod", sanic_cm_in_cuentaid.sanic_cm_ct_productoid.sanic_cm_pr_tipo,
>                              "cta", sanic_cm_in_cuentaid.sanic_cm_ct_numerodecuenta,
>                              "cliNom", sanic_cm_in_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre,
>                              "ctaRec", sanic_cm_in_cuentaid), "prod","cta","cliNom","ctaRec","Items"))
> ```
> > plantilla **`conBrow`** *(horizontal)*: `Fill=gblC.white · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=12,12,14,14 · Justif=SpaceBetween · Alinear=Center · Gap=10 · Alto=Auto`
> > ```
> > OnSelect = If(varInicioModo="cliente",
> >              Set(varCliente, ThisItem.cliRec); Set(varCliTab,"inter"); Navigate(scrCliente, ScreenTransition.Cover),
> >              Set(varCuenta, ThisItem.ctaRec); Set(varDetTab,"inter"); Set(varDetSrc,"inicio"); Navigate(scrDetalleCuenta, ScreenTransition.Cover))
> > ```
> > > **`conBrowIzq`** *(vertical)*: `lblBN`(`Text=If(varInicioModo="cliente", ThisItem.cliNom, ThisItem.prod)`, `Size=gblT.fbase`, `FontWeight=Semibold`) + `lblBS`(`Text=If(varInicioModo="cliente", "", ThisItem.cliNom & " · " & ThisItem.cta)`, `Size=gblT.fxs`, `Color=gblC.muted`)
> > > **`conBrowDer`** *(horizontal, Alinear=Center, Gap=6)*: `lblBC`(`Text=CountRows(ThisItem.Items) & " interac."`, `Fill=gblC.accentBg`, `Color=gblC.teal`, `FontWeight=Bold`, `RadiusAll=10`, `Padding=5,5,11,11`, `Size=gblT.fsm`) + `lblChev`(`Text="›"`, `Color=gblC.phoneBorder`, `Size=gblT.fmd`)

**Barra inferior:** **`cmpNav`** (Inicio activo).

---

## 🟫 PARTE 8 — Pantalla PAGOS DEL DÍA

> **`scrPagos`** *(Pantalla)*: `Fill=gblC.screen`
> ```
> OnVisible =
>    Set(varPagHoy, Filter(sanic_cm_tbl_pago, DateValue(sanic_cm_pg_fecha) = Today()));
>    Set(varSumN, Sum(Filter(varPagHoy, sanic_cm_pg_moneda.Value="NIO"), sanic_cm_pg_monto));
>    Set(varSumU, Sum(Filter(varPagHoy, sanic_cm_pg_moneda.Value="USD"), sanic_cm_pg_monto))
> ```
> **`conMaster`** *(vertical)* → **Header** `cmpHeaderBack` (`lblDnm="Pagos de hoy"`, `lblDac="Por cuenta y por moneda"`, `btnBack.OnSelect=Back()`) → **`conBody`** *(Scroll=Sí, Padding=16, Gap=12)*.

> **`cardTotalPag`** = **`cmpTarjetaVerde`**: `lblTL`(Text="Total cobrado") + `lblTB`(`Text=Moneda(varSumN, varSumU)`, `Size=gblT.fbig`, `FontWeight=Bold`, `Color=gblC.teal`).
> **`galGrupos`** *(Galería vertical)*:
> ```
> Items = GroupBy(AddColumns(varPagHoy,
>            "cliNom", sanic_cm_pg_interaccion.sanic_cm_in_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre,
>            "prod",   sanic_cm_pg_interaccion.sanic_cm_in_cuentaid.sanic_cm_ct_productoid.sanic_cm_pr_tipo,
>            "cta",    sanic_cm_pg_interaccion.sanic_cm_in_cuentaid.sanic_cm_ct_numerodecuenta),
>          "cliNom","prod","cta","Lineas")
> ```
> > plantilla **`cardGrupo`** = **`cmpTarjeta`**:
> > > **`lblGCli`** *(Etiqueta)*: `Text=ThisItem.cliNom` · `Size=gblT.fxs` · `FontWeight=Bold` · `Color=gblC.muted2`
> > > **`lblGProd`** *(Etiqueta)*: `Text=ThisItem.prod & " · " & ThisItem.cta` · `Size=gblT.fxs` · `Color=gblC.muted`
> > > **`galLineas`** *(Galería vertical)*: `Items=ThisItem.Lineas · Height=CountRows(Self.AllItems)*46`
> > > > plantilla **`conLin`** *(horizontal, Justif=SpaceBetween)*: `lblLV`(`Text=If(ThisItem.sanic_cm_pg_moneda.Value="USD","US$ ","C$ ") & Text(ThisItem.sanic_cm_pg_monto,"#,##0.00")`, `Size=gblT.fbase`, `Color=gblC.ink`) + `lblLR`(`Text="Recibo " & ThisItem.sanic_cm_pg_numerorecibo & " · " & Text(ThisItem.sanic_cm_pg_fecha,"hh:mm")`, `Size=gblT.f2xs`, `Color=gblC.muted2`)

---

## 🟦 PARTE 9 — Pantalla SINCRONIZACIÓN

> **`scrSincronizacion`** *(Pantalla)*: `Fill=gblC.screen`
> ```
> OnVisible =
>    Set(varPend, CountRows(Filter(sanic_cm_tbl_interaccion, IsBlank(sanic_cm_in_numerointeraccion)))
>               + CountRows(Filter(sanic_cm_tbl_pago, IsBlank(sanic_cm_pg_numero))));
>    Set(varEstado, If(!Connection.Connected, "off", If(varPend>0, "pend", "ok")));
>    Set(varCapHoy, Filter(sanic_cm_tbl_interaccion, DateValue(sanic_cm_in_fechacaptura) = Today()))
> ```
> **Header** `cmpHeaderBack`: `lblDnm="Sincronización"`, `lblDac="Estado de tus capturas"`, `btnBack.OnSelect=Navigate(If(varSyncBack="inicio", scrInicio, scrCartera))`. → **`conBody`** *(Scroll=Sí, Padding=16, Gap=12)*.

> **`cardEstado`** *(Cont. horizontal)*: `RadiusAll=16 · Padding=16,16,16,16 · Gap=14 · Alinear=Center · Ancho=Llenar · Alto=Auto`
> ```
> Fill = Switch(varEstado, "ok",gblC.teal, "pend",gblC.incobInk, gblC.offGray)
> ```
> > **`lblEico`** *(Etiqueta)*: `Text=Switch(varEstado,"ok","✓","pend","🔼","📴")` · `Size=28*gblScale` · `Color=gblC.white`
> > **`conEst`** *(vertical)*: `lblE1`(`Text=Switch(varEstado,"ok","Sincronizado","pend","Pendiente de subir","Sin conexión")`, `Size=gblT.flg`, `FontWeight=Bold`, `Color=gblC.white`) + `lblE2`(`Text=Switch(varEstado,"ok","Todo al día con el servidor","pend", varPend & " captura(s) por subir", varPend & " captura(s) en el dispositivo")`, `Size=gblT.fsm`, `Color=gblC.white`)

> **`cardDet`** = **`cmpTarjeta`** con 3 **`cmpKV`**: Conexión → `If(Connection.Connected,"En línea","Sin señal")` · Última sincronización → `"—"` · Pendientes por subir → `varPend & " captura(s)"`.

> **`btnBuscar`** *(Botón)*:
> ```
> Text = If(Connection.Connected,"↻ Buscar actualizaciones","Sin conexión — no se puede sincronizar")
> Fill=gblC.teal  Color=gblC.white  RadiusAll=14  Height=54  Ancho=Llenar
> DisplayMode = If(Connection.Connected, DisplayMode.Edit, DisplayMode.Disabled)
> OnSelect = Refresh(sanic_cm_tbl_interaccion); Refresh(sanic_cm_tbl_pago); Refresh(sanic_cm_tbl_actualizaciondatos); Refresh(sanic_cm_tbl_asignacion); Refresh(sanic_cm_tbl_cuenta); Refresh(sanic_cm_tbl_cuentapan);
>             Set(varRecargarCartera, true); Set(varCliCargado, Blank()); Set(varPend, <conteo de pendientes>); Notify("Actualizando…", NotificationType.Information)
> ```
> `varRecargarCartera = true` hace que la Cartera, al volver a mostrarse (`OnVisible`), recargue también `colCuentas`, `colDirecciones` y `colTelefonos`.

> **`btnSyncCompleta`** *(Botón)*: `Text="⟳ Sincronización completa"` · `Fill=gblC.chipBg` · `Color=gblC.ink2` · `RadiusAll=14` · `Height=54` · `Ancho=Llenar` · `Visible=Host.OfflineEnabled`
> ```
> OnSelect = ShowHostInfo(HostInfo.OfflineSync)
> ```
> Abre el estado del dispositivo; ahí el cobrador toca «Buscar actualizaciones» (Check for updates) para revisar toda su cartera y quitar lo que ya no le corresponde. Debajo va `lblSyncCompletaHint` (`Size=gblT.fxs`, `Color=gblC.muted`, `Visible=Host.OfflineEnabled`) con esa explicación.

> **`lblCapTit`** *(Etiqueta `cmpSecTitle`)*: `Text="Capturas de hoy"`
> **`galCap`** *(Galería vertical)*: `Items=varCapHoy`
> > plantilla **`conCapRow`** *(horizontal, Justif=SpaceBetween, igual a conBrow)*:
> > > izquierda *(vertical)*: `lblCapN`(`Text=ThisItem.sanic_cm_in_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre & " · " & ThisItem.sanic_cm_in_cuentaid.sanic_cm_ct_productoid.sanic_cm_pr_tipo`, `Size=gblT.fbase`, `FontWeight=Semibold`) + `lblCapH`(`Text=Text(ThisItem.sanic_cm_in_fechacaptura,"hh:mm")`, `Size=gblT.fxs`, `Color=gblC.muted`)
> > > **`lblPendTag`** *(Etiqueta)*:
> > > ```
> > > Text  = If(IsBlank(ThisItem.sanic_cm_in_numerointeraccion),"sin Nº aún","Nº asignado")
> > > Fill  = If(IsBlank(ThisItem.sanic_cm_in_numerointeraccion), gblC.incobBg, gblC.accentBg)
> > > Color = If(IsBlank(ThisItem.sanic_cm_in_numerointeraccion), gblC.incobInk, gblC.accentInk)
> > > RadiusAll=10  Padding=4,4,10,10  Size=gblT.fxs  FontWeight=FontWeight.Bold
> > > ```

> **`cardNota`** *(Cont. vertical)*: `Fill=gblC.noteBg · Borde=gblC.cardBorder,1 · RadiusAll=12 · Padding=12,12,14,14 · Alto=Auto`
> > **`lblNota`** *(Etiqueta)*: `Size=gblT.fxs · Color=gblC.muted · Alto=Auto` — `Text` = explica que `INT-/PAG-` los asigna el servidor al sincronizar (por eso "sin Nº aún") y que el conteo real y el forzado viven en el **ícono de globo** del player (Device Status); esta pantalla es el complemento. *(Ver doc 04, §8.8.)*

---

## ⬜ PARTE 10 — Pantalla STATS (placeholder)

> **`scrStats`** *(Pantalla)*: `Fill=gblC.screen` → **`conMaster`** → **Header** `cmpHeaderVerde` (`lblTitulo="Mis Estadísticas"`, solo `btnAa`) → **`lblPh`** *(Etiqueta)*: `Text="📊 Ranking y gamificación — próximamente —"` · `Size=gblT.fbase` · `Color=gblC.muted2` · `Align=Align.Center` · `Padding=60,60,24,24` → **`cmpNav`** (Stats activo).

---

## ♿ PARTE 11 — Accesibilidad (botón "Aa")

Ya está todo en **§C.13** (`btnAa` + `conSheet`). Agregá `conSheet` como **último hijo de `conMaster`** en cada
pantalla con header (o convertilo en un **Componente** reutilizable y pegalo en todas).

> Como **todos** los `Size` usan `gblT.*`, al tocar un nivel la app se reescala sola. Con `Wrap=Sí` en las filas,
> nada se rompe. **Eso** es accesibilidad bien hecha.

---

## ✅ Checklist final

- [ ] Parte 0–2: app creada, **11 tablas** conectadas, perfil offline, `OnStart` corrido, `gblAgente` no vacío, tema en Segoe UI.
- [ ] **§C catálogo:** construiste los componentes una vez (tarjeta, badge, header, nav, tabs, sync pill, sheet).
- [ ] Parte 3: Cartera anda con datos reales (agrupar, filtrar, buscar, reordenar, abrir).
- [ ] Partes 4–5: Cliente y Detalle con pestañas y contacto (Llamar/Mapa).
- [ ] Parte 6: registrar interacción **crea filas reales** en `sanic_cm_tbl_interaccion` (+ pago / datos).
- [ ] Partes 7–8: Inicio y Pagos. Parte 9: Sync (Connection + pendientes). Parte 10: Stats. Parte 11: "Aa".
- [ ] **Probá en el player móvil, offline:** leer, reordenar, capturar sin señal, recuperar señal, sincronizar.

> **Última de maestro:** un control que no se ve suele ser un `Visible` en `false`, un `Alto` que quedó en 0, o
> un control fuera de su contenedor (Receta 6). Si un Choice da error, usá el **autocompletado** para el nombre
> exacto. Andá tranquilo, pantalla por pantalla. **Se entiende, ¿no?** 💪
