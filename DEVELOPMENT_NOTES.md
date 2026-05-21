# Notas de Desarrollo - Valerio's Pizza

> **Lectura recomendada antes:** [`AGENTS.md`](./AGENTS.md) tiene las
> convenciones vigentes (DI, MVVM, testing). Este archivo es el **historial
> arquitectónico** del proyecto: por qué se tomaron ciertas decisiones, qué
> refactorings se ejecutaron y cuál es la deuda técnica conocida. Si solo
> querés contribuir al código, AGENTS.md es suficiente.

## Estado Actual del Proyecto ✅

### Refactor arquitectónico (mayo 2026 — olas 0–5)
- **Generic Host + DI** en `App.xaml.cs`: el contenedor expone
  `IDbContextFactory<PizzeriaDbContext>`, `DatabaseInitializer` y todos los VMs.
  Cada llamador crea su propio `PizzeriaDbContext` y lo dispone, evitando que
  varias pantallas compartan un único context.
- **`DatabaseInitializer`** (nuevo servicio en `Services/DatabaseInitializer.cs`)
  ejecuta primero un *backup automático rotativo* (últimos 7 archivos en
  `%LOCALAPPDATA%\ValeriosPizzeria\AutoBackups`) y luego el bootstrap heredado
  de esquema (`EnsureCreated` + columnas/tablas faltantes). El backup automatico
  es la red de seguridad mientras la siguiente ola sustituye este flujo por
  migraciones EF reales.
- **Rangos de fecha medio-abiertos** (`>= inicio && < finExclusivo`) en
  `ConsultaViewModel`, `ReportesViewModel` y `ExportService` — evita el patrón
  frágil `<= AddSeconds(-1)`.
- **Borrado masivo eficiente**: `DatabaseWipeService` usa `ExecuteDelete`
  dentro de una transacción; ya no carga las filas en memoria.
- **`WeakReferenceMessenger`** reemplaza el bus estático
  `IngredientesNotifier`. La clase legacy se conserva como adaptador.
- **`Producto.Activo`** se deriva de `Estado` (sin estado duplicado).
- **Tema**: nuevos brushes `TodayHighlightBrush` / `OtherDayBackgroundBrush`
  para que el resaltado del día actual ya no sea un hex string fijo en el VM.

### Próxima ola — migraciones EF reales (Wave 4 follow-up)
La ola 4 dejó todo listo para reemplazar el par
`EnsureCreated()` + `ActualizarEsquema()` por migraciones EF reales:
1. Asegurar que la BD activa está respaldada (el initializer lo hace al inicio).
2. Generar la migración base que coincide con el esquema actual:
   ```powershell
   dotnet ef migrations add BaselineSchema --context PizzeriaDbContext
   ```
3. Para BDs legacy creadas con `EnsureCreated()` (no tienen tabla
   `__EFMigrationsHistory`), insertar manualmente la fila correspondiente al
   `MigrationId` recién generado para que `Migrate()` no intente recrear las
   tablas existentes:
   ```sql
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('YYYYMMDDHHMMSS_BaselineSchema', '8.0.11');
   ```
4. Sustituir la llamada a `EnsureCreated()` + `ActualizarEsquema()` en
   `DatabaseInitializer.BootstrapEsquema` por `db.Database.Migrate();`.
5. Eliminar las tres migraciones obsoletas en `Migrations/` (no representan el
   esquema actual) y dejar solo la baseline + las migraciones futuras.
El plan en código se documenta en `DatabaseInitializer.BootstrapEsquema`.

### Mejoras agregadas en mayo 2026
- Nueva categoría **Cajas Para Pizzas** con tracking diario por tamaño (Pequeña/Mediana/Grande), modelada como `InventarioCaja`.
- **Crear/Editar/Desactivar ingredientes** desde la UI mediante el nuevo `IngredienteDialog`. El diálogo es invocable desde la pestaña INGREDIENTES de Inventario y desde los formularios de Apertura y Cierre (botón `+ Nuevo Ingrediente` inline).
- Flag `Activo` en `Ingrediente` para preservar historial al "borrar" un ingrediente.
- **Inventario Apertura** e **Inventario Cierre**: dos formularios manuales, independientes, que registran lo que físicamente hay antes y después del servicio. Cada uno se guarda como `ConteoInventario` + `ConteoInventarioLinea`.
- Exportación adicional a **Excel (.xlsx)** y **PDF**, además del CSV existente. Servicio centralizado `ExportService`.
- Bus simple `IngredientesNotifier` para sincronizar listas de ingredientes entre ViewModels sin recargar la app.
- Esquema de la BD se actualiza automaticamente al iniciar (añade columna `Activo` si falta y crea las tablas nuevas).

