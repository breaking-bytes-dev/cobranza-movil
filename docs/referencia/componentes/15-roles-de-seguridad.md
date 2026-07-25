# Componente 15: Roles de Seguridad — definición completa (construible)

> **Qué es.** La definición **exacta y construible** de los **roles de seguridad** de la solución, lista para
> crear en Dataverse (*Configuración → Seguridad → Roles de seguridad*). Amplía la matriz **resumen** del
> [Componente 01 §5](01-seguridad-row-level.md) con **todos** los privilegios (incluidos Append/AppendTo/Assign,
> los que más se olvidan) y agrega el **rol de la cuenta de servicio** que necesita el flujo del owner
> ([Comp. 14](14-automatizacion-owner-asignacion.md)).
>
> Materializa **DEC-014** (5 roles) y **DEC-015** (estrategia *Moderado*: ownership por persona en operativas,
> Read Org en maestros).

---

## 1. Convenciones

**Niveles de acceso** (por privilegio y tabla):

| Símbolo | Nivel | Significa |
|:---:|-------|-----------|
| `—` | Ninguno | Sin ese privilegio. |
| `U` | **User (Básico)** | Solo registros que **owna** el usuario (o compartidos). |
| `O` | **Organization (Global)** | **Todos** los registros de la tabla. |

*(No usamos BU/Parent-child: la estrategia Moderado corre sobre la **BU raíz** — Comp. 01 §8.)*

**Privilegios** (columnas de las tablas de abajo):

| Col | Privilegio | Para qué |
|-----|-----------|----------|
| **C** | Create | Crear filas. |
| **R** | Read | Leer. |
| **E** | Write (Escribir) | Editar. |
| **D** | Delete | Borrar. |
| **Ap** | **Append** | Adjuntar **este** registro **a** un padre (ej. un pago **a** una interacción). |
| **ApA** | **Append To** | Que **le adjunten** hijos a **este** registro (ej. a la interacción se le adjunta un pago; a la **cuenta** se le adjunta una interacción). |
| **As** | Assign | Cambiar el **owner** (reasignar). |

> 🧨 **El asesino silencioso: Append / AppendTo.** Para **crear una interacción ligada a una cuenta** hacen
> falta **DOS** permisos: `Append` en `sanic_cm_tbl_interaccion` **y** `AppendTo` en `sanic_cm_tbl_cuenta`. Si te olvidás el
> `AppendTo` del **maestro** (aunque sea solo-lectura), la creación **falla** con un error críptico. Por eso los
> maestros llevan `ApA = O` para el localizador. **Regla:** todo lookup necesita `AppendTo` en la tabla
> **apuntada**.

> **Compartir (Share):** solo el **Admin** (`O`). El resto **no comparte** — es clave para no inflar
> `PrincipalObjectAccess` (Comp. 01 gotcha 9.5). No lo pongo como columna: asumilo en `—` salvo Admin.

---

## 2. Base común (todos los usuarios)

Cada rol de abajo se **combina** con un piso mínimo para poder entrar y usar apps:

- Asigná también el rol OOB **"Usuario básico" (Basic User)**, **o** incluí sus privilegios mínimos: leer/escribir
  **su** configuración de usuario, leer monedas/zonas horarias, y **ejecutar** las apps (canvas / model-driven)
  que le correspondan.
- **Licencia:** cada usuario necesita licencia **Power Apps** adecuada (Comp. 01 §7-A).

---

## 3. Rol `SR | CM | Localizador`

> Gestiona **solo su cartera** (owner). Captura interacciones/pagos. **No** edita maestros; **solo** reordena.

| Tabla | C | R | E | D | Ap | ApA | As |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `sanic_cm_tbl_asignacion` | — | **U** | **U** ¹ | — | U | U | — |
| `sanic_cm_tbl_interaccion` | **U** | U | U | — | **U** | U | — |
| `sanic_cm_tbl_pago` | **U** | U | U | — | **U** | U | — |
| `sanic_cm_tbl_actualizaciondatos` | **U** | U | U | — | U | U | — |
| `sanic_cm_tbl_cuenta` | — | **O** | — | — | — | **O** ² | — |
| `sanic_cm_tbl_cliente` | — | **O** | — | — | — | O | — |
| `sanic_cm_tbl_direccion` | — | **O** | — | — | — | — | — |
| `sanic_cm_tbl_telefono` | — | **O** | — | — | — | — | — |
| `sanic_cm_tbl_producto` | — | O | — | — | — | O | — |
| `sanic_cm_tbl_fiador` | — | O | — | — | — | O | — |
| `sanic_cm_tbl_agente` | — | O | — | — | — | **O** ² | — |
| `sanic_cm_tbl_cargadiaria` | — | — | — | — | — | — | — |

