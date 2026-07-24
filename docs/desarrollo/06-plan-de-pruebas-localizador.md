# Plan de Pruebas — App del Localizador 🧪

> **Para qué.** Verificar, de forma **reproducible**, que la app quedó bien: que **funciona** (cada pantalla y
> flujo), que es **rápida** (las optimizaciones que hicimos de verdad pegan), que anda **offline**, y que no se
> rompió nada de lo que arreglamos en el camino (**regresión**).
>
> **Cómo usarlo.** Recorré sección por sección. Cada prueba tiene **Pasos** y **Esperado**; marcá el resultado.
> No avances a la siguiente sección si una crítica (marcada 🔴) falla.
>
> **Herramienta estrella — Monitor.** Para las pruebas de performance abrí el **Monitor**:
> *Herramientas avanzadas → Supervisar (Monitor)* → "Reproducir en la aplicación". Te lista **cada operación**
> (consultas, escrituras a Dataverse) con su **duración**. Es la única forma de medir en serio (no "a ojo").

---

## 0. Precondiciones — datos de prueba

Para que las pruebas sean válidas, necesitás en Dataverse (en el entorno de la app):

- [ ] Tu usuario ligado a un **`sanic_agente`** (`sanic_usuario` = vos, `sanic_activo` = Sí).
- [ ] **≥ 3 clientes** asignados a vos, con **≥ 6 cuentas** en total (varios clientes con **2+ cuentas**).
- [ ] **≥ 1 cuenta dual-moneda** (con `sanic_retironio` **y** `sanic_retirousd` > 0).
- [ ] **≥ 1 cuenta de cada tipo**: Diaria, Incobrable, Especial (`sanic_asignacion.sanic_tipo`).
- [ ] **≥ 1 cuenta con mora alta** (`sanic_diasmora` > 90 **o** `sanic_nivelmora` ≥ 4).
- [ ] Cada cliente con **teléfonos** (Casa/Celular) y **direcciones** (Casa/EC/Trabajo) en `sanic_telefono` / `sanic_direccion`.
- [ ] **≥ 1 interacción de hoy con pago** (para Inicio/Pagos) y **≥ 1 interacción de hoy sin sincronizar** (`sanic_numero` en blanco) para la pantalla Sync.
- [ ] Perfil **offline** configurado y **primera sincronización** hecha (probar en el **player móvil**, no solo en el navegador).

> ⚠️ Las pruebas de **offline** y de **autonumber vacío** (sync) **solo son fieles en el player móvil**. El
> navegador de Studio no entra en modo offline real ni muestra el ícono de globo.

---

## 1. Arranque y configuración 🔴

| # | Prueba | Pasos | Esperado | ✓/✗ |
|---|--------|-------|----------|-----|
| **T-01** | OnStart corre | Abrí la app | Sin errores; la Cartera carga | |
| **T-02** | Agente resuelto | (Studio) en una etiqueta temporal poné `gblAgente.sanic_nombre` | Muestra **tu** nombre, no vacío | |
| **T-03** | Tokens de color | Header verde, badges con sus colores | Verde `#0F6B6B`, Diaria azul, Incobrable ámbar, Especial violeta | |
| **T-04** | Fuente del tema | Cualquier texto | Se ve en Segoe UI (no la default vieja) | |
| **T-05** | 11 tablas conectadas | Panel **Datos** | Están las 11 `sanic_*` | |

---

## 2. Cartera — visualización 🔴

- **T-10 · Total bimoneda del header**
  - **Pasos:** mirá "A cobrar hoy".
  - **Esperado:** muestra **C$ y US$ en paralelo** (`C$ X · US$ Y`); si una moneda es 0, **no** la muestra.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-11 · Modo cliente: nombre + cantidad de cuentas + badge**
  - **Pasos:** con el toggle en **👤 Cliente**, mirá una tarjeta de un cliente con 2+ cuentas.
  - **Esperado:** nombre del cliente, "N cuenta(s)", el badge del primer tipo, total bimoneda del cliente.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-12 · Direcciones en la card (modo cliente)**
  - **Pasos:** mirá la tarjeta de cliente.
  - **Esperado:** aparecen **🏠 dirección casa** y **💼 dirección trabajo**; si falta una, muestra "—" (no error).
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-13 · Modo cuenta: badge + producto + cuenta + retiro**
  - **Pasos:** toggle en **▤ Cuenta**.
  - **Esperado:** una fila por cuenta; badge de su tipo, producto (`sanic_productoid.sanic_tipo`), nº de cuenta, retiro bimoneda.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-14 · Mora alta resaltada**
  - **Pasos:** ubicá la cuenta con mora > 90 o nivel ≥ 4.
  - **Esperado:** su pastilla de mora va en **rojo** (`#FEE2E2`/`#DC2626`), negrita. Las demás en gris.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-15 · Contador correcto**
  - **Pasos:** mirá el `lblCount`.
  - **Esperado:** dice "N clientes" en modo cliente y "N cuentas" en modo cuenta, y el número coincide con lo visible.
  - **Resultado:** [ ] OK [ ] Falla — _____

