namespace ValeriosPizza.Models;

/// <summary>
/// Estado operativo de un producto del menú.
/// <list type="bullet">
///   <item><c>Activo</c>: disponible para la venta normal.</item>
///   <item><c>Agotado</c>: temporalmente sin existencias; vuelve a estar disponible cuando se reabastece.</item>
///   <item><c>Descontinuado</c>: retirado del menú; deja de aparecer en los selectores de cortesía y registro.</item>
/// </list>
/// </summary>
public enum EstadoProducto
{
    Activo = 0,
    Agotado = 1,
    Descontinuado = 2
}

public static class EstadoProductoExtensiones
{
    public static string Mostrar(this EstadoProducto estado) => estado switch
    {
        EstadoProducto.Activo => "Activo",
        EstadoProducto.Agotado => "Agotado",
        EstadoProducto.Descontinuado => "Descontinuado",
        _ => estado.ToString()
    };
}
