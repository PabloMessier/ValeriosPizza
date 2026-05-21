using System.Windows;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza.Windows;

/// <summary>
/// Diálogo para crear o editar un <see cref="Ingrediente"/>. Toda la lógica
/// de persistencia y validación vive en <see cref="IngredienteDialogViewModel"/>;
/// este code-behind sólo coordina el ciclo de vida de la ventana.
/// </summary>
public partial class IngredienteDialog : Window
{
    private readonly IngredienteDialogViewModel _vm;

    /// <summary>
    /// Ingrediente resultante (creado o editado). Disponible cuando DialogResult == true.
    /// </summary>
    public Ingrediente? IngredienteResultante { get; private set; }

    private IngredienteDialog(IngredienteDialogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => NombreTextBox.Focus();
    }

    public static IngredienteDialog ParaCrear(IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new IngredienteDialogViewModel(dbFactory));

    public static IngredienteDialog ParaEditar(
        Ingrediente existente,
        IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new IngredienteDialogViewModel(existente, dbFactory));

    private void CancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void GuardarClick(object sender, RoutedEventArgs e)
    {
        var resultado = await _vm.GuardarAsync();
        if (!resultado.Ok || resultado.Ingrediente == null)
        {
            // El VM dejó el motivo en MensajeError (binding en la UI).
            return;
        }

        IngredienteResultante = resultado.Ingrediente;
        var mensaje = _vm.EsEdicion
            ? $"Ingrediente \"{resultado.Ingrediente.Nombre}\" actualizado correctamente."
            : $"Ingrediente \"{resultado.Ingrediente.Nombre}\" guardado correctamente.";
        MessageBox.Show(this, mensaje, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }
}
