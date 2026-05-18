using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

public sealed class RegistrarMovimientoDiscosCommand : UndoableCommandBase
{
    public int CantidadInicial { get; set; }
    public int DiscosPreparados { get; set; }
    public int DiscosUtilizados { get; set; }
    public int DiscosMerma { get; set; }
    public int DiscosCortesia { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public DateTime Fecha { get; set; }

    public int RegistroIdAsignado { get; set; }

    public override string Descripcion =>
        $"Movimiento de discos ({DiscosPreparados} prep / {DiscosUtilizados} usados)";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var registro = new InventarioDisco
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            CantidadInicial = CantidadInicial,
            DiscosPreparados = DiscosPreparados,
            DiscosUtilizados = DiscosUtilizados,
            DiscosMerma = DiscosMerma,
            DiscosCortesia = DiscosCortesia,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion
        };
        db.InventarioDiscos.Add(registro);
        await db.SaveChangesAsync();
        RegistroIdAsignado = registro.Id;
        Fecha = registro.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var registro = await db.InventarioDiscos.FindAsync(RegistroIdAsignado)
            ?? throw new InvalidOperationException(
                $"El registro de discos #{RegistroIdAsignado} ya no existe; no se puede deshacer.");
        db.InventarioDiscos.Remove(registro);
        await db.SaveChangesAsync();
    }
}
