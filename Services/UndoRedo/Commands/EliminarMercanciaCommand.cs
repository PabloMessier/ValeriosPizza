using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

/// <summary>
/// Eliminar una Mercancía Recibida. Al ejecutarse borra la fila y resta el
/// stock; al deshacer reinserta la fila (con un nuevo Id) y suma de vuelta
/// el stock. Como la PK original se pierde al borrar, en el undo guardamos
/// el nuevo Id para poder un futuro redo.
/// </summary>
public sealed class EliminarMercanciaCommand : UndoableCommandBase
{
    public int MercanciaId { get; set; }
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
    public DateTime FechaOriginal { get; set; }

    /// <summary>Cuánto se descontó realmente (limitado por el stock disponible).</summary>
    public double CantidadRestada { get; set; }

    public override string Descripcion =>
        $"Eliminación de mercancía de {IngredienteNombre} ({Cantidad} {UnidadMedida})";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existente = await db.MercanciasRecibidas.FindAsync(MercanciaId)
            ?? throw new InvalidOperationException(
                $"La mercancía #{MercanciaId} ya no existe.");

        // Capturar metadata para poder reinsertarla idéntica al deshacer.
        IngredienteId = existente.IngredienteId;
        Cantidad = existente.Cantidad;
        Proveedor = existente.Proveedor;
        NumeroFactura = existente.NumeroFactura;
        Notas = existente.Notas;
        PrecioUnitario = existente.PrecioUnitario;
        Impuesto = existente.Impuesto;
        Retencion = existente.Retencion;
        RutaFactura = existente.RutaFactura;
        FechaOriginal = existente.Fecha;

        var ingrediente = await db.Ingredientes.FindAsync(existente.IngredienteId);
        CantidadRestada = 0;
        if (ingrediente != null)
        {
            CantidadRestada = Math.Min(existente.Cantidad, ingrediente.CantidadActual);
            ingrediente.CantidadActual -= CantidadRestada;
            if (ingrediente.CantidadActual < 0) ingrediente.CantidadActual = 0;
            ingrediente.FechaActualizacion = DateTime.Now;
        }

        db.MercanciasRecibidas.Remove(existente);
        await db.SaveChangesAsync();
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var nueva = new MercanciaRecibida
        {
            Fecha = FechaOriginal,
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
        db.MercanciasRecibidas.Add(nueva);

        var ingrediente = await db.Ingredientes.FindAsync(IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual += CantidadRestada;
            ingrediente.FechaActualizacion = DateTime.Now;
        }

        await db.SaveChangesAsync();
        // Si se vuelve a hacer redo, el comando borrará por la nueva PK.
        MercanciaId = nueva.Id;
    }
}
