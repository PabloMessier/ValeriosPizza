using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

/// <summary>
/// Registrar una Entrada de mercancía. Al ejecutar inserta la fila y suma a
/// <see cref="Ingrediente.CantidadActual"/>. Al deshacer borra la Entrada y
/// resta lo sumado.
/// </summary>
public sealed class RegistrarEntradaCommand : UndoableCommandBase
{
    public int IngredienteId { get; set; }
    public string IngredienteNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public double Cantidad { get; set; }
    public double CostoTotal { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public DateTime Fecha { get; set; }

    /// <summary>PK asignada al ejecutar; necesaria para deshacer.</summary>
    public int EntradaIdAsignado { get; set; }

    public override string Descripcion =>
        $"Entrada de {IngredienteNombre} ({Cantidad} {UnidadMedida})";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var entrada = new Entrada
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            IngredienteId = IngredienteId,
            Cantidad = Cantidad,
            CostoTotal = CostoTotal,
            Proveedor = Proveedor,
            Notas = Notas,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion
        };
        db.Entradas.Add(entrada);

        var ingrediente = await db.Ingredientes.FindAsync(IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual += Cantidad;
        }

        await db.SaveChangesAsync();
        EntradaIdAsignado = entrada.Id;
        Fecha = entrada.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var entrada = await db.Entradas.FindAsync(EntradaIdAsignado)
            ?? throw new InvalidOperationException(
                $"La entrada #{EntradaIdAsignado} ya no existe; no se puede deshacer.");

        var ingrediente = await db.Ingredientes.FindAsync(entrada.IngredienteId);
        if (ingrediente != null)
        {
            ingrediente.CantidadActual -= entrada.Cantidad;
            if (ingrediente.CantidadActual < 0) ingrediente.CantidadActual = 0;
        }

        db.Entradas.Remove(entrada);
        await db.SaveChangesAsync();
    }
}
