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

## 4. Cargar los secrets en GitHub

Los workflows leen 4 valores desde un **Environment `dev`** del repo (patrón sigil: los jobs con entorno
real son opt-in, los secrets no se exponen a PRs de forks).

1. GitHub → repo `cobranza-movil` → **Settings → Environments → New environment** → nombre **`dev`**.
2. Dentro de `dev` → **Add environment secret** (cuatro veces):

   | Secret | Valor |
   |--------|-------|
   | `DATAVERSE_URL` | URL del entorno (`https://orgXXXX.crm.dynamics.com`) |
   | `PP_CLIENT_ID` | Application (Client) ID |
   | `PP_CLIENT_SECRET` | el Client Secret (Value) |
   | `PP_TENANT_ID` | Directory (Tenant) ID |

> El SP es **el mismo** para todos los repos del entorno. En cuenta personal (`bitbreaker01`), replicás
> estos 4 secrets en cada repo. Si algún día es **organización**, usás *org secrets* scopeados y los
> definís una sola vez.

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
