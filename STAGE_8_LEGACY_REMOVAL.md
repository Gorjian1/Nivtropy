# Этап 8: Удаление Legacy кода

## Обзор текущего состояния

После выполнения этапов 1-7 в проекте сосуществуют две архитектуры:

### Новая архитектура (Domain-Driven Design)
```
Domain/
├── Model/
│   ├── Point.cs              # Узел графа
│   ├── Observation.cs        # Ребро графа (станция)
│   ├── Run.cs                # Агрегат (ход)
│   ├── LevelingNetwork.cs    # Главный агрегат (сеть)
│   └── TraverseSystem.cs     # Система ходов
├── ValueObjects/
│   ├── PointCode.cs
│   ├── Height.cs
│   ├── Distance.cs
│   ├── Reading.cs
│   └── Closure.cs
└── Services/
    ├── IHeightPropagator.cs
    ├── HeightPropagator.cs
    ├── IClosureDistributor.cs
    └── ProportionalClosureDistributor.cs

Application/
├── Commands/
│   ├── CalculateHeightsCommand.cs
│   └── Handlers/
├── Queries/
│   └── GetNetworkSummaryQuery.cs
├── DTOs/
│   └── NetworkDtos.cs
└── Mappers/
    └── NetworkMapper.cs

Infrastructure/
└── Persistence/
    └── INetworkRepository.cs

Legacy/
└── Adapters/                 # Мост между старым и новым
    ├── TraverseRowAdapter.cs
    ├── LineSummaryAdapter.cs
    └── NetworkAdapter.cs
```

### Старая архитектура (Legacy)
```
Models/                       # ← УДАЛИТЬ (частично)
├── TraverseRow.cs           # → заменён на Observation
├── LineSummary.cs           # → заменён на Run
├── MeasurementRecord.cs     # → оставить (парсинг)
├── JournalRow.cs            # → оставить (UI)
├── DesignRow.cs             # → оставить (UI)
├── ProfileStatistics.cs     # → перенести в Domain или оставить
├── OutlierPoint.cs          # → перенести в Domain
├── PointItem.cs             # → заменён на Point
├── TraverseSystem.cs        # → заменён на Domain/Model/TraverseSystem
├── SharedPointLinkItem.cs   # → не нужен (граф сам определяет)
├── GeneratedMeasurement.cs  # → оставить (экспорт)
└── RowColoringMode.cs       # → оставить (UI enum)

Services/                     # ← УДАЛИТЬ (частично)
├── Calculation/
│   ├── TraverseCalculationService.cs    # → заменён на HeightPropagator
│   ├── TraverseCorrectionService.cs     # → заменён на ClosureDistributor
│   └── SystemConnectivityService.cs     # → метод в LevelingNetwork
├── TraverseBuilder.cs                   # → перенести в Infrastructure
├── DatParser.cs                         # → перенести в Infrastructure
├── Parsers/                             # → перенести в Infrastructure
├── Statistics/                          # → перенести в Application
├── Visualization/                       # → оставить (UI-специфичный)
├── Tolerance/                           # → перенести в Domain
├── Export/                              # → перенести в Infrastructure
├── Validation/                          # → перенести в Application
├── Dialog/                              # → оставить (UI)
├── IO/                                  # → оставить (инфраструктура)
└── Logging/                             # → оставить (инфраструктура)
```

---

## План удаления Legacy кода

### Фаза 8.1: Перенос нужных сервисов

Некоторые сервисы не имеют замены в новой архитектуре - их нужно перенести, а не удалить.

#### 8.1.1 Перенос парсеров в Infrastructure

**Файлы для переноса:**
```
Services/DatParser.cs           → Infrastructure/Parsers/DatParser.cs
Services/IDataParser.cs         → Infrastructure/Parsers/IDataParser.cs
Services/Parsers/               → Infrastructure/Parsers/
```

**Изменения:**
```csharp
// Было
namespace Nivtropy.Services;

// Стало
namespace Nivtropy.Infrastructure.Parsers;
```