**Notas:**
1. **Escribe solo `sanic_cm_as_orden`.** Para que no pueda tocar otros campos de la asignación, aplicá **seguridad a
   nivel de columna** (Field Security Profile): todas las columnas de `sanic_cm_tbl_asignacion` **restringidas**, menos
   `sanic_cm_as_orden` (Read+Update para el localizador). El `E=U` de la matriz habilita el privilegio; la columna lo acota.
2. `AppendTo O` en **cuenta** y **agente** es **obligatorio**: la interacción tiene lookups a `sanic_cm_in_cuentaid` y
   `sanic_cm_as_agenteid` → sin esto, **no puede crear la interacción**. En `producto/fiador/cliente` va por prolijidad
   (navegación y futuros lookups).

---

## 4. Rol `SR | CM | Backoffice`

> **Transcribe al core.** Lee todo lo operativo; **escribe solo** los campos de transcripción de la interacción.

| Tabla | C | R | E | D | Ap | ApA | As |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `sanic_cm_tbl_interaccion` | — | **O** | **O** ¹ | — | — | O | — |
| `sanic_cm_tbl_pago` | — | **O** | — | — | — | — | — |
| `sanic_cm_tbl_asignacion` | — | **O** | — | — | — | — | — |
| `sanic_cm_tbl_cuenta` / `sanic_cm_tbl_cliente` | — | O | — | — | — | — | — |
| `sanic_cm_tbl_direccion` / `sanic_cm_tbl_telefono` | — | O | — | — | — | — | — |
| `sanic_cm_tbl_producto` / `sanic_cm_tbl_fiador` / `sanic_cm_tbl_agente` | — | O | — | — | — | — | — |
| `sanic_cm_tbl_cargadiaria` | — | O ² | — | — | — | — | — |

**Notas:**
1. **Escribe solo** `sanic_cm_in_codigograbacion`, `sanic_cm_in_estadograbacion`, `sanic_cm_in_fechagrabacion`, `sanic_cm_in_grabadopor`.
   Acotalo con **seguridad a nivel de columna** (el resto de la interacción — comentario, geo — **inmutable**).
2. Lectura del monitoreo del import **opcional** (Comp. 01 §5 nota 4). Disparar el import es del **Admin**.

---

## 5. Rol `SR | CM | Solicitante`

> Crea **asignaciones Especiales** y, si faltan, los **datos maestros** del cliente/cuenta (DEC-013).

| Tabla | C | R | E | D | Ap | ApA | As |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `sanic_cm_tbl_asignacion` | **O** | O | O | — | O | O | — |
| `sanic_cm_tbl_cliente` | **O** | O | O | — | O | O | — |
| `sanic_cm_tbl_cuenta` | **O** | O | O | — | O | O | — |
| `sanic_cm_tbl_direccion` | **O** | O | O | — | O | O | — |
| `sanic_cm_tbl_telefono` | **O** | O | O | — | O | O | — |
| `sanic_cm_tbl_producto` / `sanic_cm_tbl_fiador` | — | O | — | — | — | O | — |
| `sanic_cm_tbl_agente` | — | O | — | — | — | O | — |
| `sanic_cm_tbl_interaccion` / `sanic_cm_tbl_pago` | — | — | — | — | — | — | — |

> La Especial nace `estado=Activa`, `estadoaprobacion=Pendiente`. El **owner** lo pone el flujo cuando el
> Supervisor asigne el localizador (§8 / Comp. 14). El Solicitante **no** aprueba.

---

## 6. Rol `SR | CM | Supervisor`

> **Aprueba/rechaza** Especiales y **asigna el localizador** (setea `sanic_cm_as_agenteid`).

| Tabla | C | R | E | D | Ap | ApA | As |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `sanic_cm_tbl_asignacion` | — | **O** | **O** ¹ | — | O | O | — |
| `sanic_cm_tbl_interaccion` / `sanic_cm_tbl_pago` | — | O | — | — | — | — | — |
| `sanic_cm_tbl_cuenta` / `sanic_cm_tbl_cliente` | — | O | — | — | — | — | — |
| `sanic_cm_tbl_direccion` / `sanic_cm_tbl_telefono` | — | O | — | — | — | — | — |
| `sanic_cm_tbl_producto` / `sanic_cm_tbl_fiador` / `sanic_cm_tbl_agente` | — | O | — | — | — | O | — |

**Nota 1:** escribe `sanic_cm_as_estadoaprobacion`, `sanic_cm_as_resolutorid` ⚠️TODO(sanic_supervisorid→resolutorid: confirmar), `sanic_cm_as_agenteid`, `sanic_cm_as_motivo` ⚠️TODO(sanic_motivorechazo→motivo: confirmar).
**No** necesita `Assign` — cambia el **lookup del agente** (Write), y el **owner lo pone el flujo** (Comp. 14).

---

## 7. Rol `SR | CM | Admin`

