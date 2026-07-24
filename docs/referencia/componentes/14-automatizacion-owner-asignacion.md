# Componente 14: Automatización del Owner de la Asignación (Power Automate)

> **Fase — Lógica de negocio.** Detalla la pieza que el Componente 01 (§6 / paso 10) dejó marcada como
> *"automatización pendiente"*: **cómo se setea el `ownerid` de `sanic_asignacion`** = el `systemuser` del
> localizador. **Es la bisagra del aislamiento** ("cada localizador ve solo su cartera", DEC-015).
>
> Sin esta pieza, todas las asignaciones quedan con owner = cuenta de servicio del import → **nadie las ve**.

---

## 1. Por qué Power Automate (y NO low-code plugin Power Fx)

| Opción | Veredicto | Motivo |
|--------|-----------|--------|
| **Low-code plugin (Power Fx / Dataverse Accelerator)** | ❌ Descartado | **Preview, "not meant for production"**; *instant plugins* **deprecados** (los reemplaza "Functions", también preview); escritura limitada. No para un banco. |
| **Plugin C# (pro-code, síncrono)** | ✅ Válido (no elegido) | El más robusto y eficiente en **bulk** (pre-operation, sin latencia). Pero es **pro-code**; se reserva por si el volumen lo exige. |
| **Power Automate (flow Dataverse)** | ✅ **Elegido** | **GA / producción**, low-code, puede **asignar owner**, maneja creación e (re)asignación. Async, pero para el caso (import 1×/día) es más que suficiente. |

> Fuente verificada: *Low-code plug-ins Power Fx* (learn.microsoft.com) — preview, no producción, deprecado.

---

## 2. Qué tiene que hacer (contrato)

Cuando una `sanic_asignacion` **nace o cambia de agente**:
1. Resolver el `systemuser` del localizador vía `sanic_agenteid → sanic_agente.sanic_usuario`.
2. Setear el **Owner** de la asignación = ese `systemuser`.
3. Si el agente **no** tiene `sanic_usuario` vinculado → **no** romper: registrar/alertar y salir (gotcha 9.1).
4. **No** entrar en loop ni re-ejecutarse por cambios irrelevantes (ej. cuando el localizador toca `sanic_orden`).

---

## 3. Diseño del flujo

**Nombre:** `Cobranza · Owner de Asignación`
**Tipo:** flujo **automatizado** (cloud flow), en la **solución** (ALM, con connection reference).

### 3.1 Disparador (Trigger)
- **Conector:** Microsoft Dataverse → **"Cuando se agrega, modifica o elimina una fila"**.
- **Mensaje de cambio:** **Agregado** *y* **Modificado** (no Eliminado).
- **Tabla:** `sanic_asignacion`.
- **Ámbito (Scope):** **Organización** (el import corre como servicio; debe capturar todas).
- **🔑 Columnas de filtro (Select columns / Filtering attributes):** **`sanic_agenteid`** — **solo** este campo.
  - **Por qué es CRÍTICO:** hace que el flujo dispare **solo cuando cambia el agente**, no en cada update.
    Sin esto, cuando el localizador reordena (`sanic_orden`) o el backoffice toca algo, el flujo se
    dispararía al pedo — y peor, al setear el owner (que es un update) **se re-dispararía a sí mismo = loop**.
    Limitando a `sanic_agenteid`, el update de owner (que **no** cambia `sanic_agenteid`) **no** re-dispara.

### 3.2 Pasos

```
① Trigger: fila agregada/modificada de sanic_asignacion  (filtro: sanic_agenteid)
     │
② Obtener fila por Id → sanic_agente  (Id = _sanic_agenteid_value del trigger)
     │   ↳ leemos  _sanic_usuario_value  (el systemuser del localizador)
     │
③ Condición:  ¿ _sanic_usuario_value  está vacío ?
     ├── SÍ  → ④ Rama "sin usuario":
     │           - (opción) Crear fila de log / enviar correo al Admin:
     │             "Agente {nombre} sin usuario vinculado — asignación {nº} sin owner."
     │           - Terminar (Terminate: Succeeded).
     │
     └── NO  → ⑤ Condición de idempotencia:
                 ¿ el Owner actual de la asignación  ≠  _sanic_usuario_value ?
                 ├── NO (ya es el correcto) → Terminar (nada que hacer).
                 └── SÍ →
                     ⑥ Actualizar fila → sanic_asignacion (la del trigger)
                         Owner (Type) = systemusers
                         Owner        = _sanic_usuario_value   (el systemuser)
```

