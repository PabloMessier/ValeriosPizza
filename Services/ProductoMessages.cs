using CommunityToolkit.Mvvm.Messaging;

namespace ValeriosPizza.Services;

/// <summary>
/// Mensaje publicado cuando se crea, edita o cambia el estado de un producto.
/// Los ViewModels que muestran listas de productos (Cortesía en Registro
/// Rápido, pestaña Productos en Inventario) deben registrarse con
/// <see cref="WeakReferenceMessenger"/> para recargar sus listas en cuanto
/// llegue este mensaje.
/// </summary>
public sealed record ProductosChangedMessage;

/// <summary>
/// Helper estático para emitir <see cref="ProductosChangedMessage"/> sin
/// repetir la línea del messenger en cada VM. Equivalente al patrón
/// <see cref="IngredientesNotifier"/>.
/// </summary>
public static class ProductosNotifier
{
    public static void NotificarCambio() =>
        WeakReferenceMessenger.Default.Send(new ProductosChangedMessage());
}
