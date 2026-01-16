# DDD Migration Instructions for Codex

## О проекте

**Nivtropy** - WPF приложение для обработки данных геометрического нивелирования.
Выполняет импорт данных с цифровых нивелиров, расчёт высот точек, уравнивание ходов и экспорт результатов.

## Что было сделано (сессия review-ddd-legacy-removal)

### Исправленные проблемы

1. **Опечатка в namespace** - `ViewModelss` → `ViewModels` (24 файла)
2. **Удалены дубликаты папок:**
   - `ViewModel/` (старая) - удалена, используется `Presentation/ViewModels/`
   - `View/` (старая) - удалена, используется `Presentation/Views/`
   - `Converters/` (старая) - удалена, используется `Presentation/Converters/`
   - `Legacy/` - удалена полностью
   - `Presentation/Services/` - удалена (дубликат `Services/`)

3. **Удалены дубликаты моделей из `Models/`:**
   - `DesignRow.cs` → оставлен только `Presentation/Models/DesignRow.cs`
   - `JournalRow.cs` → оставлен только `Presentation/Models/JournalRow.cs`
   - `OutlierPoint.cs` → оставлен только `Presentation/Models/OutlierPoint.cs`
   - `RowColoringMode.cs` → оставлен только `Presentation/Models/RowColoringMode.cs`

4. **Исправлены using директивы во всех файлах:**
   - `Nivtropy.ViewModels` → `Nivtropy.Presentation.ViewModels`
   - `Nivtropy.Views` → `Nivtropy.Presentation.Views`
   - `Nivtropy.Presentation.Services` → `Nivtropy.Services.Dialog` / `Nivtropy.Services.Visualization`
   - Добавлены `using Nivtropy.Presentation.Models` где нужны UI-модели

5. **Исправлен NetworkViewModel:**
   - `_mapper.ToObservationDto()` → `_mapper.ToDto()`

6. **Восстановлены удалённые файлы (нужны для работы):**
   - `Presentation/Models/PointItem.cs` (PointItem + BenchmarkItem)
   - `Presentation/Models/SharedPointLinkItem.cs`
   - `Presentation/Models/TraverseSystem.cs` (UI версия)
   - `Services/ITraverseBuilder.cs`
   - `Services/TraverseBuilder.cs`
   - `Services/Calculation/TraverseCorrectionService.cs`
   - `Services/Calculation/SystemConnectivityService.cs`

---

## Текущее состояние кода

### Статистика

| Слой | Файлов | Строк | Статус |
|------|--------|-------|--------|
| **Domain/Application/Infrastructure** | 34 | ~3,100 | ✅ Новая DDD архитектура |
| **Models + Services + Presentation/Models** | 29 | ~3,500 | ⚠️ Legacy (переходный) |
| **Presentation/ViewModels** | 18 | ~5,600 | 🔄 Смешанный |
| **Presentation/Views + Converters** | - | ~1,850 | ✅ UI (останется) |

### Прогресс миграции

```
┌─────────────────────────────────────┐
│  DDD готово:        ~25%           │
│  Нужно мигрировать: ~50%           │
│  Удалить потом:     ~25%           │
└─────────────────────────────────────┘
```

---

## Архитектура проекта

### Структура папок

