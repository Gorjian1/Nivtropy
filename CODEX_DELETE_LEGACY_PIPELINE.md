# CODEX: Удаление легаси‑пайплайна и переход на единственный DDD‑путь (Nivtropy)

Цель этой инструкции — **полностью убрать “Traverse/Journal legacy” расчётный путь** (TraverseCalculationService / TraverseProcessingService и связанные VM/экспорт), и сделать так, чтобы:

- **все режимы (None / Local / Network(МНК))** считались только через **Domain модель сети** `LevelingNetwork` + `CalculateHeightsHandler`,
- таблица/журнал/CSV брались **из результатов DDD‑сети**,  
- **виртуальных станций не существовало физически** (ни в домене, ни в таблицах).

> Контекст: сейчас в проекте параллельно живут 2 пути:
> 1) “журнальный/CSV” путь через `TraverseProcessingService` → он и даёт одинаковые `нет.csv` и `сеть.csv`  
> 2) DDD‑путь через `CalculateHeightsHandler` → он умеет Local/Network, но UI/CSV к нему не подключены.  
> Нужно оставить **только (2)**.

---

## 0) Definition of Done (DoD)

Считаем задачу завершённой, когда:

1. **В коде больше нет использования**:
   - `ITraverseCalculationService`, `TraverseCalculationService`
   - `ITraverseProcessingService`, `TraverseProcessingService`
   - `ITraverseCorrectionService`, `TraverseCorrectionService`
   - `ISystemConnectivityService`, `SystemConnectivityService`
   - `ITraverseExportService`, `TraverseExportService`
   - `TraverseCalculationViewModel`, `TraverseJournalViewModel`, `TraverseDesignViewModel` (или их legacy‑частей)

2. UI и экспорт используют **только**:
   - `LevelingNetwork` (Domain)
   - `INetworkRepository`
   - `CalculateHeightsHandler` (AdjustmentMode.None/Local/Network)
   - `GetNetworkSummaryHandler` / `INetworkMapper` для DTO

3. **Network ≠ None**: режим Network реально меняет поправки/высоты (проверяется на ваших CSV).

4. **Виртуальных станций нет**: ни одной строки “пустая станция” (как на скрине) не появляется.

---

## 1) Сначала починить математику DDD (обязательно перед вырезанием легаси)

Иначе вы вырежете легаси, а новый путь будет выдавать неверную невязку/поправки.

### 1.1. Исправить знак теоретического превышения (BUG)

Сейчас в двух местах используется:
- `start.Height - end.Height`  
а нужно:
- **`end.Height - start.Height`**

Патч:

**`Domain/Model/Run.cs`**
```csharp
// Было:
var theoretical = StartPoint!.Height - EndPoint!.Height;

// Должно быть:
var theoretical = EndPoint!.Height - StartPoint!.Height;
```

**`Domain/Services/ProportionalClosureDistributor.cs`** (в `DistributeClosureWithSections`)
```csharp
// Было:
var theoretical = startPoint.Height - endPoint.Height;

// Должно быть:
var theoretical = endPoint.Height - startPoint.Height;
```

✅ После этого:
- невязка секции и хода будет считаться корректно по формуле
  **f = Σ(Δh) − (H_end − H_start)**.

### 1.2. (Рекомендуется) переопределить допуски/критерий уравнивания локально

Сейчас в `CalculateHeightsHandler` поправки по секциям применяются **только если невязка в допуске**:
```csharp
if (command.Mode == AdjustmentMode.Local && run.Closure?.IsWithinTolerance == true)
    _closureDistributor.DistributeClosureWithSections(run);
```

Ожидаемое поведение обычно:
- если “Локальное уравнивание” включено — распределяем невязку **всегда**,  
- а допуск используем для статуса/подсветки, но не для запрета уравнивания.

Решение: изменить условие на:
```csharp
if (command.Mode == AdjustmentMode.Local && run.Closure != null)
    _closureDistributor.DistributeClosureWithSections(run);
```

---

## 2) Вынести “построение станций” из легаси в DDD (и убрать виртуальные станции)

Самое важное: **DDD должен уметь построить `LevelingNetwork` из импортированных `MeasurementRecord`** без участия `TraverseCalculationService`.

