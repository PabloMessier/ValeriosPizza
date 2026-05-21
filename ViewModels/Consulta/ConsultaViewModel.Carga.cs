using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Carga de registros desde la BD con filtros por tipo y por rango de
/// fechas. Cada tipo de movimiento (Entradas, Gastos, Mermas, Cortesías,
/// Mercancías, Cajas, Discos, Conteos) se consulta por separado solo si
/// su <c>Mostrar*</c> está activo; los resultados se proyectan a
/// <see cref="RegistroConsulta"/> y se ordenan por fecha descendente al
/// final.
/// </summary>
public partial class ConsultaViewModel
{
    /// <summary>
    /// Helper: produce " | Total: $X COP" si la fila usa el desglose monetario.
    /// </summary>
    private static string DesgloseCop(decimal precio, decimal impuesto, decimal retencion, double cantidad)
    {
        if (precio == 0m && impuesto == 0m && retencion == 0m) return string.Empty;
        var total = (decimal)cantidad * precio + impuesto - retencion;
        return $" | Total: ${total:N0} COP";
    }

    private async Task CargarRegistrosAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        Registros.Clear();
        var inicio = FechaInicio.Date;
        var finExclusivo = FechaFinExclusivo;

        int entradas = 0, gastos = 0, mermas = 0, cortesias = 0, mercancias = 0,
            cajas = 0, discos = 0, apertura = 0, cierre = 0;

