using CommunityToolkit.Mvvm.Messaging;

namespace ValeriosPizza.Services;

/// <summary>
/// Adaptador histórico que ahora delega en
/// <see cref="WeakReferenceMessenger"/>. La API se conserva para no romper
/// llamadores existentes; los nuevos suscriptores deben registrarse
/// directamente con el messenger usando <see cref="IngredientesChangedMessage"/>
/// para evitar fugas de memoria asociadas al patrón event += handler estático.
/// </summary>
public static class IngredientesNotifier
{
    /// <summary>
    /// Publica un <see cref="IngredientesChangedMessage"/> a todos los
    /// suscriptores registrados con WeakReferenceMessenger.
    /// </summary>
    public static void NotificarCambio()
    {
        WeakReferenceMessenger.Default.Send(new IngredientesChangedMessage());
    }
}
