using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Data;
using ValeriosPizza.Services;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace ValeriosPizza.ViewModels;

// Clase para mostrar registros en la consulta
public class RegistroConsulta
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Ingrediente { get; set; } = string.Empty;
    public string Cantidad { get; set; } = string.Empty;
    public string Detalles { get; set; } = string.Empty;
    public string ColorTipo { get; set; } = "#666666";
}

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

    // Totales
    [ObservableProperty]
    private int _totalRegistros;

    [ObservableProperty]
    private int _totalEntradas;

    [ObservableProperty]
    private int _totalGastos;

    [ObservableProperty]
    private int _totalMermas;

    [ObservableProperty]
    private int _totalCortesias;

    [ObservableProperty]
    private int _totalMercancias;

    [ObservableProperty]
    private int _totalCajas;

    [ObservableProperty]
    private int _totalDiscos;

    [ObservableProperty]
    private int _totalApertura;

    [ObservableProperty]
    private int _totalCierre;

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
        // Normalizar a fechas sin componente de hora; FechaFinExclusivo se ocupa del límite superior.
        FechaInicio = FechaInicio.Date;
        FechaFin = FechaFin.Date;
        PeriodoTexto = $"Del {FechaInicio:dd/MM/yyyy} al {FechaFin:dd/MM/yyyy}";
        return CargarRegistrosAsync();
    }

    [RelayCommand]
    private Task AplicarFiltrosTipoAsync() => CargarRegistrosAsync();

    /// <summary>
    /// Envía el registro seleccionado a la pantalla "Bodega". Funciona con
    /// cualquier fila de la tabla de resultados; si el tipo no asocia un
    /// ingrediente concreto (p. ej. APERTURA / CIERRE), se guarda igualmente
    /// como referencia con la descripción original.
    /// </summary>
    [RelayCommand]
    private async Task AgregarABodegaAsync(RegistroConsulta? registro)
    {
        if (registro == null) return;

        // Intentar localizar el ingrediente por nombre para mantener un FK
        // blando útil en futuras consultas. No es obligatorio.
        int? ingredienteId = null;
        string unidad = string.Empty;
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var ing = await db.Ingredientes
                .FirstOrDefaultAsync(i => i.Nombre == registro.Ingrediente);
            if (ing != null)
            {
                ingredienteId = ing.Id;
                unidad = ing.UnidadMedida;
            }

            db.BodegaItems.Add(new BodegaItem
            {
                FechaAgregado = DateTime.Now,
                Nombre = registro.Ingrediente,
                Categoria = registro.Tipo switch
                {
                    "MERCANCÍA" => "Mercancía",
                    "ENTRADA" or "GASTO" or "MERMA" or "APERTURA" or "CIERRE" => "Ingrediente",
                    "CORTESÍA" => "Producto",
                    _ => "Otro"
                },
                // Para consulta no tenemos un número suelto: guardamos la
                // cadena formateada como nota, y dejamos Cantidad en 0 para
                // no confundir totales.
                Cantidad = 0,
                UnidadMedida = unidad,
                Notas = $"{registro.Tipo} → {registro.Cantidad}. {registro.Detalles}".Trim('.', ' '),
                Origen = $"Consulta ({registro.Tipo})",
                IngredienteId = ingredienteId
            });
            await db.SaveChangesAsync();
            BodegaNotifier.NotificarCambio();
        }
        catch (System.Exception ex)
        {
            App.GuardarErrorDump(ex, "AgregarABodega (Consulta)");
            System.Windows.MessageBox.Show($"No se pudo agregar a bodega.\n\n{ex.Message}",
                "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task CargarRegistrosAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        Registros.Clear();
        var inicio = FechaInicio.Date;
        var finExclusivo = FechaFinExclusivo;

        // Helper: produce " | Total: $X COP" si la fila usa el desglose monetario.
        static string DesgloseCop(decimal precio, decimal impuesto, decimal retencion, double cantidad)
        {
            if (precio == 0m && impuesto == 0m && retencion == 0m) return string.Empty;
            var total = (decimal)cantidad * precio + impuesto - retencion;
            return $" | Total: ${total:N0} COP";
        }

        int entradas = 0, gastos = 0, mermas = 0, cortesias = 0, mercancias = 0, cajas = 0, discos = 0, apertura = 0, cierre = 0;

        // Cargar Entradas
        if (MostrarEntradas)
        {
            var entradasDb = await db.Entradas
                .Include(e => e.Ingrediente)
                .Where(e => e.Fecha >= inicio && e.Fecha < finExclusivo)
                .OrderByDescending(e => e.Fecha)
                .ToListAsync();

            foreach (var e in entradasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = e.Fecha,
                    Tipo = "ENTRADA",
                    Ingrediente = e.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"+{e.Cantidad:N2} {e.Ingrediente?.UnidadMedida}",
                    Detalles = $"{e.Proveedor} | Costo: ${e.CostoTotal:N2}" + DesgloseCop(e.PrecioUnitario, e.Impuesto, e.Retencion, e.Cantidad),
                    ColorTipo = "#4CAF50"
                });
            }
            entradas = entradasDb.Count;
        }

        // Cargar Gastos
        if (MostrarGastos)
        {
            var gastosDb = await db.Gastos
                .Include(g => g.Ingrediente)
                .Where(g => g.Fecha >= inicio && g.Fecha < finExclusivo)
                .OrderByDescending(g => g.Fecha)
                .ToListAsync();

            foreach (var g in gastosDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = g.Fecha,
                    Tipo = "GASTO",
                    Ingrediente = g.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"-{g.Cantidad:N2} {g.Ingrediente?.UnidadMedida}",
                    Detalles = (g.Descripcion ?? "") + DesgloseCop(g.PrecioUnitario, g.Impuesto, g.Retencion, g.Cantidad),
                    ColorTipo = "#2196F3"
                });
            }
            gastos = gastosDb.Count;
        }

        // Cargar Mermas
        if (MostrarMermas)
        {
            var mermasDb = await db.Mermas
                .Include(m => m.Ingrediente)
                .Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            foreach (var m in mermasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = m.Fecha,
                    Tipo = "MERMA",
                    Ingrediente = m.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"-{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
                    Detalles = (m.Motivo ?? "") + DesgloseCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad),
                    ColorTipo = "#FF9800"
                });
            }
            mermas = mermasDb.Count;
        }

        // Cargar Cortesías
        if (MostrarCortesias)
        {
            var cortesiasDb = await db.Cortesias
                .Include(c => c.Producto)
                .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();

            foreach (var c in cortesiasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = c.Fecha,
                    Tipo = "CORTESÍA",
                    Ingrediente = c.Producto?.Nombre ?? "N/A",
                    Cantidad = $"{c.Cantidad} unidad(es)",
                    Detalles = (c.Motivo ?? "") + DesgloseCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.Cantidad),
                    ColorTipo = "#9C27B0"
                });
            }
            cortesias = cortesiasDb.Count;
        }

        // Cargar Mercancía Recibida
        if (MostrarMercancias)
        {
            var mercanciasDb = await db.MercanciasRecibidas
                .Include(m => m.Ingrediente)
                .Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            foreach (var m in mercanciasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = m.Fecha,
                    Tipo = "MERCANCÍA",
                    Ingrediente = m.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"+{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
                    Detalles = $"{m.Proveedor} | Fact: {m.NumeroFactura}" + DesgloseCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad),
                    ColorTipo = "#00BCD4"
                });
            }
            mercancias = mercanciasDb.Count;
        }

        // Cargar Cajas
        if (MostrarCajas)
        {
            var cajasDb = await db.InventarioCajas
                .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();

            foreach (var c in cajasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = c.Fecha,
                    Tipo = "CAJA",
                    Ingrediente = "Cajas para Pizzas (30 cm)",
                    Cantidad = $"{c.CantidadDisponible} disp.",
                    Detalles = $"Inicial:{c.CantidadInicial} +{c.CajasRecibidas} -{c.CajasUtilizadas} -{c.CajasMerma}m" + DesgloseCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.CantidadInicial + c.CajasRecibidas),
                    ColorTipo = "#795548"
                });
            }
            cajas = cajasDb.Count;
        }

        // Cargar Discos
        if (MostrarDiscos)
        {
            var discosDb = await db.InventarioDiscos
                .Where(d => d.Fecha >= inicio && d.Fecha < finExclusivo)
                .OrderByDescending(d => d.Fecha)
                .ToListAsync();

            foreach (var d in discosDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = d.Fecha,
                    Tipo = "DISCO",
                    Ingrediente = "Discos de Pizza",
                    Cantidad = $"{d.CantidadDisponible} disp.",
                    Detalles = $"Inicial:{d.CantidadInicial} +{d.DiscosPreparados}p -{d.DiscosUtilizados}u -{d.DiscosMerma}m -{d.DiscosCortesia}c" + DesgloseCop(d.PrecioUnitario, d.Impuesto, d.Retencion, d.CantidadInicial + d.DiscosPreparados),
                    ColorTipo = "#E91E63"
                });
            }
            discos = discosDb.Count;
        }

        // Cargar Conteos (Apertura y Cierre)
        if (MostrarApertura || MostrarCierre)
        {
            var conteosDb = await db.ConteosInventario
                .Include(c => c.Lineas).ThenInclude(l => l.Ingrediente)
                .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();

            foreach (var c in conteosDb)
            {
                if (c.Tipo == TipoConteo.Apertura && !MostrarApertura) continue;
                if (c.Tipo == TipoConteo.Cierre && !MostrarCierre) continue;

                foreach (var l in c.Lineas)
                {
                    Registros.Add(new RegistroConsulta
                    {
                        Fecha = c.Fecha,
                        Tipo = c.Tipo == TipoConteo.Apertura ? "APERTURA" : "CIERRE",
                        Ingrediente = l.Ingrediente?.Nombre ?? "N/A",
                        Cantidad = $"{l.Cantidad:N2} {l.Ingrediente?.UnidadMedida}",
                        Detalles = c.Notas ?? "",
                        ColorTipo = c.Tipo == TipoConteo.Apertura ? "#1976D2" : "#7B1FA2"
                    });
                }

                if (c.Tipo == TipoConteo.Apertura) apertura++;
                else cierre++;
            }
        }

        // Ordenar por fecha descendente
        var ordenados = Registros.OrderByDescending(r => r.Fecha).ToList();
        Registros.Clear();
        foreach (var reg in ordenados)
        {
            Registros.Add(reg);
        }

        // Actualizar totales
        TotalEntradas = entradas;
        TotalGastos = gastos;
        TotalMermas = mermas;
        TotalCortesias = cortesias;
        TotalMercancias = mercancias;
        TotalCajas = cajas;
        TotalDiscos = discos;
        TotalApertura = apertura;
        TotalCierre = cierre;
        TotalRegistros = Registros.Count;
    }
}
