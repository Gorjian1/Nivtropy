# DDD Refactoring Plan for Codex

## КРИТИЧЕСКИ ВАЖНО

**НЕ ДЕЛАЙ:**
- НЕ исправляй ошибки компилятора добавлением `using Nivtropy.Presentation.*`
- НЕ возвращай код к предыдущему состоянию
- НЕ удаляй функционал сервисов
- НЕ меняй публичный API ViewModels

**ДЕЛАЙ:**
- Создавай новые DTO в Application/DTOs
- Заменяй зависимости от Presentation на DTO
- Сохраняй логику вычислений без изменений

---

## Проблема

Domain и Application слои имеют запрещённые зависимости от Presentation:

```
НЕПРАВИЛЬНО (сейчас):
Domain → Presentation.Models (ЗАПРЕЩЕНО!)
Application → Presentation.Models (ЗАПРЕЩЕНО!)

ПРАВИЛЬНО (цель):
Presentation → Application → Domain
Infrastructure → Application → Domain
```

---

## Файлы с нарушениями

### DOMAIN слой (приоритет 1 - исправить первым)

| Файл | Нарушающий импорт | Используемые типы |
|------|-------------------|-------------------|
| `Domain/Services/TraverseCorrectionService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow`, `LineSummary`, `TraverseClosureMode` |
| `Domain/Services/SystemConnectivityService.cs` | `Nivtropy.Presentation.Models`, `Nivtropy.Presentation.ViewModels.Managers` | `LineSummary`, `SharedPointLinkItem` |

### APPLICATION слой (приоритет 2)

| Файл | Нарушающий импорт | Используемые типы |
|------|-------------------|-------------------|
| `Application/Services/TraverseCalculationService.cs` | `Nivtropy.Presentation.Models`, `Nivtropy.Models` | `TraverseRow`, `LineSummary`, `MeasurementRecord` |
| `Application/Services/ClosureCalculationService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow` |
| `Application/Services/DesignCalculationService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow`, `DesignRow` |
| `Application/Services/RunAnnotationService.cs` | `Nivtropy.Presentation.Models`, `Nivtropy.Models` | `LineSummary`, `MeasurementRecord` |
| `Application/Services/ProfileStatisticsService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow` |
| `Application/Services/IClosureCalculationService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow` |
| `Application/Services/IDesignCalculationService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow`, `DesignRow` |
| `Application/Services/IProfileStatisticsService.cs` | `Nivtropy.Presentation.Models` | `TraverseRow` |
| `Application/DTOs/ProfileStatistics.cs` | `Nivtropy.Presentation.Models` | `OutlierPoint` |

---

## План исправления

### Шаг 1: Создать DTO в Application/DTOs

#### 1.1 Создать `Application/DTOs/StationDto.cs`

```csharp
namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO станции нивелирования для расчётов (замена TraverseRow).
/// Не содержит UI-логики (INotifyPropertyChanged, Display-свойства).
/// </summary>
public class StationDto
{
    public string LineName { get; set; } = "";
    public int Index { get; set; }
    public string? BackCode { get; set; }
    public string? ForeCode { get; set; }

    // Измерения
    public double? BackReading { get; set; }      // Rb_m
    public double? ForeReading { get; set; }      // Rf_m
    public double? BackDistance { get; set; }     // HdBack_m
    public double? ForeDistance { get; set; }     // HdFore_m

    // Вычисляемые
    public double? DeltaH => (BackReading.HasValue && ForeReading.HasValue)
        ? BackReading.Value - ForeReading.Value
        : null;

    public double? StationLength => (BackDistance.HasValue && ForeDistance.HasValue)
        ? BackDistance.Value + ForeDistance.Value
        : null;

    public double? ArmDifference => (BackDistance.HasValue && ForeDistance.HasValue)
        ? BackDistance.Value - ForeDistance.Value
        : null;

    // Высоты
    public double? BackHeight { get; set; }
    public double? ForeHeight { get; set; }
    public double? BackHeightRaw { get; set; }    // Z0 без поправки
    public double? ForeHeightRaw { get; set; }    // Z0 без поправки
    public bool IsBackHeightKnown { get; set; }
    public bool IsForeHeightKnown { get; set; }

    // Уравнивание
    public double? Correction { get; set; }
    public double? BaselineCorrection { get; set; }

    public double? AdjustedDeltaH => DeltaH.HasValue && Correction.HasValue
        ? DeltaH.Value + Correction.Value
        : DeltaH;

    public bool IsVirtualStation => string.IsNullOrWhiteSpace(ForeCode) && !DeltaH.HasValue;
}
```

