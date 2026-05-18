namespace ValeriosPizza.Models;

public class Merma
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int? IngredienteId { get; set; }
    public Ingrediente? Ingrediente { get; set; }
    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public double Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty; // Quemado, Expirado, Dañado, etc.
    public double CostoEstimado { get; set; }
    public string? Notas { get; set; }

    // Calculadora COP (opcional).
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