**Новый интерфейс (адаптировать к Domain):**
```csharp
namespace Nivtropy.Infrastructure.Parsers;

using Nivtropy.Domain.Model;

public interface INetworkParser
{
    /// <summary>
    /// Парсит файл и строит LevelingNetwork
    /// </summary>
    Task<LevelingNetwork> ParseAsync(string filePath);

    /// <summary>
    /// Парсит файл в сырые записи (для совместимости)
    /// </summary>
    Task<IReadOnlyList<RawMeasurement>> ParseRawAsync(string filePath);
}

/// <summary>
/// Сырое измерение из файла (замена MeasurementRecord)
/// </summary>
public record RawMeasurement(
    int Sequence,
    string Mode,
    string Target,
    string StationCode,
    string? LineMarker,
    string? OriginalLineNumber,
    double BackReading,
    double ForeReading,
    double BackDistance,
    double ForeDistance,
    double? AbsoluteHeight
);
```

#### 8.1.2 Перенос ToleranceService в Domain

**Файлы для переноса:**
```
Services/Tolerance/IToleranceService.cs  → Domain/Services/IToleranceCalculator.cs
Services/Tolerance/ToleranceService.cs   → Domain/Services/ToleranceCalculator.cs
```

**Адаптация интерфейса:**
```csharp
namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

public interface IToleranceCalculator
{
    /// <summary>
    /// Вычислить допустимую невязку для хода
    /// </summary>
    double CalculateToleranceMm(Run run, LevelingClass levelingClass);

    /// <summary>
    /// Вычислить допуск по количеству станций
    /// </summary>
    double CalculateByStationCount(int stationCount, LevelingClass levelingClass);

    /// <summary>
    /// Вычислить допуск по длине хода
    /// </summary>
    double CalculateByLength(double lengthKm, LevelingClass levelingClass);
}

public enum LevelingClass
{
    ClassI,
    ClassII,
    ClassIII,
    ClassIV,
    Technical
}
```

#### 8.1.3 Перенос StatisticsService в Application

**Файлы для переноса:**
```
Services/Statistics/IProfileStatisticsService.cs  → Application/Services/IStatisticsService.cs
Services/Statistics/ProfileStatisticsService.cs   → Application/Services/StatisticsService.cs
```

**Или как Query:**
```csharp
namespace Nivtropy.Application.Queries;

public record GetRunStatisticsQuery(Guid NetworkId, Guid RunId);

public record RunStatisticsDto(
    // Высоты
    double? MinHeight,
    double? MaxHeight,
    double? MeanHeight,
    double? StdDevHeight,

    // Превышения
    double MinDeltaH,
    double MaxDeltaH,
    double MeanDeltaH,
    double StdDevDeltaH,

    // Длины станций
    double MinStationLength,
    double MaxStationLength,
    double MeanStationLength,

    // Разности плеч
    double MinArmDifference,
    double MaxArmDifference,
    double AccumulatedArmDifference,

    // Аномалии
    IReadOnlyList<AnomalyDto> Anomalies
);
```

#### 8.1.4 Перенос ValidationService в Application

**Файлы для переноса:**
```
Services/Validation/IImportValidationService.cs  → Application/Services/IImportValidator.cs
Services/Validation/ImportValidationService.cs  → Application/Services/ImportValidator.cs
```

#### 8.1.5 Перенос ExportService в Infrastructure

**Файлы для переноса:**
```
Services/Export/IExportService.cs        → Infrastructure/Export/IExportService.cs
Services/Export/TraverseExportService.cs → Infrastructure/Export/CsvExporter.cs
```

**Новый интерфейс:**
```csharp
namespace Nivtropy.Infrastructure.Export;

using Nivtropy.Domain.Model;

public interface INetworkExporter
{
    Task ExportToCsvAsync(LevelingNetwork network, string filePath);
    Task ExportToJsonAsync(LevelingNetwork network, string filePath);
    Task ExportRunToCsvAsync(Run run, string filePath);
}
```

---

### Фаза 8.2: Удаление заменённых моделей

После переноса сервисов можно удалять модели, которые полностью заменены.

#### 8.2.1 Модели для удаления

