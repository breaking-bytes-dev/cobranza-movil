using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Moq;

namespace Sanic.CobranzaMovil.Plugins.Tests
{
    /// <summary>
    /// Arma el IServiceProvider de un plugin con dobles de Moq: contexto de ejecucion, trazas capturadas,
    /// fabrica de servicios y servicio de organizacion.
    /// Se usa Moq y no FakeXrmEasy: ver README (licencia de FakeXrmEasy v3) y porque aqui hace falta
    /// afirmar con precision QUE se consulta (una sola columna) y CUANTAS veces.
    /// </summary>
    internal sealed class Harness
    {
        public static readonly Guid UsuarioLlamador = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public readonly Mock<IPluginExecutionContext> Context = new Mock<IPluginExecutionContext>();
        public readonly Mock<ITracingService> Tracing = new Mock<ITracingService>();
        public readonly Mock<IOrganizationServiceFactory> Factory = new Mock<IOrganizationServiceFactory>();
        public readonly Mock<IOrganizationService> Service = new Mock<IOrganizationService>();
        public readonly Mock<IServiceProvider> Provider = new Mock<IServiceProvider>();

        public readonly ParameterCollection Inputs = new ParameterCollection();
        public readonly ParameterCollection Outputs = new ParameterCollection();
        public readonly EntityImageCollection PreImages = new EntityImageCollection();
        public readonly List<string> Trazas = new List<string>();

        public Harness(string mensaje, string entidad, int stage = 20)
        {
            Context.SetupGet(c => c.MessageName).Returns(mensaje);
            Context.SetupGet(c => c.PrimaryEntityName).Returns(entidad);
            Context.SetupGet(c => c.Stage).Returns(stage);
            Context.SetupGet(c => c.UserId).Returns(UsuarioLlamador);
            Context.SetupGet(c => c.Depth).Returns(1);
            Context.SetupGet(c => c.InputParameters).Returns(Inputs);
            Context.SetupGet(c => c.OutputParameters).Returns(Outputs);
            Context.SetupGet(c => c.PreEntityImages).Returns(PreImages);
            Context.SetupGet(c => c.PostEntityImages).Returns(new EntityImageCollection());

            Tracing.Setup(t => t.Trace(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((f, a) =>
                    Trazas.Add(a == null || a.Length == 0 ? f : string.Format(f, a)));

            Factory.Setup(f => f.CreateOrganizationService(It.IsAny<Guid?>())).Returns(Service.Object);

            Provider.Setup(p => p.GetService(typeof(IPluginExecutionContext))).Returns(Context.Object);
            Provider.Setup(p => p.GetService(typeof(ITracingService))).Returns(Tracing.Object);
            Provider.Setup(p => p.GetService(typeof(IOrganizationServiceFactory))).Returns(Factory.Object);
        }

        /// <summary>Target del mensaje; es la MISMA instancia que ve el plugin (los plugins PreOperation la mutan).</summary>
        public Entity Target { get; private set; }

        public Harness ConTarget(Entity target)
        {
            Target = target;
            Inputs["Target"] = target;
            return this;
        }

        public Harness ConPreImage(Entity imagen)
        {
            PreImages["PreImage"] = imagen;
            return this;
        }

        /// <summary>Hace que Retrieve(entidad, id, columnas exactas) devuelva la fila indicada.</summary>
        public Harness RetrieveDevuelve(Entity fila, params string[] columnasEsperadas)
        {
            Service.Setup(s => s.Retrieve(
                    fila.LogicalName,
                    fila.Id,
                    It.Is<Microsoft.Xrm.Sdk.Query.ColumnSet>(c =>
                        !c.AllColumns && c.Columns.Count == columnasEsperadas.Length && ContieneTodas(c, columnasEsperadas))))
                .Returns(fila);
            return this;
        }

        public static bool ContieneTodas(Microsoft.Xrm.Sdk.Query.ColumnSet cs, string[] esperadas)
        {
            foreach (var e in esperadas)
            {
                if (!cs.Columns.Contains(e)) return false;
            }
            return true;
        }

        /// <summary>
        /// Roles de seguridad del llamador: cualquier RetrieveMultiple devuelve filas de "role" con esos nombres
        /// (los roles directos y los que llegan por equipos se consultan igual).
        /// </summary>
        public Harness RolesDelLlamador(params string[] nombres)
        {
            var filas = new EntityCollection();
            foreach (var n in nombres)
            {
                var r = new Entity("role", Guid.NewGuid());
                r["name"] = n;
                filas.Entities.Add(r);
            }
            Service.Setup(s => s.RetrieveMultiple(It.IsAny<Microsoft.Xrm.Sdk.Query.QueryBase>())).Returns(filas);
            return this;
        }

        public int ConsultasAlServicio()
        {
            var n = 0;
            foreach (var inv in Service.Invocations)
            {
                if (inv.Method.Name == "RetrieveMultiple") n++;
            }
            return n;
        }

        public void Ejecutar(IPlugin plugin)
        {
            plugin.Execute(Provider.Object);
        }

        public int RetrievesAlServicio()
        {
            // Cuenta las invocaciones reales a Retrieve (con cualquier argumento).
            var n = 0;
            foreach (var inv in Service.Invocations)
            {
                if (inv.Method.Name == "Retrieve") n++;
            }
            return n;
        }

        public string TodoElTexto()
        {
            return string.Join("\n", Trazas);
        }
    }
}
