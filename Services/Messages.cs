namespace ValeriosPizza.Services;

/// <summary>
/// Mensaje publicado cuando se crea, edita, desactiva o elimina un ingrediente.
/// Los ViewModels que muestran listas de ingredientes deben registrarse con
/// <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> para
/// recargar sus datos cuando llegue este mensaje.
/// </summary>
public sealed record IngredientesChangedMessage;
