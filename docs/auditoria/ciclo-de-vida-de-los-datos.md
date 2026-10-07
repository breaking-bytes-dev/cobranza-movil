# Ciclo de vida de los datos: Cobranza Móvil

Para cada tabla de la solución `cm_core_cobranzamovil`, este documento responde:

- quién **crea** las filas;
- quién las **cambia** y qué columnas;
- **qué estados** atraviesan;
- **cómo terminan**: cierre lógico o borrado;
- qué **automatismos** del servidor intervienen (plugins, flow, cascadas);
- cómo viajan al **teléfono**.

Estado de DEV al **2026-10-06**. Lo marcado "verificado" se consultó en vivo: cascadas, claves, privilegios y perfil offline.

> Idea central: en esta solución **casi nada se borra**. Las asignaciones se **cierran** (`[AS] Estado = false`), las
> interacciones pasan a **Grabado**, y el histórico (carga diaria, pagos) se acumula. Borrar solo puede el rol Admin por la
> API, y lo hacen los scripts de QA o de migración.

---

## 1. Vista global

```mermaid
flowchart LR
    CSV[/"CSV diario del banco"/] --> SM["Smelter<br/>(corre como SYSTEM)"]
    SM -->|"crea / actualiza"| CL[(Cliente)]
    SM --> DT[(Dirección / Teléfono)]
    SM --> AG[(Agente<br/>solo nombre)]
    SM --> CT[(Cuenta<br/>número enmascarado)]
    SM --> CP[(PAN de cuentas<br/>bóveda)]
    SM --> CD[(Carga diaria<br/>histórico)]
    SM -->|"Diaria / Incobrable<br/>(Especial: nace Pendiente)"| AS[(Asignación)]

    SOL(["Solicitante"]) -->|"pide Especial<br/>Pendiente, sin agente"| AS
    SUP(["Supervisor"]) -->|"aprueba + agente<br/>o rechaza"| AS
    ADM(["Admin"]) -->|"vincula usuario"| AG
    ADM -->|"cierra: Cierres /<br/>Reconciliación"| AS

    AS -->|"agente con usuario<br/>y no es Especial sin aprobar"| FL{{"Flow<br/>Cambio de Owner"}}
    FL -->|"dueño = cobrador<br/>comparte lectura"| CT
    FL --> CL

    CT -.->|"perfil offline<br/>(solo lo compartido)"| TEL["Teléfono del cobrador"]
    AS -.->|"solo activas"| TEL
    TEL -->|"crea (también sin señal)"| IN[(Interacción<br/>Pendiente)]
    TEL --> PG[(Pago)]
    TEL --> AD[(Actualización<br/>de datos)]

    BO(["BackOffice"]) -->|"transcribe al core<br/>con el número completo"| IN
    CP -.->|"solo BackOffice<br/>(y el cobrador, lo suyo)"| BO
```

**El día típico, en orden:**

1. **Carga (madrugada).** Smelter procesa el CSV y hace upsert en este orden: cliente, agente, dirección y teléfono, cuenta,
   bóveda, asignación. Además deja la foto del día en carga diaria. El número de tarjeta completo solo se escribe en la
   bóveda; el plugin **PanGuard** rechaza un número en claro en cualquier otra columna vigilada.
2. **Reparto.** Por cada asignación con agente, el **flow** pasa el dueño al usuario del cobrador y le **comparte** (solo
   lectura) la cuenta, el cliente y el fiador. Por cascada se comparten también la bóveda, las direcciones y los
   teléfonos. Si el agente no tiene usuario, no pasa nada (DAT-07, abierto).
3. **Especiales.**
   - El Solicitante pide una Especial: queda Pendiente y sin agente.
   - El Supervisor la aprueba eligiendo cobrador, o la rechaza.
   - El plugin **AprobacionEspecial** impide que otro rol lo haga, y el flow solo comparte las aprobadas.
4. **Calle.** El teléfono baja su cartera por el perfil offline: asignaciones activas y 90 días de historial. El cobrador
   registra interacciones, pagos y actualizaciones de datos, aunque no tenga señal.
5. **Sincronización.** El servidor numera (IN-, PG-, AD-). El plugin **CuentaDesdeInteraccion** completa la cuenta y el N°
   de interacción del pago.
6. **Oficina.** El BackOffice transcribe cada interacción al core (con el número completo de la bóveda) y la marca
   **Grabada**.
7. **Cierre.**
   - Reconciliación cierra las Diarias que no vinieron en la última carga.
   - Cierres cierra las Incobrables y las Especiales.
   - El flow revoca el compartido.
   - En la próxima sincronización, la asignación desaparece del teléfono.

---

## 2. Relaciones y qué pasa al borrar (verificado)

