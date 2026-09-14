# Guía 12 — scrMaestros

Datos maestros (solo **Admin**): buscar clientes contra Dataverse, ver su ficha completa
(teléfonos, direcciones, cuentas en mora), crear clientes nuevos y agregar teléfonos/direcciones.
Alcance acotado por **LED-08** y **LED-13** (00 §2): se crea cliente, se editan sus datos básicos y
se AGREGAN tel/dir; las **cuentas NO se editan** (el import es su dueño) y editar/borrar tel-dir
existentes es backlog.

**Contrato de entrada**: `gblRol`, `gblC`, `gblE`, `gblUsuarioDV` seteados por `App.OnStart`
(guía 01 §4). `nfMovil` y `Moneda()` de `App.Formulas` (01 §3). `gblNavMovilAbierta` (estado del
drawer móvil — la setea cmpEncabezado/cmpNavLateral, 01 §8). Esta pantalla no recibe variables
de otras pantallas: todo su estado (`varCliMae`, `varModoMae`, `varPanelMae`, `varNuevoCliMae`,
`colTelMae`, `colDirMae`, `colCtasMae`) nace acá.

> **Por qué acá NO hay colección de clientes** (divergencia de patrón vs receta 7.7): `cliente`
> tiene ~7 000 filas — coleccionarla rompería la cota de 2 000 (00 §4.1). La búsqueda de esta
> pantalla es **la ÚNICA directa al datasource** del proyecto (receta 7.9): `StartsWith` es
> delegable y la galería pagina sola. Las colecciones de esta pantalla son solo las del cliente
> SELECCIONADO (tel/dir/cuentas: unidades por cliente).

## Árbol de controles

```
scrMaestros                          (ESTILO-Pantalla)
├── cmpEncMae                        cmpEncabezado (receta 7.5)
├── conToolbarMae                    toolbar de búsqueda (receta 7.6)
│   ├── txtBuscarMae
│   ├── icoBuscarMae
│   └── btnNuevoMae
├── galClientesMae                   resultados — delegable (receta 7.9)
│   ├── recFilaMae                   overlay clicable (receta 7.3)
│   ├── lblNomFilaMae
│   ├── lblSubFilaMae
│   ├── lblTrabFilaMae
│   └── recSepMae                    separador (receta 7.11)
├── cmpVacioMae                      estado vacío (receta 7.12)
├── recScrimMae                      panel lateral (receta 7.4)
├── conPanelMae                      panel — UN árbol, TRES modos por varModoMae
│   ├── lblTituloPanelMae
│   ├── icoCerrarPanelMae
│   │   ── bloque MODO VER ──
│   ├── lblKCifMae ─ lblVCifMae
│   ├── lblKDeptoMae ─ lblVDeptoMae
│   ├── lblKTrabMae ─ lblVTrabMae
│   ├── lblSecTelMae
│   ├── galTelMae
│   │   └── lblTelFilaMae
│   ├── lblTelVacioMae
│   ├── lblSecDirMae
│   ├── galDirMae
│   │   └── lblDirFilaMae
│   ├── lblDirVacioMae
│   ├── lblSecCtasMae
│   ├── galCtasMae
│   │   └── lblCtaFilaMae
│   ├── lblCtasVacioMae
│   ├── btnCerrarPanelMae
│   ├── btnEditarMae
│   │   ── bloque MODO EDITAR ──
│   ├── lblEdNombreMae ─ txtNombreMae
│   ├── lblEdDeptoMae ─ txtDeptoMae
│   ├── lblEdDistritoMae ─ txtDistritoMae
│   ├── lblEdTrabajoMae ─ txtTrabajoMae
│   ├── btnGuardarEdMae
│   ├── lblSecAddTelMae
│   ├── txtNuevoTelMae ─ cmbTipoTelMae ─ btnAddTelMae
│   ├── lblSecAddDirMae
│   ├── txtNuevaDirMae ─ cmbTipoDirMae ─ btnAddDirMae
│   ├── btnVolverEdMae
│   │   ── bloque MODO CREAR ──
│   ├── lblNvCifMae ─ txtCifNvoMae
│   ├── lblNvNombreMae ─ txtNombreNvoMae
│   ├── lblNvDeptoMae ─ txtDeptoNvoMae
│   ├── lblNvDistritoMae ─ txtDistritoNvoMae
│   ├── lblNvTrabajoMae ─ txtTrabajoNvoMae
│   ├── lblNvTelCelMae ─ txtTelCelNvoMae
│   ├── lblNvTelCasMae ─ txtTelCasNvoMae
│   ├── lblNvDirCasaMae ─ txtDirCasaNvoMae
│   ├── lblNvDirTrabMae ─ txtDirTrabNvoMae
│   ├── lblHintCifMae
│   ├── btnCancelarNvoMae
│   └── btnCrearMae
└── cmpNavMae                        cmpNavLateral (01 §8.1 — al FINAL del árbol)
```

