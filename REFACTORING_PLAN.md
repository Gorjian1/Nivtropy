# План рефакторинга Nivtropy

## Обзор

Этот документ содержит пошаговый план исправления проблем, выявленных в ходе анализа кода.

---

## Фаза 1: Критические исправления

### 1.1 Разбиение TraverseCalculationViewModel (1812 строк)

**Проблема:** God Class с множеством ответственностей.

**Решение:** Извлечь 4 менеджера:

#### 1.1.1 Создать `BenchmarkManager.cs`
```
Файл: /ViewModel/Managers/BenchmarkManager.cs
Ответственность: Управление реперами (известными высотами)

Переносимые члены из TraverseCalculationViewModel:
- _benchmarks : ObservableCollection<BenchmarkItem>
- _benchmarkSystems : Dictionary<string, string>
- AddBenchmarkCommand : ICommand
- RemoveBenchmark() : void
- UpdateBenchmarks() : void
- GetBenchmarksForSystem() : IEnumerable<BenchmarkItem>
- SetKnownHeightForPoint() : void

Зависимости:
- DataViewModel (для доступа к известным высотам)
- Событие BenchmarksChanged для уведомления ViewModel
```

#### 1.1.2 Создать `SharedPointsManager.cs`
```
Файл: /ViewModel/Managers/SharedPointsManager.cs
Ответственность: Управление общими точками между ходами

Переносимые члены:
- _sharedPoints : ObservableCollection<SharedPointLinkItem>
- _sharedPointLookup : Dictionary<string, SharedPointLinkItem>
- _sharedPointsByRun : Dictionary<int, List<string>>
- UpdateSharedPoints() : void
- GetSharedPointsForRun() : List<string>
- ToggleSharedPoint() : void

Зависимости:
- DataViewModel
- Событие SharedPointsChanged
```

#### 1.1.3 Создать `TraverseSystemsManager.cs`
```
Файл: /ViewModel/Managers/TraverseSystemsManager.cs
Ответственность: Управление системами ходов

Переносимые члены:
- _systems : ObservableCollection<TraverseSystem>
- _selectedSystem : TraverseSystem?
- CreateSystem() : TraverseSystem
- DeleteSystem() : void
- RenameSystem() : void
- MoveRunToSystem() : void
- GetSystemRuns() : IEnumerable<LineSummary>

Зависимости:
- DataViewModel
- Событие SystemsChanged
```

#### 1.1.4 Создать `TraverseCalculationService.cs`
```
Файл: /Services/Calculation/TraverseCalculationService.cs
Интерфейс: /Services/Calculation/ITraverseCalculationService.cs
Ответственность: Чистая бизнес-логика расчётов

Переносимые методы:
- CalculateHeights() : void
- DistributeClosure() : void
- CalculateClosure() : double?
- CalculateAllowableClosure() : double?
- ApplyCorrections() : void

Это должен быть stateless сервис, принимающий данные как параметры.
```

**Результат:** TraverseCalculationViewModel уменьшится до ~400-500 строк.

---

### 1.2 Улучшение обработки ошибок

#### 1.2.1 Добавить интерфейс логирования
```
Файл: /Services/Logging/ILoggerService.cs

public interface ILoggerService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}
```

#### 1.2.2 Реализовать FileLoggerService
```
Файл: /Services/Logging/FileLoggerService.cs
Путь логов: %APPDATA%/Nivtropy/logs/

Формат: [2024-01-15 10:30:45] [ERROR] Message
         Exception: ...
```

#### 1.2.3 Исправить DatParser.cs
```csharp
// Было (строка 340):
catch (Exception ex) { last = ex; }

// Станет:
catch (Exception ex)
{
    _logger.LogWarning($"Не удалось прочитать файл в кодировке {encName}: {ex.Message}");
    last = ex;
}
```

#### 1.2.4 Исправить молчаливые catch
```
Файлы для исправления:
- DatParser.cs: строки 340, 381
- DynamicFormatConverter.cs: строка 44
- TraverseExportService.cs: строка 46
```

---

### 1.3 Удаление устаревшего кода

#### 1.3.1 Удалить TraverseBuilder.BuildStatic()
```
Файл: /Service/TraverseBuilder.cs

Удалить:
- [Obsolete] public static List<TraverseRow> BuildStatic(...)

Заменить использование в TraverseCalculationViewModel.cs (строка 621):
// Было:
var items = TraverseBuilder.BuildStatic(records);

// Станет:
var items = _traverseBuilder.Build(records);
```

