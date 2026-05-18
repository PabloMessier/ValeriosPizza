using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;

namespace ValeriosPizza.Services;

/// <summary>
/// Recordatorio del conteo de CIERRE diario. Una vez iniciado, dispara cada
/// 15 minutos y, a partir de la <see cref="HoraRecordatorio"/>, comprueba si
/// ya existe un <see cref="ConteoInventario"/> de tipo <see cref="TipoConteo.Cierre"/>
/// para la fecha actual. Si no existe, muestra UNA sola alerta por día para
/// no molestar a la usuaria.
///
/// El servicio depende de <see cref="DispatcherTimer"/> y debe construirse en
/// el hilo de UI (lo hacemos desde <c>MainWindow.Loaded</c>).
/// </summary>
public sealed class CierreReminderService : IDisposable
{
    /// <summary>Hora local a partir de la cual se empieza a verificar (24h).</summary>
    private const int HoraRecordatorio = 21;

    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly DispatcherTimer _timer;
    private DateOnly _ultimoAvisoFecha = DateOnly.MinValue;
    private bool _iniciado;

    public CierreReminderService(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _timer.Tick += (_, _) => Verificar();
    }

    /// <summary>Arranca el timer y ejecuta una verificación inmediata.</summary>
    public void Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;
        _timer.Start();
        Verificar();
    }

    private void Verificar()
    {
        var ahora = DateTime.Now;
        if (ahora.Hour < HoraRecordatorio) return;

        var hoyOnly = DateOnly.FromDateTime(ahora);
        if (_ultimoAvisoFecha == hoyOnly) return;

        bool existeCierre;
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var hoy = ahora.Date;
            var manana = hoy.AddDays(1);
            existeCierre = db.ConteosInventario.Any(c =>
                c.Tipo == TipoConteo.Cierre &&
                c.Fecha >= hoy && c.Fecha < manana);
        }
        catch
        {
            // Si la BD no responde no insistimos: lo intentamos en el próximo tick.
            return;
        }

        // Marcamos la fecha tanto si existe como si no, para mostrar el aviso
        // máximo una vez por día.
        _ultimoAvisoFecha = hoyOnly;
        if (existeCierre) return;

        MessageBox.Show(
            Application.Current?.MainWindow!,
            $"Aún no se ha registrado el conteo de CIERRE de hoy ({ahora:dd/MM/yyyy}).\n\n" +
            "Recuerda hacerlo antes de finalizar el turno desde:\n" +
            "    Registro Rápido → sección \"Conteos\".",
            "Recordatorio de cierre",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