### 2.1. Создать Application use-case: BuildNetworkFromMeasurements

Создайте новый сценарий в Application:

- `Application/Commands/BuildNetworkCommand.cs`
- `Application/Commands/Handlers/BuildNetworkHandler.cs`
- (опционально) `Application/Builders/NetworkBuilder.cs` — как чистую функцию.

**Вход**:
- `IReadOnlyList<Domain.Model.MeasurementRecord> records` (из парсера)
- `IReadOnlyDictionary<string,double> knownHeights` (из DataViewModel)
- `IReadOnlyDictionary<string,bool> sharedPointStates` (если поддерживается)
- имя проекта

**Выход**:
- `Guid networkId`

### 2.2. Логика построения: перенести pairing из TraverseCalculationService

В легаси `TraverseCalculationService.BuildTraverseRowsInternal` реализована ключевая логика:
- группировка по ходам,
- pairing записей `Rb` и `Rf` в одну “станцию”,
- учёт режима `BF/FB`.

Эту логику **не переписываем “по-новому”**, а переносим как есть, но вместо `StationDto` создаём **Domain Observations**:

- `Point from` = back point
- `Point to` = fore point
- `Reading backReading` = `Rb_m`
- `Reading foreReading` = `Rf_m`
- `Distance backDistance` / `foreDistance`
- `Run` = соответствующий ход
- `StationIndex` = 1..N

### 2.3. Полное удаление виртуальных станций

В легаси “виртуальная станция” появляется из двух причин:
1) В конце хода остаётся `pending`, и его добавляют даже если пара неполная:
```csharp
if (pending != null) list.Add(pending);
```
2) В `TraverseProcessingService` есть `UpdateVirtualStations(...)`.

В DDD‑строителе делаем жёстко:

- если пара `Rb/Rf` не собрана → **не создаём Observation**
- такие случаи фиксируем как ошибку импорта (и/или показываем пользователю):
  - “незакрытая пара отсчётов”
  - “нет Rf после Rb” и т.п.

То есть:
- **ни одной Observation без обеих рейек быть не может**
- если данные неполные — это **validation error**, а не “виртуальная станция”.

### 2.4. Где брать группы ходов

Используйте уже существующий сервис:
- `Application/Services/RunAnnotationService.AnnotateRuns(...)`

Он уже повторяет группировку по `Start-Line/End-Line/Seq gap`.

---

## 3) Переключение UI: расчёт/таблица/CSV должны читать из DDD‑сети

### 3.1. Заменить TraverseCalculationViewModel на NetworkViewModel как источник данных

Сейчас:
- DataViewModel импортирует `MeasurementRecord`
- TraverseCalculationViewModel строит `StationDto` через TraverseCalculationService/TraverseProcessingService

Нужно:
1) После импорта вызывать `BuildNetworkHandler` → получить `networkId`
2) Передавать `networkId` в `NetworkViewModel`
3) Вызов расчёта → `CalculateHeightsHandler.HandleAsync(new CalculateHeightsCommand(networkId, выбранный режим))`
4) Таблицы/журнал/визуализация берутся из:
   - `NetworkViewModel.Runs` (NetworkRunSummaryDto)
   - `NetworkViewModel.Observations` (ObservationDto)

> В проекте `NetworkViewModel` уже есть, но сейчас он:
> - создаёт пустую сеть вручную
> - считает только Local (хардкод)
> Его надо сделать “главным”, а не proof-of-concept.

### 3.2. Режим уравнивания должен реально переключаться

В `NetworkViewModel.CalculateHeightsAsync()` сейчас:
```csharp
new CalculateHeightsCommand(_networkId, AdjustmentMode.Local)
```
Заменить на значение из Settings (или UI переключателя):
- None / Local / Network

### 3.3. Журнал (табличка) без StationDto

Если ваш журнал UI жёстко привязан к `JournalRow/TraverseRow`, есть два варианта:

**Вариант А (правильный, DDD‑native):**
- сделать новый `NetworkJournalRow` на базе `ObservationDto`
- переписать `TraverseJournalView.xaml` на `Observations`