| Файл | Причина удаления | Замена |
|------|------------------|--------|
| `Models/TraverseRow.cs` | Заменён на Observation | `Domain/Model/Observation.cs` |
| `Models/LineSummary.cs` | Заменён на Run | `Domain/Model/Run.cs` |
| `Models/PointItem.cs` | Заменён на Point | `Domain/Model/Point.cs` |
| `Models/TraverseSystem.cs` | Дублирует Domain | `Domain/Model/TraverseSystem.cs` |
| `Models/SharedPointLinkItem.cs` | Не нужен (граф) | `Point.IsSharedPoint` |

#### 8.2.2 Порядок удаления моделей

```bash
# 1. Сначала убедиться что адаптеры покрывают все использования
git grep "TraverseRow" --name-only
git grep "LineSummary" --name-only

# 2. Заменить прямые использования на адаптеры или новые типы

# 3. Удалить файлы
rm Models/TraverseRow.cs
rm Models/LineSummary.cs
rm Models/PointItem.cs
rm Models/TraverseSystem.cs
rm Models/SharedPointLinkItem.cs
```

#### 8.2.3 Модели для сохранения (UI/Presentation)

| Файл | Причина сохранения |
|------|-------------------|
| `Models/JournalRow.cs` | UI-специфичная модель для журнала |
| `Models/DesignRow.cs` | UI-специфичная модель для редактирования |
| `Models/ProfileStatistics.cs` | Может использоваться для визуализации |
| `Models/OutlierPoint.cs` | UI для отображения аномалий |
| `Models/RowColoringMode.cs` | UI enum |
| `Models/GeneratedMeasurement.cs` | Для экспорта/генерации |

**Рекомендация:** Перенести в `Presentation/Models/` или `Presentation/DTOs/`

---

### Фаза 8.3: Удаление заменённых сервисов

#### 8.3.1 Сервисы для удаления

| Файл | Причина удаления | Замена |
|------|------------------|--------|
| `Services/Calculation/TraverseCalculationService.cs` | Заменён | `Domain/Services/HeightPropagator.cs` |
| `Services/Calculation/TraverseCorrectionService.cs` | Заменён | `Domain/Services/ProportionalClosureDistributor.cs` |
| `Services/Calculation/SystemConnectivityService.cs` | Заменён | `LevelingNetwork.IsConnected()` |
| `Services/TraverseBuilder.cs` | Заменён | `Application/Commands/ImportHandler` |
| `Services/ITraverseBuilder.cs` | Заменён | - |

#### 8.3.2 Порядок удаления сервисов

```bash
# 1. Проверить использования
git grep "ITraverseCalculationService" --name-only
git grep "TraverseBuilder" --name-only

# 2. Обновить DI регистрацию (убрать старые, добавить новые)

# 3. Удалить файлы
rm Services/Calculation/TraverseCalculationService.cs
rm Services/Calculation/ITraverseCalculationService.cs
rm Services/Calculation/TraverseCorrectionService.cs
rm Services/Calculation/SystemConnectivityService.cs
rm Services/TraverseBuilder.cs
rm Services/ITraverseBuilder.cs

# 4. Удалить пустую папку если осталась
rmdir Services/Calculation/
```

---

### Фаза 8.4: Удаление Legacy адаптеров

После того как все ViewModels перешли на новую архитектуру, адаптеры больше не нужны.

#### 8.4.1 Проверка использований адаптеров

```bash
# Проверить где используются адаптеры
git grep "TraverseRowAdapter" --name-only
git grep "LineSummaryAdapter" --name-only
git grep "NetworkAdapter" --name-only
```

#### 8.4.2 Замена использований в ViewModels

**До (с адаптерами):**
```csharp
public class TraverseCalculationViewModel
{
    public void UpdateRows()
    {
        // Используем адаптер для конвертации
        var adapter = new NetworkAdapter(_network);
        Rows = new ObservableCollection<TraverseRow>(adapter.GetAllTraverseRows());
    }
}
```

**После (без адаптеров):**
```csharp
public class TraverseCalculationViewModel
{
    public void UpdateRows()
    {
        // Работаем напрямую с DTOs
        var observations = _network.Runs
            .SelectMany(r => r.Observations)
            .Select(_mapper.ToObservationDto);

        Observations = new ObservableCollection<ObservationDto>(observations);
    }
}
```

