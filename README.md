# Valerio's Pizza - Sistema de Inventario

Sistema de gestión de inventario desarrollado específicamente para Valerio's Pizza.

> **Para desarrolladores y agentes IA:** las convenciones de código, patrones
> obligatorios y comandos esenciales viven en [`AGENTS.md`](./AGENTS.md).
> Este README documenta la **funcionalidad** del sistema (módulos,
> almacenamiento, diseño visual). El historial de decisiones arquitectónicas
> está en [`DEVELOPMENT_NOTES.md`](./DEVELOPMENT_NOTES.md).

## 🎯 Objetivo

Crear una solución compacta, formal y minimalista que permita gestionar el inventario diario de manera rápida, minimizando el tiempo que la dueña pasa en el software para que pueda atender más clientes.

## ✨ Módulos Implementados

### 1. Dashboard
- Vista rápida de las operaciones del día actual
- Tarjetas con contadores en tiempo real:
  - **Entradas** (verde): Ingredientes agregados al inventario
  - **Gastos** (azul): Ingredientes consumidos en operación
  - **Mermas** (naranja): Pérdidas por desperdicio, daño, expiración
  - **Cortesías** (morado): Productos regalados a clientes
- Alertas de stock bajo (ingredientes bajo mínimo)
- Actividad reciente con los últimos movimientos

### 2. Registro Rápido
Interfaz de página única con acceso directo a todos los tipos de registro:

- **Entradas**: Registro de ingredientes que ingresan al inventario
  - Selección de ingrediente
  - Cantidad con unidad de medida
  - Notas opcionales
  
- **Gastos**: Control de ingredientes utilizados en operación
  - Selección de ingrediente
  - Cantidad consumida
  - Notas opcionales

- **Mermas**: Registro de pérdidas
  - Selección de ingrediente
  - Cantidad perdida
  - Motivo (Expirado, Dañado, Quemado, Otro)
  - Notas opcionales

- **Cortesías**: Seguimiento de productos regalados
  - Selección de producto (Pizza/Panini)
  - Cantidad entregada
  - Motivo (Queja cliente, Promoción, Empleado, Otro)
  - Notas opcionales

- **Discos (Bases de Pizza)**: Control de producción de bases
  - Cantidad inicial del día
  - Discos preparados
  - Discos utilizados
  - Merma de discos
  - Cortesía de discos
  - Cálculo automático de disponibilidad

- **Cajas Para Pizzas**: Control diario por tamaño (Pequeña, Mediana, Grande)
  - Cantidad inicial, Recibidas, Utilizadas, Merma
  - Disponibles calculadas automáticamente por tamaño

- **Inventario Apertura**: Conteo físico antes de iniciar el servicio
  - DataGrid con todos los ingredientes activos
  - Campo de cantidad editable manual por ingrediente
  - Botón `+ Nuevo Ingrediente` para crear ingredientes al vuelo

- **Inventario Cierre**: Conteo físico al finalizar el servicio
  - Mismo flujo manual que Apertura, independiente al guardar
  - Botón `+ Nuevo Ingrediente` para crear ingredientes al vuelo

### 3. Mercancía Nueva
Módulo dedicado para recibir mercancía de proveedores:
- Selección de ingrediente
- Cantidad recibida
- Nombre del proveedor
- Notas opcionales
- Historial de recepciones recientes

### 4. Inventario
Vista completa del estado actual del inventario:

**Panel de Estado Actual:**
- Tarjetas con el balance del día (Entradas, Gastos, Mermas, Cortesías)
- Estado de discos de pizza disponibles

**Filtros por Período:**
- Hoy
- Esta Semana
- Este Mes

**Historial de Movimientos:**
- Listado completo de todos los registros
- Incluye mercancía recibida
- Tipo de movimiento con código de color
- Fecha, ingrediente/producto, cantidad y notas

**Tablas de Referencia:**
- Lista de ingredientes con stock actual, unidad, stock mínimo y última actualización
- Botón **+ Agregar Ingrediente** y acciones por fila para Editar / Desactivar
- Lista de productos (Pizzas y Paninis)

**Estado del día:**
- Indica si el Inventario Apertura y/o Cierre están registrados (con la hora)

### 5. Consulta
Módulo de búsqueda avanzada con filtros:

