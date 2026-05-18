using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace ValeriosPizza.Windows;

public partial class VisorFacturaWindow : Window
{
    private static readonly string[] ExtensionesImagen =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };

    private readonly string _rutaArchivo;

    public VisorFacturaWindow(string rutaArchivo)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;
        Loaded += VisorFacturaWindow_Loaded;
    }

    private async void VisorFacturaWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_rutaArchivo))
        {
            MostrarMensaje($"No se encontró el archivo:\n{_rutaArchivo}");
            return;
        }

        NombreArchivoText.Text = Path.GetFileName(_rutaArchivo);
        var ext = Path.GetExtension(_rutaArchivo).ToLowerInvariant();

        if (Array.IndexOf(ExtensionesImagen, ext) >= 0)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(_rutaArchivo, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                VistaImagen.Source = bmp;
                ImagenScroll.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MostrarMensaje($"No se pudo cargar la imagen:\n{ex.Message}");
            }
            return;
        }

        // PDF y otros: usar WebView2 (Edge) para renderizar
        try
        {
            var carpetaDatos = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ValeriosPizzeria", "WebView2");
            Directory.CreateDirectory(carpetaDatos);
            var entorno = await CoreWebView2Environment.CreateAsync(null, carpetaDatos);
            await VistaWeb.EnsureCoreWebView2Async(entorno);
            VistaWeb.Visibility = Visibility.Visible;
            VistaWeb.CoreWebView2.Navigate(new Uri(_rutaArchivo).AbsoluteUri);
        }
        catch (Exception ex)
        {
            MostrarMensaje(
                "No se pudo iniciar la vista previa del PDF.\n\n" +
                "Asegúrate de tener instalado el WebView2 Runtime de Microsoft Edge.\n\n" +
                $"Detalle: {ex.Message}");
        }
    }

    private void MostrarMensaje(string texto)
    {
        MensajeText.Text = texto;
        MensajeText.Visibility = Visibility.Visible;
    }

    private void Descargar_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_rutaArchivo))
        {
            MessageBox.Show(this, "El archivo original ya no existe.", "Descargar copia",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ext = Path.GetExtension(_rutaArchivo);
        var dlg = new SaveFileDialog
        {
            FileName = Path.GetFileName(_rutaArchivo),
            DefaultExt = ext,
            Filter = string.IsNullOrWhiteSpace(ext)
                ? "Todos los archivos (*.*)|*.*"
                : $"Archivo {ext} (*{ext})|*{ext}|Todos los archivos (*.*)|*.*",
            Title = "Guardar copia de la factura"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            File.Copy(_rutaArchivo, dlg.FileName, overwrite: true);
            MessageBox.Show(this, "Copia guardada correctamente.", "Descargar copia",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo guardar la copia:\n{ex.Message}",
                "Descargar copia", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AbrirExterno_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_rutaArchivo))
        {
            MessageBox.Show(this, "El archivo original ya no existe.", "Abrir factura",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _rutaArchivo,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo abrir el archivo:\n{ex.Message}",
                "Abrir factura", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();
}
