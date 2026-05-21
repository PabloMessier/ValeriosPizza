using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ValeriosPizza.Services;

/// <summary>
/// Provider de <see cref="ILogger"/> que escribe a un archivo plano
/// rotativo en <c>%LOCALAPPDATA%\ValeriosPizzeria\Logs\app.log</c>.
///
/// Se implementa "a mano" en lugar de depender de Serilog/NLog porque:
/// <list type="bullet">
///   <item>Las dependencias actuales del proyecto son mínimas y queremos mantenerlo así.</item>
///   <item>El volumen de logging es bajo (UI events + startup); no necesitamos sinks complejos.</item>
///   <item>Reusamos el patrón de rotación ya implementado en <c>error_dump.txt</c>.</item>
/// </list>
///
/// La rotación es la misma estrategia que usa <see cref="App.GuardarErrorDump"/>:
/// cuando <c>app.log</c> supera el tope se mueve a <c>app.1.log</c>
/// sobrescribiendo el anterior. Una sola generación es suficiente para
/// soporte remoto sin gastar disco.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxBytes = 1_000_000; // ~1 MB

    private readonly string _logFilePath;
    private readonly string _rotatedFilePath;
    private readonly object _writeLock = new();
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(LogLevel minimumLevel = LogLevel.Information)
    {
        _minimumLevel = minimumLevel;

        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ValeriosPizzeria",
            "Logs");
        Directory.CreateDirectory(carpeta);

        _logFilePath = Path.Combine(carpeta, "app.log");
        _rotatedFilePath = Path.Combine(carpeta, "app.1.log");
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _minimumLevel, AppendLine));

    public void Dispose() => _loggers.Clear();

    private void AppendLine(string line)
    {
        try
        {
            lock (_writeLock)
            {
                RotarSiHaceFalta();
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging nunca debe propagar excepciones al caller.
        }
    }

    private void RotarSiHaceFalta()
    {
        var info = new FileInfo(_logFilePath);
        if (!info.Exists || info.Length < MaxBytes) return;

        try
        {
            if (File.Exists(_rotatedFilePath)) File.Delete(_rotatedFilePath);
            File.Move(_logFilePath, _rotatedFilePath);
        }
        catch
        {
            // Si la rotación falla seguimos appendeando; mejor un archivo
            // grande que perder el log.
        }
    }

    /// <summary>
    /// Logger por categoría. Formatea cada evento con timestamp + nivel +
    /// categoría + mensaje (+ excepción si la hay) en una línea ASCII-ish.
    /// </summary>
    private sealed class FileLogger : ILogger
    {
        private readonly string _categoria;
        private readonly LogLevel _minimumLevel;
        private readonly Action<string> _append;

        public FileLogger(string categoria, LogLevel minimumLevel, Action<string> append)
        {
            _categoria = categoria;
            _minimumLevel = minimumLevel;
            _append = append;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && logLevel >= _minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var mensaje = formatter(state, exception);
            var sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ");
            sb.Append(NivelCorto(logLevel));
            sb.Append(' ');
            sb.Append(_categoria);
            sb.Append(" - ");
            sb.Append(mensaje);
            if (exception != null)
            {
                sb.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            }
            _append(sb.ToString());
        }

        private static string NivelCorto(LogLevel level) => level switch
        {
            LogLevel.Trace       => "TRC",
            LogLevel.Debug       => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning     => "WRN",
            LogLevel.Error       => "ERR",
            LogLevel.Critical    => "CRT",
            _ => "???"
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