#### 1.3.2 Инжектировать ITraverseBuilder в ViewModel
```csharp
public TraverseCalculationViewModel(
    DataViewModel dataViewModel,
    SettingsViewModel settingsViewModel,
    ITraverseBuilder traverseBuilder,  // Добавить
    IToleranceService toleranceService)
```

---

## Фаза 2: Устранение дублирования

### 2.1 Создать базовый ViewModelBase

```
Файл: /ViewModel/Base/ViewModelBase.cs

public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected readonly DataViewModel DataViewModel;
    private bool _isUpdating;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected ViewModelBase(DataViewModel dataViewModel)
    {
        DataViewModel = dataViewModel;
        DataViewModel.BeginBatchUpdate += OnBeginBatchUpdate;
        DataViewModel.EndBatchUpdate += OnEndBatchUpdate;
    }

    protected virtual void OnBeginBatchUpdate(object? sender, EventArgs e)
    {
        _isUpdating = true;
    }

    protected virtual void OnEndBatchUpdate(object? sender, EventArgs e)
    {
        _isUpdating = false;
        OnBatchUpdateCompleted();
    }

    protected abstract void OnBatchUpdateCompleted();

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
```

### 2.2 Унаследовать ViewModels от базового класса

```
Файлы для изменения:
- TraverseCalculationViewModel.cs
- TraverseDesignViewModel.cs
- DataGeneratorViewModel.cs
- TraverseJournalViewModel.cs
```

### 2.3 Удалить дублирование CSV экспорта

```
Проблема: Логика экспорта в двух местах:
- TraverseCalculationViewModel.cs (строки 421-506)
- TraverseExportService.cs (строки 17-117)

Решение:
1. Оставить только TraverseExportService
2. В ViewModel вызывать сервис:

// TraverseCalculationViewModel.cs
private void ExportToCsv()
{
    var dialog = new SaveFileDialog { Filter = "CSV|*.csv" };
    if (dialog.ShowDialog() == true)
    {
        _exportService.ExportToCsv(_rows, dialog.FileName);
    }
}
```

---

## Фаза 3: Улучшение архитектуры

### 3.1 Убрать зависимость ViewModel от UI

#### 3.1.1 Создать IDialogService
```
Файл: /Services/Dialog/IDialogService.cs

public interface IDialogService
{
    void ShowMessage(string message, string title = "");
    void ShowError(string message, string title = "Ошибка");
    bool ShowConfirmation(string message, string title = "Подтверждение");
    string? ShowOpenFileDialog(string filter);
    string? ShowSaveFileDialog(string filter, string defaultFileName = "");
}
```

#### 3.1.2 Реализовать WpfDialogService
```
Файл: /Services/Dialog/WpfDialogService.cs

Использует:
- System.Windows.MessageBox
- Microsoft.Win32.OpenFileDialog
- Microsoft.Win32.SaveFileDialog
```

#### 3.1.3 Инжектировать в ViewModels
```csharp
// Было:
System.Windows.MessageBox.Show("Экспорт завершен");

// Станет:
_dialogService.ShowMessage("Экспорт завершен");
```

### 3.2 Заменить Magic Strings на константы

```
Файл: /Constants/LineMarkers.cs

public static class LineMarkers
{
    public const string StartLine = "Start-Line";
    public const string EndLine = "End-Line";
    public const string ContLine = "Cont-Line";
}
```

### 3.3 Создать интерфейс для общих свойств строк

```
Файл: /Models/Interfaces/ITraverseDataRow.cs

public interface ITraverseDataRow
{
    string? LineName { get; }
    double? DeltaH { get; }
    double? Closure { get; }
    int StationCount { get; }
}

// Реализовать в:
- TraverseRow : ITraverseDataRow
- JournalRow : ITraverseDataRow
```

---

## Фаза 4: Оптимизация производительности

### 4.1 Кэширование результатов TraverseBuilder

```
Файл: /Services/TraverseBuilder.cs

private readonly Dictionary<int, List<TraverseRow>> _cache = new();

public List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null)
{
    var hash = ComputeHash(records, run);
    if (_cache.TryGetValue(hash, out var cached))
        return cached;

    var result = BuildInternal(records, run);
    _cache[hash] = result;
    return result;
}

public void InvalidateCache() => _cache.Clear();
```

### 4.2 Инкрементальные обновления

