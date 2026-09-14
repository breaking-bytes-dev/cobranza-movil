# Guía 01 — La app: creación, datos, fórmulas globales, estilos y recetas

App: **App - CM - Backoffice** (canvas). Mockup aprobado: `docs/mockups/backoffice-canvas.html`.
Esquema verificado contra Dev el 2026-09-13 (ver 00 §encabezado). **Si IntelliSense difiere de un
display name de estas guías, el IntelliSense manda** — anotalo y avisá.

## 0. Contrato de formato de las guías de pantalla (02–12)

Toda guía de pantalla cumple ESTO (el revisor R2 verifica contra esta lista):

1. Abre con **propósito + contrato de entrada**: qué variables globales/colecciones deben venir
   seteadas y quién las setea.
2. **Árbol de controles completo** con el nombre final de CADA control (sufijo de pantalla incluido).
3. **Cobertura 1:1**: todo control del árbol aparece después con su tabla de propiedades, una fila
   compartida, o un puntero a receta de esta guía que fija TODAS sus propiedades.
4. **Tipo explícito** con el nombre del menú Insertar de Studio. Catálogo cerrado de este proyecto:
   - `Contenedor` (manual — NUNCA los de auto-layout horizontal/vertical)
   - `Etiqueta clásica` · `Botón clásico` · `Icono` · `Imagen` · `Rectángulo`
   - `Entrada de texto clásica` · `Cuadro combinado` · `Casilla clásica` · `Alternar clásico`
   - `Galería vertical en blanco` · `Galería horizontal en blanco`
   - `Temporizador` (solo si una receta lo pide)
   - Componentes locales: `cmpNavLateral` · `cmpEncabezado` · `cmpKpi` · `cmpVacio`
5. **Propiedades**: solo las que cambian del default; tipografía/colores por alias `ESTILO-*` (§6).
6. Posiciones/tamaños **relativos** (`Parent.Width - 2*gblE.Pad`); los únicos literales permitidos
   son los tokens (`gblE.*`) y alturas de fila/TemplateSize documentadas.
7. Fórmulas **completas y listas para pegar** (OnVisible entero, Patch enteros). Nada de "similar a".
8. Cada guía cierra con **Checklist de salida** verificable por el constructor.
9. Toda desviación del mockup referencia su entrada `LED-xx` del 00 §2.
10. Formatos de fecha SIEMPRE con `HH` (24 h): `Text(x, "dd/mm HH:mm")`.

## 1. Crear la app

1. make.powerapps.com → entorno Dev → **Nueva app en blanco → Formato tableta**.
2. Configuración → Pantalla: **Escalar para ajustar = Desactivado** · Bloquear relación de aspecto =
   Desactivado · Bloquear orientación = Desactivado. (Esto la vuelve responsive de verdad.)
3. Configuración → General: **Límite de filas de consultas no delegables = 2000**.
4. Nombre al guardar: `App - CM - Backoffice Canvas` (el definitivo se decide al retirar la MDA).
5. Icono: el del paso 3 del proyecto (teal `#0F6B6B`).

## 2. Orígenes de datos (Dataverse — agregar TODOS antes de construir)

`'CM | TBL | Interaccions'` · `'CM | TBL | Pagos'` · `'CM | TBL | Asignacions'` ·
`'CM | TBL | Cuentas'` · `'CM | TBL | Clientes'` · `'CM | TBL | Agentes'` ·
`'CM | TBL | Carga diarias'` · `'CM | TBL | Telefonos'` · `'CM | TBL | Direccions'` ·
`'CM | TBL | Actualizacion de datos'` (⚠ plural raro — verificar en el panel de datos) ·
`'CM | TBL | Usuario Apps'` (tabla nueva, 00 §3.1) · `Users` (systemuser).

## 3. App.Formulas (fórmulas con nombre — pegar completo)

