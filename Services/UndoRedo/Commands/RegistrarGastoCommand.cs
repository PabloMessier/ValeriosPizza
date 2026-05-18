using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

public sealed class RegistrarGastoCommand : UndoableCommandBase
{
    public int IngredienteId { get; set; }
    public string IngredienteNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public double Cantidad { get; set; }
    public string DescripcionGasto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public DateTime Fecha { get; set; }

    public int GastoIdAsignado { get; set; }

    /// <summary>
    /// Cantidad realmente descontada de la existencia (puede ser menor a
    /// <see cref="Cantidad"/> si el stock al momento de ejecutar era inferior;
    /// la lógica original recortaba a cero). Se guarda para reponer
    /// exactamente lo descontado en el undo.
    /// </summary>
    public double CantidadDescontada { get; set; }

    public override string Descripcion =>
        $"Gasto de {IngredienteNombre} ({Cantidad} {UnidadMedida})";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var gasto = new Gasto
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            IngredienteId = IngredienteId,
            Cantidad = Cantidad,
            Descripcion = DescripcionGasto,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion
        };
        db.Gastos.Add(gasto);

        var ingrediente = await db.Ingredientes.FindAsync(IngredienteId);
        CantidadDescontada = 0;
        if (ingrediente != null)
        {
            CantidadDescontada = Math.Min(Cantidad, ingrediente.CantidadActual);
            ingrediente.CantidadActual -= CantidadDescontada;
            if (ingrediente.CantidadActual < 0) ingrediente.CantidadActual = 0;
        }

        await db.SaveChangesAsync();
        GastoIdAsignado = gasto.Id;
        Fecha = gasto.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var gasto = await db.Gastos.FindAsync(GastoIdAsignado)
            ?? throw new InvalidOperationException(
                $"El gasto #{GastoIdAsignado} ya no existe; no se puede deshacer.");

        var ingrediente = await db.Ingredientes.FindAsync(gasto.IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual += CantidadDescontada;
        }

        db.Gastos.Remove(gasto);
        await db.SaveChangesAsync();
    }
}
