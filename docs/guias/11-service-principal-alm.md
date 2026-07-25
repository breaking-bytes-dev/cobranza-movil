# Guía — Crear el Service Principal para ALM (CI/CD)

> **Instructivo paso a paso** para crear la identidad que los workflows usan para exportar/importar la
> solución sin intervención humana. Materializa la [Fase 4 del doc 10](../desarrollo/10-estructura-del-repositorio.md)
> y la [decisión de un SP por entorno](../referencia/decisiones/registro-de-decisiones.md).

---

## 0. Qué es un Service Principal (el concepto, primero)

Un **Service Principal (SP)** es una **identidad de aplicación** en Microsoft Entra ID (ex Azure AD). No es
una persona: es una cuenta que un programa (acá, GitHub Actions) usa para autenticarse **sin humano**.

Piezas:
- **App Registration** (en Entra): la definición de la app → te da un **Client ID** y un **Tenant ID**.
- **Client Secret**: la "contraseña" de esa app. Se muestra **una sola vez**.
- **Application User** (en el entorno Dataverse): el SP metido *dentro* del entorno, con un **rol de
  seguridad**. **Acá se decide qué puede hacer.**

> 🔑 **Regla de oro (ya decidida):** el SP se autentica contra el **ENTORNO**, no contra una solución.
> Por eso **UN solo SP** sirve para cobranza, sigil y toda solución del mismo entorno. No creamos uno por
> solución.

---

## 1. Precondiciones

- [ ] Sos **Power Platform Admin** (o Global Admin) del tenant.
- [ ] Tu tenant permite **registrar apps** en Entra (o tenés rol *Application Developer*).
- [ ] Tenés a mano la **URL del entorno** (ej. `https://orgXXXX.crm.dynamics.com`).
- [ ] `pac` instalado (verificado: 2.9.3).

---

## 2. Camino A — con `pac` (rápido, recomendado)

`pac` crea el App Registration **y** el application user con rol **System Administrator**, en un comando.

```bash
# 1) Autenticarte como ADMIN (abre el navegador; entrá con tu cuenta admin)
pac auth create --name admin

# 2) Ver tus entornos y copiar el Environment Id (o usá la URL)
pac admin list

# 3) Crear el SP contra el entorno (reemplazá la URL o el id)
pac admin create-service-principal --environment https://orgXXXX.crm.dynamics.com
```

La salida te da **TODO lo que necesitás — copialo YA** (el secret no se vuelve a mostrar):

```
Application (Client) ID:  xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
Client Secret:            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Tenant Id:                xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

> ⚠️ `pac` asigna **System Administrator**. Es amplio, pero el **entorno es el límite** y es lo estándar
> para un SP de deploy (evita que un import falle por falta de privilegios). Si querés *least privilege*
> real (solo export), usá el **Camino B** con **System Customizer**.

Saltá a la [sección 4](#4-cargar-los-secrets-en-github).

---

## 3. Camino B — manual (control total / least privilege)

### 3.1 Entra ID — App Registration
1. [entra.microsoft.com](https://entra.microsoft.com) → **Identity → Applications → App registrations** →
   **+ New registration**.
   - **Name:** `sp-dataverse-dev` *(nombrelo por ENTORNO, no por solución — es compartido)*.
   - **Supported account types:** *Single tenant*.
   - **Redirect URI:** vacío. → **Register**.
2. En **Overview**, copiá **Application (client) ID** y **Directory (tenant) ID**.
3. **Certificates & secrets → + New client secret** → descripción + expiración (ej. **24 meses**) → **Add**
   → copiá el **Value** (la columna *Value*, **no** *Secret ID*). Se muestra una sola vez.

> No hace falta configurar *API permissions*: en la auth server-to-server contra Dataverse, quien manda es
> el **application user** del entorno (paso 3.2), no permisos delegados de Graph.

### 3.2 Power Platform Admin Center — Application User
4. [admin.powerplatform.microsoft.com](https://admin.powerplatform.microsoft.com) → **Environments** →
   tu entorno → **Settings → Users + permissions → Application users** → **+ New app user**.
5. **+ Add an app** → buscá por el **Client ID** → seleccionala.
6. **Business unit:** la raíz (default).
7. **Security roles:**
   - **System Customizer** → alcanza para **exportar** soluciones (least privilege).
   - **System Administrator** → necesario si el SP también **importa** (Test/Prod). Para deploy real, este.
   - **Create.**

---

## 4. Cargar los secrets

Los workflows leen **4 valores**. La sintaxis en el YAML —`${{ secrets.NOMBRE }}`— es **idéntica** sea el
secret a nivel **repo** o a nivel **org**: por eso la elección de dónde guardarlos **no cambia una coma de
los workflows**.

| Secret | Valor | ¿Sensible? |
|--------|-------|:---:|
| `DATAVERSE_URL` | URL del entorno (`https://orgXXXX.crm.dynamics.com`) | no |
| `PP_CLIENT_ID` | Application (Client) ID | no |
| `PP_TENANT_ID` | Directory (Tenant) ID | no |
| `PP_CLIENT_SECRET` | el Client Secret (Value) | **SÍ** |