```
Nivtropy/
├── Domain/                      # ✅ DDD Domain Layer
│   ├── Model/                   # Доменные сущности
│   │   ├── LevelingNetwork.cs   # Агрегат нивелирной сети
│   │   ├── Run.cs               # Нивелирный ход
│   │   ├── Point.cs             # Точка (репер/связующая)
│   │   ├── Observation.cs       # Наблюдение (станция)
│   │   └── TraverseSystem.cs    # Система ходов (DOMAIN версия!)
│   ├── Services/                # Доменные сервисы
│   └── ValueObjects/            # Value Objects
│       ├── Height.cs            # Высота (Known/Unknown)
│       ├── Distance.cs          # Расстояние
│       ├── PointCode.cs         # Код точки
│       └── Closure.cs           # Невязка хода
│
├── Application/                 # ✅ Application Layer (CQRS)
│   ├── Commands/                # Команды (изменение состояния)
│   │   ├── CalculateHeightsCommand.cs
│   │   └── Handlers/
│   ├── Queries/                 # Запросы (чтение данных)
│   │   ├── GetNetworkSummaryQuery.cs
│   │   └── Handlers/
│   ├── DTOs/                    # Data Transfer Objects
│   │   ├── NetworkSummaryDto.cs
│   │   ├── RunSummaryDto.cs
│   │   ├── ObservationDto.cs
│   │   └── PointDto.cs
│   ├── Mappers/                 # Маппинг Domain → DTO
│   │   └── NetworkMapper.cs
│   └── Services/                # Application Services
│       ├── IProfileStatisticsService.cs
│       └── ProfileStatisticsService.cs
│
├── Infrastructure/              # ✅ Infrastructure Layer
│   ├── Parsers/                 # Парсеры файлов нивелиров
│   │   ├── IDataParser.cs
│   │   ├── DatParser.cs         # Trimble DiNi
│   │   ├── ForFormatParser.cs   # Leica формат
│   │   └── TrimbleDiniParser.cs
│   ├── Export/                  # Экспорт результатов
│   │   └── TraverseExportService.cs
│   └── Persistence/             # Хранение данных
│       ├── INetworkRepository.cs
│       └── InMemoryNetworkRepository.cs
│
├── Presentation/                # UI Layer (WPF MVVM)
│   ├── ViewModels/              # 🔄 ViewModels (частично на DDD)
│   │   ├── Base/
│   │   │   ├── ViewModelBase.cs
│   │   │   └── RelayCommand.cs
│   │   ├── MainViewModel.cs
│   │   ├── NetworkViewModel.cs        # ✅ Использует DDD
│   │   ├── TraverseCalculationViewModel.cs  # ⚠️ GOD FILE (1824 строки!)
│   │   ├── TraverseJournalViewModel.cs
│   │   ├── DataViewModel.cs
│   │   ├── DataGeneratorViewModel.cs
│   │   └── TraverseDesignViewModel.cs
│   ├── Views/                   # XAML Views
│   ├── Models/                  # ⚠️ UI Models (для DataGrid binding)
│   │   ├── TraverseRow.cs       # Строка журнала нивелирования
│   │   ├── LineSummary.cs       # Сводка по ходу
│   │   ├── JournalRow.cs        # Строка журнала
│   │   ├── DesignRow.cs         # Строка проектирования
│   │   ├── PointItem.cs         # Элемент списка точек
│   │   ├── BenchmarkItem.cs     # Элемент списка реперов
│   │   ├── SharedPointLinkItem.cs
│   │   ├── OutlierPoint.cs      # Аномалия
│   │   ├── RowColoringMode.cs
│   │   └── TraverseSystem.cs    # UI версия (с INotifyPropertyChanged!)
│   ├── Converters/              # WPF Value Converters
│   └── Resources/               # Стили, темы
│
├── Models/                      # ⚠️ LEGACY Models
│   ├── MeasurementRecord.cs     # Запись измерения (используется везде!)
│   ├── GeneratedMeasurement.cs  # Для генератора тестовых данных
│   ├── ProfileStatistics.cs     # Статистика профиля
│   └── ValidationResult.cs      # Результат валидации
│
├── Services/                    # ⚠️ Mixed Services
│   ├── Dialog/                  # UI сервисы
│   │   ├── IDialogService.cs
│   │   └── DialogService.cs
│   ├── Visualization/           # Сервисы визуализации
│   │   ├── IProfileVisualizationService.cs
│   │   ├── ProfileVisualizationService.cs
│   │   └── ...
│   ├── Calculation/             # Legacy расчёты
│   │   ├── TraverseCorrectionService.cs
│   │   └── SystemConnectivityService.cs
│   ├── ITraverseBuilder.cs      # Legacy builder
│   ├── TraverseBuilder.cs
│   └── ServiceCollectionExtensions.cs  # DI регистрация
│
└── Constants/                   # Константы приложения
```

---

## ВАЖНО: Два TraverseSystem!

В проекте есть ДВА класса `TraverseSystem` с РАЗНЫМИ реализациями:

### 1. Domain версия (`Domain/Model/TraverseSystem.cs`)
```csharp
namespace Nivtropy.Domain.Model
{
    public class TraverseSystem
    {
        public Guid Id { get; }
        public string Name { get; private set; }
        public IReadOnlyList<Run> Runs => _runs.AsReadOnly();

        public void AddRun(Run run) { ... }
        public void RemoveRun(Run run) { ... }
        // Бизнес-логика группировки ходов в системы
    }
}
```

