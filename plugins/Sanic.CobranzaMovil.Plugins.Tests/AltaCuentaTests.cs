using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Xunit;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// La Custom API AltaCuenta arma en el servidor una fila del archivo de carga y la manda como un Import Job del
    /// llamador (sin estado: lo pone la app). Cubre los rechazos, las cinco trampas del upsert de la
    /// plantilla (fecha de carga, agente y producto por nombre, cliente existente, cuenta existente, aviso por tipo), el
    /// armado exacto del CSV y que el PAN nunca aparece en trazas, mensajes, avisos ni en el nombre del job.
    /// </summary>
    public class AltaCuentaTests
    {
        private const string Mensaje = "sanic_cm_AltaCuenta";
        private const string Admin = "SR | CM | Admin";
        private const string Servicio = "SR | CM | Servicio - Cobranza";
        private const string Solicitante = "SR | CM | Solicitante";
        private const string Supervisor = "SR | CM | Supervisor";
        private const string Localizador = "SR | CM | Localizador";
        private const string BackOffice = "SR | CM | BackOffice";

        private const string Cliente = "sanic_cm_tbl_cliente";
        private const string Cuenta = "sanic_cm_tbl_cuenta";
        private const string Agente = "sanic_cm_tbl_agente";
        private const string Producto = "sanic_cm_tbl_producto";
        private const string Fiador = "sanic_cm_tbl_fiador";
        private const string Direccion = "sanic_cm_tbl_direccion";
        private const string Telefono = "sanic_cm_tbl_telefono";
        private const string CargaDiaria = "sanic_cm_tbl_cargadiaria";
        private const string Asignacion = "sanic_cm_tbl_asignacion";
        private const string Plantilla = "sanic_tbl_smlt_importtemplate";
        private const string Job = "sanic_tbl_smlt_importjob";

        private const string Encabezado = "Cliente,CIF,DireccionCasa,DireccionEC,DireccionTrabajo,Departamento,NumeroCelular,NumeroCasa,"
            + "NumeroCuenta,SaldoVencidoNIO,SaldoVencidoUSD,TotalSaldoVencido,ContagiadoUSD,PagoMinimoNIO,PagoMinimoUSD,DiasMora,"
            + "NivelMora,FechaUltimoPago,FechaPago,Ciclo,CodigoProducto,TipoProducto,NombreFianza,DireccionFianza,RetiroNIO,RetiroUSD,"
            + "TipoEspecial,Nota,Distrito,Trabajo,TipoAsignacion,Agente,FechaCarga";

        /// <summary>PAN ficticio (BIN 9999) con digito verificador de Luhn calculado aca, sin usar el codigo probado.</summary>
        private static readonly string Pan = ConLuhn("999900001234567");
        private static readonly string Ultimos4 = Pan.Substring(Pan.Length - 4);

        private static readonly Guid AgenteId = Guid.NewGuid();
        private static readonly Guid ProductoId = Guid.NewGuid();
        private static readonly Guid PlantillaId = Guid.NewGuid();
        private static readonly Guid ClienteId = Guid.NewGuid();

        private static string ConLuhn(string base_)
        {
            var suma = 0;
            for (var i = 0; i < base_.Length; i++)
            {
                var d = base_[base_.Length - 1 - i] - '0';
                if (i % 2 == 0) { d *= 2; if (d > 9) d -= 9; }
                suma += d;
            }
            return base_ + ((10 - suma % 10) % 10);
        }

        private sealed class Escenario
        {
            public readonly Harness H = new Harness(Mensaje, "none", 30);
            public readonly Mock<IOrganizationService> Sistema = new Mock<IOrganizationService>();
            public readonly Guid JobId = Guid.NewGuid();
            public readonly List<OrganizationRequest> Pedidos = new List<OrganizationRequest>();
            public Entity JobCreado;
            public Entity ClienteExistente;
            public readonly List<Entity> Direcciones = new List<Entity>();
            public readonly List<Entity> Telefonos = new List<Entity>();
            public readonly List<Entity> CuentasDelCliente = new List<Entity>();
            public readonly List<Entity> CuentasDeOtros = new List<Entity>();
            public Entity FiadorExistente;
            public DateTime? UltimaCargaDiaria = new DateTime(2026, 10, 13);
            public DateTime? DiariaMasVieja;
            public Entity AgenteFila = Fila(Agente, AgenteId, "sanic_cm_ag_nombre", "Marvin Obando", "sanic_cm_ag_activo", true);
            public Entity ProductoFila = Fila(Producto, ProductoId, "sanic_cm_pr_codigo", "PV020", "sanic_cm_pr_tipo", "Préstamo Vehículo");
            public bool PlantillaVisible = true;
            public bool FallaSubida;

            public Escenario(params string[] roles)
            {
                H.Factory.Setup(f => f.CreateOrganizationService(It.Is<Guid?>(g => g == null))).Returns(Sistema.Object);

                var filasRoles = new EntityCollection();
                foreach (var n in roles)
                {
                    var r = new Entity("role", Guid.NewGuid());
                    r["name"] = n;
                    filasRoles.Entities.Add(r);
                }
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == "role"))).Returns(filasRoles);
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Cliente)))
                    .Returns(() => Coleccion(ClienteExistente));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Direccion))).Returns(() => new EntityCollection(Direcciones));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Telefono))).Returns(() => new EntityCollection(Telefonos));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Cuenta)))
                    .Returns<QueryBase>(q => new EntityCollection(DelCliente(q) ? CuentasDelCliente : CuentasDeOtros));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Fiador))).Returns(() => Coleccion(FiadorExistente));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == CargaDiaria)))
                    .Returns(() => Coleccion(UltimaCargaDiaria.HasValue ? Fila(CargaDiaria, Guid.NewGuid(), "sanic_cm_cd_fechacarga", UltimaCargaDiaria.Value) : null));
                Sistema.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Asignacion)))
                    .Returns(() => Coleccion(DiariaMasVieja.HasValue ? Fila(Asignacion, Guid.NewGuid(), "sanic_cm_as_ultimacarga", DiariaMasVieja.Value) : null));
                Sistema.Setup(s => s.Retrieve(Agente, AgenteId, It.IsAny<ColumnSet>())).Returns(() => Devolver(AgenteFila));
                Sistema.Setup(s => s.Retrieve(Producto, ProductoId, It.IsAny<ColumnSet>())).Returns(() => Devolver(ProductoFila));

                H.Service.Setup(s => s.RetrieveMultiple(It.Is<QueryBase>(q => Tabla(q) == Plantilla)))
                    .Returns(() => Coleccion(PlantillaVisible ? Fila(Plantilla, PlantillaId, "sanic_smlt_it_name", "Cobranza movil") : null));
                H.Service.Setup(s => s.Create(It.IsAny<Entity>())).Callback<Entity>(e => JobCreado = e).Returns(JobId);
                H.Service.Setup(s => s.Execute(It.IsAny<OrganizationRequest>())).Returns<OrganizationRequest>(r =>
                {
                    Pedidos.Add(r);
                    if (FallaSubida && r.RequestName == "UploadBlock")
                    {
                        throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -2147220970 }, "fallo con " + Pan);
                    }
                    var resp = new OrganizationResponse();
                    if (r.RequestName == "InitializeFileBlocksUpload") resp.Results["FileContinuationToken"] = "token-1";
                    return resp;
                });

                var i = H.Inputs;
                i["CIF"] = "001-010180-0001A";
                i["Cliente"] = "Cliente Nuevo QA";
                i["DireccionCasa"] = "De la rotonda 2 c al sur, casa 5";
                i["DireccionEC"] = "Igual a casa";
                i["DireccionTrabajo"] = "Oficina central";
                i["Departamento"] = "Managua";
                i["Distrito"] = "Distrito X";
                i["Trabajo"] = "Empresa QA";
                i["NumeroCelular"] = "88880000";
                i["NumeroCasa"] = "22220000";
                i["NumeroCuenta"] = Pan;
                i["SaldoVencidoNIO"] = 1500m;
                i["SaldoVencidoUSD"] = 0m;
                i["TotalSaldoVencido"] = 1500.5m;
                i["ContagiadoUSD"] = 10.25m;
                i["PagoMinimoNIO"] = 100m;
                i["PagoMinimoUSD"] = 0m;
                i["DiasMora"] = 20;
                i["NivelMora"] = 1;
                i["FechaUltimoPago"] = "2026-09-01";
                i["FechaPago"] = "2026-10-20";
                i["Ciclo"] = "05";
                i["ProductoId"] = ProductoId;
                i["AgenteId"] = AgenteId;
                i["NombreFianza"] = "Fiador QA";
                i["DireccionFianza"] = "Del mercado 5 c al lago";
                i["RetiroNIO"] = 450m;
                i["RetiroUSD"] = 0m;
                i["TipoEspecial"] = "VIP";
                i["Nota"] = "Alta manual de prueba";
                i["TipoAsignacion"] = "Especial";
                i["EsPrueba"] = true;
            }

            public void Ejecutar()
            {
                H.Ejecutar(new AltaCuenta());
            }

            public InvalidPluginExecutionException Rechaza()
            {
                var ex = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar());
                Assert.DoesNotContain(Pan, ex.Message);
                Assert.DoesNotContain(Pan, H.TodoElTexto());
                return ex;
            }

            public void NoCreoNada()
            {
                Assert.Null(JobCreado);
                Assert.Empty(Pedidos);
                Sistema.Verify(s => s.Create(It.IsAny<Entity>()), Times.Never());
            }

            public string Csv
            {
                get
                {
                    var subir = Pedidos.Single(p => p.RequestName == "UploadBlock");
                    return Encoding.UTF8.GetString((byte[])subir["BlockData"]);
                }
            }

            /// <summary>La fila de datos como diccionario columna → valor (los valores de prueba no traen comas salvo DireccionCasa).</summary>
            public Dictionary<string, string> Valores
            {
                get
                {
                    var lineas = Csv.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    var cols = lineas[0].Split(',');
                    var vals = Partir(lineas[1]);
                    Assert.Equal(cols.Length, vals.Count);
                    return cols.Zip(vals, (c, v) => new { c, v }).ToDictionary(x => x.c, x => x.v);
                }
            }

            public string Avisos
            {
                get { return (string)H.Outputs["Advertencias"]; }
            }

            private static Entity Devolver(Entity fila)
            {
                if (fila == null) throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault(), "no existe");
                return fila;
            }

            private static bool DelCliente(QueryBase q)
            {
                var qe = (QueryExpression)q;
                return qe.Criteria.Conditions.Any(c => c.AttributeName == "sanic_cm_ct_clienteid" && c.Operator == ConditionOperator.Equal);
            }
        }

        private static string Tabla(QueryBase q)
        {
            var qe = q as QueryExpression;
            return qe == null ? null : qe.EntityName;
        }

        private static EntityCollection Coleccion(Entity e)
        {
            return e == null ? new EntityCollection() : new EntityCollection(new List<Entity> { e });
        }

        private static Entity Fila(string tabla, Guid id, params object[] pares)
        {
            var e = new Entity(tabla, id);
            for (var i = 0; i < pares.Length; i += 2) e[(string)pares[i]] = pares[i + 1];
            return e;
        }

        /// <summary>Parte una linea CSV con comillas RFC 4180 (para leer lo que armo el plugin).</summary>
        private static List<string> Partir(string linea)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            var entre = false;
            for (var i = 0; i < linea.Length; i++)
            {
                var c = linea[i];
                if (entre)
                {
                    if (c == '"' && i + 1 < linea.Length && linea[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') entre = false;
                    else sb.Append(c);
                }
                else if (c == '"') entre = true;
                else if (c == ',') { res.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            res.Add(sb.ToString());
            return res;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Registro y roles

        [Fact]
        public void Falla_si_se_registra_en_otro_mensaje()
        {
            var e = new Escenario(Admin);
            e.H.Context.SetupGet(c => c.MessageName).Returns("Create");
            Assert.Contains("registro incorrecto", e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Theory]
        [InlineData(Solicitante)]
        [InlineData(Supervisor)]
        [InlineData(Localizador)]
        [InlineData(BackOffice)]
        public void Solo_el_Admin_da_de_alta(string rol)
        {
            var e = new Escenario(rol);
            Assert.Contains("solo un Admin", e.Rechaza().Message);
            e.NoCreoNada();
            e.Sistema.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ColumnSet>()), Times.Never());
        }

        [Theory]
        [InlineData(Admin)]
        [InlineData(Servicio)]
        [InlineData(null)]
        public void Admin_Servicio_y_sin_roles_de_CM_pueden(string rol)
        {
            var e = rol == null ? new Escenario() : new Escenario(rol);
            e.Ejecutar();
            Assert.Equal(e.JobId, (Guid)e.H.Outputs["JobId"]);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Validaciones de la entrada (todas antes de crear nada)

        [Theory]
        [InlineData("4111 1111 1111")]           // 12 digitos
        [InlineData("41111111111111111111")]      // 20 digitos
        [InlineData("4111x111111111111")]         // letras
        [InlineData("")]
        public void Un_PAN_con_largo_o_caracteres_invalidos_se_rechaza(string pan)
        {
            var e = new Escenario(Admin);
            e.H.Inputs["NumeroCuenta"] = pan;
            Assert.Contains("entre 13 y 19 digitos", e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Fact]
        public void Un_PAN_que_no_pasa_Luhn_se_rechaza_sin_citarlo()
        {
            var e = new Escenario(Admin);
            var malo = Pan.Substring(0, Pan.Length - 1) + ((Pan[Pan.Length - 1] - '0' + 1) % 10);
            e.H.Inputs["NumeroCuenta"] = malo;
            var ex = e.Rechaza();
            Assert.Contains("digito verificador", ex.Message);
            Assert.DoesNotContain(malo, ex.Message);
            Assert.DoesNotContain(malo, e.H.TodoElTexto());
            e.NoCreoNada();
        }

        [Fact]
        public void El_PAN_con_espacios_o_guiones_va_al_CSV_sin_separadores()
        {
            var e = new Escenario(Admin);
            e.H.Inputs["NumeroCuenta"] = " " + Pan.Substring(0, 4) + " " + Pan.Substring(4, 4) + "-" + Pan.Substring(8) + " ";
            e.Ejecutar();
            Assert.Equal(Pan, e.Valores["NumeroCuenta"]);
        }

        [Fact]
        public void Agente_inexistente_o_inactivo_se_rechaza()
        {
            var e = new Escenario(Admin) { AgenteFila = null };
            Assert.Contains("el agente no existe", e.Rechaza().Message);
            e.NoCreoNada();

            var i = new Escenario(Admin);
            i.AgenteFila["sanic_cm_ag_activo"] = false;
            Assert.Contains("inactivo", i.Rechaza().Message);
            i.NoCreoNada();
        }

        [Fact]
        public void Producto_inexistente_se_rechaza()
        {
            var e = new Escenario(Admin) { ProductoFila = null };
            Assert.Contains("el producto no existe", e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Theory]
        [InlineData("diaria", "Diaria")]
        [InlineData("INCOBRABLE", "Incobrable")]
        [InlineData(" Especial ", "Especial")]
        public void El_tipo_se_normaliza(string entrada, string esperado)
        {
            var e = new Escenario(Admin);
            e.H.Inputs["TipoAsignacion"] = entrada;
            e.Ejecutar();
            Assert.Equal(esperado, e.Valores["TipoAsignacion"]);
        }

        [Theory]
        [InlineData("Normal")]
        [InlineData("")]
        public void Un_tipo_desconocido_se_rechaza(string tipo)
        {
            var e = new Escenario(Admin);
            e.H.Inputs["TipoAsignacion"] = tipo;
            Assert.Contains("Diaria, Incobrable o Especial", e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Theory]
        [InlineData("Nota", "linea 1\nlinea 2")]
        [InlineData("DireccionCasa", "casa \"azul\"")]
        [InlineData("Cliente", "nom\rbre")]
        public void Comillas_y_saltos_de_linea_se_rechazan(string campo, string valor)
        {
            var e = new Escenario(Admin);
            e.H.Inputs[campo] = valor;
            Assert.Contains(campo + " no puede llevar comillas", e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Theory]
        [InlineData("Cliente", 200)]
        [InlineData("DireccionCasa", 200)]
        [InlineData("DireccionEC", 200)]
        [InlineData("DireccionTrabajo", 200)]
        [InlineData("Departamento", 100)]
        [InlineData("Distrito", 100)]
        [InlineData("Trabajo", 200)]
        [InlineData("NumeroCelular", 20)]
        [InlineData("NumeroCasa", 20)]
        [InlineData("NombreFianza", 200)]
        [InlineData("DireccionFianza", 200)]
        [InlineData("Ciclo", 10)]
        [InlineData("TipoEspecial", 200)]
        [InlineData("Nota", 400)]
        public void Cada_campo_libre_tiene_el_tope_de_su_columna(string campo, int largo)
        {
            var e = new Escenario(Admin);
            e.H.Inputs[campo] = new string('a', largo + 1);
            Assert.Contains(campo + " pasa de " + largo + " caracteres", e.Rechaza().Message);
            e.NoCreoNada();

            var justo = new Escenario(Admin);
            justo.H.Inputs[campo] = new string('a', largo);
            justo.Ejecutar();
        }

        [Theory]
        [InlineData("Nota")]
        [InlineData("TipoEspecial")]
        [InlineData("DireccionTrabajo")]
        [InlineData("DireccionFianza")]
        public void Un_numero_de_tarjeta_en_un_campo_libre_se_rechaza(string campo)
        {
            var e = new Escenario(Admin);
            e.H.Inputs[campo] = "pago con 4111 1111 1111 1111";
            var ex = e.Rechaza();
            Assert.Contains(campo + " no puede llevar un numero de tarjeta", ex.Message);
            Assert.DoesNotContain("4111", ex.Message);
            Assert.DoesNotContain("4111", e.H.TodoElTexto());
            e.NoCreoNada();
        }

        [Theory]
        [InlineData("CIF")]
        [InlineData("SaldoVencidoNIO")]
        [InlineData("TotalSaldoVencido")]
        [InlineData("DiasMora")]
        [InlineData("NivelMora")]
        [InlineData("AgenteId")]
        [InlineData("ProductoId")]
        [InlineData("EsPrueba")]
        public void Los_obligatorios_se_exigen(string campo)
        {
            var e = new Escenario(Admin);
            e.H.Inputs.Remove(campo);
            Assert.Contains(campo, e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Fact]
        public void Montos_fuera_de_rango_y_fechas_mal_escritas_se_rechazan()
        {
            var e = new Escenario(Admin);
            e.H.Inputs["SaldoVencidoNIO"] = -1m;
            Assert.Contains("fuera de rango", e.Rechaza().Message);

            var f = new Escenario(Admin);
            f.H.Inputs["FechaPago"] = "20/10/2026";
            Assert.Contains("aaaa-mm-dd", f.Rechaza().Message);
            f.NoCreoNada();
        }

        [Fact]
        public void Un_cliente_nuevo_sin_nombre_se_rechaza()
        {
            var e = new Escenario(Admin);
            e.H.Inputs["Cliente"] = "  ";
            Assert.Contains("falta su nombre", e.Rechaza().Message);
            e.NoCreoNada();
        }

        [Fact]
        public void Sin_plantilla_visible_se_rechaza_antes_de_crear()
        {
            var e = new Escenario(Admin) { PlantillaVisible = false };
            Assert.Contains("equipo de carga", e.Rechaza().Message);
            e.NoCreoNada();
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Trampas del upsert de la plantilla

        [Fact]
        public void Trampa1_la_fecha_de_carga_es_la_de_la_ultima_carga_del_banco()
        {
            var e = new Escenario(Admin) { UltimaCargaDiaria = new DateTime(2026, 10, 13) };
            e.H.Inputs["FechaCarga"] = "2026-12-31"; // no es un parametro de la API: se ignora
            e.Ejecutar();
            Assert.Equal("2026-10-13", e.Valores["FechaCarga"]);
            Assert.Contains("2026-10-13", e.Avisos);
        }

        [Fact]
        public void Trampa1_sin_carga_diaria_usa_la_Diaria_activa_mas_vieja()
        {
            var e = new Escenario(Admin)
            {
                UltimaCargaDiaria = null,
                DiariaMasVieja = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            };
            e.Ejecutar();
            Assert.Equal("2026-06-20", e.Valores["FechaCarga"]);
        }

        [Fact]
        public void Trampa1_sin_carga_diaria_ni_Diarias_usa_hoy()
        {
            var e = new Escenario(Admin) { UltimaCargaDiaria = null, DiariaMasVieja = null };
            var antes = DateTime.UtcNow.ToString("yyyy-MM-dd");
            e.Ejecutar();
            var despues = DateTime.UtcNow.ToString("yyyy-MM-dd");
            Assert.Contains(e.Valores["FechaCarga"], new[] { antes, despues });
        }

        [Fact]
        public void Trampa2_agente_y_producto_van_con_el_nombre_y_codigo_de_Dataverse()
        {
            var e = new Escenario(Admin);
            e.H.Inputs["Agente"] = "Marvn Obado";       // no son parametros: se ignoran
            e.H.Inputs["CodigoProducto"] = "PV02O";
            e.Ejecutar();
            var v = e.Valores;
            Assert.Equal("Marvin Obando", v["Agente"]);
            Assert.Equal("PV020", v["CodigoProducto"]);
            Assert.Equal("Préstamo Vehículo", v["TipoProducto"]);
        }

        [Fact]
        public void Trampa3_un_cliente_existente_conserva_sus_datos()
        {
            var e = new Escenario(Admin)
            {
                ClienteExistente = Fila(Cliente, ClienteId, "sanic_cm_cl_nombre", "Nombre Real", "sanic_cm_cl_departamento", "Boaco",
                    "sanic_cm_cl_distrito", "Distrito Real", "sanic_cm_cl_trabajo", "Trabajo Real"),
            };
            e.Direcciones.Add(Fila(Direccion, Guid.NewGuid(), "sanic_cm_dr_tipo", "Casa", "sanic_cm_dr_direccion", "Casa real"));
            e.Direcciones.Add(Fila(Direccion, Guid.NewGuid(), "sanic_cm_dr_tipo", "Trabajo", "sanic_cm_dr_direccion", "Trabajo real"));
            e.Telefonos.Add(Fila(Telefono, Guid.NewGuid(), "sanic_cm_tl_tipo", "Celular", "sanic_cm_tl_numero", "77770000"));
            e.Ejecutar();
            var v = e.Valores;
            Assert.Equal("Nombre Real", v["Cliente"]);
            Assert.Equal("Boaco", v["Departamento"]);
            Assert.Equal("Distrito Real", v["Distrito"]);
            Assert.Equal("Trabajo Real", v["Trabajo"]);
            Assert.Equal("Casa real", v["DireccionCasa"]);
            Assert.Equal("Trabajo real", v["DireccionTrabajo"]);
            Assert.Equal("77770000", v["NumeroCelular"]);
            // Lo que el cliente no tiene se manda como vino: crearlo no pisa nada.
            Assert.Equal("Igual a casa", v["DireccionEC"]);
            Assert.Equal("22220000", v["NumeroCasa"]);
            Assert.Contains("El cliente ya existe", e.Avisos);
        }

        [Fact]
        public void Trampa3_un_fiador_existente_conserva_su_direccion()
        {
            var e = new Escenario(Admin)
            {
                FiadorExistente = Fila(Fiador, Guid.NewGuid(), "sanic_cm_fi_direccion", "Direccion real del fiador"),
            };
            e.Ejecutar();
            Assert.Equal("Direccion real del fiador", e.Valores["DireccionFianza"]);
            Assert.Contains("El fiador ya existe", e.Avisos);
        }

        [Fact]
        public void Trampa4_si_el_cliente_ya_tiene_esos_ultimos_4_se_rechaza()
        {
            var e = new Escenario(Admin) { ClienteExistente = Fila(Cliente, ClienteId, "sanic_cm_cl_nombre", "Nombre Real") };
            e.CuentasDelCliente.Add(new Entity(Cuenta, Guid.NewGuid()));
            var ex = e.Rechaza();
            Assert.Contains("ese cliente ya tiene una cuenta ••••" + Ultimos4, ex.Message);
            Assert.Contains("Nueva solicitud", ex.Message);
            e.NoCreoNada();
        }

        [Fact]
        public void Trampa4_cuentas_de_otros_clientes_con_esos_ultimos_4_solo_avisan()
        {
            var e = new Escenario(Admin);
            e.CuentasDeOtros.Add(new Entity(Cuenta, Guid.NewGuid()));
            e.Ejecutar();
            Assert.Contains("1 cuenta(s) de otros clientes terminadas en " + Ultimos4, e.Avisos);
        }

        [Theory]
        [InlineData("Diaria", "la reconciliacion la muestra fuera de cartera")]
        [InlineData("Incobrable", "la reconciliacion no la cierra")]
        [InlineData("Especial", "pendiente de aprobacion")]
        public void Trampa5_avisa_que_pasa_con_cada_tipo(string tipo, string aviso)
        {
            var e = new Escenario(Admin);
            e.H.Inputs["TipoAsignacion"] = tipo;
            e.Ejecutar();
            Assert.Contains(aviso, e.Avisos);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Armado del CSV, job y subida

        [Fact]
        public void El_CSV_es_el_encabezado_exacto_y_una_fila()
        {
            var e = new Escenario(Admin);
            e.Ejecutar();
            var esperado = Encabezado + "\r\n"
                + "Cliente Nuevo QA,001-010180-0001A,\"De la rotonda 2 c al sur, casa 5\",Igual a casa,Oficina central,Managua,88880000,22220000,"
                + Pan + ",1500.00,0.00,1500.50,10.25,100.00,0.00,20,1,2026-09-01,2026-10-20,05,PV020,Préstamo Vehículo,Fiador QA,"
                + "Del mercado 5 c al lago,450.00,0.00,VIP,Alta manual de prueba,Distrito X,Empresa QA,Especial,Marvin Obando,2026-10-13\r\n";
            Assert.Equal(esperado, e.Csv);
            var bytes = (byte[])e.Pedidos.Single(p => p.RequestName == "UploadBlock")["BlockData"];
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "sin BOM");
        }

        [Fact]
        public void Los_opcionales_vacios_van_vacios()
        {
            var e = new Escenario(Admin);
            foreach (var c in new[] { "ContagiadoUSD", "PagoMinimoNIO", "RetiroNIO", "FechaPago", "Nota", "NombreFianza", "DireccionFianza" })
            {
                e.H.Inputs.Remove(c);
            }
            e.Ejecutar();
            var v = e.Valores;
            Assert.Equal(string.Empty, v["ContagiadoUSD"]);
            Assert.Equal(string.Empty, v["PagoMinimoNIO"]);
            Assert.Equal(string.Empty, v["RetiroNIO"]);
            Assert.Equal(string.Empty, v["FechaPago"]);
            Assert.Equal(string.Empty, v["Nota"]);
            Assert.Equal(string.Empty, v["NombreFianza"]);
        }

        [Theory]
        [InlineData(true, "prueba")]
        [InlineData(false, "real")]
        public void El_job_lo_crea_el_llamador_sin_estado_y_sin_PAN_en_el_nombre(bool prueba, string sufijo)
        {
            var e = new Escenario(Admin);
            e.H.Inputs["EsPrueba"] = prueba;
            e.Ejecutar();

            e.H.Service.Verify(s => s.Create(It.IsAny<Entity>()), Times.Once());
            e.Sistema.Verify(s => s.Create(It.IsAny<Entity>()), Times.Never());
            e.Sistema.Verify(s => s.Execute(It.IsAny<OrganizationRequest>()), Times.Never());
            var j = e.JobCreado;
            Assert.Equal(Job, j.LogicalName);
            var nombre = j.GetAttributeValue<string>("sanic_smlt_ij_name");
            Assert.StartsWith("Alta manual 001-010180-0001A ••••" + Ultimos4 + " ", nombre);
            Assert.EndsWith(" · " + sufijo, nombre);
            Assert.DoesNotContain(Pan, nombre);
            Assert.False(Regex.IsMatch(nombre, @"\d{13,}"), "sin 13 digitos seguidos");
            Assert.Equal(prueba, j.GetAttributeValue<bool>("sanic_smlt_ij_isdryrun"));
            Assert.Equal(PlantillaId, j.GetAttributeValue<EntityReference>("sanic_smlt_ij_importtemplateid").Id);
            Assert.False(j.Contains("sanic_smlt_ij_status"), "el estado lo pone la app despues de la espera");
            Assert.False(j.Contains("sanic_smlt_ij_sourcefile"));
        }

        [Fact]
        public void Un_CIF_con_13_digitos_seguidos_no_va_entero_al_nombre_del_job()
        {
            var e = new Escenario(Admin);
            e.H.Inputs["CIF"] = "0010101800001A";
            e.Ejecutar();
            var nombre = e.JobCreado.GetAttributeValue<string>("sanic_smlt_ij_name");
            Assert.DoesNotContain("0010101800001", nombre);
            Assert.Contains("CIF …001A", nombre);
            Assert.Equal("0010101800001A", e.Valores["CIF"]);
        }

        [Fact]
        public void El_archivo_se_sube_en_tres_pasos_al_job()
        {
            var e = new Escenario(Admin);
            e.Ejecutar();
            Assert.Equal(new[] { "InitializeFileBlocksUpload", "UploadBlock", "CommitFileBlocksUpload" }, e.Pedidos.Select(p => p.RequestName).ToArray());
            var ini = e.Pedidos[0];
            Assert.Equal(e.JobId, ((EntityReference)ini["Target"]).Id);
            Assert.Equal(Job, ((EntityReference)ini["Target"]).LogicalName);
            Assert.Equal("sanic_smlt_ij_sourcefile", ini["FileAttributeName"]);
            var archivo = (string)ini["FileName"];
            Assert.StartsWith("alta-manual-", archivo);
            Assert.EndsWith(".csv", archivo);
            Assert.DoesNotContain(Pan, archivo);
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(archivo, @"\d{13,}"), "el nombre del archivo no puede parecer un numero de tarjeta");
            Assert.Equal("token-1", e.Pedidos[1]["FileContinuationToken"]);
            var commit = e.Pedidos[2];
            Assert.Equal("text/csv", commit["MimeType"]);
            Assert.Equal("token-1", commit["FileContinuationToken"]);
            Assert.Equal(new[] { (string)e.Pedidos[1]["BlockId"] }, (string[])commit["BlockList"]);
            Assert.Equal(archivo, commit["FileName"]);
        }

        [Fact]
        public void Si_falla_la_subida_borra_el_job_y_no_cita_el_PAN()
        {
            var e = new Escenario(Admin) { FallaSubida = true };
            var ex = e.Rechaza();
            Assert.Contains("no se pudo subir", ex.Message);
            Assert.Contains("-2147220970", e.H.TodoElTexto());
            e.H.Service.Verify(s => s.Delete(Job, e.JobId), Times.Once());
            Assert.False(e.H.Outputs.Contains("JobId"));
        }

        [Fact]
        public void El_PAN_y_los_montos_nunca_salen_en_trazas_ni_avisos()
        {
            var e = new Escenario(Admin);
            e.CuentasDeOtros.Add(new Entity(Cuenta, Guid.NewGuid()));
            e.Ejecutar();
            var texto = e.H.TodoElTexto() + "\n" + e.Avisos;
            Assert.DoesNotContain(Pan, texto);
            Assert.DoesNotContain(Pan.Substring(0, 6), texto);
            Assert.DoesNotContain("1500", texto);
            Assert.DoesNotContain("Cliente Nuevo QA", e.H.TodoElTexto());
            Assert.False(PanDetector.ContieneSecuenciaDeDigitos(texto, 13));
            Assert.Equal(e.JobId, (Guid)e.H.Outputs["JobId"]);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // FilaDeCarga (logica pura)

        [Theory]
        [InlineData(null, "")]
        [InlineData("simple", "simple")]
        [InlineData("con, coma", "\"con, coma\"")]
        [InlineData("con \"comillas\"", "\"con \"\"comillas\"\"\"")]
        [InlineData("dos\nlineas", "\"dos\nlineas\"")]
        public void Escapar_sigue_RFC_4180(string valor, string esperado)
        {
            Assert.Equal(esperado, FilaDeCarga.Escapar(valor));
        }

        [Fact]
        public void Canonizar_y_Luhn()
        {
            Assert.Equal(Pan, FilaDeCarga.Canonizar(Pan.Substring(0, 8) + " - " + Pan.Substring(8)));
            Assert.Null(FilaDeCarga.Canonizar("123456789012"));
            Assert.Null(FilaDeCarga.Canonizar("4111.1111.1111.1111"));
            Assert.True(FilaDeCarga.LuhnValido(Pan));
            Assert.True(FilaDeCarga.LuhnValido("4111111111111111"));
            Assert.False(FilaDeCarga.LuhnValido("4111111111111112"));
            Assert.False(FilaDeCarga.LuhnValido(string.Empty));
        }

        [Fact]
        public void Armar_rechaza_columnas_que_no_son_del_archivo_del_banco()
        {
            Assert.Throws<ArgumentException>(() => FilaDeCarga.Armar(new Dictionary<string, string> { { "Otra", "x" } }));
            Assert.Equal(33, FilaDeCarga.Encabezado.Length);
            Assert.Equal(Encabezado, string.Join(",", FilaDeCarga.Encabezado));
        }
    }
}
