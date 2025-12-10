# Архитектура проекта Nivtropy

## 📁 Структура проекта

```
Nivtropy/
├── Services/                      # Сервисный слой (бизнес-логика)
│   ├── Visualization/            # Сервисы визуализации
│   │   ├── IProfileVisualizationService.cs
│   │   ├── ProfileVisualizationService.cs
│   │   ├── ITraverseSystemVisualizationService.cs
│   │   └── TraverseSystemVisualizationService.cs
│   ├── Statistics/               # Сервисы статистики и анализа
│   │   ├── IProfileStatisticsService.cs
│   │   └── ProfileStatisticsService.cs
│   ├── Export/                   # Сервисы экспорта данных
│   │   ├── IExportService.cs
│   │   └── TraverseExportService.cs
│   ├── Tolerance/                # Сервисы работы с допусками
│   │   ├── IToleranceService.cs
│   │   └── ToleranceService.cs
│   ├── IDataParser.cs           # Парсинг файлов данных
│   ├── DatParser.cs
│   ├── ITraverseBuilder.cs      # Построение структуры ходов
│   ├── TraverseBuilder.cs
│   └── ServiceCollectionExtensions.cs  # Регистрация DI
│
├── ViewModel/                    # Слой представления (MVVM)
│   ├── MainViewModel.cs
│   ├── DataViewModel.cs
│   ├── TraverseCalculationViewModel.cs
│   ├── TraverseJournalViewModel.cs
│   ├── TraverseDesignViewModel.cs
│   ├── DataGeneratorViewModel.cs
│   └── SettingsViewModel.cs
│
├── Models/                       # Модели данных
│   ├── MeasurementRecord.cs
│   ├── TraverseRow.cs
│   ├── LineSummary.cs
│   ├── ProfileStatistics.cs
│   ├── OutlierPoint.cs
│   └── ...
│
├── View/                         # XAML представления
│   ├── TraverseJournalView.xaml
│   ├── TraverseDesignView.xaml
│   └── ...
│
├── Converters/                   # Value Converters для XAML
└── Resources/                    # Ресурсы приложения
```

---

## 🏗️ Архитектурные принципы

### MVVM (Model-View-ViewModel)

Приложение следует паттерну MVVM:

- **Model**: Чистые модели данных без бизнес-логики (Models/)
- **View**: XAML интерфейсы без кода (View/)
- **ViewModel**: Логика представления, команды, биндинги (ViewModel/)

### SOLID принципы

#### 1️⃣ **Single Responsibility Principle (SRP)**

Каждый класс имеет одну ответственность:

- `ProfileVisualizationService` - только визуализация профилей
- `ProfileStatisticsService` - только вычисление статистики
- `TraverseExportService` - только экспорт данных
- `ToleranceService` - только работа с допусками

#### 2️⃣ **Open/Closed Principle (OCP)**

Сервисы расширяемы через интерфейсы:

```csharp
// Можно легко добавить новую реализацию визуализации
public class Enhanced3DVisualizationService : IProfileVisualizationService
{
    // Новая реализация
}
```

#### 3️⃣ **Liskov Substitution Principle (LSP)**

Любая реализация интерфейса взаимозаменяема:

```csharp
IProfileVisualizationService service = new ProfileVisualizationService();
// или
IProfileVisualizationService service = new AlternativeVisualizationService();
```

#### 4️⃣ **Interface Segregation Principle (ISP)**

Интерфейсы сфокусированы на конкретных задачах:

- `IProfileVisualizationService` - только визуализация
- `IProfileStatisticsService` - только статистика
- Не смешиваем несвязанные методы

#### 5️⃣ **Dependency Inversion Principle (DIP)**

Зависимости через абстракции:

```csharp
public class TraverseJournalViewModel
{
    private readonly IProfileVisualizationService _visualizationService;

    // Зависимость от интерфейса, а не от конкретной реализации
    public TraverseJournalViewModel(IProfileVisualizationService visualizationService)
    {
        _visualizationService = visualizationService;
    }
}
```

---

## 🔧 Dependency Injection

### Регистрация сервисов

В `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    // Сервисы парсинга и построения данных
    services.AddSingleton<IDataParser, DatParser>();
    services.AddSingleton<ITraverseBuilder, TraverseBuilder>();

    // Сервисы визуализации
    services.AddSingleton<IProfileVisualizationService, ProfileVisualizationService>();
    services.AddSingleton<ITraverseSystemVisualizationService, TraverseSystemVisualizationService>();

    // Сервисы статистики и анализа
    services.AddSingleton<IProfileStatisticsService, ProfileStatisticsService>();

    // Сервисы экспорта
    services.AddSingleton<IExportService, TraverseExportService>();

    // Сервисы работы с допусками
    services.AddSingleton<IToleranceService, ToleranceService>();

    return services;
}
```

