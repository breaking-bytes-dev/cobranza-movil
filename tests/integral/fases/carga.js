// F0 Preparación · F1 Carga por Smelter · F2 Reparto automático (plugin RepartoAsignacion).
const fs = require('fs');
const path = require('path');
const dv = require('../../backoffice-canvas/lib/dataverse');
const v = require('../../backoffice-canvas/lib/verdad');
const { PREFIJO, AGENTE, U, ES, esperar, dormir } = require('../lib/ctx');
const csv = require('../lib/csv');
const smelter = require('../lib/smelter');
const datos = require('../lib/datos');

const luhnOk = (n) => { const d = [...n].map(Number).reverse(); return d.reduce((a, x, i) => a + (i % 2 ? (x * 2 > 9 ? x * 2 - 9 : x * 2) : x), 0) % 10 === 0; };
const HEX64 = /^[0-9a-f]{64}$/;
const tipoNombre = { 1: 'Diaria', 2: 'Incobrable', 3: 'Especial' };

// ── F0 ─────────────────────────────────────────────────────────────────────────────────────────
async function f0(ctx, { conservarCsv = false } = {}) {
  const g = csv.generar(ctx);
  ctx.csvPath = path.join(ctx.dir, `carga-${PREFIJO}-${ctx.runid}.csv`);
  fs.writeFileSync(ctx.csvPath, g.contenido, { mode: 0o600 });
  ctx.secretos = { pans: Object.fromEntries(g.cuentas.map((c) => [c.clave, c.pan])), contenido: g.contenido };
  // Lo esperado (sin PAN) queda en ctx.datos; los PAN solo en memoria (ctx.secretos).
  ctx.datos.cuentas = g.cuentas.map(({ pan, ...resto }) => resto);
  ctx.datos.cliente = { nombre: ctx.nombreCliente, cif: ctx.cif };
  ctx.datos.hoy = ctx.hoy.fecha;

  const lineas = g.contenido.toString('utf8').split('\r\n').filter(Boolean);
  const mock = path.join(__dirname, '..', '..', '..', 'test-data', 'cobranza-mock-1000.csv');
  const encMock = fs.existsSync(mock) ? fs.readFileSync(mock, 'utf8').split(/\r?\n/)[0] : csv.ENCABEZADO;
  ctx.chk('F0-01', g.columnas === 33 && lineas[0] === encMock && lineas.length === 4 && lineas.every((l) => l.split(',').length === 33),
    `CSV QA generado: 33 columnas, encabezado idéntico al del mock, 3 filas (A Diaria, B Incobrable, C Especial) — ${path.relative(path.join(__dirname, '..'), ctx.csvPath)}`);
  const pans = Object.values(ctx.secretos.pans);
  ctx.chk('F0-02', pans.every((p) => /^9999\d{12}$/.test(p) && luhnOk(p)) && new Set(g.cuentas.map((c) => c.ult4)).size === 3,
    `PAN ficticios: BIN 9999, 16 dígitos, Luhn válido, últimos 4 distintos (${g.cuentas.map((c) => `${c.clave}=****${c.ult4}`).join(' · ')})`);
  ctx.chk('F0-03', true, `valores esperados registrados: cliente "${ctx.nombreCliente}" CIF ${ctx.cif}, agente "${AGENTE}", FechaCarga ${ctx.hoy.fecha}, Especial C retiro C$ 450 tipo VIP`);
  if (!conservarCsv) ctx.nota('El CSV se borra apenas termina la importación (F1) o, a más tardar, en la limpieza.');
  else ctx.nota(`CSV conservado (--hasta F0): ${ctx.csvPath}. Contiene números de tarjeta ficticios; borralo al terminar (node run.js --solo-limpieza).`);
}

