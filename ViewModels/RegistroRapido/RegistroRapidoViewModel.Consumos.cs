using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo.Commands;
using ValeriosPizza.Windows;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Secciones de consumo: Gasto, Merma y Cortesía. Las tres comparten el
/// patrón ingrediente/producto + cantidad + motivo y se persisten via
/// UndoRedo. Agrupadas en un solo archivo para que el flujo común sea
/// fácil de comparar.
/// </summary>
public partial class RegistroRapidoViewModel
{
    // ─────────────────────────────── GASTO ───────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Seleccione un ingrediente.")]
    private Ingrediente? _gastoIngredienteSeleccionado;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    private double _gastoCantidad;

    [ObservableProperty]
    private string _gastoDescripcion = string.Empty;

    public CalculadoraCopVM CalcGasto { get; } = new();

    partial void OnGastoCantidadChanged(double value) => CalcGasto.Cantidad = value;

    [RelayCommand]
    private async Task GuardarGastoAsync()
    {
        if (GastoIngredienteSeleccionado == null || GastoCantidad <= 0)
        {
            MessageBox.Show("Seleccione un ingrediente y cantidad válida", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ing = GastoIngredienteSeleccionado;
        var cmd = new RegistrarGastoCommand
        {
            IngredienteId = ing.Id, IngredienteNombre = ing.Nombre, UnidadMedida = ing.UnidadMedida,
            Cantidad = GastoCantidad, DescripcionGasto = GastoDescripcion,
            PrecioUnitario = CalcGasto.PrecioUnitario, Impuesto = CalcGasto.Impuesto, Retencion = CalcGasto.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Gasto registrado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarGasto();
        CargarDatos();
    }

    private void LimpiarGasto()
    {
        GastoIngredienteSeleccionado = null;
        GastoCantidad = 0;
        GastoDescripcion = string.Empty;
    }

    private bool TieneDatosGasto() =>
        GastoIngredienteSeleccionado != null || GastoCantidad > 0
        || !string.IsNullOrWhiteSpace(GastoDescripcion);

    private (bool ok, string? motivo) GuardarGastoSilencioso()
    {
        if (GastoIngredienteSeleccionado == null || GastoCantidad <= 0)
            return (false, "falta ingrediente o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Gastos.Add(new Gasto
        {
            Fecha = System.DateTime.Now, IngredienteId = GastoIngredienteSeleccionado.Id,
            Cantidad = GastoCantidad, Descripcion = GastoDescripcion,
            PrecioUnitario = CalcGasto.PrecioUnitario, Impuesto = CalcGasto.Impuesto, Retencion = CalcGasto.Retencion
        });
        var ing = db.Ingredientes.Find(GastoIngredienteSeleccionado.Id);
        if (ing != null)
        {
            ing.CantidadActual -= GastoCantidad;
            if (ing.CantidadActual < 0) ing.CantidadActual = 0;
        }
        db.SaveChanges();
        LimpiarGasto();
        return (true, null);
    }

    // ─────────────────────────────── MERMA ───────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Seleccione un ingrediente.")]
    private Ingrediente? _mermaIngredienteSeleccionado;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    private double _mermaCantidad;

    [ObservableProperty]
    private string _mermaMotivo = string.Empty;

    public CalculadoraCopVM CalcMerma { get; } = new();

    partial void OnMermaCantidadChanged(double value) => CalcMerma.Cantidad = value;

    [RelayCommand]
    private async Task GuardarMermaAsync()
    {
        if (MermaIngredienteSeleccionado == null || MermaCantidad <= 0)
        {
            MessageBox.Show("Seleccione un ingrediente y cantidad válida", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ing = MermaIngredienteSeleccionado;
        var cmd = new RegistrarMermaCommand
        {
            IngredienteId = ing.Id, IngredienteNombre = ing.Nombre, UnidadMedida = ing.UnidadMedida,
            Cantidad = MermaCantidad, Motivo = MermaMotivo,
            PrecioUnitario = CalcMerma.PrecioUnitario, Impuesto = CalcMerma.Impuesto, Retencion = CalcMerma.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Merma registrada correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarMerma();
        CargarDatos();
    }

    private void LimpiarMerma()
    {
        MermaIngredienteSeleccionado = null;
        MermaCantidad = 0;
        MermaMotivo = string.Empty;
    }

    private bool TieneDatosMerma() =>
        MermaIngredienteSeleccionado != null || MermaCantidad > 0
        || !string.IsNullOrWhiteSpace(MermaMotivo);

    private (bool ok, string? motivo) GuardarMermaSilencioso()
    {
        if (MermaIngredienteSeleccionado == null || MermaCantidad <= 0)
            return (false, "falta ingrediente o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Mermas.Add(new Merma
        {
            Fecha = System.DateTime.Now, IngredienteId = MermaIngredienteSeleccionado.Id,
            Cantidad = MermaCantidad, Motivo = MermaMotivo,
            PrecioUnitario = CalcMerma.PrecioUnitario, Impuesto = CalcMerma.Impuesto, Retencion = CalcMerma.Retencion
        });
        var ing = db.Ingredientes.Find(MermaIngredienteSeleccionado.Id);
        if (ing != null)
        {
            ing.CantidadActual -= MermaCantidad;
            if (ing.CantidadActual < 0) ing.CantidadActual = 0;
        }
        db.SaveChanges();
        LimpiarMerma();
        return (true, null);
    }

    // ────────────────────────────── CORTESÍA ──────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Seleccione un producto.")]
    private Producto? _cortesiaProductoSeleccionado;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
    private int _cortesiaCantidad;

    [ObservableProperty]
    private string _cortesiaMotivo = string.Empty;

    public CalculadoraCopVM CalcCortesia { get; } = new();

    partial void OnCortesiaCantidadChanged(int value) => CalcCortesia.Cantidad = value;

    /// <summary>
    /// Abre el diálogo de creación de producto desde el formulario de
    /// Cortesía y, si la dueña confirma, deja el producto recién creado
    /// preseleccionado en el combo para que pueda guardar la cortesía sin
    /// pasos adicionales.
    /// </summary>
    [RelayCommand]
    private void AgregarProductoCortesia()
    {
        var dialog = ProductoDialog.ParaCrear(_dbFactory);
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true && dialog.ProductoResultante != null)
        {
            var idNuevo = dialog.ProductoResultante.Id;
            CargarDatos();
            CortesiaProductoSeleccionado = Productos.FirstOrDefault(p => p.Id == idNuevo);
            ProductosNotifier.NotificarCambio();
        }
    }

    [RelayCommand]
    private async Task GuardarCortesiaAsync()
    {
        if (CortesiaProductoSeleccionado == null || CortesiaCantidad <= 0)
        {
            MessageBox.Show("Seleccione un producto y cantidad válida", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var prod = CortesiaProductoSeleccionado;
        var cmd = new RegistrarCortesiaCommand
        {
            ProductoId = prod.Id, ProductoNombre = prod.Nombre,
            Cantidad = CortesiaCantidad, Motivo = CortesiaMotivo,
            PrecioUnitario = CalcCortesia.PrecioUnitario, Impuesto = CalcCortesia.Impuesto, Retencion = CalcCortesia.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Cortesía registrada correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarCortesia();
    }

    private void LimpiarCortesia()
    {
        CortesiaProductoSeleccionado = null;
        CortesiaCantidad = 0;
        CortesiaMotivo = string.Empty;
    }

    private bool TieneDatosCortesia() =>
        CortesiaProductoSeleccionado != null || CortesiaCantidad > 0
        || !string.IsNullOrWhiteSpace(CortesiaMotivo);

    private (bool ok, string? motivo) GuardarCortesiaSilencioso()
    {
        if (CortesiaProductoSeleccionado == null || CortesiaCantidad <= 0)
            return (false, "falta producto o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Cortesias.Add(new Cortesia
        {
            Fecha = System.DateTime.Now, ProductoId = CortesiaProductoSeleccionado.Id,
            Cantidad = CortesiaCantidad, Motivo = CortesiaMotivo,
            PrecioUnitario = CalcCortesia.PrecioUnitario, Impuesto = CalcCortesia.Impuesto, Retencion = CalcCortesia.Retencion
        });
        db.SaveChanges();
        LimpiarCortesia();
        return (true, null);
    }
}
