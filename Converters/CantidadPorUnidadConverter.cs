using System;
using System.Globalization;
using System.Windows.Data;

namespace ValeriosPizza.Converters;

/// <summary>
/// MultiValueConverter que formatea una cantidad numérica adaptándose a la
/// unidad de medida: las unidades discretas (p. ej. "unidades", "uds")
/// se muestran sin decimales; el resto se muestra con dos decimales.
/// Espera dos valores: [0] cantidad (double/decimal), [1] unidad (string).
/// </summary>
public sealed class CantidadPorUnidadConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2 || values[0] is null)
        {
            return string.Empty;
        }

        double cantidad;
        try
        {
            cantidad = System.Convert.ToDouble(values[0], culture);
        }
        catch
        {
            return values[0]?.ToString() ?? string.Empty;
        }

        var unidad = values[1] as string ?? string.Empty;
        return EsUnidadDiscreta(unidad)
            ? cantidad.ToString("N0", culture)
            : cantidad.ToString("N2", culture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static bool EsUnidadDiscreta(string unidad)
    {
        if (string.IsNullOrWhiteSpace(unidad))
        {
            return false;
        }

        var u = unidad.Trim().ToLowerInvariant();
        return u is "unidades" or "unidad" or "uds" or "ud" or "u";
    }
}
