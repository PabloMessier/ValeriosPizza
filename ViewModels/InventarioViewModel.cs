using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ValeriosPizza.Models;
using ValeriosPizza.Data;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;
using ValeriosPizza.Services.UndoRedo.Commands;
using ValeriosPizza.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace ValeriosPizza.ViewModels;

// Clase auxiliar para representar un día en la vista semanal.
// El color de fondo lo decide la XAML (DataTrigger sobre IsToday) usando
// los recursos de tema TodayHighlightBrush / OtherDayBackgroundBrush, en vez
// de devolver un hex string fijo desde el ViewModel.
public class DiaResumen
{
    public string DiaNombre { get; set; } = string.Empty;
    public string Fecha { get; set; } = string.Empty;
    public DateTime FechaCompleta { get; set; }
    public int Entradas { get; set; }
    public int Gastos { get; set; }
    public int Mermas { get; set; }
    public int Cortesias { get; set; }
    public int Mercancias { get; set; }
    public bool IsToday => FechaCompleta.Date == DateTime.Today;
}

public class ProductoDisplay
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public Categoria Categoria { get; set; }
    public EstadoProducto Estado { get; set; }
    public string EstadoTexto => Estado.Mostrar();
    public string ColorEstado => Estado switch
    {
        EstadoProducto.Activo => "#2E7D32",
        EstadoProducto.Agotado => "#EF6C00",
        EstadoProducto.Descontinuado => "#C62828",
        _ => "#666666"
    };
}

public class MovimientoDisplay
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Cantidad { get; set; } = string.Empty;
    public string Detalles { get; set; } = string.Empty;
    public string ColorTipo { get; set; } = "#666666";
}