---

## 3. Cartera — filtro y búsqueda 🔴

- **T-20 · Filtro por tipo**
  - **Pasos:** tocá **Diaria** → **Incobrable** → **Especial** → **Todos**.
  - **Esperado:** la lista se reduce a clientes/cuentas con ese tipo; en modo cliente, un cliente aparece si **alguna** cuenta es de ese tipo. "Todos" muestra todo.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-21 · Búsqueda por nombre**
  - **Pasos:** escribí parte del nombre de un cliente (probá mayúsculas y minúsculas).
  - **Esperado:** filtra sin distinguir may/min; el contador se actualiza.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-22 · Búsqueda por número de cuenta**
  - **Pasos:** escribí parte de un número de cuenta.
  - **Esperado:** en modo cuenta aparece esa cuenta; en modo cliente aparece el **cliente dueño** de esa cuenta.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-23 · Filtro + búsqueda combinados**
  - **Pasos:** filtrá Incobrable **y** escribí un nombre.
  - **Esperado:** se aplican **las dos** condiciones a la vez.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-24 · Filtro/búsqueda conviven con el orden**
  - **Pasos:** con un orden custom ya armado, filtrá y limpiá el filtro.
  - **Esperado:** al limpiar, **se respeta tu orden** (no se resetea).
  - **Resultado:** [ ] OK [ ] Falla — _____

---

## 4. Cartera — reorden 🔴

- **T-30 · Inicialización del orden**
  - **Pasos:** con la cartera recién cargada (orden vacío/0), corré el botón **"Inicializar orden"** una vez.
  - **Esperado:** cada cliente queda con un `sanic_orden` **único** (1..N); todas las cuentas de un mismo cliente comparten el mismo número.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-31 · Subir un cliente (▲)**
  - **Pasos:** tocá ▲ en el 3er cliente.
  - **Esperado:** sube **una** posición e **intercambia** lugar con el que estaba arriba; el de arriba baja.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-32 · Bajar un cliente (▼)**
  - **Pasos:** tocá ▼ en el 1er cliente.
  - **Esperado:** baja una posición; swap con el de abajo.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-33 · Topes**
  - **Pasos:** ▲ en el **primero** y ▼ en el **último**.
  - **Esperado:** **no pasa nada** (no error, no se mueve).
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-34 · El bloque del cliente se mueve entero**
  - **Pasos:** mové un cliente que tiene 2+ cuentas; cambiá a **modo cuenta**.
  - **Esperado:** sus cuentas siguen **juntas** (mismo orden) y en la posición nueva.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-35 · El orden persiste entre sesiones**
  - **Pasos:** reordená, salí de la pantalla, cerrá y reabrí la app.
  - **Esperado:** el orden **se mantiene** (se escribió a `sanic_orden`).
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-36 · Asignaciones nuevas sin orden**
  - **Pasos:** simulá una asignación nueva (orden vacío) y corré el **"agregar al final"** (paso 2 del init).
  - **Esperado:** el cliente nuevo queda **al final** (max+1), sin pisar tu orden existente.
  - **Resultado:** [ ] OK [ ] Falla — _____

---

## 5. Navegación

