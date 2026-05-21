using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;
using ValeriosPizza.Data;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza;

// CA1001 normalmente exige que cualquier tipo con campos IDisposable
// implemente IDisposable. En este caso <see cref="App"/> hereda de
// <see cref="Application"/>, que tiene su propio ciclo de vida (<c>OnExit</c>),
// y los recursos (_host, _singleInstanceMutex) se liberan ahí explícitamente.
// Añadir <c>IDisposable</c> no aporta nada porque nadie llama Dispose sobre
// la instancia de la Application.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Application lifetime managed by WPF; disposal happens in OnExit.")]
public partial class App : Application
{
    /// <summary>
    /// Carpeta donde se guarda el archivo de errores. Se ubica junto al
    /// ejecutable para que el usuario pueda encontrarlo fácilmente y enviarlo
    /// a soporte sin tener que navegar a %LocalAppData%.
    /// Se usa <see cref="Environment.ProcessPath"/> en lugar de
    /// <c>AppContext.BaseDirectory</c> porque, en publicaciones de archivo
    /// único con extracción, BaseDirectory apunta a la carpeta temporal de
    /// extracción y no a la carpeta real del .exe.
    /// </summary>
    private static readonly string LogFolder =
        Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
        ?? AppContext.BaseDirectory;

    /// <summary>
    /// Archivo único donde se acumulan todos los errores. Si ya existe, los
    /// nuevos errores se agregan al final en lugar de crear archivos nuevos.
    /// </summary>
    private static readonly string LogFilePath = Path.Combine(LogFolder, "error_dump.txt");

    /// <summary>
    /// Lock para evitar entrelazado de escrituras cuando varios hilos fallan
    /// simultáneamente (UI + tarea de fondo + finalizer, etc.).
    /// </summary>
    private static readonly object LogFileLock = new();

    // Win32: permite a una app WPF (WinExe) reusar la consola del proceso padre
    // (por ejemplo, la terminal donde se ejecutó `dotnet run`) para imprimir
    // diagnósticos. Si no hay consola padre, simplemente falla silenciosamente.
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    // Win32 (user32) para localizar y traer al frente la ventana de la
    // instancia previa cuando se intenta abrir la app por segunda vez.
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    /// <summary>
    /// Nombre del Mutex global usado para detectar instancias previas. El
    /// prefijo <c>Local\</c> limita el alcance a la sesión de Windows del
    /// usuario actual (no es global a todo el equipo), que es lo que queremos
    /// para una app de escritorio mono-usuario.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\ValeriosPizzeria.SingleInstance.v1";

    /// <summary>
    /// Handle del Mutex mantenido vivo durante toda la sesión del proceso
    /// para que ninguna otra instancia pueda adquirirlo mientras estemos en
    /// ejecución. Se libera automáticamente al salir del proceso.
    /// </summary>
    private Mutex? _singleInstanceMutex;

    /// <summary>
    /// Generic Host activo durante la vida del proceso. Construido en
    /// <see cref="OnStartup"/> y dispuesto en <see cref="OnExit"/>.
    /// </summary>
    private IHost? _host;

    /// <summary>
    /// Acceso estático al contenedor de servicios para llamadores que aún no
    /// reciben sus dependencias por constructor (por ejemplo código legacy en
    /// dialógos o servicios estáticos). Idealmente todo flujo nuevo debe
    /// recibir sus dependencias por inyección en lugar de leer este estático.
    /// </summary>
    public static IServiceProvider Services => Current is App app && app._host != null
        ? app._host.Services
        : throw new InvalidOperationException(
            "El contenedor de DI no está disponible: la app aún no se ha iniciado.");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Engancharse a la consola del padre (si existe) ANTES de cualquier otra
        // cosa para que los errores tempranos sean visibles en `dotnet run`.
        AttachConsole(ATTACH_PARENT_PROCESS);

