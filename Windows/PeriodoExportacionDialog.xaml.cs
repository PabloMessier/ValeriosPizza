using System;
using System.Windows;
using System.Windows.Controls;

namespace ValeriosPizza.Windows;

/// <summary>
/// Diálogo modal que permite a la usuaria elegir el período (hoy, semana,
/// mes, año, personalizado, etc.) que se aplicará a una exportación de
/// reportes (CSV, Excel o PDF) antes de generar el archivo. Soporta
/// fecha + hora exactas a través de un calendario y selectores de hora/min.
/// </summary>
public partial class PeriodoExportacionDialog : Window
{
    /// <summary>Fecha+hora de inicio elegida. Disponible si DialogResult == true.</summary>
    public DateTime FechaInicio { get; private set; }

    /// <summary>Fecha+hora de fin elegida. Disponible si DialogResult == true.</summary>
    public DateTime FechaFin { get; private set; }

    /// <summary>Texto descriptivo del período (p. ej. "Este mes", "Personalizado").</summary>
    public string EtiquetaPeriodo { get; private set; } = string.Empty;

    // Bandera para que rellenar los controles desde un atajo no dispare los
    // eventos *_SelectedDatesChanged y cambie inadvertidamente a "Personalizado".
    private bool _aplicandoAtajo;

    public PeriodoExportacionDialog(string formato)
    {
        InitializeComponent();
        EncabezadoTextBlock.Text = $"Exportar a {formato} - Seleccione el período";

        // Poblar combos de hora (00–23) y minutos (00, 05, …, 55).
        for (int h = 0; h < 24; h++)
        {
            var item = h.ToString("00");
            HoraInicioComboBox.Items.Add(item);
            HoraFinComboBox.Items.Add(item);
        }
        for (int m = 0; m < 60; m += 5)
        {
            var item = m.ToString("00");
            MinutoInicioComboBox.Items.Add(item);
            MinutoFinComboBox.Items.Add(item);
        }

        // Inicializar con el atajo por defecto (Este mes).
        AplicarAtajoEsteMes();
    }

    // -------------------- Atajos --------------------

