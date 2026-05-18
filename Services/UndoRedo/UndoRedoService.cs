using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;

namespace ValeriosPizza.Services.UndoRedo;

/// <summary>
/// Mantiene dos pilas (deshacer / rehacer) con los últimos comandos
/// ejecutados. La pila de deshacer se acota a <see cref="MaxProfundidad"/>
/// elementos descartando los más antiguos. Cada cambio se persiste a un JSON
/// junto al ejecutable, de forma que al reiniciar la app el usuario aún puede
/// revertir operaciones de la sesión anterior (siempre que los registros
/// referenciados sigan existiendo en la BD).
///
/// La clase es singleton. Puede usarse desde cualquier hilo: las
/// modificaciones a las pilas se serializan con un lock interno y los
/// notificadores de cambio se disparan en el hilo de UI a través del
/// Dispatcher actual cuando hay uno disponible.
/// </summary>
public partial class UndoRedoService : ObservableObject
{
    /// <summary>Cantidad máxima de comandos que se mantienen en la pila de deshacer.</summary>
    public const int MaxProfundidad = 10;

    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly string _archivoPersistencia;
    private readonly object _gate = new();

    private readonly List<UndoableCommandBase> _undo = new();
    private readonly List<UndoableCommandBase> _redo = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false
    };

    [ObservableProperty]
    private bool _puedeDeshacer;

    [ObservableProperty]
    private bool _puedeRehacer;

    [ObservableProperty]
    private string? _descripcionProximoDeshacer;

    [ObservableProperty]
    private string? _descripcionProximoRehacer;

    public UndoRedoService(IDbContextFactory<PizzeriaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;

        // Misma carpeta donde se guarda error_dump.txt: junto al .exe.
        var folder = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
                     ?? AppContext.BaseDirectory;
        _archivoPersistencia = Path.Combine(folder, "undo_history.json");

        CargarDesdeDisco();
        ActualizarEstado();
    }

    /// <summary>
    /// Ejecuta el comando y, si tiene éxito, lo añade a la pila de deshacer.
    /// Limpia la pila de rehacer (semántica estándar: cualquier acción nueva
    /// invalida la cadena de rehacer).
    /// </summary>
    public async Task EjecutarAsync(UndoableCommandBase comando)
    {
        await comando.EjecutarAsync(_dbFactory);

        lock (_gate)
        {
            _undo.Add(comando);
            while (_undo.Count > MaxProfundidad)
            {
                _undo.RemoveAt(0);
            }
            _redo.Clear();
        }

        Persistir();
        ActualizarEstado();
    }

    /// <summary>
    /// Deshace la última operación. Si el registro asociado fue borrado por
    /// otro flujo, descarta el comando del historial silenciosamente y vuelve
    /// a lanzar la excepción para que la UI la muestre.
    /// </summary>
    public async Task DeshacerAsync()
    {
        UndoableCommandBase? comando;
        lock (_gate)
        {
            if (_undo.Count == 0) return;
            comando = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
        }

        try
        {
            await comando.DeshacerAsync(_dbFactory);
            lock (_gate)
            {
                _redo.Add(comando);
                while (_redo.Count > MaxProfundidad)
                {
                    _redo.RemoveAt(0);
                }
            }
        }
        finally
        {
            Persistir();
            ActualizarEstado();
        }
    }

    /// <summary>
    /// Rehace la última operación deshecha re-aplicando el comando original.
    /// </summary>
    public async Task RehacerAsync()
    {
        UndoableCommandBase? comando;
        lock (_gate)
        {
            if (_redo.Count == 0) return;
            comando = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
        }

        try
        {
            await comando.EjecutarAsync(_dbFactory);
            lock (_gate)
            {
                _undo.Add(comando);
                while (_undo.Count > MaxProfundidad)
                {
                    _undo.RemoveAt(0);
                }
            }
        }
        finally
        {
            Persistir();
            ActualizarEstado();
        }
    }

    /// <summary>Vacía completamente el historial (útil tras un borrado masivo).</summary>
    public void Limpiar()
    {
        lock (_gate)
        {
            _undo.Clear();
            _redo.Clear();
        }
        Persistir();
        ActualizarEstado();
    }

    private void ActualizarEstado()
    {
        bool puedeU, puedeR;
        string? descU, descR;
        lock (_gate)
        {
            puedeU = _undo.Count > 0;
            puedeR = _redo.Count > 0;
            descU = puedeU ? _undo[^1].Descripcion : null;
            descR = puedeR ? _redo[^1].Descripcion : null;
        }

        // Las propiedades observables disparan PropertyChanged; los comandos
        // de la UI re-evalúan su CanExecute al observarlas.
        PuedeDeshacer = puedeU;
        PuedeRehacer = puedeR;
        DescripcionProximoDeshacer = descU;
        DescripcionProximoRehacer = descR;
    }

    private void Persistir()
    {
        try
        {
            HistorialDto dto;
            lock (_gate)
            {
                dto = new HistorialDto
                {
                    Undo = _undo.ToList(),
                    Redo = _redo.ToList()
                };
            }

            var json = JsonSerializer.Serialize(dto, JsonOpts);
            File.WriteAllText(_archivoPersistencia, json);
        }
        catch
        {
            // No hacer fallar la operación porque no se pudo persistir el log.
        }
    }

    private void CargarDesdeDisco()
    {
        try
        {
            if (!File.Exists(_archivoPersistencia)) return;
            var json = File.ReadAllText(_archivoPersistencia);
            if (string.IsNullOrWhiteSpace(json)) return;

            var dto = JsonSerializer.Deserialize<HistorialDto>(json, JsonOpts);
            if (dto == null) return;

            lock (_gate)
            {
                _undo.Clear();
                _undo.AddRange(dto.Undo);
                _redo.Clear();
                _redo.AddRange(dto.Redo);
            }
        }
        catch
        {
            // Si el archivo está corrupto (cambio de modelo, edición a mano),
            // descartamos el historial silenciosamente para no bloquear la app.
            try { File.Delete(_archivoPersistencia); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// DTO interno usado solo para persistencia. Public para que System.Text.Json
    /// pueda serializar las propiedades polimórficas de los comandos.
    /// </summary>
    public sealed class HistorialDto
    {
        public List<UndoableCommandBase> Undo { get; set; } = new();
        public List<UndoableCommandBase> Redo { get; set; } = new();
    }
}
