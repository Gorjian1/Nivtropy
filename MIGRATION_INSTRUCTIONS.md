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
| **Domain/Application/Infrastructure** | 42 | ~3,800 | ✅ Новая DDD архитектура |
| **Models + Services + Presentation/Models** | 25 | ~2,800 | ⚠️ Legacy (переходный) |
| **Presentation/ViewModels** | 18 | ~4,800 | 🔄 Рефакторинг идёт |
| **Presentation/Views + Converters** | - | ~1,850 | ✅ UI (останется) |

### Прогресс миграции

```
┌─────────────────────────────────────┐
│  DDD готово:        ~60%           │
│  Нужно мигрировать: ~25%           │
│  Удалить потом:     ~15%           │
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

### Фаза 3: ✅ ВЫПОЛНЕНО - Рефакторинг TraverseCalculationViewModel

**Файл:** `Presentation/ViewModels/TraverseCalculationViewModel.cs`
**Размер:** ~~1824~~ → ~1700 строк (после рефакторинга)

#### ✅ Созданные сервисы:

| Сервис | Файл | Статус |
|--------|------|--------|
| `ITraverseCalculationService` | `Application/Services/TraverseCalculationService.cs` | ✅ Создан, интегрирован в ViewModel |
| `IClosureCalculationService` | `Application/Services/ClosureCalculationService.cs` | ✅ Создан, зарегистрирован в DI |
| `IRunAnnotationService` | `Application/Services/RunAnnotationService.cs` | ✅ Создан, используется в DataViewModel |
| `INetworkAdjuster` | `Domain/Services/LeastSquaresNetworkAdjuster.cs` | ✅ Создан |
| `AdjustmentMode` | `Application/Enums/AdjustmentMode.cs` | ✅ Добавлен (Local/Global режимы) |

#### Шаг 3.1: Анализ текущей структуры (ВЫПОЛНЕНО)

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

#### Шаг 3.2: ✅ ВЫПОЛНЕНО - Создать Application Services

**ITraverseCalculationService** - строки, высоты, поправки:
- `BuildTraverseRows()` - построение строк хода
- `RecalculateHeights()` - пересчёт высот
- `CalculateLineSummaries()` - расчёт итогов секций
- `ApplyCorrections()` - применение поправок с режимами (Local/Global)

**IClosureCalculationService** - невязка и допуски:
- `CalculateClosure()` - расчёт невязки хода
- `CalculateTolerance()` - расчёт допуска (SqrtStations/SqrtLength)
- `Calculate()` - полный расчёт с вердиктом
- `GenerateVerdict()` - формирование текстового вывода

#### Шаг 3.3: ✅ ВЫПОЛНЕНО - Методы перенесены

ViewModel теперь использует:
```csharp
private readonly ITraverseCalculationService _calculationService;

// Вместо _traverseBuilder.Build():
var items = _calculationService.BuildTraverseRows(records, Runs);

// Вместо RecalculateHeightsForRunInternal():
_calculationService.RecalculateHeights(runRows, code => knownHeights.TryGetValue(code, out var h) ? h : null);

// Вместо CalculateCorrections():
_calculationService.ApplyCorrections(groupItems, anchorChecker, MethodOrientationSign, AdjustmentMode);
```

#### Шаг 3.4: ✅ ВЫПОЛНЕНО - DI регистрация

```csharp
services.AddSingleton<ITraverseCalculationService, TraverseCalculationService>();
services.AddSingleton<IClosureCalculationService, ClosureCalculationService>();
services.AddSingleton<IRunAnnotationService, RunAnnotationService>();
```

#### Шаг 3.5: ✅ ВЫПОЛНЕНО - Интегрировать IClosureCalculationService

ViewModel теперь использует сервис:
```csharp
private readonly IClosureCalculationService _closureService;

// RecalculateClosure():
Closure = _closureService.CalculateClosure(_rows.ToList(), MethodOrientationSign);

// UpdateTolerance():
ClosureVerdict = _closureService.GenerateVerdict(
    Closure, AllowableClosure, MethodTolerance, ClassTolerance,
    SelectedMethod?.Code, SelectedClass?.Code);
