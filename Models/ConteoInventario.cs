namespace ValeriosPizza.Models;

/// <summary>
/// Representa un conteo manual de inventario hecho por la dueña, ya sea
/// antes de iniciar el servicio (Apertura) o al cerrar el establecimiento (Cierre).
/// El conteo es un registro estático: solo guarda lo que físicamente se contó.
/// </summary>
public class ConteoInventario
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public TipoConteo Tipo { get; set; }
    public string? Notas { get; set; }

    public List<ConteoInventarioLinea> Lineas { get; set; } = new();
}
