# Pruebas web de la app del cobrador (Playwright)

Prueban en el **web player** lo que no necesita el teléfono. Son rápidas y se pueden repetir.
**Ojo:** el web player corre la app **en línea**: no hay base local ni sincronización, y cada Patch va directo al servidor.
Lo que depende del modo offline (estado de sincronización, N° de interacción vacío, `Created On` local, GPS real,
botón Atrás físico de Android) se verifica en el teléfono.

## Requisitos
- Dependencias del arnés del backoffice (`tests/backoffice-canvas/node_modules`, Playwright con Chromium).
- Sesión web de **User5** en `local/sesion-user5.json` (gitignoreada). Se genera **en tu máquina**:
  ```bash
  npx playwright codegen --save-storage=sesion-user5.json "https://apps.powerapps.com/play/e/9c8994b8-b1bc-ee23-9184-2f6346502613/a/dc5e56bb-9598-4f68-baa5-c04dc56ccba4?tenantId=7bcfee03-896e-4ad3-947b-8a2d9ce17974"
  ```
  Iniciás sesión como User5, esperás a que cargue la app y cerrás el navegador. Después copiás el archivo a `local/` con `chmod 600`.
- `pp_secrets.env` (Service Principal) para cruzar los resultados contra Dataverse.

## Uso
```bash
cd tests/localizador-canvas
NODE_PATH=../backoffice-canvas/node_modules node humo.js     # abre la app y saca una captura
NODE_PATH=../backoffice-canvas/node_modules node grupo2.js   # APP-07, 15, 16, 17, 18, 19, 24, 25, 27, 28, 30, 32 y formatos
```
Resultados y capturas en `resultados/` (gitignoreada). `grupo2.js` crea **1 interacción QA** por corrida en la cartera
QA-E2E-LOC de User5 (comentario "QA web APP-27 agente").

## Detalles del player que conviene saber
- Cada control expone su nombre de Studio en `data-control-name`. `lib/app.js` → `pantalla(app)` reconoce la pantalla visible.
- Las tarjetas tienen un botón transparente encima del texto: el clic por texto va con `force: true` y cae en ese botón.
- El texto de un botón clásico está en `.appmagic-button-label`, y el valor de un desplegable o campo moderno en el `value` de su `<input>`.
- Los botones clásicos **no** admiten `AccessibleLabel` (Studio rechaza la app). Se anuncian por su texto.
