using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Services;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Comandos para enviar mercancía a la pantalla "Bodega" (almacén general).
/// Solo crean filas en <c>BodegaItems</c>; no afectan el stock activo ni el
/// registro original de mercancía recibida.
/// </summary>
public partial class MercanciaNuevaViewModel
{
    /// <summary>
    /// Envía esta fila de mercancía recibida a la pantalla "Bodega". Se
    /// usa cuando el dueño quiere separar de la circulación del día los
    /// items recibidos para llevarlos al almacén general.
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
    /// cantidad actualmente cargados, aunque todavía no se haya guardado
    /// como mercancía recibida.
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
}
