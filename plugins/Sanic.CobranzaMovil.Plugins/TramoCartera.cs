using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Reparte la cartera en tramos: cada copia a una coleccion de la app se corta en 2000 filas, asi que la app la
    /// trae por partes (Filter(Asignaciones, Tramo = n) para n = 0..TramosCartera-1), cada una muy por debajo del limite.
    ///
    /// El tramo de la asignacion sale de su CUENTA, para que sea estable aunque la carga vuelva a crear la asignacion.
    /// Es un hash FNV-1a del id porque los ids de Dataverse son secuenciales y sus primeros bytes reparten mal.
    /// Solo completa lo vacio; nunca rechaza.
    /// La cuenta lleva el tramo de su propio id (la app cruza cuentas y asignaciones por tramo) y el pago, el de su
    /// interaccion (los pagos de una misma visita caen juntos).
    /// Registro: PreOperation sincrono, Create de asignacion y pago; PostOperation asincrono, Create de cuenta (el id
    /// recien existe despues de crearla; asincrono para no frenar la carga).
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

        /// <summary>Tramo 0..TramosCartera-1 de un id (cuenta, o interaccion para el pago). Lo usa tambien el script de relleno.</summary>
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
