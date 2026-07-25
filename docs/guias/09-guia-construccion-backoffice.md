# Guía de Construcción — App de Oficina (Backoffice)

> **Instructivo paso a paso** para construir la app model-driven, **incremental**: cada entrega arma una pieza,
> la verificás, y seguimos. Es el equivalente al doc 05 del localizador, pero para model-driven (se **configura**
> por menús en `make.powerapps.com`, no se dibujan controles).
>
> Diseño de referencia: [doc 07](../desarrollo/07-app-backoffice-modeldriven.md) (macro) y [doc 08](../desarrollo/08-backoffice-transcripcion.md)
> (transcripción).

---

## 📋 Antes de empezar (precondiciones)

- [ ] Las **tablas `sanic_*` ya existen** en el entorno (Componentes 02–13).
- [ ] Sos **maker / administrador** del entorno (podés crear soluciones, apps, roles).
- [ ] Existe **≥ 1 `sanic_cm_tbl_interaccion`** con `sanic_cm_in_estadograbacion = Pendiente` (si no, la cola sale vacía).
- [ ] Un **usuario de prueba** para asignarle el rol Backoffice.

---

## 🧭 Glosario del portal (lo que vas a ver)

- **Solución (Solution):** el "contenedor" donde vive todo (tablas, apps, roles, flujos). Clave para ALM.
- **App basada en modelo (Model-driven app):** el caparazón con menú (áreas) que abre el usuario.
- **Vista (View):** una lista filtrada de una tabla.
- **Formulario (Form):** la pantalla de un registro.
- **Rol de seguridad (Security role):** quién puede ver/hacer qué.
- **Barra de comandos (Command bar):** los botones de arriba (Nuevo, Guardar… y los tuyos).

---

## 🗺️ Plan de construcción (las piezas)

```
Parte 0  →  La solución (dónde vive todo)         ← ESTA ENTREGA
Parte 1  →  Rol de seguridad "Backoffice"         ← ESTA ENTREGA
Parte 2  →  La app basada en modelo (caparazón)   ← ESTA ENTREGA
Parte 3  →  Vista "Por transcribir" (la cola)     ← ESTA ENTREGA
Parte 4  →  Probar (entrar como backoffice)       ← ESTA ENTREGA
── próximas entregas ──
Parte 5  →  Formulario de transcripción (4 secciones)
Parte 6  →  Comando "Marcar como grabada"
Parte 7  →  Business rule (bloqueo) + Field security
Parte 8  →  Flujo de auditoría (Power Automate)
```

> **Regla de oro:** terminá la Parte 4 y **verificá que un backoffice ve la cola** antes de seguir. Si eso anda,
> el resto es sumar piezas encima.

---

## 🟢 PARTE 0 — La solución (ALM)

Todo lo que construyas tiene que vivir **dentro de una solución** (así viaja Dev→Test→Prod).

1. Entrá a **make.powerapps.com** → confirmá el **entorno** correcto (arriba a la derecha).
2. Menú izquierdo → **Soluciones** (Solutions).
3. ¿Ya tenés una solución donde están las tablas `sanic_*`? → **abrila** y usá esa.
   ¿No? → **+ Nueva solución**:
   - **Nombre para mostrar:** `Cobranza Móvil`.
   - **Editor (Publisher):** elegí el que tenga el **prefijo `sanic`** (el mismo de tus tablas). Si no existe,
     creá uno con prefijo `sanic`.
   - **Crear.**
4. **Abrí la solución.** De acá en más, **todo lo creás con la solución abierta** (para que quede adentro).

---

## 🔐 PARTE 1 — Rol de seguridad "Backoffice"

Crea el rol y aplicale los permisos (del [Comp. 15 §4](../referencia/componentes/15-roles-de-seguridad.md)). Para **esta
entrega** alcanza con lo que hace falta para ver la cola; el resto (field security) va en la Parte 7.

1. **Crear el rol.** Ruta estable: **admin.powerplatform.microsoft.com** → **Entornos** → tu entorno →
   **Configuración** → **Usuarios y permisos** → **Roles de seguridad** → **+ Nuevo rol**.
   *(Alternativa moderna: dentro de la solución → **+ Nuevo → Más → Seguridad → Rol de seguridad**.)*
   - **Nombre:** `SR | CM | Backoffice`.
2. **Aplicar permisos.** En el editor, cada tabla tiene una fila con círculos por privilegio (Crear, Leer,
   Escribir, Eliminar, Anexar, Anexar a, Asignar, Compartir). **Cada clic llena más el círculo** = sube el
   nivel: *(vacío = Ninguno · ¼/½ = User · lleno = Organización)*. Buscá cada tabla en la pestaña **Entidades
   personalizadas** y seteá:

   | Tabla | Leer | Escribir | Anexar a |
   |-------|:----:|:--------:|:--------:|
   | `sanic_cm_tbl_interaccion` | **Org** | **Org** | Org |
   | `sanic_cm_tbl_pago` | **Org** | — | — |
   | `sanic_cm_tbl_asignacion` | **Org** | — | — |
   | `sanic_cm_tbl_cuenta` · `sanic_cm_tbl_cliente` | **Org** | — | — |
   | `sanic_cm_tbl_direccion` · `sanic_cm_tbl_telefono` | **Org** | — | — |
   | `sanic_cm_tbl_producto` · `sanic_cm_tbl_fiador` · `sanic_cm_tbl_agente` | **Org** | — | — |

   *(La matriz completa —con Create/Delete/Append/Assign de todos los roles— está en Comp. 15. Esto es el
   subconjunto para que la cola funcione.)*
