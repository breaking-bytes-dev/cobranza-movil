// F0 · Generación del CSV de la carga (33 columnas, formato del banco) con 1 cliente QA y 3 cuentas:
// A Diaria, B Incobrable, C Especial. Los números de cuenta son PAN FICTICIOS (BIN 9999, 16 dígitos, Luhn válido,
// aleatorios por corrida): viven solo en memoria y en el archivo temporal; jamás se imprimen ni se guardan en otro lado.
const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');
const { PREFIJO, AGENTE } = require('./ctx');

// Encabezado EXACTO (el de test-data/cobranza-mock-1000.csv).
const ENCABEZADO = 'Cliente,CIF,DireccionCasa,DireccionEC,DireccionTrabajo,Departamento,NumeroCelular,NumeroCasa,NumeroCuenta,SaldoVencidoNIO,SaldoVencidoUSD,TotalSaldoVencido,ContagiadoUSD,PagoMinimoNIO,PagoMinimoUSD,DiasMora,NivelMora,FechaUltimoPago,FechaPago,Ciclo,CodigoProducto,TipoProducto,NombreFianza,DireccionFianza,RetiroNIO,RetiroUSD,TipoEspecial,Nota,Distrito,Trabajo,TipoAsignacion,Agente,FechaCarga';
const COLUMNAS = ENCABEZADO.split(',');

function luhn(base) {
  const d = [...base].map(Number).reverse();
  const s = d.reduce((acc, x, i) => acc + (i % 2 ? x : (x * 2 > 9 ? x * 2 - 9 : x * 2)), 0);
  return base + String((10 - (s % 10)) % 10);
}
const panFicticio = () => luhn('9999' + Array.from({ length: 11 }, () => crypto.randomInt(10)).join(''));

// Clave HMAC de DEV (la misma que usa el plugin ClaveDesdeCuenta). Si el archivo no está, la verificación del HMAC se omite.
function claveHmac() {
  const f = process.env.PAN_HMAC_KEY_FILE || path.join(os.homedir(), '.config', 'cobranza-movil', 'pan_hmac_dev.key');
  try { return Buffer.from(fs.readFileSync(f, 'utf8').trim(), 'hex'); } catch (e) { return null; }
}
const hmacPan = (key, pan) => crypto.createHmac('sha256', key).update(pan.trim()).digest('hex');

// Genera las 3 cuentas esperadas (con sus PAN en memoria) y el contenido del CSV.
function generar(ctx) {
  // últimos 4 distintos entre las 3 cuentas (la búsqueda por últimos 4 de las apps tiene que poder separarlas)
  let cuentas;
  do {
    cuentas = [
      { clave: 'A', tipo: 'Diaria', tipoCod: 1, saldoNio: 1500, dias: 20, nivel: 1, pan: panFicticio() },
      { clave: 'B', tipo: 'Incobrable', tipoCod: 2, saldoNio: 3200, dias: 200, nivel: 5, pan: panFicticio() },
      { clave: 'C', tipo: 'Especial', tipoCod: 3, saldoNio: 800, dias: 60, nivel: 3, pan: panFicticio(), retiroNio: 450, tipoEspecial: 'VIP', nota: `${ctx.nombreCliente} especial del CSV` },
    ];
  } while (new Set(cuentas.map((c) => c.pan.slice(-4))).size < 3);
  for (const c of cuentas) c.ult4 = c.pan.slice(-4);

  const f = ctx.hoy.fecha;
  const filas = cuentas.map((c) => {
    const r = {
      Cliente: ctx.nombreCliente, CIF: ctx.cif,
      DireccionCasa: `${PREFIJO} casa`, DireccionEC: `${PREFIJO} estado de cuenta`, DireccionTrabajo: `${PREFIJO} trabajo`,
      Departamento: 'Managua', NumeroCelular: '00000000', NumeroCasa: '00000000', NumeroCuenta: c.pan,
      SaldoVencidoNIO: c.saldoNio.toFixed(1), SaldoVencidoUSD: '0.0', TotalSaldoVencido: c.saldoNio.toFixed(2), ContagiadoUSD: '0.0',
      PagoMinimoNIO: '100.0', PagoMinimoUSD: '0.0', DiasMora: c.dias, NivelMora: c.nivel, FechaUltimoPago: '2026-09-01', FechaPago: '2026-10-20',
      Ciclo: '05', CodigoProducto: `${PREFIJO}-PR`, TipoProducto: `${PREFIJO} Producto`, NombreFianza: `${PREFIJO} Fiador`,
      DireccionFianza: `${PREFIJO} direccion fianza`, RetiroNIO: (c.retiroNio || 0).toFixed(1), RetiroUSD: '0.0',
      TipoEspecial: c.tipoEspecial || '', Nota: c.nota || '', Distrito: 'Distrito X', Trabajo: `${PREFIJO} empleador`,
      TipoAsignacion: c.tipo, Agente: AGENTE, FechaCarga: f,
    };
    return COLUMNAS.map((k) => { const s = String(r[k]); if (/[",\n]/.test(s)) throw new Error(`Valor con separador en ${k}`); return s; }).join(',');
  });
  const contenido = Buffer.from([ENCABEZADO, ...filas].join('\r\n') + '\r\n', 'utf8');
  return { cuentas, contenido, columnas: COLUMNAS.length };
}

module.exports = { ENCABEZADO, COLUMNAS, panFicticio, luhn, claveHmac, hmacPan, generar };