#### 8.4.3 Удаление папки Legacy

```bash
# После замены всех использований
rm -rf Legacy/
```

---

### Фаза 8.5: Обновление Managers

ViewModels/Managers также содержат legacy-логику, которую нужно обновить.

#### 8.5.1 BenchmarkManager

**Текущая реализация** хранит реперы отдельно.

**Новая реализация** - реперы это `Point` с `Type == Benchmark` в `LevelingNetwork`.

```csharp
// Удалить или переделать
// ViewModel/Managers/BenchmarkManager.cs
// ViewModel/Managers/IBenchmarkManager.cs

// Заменить на методы LevelingNetwork:
network.SetBenchmarkHeight(code, height);
network.Benchmarks; // IEnumerable<Point>
```

#### 8.5.2 SharedPointsManager

**Не нужен** - общие точки определяются автоматически в графе.

```csharp
// Удалить
// ViewModel/Managers/SharedPointsManager.cs
// ViewModel/Managers/ISharedPointsManager.cs

// Заменить на:
network.SharedPoints; // IEnumerable<Point> где Degree > 2
point.IsSharedPoint;  // bool
```

#### 8.5.3 TraverseSystemsManager

**Переделать** - использовать `TraverseSystem` из Domain.

```csharp
// Обновить или удалить
// ViewModel/Managers/TraverseSystemsManager.cs

// Использовать:
network.Systems;
network.CreateSystem(name);
network.AddRunToSystem(run, system);
```

---

### Фаза 8.6: Обновление DI регистрации

#### 8.6.1 Финальная регистрация сервисов

```csharp
// Services/ServiceCollectionExtensions.cs

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Domain Services
        services.AddSingleton<IHeightPropagator, HeightPropagator>();
        services.AddSingleton<IClosureDistributor, ProportionalClosureDistributor>();
        services.AddSingleton<IToleranceCalculator, ToleranceCalculator>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Command Handlers
        services.AddTransient<CalculateHeightsHandler>();
        services.AddTransient<ImportMeasurementsHandler>();

        // Query Handlers
        services.AddTransient<GetNetworkSummaryHandler>();
        services.AddTransient<GetRunStatisticsHandler>();

        // Mappers
        services.AddSingleton<INetworkMapper, NetworkMapper>();

        // Application Services
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IImportValidator, ImportValidator>();

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Parsers
        services.AddSingleton<INetworkParser, DatParser>();
        services.AddSingleton<TrimbleDiniParser>();
        services.AddSingleton<ForFormatParser>();

        // Export
        services.AddSingleton<INetworkExporter, CsvExporter>();

        // Persistence
        services.AddSingleton<INetworkRepository, InMemoryNetworkRepository>();

        // IO
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ILoggerService, FileLoggerService>();

        return services;
    }

    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        // UI Services (остаются в Presentation)
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IProfileVisualizationService, ProfileVisualizationService>();
        services.AddSingleton<ITraverseSystemVisualizationService, TraverseSystemVisualizationService>();

        return services;
    }

    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<NetworkViewModel>();
        services.AddSingleton<TraverseCalculationViewModel>();
        services.AddSingleton<TraverseJournalViewModel>();
        services.AddSingleton<TraverseDesignViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DataGeneratorViewModel>();

        return services;
    }
}
```

#### 8.6.2 Обновление App.xaml.cs

```csharp
// App.xaml.cs

protected override void OnStartup(StartupEventArgs e)
{
    var services = new ServiceCollection();

    // Регистрация по слоям (порядок важен!)
    services.AddDomainServices();
    services.AddApplicationServices();
    services.AddInfrastructureServices();
    services.AddPresentationServices();
    services.AddViewModels();

    _serviceProvider = services.BuildServiceProvider();

    // ...
}
```

---

### Фаза 8.7: Реорганизация папок

#### 8.7.1 Финальная структура проекта

