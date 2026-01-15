# Nivtropy: План миграции к графовой архитектуре и DDD

## Содержание
1. [Обзор текущей архитектуры](#1-обзор-текущей-архитектуры)
2. [Целевая архитектура](#2-целевая-архитектура)
3. [План миграции](#3-план-миграции)
4. [Этап 1: Value Objects](#этап-1-value-objects)
5. [Этап 2: Domain Entities](#этап-2-domain-entities)
6. [Этап 3: LevelingNetwork (граф)](#этап-3-levelingnetwork-граф)
7. [Этап 4: Domain Services](#этап-4-domain-services)
8. [Этап 5: Application Layer](#этап-5-application-layer)
9. [Этап 6: Адаптеры и миграция](#этап-6-адаптеры-и-миграция)
10. [Этап 7: Рефакторинг ViewModels](#этап-7-рефакторинг-viewmodels)

---

## 1. Обзор текущей архитектуры

### Текущая структура данных (плоская/табличная)

```
LineSummary[] (ходы как список)
    └── TraverseRow[] (станции как список)
            ├── BackCode: string     ─┐
            ├── ForeCode: string     ─┤── Точки как строки, не сущности
            ├── Rb_m: double         ─┤── Измерения как примитивы
            └── Rf_m: double         ─┘
```

### Проблемы текущей архитектуры

| Проблема | Описание |
|----------|----------|
| **Anemic Domain Model** | Модели содержат только данные, логика в сервисах |
| **Точки как строки** | `BackCode`/`ForeCode` - строки, а не сущности |
| **Нет графовой структуры** | Связи между ходами ищутся динамически |
| **Дублирование** | SharedPointsManager дублирует логику поиска общих точек |
| **Примитивы везде** | Высоты, расстояния - просто `double` без типизации |
| **UI в Domain** | `INotifyPropertyChanged` в моделях данных |

### Текущий граф зависимостей

```
View (XAML)
    ↓
ViewModel (MainViewModel, TraverseCalculationViewModel, ...)
    ↓
Services (TraverseBuilder, TraverseCalculationService, ...)
    ↓
Models (TraverseRow, LineSummary, MeasurementRecord, ...)
```

---

## 2. Целевая архитектура

### Графовая структура данных

```
LevelingNetwork (граф)
    │
    ├── Points: Dictionary<PointCode, Point>
    │       │
    │       └── Point (узел графа)
    │               ├── Code: PointCode
    │               ├── Height: Height
    │               ├── Type: PointType
    │               ├── OutgoingObservations: List<Observation>
    │               └── IncomingObservations: List<Observation>
    │
    └── Runs: List<Run>
            │
            └── Run (агрегат)
                    └── Observations: List<Observation>
                            │
                            └── Observation (ребро графа)
                                    ├── From: Point ──┐
                                    ├── To: Point ────┤── Граф!
                                    ├── BackReading   │
                                    └── ForeReading   │
```

### Преимущества графовой модели

1. **Общие точки** - это просто точки с `degree > 2`
2. **Связность** - проверяется обходом графа (BFS/DFS)
3. **Распространение высот** - естественный обход от реперов
4. **Циклы** - обнаруживаются алгоритмами графа
5. **Пути** - находятся Dijkstra/BFS

### DDD структура папок (целевая)

```
Nivtropy/
├── Domain/                          # Ядро (0 внешних зависимостей)
│   ├── Model/
│   │   ├── Point.cs                 # Entity - узел графа
│   │   ├── Observation.cs           # Entity - ребро графа
│   │   ├── Run.cs                   # Aggregate Root - ход
│   │   ├── LevelingNetwork.cs       # Aggregate Root - сеть
│   │   └── TraverseSystem.cs        # Entity - система ходов
│   ├── ValueObjects/
│   │   ├── PointCode.cs
│   │   ├── Height.cs
│   │   ├── Distance.cs
│   │   ├── Closure.cs
│   │   └── Reading.cs
│   ├── Events/
│   │   ├── IDomainEvent.cs
│   │   ├── HeightCalculatedEvent.cs
│   │   └── ClosureDistributedEvent.cs
│   ├── Services/                    # Domain Services (чистая логика)
│   │   ├── IHeightPropagator.cs
│   │   ├── HeightPropagator.cs
│   │   ├── IClosureCalculator.cs
│   │   ├── ClosureCalculator.cs
│   │   ├── IAdjustmentService.cs
│   │   └── LeastSquaresAdjustment.cs
│   └── Repositories/                # Интерфейсы репозиториев
│       ├── INetworkRepository.cs
│       └── IRunRepository.cs
│
├── Application/                     # Use Cases, Orchestration
│   ├── Commands/
│   │   ├── ImportMeasurementsCommand.cs
│   │   ├── CalculateHeightsCommand.cs
│   │   ├── AdjustNetworkCommand.cs
│   │   └── Handlers/
│   ├── Queries/
│   │   ├── GetNetworkStatisticsQuery.cs
│   │   ├── GetRunDetailsQuery.cs
│   │   ├── GetAnomaliesQuery.cs
│   │   └── Handlers/
│   ├── DTOs/
│   │   ├── NetworkSummaryDto.cs
│   │   ├── RunStatisticsDto.cs
│   │   ├── ObservationDto.cs
│   │   └── AnomalyDto.cs
│   └── Mappers/
│       └── NetworkMapper.cs
│
├── Infrastructure/                  # Внешние зависимости
│   ├── Persistence/
│   │   ├── InMemoryNetworkRepository.cs
│   │   └── JsonNetworkRepository.cs
│   ├── Parsers/
│   │   ├── IFileParser.cs
│   │   ├── DatFileParser.cs
│   │   ├── TrimbleDiniParser.cs
│   │   └── ForFormatParser.cs
│   ├── Export/
│   │   ├── IExporter.cs
│   │   └── CsvExporter.cs
│   └── Logging/
│       └── FileLogger.cs
│
├── Presentation/                    # UI Layer (WPF)
│   ├── ViewModels/
│   │   ├── Base/
│   │   ├── MainViewModel.cs
│   │   ├── NetworkViewModel.cs      # Новый - для работы с графом
│   │   ├── RunViewModel.cs
│   │   └── ...
│   ├── Views/
│   │   └── ...
│   ├── Converters/
│   ├── Services/                    # UI-specific services
│   │   ├── IDialogService.cs
│   │   ├── IVisualizationService.cs
│   │   └── ...
│   └── Resources/
│
├── Legacy/                          # Временно - старый код
│   ├── Models/
│   ├── Services/
│   └── Adapters/                    # Адаптеры старое ↔ новое
│
└── Tests/
    ├── Domain.Tests/
    ├── Application.Tests/
    └── Infrastructure.Tests/
```

### Целевой граф зависимостей

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation                          │
│              (ViewModels, Views, UI Services)            │
└────────────────────────┬────────────────────────────────┘
                         │ uses
┌────────────────────────▼────────────────────────────────┐
│                     Application                          │
│           (Commands, Queries, Handlers, DTOs)            │
└────────────────────────┬────────────────────────────────┘
                         │ uses
┌────────────────────────▼────────────────────────────────┐
│                       Domain                             │
│    (Entities, Value Objects, Domain Services, Events)    │
└────────────────────────┬────────────────────────────────┘
                         │ interfaces implemented by
┌────────────────────────▼────────────────────────────────┐
│                   Infrastructure                         │
│         (Repositories, Parsers, Export, Logging)         │
└─────────────────────────────────────────────────────────┘
```

**Правило:** Зависимости направлены только внутрь. Domain не знает о других слоях.

---

## 3. План миграции

### Принципы миграции

1. **Параллельное существование** - старый и новый код работают одновременно
2. **Адаптеры** - связывают старый код с новым
3. **Постепенная замена** - заменяем компонент за компонентом
4. **Тесты** - каждый этап покрыт тестами
5. **Без поломок** - приложение работает на каждом этапе

### Общий план

| Этап | Описание | Зависит от |
|------|----------|------------|
| 1 | Value Objects | - |
| 2 | Domain Entities (Point, Observation, Run) | 1 |
| 3 | LevelingNetwork (граф) | 2 |
| 4 | Domain Services | 3 |
| 5 | Application Layer (Commands/Queries) | 4 |
| 6 | Адаптеры Legacy ↔ New | 5 |
| 7 | Рефакторинг ViewModels | 6 |
| 8 | Удаление Legacy кода | 7 |

### Маппинг старых классов на новые

| Старый класс | Новый класс | Слой |
|--------------|-------------|------|
| `TraverseRow` | `Observation` | Domain/Model |
| `LineSummary` | `Run` | Domain/Model |
| `MeasurementRecord` | `RawMeasurement` (DTO) | Infrastructure |
| `PointItem` | `Point` | Domain/Model |
| `TraverseSystem` | `TraverseSystem` | Domain/Model |
| `TraverseBuilder` | `NetworkBuilder` | Application |
| `TraverseCalculationService` | `HeightPropagator` | Domain/Services |
| `TraverseCorrectionService` | `AdjustmentService` | Domain/Services |
| `ProfileStatisticsService` | `StatisticsQueryHandler` | Application |
| `BenchmarkManager` | Часть `LevelingNetwork` | Domain |
| `SharedPointsManager` | Не нужен (граф) | - |

---

## Этап 1: Value Objects

### Цель
Создать типизированные Value Objects для замены примитивов.

### Файлы для создания

```
Domain/
└── ValueObjects/
    ├── PointCode.cs
    ├── Height.cs
    ├── Distance.cs
    ├── Reading.cs
    └── Closure.cs
```

### 1.1 PointCode.cs

```csharp
namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Код точки нивелирования (репер, связующая точка).
/// Immutable value object с нормализацией.
/// </summary>
public readonly record struct PointCode : IComparable<PointCode>
{
    public string Value { get; }

    public PointCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Point code cannot be empty", nameof(value));

        // Нормализация: trim + uppercase
        Value = value.Trim().ToUpperInvariant();
    }

    // Implicit conversion для совместимости со старым кодом
    public static implicit operator string(PointCode code) => code.Value;
    public static explicit operator PointCode(string value) => new(value);

    public override string ToString() => Value;

    public int CompareTo(PointCode other) =>
        string.Compare(Value, other.Value, StringComparison.Ordinal);

    // Для использования в Dictionary
    public override int GetHashCode() => Value.GetHashCode();
}
```

### 1.2 Height.cs

```csharp
namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Высота точки в метрах. Может быть известной или неизвестной.
/// </summary>
public readonly record struct Height : IComparable<Height>
{
    public double Value { get; }
    public bool IsKnown { get; }

    private Height(double value, bool isKnown)
    {
        Value = isKnown ? value : 0;
        IsKnown = isKnown;
    }

    /// <summary>Создать известную высоту</summary>
    public static Height Known(double meters) => new(meters, true);

    /// <summary>Неизвестная высота</summary>
    public static Height Unknown => new(0, false);

    /// <summary>Создать высоту из nullable</summary>
    public static Height FromNullable(double? meters) =>
        meters.HasValue ? Known(meters.Value) : Unknown;

    // Арифметика (только для известных высот)
    public static Height operator +(Height a, double delta) =>
        a.IsKnown ? Known(a.Value + delta) : Unknown;

    public static Height operator -(Height a, double delta) =>
        a.IsKnown ? Known(a.Value - delta) : Unknown;

    public static double operator -(Height a, Height b)
    {
        if (!a.IsKnown || !b.IsKnown)
            throw new InvalidOperationException("Cannot subtract unknown heights");
        return a.Value - b.Value;
    }

    // Форматирование
    public override string ToString() =>
        IsKnown ? $"{Value:F4} м" : "—";

    public string ToString(string format) =>
        IsKnown ? Value.ToString(format) : "—";

    public int CompareTo(Height other)
    {
        if (!IsKnown && !other.IsKnown) return 0;
        if (!IsKnown) return -1;
        if (!other.IsKnown) return 1;
        return Value.CompareTo(other.Value);
    }
}
```

### 1.3 Distance.cs

```csharp
namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Расстояние (горизонтальное проложение) в метрах.
/// Всегда неотрицательное.
/// </summary>
public readonly record struct Distance : IComparable<Distance>
{
    public double Meters { get; }

    public Distance(double meters)
    {
        if (meters < 0)
            throw new ArgumentOutOfRangeException(nameof(meters), "Distance cannot be negative");
        Meters = meters;
    }

    // Конверсии
    public double Kilometers => Meters / 1000.0;
    public double Centimeters => Meters * 100.0;

    // Фабричные методы
    public static Distance FromMeters(double m) => new(m);
    public static Distance FromKilometers(double km) => new(km * 1000);
    public static Distance Zero => new(0);

    // Арифметика
    public static Distance operator +(Distance a, Distance b) =>
        new(a.Meters + b.Meters);

    public static Distance operator -(Distance a, Distance b) =>
        new(Math.Max(0, a.Meters - b.Meters));

    public static Distance operator *(Distance d, double factor) =>
        new(d.Meters * factor);

    public static Distance operator /(Distance d, double divisor) =>
        new(d.Meters / divisor);

    // Сравнение
    public static bool operator >(Distance a, Distance b) => a.Meters > b.Meters;
    public static bool operator <(Distance a, Distance b) => a.Meters < b.Meters;
    public static bool operator >=(Distance a, Distance b) => a.Meters >= b.Meters;
    public static bool operator <=(Distance a, Distance b) => a.Meters <= b.Meters;

    public int CompareTo(Distance other) => Meters.CompareTo(other.Meters);

    public override string ToString() => $"{Meters:F3} м";
}
```

### 1.4 Reading.cs

```csharp
namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Отсчёт по рейке (в метрах).
/// </summary>
public readonly record struct Reading
{
    public double Meters { get; }

    public Reading(double meters)
    {
        // Отсчёт может быть любым (положительным или отрицательным для инвар-реек)
        Meters = meters;
    }

    public double Millimeters => Meters * 1000.0;

    public static Reading FromMeters(double m) => new(m);
    public static Reading FromMillimeters(double mm) => new(mm / 1000.0);

    // Превышение = задний отсчёт - передний отсчёт
    public static double operator -(Reading back, Reading fore) =>
        back.Meters - fore.Meters;

    public override string ToString() => $"{Meters:F5}";
}
```

### 1.5 Closure.cs

```csharp
namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Невязка хода с допуском.
/// </summary>
public readonly record struct Closure
{
    /// <summary>Невязка в миллиметрах</summary>
    public double ValueMm { get; }

    /// <summary>Допустимая невязка в миллиметрах</summary>
    public double ToleranceMm { get; }

    public Closure(double valueMm, double toleranceMm)
    {
        ValueMm = valueMm;
        ToleranceMm = Math.Abs(toleranceMm);
    }

    /// <summary>Невязка в пределах допуска</summary>
    public bool IsWithinTolerance => Math.Abs(ValueMm) <= ToleranceMm;

    /// <summary>Отношение невязки к допуску (для визуализации)</summary>
    public double Ratio => ToleranceMm > 0 ? ValueMm / ToleranceMm : 0;

    /// <summary>Абсолютное значение невязки</summary>
    public double AbsoluteValueMm => Math.Abs(ValueMm);

    /// <summary>Превышение допуска в мм (отрицательное если в допуске)</summary>
    public double ExcessMm => AbsoluteValueMm - ToleranceMm;

    public override string ToString() =>
        $"{ValueMm:+0.0;-0.0} мм (доп. ±{ToleranceMm:F1} мм) {(IsWithinTolerance ? "✓" : "✗")}";
}
```

### Тесты для Value Objects

Создай файл `Tests/Domain.Tests/ValueObjects/HeightTests.cs`:

```csharp
namespace Nivtropy.Tests.Domain.ValueObjects;

public class HeightTests
{
    [Fact]
    public void Known_CreatesKnownHeight()
    {
        var height = Height.Known(100.5);

        Assert.True(height.IsKnown);
        Assert.Equal(100.5, height.Value);
    }

    [Fact]
    public void Unknown_CreatesUnknownHeight()
    {
        var height = Height.Unknown;

        Assert.False(height.IsKnown);
    }

    [Fact]
    public void Addition_WithKnownHeight_ReturnsCorrectResult()
    {
        var height = Height.Known(100.0);
        var result = height + 1.5;

        Assert.Equal(101.5, result.Value);
    }

    [Fact]
    public void Addition_WithUnknownHeight_ReturnsUnknown()
    {
        var height = Height.Unknown;
        var result = height + 1.5;

        Assert.False(result.IsKnown);
    }

    [Fact]
    public void Subtraction_BetweenKnownHeights_ReturnsDifference()
    {
        var h1 = Height.Known(100.0);
        var h2 = Height.Known(99.5);

        Assert.Equal(0.5, h1 - h2);
    }
}
```

---

## Этап 2: Domain Entities

### Цель
Создать основные сущности доменной модели как узлы и рёбра графа.

### Файлы для создания

```
Domain/
└── Model/
    ├── PointType.cs
    ├── Point.cs
    ├── Observation.cs
    └── Run.cs
```

### 2.1 PointType.cs

```csharp
namespace Nivtropy.Domain.Model;

/// <summary>
/// Тип точки нивелирования
/// </summary>
public enum PointType
{
    /// <summary>Репер - точка с известной высотой</summary>
    Benchmark,

    /// <summary>Связующая точка - используется для связи станций</summary>
    TurningPoint,

    /// <summary>Промежуточная точка</summary>
    Intermediate
}
```

### 2.2 Point.cs

```csharp
namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Точка нивелирования - узел графа.
/// Содержит ссылки на входящие и исходящие наблюдения (рёбра).
/// </summary>
public class Point : IEquatable<Point>
{
    private readonly List<Observation> _outgoingObservations = new();
    private readonly List<Observation> _incomingObservations = new();

    /// <summary>Уникальный код точки</summary>
    public PointCode Code { get; }

    /// <summary>Высота точки</summary>
    public Height Height { get; private set; }

    /// <summary>Тип точки</summary>
    public PointType Type { get; private set; }

    /// <summary>Исходящие наблюдения (эта точка - задняя)</summary>
    public IReadOnlyList<Observation> OutgoingObservations => _outgoingObservations;

    /// <summary>Входящие наблюдения (эта точка - передняя)</summary>
    public IReadOnlyList<Observation> IncomingObservations => _incomingObservations;

    /// <summary>Все наблюдения, связанные с точкой</summary>
    public IEnumerable<Observation> AllObservations =>
        _outgoingObservations.Concat(_incomingObservations);

    /// <summary>Степень вершины (количество связей)</summary>
    public int Degree => _outgoingObservations.Count + _incomingObservations.Count;

    /// <summary>Является ли точка общей (связывает несколько ходов)</summary>
    public bool IsSharedPoint => AllObservations
        .Select(o => o.Run)
        .Distinct()
        .Count() > 1;

    /// <summary>Соседние точки в графе</summary>
    public IEnumerable<Point> AdjacentPoints =>
        _outgoingObservations.Select(o => o.To)
        .Concat(_incomingObservations.Select(o => o.From))
        .Distinct();

    /// <summary>Ходы, проходящие через эту точку</summary>
    public IEnumerable<Run> ConnectedRuns =>
        AllObservations.Select(o => o.Run).Distinct();

    public Point(PointCode code, PointType type = PointType.TurningPoint)
    {
        Code = code;
        Type = type;
        Height = Height.Unknown;
    }

    /// <summary>Установить известную высоту (для реперов)</summary>
    public void SetKnownHeight(Height height)
    {
        if (!height.IsKnown)
            throw new ArgumentException("Height must be known", nameof(height));

        Height = height;
        Type = PointType.Benchmark;
    }

    /// <summary>Установить вычисленную высоту</summary>
    public void SetCalculatedHeight(Height height)
    {
        if (Type == PointType.Benchmark)
            return; // Не перезаписываем известную высоту

        Height = height;
    }

    /// <summary>Сбросить вычисленную высоту</summary>
    public void ResetCalculatedHeight()
    {
        if (Type != PointType.Benchmark)
        {
            Height = Height.Unknown;
        }
    }

    /// <summary>Пометить как репер</summary>
    public void MarkAsBenchmark()
    {
        Type = PointType.Benchmark;
    }

    // Внутренние методы для добавления связей (вызываются из LevelingNetwork)
    internal void AddOutgoingObservation(Observation observation)
    {
        if (observation.From != this)
            throw new ArgumentException("Observation.From must be this point");
        _outgoingObservations.Add(observation);
    }

    internal void AddIncomingObservation(Observation observation)
    {
        if (observation.To != this)
            throw new ArgumentException("Observation.To must be this point");
        _incomingObservations.Add(observation);
    }

    internal void RemoveObservation(Observation observation)
    {
        _outgoingObservations.Remove(observation);
        _incomingObservations.Remove(observation);
    }

    // Equality по коду точки
    public bool Equals(Point? other) => other != null && Code.Equals(other.Code);
    public override bool Equals(object? obj) => Equals(obj as Point);
    public override int GetHashCode() => Code.GetHashCode();
    public override string ToString() => $"Point({Code}, {Height}, {Type})";
}
```

### 2.3 Observation.cs

```csharp
namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Наблюдение (станция) - ребро графа между двумя точками.
/// Содержит измерения и вычисляемые характеристики.
/// </summary>
public class Observation
{
    /// <summary>Уникальный идентификатор</summary>
    public Guid Id { get; }

    /// <summary>Задняя точка (откуда)</summary>
    public Point From { get; }

    /// <summary>Передняя точка (куда)</summary>
    public Point To { get; }

    /// <summary>Ход, к которому принадлежит наблюдение</summary>
    public Run Run { get; }

    /// <summary>Индекс станции в ходе (1, 2, 3...)</summary>
    public int StationIndex { get; }

    // Измерения
    /// <summary>Отсчёт по задней рейке</summary>
    public Reading BackReading { get; }

    /// <summary>Отсчёт по передней рейке</summary>
    public Reading ForeReading { get; }

    /// <summary>Расстояние до задней точки</summary>
    public Distance BackDistance { get; }

    /// <summary>Расстояние до передней точки</summary>
    public Distance ForeDistance { get; }

    // Вычисляемые свойства
    /// <summary>Измеренное превышение (Back - Fore)</summary>
    public double DeltaH => BackReading - ForeReading;

    /// <summary>Длина станции (сумма плеч)</summary>
    public Distance StationLength => BackDistance + ForeDistance;

    /// <summary>Разность плеч</summary>
    public double ArmDifference => BackDistance.Meters - ForeDistance.Meters;

    // Уравнивание
    /// <summary>Поправка в превышение</summary>
    public double Correction { get; private set; }

    /// <summary>Исправленное превышение</summary>
    public double AdjustedDeltaH => DeltaH + Correction;

    /// <summary>Создать наблюдение</summary>
    public Observation(
        Point from,
        Point to,
        Run run,
        int stationIndex,
        Reading backReading,
        Reading foreReading,
        Distance backDistance,
        Distance foreDistance)
    {
        Id = Guid.NewGuid();
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        Run = run ?? throw new ArgumentNullException(nameof(run));
        StationIndex = stationIndex;
        BackReading = backReading;
        ForeReading = foreReading;
        BackDistance = backDistance;
        ForeDistance = foreDistance;
        Correction = 0;
    }

    /// <summary>Применить поправку</summary>
    public void ApplyCorrection(double correction)
    {
        Correction = correction;
    }

    /// <summary>Сбросить поправку</summary>
    public void ResetCorrection()
    {
        Correction = 0;
    }

    /// <summary>Вычислить высоту передней точки по задней</summary>
    public Height CalculateForeHeight()
    {
        if (!From.Height.IsKnown)
            return Height.Unknown;

        return Height.Known(From.Height.Value - AdjustedDeltaH);
    }

    /// <summary>Вычислить высоту задней точки по передней</summary>
    public Height CalculateBackHeight()
    {
        if (!To.Height.IsKnown)
            return Height.Unknown;

        return Height.Known(To.Height.Value + AdjustedDeltaH);
    }

    public override string ToString() =>
        $"Observation({From.Code} → {To.Code}, ΔH={DeltaH:F4})";
}
```

### 2.4 Run.cs

```csharp
namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Нивелирный ход - Aggregate Root.
/// Упорядоченная последовательность наблюдений (станций).
/// </summary>
public class Run
{
    private readonly List<Observation> _observations = new();

    /// <summary>Уникальный идентификатор</summary>
    public Guid Id { get; }

    /// <summary>Название хода (например "Ход 01")</summary>
    public string Name { get; private set; }

    /// <summary>Оригинальный номер из файла</summary>
    public string? OriginalNumber { get; set; }

    /// <summary>Активен ли ход</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Система, к которой принадлежит ход</summary>
    public TraverseSystem? System { get; internal set; }

    /// <summary>Наблюдения (станции) хода</summary>
    public IReadOnlyList<Observation> Observations => _observations;

    /// <summary>Количество станций</summary>
    public int StationCount => _observations.Count;

    // Граничные точки
    /// <summary>Начальная точка хода</summary>
    public Point? StartPoint => _observations.FirstOrDefault()?.From;

    /// <summary>Конечная точка хода</summary>
    public Point? EndPoint => _observations.LastOrDefault()?.To;

    /// <summary>Все точки хода в порядке прохождения</summary>
    public IEnumerable<Point> Points
    {
        get
        {
            if (_observations.Count == 0)
                yield break;

            yield return _observations[0].From;
            foreach (var obs in _observations)
                yield return obs.To;
        }
    }

    // Агрегированные характеристики
    /// <summary>Общая длина хода</summary>
    public Distance TotalLength => _observations.Aggregate(
        Distance.Zero,
        (sum, obs) => sum + obs.StationLength);

    /// <summary>Сумма превышений</summary>
    public double DeltaHSum => _observations.Sum(o => o.DeltaH);

    /// <summary>Сумма исправленных превышений</summary>
    public double AdjustedDeltaHSum => _observations.Sum(o => o.AdjustedDeltaH);

    /// <summary>Накопленная разность плеч</summary>
    public double AccumulatedArmDifference => _observations.Sum(o => o.ArmDifference);

    // Невязка
    /// <summary>Невязка хода (если замкнут или между реперами)</summary>
    public Closure? Closure { get; private set; }

    /// <summary>Является ли ход замкнутым (начало = конец)</summary>
    public bool IsClosed => StartPoint != null &&
                            EndPoint != null &&
                            StartPoint.Code.Equals(EndPoint.Code);

    /// <summary>Есть ли известные точки на концах</summary>
    public bool HasKnownEndPoints =>
        (StartPoint?.Height.IsKnown ?? false) &&
        (EndPoint?.Height.IsKnown ?? false);

    public Run(string name)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Переименовать ход</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        Name = newName;
    }

    /// <summary>Добавить наблюдение в ход</summary>
    internal void AddObservation(Observation observation)
    {
        if (observation.Run != this)
            throw new ArgumentException("Observation belongs to different run");

        // Проверяем связность: новое наблюдение должно начинаться там, где закончилось предыдущее
        if (_observations.Count > 0)
        {
            var lastTo = _observations[^1].To;
            if (!observation.From.Code.Equals(lastTo.Code))
                throw new InvalidOperationException(
                    $"Observation must start at {lastTo.Code}, but starts at {observation.From.Code}");
        }

        _observations.Add(observation);
    }

    /// <summary>Вычислить невязку</summary>
    public void CalculateClosure(double toleranceMm)
    {
        if (!HasKnownEndPoints)
        {
            Closure = null;
            return;
        }

        // Теоретическое превышение
        var theoretical = StartPoint!.Height - EndPoint!.Height;

        // Измеренное превышение
        var measured = DeltaHSum;

        // Невязка в мм
        var closureMm = (measured - theoretical) * 1000;

        Closure = new Closure(closureMm, toleranceMm);
    }

    /// <summary>Проверить, содержит ли ход точку</summary>
    public bool ContainsPoint(PointCode code) =>
        Points.Any(p => p.Code.Equals(code));

    /// <summary>Получить точку по коду</summary>
    public Point? GetPoint(PointCode code) =>
        Points.FirstOrDefault(p => p.Code.Equals(code));

    /// <summary>Активировать ход</summary>
    public void Activate() => IsActive = true;

    /// <summary>Деактивировать ход</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Сбросить все поправки</summary>
    public void ResetCorrections()
    {
        foreach (var obs in _observations)
            obs.ResetCorrection();
    }

    public override string ToString() =>
        $"Run({Name}, {StationCount} stations, {TotalLength})";
}
```

---

## Этап 3: LevelingNetwork (граф)

### Цель
Создать главный Aggregate Root - нивелирную сеть как граф точек и наблюдений.

### 3.1 LevelingNetwork.cs

```csharp
namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Нивелирная сеть - главный Aggregate Root.
/// Представляет граф точек (узлов) и наблюдений (рёбер).
/// </summary>
public class LevelingNetwork
{
    private readonly Dictionary<PointCode, Point> _points = new();
    private readonly List<Run> _runs = new();
    private readonly List<TraverseSystem> _systems = new();

    /// <summary>Уникальный идентификатор сети</summary>
    public Guid Id { get; }

    /// <summary>Название проекта</summary>
    public string Name { get; set; }

    /// <summary>Дата создания</summary>
    public DateTime CreatedAt { get; }

    // Коллекции
    /// <summary>Все точки сети (узлы графа)</summary>
    public IReadOnlyDictionary<PointCode, Point> Points => _points;

    /// <summary>Все ходы сети</summary>
    public IReadOnlyList<Run> Runs => _runs;

    /// <summary>Системы ходов</summary>
    public IReadOnlyList<TraverseSystem> Systems => _systems;

    // Агрегированные данные
    /// <summary>Реперы (точки с известной высотой)</summary>
    public IEnumerable<Point> Benchmarks =>
        _points.Values.Where(p => p.Type == PointType.Benchmark);

    /// <summary>Общие точки (связывают несколько ходов)</summary>
    public IEnumerable<Point> SharedPoints =>
        _points.Values.Where(p => p.IsSharedPoint);

    /// <summary>Связующие точки</summary>
    public IEnumerable<Point> TurningPoints =>
        _points.Values.Where(p => p.Type == PointType.TurningPoint);

    /// <summary>Все наблюдения сети</summary>
    public IEnumerable<Observation> AllObservations =>
        _runs.SelectMany(r => r.Observations);

    /// <summary>Общее количество станций</summary>
    public int TotalStationCount => _runs.Sum(r => r.StationCount);

    /// <summary>Общая длина всех ходов</summary>
    public Distance TotalLength => _runs.Aggregate(
        Distance.Zero,
        (sum, run) => sum + run.TotalLength);

    public LevelingNetwork(string name = "Новый проект")
    {
        Id = Guid.NewGuid();
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }

    #region Управление точками

    /// <summary>Получить или создать точку</summary>
    public Point GetOrCreatePoint(PointCode code)
    {
        if (!_points.TryGetValue(code, out var point))
        {
            point = new Point(code);
            _points[code] = point;
        }
        return point;
    }

    /// <summary>Получить точку (если существует)</summary>
    public Point? GetPoint(PointCode code) =>
        _points.GetValueOrDefault(code);

    /// <summary>Установить высоту репера</summary>
    public void SetBenchmarkHeight(PointCode code, Height height)
    {
        var point = GetOrCreatePoint(code);
        point.SetKnownHeight(height);
    }

    /// <summary>Сбросить все вычисленные высоты</summary>
    public void ResetCalculatedHeights()
    {
        foreach (var point in _points.Values)
            point.ResetCalculatedHeight();
    }

    #endregion

    #region Управление ходами

    /// <summary>Создать новый ход</summary>
    public Run CreateRun(string name)
    {
        var run = new Run(name);
        _runs.Add(run);
        return run;
    }

    /// <summary>Добавить наблюдение в ход</summary>
    public Observation AddObservation(
        Run run,
        PointCode fromCode,
        PointCode toCode,
        Reading backReading,
        Reading foreReading,
        Distance backDistance,
        Distance foreDistance)
    {
        if (!_runs.Contains(run))
            throw new ArgumentException("Run does not belong to this network");

        var from = GetOrCreatePoint(fromCode);
        var to = GetOrCreatePoint(toCode);

        var observation = new Observation(
            from: from,
            to: to,
            run: run,
            stationIndex: run.StationCount + 1,
            backReading: backReading,
            foreReading: foreReading,
            backDistance: backDistance,
            foreDistance: foreDistance);

        // Добавляем связи в граф
        from.AddOutgoingObservation(observation);
        to.AddIncomingObservation(observation);

        // Добавляем в ход
        run.AddObservation(observation);

        return observation;
    }

    /// <summary>Удалить ход</summary>
    public void RemoveRun(Run run)
    {
        if (!_runs.Remove(run))
            return;

        // Удаляем связи из точек
        foreach (var obs in run.Observations)
        {
            obs.From.RemoveObservation(obs);
            obs.To.RemoveObservation(obs);
        }

        // Удаляем осиротевшие точки (без связей)
        var orphanedPoints = _points.Values
            .Where(p => p.Degree == 0 && p.Type != PointType.Benchmark)
            .Select(p => p.Code)
            .ToList();

        foreach (var code in orphanedPoints)
            _points.Remove(code);
    }

    #endregion

    #region Управление системами

    /// <summary>Создать систему ходов</summary>
    public TraverseSystem CreateSystem(string name)
    {
        var system = new TraverseSystem(name, _systems.Count);
        _systems.Add(system);
        return system;
    }

    /// <summary>Добавить ход в систему</summary>
    public void AddRunToSystem(Run run, TraverseSystem system)
    {
        if (!_systems.Contains(system))
            throw new ArgumentException("System does not belong to this network");

        run.System?.RemoveRun(run);
        system.AddRun(run);
    }

    #endregion

    #region Граф-операции

    /// <summary>Найти все ходы, проходящие через точку</summary>
    public IEnumerable<Run> GetRunsContainingPoint(PointCode code)
    {
        var point = GetPoint(code);
        return point?.ConnectedRuns ?? Enumerable.Empty<Run>();
    }

    /// <summary>Проверить связность сети</summary>
    public bool IsConnected()
    {
        if (_points.Count == 0)
            return true;

        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        queue.Enqueue(_points.Values.First());

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            foreach (var neighbor in current.AdjacentPoints)
            {
                if (!visited.Contains(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return visited.Count == _points.Count;
    }

    /// <summary>Найти компоненты связности</summary>
    public IEnumerable<IReadOnlyList<Point>> FindConnectedComponents()
    {
        var visited = new HashSet<Point>();
        var components = new List<List<Point>>();

        foreach (var startPoint in _points.Values)
        {
            if (visited.Contains(startPoint))
                continue;

            var component = new List<Point>();
            var queue = new Queue<Point>();
            queue.Enqueue(startPoint);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current))
                    continue;

                visited.Add(current);
                component.Add(current);

                foreach (var neighbor in current.AdjacentPoints)
                {
                    if (!visited.Contains(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            components.Add(component);
        }

        return components;
    }

    /// <summary>Найти путь между точками (BFS)</summary>
    public IEnumerable<Observation>? FindPath(PointCode from, PointCode to)
    {
        var startPoint = GetPoint(from);
        var endPoint = GetPoint(to);

        if (startPoint == null || endPoint == null)
            return null;

        if (from.Equals(to))
            return Enumerable.Empty<Observation>();

        var visited = new HashSet<Point>();
        var queue = new Queue<(Point point, List<Observation> path)>();

        queue.Enqueue((startPoint, new List<Observation>()));

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            if (visited.Contains(current))
                continue;

            visited.Add(current);

            // Проверяем исходящие наблюдения
            foreach (var obs in current.OutgoingObservations)
            {
                var newPath = new List<Observation>(path) { obs };

                if (obs.To.Code.Equals(to))
                    return newPath;

                if (!visited.Contains(obs.To))
                    queue.Enqueue((obs.To, newPath));
            }

            // Проверяем входящие наблюдения (обратное направление)
            foreach (var obs in current.IncomingObservations)
            {
                var newPath = new List<Observation>(path) { obs };

                if (obs.From.Code.Equals(to))
                    return newPath;

                if (!visited.Contains(obs.From))
                    queue.Enqueue((obs.From, newPath));
            }
        }

        return null; // Путь не найден
    }

    /// <summary>Найти циклы в сети (замкнутые полигоны)</summary>
    public IEnumerable<IReadOnlyList<Point>> FindCycles()
    {
        // Simplified DFS cycle detection
        var cycles = new List<List<Point>>();
        var visited = new HashSet<Point>();
        var recursionStack = new List<Point>();

        void DFS(Point current, Point? parent)
        {
            visited.Add(current);
            recursionStack.Add(current);

            foreach (var neighbor in current.AdjacentPoints)
            {
                if (!visited.Contains(neighbor))
                {
                    DFS(neighbor, current);
                }
                else if (neighbor != parent && recursionStack.Contains(neighbor))
                {
                    // Найден цикл
                    var cycleStart = recursionStack.IndexOf(neighbor);
                    var cycle = recursionStack.Skip(cycleStart).ToList();
                    cycles.Add(cycle);
                }
            }

            recursionStack.Remove(current);
        }

        foreach (var point in _points.Values)
        {
            if (!visited.Contains(point))
                DFS(point, null);
        }

        return cycles;
    }

    /// <summary>Топологическая сортировка от реперов (для распространения высот)</summary>
    public IEnumerable<Point> TopologicalSortFromBenchmarks()
    {
        var result = new List<Point>();
        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        // Начинаем с реперов
        foreach (var benchmark in Benchmarks)
        {
            queue.Enqueue(benchmark);
            visited.Add(benchmark);
            result.Add(benchmark);
        }

        // BFS обход
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in current.AdjacentPoints)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    result.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return result;
    }

    #endregion

    public override string ToString() =>
        $"LevelingNetwork({Name}, {_points.Count} points, {_runs.Count} runs)";
}
```

### 3.2 TraverseSystem.cs

```csharp
namespace Nivtropy.Domain.Model;

/// <summary>
/// Система ходов - логическая группировка ходов.
/// </summary>
public class TraverseSystem
{
    private readonly List<Run> _runs = new();

    public Guid Id { get; }
    public string Name { get; private set; }
    public int Order { get; set; }

    public IReadOnlyList<Run> Runs => _runs;
    public int RunCount => _runs.Count;

    public string DisplayName => $"{Name} ({RunCount})";

    public TraverseSystem(string name, int order = 0)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Order = order;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        Name = newName;
    }

    internal void AddRun(Run run)
    {
        if (!_runs.Contains(run))
        {
            _runs.Add(run);
            run.System = this;
        }
    }

    internal void RemoveRun(Run run)
    {
        if (_runs.Remove(run))
        {
            run.System = null;
        }
    }

    public override string ToString() => DisplayName;
}
```

---

## Этап 4: Domain Services

### Цель
Создать доменные сервисы для расчёта высот и уравнивания.

### Файлы для создания

```
Domain/
└── Services/
    ├── IHeightPropagator.cs
    ├── HeightPropagator.cs
    ├── IClosureDistributor.cs
    └── ProportionalClosureDistributor.cs
```

### 4.1 IHeightPropagator.cs

```csharp
namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

/// <summary>
/// Сервис распространения высот от реперов по графу сети.
/// </summary>
public interface IHeightPropagator
{
    /// <summary>
    /// Распространить высоты от известных точек (реперов) по всей сети.
    /// </summary>
    /// <param name="network">Нивелирная сеть</param>
    /// <returns>Количество точек, для которых вычислена высота</returns>
    int PropagateHeights(LevelingNetwork network);

    /// <summary>
    /// Распространить высоты в рамках одного хода.
    /// </summary>
    /// <param name="run">Ход</param>
    /// <returns>Количество точек, для которых вычислена высота</returns>
    int PropagateHeightsInRun(Run run);
}
```

### 4.2 HeightPropagator.cs

```csharp
namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Реализация распространения высот через BFS обход графа.
/// </summary>
public class HeightPropagator : IHeightPropagator
{
    public int PropagateHeights(LevelingNetwork network)
    {
        // Сбрасываем ранее вычисленные высоты
        network.ResetCalculatedHeights();

        var calculatedCount = 0;
        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        // Начинаем с реперов
        foreach (var benchmark in network.Benchmarks)
        {
            queue.Enqueue(benchmark);
            visited.Add(benchmark);
        }

        // BFS обход
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Распространяем по исходящим наблюдениям (вперёд)
            foreach (var obs in current.OutgoingObservations)
            {
                if (!visited.Contains(obs.To) && !obs.To.Height.IsKnown)
                {
                    var newHeight = obs.CalculateForeHeight();
                    if (newHeight.IsKnown)
                    {
                        obs.To.SetCalculatedHeight(newHeight);
                        calculatedCount++;
                        visited.Add(obs.To);
                        queue.Enqueue(obs.To);
                    }
                }
            }

            // Распространяем по входящим наблюдениям (назад)
            foreach (var obs in current.IncomingObservations)
            {
                if (!visited.Contains(obs.From) && !obs.From.Height.IsKnown)
                {
                    var newHeight = obs.CalculateBackHeight();
                    if (newHeight.IsKnown)
                    {
                        obs.From.SetCalculatedHeight(newHeight);
                        calculatedCount++;
                        visited.Add(obs.From);
                        queue.Enqueue(obs.From);
                    }
                }
            }
        }

        return calculatedCount;
    }

    public int PropagateHeightsInRun(Run run)
    {
        var calculatedCount = 0;

        // Пытаемся распространить от начала к концу
        if (run.StartPoint?.Height.IsKnown == true)
        {
            Height currentHeight = run.StartPoint.Height;

            foreach (var obs in run.Observations)
            {
                if (!obs.To.Height.IsKnown)
                {
                    var newHeight = Height.Known(currentHeight.Value - obs.AdjustedDeltaH);
                    obs.To.SetCalculatedHeight(newHeight);
                    calculatedCount++;
                }
                currentHeight = obs.To.Height;
            }
        }
        // Или от конца к началу
        else if (run.EndPoint?.Height.IsKnown == true)
        {
            Height currentHeight = run.EndPoint.Height;

            for (int i = run.Observations.Count - 1; i >= 0; i--)
            {
                var obs = run.Observations[i];
                if (!obs.From.Height.IsKnown)
                {
                    var newHeight = Height.Known(currentHeight.Value + obs.AdjustedDeltaH);
                    obs.From.SetCalculatedHeight(newHeight);
                    calculatedCount++;
                }
                currentHeight = obs.From.Height;
            }
        }

        return calculatedCount;
    }
}
```

### 4.3 IClosureDistributor.cs

```csharp
namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

/// <summary>
/// Сервис распределения невязки по наблюдениям хода.
/// </summary>
public interface IClosureDistributor
{
    /// <summary>
    /// Распределить невязку по наблюдениям хода.
    /// </summary>
    /// <param name="run">Ход</param>
    void DistributeClosure(Run run);

    /// <summary>
    /// Распределить невязку с разбиением по секциям (между известными точками).
    /// </summary>
    /// <param name="run">Ход</param>
    void DistributeClosureWithSections(Run run);
}
```

### 4.4 ProportionalClosureDistributor.cs

```csharp
namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Распределение невязки пропорционально длине станций.
/// </summary>
public class ProportionalClosureDistributor : IClosureDistributor
{
    public void DistributeClosure(Run run)
    {
        if (run.Closure == null || !run.Closure.Value.IsWithinTolerance)
            return;

        var totalLength = run.TotalLength.Meters;
        if (totalLength <= 0)
            return;

        var closureMeters = run.Closure.Value.ValueMm / 1000.0;
        var accumulatedLength = 0.0;

        foreach (var obs in run.Observations)
        {
            accumulatedLength += obs.StationLength.Meters;
            var correction = -closureMeters * (accumulatedLength / totalLength);
            obs.ApplyCorrection(correction - obs.Correction); // Добавляем дельту
        }
    }

    public void DistributeClosureWithSections(Run run)
    {
        // Находим известные точки внутри хода
        var knownPoints = run.Points
            .Where(p => p.Height.IsKnown)
            .ToList();

        if (knownPoints.Count < 2)
        {
            // Нет секций - обычное распределение
            DistributeClosure(run);
            return;
        }

        // Разбиваем на секции
        var sections = new List<(int startIdx, int endIdx, double closure)>();

        for (int i = 0; i < knownPoints.Count - 1; i++)
        {
            var startPoint = knownPoints[i];
            var endPoint = knownPoints[i + 1];

            // Находим индексы наблюдений для этой секции
            var startIdx = run.Observations
                .ToList()
                .FindIndex(o => o.From.Code.Equals(startPoint.Code));

            var endIdx = run.Observations
                .ToList()
                .FindIndex(o => o.To.Code.Equals(endPoint.Code));

            if (startIdx >= 0 && endIdx >= 0 && endIdx >= startIdx)
            {
                // Вычисляем невязку секции
                var theoretical = startPoint.Height - endPoint.Height;
                var measured = run.Observations
                    .Skip(startIdx)
                    .Take(endIdx - startIdx + 1)
                    .Sum(o => o.DeltaH);

                sections.Add((startIdx, endIdx, measured - theoretical));
            }
        }

        // Распределяем невязку по каждой секции
        foreach (var (startIdx, endIdx, closure) in sections)
        {
            var sectionObs = run.Observations
                .Skip(startIdx)
                .Take(endIdx - startIdx + 1)
                .ToList();

            var sectionLength = sectionObs.Sum(o => o.StationLength.Meters);
            if (sectionLength <= 0)
                continue;

            var accumulatedLength = 0.0;
            foreach (var obs in sectionObs)
            {
                accumulatedLength += obs.StationLength.Meters;
                var correction = -closure * (accumulatedLength / sectionLength);
                obs.ApplyCorrection(correction);
            }
        }
    }
}
```

---

## Этап 5: Application Layer

### Цель
Создать слой приложения с Commands и Queries.

### Структура

```
Application/
├── Commands/
│   ├── ImportMeasurementsCommand.cs
│   ├── CalculateHeightsCommand.cs
│   └── Handlers/
│       ├── ImportMeasurementsHandler.cs
│       └── CalculateHeightsHandler.cs
├── Queries/
│   ├── GetNetworkSummaryQuery.cs
│   ├── GetRunStatisticsQuery.cs
│   └── Handlers/
│       ├── GetNetworkSummaryHandler.cs
│       └── GetRunStatisticsHandler.cs
├── DTOs/
│   ├── NetworkSummaryDto.cs
│   ├── RunSummaryDto.cs
│   ├── ObservationDto.cs
│   └── PointDto.cs
└── Mappers/
    └── NetworkMapper.cs
```

### 5.1 DTOs

```csharp
namespace Nivtropy.Application.DTOs;

// Используются для передачи данных в UI без зависимости от Domain

public record NetworkSummaryDto(
    Guid Id,
    string Name,
    int PointCount,
    int BenchmarkCount,
    int RunCount,
    int TotalStationCount,
    double TotalLengthMeters,
    bool IsConnected,
    IReadOnlyList<RunSummaryDto> Runs
);

public record RunSummaryDto(
    Guid Id,
    string Name,
    string? OriginalNumber,
    int StationCount,
    double TotalLengthMeters,
    string StartPointCode,
    string EndPointCode,
    double DeltaHSum,
    double? ClosureValueMm,
    double? ClosureToleranceMm,
    bool? IsClosureWithinTolerance,
    bool IsActive,
    string? SystemName
);

public record ObservationDto(
    Guid Id,
    int StationIndex,
    string FromPointCode,
    string ToPointCode,
    double BackReadingM,
    double ForeReadingM,
    double BackDistanceM,
    double ForeDistanceM,
    double DeltaH,
    double Correction,
    double AdjustedDeltaH,
    double? FromHeight,
    double? ToHeight
);

public record PointDto(
    string Code,
    double? Height,
    bool IsKnown,
    string Type,
    int Degree,
    bool IsSharedPoint
);
```

### 5.2 Commands

```csharp
namespace Nivtropy.Application.Commands;

public record ImportMeasurementsCommand(
    string FilePath,
    string Format // "DAT", "DINI", "FOR"
);

public record ImportMeasurementsResult(
    Guid NetworkId,
    int PointCount,
    int RunCount,
    IReadOnlyList<string> Warnings
);

// Handler
public class ImportMeasurementsHandler
{
    private readonly IFileParser _parser;
    private readonly INetworkRepository _repository;
    private readonly INetworkBuilder _builder;

    public ImportMeasurementsHandler(
        IFileParser parser,
        INetworkRepository repository,
        INetworkBuilder builder)
    {
        _parser = parser;
        _repository = repository;
        _builder = builder;
    }

    public async Task<ImportMeasurementsResult> HandleAsync(ImportMeasurementsCommand command)
    {
        // 1. Парсим файл
        var rawMeasurements = await _parser.ParseAsync(command.FilePath, command.Format);

        // 2. Строим граф
        var network = _builder.Build(rawMeasurements);

        // 3. Сохраняем
        await _repository.SaveAsync(network);

        return new ImportMeasurementsResult(
            network.Id,
            network.Points.Count,
            network.Runs.Count,
            _builder.Warnings
        );
    }
}
```

```csharp
namespace Nivtropy.Application.Commands;

public record CalculateHeightsCommand(Guid NetworkId);

public record CalculateHeightsResult(
    int CalculatedPointCount,
    IReadOnlyList<RunClosureDto> Closures
);

public record RunClosureDto(
    Guid RunId,
    string RunName,
    double? ClosureMm,
    double? ToleranceMm,
    bool? IsWithinTolerance
);

// Handler
public class CalculateHeightsHandler
{
    private readonly INetworkRepository _repository;
    private readonly IHeightPropagator _heightPropagator;
    private readonly IClosureDistributor _closureDistributor;
    private readonly IToleranceCalculator _toleranceCalculator;

    public async Task<CalculateHeightsResult> HandleAsync(CalculateHeightsCommand command)
    {
        var network = await _repository.GetByIdAsync(command.NetworkId);
        if (network == null)
            throw new NotFoundException($"Network {command.NetworkId} not found");

        // 1. Вычисляем невязки и распределяем поправки
        var closures = new List<RunClosureDto>();
        foreach (var run in network.Runs.Where(r => r.IsActive))
        {
            // Вычисляем допуск
            var tolerance = _toleranceCalculator.Calculate(run);

            // Вычисляем невязку
            run.CalculateClosure(tolerance);

            // Распределяем поправки
            if (run.Closure?.IsWithinTolerance == true)
            {
                _closureDistributor.DistributeClosureWithSections(run);
            }

            closures.Add(new RunClosureDto(
                run.Id,
                run.Name,
                run.Closure?.ValueMm,
                run.Closure?.ToleranceMm,
                run.Closure?.IsWithinTolerance
            ));
        }

        // 2. Распространяем высоты
        var calculatedCount = _heightPropagator.PropagateHeights(network);

        // 3. Сохраняем
        await _repository.SaveAsync(network);

        return new CalculateHeightsResult(calculatedCount, closures);
    }
}
```

### 5.3 Queries

```csharp
namespace Nivtropy.Application.Queries;

public record GetNetworkSummaryQuery(Guid NetworkId);

// Handler
public class GetNetworkSummaryHandler
{
    private readonly INetworkRepository _repository;
    private readonly INetworkMapper _mapper;

    public async Task<NetworkSummaryDto?> HandleAsync(GetNetworkSummaryQuery query)
    {
        var network = await _repository.GetByIdAsync(query.NetworkId);
        return network != null ? _mapper.ToSummaryDto(network) : null;
    }
}

public record GetRunStatisticsQuery(Guid NetworkId, Guid RunId);

public record RunStatisticsDto(
    Guid RunId,
    string Name,
    // Высоты
    double? MinHeight,
    double? MaxHeight,
    double? MeanHeight,
    double? HeightRange,
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
    int AnomalyCount,
    IReadOnlyList<AnomalyDto> Anomalies
);

public record AnomalyDto(
    int StationIndex,
    string Type, // "HeightJump", "StationLength", "ArmDifference"
    string Severity, // "Warning", "Critical"
    double Value,
    double Threshold,
    string Description
);
```

### 5.4 NetworkMapper

```csharp
namespace Nivtropy.Application.Mappers;

public interface INetworkMapper
{
    NetworkSummaryDto ToSummaryDto(LevelingNetwork network);
    RunSummaryDto ToSummaryDto(Run run);
    ObservationDto ToDto(Observation observation);
    PointDto ToDto(Point point);
}

public class NetworkMapper : INetworkMapper
{
    public NetworkSummaryDto ToSummaryDto(LevelingNetwork network)
    {
        return new NetworkSummaryDto(
            Id: network.Id,
            Name: network.Name,
            PointCount: network.Points.Count,
            BenchmarkCount: network.Benchmarks.Count(),
            RunCount: network.Runs.Count,
            TotalStationCount: network.TotalStationCount,
            TotalLengthMeters: network.TotalLength.Meters,
            IsConnected: network.IsConnected(),
            Runs: network.Runs.Select(ToSummaryDto).ToList()
        );
    }

    public RunSummaryDto ToSummaryDto(Run run)
    {
        return new RunSummaryDto(
            Id: run.Id,
            Name: run.Name,
            OriginalNumber: run.OriginalNumber,
            StationCount: run.StationCount,
            TotalLengthMeters: run.TotalLength.Meters,
            StartPointCode: run.StartPoint?.Code.Value ?? "",
            EndPointCode: run.EndPoint?.Code.Value ?? "",
            DeltaHSum: run.DeltaHSum,
            ClosureValueMm: run.Closure?.ValueMm,
            ClosureToleranceMm: run.Closure?.ToleranceMm,
            IsClosureWithinTolerance: run.Closure?.IsWithinTolerance,
            IsActive: run.IsActive,
            SystemName: run.System?.Name
        );
    }

    public ObservationDto ToDto(Observation obs)
    {
        return new ObservationDto(
            Id: obs.Id,
            StationIndex: obs.StationIndex,
            FromPointCode: obs.From.Code.Value,
            ToPointCode: obs.To.Code.Value,
            BackReadingM: obs.BackReading.Meters,
            ForeReadingM: obs.ForeReading.Meters,
            BackDistanceM: obs.BackDistance.Meters,
            ForeDistanceM: obs.ForeDistance.Meters,
            DeltaH: obs.DeltaH,
            Correction: obs.Correction,
            AdjustedDeltaH: obs.AdjustedDeltaH,
            FromHeight: obs.From.Height.IsKnown ? obs.From.Height.Value : null,
            ToHeight: obs.To.Height.IsKnown ? obs.To.Height.Value : null
        );
    }

    public PointDto ToDto(Point point)
    {
        return new PointDto(
            Code: point.Code.Value,
            Height: point.Height.IsKnown ? point.Height.Value : null,
            IsKnown: point.Height.IsKnown,
            Type: point.Type.ToString(),
            Degree: point.Degree,
            IsSharedPoint: point.IsSharedPoint
        );
    }
}
```

---

## Этап 6: Адаптеры и миграция

### Цель
Создать адаптеры для работы старого кода с новой архитектурой.

### 6.1 Структура

```
Legacy/
├── Adapters/
│   ├── TraverseRowAdapter.cs      # TraverseRow ↔ Observation
│   ├── LineSummaryAdapter.cs      # LineSummary ↔ Run
│   └── NetworkAdapter.cs          # LevelingNetwork → старые коллекции
└── Services/
    └── LegacyCalculationService.cs # Обёртка над новыми сервисами
```

### 6.2 TraverseRowAdapter.cs

```csharp
namespace Nivtropy.Legacy.Adapters;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;
using Nivtropy.Models; // Старые модели

/// <summary>
/// Адаптер для конвертации между TraverseRow (старый) и Observation (новый).
/// </summary>
public static class TraverseRowAdapter
{
    /// <summary>Конвертировать Observation в TraverseRow для UI</summary>
    public static TraverseRow ToTraverseRow(Observation obs)
    {
        return new TraverseRow
        {
            LineName = obs.Run.Name,
            Index = obs.StationIndex,
            BackCode = obs.From.Code.Value,
            ForeCode = obs.To.Code.Value,
            Rb_m = obs.BackReading.Meters,
            Rf_m = obs.ForeReading.Meters,
            HdBack_m = obs.BackDistance.Meters,
            HdFore_m = obs.ForeDistance.Meters,
            Correction = obs.Correction,
            BackHeight = obs.From.Height.IsKnown ? obs.From.Height.Value : (double?)null,
            ForeHeight = obs.To.Height.IsKnown ? obs.To.Height.Value : (double?)null,
            IsBackHeightKnown = obs.From.Height.IsKnown && obs.From.Type == PointType.Benchmark,
            IsForeHeightKnown = obs.To.Height.IsKnown && obs.To.Type == PointType.Benchmark,
            IsVirtualStation = false
        };
    }

    /// <summary>Конвертировать список Observations в TraverseRows</summary>
    public static List<TraverseRow> ToTraverseRows(Run run)
    {
        var rows = new List<TraverseRow>();

        // Добавляем виртуальную станцию для первой точки
        if (run.StartPoint != null)
        {
            rows.Add(new TraverseRow
            {
                LineName = run.Name,
                Index = 0,
                BackCode = run.StartPoint.Code.Value,
                ForeCode = "",
                IsVirtualStation = true,
                BackHeight = run.StartPoint.Height.IsKnown ? run.StartPoint.Height.Value : null,
                IsBackHeightKnown = run.StartPoint.Type == PointType.Benchmark
            });
        }

        // Добавляем обычные станции
        foreach (var obs in run.Observations)
        {
            rows.Add(ToTraverseRow(obs));
        }

        return rows;
    }

    /// <summary>Создать Observation из TraverseRow (для импорта)</summary>
    public static (PointCode from, PointCode to, Reading back, Reading fore, Distance backDist, Distance foreDist)
        FromTraverseRow(TraverseRow row)
    {
        return (
            new PointCode(row.BackCode),
            new PointCode(row.ForeCode),
            new Reading(row.Rb_m),
            new Reading(row.Rf_m),
            new Distance(row.HdBack_m),
            new Distance(row.HdFore_m)
        );
    }
}
```

### 6.3 LineSummaryAdapter.cs

```csharp
namespace Nivtropy.Legacy.Adapters;

using Nivtropy.Domain.Model;
using Nivtropy.Models;

/// <summary>
/// Адаптер для конвертации между LineSummary (старый) и Run (новый).
/// </summary>
public static class LineSummaryAdapter
{
    public static LineSummary ToLineSummary(Run run, int index)
    {
        return new LineSummary
        {
            Index = index,
            OriginalLineNumber = run.OriginalNumber ?? index.ToString(),
            StartTarget = run.StartPoint?.Code.Value ?? "",
            EndTarget = run.EndPoint?.Code.Value ?? "",
            RecordCount = run.StationCount,
            DeltaHSum = run.DeltaHSum,
            TotalDistanceBack = run.Observations.Sum(o => o.BackDistance.Meters),
            TotalDistanceFore = run.Observations.Sum(o => o.ForeDistance.Meters),
            TotalAverageLength = run.TotalLength.Meters,
            ArmDifferenceAccumulation = run.AccumulatedArmDifference,
            SystemId = run.System?.Id,
            IsActive = run.IsActive,
            KnownPointsCount = run.Points.Count(p => p.Height.IsKnown),
            // Closure info
            Closures = run.Closure != null
                ? new List<double> { run.Closure.Value.ValueMm / 1000.0 }
                : new List<double>()
        };
    }

    public static List<LineSummary> ToLineSummaries(LevelingNetwork network)
    {
        return network.Runs
            .Select((run, idx) => ToLineSummary(run, idx))
            .ToList();
    }
}
```

### 6.4 NetworkAdapter.cs

```csharp
namespace Nivtropy.Legacy.Adapters;

using Nivtropy.Domain.Model;
using Nivtropy.Models;

/// <summary>
/// Адаптер для получения данных в старом формате из LevelingNetwork.
/// Используется ViewModels для обратной совместимости с UI.
/// </summary>
public class NetworkAdapter
{
    private readonly LevelingNetwork _network;

    public NetworkAdapter(LevelingNetwork network)
    {
        _network = network;
    }

    /// <summary>Получить все TraverseRows для отображения</summary>
    public List<TraverseRow> GetAllTraverseRows()
    {
        return _network.Runs
            .SelectMany(TraverseRowAdapter.ToTraverseRows)
            .ToList();
    }

    /// <summary>Получить TraverseRows для конкретного хода</summary>
    public List<TraverseRow> GetTraverseRowsForRun(Guid runId)
    {
        var run = _network.Runs.FirstOrDefault(r => r.Id == runId);
        return run != null ? TraverseRowAdapter.ToTraverseRows(run) : new List<TraverseRow>();
    }

    /// <summary>Получить все LineSummaries</summary>
    public List<LineSummary> GetLineSummaries()
    {
        return LineSummaryAdapter.ToLineSummaries(_network);
    }

    /// <summary>Получить словарь известных высот</summary>
    public Dictionary<string, double> GetKnownHeights()
    {
        return _network.Benchmarks
            .Where(p => p.Height.IsKnown)
            .ToDictionary(p => p.Code.Value, p => p.Height.Value);
    }

    /// <summary>Получить общие точки</summary>
    public List<SharedPointLinkItem> GetSharedPointLinks()
    {
        return _network.SharedPoints
            .Select(p => new SharedPointLinkItem
            {
                PointCode = p.Code.Value,
                RunNames = p.ConnectedRuns.Select(r => r.Name).ToList(),
                Height = p.Height.IsKnown ? p.Height.Value : null
            })
            .ToList();
    }
}
```

### 6.5 LegacyCalculationService.cs

```csharp
namespace Nivtropy.Legacy.Services;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.Services;
using Nivtropy.Legacy.Adapters;
using Nivtropy.Models;
using Nivtropy.Services.Calculation;

/// <summary>
/// Обёртка над новыми сервисами, предоставляющая старый API.
/// Позволяет постепенно мигрировать ViewModels.
/// </summary>
public class LegacyCalculationService : ITraverseCalculationService
{
    private readonly IHeightPropagator _heightPropagator;
    private readonly IClosureDistributor _closureDistributor;
    private LevelingNetwork? _network;

    public LegacyCalculationService(
        IHeightPropagator heightPropagator,
        IClosureDistributor closureDistributor)
    {
        _heightPropagator = heightPropagator;
        _closureDistributor = closureDistributor;
    }

    /// <summary>Установить текущую сеть</summary>
    public void SetNetwork(LevelingNetwork network)
    {
        _network = network;
    }

    // Старый API
    public List<TraverseRow> CalculateHeights(
        List<TraverseRow> rows,
        Dictionary<string, double> knownHeights)
    {
        if (_network == null)
            throw new InvalidOperationException("Network not set");

        // Устанавливаем реперы
        foreach (var (code, height) in knownHeights)
        {
            _network.SetBenchmarkHeight(
                new Domain.ValueObjects.PointCode(code),
                Domain.ValueObjects.Height.Known(height));
        }

        // Распространяем высоты
        _heightPropagator.PropagateHeights(_network);

        // Возвращаем адаптированные данные
        return new NetworkAdapter(_network).GetAllTraverseRows();
    }

    public void DistributeClosure(LineSummary lineSummary, List<TraverseRow> rows)
    {
        if (_network == null)
            return;

        var run = _network.Runs.FirstOrDefault(r => r.Name == lineSummary.OriginalLineNumber);
        if (run != null)
        {
            _closureDistributor.DistributeClosureWithSections(run);
        }
    }
}
```

---

## Этап 7: Рефакторинг ViewModels

### Цель
Постепенно перевести ViewModels на использование новой архитектуры.

### 7.1 План рефакторинга ViewModel

**Фаза 1: Использование адаптеров**
- ViewModels продолжают работать с `TraverseRow`, `LineSummary`
- Но данные берутся из `LevelingNetwork` через адаптеры

**Фаза 2: Прямое использование DTOs**
- ViewModels переходят на работу с `RunSummaryDto`, `ObservationDto`
- Старые модели используются только для legacy компонентов

**Фаза 3: Полная миграция**
- Удаление адаптеров
- Удаление старых моделей
- ViewModels работают напрямую с Application Layer

### 7.2 Пример: TraverseCalculationViewModel (Фаза 1)

```csharp
// До миграции
public class TraverseCalculationViewModel : ViewModelBase
{
    private readonly ITraverseBuilder _traverseBuilder;
    private readonly ITraverseCalculationService _calculationService;

    public void UpdateRows()
    {
        var rows = _traverseBuilder.Build(records, lineSummary);
        rows = _calculationService.CalculateHeights(rows, knownHeights);
        Rows = new ObservableCollection<TraverseRow>(rows);
    }
}

// После миграции (Фаза 1)
public class TraverseCalculationViewModel : ViewModelBase
{
    private readonly INetworkBuilder _networkBuilder;
    private readonly IHeightPropagator _heightPropagator;
    private readonly IClosureDistributor _closureDistributor;
    private LevelingNetwork? _network;

    public void UpdateRows()
    {
        // Строим граф
        _network = _networkBuilder.Build(rawMeasurements);

        // Устанавливаем реперы
        foreach (var (code, height) in knownHeights)
        {
            _network.SetBenchmarkHeight(
                new PointCode(code),
                Height.Known(height));
        }

        // Вычисляем невязки и распределяем
        foreach (var run in _network.Runs)
        {
            run.CalculateClosure(toleranceMm);
            _closureDistributor.DistributeClosureWithSections(run);
        }

        // Распространяем высоты
        _heightPropagator.PropagateHeights(_network);

        // Адаптируем для UI (временно)
        var adapter = new NetworkAdapter(_network);
        Rows = new ObservableCollection<TraverseRow>(adapter.GetAllTraverseRows());
        Runs = new ObservableCollection<LineSummary>(adapter.GetLineSummaries());
    }
}
```

### 7.3 Пример: TraverseCalculationViewModel (Фаза 2)

```csharp
// После миграции (Фаза 2) - с Commands/Queries
public class TraverseCalculationViewModel : ViewModelBase
{
    private readonly ImportMeasurementsHandler _importHandler;
    private readonly CalculateHeightsHandler _calculateHandler;
    private readonly GetNetworkSummaryHandler _summaryHandler;

    private Guid _networkId;

    public async Task ImportFileAsync(string path)
    {
        var result = await _importHandler.HandleAsync(
            new ImportMeasurementsCommand(path, "DAT"));

        _networkId = result.NetworkId;
        await RefreshDataAsync();
    }

    public async Task CalculateAsync()
    {
        var result = await _calculateHandler.HandleAsync(
            new CalculateHeightsCommand(_networkId));

        // Показываем невязки
        Closures = new ObservableCollection<RunClosureDto>(result.Closures);

        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        var summary = await _summaryHandler.HandleAsync(
            new GetNetworkSummaryQuery(_networkId));

        if (summary != null)
        {
            // Работаем напрямую с DTO
            RunSummaries = new ObservableCollection<RunSummaryDto>(summary.Runs);
            PointCount = summary.PointCount;
            BenchmarkCount = summary.BenchmarkCount;
        }
    }
}
```

### 7.4 Пример: NetworkViewModel (новый)

```csharp
// Новый ViewModel для графовой визуализации
public class NetworkViewModel : ViewModelBase
{
    private readonly GetNetworkVisualizationHandler _visualizationHandler;

    public ObservableCollection<PointDto> Points { get; } = new();
    public ObservableCollection<EdgeDto> Edges { get; } = new();

    public async Task LoadNetworkAsync(Guid networkId)
    {
        var visualization = await _visualizationHandler.HandleAsync(
            new GetNetworkVisualizationQuery(networkId));

        Points.Clear();
        foreach (var point in visualization.Points)
            Points.Add(point);

        Edges.Clear();
        foreach (var edge in visualization.Edges)
            Edges.Add(edge);
    }

    // Для Canvas визуализации
    public record EdgeDto(
        string FromCode,
        string ToCode,
        double FromX, double FromY,
        double ToX, double ToY,
        string RunName,
        bool IsHighlighted
    );
}
```

---

## Чеклист выполнения

### Этап 1: Value Objects
- [ ] Создать папку `Domain/ValueObjects/`
- [ ] Реализовать `PointCode.cs`
- [ ] Реализовать `Height.cs`
- [ ] Реализовать `Distance.cs`
- [ ] Реализовать `Reading.cs`
- [ ] Реализовать `Closure.cs`
- [ ] Написать unit-тесты для Value Objects

### Этап 2: Domain Entities
- [ ] Создать папку `Domain/Model/`
- [ ] Реализовать `PointType.cs`
- [ ] Реализовать `Point.cs`
- [ ] Реализовать `Observation.cs`
- [ ] Реализовать `Run.cs`
- [ ] Написать unit-тесты для Entities

### Этап 3: LevelingNetwork
- [ ] Реализовать `LevelingNetwork.cs`
- [ ] Реализовать `TraverseSystem.cs`
- [ ] Реализовать граф-операции (BFS, связность, пути)
- [ ] Написать unit-тесты для графа

### Этап 4: Domain Services
- [ ] Создать папку `Domain/Services/`
- [ ] Реализовать `IHeightPropagator` + `HeightPropagator`
- [ ] Реализовать `IClosureDistributor` + `ProportionalClosureDistributor`
- [ ] Написать unit-тесты для сервисов

### Этап 5: Application Layer
- [ ] Создать структуру `Application/`
- [ ] Реализовать DTOs
- [ ] Реализовать Commands и Handlers
- [ ] Реализовать Queries и Handlers
- [ ] Реализовать `NetworkMapper`
- [ ] Написать integration-тесты

### Этап 6: Адаптеры
- [ ] Создать папку `Legacy/Adapters/`
- [ ] Реализовать `TraverseRowAdapter`
- [ ] Реализовать `LineSummaryAdapter`
- [ ] Реализовать `NetworkAdapter`
- [ ] Реализовать `LegacyCalculationService`
- [ ] Проверить работу со старыми ViewModels

### Этап 7: Рефакторинг ViewModels
- [ ] Обновить `TraverseCalculationViewModel`
- [ ] Обновить `TraverseJournalViewModel`
- [ ] Обновить `DataViewModel`
- [ ] Создать `NetworkViewModel` (для графа)
- [ ] Обновить DI регистрацию
- [ ] Полное тестирование

### Финал: Удаление Legacy
- [ ] Удалить адаптеры
- [ ] Удалить старые модели
- [ ] Удалить папку `Legacy/`
- [ ] Обновить документацию

---

## Важные замечания

### Принципы при миграции

1. **Один этап за раз** - не смешивать изменения
2. **Тесты после каждого этапа** - приложение должно работать
3. **Коммиты после каждого файла** - легко откатить
4. **Не удалять старый код сразу** - сначала адаптеры

### Что НЕ менять

1. **View слой** - XAML остаётся как есть
2. **Converters** - работают с примитивами
3. **Resources** - без изменений
4. **App.xaml.cs** - только DI регистрация

### Порядок работы для Sonnet

1. Читай этот файл перед каждым этапом
2. Создавай файлы по одному
3. Копируй код из этого документа
4. Адаптируй namespaces если нужно
5. Компилируй и тестируй после каждого файла
6. Коммить с описанием: `feat(domain): add PointCode value object`

---

## Визуализация целевой архитектуры

```
┌─────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │
│  │ MainWindow   │  │ Converters   │  │ ViewModels               │   │
│  │ Views/*.xaml │  │              │  │ - NetworkViewModel       │   │
│  └──────────────┘  └──────────────┘  │ - RunViewModel           │   │
│                                       │ - CalculationViewModel   │   │
│                                       └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ uses DTOs
┌─────────────────────────────────────────────────────────────────────┐
│                          APPLICATION                                 │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────────┐   │
│  │ Commands         │  │ Queries          │  │ DTOs            │   │
│  │ - Import         │  │ - GetSummary     │  │ - NetworkDto    │   │
│  │ - Calculate      │  │ - GetStatistics  │  │ - RunDto        │   │
│  │ - Adjust         │  │ - GetAnomalies   │  │ - ObservationDto│   │
│  └──────────────────┘  └──────────────────┘  └─────────────────┘   │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                         Handlers                              │   │
│  │  ImportHandler, CalculateHandler, GetSummaryHandler, ...     │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ uses Domain
┌─────────────────────────────────────────────────────────────────────┐
│                            DOMAIN                                    │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    LevelingNetwork (Graph)                    │   │
│  │  ┌─────────┐        ┌─────────────┐        ┌─────────┐       │   │
│  │  │  Point  │◄──────►│ Observation │◄──────►│  Point  │       │   │
│  │  │ (Node)  │        │   (Edge)    │        │ (Node)  │       │   │
│  │  └─────────┘        └─────────────┘        └─────────┘       │   │
│  │       │                    │                    │             │   │
│  │       └────────────────────┼────────────────────┘             │   │
│  │                            ▼                                  │   │
│  │                    ┌─────────────┐                            │   │
│  │                    │     Run     │                            │   │
│  │                    │ (Aggregate) │                            │   │
│  │                    └─────────────┘                            │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────────┐    │
│  │ Value Objects  │  │ Domain Services│  │ Repository Intf    │    │
│  │ - Height       │  │ - HeightProp   │  │ - INetworkRepo     │    │
│  │ - Distance     │  │ - ClosureDist  │  │ - IRunRepo         │    │
│  │ - PointCode    │  │ - Adjustment   │  │                    │    │
│  └────────────────┘  └────────────────┘  └────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ implements interfaces
┌─────────────────────────────────────────────────────────────────────┐
│                        INFRASTRUCTURE                                │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────────┐    │
│  │ Persistence    │  │ Parsers        │  │ Export             │    │
│  │ - InMemoryRepo │  │ - DatParser    │  │ - CsvExporter      │    │
│  │ - JsonRepo     │  │ - DiniParser   │  │                    │    │
│  └────────────────┘  └────────────────┘  └────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

---

*Документ создан: 2026-01-15*
*Версия: 1.0*
