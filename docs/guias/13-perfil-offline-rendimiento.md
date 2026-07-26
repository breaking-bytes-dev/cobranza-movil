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

## 2. El diseño del perfil — EXACTO según el UI

Cada tabla del perfil ofrece: **Organization rows** · **All rows** · **Related rows only** · **Custom**
(con **Edit filter** y una sección **Relationships** para tildar por qué relación bajar filas relacionadas).

La idea: **1 tabla RAÍZ** con las filas del localizador (`All rows`, que la seguridad recorta a lo suyo),
y **el resto se baja "relacionado"** siguiendo los lookups. Así el celular baja **solo su cartera.**

| Tabla | Opción a elegir | Relationships (tildar) | Por qué |
|-------|-----------------|------------------------|---------|
| **CM \| TBL \| Asignacion** | **All rows** | — (es la raíz) | Offline recorta por seguridad → solo TUS asignaciones (User-level, DEC-015) |
| **CM \| TBL \| Interaccion** | **All rows** | — | Tus gestiones (owned) |
| **CM \| TBL \| Pago** | **Custom** | ✅ `CM \| TBL \| Interaccion` | Pagos ligados a tus interacciones |
| **CM \| TBL \| Cuenta** | **Custom** | ✅ `CM \| TBL \| Asignacion` | Solo cuentas que tienen asignación tuya |
| **CM \| TBL \| Cliente** | **Custom** | ✅ `CM \| TBL \| Cuenta` | Solo clientes de tus cuentas |
| **CM \| TBL \| Direccion** | **Custom** | ✅ `CM \| TBL \| Cliente` | Direcciones de tus clientes |
| **CM \| TBL \| Telefono** | **Custom** | ✅ `CM \| TBL \| Cliente` | Teléfonos de tus clientes |
| **CM \| TBL \| Fiador** | **Custom** | ✅ `CM \| TBL \| Cuenta` | Fiadores de tus cuentas |
| **CM \| TBL \| Producto** | **Organization rows** | — | Tabla chica (7 filas), se baja entera |
| **CM \| TBL \| Agente** | **Organization rows** | — | Chica; necesaria para `gblAgente` |

### Cómo se lee cada opción
- **All rows** = todas las filas que **el usuario puede ver** (la seguridad recorta). En tablas User-owned
  (asignacion, interaccion) = solo las suyas.
- **Organization rows** = todas las de la org (para tablas de referencia chicas que todos comparten).
- **Related rows only / Custom + Relationships** = **la palanca**: baja una fila **solo si está relacionada**
  con otra que ya está en el perfil. `Cuenta` se baja porque una `Asignacion` tuya la apunta; `Cliente`
  porque una `Cuenta` tuya lo apunta; y así en cadena. **Eso acota a tu cartera.**

### Para la pantalla que estás viendo (Pago)
Estás en **Custom**. Expandí **Relationships (0/6)** y **tildá `CM | TBL | Interaccion`**. Con eso, bajan
los pagos ligados a tus interacciones. (No hace falta tildar `User`.)

> **El orden importa conceptualmente:** Asignacion (raíz) → Cuenta (relacionada a Asignacion) → Cliente
> (relacionada a Cuenta) → Direccion/Telefono (relacionadas a Cliente). Cada eslabón cuelga del anterior.
> El motor de sync resuelve el orden solo; vos solo tildás la relación correcta en cada tabla.

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