```
Nivtropy/
├── Domain/                          # Ядро приложения
│   ├── Model/
│   │   ├── Point.cs
│   │   ├── Observation.cs
│   │   ├── Run.cs
│   │   ├── LevelingNetwork.cs
│   │   └── TraverseSystem.cs
│   ├── ValueObjects/
│   │   ├── PointCode.cs
│   │   ├── Height.cs
│   │   ├── Distance.cs
│   │   ├── Reading.cs
│   │   └── Closure.cs
│   └── Services/
│       ├── IHeightPropagator.cs
│       ├── HeightPropagator.cs
│       ├── IClosureDistributor.cs
│       ├── ProportionalClosureDistributor.cs
│       ├── IToleranceCalculator.cs
│       └── ToleranceCalculator.cs
│
├── Application/                     # Use Cases
│   ├── Commands/
│   │   ├── CalculateHeightsCommand.cs
│   │   ├── ImportMeasurementsCommand.cs
│   │   └── Handlers/
│   ├── Queries/
│   │   ├── GetNetworkSummaryQuery.cs
│   │   ├── GetRunStatisticsQuery.cs
│   │   └── Handlers/
│   ├── DTOs/
│   │   └── NetworkDtos.cs
│   ├── Mappers/
│   │   └── NetworkMapper.cs
│   └── Services/
│       ├── IStatisticsService.cs
│       ├── StatisticsService.cs
│       ├── IImportValidator.cs
│       └── ImportValidator.cs
│
├── Infrastructure/                  # Внешние зависимости
│   ├── Parsers/
│   │   ├── INetworkParser.cs
│   │   ├── DatParser.cs
│   │   ├── TrimbleDiniParser.cs
│   │   └── ForFormatParser.cs
│   ├── Persistence/
│   │   ├── INetworkRepository.cs
│   │   └── InMemoryNetworkRepository.cs
│   ├── Export/
│   │   ├── INetworkExporter.cs
│   │   └── CsvExporter.cs
│   ├── IO/
│   │   ├── IFileService.cs
│   │   └── FileService.cs
│   └── Logging/
│       ├── ILoggerService.cs
│       └── FileLoggerService.cs
│
├── Presentation/                    # UI Layer
│   ├── ViewModels/
│   │   ├── Base/
│   │   │   ├── ViewModelBase.cs
│   │   │   └── RelayCommand.cs
│   │   ├── MainViewModel.cs
│   │   ├── NetworkViewModel.cs
│   │   ├── TraverseCalculationViewModel.cs
│   │   ├── TraverseJournalViewModel.cs
│   │   ├── TraverseDesignViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── TraverseJournalView.xaml
│   │   └── ...
│   ├── Models/                      # UI-специфичные модели
│   │   ├── JournalRow.cs
│   │   ├── DesignRow.cs
│   │   └── RowColoringMode.cs
│   ├── Services/
│   │   ├── IDialogService.cs
│   │   ├── DialogService.cs
│   │   ├── IProfileVisualizationService.cs
│   │   └── ProfileVisualizationService.cs
│   ├── Converters/
│   │   └── ...
│   └── Resources/
│       └── ...
│
├── Constants/                       # Общие константы
├── Utilities/                       # Общие утилиты
│
├── App.xaml
├── App.xaml.cs
└── Nivtropy.csproj
```

#### 8.7.2 Команды для реорганизации

```bash
# Создать новые папки
mkdir -p Presentation/ViewModels/Base
mkdir -p Presentation/Views
mkdir -p Presentation/Models
mkdir -p Presentation/Services
mkdir -p Presentation/Converters
mkdir -p Presentation/Resources

# Перенести ViewModels
mv ViewModel/* Presentation/ViewModels/

# Перенести Views
mv View/* Presentation/Views/

# Перенести UI-модели
mv Models/JournalRow.cs Presentation/Models/
mv Models/DesignRow.cs Presentation/Models/
mv Models/RowColoringMode.cs Presentation/Models/

# Перенести UI-сервисы
mv Services/Dialog/* Presentation/Services/
mv Services/Visualization/* Presentation/Services/

# Перенести Converters и Resources
mv Converters/* Presentation/Converters/
mv Resources/* Presentation/Resources/

# Удалить пустые папки
rmdir ViewModel View Models/

# Обновить namespaces в перенесённых файлах!
```

