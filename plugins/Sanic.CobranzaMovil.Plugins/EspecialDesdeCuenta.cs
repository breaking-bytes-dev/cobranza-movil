using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// La carga escribe el tipo, la nota y el retiro de la Especial en la cuenta, pero las apps los leen de la
    /// asignacion; esto los copia de la cuenta a la asignacion para que haya un solo lugar donde leerlos.
    /// Solo en una Especial sin solicitante (carga o alta) y solo lo vacio: nunca pisa lo que escribio una persona.
    /// Las notas quedan "Tipo — nota", como en la Especial pedida. Lee con el servicio del sistema; nunca traza valores.
    /// Registro: PreOperation sincrono, Create de sanic_cm_tbl_asignacion y Update filtrado por ultima carga con
    /// imagen previa "PreImage" de tipo, cuenta, solicitante, notas y retiros.
    /// </summary>
    public sealed class EspecialDesdeCuenta : PluginBase
    {
        protected override string Nombre
        {
            get { return "EspecialDesdeCuenta"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
            local.ExigirRegistro(Esquema.Asignacion, Esquema.EtapaPreOperation, Esquema.MensajeCreate, Esquema.MensajeUpdate);

            var target = local.ObtenerTarget();
            var previa = local.EsMensaje(Esquema.MensajeUpdate) ? local.ObtenerImagenPrevia() : null;

            var tipo = Valor<OptionSetValue>(target, previa, Esquema.AsignacionTipo);
            var cuenta = Valor<EntityReference>(target, previa, Esquema.AsignacionCuentaId);
            if (tipo == null || tipo.Value != Esquema.TipoEspecial || cuenta == null)
            {
                local.Trace("no es una Especial con cuenta; sin cambios.");
                return;
            }
            if (Valor<EntityReference>(target, previa, Esquema.AsignacionSolicitadoPor) != null)
            {
                local.Trace("Especial pedida por una persona; sus datos viven en la asignacion.");
                return;
            }

            var faltanNotas = string.IsNullOrWhiteSpace(Valor<string>(target, previa, Esquema.AsignacionNotas));
            var faltaNio = Valor<decimal?>(target, previa, Esquema.AsignacionRetiroNio) == null;
            var faltaUsd = Valor<decimal?>(target, previa, Esquema.AsignacionRetiroUsd) == null;
            if (!faltanNotas && !faltaNio && !faltaUsd)
            {
                local.Trace("la Especial ya tiene tipo, nota y retiro; sin cambios.");
                return;
            }

            var fila = local.ServicioSistema.Retrieve(Esquema.Cuenta, cuenta.Id, new ColumnSet(
                Esquema.CuentaTipoEspecial, Esquema.CuentaNota, Esquema.CuentaRetiroNio, Esquema.CuentaRetiroUsd));
            if (fila == null) return;

            var copiadas = 0;
            if (faltanNotas)
            {
                var notas = Notas(fila.GetAttributeValue<string>(Esquema.CuentaTipoEspecial), fila.GetAttributeValue<string>(Esquema.CuentaNota));
                if (notas != null)
                {
                    target[Esquema.AsignacionNotas] = notas;
                    copiadas++;
                }
            }
            copiadas += Copiar(target, faltaNio, fila, Esquema.CuentaRetiroNio, Esquema.AsignacionRetiroNio);
            copiadas += Copiar(target, faltaUsd, fila, Esquema.CuentaRetiroUsd, Esquema.AsignacionRetiroUsd);
            local.Trace("F3: {0} dato(s) de la Especial copiados de la cuenta.", copiadas);
        }

        /// <summary>"Tipo — nota" (o solo la nota si no hay tipo); null si la cuenta no trae ninguno.</summary>
        private static string Notas(string tipo, string nota)
        {
            tipo = (tipo ?? string.Empty).Trim();
            nota = (nota ?? string.Empty).Trim();
            if (tipo.Length == 0 && nota.Length == 0) return null;
            var texto = tipo.Length == 0 ? nota : tipo + Esquema.SeparadorNotasEspecial + nota;
            return texto.Length > Esquema.LargoMaximoNotasAsignacion ? texto.Substring(0, Esquema.LargoMaximoNotasAsignacion) : texto;
        }

        private static int Copiar(Entity target, bool falta, Entity cuenta, string deCuenta, string aAsignacion)
        {
            var valor = cuenta.GetAttributeValue<decimal?>(deCuenta);
            if (!falta || valor == null) return 0;
            target[aAsignacion] = valor.Value;
            return 1;
        }

        private static T Valor<T>(Entity target, Entity previa, string columna)
        {
            if (target.Contains(columna)) return target.GetAttributeValue<T>(columna);
            return previa == null ? default(T) : previa.GetAttributeValue<T>(columna);
        }
    }
}