```
Файл: /ViewModel/TraverseCalculationViewModel.cs

// Вместо полной перестройки при изменении одной высоты:
private void OnKnownHeightChanged(string pointCode, double newHeight)
{
    // Найти затронутые ходы
    var affectedRuns = FindRunsContainingPoint(pointCode);

    // Пересчитать только их
    foreach (var run in affectedRuns)
    {
        RecalculateRun(run);
    }
}
```

### 4.3 Асинхронные вычисления

```csharp
// Было:
private void UpdateRows()
{
    var items = _traverseBuilder.Build(records);
    // ... тяжёлые вычисления
}

// Станет:
private async Task UpdateRowsAsync()
{
    IsCalculating = true;
    try
    {
        var items = await Task.Run(() => _traverseBuilder.Build(records));
        // ... тяжёлые вычисления в фоне

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Обновление UI коллекций
            _rows.Clear();
            foreach (var item in items)
                _rows.Add(item);
        });
    }
    finally
    {
        IsCalculating = false;
    }
}
```

### 4.4 Оптимизация LINQ

```csharp
// Было (множественные проходы):
var groups = items.GroupBy(r => r.LineName).ToList();
var firstGroup = groups.First();
var count = groups.Count();

// Станет (один проход):
var groupsDict = items.GroupBy(r => r.LineName)
    .ToDictionary(g => g.Key, g => g.ToList());
```

---

## Фаза 5: Тестирование и документация

### 5.1 Добавить проект unit-тестов

```
Создать: Nivtropy.Tests/
Фреймворк: xUnit + Moq

Тесты для:
- TraverseBuilder.Build()
- ProfileStatisticsService.CalculateStatistics()
- ToleranceService.CalculateTolerance()
- DatParser.Parse()
```

### 5.2 Тесты для менеджеров

```
Файлы:
- BenchmarkManagerTests.cs
- SharedPointsManagerTests.cs
- TraverseSystemsManagerTests.cs
```

### 5.3 Обновить документацию

```
Обновить ARCHITECTURE.md:
- Добавить диаграмму новой структуры
- Описать менеджеры и их ответственности
- Добавить примеры использования сервисов
```

---

## Порядок выполнения

### Этап 1 (Критический) - 1-2 дня
- [ ] 1.1 Разбить TraverseCalculationViewModel
- [ ] 1.2 Добавить логирование ошибок
- [ ] 1.3 Удалить устаревший код

### Этап 2 (Важный) - 1 день
- [ ] 2.1 Создать ViewModelBase
- [ ] 2.2 Унаследовать ViewModels
- [ ] 2.3 Устранить дублирование CSV

### Этап 3 (Архитектурный) - 1 день
- [ ] 3.1 Создать IDialogService
- [ ] 3.2 Заменить magic strings
- [ ] 3.3 Создать ITraverseDataRow

### Этап 4 (Производительность) - 1-2 дня
- [ ] 4.1 Кэширование TraverseBuilder
- [ ] 4.2 Инкрементальные обновления
- [ ] 4.3 Асинхронные вычисления
- [ ] 4.4 Оптимизация LINQ

### Этап 5 (Качество) - 1 день
- [ ] 5.1 Создать проект тестов
- [ ] 5.2 Написать базовые тесты
- [ ] 5.3 Обновить документацию

---

## Новая структура после рефакторинга

```
Nivtropy/
├── Constants/
│   └── LineMarkers.cs
├── Models/
│   ├── Interfaces/
│   │   └── ITraverseDataRow.cs
│   └── ... (существующие модели)
├── Services/
│   ├── Calculation/
│   │   ├── ITraverseCalculationService.cs
│   │   └── TraverseCalculationService.cs
│   ├── Dialog/
│   │   ├── IDialogService.cs
│   │   └── WpfDialogService.cs
│   ├── Logging/
│   │   ├── ILoggerService.cs
│   │   └── FileLoggerService.cs
│   └── ... (существующие сервисы)
├── ViewModel/
│   ├── Base/
│   │   └── ViewModelBase.cs
│   ├── Managers/
│   │   ├── BenchmarkManager.cs
│   │   ├── SharedPointsManager.cs
│   │   └── TraverseSystemsManager.cs
│   └── ... (существующие ViewModels)
└── View/
    └── ... (без изменений)
```

---

## Метрики успеха

| Метрика | До | После |
|---------|-----|-------|
| TraverseCalculationViewModel | 1812 строк | ~500 строк |
| Дублирование кода | ~15% | <5% |
| Покрытие тестами | 0% | >50% |
| Молчаливых catch | 4 | 0 |
| Magic strings | ~10 | 0 |

---

*Последнее обновление: 2024*
