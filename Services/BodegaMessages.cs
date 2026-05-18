using CommunityToolkit.Mvvm.Messaging;

namespace ValeriosPizza.Services;

/// <summary>
/// Mensaje publicado cada vez que se agrega o elimina algo en la tabla
/// <c>BodegaItems</c>. Lo emiten las pantallas de Inventario / Mercancía
/// Nueva / Consulta tras un "Agregar a Bodega", y lo escucha
/// <c>BodegaViewModel</c> para refrescar su listado sin necesidad de
/// navegar.
/// </summary>
public sealed record BodegaChangedMessage;

/// <summary>
/// Helper estático para que los VMs no repitan la línea de
/// <c>WeakReferenceMessenger.Default.Send(new BodegaChangedMessage())</c>.
/// </summary>
public static class BodegaNotifier
{
    public static void NotificarCambio() =>
        WeakReferenceMessenger.Default.Send(new BodegaChangedMessage());
}
