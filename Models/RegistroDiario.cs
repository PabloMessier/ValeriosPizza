namespace ValeriosPizza.Models;

// Resumen del inventario de un día específico
public class RegistroDiario
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int TotalEntradas { get; set; }
    public int TotalGastos { get; set; }
    public int TotalMermas { get; set; }
    public int TotalCortesias { get; set; }
    public int DiscosDisponibles { get; set; }
    public string Notas { get; set; } = string.Empty;
    
    // Propiedad calculada para saber el día de la semana
    public string DiaSemana => Fecha.ToString("dddd", new System.Globalization.CultureInfo("es-ES"));
    public string FechaCorta => Fecha.ToString("dd/MM");
}