---

### Фаза 8.8: Обновление namespaces

После реорганизации папок нужно обновить namespaces во всех файлах.

#### 8.8.1 Скрипт для обновления namespaces

```bash
# Найти все файлы с старыми namespaces
grep -r "namespace Nivtropy.ViewModel" --include="*.cs" -l
grep -r "namespace Nivtropy.View" --include="*.cs" -l
grep -r "namespace Nivtropy.Models" --include="*.cs" -l
grep -r "namespace Nivtropy.Services" --include="*.cs" -l
```

#### 8.8.2 Маппинг namespaces

| Старый namespace | Новый namespace |
|------------------|-----------------|
| `Nivtropy.ViewModel` | `Nivtropy.Presentation.ViewModels` |
| `Nivtropy.ViewModel.Base` | `Nivtropy.Presentation.ViewModels.Base` |
| `Nivtropy.ViewModel.Managers` | `Nivtropy.Presentation.ViewModels.Managers` |
| `Nivtropy.View` | `Nivtropy.Presentation.Views` |
| `Nivtropy.Models` (UI) | `Nivtropy.Presentation.Models` |
| `Nivtropy.Services.Dialog` | `Nivtropy.Presentation.Services` |
| `Nivtropy.Services.Visualization` | `Nivtropy.Presentation.Services` |
| `Nivtropy.Services.Calculation` | (удалено) |
| `Nivtropy.Services.Statistics` | `Nivtropy.Application.Services` |
| `Nivtropy.Services.Tolerance` | `Nivtropy.Domain.Services` |
| `Nivtropy.Services.Export` | `Nivtropy.Infrastructure.Export` |
| `Nivtropy.Services.Parsers` | `Nivtropy.Infrastructure.Parsers` |
| `Nivtropy.Services.Validation` | `Nivtropy.Application.Services` |
| `Nivtropy.Services.IO` | `Nivtropy.Infrastructure.IO` |
| `Nivtropy.Services.Logging` | `Nivtropy.Infrastructure.Logging` |

#### 8.8.3 Обновление using директив

После переименования namespaces нужно обновить using во всех файлах:

```csharp
// Было
using Nivtropy.Models;
using Nivtropy.Services.Calculation;
using Nivtropy.ViewModel;

// Стало
using Nivtropy.Domain.Model;
using Nivtropy.Domain.Services;
using Nivtropy.Presentation.ViewModels;
```

---

### Фаза 8.9: Обновление XAML

#### 8.9.1 Обновление xmlns

```xml
<!-- Было -->
<Window xmlns:vm="clr-namespace:Nivtropy.ViewModel"
        xmlns:conv="clr-namespace:Nivtropy.Converters">

<!-- Стало -->
<Window xmlns:vm="clr-namespace:Nivtropy.Presentation.ViewModels"
        xmlns:conv="clr-namespace:Nivtropy.Presentation.Converters">
```

#### 8.9.2 Файлы XAML для обновления

```bash
# Найти все XAML с старыми namespaces
grep -r "clr-namespace:Nivtropy.ViewModel" --include="*.xaml" -l
grep -r "clr-namespace:Nivtropy.View" --include="*.xaml" -l
grep -r "clr-namespace:Nivtropy.Models" --include="*.xaml" -l
grep -r "clr-namespace:Nivtropy.Converters" --include="*.xaml" -l
```

---

### Фаза 8.10: Финальная проверка

#### 8.10.1 Компиляция

```bash
dotnet build
```

#### 8.10.2 Проверка отсутствия Legacy

```bash
# Не должно быть результатов:
ls Models/TraverseRow.cs 2>/dev/null
ls Models/LineSummary.cs 2>/dev/null
ls Services/Calculation/ 2>/dev/null
ls Services/TraverseBuilder.cs 2>/dev/null
ls Legacy/ 2>/dev/null

# Не должно быть использований:
grep -r "TraverseRow" --include="*.cs" | grep -v "Adapter"
grep -r "LineSummary" --include="*.cs" | grep -v "Adapter"
grep -r "ITraverseBuilder" --include="*.cs"
grep -r "ITraverseCalculationService" --include="*.cs"
```

