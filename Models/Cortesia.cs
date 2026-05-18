namespace ValeriosPizza.Models;

public class Cortesia
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty; // Cliente frecuente, Promoción, Error, etc.
    public double ValorEstimado { get; set; }
    public string? Notas { get; set; }

    // Calculadora COP (opcional).
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
