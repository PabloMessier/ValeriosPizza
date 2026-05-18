using System;
using System.Globalization;
using System.Windows.Data;

namespace ValeriosPizza.Converters;

/// <summary>
/// Devuelve true si todos los valores recibidos por la MultiBinding son
/// iguales (reference o Equals). Pensado para resaltar el botón de
/// navegación cuyo Tag coincide con CurrentView del MainWindowViewModel.
/// </summary>
public sealed class EqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2)
        {
            return false;
        }

        var first = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (!Equals(first, values[i]))
            {
                return false;
            }
        }
        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
