# Componente: Tabla `sanic_interaccion`

> **Fase 1 — Modelo de datos · Componente 10 (operativa) — el núcleo de la captura.**
> Cada interacción del localizador con un cliente (comentario libre + si hubo pago + geo + estado de
> transcripción al core).
>
> Materializa: **DEC-002** (write-back: el código lo asigna el backoffice), **DEC-008** (geo),
> **DEC-009** (nombre `interaccion`), **DEC-010** (el pago es entidad aparte), **DEC-015** (User-level).

---

## 1. Objetivo

Registrar, **desde la app de campo (offline)**, lo que pasó en cada visita/contacto: el **comentario** del
localizador, si **aplica pago**, si **hay que actualizar datos** del cliente, **dónde** ocurrió (geo), y
llevar el **estado de transcripción** al core (lo completa el Backoffice).

---

## 2. Esquema de columnas

| Nombre lógico | Nombre físico | Tipo | Req. | Quién lo llena / descripción |
|---------------|---------------|------|:----:|------------------------------|
| Número | `sanic_numero` | Autonumber (`INT-{SEQNUM:6}`) | auto | **Primary name.** Server-assigned (ver gotcha 7.1). |
| Cuenta | `sanic_cuentaid` | Lookup → `sanic_cuenta` | ✔ | localizador — la cuenta gestionada. |
| Agente | `sanic_agenteid` | Lookup → `sanic_agente` | ✔ | localizador (auto, = él mismo). |
| Comentario | `sanic_comentario` | Multiline Text | ✔ | localizador — **texto libre** de la interacción. |
| Fecha de interacción | `sanic_fechainteraccion` | DateTime | ✔ | localizador (auto al registrar, en campo). |
| ¿Actualiza info cliente? | `sanic_actualizacliente` | Yes/No (def. No) | — | localizador — **marca** que hay datos a actualizar (no los edita). |
| ¿Aplica pago? | `sanic_aplicapago` | Yes/No (def. No) | — | localizador — si hubo cobro (los montos van en `sanic_pago`). |
| Latitud | `sanic_latitud` | Decimal (5 dec.) | — | localizador (auto, GPS). DEC-008. |
| Longitud | `sanic_longitud` | Decimal (5 dec.) | — | localizador (auto, GPS). DEC-008. |
| Estado transcripción | `sanic_estadotranscripcion` | Choice (ver §5) | ✔ | sistema/backoffice. Default `Pendiente`. |
| Código del core | `sanic_codigocore` | Text | — | **Backoffice** — el código con que lo grabó en el core (DEC-002). |
| Fecha grabada en core | `sanic_fechagrabacore` | DateTime | — | Backoffice — cuándo lo transcribió. |
| Grabada por | `sanic_grabadaporid` | Lookup → `systemuser` | — | Backoffice — quién la grabó. |
| Propietario | `ownerid` | Owner (system) | ✔ | = el **localizador** (creador). Base del User-level. |

> **No hay montos acá** (DEC-010): viven en `sanic_pago`. El booleano `sanic_aplicapago` es solo la bandera.

---

## 3. Alternate Key

- **Ninguna.** La interacción se crea en la app (no por import) → usa su **GUID**. Es un **hecho fechado**
  (append-only); no se deduplica por clave de negocio.

---

## 4. Relaciones

| Relación | Cardinalidad | Columna |
|----------|:------------:|---------|
| `sanic_cuenta` → `sanic_interaccion` | 1:N | `sanic_cuentaid` |
| `sanic_agente` → `sanic_interaccion` | 1:N | `sanic_agenteid` |
| `systemuser` → `sanic_interaccion` | 1:N | `sanic_grabadaporid` |
| `sanic_interaccion` → `sanic_pago` | 1:N | `sanic_interaccionid` *(Componente 11)* |
| `sanic_interaccion` → `sanic_actualizaciondatos` | 1:N | `sanic_interaccionid` *(Componente 13)* |

---

## 5. Choices

**`sanic_estadotranscripcion`** (write-back, DEC-002):
| Valor | Código | Significado |
|-------|:------:|-------------|
| Pendiente | 1 | Aún no grabada en el core (default). |
| Grabada en core | 2 | El Backoffice la transcribió (con código + fecha + usuario). |

> Diseño **expandible**: si más adelante hace falta un estado intermedio ("En proceso"), se agrega un valor
> sin romper lo existente (DEC-002).

---

## 6. Pasos de construcción

1. Crear la tabla `sanic_interaccion`, **User/Team owned** (Componente 01, §4).
2. Configurar el **Primary name** como **Autonumber** `INT-{SEQNUM:6}`.
3. Crear las columnas de la §2 con sus tipos.
4. Crear el Choice `sanic_estadotranscripcion` (§5) con default `Pendiente`.
5. Crear los lookups (`sanic_cuentaid`, `sanic_agenteid`, `sanic_grabadaporid`).
6. **Auditoría:** habilitar auditoría en `sanic_codigocore`, `sanic_estadotranscripcion`,
   `sanic_grabadaporid` (rastro del write-back).
7. **Reglas de negocio** (a detallar en su md): si `sanic_aplicapago = Sí`, exigir al menos un `sanic_pago`;
   al setear `sanic_codigocore`, pasar `estadotranscripcion` a `Grabada en core` y sellar fecha/usuario.

---

## 7. Gotchas

- **7.1 Autonumber + offline:** el `sanic_numero` lo asigna el **servidor al sincronizar**, no en el
  dispositivo. Esto **no** es problema de colisión (la numeración anti-colisión de DEC-007 es **solo** para el
  recibo digital). En modo offline, mostrar la interacción por fecha/comentario hasta que sincronice.
- **7.2 Anti-doble-carga:** dos Backoffice no deben grabar la misma interacción. El filtro natural es
  `estadotranscripcion = Pendiente`; al grabarla pasa a `Grabada en core` (con auditoría de quién).
- **7.3 Geo opcional:** si el dispositivo niega ubicación, `latitud/longitud` quedan vacías — **no** bloquear
  la captura por eso.

---

## 8. Checklist

- [ ] Tabla creada User/Team owned, primary name autonumber.
- [ ] Todas las columnas de §2 y el Choice de §5.
- [ ] Lookups a cuenta, agente y systemuser (grabada por).
- [ ] Auditoría activa en los campos del write-back.
- [ ] El localizador (User-level) solo ve **sus** interacciones; Backoffice las ve todas (Componente 01).

---

## 9. Dependencias

- **Depende de:** `sanic_cuenta` (Comp. 08) y `sanic_agente` (Comp. 04) — **tablas que se construyen en
  esta solución** — y la seguridad (Comp. 01).
- **Habilita:** `sanic_pago` (Comp. 11) y las estadísticas/gamificación (DEC-011).
