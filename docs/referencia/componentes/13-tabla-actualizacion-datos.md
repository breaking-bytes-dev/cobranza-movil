# Componente: Tabla `sanic_actualizaciondatos`

> **Fase 1 — Modelo de datos · Componente 13 (operativa, hija de la interacción).**
> Captura los **nuevos valores de contacto** que el cliente pide actualizar durante una interacción
> (teléfonos, direcciones, correo). Luego el Backoffice/Solicitante los **aplica** a los datos maestros (o
> los transcribe al core).
>
> Se vincula con el booleano `sanic_actualizacliente` de la interacción (Comp. 10): cuando el localizador lo
> marca en `Sí`, crea un registro de esta tabla.

---

## 1. Objetivo

Que el localizador **registre en campo** qué datos de contacto cambió el cliente, sin tener que editar él los
datos maestros (no tiene permiso, DEC-015). Es una **bandeja de cambios pedidos**, que la oficina procesa.

> **Por qué tabla aparte y no columnas en la interacción:** la interacción se crea en cada contacto; la
> mayoría **no** trae cambios. Una tabla hija que solo existe cuando hay algo que actualizar mantiene la
> tabla núcleo limpia.

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Quién lo llena / descripción |
|---------------|---------------|------|:----:|------------------------------|
| Número | `sanic_numero` | Autonumber (`ACT-{SEQNUM:6}`) | auto | **Primary name.** Server-assigned al sincronizar. |
| Interacción | `sanic_interaccionid` | Lookup → `sanic_interaccion` | ✔ | El vínculo. Una actualización **pertenece** a una interacción. |
| Teléfono casa | `sanic_telefonocasa` | Text | — | Nuevo número de casa (si cambió). |
| Teléfono celular | `sanic_telefonocelular` | Text | — | Nuevo número de celular (si cambió). |
| Teléfono trabajo | `sanic_telefonotrabajo` | Text | — | Nuevo número de trabajo (si cambió). |
| Dirección casa | `sanic_direccioncasa` | Text | — | Nueva dirección de domicilio (si cambió). |
| Dirección EC | `sanic_direccionec` | Text | — | Nueva dirección de estado de cuenta (si cambió). |
| Dirección trabajo | `sanic_direcciontrabajo` | Text | — | Nueva dirección de trabajo (si cambió). |
| Correo electrónico | `sanic_correo` | Text (formato Email) | — | Nuevo correo (si cambió). |
| Propietario | `ownerid` | Owner (system) | ✔ | = el **localizador** (creador). User-level. |

> **Todos los campos de dato son opcionales:** el localizador completa **solo lo que cambió**. Un campo
> vacío = ese dato no se toca.

---

## 3. Alternate Key

- **Ninguna.** Se crea en la app (no por import) → usa su **GUID**. Es un hecho ligado a una interacción.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna |
|----------|:------------:|---------|
| `sanic_interaccion` → `sanic_actualizaciondatos` | 1:N (en la práctica 1:1) | `sanic_interaccionid` |

> Se modela como lookup (1:N) por simplicidad; lo normal es **un** registro de actualización por interacción.

---

## 5. Seguridad (DEC-015)

- **Propiedad:** User/Team owned; owner = el **localizador** (lo crea él).
- **Localizador:** `C R U` (User-level) — registra los cambios de su interacción.
- **Backoffice / Solicitante:** `R` (Org) — leen para **aplicar** los cambios a los datos maestros / core.
- **Admin:** `C R U D` (Org). Ver matriz Comp. 01 §5 (fila agregada).

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_actualizaciondatos`, **User/Team owned**. Primary name autonumber `ACT-{SEQNUM:6}`.
2. Crear el lookup `sanic_interaccionid` (Lookup → `sanic_interaccion`, **requerido**).
3. Crear los 7 campos de dato de §2 (3 teléfonos, 3 direcciones — todos Text; `sanic_correo` con **formato
   Email**).
4. **Regla de negocio** (su md): al crear este registro, marcar la interacción padre
   `sanic_actualizacliente = Sí` (y viceversa, ofrecer crearlo cuando se marca el booleano).

---

## 7. Gotchas

- **7.1 Offline:** se crea sin señal junto a la interacción; sincroniza con ella. El `sanic_numero` lo
  asigna el servidor al sincronizar.
- **7.2 Es una solicitud, no el dato vivo:** este registro **no** modifica `cliente`/`telefono`/`direccion`.
  Es la **oficina** quien aplica el cambio (el localizador no tiene permiso de escritura sobre maestros).
- **7.3 Campos capture-only vs aplicables al master (DECIDIDO):**
  - **Capture-only** (viven **solo** acá; se transcriben al core, **no** se aplican a tablas master): el
    **correo** (`cliente` no tiene email) y el **teléfono de trabajo** (el master `telefono` solo tiene
    Celular/Casa).
  - **Aplicables al master:** `telefonocasa`/`telefonocelular` → `telefono`; `direccioncasa`/`ec`/`trabajo` →
    `direccion`.

---

## 8. Checklist

- [ ] Tabla User/Team owned, primary name autonumber.
- [ ] Lookup requerido a `sanic_interaccion`.
- [ ] Los 7 campos de dato (3 teléfonos, 3 direcciones, correo formato Email).
- [ ] El localizador solo ve **sus** registros (User-level); la oficina los lee (Org).
- [ ] Fila agregada a la matriz de seguridad (Comp. 01 §5).

---

## 9. Dependencias

- **Depende de:** `sanic_interaccion` (Comp. 10), seguridad (Comp. 01).
- **Pendiente (otra fase):** la **regla** que la enlaza con el booleano de la interacción y el **proceso de
  aplicación** de los cambios a los maestros/core tendrán su markdown de lógica de negocio.