Regla de coordenadas de esta guía: dentro de `conPanelMae` todo es relativo al panel
(`Parent` = `conPanelMae`); dentro de un template de galería, X template = X del control − X de la
galería. Ancho útil del panel: `Parent.Width - 2*gblE.Pad`; para filas dobles ("duo"),
`WDuo = (Parent.Width - 3*gblE.Pad) / 2` y la columna derecha arranca en `2*gblE.Pad + WDuo`.

## 1. Pantalla

| Propiedad | Valor |
|---|---|
| Fill | `gblC.pantalla` (ESTILO-Pantalla) |
| OnVisible | ver abajo — guard PRIMERO |

```powerfx
// Guard de rol (01 §4) — primera línea, sin excepción.
If(!("Admin" in gblRol), Navigate(scrInicio));
// Estado limpio de la pantalla. NO se colecciona nada acá: la búsqueda va directa
// al datasource (receta 7.9) y las colecciones del cliente nacen al clic en la fila.
Set(varPanelMae, false);
Set(varModoMae, "ver");
Reset(txtBuscarMae)
```

**`cmpEncMae`** (componente `cmpEncabezado`, receta 7.5 — posición/tamaño fijados ahí):
`Titulo = "Datos maestros"` · `Subtitulo = "Clientes y cuentas. Solo el Admin crea y edita; el
Solicitante los referencia."`

**`cmpNavMae`** (componente `cmpNavLateral`, instanciación de 01 §8.1 — TODAS sus propiedades
fijadas ahí): `RutaActiva = "mae"` · `Visible = !nfMovil || gblNavMovilAbierta` ·
`X = 0 · Y = 0 · Height = Parent.Height`. Va al final del árbol, después de `conPanelMae`.

## 2. Toolbar de búsqueda (receta 7.6)

| Control | Propiedad | Valor |
|---|---|---|
| `conToolbarMae` (Contenedor) | X | `If(nfMovil, 0, gblE.AnchoNav) + Max(gblE.Pad, (Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - gblE.AnchoMax) / 2)` *(receta 7.13 — Contenido centrado; `galClientesMae` hereda X/Width de acá)* |
| | Y | `cmpEncMae.Y + cmpEncMae.Height` |
| | Width / Height | `Min(gblE.AnchoMax, Parent.Width - If(nfMovil, 0, gblE.AnchoNav) - 2*gblE.Pad)` *(receta 7.13)* / `gblE.AltoInput` |
| | Fill | `Color.Transparent` |
| `txtBuscarMae` (Entrada de texto clásica) | X/Y/W/H | `0` / `0` / `Parent.Width - 170 - gblE.PadSm` / `gblE.AltoInput` — ESTILO-Input |
| | HintText | `"CIF o nombre…"` |
| | PaddingLeft | `34` (deja lugar al ícono) |
| `icoBuscarMae` (Icono, Search) | X/Y/W/H | `8` / `10` / `20` / `20` · Color `gblC.muted` |
| `btnNuevoMae` (Botón clásico) | X/Y/W/H | `Parent.Width - 170` / `0` / `170` / `gblE.AltoInput` — ESTILO-BotonPri |
| | Text | `"＋ Nuevo cliente"` |
| | OnSelect | ver abajo |

```powerfx
// btnNuevoMae.OnSelect — abre el panel en modo crear, con el form limpio.
Set(varCliMae, Blank());
Set(varModoMae, "crear");
Reset(txtCifNvoMae); Reset(txtNombreNvoMae); Reset(txtDeptoNvoMae); Reset(txtDistritoNvoMae);
Reset(txtTrabajoNvoMae); Reset(txtTelCelNvoMae); Reset(txtTelCasNvoMae);
Reset(txtDirCasaNvoMae); Reset(txtDirTrabNvoMae);
Set(varPanelMae, true)
```

## 3. galClientesMae — resultados (Galería vertical en blanco)

| Propiedad | Valor |
|---|---|
| X / Y | `conToolbarMae.X` / `conToolbarMae.Y + conToolbarMae.Height + gblE.PadSm` |
| Width / Height | `conToolbarMae.Width` / `Parent.Height - Self.Y - gblE.Pad` |
| TemplateSize | `gblE.AltoFila` |
| TemplateFill | `If(ThisItem.IsSelected && varPanelMae, gblC.tealSuave, gblC.card)` |
| Items | ver abajo — **la ÚNICA consulta directa al datasource (receta 7.9)** |
| OnSelect | ver abajo |

