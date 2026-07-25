# Backoffice — Transcripción al core (drill-down)

> **Qué es.** El detalle construible de la **tarea central del backoffice**: revisar lo capturado en campo,
> grabarlo en el **core del banco** y **asignarle el código**, con **auditoría anti-doble-carga**. Es el
> desarrollo de [doc 07 §3](07-app-backoffice-modeldriven.md) sobre `sanic_cm_tbl_interaccion`.
>
> Se apoya en: rol **SR | CM | Backoffice** ([Comp. 15 §4](../referencia/componentes/15-roles-de-seguridad.md)), esquema de
> `sanic_cm_tbl_interaccion` ([Comp. 10](../referencia/componentes/10-tabla-interaccion.md)), DEC-002.

---

## 1. El bucle diario (lo que estamos construyendo)

```
Localizador captura (campo)  →  sanic_cm_tbl_interaccion  sanic_cm_in_estadograbacion = Pendiente
                                          │
Backoffice, en la app de Oficina:
   1. Abre la vista "Por transcribir"  (§4)
   2. Abre una interacción → formulario de transcripción  (§6)
   3. Lee comentario + pago(s) + geo, y la graba en el CORE (sistema externo, a mano)
   4. Escribe el CÓDIGO con que la grabó  → sanic_cm_in_codigograbacion
   5. Cambia estado → "Grabada en core"  y guarda
                                          │
   (auto, flujo §7) sanic_cm_in_fechagrabacion = ahora · sanic_cm_in_grabadopor = el backoffice
   (regla §8) el registro queda BLOQUEADO (no se re-graba) y SALE de la cola
```

---

## 2. Enfoque elegido

**(A) Vista + formulario nativo** — el backoffice abre cada interacción de la cola y completa. 100% nativo, sale
ya, valida el modelo. *(La página tipo "cola" —opción C— queda como **mejora opcional** §11 si el volumen diario
es alto.)*

---

## 3. Seguridad (rol SR | CM | Backoffice) — recordatorio

Del [Comp. 15 §4](../referencia/componentes/15-roles-de-seguridad.md):
- **Lee** (Org) todo lo operativo: interacción, pago, asignación, cuenta/cliente, maestros.
- **Escribe** (Org) en `sanic_cm_tbl_interaccion` **solo** los campos de transcripción → se acota con **Field Security
  Profile** (§9): el comentario, la geo y los pagos son **inmutables** para el backoffice (evidencia de campo).
- **No crea ni borra** interacciones.

---

## 4. Vista `Por transcribir` (la cola)

| Propiedad | Valor |
|-----------|-------|
| Tabla | `sanic_cm_tbl_interaccion` |
| Filtro | `sanic_cm_in_estadograbacion = Pendiente` |
| Orden | `⚠️TODO(sanic_fechainteraccion)` **ascendente** (lo más viejo primero — FIFO) |
| Columnas | Fecha interacción · Cliente (`sanic_cm_in_cuentaid.sanic_cm_ct_clienteid.sanic_cm_cl_nombre`) · Cuenta (`sanic_cm_in_cuentaid.sanic_cm_ct_numerodecuenta`) · **Localizador** (`sanic_cm_in_agente`) · ¿Aplica pago? (`sanic_cm_cd_aplicapago`) · Comentario |

- **Filtrar/agrupar por localizador** (doc 07 §5.1): *Group by → Localizador* en la grilla moderna, o **vista
  personal** *"Por transcribir — mis localizadores"* filtrando `sanic_cm_in_agente In (los suyos)`.
- **Vista por defecto** de la app para el rol Backoffice (primero que ve al entrar).

---

## 5. Vista `Transcritas hoy` (control)

| Propiedad | Valor |
|-----------|-------|
| Filtro | `sanic_cm_in_estadograbacion = Grabada en core` **y** `sanic_cm_in_fechagrabacion = Hoy` **y** `sanic_cm_in_grabadopor = Usuario actual` |
| Orden | `sanic_cm_in_fechagrabacion` descendente |
| Columnas | Hora grabada · Cuenta · Localizador · **Código core** · Comentario |

