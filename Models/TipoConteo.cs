namespace ValeriosPizza.Models;

public enum TipoConteo
{
    Apertura,
    Cierre
}

public static class TipoConteoExtensions
{
    public static string Mostrar(this TipoConteo tipo) => tipo switch
    {
        TipoConteo.Apertura => "Apertura",
        TipoConteo.Cierre => "Cierre",
        _ => tipo.ToString()
    };
}