### 2. UI версия (`Presentation/Models/TraverseSystem.cs`)
```csharp
namespace Nivtropy.Presentation.Models
{
    public class TraverseSystem : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Name { get; set; }  // С уведомлением UI
        public List<int> RunIndexes { get; }

        // Для отображения в UI, binding к DataGrid
    }
}
```

**НЕ ПУТАТЬ!** При миграции ViewModels нужно маппить между ними.

---

## План миграции (для Codex)

### Фаза 1: ✅ ВЫПОЛНЕНО - Компиляция работает

Все ошибки компиляции исправлены. Приложение запускается и работает.

---

### Фаза 2: ✅ ВЫПОЛНЕНО - Чистая архитектура моделей

**Результат:**
- `ValidationResult` перенесён в `Application/DTOs/`
- `ProfileStatistics` перенесён в `Application/DTOs/`
- `MeasurementRecord` остаётся в `Models/` (зависит от LineSummary)
- `GeneratedMeasurement` остаётся в `Models/` (UI-специфичная модель)

#### Текущая структура моделей:

```
Models/                          # "Входные" модели (данные из файлов)
├── MeasurementRecord.cs         # Запись измерения с нивелира
└── GeneratedMeasurement.cs      # Сгенерированные тестовые данные

Presentation/Models/             # "UI" модели (для отображения в DataGrid)
├── TraverseRow.cs               # Строка журнала нивелирования
├── LineSummary.cs               # Сводка по ходу
├── JournalRow.cs                # Строка журнала
├── DesignRow.cs                 # Строка проектирования
├── PointItem.cs                 # Элемент ComboBox точек
├── BenchmarkItem.cs             # Элемент ComboBox реперов
├── SharedPointLinkItem.cs       # Общая точка между ходами
├── OutlierPoint.cs              # Аномалия
├── RowColoringMode.cs           # Режим окраски строк
└── TraverseSystem.cs            # Система ходов (UI версия!)
```

#### Что НЕ нужно делать:
- ❌ Не перемещать файлы между папками
- ❌ Не удалять Models/ или Presentation/Models/
- ❌ Не объединять модели

#### Что нужно сделать:

**Шаг 2.1:** Перенести `MeasurementRecord` в Domain слой

Файл `Models/MeasurementRecord.cs` - это ключевая модель данных.
Она должна быть в `Domain/Model/` как доменная сущность.

```bash
# Действие:
1. Создать Domain/Model/MeasurementRecord.cs (скопировать содержимое)
2. Изменить namespace на Nivtropy.Domain.Model
3. Обновить все using директивы в проекте
4. Удалить Models/MeasurementRecord.cs
```

**Шаг 2.2:** Перенести `ValidationResult` в Application слой

```bash
# Действие:
1. Переместить Models/ValidationResult.cs → Application/DTOs/ValidationResult.cs
2. Изменить namespace на Nivtropy.Application.DTOs
3. Обновить using директивы
```

**Шаг 2.3:** Перенести `ProfileStatistics` в Application слой

```bash
# Действие:
1. Переместить Models/ProfileStatistics.cs → Application/DTOs/ProfileStatistics.cs
2. Изменить namespace на Nivtropy.Application.DTOs
3. Обновить using директивы
```

**Шаг 2.4:** Оставить `GeneratedMeasurement` в Models/

Это специфичная модель для генератора тестовых данных, не относится к Domain.

#### Результат Фазы 2 (фактический):

```
Models/                          # Входные модели (парсинг файлов)
├── MeasurementRecord.cs         # Запись измерения (зависит от LineSummary!)
└── GeneratedMeasurement.cs      # Для генератора тестов

Application/DTOs/                # DTO для передачи данных
├── NetworkSummaryDto.cs
├── RunSummaryDto.cs
├── ObservationDto.cs
├── PointDto.cs
├── ValidationResult.cs          # ✅ ПЕРЕНЕСЁН
└── ProfileStatistics.cs         # ✅ ПЕРЕНЕСЁН

Presentation/Models/             # UI модели (без изменений)
└── ... (все файлы остаются)
```

