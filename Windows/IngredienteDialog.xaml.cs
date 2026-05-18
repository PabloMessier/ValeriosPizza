using System.Windows;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza.Windows;

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

    /// <summary>
    /// Crea un diálogo en modo "nuevo ingrediente".
    /// </summary>
    public static IngredienteDialog ParaCrear() => new(new IngredienteDialogViewModel());

    /// <summary>
    /// Crea un diálogo en modo "editar ingrediente existente".
    /// </summary>
    public static IngredienteDialog ParaEditar(Ingrediente existente) =>
        new(new IngredienteDialogViewModel(existente));

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
            // Se usa EF.Functions.Like porque el proveedor de SQLite no traduce
            // string.Equals(..., StringComparison.OrdinalIgnoreCase). LIKE en SQLite
            // es case-insensitive para ASCII por defecto, que es lo que necesitamos
            // para nombres de ingredientes.
            var nombreNormalizado = _vm.Nombre.Trim();
            var conflicto = db.Ingredientes.Any(i =>
                EF.Functions.Like(i.Nombre, nombreNormalizado)
                && (!_vm.IngredienteId.HasValue || i.Id != _vm.IngredienteId.Value));

            if (conflicto)
            {
                _vm.MensajeError = "Ya existe un ingrediente con ese nombre.";
                return;
            }

            Ingrediente ingrediente;
            if (_vm.EsEdicion && _vm.IngredienteId.HasValue)
            {
                ingrediente = db.Ingredientes.First(i => i.Id == _vm.IngredienteId.Value);
                ingrediente.Nombre = _vm.Nombre.Trim();
                ingrediente.UnidadMedida = _vm.UnidadMedida.Trim();
                ingrediente.CantidadActual = _vm.CantidadActualValor;
                ingrediente.CantidadMinima = _vm.CantidadMinimaValor;
                ingrediente.FechaActualizacion = System.DateTime.Now;
            }
            else
            {
                ingrediente = new Ingrediente
                {
                    Nombre = _vm.Nombre.Trim(),
                    UnidadMedida = _vm.UnidadMedida.Trim(),
                    CantidadActual = _vm.CantidadActualValor,
                    CantidadMinima = _vm.CantidadMinimaValor,
                    FechaActualizacion = System.DateTime.Now,
                    Activo = true
                };
                db.Ingredientes.Add(ingrediente);
            }

            db.SaveChanges();
            IngredienteResultante = ingrediente;

            var mensaje = _vm.EsEdicion
                ? $"Ingrediente \"{ingrediente.Nombre}\" actualizado correctamente."
                : $"Ingrediente \"{ingrediente.Nombre}\" guardado correctamente.";
            MessageBox.Show(this, mensaje, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (DbUpdateException ex)
        {
            _vm.MensajeError = $"Error al guardar: {ex.InnerException?.Message ?? ex.Message}";
        }
    }
}