```powerfx
// Bimoneda: "C$ X · US$ Y", omite la moneda en cero. Igual que la app del localizador.
Moneda(nio:Number, usd:Number):Text =
    Coalesce(
        Concat(
            Table(
                {t: If(Coalesce(nio,0) > 0, "C$ " & Text(nio, "#,##0.00"))},
                {t: If(Coalesce(usd,0) > 0, "US$ " & Text(usd, "#,##0.00"))}
            ),
            t, " · "
        ),
        "C$ 0.00"
    );

// Breakpoint responsive único del proyecto (00 §7).
nfMovil = App.ActiveScreen.Width < 920;

// Última carga registrada (delegable: Sort + First). La usan Monitoreo y Reconciliación.
nfUltimaCarga = First(
    Sort('CM | TBL | Carga diarias', '[CD] FechaCarga', SortOrder.Descending)
).'[CD] FechaCarga';
```

> Si Studio rechaza `App.ActiveScreen` en fórmulas con nombre, mover `nfMovil` a cada pantalla como
> `Parent.Width < 920` — la guía de pantalla ya escribe `nfMovil`, solo cambia la definición.

## 4. App.OnStart, App.OnError y StartScreen

```powerfx
// ===== App.OnStart =====
// 1) Tokens de color (identidad de la familia, modo día)
Set(gblC, {
    teal:ColorValue("#0F6B6B"), tealOscuro:ColorValue("#0B5252"), tealSuave:ColorValue("#E0EEEE"),
    pantalla:ColorValue("#F2F4F7"), card:ColorValue("#FFFFFF"),
    borde:ColorValue("#E4E7EC"), hairline:ColorValue("#F0F2F5"),
    ink:ColorValue("#101828"), ink2:ColorValue("#475467"), muted:ColorValue("#667085"), muted2:ColorValue("#98A2B3"),
    okBg:ColorValue("#ECFDF5"), okInk:ColorValue("#067647"),
    pendBg:ColorValue("#FEF3C7"), pendInk:ColorValue("#B45309"),
    redBg:ColorValue("#FEE2E2"), redInk:ColorValue("#DC2626"),
    diariaBg:ColorValue("#DBEAFE"), diariaInk:ColorValue("#2563EB"),
    incobBg:ColorValue("#FEF3C7"), incobInk:ColorValue("#B45309"),
    espBg:ColorValue("#EDE9FE"), espInk:ColorValue("#7C3AED"),
    blancoTr:RGBA(255,255,255,0.18), scrim:RGBA(16,24,40,0.45)
});
// 2) Tokens de medida (los ÚNICOS números "absolutos" permitidos en las guías)
Set(gblE, {Pad:16, PadSm:10, AnchoNav:240, Radio:12, RadioSm:9, AltoBarra:56, AltoFila:64,
           AltoInput:40, AltoBoton:44, AnchoMax:1160});
// 3) Identidad: usuario Dataverse + roles de app (00 §3.1 — la tabla REAL es lookup + MULTI-rol)
Set(gblUsuarioDV, LookUp(Users, 'Primary Email' = Lower(User().Email)));
Set(gblFilaRol, LookUp('CM | TBL | Usuario Apps',
        '[UA] Usuario' = gblUsuarioDV && '[UA] Activo' = true));
// '[UA] Rol' es MULTI-SELECT: devuelve una tabla de choices. gblRol = etiquetas unidas con "|".
// Ej.: "Backoffice|Admin". Sin fila => Blank() => estado "sin acceso" de scrInicio.
Set(gblRol, Concat(gblFilaRol.'[UA] Rol', Text(Value), "|"));
// ⚠ El display del lookup hoy tiene un typo en Dev: "[UA[ Usuario". Si IntelliSense lo muestra
// así, usalo así (IntelliSense manda) hasta que se renombre a '[UA] Usuario'.
// 4) Navegación por rol: la tabla que alimenta cmpNavLateral
ClearCollect(colNav,
    {Orden:1, Icono:"🏠", Titulo:"Inicio",            Ruta:"ini",  Roles:"Backoffice|Solicitante|Supervisor|Admin"},
    {Orden:2, Icono:"📥", Titulo:"Por transcribir",   Ruta:"trx",  Roles:"Backoffice"},
    {Orden:3, Icono:"✅", Titulo:"Transcritas hoy",   Ruta:"tsc",  Roles:"Backoffice"},
    {Orden:4, Icono:"✨", Titulo:"Nueva solicitud",   Ruta:"nva",  Roles:"Solicitante"},
    {Orden:5, Icono:"🗂️", Titulo:"Mis solicitudes",  Ruta:"sol",  Roles:"Solicitante"},
    {Orden:6, Icono:"⚖️", Titulo:"Aprobaciones",      Ruta:"aprb", Roles:"Supervisor"},
    {Orden:7, Icono:"📡", Titulo:"Monitoreo de carga",Ruta:"mon",  Roles:"Admin"},
    {Orden:8, Icono:"🧹", Titulo:"Reconciliación",    Ruta:"rec",  Roles:"Admin"},
    {Orden:9, Icono:"🔒", Titulo:"Cierres",           Ruta:"cie",  Roles:"Admin"},
    {Orden:10,Icono:"🏍️", Titulo:"Agentes y usuarios",Ruta:"age", Roles:"Admin"},
    {Orden:11,Icono:"👥", Titulo:"Datos maestros",    Ruta:"mae",  Roles:"Admin"}
);
Set(gblNavMovilAbierta, false)

// ===== App.OnError =====
// NO SE DEFINE — decisión del usuario (2026-09-14): el banner nativo de Power Apps muestra el
// error CRUDO con su detalle, para que el usuario lo vea, lo entienda y lo pueda reportar.
// Ninguna guía debe agregar App.OnError ni envolver errores en Notify genéricos.

// ===== App.StartScreen =====
scrInicio
```