```mermaid
flowchart TD
    CL[Cliente] -->|"Delete: CASCADE · Share: no"| CT[Cuenta]
    CL -->|"Delete: CASCADE · Share: CASCADE"| DT[Dirección / Teléfono]
    CL -->|"Delete: CASCADE"| CD[Carga diaria]
    CT -->|"Delete: CASCADE · Share: CASCADE"| CP[PAN de cuentas]
    CT -->|"Delete: CASCADE · Share: no"| AS[Asignación]
    CT -->|"Delete: quita el vínculo"| IN[Interacción]
    CT -->|"Delete: quita el vínculo"| PG[Pago]
    IN -->|"Delete: CASCADE · Share: CASCADE"| PG
    IN -->|"Delete: CASCADE · Share: CASCADE"| AD[Actualización de datos]
    AG[Agente] -->|"Delete: RESTRICT"| AS
    AG -->|"Delete: RESTRICT"| IN
    FI[Fiador] -->|"Delete: RESTRICT"| CT
```

Cómo se lee:
- **Share CASCADE:** compartir el padre comparte los hijos. Por eso al compartir el cliente, el cobrador ve sus
  direcciones y teléfonos; y al compartir la cuenta, ve su número en la bóveda.
- **Delete CASCADE:** borrar el padre borra los hijos. **Borrar un cliente borra sus cuentas, sus asignaciones y su
  bóveda.** Nadie lo hace desde las apps, pero el rol Admin puede por la API (DAT-18).
- **RESTRICT:** no se puede borrar un agente o un fiador que tenga hijos.
- **Quita el vínculo:** si se borra una cuenta, sus interacciones y pagos quedan sin cuenta, pero no se borran.

**Reparent** (qué pasa cuando un registro se guarda apuntando a un padre):
- Con `Cascade`, todos los que tienen acceso al padre reciben lectura **heredada** del hijo. Esto pasa también cuando la
  carga vuelve a escribir el mismo padre.
- **Hasta el 2026-10-06** estaba en `Cascade` en Cliente→Cuenta y Cuenta→Asignación. Cada carga diaria le daba al
  cobrador lectura heredada de **todas las cuentas de sus clientes**, con su bóveda (número completo) y sus asignaciones,
  aunque no estuvieran asignadas a él. Era el defecto 2 de la prueba integral.
- Desde esa fecha está en `NoCascade` en **Cliente→Cuenta, Cliente→Carga diaria y Cuenta→Asignación**
  (`scripts/seguridad/paso1_cascadas_auditoria.py`).
- Sigue en `Cascade`, a propósito, en Cuenta→Bóveda, Cliente→Teléfono/Dirección e Interacción→Pago/Actualización. Ahí la
  audiencia es la misma que la del compartido que el diseño quiere.

---

## 3. Asignación: la tabla con más vida

La asignación tiene **dos ejes independientes**:
- **Estado:** Activa o Cerrada (`[AS] Estado`).
- **Aprobación:** Borrador, Pendiente, Aprobado, Rechazada. Solo tiene sentido para las **Especiales**.

### 3.1 Especial

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Pendiente: Solicitante pide (sin agente)
    [*] --> Pendiente: Smelter (CSV), con o sin agente · default de la columna
    Borrador --> Aprobado: solo por la API (la cola de Aprobaciones no las muestra)
    Pendiente --> Aprobado: Supervisor aprueba + agente
    Pendiente --> Rechazada: Supervisor rechaza (motivo)
    Aprobado --> Cerrada: Admin cierra (Gestión completada / Ya no aplica / Solicitud errónea)
    Rechazada --> Cerrada: Admin cierra
    Pendiente --> Cerrada: Admin cierra
    Cerrada --> Pendiente: Solicitante la vuelve a pedir (reactiva el MISMO registro)
    Aprobado --> Aprobado: el flow pasa el dueño al cobrador y comparte la cuenta