    private void Atajo_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        var hoy = DateTime.Today;
        if (sender == RbHoy)              AplicarAtajo(hoy, hoy);
        else if (sender == RbAyer)        { var d = hoy.AddDays(-1); AplicarAtajo(d, d); }
        else if (sender == RbUltimos7)    AplicarAtajo(hoy.AddDays(-6), hoy);
        else if (sender == RbSemana)
        {
            var diasDesdeLunes = ((int)hoy.DayOfWeek - 1 + 7) % 7;
            AplicarAtajo(hoy.AddDays(-diasDesdeLunes), hoy);
        }
        else if (sender == RbUltimos30)   AplicarAtajo(hoy.AddDays(-29), hoy);
        else if (sender == RbEsteMes)     AplicarAtajoEsteMes();
        else if (sender == RbMesAnterior)
        {
            var ma = hoy.AddMonths(-1);
            var inicio = new DateTime(ma.Year, ma.Month, 1);
            AplicarAtajo(inicio, inicio.AddMonths(1).AddDays(-1));
        }
        else if (sender == RbEsteAno)
            AplicarAtajo(new DateTime(hoy.Year, 1, 1), hoy);
        else if (sender == RbAnoAnterior)
            AplicarAtajo(new DateTime(hoy.Year - 1, 1, 1), new DateTime(hoy.Year - 1, 12, 31));
        else if (sender == RbTodo)
            AplicarAtajo(new DateTime(2000, 1, 1), hoy);
    }

    private void AplicarAtajoEsteMes()
    {
        var hoy = DateTime.Today;
        AplicarAtajo(new DateTime(hoy.Year, hoy.Month, 1), hoy);
    }

    /// <summary>
    /// Refleja un atajo en el calendario y los selectores de hora (Desde 00:00,
    /// Hasta 23:55) sin disparar el cambio a modo "Personalizado".
    /// </summary>
    private void AplicarAtajo(DateTime inicio, DateTime fin)
    {
        _aplicandoAtajo = true;
        try
        {
            CalendarioInicio.SelectedDate = inicio.Date;
            CalendarioInicio.DisplayDate = inicio.Date;
            CalendarioFin.SelectedDate = fin.Date;
            CalendarioFin.DisplayDate = fin.Date;

            HoraInicioComboBox.SelectedItem = "00";
            MinutoInicioComboBox.SelectedItem = "00";
            HoraFinComboBox.SelectedItem = "23";
            MinutoFinComboBox.SelectedItem = "55";
        }
        finally
        {
            _aplicandoAtajo = false;
        }

        ActualizarResumenes();
    }

    // -------------------- Modo personalizado --------------------

    private void RbPersonalizado_Checked(object sender, RoutedEventArgs e)
    {
        // No alteramos el calendario: la usuaria seguirá editando lo que ya tenía.
        ActualizarResumenes();
    }

    private void CalendarioInicio_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoAtajo) return;
        CambiarAModoPersonalizado();
        ActualizarResumenes();
    }

    private void CalendarioFin_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoAtajo) return;
        CambiarAModoPersonalizado();
        ActualizarResumenes();
    }

    private void HoraOMinuto_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoAtajo) return;
        CambiarAModoPersonalizado();
        ActualizarResumenes();
    }

    private void CambiarAModoPersonalizado()
    {
        if (RbPersonalizado != null && RbPersonalizado.IsChecked != true)
        {
            RbPersonalizado.IsChecked = true;
        }
    }

    // -------------------- Resúmenes y validación --------------------

    private void ActualizarResumenes()
    {
        if (ResumenInicio == null || ResumenFin == null) return;

        if (TryConstruirFechaHora(CalendarioInicio, HoraInicioComboBox, MinutoInicioComboBox, out var ini))
            ResumenInicio.Text = $"Inicio: {ini:dd/MM/yyyy HH:mm}";
        else
            ResumenInicio.Text = "Inicio: —";

        if (TryConstruirFechaHora(CalendarioFin, HoraFinComboBox, MinutoFinComboBox, out var fin))
            ResumenFin.Text = $"Fin:    {fin:dd/MM/yyyy HH:mm}";
        else
            ResumenFin.Text = "Fin:    —";
    }

    private static bool TryConstruirFechaHora(Calendar cal, ComboBox horaCb, ComboBox minutoCb,
                                              out DateTime resultado)
    {
        resultado = default;
        if (cal.SelectedDate == null) return false;
        if (horaCb.SelectedItem is not string horaStr) return false;
        if (minutoCb.SelectedItem is not string minutoStr) return false;

        if (!int.TryParse(horaStr, out var h) || !int.TryParse(minutoStr, out var m)) return false;

        var dia = cal.SelectedDate.Value.Date;
        resultado = new DateTime(dia.Year, dia.Month, dia.Day, h, m, 0);
        return true;
    }

    // -------------------- Botones --------------------

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryConstruirFechaHora(CalendarioInicio, HoraInicioComboBox, MinutoInicioComboBox, out var inicio))
        {
            MostrarError("Seleccione la fecha y hora de inicio.");
            return;
        }
        if (!TryConstruirFechaHora(CalendarioFin, HoraFinComboBox, MinutoFinComboBox, out var fin))
        {
            MostrarError("Seleccione la fecha y hora de fin.");
            return;
        }
        if (inicio > fin)
        {
            MostrarError("La fecha/hora de inicio no puede ser posterior a la de fin.");
            return;
        }

        FechaInicio = inicio;
        FechaFin = fin;
        EtiquetaPeriodo = ObtenerEtiqueta(inicio, fin);
        DialogResult = true;
        Close();
    }

    private string ObtenerEtiqueta(DateTime inicio, DateTime fin)
    {
        // Si el atajo seleccionado coincide con los valores actuales, usamos su nombre;
        // de lo contrario es una selección personalizada.
        if (RbPersonalizado.IsChecked == true)
            return $"Personalizado ({inicio:dd/MM/yyyy HH:mm} – {fin:dd/MM/yyyy HH:mm})";
        if (RbHoy.IsChecked == true)         return "Hoy";
        if (RbAyer.IsChecked == true)        return "Ayer";
        if (RbUltimos7.IsChecked == true)    return "Últimos 7 días";
        if (RbSemana.IsChecked == true)      return "Esta semana";
        if (RbUltimos30.IsChecked == true)   return "Últimos 30 días";
        if (RbEsteMes.IsChecked == true)     return "Este mes";
        if (RbMesAnterior.IsChecked == true) return "Mes anterior";
        if (RbEsteAno.IsChecked == true)     return "Este año";
        if (RbAnoAnterior.IsChecked == true) return "Año anterior";
        if (RbTodo.IsChecked == true)        return "Todo el historial";
        return $"{inicio:dd/MM/yyyy HH:mm} – {fin:dd/MM/yyyy HH:mm}";
    }

    private void MostrarError(string mensaje)
    {
        MensajeError.Text = mensaje;
        MensajeError.Visibility = Visibility.Visible;
    }
}
