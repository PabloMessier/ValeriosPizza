using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo;
using ValeriosPizza.ViewModels;
using Xunit;

namespace ValeriosPizza.Tests.ViewModels;

/// <summary>
/// Smoke tests del <see cref="RegistroRapidoViewModel"/>. Cubren la
/// composición de la clase tras el split en partial classes:
/// ctor + carga inicial, helpers de "cambios pendientes" y el flujo
/// silencioso de "Guardar todo al cerrar" usando una BD en memoria.
///
/// No se prueban comandos que muestran MessageBox (requieren UI thread
/// real); los flujos críticos de persistencia ya están cubiertos por
/// los tests de UndoRedo Commands.
/// </summary>
public class RegistroRapidoViewModelTests
{
    private static RegistroRapidoViewModel CrearVM(InMemoryDbFixture fx) =>
        new(fx, new UndoRedoService(fx));

    [Fact]
    public void Ctor_CargaIngredientesActivosYProductosNoDescontinuados()
    {
        using var fx = new InMemoryDbFixture();
        using (var db = fx.CreateDbContext())
        {
            db.Ingredientes.Add(new Ingrediente
            {
                Nombre = "Harina", UnidadMedida = "kg", CantidadActual = 0,
                Activo = true, FechaActualizacion = System.DateTime.Now
            });
            db.Ingredientes.Add(new Ingrediente
            {
                Nombre = "Inactivo", UnidadMedida = "kg", CantidadActual = 0,
                Activo = false, FechaActualizacion = System.DateTime.Now
            });
            db.Productos.Add(new Producto { Nombre = "Pizza", Categoria = Categoria.Pizza, Estado = EstadoProducto.Activo });
            db.Productos.Add(new Producto { Nombre = "Descontinuada", Categoria = Categoria.Pizza, Estado = EstadoProducto.Descontinuado });
            db.SaveChanges();
        }

        var vm = CrearVM(fx);

        // Solo el ingrediente activo aparece en el listado del combo.
        Assert.Single(vm.Ingredientes);
        Assert.Equal("Harina", vm.Ingredientes[0].Nombre);
        // Los Descontinuados se ocultan; los Activos/Agotados se conservan.
        Assert.Single(vm.Productos);
        Assert.Equal("Pizza", vm.Productos[0].Nombre);
    }

    [Fact]
    public void TieneCambiosSinGuardar_FormularioVacio_DevuelveFalse()
    {
        using var fx = new InMemoryDbFixture();
        var vm = CrearVM(fx);

        Assert.False(vm.TieneCambiosSinGuardar);
        Assert.Empty(vm.SeccionesPendientes());
    }

    [Fact]
    public void SeccionesPendientes_DetectaCadaSeccionPorSeparado()
    {
        using var fx = new InMemoryDbFixture();
        var vm = CrearVM(fx);

        vm.GastoCantidad = 3;
        vm.MermaMotivo = "Quemado";
        vm.DiscosPreparados = 5;

        var pendientes = vm.SeccionesPendientes().ToList();
        Assert.Contains("Gasto", pendientes);
        Assert.Contains("Merma", pendientes);
        Assert.Contains("Discos", pendientes);
        Assert.DoesNotContain("Entrada", pendientes);
        Assert.True(vm.TieneCambiosSinGuardar);
    }

    [Fact]
    public async Task IntentarGuardarTodoSilencioso_PersisteSeccionesValidas()
    {
        using var fx = new InMemoryDbFixture();
        // Sembrar un ingrediente para que Entrada/Gasto puedan referenciarlo.
        int ingId;
        await using (var db = fx.CreateDbContext())
        {
            var ing = new Ingrediente
            {
                Nombre = "Tomate", UnidadMedida = "kg", CantidadActual = 10,
                Activo = true, FechaActualizacion = System.DateTime.Now
            };
            db.Ingredientes.Add(ing);
            await db.SaveChangesAsync();
            ingId = ing.Id;
        }

        var vm = CrearVM(fx);
        var tomate = vm.Ingredientes.First(i => i.Id == ingId);

        // Llenar Entrada con datos válidos y Gasto con datos incompletos.
        vm.IngredienteSeleccionado = tomate;
        vm.Cantidad = 4;
        vm.Costo = 50;
        vm.GastoCantidad = 1; // sin ingrediente seleccionado: debe omitirse

        var resultado = vm.IntentarGuardarTodoSilencioso();

        Assert.Contains("Entrada", resultado.Guardadas);
        Assert.Contains(resultado.Omitidas, o => o.Seccion == "Gasto");

        // La Entrada debe haberse persistido y el stock sumado.
        await using var db2 = fx.CreateDbContext();
        Assert.Single(await db2.Entradas.AsNoTracking().ToListAsync());
        Assert.Equal(14, (await db2.Ingredientes.AsNoTracking().FirstAsync(i => i.Id == ingId)).CantidadActual);
        // La sección Entrada se limpió tras el guardado; los datos de Gasto
        // siguen ahí porque no se logró persistir (sin ingrediente).
        Assert.Null(vm.IngredienteSeleccionado);
        Assert.Equal(0, vm.Cantidad);
        Assert.Equal(1, vm.GastoCantidad);
    }

    [Fact]
    public void TieneDatos_PorSeccion_FuncionaIndependientemente()
    {
        using var fx = new InMemoryDbFixture();
        var vm = CrearVM(fx);

        Assert.Empty(vm.SeccionesPendientes());

        vm.CajaInicial = 10;
        Assert.Contains("Cajas", vm.SeccionesPendientes());

        vm.CajaInicial = 0;
        vm.CortesiaCantidad = 2;
        Assert.Contains("Cortesía", vm.SeccionesPendientes());
        Assert.DoesNotContain("Cajas", vm.SeccionesPendientes());
    }
}