#### 1.2 Создать `Application/DTOs/RunSummaryDto.cs`

```csharp
namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO сводки по ходу для расчётов (замена LineSummary).
/// </summary>
public class RunSummaryDto
{
    public int Index { get; set; }
    public string? OriginalLineNumber { get; set; }
    public string? StartPointCode { get; set; }
    public string? EndPointCode { get; set; }
    public int StationCount { get; set; }
    public double? DeltaHSum { get; set; }
    public double? TotalDistanceBack { get; set; }
    public double? TotalDistanceFore { get; set; }
    public double? ArmDifferenceAccumulation { get; set; }
    public string? SystemId { get; set; }
    public bool IsActive { get; set; } = true;
    public int KnownPointsCount { get; set; }
    public bool UseLocalAdjustment { get; set; }

    public double? TotalLength => TotalDistanceBack.HasValue && TotalDistanceFore.HasValue
        ? TotalDistanceBack.Value + TotalDistanceFore.Value
        : null;

    public List<double> Closures { get; set; } = new();
    public List<string> SharedPointCodes { get; set; } = new();
}
```

#### 1.3 Создать `Application/DTOs/SharedPointDto.cs`

```csharp
namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO общей точки для расчётов связности (замена SharedPointLinkItem).
/// </summary>
public class SharedPointDto
{
    public string Code { get; set; } = "";
    public bool IsEnabled { get; set; }
    public List<int> RunIndexes { get; set; } = new();
}
```

#### 1.4 Создать `Application/DTOs/CorrectionInputDto.cs`

```csharp
namespace Nivtropy.Application.DTOs;

/// <summary>
/// Входные данные для расчёта поправок (замена StationCorrectionInput).
/// </summary>
public class CorrectionInputDto
{
    public string? PointCode { get; set; }
    public double? DeltaH { get; set; }
    public double? BackDistance { get; set; }
    public double? ForeDistance { get; set; }

    public double? AverageDistance => (BackDistance.HasValue && ForeDistance.HasValue)
        ? (BackDistance.Value + ForeDistance.Value) / 2.0
        : null;
}
```

#### 1.5 Переместить enum `TraverseClosureMode` в Application/Enums

Создать `Application/Enums/TraverseClosureMode.cs`:

```csharp
namespace Nivtropy.Application.Enums;

/// <summary>
/// Режим замыкания хода
/// </summary>
public enum TraverseClosureMode
{
    /// <summary>Открытый ход - без замыкания</summary>
    Open,
    /// <summary>Простое замыкание - один репер в начале и конце</summary>
    Simple,
    /// <summary>Локальное уравнивание - несколько реперов внутри хода</summary>
    Local
}
```

---

### Шаг 2: Рефакторинг Domain/Services

#### 2.1 TraverseCorrectionService.cs

**БЫЛО:**
```csharp
using Nivtropy.Presentation.Models;
// использует TraverseRow, LineSummary, TraverseClosureMode
```

**ДОЛЖНО СТАТЬ:**
```csharp
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
// использует StationDto или CorrectionInputDto, RunSummaryDto, TraverseClosureMode
```