public partial class InventarioViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly UndoRedoService _undoRedo;

    [ObservableProperty]
    private ObservableCollection<Ingrediente> _ingredientes = new();

    [ObservableProperty]
    private ObservableCollection<ProductoDisplay> _productos = new();

    [ObservableProperty]
    private ObservableCollection<DiaResumen> _diasSemana = new();

    [ObservableProperty]
    private ObservableCollection<MovimientoDisplay> _movimientos = new();

    /// <summary>
    /// Texto de búsqueda para la pestaña Ingredientes. Filtra por nombre
    /// (case-insensitive). Cadena vacía muestra todos.
    /// </summary>
    [ObservableProperty]
    private string _filtroIngredientes = string.Empty;

    /// <summary>Texto de búsqueda para la pestaña Productos.</summary>
    [ObservableProperty]
    private string _filtroProductos = string.Empty;

    /// <summary>
    /// Vista filtrable de <see cref="Ingredientes"/>. La XAML enlaza esta
    /// propiedad en lugar de la colección cruda para que el filtro de
    /// búsqueda actúe sin reordenar/duplicar datos.
    /// </summary>
    public ICollectionView IngredientesView { get; }

    /// <summary>Vista filtrable de <see cref="Productos"/>.</summary>
    public ICollectionView ProductosView { get; }

    /// <summary>Vistas paginadas (10/20/30… por página) sobre las vistas
    /// filtradas. Las usan los DataGrid para no renderizar listas enteras
    /// cuando el inventario crece.</summary>
    public PagedCollectionView<Ingrediente> IngredientesPaged { get; }
    public PagedCollectionView<ProductoDisplay> ProductosPaged { get; }
    public PagedCollectionView<MovimientoDisplay> MovimientosPaged { get; }

    [ObservableProperty]
    private string _semanaActual = string.Empty;

    [ObservableProperty]
    private string _periodoActual = string.Empty;

    [ObservableProperty]
    private string _filtroSeleccionado = "Hoy";

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

    // Estado de Apertura/Cierre del día (para tarjeta resumen).
    [ObservableProperty]
    private string _aperturaHoyTexto = "Sin registrar";

    [ObservableProperty]
    private string _cierreHoyTexto = "Sin registrar";

    /// <summary>
    /// Calculadora COP del inventario (helper de UI, no se persiste).
    /// La usuaria escribe cantidad y precio unitario para valorar el stock,
    /// con impuesto/retención opcionales.
    /// </summary>
    public CalculadoraCopVM CalcInventario { get; } = new();

    public InventarioViewModel(
        IDbContextFactory<PizzeriaDbContext> dbFactory,
        UndoRedoService undoRedo)
    {
        _dbFactory = dbFactory;
        _undoRedo = undoRedo;

        // Vistas filtrables construidas sobre las colecciones observables.
        // Refrescamos manualmente cuando cambian los textos de búsqueda.
        IngredientesView = CollectionViewSource.GetDefaultView(Ingredientes);
        IngredientesView.Filter = obj =>
            obj is Ingrediente i &&
            (string.IsNullOrWhiteSpace(FiltroIngredientes) ||
             i.Nombre.Contains(FiltroIngredientes, System.StringComparison.OrdinalIgnoreCase));

        ProductosView = CollectionViewSource.GetDefaultView(Productos);
        ProductosView.Filter = obj =>
            obj is ProductoDisplay p &&
            (string.IsNullOrWhiteSpace(FiltroProductos) ||
             p.Nombre.Contains(FiltroProductos, System.StringComparison.OrdinalIgnoreCase));

        // Vistas paginadas: se enganchan a la vista filtrada para que el
        // paginador respete los filtros del usuario.
        IngredientesPaged = new PagedCollectionView<Ingrediente>(
            IngredientesView.Cast<Ingrediente>(),
            (System.Collections.Specialized.INotifyCollectionChanged)IngredientesView);
        ProductosPaged = new PagedCollectionView<ProductoDisplay>(
            ProductosView.Cast<ProductoDisplay>(),
            (System.Collections.Specialized.INotifyCollectionChanged)ProductosView);
        MovimientosPaged = new PagedCollectionView<MovimientoDisplay>(Movimientos);

        // Carga inicial fire-and-forget; cualquier excepción se vuelca al log.
        _ = RecargarTodoAsync();
        // Registro con referencia débil: cuando el VM sea recolectado, el
        // messenger limpia automáticamente la suscripción. Reemplaza al
        // antiguo evento estático que nunca liberaba el handler.
        WeakReferenceMessenger.Default.Register<IngredientesChangedMessage>(
            this, (_, _) => _ = CargarInventarioAsync());
        // También recargamos la pestaña Productos cuando algo cambia desde
        // otras pantallas (Registro Rápido / diálogo "+ AGREGAR PRODUCTO").
        WeakReferenceMessenger.Default.Register<ProductosChangedMessage>(
            this, (_, _) => _ = CargarInventarioAsync());
    }

    private async Task RecargarTodoAsync()
    {
        try
        {
            await CargarInventarioAsync();
            await CargarVistaSemanalAsync();
            await CargarMovimientosAsync();
            await ActualizarEstadoDelDiaAsync();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "InventarioViewModel.RecargarTodoAsync");
        }
    }

    [RelayCommand]
    private Task FiltrarHoyAsync()    { FiltroSeleccionado = "Hoy";    return CargarMovimientosAsync(); }

    [RelayCommand]
    private Task FiltrarSemanaAsync() { FiltroSeleccionado = "Semana"; return CargarMovimientosAsync(); }

    [RelayCommand]
    private Task FiltrarMesAsync()    { FiltroSeleccionado = "Mes";    return CargarMovimientosAsync(); }

    [RelayCommand]
    private void AgregarIngrediente()
    {
        var dialog = IngredienteDialog.ParaCrear(_dbFactory);
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            _ = CargarInventarioAsync();
            IngredientesNotifier.NotificarCambio();
        }
    }

    /// <summary>
    /// Abre el diálogo de creación de producto del menú. Al guardar
    /// notifica a otros VMs para que recarguen sus combos (por ejemplo, el
    /// de Cortesía en "Registro Rápido").
    /// </summary>
    [RelayCommand]
    private void AgregarProducto()
    {
        var dialog = ProductoDialog.ParaCrear(_dbFactory);
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            _ = CargarInventarioAsync();
            ProductosNotifier.NotificarCambio();
        }
    }

    [RelayCommand]
    private void EditarIngrediente(Ingrediente? ingrediente)
    {
        if (ingrediente == null) return;
        var dialog = IngredienteDialog.ParaEditar(ingrediente, _dbFactory);
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            _ = CargarInventarioAsync();
            IngredientesNotifier.NotificarCambio();
        }
    }

    /// <summary>
    /// Envía el ingrediente seleccionado a la pantalla "Bodega" (almacén
    /// general). No toca el stock activo del ingrediente: la bodega es una
    /// tabla aparte. Si la cantidad actual en inventario es cero, igualmente
    /// se permite enviar la fila como referencia (cantidad = 0).
    /// </summary>
    [RelayCommand]
    private async Task AgregarABodegaAsync(Ingrediente? ingrediente)
    {
        if (ingrediente == null) return;

        var resp = MessageBox.Show(
            $"¿Agregar \"{ingrediente.Nombre}\" a la bodega?\n\n" +
            $"  • Cantidad: {ingrediente.CantidadActual:N2} {ingrediente.UnidadMedida}\n\n" +
            "Esta acción NO modifica el stock activo del ingrediente; sólo registra\n" +
            "una entrada en la pantalla Bodega.",
            "Agregar a Bodega",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            db.BodegaItems.Add(new BodegaItem
            {
                FechaAgregado = DateTime.Now,
                Nombre = ingrediente.Nombre,
                Categoria = "Ingrediente",
                Cantidad = ingrediente.CantidadActual,
                UnidadMedida = ingrediente.UnidadMedida,
                Origen = "Inventario",
                IngredienteId = ingrediente.Id
            });
            await db.SaveChangesAsync();
            BodegaNotifier.NotificarCambio();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "AgregarABodega (Inventario)");
            MessageBox.Show($"No se pudo agregar a bodega.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DesactivarIngrediente(Ingrediente? ingrediente)
    {
        if (ingrediente == null) return;
        var resp = MessageBox.Show(
            $"¿Desactivar el ingrediente \"{ingrediente.Nombre}\"?\n\n" +
            "Dejará de aparecer en los formularios pero su historial se conservará.",
            "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (resp != MessageBoxResult.Yes) return;

        using var db = _dbFactory.CreateDbContext();
        var ing = db.Ingredientes.Find(ingrediente.Id);
        if (ing != null)
        {
            ing.Activo = false;
            db.SaveChanges();
        }
        _ = CargarInventarioAsync();
        IngredientesNotifier.NotificarCambio();
    }

    /// <summary>
    /// Borra DEFINITIVAMENTE un ingrediente. A diferencia de Desactivar, esta
    /// operación elimina el registro de la base de datos. Si el ingrediente
    /// tiene historial (entradas, gastos, mermas, mercancía recibida o líneas
    /// de conteo) la operación se rechaza para no romper la integridad
    /// referencial; el usuario puede usar Desactivar en su lugar.
    /// </summary>
    [RelayCommand]
    private void RemoverIngrediente(Ingrediente? ingrediente)
    {
        _ = RemoverIngredienteAsync(ingrediente);
    }

    private async Task RemoverIngredienteAsync(Ingrediente? ingrediente)
    {
        if (ingrediente == null) return;

        using var db = _dbFactory.CreateDbContext();

        var entradas = db.Entradas.Count(e => e.IngredienteId == ingrediente.Id);
        var gastos = db.Gastos.Count(g => g.IngredienteId == ingrediente.Id);
        var mermas = db.Mermas.Count(m => m.IngredienteId == ingrediente.Id);
        var mercancias = db.MercanciasRecibidas.Count(m => m.IngredienteId == ingrediente.Id);
        var lineasConteo = db.ConteoInventarioLineas.Count(l => l.IngredienteId == ingrediente.Id);
        var totalHistorial = entradas + gastos + mermas + mercancias + lineasConteo;

        if (totalHistorial > 0)
        {
            MessageBox.Show(
                $"No se puede eliminar \"{ingrediente.Nombre}\" porque tiene historial registrado:\n\n" +
                $"  • Entradas: {entradas}\n" +
                $"  • Gastos: {gastos}\n" +
                $"  • Mermas: {mermas}\n" +
                $"  • Mercancía recibida: {mercancias}\n" +
                $"  • Líneas de conteo: {lineasConteo}\n\n" +
                "Use \"Desactivar\" para ocultarlo sin perder el historial.",
                "No se puede remover",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var resp = MessageBox.Show(
            $"¿Eliminar DEFINITIVAMENTE el ingrediente \"{ingrediente.Nombre}\"?\n\n" +
            "Esta acción no se puede deshacer.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            var cmd = new EliminarIngredienteCommand { IngredienteId = ingrediente.Id };
            await _undoRedo.EjecutarAsync(cmd);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Remover Ingrediente");
            MessageBox.Show(
                $"No se pudo eliminar el ingrediente.\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _ = CargarInventarioAsync();
        IngredientesNotifier.NotificarCambio();
    }

    private async Task CargarInventarioAsync()
    {
        await using var db = _dbFactory.CreateDbContext();

        var ingredientesActivos = await db.Ingredientes
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .ToListAsync();
        Ingredientes.ReplaceAll(ingredientesActivos);

        var productosUI = await db.Productos
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoDisplay
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                Estado = p.Estado
            })
            .ToListAsync();
        Productos.ReplaceAll(productosUI);
    }

    /// <summary>
    /// Cambia el estado operativo de un producto (Activo / Agotado / Descontinuado).
    /// La bandera legacy <c>Producto.Activo</c> se deriva automáticamente de
    /// <see cref="EstadoProducto"/>, no hay que actualizarla manualmente.
    /// El parámetro es un <see cref="Tuple{ProductoDisplay,EstadoProducto}"/>
    /// construido por <see cref="System.Windows.Data.MultiBinding"/> en XAML.
    /// </summary>
    private async void CambiarEstadoProducto(ProductoDisplay producto, EstadoProducto nuevoEstado)
    {
        if (producto.Estado == nuevoEstado) return;

        try
        {
            var cmd = new CambiarEstadoProductoCommand
            {
                ProductoId = producto.Id,
                ProductoNombre = producto.Nombre,
                EstadoAnterior = producto.Estado,
                EstadoNuevo = nuevoEstado
            };
            await _undoRedo.EjecutarAsync(cmd);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Cambiar Estado Producto");
        }
        _ = CargarInventarioAsync();
    }

    /// <summary>
    /// Comando único parametrizado por el nuevo <see cref="EstadoProducto"/>.
    /// El <c>CommandParameter</c> en XAML es un <c>MultiBinding</c> que
    /// combina el <see cref="ProductoDisplay"/> de la fila con el valor de
    /// estado deseado (<c>x:Static</c>). Reemplaza a las antiguas
    /// <c>MarcarProductoActivo/Agotado/Descontinuado</c>.
    /// </summary>
    [RelayCommand]
    private void MarcarProducto(object? parametro)
    {
        // Aceptamos tanto Tuple<,> (vía MultiBinding + IMultiValueConverter)
        // como object[] (vía IMultiValueConverter directo) para dar flexibilidad
        // al consumidor XAML.
        ProductoDisplay? producto = null;
        EstadoProducto? estado = null;

        switch (parametro)
        {
            case Tuple<ProductoDisplay, EstadoProducto> t:
                producto = t.Item1; estado = t.Item2; break;
            case object[] arr when arr.Length == 2 && arr[0] is ProductoDisplay p && arr[1] is EstadoProducto s:
                producto = p; estado = s; break;
        }

        if (producto == null || estado == null) return;
        CambiarEstadoProducto(producto, estado.Value);
    }

    private async Task CargarVistaSemanalAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        var hoy = DateTime.Today;
        var diasDesdeInicioSemana = ((int)hoy.DayOfWeek - 1 + 7) % 7;
        var inicioSemana = hoy.AddDays(-diasDesdeInicioSemana);
        var finSemana = inicioSemana.AddDays(6);

        SemanaActual = $"Semana del {inicioSemana:dd/MM} al {finSemana:dd/MM/yyyy}";

        DiasSemana.Clear();
        string[] nombresDias = { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };

        for (int i = 0; i < 7; i++)
        {
            var fecha = inicioSemana.AddDays(i);
            var dia = new DiaResumen
            {
                DiaNombre = nombresDias[i],
                Fecha = fecha.ToString("dd/MM"),
                FechaCompleta = fecha,
                Entradas = await db.Entradas.CountAsync(e => e.Fecha.Date == fecha),
                Gastos = await db.Gastos.CountAsync(g => g.Fecha.Date == fecha),
                Mermas = await db.Mermas.CountAsync(m => m.Fecha.Date == fecha),
                Cortesias = await db.Cortesias.CountAsync(c => c.Fecha.Date == fecha),
                Mercancias = await db.MercanciasRecibidas.CountAsync(m => m.Fecha.Date == fecha)
            };
            DiasSemana.Add(dia);
        }
    }

    private async Task ActualizarEstadoDelDiaAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);

        var apertura = await db.ConteosInventario
            .Where(c => c.Tipo == TipoConteo.Apertura && c.Fecha >= hoy && c.Fecha < manana)
            .OrderByDescending(c => c.Fecha)
            .FirstOrDefaultAsync();
        AperturaHoyTexto = apertura != null ? $"Registrada a las {apertura.Fecha:HH:mm}" : "Sin registrar";

        var cierre = await db.ConteosInventario
            .Where(c => c.Tipo == TipoConteo.Cierre && c.Fecha >= hoy && c.Fecha < manana)
            .OrderByDescending(c => c.Fecha)
            .FirstOrDefaultAsync();
        CierreHoyTexto = cierre != null ? $"Registrado a las {cierre.Fecha:HH:mm}" : "Sin registrar";
    }

    private async Task CargarMovimientosAsync()
    {
        await using var db = _dbFactory.CreateDbContext();

        DateTime fechaInicio;
        DateTime fechaFinExclusivo;

        switch (FiltroSeleccionado)
        {
            case "Semana":
                var diasDesdeInicioSemana = ((int)DateTime.Today.DayOfWeek - 1 + 7) % 7;
                fechaInicio = DateTime.Today.AddDays(-diasDesdeInicioSemana);
                fechaFinExclusivo = fechaInicio.AddDays(7);
                PeriodoActual = $"Semana del {fechaInicio:dd/MM} al {fechaInicio.AddDays(6):dd/MM/yyyy}";
                break;
            case "Mes":
                fechaInicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                fechaFinExclusivo = fechaInicio.AddMonths(1);
                PeriodoActual = $"Mes de {DateTime.Today:MMMM yyyy}";
                break;
            default: // "Hoy"
                fechaInicio = DateTime.Today;
                fechaFinExclusivo = fechaInicio.AddDays(1);
                PeriodoActual = $"Hoy - {DateTime.Today:dd/MM/yyyy}";
                break;
        }

        var entradas = await db.Entradas.Include(e => e.Ingrediente)
            .Where(e => e.Fecha >= fechaInicio && e.Fecha < fechaFinExclusivo).ToListAsync();
        var gastos = await db.Gastos.Include(g => g.Ingrediente)
            .Where(g => g.Fecha >= fechaInicio && g.Fecha < fechaFinExclusivo).ToListAsync();
        var mermas = await db.Mermas.Include(m => m.Ingrediente)
            .Where(m => m.Fecha >= fechaInicio && m.Fecha < fechaFinExclusivo).ToListAsync();
        var cortesias = await db.Cortesias.Include(c => c.Producto)
            .Where(c => c.Fecha >= fechaInicio && c.Fecha < fechaFinExclusivo).ToListAsync();
        var mercancias = await db.MercanciasRecibidas.Include(m => m.Ingrediente)
            .Where(m => m.Fecha >= fechaInicio && m.Fecha < fechaFinExclusivo).ToListAsync();
        var cajas = await db.InventarioCajas
            .Where(c => c.Fecha >= fechaInicio && c.Fecha < fechaFinExclusivo).ToListAsync();
        var conteos = await db.ConteosInventario
            .Include(c => c.Lineas).ThenInclude(l => l.Ingrediente)
            .Where(c => c.Fecha >= fechaInicio && c.Fecha < fechaFinExclusivo).ToListAsync();

        // Construimos la lista completa primero y luego la asignamos en una
        // sola pasada para evitar N notificaciones CollectionChanged.
        var todos = new List<MovimientoDisplay>(
            entradas.Count + gastos.Count + mermas.Count + cortesias.Count
            + mercancias.Count + cajas.Count + conteos.Sum(c => c.Lineas.Count));

        todos.AddRange(entradas.Select(e => new MovimientoDisplay
        {
            Fecha = e.Fecha, Tipo = "ENTRADA",
            Descripcion = e.Ingrediente?.Nombre ?? "N/A",
            Cantidad = $"+{e.Cantidad:N2} {e.Ingrediente?.UnidadMedida}",
            Detalles = e.Proveedor ?? "",
            ColorTipo = "#4CAF50"
        }));
        todos.AddRange(gastos.Select(g => new MovimientoDisplay
        {
            Fecha = g.Fecha, Tipo = "GASTO",
            Descripcion = g.Ingrediente?.Nombre ?? "N/A",
            Cantidad = $"-{g.Cantidad:N2} {g.Ingrediente?.UnidadMedida}",
            Detalles = g.Descripcion ?? "",
            ColorTipo = "#2196F3"
        }));
        todos.AddRange(mermas.Select(m => new MovimientoDisplay
        {
            Fecha = m.Fecha, Tipo = "MERMA",
            Descripcion = m.Ingrediente?.Nombre ?? "N/A",
            Cantidad = $"-{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
            Detalles = m.Motivo ?? "",
            ColorTipo = "#FF9800"
        }));
        todos.AddRange(cortesias.Select(c => new MovimientoDisplay
        {
            Fecha = c.Fecha, Tipo = "CORTESÍA",
            Descripcion = c.Producto?.Nombre ?? "N/A",
            Cantidad = $"{c.Cantidad} unidad(es)",
            Detalles = c.Motivo ?? "",
            ColorTipo = "#9C27B0"
        }));
        todos.AddRange(mercancias.Select(m => new MovimientoDisplay
        {
            Fecha = m.Fecha, Tipo = "MERCANCÍA",
            Descripcion = m.Ingrediente?.Nombre ?? "N/A",
            Cantidad = $"+{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
            Detalles = $"{m.Proveedor} | Fact: {m.NumeroFactura}",
            ColorTipo = "#00BCD4"
        }));
        todos.AddRange(cajas.Select(c => new MovimientoDisplay
        {
            Fecha = c.Fecha, Tipo = "CAJA",
            Descripcion = "Cajas para Pizzas (30 cm)",
            Cantidad = $"{c.CantidadDisponible} disp.",
            Detalles = $"Inicial: {c.CantidadInicial}, +{c.CajasRecibidas}, -{c.CajasUtilizadas} usadas, -{c.CajasMerma} merma",
            ColorTipo = "#795548"
        }));
        foreach (var c in conteos)
        {
            foreach (var l in c.Lineas)
            {
                todos.Add(new MovimientoDisplay
                {
                    Fecha = c.Fecha,
                    Tipo = c.Tipo == TipoConteo.Apertura ? "APERTURA" : "CIERRE",
                    Descripcion = l.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"{l.Cantidad:N2} {l.Ingrediente?.UnidadMedida}",
                    Detalles = c.Notas ?? "",
                    ColorTipo = c.Tipo == TipoConteo.Apertura ? "#1976D2" : "#7B1FA2"
                });
            }
        }

        Movimientos.ReplaceAll(todos.OrderByDescending(m => m.Fecha));

        TotalEntradas = entradas.Count;
        TotalGastos = gastos.Count;
        TotalMermas = mermas.Count;
        TotalCortesias = cortesias.Count;
        TotalMercancias = mercancias.Count;
        TotalCajas = cajas.Count;
        TotalConteos = conteos.Count;
    }

    public Task ActualizarInventarioAsync() => RecargarTodoAsync();

    // Generados por [ObservableProperty]: refrescan la ICollectionView cuando
    // la usuaria escribe en los campos de búsqueda.
    partial void OnFiltroIngredientesChanged(string value) => IngredientesView.Refresh();
    partial void OnFiltroProductosChanged(string value) => ProductosView.Refresh();
}