```

**Reglas del servidor:**
- Solo Supervisor, Admin o la identidad de servicio pueden aprobar, rechazar o poner agente (plugin **AprobacionEspecial**).
- El flow **no comparte** una Especial que no esté Aprobada.
- La carga de Smelter (SYSTEM, sin roles de CM) puede crear Especiales con agente, pero quedan sin compartir hasta que el
  Supervisor las aprueba.

**Especiales del CSV:**
- La plantilla de Smelter **no manda** el estado de aprobación, así que Dataverse pone el valor por defecto de la columna
  (**Pendiente**) al crear. Verificado: las asignaciones QA creadas por la API sin ese campo quedaron Pendientes.
- Así, una Especial nueva del CSV entra sola en la cola del Supervisor. Como el valor no se manda, la carga diaria **no
  lo pisa** después.
- Las **47 Especiales en Borrador** de DEV son de la carga mock de junio, anterior al cambio del default. La cola de
  Aprobaciones solo muestra Pendientes, así que esas 47 no llegan al Supervisor.

**Lo que ve el cobrador:** solo las **Aprobadas y activas**.

**Detalles:**
- Una **Rechazada** sigue Activa y bloquea volver a pedir la misma cuenta hasta que el Admin la cierra (DAT-14).
- Volver a pedir una Cerrada **reactiva el mismo registro**, por la clave alterna cuenta + tipo.

### 3.2 Diaria e Incobrable

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Activa: Smelter (CSV) crea o actualiza · Última carga = hoy
    Activa --> Activa: viene otra vez en el CSV (upsert, Última carga = hoy)
    Activa --> Cerrada: Diaria que no vino en la última carga → Reconciliación (cierre en lote, "fuera de cartera")
    Activa --> Cerrada: Incobrable → Admin en Cierres (Recuperada / Baja / Fuera de cartera)
    Cerrada --> Cerrada: reaparece en el CSV → NO se reactiva (DAT-10, abierto)
```

**Freno del cierre en lote:** la app no deja cerrar si las desaparecidas superan el **30%** o si no hay carga de hoy. Es un
freno de la app, no del servidor.

**Cambios del cobrador:** solo el orden de su ruta (`[AS] Orden`), también sin conexión.

**Abiertos:**
- **DAT-10:** una Diaria cerrada que vuelve en el CSV no se reactiva.
- **DAT-15:** la reclasificación de Diaria a Incobrable no está implementada.
- **DAT-29:** no hay pantalla de reasignación manual.
- **S-11:** el Supervisor puede escribir cualquier columna.

### 3.3 Qué pasa con el acceso del cobrador

```mermaid
sequenceDiagram
    participant A as Asignación
    participant F as Flow Cambio de Owner
    participant C as Cuenta / Cliente / Fiador
    participant T as Teléfono
    A->>F: cambia agente, estado o aprobación (con agente)
    alt activa y no es Especial sin aprobar, agente con usuario
        F->>A: dueño = usuario del agente
        F->>C: GrantAccess (lectura) al cobrador
        F->>C: RevokeAccess al dueño anterior (si no tiene otra asignación activa de esa cuenta)
        C-->>T: baja en la próxima sincronización
    else cerrada
        F->>C: RevokeAccess al dueño (si no tiene otra asignación activa que lo justifique)
        C-->>T: desaparece en la próxima sincronización
    else Especial sin aprobar
        F-->>A: no hace nada
    end
```

---

## 4. Interacción

```mermaid
stateDiagram-v2
    direction LR
    [*] --> EnTelefono: cobrador guarda (sin señal)
    [*] --> Pendiente: cobrador guarda (con señal)
    EnTelefono --> Pendiente: sincroniza · el servidor asigna IN-xxxx
    Pendiente --> Grabado: BackOffice transcribe al core (código, fecha, "grabado por")
    Grabado --> [*]
```

**Quién crea:** solo el cobrador. Escribe la cuenta, el agente, el comentario, el GPS (6 decimales) y la fecha de captura,
que la pone el teléfono.

**Grabado es terminal solo por costumbre.** Nada en el servidor impide volver a transcribir, ni que el cobrador edite su
interacción después (**S-9, abierto**).

**En el teléfono:** últimos 90 días (por fecha de modificación).

**Si se borra:** se borran sus pagos y su actualización de datos (cascada).

---

## 5. Pago

```mermaid
flowchart LR
    T["Cobrador guarda<br/>monto + moneda + recibo<br/>Fecha del pago = ahora"] --> P[(Pago)]
    P -->|"al crear en el servidor"| PL["Plugin CuentaDesdeInteraccion<br/>completa cuenta y N° de interacción"]
    P -->|"clave alterna activa"| K["recibo + cuenta + moneda<br/>no se repiten"]
    P -->|"BackOffice lo ve al transcribir<br/>y en el arqueo"| BO[Transcritas hoy]
```

- **Sin estados:** un pago no se aprueba ni se cierra; es un hecho registrado.
- **Validaciones:**
  - **App:** monto mayor que 0, recibo obligatorio (hasta 30 caracteres), confirmación si supera el saldo vencido y bloqueo
    de doble toque.
  - **Servidor:** la clave alterna impide duplicar un recibo en la misma cuenta y moneda.
- **Abierto (S-9):** el cobrador puede **editar o reasignar** su pago después de grabarlo. Es el hueco más sensible:
  toca la plata.
- **En el teléfono:** últimos 90 días.

---

## 6. Actualización de datos

