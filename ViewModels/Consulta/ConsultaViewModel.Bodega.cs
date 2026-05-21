using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Models;
using ValeriosPizza.Services;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Comando para enviar un registro de consulta a la pantalla "Bodega".
/// Es un FK blando: si el ingrediente con ese nombre existe se vincula
/// para futuras consultas, pero la fila se guarda igual aunque no se
/// pueda resolver (por ejemplo, registros históricos con ingredientes
/// eliminados).
/// </summary>
public partial class ConsultaViewModel
{
    [RelayCommand]
    private async Task AgregarABodegaAsync(RegistroConsulta? registro)
    {
        if (registro == null) return;

        int? ingredienteId = null;
        string unidad = string.Empty;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var ing = await db.Ingredientes
                .FirstOrDefaultAsync(i => i.Nombre == registro.Ingrediente);
            if (ing != null)
            {
                ingredienteId = ing.Id;
                unidad = ing.UnidadMedida;
            }

            db.BodegaItems.Add(new BodegaItem
            {
                FechaAgregado = System.DateTime.Now,
                Nombre = registro.Ingrediente,
                Categoria = registro.Tipo switch
                {
                    "MERCANCÍA" => "Mercancía",
                    "ENTRADA" or "GASTO" or "MERMA" or "APERTURA" or "CIERRE" => "Ingrediente",
                    "CORTESÍA" => "Producto",
                    _ => "Otro"
                },
                // Para consulta no tenemos un número suelto: guardamos la
                // cadena formateada como nota, y dejamos Cantidad en 0 para
                // no confundir totales.
                Cantidad = 0,
                UnidadMedida = unidad,
                Notas = $"{registro.Tipo} → {registro.Cantidad}. {registro.Detalles}".Trim('.', ' '),
                Origen = $"Consulta ({registro.Tipo})",
                IngredienteId = ingredienteId
            });
            await db.SaveChangesAsync();
            BodegaNotifier.NotificarCambio();
        }
        catch (System.Exception ex)
        {
            App.GuardarErrorDump(ex, "AgregarABodega (Consulta)");
            MessageBox.Show($"No se pudo agregar a bodega.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
