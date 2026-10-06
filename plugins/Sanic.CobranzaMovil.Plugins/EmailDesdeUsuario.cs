namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>Esqueleto en rojo.</summary>
    public sealed class EmailDesdeUsuario : PluginBase
    {
        protected override string Nombre
        {
            get { return "EmailDesdeUsuario"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
        }
    }
}