**Изменения в методах:**
- Заменить `List<TraverseRow>` на `IReadOnlyList<StationDto>` или `IReadOnlyList<CorrectionInputDto>`
- Заменить `LineSummary` на `RunSummaryDto`
- Обновить обращения к свойствам:
  - `row.HdBack_m` → `station.BackDistance`
  - `row.HdFore_m` → `station.ForeDistance`
  - `row.BackCode` → `station.BackCode`
  - `row.ForeCode` → `station.ForeCode`
  - `row.Correction` → `station.Correction`

#### 2.2 SystemConnectivityService.cs

**БЫЛО:**
```csharp
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.ViewModels.Managers;
// использует LineSummary, SharedPointLinkItem
```

**ДОЛЖНО СТАТЬ:**
```csharp
using Nivtropy.Application.DTOs;
// использует RunSummaryDto, SharedPointDto
```

**Изменения в методах:**
- Заменить `IReadOnlyList<LineSummary>` на `IReadOnlyList<RunSummaryDto>`
- Заменить `IReadOnlyList<SharedPointLinkItem>` на `IReadOnlyList<SharedPointDto>`
- Обновить обращения:
  - `line.Index` → `run.Index`
  - `sharedPoint.Code` → `point.Code`
  - `sharedPoint.IsUsedInRun(i)` → `point.RunIndexes.Contains(i)`

---

### Шаг 3: Рефакторинг Application/Services

Применить аналогичные изменения ко всем сервисам Application:

1. Заменить импорты `Nivtropy.Presentation.Models` на `Nivtropy.Application.DTOs`
2. Заменить типы параметров и возвращаемых значений
3. Обновить обращения к свойствам

**Особые случаи:**

- `MeasurementRecord` (из `Nivtropy.Models`) - это входные данные парсинга, оставить как есть или создать `Application/DTOs/MeasurementDto.cs`
- `DesignRow` - создать `Application/DTOs/DesignPointDto.cs`
- `OutlierPoint` - создать `Application/DTOs/OutlierDto.cs`

---

### Шаг 4: Создать мапперы в Presentation

Создать `Presentation/Mappers/DtoMapper.cs`:

```csharp
namespace Nivtropy.Presentation.Mappers;

using Nivtropy.Application.DTOs;
using Nivtropy.Presentation.Models;

public static class DtoMapper
{
    public static StationDto ToDto(this TraverseRow row) => new()
    {
        LineName = row.LineName,
        Index = row.Index,
        BackCode = row.BackCode,
        ForeCode = row.ForeCode,
        BackReading = row.Rb_m,
        ForeReading = row.Rf_m,
        BackDistance = row.HdBack_m,
        ForeDistance = row.HdFore_m,
        BackHeight = row.BackHeight,
        ForeHeight = row.ForeHeight,
        BackHeightRaw = row.BackHeightZ0,
        ForeHeightRaw = row.ForeHeightZ0,
        IsBackHeightKnown = row.IsBackHeightKnown,
        IsForeHeightKnown = row.IsForeHeightKnown,
        Correction = row.Correction,
        BaselineCorrection = row.BaselineCorrection
    };

    public static void ApplyFrom(this TraverseRow row, StationDto dto)
    {
        row.Correction = dto.Correction;
        row.BaselineCorrection = dto.BaselineCorrection;
        row.BackHeight = dto.BackHeight;
        row.ForeHeight = dto.ForeHeight;
        row.BackHeightZ0 = dto.BackHeightRaw;
        row.ForeHeightZ0 = dto.ForeHeightRaw;
    }

    public static RunSummaryDto ToDto(this LineSummary line) => new()
    {
        Index = line.Index,
        OriginalLineNumber = line.OriginalLineNumber,
        StartPointCode = line.StartTarget ?? line.StartStation,
        EndPointCode = line.EndTarget ?? line.EndStation,
        StationCount = line.RecordCount,
        DeltaHSum = line.DeltaHSum,
        TotalDistanceBack = line.TotalDistanceBack,
        TotalDistanceFore = line.TotalDistanceFore,
        ArmDifferenceAccumulation = line.ArmDifferenceAccumulation,
        SystemId = line.SystemId,
        IsActive = line.IsActive,
        KnownPointsCount = line.KnownPointsCount,
        UseLocalAdjustment = line.UseLocalAdjustment,
        Closures = line.Closures.ToList(),
        SharedPointCodes = line.SharedPointCodes.ToList()
    };

    public static SharedPointDto ToDto(this SharedPointLinkItem item, IEnumerable<int> runIndexes) => new()
    {
        Code = item.Code,
        IsEnabled = item.IsEnabled,
        RunIndexes = runIndexes.ToList()
    };
}
```

