using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Escala de la cartera: la app copia la cartera a colecciones, y cada copia se corta en el limite de filas (2000).
    /// Con el tramo, la app la trae en partes: Filter(Asignaciones, Tramo = n) para n = 0..TramosCartera-1, cada una
    /// muy por debajo del limite (20 tramos: hasta 40.000 asignaciones por cobrador).
    ///
    /// El tramo sale de la CUENTA y no de la asignacion: es estable (la misma cuenta cae siempre en el mismo tramo, aunque
    /// la carga la vuelva a crear). Es un hash FNV-1a del id: los ids de Dataverse son SECUENCIALES, y tomar sus primeros
    /// bytes repartia mal (228 a 410 por tramo con 6.005 asignaciones). Solo completa lo vacio; nunca rechaza.
    /// La CUENTA lleva el mismo tramo (sale de su propio id), asi la app trae tambien las cuentas por tramos y las cruza con
    /// las asignaciones sin consultar una por una.
    /// El PAGO lleva el tramo de su interaccion (todo pago cuelga de una, DAT-13): el backoffice trae los pagos del dia de
    /// todos los cobradores por tramos. Los pagos de una misma visita caen juntos.
    /// Registro: PreOperation sincrono, Create de sanic_cm_tbl_asignacion y de sanic_cm_tbl_pago; y PostOperation ASINCRONO,
    /// Create de sanic_cm_tbl_cuenta (el id de la cuenta recien existe despues de crearla; asincrono para no frenar la carga).
    /// </summary>
    public sealed class TramoCartera : PluginBase
    {
        protected override string Nombre
        {
            get { return "TramoCartera"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            if (local.Context.PrimaryEntityName == Esquema.Cuenta)
            {
                TramoDeLaCuenta(local);
                return;
            }
            if (local.Context.PrimaryEntityName == Esquema.Pago)
            {
                local.ExigirRegistro(Esquema.Pago, Esquema.EtapaPreOperation, Esquema.MensajeCreate);
                TramoDesde(local, Esquema.PagoTramo, Esquema.PagoInteraccion, "el pago");
                return;
            }
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeCreate);
            TramoDesde(local, Esquema.AsignacionTramo, Esquema.AsignacionCuentaId, "la asignacion");
        }

        /// <summary>PreOperation: el tramo sale del id del lookup indicado. Solo completa lo vacio.</summary>
        private static void TramoDesde(LocalContext local, string columnaTramo, string lookup, string que)
        {
            var target = local.ObtenerTarget();
            if (target.GetAttributeValue<int?>(columnaTramo) != null)
            {
                local.Trace("{0} ya trae tramo; sin cambios.", que);
                return;
            }
            var origen = target.GetAttributeValue<EntityReference>(lookup);
            if (origen == null)
            {
                local.Trace("{0} no trae {1}; queda sin tramo.", que, lookup);
                return;
            }
            target[columnaTramo] = Tramo(origen.Id);
            local.Trace("tramo asignado.");
        }

        private static void TramoDeLaCuenta(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Cuenta, Esquema.EtapaPostOperation, Esquema.MensajeCreate);
            var id = local.Context.PrimaryEntityId;
            if (id == Guid.Empty)
            {
                local.Trace("la cuenta no trae id; sin tramo.");
                return;
            }
            var cambio = new Entity(Esquema.Cuenta, id);
            cambio[Esquema.CuentaTramo] = Tramo(id);
            local.ServicioSistema.Update(cambio);
            local.Trace("tramo de la cuenta asignado.");
        }

        /// <summary>Tramo 0..TramosCartera-1 de un id (cuenta, o interaccion para el pago). Lo usa tambien el relleno (paso18).</summary>
        public static int Tramo(Guid cuenta)
        {
            // FNV-1a de 32 bits sobre los 16 bytes del id (orden de ToByteArray; en Python, uuid.UUID(id).bytes_le)
            unchecked
            {
                var h = 2166136261u;
                foreach (var b in cuenta.ToByteArray())
                {
                    h ^= b;
                    h *= 16777619u;
                }
                return (int)(h % (uint)Esquema.TramosCartera);
            }
        }
    }
}
