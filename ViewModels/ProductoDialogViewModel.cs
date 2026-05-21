using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// ViewModel para el diálogo <c>ProductoDialog</c>. Soporta crear y editar
/// productos del menú (pizzas, paninis, discos). Mantiene un campo de error
/// observable para mostrarlo en la UI sin necesidad de un MessageBox aparte.
/// Encapsula además la persistencia (<see cref="GuardarAsync"/>) para que
/// el code-behind del diálogo solo orqueste la ventana.
/// </summary>
public partial class ProductoDialogViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext>? _dbFactory;
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

    public ProductoDialogViewModel() { }

    public ProductoDialogViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public ProductoDialogViewModel(Producto existente)
        : this(existente, dbFactory: null) { }

    public ProductoDialogViewModel(Producto existente, IDbContextFactory<PizzeriaDbContext>? dbFactory)
    {
        _dbFactory = dbFactory;
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

    public sealed record GuardarResultado(bool Ok, Producto? Producto);

    /// <summary>
    /// Persiste el producto (crear o editar). Valida unicidad del nombre
    /// case-insensitive antes de aplicar el cambio.
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

            var nombreNormalizado = Nombre.Trim();
            var conflicto = await db.Productos.AsNoTracking().AnyAsync(p =>
                EF.Functions.Like(p.Nombre, nombreNormalizado)
                && (!ProductoId.HasValue || p.Id != ProductoId.Value));

            if (conflicto)
            {
                MensajeError = "Ya existe un producto con ese nombre.";
                return new GuardarResultado(false, null);
            }

            Producto producto;
            if (EsEdicion && ProductoId.HasValue)
            {
                producto = await db.Productos.FindAsync(ProductoId.Value)
                    ?? throw new InvalidOperationException(
                        $"No se encontró el producto con id {ProductoId.Value}.");
                producto.Nombre = nombreNormalizado;
                producto.Categoria = CategoriaSeleccionada;
                producto.Estado = EstadoSeleccionado;
            }
            else
            {
                producto = new Producto
                {
                    Nombre = nombreNormalizado,
                    Categoria = CategoriaSeleccionada,
                    Estado = EstadoSeleccionado
                };
                db.Productos.Add(producto);
            }

            await db.SaveChangesAsync();
            return new GuardarResultado(true, producto);
        }
        catch (DbUpdateException ex)
        {
            MensajeError = $"Error al guardar: {ex.InnerException?.Message ?? ex.Message}";
            return new GuardarResultado(false, null);
        }
    }
}
