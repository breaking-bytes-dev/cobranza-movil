# App del Localizador — Guía de Construcción Canvas (0 → 100)

> **Propósito de este documento.** Reproducir **idénticamente** el prototipo
> [`mockups/prototipo-localizador.html`](../mockups/prototipo-localizador.html) como una **Canvas App** de Power
> Platform. Acá está **todo**: colores (hex exactos), tipografías y tamaños, radios, sombras, espaciados, cada
> control, cada propiedad y cada evento. Si seguís esta guía al pie, el resultado es **pixel-fiel** al prototipo.
>
> **Cómo leerla:** primero el **sistema de diseño** (§2) y los **componentes reutilizables** (§6) — son la base.
> Después, **pantalla por pantalla** (§8), donde cada control referencia los *tokens* del sistema de diseño.
> No repetimos un hex 200 veces: lo definimos una vez como token y lo referenciamos.

---

## 0. Convenciones de esta guía

- **Tokens de color:** `gblC.<nombre>` (definidos en `App.OnStart`, §5). Donde digo `gblC.teal` = `#0F6B6B`.
- **Tokens de tamaño de fuente:** `gblT.<nombre>` (escalan con accesibilidad, §3). Donde digo `gblT.fmd` = `15 × gblScale`.
- **Medidas (radio, padding, gap, alto):** en **unidades de Power Apps** (equivalen a px del prototipo, 1:1).
- **Layout:** se construye con **contenedores** (`Container` / galería) en lugar de posiciones absolutas, porque
  el prototipo es *flexbox* y porque la **accesibilidad exige reflow** (§3). Cada pantalla es un árbol de
  contenedores **verticales/horizontales** con `Padding`, `Gap`, `Fill` y controles hoja.
- **Fuente:** la familia del prototipo es `-apple-system, "Segoe UI", Roboto, …`. En Canvas usá la fuente del
  tema **`Segoe UI`** (la más cercana). Definí `Set(gblFont, Font.'Segoe UI')` y usá `Font = gblFont` en todo.
- **Pesos:** el prototipo usa 400/600/700/800. Mapeo Canvas: `400 → FontWeight.Normal`, `600 → FontWeight.Semibold`,
  `700/800 → FontWeight.Bold`.

---

## 1. Plataforma, creación y formato

| Decisión | Valor | Por qué |
|----------|-------|---------|
| Tipo de app | **Canvas** | Offline-first real (player nativo), captura por formularios (DEC-001). |
| Formato | **Teléfono** (portrait) | El localizador trabaja a una mano, en moto, en la calle. |
| `App.Width` de referencia | el prototipo mide **400 × 824**; el lienzo phone de Canvas es **640 × 1136** | Las **proporciones** son lo que importa: construimos con contenedores **`Width = Parent.Width`**, así escala solo. Las medidas de fuente/radio/padding son **absolutas** (no se multiplican). |
| Orientación | Bloqueada en **Portrait** | `App > Settings > Display > Orientation = Portrait`, *Lock* = On. |
| Scale to fit / Lock aspect | **On / On** | Mantiene la relación en cualquier teléfono. |
| Tema | Custom: fuente `Segoe UI`, color de acento `#0F6B6B` | Coherencia con el header verde petróleo. |

**Estructura raíz de cada pantalla** (todas la comparten):

```
Screen  (Fill = gblC.screen  →  #F2F4F7)
└── conMain  (Container vertical, Width=Parent.Width, Height=Parent.Height)
    ├── conHeader     (el header verde — fijo arriba)
    ├── galScroll / conBody  (zona scrolleable, Fill=gblC.screen, padding 16)
    └── conNav        (barra inferior — solo en Inicio/Cartera/Stats; oculta en pantallas apiladas)
```

> En pantallas **apiladas** (Cliente, Detalle, Interacción, Pagos, Sync) **no hay barra inferior**: se reemplaza
> el header verde por el **`conHeaderBack`** (con flecha ←), igual que en el prototipo (`.dhdr`).

---

## 2. Sistema de diseño (DESIGN TOKENS)

### 2.1 Paleta de colores — hex exactos

| Token (`gblC.`) | Hex | Dónde se usa |
|-----------------|-----|--------------|
| `teal` | `#0F6B6B` | Header, botones primarios, montos a cobrar, estados activos, bordes activos. |
| `tealSub` | `#BFE3E3` | Texto secundario **sobre** el header verde (subtítulos, labels). |
| `screen` | `#F2F4F7` | Fondo de pantalla; **divisores**; fondo de “mora normal”. |
| `card` | `#FFFFFF` | Tarjetas, inputs, mini-cards. |
| `cardBorder` | `#EAECF0` | Borde de todas las tarjetas. |
| `borderSoft` | `#E4E7EC` | Borde de inputs/búsqueda/segmentos; fondo del toggle de agrupación. |
| `hairline` | `#F6F7F9` | Línea divisoria entre filas de contacto (`.crow`). |
| `ink` | `#101828` | Texto principal (nombres, montos en input, comentarios). |
| `ink2` | `#344054` | Texto fuerte secundario (valores de grid, labels de campo). |
| `ink3` | `#475467` | Segmento inactivo; texto de “mora normal”. |
| `muted` | `#667085` | Texto secundario (cuentas, subtítulos de fila). |
| `muted2` | `#98A2B3` | Labels apagados, placeholders, flechas de reordenar, chevrons. |
| `green` | `#34D399` | Punto del indicador “Sincronizado”. |
| `accentBg` | `#ECFDF5` | Fondo de tarjetas/realces de éxito (Retiro, badges 💬, chips de pago). |
| `accentBorder` | `#A7F3D0` | Borde de los realces verdes. |
| `accentInk` | `#065F46` | Texto verde oscuro (geo, tag de pago, “Nº asignado”). |
| `accentInk2` | `#059669` | Texto verde secundario (coordenadas geo). |
| `diariaBg` / `diariaInk` | `#DBEAFE` / `#2563EB` | Badge **Diaria**; tag de datos. |
| `incobBg` / `incobInk` | `#FEF3C7` / `#B45309` | Badge **Incobrable**; estado de sync “pendiente”. |
| `espBg` / `espInk` | `#EDE9FE` / `#7C3AED` | Badge **Especial**. |
| `redBg` / `redInk` | `#FEE2E2` / `#DC2626` | Mora **alta** (≥90 días o nivel ≥4); botón quitar pago. |
| `chipBg` | `#EEF2F6` | Chips rápidos del comentario; botón secundario (Mapa). |
| `amber` / `amberOff` | `#FBBF24` / `#F59E0B` | Punto del indicador con pendientes / sin conexión. |
| `offGray` | `#475467` | Tarjeta grande de estado “Sin conexión”. |
| `phoneBorder` | `#CBD2D9` | Borde del dispositivo; toggle apagado; chevron `›`. |
| `noteBg` / `noteBorder` | `#F8FAFC` / `#EAECF0` | Nota informativa (pantalla Sync). |
| `demoBg` / `demoBorder` / `demoInk` | `#FFFBEB` / `#FCD34D` / `#92400E` | Banner de demo (solo prototipo; **no construir** en la app real). |
| `hint` | `#D97706` | Texto del hint de validación (“Falta: …”). |

**Sombras** (en Canvas se aproximan con un `DropShadow` o un rectángulo desenfocado; si tu versión no soporta
`DropShadow`, usá borde sutil + el realce de color):

| Token | Valor CSS | Equivalente Canvas |
|-------|-----------|--------------------|
| `shCard` | `0 2px 6px rgba(16,24,40,.05)` | `DropShadow.Light` / borde `gblC.cardBorder`. |
| `shTeal` | `0 6px 18px rgba(15,107,107,.30)` | `DropShadow.Regular` con tinte teal (hero, CTA, botón sync). |
| `shPhone` | `0 12px 44px rgba(16,24,40,.20)` | (marco del dispositivo — no aplica dentro de la app). |

### 2.2 Tipografía — familia, pesos y **escala**

- **Familia:** `gblFont = Font.'Segoe UI'`.
- **Escala de tamaños** (valor **base**, en px; el real = `base × gblScale`, ver §3):

| Token (`gblT.`) | Base (px) | Uso típico |
|-----------------|:---------:|------------|
| `f2xs` | **10** | Labels mínimos (“A cobrar”, `gl` de grid, coordenadas). |
| `fxs` | **11** | Badges, cuentas, tags, subtítulos de fila, pills. |
| `fsm` | **12.5** | Segmentos, contador, títulos de sección, subtítulos de header. |
| `fbase` | **13.5** | Texto de cuerpo, inputs, comentarios, valores `kv`. |
| `fmd` | **15** | Nombres (cliente/cuenta), montos de tarjeta, CTA, botón. |
| `flg` | **17** | Total del header, `dnm` (título apilado), `dbig` chico, stat value. |
| `fxl` | **23** | Título de pantalla principal (`ttl`: “Mi Cartera”, “Hola, Marvin”). |
| `fbig` | **24** | Monto hero/grande (“Cobrado hoy”, “A cobrar (Retiro)”). |

