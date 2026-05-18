namespace ValeriosPizza.Models;

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public Categoria Categoria { get; set; }

    /// <summary>
    /// Estado operativo del producto: Activo / Agotado / Descontinuado.
    /// Es la fuente de verdad; <see cref="Activo"/> se deriva de aquí.
    /// </summary>
    public EstadoProducto Estado { get; set; } = EstadoProducto.Activo;

    /// <summary>
    /// Bandera legacy persistida en la BD. EF la sigue mapeando para no romper
    /// la columna existente, pero su valor es 100% una función de
    /// <see cref="Estado"/>: <c>false</c> sólo cuando el producto está
    /// descontinuado. El setter normaliza cualquier valor entrante para
    /// mantener la invariante; el getter calcula a partir del estado.
    /// </summary>
    public bool Activo
    {
        get => Estado != EstadoProducto.Descontinuado;
        set { /* setter intencionalmente vacío: el valor se deriva de Estado. */ }
    }
}