**Guard de acceso** (va en el `OnVisible` de CADA pantalla que no sea scrInicio, primera línea —
las guías lo incluyen ya con sus roles). Con multi-rol, la pregunta es "¿el usuario TIENE este
rol?" — el rol buscado va a la IZQUIERDA del `in` (contains de texto local, no delegación):
```powerfx
If(!("Backoffice" in gblRol), Navigate(scrInicio)); // ejemplo scrTranscripcion
If(!("Backoffice" in gblRol || "Admin" in gblRol), Navigate(scrInicio)); // multi-rol OR
```
Seguro porque ningún nombre de rol es substring de otro (Backoffice/Solicitante/Supervisor/Admin).

## 5. Convenciones de nombres (no negociable)

- Prefijos: `scr` pantalla · `con` contenedor · `gal` galería · `lbl` etiqueta · `btn` botón ·
  `txt` entrada de texto · `cmb` cuadro combinado · `chk` casilla · `tgl` alternar · `ico` icono ·
  `rec` rectángulo · `img` imagen · `cmp` componente instanciado.
- **Sufijo de pantalla obligatorio** en todo control: `Ini, Trx, Tsc, Sol, Nva, Aprb, Mon, Rec, Cie, Age, Mae`.
  Ej.: `galColaTrx`, `txtCodigoTrx`, `conPanelAprb`. Sin sufijo, Studio renombra en silencio al
  duplicar patrones y las fórmulas de las guías dejan de corresponder (PA2110).
- Variables: `gbl*` globales · `var*` de pantalla · `col*` colecciones. Nada de `Set` dentro de
  galerías; los `UpdateContext` no se usan en este proyecto (solo `Set`/`ClearCollect`).

## 6. Defaults tipográficos y alias ESTILO-*

**Defaults de TODA etiqueta/botón salvo indicación**: `Font = Font.'Segoe UI'` · `Color = gblC.ink` ·
`Size = 10`. Escala del proyecto (Size): `8` xs · `9` sm · `10` base · `11` md · `13` lg · `15` xl ·
`18` big · `24` héroe (solo nº de cuenta de la estación).

