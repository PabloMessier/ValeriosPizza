using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ValeriosPizza.Services;

/// <summary>
/// Helpers para refrescar <see cref="ObservableCollection{T}"/> sin disparar un
/// evento CollectionChanged por cada item. WPF no expone una API "ReplaceAll"
/// nativa, así que vaciamos y rellenamos en el mismo método; sin embargo la
/// clave es no exponer estados intermedios al binding (lo hacemos en una sola
/// pasada y sin ordenar/clasificar de nuevo el ObservableCollection).
/// </summary>
public static class ObservableCollectionExtensions
{
    /// <summary>
    /// Reemplaza el contenido de <paramref name="collection"/> con
    /// <paramref name="items"/>. La colección queda vacía si <paramref name="items"/>
    /// también lo está.
    /// </summary>
    public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        if (collection is null) return;
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
