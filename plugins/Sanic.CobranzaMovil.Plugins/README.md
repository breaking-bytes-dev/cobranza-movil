# Sanic.CobranzaMovil.Plugins

Ensamblado único con los *plugins* y las *Custom APIs* de Cobranza Móvil. Destino: .NET Framework 4.6.2, C# 7.3, *sandbox* de Dataverse, firmado con nombre seguro y sin dependencias fuera del SDK de Dataverse. Versión actual del ensamblado: 1.0.10.8.

La descripción de cada plugin y cada Custom API (mensaje, etapa, qué hace y por qué) está en [04 · Plugins y Custom APIs](../../docs/tecnica/04-plugins-y-custom-apis.md).

## Qué contiene

| Carpeta o archivo | Contenido |
|---|---|
| `*.cs` de esta carpeta | Un archivo por plugin, uno por Custom API (`SolicitarEspecial`, `AltaCuenta`, `PartirCarga`, `CerrarFueraDeCartera`, `MisRoles`) y las clases de apoyo (`PluginBase`, `Esquema`, `RolesCm`, `UsuarioSistema`, `PanDetector` (detecta y enmascara números de tarjeta), `CarteraSinCobrador`, `SolicitadoPor`, `FilaDeCarga`, `ArchivoCsv`, `ParticionCarga`) |
| [`Esquema.cs`](Esquema.cs) | Todos los nombres lógicos de tablas y columnas que usan los plugins |
| [`../Sanic.CobranzaMovil.Plugins.Tests/`](../Sanic.CobranzaMovil.Plugins.Tests/) | 665 pruebas con xUnit y Moq (net462); [`Harness.cs`](../Sanic.CobranzaMovil.Plugins.Tests/Harness.cs) arma el contexto de ejecución |
| [`Sanic.CobranzaMovil.Plugins.csproj`](Sanic.CobranzaMovil.Plugins.csproj) | Proyecto: destino, versión y firma |

## Cómo se instala

En un ambiente de destino el ensamblado **no se compila ni se registra**: viaja con todos sus pasos y sus *Custom APIs* dentro de la *solution* `cm_core_cobranzamovil`, y se instala al importarla. Después de importar, comprobar que todos los pasos estén **On**, incluidos los seis de `PanGuard`. Paso a paso: [09 · Instalación y despliegue](../../docs/tecnica/09-instalacion-y-despliegue.md#3-revisar-lo-importado).

## Cómo se cambia (ambiente de desarrollo)

1. **Código y pruebas.** Abrir el proyecto en Visual Studio, hacer el cambio y correr las pruebas desde **Test Explorer**. Las pruebas corren en net462, el mismo *runtime* del *sandbox*. Detalle en [04 · Cómo agregar o cambiar un plugin](../../docs/tecnica/04-plugins-y-custom-apis.md#cómo-agregar-o-cambiar-un-plugin).
2. **Versión.** Subir `AssemblyVersion`, `FileVersion` y `Version` en el `.csproj`, cambiando solo los dos últimos números: Dataverse trata un cambio de versión mayor o menor como otro ensamblado.
3. **Compilar firmado** (ver [Firma](#firma)) en configuración **Release**.
4. **Actualizar el ensamblado y sus pasos** en el ambiente de desarrollo con **Plug-in Registration Tool**, la herramienta gráfica de Microsoft para Windows: **Update** sobre el ensamblado `Sanic.CobranzaMovil.Plugins` para cargar la DLL nueva y, si hace falta, **Register New Step** o editar el paso existente (mensaje, tabla, etapa, modo, columnas de filtro e imágenes, como indica la ficha del plugin en el 04). ⚠️ A confirmar cómo se obtiene la herramienta en el puesto de trabajo sin línea de comandos (se distribuye como paquete NuGet `Microsoft.CrmSdk.XrmTooling.PluginRegistrationTool`).
5. **Agregar a la *solution*** lo que sea nuevo: make.powerapps.com → **Solutions** → `cm_core_cobranzamovil` → **Add existing** → **More** → **Developer** → **Plug-in step** (o **Custom API**). Un paso que no está en la *solution* no llega a los demás ambientes.
6. **Custom API nueva o parámetro nuevo:** crearla en la *solution* (**+ New** → **More** → **Other** → **Custom API**, con sus parámetros de solicitud y propiedades de respuesta) apuntando a la clase del ensamblado. Dataverse no permite cambiar el tipo ni la opcionalidad de un parámetro ya creado: hay que borrarlo y recrearlo. Después, en Power Apps Studio, refrescar la fuente «Environment» de la app de la oficina.
7. **Llevarlo a los demás ambientes** exportando una versión nueva de la *solution*: [09 · Actualizar a una versión nueva](../../docs/tecnica/09-instalacion-y-despliegue.md#actualizar-a-una-versión-nueva).

⚠️ Los pasos de `PanGuard` tienen que quedar **activos**. Un paso nuevo registrado a mano puede quedar desactivado: revisarlo en **Plug-in steps** antes de exportar.

### Firma

Dataverse exige un ensamblado firmado con nombre seguro y rechaza actualizarlo con una clave distinta de la registrada. La clave `.snk` no se versiona (`*.snk` está en `.gitignore`): se genera una sola vez por tenant y se guarda en el gestor de secretos del equipo. Si se pierde, hay que eliminar el ensamblado registrado (con sus pasos) y registrarlo de nuevo.

El `.csproj` firma solo si recibe la propiedad `AssemblyOriginatorKeyFile`; sin ella compila sin firmar, lo que sirve para las pruebas pero no para registrar. En Visual Studio se indica en las propiedades del proyecto → **Build** → **Strong naming** → **Sign the assembly**, eligiendo el `.snk` guardado fuera del repositorio. No incluir en el repositorio el cambio del `.csproj` que apunta a esa ruta local. ⚠️ A confirmar el nombre de la opción en la versión de Visual Studio del equipo y si permite crear la clave nueva desde ahí.
