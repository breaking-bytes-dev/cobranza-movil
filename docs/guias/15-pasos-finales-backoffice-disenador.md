# Guía — Pasos finales del Backoffice (diseñador) 🏁

> **Contexto.** Toda la **estructura** del backoffice (formularios, vistas, roles) **ya está construida por API**.
> Esta guía cubre **solo lo que se hace en el portal** — comandos Power Fx, áreas de la app, seguridad de
> formularios y asignación de roles — porque eso **no tiene una API limpia**. Hacelo sección por sección; al
> final hay una checklist y el bloque que quedó **diferido para cuando tengas la carga**.
>
> **Reglas de oro de los comandos:**
> 1. **Todos van en el `Formulario principal`** (Main form) de su tabla — porque cada uno depende de un campo
>    que se llena/guarda ANTES (código, motivo, localizador), y eso vive en el form abierto.
> 2. Los nombres de campos son el **display name real de la columna**, no la etiqueta del form. Tipeá el `.`
>    después de `Self.Selected.Item` y **elegí del intellisense** — así no fallás por un nombre.
> 3. **El command bar NO acepta `Set`** (sin estado imperativo). Para updates → un solo `Patch`.
> 4. **Las tablas se referencian por su nombre plural con pipes**, entre comillas: `'CM | TBL | Asignacions'`,
>    `'CM | TBL | Interaccions'`, `'CM | TBL | Cuentas'` (NO `Asignaciones`/`Interacciones`).
> 5. **El comando SOLO puede tocar SU PROPIA tabla** — no puede crear/leer en otra. Por eso "Solicitar
>    Especial" no es un comando, se hace desde el subgrid (§2.1).

---

## Dónde se hace cada cosa

| Tarea | Ruta |
|-------|------|
| **Comandos** | Solución → Tablas → *(tabla)* → **… Editar barra de comandos** → *Formulario principal* |
| **Áreas de la app** | Solución → Apps → **App - CM - Backoffice** → **Editar** (diseñador de apps) |
| **Seguridad de formulario** | Tabla → Formularios → *(form)* → **Habilitar roles de seguridad** |
| **Roles a usuarios** | admin.powerplatform.microsoft.com → Entorno → Usuarios |

---

## 1. Backoffice — Transcripción

### 1.1 Comando "Marcar como grabada" (tabla **Interacciones**, Formulario principal)
Acción → *Ejecutar fórmula* (OnSelect):
```powerfx
If(
    IsBlank(Self.Selected.Item.'Código del core'),
    Notify("Primero cargá y GUARDÁ el Código del core.", NotificationType.Error),
    Patch(
        'CM | TBL | Interaccions', Self.Selected.Item,
        {
            'Estado de transcripción': 'Estado de transcripción (Interacciones)'.'Grabado',
            'Fecha grabada en core': Now(),
            'Grabada por': Self.Selected.Item.'Modified By'
        }
    );
    Notify("Marcada como grabada en el core.", NotificationType.Success)
)
```
**Visibilidad** (Mostrar en condición):
```powerfx
Self.Selected.Item.'Estado de transcripción' = 'Estado de transcripción (Interacciones)'.'Pendiente'
```

### 1.2 Barra minimalista
Ocultar (Visibilidad → Ocultar): **Save & Close · Deactivate · Refresh · Check Access · Flow · Run Report · Abrir en ventana**.
Dejar: **Atrás · Save · Marcar como grabada**.

---

## 2. Especiales — Solicitante

### 2.1 Crear un Especial (el Solicitante NO crea clientes)
> 🔒 **Gobernanza:** el Solicitante **solo LEE** clientes/cuentas — **no crea ni edita** (eso es del **Admin**).
> Crea la **solicitud** referenciando una **cuenta existente** y completa el **retiro** (el monto a cobrar).
> La esencia del Especial: *"andá al lugar X del cliente a retirar $X"*.

El Solicitante:
1. **Especiales → Mis solicitudes → + Nuevo** (o desde el subgrid "Asignaciones" de una cuenta existente).
2. En **"CM | Solicitud de Especial"** completa:
   - **Cuenta** → busca una **existente** (lookup).
   - **Tipo = Especial** · **Estado de aprobación = Pendiente aprobacion**.
   - **Retiro NIO** y **Retiro USD** → el monto a cobrar (viven en la solicitud, NO en la cuenta).
   - **Motivo**.
3. **Guarda** → queda Pendiente, va al Supervisor. (`Estado` nace **Activa** por default.)

> ⚠️ **Si la cuenta/cliente NO existe:** el Solicitante le **pide al Admin** que lo cree — el Solicitante ya
> no tiene permiso de crear/editar datos maestros (Read only). El Admin usa los forms de Cliente/Cuenta
> (con sus subgrids) para cargarlo.
>
> 💡 Opcional (reducir clics): una **regla de negocio** que fije `Tipo = Especial` al abrir el form.

### 2.2 Seguridad de formulario
Form **"CM | Solicitud de Especial"** → habilitar **solo** el rol `SR | CM | Solicitante`.

---

## 3. Especiales — Supervisor

