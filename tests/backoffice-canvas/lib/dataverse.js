// Cliente mínimo de la Web API de Dataverse con el Service Principal (client_credentials).
// Lee las credenciales del archivo de secretos (fuera del repo) SIN imprimirlas nunca.
const fs = require('fs');
const cfg = require('./config');

function leerSecretos() {
  const txt = fs.readFileSync(cfg.secretsFile, 'utf8').replace(/\r/g, '');
  const out = {};
  for (const linea of txt.split('\n')) {
    const m = linea.match(/^(DATAVERSE_URL|PP_CLIENT_ID|PP_TENANT_ID|PP_CLIENT_SECRET)=(.*)$/);
    if (m) out[m[1]] = m[2].trim().replace(/^["']|["']$/g, '');
  }
  for (const k of ['DATAVERSE_URL', 'PP_CLIENT_ID', 'PP_TENANT_ID', 'PP_CLIENT_SECRET'])
    if (!out[k]) throw new Error(`Falta ${k} en el archivo de secretos`);
  return out;
}

const sec = leerSecretos();
const ORG = sec.DATAVERSE_URL.replace(/\/+$/, '');
const API = `${ORG}/api/data/v9.2`;
let token = null, tokenExp = 0;

async function getToken() {
  if (token && Date.now() < tokenExp - 60000) return token;
  const body = new URLSearchParams({
    grant_type: 'client_credentials', client_id: sec.PP_CLIENT_ID,
    client_secret: sec.PP_CLIENT_SECRET, scope: `${ORG}/.default`,
  });
  const r = await fetch(`https://login.microsoftonline.com/${sec.PP_TENANT_ID}/oauth2/v2.0/token`, { method: 'POST', body });
  const j = await r.json();
  if (!j.access_token) throw new Error(`Token SP falló: ${j.error || r.status}`);
  token = j.access_token; tokenExp = Date.now() + (j.expires_in || 3600) * 1000;
  return token;
}

async function req(method, rel, body, extraHeaders = {}) {
  const url = rel.startsWith('http') ? rel : `${API}/${rel.replace(/^\//, '')}`;
  const headers = {
    Authorization: `Bearer ${await getToken()}`, Accept: 'application/json',
    'OData-MaxVersion': '4.0', 'OData-Version': '4.0',
    Prefer: 'odata.include-annotations="*"', ...extraHeaders,
  };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const r = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const txt = await r.text();
  let json = null; try { json = txt ? JSON.parse(txt) : null; } catch (e) { /* no-json */ }
  if (!r.ok) {
    const msg = json?.error?.message || txt.slice(0, 300);
    const e = new Error(`${method} ${rel.slice(0, 120)} → ${r.status}: ${msg}`); e.status = r.status; throw e;
  }
  return { status: r.status, json, headers: r.headers };
}

const dv = {
  ORG, API,
  get: async (rel) => (await req('GET', rel)).json,
  // Todas las páginas (sigue @odata.nextLink).
  getAll: async (rel) => {
    let out = [], next = rel;
    while (next) { const j = (await req('GET', next, undefined, { Prefer: 'odata.include-annotations="*",odata.maxpagesize=5000' })).json; out = out.concat(j.value); next = j['@odata.nextLink']; }
    return out;
  },
  // Conteo EXACTO: pide solo la PK con $count=true y recorre páginas (no depende del tope de 5000 de $count).
  count: async (entitySet, filter, pk) => {
    const f = filter ? `&$filter=${encodeURIComponent(filter)}` : '';
    const rows = await dv.getAll(`${entitySet}?$select=${pk}${f}`);
    return rows.length;
  },
  create: async (entitySet, data) => {
    const r = await req('POST', entitySet, data, { Prefer: 'return=representation' });
    return r.json;
  },
  patch: async (entitySet, id, data) => (await req('PATCH', `${entitySet}(${id})`, data)).status,
  del: async (entitySet, id) => {
    try { return (await req('DELETE', `${entitySet}(${id})`)).status; }
    catch (e) { if (e.status === 404) return 404; throw e; }
  },
  post: async (rel, data) => (await req('POST', rel, data)).json,
  delRef: async (rel) => {
    try { return (await req('DELETE', rel)).status; }
    catch (e) { if (e.status === 404) return 404; throw e; }
  },
  raw: req,
  // Sube bytes a una columna de archivo (PATCH octet-stream + x-ms-file-name). Lo usa tests/integral para
  // cargar el CSV en el job de Smelter (sanic_smlt_ij_sourcefile). `req` fuerza JSON, por eso va aparte.
  subirArchivo: async (rel, buffer, nombreArchivo) => {
    const r = await fetch(`${API}/${rel.replace(/^\//, '')}`, {
      method: 'PATCH', body: buffer,
      headers: { Authorization: `Bearer ${await getToken()}`, 'Content-Type': 'application/octet-stream',
        'x-ms-file-name': nombreArchivo, 'OData-MaxVersion': '4.0', 'OData-Version': '4.0' },
    });
    if (!r.ok) { const e = new Error(`PATCH ${rel.slice(0, 120)} → ${r.status}: ${(await r.text()).slice(0, 300)}`); e.status = r.status; throw e; }
    return r.status;
  },
};

module.exports = dv;
