using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using ValeriosPizza.Data;
using ValeriosPizza.Models;
using ValeriosPizza.Services;
using ValeriosPizza.Services.UndoRedo;

namespace ValeriosPizza.ViewModels;

/// <summary>
/// ViewModel del módulo "Registro Rápido". La pantalla integra ocho
/// secciones (Entrada, Gasto, Merma, Cortesía, Discos, Cajas, Inventario
/// Apertura, Inventario Cierre); cada sección vive en su propio archivo
/// <c>RegistroRapidoViewModel.&lt;Seccion&gt;.cs</c> como
/// <c>partial class</c>, manteniendo este archivo enfocado en el estado
/// compartido y la orquestación del guardado en bloque al cerrar la app.
///
/// Esta clase es responsable de:
/// <list type="bullet">
///   <item>Catálogos compartidos (<see cref="Ingredientes"/>, <see cref="Productos"/>).</item>
///   <item>Recarga reactiva via WeakReferenceMessenger.</item>
///   <item>El flujo "Guardar todo lo pendiente al cerrar" sin diálogos.</item>
/// </list>
/// </summary>
public partial class RegistroRapidoViewModel : ViewModelBase
{
    private readonly IDbContextFactory<PizzeriaDbContext> _dbFactory;
    private readonly UndoRedoService _undoRedo;

    [ObservableProperty]
    private ObservableCollection<Ingrediente> _ingredientes = new();

    [ObservableProperty]
    private ObservableCollection<Producto> _productos = new();

    public RegistroRapidoViewModel(
        IDbContextFactory<PizzeriaDbContext> dbFactory,
        UndoRedoService undoRedo)
    {
        _dbFactory = dbFactory;
        _undoRedo = undoRedo;

        // Las vistas paginadas viven en la sección Conteo (archivo separado).
        InicializarPaginadosConteo();

        CargarDatos();

        // Suscripción con referencia débil: si este VM se recicla, el
        // messenger libera automáticamente el handler.
        WeakReferenceMessenger.Default.Register<IngredientesChangedMessage>(
            this, (_, _) => OnIngredientesActualizados());
        // También nos suscribimos a cambios en Productos para que el combo
        // de Cortesía se actualice si la dueña agrega/edita productos desde
        // Inventario o desde el botón "+" del propio formulario.
        WeakReferenceMessenger.Default.Register<ProductosChangedMessage>(
            this, (_, _) => CargarDatos());
    }

    private void OnIngredientesActualizados()
    {
        // Recargamos los listados para reflejar nuevos ingredientes creados desde otras pantallas.
        CargarDatos();
        SincronizarConteoConIngredientes(LineasApertura);
        SincronizarConteoConIngredientes(LineasCierre);
    }

    private void CargarDatos()
    {
        using var db = _dbFactory.CreateDbContext();

        var ingredientesActivos = db.Ingredientes
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .ToList();

        Ingredientes.Clear();
        foreach (var ing in ingredientesActivos) Ingredientes.Add(ing);

        Productos.Clear();
        // Excluimos solo Descontinuados; los Agotados siguen apareciendo para
        // que la usuaria pueda registrar cortesías históricas si fuese el caso.
        foreach (var prod in db.Productos
            .Where(p => p.Estado != EstadoProducto.Descontinuado)
            .OrderBy(p => p.Nombre)
            .ToList())
        {
            Productos.Add(prod);
        }

        // Si los listados de Apertura/Cierre están vacíos, prellenarlos.
        PrellenarConteosSiVacios(ingredientesActivos);
    }

    /// <summary>
    /// Concatena los mensajes de error de las propiedades indicadas. Si
    /// alguna tiene errores, devuelve <c>true</c> con el texto agregado en
    /// <paramref name="mensaje"/>; si todas son válidas, devuelve <c>false</c>.
    /// </summary>
    private bool HasErroresEnPropiedades(string[] propiedades, out string mensaje)
    {
        var errores = new List<string>();
        foreach (var prop in propiedades)
        {
            foreach (var err in GetErrors(prop))
            {
                if (err is System.ComponentModel.DataAnnotations.ValidationResult vr
                    && !string.IsNullOrEmpty(vr.ErrorMessage))
                {
                    errores.Add("• " + vr.ErrorMessage);
                }
            }
        }
        mensaje = string.Join(Environment.NewLine, errores);
        return errores.Count > 0;
    }