### 3.1 Comando "Aprobar" (tabla **Asignaciones**, Formulario principal)
> ⚠️ Sin `Set`: un solo `Patch` leyendo `Modified By` directo (el registro ya existe, es un update).
```powerfx
If(
    IsBlank(Self.Selected.Item.Agente),
    Notify("Asigná un localizador antes de aprobar.", NotificationType.Error),
    Patch('CM | TBL | Asignacions', Self.Selected.Item,
        {
            'Estado de aprobación': 'Estado de aprobación (Asignaciones)'.'Aprobado',
            'Resuelto por': Self.Selected.Item.'Modified By'
        });
    Notify("Especial aprobada y asignada.", NotificationType.Success)
)
```

### 3.2 Comando "Rechazar" (tabla **Asignaciones**, Formulario principal)
```powerfx
If(
    IsBlank(Self.Selected.Item.'Motivo de rechazo'),
    Notify("Escribí el motivo de rechazo.", NotificationType.Error),
    Patch('CM | TBL | Asignacions', Self.Selected.Item,
        {
            'Estado de aprobación': 'Estado de aprobación (Asignaciones)'.'Rechazada',
            'Resuelto por': Self.Selected.Item.'Modified By'
        });
    Notify("Especial rechazada.", NotificationType.Success)
)
```
**Visibilidad de ambos:**
```powerfx
Self.Selected.Item.'Estado de aprobación' = 'Estado de aprobación (Asignaciones)'.'Pendiente aprobacion'
```

### 3.3 Seguridad de formulario
Form **"CM | Aprobación de Especial"** → habilitar **solo** el rol `SR | CM | Supervisor`.

---

## 4. Admin

### 4.1 Comando "Cerrar" (tabla **Asignaciones** — Incobrables/Especiales, Formulario principal)
```powerfx
If(
    IsBlank(Self.Selected.Item.Motivo),
    Notify("Escribí el motivo de cierre.", NotificationType.Error),
    Patch('CM | TBL | Asignacions', Self.Selected.Item, { Estado: false, 'Fecha de cierre': Now() });
    Notify("Asignación cerrada.", NotificationType.Success)
)
```
**Visibilidad:** `Self.Selected.Item.Estado = true`

### 4.2 Ligar usuario ↔ agente
El Admin abre el form de **Agente** y setea el campo **Usuario** (`sanic_cm_ag_usuarioid`). El form default sirve.

---

## 5. Áreas de la app (todas juntas — en el diseñador de "App - CM - Backoffice")

Agregá páginas (**+ Agregar página → Vista y formulario basados en tabla**) y organizá la navegación:

```
📥 Cobranza        → Por transcribir · Transcritas hoy
⭐ Especiales       → Clientes · Mis solicitudes · Aprobaciones pendientes
🛠️ Administración   → Incobrables abiertas · Especiales activas · Diarias (revisar carga) ·
                       Monitoreo de carga · Agentes y usuarios
```

> Cada subárea apunta a la **vista** que ya creé por API (buscala por nombre). **Guardar → Publicar.**

---

## 6. Roles a usuarios de prueba + compartir

- Asigná los roles a usuarios de prueba (uno por perfil): `SR | CM | Solicitante`, `SR | CM | Supervisor`,
  `SR | CM | Backoffice`, `SR | CM | Admin` (siempre + **Usuario básico**).
- **Compartí** la app "App - CM - Backoffice" con esos usuarios.

---

## 7. Pruebas (con usuarios REALES, no admin)

- [ ] **Backoffice:** cola filtrada, barra minimalista, subgrid Pagos sin "New/Add", comentario/geo RO, "Marcar como grabada" llena estado+fecha+grabadopor y aparece en "Transcritas hoy".
- [ ] **Solicitante:** crear cliente nuevo con direcciones/teléfonos/productos, pedir Especial → aparece en "Mis solicitudes" como Pendiente.
- [ ] **Supervisor:** ve la Pendiente en "Aprobaciones pendientes", asigna localizador y Aprueba (o Rechaza con motivo).
- [ ] **Admin:** cierra una Incobrable/Especial con motivo; liga un usuario a un agente.

---

## 8. 🔴 Bloque DIFERIDO — "carga + owner" (cuando tengas el import)

Estas tres van juntas y **no se pueden probar sin la carga real**:

1. **La carga (import Smelter).**
2. **Comp 14 — automatización de owner.** Setea `owner = localizador`. Sin esto: NO hay aislamiento real del
   localizador (prueba E5) NI las Especiales aprobadas bajan al celular.
3. **Reconciliación de Diarias desaparecidas (DEC-018).** Página personalizada (preview + umbral + cierre en
   lote). Depende de `sanic_cm_as_ultimacarga`, que solo se llena con el import → no se puede construir/probar
   antes.

> Hasta que corra la carga y comp 14, el backoffice de **oficina funciona** (transcripción, solicitudes,
> aprobaciones, cierres), pero el **puente al campo** (Especial aprobada → celular) y el **aislamiento entre
> localizadores** quedan pendientes de ese bloque.
</content>