        if (MostrarEntradas)
        {
            var entradasDb = await db.Entradas
                .Include(e => e.Ingrediente)
                .Where(e => e.Fecha >= inicio && e.Fecha < finExclusivo)
                .OrderByDescending(e => e.Fecha)
                .ToListAsync();

            foreach (var e in entradasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = e.Fecha,
                    Tipo = "ENTRADA",
                    Ingrediente = e.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"+{e.Cantidad:N2} {e.Ingrediente?.UnidadMedida}",
                    Detalles = $"{e.Proveedor} | Costo: ${e.CostoTotal:N2}" + DesgloseCop(e.PrecioUnitario, e.Impuesto, e.Retencion, e.Cantidad),
                    ColorTipo = "#4CAF50"
                });
            }
            entradas = entradasDb.Count;
        }

        if (MostrarGastos)
        {
            var gastosDb = await db.Gastos
                .Include(g => g.Ingrediente)
                .Where(g => g.Fecha >= inicio && g.Fecha < finExclusivo)
                .OrderByDescending(g => g.Fecha)
                .ToListAsync();

            foreach (var g in gastosDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = g.Fecha,
                    Tipo = "GASTO",
                    Ingrediente = g.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"-{g.Cantidad:N2} {g.Ingrediente?.UnidadMedida}",
                    Detalles = (g.Descripcion ?? "") + DesgloseCop(g.PrecioUnitario, g.Impuesto, g.Retencion, g.Cantidad),
                    ColorTipo = "#2196F3"
                });
            }
            gastos = gastosDb.Count;
        }

        if (MostrarMermas)
        {
            var mermasDb = await db.Mermas
                .Include(m => m.Ingrediente)
                .Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            foreach (var m in mermasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = m.Fecha,
                    Tipo = "MERMA",
                    Ingrediente = m.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"-{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
                    Detalles = (m.Motivo ?? "") + DesgloseCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad),
                    ColorTipo = "#FF9800"
                });
            }
            mermas = mermasDb.Count;
        }

        if (MostrarCortesias)
        {
            var cortesiasDb = await db.Cortesias
                .Include(c => c.Producto)
                .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();

            foreach (var c in cortesiasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = c.Fecha,
                    Tipo = "CORTESÍA",
                    Ingrediente = c.Producto?.Nombre ?? "N/A",
                    Cantidad = $"{c.Cantidad} unidad(es)",
                    Detalles = (c.Motivo ?? "") + DesgloseCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.Cantidad),
                    ColorTipo = "#9C27B0"
                });
            }
            cortesias = cortesiasDb.Count;
        }

        if (MostrarMercancias)
        {
            var mercanciasDb = await db.MercanciasRecibidas
                .Include(m => m.Ingrediente)
                .Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            foreach (var m in mercanciasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = m.Fecha,
                    Tipo = "MERCANCÍA",
                    Ingrediente = m.Ingrediente?.Nombre ?? "N/A",
                    Cantidad = $"+{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
                    Detalles = $"{m.Proveedor} | Fact: {m.NumeroFactura}" + DesgloseCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad),
                    ColorTipo = "#00BCD4"
                });
            }
            mercancias = mercanciasDb.Count;
        }

        if (MostrarCajas)
        {
            var cajasDb = await db.InventarioCajas
                .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();

            foreach (var c in cajasDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = c.Fecha,
                    Tipo = "CAJA",
                    Ingrediente = "Cajas para Pizzas (30 cm)",
                    Cantidad = $"{c.CantidadDisponible} disp.",
                    Detalles = $"Inicial:{c.CantidadInicial} +{c.CajasRecibidas} -{c.CajasUtilizadas} -{c.CajasMerma}m" + DesgloseCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.CantidadInicial + c.CajasRecibidas),
                    ColorTipo = "#795548"
                });
            }
            cajas = cajasDb.Count;
        }

        if (MostrarDiscos)
        {
            var discosDb = await db.InventarioDiscos
                .Where(d => d.Fecha >= inicio && d.Fecha < finExclusivo)
                .OrderByDescending(d => d.Fecha)
                .ToListAsync();

            foreach (var d in discosDb)
            {
                Registros.Add(new RegistroConsulta
                {
                    Fecha = d.Fecha,
                    Tipo = "DISCO",
                    Ingrediente = "Discos de Pizza",
                    Cantidad = $"{d.CantidadDisponible} disp.",
                    Detalles = $"Inicial:{d.CantidadInicial} +{d.DiscosPreparados}p -{d.DiscosUtilizados}u -{d.DiscosMerma}m -{d.DiscosCortesia}c" + DesgloseCop(d.PrecioUnitario, d.Impuesto, d.Retencion, d.CantidadInicial + d.DiscosPreparados),
                    ColorTipo = "#E91E63"
                });
            }
            discos = discosDb.Count;
        }

        // Conteos (Apertura y Cierre) comparten consulta porque viven en
        // la misma tabla; se separan al formatear las filas individuales.
        if (MostrarApertura || MostrarCierre)
        {
            var conteosDb = await db.ConteosInventario
                .Include(c => c.Lineas).ThenInclude(l => l.Ingrediente)
                .Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();

            foreach (var c in conteosDb)
            {
                if (c.Tipo == TipoConteo.Apertura && !MostrarApertura) continue;
                if (c.Tipo == TipoConteo.Cierre && !MostrarCierre) continue;

                foreach (var l in c.Lineas)
                {
                    Registros.Add(new RegistroConsulta
                    {
                        Fecha = c.Fecha,
                        Tipo = c.Tipo == TipoConteo.Apertura ? "APERTURA" : "CIERRE",
                        Ingrediente = l.Ingrediente?.Nombre ?? "N/A",
                        Cantidad = $"{l.Cantidad:N2} {l.Ingrediente?.UnidadMedida}",
                        Detalles = c.Notas ?? "",
                        ColorTipo = c.Tipo == TipoConteo.Apertura ? "#1976D2" : "#7B1FA2"
                    });
                }

                if (c.Tipo == TipoConteo.Apertura) apertura++;
                else cierre++;
            }
        }

        // Ordenar todos los registros por fecha descendente al final, en
        // lugar de ordenar cada bucket por separado (más simple, mismo costo).
        var ordenados = Registros.OrderByDescending(r => r.Fecha).ToList();
        Registros.Clear();
        foreach (var reg in ordenados) Registros.Add(reg);

        TotalEntradas = entradas;
        TotalGastos = gastos;
        TotalMermas = mermas;
        TotalCortesias = cortesias;
        TotalMercancias = mercancias;
        TotalCajas = cajas;
        TotalDiscos = discos;
        TotalApertura = apertura;
        TotalCierre = cierre;
        TotalRegistros = Registros.Count;
    }
}
