using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ValeriosPizza.Controls;

/// <summary>
/// Control numérico tipo "stepper" con tres elementos:
/// botón <c>−</c>, <see cref="TextBox"/> editable y botón <c>+</c>. Soporta:
/// <list type="bullet">
///   <item>Clic en + / − para sumar o restar <see cref="Step"/> al valor.</item>
///   <item>Mantener presionado (<c>PreviewMouseLeftButtonDown</c>) un botón
///         para entrar en repetición rápida: el primer paso se aplica al
///         instante, luego se acelera de forma progresiva.</item>
///   <item>Escribir un número directamente; el binding se actualiza al
///         perder el foco o al presionar Enter (Esc descarta).</item>
///   <item>Rueda del ratón sobre el TextBox para ajustar fino paso a paso.</item>
/// </list>
/// El valor se mantiene siempre dentro del rango [Minimum, Maximum] gracias
/// a un <c>CoerceValueCallback</c> en la <see cref="ValueProperty"/>.
/// </summary>
public partial class NumericStepper : UserControl
{
    // ============================================================
    //  Dependency Properties
    // ============================================================

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(NumericStepper),
            new FrameworkPropertyMetadata(
                defaultValue: 0,
                flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                propertyChangedCallback: null,
                coerceValueCallback: CoerceValue));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum), typeof(int), typeof(NumericStepper),
            new PropertyMetadata(0, OnLimitsChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum), typeof(int), typeof(NumericStepper),
            new PropertyMetadata(int.MaxValue, OnLimitsChanged));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(
            nameof(Step), typeof(int), typeof(NumericStepper),
            new PropertyMetadata(1));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Valor mínimo permitido (inclusivo). Default: 0.</summary>
    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Valor máximo permitido (inclusivo). Default: int.MaxValue.</summary>
    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Tamaño de cada paso. Default: 1.</summary>
    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        if (d is not NumericStepper s) return baseValue;
        var v = (int)baseValue;
        if (v < s.Minimum) v = s.Minimum;
        if (v > s.Maximum) v = s.Maximum;
        return v;
    }

    private static void OnLimitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Si cambian Min/Max, volver a coercionar el valor actual.
        d.CoerceValue(ValueProperty);
    }

    // ============================================================
    //  Auto-repeat (mantener presionado)
    // ============================================================

    private DispatcherTimer? _repeatTimer;

    /// <summary>+1 si el botón + está siendo mantenido; -1 si lo es el botón −; 0 si ninguno.</summary>
    private int _direccionRepeticion;

    /// <summary>Cuenta cuántos ticks han ocurrido en la ráfaga actual para acelerar.</summary>
    private int _ticksRafagaActual;

    // Tiempos calibrados para una experiencia de spinner típica:
    //  - 400 ms entre el primer paso y el segundo (evita ráfaga involuntaria
    //    si la usuaria sólo quiere un clic largo).
    //  - 60 ms entre pasos a partir del segundo (modo "rápido").
    //  - 25 ms después de ~30 pasos consecutivos (modo "turbo" para llegar
    //    rápido a números altos).
    private static readonly TimeSpan PrimerDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan PasoRapido = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan PasoTurbo = TimeSpan.FromMilliseconds(25);
    private const int TicksParaTurbo = 30;

    public NumericStepper()
    {
        InitializeComponent();
        // Si la ventana padre se cierra mientras un botón está presionado,
        // asegurarnos de soltar el timer para no dejar callbacks huérfanos.
        Unloaded += (_, _) => DetenerRepeticion();
    }

    // ---- Manejo del botón − ----

    private void DecrementButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IniciarRepeticion(-1, (UIElement)sender);
        e.Handled = true;
    }

    // ---- Manejo del botón + ----

    private void IncrementButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IniciarRepeticion(+1, (UIElement)sender);
        e.Handled = true;
    }

    // ---- Eventos comunes que terminan la repetición ----

    private void StepperButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement uie && uie.IsMouseCaptured)
        {
            uie.ReleaseMouseCapture();
        }
        DetenerRepeticion();
    }

    private void StepperButton_MouseLeave(object sender, MouseEventArgs e)
    {
        // Si el cursor sale del botón mientras está presionado, paramos la
        // ráfaga: es el comportamiento típico de un control nativo y evita
        // que la usuaria sume sin querer al arrastrar el ratón.
        DetenerRepeticion();
    }

    private void StepperButton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        DetenerRepeticion();
    }

    private void IniciarRepeticion(int direccion, UIElement boton)
    {
        // Mover el foco al control para que las flechas/Enter del teclado
        // sigan funcionando si el usuario suelta y luego usa el teclado.
        Focus();

        _direccionRepeticion = direccion;
        _ticksRafagaActual = 0;

        // Aplicar el primer paso inmediatamente.
        AplicarPaso();

        // Capturar el ratón para recibir el MouseUp aunque el cursor salga
        // del botón.
        boton.CaptureMouse();

        _repeatTimer?.Stop();
        _repeatTimer = new DispatcherTimer { Interval = PrimerDelay };
        _repeatTimer.Tick += RepeatTimer_Tick;
        _repeatTimer.Start();
    }

    private void RepeatTimer_Tick(object? sender, EventArgs e)
    {
        if (_direccionRepeticion == 0)
        {
            DetenerRepeticion();
            return;
        }

        AplicarPaso();
        _ticksRafagaActual++;

        // Tras el primer tick (después del delay inicial) cambiar al ritmo rápido.
        if (_ticksRafagaActual == 1 && _repeatTimer != null)
        {
            _repeatTimer.Interval = PasoRapido;
        }
        else if (_ticksRafagaActual == TicksParaTurbo && _repeatTimer != null)
        {
            _repeatTimer.Interval = PasoTurbo;
        }
    }

    private void DetenerRepeticion()
    {
        if (_repeatTimer != null)
        {
            _repeatTimer.Stop();
            _repeatTimer.Tick -= RepeatTimer_Tick;
            _repeatTimer = null;
        }
        _direccionRepeticion = 0;
        _ticksRafagaActual = 0;
    }

    private void AplicarPaso()
    {
        // Usar long en la suma para evitar wrap-around si Step es grande y
        // Value está cerca del límite int.MaxValue.
        long nuevo = (long)Value + (long)_direccionRepeticion * Math.Max(1, Step);
        if (nuevo < Minimum) nuevo = Minimum;
        if (nuevo > Maximum) nuevo = Maximum;
        Value = (int)nuevo;
    }

    // ============================================================
    //  TextBox: input filtering, Enter / Esc, rueda del ratón
    // ============================================================

    // Acepta dígitos, y un signo "-" sólo si Minimum permite negativos y
    // está al principio del texto. Cualquier otro carácter se rechaza.
    private static readonly Regex SoloDigitos = new(@"^[0-9]+$", RegexOptions.Compiled);

    private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox tb) return;

        // Reconstruir lo que QUEDARÍA en el textbox si aceptáramos la entrada.
        var futuro = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                            .Insert(tb.SelectionStart, e.Text);

        if (Minimum < 0)
        {
            // Permitir un signo - opcional al inicio.
            if (futuro == "-" || (futuro.StartsWith('-') && SoloDigitos.IsMatch(futuro[1..])))
            {
                return;
            }
            if (SoloDigitos.IsMatch(futuro)) return;
        }
        else
        {
            if (SoloDigitos.IsMatch(futuro)) return;
        }

        e.Handled = true;
    }

    private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        switch (e.Key)
        {
            case Key.Enter:
                // Forzar commit del texto al binding.
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                // Volver a leer el valor coercionado y reflejarlo en el TextBox.
                tb.Text = Value.ToString(CultureInfo.InvariantCulture);
                tb.SelectAll();
                e.Handled = true;
                break;

            case Key.Escape:
                // Descartar cambios pendientes en el TextBox.
                tb.Text = Value.ToString(CultureInfo.InvariantCulture);
                tb.SelectAll();
                e.Handled = true;
                break;

            case Key.Up:
                IncrementarUnPaso(+1);
                e.Handled = true;
                break;

            case Key.Down:
                IncrementarUnPaso(-1);
                e.Handled = true;
                break;
        }
    }

    private void ValueTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // Cuando el TextBox recibe el foco, seleccionar todo el contenido
        // para que la usuaria pueda sobrescribir el valor sin tener que
        // borrar primero.
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private void ValueTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Sólo respondemos a la rueda si el TextBox tiene el foco; de lo
        // contrario dejamos que el scroll de la página haga lo suyo.
        if (sender is TextBox tb && tb.IsKeyboardFocusWithin)
        {
            IncrementarUnPaso(e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        }
    }

    private void IncrementarUnPaso(int signo)
    {
        long nuevo = (long)Value + (long)signo * Math.Max(1, Step);
        if (nuevo < Minimum) nuevo = Minimum;
        if (nuevo > Maximum) nuevo = Maximum;
        Value = (int)nuevo;
    }
}
