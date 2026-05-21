using System;
using System.IO;
using System.Threading.Tasks;
using ValeriosPizza.Models;
using ValeriosPizza.Services;
using Xunit;

namespace ValeriosPizza.Tests.Services;

/// <summary>
/// Smoke tests del <see cref="ExportService"/>. Verifican que el round-trip
/// "cargar datos de la BD → exportar a CSV" produce un archivo con los
/// registros esperados. No probamos la generación de Excel/PDF aquí porque
/// requeriría parsear el binario; el CSV es suficiente para detectar
/// regresiones de mapeo de columnas y filtrado por fecha.
/// </summary>
public class ExportServiceTests
{
    [Fact]
    public async Task ExportarCsv_IncluyeEntradasDelPeriodo()
    {
        using var fx = new InMemoryDbFixture();

        // Arrange: un ingrediente y dos entradas, una dentro del periodo
        // y otra fuera, para confirmar que el filtro de fecha funciona.
        int ingId;
        await using (var db = fx.CreateDbContext())
        {
            var ing = new Ingrediente
            {
                Nombre = "Harina",
                UnidadMedida = "kg",
                CantidadActual = 0,
                Activo = true,
                FechaActualizacion = DateTime.Now
            };
            db.Ingredientes.Add(ing);
            await db.SaveChangesAsync();
            ingId = ing.Id;

            db.Entradas.Add(new Entrada
            {
                Fecha = new DateTime(2026, 1, 10, 9, 0, 0),
                IngredienteId = ingId,
                Cantidad = 12,
                CostoTotal = 100,
                Notas = "dentro del rango"
            });
            db.Entradas.Add(new Entrada
            {
                Fecha = new DateTime(2026, 2, 5, 9, 0, 0),
                IngredienteId = ingId,
                Cantidad = 7,
                CostoTotal = 70,
                Notas = "fuera del rango"
            });
            await db.SaveChangesAsync();
        }

        var service = new ExportService(fx);

        // Act
        var datos = await service.CargarDatosAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        // Sanity check sobre los datos cargados antes de tocar disco.
        Assert.Single(datos.Entradas);
        Assert.Equal(12, datos.Entradas[0].Cantidad);

        var ruta = ExportService.ExportarCsv(datos);

        try
        {
            // Assert: el CSV existe y contiene la fila esperada (no la otra).
            Assert.True(File.Exists(ruta), $"CSV no generado: {ruta}");
            var contenido = await File.ReadAllTextAsync(ruta);
            Assert.Contains("Harina", contenido);
            Assert.Contains("dentro del rango", contenido);
            Assert.DoesNotContain("fuera del rango", contenido);
        }
        finally
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
    }

    [Fact]
    public async Task CargarDatosAsync_RangoVacio_DevuelveColeccionesVacias()
    {
        using var fx = new InMemoryDbFixture();
        var service = new ExportService(fx);

        var datos = await service.CargarDatosAsync(
            new DateTime(2030, 1, 1), new DateTime(2030, 1, 31));

        Assert.Empty(datos.Entradas);
        Assert.Empty(datos.Gastos);
        Assert.Empty(datos.Mermas);
        Assert.Empty(datos.Cortesias);
        Assert.Empty(datos.Mercancias);
        Assert.Empty(datos.Discos);
        Assert.Empty(datos.Cajas);
        Assert.Empty(datos.Conteos);
        Assert.Empty(datos.Bodega);
    }
}