> **Setear el Owner en "Actualizar fila":** el conector expone **Owner (Type)** = `systemusers` y **Owner** =
> el GUID del `systemuser`. Si tu versión del conector **no** mostrara Owner en *Actualizar fila*, usá
> **"Realizar una acción enlazada"** con el mensaje **`Assign`** (equivalente nativo de asignar).

### 3.3 Prevención de loops (resumen)
- **Filtro `sanic_agenteid` en el trigger** → el update de owner no re-dispara (no cambia el agente).
- **Condición de idempotencia (⑤)** → si el owner ya es el correcto, no escribe. Doble red.

---

## 4. Casos que cubre (mapeo a los gotchas del Comp. 01)

| Caso | Cómo lo maneja |
|------|----------------|
| **Alta Diaria/Incobrable (import)** | Trigger *Agregado* → setea owner. |
| **Reasignación diaria (9.2)** | El import cambia `sanic_agenteid` → Trigger *Modificado* (por el filtro) → **re-setea** owner. |
| **Reasignación manual intradía** | Igual: cambia el agente → re-setea. |
| **Especial aprobada** | El Supervisor asigna `sanic_agenteid` al aprobar → Trigger *Modificado* → setea owner. |
| **Agente sin usuario (9.1)** | Rama ④: no rompe, alerta al Admin, deja la asignación sin owner (el localizador no la ve hasta vincular). |
| **Localizador reordena (`sanic_orden`)** | **No dispara** (el filtro es solo `sanic_agenteid`). |

---

## 5. Consideraciones

- **Latencia (async):** el flujo corre **después** de commit; hay una ventana de segundos con el owner viejo.
  **Aceptable:** el import es 1×/día (de madrugada) y el localizador abre la app después. Si algún día se
  necesitara **inmediatez o bulk masivo**, migrar a **plugin C# síncrono** (§1).
- **Throughput en bulk:** el import crea muchas filas → el flujo corre 1×/fila. Para cientos/día está bien;
  vigilar límites de API de Power Platform si la cartera crece a miles. (El C# plugin no tiene este costo.)
- **Privilegios del flujo:** la **conexión** (service account / connection reference) necesita en
  `sanic_asignacion`: **Write** y **Assign** (a nivel Organización). Documentar la cuenta.
- **ALM:** construir el flujo **dentro de la solución**, con **connection reference** (no conexión hardcodeada),
  para que viaje entre entornos (Dev→Test→Prod).
- **Orden con el import:** el flujo depende de que `sanic_agente.sanic_usuario` **ya esté** vinculado (Admin,
  Comp. 01 §7-B). La vista *"Agentes sin usuario vinculado"* debe estar **vacía antes de importar**.

---

## 6. Pruebas (verificar con 2 localizadores)

- [ ] Import crea una asignación de "Marvin" → en segundos, **Owner = usuario de Marvin**.
- [ ] Marvin **ve** la asignación en la app; **Xiomara no**.
- [ ] Reasignar la cuenta a "Xiomara" (cambiar `sanic_agenteid`) → **Owner pasa a Xiomara**; ahora la ve ella y no Marvin.
- [ ] Marvin **reordena** su cartera (`sanic_orden`) → el flujo **NO** corre (revisar historial de ejecuciones).
- [ ] Asignación de un agente **sin** `sanic_usuario` → el flujo **no rompe**, alerta al Admin, queda sin owner.
- [ ] Aprobar una **Especial** asignando localizador → Owner queda en ese localizador.
- [ ] Correr el mismo update dos veces (idempotencia) → la 2ª no reescribe (condición ⑤).

---

## 7. Trazabilidad y dependencias

- **Materializa:** Comp. 01 §6 / paso 10 (owner de la asignación) · DEC-015 (Moderado) · DEC-006 (agente=localizador).
- **Depende de:** `sanic_agente.sanic_usuario` vinculado (Admin) · tabla `sanic_asignacion` con propiedad
  **User/Team owned** (Comp. 01 §7-D) · Smelter poblando `sanic_agenteid` (Comp. 02 / doc 02).
- **Habilita:** que la app de campo resuelva *"mi cartera"* = asignaciones **owned por el usuario actual**, y el
  **perfil offline** que baja solo eso (doc 04 §4).
- **Alternativa futura:** plugin **C# síncrono** si se requiere inmediatez o eficiencia en bulk masivo.
