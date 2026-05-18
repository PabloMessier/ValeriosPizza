namespace ValeriosPizza.Models;

public class Entrada
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int IngredienteId { get; set; }
    public Ingrediente? Ingrediente { get; set; }
    public double Cantidad { get; set; }
    public double CostoTotal { get; set; }
    public string? Proveedor { get; set; }
    public string? Notas { get; set; }

    // Calculadora COP (opcional). Si se diligencia, sirve como desglose monetario
    // del ingreso: PrecioUnitario × Cantidad + Impuesto − Retención.
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
