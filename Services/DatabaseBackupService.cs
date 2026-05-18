using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using ValeriosPizza.Data;

namespace ValeriosPizza.Services;

/// <summary>
/// Permite exportar (respaldar) la base de datos SQLite del sistema a un archivo
/// portable, e importar un archivo previamente exportado para restaurar/migrar
/// los datos. Pensado para que una versión futura (v2.0) pueda continuar con la
/// data persistida por la versión actual (v1.0).
///
/// Formato de exportación: archivo SQLite estándar generado con
/// <c>VACUUM INTO</c>, lo que produce una copia compacta y consistente sin
/// requerir bloquear la base de datos en uso. Extensión recomendada:
/// <c>.valdb</c> (también acepta <c>.db</c> / <c>.sqlite</c> al importar).
/// </summary>
public static class DatabaseBackupService
{
    /// <summary>
    /// Tablas mínimas que debe contener un archivo para considerarse un respaldo
    /// válido de Valerio's Pizza. Se valida en <see cref="Importar"/> antes
    /// de sobrescribir la base de datos actual.
    /// </summary>
    private static readonly string[] TablasRequeridas =
    {
        "Ingredientes",
        "Productos",
        "Entradas",
        "Gastos",
        "Mermas",
        "Cortesias",
        "MercanciasRecibidas"
    };

