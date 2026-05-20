using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// Punto de la mini-tendencia de los últimos 7 días que se dibuja en el
/// Dashboard. La barra se renderiza con un <c>Rectangle</c> cuyo <c>Width</c>
/// se enlaza a <see cref="AnchoRelativo"/> (0..200 px). No requiere ninguna
/// librería de gráficos externa.
/// </summary>
public class TendenciaDia
{
    public string Etiqueta { get; init; } = string.Empty;
    public int Movimientos { get; init; }
    public double AnchoRelativo { get; init; }
    public bool EsHoy { get; init; }
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    [ObservableProperty]
    private string _fechaHoy = DateTime.Now.ToString("D");

    [ObservableProperty]
    private int _ingresosHoy = 0;

    [ObservableProperty]
    private int _gastosHoy = 0;

    [ObservableProperty]
    private int _mermasHoy = 0;

    [ObservableProperty]
    private int _cortesiasHoy = 0;

    // El valor del stepper para Discos. Editarlo NO escribe en la BD; eso
    // sólo ocurre cuando la usuaria presiona el botón "Actualizar". Así se
    // evita pelear con la pantalla "Registro Rápido" — que también inserta
    // filas en InventarioDiscos / InventarioCajas a su propio ritmo — y se
    // mantiene un único momento de intención por parte de la dueña.
    // NotifyCanExecuteChangedFor hace que el botón Actualizar se habilite
    // automáticamente cuando hay un cambio pendiente y se inhabilite cuando
    // el stepper iguala al último valor leído de BD.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActualizarDiscosCommand))]
    private int _discosDisponibles;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActualizarCajasCommand))]
    private int _cajasDisponibles;

    // Snapshot del valor que vino de la BD en la última carga; sirve para
    // distinguir cambios hechos por la usuaria (a través del NumericStepper)
    // de los que vienen de recargar la tabla y para saber si hay un ajuste
    // pendiente de aplicar.
    private int _discosBaseDb;
    private int _cajasBaseDb;

    [ObservableProperty]
    private ObservableCollection<Ingrediente> _alertasStock = new();

    /// <summary>Tendencia diaria (últimos 7 días) de movimientos totales.</summary>
    [ObservableProperty]
    private ObservableCollection<TendenciaDia> _tendencia7Dias = new();

    public bool SinAlertas => AlertasStock.Count == 0;

    public DashboardViewModel(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;

        // Fire-and-forget: la carga inicial se hace en segundo plano para no
        // bloquear la creación del VM. Cualquier excepción se captura y se
        // vuelca al log para no perderla.
        _ = CargarDatosHoyAsync();
    }

    // ============================================================
    //  Botón "Actualizar" — commit explícito con transacción
    // ============================================================

    /// <summary>Hay un ajuste de discos pendiente de aplicar.</summary>
    private bool PuedeActualizarDiscos() => DiscosDisponibles != _discosBaseDb;

    /// <summary>Hay un ajuste de cajas pendiente de aplicar.</summary>
    private bool PuedeActualizarCajas() => CajasDisponibles != _cajasBaseDb;

    /// <summary>
    /// Persiste el ajuste de discos como una fila en <c>InventarioDiscos</c>
    /// dentro de una transacción explícita. Para evitar que un cambio hecho
    /// concurrentemente desde "Registro Rápido" se pierda, se recalcula el
    /// balance vigente al inicio de la transacción y se usa para derivar el
    /// delta efectivo — de manera que <c>DiscosDisponibles</c> queda
    /// exactamente igual al valor que muestra el stepper.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeActualizarDiscos))]
    private async Task ActualizarDiscosAsync()
    {
        await ActualizarBalanceAsync(
            esDiscos: true,
            objetivo: DiscosDisponibles,
            etiquetaError: "ActualizarDiscosAsync");
        // Re-leer dashboard completo: refresca el balance y demás tarjetas.
        await CargarDatosHoyAsync();
    }

    /// <summary>Análogo a <see cref="ActualizarDiscosAsync"/> para Cajas.</summary>
    [RelayCommand(CanExecute = nameof(PuedeActualizarCajas))]
    private async Task ActualizarCajasAsync()
    {
        await ActualizarBalanceAsync(
            esDiscos: false,
            objetivo: CajasDisponibles,
            etiquetaError: "ActualizarCajasAsync");
        await CargarDatosHoyAsync();
    }

    /// <summary>
    /// Implementación común: dentro de una transacción SQLite, lee el
    /// balance vigente, calcula el delta hacia <paramref name="objetivo"/>
    /// y persiste una sola fila de ajuste. Si algo falla, <c>RollbackAsync</c>
    /// deja la tabla intacta.
    /// </summary>
    private async Task ActualizarBalanceAsync(bool esDiscos, int objetivo, string etiquetaError)
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                if (esDiscos)
                {
                    var balanceVigente = await db.InventarioDiscos
                        .SumAsync(d => (int?)(d.CantidadInicial + d.DiscosPreparados
                                          - d.DiscosUtilizados - d.DiscosMerma - d.DiscosCortesia)) ?? 0;
                    var delta = objetivo - balanceVigente;
                    if (delta != 0)
                    {
                        db.InventarioDiscos.Add(new InventarioDisco
                        {
                            Fecha = DateTime.Now,
                            CantidadInicial = delta > 0 ? delta : 0,
                            DiscosPreparados = 0,
                            DiscosUtilizados = delta < 0 ? -delta : 0,
                            DiscosMerma = 0,
                            DiscosCortesia = 0,
                            Notas = $"Ajuste manual desde Dashboard (Δ {(delta > 0 ? "+" : "")}{delta})"
                        });
                        await db.SaveChangesAsync();
                    }
                }
                else
                {
                    var balanceVigente = await db.InventarioCajas
                        .SumAsync(c => (int?)(c.CantidadInicial + c.CajasRecibidas
                                          - c.CajasUtilizadas - c.CajasMerma)) ?? 0;
                    var delta = objetivo - balanceVigente;
                    if (delta != 0)
                    {
                        db.InventarioCajas.Add(new InventarioCaja
                        {
                            Fecha = DateTime.Now,
                            CantidadInicial = delta > 0 ? delta : 0,
                            CajasRecibidas = 0,
                            CajasUtilizadas = delta < 0 ? -delta : 0,
                            CajasMerma = 0,
                            Notas = $"Ajuste manual desde Dashboard (Δ {(delta > 0 ? "+" : "")}{delta})"
                        });
                        await db.SaveChangesAsync();
                    }
                }

                await tx.CommitAsync();
            }
            catch
            {
                // Cualquier error revierte la transacción para no dejar la
                // tabla en un estado a medias y se vuelve a lanzar para que
                // el catch externo lo registre y notifique a la usuaria.
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, etiquetaError);
            MessageBox.Show(
                $"No se pudo guardar el ajuste.\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CargarDatosHoyAsync()
    {
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var hoy = DateTime.Today;

            IngresosHoy = await db.Entradas.CountAsync(e => e.Fecha.Date == hoy);
            GastosHoy = await db.Gastos.CountAsync(g => g.Fecha.Date == hoy);
            MermasHoy = await db.Mermas.CountAsync(m => m.Fecha.Date == hoy);
            CortesiasHoy = await db.Cortesias.CountAsync(c => c.Fecha.Date == hoy);

            // Discos disponibles: balance acumulado de TODOS los registros de discos
            // (mismo modelo que Cajas). Cada nuevo guardado SUMA su CantidadInicial y
            // DiscosPreparados al stock y RESTA Utilizados/Merma/Cortesía. Esto permite
            // que las "DISCOS INICIALES" de un guardado posterior se acumulen, y que
            // los Preparados/Utilizados/Merma/Cortesía se reflejen en tiempo real.
            var discosBalance = await db.InventarioDiscos
                .SumAsync(d => (int?)(d.CantidadInicial + d.DiscosPreparados
                                  - d.DiscosUtilizados - d.DiscosMerma - d.DiscosCortesia)) ?? 0;

            // Cajas disponibles: balance acumulado de todos los registros de cajas.
            // Cada fila aporta CantidadInicial + Recibidas - Utilizadas - Merma, lo que
            // refleja el stock real al momento (incluye reposiciones y mermas históricas).
            var cajasBalance = await db.InventarioCajas
                .SumAsync(c => (int?)(c.CantidadInicial + c.CajasRecibidas - c.CajasUtilizadas - c.CajasMerma)) ?? 0;

            // Asignamos primero el base y luego el valor visible para que
            // PuedeActualizarXxx vea base == disponibles y deje los botones
            // "Actualizar" deshabilitados al cargar.
            _discosBaseDb = discosBalance;
            _cajasBaseDb = cajasBalance;
            DiscosDisponibles = discosBalance;
            CajasDisponibles = cajasBalance;
            ActualizarDiscosCommand.NotifyCanExecuteChanged();
            ActualizarCajasCommand.NotifyCanExecuteChanged();

            // Cargar alertas de stock bajo
            var ingredientesBajos = await db.Ingredientes
                .Where(i => i.CantidadActual <= i.CantidadMinima && i.CantidadMinima > 0)
                .OrderBy(i => i.CantidadActual)
                .ToListAsync();
            AlertasStock.Clear();
            foreach (var ing in ingredientesBajos)
            {
                AlertasStock.Add(ing);
            }

            OnPropertyChanged(nameof(SinAlertas));

            await CargarTendenciaAsync(db);
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, "DashboardViewModel.CargarDatosHoyAsync");
        }
    }

    /// <summary>
    /// Calcula los movimientos totales (entradas+gastos+mermas+cortesías+mercancías)
    /// de cada uno de los últimos 7 días y normaliza el ancho de barra para
    /// que el día con más movimientos llene 200 px.
    /// </summary>
    private async Task CargarTendenciaAsync(PizzeriaDbContext db)
    {
        const double AnchoMaximoPx = 200.0;
        var hoy = DateTime.Today;
        var inicio = hoy.AddDays(-6);
        var finExclusivo = hoy.AddDays(1);

        // Una sola consulta por tabla, agrupando en memoria por día.
        var entradas = await db.Entradas.Where(e => e.Fecha >= inicio && e.Fecha < finExclusivo)
            .Select(e => e.Fecha).ToListAsync();
        var gastos = await db.Gastos.Where(g => g.Fecha >= inicio && g.Fecha < finExclusivo)
            .Select(g => g.Fecha).ToListAsync();
        var mermas = await db.Mermas.Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
            .Select(m => m.Fecha).ToListAsync();
        var cortesias = await db.Cortesias.Where(c => c.Fecha >= inicio && c.Fecha < finExclusivo)
            .Select(c => c.Fecha).ToListAsync();
        var mercancias = await db.MercanciasRecibidas.Where(m => m.Fecha >= inicio && m.Fecha < finExclusivo)
            .Select(m => m.Fecha).ToListAsync();

        int Conteo(List<DateTime> origen, DateTime fecha) => origen.Count(f => f.Date == fecha);

        var totalesPorDia = new int[7];
        for (int i = 0; i < 7; i++)
        {
            var fecha = inicio.AddDays(i);
            totalesPorDia[i] = Conteo(entradas, fecha) + Conteo(gastos, fecha)
                + Conteo(mermas, fecha) + Conteo(cortesias, fecha) + Conteo(mercancias, fecha);
        }

        var max = totalesPorDia.Max();
        Tendencia7Dias.Clear();
        string[] nombresDias = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
        for (int i = 0; i < 7; i++)
        {
            var fecha = inicio.AddDays(i);
            var total = totalesPorDia[i];
            Tendencia7Dias.Add(new TendenciaDia
            {
                Etiqueta = $"{nombresDias[(int)fecha.DayOfWeek]} {fecha:dd/MM}",
                Movimientos = total,
                AnchoRelativo = max > 0 ? (total / (double)max) * AnchoMaximoPx : 0,
                EsHoy = fecha == hoy
            });
        }
    }

    [RelayCommand]
    public Task ActualizarDatosAsync() => CargarDatosHoyAsync();
}
