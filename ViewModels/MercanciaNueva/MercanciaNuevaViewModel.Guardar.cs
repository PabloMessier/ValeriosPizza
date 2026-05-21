using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo.Commands;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Flujo de guardado/edición/eliminación de mercancías recibidas. La
/// creación pasa por <see cref="UndoRedoService"/>; la actualización
/// (con sus swaps de ingrediente y de factura) se mantiene fuera del
/// undo porque revertirla sería frágil.
/// </summary>
public partial class MercanciaNuevaViewModel
{
    [RelayCommand]
    private async Task GuardarMercanciaAsync()
    {
        if (IngredienteSeleccionado == null || Cantidad <= 0)
        {
            MessageBox.Show("Seleccione un ingrediente y cantidad válida", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Modo edición: la actualización de un registro existente NO pasa
        // por undo/redo (los swaps de archivos PDF y de ingredientes hacen
        // frágil la reversión). Mantenemos el flujo síncrono original.
        if (EnModoEdicion && MercanciaEnEdicionId.HasValue)
        {
            ActualizarMercanciaExistente();
            LimpiarFormulario();
            CargarMercanciasHoy();
            return;
        }

        // ===== Crear registro nuevo (con undo) =====
        var ing = IngredienteSeleccionado;
        string? rutaFinal = null;
        // Copiamos la factura a la carpeta interna ANTES del save porque la
        // ruta forma parte del registro a insertar. Si el save falla, hay
        // que borrar el archivo recién copiado para no acumular huérfanos.
        bool rutaFinalEsCopiaNueva = false;
        if (!string.IsNullOrWhiteSpace(_rutaFacturaPendiente))
        {
            rutaFinal = CopiarFacturaACarpeta(_rutaFacturaPendiente!);
            rutaFinalEsCopiaNueva = true;
        }
        else if (!string.IsNullOrWhiteSpace(RutaFactura))
        {
            rutaFinal = RutaFactura;
        }

        var cmd = new RegistrarMercanciaCommand
        {
            IngredienteId = ing.Id,
            IngredienteNombre = ing.Nombre,
            UnidadMedida = ing.UnidadMedida,
            Cantidad = Cantidad,
            Proveedor = Proveedor,
            NumeroFactura = NumeroFactura,
            Notas = Notas,
            PrecioUnitario = CalcMercancia.PrecioUnitario,
            Impuesto = CalcMercancia.Impuesto,
            Retencion = CalcMercancia.Retencion,
            RutaFactura = rutaFinal
        };
        try
        {
            await _undoRedo.EjecutarAsync(cmd);
        }
        catch
        {
            // Si el save falla y habíamos copiado la factura a la carpeta
            // interna, eliminamos el archivo huérfano para no llenar el disco.
            if (rutaFinalEsCopiaNueva && rutaFinal != null)
            {
                BorrarSiExiste(rutaFinal);
            }
            throw;
        }

        MessageBox.Show($"Mercancía registrada: {Cantidad} {ing.UnidadMedida} de {ing.Nombre}",
            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

        LimpiarFormulario();
        CargarMercanciasHoy();
    }

    /// <summary>
    /// Borra un archivo si existe, tragándose cualquier IOException para no
    /// enmascarar la excepción original que disparó la limpieza.
    /// </summary>
    private static void BorrarSiExiste(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); }
        catch { /* mejor esfuerzo */ }
    }

    /// <summary>
    /// Actualiza un registro existente sin pasar por la pila de undo. Esta
    /// rama mantiene el comportamiento original previo a undo/redo porque
    /// involucra revertir y reaplicar deltas en dos ingredientes posiblemente
    /// distintos más un swap de archivo PDF, lo cual es complejo de revertir.
    /// </summary>
    private void ActualizarMercanciaExistente()
    {
        using var db = _dbFactory.CreateDbContext();

        var existente = db.MercanciasRecibidas.Find(MercanciaEnEdicionId!.Value);
        if (existente == null)
        {
            MessageBox.Show("El registro ya no existe (puede haber sido eliminado).",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            CancelarEdicion();
            return;
        }

        var ingredienteAnterior = db.Ingredientes.Find(existente.IngredienteId);
        if (ingredienteAnterior != null)
        {
            ingredienteAnterior.CantidadActual -= existente.Cantidad;
            if (ingredienteAnterior.CantidadActual < 0) ingredienteAnterior.CantidadActual = 0;
            ingredienteAnterior.FechaActualizacion = DateTime.Now;
        }

        existente.IngredienteId = IngredienteSeleccionado!.Id;
        existente.Cantidad = Cantidad;
        existente.Proveedor = Proveedor;
        existente.NumeroFactura = NumeroFactura;
        existente.Notas = Notas;
        existente.PrecioUnitario = CalcMercancia.PrecioUnitario;
        existente.Impuesto = CalcMercancia.Impuesto;
        existente.Retencion = CalcMercancia.Retencion;
        // Si hay una factura pendiente la copiamos a la carpeta interna
        // antes del save; si el save falla, borramos la copia para no dejar
        // archivos huérfanos referenciados por nadie.
        string? rutaCopia = null;
        if (!string.IsNullOrWhiteSpace(_rutaFacturaPendiente))
        {
            rutaCopia = CopiarFacturaACarpeta(_rutaFacturaPendiente!);
            existente.RutaFactura = rutaCopia;
        }
        else
        {
            existente.RutaFactura = RutaFactura;
        }

        var ingredienteNuevo = db.Ingredientes.Find(IngredienteSeleccionado.Id);
        if (ingredienteNuevo != null)
        {
            ingredienteNuevo.CantidadActual += Cantidad;
            ingredienteNuevo.FechaActualizacion = DateTime.Now;
        }

        try
        {
            db.SaveChanges();
        }
        catch
        {
            if (rutaCopia != null) BorrarSiExiste(rutaCopia);
            throw;
        }

        MessageBox.Show($"Mercancía actualizada: {Cantidad} {IngredienteSeleccionado.UnidadMedida} de {IngredienteSeleccionado.Nombre}",
            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Carga el registro indicado en el formulario y entra en modo edición.
    /// </summary>
    [RelayCommand]
    private void EditarMercancia(MercanciaRecibida? mercancia)
    {
        if (mercancia == null) return;

        IngredienteSeleccionado = System.Linq.Enumerable.FirstOrDefault(Ingredientes, i => i.Id == mercancia.IngredienteId);
        Cantidad = mercancia.Cantidad;
        Proveedor = mercancia.Proveedor;
        NumeroFactura = mercancia.NumeroFactura;
        Notas = mercancia.Notas;
        CalcMercancia.PrecioUnitario = mercancia.PrecioUnitario;
        CalcMercancia.Impuesto = mercancia.Impuesto;
        CalcMercancia.Retencion = mercancia.Retencion;
        RutaFactura = mercancia.RutaFactura;
        _rutaFacturaPendiente = null;
        OnPropertyChanged(nameof(TieneFacturaAdjunta));
        OnPropertyChanged(nameof(NombreFacturaAdjunta));
        MercanciaEnEdicionId = mercancia.Id;
    }

    [RelayCommand]
    private void CancelarEdicion() => LimpiarFormulario();

    /// <summary>
    /// Elimina un registro de mercancía y revierte su efecto sobre el stock
    /// del ingrediente. Pasa por undo para que sea reversible.
    /// </summary>
    [RelayCommand]
    private async Task RemoverMercanciaAsync(MercanciaRecibida? mercancia)
    {
        if (mercancia == null) return;

        var nombreIng = mercancia.Ingrediente?.Nombre ?? "(ingrediente)";
        var resp = MessageBox.Show(
            $"¿Eliminar este registro de mercancía?\n\n" +
            $"  • Ingrediente: {nombreIng}\n" +
            $"  • Cantidad: {mercancia.Cantidad:N2}\n" +
            $"  • Proveedor: {mercancia.Proveedor}\n\n" +
            "Se restará esta cantidad del stock actual del ingrediente. Puede deshacerla con Ctrl+Z.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (resp != MessageBoxResult.Yes) return;

        try
        {
            var cmd = new EliminarMercanciaCommand
            {
                MercanciaId = mercancia.Id,
                IngredienteNombre = nombreIng,
                UnidadMedida = mercancia.Ingrediente?.UnidadMedida ?? string.Empty,
                Cantidad = mercancia.Cantidad
            };
            await _undoRedo.EjecutarAsync(cmd);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Remover Mercancía");
            MessageBox.Show($"No se pudo eliminar el registro.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (MercanciaEnEdicionId == mercancia.Id) LimpiarFormulario();
        CargarMercanciasHoy();
    }

    private void LimpiarFormulario()
    {
        IngredienteSeleccionado = null;
        Cantidad = 0;
        Proveedor = string.Empty;
        NumeroFactura = string.Empty;
        Notas = string.Empty;
        MercanciaEnEdicionId = null;
        CalcMercancia.Limpiar();
        RutaFactura = null;
        _rutaFacturaPendiente = null;
        OnPropertyChanged(nameof(TieneFacturaAdjunta));
        OnPropertyChanged(nameof(NombreFacturaAdjunta));
    }
}
