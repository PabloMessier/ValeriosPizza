using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo.Commands;
using ValeriosPizza.Windows;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Línea editable de un conteo Apertura/Cierre. La cantidad la escribe la dueña.
/// </summary>
public partial class ConteoLineaVM : ObservableObject
{
    [ObservableProperty]
    private int _ingredienteId;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _unidadMedida = string.Empty;

    [ObservableProperty]
    private double _cantidad;
}

/// <summary>
/// Conteos de inventario Apertura y Cierre. Comparten la misma lógica de
/// guardado (con captura de snapshot del conteo previo para undo) y de
/// limpieza; la única diferencia es el <see cref="TipoConteo"/>.
/// </summary>
public partial class RegistroRapidoViewModel
{
    // ─────────────────────────── APERTURA ───────────────────────────

    [ObservableProperty]
    private ObservableCollection<ConteoLineaVM> _lineasApertura = new();

    [ObservableProperty]
    private string _notasApertura = string.Empty;

    // ─────────────────────────── CIERRE ───────────────────────────

    [ObservableProperty]
    private ObservableCollection<ConteoLineaVM> _lineasCierre = new();

    [ObservableProperty]
    private string _notasCierre = string.Empty;

    /// <summary>Vistas paginadas para no renderizar cientos de filas a la vez.</summary>
    public PagedCollectionView<ConteoLineaVM> LineasAperturaPaged { get; private set; } = null!;
    public PagedCollectionView<ConteoLineaVM> LineasCierrePaged   { get; private set; } = null!;

    partial void OnLineasAperturaChanged(ObservableCollection<ConteoLineaVM> value)
        => LineasAperturaPaged?.CambiarOrigen(value);

    partial void OnLineasCierreChanged(ObservableCollection<ConteoLineaVM> value)
        => LineasCierrePaged?.CambiarOrigen(value);

    private void InicializarPaginadosConteo()
    {
        LineasAperturaPaged = new PagedCollectionView<ConteoLineaVM>(LineasApertura);
        LineasCierrePaged   = new PagedCollectionView<ConteoLineaVM>(LineasCierre);
    }

    [RelayCommand]
    private Task GuardarConteoAperturaAsync() =>
        GuardarConteoAsync(TipoConteo.Apertura, LineasApertura, NotasApertura);

    [RelayCommand]
    private Task GuardarConteoCierreAsync() =>
        GuardarConteoAsync(TipoConteo.Cierre, LineasCierre, NotasCierre);

