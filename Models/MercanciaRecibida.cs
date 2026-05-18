namespace ValeriosPizza.Models;

// Registro de mercancía nueva que llega de proveedores
public class MercanciaRecibida
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int IngredienteId { get; set; }
    public Ingrediente? Ingrediente { get; set; }
    public double Cantidad { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string NumeroFactura { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;

    /// <summary>
    /// Ruta absoluta del archivo (PDF / imagen) de la factura digital, si la
    /// usuaria adjuntó uno. El archivo se copia a la carpeta de facturas de la
    /// app al guardar la mercancía. Null = sin adjunto.
    /// </summary>
    public string? RutaFactura { get; set; }

    // Calculadora COP (opcional).
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
