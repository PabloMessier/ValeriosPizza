using ValeriosPizza.ViewModels;
using Xunit;

namespace ValeriosPizza.Tests.ViewModels;

/// <summary>
/// El balance de un ingrediente es una función pura
/// <c>(Entradas + Mercancía) − (Gastos + Mermas)</c>. Estos tests fijan
/// la fórmula y los formatos auxiliares.
/// </summary>
public class ResumenIngredienteBalanceTests
{
    [Fact]
    public void Balance_SumaEntradasYMercanciaRestaGastosYMermas()
    {
        var r = new ResumenIngrediente
        {
            TotalEntradas = 10,
            TotalMercancia = 5,
            TotalGastos = 4,
            TotalMermas = 1
        };

        Assert.Equal(10, r.Balance);
    }

    [Fact]
    public void BalanceTexto_PositivoIncluyeSignoMas()
    {
        var r = new ResumenIngrediente { TotalEntradas = 3.5, TotalGastos = 1 };

        Assert.StartsWith("+", r.BalanceTexto);
    }

    [Fact]
    public void BalanceTexto_NegativoConservaSigno()
    {
        var r = new ResumenIngrediente { TotalEntradas = 1, TotalGastos = 5 };

        Assert.StartsWith("-", r.BalanceTexto);
    }
}
