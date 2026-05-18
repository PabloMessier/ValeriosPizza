using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Helper de UI (no se persiste en BD) para calcular en pesos colombianos
/// el valor monetario de una sección: subtotal = cantidad × precio unitario,
/// con campos opcionales de impuesto y retención (montos fijos en COP).
///
/// Total = Subtotal + Impuesto − Retención.
/// </summary>
public partial class CalculadoraCopVM : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(Total))]
    private double _cantidad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(Total))]
    private decimal _precioUnitario;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    private decimal _impuesto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    private decimal _retencion;

    /// <summary>Subtotal = Cantidad × Precio Unitario.</summary>
    public decimal Subtotal => (decimal)Cantidad * PrecioUnitario;

    /// <summary>Total = Subtotal + Impuesto − Retención.</summary>
    public decimal Total => Subtotal + Impuesto - Retencion;

    [RelayCommand]
    public void Limpiar()
    {
        PrecioUnitario = 0m;
        Impuesto = 0m;
        Retencion = 0m;
    }
}
