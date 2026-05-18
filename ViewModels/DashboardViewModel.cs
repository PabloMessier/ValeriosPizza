using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using System.Collections.ObjectModel;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Punto de la mini-tendencia de los últimos 7 días que se dibuja en el
/// Dashboard. La barra se renderiza con un <c>Rectangle</c> cuyo <c>Width</c>
/// se enlaza a <see cref="AnchoRelativo"/> (0..200 px). No requiere ninguna
/// librería de gráficos externa.
/// </summary>
public class TendenciaDia
{
    public string Etiqueta { get; init; } = string.Empty;
    public int Movimientos { get; init; }
    public double AnchoRelativo { get; init; }
    public bool EsHoy { get; init; }
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    [ObservableProperty]
    private string _fechaHoy = DateTime.Now.ToString("D");

    [ObservableProperty]
    private int _ingresosHoy = 0;

    [ObservableProperty]
    private int _gastosHoy = 0;

    [ObservableProperty]
    private int _mermasHoy = 0;

    [ObservableProperty]
    private int _cortesiasHoy = 0;

    [ObservableProperty]
    private int _discosDisponibles = 0;

    [ObservableProperty]
    private int _cajasDisponibles = 0;

    [ObservableProperty]
    private ObservableCollection<Ingrediente> _alertasStock = new();

    /// <summary>Tendencia diaria (últimos 7 días) de movimientos totales.</summary>
    [ObservableProperty]
    private ObservableCollection<TendenciaDia> _tendencia7Dias = new();

    public bool SinAlertas => AlertasStock.Count == 0;

    public DashboardViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        // Fire-and-forget: la carga inicial se hace en segundo plano para no
        // bloquear la creación del VM. Cualquier excepción se captura y se
        // vuelca al log para no perderla.
        _ = CargarDatosHoyAsync();
    }

    private async Task CargarDatosHoyAsync()
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var hoy = DateTime.Today;

            IngresosHoy = await db.Entradas.CountAsync(e => e.Fecha.Date == hoy);
            GastosHoy = await db.Gastos.CountAsync(g => g.Fecha.Date == hoy);
            MermasHoy = await db.Mermas.CountAsync(m => m.Fecha.Date == hoy);
            CortesiasHoy = await db.Cortesias.CountAsync(c => c.Fecha.Date == hoy);

            // Discos disponibles: balance acumulado de TODOS los registros de discos
            // (mismo modelo que Cajas). Cada nuevo guardado SUMA su CantidadInicial y
            // DiscosPreparados al stock y RESTA Utilizados/Merma/Cortesía. Esto permite
            // que las "DISCOS INICIALES" de un guardado posterior se acumulen, y que
            // los Preparados/Utilizados/Merma/Cortesía se reflejen en tiempo real.
            DiscosDisponibles = await db.InventarioDiscos
                .SumAsync(d => (int?)(d.CantidadInicial + d.DiscosPreparados
                                  - d.DiscosUtilizados - d.DiscosMerma - d.DiscosCortesia)) ?? 0;

            // Cajas disponibles: balance acumulado de todos los registros de cajas.
            // Cada fila aporta CantidadInicial + Recibidas - Utilizadas - Merma, lo que
            // refleja el stock real al momento (incluye reposiciones y mermas históricas).
            CajasDisponibles = await db.InventarioCajas
                .SumAsync(c => (int?)(c.CantidadInicial + c.CajasRecibidas - c.CajasUtilizadas - c.CajasMerma)) ?? 0;

            // Cargar alertas de stock bajo
            var ingredientesBajos = await db.Ingredientes
                .Where(i => i.CantidadActual <= i.CantidadMinima && i.CantidadMinima > 0)
                .OrderBy(i => i.CantidadActual)
                .ToListAsync();
            AlertasStock.Clear();
            foreach (var ing in ingredientesBajos)
            {
                AlertasStock.Add(ing);
            }

            OnPropertyChanged(nameof(SinAlertas));

            await CargarTendenciaAsync(db);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "DashboardViewModel.CargarDatosHoyAsync");
        }
    }

    /// <summary>
    /// Calcula los movimientos totales (entradas+gastos+mermas+cortesías+mercancías)
    /// de cada uno de los últimos 7 días y normaliza el ancho de barra para
    /// que el día con más movimientos llene 200 px.
    /// </summary>
    private async Task CargarTendenciaAsync(PizzeriaDbContext db)
    {
        const double AnchoMaximoPx = 200.0;
        var hoy = DateTime.Today;
        var inicio = hoy.AddDays(-6);
        var finExclusivo = hoy.AddDays(1);

        // Una sola consulta por tabla, agrupando en memoria por día.
        var entradas = await db.Entradas.Where(e => e.Fecha >= inicio && e.Fecha < finExclusivo)
            .Select(e => e.Fecha).ToListAsync();
        var gastos = await db.Gastos.Where(g => g.Fecha >= inicio && g.Fecha < finExclusivo)
            .Select(g => g.Fecha).ToListAsync();
        var mermas = await db.Mermas.Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
            .Select(m => m.Fecha).ToListAsync();
        var cortesias = await db.Cortesias.Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
            .Select(c => c.Fecha).ToListAsync();
        var mercancias = await db.MercanciasRecibidas.Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
            .Select(m => m.Fecha).ToListAsync();

        int Conteo(List<DateTime> origen, DateTime fecha) => origen.Count(f => f.Date == fecha);

        var totalesPorDia = new int[7];
        for (int i = 0; i < 7; i++)
        {
            var fecha = inicio.AddDays(i);
            totalesPorDia[i] = Conteo(entradas, fecha) + Conteo(gastos, fecha)
                + Conteo(mermas, fecha) + Conteo(cortesias, fecha) + Conteo(mercancias, fecha);
        }

        var max = totalesPorDia.Max();
        Tendencia7Dias.Clear();
        string[] nombresDias = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
        for (int i = 0; i < 7; i++)
        {
            var fecha = inicio.AddDays(i);
            var total = totalesPorDia[i];
            Tendencia7Dias.Add(new TendenciaDia
            {
                Etiqueta = $"{nombresDias[(int)fecha.DayOfWeek]} {fecha:dd/MM}",
                Movimientos = total,
                AnchoRelativo = max > 0 ? (total / (double)max) * AnchoMaximoPx : 0,
                EsHoy = fecha == hoy
            });
        }
    }

    [RelayCommand]
    public Task ActualizarDatosAsync() => CargarDatosHoyAsync();
}