    /// <summary>
    /// Ruta absoluta del archivo de base de datos SQLite que la aplicación usa
    /// actualmente. Coincide con la ruta configurada en
    /// <see cref="PizzeriaDbContext"/>.
    /// </summary>
    public static string RutaBaseDatosActual
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ValeriosPizzeria",
                "pizzeria.db");
        }
    }

    /// <summary>
    /// Carpeta sugerida (en Documentos del usuario) para guardar respaldos.
    /// Se crea si no existe.
    /// </summary>
    public static string CarpetaRespaldosSugerida
    {
        get
        {
            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ValeriosPizzeria",
                "Respaldos");

            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }
            return carpeta;
        }
    }

    /// <summary>
    /// Genera un nombre de archivo sugerido para un respaldo nuevo, con
    /// timestamp para evitar colisiones.
    /// </summary>
    public static string NombreArchivoSugerido()
    {
        return $"valerios_pizzeria_backup_{DateTime.Now:yyyy-MM-dd_HHmm}.valdb";
    }

    /// <summary>
    /// Exporta la base de datos actual a <paramref name="rutaDestino"/>.
    /// Usa <c>VACUUM INTO</c> para producir una copia íntegra y compacta.
    /// </summary>
    /// <returns>Ruta absoluta del archivo generado.</returns>
    public static string Exportar(string rutaDestino)
    {
        if (string.IsNullOrWhiteSpace(rutaDestino))
            throw new ArgumentException("La ruta destino no puede estar vacía.", nameof(rutaDestino));

        var origen = RutaBaseDatosActual;
        if (!File.Exists(origen))
            throw new FileNotFoundException("No se encontró la base de datos actual para exportar.", origen);

        // Crear carpeta destino si hace falta
        var carpetaDestino = Path.GetDirectoryName(rutaDestino);
        if (!string.IsNullOrEmpty(carpetaDestino) && !Directory.Exists(carpetaDestino))
        {
            Directory.CreateDirectory(carpetaDestino);
        }

        // VACUUM INTO falla si el archivo destino existe; eliminarlo primero.
        if (File.Exists(rutaDestino))
        {
            File.Delete(rutaDestino);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = origen,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // Parámetros no se aceptan en VACUUM INTO; la ruta se inserta escapada.
        cmd.CommandText = $"VACUUM INTO '{rutaDestino.Replace("'", "''")}'";
        cmd.ExecuteNonQuery();

        return rutaDestino;
    }

    /// <summary>
    /// Resultado de validar un archivo candidato a restaurar.
    /// </summary>
    public sealed class ResultadoValidacion
    {
        public bool EsValido { get; init; }
        public string? Motivo { get; init; }
        public IReadOnlyCollection<string> TablasFaltantes { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Verifica que el archivo indicado sea una base de datos SQLite válida y
    /// que contenga las tablas esperadas por la aplicación.
    /// </summary>
    public static ResultadoValidacion Validar(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            return new ResultadoValidacion { EsValido = false, Motivo = "El archivo no existe." };
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = rutaArchivo,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            // Integrity check
            using (var integrityCmd = conn.CreateCommand())
            {
                integrityCmd.CommandText = "PRAGMA integrity_check;";
                var resultado = integrityCmd.ExecuteScalar() as string;
                if (!string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return new ResultadoValidacion
                    {
                        EsValido = false,
                        Motivo = $"El archivo está corrupto (integrity_check = {resultado})."
                    };
                }
            }

            // Listar tablas existentes
            var tablasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var listCmd = conn.CreateCommand())
            {
                listCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                using var reader = listCmd.ExecuteReader();
                while (reader.Read())
                {
                    tablasExistentes.Add(reader.GetString(0));
                }
            }

            var faltantes = TablasRequeridas.Where(t => !tablasExistentes.Contains(t)).ToArray();
            if (faltantes.Length > 0)
            {
                return new ResultadoValidacion
                {
                    EsValido = false,
                    Motivo = "El archivo no parece ser un respaldo de Valerio's Pizza.",
                    TablasFaltantes = faltantes
                };
            }

            return new ResultadoValidacion { EsValido = true };
        }
        catch (SqliteException ex)
        {
            return new ResultadoValidacion
            {
                EsValido = false,
                Motivo = $"No es un archivo SQLite válido: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Reemplaza la base de datos actual con el contenido de <paramref name="rutaOrigen"/>.
    /// Antes de sobrescribir crea un respaldo de seguridad de la BD vigente
    /// (en la carpeta de Respaldos) por si el usuario necesita revertir.
    /// La aplicación debe reiniciarse después de importar para que las
    /// instancias de <see cref="PizzeriaDbContext"/> usen el nuevo archivo.
    /// </summary>
    /// <returns>
    /// Ruta del respaldo de seguridad creado de la BD anterior, o <c>null</c>
    /// si no había BD previa.
    /// </returns>
    public static string? Importar(string rutaOrigen)
    {
        if (string.IsNullOrWhiteSpace(rutaOrigen))
            throw new ArgumentException("La ruta origen no puede estar vacía.", nameof(rutaOrigen));

        var validacion = Validar(rutaOrigen);
        if (!validacion.EsValido)
        {
            var detalle = validacion.TablasFaltantes.Count > 0
                ? $" Tablas faltantes: {string.Join(", ", validacion.TablasFaltantes)}."
                : string.Empty;
            throw new InvalidDataException($"{validacion.Motivo}{detalle}");
        }

        var destino = RutaBaseDatosActual;
        var carpetaDestino = Path.GetDirectoryName(destino);
        if (!string.IsNullOrEmpty(carpetaDestino) && !Directory.Exists(carpetaDestino))
        {
            Directory.CreateDirectory(carpetaDestino);
        }

        // Forzar liberación de cualquier pool de conexiones SQLite que apunte
        // al archivo actual para poder sobrescribirlo sin errores de bloqueo.
        SqliteConnection.ClearAllPools();

        string? rutaRespaldoSeguridad = null;
        if (File.Exists(destino))
        {
            rutaRespaldoSeguridad = Path.Combine(
                CarpetaRespaldosSugerida,
                $"pizzeria_pre_import_{DateTime.Now:yyyy-MM-dd_HHmmss}.valdb");
            File.Copy(destino, rutaRespaldoSeguridad, overwrite: true);
        }

        // Sobrescribir la BD activa con la importada.
        File.Copy(rutaOrigen, destino, overwrite: true);

        return rutaRespaldoSeguridad;
    }
}
