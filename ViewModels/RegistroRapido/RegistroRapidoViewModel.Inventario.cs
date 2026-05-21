using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo.Commands;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Secciones de inventario de insumos físicos: Discos (bases de pizza) y
/// Cajas. Mismo patrón: inicial + recibidos/preparados − utilizados − merma,
/// con un total calculado expuesto a la UI. Guardado via UndoRedo.
/// </summary>
public partial class RegistroRapidoViewModel
{
    // ─────────────────────────────── DISCOS ───────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _discosIniciales;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _discosPreparados;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _discosUtilizados;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _discosMerma;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _discosCortesia;

    public int DiscosDisponibles =>
        DiscosIniciales + DiscosPreparados - DiscosUtilizados - DiscosMerma - DiscosCortesia;

    public CalculadoraCopVM CalcDiscos { get; } = new();

    partial void OnDiscosInicialesChanged(int value)  { OnPropertyChanged(nameof(DiscosDisponibles)); CalcDiscos.Cantidad = DiscosIniciales + DiscosPreparados; }
    partial void OnDiscosPreparadosChanged(int value) { OnPropertyChanged(nameof(DiscosDisponibles)); CalcDiscos.Cantidad = DiscosIniciales + DiscosPreparados; }
    partial void OnDiscosUtilizadosChanged(int value) => OnPropertyChanged(nameof(DiscosDisponibles));
    partial void OnDiscosMermaChanged(int value)      => OnPropertyChanged(nameof(DiscosDisponibles));
    partial void OnDiscosCortesiaChanged(int value)   => OnPropertyChanged(nameof(DiscosDisponibles));

    [RelayCommand]
    private async Task GuardarDiscosAsync()
    {
        var cmd = new RegistrarMovimientoDiscosCommand
        {
            CantidadInicial = DiscosIniciales,
            DiscosPreparados = DiscosPreparados,
            DiscosUtilizados = DiscosUtilizados,
            DiscosMerma = DiscosMerma,
            DiscosCortesia = DiscosCortesia,
            PrecioUnitario = CalcDiscos.PrecioUnitario,
            Impuesto = CalcDiscos.Impuesto,
            Retencion = CalcDiscos.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Registro de discos guardado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarDiscos();
    }

    private void LimpiarDiscos()
    {
        DiscosIniciales = DiscosPreparados = DiscosUtilizados = DiscosMerma = DiscosCortesia = 0;
    }

    private bool TieneDatosDiscos() =>
        DiscosIniciales > 0 || DiscosPreparados > 0 || DiscosUtilizados > 0
        || DiscosMerma > 0 || DiscosCortesia > 0;

    private (bool ok, string? motivo) GuardarDiscosSilencioso()
    {
        using var db = _dbFactory.CreateDbContext();
        db.InventarioDiscos.Add(new InventarioDisco
        {
            Fecha = System.DateTime.Now,
            CantidadInicial = DiscosIniciales, DiscosPreparados = DiscosPreparados,
            DiscosUtilizados = DiscosUtilizados, DiscosMerma = DiscosMerma,
            DiscosCortesia = DiscosCortesia,
            PrecioUnitario = CalcDiscos.PrecioUnitario, Impuesto = CalcDiscos.Impuesto, Retencion = CalcDiscos.Retencion
        });
        db.SaveChanges();
        LimpiarDiscos();
        return (true, null);
    }

    // ──────────────────────────────── CAJAS ────────────────────────────────
    // Tamaño único (30 cm).

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _cajaInicial;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _cajaRecibidas;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _cajaUtilizadas;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "No puede ser negativo.")]
    private int _cajaMerma;

    public int CajaDisponible => CajaInicial + CajaRecibidas - CajaUtilizadas - CajaMerma;

    public CalculadoraCopVM CalcCajas { get; } = new();

    partial void OnCajaInicialChanged(int value)   { OnPropertyChanged(nameof(CajaDisponible)); CalcCajas.Cantidad = CajaInicial + CajaRecibidas; }
    partial void OnCajaRecibidasChanged(int value) { OnPropertyChanged(nameof(CajaDisponible)); CalcCajas.Cantidad = CajaInicial + CajaRecibidas; }
    partial void OnCajaUtilizadasChanged(int value) => OnPropertyChanged(nameof(CajaDisponible));
    partial void OnCajaMermaChanged(int value)      => OnPropertyChanged(nameof(CajaDisponible));

    [RelayCommand]
    private async Task GuardarCajasAsync()
    {
        var cmd = new RegistrarMovimientoCajasCommand
        {
            CantidadInicial = CajaInicial,
            CajasRecibidas = CajaRecibidas,
            CajasUtilizadas = CajaUtilizadas,
            CajasMerma = CajaMerma,
            PrecioUnitario = CalcCajas.PrecioUnitario,
            Impuesto = CalcCajas.Impuesto,
            Retencion = CalcCajas.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Registro de cajas guardado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarCajas();
    }

    private void LimpiarCajas() { CajaInicial = CajaRecibidas = CajaUtilizadas = CajaMerma = 0; }

    private bool TieneDatosCajas() =>
        CajaInicial > 0 || CajaRecibidas > 0 || CajaUtilizadas > 0 || CajaMerma > 0;

    private (bool ok, string? motivo) GuardarCajasSilencioso()
    {
        using var db = _dbFactory.CreateDbContext();
        db.InventarioCajas.Add(new InventarioCaja
        {
            Fecha = System.DateTime.Now,
            CantidadInicial = CajaInicial, CajasRecibidas = CajaRecibidas,
            CajasUtilizadas = CajaUtilizadas, CajasMerma = CajaMerma,
            PrecioUnitario = CalcCajas.PrecioUnitario, Impuesto = CalcCajas.Impuesto, Retencion = CalcCajas.Retencion
        });
        db.SaveChanges();
        LimpiarCajas();
        return (true, null);
    }
}