| Alias | Se aplica así |
|---|---|
| ESTILO-Pantalla | `Fill = gblC.pantalla` en la pantalla |
| ESTILO-Card | Contenedor: `Fill = gblC.card`, `BorderColor = gblC.borde`, `BorderThickness = 1`, `RadiusTopLeft/TopRight/BottomLeft/BottomRight = gblE.Radio` (⚠ Radius solo existe en contenedor/botón/rectángulo; si el control no lo tiene, va dentro de un contenedor que sí) |
| ESTILO-CardTeal | Igual + `Fill = gblC.teal`; textos internos `Color = Color.White` |
| ESTILO-TituloPagina | Etiqueta: `Size = 18`, `FontWeight = Bold`, `Color = gblC.ink` |
| ESTILO-Subtitulo | Etiqueta: `Size = 9`, `Color = gblC.muted` |
| ESTILO-SecHead | Etiqueta: `Size = 8`, `FontWeight = Bold`, `Color = gblC.muted` — texto en MAYÚSCULAS |
| ESTILO-BotonPri | Botón clásico: `Fill = gblC.teal`, `Color = White`, `HoverFill = ColorFade(gblC.teal, 10%)`, `Radius* = 10`, `Height = gblE.AltoBoton`, `FontWeight = Semibold`, `DisabledFill = ColorFade(gblC.teal, 60%)`, `DisabledColor = White` |
| ESTILO-BotonSec | `Fill = gblC.tealSuave`, `Color = gblC.teal`, resto como Pri |
| ESTILO-BotonPeligro | `Fill = gblC.redBg`, `Color = gblC.redInk`, resto como Pri |
| ESTILO-BotonFantasma | `Fill = gblC.card`, `Color = gblC.ink2`, `BorderColor = gblC.borde`, `BorderThickness = 1` |
| ESTILO-Input | Entrada de texto: `Fill = gblC.card`, `BorderColor = gblC.borde`, `Height = gblE.AltoInput`, `RadiusTopLeft… = 10`, `Size = 10`; `BorderColor` en foco lo da el tema |
| ESTILO-Chip | Receta 7.2 |

## 7. Recetas reutilizables

### 7.1 Card blanca
`Contenedor` con ESTILO-Card. Los hijos se posicionan relativos: `X = gblE.Pad`,
`Width = Parent.Width - 2*gblE.Pad`.

### 7.2 Chip / píldora de estado (`chp…`)
`Botón clásico` en `DisplayMode = DisplayMode.View`, `Radius* = 8`, `Size = 8`, `FontWeight = Bold`,
⚠ **Excepción (hallazgo R2-11): dentro de una fila clicable (7.3) el chip va SIN `DisplayMode.View`**
y con `OnSelect = Select(Parent)` — en View el chip se come el clic de la fila. Fuera de galerías
clicables, View se mantiene.
`Height = 26`, `Width = 14 + 7*Len(Self.Text)` (aprox; ajustar si trunca), sin borde.
Variantes: Diaria `Fill=gblC.diariaBg, Color=gblC.diariaInk` · Incobrable `incobBg/incobInk` ·
Especial `espBg/espInk` · OK `okBg/okInk` · Pendiente `pendBg/pendInk` · Error `redBg/redInk` ·
Neutro `Fill=gblC.hairline, Color=gblC.ink2` (sobre card blanca SIEMPRE lleva esta variante con
fill visible — nunca blanco sobre blanco).

### 7.3 Fila clicable de galería
La acción vive en el `OnSelect` de la **galería**. TODO hijo del template (etiquetas, chips, iconos)
lleva `OnSelect = Select(Parent)`. Overlay opcional `recFilaX`: `X=0 · Y=0 · Width=Parent.TemplateWidth ·
Height=Parent.TemplateHeight · Fill=Transparent · al FONDO del z-order · OnSelect=Select(Parent)`.
Resaltado de fila seleccionada: `TemplateFill = If(ThisItem.IsSelected, gblC.tealSuave, gblC.card)`.

