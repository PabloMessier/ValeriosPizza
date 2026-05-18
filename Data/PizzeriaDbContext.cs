using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using System.IO;

namespace ValeriosPizza.Data;

public class PizzeriaDbContext : DbContext
{
    /// <summary>
    /// Ruta absoluta del archivo SQLite que la app usa por defecto.
    /// </summary>
    public static string DefaultDbPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValeriosPizzeria",
        "pizzeria.db");

    public DbSet<Ingrediente> Ingredientes { get; set; }
    public DbSet<Producto> Productos { get; set; }
    public DbSet<Entrada> Entradas { get; set; }
    public DbSet<Gasto> Gastos { get; set; }
    public DbSet<Merma> Mermas { get; set; }
    public DbSet<Cortesia> Cortesias { get; set; }
    public DbSet<InventarioDisco> InventarioDiscos { get; set; }
    public DbSet<MercanciaRecibida> MercanciasRecibidas { get; set; }
    public DbSet<InventarioCaja> InventarioCajas { get; set; }
    public DbSet<ConteoInventario> ConteosInventario { get; set; }
    public DbSet<ConteoInventarioLinea> ConteoInventarioLineas { get; set; }
    /// <summary>
    /// Items que el dueño decide enviar a la "Bodega" (almacén general).
    /// Tabla independiente del inventario activo; cada fila es un evento de
    /// "agregar a bodega" disparado desde Inventario / Mercancía Nueva / Consulta.
    /// </summary>
    public DbSet<BodegaItem> BodegaItems { get; set; }

    /// <summary>
    /// Constructor sin parámetros para llamadas legacy (<c>new PizzeriaDbContext()</c>)
    /// que aún no han migrado al <c>IDbContextFactory&lt;PizzeriaDbContext&gt;</c>.
    /// Usa la ruta por defecto vía <see cref="OnConfiguring"/>.
    /// </summary>
    public PizzeriaDbContext() { }

    /// <summary>
    /// Constructor usado por DI cuando el contenedor entrega opciones ya
    /// configuradas (por ejemplo, desde <c>AddDbContextFactory</c> en
    /// <c>App.xaml.cs</c>).
    /// </summary>
    public PizzeriaDbContext(DbContextOptions<PizzeriaDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Sólo configuramos manualmente cuando el ctor por DI no aportó opciones.
        if (optionsBuilder.IsConfigured) return;

        var directory = Path.GetDirectoryName(DefaultDbPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        optionsBuilder.UseSqlite($"Data Source={DefaultDbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuraciones adicionales para campos monetarios
        modelBuilder.Entity<Entrada>()
            .Property(e => e.CostoTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Merma>()
            .Property(m => m.CostoEstimado)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Cortesia>()
            .Property(c => c.ValorEstimado)
            .HasPrecision(18, 2);

        // Conteos de inventario (Apertura/Cierre): borrado en cascada de las líneas
        modelBuilder.Entity<ConteoInventarioLinea>()
            .HasOne(l => l.ConteoInventario)
            .WithMany(c => c.Lineas)
            .HasForeignKey(l => l.ConteoInventarioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConteoInventarioLinea>()
            .HasOne(l => l.Ingrediente)
            .WithMany()
            .HasForeignKey(l => l.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Permite consultar rápido por fecha+tipo (un Apertura y un Cierre por día).
        modelBuilder.Entity<ConteoInventario>()
            .HasIndex(c => new { c.Fecha, c.Tipo });

        modelBuilder.Entity<InventarioCaja>()
            .HasIndex(c => c.Fecha);

        // Bodega: índice por fecha para mostrar los más recientes primero
        // sin escanear toda la tabla. No imponemos FK a Ingredientes /
        // MercanciasRecibidas para que las filas históricas sobrevivan a
        // borrados aguas arriba.
        modelBuilder.Entity<BodegaItem>()
            .HasIndex(b => b.FechaAgregado);
    }
}