- **T-40 · Barra inferior** — Inicio / Cartera / Stats navegan y resaltan el activo (teal + negrita). [ ]
- **T-41 · Abrir cliente (modo cliente)** — tocar tarjeta → `scrCliente` del cliente correcto. [ ]
- **T-42 · Abrir detalle (modo cuenta)** — tocar tarjeta → `scrDetalleCuenta` de la cuenta correcta. [ ]
- **T-43 · Volver (←)** — desde Cliente/Detalle/Interacción vuelve a la pantalla anterior. [ ]
- **T-44 · Deep-link desde Inicio** — tocar una fila del desglose abre el cliente/cuenta en la pestaña **Interacciones**. [ ]
- **T-45 · Indicador de sync** — tocarlo abre `scrSincronizacion` y vuelve bien. [ ]

---

## 6. Cliente

- **T-50 · Header** — nombre, departamento · trabajo correctos. [ ]
- **T-51 · Pestañas** — "Cuentas (N)" / "Interacciones (M)" con conteos correctos; cambian al tocar. [ ]
- **T-52 · Total del cliente** — bimoneda, suma de los retiros de sus cuentas. [ ]
- **T-53 · Contacto — Llamar** — botón abre el discador con el número. [ ]
- **T-54 · Contacto — Mapa** — botón abre Google Maps con la dirección. [ ]
- **T-55 · Mini-cuentas** — una por cuenta, con badge y retiro; tocar abre el detalle. [ ]
- **T-56 · Pestaña Interacciones** — lista las del cliente (todas sus cuentas), orden desc por fecha. [ ]

---

## 7. Detalle de cuenta 🔴

- **T-60 · Card Retiro (bimoneda)** — "A cobrar (Retiro)" bimoneda grande. [ ]
- **T-61 · Grid de 3 — verificación de campos reales:**
  - Saldo vencido = `Moneda(sanic_saldovencidonio, sanic_saldovencidousd)` [ ]
  - Pago mínimo = `Moneda(sanic_pagominimonio, sanic_pagominimousd)` [ ]
  - **Total (dolariz.) = `US$ sanic_totalsaldovencido`** (¡el campo real, no el inventado!) [ ]
- **T-62 · En riesgo** — "En riesgo (contagiado): US$ X" usa `sanic_contagiadousd`. [ ]
- **T-63 · Card Mora** — días/nivel (rojo si alta), fecha de pago, último pago, ciclo. [ ]
- **T-64 · Contacto** — teléfonos y direcciones del cliente de la cuenta. [ ]
- **T-65 · Producto/Fiador/Nota** — `sanic_productoid.sanic_tipo`, `sanic_fiadorid.sanic_nombre`, `sanic_nota`. [ ]
- **T-66 · Pestaña Interacciones** — solo las de **esta cuenta**. [ ]
- **T-67 · Botón Registrar** — fijo abajo, abre `scrInteraccion`. [ ]

---

## 8. Registrar interacción — escritura a Dataverse 🔴

- **T-70 · Validación: comentario obligatorio**
  - **Esperado:** sin comentario, **Guardar deshabilitado**; el hint dice "Falta: comentario".
  - [ ] OK [ ] Falla — _____

- **T-71 · Validación: pago sin monto/recibo**
  - **Pasos:** prendé "Aplica pago", dejá monto en 0 o recibo vacío.
  - **Esperado:** **Guardar deshabilitado** hasta que haya monto > 0 **y** recibo.
  - [ ] OK [ ] Falla — _____

- **T-72 · Validación: datos sin ningún campo**
  - **Pasos:** prendé "Actualizar datos", no llenés nada.
  - **Esperado:** Guardar deshabilitado hasta llenar ≥ 1 campo.
  - [ ] OK [ ] Falla — _____

- **T-73 · Texto dinámico del botón** — "Guardar interacción + pago + datos" según toggles. [ ]

- **T-74 · Guardar crea la interacción**
  - **Pasos:** comentario + Guardar. Verificá en `sanic_interaccion`.
  - **Esperado:** fila nueva con `sanic_cuentaid`, `sanic_agenteid` = vos, `sanic_comentario`, `sanic_fechainteraccion`, `sanic_estadotranscripcion` = **Pendiente**, geo en lat/long.
  - [ ] OK [ ] Falla — _____

- **T-75 · Pago mixto (NIO + USD)**
  - **Pasos:** agregá 2 líneas (una NIO, una USD) con recibos distintos → Guardar.
  - **Esperado:** **2 filas** en `sanic_pago` ligadas a la interacción (`sanic_interaccionid`), con `sanic_moneda` correcto y `sanic_numerorecibo`.
  - [ ] OK [ ] Falla — _____

