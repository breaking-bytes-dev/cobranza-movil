# Divergencias Diseño (docs) ↔ Build (entorno real)

> Surgió al reconciliar los docs con el esquema real (2026-07-25). Son campos/comportamientos que **los
> docs asumían y el entorno NO tiene**. En los docs quedaron marcados inline como `⚠️TODO(nombre-viejo)`.
> **Cada uno necesita una decisión tuya:** agregar la columna, reusar otra, o rediseñar. No se inventó nada.

## 🔴 Críticos (bloquean funcionalidad diseñada)

### 1. `sanic_fechainteraccion` — la interacción no tiene fecha de captura *(19 ocurrencias)*
- **El build:** `sanic_cm_tbl_interaccion` solo tiene `sanic_cm_in_fechagrabacion` (fecha de **grabación al core**, la pone el backoffice), NO una fecha de cuándo el localizador registró la gestión en campo.
- **Impacto:** el `Patch` de creación de la app, la cola FIFO "por transcribir" (ordena por fecha), y varios tests dependen de este dato.
- **Opciones:** (a) usar `createdon` — ⚠️ ojo: en offline `createdon` = hora de **sync** (servidor), no de captura real; (b) **agregar** `sanic_cm_in_fechacaptura` que la app setea al crear (recomendado si la hora de campo importa).

### 2. `sanic_ultimacarga` + `fechacierre` — el sweep DEC-018 no tiene dónde apoyarse *(14 + 2)*
- **El build:** `sanic_cm_tbl_asignacion` no tiene ni fecha de última carga ni fecha de cierre.
- **Impacto:** el mecanismo de barrido de Diarias desaparecidas (DEC-018) usa `ultimacarga` para detectar cuáles no vinieron en el CSV de hoy, y `fechacierre` para marcar el cierre. **Sin estas columnas, el sweep no se puede implementar como está diseñado.**
- **Opciones:** agregar `sanic_cm_as_ultimacarga` (DateTime) y `sanic_cm_as_fechacierre` (DateTime), o rediseñar el sweep para inferir por `sanic_cm_cd_fechacarga` de la cargadiaria.

### 3. `sanic_fechapago` — la tabla `pago` no tiene fecha *(13)*
- **El build:** `sanic_cm_tbl_pago` = interaccion, moneda, monto, numero, numerorecibo. Sin fecha.
- **Nota:** el `sanic_cm_ct_fechapago` de **cuenta** sí existe (es otra cosa: fecha de pago de la cuenta).
- **Opciones:** (a) `createdon` (= sync time); (b) agregar `sanic_cm_pg_fecha` si importa la fecha real del pago.

## 🟡 Medios (funciona, pero el diseño esperaba otra cosa)

### 4. `sanic_motivocierre` vs `sanic_motivorechazo` — un solo campo para dos usos *(4 + 2)*
- **El build:** asignacion tiene **un** `sanic_cm_as_motivo` genérico. El diseño separaba "motivo de cierre" y "motivo de rechazo (Especial)".
- **Opción:** aceptar el campo único (y documentar que sirve para ambos), o agregar `sanic_cm_as_motivorechazo`.

### 5. Agrupar la cartera por cliente — `sanic_clienteid` no es columna de asignacion *(1, doc 04)*
- **El build:** asignacion llega al cliente **solo navegando** `sanic_cm_as_cuentaid.sanic_cm_ct_clienteid`. No hay lookup directo para un `GroupBy`.
- **Opción:** `AddColumns` previo (traer el cliente a una columna local) antes de agrupar — ya lo veníamos resolviendo así en la app.

### 6. Valor "Pendiente" del choice de grabación *(1)*
- **El build:** de `sanic_cm_choice_estadograbacioninteraccion` solo tenemos confirmado `'2' = grabada`. El valor inicial "Pendiente" (que setea el Patch y filtra la cola) **no está confirmado**.
- **Acción:** verificar el nombre/valor real de la opción "Pendiente" en el choice.

## 🟢 Bajos / futuros (estaban marcados como opcionales)

- **`sanic_escalavista`** *(3, agente)* — escala de accesibilidad del localizador. No existe. ¿Se usa realmente o era idea?
- **`sanic_backofficeid`** *(2, doc 07)* — asignar backoffice a localizador. Ya estaba anotado como "a futuro, no construido".
- **`sanic_tomadapor`** *(1, doc 08)* — quién tomó la transcripción. Opcional.

## ⚙️ A confirmar (mapeados por cercanía, validá el semántico)

- **`sanic_supervisorid` → `sanic_cm_as_resolutorid`** — quién aprueba/rechaza una Especial. El real es "resolutor"; confirmá que es el mismo rol.
- **`sanic_motivorechazo` → `sanic_cm_as_motivo`** — se mapeó al motivo genérico (ver punto 4).

---

## Cómo cerrar esto

Por cada punto, la decisión es una de tres: **(A) agregar la columna** al build (y actualizar el doc quitando el `⚠️TODO`), **(B) reusar** un campo existente / `createdon` (documentar la decisión), o **(C) rediseñar** el mecanismo. Una vez decidido, se quita el marcador `⚠️TODO(...)` del doc correspondiente.

> Buscar en el repo: `⚠️TODO(` lista todas las ocurrencias pendientes.
