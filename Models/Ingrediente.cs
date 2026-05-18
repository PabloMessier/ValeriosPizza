namespace ValeriosPizza.Models;

public class Ingrediente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty; // kg, unidades, litros, etc.
    public double CantidadActual { get; set; }
    public double CantidadMinima { get; set; }
    public DateTime FechaActualizacion { get; set; }
    // Permite "borrar" un ingrediente sin perder el historial: los inactivos
    // dejan de aparecer en los selectores pero conservan sus registros.
    public bool Activo { get; set; } = true;
}
