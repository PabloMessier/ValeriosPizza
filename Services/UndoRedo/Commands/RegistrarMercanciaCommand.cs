using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

/// <summary>
/// Registrar una Mercancía Recibida nueva. La actualización (modo edición de
/// la pantalla) NO está cubierta porque implica intercambios de archivos PDF
/// y de ingredientes que la lógica original ya gestiona transaccionalmente
/// pero serían frágiles de revertir.
/// </summary>
public sealed class RegistrarMercanciaCommand : UndoableCommandBase
{
    public int IngredienteId { get; set; }
    public string IngredienteNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public double Cantidad { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string NumeroFactura { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public string? RutaFactura { get; set; }
    public DateTime Fecha { get; set; }

    public int MercanciaIdAsignado { get; set; }

    public override string Descripcion =>
        $"Mercancía de {IngredienteNombre} ({Cantidad} {UnidadMedida})";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var mercancia = new MercanciaRecibida
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            IngredienteId = IngredienteId,
            Cantidad = Cantidad,
            Proveedor = Proveedor,
            NumeroFactura = NumeroFactura,
            Notas = Notas,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion,
            RutaFactura = RutaFactura
        };
        db.MercanciasRecibidas.Add(mercancia);

        var ingrediente = await db.Ingredientes.FindAsync(IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual += Cantidad;
            ingrediente.FechaActualizacion = DateTime.Now;
        }

        await db.SaveChangesAsync();
        MercanciaIdAsignado = mercancia.Id;
        Fecha = mercancia.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var mercancia = await db.MercanciasRecibidas.FindAsync(MercanciaIdAsignado)
            ?? throw new InvalidOperationException(
                $"La mercancía #{MercanciaIdAsignado} ya no existe; no se puede deshacer.");

        var ingrediente = await db.Ingredientes.FindAsync(mercancia.IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual -= mercancia.Cantidad;
            if (ingrediente.CantidadActual < 0) ingrediente.CantidadActual = 0;
            ingrediente.FechaActualizacion = DateTime.Now;
        }

        db.MercanciasRecibidas.Remove(mercancia);
        await db.SaveChangesAsync();
    }
}