> Administra **todo**: reconciliación (DEC-018), cierres (DEC-020), links agente↔usuario, import.

| Tabla | C | R | E | D | Ap | ApA | As | Share |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **Todas las `sanic_*`** (operativas, maestros, catálogos, `cargadiaria`) | O | O | O | O | O | O | **O** | **O** |
| Config del import (Smelter: Template/Mappings/Job/Log) | O | O | O | O | O | O | O | O |

> Es el único con **Delete**, **Assign** y **Share** a nivel Org. Para tareas de personalización (crear/editar
> tablas/vistas) se combina con el OOB **System Customizer** en el entorno de desarrollo; en producción, solo lo
> necesario.

---

## 8. Rol `SR | CM | Servicio - Cobranza` (cuentas de servicio — flujo + import) 🔑

> **No es para personas.** Es el rol de las **cuentas de servicio / connection references** de: (a) el **flujo
> del owner** ([Comp. 14](14-automatizacion-owner-asignacion.md)) y (b) el **import Smelter**.

| Tabla | C | R | E | D | Ap | ApA | **As** |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `sanic_cm_tbl_asignacion` | **O** | O | **O** | — | O | O | **O** ¹ |
| `sanic_cm_tbl_agente` | O ² | O | O ² | — | O | O | — |
| `sanic_cm_tbl_cuenta` | O ² | O | O ² | — | O | O | — |
| `sanic_cm_tbl_cliente` / `sanic_cm_tbl_direccion` / `sanic_cm_tbl_telefono` | O ² | O | O ² | — | O | O | — |
| `sanic_cm_tbl_producto` / `sanic_cm_tbl_fiador` | O ² | O | O ² | — | O | O | — |
| `sanic_cm_tbl_cargadiaria` | O ² | O | O ² | — | O | O | — |
| `systemuser` (tabla del sistema) | — | **O** ³ | — | — | — | — | — |

**Notas:**
1. **`Assign O` en `sanic_cm_tbl_asignacion` es LA razón de ser de este rol.** Sin `Assign`, el flujo **no puede
   cambiar el owner** → todo el aislamiento no funciona (Comp. 14 §5).
2. **Create/Write** en maestros = para el **import (Smelter)**, que upsertea. Si separás la cuenta del **flujo**
   de la del **import**, la del flujo solo necesita `sanic_cm_tbl_asignacion` (R/W/Assign) + `sanic_cm_tbl_agente` (R) +
   `systemuser` (R); la del import lleva el resto.
3. **Read en `systemuser`** = para que el flujo resuelva `sanic_cm_tbl_agente.sanic_cm_ag_usuarioid` → el usuario destino.

> **Buenas prácticas:** cuenta de servicio **dedicada** (no un humano), con este rol y **nada más**; conexión vía
> **connection reference** en la solución (ALM), no credenciales embebidas.

---

## 9. Gotchas y verificación

- **Append/AppendTo faltantes** → el error más común. Si "no puede guardar la interacción/pago", revisá
  `Append` en la tabla hija y `AppendTo` en **cada** tabla apuntada por un lookup (incluidos maestros).
- **`Assign` del servicio** → si el owner "no cambia", casi seguro le falta `Assign O` en `sanic_cm_tbl_asignacion`.
- **Seguridad de columna (Field Security)** → recomendada para acotar Localizador (solo `sanic_cm_as_orden`) y
  Backoffice (solo campos de transcripción). El privilegio de tabla habilita; la columna acota.
- **Compartir apagado** salvo Admin → mantiene `PrincipalObjectAccess` chico (performance).

**Checklist (probar con 2 localizadores + 1 de cada rol de oficina):**
- [ ] Localizador **A** ve/gestiona su cartera; **NO** ve la de **B**; solo puede tocar el **orden**.
- [ ] Localizador crea interacción **con pago** (verifica Append/AppendTo OK).
- [ ] Backoffice ve todas las interacciones; escribe **solo** código+estado; **no** toca comentario/geo.
- [ ] Solicitante crea cliente + Especial; **no** aprueba.
- [ ] Supervisor aprueba y asigna localizador → (flujo) el **owner** queda en ese localizador y **él la ve**.
- [ ] Admin ve/administra todo, incl. import.
- [ ] **Cuenta de servicio:** el flujo del owner **cambia el owner** (tiene `Assign`); el import upsertea maestros.

---

## 10. Trazabilidad

| Aspecto | Referencia |
|---------|-----------|
| 5 roles de negocio | DEC-014 · Comp. 01 §5 |
| Estrategia Moderado (ownership + Read Org) | DEC-015 · Comp. 01 |
| Rol de servicio con `Assign` (flujo owner) | Comp. 14 |
| Vínculo agente↔usuario (dependencia) | Comp. 01 §3 · Comp. 04 |
| Sin BUs / sin Share (performance) | Comp. 01 §8 · gotcha 9.5 |