```powerfx
// Items — ambas ramas delegables: Sort por columna escalar y StartsWith (00 §4.2).
// La galería pagina sola contra Dataverse; por eso NO hay colección de clientes.
If(
    IsBlank(Trim(txtBuscarMae.Text)),
    Sort('CM | TBL | Clientes', '[CL] Nombre'),
    Filter('CM | TBL | Clientes',
        StartsWith('[CL] CIF', Trim(txtBuscarMae.Text)) ||
        StartsWith('[CL] Nombre', Trim(txtBuscarMae.Text)))
)
```

```powerfx
// OnSelect — carga las colecciones del cliente elegido (los 3 filtros delegables:
// texto exacto por CIF y lookup por registro — 00 §4.2) y abre el panel en modo ver.
Set(varCliMae, ThisItem);
Set(varModoMae, "ver");
Concurrent(
    ClearCollect(colTelMae, Filter('CM | TBL | Telefonos', '[TL] CIF' = ThisItem.'[CL] CIF')),
    ClearCollect(colDirMae, Filter('CM | TBL | Direccions', '[DR] CIF' = ThisItem.'[CL] CIF')),
    ClearCollect(colCtasMae, Filter('CM | TBL | Cuentas', '[CT] Cliente' = ThisItem))
);
Set(varPanelMae, true)
```

Template (receta 7.3: la acción vive en la galería; TODO hijo delega con `Select(Parent)` en su
OnSelect — las cuatro filas de abajo lo llevan aunque no se repita en cada celda):

