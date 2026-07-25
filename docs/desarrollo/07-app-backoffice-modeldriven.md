# App de Oficina (Backoffice) — Diseño macro (Model-driven)

> **Qué es.** La **segunda app** de la solución: una **model-driven** app, **una sola**, diferenciada por
> **roles** (DEC-014). Vive en la oficina (escritorio, con conexión) y es **data-centric**: revisar lo que el
> localizador capturó, **transcribir al core** y gestionar el ciclo de vida de las asignaciones.
>
> **Por qué model-driven (no canvas):** el trabajo de oficina es **sobre datos** (listas, formularios,
> auditoría, roles), online, en pantalla grande. Model-driven te da **gratis**: vistas filtradas, formularios,
> subgrids, búsqueda, flujos de proceso (BPF), reglas de negocio y **seguridad por rol**. La canvas brilla en
> campo/offline; la model-driven brilla acá. (DEC-014: 2 apps confirmado.)
>
> **Cómo se "construye":** se **configura** (tablas → formularios → vistas → BPF → reglas → roles → páginas
> personalizadas), no se dibuja control por control. Este doc es el **plano**; después drilleamos cada pieza.

---

## 1. Roles y segregación de funciones (DEC-014)

Una app, cuatro roles. Cada uno ve **solo lo suyo** vía **roles de seguridad de Dataverse** + **site map por rol**.

| Rol | Qué hace | Tablas que toca | Escribe |
|-----|----------|-----------------|---------|
| **SR | CM | Backoffice** | Revisa interacciones del día y las **transcribe al core**: asigna `sanic_cm_in_codigograbacion`, marca `Grabada en core`. | `sanic_cm_tbl_interaccion` (lee), `sanic_cm_tbl_pago` (lee), cuenta/cliente (lee) | `sanic_cm_in_codigograbacion`, `sanic_cm_in_estadograbacion`, `sanic_cm_in_fechagrabacion`, `sanic_cm_in_grabadopor` |
| **SR | CM | Solicitante** | Crea **asignaciones Especiales** (encargos) → nacen en `Pendiente`. Carga datos maestros si faltan (DEC-013). | `sanic_cm_tbl_asignacion` (crea), cliente/cuenta (puede crear) | nueva asignación Especial + maestros |
| **SR | CM | Supervisor** | **Aprueba / rechaza** las Especiales pendientes; reasigna localizador. | `sanic_cm_tbl_asignacion` | `sanic_cm_as_estadoaprobacion`, `sanic_cm_as_resolutorid`, `sanic_cm_as_agenteid`, `sanic_cm_as_motivo` |
| **SR | CM | Admin** | **Reconciliación** de Diarias desaparecidas (DEC-018), **cierre** de Incobrables/Especiales (DEC-020), liga usuarios↔agentes, monitorea el import. | todas | `sanic_cm_as_estado`, `⚠️TODO(sanic_motivocierre)`, links `sanic_cm_ag_usuarioid`, ejecuta funciones |

> **Principio:** **segregación de funciones.** El que captura (localizador) **no** transcribe; el que crea una
> Especial (Solicitante) **no** la aprueba (Supervisor); el que aprueba **no** la cierra (Admin). Cada estado lo
> cambia un rol distinto → trazabilidad y control.

---

## 2. Mapa del sitio (site map / áreas)

La app tiene **áreas** (grupos del menú). Cada rol ve las áreas que su rol de seguridad le habilita.

```
App de Oficina — Cobranza Móvil
│
├── 📥 COBRANZA  (Backoffice)
│   ├── Por transcribir        → vista: interacciones Pendientes (mi cola del día)
│   ├── Transcritas hoy        → vista: lo que ya grabé hoy
│   ├── Interacciones          → todas (búsqueda/auditoría)
│   ├── Pagos                  → todos los pagos (conciliación con recibos)
│   └── Cuentas / Clientes     → consulta (solo lectura)
│
├── ⭐ ESPECIALES
│   ├── Nueva solicitud        → (Solicitante) crear Especial
│   ├── Mis solicitudes        → (Solicitante) seguimiento
│   └── Aprobaciones pendientes→ (Supervisor) cola de aprobación
│
├── 🛠️ ADMINISTRACIÓN  (Admin)
│   ├── Reconciliación diaria  → página: sweep de Diarias desaparecidas (DEC-018)
│   ├── Cierres                → vistas de Incobrables/Especiales para cerrar
│   ├── Asignaciones           → todas (gestión)
│   ├── Agentes y usuarios     → ligar systemuser ↔ sanic_cm_tbl_agente
│   └── Cargas (import)        → monitoreo de `sanic_cm_tbl_cargadiaria` / última carga
│
└── 📊 TABLEROS
    ├── Cobranza del día       → KPIs: pendientes, transcritas, cobrado por moneda
    └── Especiales             → pendientes de aprobación, SLA
```