// ── F1 ─────────────────────────────────────────────────────────────────────────────────────────
const SEL_CT = 'sanic_cm_tbl_cuentaid,sanic_cm_ct_numerodecuenta,sanic_cm_ct_ultimos4,sanic_cm_ct_clavepan,sanic_cm_ct_nombrecliente,sanic_cm_ct_cifcliente,sanic_cm_ct_saldovencidonio,sanic_cm_ct_diasmora,sanic_cm_ct_tipoespecial,sanic_cm_ct_nota,sanic_cm_ct_retironio,_sanic_cm_ct_productoid_value,_sanic_cm_ct_fiador_value,_ownerid_value';
const SEL_AS = 'sanic_cm_tbl_asignacionid,sanic_cm_as_numero,sanic_cm_as_tipo,sanic_cm_as_estado,sanic_cm_as_estadoaprobacion,sanic_cm_as_clavepan,sanic_cm_as_retironio,sanic_cm_as_notas,sanic_cm_as_ultimacarga,sanic_cm_as_fechaasignada,_sanic_cm_as_agenteid_value,_sanic_cm_as_cuentaid_value,_ownerid_value';

async function f1(ctx) {
  ctx.requiere(ctx.secretos, 'F0 no generó el CSV');
  const key = csv.claveHmac();
  const D = ctx.datos;
  let imp1;
  try {
    // ── Importación 1 ──
    // --carga-ui: la 1.ª importación la hace User4 desde la pantalla "Cargar archivo" (prueba en seco + carga real); sin el flag, por la API.
    imp1 = ctx.cargaUi
      ? await ctx.paso('F1-01', 'importar por la pantalla «Cargar archivo» (User4)', () => require('../lib/cargaui').importarPorPantalla(ctx, ctx.csvPath))
      : await ctx.paso('F1-01', 'importar por Smelter', () => smelter.importar(ctx, ctx.secretos.contenido, 1));
  } finally {
    // El CSV tiene números completos: se borra apenas se usó, pase lo que pase.
    try { fs.unlinkSync(ctx.csvPath); } catch (e) { /* ya no estaba */ }
  }
  ctx.requiere(imp1, 'la importación 1 no terminó');
  const j1 = imp1.job;
  ctx.chk('F1-01', j1.sanic_smlt_ij_status === 2 && !j1.sanic_smlt_ij_failedcount && j1.sanic_smlt_ij_totalrows === 3,
    `job 1 Completed (2), 3 filas, 0 fallidas — ${smelter.resumenJob(j1)}`);
  ctx.chk('F1-02', imp1.logs.length === 0, `job logs sin fallos (${imp1.logs.length})${imp1.logs.length ? ': ' + smelter.textoLogs(imp1.logs) : ''}`);
  ctx.chk('F1-03', !imp1.logs.some((l) => /\d{13,19}/.test(l.sanic_smlt_ijl_errormessage || '')), 'los logs de la carga no traen secuencias de 13 a 19 dígitos');
  ctx.chk('F1-04', !fs.existsSync(ctx.csvPath), 'el CSV con números ficticios ya se borró del disco');
  D.tCarga = Date.now();

  // ── Cliente ──
  const cli = (await dv.get(`sanic_cm_tbl_clientes?$select=sanic_cm_tbl_clienteid,sanic_cm_cl_nombre,sanic_cm_cl_cif&$filter=sanic_cm_cl_cif eq '${ctx.cif}'`)).value;
  ctx.chk('F1-05', cli.length === 1 && cli[0].sanic_cm_cl_nombre === ctx.nombreCliente, `cliente "${ctx.nombreCliente}" creado (CIF ${ctx.cif}): ${cli.length}`);
  ctx.requiere(cli.length === 1, 'no se creó el cliente');
  D.cliente.id = cli[0].sanic_cm_tbl_clienteid; ctx.registrar('cliente', D.cliente.id);

  // ── Cuentas ──
  const cts = (await dv.get(`sanic_cm_tbl_cuentas?$select=${SEL_CT}&$filter=_sanic_cm_ct_clienteid_value eq ${D.cliente.id}`)).value;
  ctx.chk('F1-06', cts.length === 3, `3 cuentas del cliente (${cts.length})`);
  for (const e of D.cuentas) {
    const ct = cts.find((x) => x.sanic_cm_ct_ultimos4 === e.ult4);
    if (!ct) { ctx.chk(`F1-07${e.clave}`, false, `cuenta ${e.clave} (****${e.ult4}) no existe`); continue; }
    e.id = ct.sanic_cm_tbl_cuentaid;
    const enmasc = ct.sanic_cm_ct_numerodecuenta === `****${e.ult4}`;
    const hex = HEX64.test(ct.sanic_cm_ct_clavepan || '');
    const hm = key ? ct.sanic_cm_ct_clavepan === csv.hmacPan(key, ctx.secretos.pans[e.clave]) : null;
    ctx.chk(`F1-07${e.clave}`, enmasc && ct.sanic_cm_ct_ultimos4 === e.ult4 && hex && hm !== false,
      `cuenta ${e.clave}: numerodecuenta enmascarado (****${e.ult4}) ${enmasc ? 'sí' : 'NO'} · ultimos4 ${ct.sanic_cm_ct_ultimos4} · clavepan 64 hex ${hex ? 'sí' : 'NO'} · HMAC ${hm === null ? 'no verificado (sin llave)' : hm ? 'correcto' : 'DISTINTO'}`);
    ctx.chk(`F1-08${e.clave}`, ct.sanic_cm_ct_nombrecliente === ctx.nombreCliente && ct.sanic_cm_ct_cifcliente === ctx.cif && Number(ct.sanic_cm_ct_saldovencidonio) === e.saldoNio && ct.sanic_cm_ct_diasmora === e.dias && !!ct._sanic_cm_ct_productoid_value && !!ct._sanic_cm_ct_fiador_value,
      `cuenta ${e.clave}: cliente/CIF, saldo C$ ${e.saldoNio}, ${e.dias} días de mora, producto y fiador enlazados`);
    e.clavepan = ct.sanic_cm_ct_clavepan;
    e.ownerCuenta = ct._ownerid_value;
    ctx.registrar('cuenta', e.id, e.clave);
  }
  D.productoId = cts[0]?._sanic_cm_ct_productoid_value; D.fiadorId = cts[0]?._sanic_cm_ct_fiador_value;

  // ── Bóveda 1:1 ──
  const cuentaIds = D.cuentas.map((e) => e.id).filter(Boolean);
  const bov = cuentaIds.length ? (await dv.get(`sanic_cm_tbl_cuentapans?$select=sanic_cm_tbl_cuentapanid,sanic_cm_cp_name,sanic_cm_cp_clavepan,_sanic_cm_cp_cuentaid_value&$filter=${encodeURIComponent(datos.orEq('_sanic_cm_cp_cuentaid_value', cuentaIds))}`)).value : [];
  ctx.chk('F1-09', bov.length === 3 && new Set(bov.map((b) => b._sanic_cm_cp_cuentaid_value)).size === 3, `bóveda 1:1 con las cuentas (${bov.length} filas)`);
  for (const e of D.cuentas) {
    if (!e.id) continue;
    const b = bov.find((x) => x._sanic_cm_cp_cuentaid_value === e.id);
    // cp_pan se lee aparte, se compara en memoria y NUNCA se imprime
    let panOk = false, largo = 0;
    if (b) { const p = (await dv.get(`sanic_cm_tbl_cuentapans(${b.sanic_cm_tbl_cuentapanid})?$select=sanic_cm_cp_pan`)).sanic_cm_cp_pan || ''; largo = p.length; panOk = p === ctx.secretos.pans[e.clave]; }
    ctx.chk(`F1-10${e.clave}`, !!b && b.sanic_cm_cp_name === `****${e.ult4}` && largo === 16 && panOk && b.sanic_cm_cp_clavepan === e.clavepan,
      `bóveda ${e.clave}: cp_name enmascarado (****${e.ult4}), cp_pan de 16 dígitos = el del CSV, clavepan = la de la cuenta`);
    if (b) ctx.registrar('boveda', b.sanic_cm_tbl_cuentapanid, e.clave);
  }

  // ── Asignaciones ──
  const asg = cuentaIds.length ? (await dv.get(`sanic_cm_tbl_asignacions?$select=${SEL_AS}&$filter=${encodeURIComponent(datos.orEq('_sanic_cm_as_cuentaid_value', cuentaIds))}`)).value : [];
  const ag = (await dv.get(`sanic_cm_tbl_agentes?$select=sanic_cm_tbl_agenteid,_sanic_cm_ag_usuarioid_value&$filter=sanic_cm_ag_nombre eq '${AGENTE}'`)).value[0];
  D.agente = { id: ag?.sanic_cm_tbl_agenteid, usuario: ag?._sanic_cm_ag_usuarioid_value };
  ctx.chk('F1-11', asg.length === 3, `3 asignaciones (${asg.length})`);
  for (const e of D.cuentas) {
    const a = asg.find((x) => x._sanic_cm_as_cuentaid_value === e.id);
    if (!a) { ctx.chk(`F1-12${e.clave}`, false, `asignación ${e.clave} no existe`); continue; }
    e.asigId = a.sanic_cm_tbl_asignacionid; e.asigNum = a.sanic_cm_as_numero; e.ownerInicial = a._ownerid_value;
    ctx.registrar('asignacion', e.asigId, e.clave);
    ctx.chk(`F1-12${e.clave}`, a.sanic_cm_as_tipo === e.tipoCod && a._sanic_cm_as_agenteid_value === D.agente.id && a.sanic_cm_as_estado === true && a.sanic_cm_as_clavepan === e.clavepan,
      `asignación ${e.clave} (${a.sanic_cm_as_numero}): tipo ${tipoNombre[a.sanic_cm_as_tipo]} (esperado ${e.tipo}), agente "${AGENTE}", activa, clavepan = la de la cuenta`);
    e.aprobInicial = a.sanic_cm_as_estadoaprobacion;
    ctx.nota(`asignación ${e.clave} ${a.sanic_cm_as_numero}: estadoaprobacion=${a.sanic_cm_as_estadoaprobacion} · dueño inicial ${e.ownerInicial} · ultimacarga ${a.sanic_cm_as_ultimacarga} · notas "${a.sanic_cm_as_notas ?? ''}" · retiro ${a.sanic_cm_as_retironio ?? ''}`);
  }
  const C = D.cuentas.find((e) => e.clave === 'C');
  const aC = asg.find((x) => x._sanic_cm_as_cuentaid_value === C?.id);
  ctx.chk('F1-13', !!aC && aC.sanic_cm_as_estadoaprobacion === 2, `LA CLAVE: la Especial C del CSV nace Pendiente de aprobación (2) → entra a la cola del Supervisor (obtenido ${aC?.sanic_cm_as_estadoaprobacion})`);
  // Smelter mapea RetiroNIO / TipoEspecial / Nota SOLO a la cuenta (DAT-01, documentado): la asignación queda sin retiro ni notas.
  const ctC = cts.find((x) => x.sanic_cm_tbl_cuentaid === C?.id) || {};
  ctx.chk('F1-14', Number(ctC.sanic_cm_ct_retironio) === 450 && ctC.sanic_cm_ct_tipoespecial === 'VIP' && (ctC.sanic_cm_ct_nota || '').includes(ctx.nombreCliente),
    `Especial C: los datos del CSV llegaron a la cuenta (retiro C$ ${ctC.sanic_cm_ct_retironio ?? ''} · tipo "${ctC.sanic_cm_ct_tipoespecial ?? ''}" · nota "${ctC.sanic_cm_ct_nota ?? ''}")`);
  ctx.nota(`DAT-01 (conocido): la asignación C ${C?.asigNum} llegó con retiro "${aC?.sanic_cm_as_retironio ?? ''}" y notas "${aC?.sanic_cm_as_notas ?? ''}" (la plantilla de Smelter mapea esos datos solo a la cuenta)`);

  // ── Carga diaria ──
  const cds = cuentaIds.length ? await dv.getAll(`sanic_cm_tbl_cargadiarias?$select=sanic_cm_tbl_cargadiariaid,sanic_cm_cd_clavepan,sanic_cm_cd_fechacarga,sanic_cm_cd_numerocuenta&$filter=${encodeURIComponent(`(${datos.orEq('_sanic_cm_cd_cuenta_value', cuentaIds)}) and sanic_cm_cd_fechacarga ge ${ctx.hoy.fecha}`)}`) : [];
  ctx.chk('F1-15', cds.length === 3 && cds.every((x) => D.cuentas.some((e) => e.clavepan === x.sanic_cm_cd_clavepan) && /^\*{4}\d{4}$/.test(x.sanic_cm_cd_numerocuenta || '')),
    `3 filas de carga diaria con FechaCarga de hoy (${ctx.hoy.fecha}), clavepan de las cuentas y número enmascarado (${cds.length})`);
  for (const x of cds) ctx.registrar('carga', x.sanic_cm_tbl_cargadiariaid);
  D.cargaHoyAntes = await v.cargaHoy();

  // ── Direcciones, teléfonos, producto, fiador (registro para limpieza) ──
  const base = await datos.conteosCarga(ctx);
  ctx.nota(`conteos tras la importación 1: ${JSON.stringify(base)}`);
  ctx.chk('F1-16', base.productos === 1 && base.fiadores === 1 && base.direcciones >= 1 && base.telefonos >= 1, `producto y fiador QA creados (1 y 1), direcciones ${base.direcciones}, teléfonos ${base.telefonos}`);
  D.conteosBase = base;

  D.tCarga = Date.now();
}

