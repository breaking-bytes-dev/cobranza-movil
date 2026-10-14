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
        public const string CreadoPor = "createdby"; // S-7: no editable, a diferencia de "Solicitado por"

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
        public const int GrabacionPendiente = 1;
        public const int GrabacionGrabado = 2;
        public const string AgenteNombre = "sanic_cm_ag_nombre";

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

        // Imagen previa que registra el paso Update de ClaveDesdeCuenta
        public const string NombreImagenPrevia = "PreImage";
    }
}
