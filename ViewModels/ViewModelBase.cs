using CommunityToolkit.Mvvm.ComponentModel;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Base común de todos los ViewModels. Hereda de
/// <see cref="ObservableValidator"/> en lugar de <see cref="ObservableObject"/>
/// para habilitar validación declarativa con <c>System.ComponentModel.DataAnnotations</c>
/// (atributos como <c>[Required]</c>, <c>[Range]</c>, etc.) y la API
/// <c>HasErrors</c> / <c>GetErrors</c> de <see cref="System.ComponentModel.INotifyDataErrorInfo"/>.
/// La conversión es no-rompedora: <c>ObservableValidator</c> hereda de
/// <c>ObservableObject</c>, así que cualquier VM existente sigue funcionando.
/// </summary>
public abstract class ViewModelBase : ObservableValidator
{
}
