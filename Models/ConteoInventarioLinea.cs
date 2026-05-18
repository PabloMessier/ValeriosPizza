namespace ValeriosPizza.Models;

/// <summary>
/// Una línea individual de un ConteoInventario: ingrediente y cantidad física contada.
/// </summary>
public class ConteoInventarioLinea
{
    public int Id { get; set; }
    public int ConteoInventarioId { get; set; }
    public ConteoInventario? ConteoInventario { get; set; }

    public int IngredienteId { get; set; }
    public Ingrediente? Ingrediente { get; set; }

    public double Cantidad { get; set; }
}