        // Single-instance: si ya hay otra instancia en ejecución, traerla al
        // frente y salir sin desplegar la ventana. Esto evita que el doble
        // clic en el icono de la app abra múltiples copias.
        if (!IntentarAdquirirInstanciaUnica())
        {
            ActivarInstanciaExistente();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Aplicar el tema guardado por el usuario (Light / Dark / Metallic).
        ThemeManager.AplicarTema(ThemeManager.CargarPreferencia());

        // Configurar manejo global de errores
        ConfigurarManejadorErrores();

        try
        {
            // QuestPDF requiere aceptar la licencia comunitaria.
            QuestPDF.Settings.License = LicenseType.Community;

            _host = ConstruirHost();

            // Backup automático + bootstrap de esquema. El initializer captura
            // sus propias excepciones para que un fallo en el backup no impida
            // que la app arranque.
            var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
            initializer.Inicializar((ex, contexto) =>
            {
                GuardarErrorDump(ex, contexto);
                EscribirErrorEnConsola(ex, contexto);
            });

            // Construir la ventana principal con su VM vía DI.
            var mainVm = _host.Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm };
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            GuardarErrorDump(ex, "Error al inicializar la aplicación");
            EscribirErrorEnConsola(ex, "Error al inicializar la aplicación");
            MessageBox.Show(
                $"Error al iniciar la aplicación.\n\n{ex.GetType().Name}: {ex.Message}\n\nSe ha guardado un archivo de diagnóstico en:\n{LogFilePath}",
                "Error de Inicio",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        // Liberar el Mutex de instancia única al cerrar, para que la siguiente
        // ejecución pueda adquirirlo limpiamente.
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Si no éramos el dueño (caso de la 2ª instancia) ignoramos.
        }
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }

    /// <summary>
    /// Intenta adquirir el Mutex de instancia única. Devuelve <c>true</c> si
    /// somos la única instancia (y por lo tanto debemos arrancar normalmente),
    /// o <c>false</c> si ya hay otra corriendo (y deberíamos cederle el foco).
    /// </summary>
    private bool IntentarAdquirirInstanciaUnica()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true,
                name: SingleInstanceMutexName, out var creadoNuevo);
            if (creadoNuevo) return true;

            // El Mutex ya existía; no somos la primera instancia. Disponemos
            // del handle para no retener referencia al semáforo de la otra.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            return false;
        }
        catch (Exception ex)
        {
            // Si la creación del Mutex falla (por seguridad de la sesión o
            // similar), preferimos dejar arrancar la app que bloquearla.
            GuardarErrorDump(ex, "Crear Mutex de instancia única");
            return true;
        }
    }

    /// <summary>
    /// Busca el proceso de la instancia previa de Valerio's Pizza y trae
    /// su ventana principal al frente (restaurándola si está minimizada).
    /// </summary>
    private static void ActivarInstanciaExistente()
    {
        try
        {
            var actual = Process.GetCurrentProcess();
            var otros = Process.GetProcessesByName(actual.ProcessName)
                .Where(p => p.Id != actual.Id)
                .ToArray();

            foreach (var p in otros)
            {
                var handle = p.MainWindowHandle;
                if (handle == IntPtr.Zero) continue;

                if (IsIconic(handle))
                {
                    ShowWindow(handle, SW_RESTORE);
                }
                else
                {
                    ShowWindow(handle, SW_SHOW);
                }
                SetForegroundWindow(handle);
                return;
            }
        }
        catch (Exception ex)
        {
            // Si fallamos al activar la ventana previa, no insistimos: el
            // shutdown subsecuente cierra esta instancia y el usuario puede
            // alternar manualmente desde la barra de tareas.
            GuardarErrorDump(ex, "Activar instancia existente");
        }
    }

    /// <summary>
    /// Compone el host genérico de .NET con todos los servicios y ViewModels
    /// que la app necesita. Mantener centralizada la composición facilita
    /// reemplazar implementaciones por dobles en pruebas y evita la dispersión
    /// de <c>new PizzeriaDbContext()</c> a lo largo del código.
    /// </summary>
    private static IHost ConstruirHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                // Generic Host ya añade Console + Debug + EventSource +
                // EventLog (Windows). Limitamos el nivel global a
                // Information para no inundar la salida de Debug en
                // producción; las consultas de EF se silencian aparte.
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

                // Sink a archivo (%LOCALAPPDATA%\ValeriosPizzeria\Logs\app.log)
                // para que el log persista en la PC de la dueña y se pueda
                // enviar a soporte sin esperar a que ocurra una excepción
                // (que iría a error_dump.txt). Rota a 1 MB.
                logging.AddProvider(new FileLoggerProvider(LogLevel.Information));
            })
            .ConfigureServices((_, services) =>
            {
                // Factoría de DbContext: cada llamador crea un context dedicado
                // y lo dispone, evitando contienda entre VMs sin compartir uno solo.
                services.AddDbContextFactory<PizzeriaDbContext>(o =>
                {
                    var dir = Path.GetDirectoryName(PizzeriaDbContext.DefaultDbPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    o.UseSqlite($"Data Source={PizzeriaDbContext.DefaultDbPath}");
                    // Por defecto las consultas NO trackean entidades:
                    // la inmensa mayoría son lecturas para mostrar en UI
                    // (Reportes, Consulta, Inventario, Dashboard). Las
                    // mutaciones usan Find() (tracking implícito) o
                    // AsTracking() explícito cuando hace falta.
                    o.UseQueryTrackingBehavior(
                        Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTracking);
                });

                services.AddSingleton<DatabaseInitializer>();

                // ExportService usa la factoría de DbContext de DI; lo
                // registramos como singleton porque no mantiene estado
                // mutable (sólo lee la BD bajo demanda).
                services.AddSingleton<ExportService>();

                // Recordatorio de cierre diario. Singleton porque mantiene
                // estado (último día avisado) y es propietario de un timer.
                services.AddSingleton<CierreReminderService>();

                // Historial de deshacer/rehacer. Singleton porque comparte
                // pila entre todos los VMs y persiste a disco.
                services.AddSingleton<UndoRedoService>();

                // ViewModels: transient porque son baratos y casi todos
                // mantienen estado de pantalla. MainWindowViewModel se resuelve
                // una sola vez al construir la ventana.
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<RegistroRapidoViewModel>();
                services.AddTransient<MercanciaNuevaViewModel>();
                services.AddTransient<InventarioViewModel>();
                services.AddTransient<ConsultaViewModel>();
                services.AddTransient<ReportesViewModel>();
                // BodegaVM como singleton: el conteo y el listado son compartidos
                // entre la pantalla "Bodega" del sidebar y los emisores de
                // BodegaChangedMessage en otras pantallas.
                services.AddSingleton<BodegaViewModel>();
                services.AddSingleton<MainWindowViewModel>();
            })
            .Build();
    }

    private void ConfigurarManejadorErrores()
    {
        // Errores no manejados en el hilo de UI
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // Errores no manejados en hilos secundarios
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // Errores en tareas asíncronas
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        GuardarErrorDump(e.Exception, "Error en interfaz de usuario");
        EscribirErrorEnConsola(e.Exception, "Error en interfaz de usuario");
        MostrarMensajeError();
        e.Handled = true; // Evitar que la app se cierre abruptamente
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            GuardarErrorDump(ex, "Error crítico no manejado");
            EscribirErrorEnConsola(ex, "Error crítico no manejado");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        GuardarErrorDump(e.Exception, "Error en tarea asíncrona");
        EscribirErrorEnConsola(e.Exception, "Error en tarea asíncrona");
        e.SetObserved();
    }

    /// <summary>
    /// Imprime un resumen del error a stderr para que sea visible cuando la
    /// app se ejecuta desde una terminal (por ejemplo con `dotnet run`).
    /// Como WPF se compila como WinExe, la salida sólo es visible si
    /// AttachConsole tuvo éxito al inicio.
    /// </summary>
    private static void EscribirErrorEnConsola(Exception ex, string contexto)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("========================================================");
            sb.AppendLine($"[VALERIO'S PIZZA] {contexto}");
            sb.AppendLine($"  {ex.GetType().FullName}: {ex.Message}");
            var inner = ex.InnerException;
            int nivel = 1;
            while (inner != null)
            {
                sb.AppendLine($"  └─ Inner ({nivel}) {inner.GetType().FullName}: {inner.Message}");
                inner = inner.InnerException;
                nivel++;
            }
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                sb.AppendLine("  Stack trace:");
                sb.AppendLine(ex.StackTrace);
            }
            sb.AppendLine($"  (Detalles completos en: {LogFilePath})");
            sb.AppendLine("========================================================");

            Console.Error.WriteLine(sb.ToString());
            Console.Error.Flush();
        }
        catch
        {
            // No hay consola adjunta o ya está cerrada; ignorar.
        }
    }

    /// <summary>
    /// Guarda un reporte de error en <see cref="LogFilePath"/>. El archivo se
    /// crea si no existe y se va acumulando: cada error nuevo se anexa al
    /// final con su propio bloque, separado del anterior. De esta forma queda
    /// un único archivo con el historial completo de errores junto al .exe.
    /// </summary>
    public static void GuardarErrorDump(Exception ex, string contexto)
    {
        try
        {
            var sb = new StringBuilder();
            var existe = File.Exists(LogFilePath);

            // Cabecera del archivo (solo la primera vez).
            if (!existe)
            {
                sb.AppendLine("═══════════════════════════════════════════════════════════════");
                sb.AppendLine("     VALERIO'S PIZZA - HISTORIAL DE ERRORES");
                sb.AppendLine("═══════════════════════════════════════════════════════════════");
                sb.AppendLine("Por favor envíe este archivo a soporte técnico.");
                sb.AppendLine();
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"     REPORTE DE ERROR - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Contexto: {contexto}");
            sb.AppendLine($"Versión del Sistema: {Environment.OSVersion}");
            sb.AppendLine($"Versión .NET: {Environment.Version}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("INFORMACIÓN DEL ERROR:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine();
            sb.AppendLine($"Tipo de Error: {ex.GetType().FullName}");
            sb.AppendLine($"Mensaje: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.StackTrace ?? "No disponible");
            sb.AppendLine();

            // Excepciones internas
            var innerEx = ex.InnerException;
            int nivel = 1;
            while (innerEx != null)
            {
                sb.AppendLine($"───────────────────────────────────────────────────────────────");
                sb.AppendLine($"EXCEPCIÓN INTERNA (Nivel {nivel}):");
                sb.AppendLine($"───────────────────────────────────────────────────────────────");
                sb.AppendLine($"Tipo: {innerEx.GetType().FullName}");
                sb.AppendLine($"Mensaje: {innerEx.Message}");
                sb.AppendLine($"Stack Trace: {innerEx.StackTrace ?? "No disponible"}");
                sb.AppendLine();
                innerEx = innerEx.InnerException;
                nivel++;
            }

            sb.AppendLine();

            // Bloqueo para serializar escrituras concurrentes desde varios hilos.
            lock (LogFileLock)
            {
                RotarLogSiHaceFalta();
                File.AppendAllText(LogFilePath, sb.ToString());
            }
        }
        catch
        {
            // Si falla el guardado del log, no hacer nada para evitar cascada de errores
        }
    }

    /// <summary>
    /// Si el log actual supera el tope, lo mueve a <c>error_dump.1.txt</c>
    /// (sobrescribiendo el rotado anterior) para que el archivo "vivo" no
    /// crezca sin límite y se mantenga manejable para enviarlo a soporte.
    /// Debe llamarse dentro de <see cref="LogFileLock"/>.
    /// </summary>
    private const long LogFileMaxBytes = 1_000_000; // ~1 MB
    private static void RotarLogSiHaceFalta()
    {
        try
        {
            var info = new FileInfo(LogFilePath);
            if (!info.Exists || info.Length < LogFileMaxBytes) return;

            var rotado = Path.Combine(LogFolder, "error_dump.1.txt");
            if (File.Exists(rotado)) File.Delete(rotado);
            File.Move(LogFilePath, rotado);
        }
        catch
        {
            // No interrumpir el flujo de logging por un fallo de rotación.
        }
    }

    private void MostrarMensajeError()
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"Ha ocurrido un error inesperado.\n\n" +
                $"Se ha guardado un archivo de diagnóstico en:\n{LogFilePath}\n\n" +
                $"Por favor envíe este archivo a soporte técnico para ayudar a solucionar el problema.",
                "Error - Valerio's Pizza",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        });
    }
}