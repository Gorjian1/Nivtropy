# Инструкция для Codex: управление уравниванием (3 режима) + правка формулы + заготовка МНК

Ниже — “анти-галлюцинаторная” инструкция для Codex по репозиторию **Nivtropy** (из `Nivtropy.zip`).  
Цели: **вынести уравнивание из автоматического применения в управление в панели**, добавить **3 режима** (нет/локальное/сеть), сделать **заготовку под сетевое уравнивание (МНК)** и **исправить формулу** распределения невязки.

---

## 0) Что надо получить в итоге (Definition of Done)

1) В правой панели (строка “Метод | Класс | Экспорт”) появляется блок “уравнивание” с 3 переключателями:
- **нет**
- **локальное**
- **сеть (МНК)**

2) **Локальное уравнивание НЕ включается автоматически**, когда в ходе встречается >1 известной высоты/репера. Оно включается **только** если выбран режим **“локальное”**.

3) Режим **“сеть (МНК)”** пока **не делает настоящего МНК**, но:
- режим есть в UI,
- он проходит по коду без падений,
- это явная “заготовка”: в коде есть интерфейс/класс под сетевое уравнивание и TODO.

4) Исправлена формула в `Domain/Services/ProportionalClosureDistributor.cs`: сейчас поправки **накопительные** (ошибка), нужно **распределение невязки по длинам станций** (поправка на каждое наблюдение).

---

## 1) Добавить enum режима уравнивания (один источник правды)

### Создать файл
**`Application/Enums/AdjustmentMode.cs`**
```csharp
namespace Nivtropy.Application.Enums
{
    public enum AdjustmentMode
    {
        None = 0,    // без уравнивания
        Local = 1,   // локальное (по ходам / секциям)
        Network = 2  // сетевое (МНК) - пока заглушка
    }
}
```

---

## 2) UI: добавить 3 кнопки в панель в `TraverseJournalView.xaml`

Файл: **`Presentation/Views/TraverseJournalView.xaml`**

### 2.1. Добавить xmlns для enum (в шапке UserControl)
В начале файла, где уже есть `xmlns:converters=...`, добавить:
```xml
xmlns:enums="clr-namespace:Nivtropy.Application.Enums"
```

### 2.2. Добавить EnumToBoolConverter в ресурсы
В `<UserControl.Resources>` добавить:
```xml
<converters:EnumToBoolConverter x:Key="EnumToBoolConverter" />
```

### 2.3. Найти блок “СТРОКА 1: Метод | Класс | Экспорт…”
Он начинается так:
```xml
<!-- СТРОКА 1: Метод | Класс | Экспорт (в один ряд) -->
<Grid Margin="0,0,0,8">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="4" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="4" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
```

Заменить `ColumnDefinitions` на 7 колонок (вставляем новый блок перед экспортом):
```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="4" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="4" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="4" />
    <ColumnDefinition Width="Auto" />
</Grid.ColumnDefinitions>
```

### 2.4. Вставить новый Border “уравнивание” в `Grid.Column="4"`
Сразу после блока “Класс” (в `Grid.Column="2"`) вставить:

```xml
<!-- Уравнивание -->
<Border Grid.Column="4"
        BorderBrush="#FFBCC7D3"
        BorderThickness="1"
        CornerRadius="2"
        Padding="8"
        Background="White">
    <Border.Effect>
        <DropShadowEffect Color="#40000000" BlurRadius="3" ShadowDepth="1" Direction="270" Opacity="0.25"/>
    </Border.Effect>

    <StackPanel>
        <TextBlock Text="уравнивание"
                   FontSize="9"
                   Foreground="#2B579A"
                   FontWeight="SemiBold"
                   Margin="0,0,0,6" />

        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">

            <RadioButton Content="нет"
                         GroupName="AdjMode"
                         Margin="0,0,10,0"
                         IsChecked="{Binding Calculation.AdjustmentMode,
                                             Converter={StaticResource EnumToBoolConverter},
                                             ConverterParameter={x:Static enums:AdjustmentMode.None},
                                             Mode=TwoWay}" />

            <RadioButton Content="локальное"
                         GroupName="AdjMode"
                         Margin="0,0,10,0"
                         IsChecked="{Binding Calculation.AdjustmentMode,
                                             Converter={StaticResource EnumToBoolConverter},
                                             ConverterParameter={x:Static enums:AdjustmentMode.Local},
                                             Mode=TwoWay}" />

            <RadioButton Content="сеть (МНК)"
                         GroupName="AdjMode"
                         IsChecked="{Binding Calculation.AdjustmentMode,
                                             Converter={StaticResource EnumToBoolConverter},
                                             ConverterParameter={x:Static enums:AdjustmentMode.Network},
                                             Mode=TwoWay}" />
        </StackPanel>
    </StackPanel>
</Border>
```

### 2.5. Сдвинуть “Экспорт” в новую колонку
То, что было `Grid.Column="4"` (экспорт), поменять на `Grid.Column="6"`.

---

## 3) ViewModel: добавить свойство AdjustmentMode и пересчёт при смене

Файл: **`Presentation/ViewModels/TraverseCalculationViewModel.cs`**

