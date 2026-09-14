# Checklist de pendientes — Cobranza Móvil

> Estado consolidado (2026-07-29). Ordenado por **prioridad real**: qué bloquea hoy y qué depende de qué.
> **Regla de dependencia clave:** el bloque **A destraba el C**. Sin la carga real corriendo bien (A) +
> Comp 14, el bloque C no se puede ni probar → **A es la prioridad absoluta**. El **B** se puede hacer en
> paralelo (no depende de la data).

---

## 🔴 A. Carga de datos — *el blocker de ahora*

- [ ] **Resolver el error de Smelter** (`errorcategory` Picklist vs EntityReference) → alinear versión del
      plugin ↔ esquema de la tabla `sanic_tbl_smlt_importjoblog`. Es del lado del **producto Smelter**, no de
      la config de Cobranza. Ver detalle del diagnóstico abajo.
- [ ] **Confirmar si el import de hoy aterriza data o hace rollback** (chequear `createdon` reciente de
      cuentas/clientes). Define si el error es fatal o solo cosmético.
- [ ] **Agregar usuarios como miembros de los teams** DEV (Admin y Carga, Localizadores, Backoffice,
      Solicitantes, Supervisores). Hoy `CM · Admin y Carga · DEV` tiene **1 solo miembro** (User 10).
- [ ] **Compartir la app** con esos teams.

> **Diagnóstico del error Smelter (verificado read-only por API):** NO es permisos (es "Incorrect type",
> error de TIPO, no de acceso). NO es el template (las tablas destino tienen data → los mappings funcionan:
> clientes 958, cuentas 1002, direcciones 2696, etc.). El culpable exacto: la columna
> `sanic_smlt_ijl_errorcategory` es **Picklist** pero el plugin le escribe un **EntityReference**. La alt key
> del log (`id`+`rownumber`+`targettable`, todos String/Integer) NO es el problema.

---

## 🟡 B. Pasos de diseñador — backoffice *(guía 15, se hacen en el portal)*

- [ ] Comando **"Marcar como grabada"** (Interacciones) + barra minimalista
- [ ] Comandos **"Aprobar" / "Rechazar"** (Asignaciones — Supervisor)
- [ ] Comando **"Cerrar"** (Asignaciones — Admin)
- [ ] **Áreas de la app**: 📥 Cobranza · ⭐ Especiales · 🛠️ Administración
- [ ] **Seguridad de formularios**: Solicitud → Solicitante, Aprobación → Supervisor
- [ ] **Reglas de negocio de bloqueo**: post-aprobación y post-cierre (que no se modifique)
- [ ] *(Opcional)* Regla que fije **Tipo = Especial** al abrir el form

---

## 🔵 C. Bloque diferido — *depende de que la carga real corra primero*

- [ ] **Comp 14 — automatización de owner** (`owner = localizador`). Sin esto: NO hay aislamiento entre
      localizadores **ni** bajan las Especiales aprobadas al celular.
- [ ] **Perfil offline** (guía 13): configurar + sincronizar en un celu de prueba
- [ ] **Reconciliación DEC-018** (página personalizada; depende de `sanic_cm_as_ultimacarga`, que solo se
      llena con el import)
- [ ] **Canvas**: que el localizador vea **Retiro NIO/USD + Notas** del Especial

---

## 🟢 D. Pruebas UAT *(guía 06b, con usuarios reales — no admin)*

- [ ] Flujo **Backoffice** (cola filtrada, barra, "Marcar como grabada")
- [ ] Flujo **Solicitante** (crear Especial con retiro → "Mis solicitudes")
- [ ] Flujo **Supervisor** (aprobar/rechazar con localizador)
- [ ] Flujo **Admin** (cerrar con motivo, ligar usuario ↔ agente)
- [ ] **E5 — aislamiento entre localizadores** *(necesita Comp 14)*

---

## ⚪ E. Higiene / opcional

- [ ] Trimear `prvCreateWorkflow` / `prvWriteWorkflow` del rol Admin (dejarlo 100% scopeado a la solución)
- [ ] Cuando exista PRD: **replicar los 5 teams** con sufijo `· PRD`

---

## Referencias

- [Guía 15 — Pasos finales del backoffice (diseñador)](guias/15-pasos-finales-backoffice-disenador.md)
- [Guía 14 — Estrategia de carga de datos](guias/14-estrategia-carga-datos.md)
- [Guía 06b — Pruebas con dos usuarios (UAT)](desarrollo/06b-guia-pruebas-dos-usuarios.md)
- [Config del import Smelter](referencia/cobranza-import-config.md)
