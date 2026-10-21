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
    /// Registro: PreOperation sincrono, solo Create de sanic_cm_tbl_asignacion.
    /// </summary>
    public sealed class TramoCartera : PluginBase
    {
        protected override string Nombre
        {
            get { return "TramoCartera"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeCreate);

            var target = local.ObtenerTarget();
            if (target.GetAttributeValue<int?>(Esquema.AsignacionTramo) != null)
            {
                local.Trace("la asignacion ya trae tramo; sin cambios.");
                return;
            }
            var cuenta = target.GetAttributeValue<EntityReference>(Esquema.AsignacionCuentaId);
            if (cuenta == null)
            {
                local.Trace("la asignacion no trae cuenta; queda sin tramo.");
                return;
            }
            target[Esquema.AsignacionTramo] = Tramo(cuenta.Id);
            local.Trace("tramo asignado.");
        }

        /// <summary>Tramo 0..TramosCartera-1 de una cuenta. Lo usa tambien el relleno (paso18) con la misma cuenta.</summary>
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