**Filtros de Fecha:**
- Rango personalizado con selector de fechas
- Botones rápidos: Hoy, Semana, Mes, Todo

**Filtros por Tipo de Registro:**
- Checkbox para cada tipo: Entradas, Gastos, Mermas, Cortesías, Mercancía, Cajas, Apertura, Cierre
- Contador de resultados

**Resultados:**
- Tabla con todos los registros que coinciden
- Columnas: Fecha, Tipo, Ingrediente/Producto, Cantidad, Motivo/Proveedor

### 6. Reportes
Módulo completo de análisis y estadísticas:

**Filtros de Período:**
- Últimos 7 días
- Últimos 30 días
- Este mes
- Mes anterior
- Rango de fechas personalizado

**Tarjetas de Totales:**
- Total de entradas, gastos, mermas, cortesías, mercancía nueva
- Total general de movimientos

**Estadísticas de Discos:**
- Discos preparados en el período
- Discos utilizados
- Discos perdidos (merma)
- Discos de cortesía

**Actividad Diaria:**
- Visualización de los últimos 14 días
- Desglose por tipo de movimiento

**Tablas de Análisis:**
- Movimientos por ingrediente con balance (entradas + mercancía - gastos - mermas)
- Cortesías por producto

**Exportación:**
- Exportar a **CSV**, **Excel (.xlsx)** o **PDF** con todos los datos del período
- Se guarda en Documentos/ValeriosPizzeria/Reportes/
- Abre automáticamente la carpeta al exportar
- Excel: una hoja por tipo de movimiento más una hoja Resumen
- PDF: documento con totales y tabla por tipo de movimiento

Librerías usadas: ClosedXML para Excel, QuestPDF (licencia Community) para PDF.

## 🛠️ Tecnologías

- **Framework**: .NET 8 / WPF
- **Plataforma**: Windows 10+
- **Base de Datos**: SQLite (local, no requiere internet ni servidor)
- **Patrón**: MVVM con CommunityToolkit.Mvvm 8.4.0
- **ORM**: Entity Framework Core 8.0.11
- **Excel**: ClosedXML 0.104.x
- **PDF**: QuestPDF 2024.x (Community)

## 📦 Estructura del Proyecto

```
ValeriosPizzeria/
├── Data/
│   ├── PizzeriaDbContext.cs       # Contexto de base de datos
│   └── Migrations/                 # Migraciones de EF Core
├── Models/
│   ├── Categoria.cs                # Enum: Pizza, Panini
│   ├── Ingrediente.cs              # Ingredientes del inventario
│   ├── Producto.cs                 # Pizzas y Paninis
│   ├── Entrada.cs                  # Registros de entrada
│   ├── Gasto.cs                    # Registros de gastos/consumo
│   ├── Merma.cs                    # Registros de pérdidas
│   ├── Cortesia.cs                 # Registros de cortesías
│   ├── InventarioDisco.cs          # Control diario de discos
│   ├── RegistroDiario.cs           # Resumen diario consolidado
│   └── MercanciaRecibida.cs        # Recepciones de proveedores
├── ViewModels/
│   ├── ViewModelBase.cs            # Clase base para ViewModels
│   ├── MainWindowViewModel.cs      # Controlador principal de navegación
│   ├── DashboardViewModel.cs       # Lógica del panel principal
│   ├── RegistroRapidoViewModel.cs  # Lógica de registro rápido
│   ├── MercanciaNuevaViewModel.cs  # Lógica de mercancía nueva
│   ├── InventarioViewModel.cs      # Lógica de inventario y tracking
│   ├── ConsultaViewModel.cs        # Lógica de búsqueda avanzada
│   └── ReportesViewModel.cs        # Lógica de reportes y exportación
├── Views/
│   ├── DashboardView.xaml          # Vista del dashboard
│   ├── RegistroRapidoView.xaml     # Vista de registro rápido
│   ├── MercanciaNuevaView.xaml     # Vista de mercancía nueva
│   ├── InventarioView.xaml         # Vista de inventario
│   ├── ConsultaView.xaml           # Vista de consulta avanzada
│   └── ReportesView.xaml           # Vista de reportes
├── Services/
│   └── DatabaseSeeder.cs           # Generador de datos de muestra
├── App.xaml                        # Recursos y estilos globales
├── App.xaml.cs                     # Configuración e inicialización
└── MainWindow.xaml                 # Ventana principal con navegación
```