### 7.4 Panel lateral de detalle (overlay)
Dos controles directos en la pantalla, AL FINAL del árbol (encima de todo):
- `recScrim…`: `Rectángulo` · `X=0 Y=0 Width=Parent.Width Height=Parent.Height` · `Fill=gblC.scrim` ·
  `Visible = varPanel…` · `OnSelect = Set(varPanel…, false)`.
- `conPanel…`: `Contenedor` ESTILO-Card con `Radius*=0` ·
  `Width = If(nfMovil, Parent.Width, Min(480, Parent.Width*0.45))` · `Height = Parent.Height` ·
  `X = Parent.Width - Self.Width` · `Y = 0` · `Visible = varPanel…`.
  Estructura interna fija: fila título (etiqueta ESTILO-TituloPagina Size 13 + `icoCerrar…` con
  `OnSelect = Set(varPanel…, false)`) · cuerpo · fila de botones al pie.
La pantalla abre con `Set(varPanel…, true)` desde el OnSelect de la galería (7.3).

### 7.5 Encabezado de página
Instancia de `cmpEncabezado` (§8.2): `X = If(nfMovil, 0, gblE.AnchoNav)` · `Y = 0`
· `Width = Parent.Width - Self.X` · `Height = If(nfMovil, gblE.AltoBarra + 84, 84)`.
(La barra móvil vive DENTRO del componente en Y=0 — corregido tras el Lote 0: la instancia NO
suma otro offset.) Propiedades `Titulo`/`Subtitulo` según pantalla. El contenido de la pantalla
arranca en `Y = cmpEncXxx.Y + cmpEncXxx.Height`.

### 7.6 Toolbar de búsqueda
`Contenedor` transparente con `txtBuscar…` (ESTILO-Input, `HintText` según pantalla, ícono 🔎 como
`Icono` Search superpuesto a la izquierda, `Padding` izquierdo 34) y controles extra a la derecha.
La búsqueda SIEMPRE filtra colecciones en memoria (00 §4) — jamás el datasource, salvo la receta 7.9.

### 7.7 Colección del día (el patrón anti-delegación)
```powerfx
// OnVisible tipo — SIEMPRE este orden: guard de rol → colecciones → estado de pantalla.
// El If de recarga va POR FUERA del ClearCollect (gotcha: ClearCollect(col, If(cond,...)) revienta).
If(!("Backoffice" in gblRol), Navigate(scrInicio));
Concurrent(
    ClearCollect(colPend,
        AddColumns(
            Filter('CM | TBL | Interaccions',
                '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Pendiente),
            cliente, Coalesce(LookUp(colCtasTrx, '[CT] Numero de cuenta' = '[IN] Numerocuenta').'Nombre Cliente', "—")
        )
    ),
    ClearCollect(colCtasTrx, ShowColumns('CM | TBL | Cuentas',
        '[CT] Numero de cuenta', 'Nombre Cliente', 'CIF Cliente', 'Tipo Producto'))
);
```
⚠ El `AddColumns` que hornea joins corre DESPUÉS de tener la colección de cuentas → si el join se
necesita, las dos `ClearCollect` NO van en `Concurrent` (secuencial: cuentas primero). La guía de
cada pantalla da su OnVisible exacto. Cota 2000 declarada en 00 §4.1.

