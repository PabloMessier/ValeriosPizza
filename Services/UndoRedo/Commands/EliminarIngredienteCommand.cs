using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

/// <summary>
/// Eliminar un Ingrediente sin historial. La VM solo lo permite cuando no
/// hay registros dependientes, así que el undo se reduce a reinsertarlo. Al
/// reinsertar se le asigna un nuevo Id; si el usuario reabre y rehace, se
/// borra usando ese nuevo Id.
/// </summary>
public sealed class EliminarIngredienteCommand : UndoableCommandBase
{
    public int IngredienteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public double CantidadActual { get; set; }
    public double CantidadMinima { get; set; }
    public bool Activo { get; set; }

    public override string Descripcion => $"Eliminación de ingrediente \"{Nombre}\"";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existente = await db.Ingredientes.FindAsync(IngredienteId)
            ?? throw new InvalidOperationException($"El ingrediente #{IngredienteId} ya no existe.");

        Nombre = existente.Nombre;
        UnidadMedida = existente.UnidadMedida;
        CantidadActual = existente.CantidadActual;
        CantidadMinima = existente.CantidadMinima;
        Activo = existente.Activo;

        db.Ingredientes.Remove(existente);
        await db.SaveChangesAsync();
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var nuevo = new Ingrediente
        {
            Nombre = Nombre,
            UnidadMedida = UnidadMedida,
            CantidadActual = CantidadActual,
            CantidadMinima = CantidadMinima,
            FechaActualizacion = DateTime.Now,
            Activo = Activo
        };
        db.Ingredientes.Add(nuevo);
        await db.SaveChangesAsync();
        IngredienteId = nuevo.Id;
    }
}
