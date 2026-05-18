using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

public sealed class RegistrarCortesiaCommand : UndoableCommandBase
{
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public DateTime Fecha { get; set; }

    public int CortesiaIdAsignado { get; set; }

    public override string Descripcion =>
        $"Cortesía de {ProductoNombre} ({Cantidad})";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var cortesia = new Cortesia
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            ProductoId = ProductoId,
            Cantidad = Cantidad,
            Motivo = Motivo,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion
        };
        db.Cortesias.Add(cortesia);
        await db.SaveChangesAsync();
        CortesiaIdAsignado = cortesia.Id;
        Fecha = cortesia.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var cortesia = await db.Cortesias.FindAsync(CortesiaIdAsignado)
            ?? throw new InvalidOperationException(
                $"La cortesía #{CortesiaIdAsignado} ya no existe; no se puede deshacer.");

        db.Cortesias.Remove(cortesia);
        await db.SaveChangesAsync();
    }
}