**Примечание:** MeasurementRecord не перенесён в Domain, т.к. содержит UI-зависимости (LineSummary).

---

### Фаза 3: Рефакторинг TraverseCalculationViewModel

**Файл:** `Presentation/ViewModels/TraverseCalculationViewModel.cs`
**Размер:** 1824 строки - GOD FILE!

#### Шаг 3.1: Анализ текущей структуры

Прочитай файл и выдели следующие группы методов:

```
ГРУППА A: Работа с данными (должна уйти в Application/Services)
- BuildTraverseRows()
- RecalculateHeights()
- CalculateLineSummaries()
- ApplyCorrections()

ГРУППА B: Работа с системами (должна уйти в Domain/Services)
- CreateSystem()
- DeleteSystem()
- MergeRuns()
- SplitRun()

ГРУППА C: UI логика (остаётся в ViewModel)
- Commands (RelayCommand)
- ObservableCollection свойства
- PropertyChanged уведомления
```

#### Шаг 3.2: Создать Application Service

Создай файл `Application/Services/TraverseCalculationService.cs`:

```csharp
namespace Nivtropy.Application.Services;

public interface ITraverseCalculationService
{
    /// <summary>
    /// Строит список TraverseRow из записей измерений
    /// </summary>
    List<TraverseRow> BuildTraverseRows(
        IReadOnlyList<MeasurementRecord> records,
        IReadOnlyList<LineSummary> runs);

    /// <summary>
    /// Пересчитывает высоты точек
    /// </summary>
    void RecalculateHeights(
        IList<TraverseRow> rows,
        Func<string, double?> getKnownHeight);

    /// <summary>
    /// Вычисляет сводки по ходам
    /// </summary>
    List<LineSummary> CalculateLineSummaries(IReadOnlyList<TraverseRow> rows);

    /// <summary>
    /// Применяет поправки к превышениям
    /// </summary>
    void ApplyCorrections(
        IList<TraverseRow> rows,
        LineSummary run,
        double closureValue);
}

public class TraverseCalculationService : ITraverseCalculationService
{
    // Реализация: перенести методы из TraverseCalculationViewModel
}
```

#### Шаг 3.3: Перенести методы

Для каждого метода из ГРУППЫ A:
1. Скопировать метод в `TraverseCalculationService`
2. Убрать зависимости от полей ViewModel (передавать как параметры)
3. Заменить в ViewModel вызов на `_calculationService.Method(...)`

**Пример переноса:**

ДО (в ViewModel):
```csharp
private void RecalculateHeights()
{
    foreach (var row in _rows)
    {
        if (HasKnownHeight(row.BackCode))
            row.BackHeight = GetKnownHeight(row.BackCode);
        // ...
    }
}
```

ПОСЛЕ (в Service):
```csharp
public void RecalculateHeights(
    IList<TraverseRow> rows,
    Func<string, double?> getKnownHeight)
{
    foreach (var row in rows)
    {
        var height = getKnownHeight(row.BackCode);
        if (height.HasValue)
            row.BackHeight = height.Value;
        // ...
    }
}
```

ПОСЛЕ (в ViewModel):
```csharp
private void RecalculateHeights()
{
    _calculationService.RecalculateHeights(_rows, code => GetKnownHeight(code));
}
```

#### Шаг 3.4: Зарегистрировать сервис в DI

В `Services/ServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<ITraverseCalculationService, TraverseCalculationService>();
```

#### Шаг 3.5: Обновить конструктор ViewModel

```csharp
public TraverseCalculationViewModel(
    DataViewModel dataViewModel,
    ITraverseBuilder traverseBuilder,
    ITraverseCalculationService calculationService,  // ДОБАВИТЬ
    ITraverseExportService exportService,
    // ...
)
```

#### Результат Фазы 3:

- ViewModel уменьшится с 1824 до ~800-1000 строк
- Бизнес-логика расчётов будет в отдельном сервисе
- Сервис можно будет тестировать отдельно от UI

---

### Фаза 4: Миграция остальных ViewModels

Порядок миграции (от простого к сложному):

#### 4.1 DataViewModel (436 строк) - НИЗКАЯ сложность