> Le da al backoffice su **arqueo del día**: qué grabó, con qué código. (Para supervisión, una vista sin el filtro
> "por mí" muestra lo de todo el equipo.)

---

## 6. Formulario principal de `sanic_cm_tbl_interaccion` (Transcripción)

Un solo formulario, **secciones claras**: lo de campo arriba (solo lectura), la transcripción abajo (editable).

### Sección 1 — "Gestión de campo" *(solo lectura)*
| Campo | Control |
|-------|---------|
| `sanic_cm_in_cuentaid` | Lookup (link a la cuenta) |
| Cliente | `sanic_cm_in_cuentaid.sanic_cm_ct_clienteid` (columna calculada o Quick View) |
| `sanic_cm_in_agente` | Localizador (lookup) |
| `⚠️TODO(sanic_fechainteraccion)` | Fecha/hora |
| `sanic_cm_in_comentario` | Texto multilínea (RO) |
| `sanic_cm_cd_aplicapago` / `sanic_cm_in_actualizainfo` | Sí/No (RO) |
| Ubicación | `sanic_cm_in_latitud` / `sanic_cm_in_longitud` (+ botón/enlace a mapa) |

### Sección 2 — "Pagos" *(subgrid, solo lectura)*
- **Subgrid** de `sanic_cm_tbl_pago` (relación `sanic_cm_pg_interaccion`): `sanic_cm_pg_monto` · `sanic_cm_pg_moneda` ·
  `sanic_cm_pg_numerorecibo` · `⚠️TODO(sanic_fechapago)`. Son los pagos que el backoffice tiene que **grabar en el core**.

### Sección 3 — "Transcripción al core"
| Campo | Control | Editable a mano |
|-------|---------|-----------------|
| `sanic_cm_in_codigograbacion` | Texto — **el código que asignó al grabar** | ✅ **Sí — el ÚNICO campo manual** |
| `sanic_cm_in_estadograbacion` | Choice — muestra el estado actual | ❌ **Solo lectura** — se cambia con el **comando** «Marcar como grabada» (§6.5), nunca a mano |

### Sección 4 — "Auditoría" *(solo lectura, la llena el flujo §7)*
| Campo | Control |
|-------|---------|
| `sanic_cm_in_fechagrabacion` | Fecha/hora (auto) |
| `sanic_cm_in_grabadopor` | Lookup → systemuser (auto) |

> **Quick View de la cuenta** (opcional, muy útil): un control Quick View que muestre retiro, mora y contacto de
> `sanic_cm_in_cuentaid` sin salir del formulario — contexto para grabar bien.

### 6.5 — Barra de comandos: «Marcar como grabada» 🔘

En vez de que el backoffice **cambie el combo de estado a mano**, un **botón en la barra de comandos** hace el
cambio (y dispara la auditoría §7). El **único** campo que edita a mano es `sanic_cm_in_codigograbacion`.

**Comando (barra de comandos moderna, con Power Fx):**
- **Dónde:** barra de comandos del **formulario principal** de `sanic_cm_tbl_interaccion`. *(Opcional: también en la
  vista "Por transcribir".)*
- **Etiqueta:** `Marcar como grabada` · **Ícono:** un check ✓.
- **Visible** (Power Fx) — solo si está pendiente:
  ```
  Self.Selected.Item.sanic_cm_in_estadograbacion = 'Estado grabación interacción (sanic_cm_tbl_interaccion)'.Pendiente
  ```
- **Habilitado / DisplayMode** (Power Fx) — **exige el código primero**:
  ```
  If(!IsBlank(Self.Selected.Item.sanic_cm_in_codigograbacion), DisplayMode.Edit, DisplayMode.Disabled)
  ```