- **T-76 · Actualización de datos**
  - **Pasos:** prendé datos, llená un teléfono → Guardar.
  - **Esperado:** fila en `sanic_actualizaciondatos` ligada a la interacción; `sanic_actualizacliente` = Sí en la interacción.
  - [ ] OK [ ] Falla — _____

- **T-77 · Chips** — tocar un chip agrega la frase al comentario. [ ]
- **T-78 · Quitar línea de pago** — la ✕ elimina la línea. [ ]
- **T-79 · La nueva interacción aparece** — al volver al detalle, está en la pestaña Interacciones. [ ]

---

## 9. Inicio

- **T-80 · Cobrado hoy (bimoneda)** — hero suma los pagos de **hoy** por moneda. [ ]
- **T-81 · Stats** — Interacciones / Cuentas / Clientes de hoy, conteos correctos. [ ]
- **T-82 · Desglose por cliente/cuenta** — toggle cambia el agrupado; conteo de interacciones por fila. [ ]
- **T-83 · Hero → Pagos** — tocar "Cobrado hoy" abre `scrPagos`. [ ]
- **T-84 · Saludo** — "Hola, {nombre del agente}". [ ]

---

## 10. Pagos del día

- **T-90 · Total cobrado** — bimoneda, suma de los pagos de hoy. [ ]
- **T-91 · Agrupado por cuenta** — una tarjeta por cuenta; cliente · producto · cuenta. [ ]
- **T-92 · Líneas con recibo** — cada pago: monto + moneda + "Recibo NNN · hora". [ ]

---

## 11. Sincronización (en player móvil) 🔴

- **T-100 · Estado conectado** — con señal y sin pendientes: ✓ verde "Sincronizado". [ ]
- **T-101 · Estado pendiente** — con capturas sin `sanic_numero`: 🔼 ámbar "Pendiente de subir (N)". [ ]
- **T-102 · Estado sin conexión** — en modo avión: 📴 gris "Sin conexión". [ ]
- **T-103 · Proxy de pendientes** — N = interacciones/pagos con `sanic_numero` **en blanco**. [ ]
- **T-104 · Capturas de hoy** — "sin Nº aún" (ámbar) vs "Nº asignado" (verde) por captura. [ ]
- **T-105 · Botón buscar** — deshabilitado sin señal; con señal hace `Refresh`. [ ]

---

## 12. Accesibilidad

- **T-110 · Las 4 escalas** — Aa → Normal/Grande/Muy grande/Máximo cambia **toda** la app. [ ]
- **T-111 · No se rompe** — en "Máximo", ningún texto se corta; nombre+badge, montos y filtros **se reacomodan** (wrap), no se salen de pantalla. [ ]
- **T-112 · Persiste** — la escala elegida se mantiene al navegar. [ ]

---

## 13. Offline (player móvil) 🔴

- **T-120 · Leer sin señal** — en modo avión, la cartera/cliente/detalle **se ven** (perfil offline). [ ]
- **T-121 · Capturar sin señal** — registrar interacción + pago sin señal **guarda** (Notify de éxito). [ ]
- **T-122 · Reordenar sin señal** — ▲▼ funciona y queda guardado localmente. [ ]
- **T-123 · Pendientes visibles** — la captura offline aparece "sin Nº aún" en Sync. [ ]
- **T-124 · Sincroniza al volver** — al recuperar señal, la captura sube y obtiene su `INT-`/`PAG-`. [ ]
- **T-125 · Sin conflictos** — nada se pisa al sincronizar (append-only). [ ]

---

## 14. PERFORMANCE ⚡ (con Monitor abierto)

> Abrí **Monitor** y mirá la columna de operaciones de red y su duración. Lo que buscamos es **ausencia** de
> trabajo pesado donde no corresponde.

