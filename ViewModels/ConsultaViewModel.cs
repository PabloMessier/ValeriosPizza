using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Services;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Fila proyectada para el DataGrid de Consulta. Es un POCO de
/// presentación: no se persiste y no implementa <c>INotifyPropertyChanged</c>
/// porque las filas no cambian una vez generadas.
/// </summary>
public class RegistroConsulta
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Ingrediente { get; set; } = string.Empty;
    public string Cantidad { get; set; } = string.Empty;
    public string Detalles { get; set; } = string.Empty;
    public string ColorTipo { get; set; } = "#666666";
}

/// <summary>
/// VM del módulo "Consulta": búsqueda histórica con filtros por rango
/// de fechas y por tipo de movimiento. La lectura de datos vive en
/// <c>Consulta/ConsultaViewModel.Carga.cs</c> y la integración con la
/// pantalla Bodega en <c>Consulta/ConsultaViewModel.Bodega.cs</c>.
/// Este archivo concentra el estado y los presets de fecha.
/// </summary>
public partial class ConsultaViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;

    [ObservableProperty]
    private ObservableCollection<RegistroConsulta> _registros = new();

    /// <summary>Vista paginada (10/20/30… por página) sobre <see cref="Registros"/>.
    /// Reduce el tiempo de render del DataGrid cuando el rango de fechas
    /// produce miles de filas.</summary>
    public PagedCollectionView<RegistroConsulta> RegistrosPaged { get; }

    partial void OnRegistrosChanged(ObservableCollection<RegistroConsulta> value)
        => RegistrosPaged?.CambiarOrigen(value);

    [ObservableProperty]
    private DateTime _fechaInicio = DateTime.Today;

    [ObservableProperty]
    private DateTime _fechaFin = DateTime.Today;

    [ObservableProperty]
    private string _filtroActivo = "Hoy";

    [ObservableProperty]
    private string _periodoTexto = string.Empty;

    [ObservableProperty]
    private bool _mostrarEntradas = true;

    [ObservableProperty]
    private bool _mostrarGastos = true;

    [ObservableProperty]
    private bool _mostrarMermas = true;

    [ObservableProperty]
    private bool _mostrarCortesias = true;

    [ObservableProperty]
    private bool _mostrarMercancias = true;

    [ObservableProperty]
    private bool _mostrarCajas = true;

    [ObservableProperty]
    private bool _mostrarDiscos = true;

    [ObservableProperty]
    private bool _mostrarApertura = true;

    [ObservableProperty]
    private bool _mostrarCierre = true;

    // Totales por tipo + total general.
    [ObservableProperty] private int _totalRegistros;
    [ObservableProperty] private int _totalEntradas;
    [ObservableProperty] private int _totalGastos;
    [ObservableProperty] private int _totalMermas;
    [ObservableProperty] private int _totalCortesias;
    [ObservableProperty] private int _totalMercancias;
    [ObservableProperty] private int _totalCajas;
    [ObservableProperty] private int _totalDiscos;
    [ObservableProperty] private int _totalApertura;
    [ObservableProperty] private int _totalCierre;

    public ConsultaViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        RegistrosPaged = new PagedCollectionView<RegistroConsulta>(Registros);
        // Carga inicial fire-and-forget; equivale al antiguo FiltrarHoy().
        _ = FiltrarHoyAsync();
    }

    /// <summary>
    /// Fin exclusivo del rango (medianoche del día siguiente al último día
    /// inclusivo seleccionado). Se usa con la comparación &lt; para evitar
    /// problemas de precisión con DateTime al final del día.
    /// </summary>
    private DateTime FechaFinExclusivo => FechaFin.Date.AddDays(1);

    [RelayCommand]
    private Task FiltrarHoyAsync()
    {
        FiltroActivo = "Hoy";
        FechaInicio = DateTime.Today;
        FechaFin = DateTime.Today;
        PeriodoTexto = $"Hoy - {DateTime.Today:dddd, dd MMMM yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task FiltrarAyerAsync()
    {
        FiltroActivo = "Ayer";
        FechaInicio = DateTime.Today.AddDays(-1);
        FechaFin = FechaInicio;
        PeriodoTexto = $"Ayer - {FechaInicio:dddd, dd MMMM yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task FiltrarSemanaAsync()
    {
        FiltroActivo = "Semana";
        var diasDesdeInicioSemana = ((int)DateTime.Today.DayOfWeek - 1 + 7) % 7;
        FechaInicio = DateTime.Today.AddDays(-diasDesdeInicioSemana);
        FechaFin = FechaInicio.AddDays(6);
        PeriodoTexto = $"Semana del {FechaInicio:dd/MM} al {FechaFin:dd/MM/yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task FiltrarMesAsync()
    {
        FiltroActivo = "Mes";
        FechaInicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        FechaFin = FechaInicio.AddMonths(1).AddDays(-1);
        PeriodoTexto = $"Mes de {DateTime.Today:MMMM yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task FiltrarMesAnteriorAsync()
    {
        FiltroActivo = "MesAnterior";
        var mesAnterior = DateTime.Today.AddMonths(-1);
        FechaInicio = new DateTime(mesAnterior.Year, mesAnterior.Month, 1);
        FechaFin = FechaInicio.AddMonths(1).AddDays(-1);
        PeriodoTexto = $"Mes de {mesAnterior:MMMM yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task BuscarPorFechasAsync()
    {
        FiltroActivo = "Personalizado";
        // Normalizar a fechas sin componente de hora; FechaFinExclusivo
        // se ocupa del límite superior.
        FechaInicio = FechaInicio.Date;
        FechaFin = FechaFin.Date;
        PeriodoTexto = $"Del {FechaInicio:dd/MM/yyyy} al {FechaFin:dd/MM/yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task AplicarFiltrosTipoAsync() => CargarRegistrosAsync();
}
