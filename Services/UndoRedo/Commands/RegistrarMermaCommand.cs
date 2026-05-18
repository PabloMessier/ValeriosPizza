using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

public sealed class RegistrarMermaCommand : UndoableCommandBase
{
    public int IngredienteId { get; set; }
    public string IngredienteNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public double Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public DateTime Fecha { get; set; }

    public int MermaIdAsignado { get; set; }
    public double CantidadDescontada { get; set; }

    public override string Descripcion =>
        $"Merma de {IngredienteNombre} ({Cantidad} {UnidadMedida})";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var merma = new Merma
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            IngredienteId = IngredienteId,
            Cantidad = Cantidad,
            Motivo = Motivo,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion
        };
        db.Mermas.Add(merma);

        var ingrediente = await db.Ingredientes.FindAsync(IngredienteId);
        CantidadDescontada = 0;
        if (ingrediente != null)
        {
            CantidadDescontada = Math.Min(Cantidad, ingrediente.CantidadActual);
            ingrediente.CantidadActual -= CantidadDescontada;
            if (ingrediente.CantidadActual < 0) ingrediente.CantidadActual = 0;
        }

        await db.SaveChangesAsync();
        MermaIdAsignado = merma.Id;
        Fecha = merma.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var merma = await db.Mermas.FindAsync(MermaIdAsignado)
            ?? throw new InvalidOperationException(
                $"La merma #{MermaIdAsignado} ya no existe; no se puede deshacer.");

        var ingrediente = await db.Ingredientes.FindAsync(merma.IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual += CantidadDescontada;
        }

        db.Mermas.Remove(merma);
        await db.SaveChangesAsync();
    }
}