```

#### Результат Фазы 3:

- ✅ ViewModel уменьшился с 1824 до ~1700 строк
- ✅ ITraverseCalculationService интегрирован
- ✅ IClosureCalculationService интегрирован
- ✅ ITraverseBuilder больше не используется напрямую
- ✅ Бизнес-логика вынесена в Application Services

---

### Фаза 4: ✅ ПОЛНОСТЬЮ ВЫПОЛНЕНА - Миграция остальных ViewModels

Все ViewModels мигрированы на DDD сервисы!

#### 4.1 DataViewModel - ✅ ПОЛНОСТЬЮ ВЫПОЛНЕНО

**Созданные сервисы:**
- `IRunAnnotationService` - аннотация ходов
- `BuildSummary()` - перенесён в RunAnnotationService

**Результат:** ViewModel теперь ~310 строк (было 436), полностью на DDD

#### 4.2 TraverseDesignViewModel - ✅ ПОЛНОСТЬЮ ВЫПОЛНЕНО

**Созданные сервисы:**
- `IDesignCalculationService` - расчёт проектных высот и распределение невязки
- `BuildDesignRows()` - построение строк проектирования
- `RecalculateHeightsFrom()` - пересчёт высот после редактирования
- `RecalculateCorrectionsAndHeights()` - пересчёт поправок и высот

**Результат:** ViewModel теперь ~310 строк (было 408), использует DDD сервисы

#### 4.3 TraverseJournalViewModel - ✅ УЖЕ НА DDD

**Используемые сервисы:**
- `IProfileVisualizationService` - визуализация профиля
- `IProfileStatisticsService` - статистика профиля
- `ITraverseSystemVisualizationService` - визуализация систем ходов

**Результат:** ViewModel уже использовал DDD сервисы, дополнительная миграция не требуется

#### 4.4 DataGeneratorViewModel - ✅ ПОЛНОСТЬЮ ВЫПОЛНЕНО

**Созданные сервисы:**
- `INoiseGeneratorService` - генерация нормально распределённого шума (Box-Muller transform)
- `INivelorExportService` - экспорт в формат Nivelir (Leica FOR)

**Результат:** ViewModel теперь ~690 строк (было 842), использует DDD сервисы

---

### Фаза 5: ✅ ЧАСТИЧНО ВЫПОЛНЕНА - Удаление Legacy кода

#### Шаг 5.1: ✅ ITraverseBuilder инкапсулирован

**Что сделано:**
- `ITraverseBuilder` больше не регистрируется в DI
- `TraverseBuilder` стал implementation detail внутри `TraverseCalculationService`
- Удалена публичная зависимость от `ITraverseBuilder`

**Файлы сохранены (внутреннее использование):**
- `Services/TraverseBuilder.cs` - используется внутри TraverseCalculationService
- `Services/ITraverseBuilder.cs` - интерфейс для внутреннего использования

#### Шаг 5.2: ⚠️ Legacy сервисы сохранены

**Сохранены (требуются для работы):**
- `Services/Calculation/TraverseCorrectionService.cs` - используется TraverseCalculationService
- `Services/Calculation/SystemConnectivityService.cs` - используется для связности систем

**Примечание:** Эти сервисы будут мигрированы в Domain/Services в будущем

#### Шаг 5.3: ✅ DI регистрация очищена

**Удалено из `ServiceCollectionExtensions.cs`:**
```csharp
// services.AddSingleton<ITraverseBuilder, TraverseBuilder>();
```

**Оставлено (используется):**
```csharp
services.AddSingleton<ISystemConnectivityService, SystemConnectivityService>();
services.AddSingleton<ITraverseCorrectionService, TraverseCorrectionService>();
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
- **Сессия:** Исправление ошибок + продолжение миграции DDD
- **Статус:** Фаза 3 завершена, Фаза 4 частично

---

## ✅ Итоги миграции на DDD

### Прогресс: ~95% ЗАВЕРШЕНО

| Фаза | Статус | Описание |
|------|--------|----------|
| Фаза 1 | ✅ 100% | Компиляция работает |
| Фаза 2 | ✅ 100% | Чистая архитектура моделей |
| Фаза 3 | ✅ 100% | TraverseCalculationViewModel на DDD |
| Фаза 4 | ✅ 100% | Все ViewModels мигрированы |
| Фаза 5 | ✅ 80% | Legacy код инкапсулирован/удалён |

### Созданные DDD сервисы

#### Application Services
1. `ITraverseCalculationService` - расчёты нивелирных ходов
2. `IClosureCalculationService` - расчёт невязок и допусков
3. `IRunAnnotationService` - аннотация ходов
4. `IDesignCalculationService` - расчёт проектных высот
5. `INoiseGeneratorService` - генерация статистического шума
6. `IProfileStatisticsService` - статистика профилей
7. `IImportValidationService` - валидация импорта

#### Infrastructure Services
1. `INivelorExportService` - экспорт в формат Nivelir
2. `IExportService` - экспорт результатов
3. `IDataParser` - парсинг файлов нивелиров

### Мигрированные ViewModels

| ViewModel | Было (строк) | Стало (строк) | Сокращение |
|-----------|--------------|---------------|------------|
| DataViewModel | 436 | 310 | -29% |
| TraverseCalculationViewModel | 1824 | 1700 | -7% |
| TraverseDesignViewModel | 408 | 310 | -24% |
| TraverseJournalViewModel | 413 | 413 | 0% (уже DDD) |
| DataGeneratorViewModel | 842 | 690 | -18% |

### Оставшаяся работа (5%)

1. Миграция `TraverseCorrectionService` → `Domain/Services`
2. Миграция `SystemConnectivityService` → `Domain/Services`
3. Полный перенос логики `TraverseBuilder` в Application слой (опционально)

**Текущая ветка:** `claude/complete-ddd-migration-FTStU`
**Статус:** Готово к коммиту и финализации
