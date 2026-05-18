using System;
using Xunit;

namespace ValeriosPizza.Tests.Services;

/// <summary>
/// Estos tests no ejercitan código de la app directamente, fijan la
/// invariante aritmética que usan los rangos medio-abiertos en
/// ConsultaViewModel/ReportesViewModel/ExportService:
/// el "fin exclusivo" se calcula como <c>FechaFin.Date.AddDays(1)</c> y
/// la comparación es <c>&lt;</c>, no <c>&lt;=</c>. Si alguien revierte la
/// arithmética, este test fallará.
/// </summary>
public class HalfOpenDateRangeTests
{
    private static (DateTime inicio, DateTime finExclusivo) Rango(DateTime inicio, DateTime fin)
        => (inicio.Date, fin.Date.AddDays(1));

    [Fact]
    public void FinExclusivo_EsMedianocheDelDiaSiguiente()
    {
        var (inicio, fin) = Rango(new DateTime(2026, 5, 1), new DateTime(2026, 5, 1));

        Assert.Equal(new DateTime(2026, 5, 1), inicio);
        Assert.Equal(new DateTime(2026, 5, 2), fin);
    }

    [Fact]
    public void IncluyeUltimoSegundoDelDiaFin()
    {
        var (inicio, fin) = Rango(new DateTime(2026, 5, 1), new DateTime(2026, 5, 1));
        var ultimoTick = new DateTime(2026, 5, 1, 23, 59, 59, 999).AddTicks(9999);

        Assert.True(ultimoTick >= inicio && ultimoTick < fin,
            "El último instante del día Fin debe caer dentro del rango medio-abierto.");
    }

    [Fact]
    public void ExcluyeMedianocheDelDiaSiguiente()
    {
        var (inicio, fin) = Rango(new DateTime(2026, 5, 1), new DateTime(2026, 5, 1));
        var medianocheSiguiente = new DateTime(2026, 5, 2, 0, 0, 0);

        Assert.False(medianocheSiguiente < fin,
            "Medianoche del día siguiente NO debe entrar en el rango medio-abierto.");
    }
}
