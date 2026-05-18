using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

/// <summary>
/// Registrar un Conteo de Inventario (Apertura o Cierre). Si ya existía uno
/// del mismo tipo para hoy, la lógica de la VM lo borra antes; este comando
/// guarda el snapshot anterior (líneas + notas + fecha) para poder
/// restaurarlo al deshacer. Si no existía, el undo se limita a borrar el
/// nuevo conteo.
/// </summary>
public sealed class RegistrarConteoCommand : UndoableCommandBase
{
    public TipoConteo Tipo { get; set; }
    public string? Notas { get; set; }
    public DateTime Fecha { get; set; }
    public List<LineaDto> Lineas { get; set; } = new();

    /// <summary>Snapshot del conteo previo del mismo tipo, si lo había. Null si no existía.</summary>
    public ConteoSnapshot? SnapshotAnterior { get; set; }

    public int ConteoIdAsignado { get; set; }

    public override string Descripcion => $"Inventario {Tipo.Mostrar()} guardado";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Capturar y eliminar el conteo del mismo día/tipo si existe.
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);
        var existente = await db.ConteosInventario
            .Include(c => c.Lineas)
            .FirstOrDefaultAsync(c => c.Tipo == Tipo && c.Fecha >= hoy && c.Fecha < manana);

        if (existente != null && SnapshotAnterior == null)
        {
            // Sólo capturamos en la PRIMERA ejecución; en redos posteriores ya
            // tenemos el snapshot guardado y no lo sobreescribimos.
            SnapshotAnterior = new ConteoSnapshot
            {
                Tipo = existente.Tipo,
                Fecha = existente.Fecha,
                Notas = existente.Notas,
                Lineas = existente.Lineas
                    .Select(l => new LineaDto { IngredienteId = l.IngredienteId, Cantidad = l.Cantidad })
                    .ToList()
            };
        }

        if (existente != null)
        {
            db.ConteoInventarioLineas.RemoveRange(existente.Lineas);
            db.ConteosInventario.Remove(existente);
            await db.SaveChangesAsync();
        }

        var nuevo = new ConteoInventario
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            Tipo = Tipo,
            Notas = Notas,
            Lineas = Lineas.Select(l => new ConteoInventarioLinea
            {
                IngredienteId = l.IngredienteId,
                Cantidad = l.Cantidad
            }).ToList()
        };
        db.ConteosInventario.Add(nuevo);
        await db.SaveChangesAsync();
        ConteoIdAsignado = nuevo.Id;
        Fecha = nuevo.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var nuevo = await db.ConteosInventario
            .Include(c => c.Lineas)
            .FirstOrDefaultAsync(c => c.Id == ConteoIdAsignado)
            ?? throw new InvalidOperationException(
                $"El conteo #{ConteoIdAsignado} ya no existe; no se puede deshacer.");

        db.ConteoInventarioLineas.RemoveRange(nuevo.Lineas);
        db.ConteosInventario.Remove(nuevo);

        if (SnapshotAnterior != null)
        {
            var restaurado = new ConteoInventario
            {
                Fecha = SnapshotAnterior.Fecha,
                Tipo = SnapshotAnterior.Tipo,
                Notas = SnapshotAnterior.Notas,
                Lineas = SnapshotAnterior.Lineas.Select(l => new ConteoInventarioLinea
                {
                    IngredienteId = l.IngredienteId,
                    Cantidad = l.Cantidad
                }).ToList()
            };
            db.ConteosInventario.Add(restaurado);
        }

        await db.SaveChangesAsync();
    }

    public sealed class LineaDto
    {
        public int IngredienteId { get; set; }
        public double Cantidad { get; set; }
    }

    public sealed class ConteoSnapshot
    {
        public TipoConteo Tipo { get; set; }
        public DateTime Fecha { get; set; }
        public string? Notas { get; set; }
        public List<LineaDto> Lineas { get; set; } = new();
    }
}
