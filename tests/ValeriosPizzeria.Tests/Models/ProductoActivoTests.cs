using ValeriosPizza.Models;
using Xunit;

namespace ValeriosPizza.Tests.Models;

/// <summary>
/// La bandera legacy <see cref="Producto.Activo"/> debe derivarse de
/// <see cref="EstadoProducto"/> y nunca divergir aunque alguien intente
/// asignarla manualmente.
/// </summary>
public class ProductoActivoTests
{
    [Theory]
    [InlineData(EstadoProducto.Activo, true)]
    [InlineData(EstadoProducto.Agotado, true)]
    [InlineData(EstadoProducto.Descontinuado, false)]
    public void Activo_SeDerivaDeEstado(EstadoProducto estado, bool esperado)
    {
        var p = new Producto { Estado = estado };

        Assert.Equal(esperado, p.Activo);
    }

    [Fact]
    public void Activo_SetterEsNoOp()
    {
        // Producto descontinuado: Activo debería seguir siendo false sin
        // importar lo que se intente asignar.
        var p = new Producto { Estado = EstadoProducto.Descontinuado };

        p.Activo = true; // intento de "engañar" la invariante

        Assert.False(p.Activo);
    }

    [Fact]
    public void Activo_CambiaCuandoCambiaEstado()
    {
        var p = new Producto { Estado = EstadoProducto.Activo };
        Assert.True(p.Activo);

        p.Estado = EstadoProducto.Descontinuado;
        Assert.False(p.Activo);

        p.Estado = EstadoProducto.Agotado;
        Assert.True(p.Activo);
    }
}
