using System;
using ValeriosPizza.Services;
using Xunit;

namespace ValeriosPizza.Tests.Services;

/// <summary>
/// El cálculo de rangos de borrado es una función pura, sin DB. Probamos
/// que devuelve intervalos medio-abiertos [inicio, fin) que cubren
/// exactamente el periodo solicitado.
/// </summary>
public class DatabaseWipeServiceCalcularRangoTests
{
    [Fact]
    public void Hoy_DevuelveDelDiaActualAlSiguienteMedianoche()
    {
        var (inicio, fin) = DatabaseWipeService.CalcularRango(AlcanceBorrado.Hoy);

        Assert.Equal(DateTime.Today, inicio);
        Assert.Equal(DateTime.Today.AddDays(1), fin);
        Assert.Equal(TimeSpan.FromDays(1), fin - inicio);
    }

    [Fact]
    public void EstaSemana_EmpiezaEnLunesYDuraSieteDias()
    {
        var (inicio, fin) = DatabaseWipeService.CalcularRango(AlcanceBorrado.EstaSemana);

        Assert.Equal(DayOfWeek.Monday, inicio.DayOfWeek);
        Assert.Equal(TimeSpan.FromDays(7), fin - inicio);
        // El día actual debe caer dentro del rango.
        Assert.InRange(DateTime.Today, inicio, fin.AddTicks(-1));
    }

    [Fact]
    public void EsteMes_EmpiezaElDia1YDuraExactamenteUnMes()
    {
        var (inicio, fin) = DatabaseWipeService.CalcularRango(AlcanceBorrado.EsteMes);

        Assert.Equal(1, inicio.Day);
        Assert.Equal(DateTime.Today.Year, inicio.Year);
        Assert.Equal(DateTime.Today.Month, inicio.Month);
        Assert.Equal(inicio.AddMonths(1), fin);
    }

    [Fact]
    public void EsteAno_EmpiezaEl1DeEneroYDuraUnAnio()
    {
        var (inicio, fin) = DatabaseWipeService.CalcularRango(AlcanceBorrado.EsteAno);

        Assert.Equal(1, inicio.Day);
        Assert.Equal(1, inicio.Month);
        Assert.Equal(DateTime.Today.Year, inicio.Year);
        Assert.Equal(inicio.AddYears(1), fin);
    }

    [Fact]
    public void RangoPersonalizado_DevuelveSentinelas()
    {
        var (inicio, fin) = DatabaseWipeService.CalcularRango(AlcanceBorrado.RangoPersonalizado);

        Assert.Equal(DateTime.MinValue, inicio);
        Assert.Equal(DateTime.MaxValue, fin);
    }

    [Fact]
    public void TodoElCatalogo_DevuelveSentinelas()
    {
        var (inicio, fin) = DatabaseWipeService.CalcularRango(AlcanceBorrado.TodoElCatalogo);

        Assert.Equal(DateTime.MinValue, inicio);
        Assert.Equal(DateTime.MaxValue, fin);
    }
}
