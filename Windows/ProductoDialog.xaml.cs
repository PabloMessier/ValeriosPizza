using System.Windows;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza.Windows;

/// <summary>
/// Diálogo para crear o editar un <see cref="Producto"/>. Replica el patrón
/// de <see cref="IngredienteDialog"/>: la persistencia y validación viven
/// en <see cref="ProductoDialogViewModel"/>; el code-behind se limita a
/// orquestar la ventana.
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

    public static ProductoDialog ParaCrear(IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new ProductoDialogViewModel(dbFactory));

    public static ProductoDialog ParaEditar(
        Producto existente,
        IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new ProductoDialogViewModel(existente, dbFactory));

    private void CancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void GuardarClick(object sender, RoutedEventArgs e)
    {
        var resultado = await _vm.GuardarAsync();
        if (!resultado.Ok || resultado.Producto == null)
        {
            // El motivo queda en el binding de MensajeError.
            return;
        }

        ProductoResultante = resultado.Producto;
        DialogResult = true;
        Close();
    }
}
