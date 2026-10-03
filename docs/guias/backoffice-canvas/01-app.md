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
   ⚠ **Validación en Studio:** la app estuvo en **500** (este paso nunca se había aplicado) y
   cambiarlo en Settings de Studio **NO persistió** vía export. El valor vive en el `.msapr`
   (`msapp/Properties.json` → `"DefaultConnectedDataSourceMaxGetRowsCount": 2000`) y **se fija en el
   fuente**; después de cada export, verificalo ahí (`unzip -p <app>.msapr msapp/Properties.json`).
   Por qué importa: TODA colección (`ClearCollect` de un `Filter`) se trunca a este límite — por eso
   los catálogos no sirven para joins (00 §4.1) y los conteos van con `CountIf` (00 §4.2).
4. Nombre al guardar: `App - CM - Backoffice Canvas` (el definitivo se decide al retirar la MDA).
5. Icono: el del paso 3 del proyecto (teal `#0F6B6B`).

## 2. Orígenes de datos (Dataverse — agregar TODOS antes de construir)

`'CM | TBL | Interaccions'` · `'CM | TBL | Pagos'` · `'CM | TBL | Asignacions'` ·
`'CM | TBL | Cuentas'` · `'CM | TBL | Clientes'` · `'CM | TBL | Agentes'` ·
`'CM | TBL | Carga diarias'` · `'CM | TBL | Telefonos'` · `'CM | TBL | Direccions'` ·
`'CM | TBL | Actualizacion de datos'` (⚠ plural raro — verificar en el panel de datos) ·
`Users` (systemuser) · `'Security Roles'` (role) · `Teams` — estos dos últimos alimentan la
derivación de `gblRol` desde los roles reales (00 §3.1); la tabla `Usuario App` quedó eliminada.

## 3. App.Formulas (fórmulas con nombre — pegar completo)

```powerfx
// Bimoneda: "C$ X · US$ Y", omite la moneda en cero. Igual que la app del localizador.
Moneda(nio:Number, usd:Number):Text =
    Coalesce(
        // Filter saca las entradas en blanco: sin él, Concat(0,0) devuelve " · " (no vacío)
        // y el Coalesce nunca dispara. (Bug real del diseño original, corregido en Studio.)
        Concat(
            Filter(
                Table(
                    {t: If(Coalesce(nio,0) > 0, "C$ " & Text(nio, "#,##0.00"))},
                    {t: If(Coalesce(usd,0) > 0, "US$ " & Text(usd, "#,##0.00"))}
                ),
                !IsBlank(t)
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

// Navegación por rol: catálogo ESTÁTICO que alimenta cmpNavLateral (galNavCmp.Items, §8.1).
// Fórmula con nombre y NO colección: nunca muta (App Checker: "initialized but never updated").
// Reemplaza al ClearCollect(colNav, ...) del OnStart del diseño original.
nfNav = Table(
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
```

