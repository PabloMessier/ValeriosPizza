using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

/// <summary>
/// Cambiar el estado operativo de un Producto (Activo / Agotado /
/// Descontinuado). Al deshacer restauramos el estado anterior.
/// </summary>
public sealed class CambiarEstadoProductoCommand : UndoableCommandBase
{
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public EstadoProducto EstadoAnterior { get; set; }
    public EstadoProducto EstadoNuevo { get; set; }

    public override string Descripcion =>
        $"Cambio de estado de \"{ProductoNombre}\" → {EstadoNuevo.Mostrar()}";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var prod = await db.Productos.FindAsync(ProductoId)
            ?? throw new InvalidOperationException($"El producto #{ProductoId} ya no existe.");

        // Si es la primera ejecución y aún no se capturó el estado anterior,
        // tomamos el actual como referencia para poder revertir.
        if (EstadoAnterior == default && prod.Estado != EstadoNuevo)
        {
            EstadoAnterior = prod.Estado;
        }

        prod.Estado = EstadoNuevo;
        await db.SaveChangesAsync();
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var prod = await db.Productos.FindAsync(ProductoId)
            ?? throw new InvalidOperationException($"El producto #{ProductoId} ya no existe.");

        prod.Estado = EstadoAnterior;
        await db.SaveChangesAsync();
    }
}
