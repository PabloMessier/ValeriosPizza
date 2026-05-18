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

    /// <summary>
    /// Producto creado o editado. Disponible cuando <c>DialogResult == true</c>.
    /// </summary>
    public Producto? ProductoResultante { get; private set; }

    private ProductoDialog(ProductoDialogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => NombreTextBox.Focus();
    }

    /// <summary>Crea un diálogo en modo "nuevo producto".</summary>
    public static ProductoDialog ParaCrear() => new(new ProductoDialogViewModel());

    /// <summary>Crea un diálogo en modo "editar producto existente".</summary>
    public static ProductoDialog ParaEditar(Producto existente) =>
        new(new ProductoDialogViewModel(existente));

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
            using var db = new PizzeriaDbContext();

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
                producto = db.Productos.First(p => p.Id == _vm.ProductoId.Value);
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