**Pesos por elemento** (extraído del CSS): nombres y montos = **Bold (700)**; valores hero/dbig = **Bold (800→Bold)**;
labels apagados = **Normal**; títulos de sección/segmentos activos = **Semibold/Bold**.

### 2.3 Radios, espaciados y bordes

| Elemento | Radio | Padding | Margen sup. |
|----------|:-----:|---------|:-----------:|
| Tarjeta (`card`, `dcard`, `mini`, `icard`, `brow`, `statc`, `ctxcard`, `switchrow`, `note2`) | **14** (mini/icard/brow/statc/ctxcard/switchrow/note2 = **12**) | `card`: `14 12 14 16` · `dcard`: `14 16` · `mini`: `12 14` | **12** entre tarjetas |
| Header verde (`hdr`) | 0 | `12 20 18` | — |
| Header apilado (`dhdr`) | 0 | `14 18 16` | — |
| Badge / mora / pill / tag / cnt | **11** (cnt/bcount/pendtag/itag/gtoggle/inp/sel/rm/addbtn = **10**) | badge `3 10` · mora `5 12` · cnt `3 9` | — |
| Botón pequeño (`act`, `aa`) | **9** | `act`: `7 13` · `aa`: `7 11` | — |
| Chip | **16** | `7 13` | — |
| Input / búsqueda / total / sync pill | **12** | `search`: `10 12` · `total`: `10 14` · `sync`: `5 11` | — |
| CTA / botón grande / hero / statbig | **14** (statbig/hero = **16**) | `cta`: `15` · `bigbtn`: `14` · `hero`: `16` | — |
| Sheet (accesibilidad) | **22 22 0 0** | `14 16 22` | — |
| Toggle (`switch`) | **14** (pista) / **50%** (knob) | — | — |
| Body (zona scroll) | — | **16** | — |
| Barra inferior (`nav`) | 0 | `9 0 14` | — |

### 2.4 Iconografía

El prototipo usa **emojis** (rápido y multiplataforma) y **3 íconos SVG** en la barra inferior. En Canvas:

| Ícono | Prototipo | Canvas |
|-------|-----------|--------|
| Inicio (casa) | SVG trazo | `Icon.Home` |
| Cartera (líneas) | SVG trazo | `Icon.Items` / `Icon.BulletedList` |
| Stats (barras) | SVG relleno | `Icon.Trending` / `Icon.BarChart` |
| Buscar | SVG lupa | `Icon.Search` |
| Volver | `←` | `Icon.Back` o `Icon.ChevronLeft` |
| Pago 💵 / Datos ✏️ / Geo 📍 / Llamar 📞 / Mapa 🗺️ / Reloj 🕘 | emoji | emoji en `Label` (o íconos equivalentes). |
| Reordenar ▲ ▼ | emoji | `Icon.ChevronUp` / `Icon.ChevronDown`. |

> **Punto de color del header** (estado de sync): es un círculo de **8×8**, `BorderRadius`=50%, color según estado
> (§6 componente `cmpSyncPill`).

### 2.5 Tokens en Power Fx — pegar en `App.OnStart`

```powerapps
// ----- Fuente -----
Set(gblFont, Font.'Segoe UI');

// ----- Colores (paleta exacta del prototipo) -----
Set(gblC, {
    teal:        ColorValue("#0F6B6B"),  tealSub:    ColorValue("#BFE3E3"),
    screen:      ColorValue("#F2F4F7"),  card:       ColorValue("#FFFFFF"),
    cardBorder:  ColorValue("#EAECF0"),  borderSoft: ColorValue("#E4E7EC"),
    hairline:    ColorValue("#F6F7F9"),
    ink:         ColorValue("#101828"),  ink2:       ColorValue("#344054"),
    ink3:        ColorValue("#475467"),  muted:      ColorValue("#667085"),
    muted2:      ColorValue("#98A2B3"),
    green:       ColorValue("#34D399"),  accentBg:   ColorValue("#ECFDF5"),
    accentBorder:ColorValue("#A7F3D0"),  accentInk:  ColorValue("#065F46"),
    accentInk2:  ColorValue("#059669"),
    diariaBg:    ColorValue("#DBEAFE"),  diariaInk:  ColorValue("#2563EB"),
    incobBg:     ColorValue("#FEF3C7"),  incobInk:   ColorValue("#B45309"),
    espBg:       ColorValue("#EDE9FE"),  espInk:     ColorValue("#7C3AED"),
    redBg:       ColorValue("#FEE2E2"),  redInk:     ColorValue("#DC2626"),
    chipBg:      ColorValue("#EEF2F6"),
    amber:       ColorValue("#FBBF24"),  amberOff:   ColorValue("#F59E0B"),
    offGray:     ColorValue("#475467"),  phoneBorder:ColorValue("#CBD2D9"),
    noteBg:      ColorValue("#F8FAFC"),  hint:       ColorValue("#D97706")
});

// ----- Escala de accesibilidad (4 niveles) -----
// Se lee de la preferencia del agente (offline-safe). 1 = Normal.
Set(gblScale, Coalesce(LookUp(sanic_cm_tbl_agente, sanic_cm_ag_usuarioid.'Primary Email' = User().Email).⚠️TODO(sanic_escalavista), 1));
Set(gblT, {
    f2xs: 10*gblScale,  fxs: 11*gblScale,  fsm: 12.5*gblScale, fbase: 13.5*gblScale,
    fmd: 15*gblScale,   flg: 17*gblScale,  fxl: 23*gblScale,   fbig: 24*gblScale
});

// ----- Agente actual (el localizador) -----
Set(gblAgente, LookUp(sanic_cm_tbl_agente, sanic_cm_ag_usuarioid.'Primary Email' = User().Email));

// ----- Estado de navegación inicial -----
Set(varModo, "cliente");      // agrupación de la Cartera: "cliente" | "cuenta"
Set(varFiltro, "Todos");      // "Todos" | "Diaria" | "Incobrable" | "Especial"
Set(varBuscar, "");
Set(varInicioModo, "cliente");
```

---

## 3. Accesibilidad — la escala que multiplica todo (`gblScale`)

> Cross-cutting: **todas** las pantallas. Materializa el requisito de **baja visión**: tamaño ajustable **sin
> romper el layout**.

| Nivel | `gblScale` | Clase en prototipo |
|-------|:---:|:---:|
| Normal | **1.00** | `.phone` |
| Grande | **1.20** | `.phone.s2` |
| Muy grande | **1.45** | `.phone.s3` |
| Máximo | **1.70** | `.phone.s4` |

**Reglas para que NO se rompa** (idénticas al prototipo):
1. **Todo tamaño de fuente** sale de `gblT.*` (= `base × gblScale`). Nunca un número suelto.
2. **Contenedores con auto-alto y wrap:** las filas que pueden desbordar (`nombre + badge`, `a cobrar + mora`,
   los 3 montos del grid, los segmentos de filtro) usan **contenedor horizontal con `Wrap = true`** → a mayor
   escala **se apilan**, no se salen.
3. **Targets táctiles** ≥ 44 (también escalan): flechas ▲▼, botones Llamar/Mapa.
4. **Nada de truncar** montos, mora ni nombres (`Overflow` visible, alto automático).

**Construcción:** botón **“Aa”** en el header (`cmpAa`) abre un **sheet inferior** (`cmpSheetEscala`, §6) con los
4 niveles. Al elegir: `Set(gblScale, <valor>); Patch(sanic_cm_tbl_agente, gblAgente, {⚠️TODO(sanic_escalavista):<valor>}); …`
recalcular `gblT`. Persiste en `sanic_cm_tbl_agente` (offline-safe) y aplica **al instante** en toda la app.

```powerapps
// cmpAa  →  OnSelect del nivel "Muy grande" (1.45), por ejemplo:
Set(gblScale, 1.45);
Set(gblT, { f2xs:10*gblScale, fxs:11*gblScale, fsm:12.5*gblScale, fbase:13.5*gblScale,
            fmd:15*gblScale, flg:17*gblScale, fxl:23*gblScale, fbig:24*gblScale });
Patch(sanic_cm_tbl_agente, gblAgente, { ⚠️TODO(sanic_escalavista): gblScale });
Set(varSheetEscala, false)
```

---

## 4. Estrategia offline (perfil offline)

> El localizador **lee** su cartera y **crea** interacciones/pagos sin señal; sincroniza al volver. Como la
> captura es **append-only**, hay **muy pocos conflictos** — gran ventaja del diseño.

### Perfil offline (qué baja al dispositivo)
| Tabla | Modo | Filtro (FetchXML del perfil) |
|-------|------|------------------------------|
| `sanic_cm_tbl_asignacion` | **Lectura + edición del `orden`** | owner = usuario actual **y** `estado = Activa` (Especial: + `estadoaprobacion = Aprobada`) |
| `sanic_cm_tbl_cuenta` | Lectura | las cuentas de sus asignaciones |
| `sanic_cm_tbl_cliente` · `sanic_cm_tbl_direccion` · `sanic_cm_tbl_telefono` | Lectura | de esas cuentas |
| `sanic_cm_tbl_producto` · `sanic_cm_tbl_fiador` | Lectura | catálogos referenciados |
| `sanic_cm_tbl_interaccion` | **Escritura** (+ lectura de las propias) | las que él crea; lee recientes para historial |
| `sanic_cm_tbl_pago` | Escritura | idem |
| `sanic_cm_tbl_actualizaciondatos` | Escritura | idem |

