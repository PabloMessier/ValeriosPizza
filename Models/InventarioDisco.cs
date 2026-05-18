namespace ValeriosPizza.Models;

/// <summary>
/// Representa el inventario diario de discos de pizza preparados para hornear.
/// </summary>
public class InventarioDisco
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    
    /// <summary>
    /// Cantidad de discos disponibles al inicio del día
    /// </summary>
    public int CantidadInicial { get; set; }
    
    /// <summary>
    /// Discos preparados durante el día
    /// </summary>
    public int DiscosPreparados { get; set; }
    
    /// <summary>
    /// Discos utilizados/vendidos durante el día
    /// </summary>
    public int DiscosUtilizados { get; set; }
    
    /// <summary>
    /// Discos perdidos por merma (quemados, dañados, etc.)
    /// </summary>
    public int DiscosMerma { get; set; }
    
    /// <summary>
    /// Discos dados como cortesía
    /// </summary>
    public int DiscosCortesia { get; set; }
    
    /// <summary>
    /// Cantidad disponible actual = Inicial + Preparados - Utilizados - Merma - Cortesía
    /// </summary>
    public int CantidadDisponible => CantidadInicial + DiscosPreparados - DiscosUtilizados - DiscosMerma - DiscosCortesia;
    
    public string? Notas { get; set; }

    // Calculadora COP (opcional).
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
}
