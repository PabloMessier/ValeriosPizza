using System;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ValeriosPizza.Models;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Gestión de la factura digital (PDF / imagen) adjunta a una mercancía:
/// selección de archivo, copia a la carpeta interna de la app, apertura
/// con el visor predeterminado y limpieza del adjunto en el formulario.
/// </summary>
public partial class MercanciaNuevaViewModel
{
    /// <summary>
    /// Ruta absoluta del archivo de factura ya guardado (PDF/imagen). Null si
    /// no hay adjunto. Se actualiza al elegir archivo y al cargar para editar.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneFacturaAdjunta))]
    [NotifyPropertyChangedFor(nameof(NombreFacturaAdjunta))]
    private string? _rutaFactura;

    /// <summary>Ruta de un archivo seleccionado pero todavía no copiado a la carpeta de la app (modo crear/edit).</summary>
    private string? _rutaFacturaPendiente;

    public bool TieneFacturaAdjunta =>
        !string.IsNullOrWhiteSpace(RutaFactura) || !string.IsNullOrWhiteSpace(_rutaFacturaPendiente);

    public string NombreFacturaAdjunta
    {
        get
        {
            var ruta = _rutaFacturaPendiente ?? RutaFactura;
            return string.IsNullOrWhiteSpace(ruta) ? "Sin archivo adjunto" : Path.GetFileName(ruta);
        }
    }

    /// <summary>Carpeta donde se guardan las copias de las facturas digitales.</summary>
    private static string CarpetaFacturas
    {
        get
        {
            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ValeriosPizzeria", "Facturas");
            if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
            return carpeta;
        }
    }

    [RelayCommand]
    private void SeleccionarFactura()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleccionar factura digital",
            Filter = "Documentos e imágenes (*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff)|*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff|PDF (*.pdf)|*.pdf|Imágenes (*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var info = new FileInfo(dlg.FileName);
            if (info.Length > 20 * 1024 * 1024)
            {
                MessageBox.Show("El archivo supera los 20 MB. Use una versión más liviana de la factura.",
                    "Archivo muy grande", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _rutaFacturaPendiente = dlg.FileName;
            OnPropertyChanged(nameof(TieneFacturaAdjunta));
            OnPropertyChanged(nameof(NombreFacturaAdjunta));
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Seleccionar factura");
            MessageBox.Show($"No se pudo leer el archivo.\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void QuitarFactura()
    {
        _rutaFacturaPendiente = null;
        RutaFactura = null;
        OnPropertyChanged(nameof(TieneFacturaAdjunta));
        OnPropertyChanged(nameof(NombreFacturaAdjunta));
    }

    /// <summary>Abre la factura adjunta del formulario (si la hay).</summary>
    [RelayCommand]
    private void AbrirFacturaActual()
    {
        var ruta = _rutaFacturaPendiente ?? RutaFactura;
        if (string.IsNullOrWhiteSpace(ruta)) return;
        AbrirArchivo(ruta);
    }

    /// <summary>Abre la factura asociada a un registro específico de la lista.</summary>
    [RelayCommand]
    private void AbrirFactura(MercanciaRecibida? mercancia)
    {
        if (mercancia == null || string.IsNullOrWhiteSpace(mercancia.RutaFactura)) return;
        AbrirArchivo(mercancia.RutaFactura);
    }

    private static void AbrirArchivo(string ruta)
    {
        try
        {
            if (!File.Exists(ruta))
            {
                MessageBox.Show($"El archivo ya no existe en:\n{ruta}", "Archivo no encontrado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var visor = new ValeriosPizza.Windows.VisorFacturaWindow(ruta);
            var owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.MainWindow;
            if (owner != null && owner != visor) visor.Owner = owner;
            visor.Show();
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "Abrir factura");
            MessageBox.Show($"No se pudo abrir el archivo.\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Copia el archivo seleccionado a la carpeta de facturas de la
    /// aplicación con un nombre único (timestamp + nombre original) y
    /// devuelve la ruta destino.
    /// </summary>
    private static string CopiarFacturaACarpeta(string rutaOrigen)
    {
        var nombreUnico = $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{Path.GetFileName(rutaOrigen)}";
        var destino = Path.Combine(CarpetaFacturas, nombreUnico);
        File.Copy(rutaOrigen, destino, overwrite: false);
        return destino;
    }
}
