# Sanic.CobranzaMovil.Plugins

Ensamblado único con los *plugins* y las *Custom APIs* de Cobranza Móvil. Destino: .NET Framework 4.6.2, C# 7.3, *sandbox* de Dataverse, firmado con nombre seguro y sin dependencias fuera del SDK de Dataverse. Versión actual del ensamblado: 1.0.10.7.

La descripción de cada plugin y cada Custom API (mensaje, etapa, qué hace y por qué) está en [04 · Plugins y Custom APIs](../../docs/tecnica/04-plugins-y-custom-apis.md).

## Qué contiene

| Carpeta o archivo | Contenido |
|---|---|
| `*.cs` de esta carpeta | Un archivo por plugin, uno por Custom API y las clases de apoyo (`PluginBase`, `Esquema`, `RolesCm`, `UsuarioSistema`, `PanDetector` (detecta y enmascara números de tarjeta), `CarteraSinCobrador`, `SolicitadoPor`, `FilaDeCarga`, `ArchivoCsv`, `ParticionCarga`) |
| [`Esquema.cs`](Esquema.cs) | Todos los nombres lógicos de tablas y columnas que usan los plugins |
| [`../Sanic.CobranzaMovil.Plugins.Tests/`](../Sanic.CobranzaMovil.Plugins.Tests/) | 665 pruebas con xUnit y Moq (net462); [`Harness.cs`](../Sanic.CobranzaMovil.Plugins.Tests/Harness.cs) arma el contexto de ejecución |
| [`paso5_plugins.py`](../../scripts/pan/paso5_plugins.py) | Registro del ensamblado y de sus pasos (fuente de verdad del registro) |
| [`paso12_custom_api.py`](../../scripts/pan/paso12_custom_api.py) | Definición de las Custom APIs y sus parámetros |
| [`pan-plugin-cm.yml`](../../.github/workflows/pan-plugin-cm.yml) | Workflow que prueba, compila firmado y registra |

## Cómo se compila

La compilación firmada y el registro los hace el workflow [`pan-plugin-cm.yml`](../../.github/workflows/pan-plugin-cm.yml): prueba y compila en un runner Windows (las pruebas corren en net462, el mismo runtime del *sandbox*) y registra desde un runner Linux. La clave de firma solo existe como secreto del repositorio.

Actions → **pan-plugin-cm** → *Run workflow*:

| Entrada | Efecto |
|---|---|
| `registrar = false` (defecto) | Corre las pruebas. No toca Dataverse |
| `registrar = true` | Además compila firmado y registra ensamblado y pasos en el ambiente de los secretos |
| `activar_panguard` / `desactivar_panguard` | Activa o desactiva los pasos de `PanGuard`. Excluyentes; requieren `registrar = true` |

⚠️ Los pasos de `PanGuard` tienen que quedar **activos**. Sin `activar_panguard`, un paso nuevo se crea desactivado y uno existente conserva su estado: en un ambiente nuevo, registrar con `activar_panguard = true`.

Secretos: `DATAVERSE_URL`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET`, `PP_TENANT_ID` (un *service principal* con rol System Customizer o System Administrator) y, solo para registrar, `PLUGIN_SNK_BASE64`.

Antes de registrar una versión nueva, subir `AssemblyVersion`, `FileVersion` y `Version` en [`Sanic.CobranzaMovil.Plugins.csproj`](Sanic.CobranzaMovil.Plugins.csproj), cambiando solo los dos últimos números.

### Firma

Dataverse rechaza actualizar un ensamblado con una clave distinta de la registrada. La clave `.snk` no se versiona (`*.snk` está en `.gitignore`); se genera una sola vez por tenant y se guarda en el gestor de secretos:

```powershell
sn -k 2048 Sanic.CobranzaMovil.Plugins.snk
[Convert]::ToBase64String([IO.File]::ReadAllBytes("Sanic.CobranzaMovil.Plugins.snk")) | gh secret set PLUGIN_SNK_BASE64
```

Sin `-p:AssemblyOriginatorKeyFile=<ruta>` el proyecto compila sin firmar: sirve para las pruebas, no para registrar.

## Cómo se registra

1. **Ensamblado y pasos:** el workflow con `registrar = true` corre `paso5_plugins.py --aplicar`. Es idempotente: busca cada paso por nombre (`<Tipo>: <Mensaje> of <tabla>`), lo actualiza o lo vuelve a crear, y agrega todo a la solución `cm_core_cobranzamovil`. Sin `--aplicar` solo muestra el plan.
2. **Custom APIs:** después del paso anterior, `python3 scripts/pan/paso12_custom_api.py --aplicar` (con las mismas variables de entorno de credenciales).
3. **Studio:** si cambió una Custom API, refrescar la fuente «Environment» de la app de la oficina.

Requisitos previos (columnas, equipo «Cartera sin cobrador», plantillas de Smelter) y el orden completo de instalación: [09 · Instalación y despliegue](../../docs/tecnica/09-instalacion-y-despliegue.md).