- **T-130 · Reorden instantáneo (0 llamadas a Dataverse)** 🔴
  - **Pasos:** con Monitor abierto, tocá ▲▼ varias veces.
  - **Esperado:** **ninguna** operación de red por tap (el cambio es en `colClientes`, en memoria); la UI responde **al instante** (sin lag perceptible).
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-131 · Volver a la cartera NO reconstruye** 🔴
  - **Pasos:** entrá a un detalle y volvé a la Cartera, con Monitor abierto.
  - **Esperado:** al volver, **no** se dispara la consulta a `sanic_asignacion` ni el `GroupBy`/`LookUp` (el guard `!varCarteraLista` lo saltea). Entrada instantánea.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-132 · Persistencia solo de los sucios** 🔴
  - **Pasos:** mové **2** clientes, salí de la pantalla, mirá Monitor.
  - **Esperado:** en el `OnHidden` se ejecutan **solo** las escrituras de esos 2 clientes (no de toda la cartera). Si moviste 0, **0** escrituras.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-133 · Filtro/búsqueda en vivo, sin red**
  - **Pasos:** filtrá y tipeá en la búsqueda con Monitor abierto.
  - **Esperado:** **0** llamadas a Dataverse (filtra `colClientes` en memoria); responde mientras tipeás.
  - **Resultado:** [ ] OK [ ] Falla — _____

- **T-134 · Costo del build inicial (medido, una vez)**
  - **Pasos:** primer ingreso a la Cartera; en Monitor anotá la duración del bloque `ClearCollect` + `GroupBy`.
  - **Esperado:** ocurre **una sola vez**; tomá nota del tiempo (referencia para cuando la cartera crezca). Aceptable si es < ~1 s en el player.
  - **Tiempo medido:** ______ ms · **Resultado:** [ ] OK [ ] Falla — _____

- **T-135 · Direcciones no re-consultan por render**
  - **Pasos:** scrolleá la cartera con Monitor abierto.
  - **Esperado:** **no** hay un `LookUp` a `sanic_direccion` por fila al scrollear (ya están horneadas en `colClientes`).
  - **Resultado:** [ ] OK [ ] Falla — _____

---

## 15. Regresión — los bugs que arreglamos 🔴

Verificá que **no volvieron**:

- **T-140 · Campo total dolarizado**
  - **Esperado:** el "Total (dolariz.)" usa **`sanic_totalsaldovencido`** (no el inexistente `sanic_totaldolarizado`); muestra un número real, no error.
  - [ ] OK [ ] Falla — _____

- **T-141 · Bimoneda en TODOS lados**
  - **Esperado:** ningún monto se muestra solo en córdobas; donde hay USD aparece. Revisá cartera, total header, cliente, detalle (grid), pagos.
  - [ ] OK [ ] Falla — _____

- **T-142 · Reorden no tira "same data source en ForAll"**
  - **Esperado:** ▲▼ y la persistencia (`OnHidden`) **no** dan ese error (usamos `UpdateIf` / colección).
  - [ ] OK [ ] Falla — _____

- **T-143 · Reorden con orden vacío/0**
  - **Esperado:** tras inicializar, el swap funciona (no se mueven todos juntos por compartir el 0).
  - [ ] OK [ ] Falla — _____

- **T-144 · Cliente vía cuenta (no clienteid directo)**
  - **Esperado:** la agrupación y los datos del cliente salen por `sanic_cuentaid.sanic_clienteid` (la asignación no tiene cliente directo).
  - [ ] OK [ ] Falla — _____

- **T-145 · Choices con `.Value`**
  - **Esperado:** filtros por tipo (`sanic_tipo.Value`) y moneda (`sanic_moneda.Value`) comparan bien; al **escribir** usan el nombre del conjunto autocompletado.
  - [ ] OK [ ] Falla — _____

---

## 16. Resumen de la corrida

| Sección | Críticas (🔴) | Total | OK | Falla |
|---------|:---:|:---:|:---:|:---:|
| 1. Arranque | 5 | 5 | | |
| 2. Cartera visual | 6 | 6 | | |
| 3. Filtro/búsqueda | 5 | 5 | | |
| 4. Reorden | 7 | 7 | | |
| 5–10. Pantallas | — | ~30 | | |
| 11. Sync | 6 | 6 | | |
| 12. Accesibilidad | — | 3 | | |
| 13. Offline | 6 | 6 | | |
| 14. Performance | 4 | 6 | | |
| 15. Regresión | 6 | 6 | | |

**Veredicto:** [ ] Lista para usar  ·  [ ] Necesita arreglos (anotá los T-XX que fallaron)

> **Regla de oro:** ninguna prueba **🔴 crítica** puede quedar en falla. Si una falla, arreglá **antes** de
> seguir — un bug crítico arrastrado contamina todo lo de arriba. **Se entiende, ¿no?** 💪
