using System;
using System.Globalization;
using System.Windows.Data;

namespace ValeriosPizza.Converters;

/// <summary>
/// Empaqueta los valores de una <see cref="System.Windows.Data.MultiBinding"/>
/// en un único <c>object[]</c>. Útil para enviar varios datos como
/// <c>CommandParameter</c> a un <see cref="System.Windows.Input.ICommand"/>
/// que sólo recibe un parámetro.
/// </summary>
public sealed class MultiValueArrayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Devolvemos una copia para que el ICommand no quede acoplado al
        // buffer interno reutilizado por el motor de binding.
        var copia = new object[values.Length];
        Array.Copy(values, copia, values.Length);
        return copia;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
