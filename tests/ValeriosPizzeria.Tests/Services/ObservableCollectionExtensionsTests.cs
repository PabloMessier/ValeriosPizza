using System.Collections.ObjectModel;
using ValeriosPizza.Services;
using Xunit;

namespace ValeriosPizza.Tests.Services;

public class ObservableCollectionExtensionsTests
{
    [Fact]
    public void ReplaceAll_ReemplazaTodoElContenido()
    {
        var coll = new ObservableCollection<int> { 1, 2, 3 };

        coll.ReplaceAll(new[] { 9, 8 });

        Assert.Equal(new[] { 9, 8 }, coll);
    }

    [Fact]
    public void ReplaceAll_ConIterableVacio_DejaColeccionVacia()
    {
        var coll = new ObservableCollection<string> { "a", "b" };

        coll.ReplaceAll(System.Array.Empty<string>());

        Assert.Empty(coll);
    }

    [Fact]
    public void ReplaceAll_DispararCollectionChangedAlMenosUnaVez()
    {
        var coll = new ObservableCollection<int> { 1, 2, 3 };
        int eventos = 0;
        coll.CollectionChanged += (_, _) => eventos++;

        coll.ReplaceAll(new[] { 4, 5 });

        // Aceptamos varios disparos (Clear + N Adds) pero al menos uno debe
        // haber llegado para que el binding refresque.
        Assert.True(eventos > 0);
    }
}
