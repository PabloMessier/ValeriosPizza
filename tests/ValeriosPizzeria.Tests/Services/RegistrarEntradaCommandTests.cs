using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo.Commands;
using Xunit;

namespace ValeriosPizza.Tests.Services;

/// <summary>
/// Round-trip Ejecutar/Deshacer de <see cref="RegistrarEntradaCommand"/>.
/// </summary>
public class RegistrarEntradaCommandTests
{
    [Fact]
    public async Task Ejecutar_AgregaEntradaYSumaAlIngrediente()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await SeedIngredienteAsync(fx, "Harina", "kg", 10);

        var cmd = new RegistrarEntradaCommand
        {
            IngredienteId = ingId,
            IngredienteNombre = "Harina",
            UnidadMedida = "kg",
            Cantidad = 5
        };

        await cmd.EjecutarAsync(fx);

        await using var db = fx.CreateDbContext();
        var entradas = await db.Entradas.AsNoTracking().ToListAsync();
        Assert.Single(entradas);
        Assert.Equal(5, entradas[0].Cantidad);
        var ing = await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId);
        Assert.Equal(15, ing.CantidadActual);
        Assert.NotEqual(0, cmd.EntradaIdAsignado);
    }

    [Fact]
    public async Task Deshacer_BorraEntradaYRestauraStock()
    {
        using var fx = new InMemoryDbFixture();
        var ingId = await SeedIngredienteAsync(fx, "Queso", "kg", 20);

        var cmd = new RegistrarEntradaCommand
        {
            IngredienteId = ingId,
            IngredienteNombre = "Queso",
            UnidadMedida = "kg",
            Cantidad = 3
        };
        await cmd.EjecutarAsync(fx);

        await cmd.DeshacerAsync(fx);

        await using var db = fx.CreateDbContext();
        Assert.Empty(await db.Entradas.AsNoTracking().ToListAsync());
        var ing = await db.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId);
        Assert.Equal(20, ing.CantidadActual);
    }

    internal static async Task<int> SeedIngredienteAsync(
        InMemoryDbFixture fx, string nombre, string unidad, double cantidad)
    {
        await using var db = fx.CreateDbContext();
        var ing = new Ingrediente
        {
            Nombre = nombre,
            UnidadMedida = unidad,
            CantidadActual = cantidad,
            CantidadMinima = 0,
            Activo = true,
            FechaActualizacion = System.DateTime.Now
        };
        db.Ingredientes.Add(ing);
        await db.SaveChangesAsync();
        return ing.Id;
    }
}
