using System.Windows;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza.Windows;

public partial class IngredienteDialog : Window
{
    private readonly IngredienteDialogViewModel _vm;
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;

    /// <summary>
    /// Ingrediente resultante (creado o editado). Disponible cuando DialogResult == true.
    /// </summary>
    public Ingrediente? IngredienteResultante { get; private set; }

    private IngredienteDialog(
        IngredienteDialogViewModel vm,
        IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        InitializeComponent();
        _vm = vm;
        _dbFactory = dbFactory;
        DataContext = vm;
        Loaded += (_, _) => NombreTextBox.Focus();
    }

    /// <summary>
    /// Crea un diálogo en modo "nuevo ingrediente". El llamador debe pasar
    /// la factoría de contexto (inyectada por DI en el VM padre) para que
    /// el diálogo use la misma fuente de datos que el resto de la app.
    /// </summary>
    public static IngredienteDialog ParaCrear(IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new IngredienteDialogViewModel(), dbFactory);

    /// <summary>
    /// Crea un diálogo en modo "editar ingrediente existente".
    /// </summary>
    public static IngredienteDialog ParaEditar(
        Ingrediente existente,
        IDbContextFactory<PizzeriaDbContext> dbFactory) =>
        new(new IngredienteDialogViewModel(existente), dbFactory);

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
                // Find() siempre trackea (independiente del NoTracking default
                // del DbContext) porque consulta primero el ChangeTracker local.
                ingrediente = db.Ingredientes.Find(_vm.IngredienteId.Value)
                    ?? throw new InvalidOperationException(
                        $"No se encontró el ingrediente con id {_vm.IngredienteId.Value}.");
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