⚠ App.Formulas se edita en el FUENTE (`Src/App.pa.yaml`) y se verifica en el export: editarla en
Studio por automatización no persistió (§11).

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
// 3) Identidad: gblRol derivado de la seguridad REAL (00 §3.1 — sin tabla extra).
// a) Roles DIRECTOS del usuario: "SR | CM | Admin" -> "Admin".
// b) Roles por TEAM: el rol se asigna por membresía al team "CM · <perfil> · <ENV>". Canvas NO
//    deja navegar Team -> Security Roles dentro de un ForAll ("not accessible in this context",
//    verificado en Studio, ni con LookUp(Teams, ...)), así que el perfil sale del segmento del
//    medio del nombre del team (el sufijo DEV/PRD se ignora).
//    ⚠ Renombrar un team CM rompe este mapeo (mismo acople que el prefijo "SR | CM | ").
// Conjuntos de UN usuario (puñados de filas): sin delegación en juego, corre una vez.
Set(gblUsuarioDV, LookUp(Users, 'Primary Email' = Lower(User().Email)));
// Sin colección intermedia: dos Concat (texto "|"-separado) -> Split -> Distinct.
// Ni Collect dentro de ForAll ni colección "inicializada y nunca actualizada" (App Checker).
Set(gblRol,
    With(
        {
            rolesDirectos: Concat(
                Filter(gblUsuarioDV.'Security Roles (systemuserroles_association)',
                       StartsWith(Name, "SR | CM | ")),
                Substitute(Name, "SR | CM | ", ""), "|"),
            rolesTeam: Concat(
                gblUsuarioDV.'Teams (teammembership_association)',
                If(StartsWith('Team Name', "CM · "),
                    Switch(Index(Split('Team Name', " · "), 2).Value,
                        "Backoffice",    "BackOffice",
                        "Solicitantes",  "Solicitante",
                        "Supervisores",  "Supervisor",
                        "Admin y Carga", "Admin",
                        "")),
                "|")
        },
        // Multi-rol => "BackOffice|Admin". El `in` de la nav es case-insensitive.
        // Sin ningún rol CM => gblRol vacío => estado "sin acceso" de scrInicio.
        Concat(
            Distinct(Filter(Split(rolesDirectos & "|" & rolesTeam, "|"), !IsBlank(Value)), Value),
            Value, "|")
    )
);
// Si IntelliSense ofrece las relaciones sin el paréntesis ('Security Roles' / 'Teams' pelados),
// usá esa forma — IntelliSense manda.
// 4) La navegación por rol es la fórmula con nombre nfNav (App.Formulas, §3) — ya NO hay colNav.
Set(gblNavMovilAbierta, false)

// ===== App.OnError =====
// NO SE DEFINE — decisión del usuario (2026-09-14): el banner nativo de Power Apps muestra el
// error CRUDO con su detalle, para que el usuario lo vea, lo entienda y lo pueda reportar.
// Ninguna guía debe agregar App.OnError ni envolver errores en Notify genéricos.

