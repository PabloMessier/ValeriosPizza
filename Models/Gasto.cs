namespace ValeriosPizza.Models;

// Representa el uso/consumo de ingredientes en la producción
public class Gasto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int IngredienteId { get; set; }
    public Ingrediente? Ingrediente { get; set; }
    public double Cantidad { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    // Calculadora COP (opcional).
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
