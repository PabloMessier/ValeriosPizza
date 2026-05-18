using System;
using System.Windows;

namespace ValeriosPizza.Services;

/// <summary>
/// Temas visuales disponibles en la aplicación.
/// </summary>
public enum AppTheme
{
    Light,
    Dark,
    Metallic,
    RojoDracula
}

/// <summary>
/// Gestiona el cambio de tema en tiempo de ejecución reemplazando el primer
/// <see cref="ResourceDictionary"/> combinado en <see cref="Application.Resources"/>.
/// La preferencia se persiste en %LOCALAPPDATA%\ValeriosPizza\theme.txt.
/// </summary>
public static class ThemeManager
{
    private static readonly string PreferenciaArchivo = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValeriosPizzeria",
        "theme.txt");

    public static AppTheme TemaActual { get; private set; } = AppTheme.Light;

    public static event Action<AppTheme>? TemaCambiado;

    /// <summary>
    /// Asembly del proyecto WPF; se calcula a partir del tipo del propio
    /// ThemeManager para que las URIs pack se resuelvan independientemente del
    /// nombre del exe (útil si la app se renombra al publicar).
    /// </summary>
    private static readonly string AssemblyName =
        typeof(ThemeManager).Assembly.GetName().Name ?? "ValeriosPizzeria";

    public static void AplicarTema(AppTheme tema)
    {
        var app = Application.Current;
        if (app == null) return;

        var rutaTema = tema switch
        {
            AppTheme.Dark        => "Themes/DarkTheme.xaml",
            AppTheme.Metallic    => "Themes/MetallicTheme.xaml",
            AppTheme.RojoDracula => "Themes/RojoDraculaTheme.xaml",
            _                    => "Themes/LightTheme.xaml"
        };

        // Pack URI absoluta: garantiza la resolución del recurso aunque el
        // diccionario se cargue desde un contexto donde la URI relativa fallaría
        // (por ejemplo, en pruebas o al hospedar dentro de otra app).
        var packUri = new Uri($"pack://application:,,,/{AssemblyName};component/{rutaTema}",
            UriKind.Absolute);

        ResourceDictionary nuevoDicc;
        try
        {
            nuevoDicc = new ResourceDictionary { Source = packUri };
        }
        catch (Exception)
        {
            // Si el archivo de tema falta, no destruimos el actual: simplemente
            // dejamos el tema previo en su sitio. El error se registrará en el
            // hook global de excepciones.
            return;
        }

        // El primer diccionario combinado es siempre el tema activo.
        if (app.Resources.MergedDictionaries.Count == 0)
        {
            app.Resources.MergedDictionaries.Add(nuevoDicc);
        }
        else
        {
            app.Resources.MergedDictionaries[0] = nuevoDicc;
        }

        TemaActual = tema;
        GuardarPreferencia(tema);
        TemaCambiado?.Invoke(tema);
    }

    public static AppTheme CargarPreferencia()
    {
        try
        {
            if (System.IO.File.Exists(PreferenciaArchivo))
            {
                var texto = System.IO.File.ReadAllText(PreferenciaArchivo).Trim();
                if (Enum.TryParse<AppTheme>(texto, true, out var t))
                {
                    return t;
                }
            }
        }
        catch
        {
            // ignorar; usar tema por defecto
        }
        return AppTheme.Light;
    }

    private static void GuardarPreferencia(AppTheme tema)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(PreferenciaArchivo);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            System.IO.File.WriteAllText(PreferenciaArchivo, tema.ToString());
        }
        catch
        {
            // no es crítico
        }
    }
}
