using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Mantiene coherente la clave sustituta de la asignacion con la de su cuenta:
    ///  - as_cuentaid informado y el Target sin as_clavepan  -> la copia de ct_clavepan de la cuenta.
    ///  - as_cuentaid informado y as_clavepan distinto        -> rechaza (nombra columnas, no valores).
    ///  - as_cuentaid borrado (null)                           -> no toca nada.
    /// Asi el backoffice no tiene que escribir la clave desde el cliente (S-9). No conoce la clave K:
    /// solo copia un HMAC que ya esta en la cuenta.
    ///
    /// Registro: PreOperation sincrono. Create, y Update filtrado por as_cuentaid y as_clavepan con una
    /// imagen previa "PreImage" que lleve as_cuentaid (para validar un Update que solo cambia as_clavepan).
    /// </summary>
    public sealed class ClaveDesdeCuenta : PluginBase
    {
        protected override string Nombre
        {
            get { return "ClaveDesdeCuenta"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            // Modifica el Target: solo tiene efecto en PreOperation. Falla cerrado si esta mal registrado.
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation,
                Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();

            var cuenta = ResolverCuenta(local, target);
            if (cuenta == null)
            {
                local.Trace("sin cuenta que resolver; sin cambios.");
                return;
            }

            var claveCuenta = ClaveDeLaCuenta(local, cuenta);
            if (string.IsNullOrEmpty(claveCuenta))
            {
                // Cuenta sin clave (aun sin backfill): no se puede copiar ni comparar. No se bloquea el alta.
                local.Trace("la cuenta no tiene {0}; sin cambios.", Esquema.CuentaClavePan);
                return;
            }

            var claveTarget = target.Contains(Esquema.AsignacionClavePan)
                ? target.GetAttributeValue<string>(Esquema.AsignacionClavePan)
                : null;

            if (string.IsNullOrEmpty(claveTarget))
            {
                target[Esquema.AsignacionClavePan] = claveCuenta;
                local.Trace("{0} completada desde la cuenta.", Esquema.AsignacionClavePan);
                return;
            }

            if (!string.Equals(claveTarget, claveCuenta, StringComparison.Ordinal))
            {
                local.Trace("rechazado: {0} no coincide con la clave de la cuenta.", Esquema.AsignacionClavePan);
                throw local.Error(Esquema.AsignacionClavePan + " no coincide con la clave de la cuenta indicada en "
                    + Esquema.AsignacionCuentaId + ".");
            }

            local.Trace("{0} ya coincide con la cuenta.", Esquema.AsignacionClavePan);
        }

        /// <summary>
        /// Cuenta contra la que validar. Prioriza la del Target; en un Update que solo toca as_clavepan usa
        /// la de la imagen previa. Devuelve null si no hay cuenta o si el Target la esta borrando.
        /// </summary>
        // El upsert de Smelter referencia la cuenta por su clave alternativa (ct_clavepan). La doc no garantiza que en
        // PreOperation la referencia ya traiga el Id: si no lo trae, la clave de la cuenta ES el valor de la referencia.
        // Si la plataforma ya resolvio el Id POR esa clave, la clave de la cuenta sigue siendo el valor de la referencia (la
        // clave alternativa es unica): se usa sin leer la cuenta, pero solo cuando escribe SYSTEM (la carga arma la
        // referencia en codigo de confianza). A cualquier otro llamador con Id se le lee la cuenta como siempre: con un Id
        // explicito la plataforma no tiene por que mirar la clave que acompana a la referencia (1.0.9.6).
        private static string ClaveDeLaCuenta(LocalContext local, EntityReference cuenta)
        {
            object valor;
            var claveEnReferencia = cuenta.KeyAttributes != null && cuenta.KeyAttributes.TryGetValue(Esquema.CuentaClavePan, out valor)
                ? valor as string
                : null;
            if (cuenta.Id == Guid.Empty)
            {
                if (claveEnReferencia != null) return claveEnReferencia;
                local.Trace("cuenta referenciada sin Id ni {0}; sin cambios.", Esquema.CuentaClavePan);
                return null;
            }
            if (!string.IsNullOrEmpty(claveEnReferencia) && UsuarioSistema.EsElLlamador(local))
            {
                local.Trace("cuenta de la carga resuelta por {0}; se usa la clave de la referencia sin leerla.", Esquema.CuentaClavePan);
                return claveEnReferencia;
            }

            // Una sola lectura, de una sola columna.
            var fila = local.Service.Retrieve(Esquema.Cuenta, cuenta.Id, new ColumnSet(Esquema.CuentaClavePan));
            return fila == null ? null : fila.GetAttributeValue<string>(Esquema.CuentaClavePan);
        }

        private static EntityReference ResolverCuenta(LocalContext local, Entity target)
        {
            if (target.Contains(Esquema.AsignacionCuentaId))
            {
                // null aqui = se esta borrando el lookup: se deja todo como esta.
                return target.GetAttributeValue<EntityReference>(Esquema.AsignacionCuentaId);
            }

            if (local.EsMensaje(Esquema.MensajeUpdate) && target.Contains(Esquema.AsignacionClavePan))
            {
                var imagenes = local.Context.PreEntityImages;
                if (imagenes != null && imagenes.Contains(Esquema.NombreImagenPrevia))
                {
                    var previa = imagenes[Esquema.NombreImagenPrevia];
                    if (previa != null) return previa.GetAttributeValue<EntityReference>(Esquema.AsignacionCuentaId);
                }
                local.Trace("Update de {0} sin imagen previa; no se puede validar.", Esquema.AsignacionClavePan);
            }

            return null;
        }
    }
}