### Sincronización
- **Automática** (delta sync periódico) al haber conexión. El **manual real** (“Check for updates”) y el conteo
  de pendientes los da la **plataforma** (ícono de globo / página *Device Status*). Nuestra pantalla de Sync
  (§8.8) es un **complemento**; lo único legible en Power Fx es la señal **`Connection`** (`.Connected`/`.Metered`).
- **Números server-assigned:** `INT-`/`PAG-`/`ACT-` se asignan **al sincronizar** — offline un registro tiene
  `autonumber` en blanco → ese es el **proxy de “pendiente”**.
- **Conflictos:** mínimos. La cartera es solo-lectura (la gestionan import/Admin); lo único que él edita es el
  **`orden`**. Interacciones/pagos = **append-only**.

### Seguridad (DEC-015)
Ve **solo su cartera** porque es **owner** de sus asignaciones (User-level). El perfil offline respeta ese filtro.

---

## 5. Variables globales (resumen)

| Variable | Tipo | Significado | Se setea en |
|----------|------|-------------|-------------|
| `gblFont` | Font | Familia tipográfica | `App.OnStart` |
| `gblC` | Record | Paleta de colores | `App.OnStart` |
| `gblScale` / `gblT` | Number / Record | Escala de accesibilidad y tamaños | `App.OnStart` + `cmpAa` |
| `gblAgente` | Record | El localizador (de `sanic_cm_tbl_agente`) | `App.OnStart` |
| `varModo` | Text | Cartera: `"cliente"` / `"cuenta"` | OnStart / toggle |
| `varFiltro` | Text | `"Todos"/"Diaria"/"Incobrable"/"Especial"` | Segmento |
| `varBuscar` | Text | Texto de búsqueda | Caja de búsqueda |
| `varClienteSel` | Record | Cliente abierto | al tocar cliente |
| `varCuentaSel` | Record | Cuenta abierta | al tocar cuenta |
| `varCliTab` | Text | Tab en Cliente: `"cuentas"/"inter"` | tabs |
| `varDetTab` | Text | Tab en Detalle: `"detalle"/"inter"` | tabs |
| `varInicioModo` | Text | Desglose Inicio: `"cliente"/"cuenta"` | toggle |
| `varSyncBack` | Text | Pantalla a la que vuelve Sync | al abrir Sync |
| `varSheetEscala` | Bool | Sheet de accesibilidad visible | botón Aa |

---

## 6. Componentes reutilizables (con medidas exactas)

Construí estos como **componentes** (Components) o como **grupos plantilla**. Se repiten en varias pantallas.

### 6.A — `cmpHeaderVerde` (header principal — Inicio / Cartera)
- **Contenedor:** vertical, `Fill = gblC.teal`, `Padding = 12 20 18`, `Width = Parent.Width`.
- **Fila status bar** (horizontal, `Justify = SpaceBetween`): `"9:41"` izq. + íconos `●●● 📶 🔋` der.
  `Color = White`, `Size = gblT.fbase`, `FontWeight = Bold`.
- **Fila título** (horizontal, `Wrap = true`, `Gap = 8`, `Align = Start`):
  - Izq. (vertical): `lblTitulo` (`Size = gblT.fxl`, `Bold`, `White`) + `lblSub` (`Size = gblT.fsm`,
    `Color = gblC.tealSub`).
  - Der. (horizontal, `Gap = 8`, `Wrap`, `Justify = End`): `cmpAa` + `cmpSyncPill`.
- **Tarjeta total** (solo Cartera): `Fill = RGBA(255,255,255,0.12)`, `Radius = 12`, `Padding = 10 14`,
  `margin-top 14`. Dentro: `lblTotLbl` (“A cobrar hoy”, `gblT.fxs`, `tealSub`) + `lblTotAmt`
  (`gblT.flg`, `Bold`, `White`).

### 6.B — `cmpHeaderBack` (header apilado — Cliente/Detalle/Interacción/Pagos/Sync)
- **Contenedor:** horizontal, `Fill = gblC.teal`, `Padding = 14 18 16`, `Gap = 12`, `Align = Center`, `Wrap = true`.
- `btnBack`: cuadrado `34×34` (× gblScale), `Fill = RGBA(255,255,255,0.16)`, `Radius = 10`, ícono `←`
  (`Size = 18×gblScale`, `White`). `OnSelect` = navegación de retorno (varía por pantalla).
- Bloque central (vertical, `FillPortions = 1`): `lblDnm` (`gblT.flg`, `Bold`, `White`) +
  `lblDac` (`gblT.fsm`, `tealSub`).
- Opcional a la derecha: **badge de tipo** (`cmpBadge`) y/o `cmpAa`.

### 6.C — `cmpBadge` (badge de tipo de asignación)
- `Radius = 11`, `Padding = 3 10`, `Size = gblT.fxs`, `FontWeight = Bold`, `Overflow` off (no wrap).
- Color por tipo (`Switch(tipo, …)`):
  - **Diaria** → `Fill = gblC.diariaBg` (`#DBEAFE`), `Color = gblC.diariaInk` (`#2563EB`)
  - **Incobrable** → `Fill = gblC.incobBg` (`#FEF3C7`), `Color = gblC.incobInk` (`#B45309`)
  - **Especial** → `Fill = gblC.espBg` (`#EDE9FE`), `Color = gblC.espInk` (`#7C3AED`)

### 6.D — `cmpPillMora` (pill de mora)
- `Radius = 11`, `Padding = 5 12`, `Size = gblT.fxs`, `Overflow` off.
- **Normal:** `Fill = gblC.screen` (`#F2F4F7`), `Color = gblC.ink3` (`#475467`).
- **Alta** (regla `dias > 90 || nivel >= 4`): `Fill = gblC.redBg` (`#FEE2E2`), `Color = gblC.redInk` (`#DC2626`),
  `FontWeight = Bold`.

### 6.E — `cmpTarjeta` (tarjeta blanca base)
- `Fill = gblC.card`, `BorderColor = gblC.cardBorder`, `BorderThickness = 1`, `Radius = 14`,
  `Padding = 14 12 14 16`, `margin-top 12`, sombra `shCard`.

### 6.F — `cmpBotonPrimario` / `cmpBotonSecundario` / `cmpCTA`
- **Primario (`act`):** `Fill = gblC.teal`, `Color = White`, `Radius = 9`, `Padding = 7 13`, `Size = gblT.fsm`,
  `FontWeight = Semibold`.
- **Secundario (`act.alt`):** `Fill = gblC.chipBg` (`#EEF2F6`), `Color = gblC.teal`.
- **CTA sticky (`cta`):** ancho casi completo (`Parent.Width - 28`), `Padding = 15`, `Radius = 14`,
  `Fill = gblC.teal`, `Color = White`, `Size = gblT.fmd`, `Bold`, sombra `shTeal`, fijado abajo.
  Estado **deshabilitado**: `DisplayMode.Disabled`, opacidad 0.45.

### 6.G — `cmpSyncPill` (indicador de sync del header) — **clickeable**
- `Fill = RGBA(255,255,255,0.16)`, `Radius = 12`, `Padding = 5 11`, `Size = gblT.fxs`, horizontal `Gap = 6`.
- **Punto** `8×8`, `Radius = 50%`, color:
  - sin conexión (`!Connection.Connected`) → `gblC.amberOff` (`#F59E0B`)
  - con pendientes → `gblC.amber` (`#FBBF24`)
  - todo al día → `gblC.green` (`#34D399`)
- **Texto:** `"Sin conexión"` / `"N pend."` / `"Sincronizado"`.
- `OnSelect = Set(varSyncBack, varTab); Navigate(scrSincronizacion)`.

### 6.H — `cmpNav` (barra inferior)
- Horizontal, `Fill = White`, borde superior `1` `gblC.cardBorder`, `Padding = 9 0 14`.
- 3 ítems iguales (`FillPortions = 1`, centrados, vertical): ícono (`22×gblScale`) + label (`gblT.f2xs`).
- Activo: `Color = gblC.teal`, `Bold`. Inactivo: `Color = gblC.muted2`.
- Ítems: **Inicio** (`Icon.Home`) · **Cartera** (`Icon.Items`) · **Stats** (`Icon.Trending`).
- `OnSelect` de cada uno: `Set(varTab,"inicio"|"cartera"|"stats"); Navigate(scrInicio|scrCartera|scrStats)`.

