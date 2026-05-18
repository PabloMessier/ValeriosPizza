using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services;

/// <summary>
/// Genera reportes en CSV, Excel (xlsx) y PDF a partir de los datos del rango
/// indicado. Los archivos se guardan en la carpeta de Reportes del usuario.
/// </summary>
public static class ExportService
{
    public static string CarpetaReportes
    {
        get
        {
            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ValeriosPizzeria",
                "Reportes");

            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }
            return carpeta;
        }
    }

    /// <summary>
    /// Datos consolidados que se cargan una sola vez por reporte.
    /// </summary>
    public class DatosReporte
    {
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }

        public List<Entrada> Entradas { get; set; } = new();
        public List<Gasto> Gastos { get; set; } = new();
        public List<Merma> Mermas { get; set; } = new();
        public List<Cortesia> Cortesias { get; set; } = new();
        public List<MercanciaRecibida> Mercancias { get; set; } = new();
        public List<InventarioDisco> Discos { get; set; } = new();
        public List<InventarioCaja> Cajas { get; set; } = new();
        public List<ConteoInventario> Conteos { get; set; } = new();
        /// <summary>
        /// Items registrados manualmente en la pantalla Bodega. Se filtran por
        /// <see cref="BodegaItem.FechaAgregado"/> con el mismo rango medio
        /// abierto que el resto de las colecciones.
        /// </summary>
        public List<BodegaItem> Bodega { get; set; } = new();
    }

    public static DatosReporte CargarDatos(DateTime inicio, DateTime fin)
    {
        // Normalizamos el rango a [inicio_dia, fin_exclusivo) para evitar
        // problemas de precisión con DateTime al final del día. Si el llamador
        // pasa una hora específica en `fin` (ej. desde un diálogo), respetamos
        // ese instante interpretándolo como límite exclusivo.
        var inicioDia = inicio.Date;
        var finExclusivo = fin.TimeOfDay == TimeSpan.Zero
            ? fin.Date.AddDays(1)
            : fin;
        using var db = new PizzeriaDbContext();

        return new DatosReporte
        {
            Inicio = inicio,
            Fin = fin,
            Entradas = db.Entradas.Include(e => e.Ingrediente)
                .Where(e => e.Fecha >= inicioDia && e.Fecha < finExclusivo)
                .OrderBy(e => e.Fecha).ToList(),
            Gastos = db.Gastos.Include(g => g.Ingrediente)
                .Where(g => g.Fecha >= inicioDia && g.Fecha < finExclusivo)
                .OrderBy(g => g.Fecha).ToList(),
            Mermas = db.Mermas.Include(m => m.Ingrediente)
                .Where(m => m.Fecha >= inicioDia && m.Fecha < finExclusivo)
                .OrderBy(m => m.Fecha).ToList(),
            Cortesias = db.Cortesias.Include(c => c.Producto)
                .Where(c => c.Fecha >= inicioDia && c.Fecha < finExclusivo)
                .OrderBy(c => c.Fecha).ToList(),
            Mercancias = db.MercanciasRecibidas.Include(m => m.Ingrediente)
                .Where(m => m.Fecha >= inicioDia && m.Fecha < finExclusivo)
                .OrderBy(m => m.Fecha).ToList(),
            Discos = db.InventarioDiscos
                .Where(d => d.Fecha >= inicioDia && d.Fecha < finExclusivo)
                .OrderBy(d => d.Fecha).ToList(),
            Cajas = db.InventarioCajas
                .Where(c => c.Fecha >= inicioDia && c.Fecha < finExclusivo)
                .OrderBy(c => c.Fecha).ToList(),
            Conteos = db.ConteosInventario
                .Include(c => c.Lineas).ThenInclude(l => l.Ingrediente)
                .Where(c => c.Fecha >= inicioDia && c.Fecha < finExclusivo)
                .OrderBy(c => c.Fecha).ToList(),
            Bodega = db.BodegaItems
                .Where(b => b.FechaAgregado >= inicioDia && b.FechaAgregado < finExclusivo)
                .OrderBy(b => b.FechaAgregado).ToList()
        };
    }

    // ==================== CSV ====================

    public static string ExportarCsv(DatosReporte datos)
    {
        var nombre = $"Reporte_{datos.Inicio:yyyyMMdd}_{datos.Fin:yyyyMMdd}.csv";
        var ruta = Path.Combine(CarpetaReportes, nombre);
        var sb = new StringBuilder();

        sb.AppendLine("REPORTE DE INVENTARIO - VALERIO'S PIZZA");
        sb.AppendLine($"Período: {datos.Inicio:dd/MM/yyyy} - {datos.Fin:dd/MM/yyyy}");
        sb.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
        sb.AppendLine();

        sb.AppendLine("ENTRADAS");
        sb.AppendLine("Fecha,Ingrediente,Cantidad,Unidad,Costo,Proveedor,Notas,PrecioUnitario,Impuesto,Retencion,TotalCalculado");
        foreach (var e in datos.Entradas)
        {
            sb.AppendLine($"{e.Fecha:dd/MM/yyyy HH:mm},{Escape(e.Ingrediente?.Nombre)},{e.Cantidad:N2},{Escape(e.Ingrediente?.UnidadMedida)},{e.CostoTotal:N2},{Escape(e.Proveedor)},{Escape(e.Notas)},{e.PrecioUnitario:N2},{e.Impuesto:N2},{e.Retencion:N2},{TotalCop(e.PrecioUnitario, e.Impuesto, e.Retencion, e.Cantidad):N2}");
        }
        sb.AppendLine();

        sb.AppendLine("GASTOS");
        sb.AppendLine("Fecha,Ingrediente,Cantidad,Unidad,Descripción,PrecioUnitario,Impuesto,Retencion,TotalCalculado");
        foreach (var g in datos.Gastos)
        {
            sb.AppendLine($"{g.Fecha:dd/MM/yyyy HH:mm},{Escape(g.Ingrediente?.Nombre)},{g.Cantidad:N2},{Escape(g.Ingrediente?.UnidadMedida)},{Escape(g.Descripcion)},{g.PrecioUnitario:N2},{g.Impuesto:N2},{g.Retencion:N2},{TotalCop(g.PrecioUnitario, g.Impuesto, g.Retencion, g.Cantidad):N2}");
        }
        sb.AppendLine();

        sb.AppendLine("MERMAS");
        sb.AppendLine("Fecha,Ingrediente,Cantidad,Unidad,Motivo,Notas,PrecioUnitario,Impuesto,Retencion,TotalCalculado");
        foreach (var m in datos.Mermas)
        {
            sb.AppendLine($"{m.Fecha:dd/MM/yyyy HH:mm},{Escape(m.Ingrediente?.Nombre)},{m.Cantidad:N2},{Escape(m.Ingrediente?.UnidadMedida)},{Escape(m.Motivo)},{Escape(m.Notas)},{m.PrecioUnitario:N2},{m.Impuesto:N2},{m.Retencion:N2},{TotalCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad):N2}");
        }
        sb.AppendLine();

        sb.AppendLine("CORTESÍAS");
        sb.AppendLine("Fecha,Producto,Cantidad,Motivo,Notas,PrecioUnitario,Impuesto,Retencion,TotalCalculado");
        foreach (var c in datos.Cortesias)
        {
            sb.AppendLine($"{c.Fecha:dd/MM/yyyy HH:mm},{Escape(c.Producto?.Nombre)},{c.Cantidad},{Escape(c.Motivo)},{Escape(c.Notas)},{c.PrecioUnitario:N2},{c.Impuesto:N2},{c.Retencion:N2},{TotalCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.Cantidad):N2}");
        }
        sb.AppendLine();

        sb.AppendLine("MERCANCÍA RECIBIDA");
        sb.AppendLine("Fecha,Ingrediente,Cantidad,Unidad,Proveedor,Factura,Notas,PrecioUnitario,Impuesto,Retencion,TotalCalculado,ArchivoFactura");
        foreach (var m in datos.Mercancias)
        {
            sb.AppendLine($"{m.Fecha:dd/MM/yyyy HH:mm},{Escape(m.Ingrediente?.Nombre)},{m.Cantidad:N2},{Escape(m.Ingrediente?.UnidadMedida)},{Escape(m.Proveedor)},{Escape(m.NumeroFactura)},{Escape(m.Notas)},{m.PrecioUnitario:N2},{m.Impuesto:N2},{m.Retencion:N2},{TotalCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad):N2},{Escape(m.RutaFactura)}");
        }
        sb.AppendLine();

        sb.AppendLine("DISCOS");
        sb.AppendLine("Fecha,Inicial,Preparados,Utilizados,Merma,Cortesía,Disponible,PrecioUnitario,Impuesto,Retencion,TotalCalculado");
        foreach (var d in datos.Discos)
        {
            sb.AppendLine($"{d.Fecha:dd/MM/yyyy HH:mm},{d.CantidadInicial},{d.DiscosPreparados},{d.DiscosUtilizados},{d.DiscosMerma},{d.DiscosCortesia},{d.CantidadDisponible},{d.PrecioUnitario:N2},{d.Impuesto:N2},{d.Retencion:N2},{TotalCop(d.PrecioUnitario, d.Impuesto, d.Retencion, d.CantidadInicial + d.DiscosPreparados):N2}");
        }
        sb.AppendLine();

        sb.AppendLine("CAJAS PARA PIZZAS (30 cm)");
        sb.AppendLine("Fecha,Inicial,Recibidas,Utilizadas,Merma,Disponible,PrecioUnitario,Impuesto,Retencion,TotalCalculado");
        foreach (var c in datos.Cajas)
        {
            sb.AppendLine($"{c.Fecha:dd/MM/yyyy HH:mm},{c.CantidadInicial},{c.CajasRecibidas},{c.CajasUtilizadas},{c.CajasMerma},{c.CantidadDisponible},{c.PrecioUnitario:N2},{c.Impuesto:N2},{c.Retencion:N2},{TotalCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.CantidadInicial + c.CajasRecibidas):N2}");
        }
        sb.AppendLine();

        sb.AppendLine("CONTEOS DE INVENTARIO (APERTURA / CIERRE)");
        sb.AppendLine("Fecha,Tipo,Ingrediente,Cantidad,Unidad,Notas");
        foreach (var conteo in datos.Conteos)
        {
            foreach (var linea in conteo.Lineas)
            {
                sb.AppendLine($"{conteo.Fecha:dd/MM/yyyy HH:mm},{conteo.Tipo.Mostrar()},{Escape(linea.Ingrediente?.Nombre)},{linea.Cantidad:N2},{Escape(linea.Ingrediente?.UnidadMedida)},{Escape(conteo.Notas)}");
            }
        }
        sb.AppendLine();

        // BODEGA: items registrados en el alacén general (pantalla Bodega).
        // No participan en el stock activo, pero forman parte del reporte
        // para que la dueña pueda auditar qué se reservó/separó.
        sb.AppendLine("BODEGA (ALMACÉN GENERAL)");
        sb.AppendLine("Fecha,Categoría,Nombre,Cantidad,Unidad,Origen,Notas");
        foreach (var b in datos.Bodega)
        {
            sb.AppendLine($"{b.FechaAgregado:dd/MM/yyyy HH:mm},{Escape(b.Categoria)},{Escape(b.Nombre)},{b.Cantidad:N2},{Escape(b.UnidadMedida)},{Escape(b.Origen)},{Escape(b.Notas)}");
        }

        File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
        return ruta;
    }

    private static string Escape(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;
        if (valor.Contains(',') || valor.Contains('"') || valor.Contains('\n'))
        {
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        }
        return valor;
    }

    // Calcula el total monetario (COP) del desglose calculadora.
    // Devuelve 0 si los tres campos están vacíos.
    private static decimal TotalCop(decimal precio, decimal impuesto, decimal retencion, double cantidad)
    {
        if (precio == 0m && impuesto == 0m && retencion == 0m) return 0m;
        return (decimal)cantidad * precio + impuesto - retencion;
    }

    // ==================== EXCEL ====================

    public static string ExportarExcel(DatosReporte datos)
    {
        var nombre = $"Reporte_{datos.Inicio:yyyyMMdd}_{datos.Fin:yyyyMMdd}.xlsx";
        var ruta = Path.Combine(CarpetaReportes, nombre);

        using var wb = new XLWorkbook();

        // Hoja Resumen
        var ws = wb.Worksheets.Add("Resumen");
        ws.Cell(1, 1).Value = "REPORTE DE INVENTARIO - VALERIO'S PIZZA";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 4).Merge();
        ws.Cell(2, 1).Value = $"Período: {datos.Inicio:dd/MM/yyyy} - {datos.Fin:dd/MM/yyyy}";
        ws.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";

        ws.Cell(5, 1).Value = "Tipo";
        ws.Cell(5, 2).Value = "Total registros";
        ws.Cell(5, 1).Style.Font.Bold = true;
        ws.Cell(5, 2).Style.Font.Bold = true;
        ws.Cell(6, 1).Value = "Entradas"; ws.Cell(6, 2).Value = datos.Entradas.Count;
        ws.Cell(7, 1).Value = "Gastos"; ws.Cell(7, 2).Value = datos.Gastos.Count;
        ws.Cell(8, 1).Value = "Mermas"; ws.Cell(8, 2).Value = datos.Mermas.Count;
        ws.Cell(9, 1).Value = "Cortesías"; ws.Cell(9, 2).Value = datos.Cortesias.Count;
        ws.Cell(10, 1).Value = "Mercancía recibida"; ws.Cell(10, 2).Value = datos.Mercancias.Count;
        ws.Cell(11, 1).Value = "Registros de discos"; ws.Cell(11, 2).Value = datos.Discos.Count;
        ws.Cell(12, 1).Value = "Registros de cajas"; ws.Cell(12, 2).Value = datos.Cajas.Count;
        ws.Cell(13, 1).Value = "Conteos (Apertura/Cierre)"; ws.Cell(13, 2).Value = datos.Conteos.Count;
        ws.Cell(14, 1).Value = "Bodega (almacén)"; ws.Cell(14, 2).Value = datos.Bodega.Count;
        ws.Columns().AdjustToContents();

        // Hojas de detalle
        EscribirHojaEntradas(wb, datos.Entradas);
        EscribirHojaGastos(wb, datos.Gastos);
        EscribirHojaMermas(wb, datos.Mermas);
        EscribirHojaCortesias(wb, datos.Cortesias);
        EscribirHojaMercancias(wb, datos.Mercancias);
        EscribirHojaDiscos(wb, datos.Discos);
        EscribirHojaCajas(wb, datos.Cajas);
        EscribirHojaConteos(wb, datos.Conteos);
        EscribirHojaBodega(wb, datos.Bodega);

        wb.SaveAs(ruta);
        return ruta;
    }

    private static void Encabezar(IXLWorksheet ws, params string[] columnas)
    {
        for (int i = 0; i < columnas.Length; i++)
        {
            ws.Cell(1, i + 1).Value = columnas[i];
        }
        ws.Range(1, 1, 1, columnas.Length).Style.Font.Bold = true;
        ws.Range(1, 1, 1, columnas.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private static void EscribirHojaEntradas(XLWorkbook wb, List<Entrada> lista)
    {
        var ws = wb.Worksheets.Add("Entradas");
        Encabezar(ws, "Fecha", "Ingrediente", "Cantidad", "Unidad", "Costo", "Proveedor", "Notas",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)");
        int fila = 2;
        foreach (var e in lista)
        {
            ws.Cell(fila, 1).Value = e.Fecha;
            ws.Cell(fila, 2).Value = e.Ingrediente?.Nombre ?? "";
            ws.Cell(fila, 3).Value = e.Cantidad;
            ws.Cell(fila, 4).Value = e.Ingrediente?.UnidadMedida ?? "";
            ws.Cell(fila, 5).Value = e.CostoTotal;
            ws.Cell(fila, 6).Value = e.Proveedor ?? "";
            ws.Cell(fila, 7).Value = e.Notas ?? "";
            ws.Cell(fila, 8).Value = e.PrecioUnitario;
            ws.Cell(fila, 9).Value = e.Impuesto;
            ws.Cell(fila, 10).Value = e.Retencion;
            ws.Cell(fila, 11).Value = TotalCop(e.PrecioUnitario, e.Impuesto, e.Retencion, e.Cantidad);
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaGastos(XLWorkbook wb, List<Gasto> lista)
    {
        var ws = wb.Worksheets.Add("Gastos");
        Encabezar(ws, "Fecha", "Ingrediente", "Cantidad", "Unidad", "Descripción",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)");
        int fila = 2;
        foreach (var g in lista)
        {
            ws.Cell(fila, 1).Value = g.Fecha;
            ws.Cell(fila, 2).Value = g.Ingrediente?.Nombre ?? "";
            ws.Cell(fila, 3).Value = g.Cantidad;
            ws.Cell(fila, 4).Value = g.Ingrediente?.UnidadMedida ?? "";
            ws.Cell(fila, 5).Value = g.Descripcion ?? "";
            ws.Cell(fila, 6).Value = g.PrecioUnitario;
            ws.Cell(fila, 7).Value = g.Impuesto;
            ws.Cell(fila, 8).Value = g.Retencion;
            ws.Cell(fila, 9).Value = TotalCop(g.PrecioUnitario, g.Impuesto, g.Retencion, g.Cantidad);
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaMermas(XLWorkbook wb, List<Merma> lista)
    {
        var ws = wb.Worksheets.Add("Mermas");
        Encabezar(ws, "Fecha", "Ingrediente", "Cantidad", "Unidad", "Motivo", "Notas",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)");
        int fila = 2;
        foreach (var m in lista)
        {
            ws.Cell(fila, 1).Value = m.Fecha;
            ws.Cell(fila, 2).Value = m.Ingrediente?.Nombre ?? "";
            ws.Cell(fila, 3).Value = m.Cantidad;
            ws.Cell(fila, 4).Value = m.Ingrediente?.UnidadMedida ?? "";
            ws.Cell(fila, 5).Value = m.Motivo ?? "";
            ws.Cell(fila, 6).Value = m.Notas ?? "";
            ws.Cell(fila, 7).Value = m.PrecioUnitario;
            ws.Cell(fila, 8).Value = m.Impuesto;
            ws.Cell(fila, 9).Value = m.Retencion;
            ws.Cell(fila, 10).Value = TotalCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad);
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaCortesias(XLWorkbook wb, List<Cortesia> lista)
    {
        var ws = wb.Worksheets.Add("Cortesías");
        Encabezar(ws, "Fecha", "Producto", "Cantidad", "Motivo", "Notas",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)");
        int fila = 2;
        foreach (var c in lista)
        {
            ws.Cell(fila, 1).Value = c.Fecha;
            ws.Cell(fila, 2).Value = c.Producto?.Nombre ?? "";
            ws.Cell(fila, 3).Value = c.Cantidad;
            ws.Cell(fila, 4).Value = c.Motivo ?? "";
            ws.Cell(fila, 5).Value = c.Notas ?? "";
            ws.Cell(fila, 6).Value = c.PrecioUnitario;
            ws.Cell(fila, 7).Value = c.Impuesto;
            ws.Cell(fila, 8).Value = c.Retencion;
            ws.Cell(fila, 9).Value = TotalCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.Cantidad);
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaMercancias(XLWorkbook wb, List<MercanciaRecibida> lista)
    {
        var ws = wb.Worksheets.Add("Mercancía");
        Encabezar(ws, "Fecha", "Ingrediente", "Cantidad", "Unidad", "Proveedor", "Factura", "Notas",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)", "Archivo factura");
        int fila = 2;
        foreach (var m in lista)
        {
            ws.Cell(fila, 1).Value = m.Fecha;
            ws.Cell(fila, 2).Value = m.Ingrediente?.Nombre ?? "";
            ws.Cell(fila, 3).Value = m.Cantidad;
            ws.Cell(fila, 4).Value = m.Ingrediente?.UnidadMedida ?? "";
            ws.Cell(fila, 5).Value = m.Proveedor ?? "";
            ws.Cell(fila, 6).Value = m.NumeroFactura ?? "";
            ws.Cell(fila, 7).Value = m.Notas ?? "";
            ws.Cell(fila, 8).Value = m.PrecioUnitario;
            ws.Cell(fila, 9).Value = m.Impuesto;
            ws.Cell(fila, 10).Value = m.Retencion;
            ws.Cell(fila, 11).Value = TotalCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad);
            if (!string.IsNullOrWhiteSpace(m.RutaFactura))
            {
                var c = ws.Cell(fila, 12);
                c.Value = System.IO.Path.GetFileName(m.RutaFactura);
                c.SetHyperlink(new XLHyperlink(m.RutaFactura));
            }
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaDiscos(XLWorkbook wb, List<InventarioDisco> lista)
    {
        var ws = wb.Worksheets.Add("Discos");
        Encabezar(ws, "Fecha", "Inicial", "Preparados", "Utilizados", "Merma", "Cortesía", "Disponible", "Notas",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)");
        int fila = 2;
        foreach (var d in lista)
        {
            ws.Cell(fila, 1).Value = d.Fecha;
            ws.Cell(fila, 2).Value = d.CantidadInicial;
            ws.Cell(fila, 3).Value = d.DiscosPreparados;
            ws.Cell(fila, 4).Value = d.DiscosUtilizados;
            ws.Cell(fila, 5).Value = d.DiscosMerma;
            ws.Cell(fila, 6).Value = d.DiscosCortesia;
            ws.Cell(fila, 7).Value = d.CantidadDisponible;
            ws.Cell(fila, 8).Value = d.Notas ?? "";
            ws.Cell(fila, 9).Value = d.PrecioUnitario;
            ws.Cell(fila, 10).Value = d.Impuesto;
            ws.Cell(fila, 11).Value = d.Retencion;
            ws.Cell(fila, 12).Value = TotalCop(d.PrecioUnitario, d.Impuesto, d.Retencion, d.CantidadInicial + d.DiscosPreparados);
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaCajas(XLWorkbook wb, List<InventarioCaja> lista)
    {
        var ws = wb.Worksheets.Add("Cajas");
        Encabezar(ws, "Fecha", "Inicial", "Recibidas", "Utilizadas", "Merma", "Disponible", "Notas",
            "Precio Unit. (COP)", "Impuesto (COP)", "Retención (COP)", "Total (COP)");
        int fila = 2;
        foreach (var c in lista)
        {
            ws.Cell(fila, 1).Value = c.Fecha;
            ws.Cell(fila, 2).Value = c.CantidadInicial;
            ws.Cell(fila, 3).Value = c.CajasRecibidas;
            ws.Cell(fila, 4).Value = c.CajasUtilizadas;
            ws.Cell(fila, 5).Value = c.CajasMerma;
            ws.Cell(fila, 6).Value = c.CantidadDisponible;
            ws.Cell(fila, 7).Value = c.Notas ?? "";
            ws.Cell(fila, 8).Value = c.PrecioUnitario;
            ws.Cell(fila, 9).Value = c.Impuesto;
            ws.Cell(fila, 10).Value = c.Retencion;
            ws.Cell(fila, 11).Value = TotalCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.CantidadInicial + c.CajasRecibidas);
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaConteos(XLWorkbook wb, List<ConteoInventario> lista)
    {
        var ws = wb.Worksheets.Add("Apertura-Cierre");
        Encabezar(ws, "Fecha", "Tipo", "Ingrediente", "Cantidad", "Unidad", "Notas");
        int fila = 2;
        foreach (var c in lista)
        {
            foreach (var l in c.Lineas)
            {
                ws.Cell(fila, 1).Value = c.Fecha;
                ws.Cell(fila, 2).Value = c.Tipo.Mostrar();
                ws.Cell(fila, 3).Value = l.Ingrediente?.Nombre ?? "";
                ws.Cell(fila, 4).Value = l.Cantidad;
                ws.Cell(fila, 5).Value = l.Ingrediente?.UnidadMedida ?? "";
                ws.Cell(fila, 6).Value = c.Notas ?? "";
                fila++;
            }
        }
        ws.Columns().AdjustToContents();
    }

    private static void EscribirHojaBodega(XLWorkbook wb, List<BodegaItem> lista)
    {
        var ws = wb.Worksheets.Add("Bodega");
        Encabezar(ws, "Fecha", "Categoría", "Nombre", "Cantidad", "Unidad", "Origen", "Notas");
        int fila = 2;
        foreach (var b in lista)
        {
            ws.Cell(fila, 1).Value = b.FechaAgregado;
            ws.Cell(fila, 2).Value = b.Categoria;
            ws.Cell(fila, 3).Value = b.Nombre;
            ws.Cell(fila, 4).Value = b.Cantidad;
            ws.Cell(fila, 5).Value = b.UnidadMedida;
            ws.Cell(fila, 6).Value = b.Origen ?? "";
            ws.Cell(fila, 7).Value = b.Notas ?? "";
            fila++;
        }
        ws.Columns().AdjustToContents();
    }

    // ==================== PDF ====================

    // Encabezados de cada sección del PDF promovidos a `static readonly` para
    // satisfacer la regla CA1861 (evita asignar un nuevo string[] cada vez que
    // se invoca SeccionTabla, sobre todo si el reporte se genera muchas veces
    // en una misma sesión).
    private static readonly string[] PdfHeadersEntradas =
        { "Fecha", "Ingrediente", "Cantidad", "Costo", "Proveedor", "Total (COP)" };
    private static readonly string[] PdfHeadersGastos =
        { "Fecha", "Ingrediente", "Cantidad", "Descripción", "Total (COP)" };
    private static readonly string[] PdfHeadersMermas =
        { "Fecha", "Ingrediente", "Cantidad", "Motivo", "Total (COP)" };
    private static readonly string[] PdfHeadersCortesias =
        { "Fecha", "Producto", "Cantidad", "Motivo", "Total (COP)" };
    private static readonly string[] PdfHeadersMercancia =
        { "Fecha", "Ingrediente", "Cantidad", "Proveedor", "Total (COP)", "Archivo" };
    private static readonly string[] PdfHeadersDiscos =
        { "Fecha", "Inicial", "Preparados", "Utilizados", "Merma", "Disponible", "Total (COP)" };
    private static readonly string[] PdfHeadersCajas =
        { "Fecha", "Inicial", "Recibidas", "Utilizadas", "Merma", "Disponible", "Total (COP)" };
    private static readonly string[] PdfHeadersConteos =
        { "Fecha", "Tipo", "Ingrediente", "Cantidad" };
    private static readonly string[] PdfHeadersBodega =
        { "Fecha", "Categoría", "Nombre", "Cantidad", "Origen" };

    public static string ExportarPdf(DatosReporte datos)
    {
        var nombre = $"Reporte_{datos.Inicio:yyyyMMdd}_{datos.Fin:yyyyMMdd}.pdf";
        var ruta = Path.Combine(CarpetaReportes, nombre);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Valerio's Pizza")
                        .FontSize(20).Bold().FontColor(Colors.Grey.Darken4);
                    col.Item().Text("Reporte de Inventario")
                        .FontSize(12).FontColor(Colors.Grey.Medium);
                    col.Item().Text($"Período: {datos.Inicio:dd/MM/yyyy} - {datos.Fin:dd/MM/yyyy}")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(14);

                    // Resumen
                    col.Item().Text("RESUMEN GENERAL").Bold().FontSize(11).FontColor(Colors.Grey.Darken3);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); });
                        FilaResumen(t, "Entradas", datos.Entradas.Count);
                        FilaResumen(t, "Gastos", datos.Gastos.Count);
                        FilaResumen(t, "Mermas", datos.Mermas.Count);
                        FilaResumen(t, "Cortesías", datos.Cortesias.Count);
                        FilaResumen(t, "Mercancía recibida", datos.Mercancias.Count);
                        FilaResumen(t, "Registros de discos", datos.Discos.Count);
                        FilaResumen(t, "Registros de cajas", datos.Cajas.Count);
                        FilaResumen(t, "Conteos (Apertura/Cierre)", datos.Conteos.Count);
                        FilaResumen(t, "Bodega (almacén)", datos.Bodega.Count);
                    });

                    SeccionTabla(col, "ENTRADAS", PdfHeadersEntradas,
                        datos.Entradas.Select(e => new[]
                        {
                            e.Fecha.ToString("dd/MM HH:mm"),
                            e.Ingrediente?.Nombre ?? "",
                            $"{e.Cantidad:N2} {e.Ingrediente?.UnidadMedida}",
                            $"${e.CostoTotal:N2}",
                            e.Proveedor ?? "",
                            FormatoCop(TotalCop(e.PrecioUnitario, e.Impuesto, e.Retencion, e.Cantidad))
                        }));

                    SeccionTabla(col, "GASTOS", PdfHeadersGastos,
                        datos.Gastos.Select(g => new[]
                        {
                            g.Fecha.ToString("dd/MM HH:mm"),
                            g.Ingrediente?.Nombre ?? "",
                            $"{g.Cantidad:N2} {g.Ingrediente?.UnidadMedida}",
                            g.Descripcion ?? "",
                            FormatoCop(TotalCop(g.PrecioUnitario, g.Impuesto, g.Retencion, g.Cantidad))
                        }));

                    SeccionTabla(col, "MERMAS", PdfHeadersMermas,
                        datos.Mermas.Select(m => new[]
                        {
                            m.Fecha.ToString("dd/MM HH:mm"),
                            m.Ingrediente?.Nombre ?? "",
                            $"{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
                            m.Motivo ?? "",
                            FormatoCop(TotalCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad))
                        }));

                    SeccionTabla(col, "CORTESÍAS", PdfHeadersCortesias,
                        datos.Cortesias.Select(c => new[]
                        {
                            c.Fecha.ToString("dd/MM HH:mm"),
                            c.Producto?.Nombre ?? "",
                            c.Cantidad.ToString(),
                            c.Motivo ?? "",
                            FormatoCop(TotalCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.Cantidad))
                        }));

                    SeccionTabla(col, "MERCANCÍA", PdfHeadersMercancia,
                        datos.Mercancias.Select(m => new[]
                        {
                            m.Fecha.ToString("dd/MM HH:mm"),
                            m.Ingrediente?.Nombre ?? "",
                            $"{m.Cantidad:N2} {m.Ingrediente?.UnidadMedida}",
                            m.Proveedor ?? "",
                            FormatoCop(TotalCop(m.PrecioUnitario, m.Impuesto, m.Retencion, m.Cantidad)),
                            string.IsNullOrWhiteSpace(m.RutaFactura) ? "—" : System.IO.Path.GetFileName(m.RutaFactura)
                        }));

                    SeccionTabla(col, "DISCOS", PdfHeadersDiscos,
                        datos.Discos.Select(d => new[]
                        {
                            d.Fecha.ToString("dd/MM"),
                            d.CantidadInicial.ToString(),
                            d.DiscosPreparados.ToString(),
                            d.DiscosUtilizados.ToString(),
                            d.DiscosMerma.ToString(),
                            d.CantidadDisponible.ToString(),
                            FormatoCop(TotalCop(d.PrecioUnitario, d.Impuesto, d.Retencion, d.CantidadInicial + d.DiscosPreparados))
                        }));

                    SeccionTabla(col, "CAJAS PARA PIZZAS (30 cm)", PdfHeadersCajas,
                        datos.Cajas.Select(c => new[]
                        {
                            c.Fecha.ToString("dd/MM"),
                            c.CantidadInicial.ToString(),
                            c.CajasRecibidas.ToString(),
                            c.CajasUtilizadas.ToString(),
                            c.CajasMerma.ToString(),
                            c.CantidadDisponible.ToString(),
                            FormatoCop(TotalCop(c.PrecioUnitario, c.Impuesto, c.Retencion, c.CantidadInicial + c.CajasRecibidas))
                        }));

                    var lineasConteo = datos.Conteos.SelectMany(c =>
                        c.Lineas.Select(l => new[]
                        {
                            c.Fecha.ToString("dd/MM HH:mm"),
                            c.Tipo.Mostrar(),
                            l.Ingrediente?.Nombre ?? "",
                            $"{l.Cantidad:N2} {l.Ingrediente?.UnidadMedida}"
                        }));
                    SeccionTabla(col, "CONTEOS APERTURA / CIERRE",
                        PdfHeadersConteos, lineasConteo);

                    SeccionTabla(col, "BODEGA (ALMACÉN GENERAL)", PdfHeadersBodega,
                        datos.Bodega.Select(b => new[]
                        {
                            b.FechaAgregado.ToString("dd/MM HH:mm"),
                            b.Categoria,
                            b.Nombre,
                            $"{b.Cantidad:N2} {b.UnidadMedida}",
                            b.Origen ?? ""
                        }));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontColor(Colors.Grey.Medium);
                    t.CurrentPageNumber().FontColor(Colors.Grey.Medium);
                    t.Span(" / ").FontColor(Colors.Grey.Medium);
                    t.TotalPages().FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(ruta);

        return ruta;
    }

    private static void FilaResumen(TableDescriptor t, string etiqueta, int valor)
    {
        t.Cell().PaddingVertical(2).Text(etiqueta);
        t.Cell().PaddingVertical(2).AlignRight().Text(valor.ToString()).Bold();
    }

    // Formato compacto para la columna "Total (COP)" del PDF: vacío si no hay desglose.
    private static string FormatoCop(decimal valor) =>
        valor == 0m ? "—" : $"${valor:N0}";

    private static void SeccionTabla(ColumnDescriptor col, string titulo, string[] headers, IEnumerable<string[]> filas)
    {
        var lista = filas.ToList();

        col.Item().Text(titulo).Bold().FontSize(11).FontColor(Colors.Grey.Darken3);

        if (lista.Count == 0)
        {
            col.Item().Text("Sin registros en el período.").Italic().FontColor(Colors.Grey.Medium);
            return;
        }

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (int i = 0; i < headers.Length; i++) c.RelativeColumn();
            });
            t.Header(h =>
            {
                foreach (var head in headers)
                {
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4)
                        .Text(head).Bold().FontSize(9);
                }
            });
            foreach (var fila in lista)
            {
                foreach (var celda in fila)
                {
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .Padding(4).Text(celda).FontSize(9);
                }
            }
        });
    }
}
