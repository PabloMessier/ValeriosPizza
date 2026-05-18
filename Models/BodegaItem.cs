namespace ValeriosPizza.Models;

/// <summary>
/// Item almacenado en la "Bodega" (almacén general que la dueña usa para
/// llevar control aparte del stock activo). Cada fila es una entrada manual
/// que se agrega desde las pantallas de Inventario, Mercancía Nueva o
/// Consulta. Los campos <see cref="IngredienteId"/> y
/// <see cref="MercanciaRecibidaId"/> son opcionales: se rellenan cuando la
/// fila proviene de un registro existente para conservar la traza.
/// </summary>
public class BodegaItem
{
    public int Id { get; set; }
    public DateTime FechaAgregado { get; set; } = DateTime.Now;

    /// <summary>Nombre del ingrediente / mercancía. Snapshot al momento de agregar.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>"Ingrediente", "Mercancía" u "Otro". Se usa como filtro visual.</summary>
    public string Categoria { get; set; } = "Ingrediente";

    public double Cantidad { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public string? Notas { get; set; }

    /// <summary>
    /// Pantalla desde la que se agregó: "Inventario", "Mercancía Nueva",
    /// "Consulta" — útil para auditoría. Opcional.
    /// </summary>
    public string? Origen { get; set; }

    // FKs blandos: no se imponen como FK relacional para que un ingrediente
    // borrado no rompa las filas históricas en Bodega.
    public int? IngredienteId { get; set; }
    public int? MercanciaRecibidaId { get; set; }
}
