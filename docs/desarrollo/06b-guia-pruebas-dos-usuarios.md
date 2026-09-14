# Guía de Aceptación (UAT) — Pruebas con DOS usuarios 👥

> **Qué es esto.** Una prueba de **aceptación de usuario** (UAT): dos localizadores reales, cada uno con su
> celular, corren un **día de cobranza de mentira** con datos controlados. No es el checklist técnico de
> Studio — es la prueba de que la app **sirve en la calle** y, sobre todo, de que **cada cobrador ve SOLO su
> cartera** (el corazón de la seguridad).
>
> **Diferencia con el [doc 06](06-plan-de-pruebas-localizador.md).** El 06 es QA técnico (89 casos, campos
> `sanic_*`, Monitor) para vos en Studio. **Este es de campo, con lenguaje de cobrador, y de a dos.** Corré
> el 06 primero (que la app no esté rota); después esta guía (que la app se **acepta** para producción).
>
> **Regla de oro.** Ninguna prueba marcada 🔴 puede quedar en falla. Si una 🔴 falla → **no se sale a
> producción** hasta arreglarla. Se entiende, ¿no? 💪

---

## 0. Los dos protagonistas

Para que sea fácil de seguir, les ponemos nombre:

| | **Cobrador A — "Ana"** | **Cobrador B — "Beto"** |
|---|---|---|
| Usuario Dataverse | ana@... (licencia + rol Localizador) | beto@... (licencia + rol Localizador) |
| Dispositivo | Celu 1 (app Power Apps instalada) | Celu 2 (app Power Apps instalada) |
| Cartera | Sus clientes asignados (owner = Ana) | Sus clientes asignados (owner = Beto) |

> **Importante:** Ana y Beto deben tener **carteras DISTINTAS y sin solapamiento**. Ese es el punto de la
> prueba de aislamiento (§7). Si comparten clientes, no se puede validar.

---

## 1. Preparación (la hace el admin, ANTES de convocar a los dos)

Sin esto, la prueba no es válida. Marcá cada casillero:

- [ ] **1.1** Ana y Beto tienen usuario en el entorno, con **licencia** y el **rol de seguridad Localizador**
  ([comp 15](../referencia/componentes/15-roles-de-seguridad.md)).
- [ ] **1.2** Cada uno tiene un registro **`CM | TBL | Agente`** con su email (`sanic_cm_ag_usuarioid`) y
  `sanic_cm_ag_activo` = Sí. (Sin esto, `gblAgente` sale vacío y el saludo/agente falla.)
- [ ] **1.3** **Asignaciones repartidas:** ≥ 3 clientes para Ana y ≥ 3 **distintos** para Beto, con **owner =
  el localizador correspondiente**. 🔴 Este es el pilar: si el owner quedó en SYSTEM, el aislamiento NO
  funciona. Verificá que la [automatización de owner (comp 14)](../referencia/componentes/14-automatizacion-owner-asignacion.md)
  corrió, o seteá el owner a mano para la prueba.
- [ ] **1.4** Datos ricos en AMBAS carteras (para que los casos de éxito tengan de todo):
  - [ ] ≥ 1 cuenta **dual-moneda** (retiro NIO **y** USD) por cobrador.
  - [ ] ≥ 1 cuenta de cada **tipo** (Diaria, Incobrable, Especial).
  - [ ] ≥ 1 cuenta con **mora alta** (días > 90 o nivel ≥ 4).
  - [ ] ≥ 1 **cliente con 2+ cuentas / productos** (para la prueba multi-producto, §5).
  - [ ] Teléfonos y direcciones (Casa/Trabajo) cargados por cliente.
- [ ] **1.5** **Perfil offline** configurado ([guía 13](../guias/13-perfil-offline-rendimiento.md)) y **Data
  row limit = 2000**.
- [ ] **1.6** App **publicada** con la última versión y **compartida** con Ana y Beto.
- [ ] **1.7** En **cada celu**: app Power Apps instalada, login del usuario correcto, y **primera
  sincronización del perfil offline hecha** (con señal, una vez).

