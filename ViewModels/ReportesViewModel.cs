using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Data;
using ValeriosPizza.Services;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace ValeriosPizza.ViewModels;

public class ResumenIngrediente
{
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public double TotalEntradas { get; set; }
    public double TotalGastos { get; set; }
    public double TotalMermas { get; set; }
    public double TotalMercancia { get; set; }
    public double Balance => TotalEntradas + TotalMercancia - TotalGastos - TotalMermas;
    public string BalanceTexto => Balance >= 0 ? $"+{Balance:N2}" : $"{Balance:N2}";
    public string ColorBalance => Balance >= 0 ? "#4CAF50" : "#F44336";
}

public class ResumenProducto
{
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int TotalCortesias { get; set; }
}

public class ResumenDiario
{
    public DateTime Fecha { get; set; }
    public string FechaTexto => Fecha.ToString("dd/MM");
    public int Entradas { get; set; }
    public int Gastos { get; set; }
    public int Mermas { get; set; }
    public int Cortesias { get; set; }
    public int Mercancias { get; set; }
    public int Total => Entradas + Gastos + Mermas + Cortesias + Mercancias;
}

public class ResumenCajas
{
    public int Recibidas { get; set; }
    public int Utilizadas { get; set; }
    public int Merma { get; set; }
    public int Disponible { get; set; }
}

public class ResumenConteo
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CantidadLineas { get; set; }
    public string FechaTexto => Fecha.ToString("dd/MM HH:mm");
}

