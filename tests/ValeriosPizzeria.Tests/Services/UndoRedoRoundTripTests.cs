using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo.Commands;
using Xunit;

namespace ValeriosPizza.Tests.Services;

/// <summary>
/// Cobertura de "Rehacer" (Ejecutar → Deshacer → Ejecutar otra vez) para
/// los comandos cuya reversión es más sutil. Aquí es donde aparecen los
/// bugs clásicos: IDs que cambian al reinsertar, snapshots que se
/// sobreescriben, contadores que duplican el delta, etc.
///
/// Solo cubrimos los comandos donde el redo no es trivial (Entrada,
/// Mercancía, EliminarMercancia, EliminarIngrediente, Conteo,
/// CambiarEstado). Para Gasto/Merma/Cortesía/Discos/Cajas el redo es
/// idéntico a la primera ejecución, así que los tests de
/// <c>UndoRedoCommandsTests</c> ya bastan.
/// </summary>
public class UndoRedoRoundTripTests
{
    [Fact]
    public async Task RegistrarEntrada_EjecutarDeshacerRehacer_NoDuplicaStock()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Harina", "kg", 10);

        var cmd = new RegistrarEntradaCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Harina", UnidadMedida = "kg", Cantidad = 5
        };
        await cmd.EjecutarAsync(fx);     // stock: 15
        await cmd.DeshacerAsync(fx);     // stock: 10
        await cmd.EjecutarAsync(fx);     // stock: 15 (no 20)

        await using var db = fx.CreateDbContext();
        var entradas = await db.Entradas.AsNoTracking().ToListAsync();
        Assert.Single(entradas);
        Assert.Equal(15, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
        // El nuevo Id se asigna al rehacer; el campo se actualiza para que
        // un siguiente Deshacer borre la fila correcta.
        Assert.Equal(entradas[0].Id, cmd.EntradaIdAsignado);
    }

    [Fact]
    public async Task RegistrarMercancia_EjecutarDeshacerRehacer_ConsistentEnStock()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Queso", "kg", 5);

        var cmd = new RegistrarMercanciaCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Queso", UnidadMedida = "kg",
            Cantidad = 3, Proveedor = "X"
        };
        await cmd.EjecutarAsync(fx);
        await cmd.DeshacerAsync(fx);
        await cmd.EjecutarAsync(fx);

        await using var db = fx.CreateDbContext();
        Assert.Single(await db.MercanciasRecibidas.AsNoTracking().ToListAsync());
        Assert.Equal(8, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
    }

    [Fact]
    public async Task EliminarMercancia_EjecutarDeshacerRehacer_TrackeaNuevoId()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Champiñón", "kg", 10);

        // Sembrar la mercancía vía su comando para tener Id real.
        var crear = new RegistrarMercanciaCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Champiñón", UnidadMedida = "kg",
            Cantidad = 4, Proveedor = "Y"
        };
        await crear.EjecutarAsync(fx);

        var eliminar = new EliminarMercanciaCommand { MercanciaId = crear.MercanciaIdAsignado };
        await eliminar.EjecutarAsync(fx);
        await eliminar.DeshacerAsync(fx);  // reinserta con nuevo Id

        // Al rehacer, el comando debe borrar usando el nuevo Id capturado
        // por el undo previo. Si no lo trackeara, el rehacer fallaría con
        // "ya no existe".
        await eliminar.EjecutarAsync(fx);

        await using var db = fx.CreateDbContext();
        Assert.Empty(await db.MercanciasRecibidas.AsNoTracking().ToListAsync());
        // Stock final: 10 (inicial) + 4 (crear) - 4 (eliminar) = 10
        Assert.Equal(10, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
    }

    [Fact]
    public async Task EliminarIngrediente_EjecutarDeshacerRehacer_FuncionaConNuevoId()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Orégano", "g", 100);

        var cmd = new EliminarIngredienteCommand { IngredienteId = ingId };
        await cmd.EjecutarAsync(fx);
        await cmd.DeshacerAsync(fx);  // reinserta; toma nuevo Id

        // Rehacer: debe borrar el reinsertado.
        await cmd.EjecutarAsync(fx);

        await using var db = fx.CreateDbContext();
        Assert.Empty(await db.Ingredientes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CambiarEstadoProducto_EjecutarDeshacerRehacer_PreservaEstadoAnterior()
    {
        using var fx = new InMemoryDbFixture();
        int prodId;
        await using (var db = fx.CreateDbContext())
        {
            var p = new Producto { Nombre = "Pizza X", Categoria = Categoria.Pizza, Estado = EstadoProducto.Activo };
            db.Productos.Add(p);
            await db.SaveChangesAsync();
            prodId = p.Id;
        }

        var cmd = new CambiarEstadoProductoCommand
        {
            ProductoId = prodId, ProductoNombre = "Pizza X",
            EstadoNuevo = EstadoProducto.Descontinuado
        };
        await cmd.EjecutarAsync(fx);
        Assert.Equal(EstadoProducto.Activo, cmd.EstadoAnterior);

        await cmd.DeshacerAsync(fx);
        // El estado anterior NO debe sobrescribirse al rehacer (sigue siendo
        // el original, no el "vuelto a poner" que es el mismo en este caso).
        await cmd.EjecutarAsync(fx);
        Assert.Equal(EstadoProducto.Activo, cmd.EstadoAnterior);

        await using var db2 = fx.CreateDbContext();
        Assert.Equal(EstadoProducto.Descontinuado,
            (await db2.Productos.AsNoTracking().FirstAsync(p => p.Id == prodId)).Estado);
    }

    [Fact]
    public async Task RegistrarConteo_RehacerNoSobreescribeSnapshotPrevio()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Sal", "kg", 5);

        // Sembrar un conteo previo de hoy para que la primera ejecución
        // capture un snapshot real.
        await using (var db = fx.CreateDbContext())
        {
            db.ConteosInventario.Add(new ConteoInventario
            {
                Fecha = DateTime.Now, Tipo = TipoConteo.Apertura, Notas = "previo",
                Lineas = { new ConteoInventarioLinea { IngredienteId = ingId, Cantidad = 4.0 } }
            });
            await db.SaveChangesAsync();
        }

        var cmd = new RegistrarConteoCommand
        {
            Tipo = TipoConteo.Apertura, Notas = "nuevo",
            Lineas = { new RegistrarConteoCommand.LineaDto { IngredienteId = ingId, Cantidad = 4.5 } }
        };
        await cmd.EjecutarAsync(fx);
        var snapshotCapturado = cmd.SnapshotAnterior;
        Assert.NotNull(snapshotCapturado);
        Assert.Equal("previo", snapshotCapturado!.Notas);

        await cmd.DeshacerAsync(fx);    // restaura "previo"
        await cmd.EjecutarAsync(fx);    // rehace: NO debe sobrescribir SnapshotAnterior con "previo" otra vez

        Assert.Same(snapshotCapturado, cmd.SnapshotAnterior);
        Assert.Equal("previo", cmd.SnapshotAnterior!.Notas);

        await using var db2 = fx.CreateDbContext();
        var conteoFinal = await db2.ConteosInventario.AsNoTracking().Include(c => c.Lineas).SingleAsync();
        Assert.Equal("nuevo", conteoFinal.Notas);
    }
}
