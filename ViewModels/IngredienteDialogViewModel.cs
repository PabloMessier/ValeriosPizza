using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// ViewModel para el diálogo IngredienteDialog. Soporta crear y editar.
/// Las cantidades se exponen como string para tolerar coma o punto decimal
/// independientemente de la cultura del SO (en máquinas con configuración
/// regional en español, el parseo automático de WPF rechazaba "5.5" y dejaba
/// el campo en cero sin avisar al usuario).
///
/// Además del estado del formulario, el VM encapsula la persistencia
/// (<see cref="GuardarAsync"/>) para que el code-behind del diálogo se
/// limite a coordinar el ciclo de vida de la ventana.
/// </summary>
public partial class IngredienteDialogViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext>? _dbFactory;
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

    /// <summary>
    /// Constructor sin parámetros para compatibilidad con código existente
    /// y para tests del comportamiento de validación pura.
    /// </summary>
    public IngredienteDialogViewModel() { }

    public IngredienteDialogViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public IngredienteDialogViewModel(Ingrediente existente)
        : this(existente, dbFactory: null) { }

    public IngredienteDialogViewModel(Ingrediente existente, IDbContextFactory<PizzeriaDbContext>? dbFactory)
    {
        _dbFactory = dbFactory;
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
    /// Resultado de <see cref="GuardarAsync"/>: indica si se persistió y,
    /// en caso afirmativo, devuelve la entidad creada/editada.
    /// Cuando falla, el motivo queda expuesto en
    /// <see cref="MensajeError"/> para que la UI lo muestre.
    /// </summary>
    public sealed record GuardarResultado(bool Ok, Ingrediente? Ingrediente);

    /// <summary>
    /// Persiste el ingrediente (crear o editar). Valida primero, luego
    /// comprueba unicidad case-insensitive del nombre y aplica el cambio.
    /// La transacción implícita de <c>SaveChangesAsync</c> garantiza
    /// atomicidad.
    /// </summary>
    public async Task<GuardarResultado> GuardarAsync()
    {
        if (_dbFactory == null)
        {
            MensajeError = "Error interno: no se configuró el acceso a datos.";
            return new GuardarResultado(false, null);
        }
        if (!Validar()) return new GuardarResultado(false, null);

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Validar unicidad del nombre (case-insensitive, ignorando el
            // propio registro si es edición). SQLite LIKE es
            // case-insensitive para ASCII por defecto, suficiente para
            // nombres de ingredientes.
            var nombreNormalizado = Nombre.Trim();
            var conflicto = await db.Ingredientes.AsNoTracking().AnyAsync(i =>
                EF.Functions.Like(i.Nombre, nombreNormalizado)
                && (!IngredienteId.HasValue || i.Id != IngredienteId.Value));

            if (conflicto)
            {
                MensajeError = "Ya existe un ingrediente con ese nombre.";
                return new GuardarResultado(false, null);
            }

            Ingrediente ingrediente;
            if (EsEdicion && IngredienteId.HasValue)
            {
                ingrediente = await db.Ingredientes.FindAsync(IngredienteId.Value)
                    ?? throw new InvalidOperationException(
                        $"No se encontró el ingrediente con id {IngredienteId.Value}.");
                ingrediente.Nombre = nombreNormalizado;
                ingrediente.UnidadMedida = UnidadMedida.Trim();
                ingrediente.CantidadActual = CantidadActualValor;
                ingrediente.CantidadMinima = CantidadMinimaValor;
                ingrediente.FechaActualizacion = DateTime.Now;
            }
            else
            {
                ingrediente = new Ingrediente
                {
                    Nombre = nombreNormalizado,
                    UnidadMedida = UnidadMedida.Trim(),
                    CantidadActual = CantidadActualValor,
                    CantidadMinima = CantidadMinimaValor,
                    FechaActualizacion = DateTime.Now,
                    Activo = true
                };
                db.Ingredientes.Add(ingrediente);
            }

            await db.SaveChangesAsync();
            return new GuardarResultado(true, ingrediente);
        }
        catch (DbUpdateException ex)
        {
            MensajeError = $"Error al guardar: {ex.InnerException?.Message ?? ex.Message}";
            return new GuardarResultado(false, null);
        }
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
