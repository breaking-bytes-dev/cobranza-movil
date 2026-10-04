# Sanic.CobranzaMovil.Plugins

Ensamblado de plugins de la **Fase 2 PAN/PCI** (diseño: [fase2-pan-diseno.md](../../docs/auditoria/fase2-pan-diseno.md) §2.6 y §4, filas 5 y 8).
.NET Framework 4.6.2, sandbox de Dataverse, firmado con nombre seguro. **Ningún plugin conoce ni necesita la clave K del HMAC.**

## Qué hace cada plugin

Todos son **síncronos, PreOperation**, corren con los permisos del **usuario que llama** y nunca ponen un valor de columna en un mensaje de error ni en una traza (solo el nombre de la columna).

| Plugin | Mensaje y tabla | Filtro | Qué hace |
|---|---|---|---|
| `PanGuard` | Create y Update de `sanic_cm_tbl_cuenta`, `sanic_cm_tbl_cargadiaria`, `sanic_cm_tbl_cuentapan` | Update: `sanic_cm_ct_numerodecuenta` / `sanic_cm_cd_numerocuenta` / `sanic_cm_cp_name` | Rechaza un valor con **5 o más dígitos seguidos** (tolera un espacio, guion o espacio duro entre dígitos: `4111 1111 1111 1111` también se rechaza). `****1234`, vacío y nulo pasan. |
| `PanGuard` | Create y Update de `sanic_cm_tbl_asignacion`, `sanic_cm_tbl_interaccion`, `sanic_cm_tbl_pago` | Update: `sanic_cm_as_numerocuenta` / `sanic_cm_in_numerocuenta` / `sanic_cm_pg_numerocuenta` | Rechaza **cualquier valor no vacío** en esas columnas denormalizadas viejas (el paso 8d las elimina; el script retira solo esos pasos cuando la columna ya no existe). |
| `ClaveDesdeCuenta` | Create y Update de `sanic_cm_tbl_asignacion` | Update: `sanic_cm_as_cuentaid`, `sanic_cm_as_clavepan` (+ imagen previa `PreImage` con `sanic_cm_as_cuentaid`) | Si hay `as_cuentaid`: lee `sanic_cm_ct_clavepan` de la cuenta (un solo `Retrieve`, una sola columna); sin `as_clavepan` en el Target la completa; si es distinta, rechaza. Si `as_cuentaid` se borra, no toca nada. Si la cuenta aún no tiene clave, no bloquea. |
| `CuentaDesdeInteraccion` | Create de `sanic_cm_tbl_pago` | — | Si `sanic_cm_pg_cuentaid` viene vacío y hay `sanic_cm_pg_interaccion`, lo completa con `sanic_cm_in_cuentaid` de la interacción (un solo `Retrieve`, una columna). Hace falta porque el modo offline solo admite un nivel de lookup. |

Un paso mal registrado (otra tabla, otra etapa, otro mensaje) **falla cerrado** con un error claro en vez de ignorar el control en silencio.

## Regla de activación de PanGuard (paso 8)

Hasta el paso 8 la carga de Smelter **todavía escribe el PAN** en `ct_numerodecuenta`, `cd_numerocuenta`, `cp_name` y `as_numerocuenta`. Activar `PanGuard` antes rompe la carga diaria. Por eso:

- Los 12 pasos `PanGuard` se **crean desactivados** (`statecode = 1`) y conservan su estado en cada re-ejecución.
- Se activan **solo en el paso 8**, después de cambiar la plantilla de Smelter para que enmascare, con `activar_panguard = true`.
- `desactivar_panguard = true` es el rollback.
- `ClaveDesdeCuenta` y `CuentaDesdeInteraccion` quedan **activos** desde el registro (paso 5).
- Después de la activación, la prueba negativa del plan: crear una cuenta con 13 dígitos en el nombre principal debe devolver error.

## Registro (qué crea el workflow)

15 pasos (12 `PanGuard`, 2 `ClaveDesdeCuenta`, 1 `CuentaDesdeInteraccion`), todos PreOperation (20), síncronos (0), rank 1, despliegue "solo servidor", ejecutar como el usuario que llama. Nombre de cada paso: `<Tipo>: <Mensaje> of <tabla>` (ese nombre es la clave de idempotencia). El ensamblado y los pasos se agregan a la solución `cm_core_cobranzamovil` (componentes 91 y 92).

## Cómo correr el workflow

Actions → **pan-plugin-cm** → Run workflow.

| Input | Efecto |
|---|---|
| `registrar = false` (defecto) | Compila y corre las pruebas. **No toca Dataverse.** |
| `registrar = true` | Además compila firmado y registra/actualiza ensamblado y pasos en DEV. Re-ejecutable: los pasos se buscan por nombre y se actualizan, nunca se duplican. |
| `activar_panguard = true` | Activa los pasos PanGuard (**solo paso 8**). |
| `desactivar_panguard = true` | Los desactiva. Excluyente con el anterior. |

Secrets: los de siempre (`DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET`, `PP_TENANT_ID`) y, **solo para registrar**, `PLUGIN_SNK_BASE64`.

El Service Principal necesita permisos de escritura sobre `pluginassembly`, `plugintype`, `sdkmessageprocessingstep` y su imagen, y poder agregar componentes a la solución (rol System Customizer o System Administrator en DEV).

Localmente (plan, solo lectura, con `pp_secrets.env`): `python3 scripts/pan/paso5_plugins.py --dll <dll> --version 1.0.0.0 --public-key-token <16 hex>`; con `--aplicar` registra.

### Clave de nombre seguro (una sola vez)

Dataverse **rechaza actualizar** un ensamblado con otra clave: hay que usar siempre el mismo `.snk`. No se versiona (`*.snk` está en `.gitignore`) y tampoco se genera en cada corrida.

```powershell
# En Windows con el SDK de .NET Framework (Developer PowerShell):
sn -k 2048 Sanic.CobranzaMovil.Plugins.snk
[Convert]::ToBase64String([IO.File]::ReadAllBytes("Sanic.CobranzaMovil.Plugins.snk")) | gh secret set PLUGIN_SNK_BASE64
```

Guardar el `.snk` en el gestor de secretos del equipo (un respaldo): si se pierde, hay que desregistrar y volver a registrar el ensamblado.

## Pruebas

`plugins/Sanic.CobranzaMovil.Plugins.Tests` (xunit + Moq, net462, corren en un runner Windows). Cubren: cada rechazo (y que el mensaje y las trazas no contienen el valor), enmascarados `****1234` permitidos, vacíos y nulos, Update sin la columna (no-op), `ClaveDesdeCuenta` (completa / rechaza / sin cambios / cuenta borrada / PreImage), `CuentaDesdeInteraccion` (completa / ya trae cuenta / sin interacción) y que se lee una sola columna con el usuario que llama.

Se usó **Moq y no FakeXrmEasy** (la skill de pruebas del equipo propone FakeXrmEasy v3): v3 exige licencia comercial y una clave en CI que no está provista, y aquí hace falta afirmar *qué* columnas se consultan, cosa que Moq permite directamente. Si se prefiere FakeXrmEasy, la lógica está aislada y las pruebas se portan sin tocar los plugins.

## Estructura

```
plugins/
├── Sanic.CobranzaMovil.Plugins/        PanGuard, ClaveDesdeCuenta, CuentaDesdeInteraccion, PanDetector, Esquema, PluginBase
└── Sanic.CobranzaMovil.Plugins.Tests/  xunit + Moq
scripts/pan/paso5_plugins.py            registro idempotente por Web API
.github/workflows/pan-plugin-cm.yml     compila, prueba y registra
```
