namespace ValeriosPizza.Models;

/// <summary>
/// Representa el inventario diario de cajas de pizza (tamaño único de 30 cm).
/// </summary>
public class InventarioCaja
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }

    /// <summary>
    /// Cajas disponibles al inicio del día.
    /// </summary>
    public int CantidadInicial { get; set; }

    /// <summary>
    /// Cajas recibidas durante el día (reposición).
    /// </summary>
    public int CajasRecibidas { get; set; }

    /// <summary>
    /// Cajas utilizadas (vendidas/usadas) durante el día.
    /// </summary>
    public int CajasUtilizadas { get; set; }

    /// <summary>
    /// Cajas perdidas por merma (dañadas, sucias, etc.).
    /// </summary>
    public int CajasMerma { get; set; }

    /// <summary>
    /// Cajas disponibles = Inicial + Recibidas - Utilizadas - Merma.
    /// </summary>
    public int CantidadDisponible => CantidadInicial + CajasRecibidas - CajasUtilizadas - CajasMerma;

    public string? Notas { get; set; }

    // Calculadora COP (opcional).
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
