# Componente: Seguridad Row-Level y Vínculo Agente↔Usuario

> **Fase 1 — Modelo operativo · Componente 01 (cimiento).**
> Define **cómo se protege y se ve** cada tabla, y el vínculo que hace posible "cada localizador ve solo su
> cartera". Es el **primer** componente porque condiciona a todos los demás.
>
> Materializa: **DEC-004** (empleados AAD), **DEC-006** (agente = localizador), **DEC-014** (5 roles),
> **DEC-015** (estrategia Moderado). Ver [`../decisiones/registro-de-decisiones.md`](../decisiones/registro-de-decisiones.md).

---

## 1. Objetivo y alcance

- **Objetivo:** que cada usuario vea y haga **exactamente** lo que su rol permite, y que el **localizador**
  gestione **solo su cartera**, sin pagar la complejidad de un aislamiento estricto (DEC-015).
- **Alcance:** modelo de propiedad (ownership), los **5 roles de seguridad** y su matriz de acceso, el
  vínculo `agente → systemuser`, y cómo se setea el owner de las asignaciones.
- **Fuera de alcance:** el esquema detallado de columnas de cada tabla (va en sus componentes propios).

---

## 2. Conceptos base de Dataverse (para entender el porqué)

> Si no entendés estos 3 conceptos, el resto no cierra. Acá no hay atajos.

1. **Owner:** cada registro tiene un **dueño** (un usuario o un team). Es la pieza sobre la que se apoya la
   seguridad por fila.
2. **Access Levels de un Security Role** (por tabla y por privilegio C/R/U/D…):
   - **User (Basic):** solo los registros que el usuario **owna** o que le **compartieron**.
   - **Business Unit (Local):** los de su unidad de negocio.
   - **Organization (Global):** **todos** los registros de la tabla.
3. **El acceso es la UNIÓN de los roles** del usuario, y para `User-level` **depende del owner**. Por eso el
   "quién ve qué" se resuelve combinando **rol** (qué nivel) + **owner** (de quién es el registro).

**Traducción a nuestra estrategia (Moderado):**
- Operativas → **User-level** + owner = localizador ⇒ ve **solo lo suyo**.
- Maestros → **Read Organization** ⇒ **lee el padrón** completo (la app filtra su cartera).

---

## 3. El vínculo Agente ↔ Usuario (DEC-006)

El "Agente" del CSV **es** el localizador. Para conectarlo con su identidad AAD:

- En `sanic_cm_tbl_agente` se agrega la columna **`sanic_cm_ag_usuarioid`** — **Lookup → `systemuser`**.
- **Quién lo setea:** el **Admin**, **una vez por localizador**, al darlo de alta (dato de configuración).
- **Para qué sirve:**
  1. **Setear el owner** de la asignación cuando el import la crea (sección 6).
  2. Que la app de campo resuelva *"mi cartera"* = asignaciones cuyo **owner soy yo**.

> ⚠️ **Regla:** un agente **activo sin** `sanic_cm_ag_usuarioid` vinculado generará asignaciones **sin owner
> correcto** → el localizador **no las verá**. Ver gotcha 9.1.

---

## 4. Modelo de propiedad (ownership) por tabla

| Grupo | Tablas | Tipo de propiedad | Owner típico |
|-------|--------|-------------------|--------------|
| **Operativas** | `sanic_cm_tbl_asignacion`, `sanic_cm_tbl_interaccion`, `sanic_cm_tbl_pago` | **User/Team owned** | El **localizador** (su `systemuser`). |
| **Maestros** | `sanic_cm_tbl_cliente`, `sanic_cm_tbl_cuenta`, `sanic_cm_tbl_direccion`, `sanic_cm_tbl_telefono`, `sanic_cm_tbl_cargadiaria` | User/Team owned, pero **se leen a nivel Org** | Cuenta de servicio del import (irrelevante para la lectura). |
| **Catálogos** | `sanic_cm_tbl_producto`, `sanic_cm_tbl_agente`, `sanic_cm_tbl_fiador` | User/Team owned, **Read Org** | Servicio/Admin. |
| **Config import** | Smelter (Template, Mappings, Import Job, Log) | — | Admin. |

---

## 5. Los 5 roles y su matriz de acceso

> **Leyenda:** `C`=Create · `R`=Read · `U`=Update · `D`=Delete. Entre paréntesis el **nivel**:
> `(U)`=User (solo propios) · `(O)`=Organization (todos). `—` = sin acceso.

