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

        // Mensajes y etapas del pipeline
        public const string MensajeCreate = "Create";
        public const string MensajeUpdate = "Update";
        public const int EtapaPreValidation = 10;
        public const int EtapaPreOperation = 20;

        // Imagen previa que registra el paso Update de ClaveDesdeCuenta
        public const string NombreImagenPrevia = "PreImage";
    }
}
