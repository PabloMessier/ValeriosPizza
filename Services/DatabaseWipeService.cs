using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;

namespace ValeriosPizza.Services;

/// <summary>
/// Define el alcance de un borrado masivo de la base de datos.
/// Las opciones por fecha solo eliminan registros transaccionales (entradas,
/// gastos, mermas, cortesías, mercancías, inventarios y conteos) cuya
/// columna Fecha cae dentro del rango calculado.
/// "Todo" elimina además ingredientes y productos del catálogo.
/// </summary>
public enum AlcanceBorrado
{
    Hoy,
    EstaSemana,
    EsteMes,
    EsteAno,
    RangoPersonalizado,
    TodoElCatalogo
}

/// <summary>
/// Resumen del resultado de un borrado, útil para informarle al usuario
/// cuántos registros se eliminaron de cada tabla.
/// </summary>
public sealed class ResultadoBorrado
{
    public int Entradas { get; set; }
    public int Gastos { get; set; }
    public int Mermas { get; set; }
    public int Cortesias { get; set; }
    public int InventarioDiscos { get; set; }
    public int InventarioCajas { get; set; }
    public int Mercancias { get; set; }
    public int ConteosInventario { get; set; }
    public int ConteoLineas { get; set; }
    public int Ingredientes { get; set; }
    public int Productos { get; set; }

    public int Total =>
        Entradas + Gastos + Mermas + Cortesias + InventarioDiscos +
        InventarioCajas + Mercancias + ConteosInventario + ConteoLineas +
        Ingredientes + Productos;
}

public static class DatabaseWipeService
{
    /// <summary>
    /// Calcula el rango [inicio, fin) correspondiente al alcance solicitado
    /// usando la fecha/hora actual del sistema. Para
    /// <see cref="AlcanceBorrado.RangoPersonalizado"/> y
    /// <see cref="AlcanceBorrado.TodoElCatalogo"/> el método devuelve
    /// (DateTime.MinValue, DateTime.MaxValue).
    /// </summary>
    public static (DateTime Inicio, DateTime Fin) CalcularRango(AlcanceBorrado alcance)
    {
        var ahora = DateTime.Now;
        switch (alcance)
        {
            case AlcanceBorrado.Hoy:
                var hoy = ahora.Date;
                return (hoy, hoy.AddDays(1));

            case AlcanceBorrado.EstaSemana:
                // Semana inicia en lunes (común en Colombia / configuraciones es-CO).
                int diasDesdeLunes = ((int)ahora.DayOfWeek + 6) % 7;
                var lunes = ahora.Date.AddDays(-diasDesdeLunes);
                return (lunes, lunes.AddDays(7));

            case AlcanceBorrado.EsteMes:
                var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
                return (inicioMes, inicioMes.AddMonths(1));

            case AlcanceBorrado.EsteAno:
                var inicioAno = new DateTime(ahora.Year, 1, 1);
                return (inicioAno, inicioAno.AddYears(1));

            default:
                return (DateTime.MinValue, DateTime.MaxValue);
        }
    }

    /// <summary>
    /// Borra registros transaccionales con Fecha en [inicio, fin).
    /// Usa <c>ExecuteDelete</c> (EF Core 7+) para evitar materializar entidades en
    /// memoria y agrupa todo en una transacción para que el borrado sea atómico.
    /// </summary>
    public static ResultadoBorrado BorrarPorRango(DateTime inicio, DateTime fin)
    {
        var resultado = new ResultadoBorrado();
        using var db = new PizzeriaDbContext();
        using var tx = db.Database.BeginTransaction();

        // 1. Eliminar primero las dependencias (líneas de conteo) por FK,
        //    para evitar problemas en SQLite si las FK están activadas.
        var lineasQuery = db.ConteoInventarioLineas
            .Where(l => l.ConteoInventario != null
                        && l.ConteoInventario.Fecha >= inicio
                        && l.ConteoInventario.Fecha < fin);
        resultado.ConteoLineas = lineasQuery.Count();
        lineasQuery.ExecuteDelete();

        resultado.ConteosInventario =
            db.ConteosInventario.Where(c => c.Fecha >= inicio && c.Fecha < fin).ExecuteDelete();
        resultado.Entradas =
            db.Entradas.Where(e => e.Fecha >= inicio && e.Fecha < fin).ExecuteDelete();
        resultado.Gastos =
            db.Gastos.Where(g => g.Fecha >= inicio && g.Fecha < fin).ExecuteDelete();
        resultado.Mermas =
            db.Mermas.Where(m => m.Fecha >= inicio && m.Fecha < fin).ExecuteDelete();
        resultado.Cortesias =
            db.Cortesias.Where(c => c.Fecha >= inicio && c.Fecha < fin).ExecuteDelete();
        resultado.InventarioDiscos =
            db.InventarioDiscos.Where(d => d.Fecha >= inicio && d.Fecha < fin).ExecuteDelete();
        resultado.InventarioCajas =
            db.InventarioCajas.Where(c => c.Fecha >= inicio && c.Fecha < fin).ExecuteDelete();
        resultado.Mercancias =
            db.MercanciasRecibidas.Where(m => m.Fecha >= inicio && m.Fecha < fin).ExecuteDelete();

        tx.Commit();
        return resultado;
    }

    /// <summary>
    /// Borra absolutamente todos los registros (catálogo + transacciones).
    /// Usa <c>ExecuteDelete</c> dentro de una transacción para minimizar memoria
    /// y garantizar atomicidad.
    /// </summary>
    public static ResultadoBorrado BorrarTodo()
    {
        var resultado = new ResultadoBorrado();
        using var db = new PizzeriaDbContext();
        using var tx = db.Database.BeginTransaction();

        // Orden importa: hijos antes que padres para no violar FKs.
        resultado.ConteoLineas = db.ConteoInventarioLineas.ExecuteDelete();
        resultado.ConteosInventario = db.ConteosInventario.ExecuteDelete();
        resultado.InventarioCajas = db.InventarioCajas.ExecuteDelete();
        resultado.Mercancias = db.MercanciasRecibidas.ExecuteDelete();
        resultado.InventarioDiscos = db.InventarioDiscos.ExecuteDelete();
        resultado.Cortesias = db.Cortesias.ExecuteDelete();
        resultado.Mermas = db.Mermas.ExecuteDelete();
        resultado.Gastos = db.Gastos.ExecuteDelete();
        resultado.Entradas = db.Entradas.ExecuteDelete();
        resultado.Productos = db.Productos.ExecuteDelete();
        resultado.Ingredientes = db.Ingredientes.ExecuteDelete();

        tx.Commit();
        return resultado;
    }

    /// <summary>
    /// Devuelve un resumen legible para mostrar al usuario tras un borrado.
    /// </summary>
    public static string Describir(ResultadoBorrado r)
    {
        return
            $"Entradas: {r.Entradas}\n" +
            $"Gastos: {r.Gastos}\n" +
            $"Mermas: {r.Mermas}\n" +
            $"Cortesías: {r.Cortesias}\n" +
            $"Inventario de discos: {r.InventarioDiscos}\n" +
            $"Inventario de cajas: {r.InventarioCajas}\n" +
            $"Mercancía recibida: {r.Mercancias}\n" +
            $"Conteos de inventario: {r.ConteosInventario} ({r.ConteoLineas} líneas)\n" +
            $"Ingredientes: {r.Ingredientes}\n" +
            $"Productos: {r.Productos}\n" +
            $"\nTotal eliminado: {r.Total} registro(s)";
    }
}
