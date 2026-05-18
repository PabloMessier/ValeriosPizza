using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ValeriosPizza.Services;
using ValeriosPizza.ViewModels;

namespace ValeriosPizza;

public partial class MainWindow : Window
{
    /// <summary>
    /// Indica que la salida ya fue confirmada (por ejemplo, desde el botón
    /// "Salir" del sidebar) para no volver a preguntar en el evento Closing.
    /// </summary>
    private bool _salidaConfirmada;

    public MainWindow()
    {
        InitializeComponent();
        // El DataContext lo asigna App.OnStartup tras resolver MainWindowViewModel
        // del contenedor de DI; aquí sólo inicializamos los recursos visuales.
        Loaded += MainWindow_Loaded;
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        // Conectamos el comando Salir del VM al cierre real de la ventana en
        // cuanto el DataContext esté disponible.
        if (e.NewValue is MainWindowViewModel vm)
        {
            vm.SolicitarSalir = SalirAplicacion;
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // El recordatorio de cierre necesita ejecutarse en el hilo de UI; lo
        // arrancamos cuando la ventana ya está visible para que el primer
        // MessageBox tenga un Owner válido.
        try
        {
            var reminder = App.Services.GetService(typeof(CierreReminderService)) as CierreReminderService;
            reminder?.Iniciar();
        }
        catch
        {
            // Un fallo del recordatorio nunca debe impedir que la app se use.
        }
    }

    /// <summary>
    /// Acción asociada al botón "Salir" del sidebar. Pide confirmación a la
    /// usuaria; si acepta, cierra la ventana principal y el evento Closing se
    /// encarga de verificar si hay datos sin guardar.
    /// </summary>
    private void SalirAplicacion()
    {
        var respuesta = MessageBox.Show(
            "¿Desea salir de Valerio's Pizza?",
            "Confirmar salida",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (respuesta == MessageBoxResult.Yes)
        {
            _salidaConfirmada = true;
            Close();
        }
    }

    /// <summary>
    /// Antes de cerrar, comprueba si quedan datos sin guardar en los formularios
    /// de "Registro Rápido" o "Mercancía Nueva". Si los hay, le ofrece a la
    /// usuaria guardar, descartar o cancelar el cierre.
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Si la salida no fue confirmada todavía (por ejemplo, el usuario
        // hizo clic en la X de la ventana o usó Alt+F4), pedir confirmación.
        if (!_salidaConfirmada)
        {
            var confirmar = MessageBox.Show(
                this,
                "¿Desea salir de Valerio's Pizza?",
                "Confirmar salida",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirmar != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            _salidaConfirmada = true;
        }

        if (DataContext is not MainWindowViewModel vm) return;

        var pendientes = new System.Collections.Generic.List<string>();
        pendientes.AddRange(vm.RegistroRapidoVM.SeccionesPendientes());
        pendientes.AddRange(vm.MercanciaNuevaVM.SeccionesPendientes());
        if (pendientes.Count == 0) return;

        var lista = string.Join("\n  • ", pendientes);
        var resp = MessageBox.Show(
            $"Hay datos sin guardar en las siguientes secciones:\n\n  • {lista}\n\n" +
            "¿Desea guardarlos antes de salir?\n\n" +
            "  • Sí: guardar lo que sea válido y salir\n" +
            "  • No: salir sin guardar (los datos se perderán)\n" +
            "  • Cancelar: volver a la aplicación",
            "Datos sin guardar - Valerio's Pizza",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (resp)
        {
            case MessageBoxResult.Cancel:
            case MessageBoxResult.None:
                e.Cancel = true;
                return;

            case MessageBoxResult.No:
                // Salir sin guardar; permitimos el cierre.
                return;

            case MessageBoxResult.Yes:
                IntentarGuardarTodoYReportar(vm, e);
                return;
        }
    }

    private static void IntentarGuardarTodoYReportar(
        MainWindowViewModel vm, System.ComponentModel.CancelEventArgs e)
    {
        var resultadoRegistro = vm.RegistroRapidoVM.IntentarGuardarTodoSilencioso();
        var (mercOk, mercMotivo) = vm.MercanciaNuevaVM.IntentarGuardarSilencioso();

        var guardadas = new System.Collections.Generic.List<string>(resultadoRegistro.Guardadas);
        var omitidas = new System.Collections.Generic.List<(string, string)>(resultadoRegistro.Omitidas);
        if (mercOk) guardadas.Add("Mercancía Nueva");
        else if (mercMotivo != null) omitidas.Add(("Mercancía Nueva", mercMotivo));

        if (omitidas.Count == 0)
        {
            // Todo lo que tenía datos quedó guardado; cerrar sin más diálogos.
            return;
        }

        var resumen = new System.Text.StringBuilder();
        resumen.AppendLine("No se pudieron guardar todas las secciones:");
        resumen.AppendLine();
        foreach (var (seccion, motivo) in omitidas)
        {
            resumen.AppendLine($"  • {seccion}: {motivo}");
        }
        if (guardadas.Count > 0)
        {
            resumen.AppendLine();
            resumen.AppendLine("Sí se guardaron:");
            foreach (var s in guardadas) resumen.AppendLine($"  • {s}");
        }
        resumen.AppendLine();
        resumen.AppendLine("¿Desea salir de todas formas y descartar las secciones no guardadas?");

        var respFinal = MessageBox.Show(
            resumen.ToString(),
            "Algunos datos no se pudieron guardar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (respFinal != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }

    /// <summary>
    /// Reenvía la rueda del ratón al <see cref="ScrollViewer"/> exterior de la
    /// página activa para que el desplazamiento funcione en cualquier punto de
    /// la ventana, incluso encima de controles (DataGrid, etc.) que normalmente
    /// capturan el evento.
    /// </summary>
    private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;

        // Si algún ComboBox tiene el dropdown abierto, lo cerramos antes de
        // permitir el scroll. El Popup que muestra los items está anclado al
        // ComboBox dentro del árbol visual, así que cuando la usuaria mueve
        // la rueda con el dropdown desplegado, el Popup "vibra" siguiendo el
        // scroll de la página. Cerrarlo elimina ese efecto y le da a la
        // usuaria un solo significado por gesto.
        if (CerrarComboBoxesAbiertos(this))
        {
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is not DependencyObject origen) return;

        // Recorre el árbol visual hacia arriba recolectando todos los
        // ScrollViewer entre el origen y la ventana. El primero encontrado es
        // el más interno; el último, el más externo (el de la página).
        ScrollViewer? interno = null;
        ScrollViewer? externo = null;
        DependencyObject? actual = origen;
        while (actual != null)
        {
            if (actual is ScrollViewer sv)
            {
                interno ??= sv;
                externo = sv;
            }
            actual = GetParentSafe(actual);
        }

        // Si el scroller interno todavía tiene espacio para desplazarse en la
        // dirección solicitada, dejamos que él consuma el evento (preserva el
        // scroll de DataGrids, listas, etc.).
        if (interno != null && interno != externo && PuedeDesplazarse(interno, e.Delta))
        {
            return;
        }

        if (externo == null) return;

        externo.ScrollToVerticalOffset(externo.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static bool PuedeDesplazarse(ScrollViewer sv, int delta)
    {
        if (sv.ScrollableHeight <= 0) return false;
        if (delta > 0 && sv.VerticalOffset > 0) return true;                    // hacia arriba
        if (delta < 0 && sv.VerticalOffset < sv.ScrollableHeight) return true;  // hacia abajo
        return false;
    }

    /// <summary>
    /// Obtiene el padre del nodo en el árbol visual cuando es posible y, en
    /// caso contrario, recurre al árbol lógico. <c>VisualTreeHelper.GetParent</c>
    /// lanza <see cref="System.InvalidOperationException"/> si el nodo no es
    /// <see cref="Visual"/> ni <see cref="Visual3D"/> (por ejemplo un
    /// <see cref="System.Windows.Documents.Run"/> dentro de un
    /// <see cref="TextBlock"/>), lo cual ocurría cuando la rueda del ratón se
    /// disparaba sobre texto rico y crasheaba la app.
    /// </summary>
    private static DependencyObject? GetParentSafe(DependencyObject node)
    {
        if (node is Visual or Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(node);
            if (visualParent != null) return visualParent;
        }
        return LogicalTreeHelper.GetParent(node);
    }

    /// <summary>
    /// Recorre el árbol visual a partir de <paramref name="root"/> y cierra
    /// cualquier <see cref="ComboBox"/> que tenga el dropdown abierto.
    /// Devuelve <c>true</c> si al menos uno fue cerrado para que el llamador
    /// pueda consumir el evento de la rueda y no propague el scroll.
    /// </summary>
    private static bool CerrarComboBoxesAbiertos(DependencyObject root)
    {
        bool cerrado = false;
        int hijos = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < hijos; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ComboBox cb && cb.IsDropDownOpen)
            {
                cb.IsDropDownOpen = false;
                cerrado = true;
            }
            if (CerrarComboBoxesAbiertos(child))
            {
                cerrado = true;
            }
        }
        return cerrado;
    }
}