### 6.I — `cmpTabBar` (pestañas — Cliente/Detalle)
- Horizontal, `Fill = White`, borde inferior `1` `gblC.cardBorder`.
- Cada tab (`FillPortions = 1`, centrado, `Padding = 13 4`, `Size = gblT.fsm`, `Semibold`):
  - activo → `Color = gblC.teal` + **borde inferior 2.5 `gblC.teal`** (rectángulo de 2.5 abajo).
  - inactivo → `Color = gblC.muted`.

### 6.J — `cmpToggle` (switch del formulario)
- Pista `48×28`, `Radius = 14`. `Fill` = `gblC.teal` (on) / `gblC.phoneBorder` (off, `#CBD2D9`).
- Knob `22×22`, `Radius = 50%`, `White`, `top 3`, `left 3` (off) / `left 23` (on). Animación opcional.

### 6.K — `cmpSheetEscala` (hoja inferior de accesibilidad)
- Overlay: `Fill = RGBA(16,24,40,0.45)`, cubre toda la pantalla, `Visible = varSheetEscala`.
- Tarjeta inferior: `Fill = White`, `Radius = 22 22 0 0`, `Padding = 14 16 22`.
  - Título `"TAMAÑO DE VISTA"` (`gblT.fsm`, `Bold`, `muted2`, mayúsculas).
  - 4 filas (`Padding = 14`, `Radius = 12`, `Gap = 16`): muestra `Aa` (15/19/24/29 px) + nombre.
    La seleccionada: `Fill = gblC.accentBg`, `Color = gblC.teal`, `Bold`.

---

## 7. Navegación

```
        ┌──────────── Barra inferior (cmpNav, solo estas 3) ────────────┐
        │   [ Inicio ]        [ Mi Cartera ]        [ Stats ]           │
        └───────────────────────────────────────────────────────────────┘

 Inicio ─(hero "Cobrado hoy")─► Pagos del día
 Inicio ─(fila desglose)──────► Cliente/Detalle en pestaña Interacciones
 Cartera ─(tarjeta)───────────► Cliente (modo cliente) / Detalle (modo cuenta)
 Cliente ─(mini-cuenta)───────► Detalle
 Detalle ─(CTA)───────────────► Registrar Interacción
 Header (cmpSyncPill) ────────► Sincronización
```

| Acción | Power Fx |
|--------|----------|
| Abrir cliente | `Set(varClienteSel, ThisItem); Set(varCliTab,"cuentas"); Navigate(scrCliente, Cover)` |
| Abrir detalle | `Set(varCuentaSel, ThisItem); Set(varDetTab,"detalle"); Navigate(scrDetalleCuenta, Cover)` |
| Volver | `Navigate(<pantalla origen>, UnCover)` |
| Registrar interacción | `Navigate(scrInteraccion, Cover)` |

---

## 8. Pantalla por pantalla (build control por control)

> En cada tabla: **Control** = nombre sugerido · **Estilo** = props exactas (todo lo no dicho hereda del
> componente base) · **Texto/Items/Eventos** = contenido y lógica. Recordá: las pantallas apiladas **ocultan**
> `cmpNav`.

---

### 8.1 — `scrCartera` · Mi Cartera (el hub)

**Objetivo.** Lista de asignaciones activas; **agrupar** por cliente o por cuenta, **filtrar** por tipo,
**buscar**, **reordenar** (▲▼) y entrar al detalle. Es la pantalla de inicio de la barra (`Navigate` default).

**Estructura visual:**
```
[ HEADER VERDE: "Mi Cartera" / "N clientes · N cuentas"   (Aa)(syncPill) ]
[ Total: "A cobrar hoy"  C$ X · US$ Y                                    ]
─────────────────────────────────────────────────────────────────────────
[ Segmento: Todos | Diaria | Incobrable | Especial ]
[ 🔍 Buscar cliente o cuenta…                       ]
[ "N clientes"                       👤 Cliente | ▤ Cuenta ]
[ ┌ Tarjeta ───────────────────────────────────────┐ ▲ ]
  │ Nombre                    N cuentas  💬 3        │ ▼ │
  │ [Diaria][Especial]                              │   │
  │ ────────────────────────────────                │   │
  │ Total a cobrar  C$ X·US$ Y          máx 27 días │   │
  └─────────────────────────────────────────────────┘
[ … más tarjetas … ]
─────────────────────────────────────────────────────────────────────────
[ NAV: Inicio · Cartera(activo) · Stats ]
```

**Controles:**

| Control | Tipo / Estilo | Texto · Items · Eventos |
|---------|---------------|--------------------------|
| `conHeader` | `cmpHeaderVerde` con tarjeta total | `lblTitulo="Mi Cartera"`, `lblSub = CountRows(galCli)&" clientes · "&Sum(galCli, CountRows(...)) &" cuentas"` |
| `lblTotAmt` | `gblT.flg`, Bold, White | `cmpMoneda(Sum(...retironio), Sum(...retirousd))` — ver helper bimoneda §8.10 |
| `galSeg` | Galería **horizontal** o 4 botones, `Fill=gblC.borderSoft`, `Radius=18`, `Padding=3` | items `["Todos","Diaria","Incobrable","Especial"]`. Botón activo (`varFiltro=item`): `Fill=White`, `Color=gblC.teal`, `Bold`, `Radius=15`. Inactivo: `Color=gblC.ink3`, `Size=gblT.fsm`. `OnSelect=Set(varFiltro, ThisItem.Value)` |
| `conSearch` | `cmpTarjeta` mini, horizontal, `Radius=12`, `Padding=10 12`, `Gap=9`, `margin-top 14` | `Icon.Search` (`gblC.muted2`) + `txtBuscar` (Text input, `Size=gblT.fbase`, sin borde). `OnChange=Set(varBuscar, Self.Value)` |
| `conResultBar` | horizontal, `Justify=SpaceBetween`, `Wrap`, `margin-top 14` | `lblCount` (`gblT.fsm`, `muted`, Semibold) + `cmpAgrupToggle` |
| `cmpAgrupToggle` | pill `Fill=gblC.borderSoft`, `Radius=10`, `Padding=2` | dos opciones `👤 Cliente` / `▤ Cuenta`, activa `Fill=White`,`Color=gblC.teal`,`Bold`. `OnSelect=Set(varModo,"cliente"|"cuenta")` |
| `galCartera` | **Galería vertical** flexible (template auto-alto) | `Items` y plantilla → abajo |

**`lblCount.Text`:**
```powerapps
With({ n: CountRows(galCartera.AllItems) },
    n & If(varModo="cliente", If(n=1," cliente"," clientes"), If(n=1," cuenta"," cuentas")))
```

**`galCartera.Items`** (cambia según modo; el orden lo da `sanic_cm_as_orden`):
```powerapps
If(varModo = "cliente",
    // --- AGRUPADO POR CLIENTE ---
    SortByColumns(
        AddColumns(
            Filter( GroupBy(misAsignaciones, "⚠️TODO(sanic_clienteid)", "Cuentas"),
                    (varFiltro="Todos" || CountRows(Filter(Cuentas, sanic_cm_as_tipo=varFiltro))>0)
                    && (varBuscar="" || Lower(sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre) in Lower(varBuscar)
                        || CountRows(Filter(Cuentas, varBuscar in sanic_cm_as_cuentaid.sanic_cm_ct_numerodecuenta))>0) ),
            "Orden", First(Cuentas).sanic_cm_as_orden ),
        "Orden", SortOrder.Ascending),
    // --- POR CUENTA ---
    SortByColumns(
        Filter(misAsignaciones,
            (varFiltro="Todos" || sanic_cm_as_tipo=varFiltro)
            && (varBuscar="" || Lower(sanic_cm_as_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre) in Lower(varBuscar)
                || varBuscar in sanic_cm_as_cuentaid.sanic_cm_ct_numerodecuenta)),
        "sanic_cm_as_orden", SortOrder.Ascending))
```
> `misAsignaciones = Filter(sanic_cm_tbl_asignacion, sanic_cm_as_estado='Estado (sanic_cm_tbl_asignacion)'.Activa)` (el perfil offline
> ya bajó solo las del owner — DEC-015). En modo cliente agrupamos; el badge muestra **los tipos presentes**.

**Plantilla de fila (`galCartera`)** — es una `cmpTarjeta` horizontal: a la izquierda el contenido, a la derecha
las flechas.