- **Acción (OnSelect):**
  ```
  Patch(sanic_cm_tbl_interaccion, Self.Selected.Item,
        { sanic_cm_in_estadograbacion: 'Estado grabación interacción (sanic_cm_tbl_interaccion)'.'Grabada en core' })
  ```
  *(al guardar cambia el estado → el flujo §7 estampa quién/cuándo; el registro sale de la cola.)*
  *(En la implementación real, el comando invoca la Custom API `sanic_PlugInCMMarcarinteraccioncomograbadaenCORE`.)*

> - Los nombres de Choice (`'Estado grabación interacción (sanic_cm_tbl_interaccion)'.'Grabada en core'`) los **autocompleta** el
>   diseñador de comandos — usá lo que ofrezca. El choice es `sanic_cm_choice_estadograbacioninteraccion` ('2' = grabada).
> - **El estado es solo lectura en el formulario** + el botón es el **único camino** → imposible marcar grabada
>   "a mano" o a medias. La **field security (§9)** mantiene `sanic_cm_in_estadograbacion` como **updatable** (el comando
>   escribe en nombre del usuario); lo que bloquea la edición manual es el **formulario** (campo en solo lectura).
> - **Sin código, no hay grabada:** si `sanic_cm_in_codigograbacion` está vacío, el botón queda **deshabilitado**.

---

## 7. Automatización de auditoría (Power Automate) 🔑

Estampa **quién y cuándo** grabó — **el backoffice no lo tipea** (integridad: no se puede falsear). Mismo patrón
que el flujo del owner ([Comp. 14](../referencia/componentes/14-automatizacion-owner-asignacion.md)).

**Flujo `Cobranza · Auditoría de transcripción`:**
- **Trigger:** Dataverse *"Cuando se modifica una fila"* → `sanic_cm_tbl_interaccion`, **Scope: Organización**.
- **🔑 Columnas de filtro:** **solo `sanic_cm_in_estadograbacion`** → dispara únicamente cuando cambia el estado
  (evita disparos inútiles y **loops**: el update de auditoría no toca `sanic_cm_in_estadograbacion`).
- **Condición:** `sanic_cm_in_estadograbacion = "Grabada en core"` **Y** `sanic_cm_in_fechagrabacion` está vacío
  (idempotencia).
- **Acción — Actualizar fila** (la del trigger):
  - `sanic_cm_in_fechagrabacion = utcNow()`
  - `sanic_cm_in_grabadopor = ` el **Modificado por** del trigger (`_modifiedby_value`) → es quien marcó Grabada.

> Como corre **server-side**, el backoffice **no puede alterar** la auditoría. Async (segundos) — irrelevante.

---

## 8. Bloqueo post-grabada (Regla de negocio)

Que una interacción **ya grabada no se re-edite** (anti-doble-carga en el formulario).

**Business Rule sobre `sanic_cm_tbl_interaccion`** (ámbito: Entidad):
- **Condición:** `sanic_cm_in_estadograbacion = "Grabada en core"`.
- **Acciones:** **Bloquear** (Lock) `sanic_cm_in_codigograbacion` (así no se cambia el código después de grabar).
- *(else)* Desbloquear `sanic_cm_in_codigograbacion` (mientras esté Pendiente, editable).

> El `sanic_cm_in_estadograbacion` **ya es solo lectura siempre** en el formulario (§6 / §6.5) — solo lo mueve el
> comando. La regla acá cierra el **código**: una vez grabada, ni el código se toca.

> Las Business Rules **sí** pueden bloquear/desbloquear campos. Combinado con que el registro **sale de la cola**
> "Por transcribir" al quedar Grabada, la doble carga se previene por **dos vías**.

---

## 9. Field Security Profile — acotar qué escribe el backoffice

El rol le da Write sobre `sanic_cm_tbl_interaccion`, pero **solo debe tocar la transcripción**. La forma dura de
garantizarlo:

