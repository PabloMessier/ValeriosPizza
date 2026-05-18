using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.Services.UndoRedo;
using ValeriosPizza.ViewModels;
using Xunit;

namespace ValeriosPizza.Tests.ViewModels;

/// <summary>
/// Verifica que las anotaciones <c>[Required]</c>/<c>[Range]</c> en el VM
/// se traducen en errores reportados por <c>INotifyDataErrorInfo</c> tras
/// <c>ValidateAllProperties()</c>.
/// </summary>
public class RegistroRapidoValidationTests
{
    private static (RegistroRapidoViewModel vm, IDisposable cleanup) CrearVm()
    {
        // SQLite in-memory con conexión compartida abierta: cada nuevo
        // PizzeriaDbContext que devuelva la fábrica reutiliza el mismo BD
        // (cuando la conexión se cierra, la BD desaparece).
        var factory = new InMemorySqliteFactory();
        return (new RegistroRapidoViewModel(factory, new UndoRedoService(factory)), factory);
    }

    private static void ValidateAll(RegistroRapidoViewModel vm)
        => vm.GetType().GetMethod("ValidateAllProperties",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(vm, null);

    [Fact]
    public void Cantidad_Cero_ProduceErrorDeRango()
    {
        var (vm, cleanup) = CrearVm();
        using var _ = cleanup;

        vm.Cantidad = 0;
        ValidateAll(vm);

        var errores = vm.GetErrors(nameof(vm.Cantidad)).Cast<ValidationResult>().ToList();
        Assert.NotEmpty(errores);
        Assert.Contains(errores, r => r.ErrorMessage!.Contains("mayor que cero"));
    }

    [Fact]
    public void IngredienteSeleccionado_Null_ProduceErrorRequired()
    {
        var (vm, cleanup) = CrearVm();
        using var _ = cleanup;

        vm.IngredienteSeleccionado = null;
        ValidateAll(vm);

        var errores = vm.GetErrors(nameof(vm.IngredienteSeleccionado))
            .Cast<ValidationResult>().ToList();
        Assert.NotEmpty(errores);
    }

    [Fact]
    public void Cantidad_Positiva_NoProduceErrores()
    {
        var (vm, cleanup) = CrearVm();
        using var _ = cleanup;

        vm.Cantidad = 2.5;

        var errores = vm.GetErrors(nameof(vm.Cantidad)).Cast<ValidationResult>().ToList();
        Assert.Empty(errores);
    }
}

/// <summary>
/// Fábrica de pruebas que comparte una sola conexión SQLite en memoria entre
/// todos los <see cref="PizzeriaDbContext"/> que produce. Crea el esquema en
/// el primer uso (<c>EnsureCreated</c>) y libera la conexión al disponerse.
/// </summary>
internal sealed class InMemorySqliteFactory : IDbContextFactory<PizzeriaDbContext>, IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<PizzeriaDbContext> _options;

    public InMemorySqliteFactory()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<PizzeriaDbContext>()
            .UseSqlite(_conn)
            .Options;

        using var ctx = new PizzeriaDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public PizzeriaDbContext CreateDbContext() => new(_options);

    public void Dispose() => _conn.Dispose();
}