| Sub-control | Estilo | Contenido |
|-------------|--------|-----------|
| `conMain` | vertical, `FillPortions=1` | — |
| fila `r1` | horizontal, `Justify=SpaceBetween`, `Wrap`, `Gap=8` | `lblNombre` (`gblT.fmd`, Bold, `gblC.ink`) + grupo derecho |
| `lblCnt` *(modo cliente)* | `gblT.fxs`, `muted`, `Fill=gblC.screen`, `Radius=10`, `Padding=3 9` | `CountRows(Cuentas)&" cuenta(s)"` |
| `cmpBadge` *(modo cuenta)* | §6.C | `sanic_cm_as_tipo` |
| `badge💬` (`ibadge`) | `gblT.fxs`, `Color=gblC.teal`, `Fill=gblC.accentBg`, `Border 1 gblC.accentBorder`, `Radius=10`, `Padding=3 9` | `"💬 "&<conteo interacciones>`. Solo si conteo>0. `OnSelect` (stop propagation) → abre cliente/detalle en pestaña **Interacciones** |
| `conTipos` *(modo cliente)* | horizontal, `Wrap`, `Gap=6`, `margin-top 8` | un `cmpBadge` por **tipo distinto** del cliente |
| `lblAcct` *(modo cuenta)* | `gblT.fxs`, `muted`, `margin-top 4` | `producto & " · " & cuenta` |
| `rectDivider` | rectángulo alto 1, `Fill=gblC.screen`, `margin 10 0` | — |
| fila `r2` | horizontal, `Justify=SpaceBetween`, `Wrap`, `Gap=8 10` | bloque “a cobrar” + `cmpPillMora` |
| `lblCobrarL` | `gblT.f2xs`, `muted2` | `"Total a cobrar"` (cliente) / `"A cobrar"` (cuenta) |
| `lblCobrarA` | `gblT.fmd`, Bold, `gblC.teal` | `cmpMoneda(retiroNio, retiroUsd)` |
| `cmpPillMora` | §6.D | cliente: `"máx "&Max(Cuentas, ...dias)&" días"` · cuenta: `dias&" días · Nivel "&nivel` |
| `conArrows` | vertical, `Gap=14`, centrado | `iconUp` ▲ + `iconDown` ▼ |

**Reordenar (▲▼)** — cada flecha es un `Icon`/`Label` `Size=15×gblScale`, `Fill=gblC.screen`, `Radius=6`,
`Padding=2 6`, `Color=gblC.muted2`:
```powerapps
// iconUp.OnSelect — intercambia el orden con la fila inmediatamente superior
With({ arriba: First(Sort(Filter(galCartera.AllItems, sanic_cm_as_orden < ThisItem.sanic_cm_as_orden),
                          sanic_cm_as_orden, SortOrder.Descending)) },
    If(!IsBlank(arriba),
        With({ act: ThisItem.sanic_cm_as_orden },
            Patch(sanic_cm_tbl_asignacion, ThisItem, { sanic_cm_as_orden: arriba.sanic_cm_as_orden });
            Patch(sanic_cm_tbl_asignacion, arriba,   { sanic_cm_as_orden: act }))))
// iconDown.OnSelect — espejo (busca el sanic_cm_as_orden inmediatamente MAYOR)
```
> Si `sanic_cm_as_orden` viene vacío del import, inicializar 1..N en `OnVisible` la primera vez. El `Patch` es **local**
> (offline) y sincroniza al volver señal — es lo **único** que el localizador escribe sobre `asignacion`.

**`OnSelect` de la tarjeta** (zona principal, no las flechas):
```powerapps
If(varModo="cliente",
   Set(varClienteSel, ThisItem.sanic_cm_as_cuentaid.sanic_cm_ct_clienteid); Set(varCliTab,"cuentas"); Navigate(scrCliente, Cover),
   Set(varCuentaSel, ThisItem.sanic_cm_as_cuentaid);  Set(varDetTab,"detalle"); Navigate(scrDetalleCuenta, Cover))
```

**Estados:** lista vacía → `lblEmpty` centrado (`gblT.fbase`, `muted2`, `Padding 40 20`): *“No hay clientes/cuentas
con ese filtro.”** (texto según modo).

---

### 8.2 — `scrCliente` · Cuentas del Cliente

**Objetivo.** Todas las cuentas a cobrar de un cliente + su **contacto compartido**, en **una visita**. Dos
pestañas **[Cuentas | Interacciones]**.

**Controles:**

| Control | Estilo | Contenido / Eventos |
|---------|--------|---------------------|
| `conHeader` | `cmpHeaderBack` + `cmpAa` | `lblDnm = varClienteSel.sanic_cm_cl_nombre`, `lblDac = depto & " · " & trabajo`. `btnBack.OnSelect = Navigate(<cliBack>, UnCover)` (Cartera o Inicio) |
| `cmpTabBar` | §6.I | `Cuentas (N)` / `Interacciones (M)`. `OnSelect=Set(varCliTab,"cuentas"|"inter")` |
| `conBody` | vertical, `Padding 16`, scroll | muestra `tabCuentas` o `tabInter` según `varCliTab` |

**Tab Cuentas (`varCliTab="cuentas"`):**
1. **`cardTotal`** = `cmpTarjeta` con realce verde (`Fill=gblC.accentBg`, `Border=gblC.accentBorder`):
   `lblDlbl` (“Total a cobrar del cliente”, `gblT.fxs`, `muted2`) + `lblDbig` (`gblT.fbig`, `Bold(800)`,
   `gblC.teal`) = `cmpMoneda(Sum(cuentas, retiroNio), Sum(cuentas, retiroUsd))`.
2. **`cardContacto`** = `cmpTarjeta`: título `"Contacto"` (`cmpSectTitle`) + galería de teléfonos (`cmpFilaContacto`),
   luego `"Direcciones"` + galería de direcciones.
3. **`lblCuentasN`** (`cmpSectTitle`, `margin 16 2 0`): `"Cuentas a cobrar ("&CountRows(cuentas)&")"`.
4. **`galMiniCuentas`**: una `cmpMini` por cuenta → `OnSelect=Set(varCuentaSel, ThisItem.sanic_cm_as_cuentaid);
   Set(varDetTab,"detalle"); Navigate(scrDetalleCuenta, Cover)`.

**`cmpSectTitle`** (título de sección): `gblT.fxs`, `Bold`, `gblC.muted2`, **mayúsculas**, `letter-spacing` leve,
`margin-bottom 6`.

**`cmpFilaContacto`** (`.crow`): horizontal, `Justify=SpaceBetween`, `Padding 8 0`, borde inferior 1
`gblC.hairline`, `Wrap`. Izq.: `lblTipo` (`gblT.f2xs`, `muted2`) + `lblValor` (`gblT.fbase`, `ink`).
Der.: botón. **Teléfono** → `cmpBotonPrimario` “Llamar” (`OnSelect=Launch("tel:"&numero)`). **Dirección** →
`cmpBotonSecundario` “Mapa” (`OnSelect=Launch("https://www.google.com/maps/search/?api=1&query="&EncodeUrl(direccion))`).

**`cmpMini`** (mini-tarjeta de cuenta): `Fill=White`, `Border 1 gblC.cardBorder`, `Radius=12`, `Padding 12 14`,
`margin-top 10`, sombra `shCard`, horizontal `Justify=SpaceBetween`, `Wrap`. Izq.: `lblMp`
(`producto`, `gblT.fbase`, Bold) + `cmpBadge` al lado + `lblMc` (`cuenta`, `gblT.fxs`, `muted`). Der.
(alineado derecha): `lblMr` (`cmpMoneda(retiro…)`, `gblT.fbase`, Bold, `gblC.teal`) + `lblMmora`
(`dias&" días"`, `gblT.fxs`; rojo `gblC.redInk` Bold si mora alta, si no `gblC.muted2`).

**Tab Interacciones (`varCliTab="inter"`):** ver **§8.7** (componente `cmpListaInteracciones`, scope **cliente**).
Vacío → `lblEmpty` “Sin interacciones registradas.”

**`OnVisible`:** `Set(misCtas, Filter(sanic_cm_tbl_asignacion, sanic_cm_as_cuentaid.sanic_cm_ct_clienteid = varClienteSel,
sanic_cm_as_estado='Estado (sanic_cm_tbl_asignacion)'.Activa))`.

---

### 8.3 — `scrDetalleCuenta` · Detalle de Cuenta

**Objetivo.** Toda la info de la cuenta + cliente, acciones rápidas, y **lanzar la captura**. Dos pestañas
**[Detalle | Interacciones]**. CTA sticky abajo.

| Control | Estilo | Contenido / Eventos |
|---------|--------|---------------------|
| `conHeader` | `cmpHeaderBack` + `cmpBadge(tipo)` + `cmpAa` | `lblDnm = cliente.nombre`, `lblDac = producto & " · " & cuenta`. `btnBack` → según `detSrc` (cliente/inicio/cartera) |
| `cmpTabBar` | §6.I | `Detalle` / `Interacciones (M)` |
| `conBody` | vertical, `Padding 16`, scroll | tab activo |
| `btnCTA` | `cmpCTA` **sticky** abajo | `"＋ Registrar interacción"`. `OnSelect=Navigate(scrInteraccion, Cover)` |