> Cada **subárea** apunta a una **vista** o a una **página personalizada** (las de Admin con lógica ejecutable).

---

## 3. La tarea central — TRANSCRIPCIÓN (write-back al core) 🎯

Es el 80% del día del backoffice. El flujo (DEC-002):

```
Localizador captura (campo)  →  sanic_cm_tbl_interaccion  sanic_cm_in_estadograbacion = "Pendiente"
                                          │
Backoffice abre la cola "Por transcribir"
   1. Lee: comentario, cuenta/cliente, pago(s) (subgrid), geo
   2. Graba la gestión en el CORE del banco (sistema externo, a mano)
   3. Vuelve a la app y asigna el CÓDIGO con que la grabó  → sanic_cm_in_codigograbacion
   4. Marca "Grabada en core"  → sanic_cm_in_estadograbacion = "Grabada en core"
                                          │
   (auto) sanic_cm_in_fechagrabacion = ahora · sanic_cm_in_grabadopor = el backoffice actual
```

**Diseño de la pantalla (formulario principal de `sanic_cm_tbl_interaccion`):**

- **Sección "Gestión" (solo lectura):** cliente, cuenta, comentario, fecha de interacción, agente (localizador),
  geo (📍 con link a mapa).
- **Subgrid "Pagos":** los `sanic_cm_tbl_pago` de la interacción (monto, moneda, nº de recibo) — para que el backoffice
  los grabe en el core.
- **Sección "Transcripción al core" (editable):**
  - `sanic_cm_in_codigograbacion` (Texto) — **el código que el backoffice asignó.**
  - `sanic_cm_in_estadograbacion` (Choice) — Pendiente → **Grabada en core**.
  - `sanic_cm_in_fechagrabacion` (auto) · `sanic_cm_in_grabadopor` (auto) — **auditoría**.

**Anti-doble-carga (clave — DEC-002):**
1. **Regla de negocio / flujo:** al pasar `sanic_cm_in_estadograbacion` a *Grabada en core*, **estampar** automáticamente
   `sanic_cm_in_fechagrabacion = Now()` y `sanic_cm_in_grabadopor = usuario actual`. El backoffice **no** los tipea.
2. **Bloqueo:** una vez *Grabada en core*, la sección de transcripción queda **de solo lectura** (no se re-graba).
3. La cola **"Por transcribir"** filtra `sanic_cm_in_estadograbacion = Pendiente` → lo grabado **desaparece** de la cola.

**Decisión de UX (te la propongo, ver §9):** la transcripción puede ser (A) **formulario nativo** desde la vista,
(B) un **BPF** que guía los pasos, o (C) una **página personalizada tipo cola** (siguiente/siguiente). Mi
recomendación abajo.

---

## 4. Formularios principales (qué campos, en qué secciones)

| Tabla | Formulario | Secciones |
|-------|-----------|-----------|
| `sanic_cm_tbl_interaccion` | **Transcripción** | Gestión (RO) · Pagos (subgrid) · Transcripción al core (edit) · Auditoría (RO) |
| `sanic_cm_tbl_asignacion` | **Asignación** | Datos (cuenta, tipo, agente, estado) · Especial (estadoaprobacion, resolutor, creada por, motivos) · Ciclo (fechas, última carga, motivo cierre) |
| `sanic_cm_tbl_pago` | **Pago** (RO mayormente) | Monto, moneda, recibo, interacción, fecha |
| `sanic_cm_tbl_cliente` / `sanic_cm_tbl_cuenta` | **Consulta** | Datos maestros (RO para backoffice; editable para Solicitante/Admin según rol) |

---

## 5. Vistas (listas filtradas) — las que mueven el día

| Área | Vista | Filtro | Columnas clave |
|------|-------|--------|----------------|
| Cobranza | **Por transcribir** | `sanic_cm_in_estadograbacion = Pendiente` | fecha, cliente, cuenta, **Localizador** (`sanic_cm_in_agente`), ¿pago?, comentario |
| Cobranza | **Transcritas hoy** | `sanic_cm_in_estadograbacion = Grabada` **y** `sanic_cm_in_fechagrabacion = hoy` **y** grabada por mí | fecha, cuenta, **Localizador**, **código core**, hora |
| Pagos | **Pagos del día** | `⚠️TODO(sanic_fechapago) = hoy` | cuenta, monto, moneda, recibo, interacción |
| Especiales | **Aprobaciones pendientes** | `sanic_cm_as_tipo = Especial` **y** `sanic_cm_as_estadoaprobacion = Pendiente` | cliente, cuenta, creada por, fecha |
| Admin | **Incobrables abiertas** | `sanic_cm_as_tipo = Incobrable` **y** `sanic_cm_as_estado = Activa` | cuenta, días mora, última carga |
| Admin | **Diarias “desaparecidas”** | `sanic_cm_as_tipo = Diaria` **y** `sanic_cm_as_estado = Activa` **y** `⚠️TODO(sanic_ultimacarga) < hoy` | cuenta, última carga (base del sweep DEC-018) |