> ⚠️ **Todo lo de offline, sync y persistencia de preferencias SOLO es fiel en el celular.** En el navegador
> `SaveData`/`LoadData` no hacen nada y no hay modo offline real. Esta guía se corre **en los teléfonos**.

---

## 2. Cómo se corre

1. Ana y Beto, cada uno con su celu, hacen los casos **E1 a E6** en paralelo (salvo §7 y §9, que son
   coordinados).
2. Cada uno marca **OK / Falla** y, si falla, **anota qué vio** (una foto ayuda).
3. Al final, el admin junta las dos hojas y firma el **veredicto** (§10).

---

## 3. Caso E1 — Primer arranque y personalización (features nuevas) 🔴

> Valida modo Día/Noche, los 4 tamaños de letra, y que **las preferencias se guardan al reabrir** (lo que
> arreglamos esta sesión). **Cada uno en su celu.**

- **E1.1 · Arranca en Día** — Abrí la app por primera vez. **Esperado:** arranca en modo **claro** (Día),
  todo legible. [ ] OK [ ] Falla — ______
- **E1.2 · Cambiar tamaño de letra** — Tocá el botón **"Aa"** → elegí **"Muy grande"**. **Esperado:** TODA
  la app agranda el texto; nada se corta ni se sale de pantalla. [ ] OK [ ] Falla — ______
- **E1.3 · Modo Noche** — En la misma hoja "Aa", tocá **🌙 Noche**. **Esperado:** toda la app pasa a oscuro,
  con buen contraste (se lee cómodo). [ ] OK [ ] Falla — ______
- **E1.4 · PERSISTENCIA (la clave) 🔴** — Con Noche + Muy grande puestos, **cerrá la app por completo**
  (deslizá para cerrarla, no la dejes en segundo plano) y **volvé a abrirla**. **Esperado:** abre
  **directamente en Noche y Muy grande**, sin que tengas que volver a configurarlo. [ ] OK [ ] Falla — ______

> Si E1.4 falla en el celu: confirmá que el teléfono tiene la **última versión instalada** (no un build
> viejo) — fue la causa la última vez. Reinstalá/actualizá y reintentá.

---

## 4. Caso E2 — Un día de cobranza (camino feliz completo) 🔴

> El flujo real de punta a punta: llegar, encontrar al cliente, gestionar, cobrar, ver el resultado.

- **E2.1 · Veo MI cartera** — Abrí la app. **Esperado:** en Cartera aparecen **tus** clientes (los de Ana en
  el celu de Ana; los de Beto en el de Beto). El total "A cobrar hoy" muestra **C$ y US$**. [ ] OK [ ] Falla — ______
- **E2.2 · La dirección manda** — Mirá una tarjeta de cliente. **Esperado:** se ve clara la **dirección**
  (protagonista), nombre, CIF, saldo y la pastilla de mora. [ ] OK [ ] Falla — ______
- **E2.3 · Buscar** — Escribí parte del nombre de un cliente tuyo. **Esperado:** filtra al toque, el
  contador se actualiza. [ ] OK [ ] Falla — ______
- **E2.4 · Contacto** — Entrá al cliente → tocá **Llamar** y **Mapa**. **Esperado:** abre el discador con el
  número / Google Maps con la dirección. [ ] OK [ ] Falla — ______
- **E2.5 · Registrar interacción con pago** 🔴 — Entrá a una cuenta → **Registrar** → escribí un comentario,
  prendé **"Aplica pago"**, poné monto + número de recibo → **Guardar**. **Esperado:** aviso de éxito y la
  interacción aparece en el detalle. [ ] OK [ ] Falla — ______
- **E2.6 · Se refleja en Inicio y Pagos** — Andá a **Inicio**. **Esperado:** "Cobrado hoy" sumó tu pago;
  Pagos del día lista el recibo. [ ] OK [ ] Falla — ______

---

## 5. Caso E3 — Interacción multi-producto (feature nueva) 🔴