public partial class ReportesViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;

    [ObservableProperty]
    private ObservableCollection<ResumenIngrediente> _resumenIngredientes = new();

    [ObservableProperty]
    private ObservableCollection<ResumenProducto> _resumenProductos = new();

    [ObservableProperty]
    private ObservableCollection<ResumenDiario> _resumenDiario = new();

    [ObservableProperty]
    private ObservableCollection<ResumenCajas> _resumenCajas = new();

    [ObservableProperty]
    private ObservableCollection<ResumenConteo> _resumenConteos = new();

    /// <summary>Vistas paginadas sobre las colecciones de resumen.
    /// Los DataGrid del cardo Detalle se enlazan a <c>XxxPaged.Items</c>.</summary>
    public PagedCollectionView<ResumenIngrediente> ResumenIngredientesPaged { get; }
    public PagedCollectionView<ResumenProducto>    ResumenProductosPaged    { get; }
    public PagedCollectionView<ResumenDiario>      ResumenDiarioPaged       { get; }
    public PagedCollectionView<ResumenCajas>       ResumenCajasPaged        { get; }
    public PagedCollectionView<ResumenConteo>      ResumenConteosPaged      { get; }

    partial void OnResumenIngredientesChanged(ObservableCollection<ResumenIngrediente> value)
        => ResumenIngredientesPaged?.CambiarOrigen(value);
    partial void OnResumenProductosChanged(ObservableCollection<ResumenProducto> value)
        => ResumenProductosPaged?.CambiarOrigen(value);
    partial void OnResumenDiarioChanged(ObservableCollection<ResumenDiario> value)
        => ResumenDiarioPaged?.CambiarOrigen(value);
    partial void OnResumenCajasChanged(ObservableCollection<ResumenCajas> value)
        => ResumenCajasPaged?.CambiarOrigen(value);
    partial void OnResumenConteosChanged(ObservableCollection<ResumenConteo> value)
        => ResumenConteosPaged?.CambiarOrigen(value);

    [ObservableProperty]
    private DateTime _fechaInicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime _fechaFin = DateTime.Today;

    [ObservableProperty]
    private string _periodoTexto = string.Empty;

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
    private int _totalConteos;

    [ObservableProperty]
    private int _totalMovimientos;

    [ObservableProperty]
    private int _discosPreparadosPeriodo;

    [ObservableProperty]
    private int _discosUtilizadosPeriodo;

    [ObservableProperty]
    private int _discosMermaPeriodo;

    [ObservableProperty]
    private int _discosCortesiaPeriodo;

    [ObservableProperty]
    private string _mensajeExportacion = string.Empty;

    // ─── BORRADO MASIVO DE BASE DE DATOS ────────────────────────────────────
    public ObservableCollection<string> AlcancesBorrado { get; } = new()
    {
        "Hoy",
        "Esta semana",
        "Este mes",
        "Este año",
        "Rango personalizado (fecha y hora)",
        "Todo (catálogo y transacciones)"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarRangoBorrado))]
    private string _alcanceBorradoSeleccionado = "Hoy";

    [ObservableProperty]
    private DateTime _borradoDesde = DateTime.Today;

    [ObservableProperty]
    private DateTime _borradoHasta = DateTime.Today.AddDays(1).AddSeconds(-1);

    [ObservableProperty]
    private string _mensajeBorrado = string.Empty;

    public bool MostrarRangoBorrado =>
        AlcanceBorradoSeleccionado == "Rango personalizado (fecha y hora)";

    public ReportesViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        ResumenIngredientesPaged = new PagedCollectionView<ResumenIngrediente>(ResumenIngredientes);
        ResumenProductosPaged    = new PagedCollectionView<ResumenProducto>(ResumenProductos);
        ResumenDiarioPaged       = new PagedCollectionView<ResumenDiario>(ResumenDiario);
        ResumenCajasPaged        = new PagedCollectionView<ResumenCajas>(ResumenCajas);
        ResumenConteosPaged      = new PagedCollectionView<ResumenConteo>(ResumenConteos);
        // Carga inicial fire-and-forget para no bloquear la creación del VM.
        _ = GenerarReporteAsync();
    }

    [RelayCommand]
    private Task FiltrarEsteMesAsync()
    {
        FechaInicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        FechaFin = DateTime.Today;
        return GenerarReporteAsync();
    }

    [RelayCommand]
    private Task FiltrarMesAnteriorAsync()
    {
        var mesAnterior = DateTime.Today.AddMonths(-1);
        FechaInicio = new DateTime(mesAnterior.Year, mesAnterior.Month, 1);
        FechaFin = FechaInicio.AddMonths(1).AddDays(-1);
        return GenerarReporteAsync();
    }

    [RelayCommand]
    private Task FiltrarUltimos30DiasAsync()
    {
        FechaInicio = DateTime.Today.AddDays(-30);
        FechaFin = DateTime.Today;
        return GenerarReporteAsync();
    }

    [RelayCommand]
    private Task FiltrarUltimos7DiasAsync()
    {
        FechaInicio = DateTime.Today.AddDays(-7);
        FechaFin = DateTime.Today;
        return GenerarReporteAsync();
    }

    [RelayCommand]
    private Task BuscarPorFechasAsync() => GenerarReporteAsync();

    /// <summary>
    /// Fin exclusivo (medianoche del día siguiente al último día inclusivo).
    /// </summary>
    private DateTime FechaFinExclusivo => FechaFin.Date.AddDays(1);

    [RelayCommand]
    private async Task GenerarReporteAsync()
    {
        await using var db = _dbFactory.CreateDbContext();

        var inicio = FechaInicio.Date;
        var finExclusivo = FechaFinExclusivo;
        PeriodoTexto = $"Período: {FechaInicio:dd/MM/yyyy} - {FechaFin:dd/MM/yyyy}";

        TotalEntradas = await db.Entradas.CountAsync(e => e.Fecha >= inicio && e.Fecha < finExclusivo);
        TotalGastos = await db.Gastos.CountAsync(g => g.Fecha >= inicio && g.Fecha < finExclusivo);
        TotalMermas = await db.Mermas.CountAsync(m => m.Fecha >= inicio && m.Fecha < finExclusivo);
        TotalCortesias = await db.Cortesias.CountAsync(c => c.Fecha >= inicio && c.Fecha < finExclusivo);
        TotalMercancias = await db.MercanciasRecibidas.CountAsync(m => m.Fecha >= inicio && m.Fecha < finExclusivo);
        TotalCajas = await db.InventarioCajas.CountAsync(c => c.Fecha >= inicio && c.Fecha < finExclusivo);
        TotalConteos = await db.ConteosInventario.CountAsync(c => c.Fecha >= inicio && c.Fecha < finExclusivo);
        TotalMovimientos = TotalEntradas + TotalGastos + TotalMermas + TotalCortesias + TotalMercancias + TotalCajas + TotalConteos;

        var discos = await db.InventarioDiscos
            .Where(d => d.Fecha >= inicio && d.Fecha < finExclusivo).ToListAsync();
        DiscosPreparadosPeriodo = discos.Sum(d => d.DiscosPreparados);
        DiscosUtilizadosPeriodo = discos.Sum(d => d.DiscosUtilizados);
        DiscosMermaPeriodo = discos.Sum(d => d.DiscosMerma);
        DiscosCortesiaPeriodo = discos.Sum(d => d.DiscosCortesia);

        await CargarResumenIngredientesAsync(db, inicio, finExclusivo);
        await CargarResumenProductosAsync(db, inicio, finExclusivo);
        await CargarResumenDiarioAsync(db);
        await CargarResumenCajasAsync(db, inicio, finExclusivo);
        await CargarResumenConteosAsync(db, inicio, finExclusivo);
    }

    private async Task CargarResumenIngredientesAsync(PizzeriaDbContext db, DateTime inicio, DateTime finExclusivo)
    {
        ResumenIngredientes.Clear();
        var ingredientes = await db.Ingredientes.OrderBy(i => i.Nombre).ToListAsync();

        foreach (var ing in ingredientes)
        {
            var entradas = await db.Entradas.Where(e => e.IngredienteId == ing.Id && e.Fecha >= inicio && e.Fecha < finExclusivo).SumAsync(e => (double?)e.Cantidad) ?? 0;
            var gastos = await db.Gastos.Where(g => g.IngredienteId == ing.Id && g.Fecha >= inicio && g.Fecha < finExclusivo).SumAsync(g => (double?)g.Cantidad) ?? 0;
            var mermas = await db.Mermas.Where(m => m.IngredienteId == ing.Id && m.Fecha >= inicio && m.Fecha < finExclusivo).SumAsync(m => (double?)m.Cantidad) ?? 0;
            var mercancias = await db.MercanciasRecibidas.Where(m => m.IngredienteId == ing.Id && m.Fecha >= inicio && m.Fecha < finExclusivo).SumAsync(m => (double?)m.Cantidad) ?? 0;

            if (entradas > 0 || gastos > 0 || mermas > 0 || mercancias > 0)
            {
                ResumenIngredientes.Add(new ResumenIngrediente
                {
                    Nombre = ing.Nombre,
                    UnidadMedida = ing.UnidadMedida,
                    TotalEntradas = entradas,
                    TotalGastos = gastos,
                    TotalMermas = mermas,
                    TotalMercancia = mercancias
                });
            }
        }
    }

    private async Task CargarResumenProductosAsync(PizzeriaDbContext db, DateTime inicio, DateTime finExclusivo)
    {
        ResumenProductos.Clear();
        var cortesiasPorProducto = await db.Cortesias
            .Include(c => c.Producto)
            .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
            .GroupBy(c => c.ProductoId)
            .Select(g => new { ProductoId = g.Key, Total = g.Sum(c => c.Cantidad) })
            .ToListAsync();

        foreach (var item in cortesiasPorProducto)
        {
            var producto = await db.Productos.FindAsync(item.ProductoId);
            if (producto != null)
            {
                ResumenProductos.Add(new ResumenProducto
                {
                    Nombre = producto.Nombre,
                    Categoria = producto.Categoria.ToString(),
                    TotalCortesias = item.Total
                });
            }
        }

        var ordenados = ResumenProductos.OrderByDescending(p => p.TotalCortesias).ToList();
        ResumenProductos.Clear();
        foreach (var p in ordenados) ResumenProductos.Add(p);
    }

    private async Task CargarResumenDiarioAsync(PizzeriaDbContext db)
    {
        ResumenDiario.Clear();
        var dias = (FechaFin.Date - FechaInicio.Date).Days + 1;
        var fechaInicioMostrar = dias > 14 ? FechaFin.Date.AddDays(-13) : FechaInicio.Date;

        for (var fecha = fechaInicioMostrar; fecha <= FechaFin.Date; fecha = fecha.AddDays(1))
        {
            var fechaSiguiente = fecha.AddDays(1);
            ResumenDiario.Add(new ResumenDiario
            {
                Fecha = fecha,
                Entradas = await db.Entradas.CountAsync(e => e.Fecha >= fecha && e.Fecha < fechaSiguiente),
                Gastos = await db.Gastos.CountAsync(g => g.Fecha >= fecha && g.Fecha < fechaSiguiente),
                Mermas = await db.Mermas.CountAsync(m => m.Fecha >= fecha && m.Fecha < fechaSiguiente),
                Cortesias = await db.Cortesias.CountAsync(c => c.Fecha >= fecha && c.Fecha < fechaSiguiente),
                Mercancias = await db.MercanciasRecibidas.CountAsync(m => m.Fecha >= fecha && m.Fecha < fechaSiguiente)
            });
        }
    }

    private async Task CargarResumenCajasAsync(PizzeriaDbContext db, DateTime inicio, DateTime finExclusivo)
    {
        ResumenCajas.Clear();
        var cajas = await db.InventarioCajas
            .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
            .ToListAsync();

        if (cajas.Count == 0) return;

        ResumenCajas.Add(new ResumenCajas
        {
            Recibidas = cajas.Sum(c => c.CajasRecibidas),
            Utilizadas = cajas.Sum(c => c.CajasUtilizadas),
            Merma = cajas.Sum(c => c.CajasMerma),
            Disponible = cajas.Sum(c => c.CantidadDisponible)
        });
    }

    private async Task CargarResumenConteosAsync(PizzeriaDbContext db, DateTime inicio, DateTime finExclusivo)
    {
        ResumenConteos.Clear();
        var conteos = await db.ConteosInventario
            .Include(c => c.Lineas)
            .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();

        foreach (var c in conteos)
        {
            ResumenConteos.Add(new ResumenConteo
            {
                Fecha = c.Fecha,
                Tipo = c.Tipo.Mostrar(),
                CantidadLineas = c.Lineas.Count
            });
        }
    }

    [RelayCommand]
    private void ExportarCSV() => Exportar("CSV", datos => ExportService.ExportarCsv(datos));

    [RelayCommand]
    private void ExportarExcel() => Exportar("Excel", datos => ExportService.ExportarExcel(datos));

    [RelayCommand]
    private void ExportarPDF() => Exportar("PDF", datos => ExportService.ExportarPdf(datos));

    private void Exportar(string formato, Func<ExportService.DatosReporte, string> exportador)
    {
        try
        {
            // Pedir a la usuaria que elija el período antes de generar el archivo.
            // Por defecto se usa el rango ya cargado en pantalla; si la usuaria
            // selecciona uno distinto, el reporte se filtra con el nuevo rango
            // sin alterar la vista actual.
            var dialog = new Windows.PeriodoExportacionDialog(formato)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                MensajeExportacion = $"Exportación a {formato} cancelada.";
                return;
            }

            var datos = ExportService.CargarDatos(dialog.FechaInicio, dialog.FechaFin);
            var ruta = exportador(datos);
            MensajeExportacion = $"✓ Reporte {formato} ({dialog.EtiquetaPeriodo}) exportado: {ruta}";
            System.Diagnostics.Process.Start("explorer.exe", ExportService.CarpetaReportes);
        }
        catch (Exception ex)
        {
            MensajeExportacion = $"✗ Error al exportar {formato}: {ex.Message}";
            App.GuardarErrorDump(ex, $"Exportación {formato}");
        }
    }

    /// <summary>
    /// Exporta toda la base de datos (no sólo el rango seleccionado) a un
    /// archivo .valdb que puede ser importado por una versión futura del
    /// sistema para preservar el historial completo.
    /// </summary>
    [RelayCommand]
    private void ExportarBaseDatos()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar base de datos de Valerio's Pizza",
                FileName = DatabaseBackupService.NombreArchivoSugerido(),
                InitialDirectory = DatabaseBackupService.CarpetaRespaldosSugerida,
                Filter = "Respaldo Valerio's (*.valdb)|*.valdb|Base de datos SQLite (*.db;*.sqlite)|*.db;*.sqlite|Todos los archivos (*.*)|*.*",
                DefaultExt = ".valdb",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var ruta = DatabaseBackupService.Exportar(dialog.FileName);
            MensajeExportacion = $"✓ Base de datos exportada: {ruta}";

            var carpeta = System.IO.Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(carpeta))
            {
                System.Diagnostics.Process.Start("explorer.exe", carpeta);
            }
        }
        catch (Exception ex)
        {
            MensajeExportacion = $"✗ Error al exportar base de datos: {ex.Message}";
            App.GuardarErrorDump(ex, "Exportar Base de Datos");
            System.Windows.MessageBox.Show(
                $"No se pudo exportar la base de datos.\n\n{ex.Message}",
                "Exportar base de datos",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Importa un archivo .valdb generado por <see cref="ExportarBaseDatos"/>
    /// (incluso de una versión anterior) y lo establece como la base de datos
    /// activa. Reinicia la aplicación al finalizar para que los cambios surtan
    /// efecto.
    /// </summary>
    [RelayCommand]
    private void ImportarBaseDatos()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar base de datos de Valerio's Pizza",
                InitialDirectory = DatabaseBackupService.CarpetaRespaldosSugerida,
                Filter = "Respaldos Valerio's (*.valdb;*.db;*.sqlite)|*.valdb;*.db;*.sqlite|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var validacion = DatabaseBackupService.Validar(dialog.FileName);
            if (!validacion.EsValido)
            {
                var detalle = validacion.TablasFaltantes.Count > 0
                    ? $"\n\nTablas faltantes: {string.Join(", ", validacion.TablasFaltantes)}"
                    : string.Empty;
                System.Windows.MessageBox.Show(
                    $"El archivo seleccionado no es un respaldo válido.\n\n{validacion.Motivo}{detalle}",
                    "Importar base de datos",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var confirmar = System.Windows.MessageBox.Show(
                "Esta acción reemplazará la base de datos actual con el archivo seleccionado.\n\n" +
                "Se creará automáticamente un respaldo de seguridad de los datos actuales " +
                "antes de proceder, y la aplicación se reiniciará al finalizar.\n\n" +
                "¿Desea continuar?",
                "Confirmar importación",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirmar != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            var rutaRespaldo = DatabaseBackupService.Importar(dialog.FileName);

            var mensaje = "La base de datos se importó correctamente.";
            if (!string.IsNullOrEmpty(rutaRespaldo))
            {
                mensaje += $"\n\nRespaldo de seguridad de los datos anteriores:\n{rutaRespaldo}";
            }
            mensaje += "\n\nLa aplicación se reiniciará para aplicar los cambios.";

            System.Windows.MessageBox.Show(
                mensaje,
                "Importación completada",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            // Reiniciar para que los DbContext nuevos abran el archivo importado.
            var ejecutable = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(ejecutable))
            {
                System.Diagnostics.Process.Start(ejecutable);
            }
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MensajeExportacion = $"✗ Error al importar base de datos: {ex.Message}";
            App.GuardarErrorDump(ex, "Importar Base de Datos");
            System.Windows.MessageBox.Show(
                $"No se pudo importar la base de datos.\n\n{ex.Message}",
                "Importar base de datos",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Borra registros de la base de datos según el alcance seleccionado.
    /// Pide confirmación con doble clic (Yes/No) antes de proceder y muestra
    /// un resumen de cuántos registros fueron eliminados.
    /// </summary>
    [RelayCommand]
    private void BorrarDatos()
    {
        try
        {
            AlcanceBorrado alcance = AlcanceBorradoSeleccionado switch
            {
                "Hoy"                                 => AlcanceBorrado.Hoy,
                "Esta semana"                         => AlcanceBorrado.EstaSemana,
                "Este mes"                            => AlcanceBorrado.EsteMes,
                "Este año"                            => AlcanceBorrado.EsteAno,
                "Rango personalizado (fecha y hora)"  => AlcanceBorrado.RangoPersonalizado,
                _                                     => AlcanceBorrado.TodoElCatalogo
            };

            DateTime inicio, fin;
            string descripcion;

            if (alcance == AlcanceBorrado.RangoPersonalizado)
            {
                inicio = BorradoDesde;
                fin = BorradoHasta;
                if (fin <= inicio)
                {
                    System.Windows.MessageBox.Show(
                        "La fecha y hora final debe ser posterior a la inicial.",
                        "Rango inválido",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                descripcion = $"todos los registros entre " +
                              $"{inicio:dd/MM/yyyy HH:mm} y {fin:dd/MM/yyyy HH:mm}";
            }
            else if (alcance == AlcanceBorrado.TodoElCatalogo)
            {
                inicio = DateTime.MinValue;
                fin = DateTime.MaxValue;
                descripcion = "TODA la base de datos (incluyendo ingredientes y productos del catálogo)";
            }
            else
            {
                (inicio, fin) = DatabaseWipeService.CalcularRango(alcance);
                descripcion = AlcanceBorradoSeleccionado.ToLower() +
                              $" ({inicio:dd/MM/yyyy} a {fin.AddSeconds(-1):dd/MM/yyyy})";
            }

            var primeraConfirmacion = System.Windows.MessageBox.Show(
                $"Esta acción eliminará {descripcion}.\n\n" +
                "Esta operación NO se puede deshacer.\n\n" +
                "Se recomienda exportar la base de datos antes de continuar.\n\n" +
                "¿Desea continuar?",
                "Confirmar borrado",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (primeraConfirmacion != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            var segundaConfirmacion = System.Windows.MessageBox.Show(
                "Última oportunidad para cancelar.\n\n" +
                "¿Está completamente seguro de que desea borrar estos datos de forma permanente?",
                "Confirmación final",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Stop,
                System.Windows.MessageBoxResult.No);

            if (segundaConfirmacion != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            ResultadoBorrado resultado = alcance == AlcanceBorrado.TodoElCatalogo
                ? DatabaseWipeService.BorrarTodo()
                : DatabaseWipeService.BorrarPorRango(inicio, fin);

            MensajeBorrado = $"✓ Borrado completado — {resultado.Total} registro(s) eliminado(s).";

            System.Windows.MessageBox.Show(
                "Borrado completado:\n\n" + DatabaseWipeService.Describir(resultado),
                "Resumen del borrado",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            // Refrescar el reporte para que la UI muestre la nueva realidad.
            _ = GenerarReporteAsync();
        }
        catch (Exception ex)
        {
            MensajeBorrado = $"✗ Error al borrar datos: {ex.Message}";
            App.GuardarErrorDump(ex, "Borrar datos");
            System.Windows.MessageBox.Show(
                $"No se pudieron borrar los datos.\n\n{ex.Message}",
                "Borrar datos",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