---

### Шаг 5: Обновить вызовы в ViewModels

В `TraverseCalculationViewModel.cs` и других ViewModel:

```csharp
// БЫЛО:
_correctionService.CalculateCorrections(rows, isAnchor, sign, mode);

// СТАЛО:
var dtos = rows.Select(r => r.ToDto()).ToList();
var result = _correctionService.CalculateCorrections(dtos, isAnchor, sign, mode);
// Применить результаты обратно к rows
for (int i = 0; i < rows.Count; i++)
{
    rows[i].Correction = result.Corrections[i];
}
```

---

## Порядок выполнения

1. **Создать все DTO файлы** (Шаг 1) - это не сломает компиляцию
2. **Создать enum TraverseClosureMode** в Application/Enums
3. **Рефакторить Domain/Services** (Шаг 2) - начать с TraverseCorrectionService
4. **Рефакторить Application/Services** (Шаг 3)
5. **Создать DtoMapper** в Presentation (Шаг 4)
6. **Обновить ViewModels** (Шаг 5)

---

## Проверка успешности

После рефакторинга:

```bash
# Не должно быть таких импортов в Domain:
grep -r "using Nivtropy.Presentation" Domain/

# Не должно быть таких импортов в Application:
grep -r "using Nivtropy.Presentation" Application/

# Проект должен компилироваться:
dotnet build
```

---

## Карта зависимостей свойств

### TraverseRow → StationDto

| TraverseRow | StationDto |
|-------------|------------|
| `Rb_m` | `BackReading` |
| `Rf_m` | `ForeReading` |
| `HdBack_m` | `BackDistance` |
| `HdFore_m` | `ForeDistance` |
| `BackHeightZ0` | `BackHeightRaw` |
| `ForeHeightZ0` | `ForeHeightRaw` |
| `DeltaH` | `DeltaH` (вычисляемое) |
| `StationLength_m` | `StationLength` (вычисляемое) |
| `ArmDifference_m` | `ArmDifference` (вычисляемое) |

### LineSummary → RunSummaryDto

| LineSummary | RunSummaryDto |
|-------------|---------------|
| `StartTarget/StartStation` | `StartPointCode` |
| `EndTarget/EndStation` | `EndPointCode` |
| `RecordCount` | `StationCount` |
| `TotalAverageLength` | `TotalLength` (вычисляемое) |

---

## Типичные ошибки (НЕ ДЕЛАЙ ТАК)

### Ошибка 1: Добавление using вместо рефакторинга

```csharp
// НЕПРАВИЛЬНО - это откат к плохой архитектуре
using Nivtropy.Presentation.Models; // <-- НЕ ДОБАВЛЯЙ ЭТО В DOMAIN!
```

### Ошибка 2: Удаление функционала

```csharp
// НЕПРАВИЛЬНО - не удаляй методы, только меняй типы параметров
// public CorrectionResult Calculate(...) { } // <-- НЕ УДАЛЯЙ
```

### Ошибка 3: Копирование UI-логики в DTO

```csharp
// НЕПРАВИЛЬНО - DTO не должны иметь UI-логику
public class StationDto
{
    public string HeightDisplay => ... // <-- НЕ ДОБАВЛЯЙ Display-свойства
    public event PropertyChangedEventHandler PropertyChanged; // <-- НЕ ДОБАВЛЯЙ
}
```