### 3.1. Добавить using
Вверху добавить:
```csharp
using Nivtropy.Application.Enums;
```

### 3.2. Добавить поле и property
Рядом с `_selectedMethod`, `_selectedClass` добавить:
```csharp
private AdjustmentMode _adjustmentMode = AdjustmentMode.Local; // по умолчанию сохраняем старое поведение
```

Добавить публичное свойство:
```csharp
public AdjustmentMode AdjustmentMode
{
    get => _adjustmentMode;
    set
    {
        if (SetField(ref _adjustmentMode, value))
        {
            // При смене режима уравнивания пересчитываем всё
            if (UseAsyncCalculations)
                _ = UpdateRowsAsync();
            else
                UpdateRows();
        }
    }
}
```

### 3.3. Прокинуть режим в расчёт поправок
В методе `CalculateCorrections(List<TraverseRow> items, Func<string,bool> isAnchor)` найти вызов:
```csharp
var result = _correctionService.CalculateCorrections(
    stations,
    code => !string.IsNullOrWhiteSpace(code) && isAnchor(code!),
    MethodOrientationSign,
    lineSummary?.UseLocalAdjustment ?? false);
```

Заменить на:
```csharp
var result = _correctionService.CalculateCorrections(
    stations,
    code => !string.IsNullOrWhiteSpace(code) && isAnchor(code!),
    MethodOrientationSign,
    AdjustmentMode);
```

---

## 4) Убрать “автоматику локального уравнивания” в сервисе поправок (legacy)

Файл: **`Services/Calculation/TraverseCorrectionService.cs`**

### 4.1. Добавить using и поменять сигнатуру интерфейса
Вверху добавить:
```csharp
using Nivtropy.Application.Enums;
```

В интерфейсе `ITraverseCorrectionService` заменить:
```csharp
CorrectionCalculationResult CalculateCorrections(
    IReadOnlyList<StationCorrectionInput> stations,
    Func<string?, bool> isAnchor,
    double methodOrientationSign,
    bool useLocalAdjustment);
```
на:
```csharp
CorrectionCalculationResult CalculateCorrections(
    IReadOnlyList<StationCorrectionInput> stations,
    Func<string?, bool> isAnchor,
    double methodOrientationSign,
    AdjustmentMode adjustmentMode);
```

И такую же замену сделать в реализации `TraverseCorrectionService.CalculateCorrections(...)`.

### 4.2. Внутри CalculateCorrections: определить режим замыкания без “автоматики”
Найти строку:
```csharp
var closureMode = DetermineClosureMode(stations, isAnchor, anchorPoints, useLocalAdjustment);
```
Заменить на:
```csharp
var closureMode = DetermineClosureMode(stations, isAnchor, anchorPoints, adjustmentMode);
```

### 4.3. Добавить обработку `TraverseClosureMode.Open` в switch
Добавить кейс **Open**, чтобы считать невязку (но не раздавать поправки):
```csharp
case TraverseClosureMode.Open:
{
    var closure = stations.Where(s => s.DeltaH.HasValue)
                          .Sum(s => s.DeltaH!.Value * methodOrientationSign);
    result.Closures.Add(closure);
    break;
}
```

### 4.4. Переписать DetermineClosureMode (ключевой пункт “не включать локальное автоматически”)
Заменить сигнатуру:
```csharp
private static TraverseClosureMode DetermineClosureMode(..., bool useLocalAdjustment)
```
на:
```csharp
private static TraverseClosureMode DetermineClosureMode(
    IReadOnlyList<StationCorrectionInput> stations,
    Func<string?, bool> isAnchor,
    List<(int Index, string? Code)> anchorPoints,
    AdjustmentMode adjustmentMode)
```

И заменить тело на логику:

```csharp
var startCode = stations.FirstOrDefault()?.BackCode ?? stations.FirstOrDefault()?.ForeCode;
var endCode = stations.LastOrDefault()?.ForeCode ?? stations.LastOrDefault()?.BackCode;

bool startKnown = !string.IsNullOrWhiteSpace(startCode) && isAnchor(startCode);
bool endKnown = !string.IsNullOrWhiteSpace(endCode) && isAnchor(endCode);

bool closesByLoop =
    !string.IsNullOrWhiteSpace(startCode) &&
    !string.IsNullOrWhiteSpace(endCode) &&
    string.Equals(startCode, endCode, StringComparison.OrdinalIgnoreCase);

bool isClosed = closesByLoop || (startKnown && endKnown);

var distinctAnchorCount = anchorPoints
    .Select(a => a.Code)
    .Where(c => !string.IsNullOrWhiteSpace(c))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Count();

// 1) Если режим НЕ Local — никаких секций и никаких поправок
if (adjustmentMode == AdjustmentMode.None || adjustmentMode == AdjustmentMode.Network)
{
    // Возвращаем Open, чтобы гарантированно не считались поправки
    return TraverseClosureMode.Open;
}

// 2) Режим Local
if (!isClosed)
{
    // Локальные секции возможны только если есть минимум 2 репера/якоря
    return distinctAnchorCount >= 2 ? TraverseClosureMode.Local : TraverseClosureMode.Open;
}

// Замкнутый ход:
// - если репер один (петля) → Simple
// - если реперов >1 → Local
return distinctAnchorCount > 1 ? TraverseClosureMode.Local : TraverseClosureMode.Simple;
```