#### 8.10.3 Запуск приложения

```bash
dotnet run
```

Проверить:
- [ ] Загрузка файла работает
- [ ] Расчёт высот работает
- [ ] Визуализация профиля работает
- [ ] Журнальный вид работает
- [ ] Экспорт работает
- [ ] Системы ходов работают
- [ ] Реперы работают

#### 8.10.4 Запуск тестов

```bash
dotnet test
```

---

## Чеклист Этапа 8

### 8.1 Перенос сервисов
- [ ] Перенести парсеры в Infrastructure/Parsers/
- [ ] Перенести ToleranceService в Domain/Services/
- [ ] Перенести StatisticsService в Application/Services/
- [ ] Перенести ValidationService в Application/Services/
- [ ] Перенести ExportService в Infrastructure/Export/
- [ ] Обновить интерфейсы под новую архитектуру

### 8.2 Удаление моделей
- [ ] Удалить Models/TraverseRow.cs
- [ ] Удалить Models/LineSummary.cs
- [ ] Удалить Models/PointItem.cs
- [ ] Удалить Models/TraverseSystem.cs
- [ ] Удалить Models/SharedPointLinkItem.cs
- [ ] Перенести оставшиеся модели в Presentation/Models/

### 8.3 Удаление сервисов
- [ ] Удалить Services/Calculation/TraverseCalculationService.cs
- [ ] Удалить Services/Calculation/TraverseCorrectionService.cs
- [ ] Удалить Services/Calculation/SystemConnectivityService.cs
- [ ] Удалить Services/TraverseBuilder.cs
- [ ] Удалить пустые папки

### 8.4 Удаление адаптеров
- [ ] Заменить использования адаптеров на прямую работу с DTOs
- [ ] Удалить Legacy/Adapters/TraverseRowAdapter.cs
- [ ] Удалить Legacy/Adapters/LineSummaryAdapter.cs
- [ ] Удалить Legacy/Adapters/NetworkAdapter.cs
- [ ] Удалить папку Legacy/

### 8.5 Обновление Managers
- [ ] Удалить или переделать BenchmarkManager
- [ ] Удалить SharedPointsManager
- [ ] Переделать TraverseSystemsManager

### 8.6 Обновление DI
- [ ] Обновить ServiceCollectionExtensions.cs
- [ ] Обновить App.xaml.cs
- [ ] Проверить все регистрации

### 8.7 Реорганизация папок
- [ ] Создать структуру Presentation/
- [ ] Перенести ViewModels, Views, Converters
- [ ] Перенести UI-модели и UI-сервисы
- [ ] Удалить пустые папки

### 8.8 Обновление namespaces
- [ ] Обновить namespaces в .cs файлах
- [ ] Обновить using директивы

### 8.9 Обновление XAML
- [ ] Обновить xmlns в XAML файлах

### 8.10 Финальная проверка
- [ ] Компиляция без ошибок
- [ ] Нет остатков Legacy кода
- [ ] Приложение запускается
- [ ] Все функции работают
- [ ] Тесты проходят

---

## Порядок выполнения для Sonnet

1. **Начни с переноса сервисов** (8.1) - это не ломает существующий код
2. **Затем удаляй модели** (8.2) - по одному файлу, проверяя компиляцию
3. **Удаляй сервисы** (8.3) - после удаления их использований
4. **Обновляй ViewModels** для работы без адаптеров
5. **Удаляй адаптеры** (8.4) - когда они не используются
6. **Реорганизуй папки** (8.7) - в конце
7. **Обновляй namespaces и XAML** (8.8-8.9)
8. **Финальная проверка** (8.10)

**Важно:** После каждого изменения:
```bash
dotnet build
```

Если есть ошибки - исправь перед следующим шагом.

---

## Ожидаемый результат

После выполнения Этапа 8:
- Нет папки `Legacy/`
- Нет старых моделей в `Models/`
- Нет старых сервисов расчёта в `Services/Calculation/`
- Чистая структура по слоям: Domain → Application → Infrastructure → Presentation
- Все ViewModels работают с DTOs напрямую
- Граф `LevelingNetwork` - единственный источник данных
