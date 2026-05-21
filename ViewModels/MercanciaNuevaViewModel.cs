using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Data;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;
using ValeriosPizza.Services.UndoRedo.Commands;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace ValeriosPizza.ViewModels;

public partial class MercanciaNuevaViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly UndoRedoService _undoRedo;

    [ObservableProperty]
    private ObservableCollection<Ingrediente> _ingredientes = new();

    [ObservableProperty]
    private ObservableCollection<MercanciaRecibida> _mercanciasHoy = new();

    /// <summary>Vista paginada del historial de mercancías mostrado en el grid.</summary>
    public PagedCollectionView<MercanciaRecibida> MercanciasHoyPaged { get; }

    partial void OnMercanciasHoyChanged(ObservableCollection<MercanciaRecibida> value)
        => MercanciasHoyPaged?.CambiarOrigen(value);

    [ObservableProperty]
    private Ingrediente? _ingredienteSeleccionado;

    [ObservableProperty]
    private double _cantidad;

    [ObservableProperty]
    private string _proveedor = string.Empty;

    [ObservableProperty]
    private string _numeroFactura = string.Empty;

    [ObservableProperty]
    private string _notas = string.Empty;

    /// <summary>
    /// Ruta absoluta del archivo de factura ya guardado (PDF/imagen). Null si
    /// no hay adjunto. Se actualiza al elegir archivo y al cargar para editar.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneFacturaAdjunta))]
    [NotifyPropertyChangedFor(nameof(NombreFacturaAdjunta))]
    private string? _rutaFactura;

    /// <summary>Ruta de un archivo seleccionado pero todavía no copiado a la carpeta de la app (modo crear/edit).</summary>
    private string? _rutaFacturaPendiente;

    public bool TieneFacturaAdjunta => !string.IsNullOrWhiteSpace(RutaFactura) || !string.IsNullOrWhiteSpace(_rutaFacturaPendiente);
    public string NombreFacturaAdjunta
    {
        get
        {
            var ruta = _rutaFacturaPendiente ?? RutaFactura;
            return string.IsNullOrWhiteSpace(ruta) ? "Sin archivo adjunto" : Path.GetFileName(ruta);
        }
    }

    /// <summary>Carpeta donde se guardan las copias de las facturas digitales.</summary>
    private static string CarpetaFacturas
    {
        get
        {
            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ValeriosPizzeria", "Facturas");
            if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
            return carpeta;
        }
    }

    // Identificador del registro en edición. Cuando es null, el formulario
    // está en modo "crear". Cuando tiene valor, el botón REGISTRAR cambia a
    // ACTUALIZAR y al guardar se modifica el registro existente.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnModoEdicion))]
    [NotifyPropertyChangedFor(nameof(TextoBotonGuardar))]
    private int? _mercanciaEnEdicionId;

    public bool EnModoEdicion => MercanciaEnEdicionId.HasValue;
    public string TextoBotonGuardar => EnModoEdicion ? "ACTUALIZAR MERCANCÍA" : "REGISTRAR MERCANCÍA";

    /// <summary>
    /// Calculadora COP de la sección (helper de UI, no se persiste en BD).
    /// La cantidad se sincroniza con el campo "Cantidad" del formulario.
    /// </summary>
    public CalculadoraCopVM CalcMercancia { get; } = new();

    partial void OnCantidadChanged(double value) => CalcMercancia.Cantidad = value;

    public MercanciaNuevaViewModel(
        IDbContextFactory<PizzeriaDbContext> dbFactory,
        UndoRedoService undoRedo)
    {
        _dbFactory = dbFactory;
        _undoRedo = undoRedo;
        MercanciasHoyPaged = new PagedCollectionView<MercanciaRecibida>(MercanciasHoy);
        CargarDatos();
    }

    private void CargarDatos()
    {
        using var db = _dbFactory.CreateDbContext();
        
        Ingredientes.Clear();
        foreach (var ing in db.Ingredientes.OrderBy(i => i.Nombre).ToList())
        {
            Ingredientes.Add(ing);
        }

        CargarMercanciasHoy();
    }

    private void CargarMercanciasHoy()
    {
        using var db = _dbFactory.CreateDbContext();
        var hoy = DateTime.Today;

        MercanciasHoy.Clear();
        var mercancias = db.MercanciasRecibidas
            .Where(m => m.Fecha.Date == hoy)
            .OrderByDescending(m => m.Fecha)
            .ToList();

        foreach (var m in mercancias)
        {
            // Cargar el ingrediente relacionado
            m.Ingrediente = db.Ingredientes.Find(m.IngredienteId);
            MercanciasHoy.Add(m);
        }
    }

    [RelayCommand]
    private async Task GuardarMercanciaAsync()
    {
        if (IngredienteSeleccionado == null || Cantidad <= 0)
        {
            MessageBox.Show("Seleccione un ingrediente y cantidad válida", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Modo edición: la actualización de un registro existente NO pasa
        // por undo/redo (los swaps de archivos PDF y de ingredientes hacen
        // frágil la reversión). Mantenemos el flujo síncrono original.
        if (EnModoEdicion && MercanciaEnEdicionId.HasValue)
        {
            ActualizarMercanciaExistente();
            LimpiarFormulario();
            CargarMercanciasHoy();
            return;
        }

        // ===== Crear registro nuevo (con undo) =====
        var ing = IngredienteSeleccionado;
        string? rutaFinal = null;
        // Copiamos la factura a la carpeta interna ANTES del save porque la
        // ruta forma parte del registro a insertar. Si el save falla, hay
        // que borrar el archivo recién copiado para no acumular huérfanos.
        bool rutaFinalEsCopiaNueva = false;
        if (!string.IsNullOrWhiteSpace(_rutaFacturaPendiente))
        {
            rutaFinal = CopiarFacturaACarpeta(_rutaFacturaPendiente!);
            rutaFinalEsCopiaNueva = true;
        }
        else if (!string.IsNullOrWhiteSpace(RutaFactura))
        {
            rutaFinal = RutaFactura;
        }

        var cmd = new RegistrarMercanciaCommand
        {
            IngredienteId = ing.Id,
            IngredienteNombre = ing.Nombre,
            UnidadMedida = ing.UnidadMedida,
            Cantidad = Cantidad,
            Proveedor = Proveedor,
            NumeroFactura = NumeroFactura,
            Notas = Notas,
            PrecioUnitario = CalcMercancia.PrecioUnitario,
            Impuesto = CalcMercancia.Impuesto,
            Retencion = CalcMercancia.Retencion,
            RutaFactura = rutaFinal
        };
        try
        {
            await _undoRedo.EjecutarAsync(cmd);
        }
        catch
        {
            // Si el save falla y habíamos copiado la factura a la carpeta
            // interna, eliminamos el archivo huérfano para no llenar el disco.
            if (rutaFinalEsCopiaNueva && rutaFinal != null)
            {
                BorrarSiExiste(rutaFinal);
            }
            throw;
        }

        MessageBox.Show($"Mercancía registrada: {Cantidad} {ing.UnidadMedida} de {ing.Nombre}",
            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

        LimpiarFormulario();
        CargarMercanciasHoy();
    }

    /// <summary>
    /// Borra un archivo si existe, tragándose cualquier IOException para no
    /// enmascarar la excepción original que disparó la limpieza.
    /// </summary>
    private static void BorrarSiExiste(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); }
        catch { /* mejor esfuerzo */ }
    }

    /// <summary>
    /// Actualiza un registro existente sin pasar por la pila de undo. Esta
    /// rama mantiene el comportamiento original previo a undo/redo porque
    /// involucra revertir y reaplicar deltas en dos ingredientes posiblemente
    /// distintos más un swap de archivo PDF, lo cual es complejo de revertir.
    /// </summary>
    private void ActualizarMercanciaExistente()
    {
        using var db = _dbFactory.CreateDbContext();

        var existente = db.MercanciasRecibidas.Find(MercanciaEnEdicionId!.Value);
        if (existente == null)
        {
            MessageBox.Show("El registro ya no existe (puede haber sido eliminado).",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            CancelarEdicion();
            return;
        }

        var ingredienteAnterior = db.Ingredientes.Find(existente.IngredienteId);
        if (ingredienteAnterior != null)
        {
            ingredienteAnterior.CantidadActual -= existente.Cantidad;
            if (ingredienteAnterior.CantidadActual < 0) ingredienteAnterior.CantidadActual = 0;
            ingredienteAnterior.FechaActualizacion = DateTime.Now;
        }

        existente.IngredienteId = IngredienteSeleccionado!.Id;
        existente.Cantidad = Cantidad;
        existente.Proveedor = Proveedor;
        existente.NumeroFactura = NumeroFactura;
        existente.Notas = Notas;
        existente.PrecioUnitario = CalcMercancia.PrecioUnitario;
        existente.Impuesto = CalcMercancia.Impuesto;
        existente.Retencion = CalcMercancia.Retencion;
        // Si hay una factura pendiente la copiamos a la carpeta interna
        // antes del save; si el save falla, borramos la copia para no dejar
        // archivos huérfanos referenciados por nadie.
        string? rutaCopia = null;
        if (!string.IsNullOrWhiteSpace(_rutaFacturaPendiente))
        {
            rutaCopia = CopiarFacturaACarpeta(_rutaFacturaPendiente!);
            existente.RutaFactura = rutaCopia;
        }
        else
        {
            existente.RutaFactura = RutaFactura;
        }

        var ingredienteNuevo = db.Ingredientes.Find(IngredienteSeleccionado.Id);
        if (ingredienteNuevo != null)
        {
            ingredienteNuevo.CantidadActual += Cantidad;
            ingredienteNuevo.FechaActualizacion = DateTime.Now;
        }

        try
        {
            db.SaveChanges();
        }
        catch
        {
            if (rutaCopia != null) BorrarSiExiste(rutaCopia);
            throw;
        }

        MessageBox.Show($"Mercancía actualizada: {Cantidad} {IngredienteSeleccionado.UnidadMedida} de {IngredienteSeleccionado.Nombre}",
            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Carga el registro indicado en el formulario y entra en modo edición.
    /// </summary>
    [RelayCommand]
    private void EditarMercancia(MercanciaRecibida? mercancia)
    {
        if (mercancia == null) return;

        IngredienteSeleccionado = Ingredientes.FirstOrDefault(i => i.Id == mercancia.IngredienteId);
        Cantidad = mercancia.Cantidad;
        Proveedor = mercancia.Proveedor;
        NumeroFactura = mercancia.NumeroFactura;
        Notas = mercancia.Notas;
        CalcMercancia.PrecioUnitario = mercancia.PrecioUnitario;
        CalcMercancia.Impuesto = mercancia.Impuesto;
        CalcMercancia.Retencion = mercancia.Retencion;
        RutaFactura = mercancia.RutaFactura;
        _rutaFacturaPendiente = null;
        OnPropertyChanged(nameof(TieneFacturaAdjunta));
        OnPropertyChanged(nameof(NombreFacturaAdjunta));
        MercanciaEnEdicionId = mercancia.Id;
    }

    /// <summary>
    /// Cancela la edición actual y limpia el formulario.
    /// </summary>
    [RelayCommand]
    private void CancelarEdicion()
    {
        LimpiarFormulario();
    }

    /// <summary>
    /// Envía esta fila de mercancía recibida a la pantalla "Bodega". Se
    /// usa cuando el dueño quiere separar de la circulación del día los
    /// items recibidos para llevarlos al almacén general. No revierte el
    /// efecto en stock; sólo crea una entrada en la tabla BodegaItems.
    /// </summary>
    [RelayCommand]
    private async Task AgregarABodegaAsync(MercanciaRecibida? mercancia)
    {
        if (mercancia == null) return;

        var nombreIng = mercancia.Ingrediente?.Nombre ?? "(ingrediente)";
        var unidad = mercancia.Ingrediente?.UnidadMedida ?? string.Empty;

        var resp = MessageBox.Show(
            $"¿Agregar a la bodega esta mercancía recibida?\n\n" +
            $"  • Ingrediente: {nombreIng}\n" +
            $"  • Cantidad: {mercancia.Cantidad:N2} {unidad}\n" +
            $"  • Proveedor: {mercancia.Proveedor}\n\n" +
            "Esta acción sólo crea una fila en la pantalla Bodega; no afecta el stock\n" +
            "actual ni el registro de mercancía recibida.",
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
                Nombre = nombreIng,
                Categoria = "Mercancía",
                Cantidad = mercancia.Cantidad,
                UnidadMedida = unidad,
                Notas = $"Factura: {mercancia.NumeroFactura}",
                Origen = "Mercancía Nueva",
                IngredienteId = mercancia.IngredienteId,
                MercanciaRecibidaId = mercancia.Id
            });
            await db.SaveChangesAsync();
            BodegaNotifier.NotificarCambio();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "AgregarABodega (Mercancía Nueva)");
            MessageBox.Show($"No se pudo agregar a bodega.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Variante "desde el formulario": agrega a Bodega el ingrediente +
    /// cantidad actualmente cargados en el formulario, aunque todavía no se
    /// haya guardado como mercancía. Le permite al dueño registrar
    /// directamente en bodega sin pasar por el flujo de inventario activo.
    /// </summary>
    [RelayCommand]
    private async Task AgregarFormularioABodegaAsync()
    {
        if (IngredienteSeleccionado == null || Cantidad <= 0)
        {
            MessageBox.Show("Seleccione un ingrediente y cantidad válida antes de agregar a bodega.",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            db.BodegaItems.Add(new BodegaItem
            {
                FechaAgregado = DateTime.Now,
                Nombre = IngredienteSeleccionado.Nombre,
                Categoria = "Mercancía",
                Cantidad = Cantidad,
                UnidadMedida = IngredienteSeleccionado.UnidadMedida,
                Notas = string.IsNullOrWhiteSpace(NumeroFactura) ? Notas : $"Factura: {NumeroFactura}. {Notas}".TrimEnd('.', ' '),
                Origen = "Mercancía Nueva (formulario)",
                IngredienteId = IngredienteSeleccionado.Id
            });
            await db.SaveChangesAsync();
            BodegaNotifier.NotificarCambio();

            MessageBox.Show($"Agregado a bodega: {Cantidad:N2} {IngredienteSeleccionado.UnidadMedida} de {IngredienteSeleccionado.Nombre}",
                "Bodega", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "AgregarFormularioABodega (Mercancía Nueva)");
            MessageBox.Show($"No se pudo agregar a bodega.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Elimina un registro de mercancía y revierte su efecto sobre el stock
    /// del ingrediente.
    /// </summary>
    [RelayCommand]
    private async Task RemoverMercanciaAsync(MercanciaRecibida? mercancia)
    {
        if (mercancia == null) return;

        var nombreIng = mercancia.Ingrediente?.Nombre ?? "(ingrediente)";
        var resp = MessageBox.Show(
            $"¿Eliminar este registro de mercancía?\n\n" +
            $"  • Ingrediente: {nombreIng}\n" +
            $"  • Cantidad: {mercancia.Cantidad:N2}\n" +
            $"  • Proveedor: {mercancia.Proveedor}\n\n" +
            "Se restará esta cantidad del stock actual del ingrediente. Puede deshacerla con Ctrl+Z.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            var cmd = new EliminarMercanciaCommand
            {
                MercanciaId = mercancia.Id,
                IngredienteNombre = nombreIng,
                UnidadMedida = mercancia.Ingrediente?.UnidadMedida ?? string.Empty,
                Cantidad = mercancia.Cantidad
            };
            await _undoRedo.EjecutarAsync(cmd);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Remover Mercancía");
            MessageBox.Show($"No se pudo eliminar el registro.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (MercanciaEnEdicionId == mercancia.Id)
        {
            LimpiarFormulario();
        }

        CargarMercanciasHoy();
    }

    private void LimpiarFormulario()
    {
        IngredienteSeleccionado = null;
        Cantidad = 0;
        Proveedor = string.Empty;
        NumeroFactura = string.Empty;
        Notas = string.Empty;
        MercanciaEnEdicionId = null;
        CalcMercancia.Limpiar();
        RutaFactura = null;
        _rutaFacturaPendiente = null;
        OnPropertyChanged(nameof(TieneFacturaAdjunta));
        OnPropertyChanged(nameof(NombreFacturaAdjunta));
    }

    // ============================================================
    // GESTIÓN DE FACTURA DIGITAL (PDF / IMAGEN)
    // ============================================================

    [RelayCommand]
    private void SeleccionarFactura()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar factura digital",
            Filter = "Documentos e imágenes (*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff)|*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff|PDF (*.pdf)|*.pdf|Imágenes (*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var info = new FileInfo(dlg.FileName);
            if (info.Length > 20 * 1024 * 1024)
            {
                MessageBox.Show("El archivo supera los 20 MB. Use una versión más liviana de la factura.",
                    "Archivo muy grande", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _rutaFacturaPendiente = dlg.FileName;
            OnPropertyChanged(nameof(TieneFacturaAdjunta));
            OnPropertyChanged(nameof(NombreFacturaAdjunta));
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Seleccionar factura");
            MessageBox.Show($"No se pudo leer el archivo.\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void QuitarFactura()
    {
        _rutaFacturaPendiente = null;
        RutaFactura = null;
        OnPropertyChanged(nameof(TieneFacturaAdjunta));
        OnPropertyChanged(nameof(NombreFacturaAdjunta));
    }

    /// <summary>Abre la factura adjunta del formulario (si la hay) con el visor predeterminado del sistema.</summary>
    [RelayCommand]
    private void AbrirFacturaActual()
    {
        var ruta = _rutaFacturaPendiente ?? RutaFactura;
        if (string.IsNullOrWhiteSpace(ruta)) return;
        AbrirArchivo(ruta);
    }

    /// <summary>Abre la factura asociada a un registro específico de la lista.</summary>
    [RelayCommand]
    private void AbrirFactura(MercanciaRecibida? mercancia)
    {
        if (mercancia == null || string.IsNullOrWhiteSpace(mercancia.RutaFactura)) return;
        AbrirArchivo(mercancia.RutaFactura);
    }

    private static void AbrirArchivo(string ruta)
    {
        try
        {
            if (!File.Exists(ruta))
            {
                MessageBox.Show($"El archivo ya no existe en:\n{ruta}", "Archivo no encontrado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var visor = new ValeriosPizza.Windows.VisorFacturaWindow(ruta);
            var owner = System.Windows.Application.Current?.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive)
                ?? System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner != visor) visor.Owner = owner;
            visor.Show();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Abrir factura");
            MessageBox.Show($"No se pudo abrir el archivo.\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Copia el archivo seleccionado a la carpeta de facturas de la aplicación con un
    /// nombre único (timestamp + nombre original) y devuelve la ruta destino.
    /// </summary>
    private static string CopiarFacturaACarpeta(string rutaOrigen)
    {
        var nombreUnico = $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{Path.GetFileName(rutaOrigen)}";
        var destino = Path.Combine(CarpetaFacturas, nombreUnico);
        File.Copy(rutaOrigen, destino, overwrite: false);
        return destino;
    }

    // ============================================================
    // SOPORTE PARA "GUARDAR ANTES DE SALIR"
    // ============================================================

    /// <summary>
    /// Indica si el formulario tiene datos sin guardar.
    /// </summary>
    public bool TieneCambiosSinGuardar =>
        IngredienteSeleccionado != null || Cantidad > 0
        || !string.IsNullOrWhiteSpace(Proveedor)
        || !string.IsNullOrWhiteSpace(NumeroFactura)
        || !string.IsNullOrWhiteSpace(Notas);

    public IReadOnlyList<string> SeccionesPendientes()
        => TieneCambiosSinGuardar ? new[] { "Mercancía Nueva" } : Array.Empty<string>();

    /// <summary>
    /// Intenta guardar el formulario sin diálogos. Devuelve true si se guardó,
    /// false si los datos eran insuficientes o no había nada que guardar.
    /// </summary>
    public (bool ok, string? motivo) IntentarGuardarSilencioso()
    {
        if (!TieneCambiosSinGuardar) return (false, null);
        if (IngredienteSeleccionado == null || Cantidad <= 0)
            return (false, "falta ingrediente o cantidad");

        string? rutaCopia = null;
        try
        {
            using var db = _dbFactory.CreateDbContext();
            rutaCopia = !string.IsNullOrWhiteSpace(_rutaFacturaPendiente)
                ? CopiarFacturaACarpeta(_rutaFacturaPendiente!)
                : null;
            db.MercanciasRecibidas.Add(new MercanciaRecibida
            {
                Fecha = DateTime.Now,
                IngredienteId = IngredienteSeleccionado.Id,
                Cantidad = Cantidad,
                Proveedor = Proveedor,
                NumeroFactura = NumeroFactura,
                Notas = Notas,
                PrecioUnitario = CalcMercancia.PrecioUnitario,
                Impuesto = CalcMercancia.Impuesto,
                Retencion = CalcMercancia.Retencion,
                RutaFactura = rutaCopia
            });
            var ing = db.Ingredientes.Find(IngredienteSeleccionado.Id);
            if (ing != null)
            {
                ing.CantidadActual += Cantidad;
                ing.FechaActualizacion = DateTime.Now;
            }
            db.SaveChanges();
            LimpiarFormulario();
            return (true, null);
        }
        catch (Exception ex)
        {
            // Si la copia de la factura ya sucedió, borrarla para no
            // dejar archivos huérfanos cuando el save falla.
            if (rutaCopia != null) BorrarSiExiste(rutaCopia);
            App.GuardarErrorDump(ex, "Guardar Mercancía al cerrar");
            return (false, ex.Message);
        }
    }
}
