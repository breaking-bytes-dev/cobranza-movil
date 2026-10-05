namespace Sanic.CobranzaMovil.Plugins
{
    /// <summary>S-6 (esqueleto en rojo).</summary>
    public sealed class AprobacionEspecial : PluginBase
    {
        protected override string Nombre
        {
            get { return "AprobacionEspecial"; }
        }

        protected override void Ejecutar(LocalContext local)
        {
        }
    }
}
