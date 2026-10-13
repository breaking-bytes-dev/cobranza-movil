using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>
    /// Base comun: resuelve los servicios del proveedor, traza sin datos sensibles y normaliza errores.
    /// Reglas de seguridad (PCI):
    ///  - Nunca se traza ni se pone en un mensaje de excepcion el VALOR de una columna: solo su nombre.
    ///  - De una excepcion inesperada solo se traza su TIPO (su mensaje podria citar valores del registro).
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        protected abstract string Nombre { get; }

        protected abstract void Ejecutar(LocalContext local);

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException("serviceProvider");

            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var local = new LocalContext(Nombre, context, tracing, factory);
            try
            {
                local.Trace("inicio. mensaje={0} entidad={1} etapa={2} profundidad={3}",
                    context.MessageName, context.PrimaryEntityName, context.Stage, context.Depth);
                Ejecutar(local);
            }
            catch (InvalidPluginExecutionException)
            {
                throw; // ya viene redactado para el usuario, sin valores
            }
            catch (Exception ex)
            {
                local.Trace("error inesperado de tipo {0}", ex.GetType().Name);
                throw new InvalidPluginExecutionException(Nombre + ": error inesperado al validar el registro. Avise al administrador.");
            }
        }
    }

    /// <summary>Contexto de una ejecucion: acceso perezoso al servicio y al Target.</summary>
    public sealed class LocalContext
    {
        private readonly string nombre;
        private readonly ITracingService tracing;
        private readonly IOrganizationServiceFactory factory;
        private IOrganizationService service;
        private IOrganizationService servicioSistema;

        public LocalContext(string nombre, IPluginExecutionContext context, ITracingService tracing, IOrganizationServiceFactory factory)
        {
            this.nombre = nombre;
            Context = context;
            this.tracing = tracing;
            this.factory = factory;
        }

        public IPluginExecutionContext Context { get; private set; }

        /// <summary>
        /// Servicio con los permisos del USUARIO QUE LLAMA (minimo privilegio). Se crea solo si se usa.
        /// </summary>
        public IOrganizationService Service
        {
            get
            {
                if (service == null) service = factory.CreateOrganizationService(Context.UserId);
                return service;
            }
        }

        /// <summary>
        /// Servicio del SISTEMA. Para lecturas de configuracion que el llamador no tiene por que poder hacer
        /// (por ejemplo, sus propios roles) y, en RepartoAsignacion, para el acceso a los registros (dueno, compartir
        /// y revocar), que ningun rol de persona tiene. Nunca para escribir columnas de negocio.
        /// </summary>
        public IOrganizationService ServicioSistema
        {
            get
            {
                if (servicioSistema == null) servicioSistema = factory.CreateOrganizationService(null);
                return servicioSistema;
            }
        }

        public void Trace(string formato, params object[] args)
        {
            if (tracing != null) tracing.Trace(nombre + ": " + formato, args);
        }

        public InvalidPluginExecutionException Error(string mensaje)
        {
            return new InvalidPluginExecutionException(nombre + ": " + mensaje);
        }

        /// <summary>Target como Entity. Falla cerrado si no esta (Create/Update siempre lo traen).</summary>
        public Entity ObtenerTarget()
        {
            var inputs = Context.InputParameters;
            if (inputs != null && inputs.Contains("Target"))
            {
                var target = inputs["Target"] as Entity;
                if (target != null) return target;
            }
            throw Error("el contexto no trae un Target de tipo entidad.");
        }

        /// <summary>Imagen previa "PreImage". Falla cerrado si el paso se registro sin ella.</summary>
        public Entity ObtenerImagenPrevia()
        {
            var imagenes = Context.PreEntityImages;
            if (imagenes != null && imagenes.Contains(Esquema.NombreImagenPrevia))
            {
                var previa = imagenes[Esquema.NombreImagenPrevia];
                if (previa != null) return previa;
            }
            throw Error("el paso Update no trae la imagen previa " + Esquema.NombreImagenPrevia + ".");
        }

        /// <summary>Exige entidad, etapa y alguno de los mensajes esperados; un registro mal configurado se detecta de inmediato.</summary>
        public void ExigirRegistro(string entidad, int etapa, params string[] mensajes)
        {
            if (!string.Equals(Context.PrimaryEntityName, entidad, StringComparison.Ordinal)
                || Context.Stage != etapa
                || !EsMensaje(mensajes))
            {
                throw Error("registro incorrecto del paso. Se espera " + string.Join("/", mensajes) + " sobre " + entidad
                    + " en la etapa " + etapa + ".");
            }
        }

        public bool EsMensaje(params string[] mensajes)
        {
            foreach (var m in mensajes)
            {
                if (string.Equals(Context.MessageName, m, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