| Control | Propiedad | Valor |
|---|---|---|
| `recFilaMae` (Rectángulo, overlay — al FONDO del z-order) | X/Y/W/H | `0` / `0` / `Parent.TemplateWidth` / `Parent.TemplateHeight` · Fill `Color.Transparent` · OnSelect `Select(Parent)` |
| `lblNomFilaMae` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `8` / `Parent.TemplateWidth * 0.45` / `22` |
| | Text / FontWeight | `ThisItem.'[CL] Nombre'` / `Bold` |
| `lblSubFilaMae` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `32` / `Parent.TemplateWidth * 0.45` / `20` · Size `9` · Color `gblC.muted` |
| | Text | `ThisItem.'[CL] CIF' & " · " & Coalesce(ThisItem.'[CL] Departamento', "—")` |
| `lblTrabFilaMae` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad + Parent.TemplateWidth * 0.45` / `20` / `Parent.TemplateWidth * 0.5 - 2*gblE.Pad` / `22` · Color `gblC.ink2` |
| | Text | `If(IsBlank(ThisItem.'[CL] Trabajo'), "sin trabajo registrado", ThisItem.'[CL] Trabajo')` *(una línea; si excede el ancho, se trunca — aceptado)* |
| `recSepMae` (Rectángulo, receta 7.11) | X/Y/W/H | `0` / `Parent.TemplateHeight - 1` / `Parent.TemplateWidth` / `1` · Fill `gblC.hairline` |

> **Divergencias declaradas vs mockup**: (1) el mockup muestra un chip "N cuenta(s)" por fila —
> acá NO va: sería un `LookUp`/conteo por fila contra `'CM | TBL | Cuentas'`, un N+1 no delegable
> prohibido por 00 §4.3; el conteo real se ve al abrir la ficha (`galCtasMae`). (2) El mockup
> busca por *contains*; la canvas usa `StartsWith` porque es lo delegable (receta 7.9, 00 §4.2) —
> "GUTIÉRREZ" no aparece tecleando "TIÉRREZ", y eso es aceptado.

**`cmpVacioMae`** (componente `cmpVacio`, receta 7.12): centrado sobre el área de la galería
(`X = galClientesMae.X + (galClientesMae.Width - 320)/2` · `Y = galClientesMae.Y + 60` ·
`Width 320` · `Height 140`) · `Icono = "🔎"` · `Titulo = "Sin resultados"` ·
`Mensaje = If(IsBlank(Trim(txtBuscarMae.Text)), "Todavía no hay clientes cargados.", "Ningún CIF ni nombre empieza con «" & Trim(txtBuscarMae.Text) & "».")` ·
`Visible = CountRows(galClientesMae.AllItems) = 0` *(AllItems es client-side: cuenta la página
cargada — para "¿hay cero resultados?" alcanza y no delega nada).*

## 4. Panel lateral (receta 7.4) — un árbol, tres modos

`recScrimMae` y `conPanelMae` van al final del árbol (encima de todo, antes de `cmpNavMae`),
exactamente como fija la receta 7.4 con sufijo `Mae`:

| Control | Propiedad | Valor |
|---|---|---|
| `recScrimMae` (Rectángulo) | X/Y/W/H | `0` / `0` / `Parent.Width` / `Parent.Height` · Fill `gblC.scrim` |
| | Visible / OnSelect | `varPanelMae` / `Set(varPanelMae, false)` |
| `conPanelMae` (Contenedor, ESTILO-Card con Radius* = 0) | Width | `If(nfMovil, Parent.Width, Min(480, Parent.Width * 0.45))` |
| | X / Y / Height | `Parent.Width - Self.Width` / `0` / `Parent.Height` |
| | Visible | `varPanelMae` |
| `lblTituloPanelMae` (Etiqueta clásica, ESTILO-TituloPagina Size 13) | X/Y/W/H | `gblE.Pad` / `gblE.Pad` / `Parent.Width - 2*gblE.Pad - 32` / `28` |
| | Text | `Switch(varModoMae, "crear", "Nuevo cliente", "editar", "✏️ " & varCliMae.'[CL] Nombre', varCliMae.'[CL] Nombre')` |
| `icoCerrarPanelMae` (Icono, Cancel) | X/Y/W/H | `Parent.Width - gblE.Pad - 24` / `gblE.Pad + 2` / `24` / `24` · Color `gblC.muted` |
| | OnSelect | `Set(varPanelMae, false)` |

Cada sección de los bloques siguientes lleva su `Visible` de modo (`varModoMae = "ver"`,
`"editar"` o `"crear"`); los tres bloques se superponen en Y sin conflicto porque los modos son
mutuamente excluyentes. En las tablas, el `Visible` de modo se lista una vez por bloque y aplica
a TODOS los controles del bloque.

## 5. Bloque MODO VER — `Visible = varModoMae = "ver"` (todos los controles de §5)

Pares KV (`lblK*` Etiqueta clásica ESTILO-SecHead, X `gblE.Pad`, W `120`, H `26`;
`lblV*` Etiqueta clásica, X `gblE.Pad + 120`, W `Parent.Width - 2*gblE.Pad - 120`, H `26`,
Align `Align.Right`):

| K (Text) | V (Text) | Y |
|---|---|---|
| `"CIF"` | `varCliMae.'[CL] CIF'` | `60` |
| `"DEPARTAMENTO"` | `Coalesce(varCliMae.'[CL] Departamento', "—") & " · " & Coalesce(varCliMae.'[CL] Distrito', "—")` | `86` |
| `"TRABAJO"` | `Coalesce(varCliMae.'[CL] Trabajo', "—")` | `112` |

Controles: fila 1 = `lblKCifMae`/`lblVCifMae`, fila 2 = `lblKDeptoMae`/`lblVDeptoMae`,
fila 3 = `lblKTrabMae`/`lblVTrabMae`.

Encabezados de sección — Etiquetas clásicas ESTILO-SecHead, X `gblE.Pad`,
W `Parent.Width - 2*gblE.Pad`, H `18`: `lblSecTelMae` `"TELÉFONOS"` Y `148` ·
`lblSecDirMae` `"DIRECCIONES"` Y `266` · `lblSecCtasMae` `"CUENTAS (" & CountRows(colCtasMae) & ")"` Y `384`.

Galerías de la ficha — las tres son `Galería vertical en blanco`, X `gblE.Pad`,
W `Parent.Width - 2*gblE.Pad`, TemplateSize `28`, con UNA Etiqueta clásica de template
(X `0` · Y `4` · W `Parent.TemplateWidth` · H `20` · Size `9`):

| Galería | Y / Height | Items | Etiqueta de template · Text |
|---|---|---|---|
| `galTelMae` | `170` / `84` | `colTelMae` | `lblTelFilaMae` · `"📞 " & ThisItem.'[TL] Numero' & If(IsBlank(ThisItem.'[TL] Tipo'), "", " (" & ThisItem.'[TL] Tipo' & ")")` |
| `galDirMae` | `288` / `84` | `colDirMae` | `lblDirFilaMae` · `"📍 " & ThisItem.'[DR] Direccion' & If(IsBlank(ThisItem.'[DR] Tipo'), "", " (" & ThisItem.'[DR] Tipo' & ")")` |
| `galCtasMae` | `406` / `112` | `colCtasMae` | `lblCtaFilaMae` · `ThisItem.'[CT] Numero de cuenta' & " · " & ThisItem.'Tipo Producto' & " · " & Moneda(ThisItem.'[CT] Saldo Vencido NIO', ThisItem.'[CT] Saldo Vencido USD')` |

Estados vacíos de sección — Etiquetas clásicas, misma X/W que su galería, H `24`, Color
`gblC.muted2`: `lblTelVacioMae` Y `170` Text `"—"` Visible `CountRows(colTelMae) = 0` ·
`lblDirVacioMae` Y `288` Text `"—"` Visible `CountRows(colDirMae) = 0` · `lblCtasVacioMae` Y `406`
Text `"Sin cuentas en mora."` Visible `CountRows(colCtasMae) = 0`. *(El Visible de modo del bloque
se les suma en conjunción, igual que a las galerías.)*

Pie del modo ver — Botones clásicos, Y `Parent.Height - gblE.AltoBoton - gblE.Pad`,
W `(Parent.Width - 3*gblE.Pad) / 2`, H `gblE.AltoBoton`:

| Control | X | Estilo · Text | OnSelect |
|---|---|---|---|
| `btnCerrarPanelMae` | `gblE.Pad` | ESTILO-BotonFantasma · `"Cerrar"` | `Set(varPanelMae, false)` |
| `btnEditarMae` | `2*gblE.Pad + Self.Width` | ESTILO-BotonSec · `"✏️ Editar"` | ver abajo |

```powerfx
// btnEditarMae.OnSelect — los inputs de edición tienen Default = varCliMae.*; el Reset
// los re-evalúa. Sin estos Reset, el panel reusado entre clientes muestra el texto viejo.
Set(varModoMae, "editar");
Reset(txtNombreMae); Reset(txtDeptoMae); Reset(txtDistritoMae); Reset(txtTrabajoMae);
Reset(txtNuevoTelMae); Reset(cmbTipoTelMae); Reset(txtNuevaDirMae); Reset(cmbTipoDirMae)
```

> Las cuentas se muestran y NO se tocan (**LED-08**): las crea y actualiza el import diario. Si
> IntelliSense marca `'Tipo Producto'` como choice, envolvelo en `Text(...)` dentro del
> concatenado y anotalo.

## 6. Bloque MODO EDITAR — `Visible = varModoMae = "editar"` (todos los controles de §6)

Solo datos básicos del cliente + AGREGAR tel/dir (**LED-13**). Campos: etiqueta
(Etiqueta clásica ESTILO-Subtitulo, H `16`) encima de su input (Entrada de texto clásica
ESTILO-Input, H `gblE.AltoInput`, `Default` del registro):

| Etiqueta · Text · X/Y/W | Input · Default · X/Y/W |
|---|---|
| `lblEdNombreMae` · `"Nombre completo"` · `gblE.Pad`/`60`/ancho útil | `txtNombreMae` · `varCliMae.'[CL] Nombre'` · `gblE.Pad`/`78`/ancho útil |
| `lblEdDeptoMae` · `"Departamento"` · `gblE.Pad`/`126`/`WDuo` | `txtDeptoMae` · `varCliMae.'[CL] Departamento'` · `gblE.Pad`/`144`/`WDuo` |
| `lblEdDistritoMae` · `"Distrito"` · `2*gblE.Pad + WDuo`/`126`/`WDuo` | `txtDistritoMae` · `varCliMae.'[CL] Distrito'` · `2*gblE.Pad + WDuo`/`144`/`WDuo` |
| `lblEdTrabajoMae` · `"Trabajo"` · `gblE.Pad`/`192`/ancho útil | `txtTrabajoMae` · `varCliMae.'[CL] Trabajo'` · `gblE.Pad`/`210`/ancho útil |

> ⚠ **El CIF NO se edita** — no hay input para él en este modo. Es la clave de negocio del core:
> el import identifica al cliente por CIF (upsert). Cambiarlo a mano desalinearía la fila con el
> core y el próximo import **recrearía el cliente "viejo" y duplicaría todo** (riesgo de colisión
> de CIF de **DEC-013**). Si un CIF está mal, se corrige vía import/soporte, no acá.

| Control | Propiedad | Valor |
|---|---|---|
| `btnGuardarEdMae` (Botón clásico, ESTILO-BotonPri) | X/Y/W/H | `gblE.Pad` / `262` / ancho útil / `gblE.AltoBoton` |
| | Text | `"Guardar cambios"` |
| | DisplayMode | `If(IsBlank(Trim(txtNombreMae.Text)), DisplayMode.Disabled, DisplayMode.Edit)` |
| | OnSelect | ver abajo |

```powerfx
// btnGuardarEdMae.OnSelect — LookUp delegable por texto exacto (00 §4.2).
Patch('CM | TBL | Clientes',
    LookUp('CM | TBL | Clientes', '[CL] CIF' = varCliMae.'[CL] CIF'),
    {
        '[CL] Nombre': Trim(txtNombreMae.Text),
        '[CL] Departamento': Trim(txtDeptoMae.Text),
        '[CL] Distrito': Trim(txtDistritoMae.Text),
        '[CL] Trabajo': Trim(txtTrabajoMae.Text)
    }
);
// Refresca la referencia local para que el modo ver muestre lo recién guardado.
Set(varCliMae, LookUp('CM | TBL | Clientes', '[CL] CIF' = varCliMae.'[CL] CIF'));
Notify("✓ Cliente actualizado", NotificationType.Success, 2500);
Set(varModoMae, "ver")
```

**Agregar teléfono** — `lblSecAddTelMae` (Etiqueta clásica ESTILO-SecHead, X `gblE.Pad`, Y `322`,
W ancho útil, H `18`, Text `"AGREGAR TELÉFONO"`) y una fila a Y `344`, H `gblE.AltoInput`:

| Control | X / W | Propiedades |
|---|---|---|
| `txtNuevoTelMae` (Entrada de texto clásica, ESTILO-Input) | `gblE.Pad` / `Parent.Width - 2*gblE.Pad - 210 - 2*gblE.PadSm` | HintText `"8888-8888"` |
| `cmbTipoTelMae` (Cuadro combinado) | `txtNuevoTelMae.X + txtNuevoTelMae.Width + gblE.PadSm` / `120` | Items `["Celular", "Casa"]` · DefaultSelectedItems `["Celular"]` · SelectMultiple `false` · IsSearchable `false` · Height `gblE.AltoInput` |
| `btnAddTelMae` (Botón clásico, ESTILO-BotonSec) | `cmbTipoTelMae.X + cmbTipoTelMae.Width + gblE.PadSm` / `90` | Text `"＋"` · Height `gblE.AltoInput` · DisplayMode `If(IsBlank(Trim(txtNuevoTelMae.Text)), DisplayMode.Disabled, DisplayMode.Edit)` · OnSelect abajo |

```powerfx
// btnAddTelMae.OnSelect — '[TL] Tipo' es TEXTO libre en el esquema; los valores
// "Celular"/"Casa" calcan lo que escribe el import. El lookup '[TL] Cliente' se resuelve
// con el registro fresco del datasource (LookUp por CIF), no con la copia en memoria.
Patch('CM | TBL | Telefonos', Defaults('CM | TBL | Telefonos'),
    {
        '[TL] Cliente': LookUp('CM | TBL | Clientes', '[CL] CIF' = varCliMae.'[CL] CIF'),
        '[TL] CIF': varCliMae.'[CL] CIF',
        '[TL] Numero': Trim(txtNuevoTelMae.Text),
        '[TL] Tipo': cmbTipoTelMae.Selected.Value
    }
);
ClearCollect(colTelMae, Filter('CM | TBL | Telefonos', '[TL] CIF' = varCliMae.'[CL] CIF'));
Reset(txtNuevoTelMae); Reset(cmbTipoTelMae);
Notify("✓ Teléfono agregado", NotificationType.Success, 2500)
```

**Agregar dirección** — análogo: `lblSecAddDirMae` (mismas propiedades que `lblSecAddTelMae`,
Y `400`, Text `"AGREGAR DIRECCIÓN"`) y la fila a Y `422`: `txtNuevaDirMae` (HintText
`"Del parque 2c al lago…"`), `cmbTipoDirMae` (Items `["Casa", "EC", "Trabajo"]` ·
DefaultSelectedItems `["Casa"]` — resto igual que `cmbTipoTelMae`) y `btnAddDirMae` (igual que
`btnAddTelMae` con DisplayMode sobre `txtNuevaDirMae`); X/W idénticos a la fila de teléfono:

```powerfx
// btnAddDirMae.OnSelect — '[DR] Tipo' TEXTO: "Casa" / "EC" / "Trabajo" (esquema verificado).
Patch('CM | TBL | Direccions', Defaults('CM | TBL | Direccions'),
    {
        '[DR] Cliente': LookUp('CM | TBL | Clientes', '[CL] CIF' = varCliMae.'[CL] CIF'),
        '[DR] CIF': varCliMae.'[CL] CIF',
        '[DR] Direccion': Trim(txtNuevaDirMae.Text),
        '[DR] Tipo': cmbTipoDirMae.Selected.Value
    }
);
ClearCollect(colDirMae, Filter('CM | TBL | Direccions', '[DR] CIF' = varCliMae.'[CL] CIF'));
Reset(txtNuevaDirMae); Reset(cmbTipoDirMae);
Notify("✓ Dirección agregada", NotificationType.Success, 2500)
```

Pie: `btnVolverEdMae` (Botón clásico, ESTILO-BotonFantasma) · X `gblE.Pad` ·
Y `Parent.Height - gblE.AltoBoton - gblE.Pad` · W ancho útil · H `gblE.AltoBoton` ·
Text `"Volver a la ficha"` · OnSelect `Set(varModoMae, "ver")`.

> Editar o borrar teléfonos/direcciones EXISTENTES queda fuera de v1 (**LED-13**) — solo se
> agregan. El mockup insinuaba un "Editar" genérico; esta es la versión acotada y declarada.

## 7. Bloque MODO CREAR — `Visible = varModoMae = "crear"` (todos los controles de §7)

Mismo patrón etiqueta+input que §6 (etiquetas ESTILO-Subtitulo H `16`, inputs ESTILO-Input
H `gblE.AltoInput`, `Default` en blanco — son formulario nuevo):

| Etiqueta · Text | Input · HintText | X / Y etiqueta / Y input / W |
|---|---|---|
| `lblNvCifMae` · `"CIF *"` | `txtCifNvoMae` · `"C-00000"` | `gblE.Pad` / `60` / `78` / `WDuo` |
| `lblNvNombreMae` · `"Nombre completo *"` | `txtNombreNvoMae` · — | `2*gblE.Pad + WDuo` / `60` / `78` / `WDuo` |
| `lblNvDeptoMae` · `"Departamento"` | `txtDeptoNvoMae` · — | `gblE.Pad` / `126` / `144` / `WDuo` |
| `lblNvDistritoMae` · `"Distrito"` | `txtDistritoNvoMae` · — | `2*gblE.Pad + WDuo` / `126` / `144` / `WDuo` |
| `lblNvTrabajoMae` · `"Trabajo"` | `txtTrabajoNvoMae` · — | `gblE.Pad` / `192` / `210` / ancho útil |
| `lblNvTelCelMae` · `"Teléfono celular"` | `txtTelCelNvoMae` · `"8888-8888"` | `gblE.Pad` / `258` / `276` / `WDuo` |
| `lblNvTelCasMae` · `"Teléfono casa"` | `txtTelCasNvoMae` · — | `2*gblE.Pad + WDuo` / `258` / `276` / `WDuo` |
| `lblNvDirCasaMae` · `"Dirección casa"` | `txtDirCasaNvoMae` · — | `gblE.Pad` / `324` / `342` / ancho útil |
| `lblNvDirTrabMae` · `"Dirección trabajo"` | `txtDirTrabNvoMae` · — | `gblE.Pad` / `390` / `408` / ancho útil |

| Control | Propiedad | Valor |
|---|---|---|
| `lblHintCifMae` (Etiqueta clásica) | X/Y/W/H | `gblE.Pad` / `456` / ancho útil / `40` · Wrap `true` · Size `9` · Color `gblC.pendInk` |
| | Text | `"⚠ El CIF debe ser el del core. Un CIF equivocado duplica el cliente en el próximo import."` |
| `btnCancelarNvoMae` (Botón clásico, ESTILO-BotonFantasma) | X/Y/W/H | `gblE.Pad` / `Parent.Height - gblE.AltoBoton - gblE.Pad` / `(Parent.Width - 3*gblE.Pad) / 2` / `gblE.AltoBoton` |
| | Text / OnSelect | `"Cancelar"` / `Set(varPanelMae, false)` |
| `btnCrearMae` (Botón clásico, ESTILO-BotonPri) | X/Y/W/H | `2*gblE.Pad + Self.Width` *(misma W/Y/H que el anterior)* |
| | Text | `"＋ Crear cliente"` |
| | DisplayMode | `If(IsBlank(Trim(txtCifNvoMae.Text)) || IsBlank(Trim(txtNombreNvoMae.Text)), DisplayMode.Disabled, DisplayMode.Edit)` |
| | OnSelect | ver abajo |

```powerfx
// btnCrearMae.OnSelect — anti-duplicado primero (LookUp delegable por CIF; el Trim va
// sobre el argumento local, no sobre la columna — 00 §4.2). Si el CIF ya existe, el CSV
// lo fusionaría por upsert (DEC-013): acá NO se crea y se manda al Admin a buscarlo.
If(
    !IsBlank(LookUp('CM | TBL | Clientes', '[CL] CIF' = Trim(txtCifNvoMae.Text))),
    Notify("Ya existe un cliente con CIF " & Trim(txtCifNvoMae.Text) &
           " — buscalo y editalo, no lo dupliques.", NotificationType.Warning),

    // 1) Crear el cliente y quedarse con el registro creado (para los lookups hijos).
    Set(varNuevoCliMae, Patch('CM | TBL | Clientes', Defaults('CM | TBL | Clientes'),
        {
            '[CL] CIF': Trim(txtCifNvoMae.Text),
            '[CL] Nombre': Trim(txtNombreNvoMae.Text),
            '[CL] Departamento': Trim(txtDeptoNvoMae.Text),
            '[CL] Distrito': Trim(txtDistritoNvoMae.Text),
            '[CL] Trabajo': Trim(txtTrabajoNvoMae.Text)
        }
    ));
    // 2) Hijos opcionales: un Patch por cada tel/dir NO vacío, con lookup + CIF denormalizado.
    If(!IsBlank(Trim(txtTelCelNvoMae.Text)),
        Patch('CM | TBL | Telefonos', Defaults('CM | TBL | Telefonos'),
            {'[TL] Cliente': varNuevoCliMae, '[TL] CIF': varNuevoCliMae.'[CL] CIF',
             '[TL] Numero': Trim(txtTelCelNvoMae.Text), '[TL] Tipo': "Celular"}));
    If(!IsBlank(Trim(txtTelCasNvoMae.Text)),
        Patch('CM | TBL | Telefonos', Defaults('CM | TBL | Telefonos'),
            {'[TL] Cliente': varNuevoCliMae, '[TL] CIF': varNuevoCliMae.'[CL] CIF',
             '[TL] Numero': Trim(txtTelCasNvoMae.Text), '[TL] Tipo': "Casa"}));
    If(!IsBlank(Trim(txtDirCasaNvoMae.Text)),
        Patch('CM | TBL | Direccions', Defaults('CM | TBL | Direccions'),
            {'[DR] Cliente': varNuevoCliMae, '[DR] CIF': varNuevoCliMae.'[CL] CIF',
             '[DR] Direccion': Trim(txtDirCasaNvoMae.Text), '[DR] Tipo': "Casa"}));
    If(!IsBlank(Trim(txtDirTrabNvoMae.Text)),
        Patch('CM | TBL | Direccions', Defaults('CM | TBL | Direccions'),
            {'[DR] Cliente': varNuevoCliMae, '[DR] CIF': varNuevoCliMae.'[CL] CIF',
             '[DR] Direccion': Trim(txtDirTrabNvoMae.Text), '[DR] Tipo': "Trabajo"}));
    // 3) Cerrar limpio.
    Notify("✓ Cliente " & varNuevoCliMae.'[CL] Nombre' & " creado", NotificationType.Success, 2500);
    Reset(txtCifNvoMae); Reset(txtNombreNvoMae); Reset(txtDeptoNvoMae); Reset(txtDistritoNvoMae);
    Reset(txtTrabajoNvoMae); Reset(txtTelCelNvoMae); Reset(txtTelCasNvoMae);
    Reset(txtDirCasaNvoMae); Reset(txtDirTrabNvoMae);
    Set(varPanelMae, false)
)
```

> El cliente recién creado aparece en `galClientesMae` al buscarlo (la galería consulta el
> datasource en vivo). Los toasts del mockup son `Notify` (**LED-05**).

## 8. Checklist de salida

- [ ] Con rol ≠ Admin, entrar a scrMaestros redirige a scrInicio sin parpadeo de datos.
- [ ] Búsqueda vacía lista clientes ordenados por nombre y la galería pagina al hacer scroll
      (más de 100 filas cargan solas); **cero banda azul de delegación** en `Items`.
- [ ] Teclear un CIF (`C-…`) o el inicio de un nombre filtra en vivo; texto sin coincidencias
      muestra `cmpVacioMae` con el término buscado.
- [ ] Clic en una fila abre el panel en modo ver con KV, teléfonos, direcciones y cuentas del
      cliente (secciones vacías muestran su "—" / "Sin cuentas en mora.").
- [ ] Las cuentas del panel NO tienen ningún control de edición (LED-08).
- [ ] "✏️ Editar" precarga los 4 campos; no existe input de CIF; guardar con nombre vacío está
      deshabilitado; guardar actualiza y vuelve a la ficha con los datos nuevos.
- [ ] Agregar teléfono y dirección: el nuevo ítem aparece en su galería del modo ver al volver.
- [ ] "＋ Nuevo cliente" abre el form limpio; "Crear" deshabilitado hasta tener CIF y nombre;
      con un CIF existente avisa (warning) y NO crea.
- [ ] Crear con tel/dir llenos genera cliente + hijos (verificar en la tabla que `[TL]/[DR] CIF`
      y el lookup Cliente quedaron seteados).
- [ ] Scrim e ícono ✕ cierran el panel en los tres modos.
- [ ] En ventana < 920 px: nav colapsa a hamburguesa y el panel ocupa todo el ancho.
