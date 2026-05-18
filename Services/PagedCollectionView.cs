using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ValeriosPizza.Services;

/// <summary>
/// Vista paginada genérica para colecciones grandes. Encapsula el "slice"
/// de la página visible sin modificar la colección fuente, de modo que las
/// ediciones realizadas dentro de la página (cuando T es referencia) se
/// propagan automáticamente al origen.
///
/// Soporta una colección fuente con <see cref="INotifyCollectionChanged"/>
/// (típicamente <see cref="ObservableCollection{T}"/>): si el origen cambia
/// la vista se recalcula y dispara los cambios necesarios. También acepta
/// listas estáticas; en ese caso debe llamarse <see cref="Refrescar"/>
/// manualmente cuando el contenido cambie.
///
/// El tamaño de página se selecciona desde <see cref="OpcionesTamanoPagina"/>;
/// el valor <see cref="MostrarTodos"/> (int.MaxValue) significa "sin paginar".
/// </summary>
public partial class PagedCollectionView<T> : ObservableObject
{
    /// <summary>Sentinela para mostrar todos los items sin paginar.</summary>
    public const int MostrarTodos = int.MaxValue;

    private IEnumerable<T> _source;
    private INotifyCollectionChanged? _notifier;

    [ObservableProperty]
    private int _tamanoPagina = 20;

    [ObservableProperty]
    private int _paginaActual = 1;

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private string _resumen = "0 items";

    /// <summary>Items pertenecientes a la página actual. Se reutiliza la
    /// misma instancia para que los bindings no se rompan.</summary>
    public ObservableCollection<T> Items { get; } = new();

    /// <summary>Opciones disponibles para el ComboBox de tamaño de página.</summary>
    public IReadOnlyList<OpcionTamano> OpcionesTamanoPagina { get; } = new OpcionTamano[]
    {
        new(10,  "10"),
        new(20,  "20"),
        new(30,  "30"),
        new(50,  "50"),
        new(100, "100"),
        new(MostrarTodos, "Todos"),
    };

    public IRelayCommand PrimeraCommand { get; }
    public IRelayCommand AnteriorCommand { get; }
    public IRelayCommand SiguienteCommand { get; }
    public IRelayCommand UltimaCommand { get; }

    public PagedCollectionView(IEnumerable<T> source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        PrimeraCommand   = new RelayCommand(() => PaginaActual = 1,                       () => PaginaActual > 1);
        AnteriorCommand  = new RelayCommand(() => PaginaActual = Math.Max(1, PaginaActual - 1),
                                            () => PaginaActual > 1);
        SiguienteCommand = new RelayCommand(() => PaginaActual = Math.Min(TotalPaginas, PaginaActual + 1),
                                            () => PaginaActual < TotalPaginas);
        UltimaCommand    = new RelayCommand(() => PaginaActual = TotalPaginas,            () => PaginaActual < TotalPaginas);

        Suscribir(source);
        Refrescar();
    }

    /// <summary>
    /// Constructor para fuentes filtradas (típicamente <see cref="System.ComponentModel.ICollectionView"/>)
    /// donde el enumerable y la notificación de cambios vienen de objetos
    /// distintos. <paramref name="notifier"/> normalmente es el mismo objeto
    /// que <paramref name="source"/> casteado a <see cref="INotifyCollectionChanged"/>.
    /// </summary>
    public PagedCollectionView(IEnumerable<T> source, INotifyCollectionChanged notifier)
        : this(source)
    {
        if (_notifier == null && notifier != null)
        {
            _notifier = notifier;
            notifier.CollectionChanged += OnSourceChanged;
        }
    }

    /// <summary>
    /// Sustituye el origen. Útil cuando el ViewModel reasigna la colección
    /// fuente (por ejemplo, después de una recarga completa).
    /// </summary>
    public void CambiarOrigen(IEnumerable<T> nuevoOrigen)
    {
        Desuscribir();
        _source = nuevoOrigen ?? throw new ArgumentNullException(nameof(nuevoOrigen));
        Suscribir(_source);
        PaginaActual = 1;
        Refrescar();
    }

    /// <summary>Recalcula totales y rebana la página actual.</summary>
    public void Refrescar()
    {
        var lista = _source as IList<T> ?? _source.ToList();
        TotalItems = lista.Count;

        var tamano = TamanoPagina <= 0 ? 20 : TamanoPagina;
        TotalPaginas = tamano == MostrarTodos
            ? 1
            : Math.Max(1, (int)Math.Ceiling(TotalItems / (double)tamano));

        if (PaginaActual > TotalPaginas) PaginaActual = TotalPaginas;
        if (PaginaActual < 1) PaginaActual = 1;

        IEnumerable<T> slice;
        int desde, hasta;
        if (tamano == MostrarTodos)
        {
            slice = lista;
            desde = TotalItems == 0 ? 0 : 1;
            hasta = TotalItems;
        }
        else
        {
            var skip = (PaginaActual - 1) * tamano;
            slice = lista.Skip(skip).Take(tamano);
            desde = TotalItems == 0 ? 0 : skip + 1;
            hasta = Math.Min(skip + tamano, TotalItems);
        }

        Items.Clear();
        foreach (var item in slice) Items.Add(item);

        Resumen = TotalItems == 0
            ? "Sin resultados"
            : $"Mostrando {desde}–{hasta} de {TotalItems}";

        PrimeraCommand.NotifyCanExecuteChanged();
        AnteriorCommand.NotifyCanExecuteChanged();
        SiguienteCommand.NotifyCanExecuteChanged();
        UltimaCommand.NotifyCanExecuteChanged();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        Refrescar();
    }

    partial void OnPaginaActualChanged(int value) => Refrescar();

    private void Suscribir(IEnumerable<T> source)
    {
        if (source is INotifyCollectionChanged ncc)
        {
            _notifier = ncc;
            ncc.CollectionChanged += OnSourceChanged;
        }
    }

    private void Desuscribir()
    {
        if (_notifier != null)
        {
            _notifier.CollectionChanged -= OnSourceChanged;
            _notifier = null;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refrescar();

    public sealed record OpcionTamano(int Valor, string Etiqueta);
}