    // ============================================================
    // SOPORTE PARA "GUARDAR ANTES DE SALIR"
    // ============================================================

    /// <summary>
    /// Indica si alguna sección del formulario tiene datos pendientes que aún
    /// no se han persistido a la base de datos. Se usa al cerrar la app para
    /// preguntarle a la usuaria si quiere guardar o descartar.
    /// </summary>
    public bool TieneCambiosSinGuardar => SeccionesPendientes().Any();

    /// <summary>
    /// Devuelve los nombres de las secciones del formulario que tienen datos
    /// sin guardar. Una sección "tiene datos" cuando algún campo significativo
    /// fue modificado por la usuaria.
    /// </summary>
    public IReadOnlyList<string> SeccionesPendientes()
    {
        var lista = new List<string>();
        if (TieneDatosEntrada())   lista.Add("Entrada");
        if (TieneDatosGasto())     lista.Add("Gasto");
        if (TieneDatosMerma())     lista.Add("Merma");
        if (TieneDatosCortesia())  lista.Add("Cortesía");
        if (TieneDatosDiscos())    lista.Add("Discos");
        if (TieneDatosCajas())     lista.Add("Cajas");
        if (TieneDatosConteo(LineasApertura, NotasApertura)) lista.Add("Inventario Apertura");
        if (TieneDatosConteo(LineasCierre, NotasCierre))     lista.Add("Inventario Cierre");
        return lista;
    }

    /// <summary>
    /// Resultado del intento de guardar todas las secciones pendientes al
    /// cerrar la aplicación.
    /// </summary>
    public sealed class ResultadoGuardadoCierre
    {
        public List<string> Guardadas { get; } = new();
        public List<(string Seccion, string Motivo)> Omitidas { get; } = new();
    }

    /// <summary>
    /// Intenta guardar de forma silenciosa (sin diálogos de confirmación) toda
    /// sección que tenga datos válidos. Las secciones con datos incompletos o
    /// inválidos se omiten y se reportan en
    /// <see cref="ResultadoGuardadoCierre.Omitidas"/>.
    /// </summary>
    public ResultadoGuardadoCierre IntentarGuardarTodoSilencioso()
    {
        var resultado = new ResultadoGuardadoCierre();

        TryGuardarSeccion(resultado, "Entrada",  TieneDatosEntrada,  GuardarEntradaSilencioso);
        TryGuardarSeccion(resultado, "Gasto",    TieneDatosGasto,    GuardarGastoSilencioso);
        TryGuardarSeccion(resultado, "Merma",    TieneDatosMerma,    GuardarMermaSilencioso);
        TryGuardarSeccion(resultado, "Cortesía", TieneDatosCortesia, GuardarCortesiaSilencioso);
        TryGuardarSeccion(resultado, "Discos",   TieneDatosDiscos,   GuardarDiscosSilencioso);
        TryGuardarSeccion(resultado, "Cajas",    TieneDatosCajas,    GuardarCajasSilencioso);
        TryGuardarSeccion(resultado, "Inventario Apertura",
            () => TieneDatosConteo(LineasApertura, NotasApertura),
            () => GuardarConteoSilencioso(TipoConteo.Apertura, LineasApertura, NotasApertura,
                limpiar: () => { foreach (var l in LineasApertura) l.Cantidad = 0; NotasApertura = string.Empty; }));
        TryGuardarSeccion(resultado, "Inventario Cierre",
            () => TieneDatosConteo(LineasCierre, NotasCierre),
            () => GuardarConteoSilencioso(TipoConteo.Cierre, LineasCierre, NotasCierre,
                limpiar: () => { foreach (var l in LineasCierre) l.Cantidad = 0; NotasCierre = string.Empty; }));

        return resultado;
    }

    private static void TryGuardarSeccion(
        ResultadoGuardadoCierre resultado, string nombre,
        Func<bool> tieneDatos, Func<(bool ok, string? motivo)> intentar)
    {
        if (!tieneDatos()) return;
        try
        {
            var (ok, motivo) = intentar();
            if (ok) resultado.Guardadas.Add(nombre);
            else resultado.Omitidas.Add((nombre, motivo ?? "datos incompletos"));
        }
        catch (Exception ex)
        {
            App.GuardarErrorDump(ex, $"Guardar {nombre} al cerrar");
            resultado.Omitidas.Add((nombre, ex.Message));
        }
    }
}
