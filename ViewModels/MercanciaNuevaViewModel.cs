using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// ViewModel del módulo "Mercancía Nueva": registro de recepción de
/// mercancía de proveedores, con soporte de factura adjunta (PDF/imagen)
/// y exportación a la pantalla Bodega.
///
/// Por tamaño la clase está dividida en archivos <c>partial</c>:
/// <list type="bullet">
///   <item><c>MercanciaNueva/MercanciaNuevaViewModel.Guardar.cs</c>: crear/editar/eliminar.</item>
///   <item><c>MercanciaNueva/MercanciaNuevaViewModel.Bodega.cs</c>: comandos hacia Bodega.</item>
///   <item><c>MercanciaNueva/MercanciaNuevaViewModel.Factura.cs</c>: gestión del archivo de factura.</item>
/// </list>
/// Este archivo contiene el estado compartido y el flujo silencioso de
/// "Guardar al cerrar".
/// </summary>
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
            m.Ingrediente = db.Ingredientes.Find(m.IngredienteId);
            MercanciasHoy.Add(m);
        }
    }

    // ============================================================
    // SOPORTE PARA "GUARDAR ANTES DE SALIR"
    // ============================================================

    /// <summary>Indica si el formulario tiene datos sin guardar.</summary>
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