### ✅ Completado
- [x] Estructura base del proyecto WPF
- [x] Configuración de base de datos SQLite con Entity Framework Core
- [x] Modelos de datos para todas las entidades
- [x] Sistema de navegación MVVM
- [x] Dashboard con vista resumida
- [x] Registro de entradas (ingredientes)
- [x] Vista de inventario completo
- [x] Datos de muestra para pruebas
- [x] Interfaz visual atractiva y funcional

### 🚧 En Desarrollo / Pendiente
- [ ] Validación robusta (`INotifyDataErrorInfo`) en todos los formularios de registro
- [ ] Migraciones EF reales reemplazando `EnsureCreated()` + `ActualizarEsquema()` (instrucciones más arriba).
- [ ] EF async en VMs y vistas para evitar bloqueos del hilo de UI
- [ ] División de las XAML grandes (Registro Rápido, Reportes) en UserControls por sección

> Los formularios de Gastos/Mermas/Cortesías y el módulo de Reportes ya están implementados y operativos.

## Funcionalidades Pendientes por Prioridad

### Alta Prioridad (Próxima Iteración)
1. **Completar formularios de registro**
   - Gastos: concepto, monto, categoría, fecha
   - Mermas: ingrediente/producto afectado, cantidad, motivo
   - Cortesías: producto, cantidad, motivo

2. **Reportes básicos**
   - Resumen diario (entradas, gastos, mermas, cortesías)
   - Reporte semanal consolidado
   - Exportar a Excel/PDF

3. **Actualización de inventario**
   - Que las entradas actualicen el stock automáticamente
   - Que las mermas reduzcan el stock
   - Alertas cuando un ingrediente llegue al mínimo

### Media Prioridad
4. **Mejoras de UX**
   - Confirmaciones visuales más claras
   - Validaciones de formularios mejoradas
   - Shortcuts de teclado para navegación rápida
   - Modo de vista rápida para registrar múltiples items

5. **Gestión de productos**
   - CRUD completo para productos
   - Asociar ingredientes con productos (recetas)
   - Calcular costos de producción

### Baja Prioridad
6. **Funcionalidades avanzadas**
   - Historial de cambios
   - Búsqueda avanzada
   - Gráficos y estadísticas
   - Sistema de backup automático

## Próximos Pasos Inmediatos

1. Probar la aplicación con la dueña
2. Recoger feedback sobre:
   - ¿Es fácil de usar?
   - ¿Falta algo crítico?
   - ¿Algo sobra o confunde?
   - ¿Los colores/diseño son apropiados?

3. Ajustar según feedback

## Consideraciones Técnicas

### Base de Datos
- Ubicación: `%LocalAppData%\ValeriosPizzeria\pizzeria.db`
- Migraciones automáticas al iniciar
- Datos de muestra solo se insertan si la BD está vacía

### Performance
- La app es ligera, carga rápido
- Queries optimizados con EF Core
- Sin dependencia de internet

### Mantenimiento
- Para agregar nuevos ingredientes/productos: usar la vista de inventario (cuando se implemente el CRUD)
- Para limpiar la BD: eliminar el archivo .db y reiniciar la app

## Notas de Reuniones con el Cliente

### Reunión Inicial
- 2 personas trabajando
- Actualmente usan Puvesoft pero es muy lento
- Necesitan rapidez sobre todo
- Categorías principales: Pizzas y Paninis
- Registros diarios importantes: entradas, gastos, mermas, cortesías

### Próxima Reunión
- [ ] Mostrar prototipo funcional
- [ ] Validar flujo de trabajo
- [ ] Confirmar necesidades de reportes
- [ ] Discutir posibles integraciones futuras

## Comandos Útiles

### Compilar
```powershell
dotnet build
```

### Ejecutar
```powershell
dotnet run
```

### Crear nueva migración
```powershell
dotnet ef migrations add NombreMigracion
```

### Aplicar migraciones
```powershell
dotnet ef database update
```

### Eliminar BD y empezar de cero
```powershell
Remove-Item "$env:LOCALAPPDATA\ValeriosPizzeria\pizzeria.db"
dotnet run
```

### Ejecutar las pruebas
Las pruebas viven en `tests/ValeriosPizzeria.Tests/` (xUnit). Cubren la
lógica pura: cálculo de rangos en `DatabaseWipeService`, derivación de
`Producto.Activo` desde `Estado`, balance de ingredientes en `ResumenIngrediente`,
rangos medio-abiertos de fechas y `ObservableCollectionExtensions.ReplaceAll`.
```powershell
dotnet test tests/ValeriosPizzeria.Tests -c Release
```

## Ideas para Futuras Versiones

- [ ] Integración con sistema de punto de venta
- [ ] App móvil complementaria (Xamarin/MAUI)
- [ ] Sincronización en la nube (opcional)
- [ ] Multi-usuario con roles
- [ ] Gestión de proveedores
- [ ] Órdenes de compra automáticas
- [ ] Análisis predictivo de stock

---
**Última actualización**: Enero 16, 2026
