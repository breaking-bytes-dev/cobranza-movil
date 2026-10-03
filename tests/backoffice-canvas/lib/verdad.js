// "Verdad del servidor": conteos y sumas EXACTOS por Web API con la MISMA condición que usa la app.
const dv = require('./dataverse');

const TZ = 'America/Managua';

// Fecha local de hoy (YYYY-MM-DD) y su 00:00 local expresado en UTC — equivale a Today() del player
// (el contexto de Playwright corre con timezoneId America/Managua).
function hoy() {
  const now = new Date();
  const fecha = new Intl.DateTimeFormat('en-CA', { timeZone: TZ, year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
  const medUTC = new Date(`${fecha}T00:00:00Z`);
  const off = (new Date(medUTC.toLocaleString('en-US', { timeZone: TZ })) - new Date(medUTC.toLocaleString('en-US', { timeZone: 'UTC' }))) / 60000;
  return { fecha, inicioUTC: new Date(medUTC.getTime() - off * 60000).toISOString().replace('.000', '') };
}

const cnt = (set, pk, f) => dv.count(set, f, pk);
const I = (f) => cnt('sanic_cm_tbl_interaccions', 'sanic_cm_tbl_interaccionid', f);
const A = (f) => cnt('sanic_cm_tbl_asignacions', 'sanic_cm_tbl_asignacionid', f);

async function suma(set, campo, filtro) {
  const j = await dv.get(`${set}?$apply=filter(${encodeURIComponent(filtro)})/aggregate(${campo} with sum as total)`);
  return Number(j.value?.[0]?.total || 0);
}

async function ultimaCarga() {
  const j = await dv.get('sanic_cm_tbl_cargadiarias?$select=sanic_cm_cd_fechacarga&$orderby=sanic_cm_cd_fechacarga desc&$top=1');
  return j.value[0]?.sanic_cm_cd_fechacarga || null; // 'YYYY-MM-DDT00:00:00Z' (DateOnly)
}

const v = {
  hoy,
  ultimaCarga,
  pendientes: () => I('sanic_cm_in_estadograbacion eq 1'),
  pendientesConPago: () => I('sanic_cm_in_estadograbacion eq 1 and sanic_cm_cd_aplicapago eq true'),
  transcritasHoy: (userId) => I(`sanic_cm_in_estadograbacion eq 2 and sanic_cm_in_fechagrabacion ge ${hoy().inicioUTC} and _sanic_cm_in_grabadopor_value eq ${userId}`),
  especialesDe: (userId, estadoAprob) => A(`sanic_cm_as_tipo eq 3 and _sanic_cm_as_solicitadopor_value eq ${userId} and sanic_cm_as_estadoaprobacion eq ${estadoAprob}`),
  porAprobar: () => A('sanic_cm_as_tipo eq 3 and sanic_cm_as_estadoaprobacion eq 2'),
  diariasActivas: () => A('sanic_cm_as_tipo eq 1 and sanic_cm_as_estado eq true'),
  // Corte = 00:00 UTC del día de la última carga (misma regla que la app, 00 §4.4).
  desaparecidas: async () => {
    const uc = await ultimaCarga(); if (!uc) return null;
    return A(`sanic_cm_as_tipo eq 1 and sanic_cm_as_estado eq true and sanic_cm_as_ultimacarga lt ${uc.slice(0, 10)}T00:00:00Z`);
  },
  incobrablesActivas: () => A('sanic_cm_as_tipo eq 2 and sanic_cm_as_estado eq true'),
  especialesActivas: () => A('sanic_cm_as_tipo eq 3 and sanic_cm_as_estado eq true'),
  agentesSinUsuario: () => cnt('sanic_cm_tbl_agentes', 'sanic_cm_tbl_agenteid', 'sanic_cm_ag_activo eq true and sanic_cm_ag_emailusuario eq null'),
  nombresAgentesSinUsuario: async () => (await dv.getAll("sanic_cm_tbl_agentes?$select=sanic_cm_ag_nombre&$filter=sanic_cm_ag_activo eq true and sanic_cm_ag_emailusuario eq null")).map(a => a.sanic_cm_ag_nombre),
  cuentas: () => cnt('sanic_cm_tbl_cuentas', 'sanic_cm_tbl_cuentaid', null),
  clientes: () => cnt('sanic_cm_tbl_clientes', 'sanic_cm_tbl_clienteid', null),
  // FechaCarga es DateOnly → la condición CORRECTA de "carga de hoy" compara contra la FECHA.
  cargaHoy: () => cnt('sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_cargadiariaid', `sanic_cm_cd_fechacarga ge ${hoy().fecha}`),
  // La condición que el player ENVÍA (Today() local → instante UTC) — para documentar la diferencia.
  cargaHoyComoLaApp: () => cnt('sanic_cm_tbl_cargadiarias', 'sanic_cm_tbl_cargadiariaid', `sanic_cm_cd_fechacarga ge ${hoy().inicioUTC}`),
  cobradoHoy: async () => ({
    nio: await suma('sanic_cm_tbl_pagos', 'sanic_cm_pg_monto', `sanic_cm_pg_fecha ge ${hoy().inicioUTC} and sanic_cm_pg_moneda eq 1`),
    usd: await suma('sanic_cm_tbl_pagos', 'sanic_cm_pg_monto', `sanic_cm_pg_fecha ge ${hoy().inicioUTC} and sanic_cm_pg_moneda eq 2`),
  }),
};

// Réplica de Moneda() de App.Formulas: "C$ X · US$ Y", omite la moneda en cero.
function moneda(nio, usd) {
  const f = n => n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const p = []; if (nio > 0) p.push(`C$ ${f(nio)}`); if (usd > 0) p.push(`US$ ${f(usd)}`);
  return p.length ? p.join(' · ') : 'C$ 0.00';
}
const miles = n => Number(n).toLocaleString('en-US');

module.exports = { ...v, moneda, miles };