### 5.1 — Filtrar / agrupar por LOCALIZADOR

> **El backoffice tiene "sus" localizadores asignados** (informalmente). **No** modelamos esa relación en la
> app — no hace falta. El dato ya está: **cada interacción lleva su localizador en `sanic_cm_in_agente`.** Solo lo
> aprovechamos. (Display: `sanic_cm_in_agente.sanic_cm_ag_nombre`.)

| Necesidad | Cómo (nativo, sin construir nada) |
|-----------|-----------------------------------|
| **Filtrar** por un localizador puntual | **Filtro de columna** en la grilla moderna sobre *Localizador*. |
| **"Ver siempre los míos"** | **Vista personal** que cada backoffice se guarda: *Mis localizadores* = "Por transcribir" + filtro `sanic_cm_in_agente In (los suyos)`. Queda fija en su menú. **No toca la app.** |
| **Agrupar** la cola | **Group by → Localizador** en la grilla moderna → grupos colapsables **con conteo** ("Marvin: 12 pendientes"). |
| **Repartir el día / ver carga** | **Gráfico "Pendientes por localizador"** (barras) en un tablero; clic en una barra → **drill-down** que filtra la lista. |

> 🔀 **Tradeoff a futuro:** si algún día querés que cada backoffice vea **automáticamente solo los suyos** (sin
> elegir), ahí **sí** habría que modelar la relación — la forma más liviana es **un campo opcional**
> `⚠️TODO(sanic_backofficeid)` (Lookup → systemuser) en `sanic_cm_tbl_agente`, y filtrar las vistas por
> `sanic_cm_in_agente.⚠️TODO(sanic_backofficeid) = User()`. **Hoy no se construye** (lo pediste así): con filtro/agrupación
> manual + vista personal alcanza.

---

## 6. Flujo de Especiales (Solicitante → Supervisor → Admin) — DEC-013/022

```
Solicitante: "Nueva solicitud"
   → crea sanic_cm_tbl_asignacion  sanic_cm_as_tipo=Especial · sanic_cm_as_estado=Activa · sanic_cm_as_estadoaprobacion=Pendiente
   → si el cliente/cuenta no existe, los carga (datos maestros, DEC-013)
        │
Supervisor: cola "Aprobaciones pendientes"
   → Aprobar  → sanic_cm_as_estadoaprobacion=Aprobada · asigna sanic_cm_as_agenteid (localizador) · sanic_cm_as_resolutorid=él
   → Rechazar → sanic_cm_as_estadoaprobacion=Rechazada · sanic_cm_as_motivo
        │
   (Aprobada) → entra al perfil offline del localizador → se gestiona en campo
        │
Admin: cuando termina → cierra → sanic_cm_as_estado=Cerrada · ⚠️TODO(sanic_motivocierre)
```

> **Dos estados ortogonales** (DEC-022): `sanic_cm_as_estado` (Activa/Cerrada, ciclo de vida) y `sanic_cm_as_estadoaprobacion`
> (Borrador/Pendiente/Aprobada/Rechazada, gobernanza). **Sin vencimiento** (la Especial no caduca por el sweep).

**Construcción:** lo natural es un **Business Process Flow (BPF)** sobre `sanic_cm_tbl_asignacion` para las Especiales:
etapas *Solicitud → Aprobación → En gestión → Cierre*, cada etapa habilitada para el rol que corresponde. Las
transiciones se hacen con **botones de comando** (Aprobar/Rechazar) que setean los campos + validan el rol.

---

## 7. Funciones ejecutables de Admin (páginas personalizadas)

Estas **no** son formularios: son **acciones con lógica** (preview + umbral + confirmación). Se hacen como
**páginas personalizadas (canvas embebida en la model-driven)** o **comandos + Power Automate**.

