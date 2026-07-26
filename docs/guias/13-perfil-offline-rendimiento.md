# Guía — Perfil Offline (la palanca de rendimiento a escala)

> **La pieza crítica para producción.** La app carga tablas maestras (`colCuentas`, `colClientes`…) al
> arrancar. Sin acotar, con **miles de cuentas** el `OnStart` se cae por volumen y por el **tope de 2000
> filas**. El perfil offline hace que cada dispositivo baje **solo la cartera de SU localizador** → la app
> vuela a cualquier escala del banco. **No requiere cambiar código** — el código ya carga "lo que hay en el
> dispositivo".

## 1. Por qué (medido con Monitor)

| Operación | Con ~1.000 cuentas | Extrapolado a 50.000 (sin acotar) |
|-----------|:---:|:---:|
| `App.OnStart` (cargar maestras) | 1,7 s | **~85 s + tope 2000 → datos faltantes** |
| Cartera (`LoadScreen`) | 1,0 s | degradado |
| `LookUp`/`Filter`/`Sort` en memoria | 1-5 ms | 1-5 ms (no cambia) |

**La lógica vuela; el costo es el volumen de datos.** Con el perfil acotado a ~200 cuentas por localizador:
`OnStart` ≈ **0,3 s**, sin tope, sync liviano — **sin importar que el banco tenga 500.000 cuentas.**

## 2. El diseño del perfil (qué scopear y cómo)

En el **editor de perfil offline** de la app (Power Apps → tu app → **Configuración → Offline / Sin
conexión → Editar perfil**), configurá cada tabla:

| Tabla | Regla offline | Por qué |
|-------|---------------|---------|
| `sanic_cm_tbl_asignacion` | **Filas que poseo** (owner = usuario) | La cartera del localizador (RLS ya la posee, DEC-015) |
| `sanic_cm_tbl_cuenta` | **Relacionadas con mis asignaciones** | Solo las cuentas de su cartera |
| `sanic_cm_tbl_cliente` | **Relacionadas con mis cuentas** | Solo sus clientes |
| `sanic_cm_tbl_direccion` | Relacionadas con mis clientes | |
| `sanic_cm_tbl_telefono` | Relacionadas con mis clientes | |
| `sanic_cm_tbl_producto` | **Todas** | Tabla de referencia chica (7 filas) |
| `sanic_cm_tbl_fiador` | Relacionadas con mis cuentas (o todas) | |
| `sanic_cm_tbl_agente` | **Todas** (chica) — necesaria para `gblAgente` | |
| `sanic_cm_tbl_interaccion` | **Filas que poseo** | Mis gestiones |
| `sanic_cm_tbl_pago` | **Filas que poseo** | Mis pagos |

> **La clave son las reglas "relacionadas con…"**: la cuenta se baja porque hay una asignación mía que la
> apunta; el cliente porque hay una cuenta mía; etc. Así el dispositivo baja **la cartera y nada más.**

### Importante
- **Todas** las tablas que la app usa como origen de datos **deben estar en el perfil** (si falta una,
  esa parte queda vacía offline): asignacion, cuenta, cliente, direccion, telefono, producto, fiador,
  agente, interaccion, pago.
- Las reglas de propiedad (`owner`) dependen de que el **owner esté seteado** = el `systemuser` del
  localizador ([componente 14](../referencia/componentes/14-automatizacion-owner-asignacion.md)). Si el
  owner no está, el filtro "filas que poseo" no baja nada.

## 3. Ajuste complementario (App Settings)

Aunque el perfil acote, subí el **"Límite de filas de datos para consultas no delegables"** a **2000**
(Configuración → General). Es la red de seguridad para las colecciones locales.

## 4. Verificar (con Monitor, de nuevo)

Tras acotar el perfil y re-sincronizar:
- [ ] `App.OnStart` baja de ~1,7 s a **décimas de segundo**.
- [ ] La cartera muestra **la cartera real del localizador** (no todas las cuentas del banco).
- [ ] Exportá una nueva sesión de Monitor y comparala — el `OnStart` y el `LoadScreen` deben desplomarse.

## 5. ¿Y el código?

**No cambia.** `ClearCollect(colCuentas, 'CM | TBL | Cuentas')` carga lo que el perfil dejó en el
dispositivo. Con el perfil acotado, eso ya es solo la cartera del localizador.

> **Defensa extra opcional (si no confiás en el perfil):** filtrar el `OnStart` para cargar solo las
> cuentas de las asignaciones del localizador (`ForAll` de asignaciones + `LookUp` de cuenta por número).
> Escala igual (cada `LookUp` es delegable, sin tope), pero agrega N consultas. **Con el perfil bien
> acotado NO hace falta** — el código simple es óptimo. Se evalúa solo si el perfil no puede acotar.
