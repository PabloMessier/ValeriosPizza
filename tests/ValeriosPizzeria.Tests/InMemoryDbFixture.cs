using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;

namespace ValeriosPizza.Tests;

/// <summary>
/// Crea una BD SQLite en memoria por instancia del test. La conexión
/// permanece abierta durante toda la vida de la clase de test (cuando se
/// cierra, SQLite descarta los datos), por eso los tests que la usan
/// deben heredar de <c>IClassFixture&lt;InMemoryDbFixture&gt;</c> o
/// crear/disposear esta instancia ellos mismos.
///
/// La factoría devuelve siempre contextos que apuntan a la misma
/// conexión: imita el comportamiento de <c>IDbContextFactory</c> que
/// usa la app real (cada VM crea/dispone su propio contexto, todos
/// hablando con la misma BD).
/// </summary>
public sealed class InMemoryDbFixture : IDisposable, IDbContextFactory<PizzeriaDbContext>
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PizzeriaDbContext> _options;

    public InMemoryDbFixture()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<PizzeriaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new PizzeriaDbContext(_options);
        db.Database.EnsureCreated();
    }

    public PizzeriaDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
