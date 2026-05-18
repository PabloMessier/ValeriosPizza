using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;

namespace ValeriosPizza.Services.UndoRedo;

/// <summary>
/// Base de un comando deshacer-rehacer. Cada subtipo encapsula la información
/// suficiente para ejecutar la operación contra la BD y para revertirla más
/// tarde, incluso después de cerrar y reabrir la app (la pila se persiste a
/// disco como JSON).
///
/// La discriminación de tipos para System.Text.Json se declara aquí mediante
/// <see cref="JsonDerivedTypeAttribute"/>; al añadir un nuevo comando hay que
/// registrarlo en esta lista o no se podrá deserializar la pila guardada.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Commands.RegistrarEntradaCommand), "Entrada")]
[JsonDerivedType(typeof(Commands.RegistrarGastoCommand), "Gasto")]
[JsonDerivedType(typeof(Commands.RegistrarMermaCommand), "Merma")]
[JsonDerivedType(typeof(Commands.RegistrarCortesiaCommand), "Cortesia")]
[JsonDerivedType(typeof(Commands.RegistrarMovimientoDiscosCommand), "Discos")]
[JsonDerivedType(typeof(Commands.RegistrarMovimientoCajasCommand), "Cajas")]
[JsonDerivedType(typeof(Commands.RegistrarMercanciaCommand), "Mercancia")]
[JsonDerivedType(typeof(Commands.EliminarMercanciaCommand), "MercanciaEliminada")]
[JsonDerivedType(typeof(Commands.EliminarIngredienteCommand), "IngredienteEliminado")]
[JsonDerivedType(typeof(Commands.CambiarEstadoProductoCommand), "EstadoProducto")]
[JsonDerivedType(typeof(Commands.RegistrarConteoCommand), "Conteo")]
public abstract class UndoableCommandBase
{
    /// <summary>
    /// Cuándo se ejecutó originalmente. Se usa solo para mostrar el historial
    /// y no influye en la lógica de undo.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Texto corto descriptivo (ej: "Entrada de Harina, 5 kg") que se muestra
    /// en los tooltips de los botones Deshacer/Rehacer.
    /// </summary>
    [JsonIgnore]
    public abstract string Descripcion { get; }

    /// <summary>
    /// Aplica el cambio en la BD. Si la operación es una INSERCIÓN debe
    /// guardar la PK asignada por la BD para poder revertirla luego.
    /// </summary>
    public abstract Task EjecutarAsync(IDbContextFactory<PizzeriaDbContext> dbFactory);

    /// <summary>
    /// Revierte el cambio. Si el registro fue eliminado por otro flujo
    /// mientras tanto, lanzar <see cref="InvalidOperationException"/> para que
    /// el servicio limpie esta entrada del historial.
    /// </summary>
    public abstract Task DeshacerAsync(IDbContextFactory<PizzeriaDbContext> dbFactory);
}