```mermaid
flowchart LR
    T["Cobrador: teléfonos,<br/>direcciones, correo"] --> AD[(Actualización<br/>de datos)]
    AD -->|"plugin ActualizacionConInteraccion:<br/>exige interacción con cuenta"| IN[Interacción]
    AD -->|"BackOffice ve antes / después<br/>y lo tipea a mano en el core"| CORE[(Core del banco)]
    AD -. "nadie lo aplica" .-> DT[Dirección / Teléfono de CM]
```

- **Sin estados ni cierre (DAT-19, abierto).** No hay "aplicada" o "pendiente". El BackOffice la transcribe junto con la
  interacción, y los datos maestros de CM se renuevan recién con la próxima carga del banco, si el core los actualizó.
- **Para saber de qué cliente es:** se sigue el camino actualización → interacción → cuenta → CIF. El plugin garantiza que
  ese camino exista.

---

## 7. Datos maestros (los crea la carga)

| Tabla | Crea | Cambia | Fin | Teléfono | Notas |
|---|---|---|---|---|---|
| **Carga diaria** | Smelter (una foto por día, idempotente) | Nadie | Se acumula; sin retención definida | No | Monitoreo cuenta las de hoy; Reconciliación compara contra la última |
| **Cliente** | Smelter; Admin en Maestros (anti-duplicado de CIF) | Smelter en cada carga; Admin (nombre y datos) | No se borra | Sí, lo compartido | Lo que edita el Admin lo pisa la carga siguiente; las apps leen el nombre de la cuenta (DAT-26) |
| **Cuenta** | Solo Smelter | Smelter (saldos, mora, datos del cliente) | No se borra | Sí, lo compartido | Número enmascarado `****1234` |
| **PAN de cuentas** (bóveda) | Solo Smelter | Nadie | Se borra solo con la cuenta | Sí, lo compartido (por cascada) | Lee **BackOffice** (todo) y el cobrador (lo suyo, "Ver número", 30 s) |
| **Dirección / Teléfono** | Smelter; Admin agrega | Smelter (clave CIF + tipo) | No se borra | Sí (por cascada del cliente) | No hay editar ni borrar en las apps |
| **Fiador** | Smelter | Smelter | No se borra (RESTRICT) | No | El flow lo comparte, pero no está en el perfil |
| **Agente** | Smelter (solo el nombre) | Admin vincula o desvincula el usuario, activa o desactiva; el plugin **EmailDesdeUsuario** pone el correo = UPN en minúsculas | Se desactiva | Sí | Sin usuario, el flow no reparte sus asignaciones (DAT-07) |

### 7.1 Agente

```mermaid
stateDiagram-v2
    direction LR
    [*] --> SinUsuario: Smelter crea (solo nombre)
    SinUsuario --> ConUsuario: Admin vincula (correo = UPN, plugin)
    ConUsuario --> SinUsuario: Admin desvincula (correo vacío)
    ConUsuario --> ConUsuario: Admin cambia el usuario
    ConUsuario --> Inactivo: Admin desactiva
    Inactivo --> ConUsuario: Admin activa
```

---

## 8. Brechas abiertas del ciclo de vida

| # | Brecha | Efecto |
|---|---|---|
| DAT-07 / DAT-08 | El flow no reparte si el agente no tiene usuario, ni cuando se le vincula después | Asignaciones que no llegan a ningún teléfono hasta el próximo cambio |
| DAT-10 | Una Diaria cerrada que reaparece en el CSV no se reactiva | El cobrador no la ve aunque el banco la volvió a mandar |
| DAT-14 | Una Especial Rechazada sigue Activa | Bloquea volver a pedirla hasta que el Admin la cierra |
| DAT-15 | No hay reclasificación de Diaria a Incobrable | — |
| DAT-18 | Borrar un cliente o una cuenta borra en cascada | Solo el Admin, por la API; ninguna app lo hace |
| DAT-19 | La actualización de datos no tiene ciclo | Nadie la marca como aplicada |
| DAT-20 | No se puede crear una cuenta desde el backoffice | Todo pasa por la carga |
| DAT-26 | Lo que el Admin edita en un cliente lo pisa la carga | — |
| S-9 | El cobrador edita lo ya grabado | Montos, fechas y GPS modificables después |

Fuentes: `docs/auditoria/contrato-de-datos.md` (secciones 3.x y DAT-xx, del 2026-10-03), `localizador-bugs-codigo.md`
(cierres posteriores), `fase2-pan-diseno.md` (bóveda), `scripts/seguridad/paso2_flow.py` (flow),
`scripts/pan/paso7_perfil_offline.py` (perfil), `plugins/` (reglas del servidor), y consultas en vivo a DEV del 2026-10-06.