- **Perfil de seguridad de campo** "Backoffice — transcripción":
  - `sanic_cm_in_codigograbacion`, `sanic_cm_in_estadograbacion` → **Update = Sí**.
  - `sanic_cm_in_comentario`, `sanic_cm_in_latitud`, `sanic_cm_in_longitud`, `sanic_cm_cd_aplicapago`, `sanic_cm_in_actualizainfo`,
    `⚠️TODO(sanic_fechainteraccion)` → **Update = No** (solo lectura, **inmutables**).
  - `sanic_cm_in_fechagrabacion`, `sanic_cm_in_grabadopor` → **Update = No** para personas (los pone el flujo, que corre con
    la cuenta de servicio).
- Asigná el perfil al **equipo/rol SR | CM | Backoffice**.

> Requiere marcar esas columnas como **"habilitadas para seguridad de campo"** en su definición. Es lo que hace
> que el comentario de campo sea **prueba inalterable**.

---

## 10. Anti-doble-carga — las capas (resumen)

1. **Cola filtrada** (`= Pendiente`) → lo grabado **desaparece**; nadie lo vuelve a tomar.
2. **Auditoría automática** (§7) → queda **quién/cuándo** grabó, server-side, infalsificable.
3. **Bloqueo post-grabada** (§8) → no se re-edita desde el formulario.
4. **Field security** (§9) → el backoffice **nunca** toca la evidencia de campo.
5. *(Opcional, alto concurrencia)* un campo `⚠️TODO(sanic_tomadapor)` que el backoffice "reclama" al abrir, para que dos
   personas no trabajen la misma a la vez. **No** lo agregamos ahora (la cola FIFO + el filtro alcanzan); queda
   anotado por si el equipo crece.

---

## 11. (Opcional) Página tipo "cola" para alto volumen

Si el volumen diario es alto (referencia: **> ~150–200/día por backoffice**), una **página personalizada**
(canvas embebida) acelera: muestra la interacción actual + campo de código + botón **"Grabada y siguiente"** que
guarda y trae la próxima **sin volver a la lista**. Reusa la misma tabla y el mismo flujo de auditoría. **No se
construye hasta medir** el volumen real.

---

## 12. Pruebas

- [ ] La cola "Por transcribir" muestra solo `Pendiente`, FIFO por fecha.
- [ ] Group by / vista personal **por localizador** funciona.
- [ ] Abrir una interacción: campo de campo **solo lectura**, pagos en subgrid, transcripción editable.
- [ ] El **combo de estado** NO se puede editar a mano (solo lectura en el formulario).
- [ ] Botón **«Marcar como grabada»** está **deshabilitado** si falta el código.
- [ ] Escribir `sanic_cm_in_codigograbacion` → el botón se **habilita** → tocarlo pasa el estado a **Grabada**.
- [ ] (flujo §7) a los segundos, `sanic_cm_in_fechagrabacion` y `sanic_cm_in_grabadopor` = **el backoffice**, sin tipearlos.
- [ ] El registro **sale** de "Por transcribir" y **aparece** en "Transcritas hoy".
- [ ] Reabrir la grabada → campos de transcripción **bloqueados** (§8).
- [ ] Backoffice intenta editar `sanic_cm_in_comentario`/geo → **no puede** (field security §9).
- [ ] Otro backoffice **no** ve el registro en su cola (ya no está Pendiente).

---

## 13. Trazabilidad

| Aspecto | Referencia |
|---------|-----------|
| Write-back manual + código + auditoría | DEC-002 · doc 07 §3 |
| Rol SR | CM | Backoffice (write solo transcripción) | Comp. 15 §4 |
| Auditoría por flujo (patrón) | Comp. 14 |
| Esquema `sanic_cm_tbl_interaccion` | Comp. 10 |
| Filtrar/agrupar por localizador | doc 07 §5.1 |
