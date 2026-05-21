using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo.Commands;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Sección "Entrada" del Registro Rápido: ingreso de mercancía al
/// inventario (alta de stock). Validación declarativa con DataAnnotations;
/// el guardado pasa por UndoRedo para permitir Ctrl+Z.
/// </summary>
public partial class RegistroRapidoViewModel
{
    // [NotifyDataErrorInfo] dispara la validación con DataAnnotations cada
    // vez que el setter generado se invoca, marca HasErrors y emite el evento
    // ErrorsChanged para que la UI lo refleje (binding con
    // ValidatesOnNotifyDataErrors=True).
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Seleccione un ingrediente.")]
    private Ingrediente? _ingredienteSeleccionado;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    private double _cantidad;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
    private double _costo;

    [ObservableProperty]
    private string _proveedor = string.Empty;

    [ObservableProperty]
    private string _notas = string.Empty;

    public CalculadoraCopVM CalcEntrada { get; } = new();

    partial void OnCantidadChanged(double value) => CalcEntrada.Cantidad = value;

    [RelayCommand]
    private async Task GuardarEntradaAsync()
    {
        // Forzar la validación declarativa de TODAS las propiedades de Entrada
        // antes de tocar la BD. Si algo falla, devolvemos al usuario un
        // mensaje agregado en lugar de tirar una excepción más adelante.
        ValidateAllProperties();
        var props = new[] { nameof(IngredienteSeleccionado), nameof(Cantidad), nameof(Costo) };
        if (HasErroresEnPropiedades(props, out var mensaje))
        {
            MessageBox.Show(mensaje, "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ing = IngredienteSeleccionado!;
        var cmd = new RegistrarEntradaCommand
        {
            IngredienteId = ing.Id,
            IngredienteNombre = ing.Nombre,
            UnidadMedida = ing.UnidadMedida,
            Cantidad = Cantidad,
            CostoTotal = Costo,
            Proveedor = Proveedor,
            Notas = Notas,
            PrecioUnitario = CalcEntrada.PrecioUnitario,
            Impuesto = CalcEntrada.Impuesto,
            Retencion = CalcEntrada.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Entrada registrada correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarEntrada();
        CargarDatos();
    }

    private void LimpiarEntrada()
    {
        IngredienteSeleccionado = null;
        Cantidad = 0;
        Costo = 0;
        Proveedor = string.Empty;
        Notas = string.Empty;
        ClearErrors(nameof(IngredienteSeleccionado));
        ClearErrors(nameof(Cantidad));
        ClearErrors(nameof(Costo));
    }

    private bool TieneDatosEntrada() =>
        IngredienteSeleccionado != null || Cantidad > 0 || Costo > 0
        || !string.IsNullOrWhiteSpace(Proveedor) || !string.IsNullOrWhiteSpace(Notas);

    private (bool ok, string? motivo) GuardarEntradaSilencioso()
    {
        if (IngredienteSeleccionado == null || Cantidad <= 0)
            return (false, "falta ingrediente o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Entradas.Add(new Entrada
        {
            Fecha = System.DateTime.Now, IngredienteId = IngredienteSeleccionado.Id,
            Cantidad = Cantidad, CostoTotal = Costo, Proveedor = Proveedor, Notas = Notas,
            PrecioUnitario = CalcEntrada.PrecioUnitario, Impuesto = CalcEntrada.Impuesto, Retencion = CalcEntrada.Retencion
        });
        var ing = db.Ingredientes.Find(IngredienteSeleccionado.Id);
        if (ing != null) ing.CantidadActual += Cantidad;
        db.SaveChanges();
        LimpiarEntrada();
        return (true, null);
    }
}
