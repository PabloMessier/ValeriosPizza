using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ValeriosPizza.Data;

namespace ValeriosPizza.Services;

/// <summary>
/// Encapsula el flujo de inicialización de la base de datos al iniciar la app:
/// <list type="number">
///   <item>Crea un respaldo automático rotativo del archivo SQLite vigente
///         antes de tocar el esquema (recuperable si algo sale mal).</item>
///   <item>Ejecuta el bootstrap de esquema heredado
///         (<c>EnsureCreated</c> + <c>ActualizarEsquema</c>) hasta que la
///         migración de EF tome su lugar (ver Wave 4 follow-up en
///         <c>DEVELOPMENT_NOTES.md</c>).</item>
/// </list>
/// El servicio reemplaza la lógica que antes estaba inline en
/// <see cref="App.OnStartup"/> y permite probarla por separado.
/// </summary>
// CA1848 sugiere usar LoggerMessage delegates por performance, pero
// estos logs sólo se emiten en el arranque (4 llamadas, una vez por
// proceso); no justifican la complejidad adicional.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Bajo volumen: sólo se emite en startup.")]
public sealed class DatabaseInitializer
{
    private const int RespaldosAutomaticosMaximos = 7;

    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<PizzeriaDbContext> dbFactory,
        ILogger<DatabaseInitializer>? logger = null)
    {
        _dbFactory = dbFactory;
        _logger = logger ?? NullLogger<DatabaseInitializer>.Instance;
    }

    /// <summary>
    /// Carpeta donde se guardan los respaldos automáticos diarios.
    /// </summary>
    public static string CarpetaRespaldosAutomaticos
    {
        get
        {
            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ValeriosPizzeria",
                "AutoBackups");
            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }
            return carpeta;
        }
    }

    /// <summary>
    /// Ejecuta backup-rotativo y bootstrap de esquema. Cada paso captura sus
    /// propias excepciones y las registra a través de <paramref name="reportarError"/>
    /// para que un fallo en el backup nunca impida que la app arranque.
    /// </summary>
    public void Inicializar(Action<Exception, string> reportarError)
    {
        _logger.LogInformation("Inicializando base de datos en {Ruta}", PizzeriaDbContext.DefaultDbPath);

        TryEjecutar(() => CrearRespaldoAutomaticoSiAplica(),
            "Crear respaldo automático", reportarError);

        TryEjecutar(BootstrapEsquema,
            "Inicializar esquema de base de datos", reportarError);

        _logger.LogInformation("Inicialización de base de datos completada.");
    }

    private void TryEjecutar(Action paso, string contexto, Action<Exception, string> reportarError)
    {
        try
        {
            paso();
            _logger.LogDebug("Paso completado: {Contexto}", contexto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló paso de inicialización: {Contexto}", contexto);
            reportarError(ex, contexto);
        }
    }

    /// <summary>
    /// Si existe la base de datos vigente, copia el archivo a la carpeta de
    /// respaldos automáticos con un nombre con timestamp y, a continuación,
    /// elimina los respaldos más viejos para no acumular indefinidamente.
    /// No se hace si el archivo todavía no existe (primera instalación).
    /// </summary>
    private static void CrearRespaldoAutomaticoSiAplica()
    {
        var rutaActual = PizzeriaDbContext.DefaultDbPath;
        if (!File.Exists(rutaActual)) return;

        var nombre = $"pizzeria_auto_{DateTime.Now:yyyy-MM-dd_HHmmss}.valdb";
        var destino = Path.Combine(CarpetaRespaldosAutomaticos, nombre);
        File.Copy(rutaActual, destino, overwrite: false);

        // Rotación: conservar solo los más recientes.
        var existentes = new DirectoryInfo(CarpetaRespaldosAutomaticos)
            .GetFiles("pizzeria_auto_*.valdb")
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        foreach (var sobrante in existentes.Skip(RespaldosAutomaticosMaximos))
        {
            try { sobrante.Delete(); }
            catch { /* el respaldo viejo puede estar bloqueado; ignorar. */ }
        }
    }

    /// <summary>
    /// Aplica el bootstrap heredado: <c>EnsureCreated</c> crea las tablas
    /// faltantes en BDs nuevas y <see cref="ActualizarEsquema"/> añade columnas
    /// y tablas que se introdujeron en versiones posteriores.
    /// Esta lógica se reemplazará en una ola posterior por
    /// <c>db.Database.Migrate()</c> con migraciones EF reales (ver notas en
    /// <c>DEVELOPMENT_NOTES.md</c>).
    /// </summary>
    private void BootstrapEsquema()
    {
        using var db = _dbFactory.CreateDbContext();
        db.Database.EnsureCreated();
        ActualizarEsquema(db);
        SeedProductosSiVacio(db);
    }

    /// <summary>
    /// Si la tabla Productos está vacía (primera instalación o BD recién
    /// creada), siembra un pequeño conjunto de productos por defecto del
    /// menú para que el dropdown de Cortesía en "Registro Rápido" no
    /// aparezca vacío. La dueña puede luego agregar, editar o descontinuar
    /// productos desde la pestaña Inventario → Productos.
    /// </summary>
    private static void SeedProductosSiVacio(PizzeriaDbContext db)
    {
        if (db.Productos.Any()) return;

        db.Productos.AddRange(
            new Models.Producto { Nombre = "Pizza Margherita",   Categoria = Models.Categoria.Pizza,  Estado = Models.EstadoProducto.Activo },
            new Models.Producto { Nombre = "Pizza Pepperoni",    Categoria = Models.Categoria.Pizza,  Estado = Models.EstadoProducto.Activo },
            new Models.Producto { Nombre = "Pizza Hawaiana",     Categoria = Models.Categoria.Pizza,  Estado = Models.EstadoProducto.Activo },
            new Models.Producto { Nombre = "Pizza Vegetariana",  Categoria = Models.Categoria.Pizza,  Estado = Models.EstadoProducto.Activo },
            new Models.Producto { Nombre = "Panini Italiano",    Categoria = Models.Categoria.Panini, Estado = Models.EstadoProducto.Activo },
            new Models.Producto { Nombre = "Panini de Pollo",    Categoria = Models.Categoria.Panini, Estado = Models.EstadoProducto.Activo },
            new Models.Producto { Nombre = "Disco de Pizza",     Categoria = Models.Categoria.Disco,  Estado = Models.EstadoProducto.Activo });
        db.SaveChanges();
    }

    /// <summary>
    /// Aplica cambios de esquema incrementales sobre BDs existentes creadas con
    /// EnsureCreated() en versiones anteriores. Es el mismo conjunto de DDL
    /// que vivía en <c>App.xaml.cs</c>; movido aquí para que la app sólo lo
    /// invoque a través del initializer.
    /// </summary>
    private static void ActualizarEsquema(PizzeriaDbContext db)
    {
        // 1. Agregar columna Activo a Ingredientes si no existe (default 1 = true).
        if (ContarColumna(db, "Ingredientes", "Activo") == 0)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Ingredientes ADD COLUMN Activo INTEGER NOT NULL DEFAULT 1");
        }

        // 1b. Agregar columna Estado a Productos si no existe (default 0 = Activo).
        if (ContarColumna(db, "Productos", "Estado") == 0)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Productos ADD COLUMN Estado INTEGER NOT NULL DEFAULT 0");
            // Sincronizar productos pre-existentes que estaban marcados como inactivos
            // (Activo=0) con el nuevo estado Descontinuado (Estado=2).
            db.Database.ExecuteSqlRaw("UPDATE Productos SET Estado = 2 WHERE Activo = 0");
        }

        // 2. InventarioCajas: en versiones previas tenía columna 'Tamano' (Pequeña/Mediana/Grande).
        // Ahora es tamaño único. Si la tabla vieja existe con esa columna, la eliminamos.
        if (ContarColumna(db, "InventarioCajas", "Tamano") > 0)
        {
            db.Database.ExecuteSqlRaw("DROP TABLE InventarioCajas");
        }

        // 3. Crear tablas nuevas si no existen.
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""InventarioCajas"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_InventarioCajas"" PRIMARY KEY AUTOINCREMENT,
                ""Fecha"" TEXT NOT NULL,
                ""CantidadInicial"" INTEGER NOT NULL,
                ""CajasRecibidas"" INTEGER NOT NULL,
                ""CajasUtilizadas"" INTEGER NOT NULL,
                ""CajasMerma"" INTEGER NOT NULL,
                ""Notas"" TEXT NULL
            );");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS \"IX_InventarioCajas_Fecha\" ON \"InventarioCajas\" (\"Fecha\");");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ConteosInventario"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ConteosInventario"" PRIMARY KEY AUTOINCREMENT,
                ""Fecha"" TEXT NOT NULL,
                ""Tipo"" INTEGER NOT NULL,
                ""Notas"" TEXT NULL
            );");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS \"IX_ConteosInventario_Fecha_Tipo\" ON \"ConteosInventario\" (\"Fecha\", \"Tipo\");");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ConteoInventarioLineas"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ConteoInventarioLineas"" PRIMARY KEY AUTOINCREMENT,
                ""ConteoInventarioId"" INTEGER NOT NULL,
                ""IngredienteId"" INTEGER NOT NULL,
                ""Cantidad"" REAL NOT NULL,
                CONSTRAINT ""FK_ConteoInventarioLineas_ConteosInventario_ConteoInventarioId""
                    FOREIGN KEY (""ConteoInventarioId"") REFERENCES ""ConteosInventario"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_ConteoInventarioLineas_Ingredientes_IngredienteId""
                    FOREIGN KEY (""IngredienteId"") REFERENCES ""Ingredientes"" (""Id"") ON DELETE RESTRICT
            );");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS \"IX_ConteoInventarioLineas_ConteoInventarioId\" ON \"ConteoInventarioLineas\" (\"ConteoInventarioId\");");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS \"IX_ConteoInventarioLineas_IngredienteId\" ON \"ConteoInventarioLineas\" (\"IngredienteId\");");

        // 4. Calculadora COP: PrecioUnitario / Impuesto / Retencion como TEXT (decimal en SQLite).
        string[] tablasConCalculadora =
        {
            "Entradas", "Gastos", "Mermas", "Cortesias",
            "MercanciasRecibidas", "InventarioDiscos", "InventarioCajas"
        };
        string[] columnasMonetarias = { "PrecioUnitario", "Impuesto", "Retencion" };
        foreach (var tabla in tablasConCalculadora)
        {
            foreach (var col in columnasMonetarias)
            {
                if (ContarColumna(db, tabla, col) == 0)
                {
                    var sql = "ALTER TABLE \"" + tabla + "\" ADD COLUMN \"" + col + "\" TEXT NOT NULL DEFAULT '0'";
                    db.Database.ExecuteSqlRaw(sql);
                }
            }
        }

        // 5. Mercancía: ruta opcional al archivo de factura digital (PDF / imagen).
        if (ContarColumna(db, "MercanciasRecibidas", "RutaFactura") == 0)
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE \"MercanciasRecibidas\" ADD COLUMN \"RutaFactura\" TEXT NULL");
        }

        // 6. Bodega: tabla independiente del inventario activo. Cada fila es
        //    un evento de "agregar a bodega" disparado desde
        //    Inventario / Mercancía Nueva / Consulta. No se imponen FKs
        //    duras para que las filas históricas sobrevivan a borrados
        //    aguas arriba.
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""BodegaItems"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_BodegaItems"" PRIMARY KEY AUTOINCREMENT,
                ""FechaAgregado"" TEXT NOT NULL,
                ""Nombre"" TEXT NOT NULL,
                ""Categoria"" TEXT NOT NULL,
                ""Cantidad"" REAL NOT NULL,
                ""UnidadMedida"" TEXT NOT NULL,
                ""Notas"" TEXT NULL,
                ""Origen"" TEXT NULL,
                ""IngredienteId"" INTEGER NULL,
                ""MercanciaRecibidaId"" INTEGER NULL
            );");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS \"IX_BodegaItems_FechaAgregado\" ON \"BodegaItems\" (\"FechaAgregado\");");

        // 7. Índices por Fecha en tablas de movimientos. Reportes/Consulta/
        //    Inventario filtran siempre por rangos de fecha; sin estos índices
        //    SQLite hace table scan cuando el historial crece. CREATE INDEX
        //    IF NOT EXISTS los hace idempotentes en BDs ya creadas.
        var tablasFecha = new (string Tabla, string Columna)[]
        {
            ("Entradas",            "Fecha"),
            ("Gastos",              "Fecha"),
            ("Mermas",              "Fecha"),
            ("Cortesias",           "Fecha"),
            ("MercanciasRecibidas", "Fecha"),
            ("InventarioDiscos",    "Fecha"),
        };
        foreach (var (tabla, columna) in tablasFecha)
        {
            // Identificadores hardcoded en este archivo: el warning EF1002
            // (riesgo de SQL injection con interpolación) no aplica.
            var sql = "CREATE INDEX IF NOT EXISTS \"IX_" + tabla + "_" + columna +
                      "\" ON \"" + tabla + "\" (\"" + columna + "\");";
            db.Database.ExecuteSqlRaw(sql);
        }
    }

    private static int ContarColumna(PizzeriaDbContext db, string tabla, string columna)
    {
        return db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({tabla}) WHERE name = {columna}")
            .AsEnumerable()
            .FirstOrDefault();
    }
}
