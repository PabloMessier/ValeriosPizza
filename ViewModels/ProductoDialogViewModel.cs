using CommunityToolkit.Mvvm.ComponentModel;
using ValeriosPizza.Models;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// ViewModel para el diálogo <c>ProductoDialog</c>. Soporta crear y editar
/// productos del menú (pizzas, paninis, discos). Mantiene un campo de error
/// observable para mostrarlo en la UI sin necesidad de un MessageBox aparte.
/// </summary>
public partial class ProductoDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _titulo = "Nuevo Producto";

    [ObservableProperty]
    private string _nombre = string.Empty;

    /// <summary>
    /// Categoría seleccionada. Se enlaza al ComboBox de categorías en el
    /// diálogo. Por defecto se inicializa a <see cref="Categoria.Pizza"/>
    /// porque es la categoría más usada en el menú.
    /// </summary>
    [ObservableProperty]
    private Categoria _categoriaSeleccionada = Categoria.Pizza;

    /// <summary>
    /// Estado operativo del producto (Activo / Agotado / Descontinuado).
    /// Se permite editarlo desde el diálogo para que la dueña pueda crear
    /// un producto directamente como agotado si fuese el caso.
    /// </summary>
    [ObservableProperty]
    private EstadoProducto _estadoSeleccionado = EstadoProducto.Activo;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    public bool EsEdicion { get; }
    public int? ProductoId { get; }

    /// <summary>Lista de categorías para el ComboBox del diálogo.</summary>
    public Categoria[] Categorias { get; } =
        { Categoria.Pizza, Categoria.Panini, Categoria.Disco };

    /// <summary>Lista de estados para el ComboBox del diálogo.</summary>
    public EstadoProducto[] Estados { get; } =
        { EstadoProducto.Activo, EstadoProducto.Agotado, EstadoProducto.Descontinuado };

    public ProductoDialogViewModel()
    {
    }

    public ProductoDialogViewModel(Producto existente)
    {
        EsEdicion = true;
        ProductoId = existente.Id;
        Titulo = "Editar Producto";
        Nombre = existente.Nombre;
        CategoriaSeleccionada = existente.Categoria;
        EstadoSeleccionado = existente.Estado;
    }

    /// <summary>
    /// Valida los campos. Devuelve true si todo está OK; si no, deja el
    /// motivo en <see cref="MensajeError"/> para que la UI lo muestre.
    /// </summary>
    public bool Validar()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MensajeError = "El nombre es obligatorio.";
            return false;
        }

        MensajeError = string.Empty;
        return true;
    }
}