### 7.8 Patch de transcripción (marcar y seguir)
```powerfx
Patch('CM | TBL | Interaccions', LookUp('CM | TBL | Interaccions',
        '[IN] Numero de Interaccion' = varActualTrx.'[IN] Numero de Interaccion'),
    {
        '[IN] Estado grabacion': 'Choice | CM | Estado grabacion Interaccio'.Grabado,
        '[IN] Codigo Grabacion': Upper(Trim(txtCodigoTrx.Text)),
        '[IN] Fecha grabacion': Now(),          // ← quitar estas 2 líneas cuando
        '[IN] Grabado por': gblUsuarioDV        //    el flujo de auditoría esté activo (00 §3.2)
    }
);
```
Tras el Patch: `Remove(colPend, ...)` local + reselección del siguiente — la guía 03 lo da completo.

### 7.9 Búsqueda delegable de maestros (la ÚNICA búsqueda directa a datasource)
```powerfx
Filter('CM | TBL | Clientes',
    StartsWith('[CL] CIF', txtBuscarMae.Text) || StartsWith('[CL] Nombre', txtBuscarMae.Text))
```
`StartsWith` es delegable en Dataverse. `Search`/`in` sobre datasource: prohibidos (00 §4.3).

### 7.10 Confirmación con casilla
`chkConfirmar…` (`Casilla clásica`, texto largo → `Wrap=true` y `Height` explícito ≥ 40) +
botón peligro con `DisplayMode = If(chkConfirmar….Value, DisplayMode.Edit, DisplayMode.Disabled)`.

### 7.11 Separador de fila en galería
`Rectángulo` `recSep…`: `X=0 · Width=Parent.TemplateWidth · Height=1 · Y = Parent.TemplateHeight - 1 ·
Fill = gblC.hairline`. (Regla, no dos literales sueltos.)

### 7.12 Estado vacío
Instancia de `cmpVacio` (§8.4), `Visible = CountRows(<colección>) = 0`, centrado en el área de la lista.

### 7.13 Contenido centrado (va en TODAS las pantallas)
El contenedor de contenido de cada pantalla se centra con tope, replicando el `.content` del mockup:
`X = If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` ·
`Width = Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)`.
Las guías lo referencian como "receta 7.13"; si una pantalla estira a todo el ancho, lo declara.

### 7.14 Notificaciones
Éxito: `Notify("✓ …", NotificationType.Success, 2500)`. Error de validación:
`Notify("…", NotificationType.Warning)`. Los errores duros los captura App.OnError.

## 8. Componentes locales (crear ANTES que las pantallas, en este orden)

> Todos con **Acceder al ámbito de la aplicación = Activado** (leen `gblC/gblE/nfMovil/colNav` y navegan).
> Contenedores manuales adentro, mismas convenciones de nombres con sufijo `Cmp`.

### 8.1 `cmpNavLateral` — navegación por rol (LA pieza de control de acceso visual)
- Tamaño: `Width = gblE.AnchoNav` · `Height = App.Height` (la instancia lo fija por pantalla).
- Custom properties (Entrada): `RutaActiva` (Texto) — la pantalla le pasa su ruta (`"trx"`, `"aprb"`…).
- Estructura: `conRaizNavCmp` (Fill `gblC.teal`) → marca (logo ₡ + "Cobranza Móvil / BACKOFFICE") →
  `galNavCmp` (`Galería vertical en blanco`):
  - `Items = SortByColumns(Filter(colNav, CountRows(Filter(Split(Roles, "|"), Value in gblRol)) > 0), "Orden")`
    ← el filtro de ROL vive acá (multi-rol: el ítem se muestra si ALGUNO de sus roles está en
    gblRol): un rol jamás VE navegación ajena. Todo en memoria (colNav + Split), cero delegación.
  - Template: icono (etiqueta) + título — **sin badge de conteo** (LED-17); fila activa
    `TemplateFill = If(ThisItem.Ruta = cmpNavLateral.RutaActiva, gblC.blancoTr, Color.Transparent)`.
  - `OnSelect = Switch(ThisItem.Ruta,
      "ini", Navigate(scrInicio), "trx", Navigate(scrTranscripcion), "tsc", Navigate(scrTranscritas),
      "nva", Navigate(scrNuevaSolicitud), "sol", Navigate(scrMisSolicitudes), "aprb", Navigate(scrAprobaciones),
      "mon", Navigate(scrMonitoreo), "rec", Navigate(scrReconciliacion), "cie", Navigate(scrCierres),
      "age", Navigate(scrAgentes), "mae", Navigate(scrMaestros)); Set(gblNavMovilAbierta, false)`
  - Pie: avatar (iniciales de `User().FullName`) + nombre + `gblRol`.