3. **Guardar y cerrar.**
4. **Asignar el rol** a tu **usuario de prueba:** mismo lugar → **Usuarios y permisos → Usuarios** → elegí el
   usuario → **Administrar roles de seguridad** → tildá `SR | CM | Backoffice` (dejale también **Usuario
   básico / Basic User**). **Guardar.**

> ⚠️ Sin **Usuario básico** (o sus privilegios mínimos), el usuario no puede ni abrir la app. Es el piso común
> (Comp. 15 §2).

---

## 🟦 PARTE 2 — La app basada en modelo (el caparazón)

1. Con la **solución abierta** → **+ Nuevo → Aplicación → Aplicación basada en modelo**.
2. Se abre el **diseñador de apps moderno**. Poné el **nombre:** `Cobranza Móvil – Oficina`.
3. **Agregar la primera página:** botón **+ Agregar página** (Add page) → **Vista y formulario basados en
   tabla** (Table based view and form) → elegí **`sanic_cm_tbl_interaccion`** → **Agregar**.
   - Dejá tildado que agregue la tabla a la **navegación**.
4. **Organizar el menú (navegación):** en el panel de **Navegación** (izquierda del diseñador), vas a ver un
   **Área**, un **Grupo** y una **Subárea**. Renombralos:
   - **Área:** `Cobranza`
   - **Grupo:** `Transcripción`
   - **Subárea:** `Por transcribir` (apunta a `sanic_cm_tbl_interaccion`).
   *(Más adelante sumamos "Transcritas hoy", "Especiales", etc.)*
5. **Guardar** (arriba a la derecha) → **Publicar** (Publish).
6. **Dejá esta pestaña abierta**; volvemos en la Parte 4. Primero creamos la vista.

---

## 📋 PARTE 3 — Vista "Por transcribir" (la cola)

1. Volvé a la **solución** → **Tablas** → abrí **`sanic_cm_tbl_interaccion`** → pestaña **Vistas** (Views).
2. **+ Nueva vista** → **Nombre:** `Por transcribir` → **Crear**. Se abre el **diseñador de vistas**.
3. **Filtro** (el corazón de la cola): panel derecho **Editar filtros** (Edit filters) → **+ Agregar** →
   - Columna: **`Estado de grabación`** (`sanic_cm_in_estadograbacion`) · Operador: **es igual a** · Valor:
     **`Pendiente`**.
4. **Orden (FIFO):** clic en el encabezado de la columna **Fecha de interacción** → **Ordenar A→Z**
   (ascendente: lo más viejo primero).
5. **Columnas** — agregá/quitá para que queden (botón **+ Ver columna** / **Related** para las de tablas
   vinculadas):
   - **Fecha de interacción** (`⚠️TODO(sanic_fechainteraccion)`)
   - **Cliente** → columna **relacionada**: `Cuenta (sanic_cm_in_cuentaid) → Cliente (sanic_cm_ct_clienteid) → Nombre (sanic_cm_cl_nombre)`.
     *(En el selector, entrá por la relación `Cuenta` y después `Cliente` para llegar al `Nombre`.)*
   - **Cuenta** (`sanic_cm_in_cuentaid` — el número)
   - **Localizador** (`sanic_cm_in_agente`)  ← clave para filtrar/agrupar por localizador (doc 07 §5.1)
   - **¿Aplica pago?** (`sanic_cm_cd_aplicapago`)
   - **Comentario** (`sanic_cm_in_comentario`)
6. **Guardar** → **Publicar**.

> 💡 **Ancho de columnas:** arrastrá para que **Comentario** sea la más ancha (es lo que más leen).

---

## ✅ PARTE 4 — Enganchar la vista y PROBAR

1. Volvé al **diseñador de la app** (Parte 2) → seleccioná la página/subárea **`Por transcribir`** → en sus
   **propiedades**, poné como **Vista por defecto** la vista **`Por transcribir`** que acabás de crear.
   *(Opcional: en "Vistas disponibles", dejá solo las que querés que el backoffice pueda elegir.)*
2. **Guardar → Publicar.**
3. **Compartir la app** con el backoffice: en la lista de apps (o desde el diseñador) → **⋯ → Compartir** →
   agregá tu **usuario de prueba** (o un grupo) → asignale el rol **`SR | CM | Backoffice`**.
4. **Probá como backoffice:**
   - [ ] Iniciá sesión (o "Reproducir/Play") con el **usuario de prueba**.
   - [ ] Abrí **Cobranza Móvil – Oficina** → área **Cobranza → Por transcribir**.
   - [ ] Ves **solo las interacciones `Pendiente`**, la más vieja arriba.
   - [ ] Aparecen las columnas: cliente, cuenta, **localizador**, ¿pago?, comentario.
   - [ ] Probá **agrupar por Localizador** (encabezado de la columna → **Agrupar por** en la grilla moderna) →
         ves grupos con conteo.
   - [ ] El backoffice **NO** ve tablas/registros fuera de lo permitido (ej. no puede editar un cliente).

> 🎉 Si ves la cola y podés agrupar por localizador → **la base está en pie.** De acá para arriba, todo suma.

---

## 🔜 Qué sigue (próxima entrega)

**Parte 5 — El formulario de transcripción:** las 4 secciones (Gestión de campo *solo lectura* · Pagos
*subgrid* · Transcripción *código editable* · Auditoría *solo lectura*), tal como el [doc 08 §6](../desarrollo/08-backoffice-transcripcion.md).
Después: el **comando** "Marcar como grabada", la **business rule**, el **field security** y el **flujo de
auditoría**.

> Cuando termines la Parte 4 y la cola te funcione, avisame y seguimos con el formulario. Si algo no sale,
> decime en qué paso te trancaste. **Un paso a la vez.** 💪