**Tab Detalle:**
1. **`cardRetiro`** = `cmpTarjeta` realce verde:
   - `lblDlbl` “A cobrar (Retiro)” + `lblDbig` (`gblT.fbig`, Bold, teal) = `cmpMoneda(retiroNio, retiroUsd)`.
   - **`conGrid3`** (horizontal, `Wrap`, `Gap=8`, `margin-top 12`): 3 celdas `g` (`Border 1 gblC.cardBorder`,
     `Radius=10`, `Padding=8`, `FillPortions` iguales, min-width 96, centradas):
     - **Saldo vencido** = `cmpMoneda(svNio, svUsd)`
     - **Pago mínimo** = `cmpMoneda(pmNio, pmUsd)`
     - **Total (dolariz.)** = `"US$ "&Text(totalUsd,"[$-es]#,##0.00")`
     - cada celda: `lblGl` (`gblT.f2xs`, `muted2`) + `lblGv` (`gblT.fsm`, Bold, `gblC.ink2`).
   - **`lblDnote`** (`gblT.fxs`, `muted`, `margin-top 10`): `"En riesgo (contagiado): US$ "&Text(contUsd,…)`.
2. **`cardMora`** = `cmpTarjeta`: título “Mora” + filas `cmpKV`:
   - Días en mora = `dias&" días · Nivel "&nivel` (valor en rojo `gblC.redInk` si mora alta) · Fecha de pago ·
     Último pago · Ciclo.
3. **`cardContacto`** = igual que §8.2 (teléfonos + direcciones).
4. **`cardProducto`** = `cmpTarjeta` con filas `cmpKV`: Producto · Fiador · Nota.

**`cmpKV`** (fila clave-valor `.kv`): horizontal, `Justify=SpaceBetween`, `Padding 6 0`, `Gap=10`,
`Size=gblT.fbase`. `k`: `gblC.muted`. `v`: `gblC.ink`, `Semibold`, alineado derecha.

**Tab Interacciones:** §8.7 (scope **cuenta**).

**`OnVisible`:** no carga nada (usa `varCuentaSel`). Teléfonos/direcciones:
`Filter(sanic_cm_tbl_telefono, sanic_cm_tl_clienteid = varCuentaSel.sanic_cm_ct_clienteid)` (idem direcciones con `sanic_cm_tbl_direccion` / `sanic_cm_dr_clienteid`).

---

### 8.4 — `scrInteraccion` · Registrar Interacción (el núcleo de captura)

**Objetivo.** Capturar **todo en una pantalla**: comentario + (opcional) pago(s) + (opcional) datos. **Un solo
Guardar** lo crea todo, **offline**. El botón se **habilita solo si la captura es válida**.

| Control | Estilo | Contenido / Eventos |
|---------|--------|---------------------|
| `conHeader` | `cmpHeaderBack` + `cmpAa` | `lblDnm="Registrar interacción"`, `lblDac = cliente.nombre`. `btnBack=Navigate(scrDetalleCuenta, UnCover)` |
| `cardCtx` | `.ctxcard`: `cmpTarjeta` (Radius 12, Padding 12 14), horizontal `Justify=SpaceBetween`, `Wrap` | Izq.: `lblCl` (producto, `gblT.fbase`, Bold) + `lblCc2` (cuenta, `gblT.fxs`, muted). Der.: `lblCrl` (“A cobrar”, `gblT.f2xs`, muted2) + `lblCrv` (`cmpMoneda(retiro…)`, `gblT.fmd`, Bold, teal) |
| `lblComLabel` | `gblT.fsm`, Bold, `gblC.ink2`, `margin-top 16` | `"¿Qué pasó? *"` |
| `txtComentario` | Text input multilínea, `Border 1 gblC.borderSoft`, `Radius=12`, `Padding=12`, `Size=gblT.fbase`, alto mín. 96, focus borde `gblC.teal` | placeholder “Escribí el comentario de la interacción…”. `OnChange` recalcula validación |
| `galChips` | horizontal, `Wrap`, `Gap=8`, `margin-top 10` | 6 chips `cmpChip`. `OnSelect` agrega la frase al comentario |
| `rowPago` | `cmpSwitchRow` | label “💵 Aplica pago” + sub “El cliente pagó algo en esta visita” + `cmpToggle tgPago` |
| `secPago` | sección colapsable (borde izq. 3 `gblC.teal`, `Padding 2 0 2 12`), `Visible=tgPago` | título “Pago(s)” + `galPagos` (líneas) + `btnAddPago` |
| `rowDatos` | `cmpSwitchRow` | “✏️ Actualizar datos del cliente” + sub + `cmpToggle tgDatos` |
| `secDatos` | sección colapsable, `Visible=tgDatos` | título “Nuevos datos · dejá en blanco lo que no cambió” + 7 campos `cmpDField` |
| `cardGeo` | horizontal, `Fill=gblC.accentBg`, `Border 1 gblC.accentBorder`, `Radius=12`, `Padding 12 14`, `Gap=10`, `margin-top 14` | 📍 (`20×gblScale`) + “Ubicación capturada” (`gblT.fbase`, `gblC.accentInk`, Semibold) + coords (`gblT.fxs`, `gblC.accentInk2`) |
| `lblMeta` | `gblT.fxs`, `muted2`, centrado, `margin-top 14` | `"Se registrará: "&Text(Now(),"dd/mm/yyyy hh:mm")&" · "&cliente.nombre` |
| `btnGuardar` | `cmpCTA` (no sticky aquí; al final del scroll) | texto dinámico; `DisplayMode` por validación |
| `lblHint` | `gblT.fxs`, `gblC.hint`, centrado, `margin-top 8` | “Falta: …” |

**`cmpChip`:** `Fill=gblC.chipBg`, `Color=gblC.teal`, `Radius=16`, `Padding 7 13`, `Size=gblT.fxs`, Semibold.
Chips: `["Promesa de pago","No localizado","Cliente pagó","No tiene dinero hoy","Negocio cerrado","Pidió arreglo"]`.
`OnSelect`: `Set(varCom, If(IsBlank(varCom), ThisItem.Value, Trim(varCom)&". "&ThisItem.Value))` y reflejar en
`txtComentario.Default`.

**`cmpSwitchRow`:** `Fill=White`, `Border 1 gblC.cardBorder`, `Radius=12`, `Padding 14 16`, `margin-top 12`,
horizontal `Justify=SpaceBetween`, `Wrap`, `Gap=12`. Izq.: `sl` (`gblT.fbase`, Semibold, `ink`) + `sd`
(`gblT.fxs`, `muted2`).

**Líneas de pago (`galPagos`, colección local `colPagos`):** cada línea = `cmpTarjeta` (Radius 12, Padding 10):
- fila 1: `txtMonto` (number, `Border 1 gblC.borderSoft`, `Radius=10`, `Padding=10`, `FillPortions`) +
  `drpMoneda` (`["NIO","USD"]`, `Radius=10`).
- fila 2: `txtRecibo` (“N° de recibo”) + `btnQuitar` (`Fill=gblC.redBg`, `Color=gblC.redInk`, `Radius=10`,
  ancho 42, “✕”). `OnSelect=Remove(colPagos, ThisItem)`.
- **`btnAddPago`** (`addbtn`): `Fill=gblC.chipBg`, **borde dasheado** `1` `#A7C4C4`, `Radius=10`, `Padding=11`,
  ancho completo, `Color=gblC.teal`, Semibold. `OnSelect=Collect(colPagos, {monto:0, moneda:"NIO", recibo:""})`.

**Campos de datos (`cmpDField`, 7):** label (`gblT.fxs`, `muted`) + input (`cmpInput`). Orden y etiquetas:
`Teléfono casa · Teléfono celular · Teléfono trabajo · Dirección casa · Dirección estado de cuenta ·
Dirección trabajo · Correo electrónico`.

**Validación — `btnGuardar.DisplayMode`** (idéntica al prototipo):
```powerapps
If(
   !IsBlank(txtComentario.Value)
   && (!tgPago.Value || CountRows(Filter(colPagos, monto>0 && !IsBlank(Trim(recibo))))>0)
   && (!tgDatos.Value || Or(!IsBlank(inTelCasa.Value), !IsBlank(inTelCel.Value), !IsBlank(inTelTrab.Value),
                            !IsBlank(inDirCasa.Value), !IsBlank(inDirEC.Value), !IsBlank(inDirTrab.Value),
                            !IsBlank(inCorreo.Value))),
   DisplayMode.Edit, DisplayMode.Disabled)
```
**Texto del botón:** `"Guardar interacción" & If(tgPago.Value," + pago","") & If(tgDatos.Value," + datos","")`.
**Hint (`lblHint`):** concatena lo que falta: `comentario` · `monto + n° de recibo` · `algún dato del cliente`.