**Что вынести:**
- `AnnotateRuns()` → `Application/Services/IRunAnnotationService`
- `BuildSummary()` → туда же

**Оставить в ViewModel:**
- `ObservableCollection<MeasurementRecord> Records`
- `ObservableCollection<LineSummary> Runs`
- Команды загрузки файлов

#### 4.2 TraverseDesignViewModel (408 строк) - СРЕДНЯЯ сложность

**Что вынести:**
- Расчёт проектных высот → `Application/Services/IDesignCalculationService`
- Распределение невязки → туда же

#### 4.3 TraverseJournalViewModel (413 строк) - СРЕДНЯЯ сложность

**Зависит от:** TraverseCalculationViewModel

**Что вынести:**
- Конвертация TraverseRow → JournalRow (уже есть частично)
- Статистика профиля (уже использует IProfileStatisticsService)

#### 4.4 DataGeneratorViewModel (842 строки) - СРЕДНЯЯ сложность

**Что вынести:**
- Генерация шума → `Application/Services/INoiseGeneratorService`
- Экспорт в формат Nivelir → `Infrastructure/Export/INivelorExportService`

---

### Фаза 5: Удаление Legacy кода

**ВАЖНО:** Выполнять ТОЛЬКО после успешного завершения Фаз 2-4!

#### Шаг 5.1: Удалить legacy services

После переноса логики в Application/Services:
```bash
# Удалить:
Services/TraverseBuilder.cs
Services/ITraverseBuilder.cs
Services/Calculation/TraverseCorrectionService.cs
Services/Calculation/SystemConnectivityService.cs
```

#### Шаг 5.2: Объединить TraverseSystem

После того как все ViewModels используют Domain версию:
```bash
# Удалить:
Presentation/Models/TraverseSystem.cs

# Создать маппер:
Application/Mappers/TraverseSystemMapper.cs
```

#### Шаг 5.3: Очистить DI регистрацию

Удалить из `ServiceCollectionExtensions.cs`:
```csharp
// УДАЛИТЬ:
services.AddSingleton<ITraverseBuilder, TraverseBuilder>();
services.AddSingleton<ITraverseCorrectionService, TraverseCorrectionService>();
services.AddSingleton<ISystemConnectivityService, SystemConnectivityService>();
```

---

### Чек-лист для каждой фазы

После завершения каждой фазы проверь:

- [ ] `dotnet build` - компиляция без ошибок
- [ ] Приложение запускается
- [ ] Импорт .dat/.for файлов работает
- [ ] Расчёт высот выдаёт корректные результаты
- [ ] Экспорт в Excel/CSV работает
- [ ] UI отображает данные правильно

---

## DI Регистрация

Файл: `Services/ServiceCollectionExtensions.cs`

```csharp
// Уже зарегистрировано:
services.AddSingleton<INetworkRepository, InMemoryNetworkRepository>();
services.AddSingleton<INetworkMapper, NetworkMapper>();
services.AddSingleton<CalculateHeightsHandler>();
services.AddSingleton<GetNetworkSummaryHandler>();

// Legacy (пока нужно):
services.AddSingleton<ITraverseBuilder, TraverseBuilder>();
services.AddSingleton<IDialogService, DialogService>();
services.AddSingleton<IProfileVisualizationService, ProfileVisualizationService>();
```

---

## Тестирование после изменений

1. **Сборка** - `dotnet build` без ошибок
2. **Запуск** - приложение открывается
3. **Импорт** - загрузка .dat/.for файлов работает
4. **Расчёт** - высоты вычисляются корректно
5. **Журнал** - данные отображаются в таблицах
6. **Экспорт** - сохранение результатов работает

---

## Полезные команды

```bash
# Найти все файлы использующие legacy namespace
grep -r "using Nivtropy.Models" --include="*.cs" | grep -v obj/

# Найти все ViewModels использующие DDD
grep -l "using Nivtropy.Domain\|using Nivtropy.Application" Presentation/ViewModels/*.cs

# Подсчитать строки в слоях
find Domain Application Infrastructure -name "*.cs" | xargs wc -l
```

---

## Контакты и история

- **Ветка:** `claude/review-ddd-legacy-removal-j5Icw`
- **Сессия:** Исправление ошибок после неудачной миграции Sonnet
- **Статус:** Компиляция работает, приложение запускается