---

## 5) Заготовка под “уравнивание сети (МНК)” (без реализации)

### 5.1. Создать интерфейс и результат
Файлы (например):
- **`Domain/Services/INetworkAdjuster.cs`**
- **`Domain/Services/LeastSquaresNetworkAdjuster.cs`**

`INetworkAdjuster.cs`:
```csharp
using Nivtropy.Domain.Model;

namespace Nivtropy.Domain.Services
{
    public record NetworkAdjustmentResult(bool Performed, string Message);

    public interface INetworkAdjuster
    {
        NetworkAdjustmentResult Adjust(LevelingNetwork network);
    }
}
```

`LeastSquaresNetworkAdjuster.cs`:
```csharp
using Nivtropy.Domain.Model;

namespace Nivtropy.Domain.Services
{
    public class LeastSquaresNetworkAdjuster : INetworkAdjuster
    {
        public NetworkAdjustmentResult Adjust(LevelingNetwork network)
        {
            // TODO(MNK): Реализовать МНК-уравнивание сети:
            // - неизвестные: высоты точек
            // - наблюдения: dH
            // - уравнения: H_to - H_from = dH + v
            // - закрепления: известные реперы (constraints)
            // - веса: по длинам/классу/сигме (по проекту)
            return new NetworkAdjustmentResult(false, "Сетевое уравнивание (МНК) пока не реализовано.");
        }
    }
}
```

### 5.2. Зарегистрировать в DI
Файл: **`Services/ServiceCollectionExtensions.cs`** в `AddDomainServices(...)` добавить:
```csharp
services.AddSingleton<INetworkAdjuster, LeastSquaresNetworkAdjuster>();
```

---

## 6) Новая архитектура (DDD): вынести уравнивание из “автомата” в режим команды

### 6.1. Обновить команду
Файл: **`Application/Commands/CalculateHeightsCommand.cs`**

Было:
```csharp
public record CalculateHeightsCommand(Guid NetworkId);
```

Сделать:
```csharp
using Nivtropy.Application.Enums;

public record CalculateHeightsCommand(Guid NetworkId, AdjustmentMode Mode);
```

### 6.2. Обновить вызов в NetworkViewModel
Файл: **`Presentation/ViewModels/NetworkViewModel.cs`** в `CalculateHeightsAsync()` заменить:
```csharp
new CalculateHeightsCommand(_networkId)
```
на:
```csharp
new CalculateHeightsCommand(_networkId, AdjustmentMode.Local) // позже можно прокинуть из UI
```
и добавить `using Nivtropy.Application.Enums;`.

### 6.3. Обновить handler: ветвление по Mode
Файл: **`Application/Commands/Handlers/CalculateHeightsHandler.cs`**

- добавить `INetworkAdjuster` в конструктор,
- внутри `HandleAsync`:

Требуемая логика (строго):
1) `network.ResetCorrections();` — всегда.
2) Если `command.Mode == AdjustmentMode.None`:
   - НЕ вызывать `_closureDistributor...`
3) Если `command.Mode == AdjustmentMode.Local`:
   - как сейчас (после фикса формулы distributor будет корректно)
4) Если `command.Mode == AdjustmentMode.Network`:
   - вызвать `_networkAdjuster.Adjust(network)`
   - пока результат `Performed=false`, не падать и не ломать расчёт.

---

## 7) Правка формулы (обязательная): `ProportionalClosureDistributor.cs`

Файл: **`Domain/Services/ProportionalClosureDistributor.cs`**

### 7.1. DistributeClosure: убрать накопление
Заменить накопительную логику на “поправку на станцию”:

**Было (накопительно, ошибочно):**
```csharp
var accumulatedLength = 0.0;
foreach (var obs in run.Observations)
{
    accumulatedLength += obs.StationLength.Meters;
    var correction = -closureMeters * (accumulatedLength / totalLength);
    obs.ApplyCorrection(correction - obs.Correction);
}
```

**Стало (правильно — по длинам каждой станции):**
```csharp
foreach (var obs in run.Observations)
{
    var w = obs.StationLength.Meters / totalLength;
    var target = -closureMeters * w;              // именно “на это наблюдение”
    obs.ApplyCorrection(target - obs.Correction); // выставляем в target (через дельту)
}
```

### 7.2. DistributeClosureWithSections: тоже убрать накопление + выставлять через дельту
**Было (накопительно, ошибочно):**
```csharp
var accumulatedLength = 0.0;
foreach (var obs in sectionObs)
{
    accumulatedLength += obs.StationLength.Meters;
    var correction = -closure * (accumulatedLength / sectionLength);
    obs.ApplyCorrection(correction);
}
```

**Стало (правильно — по длинам каждой станции секции):**
```csharp
foreach (var obs in sectionObs)
{
    var w = obs.StationLength.Meters / sectionLength;
    var target = -closure * w;
    obs.ApplyCorrection(target - obs.Correction);
}
```
