# Componente: Tabla `sanic_cm_tbl_agente` (Agente = Localizador)

> **Fase 1 — Modelo de datos · Componente 04 (maestra padre + extensión operativa).**
> Tabla que se **construye desde cero** en esta solución. Cumple dos papeles: es un **catálogo** que el
> **import** puebla (por el campo `Agente` del CSV) **y** lleva la **columna operativa** que la liga a la
> identidad AAD del localizador.
>
> Materializa: **DEC-006** (agente = localizador), **DEC-015** (seguridad Moderado).
> La **configuración** del import que la puebla (mappings, alt key, orden) se detalla en la Fase 2 / en
> [`../../cobranza-import-config.md`](../cobranza-import-config.md).

---

## 1. Objetivo

Representar al **Agente** (= localizador) y conectarlo con su **usuario AAD** (`systemuser`), para:
1. **Setear el owner** de las asignaciones (el localizador ve "lo suyo").
2. Que la app de campo resuelva *"mi cartera"*.

> El "Agente" del CSV y el "Localizador" que usa la app **son la misma persona** (DEC-006). No hay tabla
> `localizador` separada.

---

## 2. Esquema de columnas (tabla nueva, completa)

| Nombre lógico | Nombre físico | Tipo | Req. | Origen / descripción |
|---------------|---------------|------|:----:|----------------------|
| Nombre | `sanic_cm_ag_nombre` | Text | ✔ | **Primary name.** Lo puebla el import desde el CSV (`Agente`). |
| Usuario | `sanic_cm_ag_usuarioid` | Lookup → `systemuser` | — | **Operativo.** Identidad AAD del localizador. Lo setea el Admin (no viene del CSV). |
| Activo | `sanic_cm_ag_activo` | Yes/No (def. Sí) | — | Marca si el localizador está operativo (filtra inactivos). |

> Es una tabla **chica**: su identidad de negocio es el **nombre**. Todo lo demás del cobro vive en otras
> tablas vinculadas por lookup.

---

## 3. Alternate Key

- **`sanic_cm_pk_agente_nombre`** (sobre `sanic_cm_ag_nombre`) — el import deduplica/upsertea el agente por su nombre. **Debe quedar `Active`** antes
  de importar (regla de oro del import).

> ⚠️ El vínculo `sanic_cm_ag_usuarioid` **NO** va en la alt key (es un lookup; las alt keys sobre lookup son
> frágiles — misma lección del import, `cobranza-import-config.md` §3).

---

## 4. Relaciones

| Relación | Cardinalidad | Columna | Dónde se define |
|----------|:------------:|---------|-----------------|
| `systemuser` → `sanic_cm_tbl_agente` | 1:N | `sanic_cm_ag_usuarioid` | **Acá.** |
| `sanic_cm_tbl_agente` → `sanic_cm_tbl_cuenta` | 1:N | `sanic_cm_ct_agenteid` | Componente 08 (cuenta). |
| `sanic_cm_tbl_agente` → `sanic_cm_tbl_asignacion` | 1:N | `sanic_cm_as_agenteid` | Componente 12. |
| `sanic_cm_tbl_agente` → `sanic_cm_tbl_interaccion` | 1:N | `sanic_cm_in_agente` | Componente 10. |

---

## 5. Pasos de construcción

1. Crear la tabla `sanic_cm_tbl_agente` (User/Team owned), **Primary name = `sanic_cm_ag_nombre`**.
2. Crear la columna **`sanic_cm_ag_usuarioid`** (Lookup → `systemuser`). **No** requerida (un agente del CSV puede
   crearse antes de vincularse a un usuario).
3. Crear **`sanic_cm_ag_activo`** (Yes/No, default `Sí`).
4. Crear la **Alternate Key** `sanic_cm_pk_agente_nombre` (sobre `sanic_cm_ag_nombre`) y dejarla **`Active`**.
5. Crear la **vista de control** *"Agentes sin usuario vinculado"* (`sanic_cm_ag_usuarioid` = vacío) — salvaguarda
   del gotcha de seguridad (Componente 01, §9.1).
6. **Proceso de alta** (configuración, no del CSV): por cada localizador, el Admin abre el agente y setea
   `sanic_cm_ag_usuarioid` = su usuario AAD. Documentar quién mantiene esto.

---

## 6. Gotchas

- **6.1** El import crea/actualiza el agente **por nombre**; si el nombre cambia en el origen, el import crea
  **otro** agente (no lo renombra) → habría que re-vincular `sanic_cm_ag_usuarioid`. Acordar con el área que el
  `Agente` del CSV sea **estable**.
- **6.2** Sin `sanic_cm_ag_usuarioid`, las asignaciones de ese agente quedan **sin owner** → el localizador no las ve
  (Componente 01, §9.1). De ahí la vista de control del paso 5.

---

## 7. Checklist

- [ ] Tabla `sanic_cm_tbl_agente` creada (primary name `sanic_cm_ag_nombre`).
- [ ] Alt key `sanic_cm_pk_agente_nombre` **Active**.
- [ ] Columna `sanic_cm_ag_usuarioid` (Lookup → systemuser) y `sanic_cm_ag_activo`.
- [ ] Vista "Agentes sin usuario vinculado" disponible.
- [ ] Cada localizador activo con su `sanic_cm_ag_usuarioid` seteado.

---

## 8. Dependencias

- **Depende de:** nada (tabla raíz; el import la puebla en Fase 2).
- **Habilita:** el seteo de owner en `sanic_cm_tbl_asignacion` (Comp. 12) y la resolución de cartera en la app.
- **Relacionada:** Componente 01 (seguridad), que usa `sanic_cm_ag_usuarioid` como ancla.