**`btnGuardar.OnSelect` — un toque crea todo (offline):**
```powerapps
Set(varInt, Patch(sanic_cm_tbl_interaccion, Defaults(sanic_cm_tbl_interaccion), {
    sanic_cm_in_cuentaid: varCuentaSel, sanic_cm_in_agente: gblAgente, sanic_cm_in_comentario: txtComentario.Value,
    ⚠️TODO(sanic_fechainteraccion): Now(), sanic_cm_cd_aplicapago: tgPago.Value, sanic_cm_in_actualizainfo: tgDatos.Value,
    sanic_cm_in_latitud: Location.Latitude, sanic_cm_in_longitud: Location.Longitude,
    sanic_cm_in_estadograbacion: ⚠️TODO('Estado transcripción'.Pendiente) }));
If(tgPago.Value, ForAll(Filter(colPagos, monto>0) As L,
    Patch(sanic_cm_tbl_pago, Defaults(sanic_cm_tbl_pago), { sanic_cm_pg_interaccion: varInt, sanic_cm_pg_monto: L.monto,
        sanic_cm_pg_moneda: Switch(L.moneda,"USD",'Moneda'.USD,'Moneda'.NIO), sanic_cm_pg_numerorecibo: L.recibo,
        ⚠️TODO(sanic_fechapago): Now() })));
If(tgDatos.Value, Patch(sanic_cm_tbl_actualizaciondatos, Defaults(sanic_cm_tbl_actualizaciondatos), {
    sanic_cm_ad_interaccionid: varInt, sanic_cm_ad_telefonocasa: inTelCasa.Value, /* …solo los no vacíos… */
    sanic_cm_ad_correoelectronico: inCorreo.Value }));
Clear(colPagos); Navigate(scrDetalleCuenta, UnCover)
```
> El `Patch` de la interacción devuelve `varInt` (con su GUID) → linkea los hijos **aun sin señal**. Geo del
> dispositivo (sin señal). `INT-/PAG-` se asignan al sincronizar. **Sin conflictos** (append-only).

---

### 8.5 — `scrInicio` · Inicio (resumen del día)

**Objetivo.** “¿Cómo voy hoy?”: **cobrado** (bimoneda), **interacciones** (total y por cuenta/cliente). Hero
clickeable → detalle de pagos. Sin gamificación.

| Control | Estilo | Contenido / Eventos |
|---------|--------|---------------------|
| `conHeader` | `cmpHeaderVerde` **sin** tarjeta total | `lblTitulo="Hola, Marvin"` (`gblAgente.nombre`), `lblSub = Text(Today(),"dddd dd 'de' mmmm")`. Der.: `cmpAa` + `cmpSyncPill` |
| `cardHero` | `Fill=gblC.teal`, `Radius=16`, `Padding=16`, sombra `shTeal`, `margin-top 4` | `lblHl` (“Cobrado hoy”, `gblT.fsm`, `tealSub`) + `lblHa` (`cmpMoneda(cobradoNio, cobradoUsd)`, `gblT.fbig`, Bold(800), White) + `lblHs` (`nPagos&" pago(s) · "&nInt&" interacción(es) · Ver detalle ›"`, `gblT.fxs`, `tealSub`). `OnSelect=Navigate(scrPagos, Cover)` |
| `conStats` | horizontal, `Wrap`, `Gap=10`, `margin-top 12` | 3 `cmpStatC`: Interacciones (`nInt`) · Cuentas (`nCu`) · Clientes (`nCl`) |
| `conResultBar` | igual que Cartera | `"Interacciones de hoy"` + toggle `👤 Cliente / ▤ Cuenta` (`Set(varInicioModo,…)`) |
| `galDesglose` | galería vertical de `cmpBrow` | filas de desglose (abajo). Vacío → `lblEmpty` “Todavía no registraste interacciones hoy.” |

**`cmpStatC`:** `Fill=White`, `Border 1 gblC.cardBorder`, `Radius=12`, `Padding=12`, centrado, sombra `shCard`,
`FillPortions` iguales (min-width 92). `sv` (`gblT.flg`, Bold(800), `gblC.ink`) + `sl2` (`gblT.fxs`, `muted2`).

**`cmpBrow`** (fila de desglose): `Fill=White`, `Border 1 gblC.cardBorder`, `Radius=12`, `Padding 12 14`,
`margin-top 10`, horizontal `Justify=SpaceBetween`, `Gap=10`. Izq.: `bn` (`gblT.fbase`, Semibold, `ink`) + `bs`
(`gblT.fxs`, `muted`). Der.: `bcount` (`Fill=gblC.accentBg`, `Color=gblC.teal`, Bold(800), `Radius=10`,
`Padding 5 11`, `gblT.fsm`) = `"N interac."` + chevron `›` (`gblC.phoneBorder`, `gblT.fmd`).

**Datos (`OnVisible`):**
```powerapps
Set(varIntHoy, Filter(sanic_cm_tbl_interaccion, DateValue(⚠️TODO(sanic_fechainteraccion))=Today()));   // propias de hoy
Set(cobradoNio, Sum(Filter(sanic_cm_tbl_pago, DateValue(⚠️TODO(sanic_fechapago))=Today(), sanic_cm_pg_moneda='Moneda'.NIO), sanic_cm_pg_monto));
Set(cobradoUsd, Sum(Filter(sanic_cm_tbl_pago, DateValue(⚠️TODO(sanic_fechapago))=Today(), sanic_cm_pg_moneda='Moneda'.USD), sanic_cm_pg_monto));
Set(nInt, CountRows(varIntHoy));
Set(nCu, CountRows(Distinct(varIntHoy, sanic_cm_in_cuentaid)));
Set(nCl, CountRows(Distinct(varIntHoy, sanic_cm_in_cuentaid.sanic_cm_ct_clienteid)));
```
**`galDesglose.Items`:**
```powerapps
If(varInicioModo="cliente",
   GroupBy(AddColumns(varIntHoy,"Cli", sanic_cm_in_cuentaid.sanic_cm_ct_clienteid),"Cli","Items"),
   GroupBy(AddColumns(varIntHoy,"Cta", sanic_cm_in_cuentaid),"Cta","Items"))
```
`bcount = CountRows(ThisItem.Items)&" interac."`. **`OnSelect`** de la fila → abre el cliente/cuenta **en pestaña
Interacciones**:
```powerapps
// modo cliente:
Set(varClienteSel, ThisItem.Cli); Set(varCliTab,"inter"); Navigate(scrCliente, Cover)
// modo cuenta:
Set(varCuentaSel, ThisItem.Cta); Set(varDetTab,"inter"); Navigate(scrDetalleCuenta, Cover)
```

---

### 8.6 — `scrPagos` · Detalle de pagos del día

**Objetivo.** Desglosar el “cobrado hoy”: pagos **por cuenta**, cada línea **monto + moneda + recibo + hora**;
total por moneda arriba. Solo lectura.

| Control | Estilo | Contenido |
|---------|--------|-----------|
| `conHeader` | `cmpHeaderBack` + `cmpAa` | `lblDnm="Pagos de hoy"`, `lblDac="Por cuenta y por moneda"`. `btnBack=Navigate(scrInicio, UnCover)` |
| `cardTotal` | `cmpTarjeta` realce verde | `lblDlbl` “Total cobrado” + `lblDbig` (`gblT.fbig`, Bold, teal) = `cmpMoneda(sumN, sumU)` |
| `galGrupos` | galería vertical | una `cmpTarjeta` por cuenta. Vacío → `lblEmpty` “Sin pagos hoy.” |

**Plantilla por cuenta:** `lblCt` (cliente, `cmpSectTitle`) + `lblMc` (producto · cuenta, `gblT.fxs`, `muted`,
`margin-bottom 4`) + galería de líneas:
- cada línea `.crow` (horizontal, `Padding 8 0`, borde inferior 1 `gblC.hairline`): `lblCv`
  (`(moneda="USD"?"US$ ":"C$ ")&Text(monto,…)`, `gblT.fbase`, `ink`) + `lblCt2`
  (`"Recibo "&Coalesce(recibo,"—")&" · "&hora`, `gblT.f2xs`, `muted2`).

**Datos (`OnVisible`):**
```powerapps
Set(varPagosHoy, Filter(sanic_cm_tbl_pago, DateValue(⚠️TODO(sanic_fechapago))=Today()));   // propios de hoy
Set(sumN, Sum(Filter(varPagosHoy, sanic_cm_pg_moneda='Moneda'.NIO), sanic_cm_pg_monto));
Set(sumU, Sum(Filter(varPagosHoy, sanic_cm_pg_moneda='Moneda'.USD), sanic_cm_pg_monto));
// galGrupos.Items:
GroupBy(AddColumns(varPagosHoy, "Cta", sanic_cm_pg_interaccion.sanic_cm_in_cuentaid), "Cta", "Lineas")
```

---

### 8.7 — `cmpListaInteracciones` (pestaña Interacciones, cliente y cuenta)

> **No es pantalla aparte:** es la **pestaña** dentro de `scrCliente` ([Cuentas | **Interacciones**]) y
> `scrDetalleCuenta` ([Detalle | **Interacciones**]). Misma lista, cambia el **scope**.

**Items** (orden `fecha+hora` descendente):
```powerapps
Sort(
  If(scope="cuenta", Filter(sanic_cm_tbl_interaccion, sanic_cm_in_cuentaid = varCuentaSel),
                     Filter(sanic_cm_tbl_interaccion, sanic_cm_in_cuentaid.sanic_cm_ct_clienteid = varClienteSel)),
  ⚠️TODO(sanic_fechainteraccion), SortOrder.Descending)
```

