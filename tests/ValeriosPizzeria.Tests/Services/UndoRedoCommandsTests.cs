using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo.Commands;
using Xunit;

namespace ValeriosPizza.Tests.Services;

/// <summary>
/// Round-trip Ejecutar/Deshacer de los comandos UndoRedo restantes. Cada
/// test crea su propio <see cref="InMemoryDbFixture"/> para aislarse y
/// verifica las dos propiedades clave de un comando reversible:
/// <list type="number">
///   <item>Ejecutar produce el efecto esperado (insert + delta).</item>
///   <item>Deshacer deja el sistema en el estado anterior.</item>
/// </list>
/// </summary>
public class UndoRedoCommandsTests
{
    // ──────────────────────── Gasto ────────────────────────

    [Fact]
    public async Task RegistrarGasto_RoundTrip_RestauraStockExacto()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Tomate", "kg", 8);

        var cmd = new RegistrarGastoCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Tomate", UnidadMedida = "kg", Cantidad = 3
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            Assert.Single(await db.Gastos.AsNoTracking().ToListAsync());
            Assert.Equal(5, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
            Assert.Equal(3, cmd.CantidadDescontada);
        }

        await cmd.DeshacerAsync(fx);

        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.Gastos.AsNoTracking().ToListAsync());
        Assert.Equal(8, (await db2.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
    }

    [Fact]
    public async Task RegistrarGasto_RecortaACero_CuandoStockMenor()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Cebolla", "kg", 1);

        var cmd = new RegistrarGastoCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Cebolla", UnidadMedida = "kg", Cantidad = 5
        };
        await cmd.EjecutarAsync(fx);

        await using var db = fx.CreateDbContext();
        // El stock no puede ser negativo: la lógica recorta a 0 y guarda
        // cuánto descontó realmente (1, no 5) para reponer exacto en undo.
        Assert.Equal(0, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
        Assert.Equal(1, cmd.CantidadDescontada);
    }

    // ──────────────────────── Merma ────────────────────────

    [Fact]
    public async Task RegistrarMerma_RoundTrip()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Pepperoni", "kg", 4);

        var cmd = new RegistrarMermaCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Pepperoni", UnidadMedida = "kg",
            Cantidad = 1.5, Motivo = "Expirado"
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            Assert.Single(await db.Mermas.AsNoTracking().ToListAsync());
            Assert.Equal(2.5, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.Mermas.AsNoTracking().ToListAsync());
        Assert.Equal(4, (await db2.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
    }

    // ──────────────────────── Cortesía ────────────────────────

    [Fact]
    public async Task RegistrarCortesia_RoundTrip()
    {
        using var fx = new InMemoryDbFixture();
        var prodId = await SeedProductoAsync(fx, "Pizza Margarita", Categoria.Pizza);

        var cmd = new RegistrarCortesiaCommand
        {
            ProductoId = prodId, ProductoNombre = "Pizza Margarita", Cantidad = 2, Motivo = "Cliente VIP"
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            Assert.Single(await db.Cortesias.AsNoTracking().ToListAsync());
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.Cortesias.AsNoTracking().ToListAsync());
    }

    // ──────────────────────── Mercancía ────────────────────────

    [Fact]
    public async Task RegistrarMercancia_RoundTrip_SumaYRestauraStock()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Aceitunas", "kg", 2);

        var cmd = new RegistrarMercanciaCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Aceitunas", UnidadMedida = "kg",
            Cantidad = 6, Proveedor = "Distribuidora ACME"
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            Assert.Single(await db.MercanciasRecibidas.AsNoTracking().ToListAsync());
            Assert.Equal(8, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
            Assert.NotEqual(0, cmd.MercanciaIdAsignado);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.MercanciasRecibidas.AsNoTracking().ToListAsync());
        Assert.Equal(2, (await db2.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
    }

    // ──────────────────────── Discos ────────────────────────

    [Fact]
    public async Task RegistrarMovimientoDiscos_RoundTrip()
    {
        using var fx = new InMemoryDbFixture();

        var cmd = new RegistrarMovimientoDiscosCommand
        {
            CantidadInicial = 20, DiscosPreparados = 15, DiscosUtilizados = 10, DiscosMerma = 1, DiscosCortesia = 2
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            var reg = await db.InventarioDiscos.AsNoTracking().SingleAsync();
            Assert.Equal(15, reg.DiscosPreparados);
            Assert.Equal(reg.Id, cmd.RegistroIdAsignado);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.InventarioDiscos.AsNoTracking().ToListAsync());
    }

    // ──────────────────────── Cajas ────────────────────────

    [Fact]
    public async Task RegistrarMovimientoCajas_RoundTrip()
    {
        using var fx = new InMemoryDbFixture();

        var cmd = new RegistrarMovimientoCajasCommand
        {
            CantidadInicial = 50, CajasRecibidas = 30, CajasUtilizadas = 25, CajasMerma = 2
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            var reg = await db.InventarioCajas.AsNoTracking().SingleAsync();
            Assert.Equal(30, reg.CajasRecibidas);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.InventarioCajas.AsNoTracking().ToListAsync());
    }

    // ──────────────────────── Conteo (Apertura) ────────────────────────

    [Fact]
    public async Task RegistrarConteo_SinPrevio_GuardaConteoYLineas_YDeshaceLimpio()
    {
        using var fx = new InMemoryDbFixture();
        var ing1 = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Harina", "kg", 10);
        var ing2 = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Sal", "kg", 5);

        var cmd = new RegistrarConteoCommand
        {
            Tipo = TipoConteo.Apertura,
            Notas = "primer turno",
            Fecha = DateTime.Today.AddHours(8),
            Lineas =
            {
                new RegistrarConteoCommand.LineaDto { IngredienteId = ing1, Cantidad = 9.5 },
                new RegistrarConteoCommand.LineaDto { IngredienteId = ing2, Cantidad = 4.8 }
            }
        };

        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            var conteo = await db.ConteosInventario
                .AsNoTracking()
                .Include(c => c.Lineas)
                .SingleAsync();
            Assert.Equal(TipoConteo.Apertura, conteo.Tipo);
            Assert.Equal(2, conteo.Lineas.Count);
            Assert.Equal(conteo.Id, cmd.ConteoIdAsignado);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Empty(await db2.ConteosInventario.AsNoTracking().ToListAsync());
        Assert.Empty(await db2.ConteoInventarioLineas.AsNoTracking().ToListAsync());
    }

    // ──────────────────────── Eliminar Ingrediente ────────────────────────

    [Fact]
    public async Task EliminarIngrediente_RoundTrip_ReinsertaConMismosCampos()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Albahaca", "g", 250);

        var cmd = new EliminarIngredienteCommand { IngredienteId = ingId };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            Assert.Empty(await db.Ingredientes.AsNoTracking().ToListAsync());
            // El comando captura los datos al ejecutar para poder restaurar.
            Assert.Equal("Albahaca", cmd.Nombre);
            Assert.Equal(250, cmd.CantidadActual);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        var reinsertado = await db2.Ingredientes.AsNoTracking().SingleAsync();
        Assert.Equal("Albahaca", reinsertado.Nombre);
        Assert.Equal("g", reinsertado.UnidadMedida);
        Assert.Equal(250, reinsertado.CantidadActual);
    }

    // ──────────────────────── Eliminar Mercancía ────────────────────────

    [Fact]
    public async Task EliminarMercancia_RoundTrip()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await RegistrarEntradaCommandTests.SeedIngredienteAsync(fx, "Champiñón", "kg", 10);

        // Primero registramos una mercancía con su comando para tener algo que borrar.
        var registrar = new RegistrarMercanciaCommand
        {
            IngredienteId = ingId, IngredienteNombre = "Champiñón", UnidadMedida = "kg",
            Cantidad = 4, Proveedor = "Proveedor X"
        };
        await registrar.EjecutarAsync(fx);
        var mercanciaId = registrar.MercanciaIdAsignado;

        var eliminar = new EliminarMercanciaCommand { MercanciaId = mercanciaId };
        await eliminar.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            Assert.Empty(await db.MercanciasRecibidas.AsNoTracking().ToListAsync());
            // El stock debe haber bajado en la cantidad que tenía la mercancía.
            Assert.Equal(10, (await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
        }

        await eliminar.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        Assert.Single(await db2.MercanciasRecibidas.AsNoTracking().ToListAsync());
        Assert.Equal(14, (await db2.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
    }

    // ──────────────────────── CambiarEstadoProducto ────────────────────────

    [Fact]
    public async Task CambiarEstadoProducto_RoundTrip_GuardaEstadoAnterior()
    {
        using var fx = new InMemoryDbFixture();
        var prodId = await SeedProductoAsync(fx, "Panini Italiano", Categoria.Panini, EstadoProducto.Activo);

        var cmd = new CambiarEstadoProductoCommand
        {
            ProductoId = prodId, ProductoNombre = "Panini Italiano",
            EstadoNuevo = EstadoProducto.Agotado
        };
        await cmd.EjecutarAsync(fx);

        await using (var db = fx.CreateDbContext())
        {
            var prod = await db.Productos.AsNoTracking().FirstAsync(p => p.Id == prodId);
            Assert.Equal(EstadoProducto.Agotado, prod.Estado);
            Assert.Equal(EstadoProducto.Activo, cmd.EstadoAnterior);
        }

        await cmd.DeshacerAsync(fx);
        await using var db2 = fx.CreateDbContext();
        var restaurado = await db2.Productos.AsNoTracking().FirstAsync(p => p.Id == prodId);
        Assert.Equal(EstadoProducto.Activo, restaurado.Estado);
    }

    // ──────────────────────── helpers ────────────────────────

    private static async Task<int> SeedProductoAsync(
        InMemoryDbFixture fx, string nombre, Categoria categoria,
        EstadoProducto estado = EstadoProducto.Activo)
    {
        await using var db = fx.CreateDbContext();
        var p = new Producto { Nombre = nombre, Categoria = categoria, Estado = estado };
        db.Productos.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }
}