- Instanciación por pantalla (misma receta siempre): `Visible = !nfMovil || gblNavMovilAbierta` ·
  `X=0 · Y=0 · Height=Parent.Height`; en móvil flota encima (va al FINAL del árbol de la pantalla,
  después del panel 7.4).

### 8.2 `cmpEncabezado`
- Custom properties (Entrada): `Titulo` (Texto), `Subtitulo` (Texto).
- Estructura: `conRaizEncCmp` transparente → `lblTituloEncCmp` (ESTILO-TituloPagina,
  `Text = cmpEncabezado.Titulo`) → `lblSubEncCmp` (ESTILO-Subtitulo, `Text = cmpEncabezado.Subtitulo`,
  `Visible = !IsBlank(Self.Text)`). Padding interno `gblE.Pad`.
- La **barra móvil** vive acá también: `conBarraMovilEncCmp` (`Visible = nfMovil`, `Fill = gblC.teal`,
  `Height = gblE.AltoBarra`, ancho total) con `icoMenuEncCmp` (☰,
  `OnSelect = Set(gblNavMovilAbierta, true)`) + título corto blanco + `lblAvatarEncCmp` a la derecha
  (iniciales de `User().FullName` — `Concat(FirstN(Split(User().FullName, " "), 2), Left(Value, 1))` —
  sobre círculo `gblC.blancoTr`, hallazgo R1-13).

### 8.3 `cmpKpi`
- Custom properties (Entrada): `Etiqueta` (Texto), `Valor` (Texto), `Detalle` (Texto), `EsHero` (Booleano).
- `conRaizKpiCmp`: ESTILO-Card; si `EsHero` → ESTILO-CardTeal. Adentro: etiqueta ESTILO-SecHead
  (`Color = If(cmpKpi.EsHero, gblC.blancoTr → texto blanco 75%, gblC.muted)`), valor `Size=18 Bold`
  (blanco si hero), detalle ESTILO-Subtitulo. Altura fija 96.

### 8.4 `cmpVacio`
- Custom properties (Entrada): `Icono` (Texto, emoji), `Titulo` (Texto), `Mensaje` (Texto).
- Etiquetas centradas: emoji Size 24, título Size 11 Bold, mensaje ESTILO-Subtitulo `Wrap=true`.

## 9. Orden de construcción

1. App: configuración (§1) + datasources (§2) + Formulas/OnStart/OnError (§3–4).
2. Componentes §8 en orden (Nav → Encabezado → Kpi → Vacio).
3. Pantallas en el orden de las guías 02→12. `scrInicio` primero (StartScreen).
4. Al terminar cada pantalla: su Checklist de salida. Al terminar todo: prueba de humo (§10).

## 10. Prueba de humo (previa al plan de pruebas formal del paso 7)

- [ ] Con un usuario `Backoffice`: la nav muestra SOLO Inicio/Por transcribir/Transcritas.
- [ ] Sin fila en `Usuario Apps`: scrInicio muestra el estado "sin acceso" (guía 02) y ninguna
      otra pantalla es alcanzable.
- [ ] Studio no muestra NINGÚN subrayado azul de delegación (esa es la vara: cero warnings).
- [ ] `App.ActiveScreen.Width < 920` (ventana angosta): nav colapsa a hamburguesa en todas las pantallas.
- [ ] Monitor: abrir scrTranscripcion genera ≤ 3 llamadas a Dataverse; marcar una transcripción = 1.