    private async Task GuardarConteoAsync(
        TipoConteo tipo, ObservableCollection<ConteoLineaVM> lineas, string notas)
    {
        if (lineas.Count == 0)
        {
            MessageBox.Show("No hay ingredientes en la lista. Agregue ingredientes antes de guardar.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Si ya existe uno para hoy del mismo tipo, preguntar antes de
        // reemplazarlo. El comando se encarga de capturar el snapshot
        // anterior para que el undo lo pueda restaurar.
        using (var db = _dbFactory.CreateDbContext())
        {
            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);
            var existente = db.ConteosInventario
                .FirstOrDefault(c => c.Tipo == tipo && c.Fecha >= hoy && c.Fecha < manana);
            if (existente != null)
            {
                var resp = MessageBox.Show(
                    $"Ya existe un Inventario {tipo.Mostrar()} registrado hoy. ¿Desea reemplazarlo?",
                    "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (resp != MessageBoxResult.Yes) return;
            }
        }

        var cmd = new RegistrarConteoCommand
        {
            Tipo = tipo,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
            Lineas = lineas.Select(l => new RegistrarConteoCommand.LineaDto
            {
                IngredienteId = l.IngredienteId,
                Cantidad = l.Cantidad
            }).ToList()
        };
        await _undoRedo.EjecutarAsync(cmd);

        MessageBox.Show($"Inventario {tipo.Mostrar()} guardado correctamente",
            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

        // Limpiar las cantidades pero conservar las filas (para próximo conteo).
        foreach (var l in lineas) l.Cantidad = 0;
        if (tipo == TipoConteo.Apertura) NotasApertura = string.Empty;
        else NotasCierre = string.Empty;
    }

    [RelayCommand]
    private void AgregarIngredienteApertura() => AbrirDialogoYAgregar(LineasApertura);

    [RelayCommand]
    private void AgregarIngredienteCierre() => AbrirDialogoYAgregar(LineasCierre);

    private void AbrirDialogoYAgregar(ObservableCollection<ConteoLineaVM> lineas)
    {
        var dialog = IngredienteDialog.ParaCrear(_dbFactory);
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true && dialog.IngredienteResultante != null)
        {
            lineas.Add(NuevaLineaConteo(dialog.IngredienteResultante));
            IngredientesNotifier.NotificarCambio();
        }
    }

    /// <summary>
    /// Borra DEFINITIVAMENTE el ingrediente representado por la línea de
    /// conteo. Si tiene historial registrado la operación se rechaza para
    /// no romper la integridad referencial.
    /// </summary>
    [RelayCommand]
    private void RemoverIngrediente(ConteoLineaVM? linea)
    {
        if (linea == null) return;

        using var db = _dbFactory.CreateDbContext();

        var ing = db.Ingredientes.Find(linea.IngredienteId);
        if (ing == null)
        {
            QuitarLineaDeAmbasListas(linea.IngredienteId);
            return;
        }

        var entradas = db.Entradas.Count(e => e.IngredienteId == ing.Id);
        var gastos = db.Gastos.Count(g => g.IngredienteId == ing.Id);
        var mermas = db.Mermas.Count(m => m.IngredienteId == ing.Id);
        var mercancias = db.MercanciasRecibidas.Count(m => m.IngredienteId == ing.Id);
        var lineasConteo = db.ConteoInventarioLineas.Count(l => l.IngredienteId == ing.Id);
        var totalHistorial = entradas + gastos + mermas + mercancias + lineasConteo;

        if (totalHistorial > 0)
        {
            MessageBox.Show(
                $"No se puede eliminar \"{ing.Nombre}\" porque tiene historial registrado:\n\n" +
                $"  • Entradas: {entradas}\n" +
                $"  • Gastos: {gastos}\n" +
                $"  • Mermas: {mermas}\n" +
                $"  • Mercancía recibida: {mercancias}\n" +
                $"  • Líneas de conteo: {lineasConteo}\n\n" +
                "Váyase a Inventario y use \"Desactivar\" para ocultarlo sin perder el historial.",
                "No se puede remover", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var resp = MessageBox.Show(
            $"¿Eliminar DEFINITIVAMENTE el ingrediente \"{ing.Nombre}\"?\n\nEsta acción no se puede deshacer.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            db.Ingredientes.Remove(ing);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Remover Ingrediente (Registro Rápido)");
            MessageBox.Show($"No se pudo eliminar el ingrediente.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        QuitarLineaDeAmbasListas(linea.IngredienteId);
        IngredientesNotifier.NotificarCambio();
    }

    private void QuitarLineaDeAmbasListas(int ingredienteId)
    {
        for (int i = LineasApertura.Count - 1; i >= 0; i--)
            if (LineasApertura[i].IngredienteId == ingredienteId) LineasApertura.RemoveAt(i);
        for (int i = LineasCierre.Count - 1; i >= 0; i--)
            if (LineasCierre[i].IngredienteId == ingredienteId) LineasCierre.RemoveAt(i);
    }

    private static ConteoLineaVM NuevaLineaConteo(Ingrediente ing) => new()
    {
        IngredienteId = ing.Id,
        Nombre = ing.Nombre,
        UnidadMedida = ing.UnidadMedida,
        Cantidad = 0
    };

    private void SincronizarConteoConIngredientes(ObservableCollection<ConteoLineaVM> lineas)
    {
        var idsExistentes = lineas.Select(l => l.IngredienteId).ToHashSet();
        foreach (var ing in Ingredientes)
        {
            if (!idsExistentes.Contains(ing.Id))
                lineas.Add(NuevaLineaConteo(ing));
        }
    }

    private void PrellenarConteosSiVacios(System.Collections.Generic.IEnumerable<Ingrediente> ingredientesActivos)
    {
        if (LineasApertura.Count == 0)
            foreach (var ing in ingredientesActivos) LineasApertura.Add(NuevaLineaConteo(ing));
        if (LineasCierre.Count == 0)
            foreach (var ing in ingredientesActivos) LineasCierre.Add(NuevaLineaConteo(ing));
    }

    private static bool TieneDatosConteo(ObservableCollection<ConteoLineaVM> lineas, string notas) =>
        (lineas?.Any(l => l.Cantidad > 0) ?? false) || !string.IsNullOrWhiteSpace(notas);

    private (bool ok, string? motivo) GuardarConteoSilencioso(
        TipoConteo tipo, ObservableCollection<ConteoLineaVM> lineas, string notas, Action limpiar)
    {
        if (lineas.Count == 0) return (false, "lista vacía");

        using var db = _dbFactory.CreateDbContext();
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);

        var existente = db.ConteosInventario
            .FirstOrDefault(c => c.Tipo == tipo && c.Fecha >= hoy && c.Fecha < manana);
        if (existente != null)
            return (false, $"ya existe un conteo de {tipo.Mostrar()} hoy; no se sobrescribió");

        db.ConteosInventario.Add(new ConteoInventario
        {
            Fecha = DateTime.Now, Tipo = tipo,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
            Lineas = lineas.Select(l => new ConteoInventarioLinea
            {
                IngredienteId = l.IngredienteId, Cantidad = l.Cantidad
            }).ToList()
        });
        db.SaveChanges();
        limpiar();
        return (true, null);
    }
}
