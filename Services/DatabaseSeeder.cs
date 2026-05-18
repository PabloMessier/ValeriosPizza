namespace ValeriosPizza.Services;

/// <summary>
/// Marcador histórico: en versiones anteriores este servicio sembraba datos
/// de muestra y proporcionaba un "reset" total. Esa funcionalidad fue
/// reemplazada por <see cref="DatabaseWipeService"/> y por el flujo de
/// inicialización de la BD en <c>App.xaml.cs</c>. La clase se conserva como
/// estática vacía para mantener una potencial compatibilidad binaria con
/// código externo que la referenciara; el archivo se eliminará por completo
/// en una ola posterior junto con la migración a inyección de dependencias.
/// </summary>
public static class DatabaseSeeder
{
}