### 4.1 Qué funciona en org `free` (importante, no confundir)

| Tipo de secret | ¿Free + repo privado? |
|----------------|:---:|
| **Repository secrets** | ✅ siempre |
| **Organization secrets** (visibilidad *Selected repositories*) | ✅ sí — verificado en este proyecto vía API |
| **Environment secrets** (Environment `dev`) + reglas de protección | ❌ requiere Pro/Team/Enterprise |

> La limitación del plan `free` es sobre **environment secrets**, **NO** sobre organization secrets. Si el
> picker de la UI (**Repository access → Selected repositories**) muestra **"No repositories found"** en un
> repo privado, es un **glitch de indexado de la UI**, no un muro de plan — el CLI/API lo resuelve igual.

### 4.2 Enfoque elegido: **repo secrets** (uno por repo)

Simple y sin ambigüedad. **Corrés VOS** los comandos (el secret no pasa por logs de terceros):
```bash
# 🔒 El único sensible: SIN --body → prompt oculto (no queda en el historial)
gh secret set PP_CLIENT_SECRET --repo breaking-bytes-dev/cobranza-movil

# 📋 Los no sensibles (identificadores públicos): --body inline está bien
gh secret set DATAVERSE_URL --repo breaking-bytes-dev/cobranza-movil --body "https://orgXXXX.crm.dynamics.com"
gh secret set PP_CLIENT_ID  --repo breaking-bytes-dev/cobranza-movil --body "<client-id>"
gh secret set PP_TENANT_ID  --repo breaking-bytes-dev/cobranza-movil --body "<tenant-id>"

# Verificar
gh secret list --repo breaking-bytes-dev/cobranza-movil
```
> **Costo (asumido a conciencia):** al rotar el `PP_CLIENT_SECRET`, hay que actualizarlo en **cada repo**
> que use el SP (cobranza, smelter, heartbeat…). Para pocos repos es manejable.

### 4.3 Alternativa: **org secrets** (rotación en un solo lugar)

Si más adelante querés centralizar, se define **una vez** a nivel org con acceso selectivo:
```bash
gh secret set PP_CLIENT_SECRET --org breaking-bytes-dev --visibility selected --repos cobranza-movil,smelter
```
Agregar un repo a un org secret ya creado, **sin** re-ingresar el valor (aditivo, vía API):
```bash
rid=$(gh api /repos/breaking-bytes-dev/smelter --jq .id)
gh api -X PUT    "/orgs/breaking-bytes-dev/actions/secrets/PP_CLIENT_SECRET/repositories/$rid"   # sumar
gh api -X DELETE "/orgs/breaking-bytes-dev/actions/secrets/PP_CLIENT_SECRET/repositories/$rid"   # quitar
```

> **Org secret + protección de deploy:** el org secret da el VALOR compartido; si además querés *gating*
> (revisores requeridos antes de importar a Prod), creás un **Environment** en el repo con reglas — el org
> secret se resuelve igual dentro del job. Para este repo privado con workflows `workflow_dispatch`
> manuales, el opt-in ya lo da el disparador manual.
>
> **`PP_CLIENT_SECRET` es el único verdaderamente secreto.** `PP_CLIENT_ID` y `PP_TENANT_ID` son
> identificadores públicos (no son contraseñas); se guardan como secrets solo por prolijidad.

---

## 5. Verificar que la identidad funciona

Antes de meterla en CI, probala localmente:

```bash
pac auth create --name cobranza-sp \
  --applicationId <PP_CLIENT_ID> \
  --clientSecret  <PP_CLIENT_SECRET> \
  --tenant        <PP_TENANT_ID> \
  --environment   <DATAVERSE_URL>

pac org who     # si responde con datos del entorno → la identidad ANDA
```

- [ ] `pac org who` devuelve el nombre/URL del entorno → ✅ SP operativo.
- [ ] En el Admin Center, el application user aparece con su rol asignado.

---

## 6. Gotchas (léelos, te ahorran un día)

- **El secret EXPIRA.** Cuando expira, el pipeline se rompe sin avisar. Ponete un recordatorio ~1 mes
  antes del vencimiento para rotarlo. Documentá la fecha de expiración.
- **El secret se muestra una vez.** Si lo perdés, no lo recuperás: creás uno nuevo y actualizás los secrets.
- **Nunca** commitees el secret. Va solo a GitHub Secrets (por eso `.env` y `*.local.json` están en
  `.gitignore`).
- **Least privilege vs import:** System Customizer exporta pero puede **fallar al importar** soluciones con
  flows/roles/connection references. Para deploy end-to-end, System Administrator es lo práctico.

---

## 7. Trazabilidad

- **Materializa:** doc 10 §Fase 4 (workflows) · decisión "un SP por entorno".
- **Habilita:** `solution-release.yml` y `solution-sync.yml` — sin esta identidad, no corren.
- **Depende de:** ser admin del tenant · entorno Dataverse existente.
