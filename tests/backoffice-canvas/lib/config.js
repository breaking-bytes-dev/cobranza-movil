// Configuración de la suite. NADA sensible vive acá: todo sale de variables de entorno
// o de rutas FUERA del repo (sesiones de navegador y secretos del Service Principal).
const path = require('path');
const os = require('os');

const REPO = path.resolve(__dirname, '..', '..', '..');

const cfg = {
  // Player de la canvas (DEV). Se puede sobreescribir con BO_PLAYER_URL.
  playerUrl: process.env.BO_PLAYER_URL ||
    'https://apps.powerapps.com/play/e/9c8994b8-b1bc-ee23-9184-2f6346502613/a/87fff00d-b086-4655-9166-7a7450015fe5?tenantId=7bcfee03-896e-4ad3-947b-8a2d9ce17974',
  appId: '87fff00d-b086-4655-9166-7a7450015fe5',
  // Carpeta con sesion-user1..4.json (storageState de Playwright). Gitignoreada (local/).
  sesionesDir: process.env.BO_SESIONES_DIR || path.join(REPO, 'local'),
  // Archivo .env con DATAVERSE_URL / PP_CLIENT_ID / PP_TENANT_ID / PP_CLIENT_SECRET (gitignoreado).
  secretsFile: process.env.BO_SECRETS_FILE || path.join(REPO, 'pp_secrets.env'),
  // Resultados (screenshots, logs de red, JSON). Por defecto: resultados/ (gitignoreada).
  resultadosDir: process.env.BO_RESULTADOS_DIR || path.join(__dirname, '..', 'resultados'),
  // Tiempos del player (carga en frío ~45 s, OnVisible ~20 s).
  tCargaMs: Number(process.env.BO_T_CARGA_MS || 60000),
  tOnVisibleMs: Number(process.env.BO_T_ONVISIBLE_MS || 25000),
  headless: process.env.BO_HEADED ? false : true,
  prefijoQA: 'QA-E2E',
};

// Usuarios por rol. La sesión "admin" está PROHIBIDA en esta suite (solo Studio).
cfg.usuarios = {
  user1: { alias: 'U-BO', upn: 'User1@55xljh.onmicrosoft.com', id: '592f1177-6116-f111-8342-000d3a316609', rol: 'BackOffice' },
  user2: { alias: 'U-SOL', upn: 'User2@55xljh.onmicrosoft.com', id: '7f9ef595-6116-f111-8342-000d3a316609', rol: 'Solicitante' },
  user3: { alias: 'U-SUP', upn: 'User3@55xljh.onmicrosoft.com', id: '75c684ab-6116-f111-8342-000d3a316609', rol: 'Supervisor' },
  user4: { alias: 'U-ADM', upn: 'User4@55xljh.onmicrosoft.com', id: 'f78065ba-6116-f111-8342-000d3a316609', rol: 'Admin' },
};

cfg.teams = {
  Backoffice: '7e0f4531-a38a-f111-8076-70a8a59c703e',
  Solicitantes: '810f4531-a38a-f111-8076-70a8a59c703e',
  Supervisores: '840f4531-a38a-f111-8076-70a8a59c703e',
  AdminCarga: '870f4531-a38a-f111-8076-70a8a59c703e',
};

// Las membresías NO se hardcodean: run.js toma una foto al inicio y la restaura al final.

cfg.sesionPath = (u) => {
  if (/admin/i.test(u)) throw new Error('La sesión admin está prohibida en la suite E2E');
  return path.join(cfg.sesionesDir, `sesion-${u}.json`);
};

module.exports = cfg;
