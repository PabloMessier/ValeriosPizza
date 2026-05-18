using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ValeriosPizza.Models;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// ViewModel para el diálogo IngredienteDialog. Soporta crear y editar.
/// Las cantidades se exponen como string para tolerar coma o punto decimal
/// independientemente de la cultura del SO (en máquinas con configuración
/// regional en español, el parseo automático de WPF rechazaba "5.5" y dejaba
/// el campo en cero sin avisar al usuario).
/// </summary>
public partial class IngredienteDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _titulo = "Nuevo Ingrediente";

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _unidadMedida = "kg";

    [ObservableProperty]
    private string _cantidadActualTexto = "0";

    [ObservableProperty]
    private string _cantidadMinimaTexto = "0";

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    public double CantidadActualValor { get; private set; }
    public double CantidadMinimaValor { get; private set; }

    public bool EsEdicion { get; }
    public int? IngredienteId { get; }

    /// <summary>
    /// Unidades sugeridas en el ComboBox.
    /// </summary>
    public string[] UnidadesSugeridas { get; } = { "kg", "g", "litros", "ml", "unidades" };

    public IngredienteDialogViewModel()
    {
    }

    public IngredienteDialogViewModel(Ingrediente existente)
    {
        EsEdicion = true;
        IngredienteId = existente.Id;
        Titulo = "Editar Ingrediente";
        Nombre = existente.Nombre;
        UnidadMedida = existente.UnidadMedida;
        CantidadActualValor = existente.CantidadActual;
        CantidadMinimaValor = existente.CantidadMinima;
        CantidadActualTexto = existente.CantidadActual.ToString(CultureInfo.InvariantCulture);
        CantidadMinimaTexto = existente.CantidadMinima.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Valida los campos. Devuelve true si todo está OK.
    /// </summary>
    public bool Validar()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MensajeError = "El nombre es obligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(UnidadMedida))
        {
            MensajeError = "La unidad de medida es obligatoria.";
            return false;
        }

        if (!TryParseNumero(CantidadActualTexto, out var actual) || actual < 0)
        {
            MensajeError = "La cantidad actual debe ser un número válido (no negativo).";
            return false;
        }

        if (!TryParseNumero(CantidadMinimaTexto, out var minima) || minima < 0)
        {
            MensajeError = "La cantidad mínima debe ser un número válido (no negativo).";
            return false;
        }

        CantidadActualValor = actual;
        CantidadMinimaValor = minima;
        MensajeError = string.Empty;
        return true;
    }

    /// <summary>
    /// Parseo tolerante a la cultura: acepta tanto coma como punto decimal
    /// y descarta separadores de miles. Esto evita la trampa clásica de
    /// teclear "5.5" en una máquina con locale es-XX y que el binding deje
    /// silenciosamente el valor en cero.
    /// </summary>
    private static bool TryParseNumero(string? texto, out double valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return true; // un campo vacío equivale a 0
        }

        var normalizado = texto.Trim().Replace(",", ".");
        return double.TryParse(
            normalizado,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out valor);
    }
}
