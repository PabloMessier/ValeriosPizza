# AGENTS.md — Convenciones y guía operativa

Este archivo lo leen agentes (Devin, Copilot, etc.) y desarrolladores nuevos
al unirse al proyecto. Describe **cómo trabajamos** sobre esta base de
código: comandos, patrones obligatorios, decisiones tomadas y lo que está
expresamente prohibido.

## Stack

- **.NET 8** WPF (TFM `net8.0-windows10.0.17763.0`)
- **EF Core 8** + **SQLite** (`%LOCALAPPDATA%\ValeriosPizzeria\pizzeria.db`)
- **CommunityToolkit.Mvvm 8.4** (MVVM con source generators)
- **ClosedXML** (Excel) + **QuestPDF Community** (PDF)
- **xUnit** + SQLite in-memory para tests
- **Microsoft.Extensions.Hosting + Logging** (Generic Host como composición raíz)

## Comandos esenciales

```powershell
# Build (Release o Debug)
dotnet build -c Release --nologo

# Tests (49 al día de hoy)
dotnet test tests/ValeriosPizzeria.Tests/ValeriosPizzeria.Tests.csproj --nologo

# Publicar (single-file, x64)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

CI ejecuta `dotnet restore && dotnet build -c Release && dotnet test` en
`windows-latest` (ver `.github/workflows/ci.yml`). **No mezclar warnings**:
el proyecto se mantiene en **0 warnings**; si introducís uno, arreglalo o
suprimílo justificadamente con `[SuppressMessage]`.

## Estructura

```
ValeriosPizza/
├── App.xaml(.cs)              # Composición raíz: Host, DI, logging, single-instance
├── MainWindow.xaml(.cs)       # Shell de navegación
├── Data/PizzeriaDbContext.cs  # DbContext (NoTracking por defecto)
├── Models/                    # Entidades EF (PascalCase, propiedades públicas)
├── Migrations/                # No se usan en runtime; ver "Esquema"
├── ViewModels/                # VMs MVVM
│   ├── <Modulo>ViewModel.cs            # archivo raíz (estado + ctor)
│   └── <Modulo>/<Modulo>ViewModel.X.cs # partials por sección si > ~300 líneas
├── Views/                     # XAML + code-behind mínimo
├── Windows/                   # Diálogos modales
├── Services/                  # Lógica de aplicación, sin estado de UI
│   └── UndoRedo/              # Stack reversible (ICommandUndoable + 10 commands)
├── Controls/                  # UserControls reutilizables (NumericStepper)
├── Converters/                # IValueConverter para XAML
├── Themes/                    # ResourceDictionary por tema
└── tests/ValeriosPizzeria.Tests/
    ├── InMemoryDbFixture.cs   # Helper compartido SQLite in-memory
    └── <Categoria>/*Tests.cs
```

## Patrones obligatorios

### 1. Acceso a base de datos

- **Siempre** vía `IDbContextFactory<PizzeriaDbContext>` inyectado.
  Nunca `new PizzeriaDbContext()` directo en código nuevo.
- Lecturas: `AsNoTracking()` es el **default global** en
  `App.xaml.cs`. No hace falta repetirlo, salvo para sobreescribir con
  `.AsTracking()` cuando vas a mutar y guardar.
- Mutaciones de un registro existente: usá `db.X.Find(id)` (siempre
  trackea, ignora el default) o `.AsTracking()` explícito.
- Async siempre que se pueda: `ToListAsync`, `SaveChangesAsync`,
  `FindAsync`, `await using var db = await _dbFactory.CreateDbContextAsync(ct)`.
- Filtros por fecha en tablas grandes ya tienen índice
  (`Entradas`, `Gastos`, `Mermas`, `Cortesias`, `MercanciasRecibidas`,
  `InventarioDiscos`, `InventarioCajas`, `ConteosInventario`).

### 2. ViewModels (MVVM)

- Heredá de `ViewModelBase` (que es `ObservableValidator`).
- `[ObservableProperty]` con el campo en `_camelCase`; la propiedad
  generada es `PascalCase`.
- Validación declarativa con `[Required]`, `[Range]` +
  `[NotifyDataErrorInfo]`. Para errores en runtime, llamá
  `ValidateAllProperties()` antes de persistir.
- Comandos: `[RelayCommand]`. Para variantes async, declarar como
  `async Task` y CommunityToolkit genera el `IAsyncRelayCommand` correcto.
- **Sin lógica de BD en code-behind de View/Window**. Code-behinds
  deben limitarse a orquestar la ventana (foco inicial, cerrar al
  guardar) y llamar al VM.
- VMs grandes (~300+ líneas): dividir en `partial class` por sección
  bajo `ViewModels/<Modulo>/`. **No cambiar a sub-VMs** salvo que
  reescribas la XAML — los bindings actuales son planos y nested
  paths rompen en silencio. Ver `RegistroRapidoViewModel` y
  `MercanciaNuevaViewModel` como ejemplos.

### 3. Diálogos modales

- Lógica de persistencia y validación vive en
  `<Dialogo>ViewModel.GuardarAsync()` devolviendo un
  `record GuardarResultado(bool Ok, T? Entidad)`.
- El code-behind del diálogo solo llama `await _vm.GuardarAsync()` y,
  si ok, cierra la ventana con `DialogResult = true`.
- Factorías estáticas `ParaCrear(IDbContextFactory)` /
  `ParaEditar(entidad, IDbContextFactory)` para construir el diálogo
  desde un VM padre.

### 4. Undo/Redo

- Toda mutación reversible debe pasar por un comando que herede de
  `UndoableCommandBase` y se ejecute con `_undoRedo.EjecutarAsync(cmd)`.
- El comando captura todo lo necesario para `DeshacerAsync()`. Si el
  delta depende del estado al ejecutar (por ejemplo `Math.Min(Cantidad,
  stockActual)`), guardalo en un campo del comando para reponer exacto.
- Excepciones: rama de **edición** y operaciones con side-effects
  externos (archivos PDF) se mantienen fuera del stack porque la
  reversión sería frágil. Esto está documentado en los comandos.

### 5. Archivos en disco (facturas, exports)

- Carpetas estándar bajo `%LOCALAPPDATA%\ValeriosPizzeria\<subdir>`
  (BD, AutoBackups, Facturas) o `Documentos/ValeriosPizzeria/Reportes`.
- Cuando una persistencia DB + copia de archivo van juntas: copiar
  **primero**, persistir, y si la persistencia falla **borrar el
  archivo copiado** en el catch. Ver `MercanciaNuevaViewModel.Guardar`.

### 6. Mensajería entre VMs

- `WeakReferenceMessenger.Default` para eventos cross-VM
  (`IngredientesChangedMessage`, `ProductosChangedMessage`,
  `BodegaChangedMessage`).
- Suscripción siempre con `WeakReferenceMessenger.Default.Register<T>(this, ...)`
  para que el GC libere automáticamente.

### 7. Logging y errores

- `ILogger<T>` por DI cuando hace falta logging estructurado.
  `Host.CreateDefaultBuilder` ya añade Console/Debug/EventLog providers.
- Excepciones no manejadas se persisten en `error_dump.txt` junto al
  `.exe` (con rotación a 1 MB → `error_dump.1.txt`). Llamar
  `App.GuardarErrorDump(ex, "contexto")` para registrar manualmente.
- Nivel global por defecto: `Information`. EF Core se filtra a
  `Warning` para no inundar la salida.

## Esquema de base de datos

**Importante**: el proyecto **no usa `Database.Migrate()`** en runtime.
La carpeta `Migrations/` está congelada como referencia histórica. El
esquema vivo lo gestiona `DatabaseInitializer`:

1. `EnsureCreated()` para BDs nuevas (crea tablas según el modelo actual).
2. `ActualizarEsquema()` aplica DDL incremental idempotente
   (`ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).

Para añadir una columna/índice/tabla nueva:
- Edita la entidad / `OnModelCreating` en `PizzeriaDbContext`.
- Añade el DDL idempotente en `ActualizarEsquema`.
- **No** generes una migración EF.

## Testing

- Usar `InMemoryDbFixture` (SQLite en memoria con conexión persistente)
  para todo test que toque la BD. Implementa `IDbContextFactory`.
- Patrón estándar: `using var fx = new InMemoryDbFixture();` + sembrar
  datos con `await using var db = fx.CreateDbContext();`.
- Helpers compartidos: `RegistrarEntradaCommandTests.SeedIngredienteAsync(...)`.
- **No** probar flujos que muestran `MessageBox` (requieren UI thread).
  En su lugar, probá los métodos auxiliares y los comandos de undo/redo.
- Nuevo test: ubicar en `tests/ValeriosPizzeria.Tests/<Categoria>/`
  con sufijo `Tests.cs`.

## Lo que NO se hace

- ❌ Crear `PizzeriaDbContext` con `new` fuera de DI o tests.
- ❌ Llamar `SaveChanges()` síncrono en handlers de comandos (usá `SaveChangesAsync`).
- ❌ Hardcodear rutas absolutas. Usá `Environment.SpecialFolder.LocalApplicationData`.
- ❌ Generar migraciones EF (ver "Esquema").
- ❌ Acceder a EF desde code-behind XAML (`.xaml.cs`).
- ❌ Bindings con paths anidados (`{Binding SubVM.Prop}`) — el patrón
  actual son bindings planos sobre `partial class` única.
- ❌ Subir warnings de build sin justificar.
- ❌ Commits con mensajes sin contexto. Ver formato en `README.md` /
  historial de `git log`.

## Verificación antes de cerrar trabajo

1. `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. `dotnet test ... --nologo` → todos los tests pasan.
3. Si tocaste UI: arrancar la app y validar manualmente el flujo
   afectado (no hay tests de UI automatizados).

## Recursos

- **README.md**: descripción funcional para usuarios y desarrolladores.
- **DEVELOPMENT_NOTES.md**: decisiones de arquitectura históricas.
- **.github/workflows/ci.yml**: pipeline CI.
- **error_dump.txt** (en la carpeta del `.exe`): historial de errores
  en runtime; pedir al usuario en soporte.
