using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.Services;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// VM de la pantalla "Bodega": muestra todos los <see cref="BodegaItem"/>
/// almacenados (más recientes arriba), permite filtrar por nombre y borrar
/// filas individuales o todo el contenido.
///
/// La carga es perezosa para el primer arranque (se hace en cuanto se
/// construye el VM) y se refresca al recibir un
/// <see cref="BodegaChangedMessage"/> emitido desde otras pantallas tras
/// hacer "Agregar a Bodega".
/// </summary>
public partial class BodegaViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;

    [ObservableProperty]
    private ObservableCollection<BodegaItem> _items = new();

    /// <summary>Texto de búsqueda (filtra por nombre y categoría, case-insensitive).</summary>
    [ObservableProperty]
    private string _filtro = string.Empty;

    /// <summary>
    /// Vista filtrable de <see cref="Items"/>. La XAML enlaza esta propiedad
    /// para que la búsqueda actúe sin reordenar la colección.
    /// </summary>
    public ICollectionView ItemsView { get; }

    /// <summary>Vista paginada sobre <see cref="ItemsView"/> para que la
    /// bodega no muestre cientos de filas a la vez.</summary>
    public PagedCollectionView<BodegaItem> ItemsPaged { get; }

    /// <summary>Total de filas actualmente en bodega (para mostrar en la cabecera).</summary>
    public int Total => Items.Count;

    public BodegaViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;

        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = obj =>
            obj is BodegaItem b &&
            (string.IsNullOrWhiteSpace(Filtro) ||
             b.Nombre.Contains(Filtro, System.StringComparison.OrdinalIgnoreCase) ||
             (b.Categoria ?? string.Empty).Contains(Filtro, System.StringComparison.OrdinalIgnoreCase));

        ItemsPaged = new PagedCollectionView<BodegaItem>(
            ItemsView.Cast<BodegaItem>(),
            (System.Collections.Specialized.INotifyCollectionChanged)ItemsView);

        // Cargar al construir. Cualquier excepción se vuelca al log global.
        _ = CargarAsync();

        // Escuchar cambios desde otras pantallas.
        WeakReferenceMessenger.Default.Register<BodegaChangedMessage>(
            this, (_, _) => _ = CargarAsync());
    }

    partial void OnFiltroChanged(string value) => ItemsView.Refresh();

    /// <summary>Recarga el listado desde la base de datos.</summary>
    public async Task CargarAsync()
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var data = await db.BodegaItems
                .OrderByDescending(b => b.FechaAgregado)
                .ToListAsync();

            Items.Clear();
            foreach (var item in data) Items.Add(item);

            OnPropertyChanged(nameof(Total));
        }
        catch (System.Exception ex)
        {
            App.GuardarErrorDump(ex, "BodegaViewModel.CargarAsync");
        }
    }

    /// <summary>Quita una fila concreta de la bodega (pide confirmación).</summary>
    [RelayCommand]
    private async Task RemoverItemAsync(BodegaItem? item)
    {
        if (item == null) return;

        var resp = MessageBox.Show(
            $"¿Quitar de la bodega \"{item.Nombre}\" ({item.Cantidad:N2} {item.UnidadMedida})?",
            "Confirmar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var existente = await db.BodegaItems.FindAsync(item.Id);
            if (existente != null)
            {
                db.BodegaItems.Remove(existente);
                await db.SaveChangesAsync();
            }
            await CargarAsync();
        }
        catch (System.Exception ex)
        {
            App.GuardarErrorDump(ex, "BodegaViewModel.RemoverItemAsync");
            MessageBox.Show($"No se pudo quitar el item.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Vacía la bodega completa (doble confirmación).</summary>
    [RelayCommand]
    private async Task VaciarBodegaAsync()
    {
        if (Items.Count == 0)
        {
            MessageBox.Show("La bodega ya está vacía.", "Bodega",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var resp = MessageBox.Show(
            $"Esta acción eliminará TODAS las {Items.Count} fila(s) de la bodega.\n\n" +
            "Esta operación no se puede deshacer.\n\n¿Continuar?",
            "Vaciar bodega",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            await db.BodegaItems.ExecuteDeleteAsync();
            await CargarAsync();
        }
        catch (System.Exception ex)
        {
            App.GuardarErrorDump(ex, "BodegaViewModel.VaciarBodegaAsync");
            MessageBox.Show($"No se pudo vaciar la bodega.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Botón de refresco manual en la barra superior.</summary>
    [RelayCommand]
    private Task RefrescarAsync() => CargarAsync();
}