> Valida grabar el **mismo comentario** en varios productos del cliente de una sola vez, con el **candado**:
> si prendés pago o actualización de datos, queda **un solo producto**.

Elegí un cliente con **2+ productos/cuentas**. Entrá a una de sus cuentas → **Registrar**.

- **E3.1 · Aparece el selector de productos** — **Esperado:** arriba se ve la lista de productos del cliente;
  el actual viene **tildado** y marcado **"ACTUAL"**. [ ] OK [ ] Falla — ______
- **E3.2 · Multi-selección solo del comentario** — Escribí "No estaba en casa" y **tildá 2 o 3 productos**
  (sin prender pago ni datos). **Esperado:** cada producto se tilda/destilda **individualmente** (NO se
  seleccionan todos juntos). El botón dice **"Guardar en N producto(s)"**. [ ] OK [ ] Falla — ______
- **E3.3 · Guarda N interacciones** — **Guardar**. **Esperado:** aviso de éxito; en cada uno de esos
  productos queda la MISMA interacción. [ ] OK [ ] Falla — ______
- **E3.4 · Candado con pago 🔴** — Volvé a Registrar en ese cliente. Prendé **"Aplica pago"**. **Esperado:**
  el selector se **bloquea a UN solo producto** (el actual); no te deja multi-seleccionar. Lo mismo con
  **"Actualizar datos"**. [ ] OK [ ] Falla — ______
- **E3.5 · El producto actual siempre incluido** — En modo multi, intentá destildar el producto **ACTUAL**.
  **Esperado:** no se puede quitar (siempre va incluido). [ ] OK [ ] Falla — ______

---

## 6. Caso E4 — Trabajo offline + sincronización 🔴 (en la calle, sin señal)

> El escenario real del cobrador: sin datos en el barrio. **Cada uno en su celu.**

- **E4.1 · Poné el celu en modo avión** (simula "sin señal"). [ ]
- **E4.2 · Leer sin señal** — Navegá Cartera → Cliente → Detalle. **Esperado:** **se ve todo** (está en el
  dispositivo por el perfil offline). [ ] OK [ ] Falla — ______
- **E4.3 · Capturar sin señal** 🔴 — Registrá una interacción con pago. **Esperado:** **guarda igual**, con
  aviso de éxito. [ ] OK [ ] Falla — ______
- **E4.4 · Queda pendiente** — Andá a Sincronización. **Esperado:** la captura figura **"sin Nº aún"**
  (pendiente de subir). [ ] OK [ ] Falla — ______
- **E4.5 · Sincroniza al volver la señal** 🔴 — **Sacá el modo avión.** Esperá / tocá sincronizar.
  **Esperado:** la captura **sube** y obtiene su número; el estado pasa a **Sincronizado**. [ ] OK [ ] Falla — ______
- **E4.6 · Nada se pierde ni se duplica** — Revisá que la interacción/pago quedó **una sola vez** y con sus
  datos. [ ] OK [ ] Falla — ______

---

## 7. Caso E5 — AISLAMIENTO entre usuarios 🔴🔴 (la prueba estrella — COORDINADA)

> **Esta es LA prueba.** Demuestra que la seguridad row-level funciona: Ana **no puede** ver ni tocar la
> cartera de Beto. Si esto falla, **no se sale a producción bajo ningún concepto** — es un problema de
> confidencialidad de datos del banco. Se hace con **los dos juntos, comparando pantallas.**

- **E5.1 · Carteras distintas** 🔴 — Ana y Beto abren su Cartera al mismo tiempo y **comparan**.
  **Esperado:** listas de clientes **completamente distintas**; **ningún cliente en común**. [ ] OK [ ] Falla — ______
- **E5.2 · Conteos coherentes** — El contador de cada uno coincide con su cantidad real de clientes
  asignados (no con el total del banco). **Esperado:** Ana ve ~3-N suyos, no los 957 del entorno. [ ] OK [ ] Falla — ______
- **E5.3 · Buscar al cliente del otro** 🔴 — Ana busca por nombre a un cliente **de Beto**. **Esperado:**
  **no aparece** (no está en su dispositivo/permiso). Beto hace lo simétrico. [ ] OK [ ] Falla — ______