| Tabla | SR \| CM \| Localizador | SR \| CM \| Backoffice | SR \| CM \| Solicitante | SR \| CM \| Supervisor | SR \| CM \| Admin |
|-------|:-----------:|:----------:|:-----------:|:----------:|:-----:|
| `sanic_cm_tbl_cliente` | R(O) | R(O) | C R U (O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_cuenta` | R(O) | R(O) | C R U (O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_direccion` | R(O) | R(O) | C R U (O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_telefono` | R(O) | R(O) | C R U (O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_producto` / `sanic_cm_tbl_fiador` | R(O) | R(O) | R(O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_agente` | R(O) | R(O) | R(O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_cargadiaria` | R(O) | R(O) | R(O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_asignacion` | R(U) · U(U)¹ | R(O) | C R U (O) | R(O) · U(O)² | C R U D (O) |
| `sanic_cm_tbl_interaccion` | C R U (U) | R(O) · U(O)³ | R(O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_pago` | C R U (U) | R(O) | R(O) | R(O) | C R U D (O) |
| `sanic_cm_tbl_actualizaciondatos` | C R U (U) | R(O) | R(O) | — | C R U D (O) |
| Config import (Smelter) | — | R(O)⁴ | — | — | C R U D (O) |

**Notas:**
1. El localizador **actualiza solo el orden/prioridad** de **sus** asignaciones (no crea ni borra; las crea
   el import o el Supervisor).
2. El Supervisor actualiza asignaciones para **aprobar/rechazar** y **asignar el localizador** (Especiales).
3. El Backoffice actualiza la interacción para **asignar el código del core** y marcar `Grabada en core`.
4. El Backoffice **lee** el Import Job para monitorear (opcional); disparar el import es del **Admin**.

> **Datos maestros:** el localizador, Backoffice y Supervisor **no editan** maestros — solo el **Solicitante**
> (para Especiales, DEC-013) y el **Admin**. El localizador, si detecta que un dato del cliente cambió,
> **lo marca** con el booleano *"¿Actualiza info del cliente?"* en la interacción; **no** lo edita.

---

## 6. Cómo se materializa "ver solo su cartera"

```
  IMPORT (CSV) ──crea/actualiza──► sanic_cm_tbl_asignacion
                                        │  plugin/flow: ownerid = agente.sanic_cm_ag_usuarioid
                                        ▼
                              owner = el localizador (systemuser)
                                        │
          Localizador (rol, asignacion R User-level) ── ve SOLO las que owna ─┐
                                        │                                      │
                                        ▼                                      ▼
                            join → sanic_cm_tbl_cuenta (Read Org) ── datos para cobrar
                                        ▼
                        sanic_cm_tbl_interaccion / sanic_cm_tbl_pago (owned por él, User-level)
```

- El localizador ve **sus asignaciones** porque las **owna** (User-level), sin filtros frágiles de UI.
- Lee los **datos de la cuenta/cliente** porque los maestros son **Read Org** (Moderado).
- Sus **interacciones y pagos** son de él (User-level) — nadie más los ve, salvo los roles de oficina (Org).
- **Estadísticas/ranking:** se calculan agregando **sus** interacciones/pagos (DEC-011).

---

## 7. Pasos de construcción

> Orden recomendado. Cada paso es verificable antes de pasar al siguiente.

### A. Identidad y estructura
1. **Business Unit:** usar la **BU raíz** de la organización (Moderado **no** requiere BUs por zona).
2. Verificar que cada **localizador** tenga **usuario AAD** habilitado en el entorno con **licencia Power Apps**.

### B. Vínculo Agente↔Usuario (DEC-006)
3. En `sanic_cm_tbl_agente`, crear la columna **`sanic_cm_ag_usuarioid`** (Lookup → `systemuser`).
4. **Alta de localizador:** el Admin setea `sanic_cm_tbl_agente.sanic_cm_ag_usuarioid` = usuario AAD del localizador
   (una vez por persona). Es **dato de configuración**, documentarlo.
5. Crear una **vista de control** *"Agentes sin usuario vinculado"* (filtro `sanic_cm_ag_usuarioid` is null) para
   evitar el gotcha 9.1.

### C. Roles de seguridad
6. Crear los **5 security roles**: `SR | CM | Localizador`, `SR | CM | Backoffice`, `SR | CM | Solicitante`, `SR | CM | Supervisor`, `SR | CM | Admin`.
7. Aplicar a cada rol la **matriz de la sección 5** (tabla por tabla, privilegio por privilegio).
8. **Asignar** cada rol a sus usuarios (localizadores → `SR | CM | Localizador`, etc.). Un usuario puede tener más de
   un rol si el negocio lo permite, pero **por defecto van separados** (segregación de funciones, DEC-014).

### D. Propiedad de registros (el corazón del aislamiento)
9. Configurar `sanic_cm_tbl_asignacion`, `sanic_cm_tbl_interaccion`, `sanic_cm_tbl_pago` como **User/Team owned** (no Org-owned).
10. **Setear el owner de la asignación** (no es nativo del import — requiere automatización):
    - **Diaria/Incobrable (import):** un **plugin** o **Power Automate flow** en `Create`/`Update` de
      `sanic_cm_tbl_asignacion` que setea `ownerid = sanic_cm_tbl_agente.sanic_cm_ag_usuarioid` (resuelto por el Agente del CSV).
    - **Especial:** al **aprobar**, el Supervisor asigna el localizador → el mismo mecanismo setea el owner.
11. `sanic_cm_tbl_interaccion` / `sanic_cm_tbl_pago`: el **owner es el creador** (el localizador, automático al crearlos
    desde la app). No requiere lógica extra.

### E. Filtrado en la app
12. **App de campo:** la cartera = `sanic_cm_tbl_asignacion` **owned por el usuario actual** (nativo). De ahí se
    navega a la cuenta y datos maestros (Read Org).
13. **App de oficina:** vistas por rol (Backoffice → "interacciones pendientes de grabar"; Supervisor →
    "Especiales pendientes de aprobar"; etc.).

---

## 8. Por qué NO Business Units por localizador

Se podría intentar una BU por localizador para aislar por `User-level`→`BU`. **No conviene:** las BUs son
una estructura **pesada y rígida** (jerarquía, equipos, reasignaciones masivas al mover gente). El
**ownership** ya resuelve el aislamiento por persona con mucho menos peso. BUs **solo** se justificarían en
la estrategia **"Por zona"** (descartada en DEC-015).

---

## 9. Gotchas y casos borde

- **9.1 Agente sin usuario vinculado:** la asignación queda con owner = servicio → el localizador **no la
  ve**. **Mitigación:** vincular **antes** del import (paso 5); el plugin del paso 10 debe **registrar/alertar**
  si `sanic_cm_ag_usuarioid` viene null.
- **9.2 Reasignación de Diaria:** el import (1×/día) cambia el Agente → el plugin **re-setea** el owner. Una
  reasignación **manual** intradía cambia el owner a mano; el import del **día siguiente** puede volver a
  setearlo (política DEC-012).
- **9.3 Maestros legibles fuera de la cartera:** en Moderado, un localizador puede consultar maestros vía
  API/Advanced Find aunque la **app** solo muestre su cartera. **Aceptado** (empleados internos, DEC-015).
- **9.4 Cliente entre carteras:** resuelto por **Read Org** en maestros (no requiere owner único). Esto es,
  literalmente, **la razón** por la que elegimos Moderado.
- **9.5 POA:** al **no** usar *sharing*, la tabla `PrincipalObjectAccess` **no crece** → sin deuda de
  performance. Beneficio directo de la estrategia.

---

## 10. Checklist de verificación (probar con 2 usuarios)

- [ ] Localizador **A** ve sus asignaciones; **NO** ve las de Localizador **B**.
- [ ] Localizador lee los datos de cuenta/cliente de su cartera (y técnicamente el padrón).
- [ ] Localizador **NO** puede editar datos maestros.
- [ ] Interacciones/pagos creados por **A** **no** son visibles para **B** (sí para Backoffice/Supervisor/Admin).
- [ ] Backoffice ve **todas** las interacciones y puede asignar código + marcar `Grabada en core`.
- [ ] Solicitante crea cliente + asignación Especial; **no** puede aprobarla.
- [ ] Supervisor aprueba la Especial y asigna el localizador → el **owner** queda en ese localizador y **él la ve**.
- [ ] Admin ve y administra todo, incluido el import.
- [ ] Vista "Agentes sin usuario vinculado" funciona y está vacía antes de importar.

---

## 11. Dependencias y próximos componentes

- **Depende de:** que las tablas **se construyan en esta solución** (todas desde 0 — operativas en Fase 1;
  maestras según sus componentes). Este doc define su **seguridad**, no su esquema. La matriz de §5 se
  **aplica a medida que cada tabla se crea**.
- **Habilita:** el diseño de esquema de `sanic_cm_tbl_agente` (con `sanic_cm_ag_usuarioid`), `sanic_cm_tbl_interaccion`,
  `sanic_cm_tbl_pago`, `sanic_cm_tbl_asignacion` — cada uno en su componente.
- **Automatización del owner:** detallada en **[Componente 14](14-automatizacion-owner-asignacion.md)** —
  **Power Automate** (se descartó el low-code plugin Power Fx por ser *preview / no producción*). Setea
  `ownerid = sanic_cm_as_agenteid.sanic_cm_ag_usuarioid` en Create/Update de `sanic_cm_tbl_asignacion`.
