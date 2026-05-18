using System;
using ValeriosPizza.ViewModels;
using Xunit;

namespace ValeriosPizza.Tests.ViewModels;

/// <summary>
/// <see cref="DiaResumen.IsToday"/> reemplazó al hex-string fijo y es lo
/// que la XAML consulta para resaltar la columna de "hoy" con el brush
/// de tema correspondiente.
/// </summary>
public class DiaResumenIsTodayTests
{
    [Fact]
    public void IsToday_TrueParaFechaActual()
    {
        var dia = new DiaResumen { FechaCompleta = DateTime.Today };

        Assert.True(dia.IsToday);
    }

    [Fact]
    public void IsToday_TrueAunqueLaFechaTengaHora()
    {
        var dia = new DiaResumen { FechaCompleta = DateTime.Today.AddHours(13).AddMinutes(45) };

        Assert.True(dia.IsToday);
    }

    [Fact]
    public void IsToday_FalseParaAyerYManana()
    {
        var ayer = new DiaResumen { FechaCompleta = DateTime.Today.AddDays(-1) };
        var manana = new DiaResumen { FechaCompleta = DateTime.Today.AddDays(1) };

        Assert.False(ayer.IsToday);
        Assert.False(manana.IsToday);
    }
}