**Вариант B (быстрый, но всё равно без легаси расчёта):**
- оставить визуальные компоненты, но **перемапить** `ObservationDto → StationDto` **только как display DTO**
- важно: никакой математики в StationDto, только отображение

---

## 4) Новый экспорт CSV из DDD (и удаление TraverseExportService)

### 4.1. Создать новый порт (Application)

`Application/Export/INetworkCsvExportService.cs`
```csharp
public interface INetworkCsvExportService
{
    string BuildCsv(LevelingNetwork network);
}
```

### 4.2. Реализация в Infrastructure

`Infrastructure/Export/NetworkCsvExportService.cs`:
- группировка по `network.Runs`
- строки строятся по `run.Observations`
- “StartPoint”/“EndPoint” берутся из `run.StartPoint` / `run.EndPoint`
- поправка в мм: `obs.Correction * 1000`
- высоты: `obs.From.Height`, `obs.To.Height`

Важное:
- экспорт должен писать **и Z0, и Z** (до/после уравнивания)
- Z0 = по “сырым” Δh без correction
- Z = по adjusted Δh

### 4.3. Удалить TraverseExportService

После переключения экспорта:
- удалить `Infrastructure/Export/TraverseExportService.cs`
- удалить `Application/Export/ITraverseExportService.cs`
- убрать регистрацию из DI

---

## 5) После вырезания легаси: что физически удалить и где почистить DI

### 5.1. Удаляем файлы/типы (после переключения UI+экспорта)

**Application**
- `Application/Services/TraverseCalculationService.cs`
- `Application/Services/TraverseProcessingService.cs`
- `Application/DTOs/TraverseProcessingRequest.cs`
- `Application/DTOs/TraverseProcessingResult.cs`
- (всё, что использовалось только для этого пайплайна)

**Domain**
- `Domain/Services/TraverseCorrectionService.cs`
- `Domain/Services/SystemConnectivityService.cs`

**Infrastructure**
- `Infrastructure/Export/TraverseExportService.cs`

**Presentation**
- `TraverseCalculationViewModel` / `TraverseJournalViewModel` / `TraverseDesignViewModel`
  (или оставить только оболочки, если они стали тупыми отображалками DDD‑DTO)

### 5.2. Чистим DI

`Presentation/DependencyInjection.cs`

Убрать регистрации:
- `ITraverseCalculationService`
- `ITraverseProcessingService`
- `ISystemConnectivityService`
- `ITraverseCorrectionService`
- `ITraverseExportService`
- соответствующие ViewModel

Оставить/добавить:
- `INetworkRepository`
- `CalculateHeightsHandler`
- `GetNetworkSummaryHandler`
- `INetworkMapper`
- `INetworkCsvExportService`

---

## 6) Регрессионные проверки (минимальный набор)

1) Импорт файла → нет виртуальных строк в таблице.
2) Режим **None**:
   - Correction = 0
   - Z = Z0 (ровно совпадают)
3) Режим **Local**:
   - если есть 2+ известные точки в ходе/секции → появляются коррекции
   - Σ(Δh_corr) совпадает с (H_end − H_start) по секции (в пределах округления)
4) Режим **Network (МНК)**:
   - высоты точек отличаются от None/Local на тестовом наборе
   - `сеть.csv` != `нет.csv` (по коррекциям и/или Z)
5) Экспорт CSV:
   - содержит корректные start/end, станции, поправки в мм, Z0/Z.

---

## 7) Почему это решает “Network = None”

Потому что **мы удаляем код**, который делал:
```csharp
if (adjustmentMode == None || Network) return Open;
```
и заставлял Network быть “как None”.

После удаления легаси:
- режим Network **всегда** проходит через `LeastSquaresNetworkAdjuster`
- UI/CSV получает данные **из доменной сети**, где коррекции реально применены.

---

## 8) Подсказка по структуре коммитов

Чтобы не утонуть, делайте поэтапно:

1) Fix sign bug (Run + Distributor) + проверить Local на простом примере
2) BuildNetworkHandler (строит LevelingNetwork из MeasurementRecord)
3) UI cutover: импорт → build → calculate → показать Runs/Observations
4) Export cutover: новый NetworkCsvExportService
5) Delete files + DI cleanup
6) Финальные проверки

