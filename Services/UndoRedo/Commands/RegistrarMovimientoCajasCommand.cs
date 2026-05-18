using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services.UndoRedo.Commands;

public sealed class RegistrarMovimientoCajasCommand : UndoableCommandBase
{
    public int CantidadInicial { get; set; }
    public int CajasRecibidas { get; set; }
    public int CajasUtilizadas { get; set; }
    public int CajasMerma { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Retencion { get; set; }
    public DateTime Fecha { get; set; }

    public int RegistroIdAsignado { get; set; }

    public override string Descripcion =>
        $"Movimiento de cajas ({CajasRecibidas} rec / {CajasUtilizadas} usadas)";

    public override async Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var registro = new InventarioCaja
        {
            Fecha = Fecha == default ? DateTime.Now : Fecha,
            CantidadInicial = CantidadInicial,
            CajasRecibidas = CajasRecibidas,
            CajasUtilizadas = CajasUtilizadas,
            CajasMerma = CajasMerma,
            PrecioUnitario = PrecioUnitario,
            Impuesto = Impuesto,
            Retencion = Retencion
        };
        db.InventarioCajas.Add(registro);
        await db.SaveChangesAsync();
        RegistroIdAsignado = registro.Id;
        Fecha = registro.Fecha;
    }

    public override async Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var registro = await db.InventarioCajas.FindAsync(RegistroIdAsignado)
            ?? throw new InvalidOperationException(
                $"El registro de cajas #{RegistroIdAsignado} ya no existe; no se puede deshacer.");
        db.InventarioCajas.Remove(registro);
        await db.SaveChangesAsync();
    }
}