- **E5.4 · Cobrado de hoy separado** — Ana registra un pago; Beto registra otro. Cada uno mira su Inicio.
  **Esperado:** cada "Cobrado hoy" refleja **solo lo propio**; no se mezclan. [ ] OK [ ] Falla — ______
- **E5.5 · (Admin) Verificación en Dataverse** — El admin confirma que las interacciones de Ana tienen
  `sanic_cm_in_agente` = Ana y las de Beto = Beto; los owners de asignación son correctos. [ ] OK [ ] Falla — ______

> Si E5.1 o E5.3 fallan (uno ve datos del otro): casi seguro el **owner de las asignaciones quedó en SYSTEM**
> o el **perfil offline no acota**. Volvé a [comp 14](../referencia/componentes/14-automatizacion-owner-asignacion.md)
> y [guía 13](../guias/13-perfil-offline-rendimiento.md) — es config, no código.

---

## 8. Caso E6 — Reordenar la cartera + persistencia

- **E6.1 · Inicializar orden** — Corré "Inicializar orden" una vez. **Esperado:** cada cliente queda con un
  número único. [ ] OK [ ] Falla — ______
- **E6.2 · Subir/bajar** — Mové un cliente con ▲▼. **Esperado:** intercambia lugar con el vecino; el bloque
  del cliente (todas sus cuentas) se mueve entero. [ ] OK [ ] Falla — ______
- **E6.3 · Persiste** — Salí de la pantalla, cerrá y reabrí la app. **Esperado:** el orden se mantuvo. [ ] OK [ ] Falla — ______

---

## 9. Coordinación final entre los dos

Con Ana y Beto juntos, revisen en voz alta:

- [ ] ¿Cada uno vio **solo lo suyo** en todo momento? (lo más importante)
- [ ] ¿La app respondió **rápido** en ambos celus (sin lag al buscar/scrollear/reordenar)?
- [ ] ¿Ambos pudieron trabajar **sin señal** y sincronizar al volver?
- [ ] ¿Las preferencias (tema + tamaño) **quedaron guardadas** en los dos teléfonos?
- [ ] ¿Alguno vio algún texto cortado, botón montado, o color ilegible en Día **o** Noche?

---

## 10. Hoja de resultados y veredicto

**Por usuario:**

| Caso | Ana (Celu 1) | Beto (Celu 2) |
|------|:---:|:---:|
| E1 · Personalización + persistencia 🔴 | [ ] OK [ ] ✗ | [ ] OK [ ] ✗ |
| E2 · Día de cobranza 🔴 | [ ] OK [ ] ✗ | [ ] OK [ ] ✗ |
| E3 · Multi-producto 🔴 | [ ] OK [ ] ✗ | [ ] OK [ ] ✗ |
| E4 · Offline + sync 🔴 | [ ] OK [ ] ✗ | [ ] OK [ ] ✗ |
| E6 · Reorden | [ ] OK [ ] ✗ | [ ] OK [ ] ✗ |

**Coordinadas:**

| Caso | Resultado |
|------|:---:|
| **E5 · Aislamiento entre usuarios 🔴🔴** | [ ] OK [ ] ✗ |
| §9 · Coordinación final | [ ] OK [ ] ✗ |

**Veredicto:**
[ ] **ACEPTADA** para producción (todas las 🔴 en OK)  ·  [ ] **Necesita arreglos** (listá abajo)

---

## 11. Bitácora de fallas

Anotá cada falla para que el desarrollador la arregle:

| # | Caso (E-XX) | Usuario | Qué pasó (con foto si hay) | Severidad |
|---|-------------|---------|----------------------------|-----------|
| 1 | | | | 🔴 / 🟡 |
| 2 | | | | |
| 3 | | | | |

> **Regla de cierre:** si hay una sola 🔴 en falla —sobre todo E5 (aislamiento)— la app **no se acepta**.
> Se arregla, se re-corre el caso que falló, y recién ahí se firma. Es así de fácil. 💪
