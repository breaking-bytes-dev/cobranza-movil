namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Nombres logicos de tablas y columnas. Fuente de verdad:
    /// solutions/unpacked/Entities/*/Entity.xml y scripts/pan/paso1_esquema.py (columnas de la Fase 2).
    /// </summary>
    public static class Esquema
    {
        // Tablas
        public const string Cuenta = "sanic_cm_tbl_cuenta";
        public const string CargaDiaria = "sanic_cm_tbl_cargadiaria";
        public const string CuentaPan = "sanic_cm_tbl_cuentapan";
        public const string Asignacion = "sanic_cm_tbl_asignacion";
        public const string Interaccion = "sanic_cm_tbl_interaccion";
        public const string Pago = "sanic_cm_tbl_pago";
        public const string ActualizacionDatos = "sanic_cm_tbl_actualizaciondatos";
        public const string Rol = "role";
        public const string Agente = "sanic_cm_tbl_agente";
        public const string Usuario = "systemuser";
        public const string Equipo = "team";
        public const string Cliente = "sanic_cm_tbl_cliente";
        public const string Fiador = "sanic_cm_tbl_fiador";

        // Columnas de texto donde un PAN en claro esta prohibido (el valor valido es el enmascarado)
        public const string CuentaNumero = "sanic_cm_ct_numerodecuenta";
        public const string CargaDiariaNumero = "sanic_cm_cd_numerocuenta";
        public const string CuentaPanNombre = "sanic_cm_cp_name";

        // Columnas denormalizadas viejas: deben quedar vacias (se eliminan en el paso 8d)
        public const string AsignacionNumeroLegacy = "sanic_cm_as_numerocuenta";
        public const string InteraccionNumeroLegacy = "sanic_cm_in_numerocuenta";
        public const string PagoNumeroLegacy = "sanic_cm_pg_numerocuenta";

        // Clave sustituta (HMAC) y lookups
        public const string CuentaClavePan = "sanic_cm_ct_clavepan";
        public const string AsignacionClavePan = "sanic_cm_as_clavepan";
        public const string AsignacionCuentaId = "sanic_cm_as_cuentaid";
        public const string InteraccionCuentaId = "sanic_cm_in_cuentaid";
        public const string PagoInteraccion = "sanic_cm_pg_interaccion";
        public const string PagoCuentaId = "sanic_cm_pg_cuentaid";
        public const string InteraccionNumero = "sanic_cm_in_numerointeraccion";
        public const string PagoNumeroInteraccion = "sanic_cm_pg_numerointeraccion";
        public const string ActualizacionInteraccion = "sanic_cm_ad_interaccionid";
        // F7: offline la actualizacion llega sin interaccion; trae su cuenta y la misma fecha de captura que la interaccion
        public const string ActualizacionCuentaId = "sanic_cm_ad_cuentaid";
        public const string ActualizacionFechaCaptura = "sanic_cm_ad_fechacaptura";
        public const string InteraccionActualizaInfo = "sanic_cm_in_actualizainfo";
        public const string CreadoPor = "createdby";
        public const int ToleranciaCapturaSegundos = 2;
        // F7 1.0.8.8: offline tambien se descarta la fecha; se toma la interaccion pendiente mas vieja de esta ventana
        public const int VentanaSinFechaHoras = 24;
        public const string InteraccionId = "sanic_cm_tbl_interaccionid";
        public const string ActualizacionId = "sanic_cm_tbl_actualizaciondatosid";
        // F8: lo que viene del telefono nunca se rechaza (el runtime offline lo descarta en silencio): se acepta marcado
        public const string ActualizacionSinVincular = "sanic_cm_ad_sinvincular";

        // F6: recibo repetido; no se rechaza, se marca para que BackOffice lo revise
        public const string PagoNumeroRecibo = "sanic_cm_pg_numerorecibo";
        public const string PagoReciboRepetido = "sanic_cm_pg_reciborepetido";

        // Aprobacion de la asignacion Especial (S-6)
        public const string AsignacionTipo = "sanic_cm_as_tipo";
        public const string AsignacionEstadoAprobacion = "sanic_cm_as_estadoaprobacion";
        public const string AsignacionAgente = "sanic_cm_as_agenteid";
        public const int TipoEspecial = 3;          // sanic_choice_cm_tipoasignacion; tambien es el valor por defecto
        public const int AprobacionAprobado = 3;    // sanic_choice_cm_estadoaprobacionespecial
        public const int AprobacionRechazada = 4;

        // Reparto de la asignacion (RepartoAsignacion: reemplaza al flow "Cambio de Owner en Asignaciones")
        public const string Propietario = "ownerid";
        public const string AsignacionId = "sanic_cm_tbl_asignacionid";
        public const string AsignacionEstado = "sanic_cm_as_estado";
        public const string AsignacionSolicitadoPor = "sanic_cm_as_solicitadopor";
        public const string CuentaId = "sanic_cm_tbl_cuentaid";
        public const string CuentaCliente = "sanic_cm_ct_clienteid";
        public const string CuentaFiador = "sanic_cm_ct_fiador";

        // Cartera sin cobrador (DAT-07/08): equipo dueno, sin miembros, de las asignaciones cuyo agente no tiene usuario.
        // El nombre lleva el entorno al final, como los demas equipos de CM: "CM · Cartera sin cobrador · DEV".
        public const string PrefijoEquipoSinCobrador = "CM \u00B7 Cartera sin cobrador";
        public const string EquipoNombre = "name";
        public const string EquipoTipo = "teamtype";
        public const int EquipoTipoDueno = 0;

        // Columnas de la asignacion que escriben las personas (ColumnasAsignacion, S-11) y la carga (CargaSobreAsignacion)
        public const string PrefijoColumnaAsignacion = "sanic_cm_as_";
        public const string AsignacionOrden = "sanic_cm_as_orden";
        public const string AsignacionMotivo = "sanic_cm_as_motivo";
        public const string AsignacionMotivoRechazo = "sanic_cm_as_motivorechazo";
        public const string AsignacionFechaCierre = "sanic_cm_as_fechacierre";
        public const string AsignacionFechaAsignacion = "sanic_cm_as_fechasignacion";
        public const string AsignacionUltimaCarga = "sanic_cm_as_ultimacarga";
        public const string AsignacionResolutor = "sanic_cm_as_resolutorid";
        public const string AsignacionNotas = "sanic_cm_as_notas";
        public const string AsignacionRetiroNio = "sanic_cm_as_retironio";
        public const string AsignacionRetiroUsd = "sanic_cm_as_retirousd";
        public const int TipoDiaria = 1;
        public const int TipoIncobrable = 2;
        public const int AprobacionBorrador = 1;
        public const int AprobacionPendiente = 2;

        // Motivos de cierre que pone el sistema: la reconciliacion del backoffice (DAT-10) y la reclasificacion (DAT-15)
        public const string MotivoFueraDeCartera = "fuera de cartera";
        public const string PrefijoMotivoReclasificada = "Reclasificada a ";

        // Interaccion: grabacion en el CORE (DAT-11) y alta desde el telefono (DAT-13, SEC-08)
        public const string PrefijoColumnaCm = "sanic_cm_";
        public const string InteraccionAgente = "sanic_cm_in_agente";
        public const string InteraccionNombreAgente = "sanic_cm_in_nombreagente";
        public const string InteraccionEstadoGrabacion = "sanic_cm_in_estadograbacion";
        public const string InteraccionCodigoGrabacion = "sanic_cm_in_codigograbacion";
        public const string InteraccionFechaGrabacion = "sanic_cm_in_fechagrabacion";
        public const string InteraccionGrabadoPor = "sanic_cm_in_grabadopor";
        public const string InteraccionFechaCaptura = "sanic_cm_in_fechacaptura"; // la pone el telefono (APP-20), UTC al llegar
        public const string CreadoEl = "createdon";
        public const string AuditoriaCreadoEl = "createdon"; // columna del registro de auditoria
        public const int GrabacionPendiente = 1;
        public const int GrabacionGrabado = 2;
        public const string AgenteNombre = "sanic_cm_ag_nombre";

        // Custom API SolicitarEspecial (DAT-14): mensaje global, sin entidad; corre en la etapa principal (30)
        public const string MensajeSolicitarEspecial = "sanic_cm_SolicitarEspecial";
        public const string EntidadNinguna = "none";
        public const int EtapaMainOperation = 30;
        public const string ParamCuentaId = "CuentaId";
        public const string ParamRetiroNio = "RetiroNio";
        public const string ParamRetiroUsd = "RetiroUsd";
        public const string ParamNotas = "Notas";
        public const string SalidaAsignacionId = "AsignacionId";
        public const string SalidaResultado = "Resultado";
        public const string SalidaSolicitadoPor = "SolicitadoPor";
        public const string SalidaEstadoAprobacion = "EstadoAprobacion";
        public const string ResultadoCreada = "creada";
        public const string ResultadoReactivada = "reactivada";
        public const string ResultadoYaExiste = "ya existe";
        public const string UsuarioNombreCompleto = "fullname";

        // Custom API AltaCuenta (DAT-20): el Admin da de alta una cuenta como una importacion de UNA fila de Smelter
        public const string MensajeAltaCuenta = "sanic_cm_AltaCuenta";
        public const string ParamProductoId = "ProductoId";
        public const string ParamAgenteId = "AgenteId";
        public const string ParamEsPrueba = "EsPrueba";
        public const string SalidaJobId = "JobId";
        public const string SalidaAdvertencias = "Advertencias";
        public const string AgenteActivo = "sanic_cm_ag_activo";
        public const string Producto = "sanic_cm_tbl_producto";
        public const string ProductoCodigo = "sanic_cm_pr_codigo";
        public const string ProductoTipo = "sanic_cm_pr_tipo";
        public const string ClienteCif = "sanic_cm_cl_cif";
        public const string ClienteNombre = "sanic_cm_cl_nombre";
        public const string ClienteDepartamento = "sanic_cm_cl_departamento";
        public const string ClienteDistrito = "sanic_cm_cl_distrito";
        public const string ClienteTrabajo = "sanic_cm_cl_trabajo";
        public const string Direccion = "sanic_cm_tbl_direccion";
        public const string DireccionCif = "sanic_cm_dr_cif";
        public const string DireccionTipo = "sanic_cm_dr_tipo";
        public const string DireccionTexto = "sanic_cm_dr_direccion";
        public const string Telefono = "sanic_cm_tbl_telefono";
        public const string TelefonoCif = "sanic_cm_tl_cif";
        public const string TelefonoTipo = "sanic_cm_tl_tipo";
        public const string TelefonoNumero = "sanic_cm_tl_numero";
        public const string FiadorNombre = "sanic_cm_fi_nombre";
        public const string FiadorDireccion = "sanic_cm_fi_direccion";
        public const string CuentaUltimos4 = "sanic_cm_ct_ultimos4";
        public const string CargaDiariaFecha = "sanic_cm_cd_fechacarga";
        // Smelter (otro equipo): solo se crean Import Jobs, como la pantalla "Cargar archivo" del backoffice
        public const string PlantillaSmelter = "sanic_tbl_smlt_importtemplate";
        public const string PlantillaSmelterNombre = "sanic_smlt_it_name";
        public const string NombrePlantillaCobranza = "Cobranza movil";
        public const string JobSmelter = "sanic_tbl_smlt_importjob";
        public const string JobSmelterNombre = "sanic_smlt_ij_name";
        public const string JobSmelterPlantilla = "sanic_smlt_ij_importtemplateid";
        public const string JobSmelterEsPrueba = "sanic_smlt_ij_isdryrun";
        public const string JobSmelterArchivo = "sanic_smlt_ij_sourcefile";
        public const string JobSmelterEstado = "sanic_smlt_ij_status";
        public const string PlantillaSmelterDelimitador = "sanic_smlt_it_delimiter";
        public const string PlantillaSmelterEncabezado = "sanic_smlt_it_hasheaderrow";
        public const string PlantillaSmelterLote = "sanic_smlt_it_chunksize";
        public const string PlantillaSmelterEstado = "sanic_smlt_it_status";
        public const string PlantillaSmelterOrigen = "sanic_smlt_it_sourcetype";

        // Custom API PartirCarga (carga paralela): parte el archivo de un job en "padres" (producto, fiador y agente, con la
        // ULTIMA fila de cada uno) y N "partes" por CIF. Las plantillas derivadas las genera scripts/pan/paso20.
        public const string MensajePartirCarga = "sanic_cm_PartirCarga";
        public const string ParamJobId = "JobId";
        public const string ParamPartes = "Partes";
        /// <summary>1.0.9.9, opcional: true = solo mide el largo de los valores (prueba en seco y carga en serie), sin escribir.</summary>
        public const string ParamSoloValidar = "SoloValidar";
        public const string SalidaPadresJobId = "PadresJobId";
        public const string SalidaPartesJobIds = "PartesJobIds";
        public const string SalidaPartes = "Partes";
        public const string SalidaFilasArchivo = "FilasArchivo";
        public const string SalidaFilasPadres = "FilasPadres";
        public const string SalidaFilasPartes = "FilasPartes";
        public const string NombrePlantillaPadres = "Cobranza movil · padres";
        public const string NombrePlantillaPartes = "Cobranza movil · partes";
        public const string MapeoTabla = "sanic_tbl_smlt_tablemapping";
        public const string MapeoTablaId = "sanic_tbl_smlt_tablemappingid";
        public const string MapeoTablaPlantilla = "sanic_smlt_tm_importtemplateid";
        public const string MapeoTablaNombre = "sanic_smlt_tm_name";
        public const string MapeoTablaDestino = "sanic_smlt_tm_targettable";
        public const string MapeoTablaDedup = "sanic_smlt_tm_dedupcolumns";
        public const string MapeoTablaOrden = "sanic_smlt_tm_processingorder";
        public const string MapeoTablaCrear = "sanic_smlt_tm_createifnotexists";
        public const string MapeoTablaActualizar = "sanic_smlt_tm_updateifexists";
        public const string MapeoTablaIntermedia = "sanic_smlt_tm_isjunctiontable";
        public const string MapeoColumna = "sanic_tbl_smlt_columnmapping";
        public const string MapeoColumnaTabla = "sanic_smlt_cm_tablemappingid";
        public const string MapeoColumnaNombre = "sanic_smlt_cm_name";
        public const string MapeoColumnaDestino = "sanic_smlt_cm_targetcolumn";
        public const string MapeoColumnaFuente = "sanic_smlt_cm_sourcecolumn";
        public const string MapeoColumnaTipo = "sanic_smlt_cm_datatype";
        public const string MapeoColumnaRequerida = "sanic_smlt_cm_isrequired";
        public const string MapeoColumnaDefault = "sanic_smlt_cm_defaultvalue";
        public const string MapeoColumnaLookupTabla = "sanic_smlt_cm_lookuptable";
        public const string MapeoColumnaLookupClave = "sanic_smlt_cm_lookupmatchcolumn";
        public const string MapeoColumnaOpciones = "sanic_smlt_cm_optionsetmapping";
        public const string MapeoColumnaTransformacion = "sanic_smlt_cm_transformexpression";

        // Agente <-> usuario (app del cobrador: gblAgente busca por Lower(User().Email), que es el UPN)
        public const string AgenteUsuario = "sanic_cm_ag_usuarioid";
        public const string AgenteEmail = "sanic_cm_ag_emailusuario";
        public const string UsuarioUpn = "domainname";

        // Roles de seguridad de CM: los de personas empiezan con este prefijo
        public const string PrefijoRolCm = "SR | CM | ";

        // Mensajes y etapas del pipeline
        public const string MensajeCreate = "Create";
        public const string MensajeUpdate = "Update";
        public const int EtapaPreValidation = 10;
        public const int EtapaPreOperation = 20;
        public const int EtapaPostOperation = 40;
        public const int ModoAsincrono = 1;

        // F3: la Especial de la carga toma tipo, nota y retiro de la cuenta (EspecialDesdeCuenta)
        public const string CuentaTipoEspecial = "sanic_cm_ct_tipoespecial";
        public const string CuentaNota = "sanic_cm_ct_nota";
        public const string CuentaRetiroNio = "sanic_cm_ct_retironio";
        public const string CuentaRetiroUsd = "sanic_cm_ct_retirousd";
        public const string SeparadorNotasEspecial = " — ";   // "Tipo — nota": lo parten EspTipo/EspNota de la app
        public const int LargoMaximoNotasAsignacion = 2000;

        // Borrado por salida de cartera: la interaccion guarda con que identificar la cuenta en el core (IdentidadEnInteraccion)
        public const string CuentaCif = "sanic_cm_ct_cifcliente";
        public const string InteraccionUltimos4 = "sanic_cm_in_ultimos4";
        public const string InteraccionCif = "sanic_cm_in_cifcliente";
        public const string InteraccionClavePan = "sanic_cm_in_clavepan";

        // Escala de la cartera: la app trae las asignaciones por tramos, cada uno bajo el limite de 2000 filas (TramoCartera)
        public const string AsignacionTramo = "sanic_cm_as_tramo";
        public const string CuentaTramo = "sanic_cm_ct_tramo";
        // El pago tambien: Transcritas trae los pagos del dia (de todos los cobradores) por tramos
        public const string PagoTramo = "sanic_cm_pg_tramo";
        public const int TramosCartera = 20;

        // Imagen previa que registra el paso Update de ClaveDesdeCuenta
        public const string NombreImagenPrevia = "PreImage";
    }
}