// ── F2 ─────────────────────────────────────────────────────────────────────────────────────────
const ownerDe = async (id) => (await dv.get(`sanic_cm_tbl_asignacions(${id})?$select=_ownerid_value`))._ownerid_value;

async function f2(ctx) {
  const D = ctx.datos; ctx.requiere(D.cuentas?.every((e) => e.asigId), 'F1 no dejó las asignaciones');
  const [A, B, C] = ['A', 'B', 'C'].map((k) => D.cuentas.find((e) => e.clave === k)); const Cc2 = C;
  const pend = new Set([A, B]);
  const t0 = Date.now();
  await esperar(async () => {
    for (const e of [...pend]) if ((await ownerDe(e.asigId)) === U.cob) { e.tOwner = Math.round((Date.now() - t0) / 1000); pend.delete(e); }
    return pend.size === 0;
  }, { maxMs: 180000, cadaMs: 8000 });
  for (const e of [A, B]) ctx.chk(`F2-0${e.clave === 'A' ? 1 : 2}`, !pend.has(e), `${e.clave} (${e.tipo}, ${e.asigNum}): el reparto pasó el dueño a User5 ${pend.has(e) ? 'NO ocurrió en 3 min' : `en ~${e.tOwner} s`}`);
  // C (Especial Pendiente): ~2 min después de la carga el dueño NO tiene que haber cambiado a User5.
  const falta = 120000 - (Date.now() - D.tCarga);
  if (falta > 0) { ctx.nota(`esperando ${Math.round(falta / 1000)} s para cumplir los ~2 min de la Especial C`); await dormir(falta); }
  const oC = await ownerDe(C.asigId);
  ctx.chk('F2-03', oC !== U.cob && oC === C.ownerInicial, `C (Especial Pendiente ${C.asigNum}): sigue con el dueño de la carga (${oC === C.ownerInicial ? 'sin cambios' : 'CAMBIÓ'}) y NO es User5 — compuerta S-6 del reparto`);
  // lectura suplantando a User5: ve A y B, no ve C
  const veA = await datos.leerComo(U.cob, `sanic_cm_tbl_cuentas(${A.id})?$select=sanic_cm_tbl_cuentaid`);
  const veB = await datos.leerComo(U.cob, `sanic_cm_tbl_cuentas(${B.id})?$select=sanic_cm_tbl_cuentaid`);
  const veC = await datos.leerComo(U.cob, `sanic_cm_tbl_cuentas(${C.id})?$select=sanic_cm_tbl_cuentaid`);
  ctx.chk('F2-04', veA.status === 200 && veB.status === 200 && veC.status !== 200, `User5 (suplantado) lee las cuentas A (${veA.status}) y B (${veB.status}) y NO la C (${veC.status}) — compartido por el reparto`);

  // ── Segunda carga con el cliente YA compartido (lo que hace cada carga diaria) ──
  // Determinista para el defecto 2 (2026-10-06): con Reparent = Cascade en cliente→cuenta y cuenta→asignación, re-escribir
  // la cuenta le daba al cobrador lectura HEREDADA de las cuentas hermanas (la C, con su bóveda y su asignación) aunque no
  // estuvieran asignadas a él. Antes la segunda carga iba en F1 y el defecto dependía de una carrera con el flow.
  const cli = await esperar(async () => (await datos.leerComo(U.cob, `sanic_cm_tbl_clientes(${D.cliente.id})?$select=sanic_cm_tbl_clienteid`)).status === 200, { maxMs: 180000, cadaMs: 8000 });
  ctx.chk('F2-05', !!cli, 'el reparto ya compartió el cliente con User5 (condición de la carga diaria)');
  const base = D.conteosBase;
  const imp2 = await smelter.importar(ctx, ctx.secretos.contenido, 2);
  const j2 = imp2.job;
  ctx.chk('F2-06', j2.sanic_smlt_ij_status === 2 && !j2.sanic_smlt_ij_failedcount && imp2.logs.length === 0, `importación 2 (mismo CSV) Completed, 0 fallidas, 0 logs — ${smelter.resumenJob(j2)}${imp2.logs.length ? ' · ' + smelter.textoLogs(imp2.logs) : ''}`);
  const despues = await datos.conteosCarga(ctx);
  ctx.chk('F2-07', JSON.stringify(despues) === JSON.stringify(base), `idempotente: los conteos no cambian (${JSON.stringify(despues)})`);
  const cts2 = (await dv.get(`sanic_cm_tbl_cuentas?$select=sanic_cm_tbl_cuentaid&$filter=_sanic_cm_ct_clienteid_value eq ${D.cliente.id}`)).value.map((x) => x.sanic_cm_tbl_cuentaid).sort();
  ctx.chk('F2-08', JSON.stringify(cts2) === JSON.stringify(D.cuentas.map((e) => e.id).sort()), 'las 3 cuentas siguen siendo las mismas (mismos ids)');
  const bovC = (await dv.get(`sanic_cm_tbl_cuentapans?$select=sanic_cm_tbl_cuentapanid&$filter=_sanic_cm_cp_cuentaid_value eq ${C.id}`)).value[0].sanic_cm_tbl_cuentapanid;
  const r2 = {
    cuenta: (await datos.leerComo(U.cob, `sanic_cm_tbl_cuentas(${C.id})?$select=sanic_cm_tbl_cuentaid`)).status,
    boveda: (await datos.leerComo(U.cob, `sanic_cm_tbl_cuentapans(${bovC})?$select=sanic_cm_tbl_cuentapanid`)).status,
    asignacion: (await datos.leerComo(U.cob, `sanic_cm_tbl_asignacions(${C.asigId})?$select=sanic_cm_tbl_asignacionid`)).status,
  };
  if (Object.values(r2).some((x) => x === 200)) for (const e of [A, B, Cc2]) ctx.nota(`diagnóstico cuenta ${e.clave}: ${await datos.diagnosticoCuenta(e.id)}`);
  ctx.chk('F2-09', Object.values(r2).every((x) => x !== 200),
    `tras la segunda carga, User5 sigue SIN leer la Especial C no asignada: cuenta ${r2.cuenta} · bóveda ${r2.boveda} · asignación ${r2.asignacion} (defecto 2: acceso heredado por Reparent)`);
}

module.exports = { f0, f1, f2 };