### Инициализация в App.xaml.cs

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var services = new ServiceCollection();
    services.AddApplicationServices();
    services.AddViewModels();

    _serviceProvider = services.BuildServiceProvider();
    Services = _serviceProvider;
}
```

### Использование в MainWindow

```csharp
public MainWindow()
{
    InitializeComponent();
    DataContext = App.Services.GetRequiredService<MainViewModel>();
}
```

---

## 📊 Сервисы

### Визуализация

#### IProfileVisualizationService

Отвечает за рендеринг профилей нивелирного хода:

- Рисование графика профиля
- Отображение сетки координат
- Визуализация точек и маркеров
- Отображение аномалий

**Использование:**

```csharp
var options = new ProfileVisualizationOptions
{
    ShowZ = true,
    ShowZ0 = true,
    ShowAnomalies = true,
    SensitivitySigma = 2.5
};

_visualizationService.DrawProfile(canvas, rows, options, statistics, knownPoints);
```

#### ITraverseSystemVisualizationService

Визуализация графа связей между ходами:

- Построение графа системы ходов
- Разрешение коллизий при отрисовке
- Smooth geometry для красивых кривых

### Статистика

#### IProfileStatisticsService

Вычисляет статистику и обнаруживает аномалии:

```csharp
var statistics = _statisticsService.CalculateStatistics(rows, sensitivitySigma: 2.5);

// Доступ к результатам
Console.WriteLine($"Станций: {statistics.StationCount}");
Console.WriteLine($"Аномалий: {statistics.TotalOutliers}");
```

Типы аномалий:
- **HeightJump** - резкие перепады высот
- **StationLength** - аномальные длины станций
- **ArmDifference** - превышение разности плеч

### Экспорт

#### IExportService

Экспорт данных в различные форматы:

```csharp
// Интерактивный экспорт с диалогом
_exportService.ExportToCsv(rows);

// Экспорт в конкретный файл
_exportService.ExportToCsv(rows, "data.csv");
```

### Допуски

#### IToleranceService

Работа с допусками нивелирования:

```csharp
var tolerance = _toleranceService.CalculateTolerance(
    option: selectedClass,
    stationsCount: 25,
    totalLengthKm: 2.5);

var isOk = _toleranceService.IsWithinTolerance(closure, tolerance);

var verdict = _toleranceService.BuildToleranceVerdict(
    closure, methodTolerance, classTolerance,
    selectedMethod, selectedClass);
```

---

## 🧪 Тестируемость

Благодаря DI и интерфейсам, все компоненты легко тестируются:

```csharp
[Test]
public void TestStatisticsCalculation()
{
    // Arrange
    var service = new ProfileStatisticsService();
    var testRows = CreateTestData();

    // Act
    var statistics = service.CalculateStatistics(testRows, 2.5);

    // Assert
    Assert.That(statistics.StationCount, Is.EqualTo(10));
    Assert.That(statistics.HasOutliers, Is.False);
}
```

### Mock сервисы для тестирования ViewModels:

```csharp
[Test]
public void TestViewModelWithMockService()
{
    // Arrange
    var mockVisualization = new Mock<IProfileVisualizationService>();
    var viewModel = new TraverseJournalViewModel(mockVisualization.Object);

    // Act & Assert
    // ...
}
```

---

## 📈 Преимущества новой архитектуры

### ✅ Разделение ответственности

- Code-behind файлы минимальны (~50 строк вместо 1913)
- Логика вынесена в специализированные сервисы
- ViewModels фокусируются на биндингах, а не на бизнес-логике

### ✅ Переиспользование кода

Сервисы можно использовать в разных частях приложения:

```csharp
// В одном месте
_exportService.ExportToCsv(journalRows);

// В другом месте
_exportService.ExportToCsv(designRows);
```

### ✅ Простота замены реализации

Легко заменить сервис на альтернативную реализацию:

```csharp
// Было
services.AddSingleton<IExportService, TraverseExportService>();

// Стало (новая реализация с Excel)
services.AddSingleton<IExportService, ExcelExportService>();
```

### ✅ Unit-тестирование

Каждый сервис можно тестировать изолированно:

- Не нужно поднимать UI для тестов
- Легко создавать mock-объекты
- Быстрые и надёжные тесты

---

## 🔄 Миграция с устаревшего кода

### Устаревшие методы

Некоторые методы помечены как `[Obsolete]` для обратной совместимости:

```csharp
[Obsolete("Используйте инстансный метод Build через Dependency Injection")]
public static List<TraverseRow> BuildStatic(...)
```

**Рекомендация**: Постепенно мигрировать на использование через DI.

---

## 🚀 Дальнейшее развитие

### Следующие шаги:

1. ✅ Рефакторинг code-behind файлов:
   - TraverseJournalView.xaml.cs (1913 → 50 строк)
   - SystemsManagementWindow.xaml.cs
   - AboutWindow.xaml.cs

2. 📝 Добавить unit-тесты для сервисов

3. 🎨 Создать Attached Behaviors для повторно используемой UI логики

4. 🔌 Добавить новые форматы экспорта (Excel, JSON)

5. 📊 Расширить статистический анализ (тренды, прогнозы)

---

## 📚 Дополнительные ресурсы

- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/modernize-desktop/example-migration-core)
- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [SOLID Principles](https://www.digitalocean.com/community/conceptual-articles/s-o-l-i-d-the-first-five-principles-of-object-oriented-design)

---

**Дата последнего обновления**: 2025-12-10
**Версия архитектуры**: 2.0