**Tarjeta (`cmpICard`):** `Fill=White`, `Border 1 gblC.cardBorder`, `Radius=12`, `Padding 12 14`,
`margin-top 10`, sombra `shCard`.
- **`ihead`** (horizontal, `Justify=SpaceBetween`, `Wrap`): `itime` (`"🕘 "&Text(fecha,"dd/mm")&" · "&hora`,
  `gblT.fxs`, `muted`, Semibold) + `iacct` (solo scope cliente: `producto·cuenta`, `gblT.fxs`, `muted2`).
- **`icom`** (comentario, `gblT.fbase`, `ink`, `margin-top 8`, line-height 1.4).
- **`itags`** (horizontal, `Wrap`, `Gap=8`, `margin-top 10`): cada tag `Radius=10`, `Padding 4 10`, `gblT.fxs`,
  Semibold:
  - **pago** (si hay pagos): `Fill=gblC.accentBg`, `Color=gblC.accentInk` → `"💵 "&Concat(pagos, (moneda="USD"?"US$ ":"C$ ")&Text(monto,…)&If(!IsBlank(recibo)," (rec. "&recibo&")",""), " · ")`
  - **datos** (si `actualizacliente`): `Fill=gblC.diariaBg` (`#EFF6FF` aprox.→ usar `#EFF6FF`), `Color=gblC.diariaInk` → “✏️ Datos actualizados”
  - **geo** (siempre): `Fill=gblC.screen`, `Color=gblC.muted` → “📍 Ubicación”.

> Nota: el tag **datos** usa fondo `#EFF6FF` (azul muy claro) y texto `#2563EB`. Agregalo como `gblC.datosBg =
> ColorValue("#EFF6FF")` si querés tokenizarlo.

---

### 8.8 — `scrSincronizacion` · Sincronización

**Objetivo.** Lectura clara: ¿hay señal?, ¿cuántas capturas no subieron?, y refrescar — **complemento** del globo
nativo (fuente de verdad). Recordá: solo `Connection` es legible en Power Fx; los pendientes son **proxy** por
autonumber en blanco.

| Control | Estilo | Contenido |
|---------|--------|-----------|
| `conHeader` | `cmpHeaderBack` (sin Aa) | `lblDnm="Sincronización"`, `lblDac="Estado de tus capturas"`. `btnBack=Navigate(varSyncBack, UnCover)` |
| `cardEstado` | `statbig`: horizontal, `Radius=16`, `Padding=16`, `Gap=14`, `Color=White` | ícono grande (`28×gblScale`) + `st1` (`gblT.flg`, Bold(800)) + `st2` (`gblT.fsm`, opacidad .92). **Fill por estado:** ok→`gblC.teal` · pendiente→`gblC.incobInk` (`#B45309`) · sin conexión→`gblC.offGray` (`#475467`) |
| `cardDetalle` | `cmpTarjeta` | 3 `cmpKV`: Conexión (`If(Connection.Connected,"En línea","Sin señal")`) · Última sincronización · Pendientes por subir (`<proxy> & " captura(s)"`) |
| `btnBuscar` | botón grande `bigbtn`: `Fill=gblC.teal`, `Color=White`, `Radius=14`, `Padding=14`, ancho completo, sombra `shTeal`, `margin-top 14` | texto: `If(Connection.Connected,"↻ Buscar actualizaciones","Sin conexión — no se puede sincronizar")`. `DisplayMode = If(Connection.Connected && <pendientes>0, Edit, Disabled)`. `OnSelect=Refresh(sanic_cm_tbl_interaccion); Refresh(sanic_cm_tbl_pago)` |
| `lblCapturas` | `cmpSectTitle`, `margin-top 18` | “Capturas de hoy” |
| `galCapturas` | galería de `cmpBrow` (cursor default) | una fila por interacción de hoy: izq. `cliente · producto` + `hora · Interacción [+ N pago(s)] [+ datos]`; der. **pendtag** |
| `cardNota` | `note2`: `Fill=gblC.noteBg`, `Border 1 gblC.cardBorder`, `Radius=12`, `Padding 12 14`, `margin-top 14` | `gblT.fxs`, `muted` — explica INT-/PAG- server-assigned y remite al **globo/Device Status**. **bold** en `gblC.ink2` |

**`pendtag`:** `Radius=10`, `Padding 4 10`, `gblT.fxs`, Bold. **Pendiente** (`IsBlank(autonumber)`):
`Fill=gblC.incobBg` (`#FEF3C7`), `Color=gblC.incobInk` (`#B45309`), texto “sin Nº aún”. **Subido:**
`Fill=gblC.accentBg`, `Color=gblC.accentInk`, texto “Nº asignado”.

**Proxy de pendientes (en `OnVisible`):**
```powerapps
Set(varPend, CountRows(Filter(sanic_cm_tbl_interaccion, IsBlank(sanic_cm_in_numerointeraccion)))
           + CountRows(Filter(sanic_cm_tbl_pago, IsBlank(sanic_cm_pg_numero))));
Set(varEstado, If(!Connection.Connected, "off", If(varPend>0, "pend", "ok")));
Set(varCapHoy, Filter(sanic_cm_tbl_interaccion, DateValue(⚠️TODO(sanic_fechainteraccion))=Today()));
```
**Estado → ícono/texto:** ok `✓ Sincronizado / Todo al día con el servidor` · pend
`🔼 Pendiente de subir / N captura(s)` · off `📴 Sin conexión / N captura(s) en el dispositivo`.

> El botón **demo “simular pérdida de señal”** del prototipo **no se construye** (es solo para la maqueta). En la
> app real el estado lo da `Connection.Connected` de verdad.

---

### 8.9 — `scrStats` · Mis Estadísticas (placeholder)

Sin gamificación por ahora (DEC-011). Header `cmpHeaderVerde` (“Mis Estadísticas”, solo `cmpAa`) + centro
`lblPh` (`gblT.fbase`, `muted2`, centrado, `Padding 60 24`): *“📊 Ranking y gamificación — próximamente —”*.

---

### 8.10 — Helper bimoneda `cmpMoneda` (cross-cutting)

> 💱 Las cuentas operan en **C$ (NIO) y US$ (USD) a la vez**: saldo vencido, pago mínimo y Retiro **en ambas**,
> más **total dolarizado**. **Ningún monto se muestra en una sola moneda.** Replica del helper `m2(nio,usd)` del
> prototipo: muestra **“C$ X · US$ Y”** y **omite la moneda en cero** (si ambas son 0 → “C$ 0.00”).

```powerapps
// Componente de salida (o función reutilizable). Devuelve el texto bimoneda.
With({ pn: If(nio>0, "C$ " & Text(nio, "[$-es]#,##0.00"), ""),
       pu: If(usd>0, "US$ " & Text(usd, "[$-es]#,##0.00"), "") },
   Coalesce(
      Concatenate(pn, If(!IsBlank(pn) && !IsBlank(pu), " · ", ""), pu),
      "C$ 0.00"
   ))
```
El **total dolarizado** (`sanic_cm_ct_totalsaldovencido`) se muestra **aparte** como la cifra **comparable** entre
cuentas — nunca se suma con las otras monedas.

---

## 9. Checklist de construcción (orden recomendado)

1. **App.OnStart** con tokens (`gblFont`, `gblC`, `gblScale`, `gblT`, `gblAgente`, vars de estado). §2.5 / §5.
2. **Perfil offline** configurado con las tablas/filtros de §4. Habilitar offline en la app.
3. **Componentes** §6 (header verde, header back, badge, mora, tarjeta, botones, sync pill, nav, tabbar, toggle,
   sheet, `cmpMoneda`). Probar el `gblScale` en uno antes de seguir.
4. **scrCartera** (§8.1) — la más rica; valida agrupación, filtro, búsqueda, reordenar.
5. **scrCliente** y **scrDetalleCuenta** (§8.2/§8.3) con la **pestaña Interacciones** (§8.7).
6. **scrInteraccion** (§8.4) — la captura, validación y `Patch` múltiple.
7. **scrInicio** (§8.5) + **scrPagos** (§8.6).
8. **scrSincronizacion** (§8.8) + **scrStats** placeholder (§8.9).
9. **Probar en player móvil, en modo offline:** leer cartera, reordenar (persiste y sincroniza), capturar
   interacción+pago+datos sin señal, ver pendientes “sin Nº aún”, recuperar señal y verificar el sync.
10. **Accesibilidad:** recorrer las 4 escalas y confirmar que **nada se rompe** (reflow, sin truncar).

---

## 10. Trazabilidad de decisiones

| Aspecto | Decisión |
|---------|----------|
| Canvas offline-first | DEC-001 |
| Captura = interacción + pago(s) + datos en una pantalla | DEC-002 / DEC-010 / DEC-013 |
| Agente = localizador (owner) | DEC-006 / DEC-015 |
| 3 tipos de asignación + reordenar por `sanic_cm_as_orden` | DEC-012 / DEC-016 |
| Bimoneda + total dolarizado | modelo `sanic_cm_tbl_cuenta` (DEC-017) |
| Sin gamificación (placeholder) | DEC-011 |
| Sync: complemento del globo nativo; `Connection` único legible | verificado (learn.microsoft.com) |
