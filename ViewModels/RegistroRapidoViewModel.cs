using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Data;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;
using ValeriosPizza.Services.UndoRedo.Commands;
using ValeriosPizza.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Línea editable de un conteo Apertura/Cierre. La cantidad la escribe la dueña.
/// </summary>
public partial class ConteoLineaVM : ObservableObject
{
    [ObservableProperty]
    private int _ingredienteId;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _unidadMedida = string.Empty;

    [ObservableProperty]
    private double _cantidad;
}

public partial class RegistroRapidoViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly UndoRedoService _undoRedo;

    [ObservableProperty]
    private ObservableCollection<Ingrediente> _ingredientes = new();

    [ObservableProperty]
    private ObservableCollection<Producto> _productos = new();

    // === ENTRADA ===
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

    // === GASTO ===
    [ObservableProperty]
    private Ingrediente? _gastoIngredienteSeleccionado;

    [ObservableProperty]
    private double _gastoCantidad;

    [ObservableProperty]
    private string _gastoDescripcion = string.Empty;

    // === MERMA ===
    [ObservableProperty]
    private Ingrediente? _mermaIngredienteSeleccionado;

    [ObservableProperty]
    private double _mermaCantidad;

    [ObservableProperty]
    private string _mermaMotivo = string.Empty;

    // === CORTESIA ===
    [ObservableProperty]
    private Producto? _cortesiaProductoSeleccionado;

    [ObservableProperty]
    private int _cortesiaCantidad;

    [ObservableProperty]
    private string _cortesiaMotivo = string.Empty;

    // === DISCOS ===
    [ObservableProperty]
    private int _discosIniciales;

    [ObservableProperty]
    private int _discosPreparados;

    [ObservableProperty]
    private int _discosUtilizados;

    [ObservableProperty]
    private int _discosMerma;

    [ObservableProperty]
    private int _discosCortesia;

    public int DiscosDisponibles => DiscosIniciales + DiscosPreparados - DiscosUtilizados - DiscosMerma - DiscosCortesia;

    // === CAJAS PARA PIZZAS (tamaño único, 30 cm) ===
    [ObservableProperty] private int _cajaInicial;
    [ObservableProperty] private int _cajaRecibidas;
    [ObservableProperty] private int _cajaUtilizadas;
    [ObservableProperty] private int _cajaMerma;
    public int CajaDisponible =>
        CajaInicial + CajaRecibidas - CajaUtilizadas - CajaMerma;

    // === INVENTARIO APERTURA ===
    [ObservableProperty]
    private ObservableCollection<ConteoLineaVM> _lineasApertura = new();

    [ObservableProperty]
    private string _notasApertura = string.Empty;

    // === INVENTARIO CIERRE ===
    [ObservableProperty]
    private ObservableCollection<ConteoLineaVM> _lineasCierre = new();

    [ObservableProperty]
    private string _notasCierre = string.Empty;

    /// <summary>Vistas paginadas usadas por los DataGrid de los conteos para
    /// no renderizar cientos de filas a la vez en el equipo objetivo.</summary>
    public PagedCollectionView<ConteoLineaVM> LineasAperturaPaged { get; }
    public PagedCollectionView<ConteoLineaVM> LineasCierrePaged { get; }

    partial void OnLineasAperturaChanged(ObservableCollection<ConteoLineaVM> value)
        => LineasAperturaPaged?.CambiarOrigen(value);

    partial void OnLineasCierreChanged(ObservableCollection<ConteoLineaVM> value)
        => LineasCierrePaged?.CambiarOrigen(value);

    // === CALCULADORAS COP (helpers de UI, no se guardan en BD) ===
    // Cada sección tiene su propia calculadora con precio unitario, subtotal,
    // impuesto y retención opcionales (montos fijos en COP).
    public CalculadoraCopVM CalcEntrada { get; } = new();
    public CalculadoraCopVM CalcGasto { get; } = new();
    public CalculadoraCopVM CalcMerma { get; } = new();
    public CalculadoraCopVM CalcCortesia { get; } = new();
    public CalculadoraCopVM CalcDiscos { get; } = new();
    public CalculadoraCopVM CalcCajas { get; } = new();

    public RegistroRapidoViewModel(
        IDbContextFactory<PizzeriaDbContext> dbFactory,
        UndoRedoService undoRedo)
    {
        _dbFactory = dbFactory;
        _undoRedo = undoRedo;
        LineasAperturaPaged = new PagedCollectionView<ConteoLineaVM>(LineasApertura);
        LineasCierrePaged   = new PagedCollectionView<ConteoLineaVM>(LineasCierre);
        CargarDatos();
        // Suscripción con referencia débil: si este VM se recicla, el
        // messenger libera automáticamente el handler.
        WeakReferenceMessenger.Default.Register<IngredientesChangedMessage>(
            this, (_, _) => OnIngredientesActualizados());
        // También nos suscribimos a cambios en Productos para que el combo
        // de Cortesía se actualice si la dueña agrega/edita productos desde
        // Inventario o desde el botón "+" del propio formulario.
        WeakReferenceMessenger.Default.Register<ProductosChangedMessage>(
            this, (_, _) => CargarDatos());
    }

    private void OnIngredientesActualizados()
    {
        // Recargamos los listados para reflejar nuevos ingredientes creados desde otras pantallas.
        CargarDatos();
        SincronizarConteoConIngredientes(LineasApertura);
        SincronizarConteoConIngredientes(LineasCierre);
    }

    private void CargarDatos()
    {
        using var db = _dbFactory.CreateDbContext();

        var ingredientesActivos = db.Ingredientes
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .ToList();

        Ingredientes.Clear();
        foreach (var ing in ingredientesActivos)
        {
            Ingredientes.Add(ing);
        }

        Productos.Clear();
        // Excluimos solo Descontinuados; los Agotados siguen apareciendo para
        // que la usuaria pueda registrar cortesías históricas si fuese el caso.
        foreach (var prod in db.Productos
            .Where(p => p.Estado != EstadoProducto.Descontinuado)
            .OrderBy(p => p.Nombre)
            .ToList())
        {
            Productos.Add(prod);
        }

        // Si los listados de Apertura/Cierre están vacíos, prellenarlos.
        if (LineasApertura.Count == 0)
        {
            foreach (var ing in ingredientesActivos)
            {
                LineasApertura.Add(NuevaLineaConteo(ing));
            }
        }

        if (LineasCierre.Count == 0)
        {
            foreach (var ing in ingredientesActivos)
            {
                LineasCierre.Add(NuevaLineaConteo(ing));
            }
        }
    }

    private static ConteoLineaVM NuevaLineaConteo(Ingrediente ing) => new()
    {
        IngredienteId = ing.Id,
        Nombre = ing.Nombre,
        UnidadMedida = ing.UnidadMedida,
        Cantidad = 0
    };

    private void SincronizarConteoConIngredientes(ObservableCollection<ConteoLineaVM> lineas)
    {
        // Agrega ingredientes nuevos que aún no están en la lista; conserva las cantidades ya tipeadas.
        var idsExistentes = lineas.Select(l => l.IngredienteId).ToHashSet();
        foreach (var ing in Ingredientes)
        {
            if (!idsExistentes.Contains(ing.Id))
            {
                lineas.Add(NuevaLineaConteo(ing));
            }
        }
    }

    partial void OnDiscosInicialesChanged(int value) { OnPropertyChanged(nameof(DiscosDisponibles)); CalcDiscos.Cantidad = DiscosIniciales + DiscosPreparados; }
    partial void OnDiscosPreparadosChanged(int value) { OnPropertyChanged(nameof(DiscosDisponibles)); CalcDiscos.Cantidad = DiscosIniciales + DiscosPreparados; }
    partial void OnDiscosUtilizadosChanged(int value) => OnPropertyChanged(nameof(DiscosDisponibles));
    partial void OnDiscosMermaChanged(int value) => OnPropertyChanged(nameof(DiscosDisponibles));
    partial void OnDiscosCortesiaChanged(int value) => OnPropertyChanged(nameof(DiscosDisponibles));

    partial void OnCajaInicialChanged(int value) { OnPropertyChanged(nameof(CajaDisponible)); CalcCajas.Cantidad = CajaInicial + CajaRecibidas; }
    partial void OnCajaRecibidasChanged(int value) { OnPropertyChanged(nameof(CajaDisponible)); CalcCajas.Cantidad = CajaInicial + CajaRecibidas; }
    partial void OnCajaUtilizadasChanged(int value) => OnPropertyChanged(nameof(CajaDisponible));
    partial void OnCajaMermaChanged(int value) => OnPropertyChanged(nameof(CajaDisponible));

    // Sincronizar cantidad de cada calculadora con el campo "Cantidad" de su sección.
    partial void OnCantidadChanged(double value) => CalcEntrada.Cantidad = value;
    partial void OnGastoCantidadChanged(double value) => CalcGasto.Cantidad = value;
    partial void OnMermaCantidadChanged(double value) => CalcMerma.Cantidad = value;
    partial void OnCortesiaCantidadChanged(int value) => CalcCortesia.Cantidad = value;

    [RelayCommand]
    private async Task GuardarEntradaAsync()
    {
        // Forzar la validación declarativa de TODAS las propiedades de Entrada
        // antes de tocar la BD. Si algo falla, devolvemos al usuario un
        // mensaje agregado en lugar de tirar una excepción más adelante.
        ValidateAllProperties();
        var propsEntrada = new[] { nameof(IngredienteSeleccionado), nameof(Cantidad), nameof(Costo) };
        if (HasErroresEnPropiedades(propsEntrada, out var mensaje))
        {
            MessageBox.Show(mensaje, "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // IngredienteSeleccionado está garantizado no-nulo aquí por la validación.
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
            IngredienteId = ing.Id,
            IngredienteNombre = ing.Nombre,
            UnidadMedida = ing.UnidadMedida,
            Cantidad = GastoCantidad,
            DescripcionGasto = GastoDescripcion,
            PrecioUnitario = CalcGasto.PrecioUnitario,
            Impuesto = CalcGasto.Impuesto,
            Retencion = CalcGasto.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Gasto registrado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarGasto();
        CargarDatos();
    }

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
            IngredienteId = ing.Id,
            IngredienteNombre = ing.Nombre,
            UnidadMedida = ing.UnidadMedida,
            Cantidad = MermaCantidad,
            Motivo = MermaMotivo,
            PrecioUnitario = CalcMerma.PrecioUnitario,
            Impuesto = CalcMerma.Impuesto,
            Retencion = CalcMerma.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Merma registrada correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarMerma();
        CargarDatos();
    }

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
            // CargarDatos() vuelve a leer Productos desde la BD; luego
            // buscamos el recién creado por Id para preseleccionarlo (no se
            // puede reusar la referencia del diálogo porque proviene de un
            // DbContext ya disposed).
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
            ProductoId = prod.Id,
            ProductoNombre = prod.Nombre,
            Cantidad = CortesiaCantidad,
            Motivo = CortesiaMotivo,
            PrecioUnitario = CalcCortesia.PrecioUnitario,
            Impuesto = CalcCortesia.Impuesto,
            Retencion = CalcCortesia.Retencion
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show("Cortesía registrada correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        LimpiarCortesia();
    }

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

    [RelayCommand]
    private Task GuardarConteoAperturaAsync() => GuardarConteoAsync(TipoConteo.Apertura, LineasApertura, NotasApertura);

    [RelayCommand]
    private Task GuardarConteoCierreAsync() => GuardarConteoAsync(TipoConteo.Cierre, LineasCierre, NotasCierre);

    private async Task GuardarConteoAsync(TipoConteo tipo, ObservableCollection<ConteoLineaVM> lineas, string notas)
    {
        if (lineas.Count == 0)
        {
            MessageBox.Show("No hay ingredientes en la lista. Agregue ingredientes antes de guardar.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Si ya existe uno para hoy del mismo tipo, preguntar antes de
        // reemplazarlo. El comando se encarga de capturar el snapshot
        // anterior para que el undo lo pueda restaurar.
        using (var db = _dbFactory.CreateDbContext())
        {
            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);
            var existente = db.ConteosInventario
                .FirstOrDefault(c => c.Tipo == tipo && c.Fecha >= hoy && c.Fecha < manana);
            if (existente != null)
            {
                var resp = MessageBox.Show(
                    $"Ya existe un Inventario {tipo.Mostrar()} registrado hoy. ¿Desea reemplazarlo?",
                    "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (resp != MessageBoxResult.Yes) return;
            }
        }

        var cmd = new RegistrarConteoCommand
        {
            Tipo = tipo,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
            Lineas = lineas.Select(l => new RegistrarConteoCommand.LineaDto
            {
                IngredienteId = l.IngredienteId,
                Cantidad = l.Cantidad
            }).ToList()
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show($"Inventario {tipo.Mostrar()} guardado correctamente",
            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

        // Limpiar las cantidades pero conservar las filas (para próximo conteo)
        foreach (var l in lineas) l.Cantidad = 0;
        if (tipo == TipoConteo.Apertura) NotasApertura = string.Empty;
        else NotasCierre = string.Empty;
    }

    [RelayCommand]
    private void AgregarIngredienteApertura() => AbrirDialogoYAgregar(LineasApertura);

    [RelayCommand]
    private void AgregarIngredienteCierre() => AbrirDialogoYAgregar(LineasCierre);

    /// <summary>
    /// Borra DEFINITIVAMENTE el ingrediente representado por la línea de
    /// conteo. Si tiene historial registrado (entradas/gastos/mermas/etc.) la
    /// operación se rechaza para no romper la integridad referencial. La
    /// notificación global se encarga de actualizar las demás vistas.
    /// </summary>
    [RelayCommand]
    private void RemoverIngrediente(ConteoLineaVM? linea)
    {
        if (linea == null) return;

        using var db = _dbFactory.CreateDbContext();

        var ing = db.Ingredientes.Find(linea.IngredienteId);
        if (ing == null)
        {
            // Ya no existe en la BD; quitarlo de las listas locales y salir.
            QuitarLineaDeAmbasListas(linea.IngredienteId);
            return;
        }

        var entradas = db.Entradas.Count(e => e.IngredienteId == ing.Id);
        var gastos = db.Gastos.Count(g => g.IngredienteId == ing.Id);
        var mermas = db.Mermas.Count(m => m.IngredienteId == ing.Id);
        var mercancias = db.MercanciasRecibidas.Count(m => m.IngredienteId == ing.Id);
        var lineasConteo = db.ConteoInventarioLineas.Count(l => l.IngredienteId == ing.Id);
        var totalHistorial = entradas + gastos + mermas + mercancias + lineasConteo;

        if (totalHistorial > 0)
        {
            MessageBox.Show(
                $"No se puede eliminar \"{ing.Nombre}\" porque tiene historial registrado:\n\n" +
                $"  • Entradas: {entradas}\n" +
                $"  • Gastos: {gastos}\n" +
                $"  • Mermas: {mermas}\n" +
                $"  • Mercancía recibida: {mercancias}\n" +
                $"  • Líneas de conteo: {lineasConteo}\n\n" +
                "Váyase a Inventario y use \"Desactivar\" para ocultarlo sin perder el historial.",
                "No se puede remover",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var resp = MessageBox.Show(
            $"¿Eliminar DEFINITIVAMENTE el ingrediente \"{ing.Nombre}\"?\n\n" +
            "Esta acción no se puede deshacer.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            db.Ingredientes.Remove(ing);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Remover Ingrediente (Registro Rápido)");
            MessageBox.Show(
                $"No se pudo eliminar el ingrediente.\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        QuitarLineaDeAmbasListas(linea.IngredienteId);
        IngredientesNotifier.NotificarCambio();
    }

    private void QuitarLineaDeAmbasListas(int ingredienteId)
    {
        for (int i = LineasApertura.Count - 1; i >= 0; i--)
        {
            if (LineasApertura[i].IngredienteId == ingredienteId)
                LineasApertura.RemoveAt(i);
        }
        for (int i = LineasCierre.Count - 1; i >= 0; i--)
        {
            if (LineasCierre[i].IngredienteId == ingredienteId)
                LineasCierre.RemoveAt(i);
        }
    }

    private void AbrirDialogoYAgregar(ObservableCollection<ConteoLineaVM> lineas)
    {
        var dialog = IngredienteDialog.ParaCrear(_dbFactory);
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true && dialog.IngredienteResultante != null)
        {
            // Agregar la nueva línea al conteo actual y notificar a otros VMs.
            lineas.Add(NuevaLineaConteo(dialog.IngredienteResultante));
            IngredientesNotifier.NotificarCambio();
        }
    }

    private void LimpiarEntrada()
    {
        IngredienteSeleccionado = null;
        Cantidad = 0;
        Costo = 0;
        Proveedor = string.Empty;
        Notas = string.Empty;
        // Limpiar errores de validación acumulados de la sesión previa.
        ClearErrors(nameof(IngredienteSeleccionado));
        ClearErrors(nameof(Cantidad));
        ClearErrors(nameof(Costo));
    }

    /// <summary>
    /// Concatena los mensajes de error de las propiedades indicadas. Si alguna
    /// tiene errores, devuelve <c>true</c> y produce el texto agregado en
    /// <paramref name="mensaje"/>; si todas son válidas, devuelve <c>false</c>.
    /// </summary>
    private bool HasErroresEnPropiedades(string[] propiedades, out string mensaje)
    {
        var errores = new List<string>();
        foreach (var prop in propiedades)
        {
            foreach (var err in GetErrors(prop))
            {
                if (err is System.ComponentModel.DataAnnotations.ValidationResult vr
                    && !string.IsNullOrEmpty(vr.ErrorMessage))
                {
                    errores.Add("• " + vr.ErrorMessage);
                }
            }
        }
        mensaje = string.Join(System.Environment.NewLine, errores);
        return errores.Count > 0;
    }

    private void LimpiarGasto()
    {
        GastoIngredienteSeleccionado = null;
        GastoCantidad = 0;
        GastoDescripcion = string.Empty;
    }

    private void LimpiarMerma()
    {
        MermaIngredienteSeleccionado = null;
        MermaCantidad = 0;
        MermaMotivo = string.Empty;
    }

    private void LimpiarCortesia()
    {
        CortesiaProductoSeleccionado = null;
        CortesiaCantidad = 0;
        CortesiaMotivo = string.Empty;
    }

    private void LimpiarDiscos()
    {
        DiscosIniciales = 0;
        DiscosPreparados = 0;
        DiscosUtilizados = 0;
        DiscosMerma = 0;
        DiscosCortesia = 0;
    }

    private void LimpiarCajas()
    {
        CajaInicial = CajaRecibidas = CajaUtilizadas = CajaMerma = 0;
    }

    // ============================================================
    // SOPORTE PARA "GUARDAR ANTES DE SALIR"
    // ============================================================

    /// <summary>
    /// Indica si alguna sección del formulario tiene datos pendientes que aún
    /// no se han persistido a la base de datos. Se usa al cerrar la app para
    /// preguntarle a la usuaria si quiere guardar o descartar.
    /// </summary>
    public bool TieneCambiosSinGuardar => SeccionesPendientes().Any();

    /// <summary>
    /// Devuelve los nombres de las secciones del formulario que tienen datos
    /// sin guardar. Una sección "tiene datos" cuando algún campo significativo
    /// (cantidad &gt; 0, ingrediente/producto seleccionado, o texto no vacío)
    /// fue modificado por la usuaria.
    /// </summary>
    public IReadOnlyList<string> SeccionesPendientes()
    {
        var lista = new List<string>();
        if (TieneDatosEntrada())   lista.Add("Entrada");
        if (TieneDatosGasto())     lista.Add("Gasto");
        if (TieneDatosMerma())     lista.Add("Merma");
        if (TieneDatosCortesia())  lista.Add("Cortesía");
        if (TieneDatosDiscos())    lista.Add("Discos");
        if (TieneDatosCajas())     lista.Add("Cajas");
        if (TieneDatosConteo(LineasApertura, NotasApertura)) lista.Add("Inventario Apertura");
        if (TieneDatosConteo(LineasCierre, NotasCierre))     lista.Add("Inventario Cierre");
        return lista;
    }

    private bool TieneDatosEntrada() =>
        IngredienteSeleccionado != null || Cantidad > 0 || Costo > 0
        || !string.IsNullOrWhiteSpace(Proveedor) || !string.IsNullOrWhiteSpace(Notas);

    private bool TieneDatosGasto() =>
        GastoIngredienteSeleccionado != null || GastoCantidad > 0
        || !string.IsNullOrWhiteSpace(GastoDescripcion);

    private bool TieneDatosMerma() =>
        MermaIngredienteSeleccionado != null || MermaCantidad > 0
        || !string.IsNullOrWhiteSpace(MermaMotivo);

    private bool TieneDatosCortesia() =>
        CortesiaProductoSeleccionado != null || CortesiaCantidad > 0
        || !string.IsNullOrWhiteSpace(CortesiaMotivo);

    private bool TieneDatosDiscos() =>
        DiscosIniciales > 0 || DiscosPreparados > 0 || DiscosUtilizados > 0
        || DiscosMerma > 0 || DiscosCortesia > 0;

    private bool TieneDatosCajas() =>
        CajaInicial > 0 || CajaRecibidas > 0 || CajaUtilizadas > 0 || CajaMerma > 0;

    private static bool TieneDatosConteo(ObservableCollection<ConteoLineaVM> lineas, string notas) =>
        (lineas?.Any(l => l.Cantidad > 0) ?? false) || !string.IsNullOrWhiteSpace(notas);

    /// <summary>
    /// Resultado del intento de guardar todas las secciones pendientes al
    /// cerrar la aplicación.
    /// </summary>
    public sealed class ResultadoGuardadoCierre
    {
        public List<string> Guardadas { get; } = new();
        public List<(string Seccion, string Motivo)> Omitidas { get; } = new();
    }

    /// <summary>
    /// Intenta guardar de forma silenciosa (sin diálogos de confirmación) toda
    /// sección que tenga datos válidos. Las secciones con datos incompletos o
    /// inválidos se omiten y se reportan en <see cref="ResultadoGuardadoCierre.Omitidas"/>.
    /// </summary>
    public ResultadoGuardadoCierre IntentarGuardarTodoSilencioso()
    {
        var resultado = new ResultadoGuardadoCierre();

        TryGuardarSeccion(resultado, "Entrada", TieneDatosEntrada, GuardarEntradaSilencioso);
        TryGuardarSeccion(resultado, "Gasto", TieneDatosGasto, GuardarGastoSilencioso);
        TryGuardarSeccion(resultado, "Merma", TieneDatosMerma, GuardarMermaSilencioso);
        TryGuardarSeccion(resultado, "Cortesía", TieneDatosCortesia, GuardarCortesiaSilencioso);
        TryGuardarSeccion(resultado, "Discos", TieneDatosDiscos, GuardarDiscosSilencioso);
        TryGuardarSeccion(resultado, "Cajas", TieneDatosCajas, GuardarCajasSilencioso);
        TryGuardarSeccion(resultado, "Inventario Apertura",
            () => TieneDatosConteo(LineasApertura, NotasApertura),
            () => GuardarConteoSilencioso(TipoConteo.Apertura, LineasApertura, NotasApertura,
                limpiar: () => { foreach (var l in LineasApertura) l.Cantidad = 0; NotasApertura = string.Empty; }));
        TryGuardarSeccion(resultado, "Inventario Cierre",
            () => TieneDatosConteo(LineasCierre, NotasCierre),
            () => GuardarConteoSilencioso(TipoConteo.Cierre, LineasCierre, NotasCierre,
                limpiar: () => { foreach (var l in LineasCierre) l.Cantidad = 0; NotasCierre = string.Empty; }));

        return resultado;
    }

    private static void TryGuardarSeccion(
        ResultadoGuardadoCierre resultado, string nombre,
        Func<bool> tieneDatos, Func<(bool ok, string? motivo)> intentar)
    {
        if (!tieneDatos()) return;
        try
        {
            var (ok, motivo) = intentar();
            if (ok) resultado.Guardadas.Add(nombre);
            else resultado.Omitidas.Add((nombre, motivo ?? "datos incompletos"));
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, $"Guardar {nombre} al cerrar");
            resultado.Omitidas.Add((nombre, ex.Message));
        }
    }

    private (bool ok, string? motivo) GuardarEntradaSilencioso()
    {
        if (IngredienteSeleccionado == null || Cantidad <= 0)
            return (false, "falta ingrediente o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Entradas.Add(new Entrada
        {
            Fecha = DateTime.Now, IngredienteId = IngredienteSeleccionado.Id,
            Cantidad = Cantidad, CostoTotal = Costo, Proveedor = Proveedor, Notas = Notas,
            PrecioUnitario = CalcEntrada.PrecioUnitario, Impuesto = CalcEntrada.Impuesto, Retencion = CalcEntrada.Retencion
        });
        var ing = db.Ingredientes.Find(IngredienteSeleccionado.Id);
        if (ing != null) ing.CantidadActual += Cantidad;
        db.SaveChanges();
        LimpiarEntrada();
        return (true, null);
    }

    private (bool ok, string? motivo) GuardarGastoSilencioso()
    {
        if (GastoIngredienteSeleccionado == null || GastoCantidad <= 0)
            return (false, "falta ingrediente o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Gastos.Add(new Gasto
        {
            Fecha = DateTime.Now, IngredienteId = GastoIngredienteSeleccionado.Id,
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

    private (bool ok, string? motivo) GuardarMermaSilencioso()
    {
        if (MermaIngredienteSeleccionado == null || MermaCantidad <= 0)
            return (false, "falta ingrediente o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Mermas.Add(new Merma
        {
            Fecha = DateTime.Now, IngredienteId = MermaIngredienteSeleccionado.Id,
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

    private (bool ok, string? motivo) GuardarCortesiaSilencioso()
    {
        if (CortesiaProductoSeleccionado == null || CortesiaCantidad <= 0)
            return (false, "falta producto o cantidad");
        using var db = _dbFactory.CreateDbContext();
        db.Cortesias.Add(new Cortesia
        {
            Fecha = DateTime.Now, ProductoId = CortesiaProductoSeleccionado.Id,
            Cantidad = CortesiaCantidad, Motivo = CortesiaMotivo,
            PrecioUnitario = CalcCortesia.PrecioUnitario, Impuesto = CalcCortesia.Impuesto, Retencion = CalcCortesia.Retencion
        });
        db.SaveChanges();
        LimpiarCortesia();
        return (true, null);
    }

    private (bool ok, string? motivo) GuardarDiscosSilencioso()
    {
        using var db = _dbFactory.CreateDbContext();
        db.InventarioDiscos.Add(new InventarioDisco
        {
            Fecha = DateTime.Now,
            CantidadInicial = DiscosIniciales, DiscosPreparados = DiscosPreparados,
            DiscosUtilizados = DiscosUtilizados, DiscosMerma = DiscosMerma,
            DiscosCortesia = DiscosCortesia,
            PrecioUnitario = CalcDiscos.PrecioUnitario, Impuesto = CalcDiscos.Impuesto, Retencion = CalcDiscos.Retencion
        });
        db.SaveChanges();
        LimpiarDiscos();
        return (true, null);
    }

    private (bool ok, string? motivo) GuardarCajasSilencioso()
    {
        using var db = _dbFactory.CreateDbContext();
        db.InventarioCajas.Add(new InventarioCaja
        {
            Fecha = DateTime.Now,
            CantidadInicial = CajaInicial, CajasRecibidas = CajaRecibidas,
            CajasUtilizadas = CajaUtilizadas, CajasMerma = CajaMerma,
            PrecioUnitario = CalcCajas.PrecioUnitario, Impuesto = CalcCajas.Impuesto, Retencion = CalcCajas.Retencion
        });
        db.SaveChanges();
        LimpiarCajas();
        return (true, null);
    }

    private (bool ok, string? motivo) GuardarConteoSilencioso(
        TipoConteo tipo, ObservableCollection<ConteoLineaVM> lineas, string notas, Action limpiar)
    {
        if (lineas.Count == 0) return (false, "lista vacía");

        using var db = _dbFactory.CreateDbContext();
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);

        // Si ya existe un conteo del mismo tipo para hoy, no sobrescribimos en silencio.
        var existente = db.ConteosInventario
            .FirstOrDefault(c => c.Tipo == tipo && c.Fecha >= hoy && c.Fecha < manana);
        if (existente != null)
            return (false, $"ya existe un conteo de {tipo.Mostrar()} hoy; no se sobrescribió");

        db.ConteosInventario.Add(new ConteoInventario
        {
            Fecha = DateTime.Now, Tipo = tipo,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
            Lineas = lineas.Select(l => new ConteoInventarioLinea
            {
                IngredienteId = l.IngredienteId, Cantidad = l.Cantidad
            }).ToList()
        });
        db.SaveChanges();
        limpiar();
        return (true, null);
    }
}