## 🎨 Diseño de Interfaz

### Estilo Visual
- **Diseño**: Formal, minimalista y profesional
- **Tipografía**: Segoe UI, tamaños optimizados para legibilidad
  - Títulos: 26px
  - Botones navegación: 15px
  - Contenido general: 14px
  - Etiquetas secundarias: 12-13px

### Panel de Navegación (Izquierda)
- Fondo oscuro (#1A1A1A)
- Logo circular de 180x180 píxeles
- 6 botones de navegación principales
- ScrollViewer para pantallas pequeñas

### Área de Trabajo (Derecha)
- Fondo claro (#FAFAFA)
- Tarjetas con bordes sutiles (CardStyle)
- ScrollViewer para contenido dinámico

### Código de Colores por Tipo
| Tipo | Color | Código |
|------|-------|--------|
| Entradas | Verde | #4CAF50 |
| Gastos | Azul | #2196F3 |
| Mermas | Naranja | #FF9800 |
| Cortesías | Morado | #9C27B0 |
| Mercancía | Cyan | #00BCD4 |
| Discos | Gris | #607D8B |

## 💾 Almacenamiento de Datos

### Base de Datos
```
%LOCALAPPDATA%\ValeriosPizzeria\pizzeria.db
```

### Logs de Errores
```
%LOCALAPPDATA%\ValeriosPizzeria\ErrorLogs\
```

### Reportes Exportados
```
%USERPROFILE%\Documents\ValeriosPizzeria\Reportes\
```

## 🔧 Sistema de Manejo de Errores

La aplicación cuenta con un sistema robusto de captura de errores:

- **Errores de UI**: Capturados sin cerrar la aplicación
- **Errores de hilos secundarios**: Registrados automáticamente
- **Errores de tareas asíncronas**: Observados y registrados
- **Dump de errores**: Archivo detallado con:
  - Fecha y hora
  - Contexto del error
  - Versión del sistema operativo
  - Versión de .NET
  - Stack trace completo
  - Excepciones internas anidadas

## 🚀 Ejecución

### Requisitos
- Windows 10 o superior
- .NET 8.0 Runtime

### Compilar y ejecutar
```powershell
cd ValeriosPizzeria
dotnet build
dotnet run
```

### Publicar para distribución
```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## 📋 Datos de Muestra

La aplicación incluye datos de muestra para facilitar las pruebas:

**Ingredientes (10):**
- Harina, Queso Mozzarella, Salsa de Tomate, Pepperoni, Jamón
- Champiñones, Pimientos, Aceitunas, Pan Panini, Lechuga

**Productos - Pizzas (5):**
- Margarita, Pepperoni, Hawaiana, Vegetariana, Suprema

**Productos - Paninis (4):**
- Italiano, Pollo, Vegetariano, Jamón y Queso

## 💡 Filosofía de Diseño

> **"Entre menos tiempo pase en el software, más clientes puede atender"**

Principios aplicados:
- ✅ Una sola ventana con navegación rápida
- ✅ Formularios simples y directos
- ✅ Sin menús profundos ni pestañas complejas
- ✅ Acceso inmediato a todas las funciones
- ✅ Feedback visual claro con colores
- ✅ Scroll para adaptarse a cualquier resolución
- ✅ Datos locales sin dependencia de internet

## 📊 Funcionalidades por Módulo

| Módulo | Estado | Descripción |
|--------|--------|-------------|
| Dashboard | ✅ Completo | Resumen del día con alertas |
| Registro Rápido | ✅ Completo | Entradas, Gastos, Mermas, Cortesías, Discos |
| Mercancía Nueva | ✅ Completo | Recepción de proveedores |
| Inventario | ✅ Completo | Estado actual y tracking por período |
| Consulta | ✅ Completo | Búsqueda avanzada con filtros |
| Reportes | ✅ Completo | Estadísticas y exportación CSV |

## 📄 Licencia

Proyecto privado desarrollado para Valerio's Pizza.

---

**Versión**: 2.0.0  
**Última actualización**: Enero 2026
