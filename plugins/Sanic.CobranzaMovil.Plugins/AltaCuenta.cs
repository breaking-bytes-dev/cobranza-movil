using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// DAT-20 (decision del dueno, 2026-10-14, camino A): Custom API global "sanic_cm_AltaCuenta". El Admin da de alta
    /// una cuenta de cualquier tipo de asignacion (Diaria, Incobrable o Especial) como una importacion de UNA fila con la
    /// plantilla "Cobranza movil" de Smelter: el HMAC, la boveda y el enmascarado los hace Smelter, como con el archivo del
    /// banco, y la clave K sigue en un solo lugar. Ningun plugin de CM conoce K ni guarda el PAN: el PAN solo viaja en el
    /// pedido y queda en el archivo del job, igual que el CSV del banco.
    ///
    /// La fila la arma el servidor, no la app, porque la plantilla hace upsert por valor de negocio y eso tiene trampas:
    ///  1) Fecha de carga: la reconciliacion toma como ultima carga la fecha mas nueva de la carga diaria. Con la de hoy,
    ///     antes del archivo del banco, veria ~800 Diarias "fuera de cartera". Se usa la fecha mas nueva de la carga diaria;
    ///     si no hay ninguna (el borrado programado deja 60 dias), la MAS VIEJA de "ultima carga" de las Diarias activas (asi
    ///     ninguna queda antes del corte); y si tampoco, hoy (UTC). Nunca la que mande el cliente.
    ///  2) Agente y producto se buscan por nombre y codigo: con un error de tipeo la carga crearia uno nuevo. Se reciben
    ///     por id y se escriben con el nombre y el codigo que tienen en Dataverse (el agente, ademas, activo).
    ///  3) Cliente existente (por CIF): la carga pisaria nombre, direcciones, telefonos, departamento, distrito y trabajo.
    ///     Se completan con lo que ya hay; lo que mande la persona se ignora y se avisa. Lo mismo con la direccion de un
    ///     fiador que ya existe (la carga lo busca por nombre).
    ///  4) Si ese cliente ya tiene una cuenta con los mismos ultimos 4, se rechaza: la carga pisaria esa cuenta. Si otra
    ///     persona tiene una cuenta con esos ultimos 4 solo se avisa (sin K no se sabe si es la misma tarjeta).
    ///  5) Se avisa que hace la reconciliacion con cada tipo.
    /// Todo se valida ANTES de crear nada. El job lo crea el LLAMADOR (como la pantalla "Cargar archivo": el rol de Smelter
    /// llega por el equipo), sin estado: la app lo pone En cola despues de la espera, igual que en "Cargar archivo".
    /// Solo Admin, Servicio o sin roles de CM. Nunca traza ni cita valores: ni el PAN, ni montos, ni datos del cliente.
    /// Registro: Custom API global (sin entidad), no es funcion, sin pasos personalizados; este tipo es su plugin.
    /// </summary>
    public sealed class AltaCuenta : PluginBase
    {
        private const int LargoCif = 20;
        private const int LargoNombre = 200;
        private const int LargoTexto = 250;
        private const int LargoNota = 2000;
        private const int DigitosDePan = 13;
        private const decimal MontoMaximo = 1000000000m;
        private const int EnteroMaximo = 100000;
        private const string FormatoFecha = "yyyy-MM-dd";
        private const string Separador = "\n";
        private static readonly string[] Tipos = { "Diaria", "Incobrable", "Especial" };
        private static readonly string[] TiposDireccion = { "Casa", "EC", "Trabajo" };

        protected override string Nombre
        {
            get { return "AltaCuenta"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            var ctx = local.Context;
            if (!string.Equals(ctx.MessageName, Esquema.MensajeAltaCuenta, StringComparison.Ordinal)
                || ctx.Stage != Esquema.EtapaMainOperation)
            {
                throw local.Error("registro incorrecto: este plugin respalda la Custom API " + Esquema.MensajeAltaCuenta + ".");
            }

            var roles = RolesCm.DelLlamador(local);
            if (!roles.SinRestriccion)
            {
                throw local.Error("solo un Admin puede dar de alta una cuenta.");
            }

            // 1) Lo que manda la persona ------------------------------------------------------------------------------
            var fila = new Dictionary<string, string>(StringComparer.Ordinal);
            var avisos = new List<string>();

            var cif = Texto(local, "CIF", LargoCif, true, false);
            var pan = FilaDeCarga.Canonizar(Entrada<string>(ctx, "NumeroCuenta"));
            if (pan == null)
            {
                throw local.Error("el numero de tarjeta tiene que tener entre 13 y 19 digitos (se aceptan espacios o guiones).");
            }
            if (!FilaDeCarga.LuhnValido(pan))
            {
                throw local.Error("el numero de tarjeta no es valido: no pasa el digito verificador.");
            }
            var ultimos4 = pan.Substring(pan.Length - 4);
            var tipo = TipoAsignacion(local);

            var nombre = Texto(local, "Cliente", LargoNombre, false, true);
            var direcciones = TiposDireccion.ToDictionary(t => t, t => Texto(local, "Direccion" + t, LargoTexto, false, true));
            var departamento = Texto(local, "Departamento", LargoTexto, false, true);
            var distrito = Texto(local, "Distrito", LargoTexto, false, true);
            var trabajo = Texto(local, "Trabajo", LargoTexto, false, true);
            var telefonos = new Dictionary<string, string>
            {
                { "Celular", Texto(local, "NumeroCelular", LargoTexto, false, true) },
                { "Casa", Texto(local, "NumeroCasa", LargoTexto, false, true) },
            };
            var nombreFianza = Texto(local, "NombreFianza", LargoTexto, false, true);
            var direccionFianza = Texto(local, "DireccionFianza", LargoTexto, false, true);

            fila["SaldoVencidoNIO"] = Monto(local, "SaldoVencidoNIO", true);
            fila["SaldoVencidoUSD"] = Monto(local, "SaldoVencidoUSD", true);
            fila["TotalSaldoVencido"] = Monto(local, "TotalSaldoVencido", true);
            fila["ContagiadoUSD"] = Monto(local, "ContagiadoUSD", false);
            fila["PagoMinimoNIO"] = Monto(local, "PagoMinimoNIO", false);
            fila["PagoMinimoUSD"] = Monto(local, "PagoMinimoUSD", false);
            fila["RetiroNIO"] = Monto(local, "RetiroNIO", false);
            fila["RetiroUSD"] = Monto(local, "RetiroUSD", false);
            fila["DiasMora"] = Entero(local, "DiasMora");
            fila["NivelMora"] = Entero(local, "NivelMora");
            fila["FechaUltimoPago"] = Fecha(local, "FechaUltimoPago");
            fila["FechaPago"] = Fecha(local, "FechaPago");
            fila["Ciclo"] = Texto(local, "Ciclo", LargoTexto, false, true);
            fila["TipoEspecial"] = Texto(local, "TipoEspecial", LargoTexto, false, true);
            fila["Nota"] = Texto(local, "Nota", LargoNota, false, true);
            var agenteId = Id(local, Esquema.ParamAgenteId, "el agente");
            var productoId = Id(local, Esquema.ParamProductoId, "el producto");
            if (!(ctx.InputParameters.Contains(Esquema.ParamEsPrueba) && ctx.InputParameters[Esquema.ParamEsPrueba] is bool))
            {
                throw local.Error("falta indicar si es una prueba en seco (" + Esquema.ParamEsPrueba + ").");
            }
            var esPrueba = (bool)ctx.InputParameters[Esquema.ParamEsPrueba];

            // 2) Lo que ya dice Dataverse (lecturas con el servicio del sistema) ---------------------------------------
            var sistema = local.ServicioSistema;
            var agente = Leer(sistema, Esquema.Agente, agenteId, Esquema.AgenteNombre, Esquema.AgenteActivo);
            if (agente == null || string.IsNullOrWhiteSpace(agente.GetAttributeValue<string>(Esquema.AgenteNombre)))
            {
                throw local.Error("el agente no existe.");
            }
            if (agente.GetAttributeValue<bool?>(Esquema.AgenteActivo) != true)
            {
                throw local.Error("el agente esta inactivo: elegi un agente activo.");
            }
            var producto = Leer(sistema, Esquema.Producto, productoId, Esquema.ProductoCodigo, Esquema.ProductoTipo);
            if (producto == null || string.IsNullOrWhiteSpace(producto.GetAttributeValue<string>(Esquema.ProductoCodigo)))
            {
                throw local.Error("el producto no existe.");
            }

            var cliente = Primera(sistema, Esquema.Cliente, Esquema.ClienteCif, cif,
                Esquema.ClienteNombre, Esquema.ClienteDepartamento, Esquema.ClienteDistrito, Esquema.ClienteTrabajo);
            if (cliente != null)
            {
                var mandoAlgo = !string.IsNullOrEmpty(nombre) || !string.IsNullOrEmpty(departamento) || !string.IsNullOrEmpty(distrito)
                    || !string.IsNullOrEmpty(trabajo) || direcciones.Values.Any(v => !string.IsNullOrEmpty(v))
                    || telefonos.Values.Any(v => !string.IsNullOrEmpty(v));
                if (CuentasConUltimos4(sistema, ultimos4, cliente.Id, true) > 0)
                {
                    throw local.Error("ese cliente ya tiene una cuenta ••••" + ultimos4
                        + "; si es la misma, pedi la Especial desde Nueva solicitud.");
                }
                nombre = cliente.GetAttributeValue<string>(Esquema.ClienteNombre);
                departamento = cliente.GetAttributeValue<string>(Esquema.ClienteDepartamento);
                distrito = cliente.GetAttributeValue<string>(Esquema.ClienteDistrito);
                trabajo = cliente.GetAttributeValue<string>(Esquema.ClienteTrabajo);
                Completar(sistema, Esquema.Direccion, Esquema.DireccionCif, Esquema.DireccionTipo, Esquema.DireccionTexto, cif, direcciones);
                Completar(sistema, Esquema.Telefono, Esquema.TelefonoCif, Esquema.TelefonoTipo, Esquema.TelefonoNumero, cif, telefonos);
                if (mandoAlgo)
                {
                    avisos.Add("El cliente ya existe: se usaron sus datos actuales (nombre, direcciones, telefonos, departamento, "
                        + "distrito y trabajo) y se ignoro lo que escribiste en esos campos. Para cambiarlos, usa Maestros.");
                }
            }
            else if (string.IsNullOrEmpty(nombre))
            {
                throw local.Error("el cliente es nuevo: falta su nombre.");
            }
            var otras = CuentasConUltimos4(sistema, ultimos4, cliente == null ? Guid.Empty : cliente.Id, false);
            if (otras > 0)
            {
                avisos.Add("Hay " + otras + " cuenta(s) de otros clientes terminadas en " + ultimos4
                    + ". Si es la misma tarjeta, la carga la pasa a este cliente: revisalo antes de la carga real.");
            }

            if (!string.IsNullOrEmpty(nombreFianza))
            {
                var fiador = Primera(sistema, Esquema.Fiador, Esquema.FiadorNombre, nombreFianza, Esquema.FiadorDireccion);
                if (fiador != null)
                {
                    if (!string.IsNullOrEmpty(direccionFianza))
                    {
                        avisos.Add("El fiador ya existe: se uso su direccion actual y se ignoro la que escribiste.");
                    }
                    direccionFianza = fiador.GetAttributeValue<string>(Esquema.FiadorDireccion);
                }
            }

            var fechaCarga = FechaDeCarga(sistema);
            avisos.Add("La fila entra con la fecha de la ultima carga del banco (" + fechaCarga + "), no con la de hoy, para no "
                + "cambiar lo que la reconciliacion considera fuera de cartera.");
            avisos.Add(AvisoDelTipo(tipo));

            // La plantilla se busca con los permisos del llamador: si no la ve, no esta en el equipo de carga.
            var plantilla = Primera(local.Service, Esquema.PlantillaSmelter, Esquema.PlantillaSmelterNombre, Esquema.NombrePlantillaCobranza);
            if (plantilla == null)
            {
                throw local.Error("no se ve la plantilla de Smelter \"" + Esquema.NombrePlantillaCobranza
                    + "\": tu usuario no esta en el equipo de carga.");
            }

            // 3) La fila, con el formato del archivo del banco ---------------------------------------------------------
            fila["Cliente"] = nombre;
            fila["CIF"] = cif;
            fila["DireccionCasa"] = direcciones["Casa"];
            fila["DireccionEC"] = direcciones["EC"];
            fila["DireccionTrabajo"] = direcciones["Trabajo"];
            fila["Departamento"] = departamento;
            fila["Distrito"] = distrito;
            fila["Trabajo"] = trabajo;
            fila["NumeroCelular"] = telefonos["Celular"];
            fila["NumeroCasa"] = telefonos["Casa"];
            fila["NumeroCuenta"] = pan;
            fila["CodigoProducto"] = producto.GetAttributeValue<string>(Esquema.ProductoCodigo);
            fila["TipoProducto"] = producto.GetAttributeValue<string>(Esquema.ProductoTipo);
            fila["NombreFianza"] = nombreFianza;
            fila["DireccionFianza"] = direccionFianza;
            fila["TipoAsignacion"] = tipo;
            fila["Agente"] = agente.GetAttributeValue<string>(Esquema.AgenteNombre);
            fila["FechaCarga"] = fechaCarga;
            var archivo = FilaDeCarga.Armar(fila);
            pan = null;
            fila.Clear();

            // 4) Job de Smelter con los permisos del llamador, sin estado ----------------------------------------------
            var ahora = DateTime.UtcNow;
            var job = new Entity(Esquema.JobSmelter);
            job[Esquema.JobSmelterNombre] = "Alta manual " + CifParaNombre(cif) + " ••••" + ultimos4 + " "
                + ahora.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " · " + (esPrueba ? "prueba" : "real");
            job[Esquema.JobSmelterPlantilla] = plantilla.ToEntityReference();
            job[Esquema.JobSmelterEsPrueba] = esPrueba;
            Guid jobId;
            try
            {
                jobId = local.Service.Create(job);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                local.Trace("crear el job fallo con el codigo {0}.", ex.Detail == null ? 0 : ex.Detail.ErrorCode);
                throw local.Error("no se pudo crear la importacion en Smelter. Revisa que tu usuario este en el equipo de carga.");
            }
            try
            {
                // Fecha y hora separadas por un guion: 14 digitos seguidos parecerian un numero de tarjeta para un escaner PCI.
                Subir(local.Service, jobId, "alta-manual-" + ahora.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv", archivo);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                local.Trace("subir el archivo fallo con el codigo {0}.", ex.Detail == null ? 0 : ex.Detail.ErrorCode);
                Borrar(local, jobId);
                throw local.Error("no se pudo subir la fila a Smelter. Volve a intentar.");
            }

            var salida = ctx.OutputParameters;
            salida[Esquema.SalidaJobId] = jobId;
            salida[Esquema.SalidaAdvertencias] = string.Join(Separador, avisos);
            local.Trace("job creado ({0}), {1} aviso(s).", esPrueba ? "prueba" : "real", avisos.Count);
        }

        private static T Entrada<T>(IPluginExecutionContext ctx, string nombre)
        {
            var inputs = ctx.InputParameters;
            if (inputs == null || !inputs.Contains(nombre) || inputs[nombre] == null) return default(T);
            if (!(inputs[nombre] is T)) return default(T);
            return (T)inputs[nombre];
        }

        /// <summary>
        /// Texto de la persona, sin espacios de mas. Sin comillas ni saltos de linea (la coma va escapada en el CSV) y,
        /// salvo el CIF, sin secuencias de 13 digitos o mas: ningun campo libre puede llevar un numero de tarjeta.
        /// </summary>
        private static string Texto(LocalContext local, string nombre, int largo, bool obligatorio, bool sinPan)
        {
            var v = Entrada<string>(local.Context, nombre);
            v = v == null ? string.Empty : v.Trim();
            if (v.Length == 0)
            {
                if (obligatorio) throw local.Error("falta " + nombre + ".");
                return string.Empty;
            }
            if (v.Length > largo) throw local.Error(nombre + " pasa de " + largo + " caracteres.");
            if (v.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
            {
                throw local.Error(nombre + " no puede llevar comillas ni saltos de linea.");
            }
            if (sinPan && PanDetector.ContieneSecuenciaDeDigitos(v, DigitosDePan))
            {
                throw local.Error(nombre + " no puede llevar un numero de tarjeta.");
            }
            return v;
        }

        private static string TipoAsignacion(LocalContext local)
        {
            var v = Entrada<string>(local.Context, "TipoAsignacion");
            v = v == null ? string.Empty : v.Trim();
            var tipo = Tipos.FirstOrDefault(t => string.Equals(t, v, StringComparison.OrdinalIgnoreCase));
            if (tipo == null) throw local.Error("TipoAsignacion tiene que ser Diaria, Incobrable o Especial.");
            return tipo;
        }

        private static string Monto(LocalContext local, string nombre, bool obligatorio)
        {
            var inputs = local.Context.InputParameters;
            if (!inputs.Contains(nombre) || inputs[nombre] == null)
            {
                if (obligatorio) throw local.Error("falta " + nombre + ".");
                return string.Empty;
            }
            if (!(inputs[nombre] is decimal)) throw local.Error("el parametro " + nombre + " debe ser decimal.");
            var v = (decimal)inputs[nombre];
            if (v < 0 || v > MontoMaximo) throw local.Error("el parametro " + nombre + " esta fuera de rango.");
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Entero(LocalContext local, string nombre)
        {
            var inputs = local.Context.InputParameters;
            if (!inputs.Contains(nombre) || !(inputs[nombre] is int)) throw local.Error("falta " + nombre + " (entero).");
            var v = (int)inputs[nombre];
            if (v < 0 || v > EnteroMaximo) throw local.Error("el parametro " + nombre + " esta fuera de rango.");
            return v.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Fecha opcional como texto aaaa-mm-dd (como en el archivo del banco): sin conversiones de zona horaria.</summary>
        private static string Fecha(LocalContext local, string nombre)
        {
            var v = Entrada<string>(local.Context, nombre);
            v = v == null ? string.Empty : v.Trim();
            if (v.Length == 0) return string.Empty;
            DateTime d;
            if (!DateTime.TryParseExact(v, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                throw local.Error(nombre + " tiene que venir como aaaa-mm-dd.");
            }
            return d.ToString(FormatoFecha, CultureInfo.InvariantCulture);
        }

        private static Guid Id(LocalContext local, string nombre, string que)
        {
            var inputs = local.Context.InputParameters;
            if (!inputs.Contains(nombre) || !(inputs[nombre] is Guid) || (Guid)inputs[nombre] == Guid.Empty)
            {
                throw local.Error("falta " + que + " (" + nombre + ").");
            }
            return (Guid)inputs[nombre];
        }

        private static Entity Leer(IOrganizationService svc, string tabla, Guid id, params string[] columnas)
        {
            try
            {
                return svc.Retrieve(tabla, id, new ColumnSet(columnas));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                return null; // no existe
            }
        }

        private static Entity Primera(IOrganizationService svc, string tabla, string columna, string valor, params string[] columnas)
        {
            var q = new QueryExpression(tabla) { ColumnSet = new ColumnSet(columnas), TopCount = 1 };
            q.Criteria.AddCondition(columna, ConditionOperator.Equal, valor);
            var r = svc.RetrieveMultiple(q);
            return r == null ? null : r.Entities.FirstOrDefault();
        }

        /// <summary>
        /// Completa direcciones o telefonos con lo que ya tiene el cliente (la carga los busca por CIF y tipo). Un tipo
        /// que el cliente no tiene se deja con lo que mando la persona: crearlo no pisa nada.
        /// </summary>
        private static void Completar(IOrganizationService svc, string tabla, string colCif, string colTipo, string colValor,
            string cif, Dictionary<string, string> valores)
        {
            var q = new QueryExpression(tabla) { ColumnSet = new ColumnSet(colTipo, colValor) };
            q.Criteria.AddCondition(colCif, ConditionOperator.Equal, cif);
            var r = svc.RetrieveMultiple(q);
            if (r == null) return;
            foreach (var e in r.Entities)
            {
                var t = e.GetAttributeValue<string>(colTipo);
                if (t != null && valores.ContainsKey(t)) valores[t] = e.GetAttributeValue<string>(colValor) ?? string.Empty;
            }
        }

        /// <summary>Cuentas terminadas en esos 4 digitos, del cliente (delCliente) o de los demas.</summary>
        private static int CuentasConUltimos4(IOrganizationService svc, string ultimos4, Guid clienteId, bool delCliente)
        {
            var q = new QueryExpression(Esquema.Cuenta) { ColumnSet = new ColumnSet(false), TopCount = 50 };
            q.Criteria.AddCondition(Esquema.CuentaUltimos4, ConditionOperator.Equal, ultimos4);
            if (delCliente)
            {
                q.Criteria.AddCondition(Esquema.CuentaCliente, ConditionOperator.Equal, clienteId);
            }
            else if (clienteId != Guid.Empty)
            {
                q.Criteria.AddCondition(Esquema.CuentaCliente, ConditionOperator.NotEqual, clienteId);
            }
            var r = svc.RetrieveMultiple(q);
            return r == null ? 0 : r.Entities.Count;
        }

        /// <summary>Trampa 1: la fecha de carga la decide el servidor (ver el resumen de la clase).</summary>
        private static string FechaDeCarga(IOrganizationService svc)
        {
            var cd = new QueryExpression(Esquema.CargaDiaria) { ColumnSet = new ColumnSet(Esquema.CargaDiariaFecha), TopCount = 1 };
            cd.Criteria.AddCondition(Esquema.CargaDiariaFecha, ConditionOperator.NotNull);
            cd.AddOrder(Esquema.CargaDiariaFecha, OrderType.Descending);
            var ultima = Fechas(svc.RetrieveMultiple(cd), Esquema.CargaDiariaFecha);
            if (ultima.HasValue) return ultima.Value.ToString(FormatoFecha, CultureInfo.InvariantCulture);

            var diarias = new QueryExpression(Esquema.Asignacion) { ColumnSet = new ColumnSet(Esquema.AsignacionUltimaCarga), TopCount = 1 };
            diarias.Criteria.AddCondition(Esquema.AsignacionTipo, ConditionOperator.Equal, Esquema.TipoDiaria);
            diarias.Criteria.AddCondition(Esquema.AsignacionEstado, ConditionOperator.Equal, true);
            diarias.Criteria.AddCondition(Esquema.AsignacionUltimaCarga, ConditionOperator.NotNull);
            diarias.AddOrder(Esquema.AsignacionUltimaCarga, OrderType.Ascending);
            var masVieja = Fechas(svc.RetrieveMultiple(diarias), Esquema.AsignacionUltimaCarga);
            return (masVieja ?? DateTime.UtcNow).ToString(FormatoFecha, CultureInfo.InvariantCulture);
        }

        /// <summary>La fecha (sin hora) de la primera fila. La carga graba "ultima carga" a la medianoche UTC.</summary>
        private static DateTime? Fechas(EntityCollection filas, string columna)
        {
            if (filas == null) return null;
            var f = filas.Entities.Select(e => e.GetAttributeValue<DateTime?>(columna)).FirstOrDefault(d => d.HasValue);
            if (!f.HasValue) return null;
            var d0 = f.Value.Kind == DateTimeKind.Local ? f.Value.ToUniversalTime() : f.Value;
            return d0.Date;
        }

        private static string AvisoDelTipo(string tipo)
        {
            switch (tipo)
            {
                case "Diaria":
                    return "Diaria: si el banco no la trae en su proximo archivo, la reconciliacion la muestra fuera de cartera y se puede cerrar.";
                case "Incobrable":
                    return "Incobrable: la reconciliacion no la cierra; queda activa hasta que alguien la cierre o el banco la mande como Diaria.";
                default:
                    return "Especial: entra pendiente de aprobacion. La plantilla deja el retiro y la nota en la cuenta, no en la asignacion.";
            }
        }

        /// <summary>El CIF en el nombre del job; si trae 13 digitos seguidos, solo sus ultimos 4 (que no parezca un PAN).</summary>
        private static string CifParaNombre(string cif)
        {
            var seguidos = 0;
            foreach (var c in cif)
            {
                seguidos = c >= '0' && c <= '9' ? seguidos + 1 : 0;
                if (seguidos >= DigitosDePan) return "CIF …" + cif.Substring(cif.Length - 4);
            }
            return cif;
        }

        /// <summary>Sube el archivo a la columna de archivo del job en un solo bloque (el CSV de una fila pesa menos de 4 MB).</summary>
        private static void Subir(IOrganizationService svc, Guid jobId, string nombreArchivo, byte[] contenido)
        {
            var iniciar = new OrganizationRequest("InitializeFileBlocksUpload");
            iniciar["Target"] = new EntityReference(Esquema.JobSmelter, jobId);
            iniciar["FileAttributeName"] = Esquema.JobSmelterArchivo;
            iniciar["FileName"] = nombreArchivo;
            var token = (string)svc.Execute(iniciar)["FileContinuationToken"];

            var bloque = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
            var subir = new OrganizationRequest("UploadBlock");
            subir["BlockId"] = bloque;
            subir["BlockData"] = contenido;
            subir["FileContinuationToken"] = token;
            svc.Execute(subir);

            var confirmar = new OrganizationRequest("CommitFileBlocksUpload");
            confirmar["FileName"] = nombreArchivo;
            confirmar["MimeType"] = "text/csv";
            confirmar["BlockList"] = new[] { bloque };
            confirmar["FileContinuationToken"] = token;
            svc.Execute(confirmar);
        }

        /// <summary>Un job sin archivo y sin estado no dispara nada en Smelter; igual se intenta borrar.</summary>
        private static void Borrar(LocalContext local, Guid jobId)
        {
            try
            {
                local.Service.Delete(Esquema.JobSmelter, jobId);
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                local.Trace("no se pudo borrar el job sin archivo; queda sin estado y no dispara nada.");
            }
        }
    }
}