// ===== App.StartScreen =====
scrInicio
```

~~**Guard de acceso** en el `OnVisible` de cada pantalla~~ — **ELIMINADO (LED-20)**. El diseño
original ponía `If(!("Backoffice" in gblRol), Navigate(scrInicio))` como primera línea del
`OnVisible`; Studio lo rechaza (*"would automatically always navigate away"*). Las guías 02–12
todavía lo muestran: **no lo pegues**. El control de acceso queda así:
- **Visual**: la nav (`cmpNavLateral`, §8.1) solo lista las rutas de los roles del usuario.
- **Real**: los roles de Dataverse `SR | CM | *` — llegar a una pantalla ajena no da datos ni escritura.
- Con multi-rol, las preguntas de rol que SÍ quedan (Visible de secciones de scrInicio) siguen la
  forma `"Rol" in gblRol` — el rol buscado a la IZQUIERDA del `in` (contains de texto local, no
  delegación). Seguro porque ningún nombre de rol es substring de otro.

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
| ESTILO-Card | Contenedor: `Fill = gblC.card`, `BorderColor = gblC.borde`, `BorderThickness = 1`, `RadiusTopLeft/TopRight/BottomLeft/BottomRight = gblE.Radio` (⚠ Radius solo existe en contenedor y botón — **NO en `Rectangle@2.3.0`** (PA2108, §11); si el control no lo tiene, va dentro de un contenedor que sí) |
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
clicables, View se mantiene. **Superado por el overlay accesible de 7.3**: el overlay va ARRIBA del
z-order y recibe el clic, así que el chip de fila ya NO lleva `OnSelect` (código vigente: `chpFilaPendAprb`,
`chpTipoCie`).
`Height = 26`, `Width = 14 + 7*Len(Self.Text)` (aprox; ajustar si trunca), sin borde.
Variantes: Diaria `Fill=gblC.diariaBg, Color=gblC.diariaInk` · Incobrable `incobBg/incobInk` ·
Especial `espBg/espInk` · OK `okBg/okInk` · Pendiente `pendBg/pendInk` · Error `redBg/redInk` ·
Neutro `Fill=gblC.hairline, Color=gblC.ink2` (sobre card blanca SIEMPRE lleva esta variante con
fill visible — nunca blanco sobre blanco).

### 7.3 Fila clicable de galería — overlay accesible (un tab stop por fila, LED-19)
**Reemplaza** al diseño original (`recFilaX` transparente al FONDO + `OnSelect = Select(Parent)` en
cada hijo): eso daba N tab stops por fila (o ninguno) y etiquetas anunciadas como botones.

La acción sigue viviendo en el `OnSelect` de la **galería**. El template lleva, como **ÚLTIMO hijo**
(= ARRIBA del z-order), un overlay `btnFila<Sufijo>` (`Botón clásico`):

| Propiedad | Valor |
|---|---|
| `X` · `Y` | `0` · `0` |
| `Width` · `Height` | `Parent.TemplateWidth` · `Parent.TemplateHeight` |
| `Fill` · `Color` · `BorderThickness` | `Color.Transparent` · `Color.Transparent` · `0` |
| `HoverFill` · `PressedFill` | `RGBA(15, 107, 107, 0.06)` · `RGBA(15, 107, 107, 0.12)` (teal translúcido; sobre fondo teal, como la nav: `RGBA(255,255,255,0.08)` / `0.14`) |
| `FocusedBorderColor` · `FocusedBorderThickness` | `gblC.teal` (o `Color.White` sobre teal) · `2` |
| `TabIndex` | `0` |
| `Text` | Descriptivo — es lo que anuncia el lector y lo que lee el Accessibility checker: `"Revisar solicitud de " & ThisItem.clienteAprb` |
| `OnSelect` | `Select(Parent)` |

- Etiquetas del template: `TabIndex = -1` (default de la etiqueta clásica) y **sin `OnSelect` propio**
  (el overlay ya recibe el clic). Rectángulos decorativos (`recSep…`): `TabIndex -1` + `AccessibleLabel ""`
  (son el default del `Rectangle` — el export no los escribe; no los cambies).
- ⚠ **NO pongas `AccessibleLabel` en el overlay**: `Classic/Button@2.2.0` no tiene esa propiedad
  (PA2108, §11) — el `Text` cumple esa función.
- La galería lleva `AccessibleLabel` (p.ej. `"Solicitudes pendientes de aprobación"`) y
  `FocusedBorderThickness = 2`.
- Resaltado de fila seleccionada: `TemplateFill = If(ThisItem.IsSelected, gblC.tealSuave, gblC.card)`.
- El checker reportará "Missing tab stop" en las etiquetas: falso positivo consciente (LED-19).
- Galerías NO clicables (`galDesapRec` en `DisplayMode.View`, `galAgentesAge` con controles propios por
  fila) no llevan overlay.

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

### 7.7 Colección del día con joins por NAVEGACIÓN del lookup
**Reemplaza** al patrón original "colección de catálogo `colCtas…` + `LookUp` contra ella": el
catálogo se truncaba al data row limit y el join devolvía `"—"` para las cuentas que quedaban fuera
(00 §4.1). Ahora la columna se lee navegando el lookup del propio registro — lo resuelve el servidor:
```powerfx
// OnVisible tipo — orden: colecciones (+ conteos CountIf, receta 7.16) → estado de pantalla.
// SIN guard de rol (LED-20). El If de recarga va POR FUERA del ClearCollect
// (gotcha: ClearCollect(col, If(cond,...)) revienta).
ClearCollect(colPendTrx,
    AddColumns(
        Filter('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Pendiente),
        clienteTrx,  Upper(Coalesce(ThisRecord.'[IN] Cuenta'.'Nombre Cliente', "—")),
        cifTrx,      Coalesce(ThisRecord.'[IN] Cuenta'.'CIF Cliente', "—"),
        productoTrx, Coalesce(ThisRecord.'[IN] Cuenta'.'Tipo Producto', "—")
    )
);
```
- Nombres de las columnas agregadas: **identificadores sin comillas** (`clienteTrx`, no `"clienteTrx"`)
  y que NO existan ya en la tabla — `AddColumns` sobre una columna existente falla (§11).
- Ya no hace falta secuenciar "cuentas primero": no hay catálogo. Si la pantalla carga 2+ cosas
  independientes, van en `Concurrent`.
- Cota 2000 de la LISTA declarada en 00 §4.1; los NÚMEROS no salen de acá (receta 7.16).

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
⚠ Para ESCRIBIR el lookup se pasa el registro (`gblUsuarioDV`); para COMPARARLO en un `Filter`/
`CountIf` se compara por PK: `'[IN] Grabado por'.User = gblUsuarioDV.User` (lookup = registro da
el error "Record, Record" — §11).
Tras el Patch: `Remove(colPend, ...)` local + reselección del siguiente — la guía 03 lo da completo.

### 7.9 Búsqueda delegable de maestros (la ÚNICA búsqueda directa a datasource)
```powerfx
Filter('CM | TBL | Clientes',
    StartsWith('[CL] CIF', txtBuscarMae.Text) || StartsWith('[CL] Nombre', txtBuscarMae.Text))
```
`StartsWith` es delegable en Dataverse. `Search`/`in` sobre datasource: prohibidos (00 §4.3).

### 7.10 Confirmación con casilla
`chkConfirmar…` (`Casilla clásica`, texto largo → `Height` explícito ≥ 40 y `Width` suficiente;
⚠ **NO `Wrap`**: `Classic/CheckBox@2.1.0` no tiene esa propiedad — PA2108, §11) +
botón peligro con `DisplayMode = If(chkConfirmar….Value, DisplayMode.Edit, DisplayMode.Disabled)`.

### 7.11 Separador de fila en galería
`Rectángulo` `recSep…`: `X=0 · Width=Parent.TemplateWidth · Height=1 · Y = Parent.TemplateHeight - 1 ·
Fill = gblC.hairline`. (Regla, no dos literales sueltos.)

### 7.12 Estado vacío
Instancia de `cmpVacio` (§8.4), `Visible = <galería>.AllItemsCount = 0` (o `CountRows(<colección>) = 0`
si no hay galería que la muestre — NUNCA `CountRows(<galería>.AllItems)`, que materializa todos los
ítems), centrado en el área de la lista.

### 7.13 Contenido centrado (va en TODAS las pantallas)
El contenedor de contenido de cada pantalla se centra con tope, replicando el `.content` del mockup:
`X = If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` ·
`Width = Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)`.
Las guías lo referencian como "receta 7.13"; si una pantalla estira a todo el ancho, lo declara.

### 7.14 Notificaciones
Éxito: `Notify("✓ …", NotificationType.Success, 2500)`. Error de validación:
`Notify("…", NotificationType.Warning)`. Los errores duros los muestra el banner nativo (sin App.OnError, §4).

### 7.15 Fechas de carga y zona horaria (00 §4.4)
```powerfx
// Corte = 00:00 UTC del día de la última carga ('Última carga' es UserLocal y el import la graba
// a medianoche UTC; nfUltimaCarga es DateOnly). Lado derecho constante → la comparación delega.
Set(varCorteRec, With({d0: DateTime(Year(nfUltimaCarga), Month(nfUltimaCarga), Day(nfUltimaCarga), 0, 0, 0)},
    DateAdd(d0, -TimeZoneOffset(d0), TimeUnit.Minutes)));
