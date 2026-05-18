using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Input;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;

namespace ValeriosPizza.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _titulo = "Valerio's Pizza - Sistema de Inventario";

    public DashboardViewModel DashboardVM { get; }
    public RegistroRapidoViewModel RegistroRapidoVM { get; }
    public MercanciaNuevaViewModel MercanciaNuevaVM { get; }
    public InventarioViewModel InventarioVM { get; }
    public ConsultaViewModel ConsultaVM { get; }
    public ReportesViewModel ReportesVM { get; }
    public BodegaViewModel BodegaVM { get; }

    public ICommand NavDashboardCommand { get; }
    public ICommand NavRegistroCommand { get; }
    public ICommand NavMercanciaCommand { get; }
    public ICommand NavInventarioCommand { get; }
    public ICommand NavConsultaCommand { get; }
    public ICommand NavReportesCommand { get; }
    public ICommand NavBodegaCommand { get; }

    /// <summary>
    /// Cambia el tema visual de la aplicación. Recibe el <see cref="AppTheme"/>
    /// como parámetro para que un único comando sirva a los 4 botones del
    /// sidebar (antes eran 4 handlers de Click distintos en code-behind).
    /// </summary>
    public ICommand CambiarTemaCommand { get; }

    /// <summary>
    /// Comandos de Deshacer / Rehacer expuestos en la barra superior y
    /// asociados a Ctrl+Z / Ctrl+Y. Su CanExecute observa
    /// <see cref="UndoRedoService.PuedeDeshacer"/> y
    /// <see cref="UndoRedoService.PuedeRehacer"/>.
    /// </summary>
    public IAsyncRelayCommand DeshacerCommand { get; }
    public IAsyncRelayCommand RehacerCommand { get; }

    /// <summary>Servicio compartido del historial; expuesto para que la UI bindee tooltips/estado.</summary>
    public UndoRedoService UndoRedo { get; }

    /// <summary>
    /// Solicita el cierre de la ventana principal. La ventana se encarga de
    /// confirmar y de chequear datos sin guardar en su evento Closing.
    /// </summary>
    public ICommand SalirCommand { get; }

    /// <summary>
    /// Constructor inyectado desde el contenedor de DI. Recibe los VMs hijos
    /// ya construidos (cada uno con su propio <c>IDbContextFactory</c>) en
    /// vez de instanciarlos con <c>new</c>, para mantener una sola fuente de
    /// verdad de la composición y poder reemplazarlos por dobles en pruebas.
    /// </summary>
    public MainWindowViewModel(
        DashboardViewModel dashboard,
        RegistroRapidoViewModel registroRapido,
        MercanciaNuevaViewModel mercanciaNueva,
        InventarioViewModel inventario,
        ConsultaViewModel consulta,
        ReportesViewModel reportes,
        BodegaViewModel bodega,
        UndoRedoService undoRedo)
    {
        DashboardVM = dashboard;
        RegistroRapidoVM = registroRapido;
        MercanciaNuevaVM = mercanciaNueva;
        InventarioVM = inventario;
        ConsultaVM = consulta;
        ReportesVM = reportes;
        BodegaVM = bodega;
        UndoRedo = undoRedo;

        // Configurar comandos de navegación
        NavDashboardCommand = new RelayCommand(() =>
        {
            // Disparamos la recarga sin bloquear el cambio de vista; el VM
            // gestiona internamente sus excepciones (vuelca al log).
            _ = DashboardVM.ActualizarDatosAsync();
            CurrentView = DashboardVM;
        });
        NavRegistroCommand = new RelayCommand(() => CurrentView = RegistroRapidoVM);
        NavMercanciaCommand = new RelayCommand(() => CurrentView = MercanciaNuevaVM);
        NavInventarioCommand = new RelayCommand(() => CurrentView = InventarioVM);
        NavConsultaCommand = new RelayCommand(() => CurrentView = ConsultaVM);
        NavReportesCommand = new RelayCommand(() => CurrentView = ReportesVM);
        // Bodega: refresca el contenido al navegar (la lista puede haberse
        // alimentado desde otras pantallas vía BodegaChangedMessage, pero un
        // refresco explícito al entrar evita estados "stale" si el messenger
        // se perdió por algún motivo).
        NavBodegaCommand = new RelayCommand(() =>
        {
            _ = BodegaVM.CargarAsync();
            CurrentView = BodegaVM;
        });

        // Tema: comando único parametrizado por AppTheme.
        CambiarTemaCommand = new RelayCommand<AppTheme>(tema => ThemeManager.AplicarTema(tema));

        // Salir: el handler real vive en MainWindow (necesita acceso a Window.Close()
        // y al evento Closing). Este comando solo dispara la acción inyectada.
        SalirCommand = new RelayCommand(() => SolicitarSalir?.Invoke());

        // Deshacer / Rehacer. Reconsultamos CanExecute cada vez que el
        // servicio actualiza sus banderas para que los botones se habiliten
        // / deshabiliten automáticamente.
        DeshacerCommand = new AsyncRelayCommand(EjecutarDeshacerAsync, () => UndoRedo.PuedeDeshacer);
        RehacerCommand = new AsyncRelayCommand(EjecutarRehacerAsync, () => UndoRedo.PuedeRehacer);
        UndoRedo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UndoRedoService.PuedeDeshacer)
                or nameof(UndoRedoService.PuedeRehacer))
            {
                DeshacerCommand.NotifyCanExecuteChanged();
                RehacerCommand.NotifyCanExecuteChanged();
            }
        };

        // Vista inicial
        CurrentView = DashboardVM;
    }

    private async System.Threading.Tasks.Task EjecutarDeshacerAsync()
    {
        try { await UndoRedo.DeshacerAsync(); }
        catch (System.Exception ex)
        {
            MessageBox.Show($"No se pudo deshacer la última acción.\n\n{ex.Message}",
                "Deshacer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async System.Threading.Tasks.Task EjecutarRehacerAsync()
    {
        try { await UndoRedo.RehacerAsync(); }
        catch (System.Exception ex)
        {
            MessageBox.Show($"No se pudo rehacer la acción.\n\n{ex.Message}",
                "Rehacer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Hook que la ventana enlaza para implementar el cierre real (incluye
    /// confirmación y verificación de datos sin guardar).
    /// </summary>
    public System.Action? SolicitarSalir { get; set; }
}