### 7.1 Reconciliación de Diarias desaparecidas (DEC-018)
Una Diaria que **dejó de venir** en el CSV (su `⚠️TODO(sanic_ultimacarga) < hoy`) significa que salió de cartera. **No es
automático** — es una **función que ejecuta el Admin**:
1. **Preview:** lista las Diarias con `⚠️TODO(sanic_ultimacarga) < última fecha de carga` (candidatas a cerrar).
2. **Umbral de seguridad:** si la cantidad supera un % (ej. >30% de la cartera), **avisa y frena** (puede ser un
   import roto, no un cierre real).
3. **Confirmación:** el Admin revisa y confirma → cierra en lote (`sanic_cm_as_estado=Cerrada`, `⚠️TODO(sanic_motivocierre)="fuera de cartera"`).

### 7.2 Cierre de Incobrables / Especiales (DEC-020)
Vista + comando **"Cerrar"**: setea `sanic_cm_as_estado=Cerrada` + pide `⚠️TODO(sanic_motivocierre)`. Manual, del Admin.

### 7.3 Monitoreo del import
Tablero sobre `sanic_cm_tbl_cargadiaria` / `⚠️TODO(sanic_ultimacarga)`: ¿llegó el CSV de hoy?, ¿cuántas cuentas?, ¿última carga
por cuenta? Detecta el "no llegó el import" antes de que sea un problema.

---

## 8. Seguridad (roles de Dataverse)

Un **rol de seguridad** por rol de negocio, con privilegios mínimos:

| Rol negocio | `sanic_cm_tbl_interaccion` | `sanic_cm_tbl_pago` | `sanic_cm_tbl_asignacion` | Maestros |
|-------------|--------------------|--------------|--------------------|----------|
| SR | CM | Backoffice | Read (Org) · Write (solo campos de transcripción) | Read | Read | Read |
| SR | CM | Solicitante | — | — | Create/Read/Write (Especiales propias) | Create/Read (para cargar faltantes) |
| SR | CM | Supervisor | Read | Read | Read/Write (aprobar/reasignar) | Read |
| SR | CM | Admin | Read/Write | Read | Read/Write/Delete | Read/Write |

> Se apoya en la seguridad **row-level "Moderado"** ya definida (DEC-015). El backoffice **no** edita el
> comentario ni los pagos (lo capturado en campo es **inmutable**); solo agrega la **capa de transcripción**.

---

## 9. Decisiones a confirmar (con mi recomendación)

1. **UX de transcripción** — ¿cómo transcribe el backoffice?
   - **(A) Vista + formulario nativo** — abrir cada interacción de la cola y completar. Simple, 100% nativo.
   - **(B) BPF** que guía Pendiente → Grabada.
   - **(C) Página personalizada tipo "cola"** (canvas embebida): muestra la interacción actual, campo de código,
     botón "Grabada y siguiente". **Más rápido para volumen alto.**
   - 💡 **Recomiendo arrancar con (A)** (nativo, sale ya y valida el modelo) y, si el volumen lo pide, sumar (C)
     para velocidad. No te cases con (C) antes de medir el volumen real.

2. **Funciones de Admin (§7)** — **página personalizada (canvas embebida)** para la reconciliación (necesita
   preview + umbral + confirmación interactivos). El cierre simple puede ser **comando + Power Automate**.

3. **¿Auto-clasificador futuro?** El `sanic_cm_in_codigograbacion` se guarda hoy a mano; mañana puede alimentar un modelo
   que **sugiera** el código (DEC-002). Lo dejamos **preparado** (el dato ya se captura), no lo construimos ahora.

---

## 10. Orden de drill-down propuesto

1. **Transcripción** (el corazón): rol de seguridad SR | CM | Backoffice + vista "Por transcribir" + formulario de
   transcripción + regla anti-doble-carga. **Por acá empezaría.**
2. **Especiales:** BPF + comandos Aprobar/Rechazar + roles SR | CM | Solicitante/SR | CM | Supervisor.
3. **Admin:** página de reconciliación (DEC-018) + cierres.
4. **Tableros** y monitoreo del import.

---

## 11. Fuera de alcance / futuro

- Auto-clasificador de código del core (cuando haya datos).
- Integración automática con el core del banco (hoy el write-back es **manual asistido**, DEC-002).
- App móvil para backoffice (no se necesita: es trabajo de escritorio).

---

## 12. Trazabilidad

| Aspecto | Decisión |
|---------|----------|
| Model-driven, 1 app por roles | DEC-014 |
| Write-back manual + código del core + auditoría | DEC-002 |
| Flujo Especiales (Solicitante/Supervisor) + datos maestros | DEC-013 / DEC-022 |
| Reconciliación = función Admin (preview+umbral+confirmación) | DEC-018 |
| Cierre de Incobrables manual (Admin) | DEC-020 |
| Seguridad row-level "Moderado" | DEC-015 |
