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

### Фаза 2: Консолидация Models

**Цель:** Убрать путаницу между `Models/` и `Presentation/Models/`

Оставшиеся файлы в `Models/`:
- `MeasurementRecord.cs` - КЛЮЧЕВОЙ, используется везде для импорта данных
- `GeneratedMeasurement.cs` - только для DataGeneratorViewModel
- `ProfileStatistics.cs` - для сервисов статистики
- `ValidationResult.cs` - для валидации

**Решение:** Оставить как есть, это "входные" модели данных. UI-модели в `Presentation/Models/` - это "выходные" для отображения.

### Фаза 3: Миграция TraverseCalculationViewModel (ГЛАВНОЕ!)

**Файл:** `Presentation/ViewModels/TraverseCalculationViewModel.cs`
**Размер:** 1824 строки - это GOD FILE!

**Что внутри:**
- Загрузка и хранение MeasurementRecord[]
- Построение TraverseRow[] из измерений
- Расчёт высот и превышений
- Уравнивание ходов
- Управление системами (TraverseSystem)
- Управление общими точками (SharedPoints)
- Сохранение/восстановление состояния

**Стратегия рефакторинга:**

1. **Извлечь в Application/Services:**
   ```
   ITraverseCalculationService
   ├── BuildTraverseRows(records) → TraverseRow[]
   ├── CalculateHeights(rows) → void
   ├── ApplyCorrections(rows, closure) → void
   └── GetLineSummaries(rows) → LineSummary[]
   ```

2. **Создать Commands:**
   ```
   LoadMeasurementsCommand → загрузка из файла
   CalculateCommand → пересчёт высот
   ApplyCorrectionCommand → применение поправок
   ```

3. **Создать Queries:**
   ```
   GetTraverseRowsQuery → получение строк для UI
   GetLineSummariesQuery → получение сводок по ходам
   ```

4. **ViewModel станет тонким:**
   ```csharp
   public class TraverseCalculationViewModel : ViewModelBase
   {
       // Только:
       // - ObservableCollection для UI binding
       // - Commands для кнопок
       // - Делегирование в Application Services
   }
   ```

### Фаза 4: Миграция остальных ViewModels

| ViewModel | Строк | Сложность | Зависит от |
|-----------|-------|-----------|------------|
| DataViewModel | 436 | Низкая | MeasurementRecord |
| TraverseDesignViewModel | 408 | Средняя | TraverseRow, LineSummary |
| TraverseJournalViewModel | 413 | Средняя | TraverseCalculationViewModel |
| DataGeneratorViewModel | 842 | Средняя | GeneratedMeasurement |
| NetworkViewModel | 143 | ✅ Готов | Использует DDD |

### Фаза 5: Удаление Legacy

После миграции ViewModels можно удалить:
- `Services/TraverseBuilder.cs` → заменён на Application services
- `Services/Calculation/*` → заменён на Domain/Application
- `Presentation/Models/TraverseSystem.cs` → использовать Domain версию + маппинг

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
