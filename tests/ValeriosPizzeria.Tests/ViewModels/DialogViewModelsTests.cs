using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.ViewModels;
using Xunit;

namespace ValeriosPizza.Tests.ViewModels;

/// <summary>
/// Verifica la lógica de guardado movida desde el code-behind a los VMs
/// de los diálogos. La unicidad de nombre, el modo "crear" y "editar"
/// quedan cubiertos contra una BD real (SQLite en memoria) para que las
/// regresiones por refactor del code-behind salgan a la luz de inmediato.
/// </summary>
public class DialogViewModelsTests
{
    [Fact]
    public async Task IngredienteDialogVM_Crear_GuardaConExito()
    {
        using var fx = new InMemoryDbFixture();
        var vm = new IngredienteDialogViewModel(fx)
        {
            Nombre = "Tomate",
            UnidadMedida = "kg",
            CantidadActualTexto = "12.5",
            CantidadMinimaTexto = "2"
        };

        var r = await vm.GuardarAsync();

        Assert.True(r.Ok);
        Assert.NotNull(r.Ingrediente);
        Assert.Equal(12.5, r.Ingrediente!.CantidadActual);

        await using var db = fx.CreateDbContext();
        Assert.Single(await db.Ingredientes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task IngredienteDialogVM_Crear_RechazaNombreDuplicado()
    {
        using var fx = new InMemoryDbFixture();
        // Sembrar un ingrediente con el mismo nombre (distinta capitalización
        // para probar el matching case-insensitive con LIKE).
        await using (var seed = fx.CreateDbContext())
        {
            seed.Ingredientes.Add(new Ingrediente
            {
                Nombre = "Queso",
                UnidadMedida = "kg",
                CantidadActual = 1,
                Activo = true,
                FechaActualizacion = System.DateTime.Now
            });
            await seed.SaveChangesAsync();
        }

        var vm = new IngredienteDialogViewModel(fx)
        {
            Nombre = "queso",  // misma palabra en minúsculas
            UnidadMedida = "kg",
            CantidadActualTexto = "5",
            CantidadMinimaTexto = "0"
        };

        var r = await vm.GuardarAsync();

        Assert.False(r.Ok);
        Assert.Contains("ya existe", vm.MensajeError, System.StringComparison.OrdinalIgnoreCase);

        await using var db = fx.CreateDbContext();
        Assert.Single(await db.Ingredientes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ProductoDialogVM_Editar_ActualizaCamposExistentes()
    {
        using var fx = new InMemoryDbFixture();
        Producto existente;
        await using (var seed = fx.CreateDbContext())
        {
            existente = new Producto
            {
                Nombre = "Pizza Hawaiana",
                Categoria = Categoria.Pizza,
                Estado = EstadoProducto.Activo
            };
            seed.Productos.Add(existente);
            await seed.SaveChangesAsync();
        }

        var vm = new ProductoDialogViewModel(existente, fx)
        {
            Nombre = "Pizza Hawaiana Especial",
            EstadoSeleccionado = EstadoProducto.Agotado
        };

        var r = await vm.GuardarAsync();

        Assert.True(r.Ok);
        await using var db = fx.CreateDbContext();
        var actualizado = await db.Productos.AsNoTracking().FirstAsync();
        Assert.Equal("Pizza Hawaiana Especial", actualizado.Nombre);
        Assert.Equal(EstadoProducto.Agotado, actualizado.Estado);
    }
}
