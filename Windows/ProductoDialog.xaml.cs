using System.Windows;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza.Windows;

/// <summary>
/// Diálogo para crear o editar un producto del menú (pizzas, paninis,
/// discos). Replica el patrón de <see cref="IngredienteDialog"/>: ofrece
/// fábricas estáticas <c>ParaCrear</c> / <c>ParaEditar</c>, valida la
/// unicidad del nombre (case-insensitive) contra la BD y expone el
/// resultado a través de <see cref="ProductoResultante"/>.
/// </summary>
public partial class ProductoDialog : Window
{
    private readonly ProductoDialogViewModel _vm;
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;

    /// <summary>
    /// Producto creado o editado. Disponible cuando <c>DialogResult == true</c>.
    /// </summary>
    public Producto? ProductoResultante { get; private set; }

    private ProductoDialog(
        ProductoDialogViewModel vm,
        IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        InitializeComponent();
        _vm = vm;
        _dbFactory = dbFactory;
        DataContext = vm;
        Loaded += (_, _) => NombreTextBox.Focus();
    }

    /// <summary>Crea un diálogo en modo "nuevo producto".</summary>
    public static ProductoDialog ParaCrear(IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new ProductoDialogViewModel(), dbFactory);

    /// <summary>Crea un diálogo en modo "editar producto existente".</summary>
    public static ProductoDialog ParaEditar(
        Producto existente,
        IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new ProductoDialogViewModel(existente), dbFactory);

    private void CancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void GuardarClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.Validar())
        {
            return;
        }

        try
        {
            using var db = _dbFactory.CreateDbContext();

            // Validar unicidad del nombre (case-insensitive, ignorando el propio si edita).
            // Se usa EF.Functions.Like porque SQLite LIKE es case-insensitive
            // para ASCII por defecto, lo cual es suficiente para nombres de
            // productos del menú.
            var nombreNormalizado = _vm.Nombre.Trim();
            var conflicto = db.Productos.Any(p =>
                EF.Functions.Like(p.Nombre, nombreNormalizado)
                && (!_vm.ProductoId.HasValue || p.Id != _vm.ProductoId.Value));

            if (conflicto)
            {
                _vm.MensajeError = "Ya existe un producto con ese nombre.";
                return;
            }

            Producto producto;
            if (_vm.EsEdicion && _vm.ProductoId.HasValue)
            {
                // Find() siempre trackea (independiente del NoTracking default
                // del DbContext) porque consulta primero el ChangeTracker local.
                producto = db.Productos.Find(_vm.ProductoId.Value)
                    ?? throw new InvalidOperationException(
                        $"No se encontró el producto con id {_vm.ProductoId.Value}.");
                producto.Nombre = _vm.Nombre.Trim();
                producto.Categoria = _vm.CategoriaSeleccionada;
                producto.Estado = _vm.EstadoSeleccionado;
            }
            else
            {
                producto = new Producto
                {
                    Nombre = _vm.Nombre.Trim(),
                    Categoria = _vm.CategoriaSeleccionada,
                    Estado = _vm.EstadoSeleccionado
                };
                db.Productos.Add(producto);
            }

            db.SaveChanges();
            ProductoResultante = producto;

            DialogResult = true;
            Close();
        }
        catch (DbUpdateException ex)
        {
            _vm.MensajeError = $"Error al guardar: {ex.InnerException?.Message ?? ex.Message}";
        }
    }
}