// ... Filter(..., 'Última carga' < varCorteRec)

// Mostrar la fecha de carga (Text de una etiqueta del template). El If corta ANTES de TimeZoneOffset:
// en el template vacío ThisItem es Blank y TimeZoneOffset(Blank) es error de RUNTIME.
If(IsBlank(ThisItem.'Última carga'), "—",
    Text(DateAdd(ThisItem.'Última carga', TimeZoneOffset(ThisItem.'Última carga'), TimeUnit.Minutes), "dd/mm"))
```
Lo usan scrReconciliacion (`varCorteRec`) y scrInicio (`varCorteIni`, KPI de desaparecidas).

### 7.16 Conteos y totales: `CountIf`/`Sum` directo al datasource (00 §4.2)
```powerfx
// KPIs = conteos EXACTOS (hasta 50 000): CountIf delega. NO colección + CountRows (se trunca).
Concurrent(
    Set(varNPendIni,
        CountIf('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Pendiente)),
    Set(varNTscIni,
        CountIf('CM | TBL | Interaccions',
            '[IN] Estado grabacion' = 'Choice | CM | Estado grabacion Interaccio'.Grabado
            && '[IN] Fecha grabacion' >= Today()
            && '[IN] Grabado por'.User = gblUsuarioDV.User))     // lookup comparado por PK
);
// Total exacto de una tabla: CountIf(t, true) — CountRows(t) sin filtro da un valor CACHEADO.
Set(varNCtasMon, CountIf('CM | TBL | Cuentas', true));
// Montos: Sum delega para columnas numéricas.
Set(varCobNioIni,
    Sum(Filter('CM | TBL | Pagos', 'Fecha del pago' >= Today() &&
        '[PG] Moneda' = 'Choice | CM | Monedas en Pago'.NIO), '[PG] Monto'));
// "Sin vínculo": IsBlank sobre un LOOKUP no delega → columna de texto equivalente.
CountIf('CM | TBL | Agentes', '[AG] Activo' && 'Email Usuario' = Blank())
```
- Mismas condiciones que el `Filter` de la lista de la pantalla destino (si no, el KPI y la lista
  no cuadran).
- Si la lista puede quedar truncada, mostrar el tope: `Visible = varNDesapRec > CountRows(colDesapRec)`
  ("Mostrando las primeras N…", `lblTopeRec` de la guía 09).
- Contadores de lo que la galería MUESTRA (p.ej. "12 en cola" tras buscar): `gal.AllItemsCount`.

### 7.17 Mutaciones masivas sin `ForAll` + `Patch` por fila
```powerfx
// UN Patch con una tabla de registros que llevan la PK: Dataverse los actualiza por PK.
Patch('CM | TBL | Asignacions',
    ForAll(colDesapRec As D,
        {'CM | TBL | Asignacion': D.'CM | TBL | Asignacion',
         '[AS] Estado': false, '[AS] Motivo': "fuera de cartera", 'Fecha de cierre': Now()}));
```
Prohibido mutar colecciones dentro de `ForAll` (`ForAll(..., Collect(col, ...))`): se arma la tabla
con `ForAll` y se carga de una (`ClearCollect(col, ForAll(...))`). Leer por posición: `Index(t, n)`.

## 8. Componentes locales (crear ANTES que las pantallas, en este orden)

> Todos con **Acceder al ámbito de la aplicación = Activado** (leen `gblC/gblE/nfMovil/nfNav` y navegan).
> Contenedores manuales adentro, mismas convenciones de nombres con sufijo `Cmp`.

### 8.1 `cmpNavLateral` — navegación por rol (LA pieza de control de acceso visual)
- Tamaño: `Width = gblE.AnchoNav` · `Height = App.Height` (la instancia lo fija por pantalla).
- Custom properties (Entrada): `RutaActiva` (Texto) — la pantalla le pasa su ruta (`"trx"`, `"aprb"`…).
- Estructura: `conRaizNavCmp` (Fill `gblC.teal`) → marca (logo ₡ + "Cobranza Móvil / BACKOFFICE") →
  `galNavCmp` (`Galería vertical en blanco`):
  - `Items = Sort(Filter(nfNav, CountRows(Filter(Split(Roles, "|"), Value in gblRol)) > 0), Orden)`
    ← el filtro de ROL vive acá (multi-rol: el ítem se muestra si ALGUNO de sus roles está en
    gblRol): un rol jamás VE navegación ajena. Todo en memoria (nfNav + Split), cero delegación.
    (`Sort` con la columna como identificador, no `SortByColumns(..., "Orden")` — §11.)
  - `AccessibleLabel = "Menú de navegación"`.
  - Template: `lblIconoNavCmp` + `lblTituloNavCmp` (sin `OnSelect`) — **sin badge de conteo** (LED-17);
    fila activa `TemplateFill = If(ThisItem.Ruta = cmpNavLateral.RutaActiva, gblC.blancoTr, Color.Transparent)`.
    Último hijo: overlay accesible `btnFilaNavCmp` (receta 7.3) con `Text = ThisItem.Titulo`,
    `HoverFill = RGBA(255,255,255,0.08)`, `PressedFill = RGBA(255,255,255,0.14)`,
    `FocusedBorderColor = Color.White`, `FocusedBorderThickness = 2`, `TabIndex = 0`.
  - `OnSelect = Switch(ThisItem.Ruta,
      "ini", Navigate(scrInicio), "trx", Navigate(scrTranscripcion), "tsc", Navigate(scrTranscritas),
      "nva", Navigate(scrNuevaSolicitud), "sol", Navigate(scrMisSolicitudes), "aprb", Navigate(scrAprobaciones),
      "mon", Navigate(scrMonitoreo), "rec", Navigate(scrReconciliacion), "cie", Navigate(scrCierres),
      "age", Navigate(scrAgentes), "mae", Navigate(scrMaestros)); Set(gblNavMovilAbierta, false)`
  - Pie (`conPieNavCmp`): avatar (iniciales de `User().FullName`) + nombre + rol
    (`If(IsBlank(gblRol), "Sin acceso", Substitute(gblRol, "|", " · "))`).
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
  `OnSelect = Set(gblNavMovilAbierta, true)`, `AccessibleLabel = "Abrir menú"`, `TabIndex = 0`,
  `FocusedBorderColor = Color.White`, `FocusedBorderThickness = 2`) + título corto blanco + `lblAvatarEncCmp` a la derecha
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
- [ ] Sin rol `SR | CM | *` ni team `CM · …`: scrInicio muestra el estado "sin acceso" (guía 02) y
      la nav solo ofrece Inicio (sin guard por pantalla — LED-20; los datos los protege Dataverse).
- [ ] Studio no muestra NINGÚN subrayado azul de delegación (esa es la vara: cero warnings).
- [ ] La app ABRE en Studio sin errores PA2108 (propiedades inválidas, §11) — `pac canvas pack` no basta.
- [ ] `msapp/Properties.json` del export: `DefaultConnectedDataSourceMaxGetRowsCount = 2000` (§1).
- [ ] Teclado: Tab recorre UNA parada por fila de galería, con foco visible (LED-19).
- [ ] `App.ActiveScreen.Width < 920` (ventana angosta): nav colapsa a hamburguesa en todas las pantallas.
- [ ] Monitor: abrir scrTranscripcion genera ≤ 3 llamadas a Dataverse; marcar una transcripción = 1.

## 11. Lecciones de la validación en Studio (2026-10-02)

Lo que el diseño original asumía y Studio desmintió al construir/abrir la app. Toda guía 02–12 que
contradiga esta sección está desactualizada: **manda esta sección y el código en `apps/backoffice-canvas/Src/`**.

### 11.1 Power Fx moderno (errores reales)

| # | Lo que fallaba | Forma correcta |
|---|---|---|
| 1 | `ClearCollect(col, ShowColumns('CM \| TBL \| Cuentas', ...))` → la colección **pierde los display names**; un `LookUp` posterior falla con *"Name isn't valid"* | No proyectar tablas Dataverse a colecciones de catálogo; navegar el lookup (receta 7.7) |
| 2 | Nombres de columna como **strings** en `GroupBy`/`ShowColumns`/`RenameColumns`/`AddColumns` (`"agenteTrx"`) | **Identificadores** sin comillas: `GroupBy(colPendTrx, agenteTrx, grpTrx)` |
| 3 | `SortByColumns(colección, "col")` no reconoce display names ni columnas creadas con `AddColumns` | `Sort(t, col, SortOrder.Ascending)` |
| 4 | Comparar lookup contra registro (`'[IN] Grabado por' = gblUsuarioDV`) → error *"Record, Record"* | Comparar por PK: `'[IN] Grabado por'.User = gblUsuarioDV.User` · `'[CT] Cliente'.'CM \| TBL \| Cliente' = varCliMae.'CM \| TBL \| Cliente'` |
| 5 | `Navigate(...)` en `OnVisible` → *"would automatically always navigate away"* | Guard de rol eliminado (LED-20) |
| 6 | `Parent.TemplateSize` dentro del template | `Parent.TemplateHeight` (`TemplateSize` es propiedad de la galería, no se lee desde el hijo) |
| 7 | `AddColumns` que agrega una columna que ya existe (p.ej. re-hornear sobre una colección ya horneada) → *"column already exists"* | Hornear UNA vez desde la base sin esa columna (`colTscTmpTsc` → `AddColumns` → `colTscTsc`, guía 04) |
| 8 | Navegar `Team → 'Security Roles'` dentro de `ForAll` → *"not accessible in this context"* | Perfil derivado del nombre del team (00 §3.1, §4) |
| 9 | `ComboBox` clásico dentro de galería: `DefaultSelectedItems` no siempre pinta la selección | Mostrar el vínculo actual en `InputTextPlaceholder` desde la columna de texto (`ThisItem.'Email Usuario'`, guía 11) |
| 10 | `DisplayFields`/`SearchFields` del combo con display names | Nombres **lógicos**: `["sanic_cm_ag_nombre"]`, `["fullname","internalemailaddress"]` |

### 11.2 Propiedades inválidas que impiden ABRIR la app (PA2108)

| Control (versión) | Propiedad que NO existe | Reemplazo |
|---|---|---|
| `Classic/Button@2.2.0` | `AccessibleLabel` | El `Text` es lo que anuncia el lector (overlay 7.3: `Text` descriptivo transparente) |
| `Rectangle@2.3.0` | `RadiusTopLeft/TopRight/BottomLeft/BottomRight` | Redondear con un `Contenedor` (§6) |
| `Classic/CheckBox@2.1.0` | `Wrap` | `Height`/`Width` explícitos (receta 7.10) |

- **`pac canvas pack` NO detecta esto** (empaqueta sin error); **Studio sí**, y la app no abre.
  Vara de cierre: abrir en Studio, no "empaquetó".
- **Fuente de verdad de propiedades por control/versión**: los XML oficiales de los controles que
  vienen dentro de las soluciones exportadas, no la memoria ni la doc genérica del control — la doc describe la última versión, no la que usa la app.

### 11.3 Lo que se edita en Studio por automatización NO necesariamente persiste

- Editar **App.Formulas** y **Settings** (data row limit) en Studio por automatización **no
  persistió en el export**. Regla: esos dos se fijan en el FUENTE (`Src/App.pa.yaml` y
  `msapp/Properties.json` del `.msapr`) y **se verifican SIEMPRE en el export** después de guardar —
  nunca se da por aplicado algo porque Studio lo mostró.

### 11.4 Delegación, zona horaria y performance

Ver 00 §4.2–§4.4 y §5 (corrección de la regla de `CountIf`/`Sum`, `IsBlank` sobre lookup, corte UTC)
y las recetas 7.15–7.17.
