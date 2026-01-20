# CODEX: Полная миграция легаси в DDD и удаление старья (Nivtropy)

Этот документ — “кодекс” по доведению проекта до **рабочей DDD/Clean Architecture**, где:
- **новая архитектура — единственный рабочий путь**,  
- **легаси удалено физически**,  
- **логика вычислений сохранена**, но живёт в правильных слоях.

---

## 0) Definition of Done (DoD)

Считаем задачу завершённой, когда выполнено **всё**:

### 0.1. Архитектура
- Зависимости слоёв только такие:
  - `Presentation → Application → Domain`
  - `Infrastructure → Application → Domain`
- В `Domain/` и `Application/` нет ссылок на `Presentation`:
  - `grep -r "using Nivtropy.Presentation" Domain/ Application/` → пусто
- Infrastructure не зависит от WPF и UI моделей:
  - нет `using Microsoft.Win32` (SaveFileDialog) внутри Infrastructure
  - нет `using Nivtropy.Presentation.*` внутри Infrastructure
- В репозитории больше нет “двойной истины” (двух параллельных моделей одного и того же):
  - UI-модели остаются только как *view-model / display* сущности
  - доменная модель (Domain) — единственный источник истины

### 0.2. Функциональность
- Приложение **собирается** и **запускается**
- Поддержаны сценарии:
  - импорт/загрузка измерений
  - расчёт невязок/поправок
  - распространение высот
  - подсчёт допусков/статусов
  - экспорт результата

### 0.3. Удаление легаси
- Удалены/переписаны все “legacy” сервисы/модели так, чтобы:
  - их классов/файлов больше не существует в репо
  - новые use-cases и доменные сервисы покрывают их функционал

---

## 1) Непробиваемые правила (иначе будет откат)

1. **Никогда** не решать компиляцию добавлением `using Nivtropy.Presentation...` в Domain/Application/Infrastructure.
2. “Логику вычислений **не менять**” — переносить и переподписывать границы/типы, но не переписывать математику.
3. Любые данные для UI (таблички, display-поля, форматирование) должны жить **в Presentation**, а не в Domain/Application.
4. Domain/Application могут общаться с UI только через **DTO**.
5. Если где-то нужен `SaveFileDialog`/MessageBox — это **Presentation**, а не Infrastructure.

---

## 2) Карта слоёв: кто за что отвечает

### Domain
- Модель предметной области (сеть, точки, наблюдения)
- Value Objects (Height, Distance, PointCode и т.п.)
- Доменные сервисы (алгоритмы: корректировки, распространение высот, связность)
- Инварианты и правила предметной области

### Application
- Use cases / сценарии (Handlers, Services)
- DTO контракты для UI/инфры
- Порты (интерфейсы), например репозитории, экспорт

### Infrastructure
- Реализации портов:
  - чтение/парсинг файлов
  - сохранение/репозитории
  - генерация CSV/Excel байт/строк (без диалогов)

### Presentation
- WPF/MVVM
- ViewModels, Commands, Dialogs
- UI Models (строки таблиц, форматирование, selected-state)
- Маппинг UI ↔ DTO

---

## 3) Текущие “легаси-якоря” и почему их нужно отрезать

### 3.1. MeasurementRecord тащит UI внутрь данных
**Проблема:** MeasurementRecord хранит UI-объекты/поле сводки (LineSummary) и display-поля.  
**Решение:** разделить “сырые измерения” и “UI-аннотации”.

### 3.2. TraverseExportService в Infrastructure использует UI модели и SaveFileDialog
**Проблема:** Infrastructure зависит от Presentation и WPF.  
**Решение:** Infrastructure только генерит контент (CSV bytes/string), а путь/диалоги — Presentation.

### 3.3. TraverseBuilder — legacy сервис, который тянет Presentation
**Проблема:** алгоритм сборки станций живёт в старом сервисе с UI типами.  
**Решение:** перенести алгоритм в Application/Domain и удалить сервис.

---

## 4) Порядок миграции (делать строго так)

Порядок важен, чтобы не получилось “всё сломал и не собрать”.

**Фаза A:** DTO/Enums + маппинг  
**Фаза B:** чистка Domain (убрать UI-типы)  
**Фаза C:** чистка Application (убрать UI-типы)  
**Фаза D:** убийство MeasurementRecord-легаси (разрез данных и UI)  
**Фаза E:** перенос/удаление TraverseBuilder  
**Фаза F:** рефактор экспорта (Infrastructure без UI)  
**Фаза G:** DI/провода, чтобы реально использовался новый путь  
**Фаза H:** финальная метла: удалить старьё и поставить гейты

---

## 5) Фаза A — Контракты нового мира (DTO/Enums) и маппинг

### A1) DTO в Application/DTOs
Создать (или довести до финала):
- StationDto — базовая “станция/наблюдение” для расчётов
- RunSummaryDto — сводка по ходу (длины, невязки, допуски)
- SharedPointDto — общие точки между системами
- CorrectionInputDto — входные данные для корректировок/замыкания

**Принцип:** DTO не знает ничего про WPF, INotifyPropertyChanged, display-строки.

### A2) Enums в Application/Enums
Вынести режимы (например `TraverseClosureMode`, `AdjustmentMode`) в Application.

### A3) Маппинг в Presentation
Создать/обновить `Presentation/Mappers/DtoMapper.cs`:
- UI → DTO (для вызова use cases)
- DTO → UI (для отображения в таблицах)

---

## 6) Фаза B — Чистка Domain (UI не должен попасть внутрь)

### B1) TraverseCorrectionService
- Удалить `using Nivtropy.Presentation.*`
- Переподписать методы на DTO:
  - принимать `IReadOnlyList<StationDto>` и `CorrectionInputDto`
  - возвращать DTO (или доменные структуры без UI)

### B2) SystemConnectivityService
- Удалить зависимости от Presentation
- Использовать только DTO (`RunSummaryDto`, `SharedPointDto`) или доменные модели

**Гейт после Фазы B**
```bash
grep -r "using Nivtropy.Presentation" Domain/
```
Должно быть пусто.

---

## 7) Фаза C — Чистка Application (use cases без UI типов)

### C1) Убрать `Presentation.Models` из Application
- Все сервисы/handlers должны работать с DTO или Domain моделями.
- Если сейчас методы принимают `TraverseRow`/`LineSummary` — заменить на DTO.

### C2) Репозиторий как порт
**Важно для Clean Architecture:**
- Интерфейс репозитория (`INetworkRepository`) должен жить в Application (порт)
- Реализация — в Infrastructure (адаптер)

**Гейт после Фазы C**
```bash
grep -r "using Nivtropy.Presentation" Application/
```
Должно быть пусто.

---

## 8) Фаза D — Убить MeasurementRecord-легаси (разделить “данные” и “UI”)

### D1) Ввести чистый тип измерения
Создать **чистую модель измерения**, не зависящую от UI:
- либо в Domain (`Domain/Model/...`)
- либо как входной DTO в Application (`Application/DTOs/MeasurementDto`)

В ней оставить только “сырые” поля:
- коды/режим/номер строки/флаги
- значения отсчётов/дистанций
- вычисляемые deltaH/valid

Удалить/не переносить в этот тип:
- любые `LineSummary`
- любые display-строки/форматирование

### D2) Где будет LineSummary/сводка?
Сводки должны формироваться:
- либо отдельным Application-сервисом (например `RunAnnotationService`)
- либо use-case возвращает `RunSummaryDto`, а UI делает красивый вывод

### D3) Удалить старый файл
После перевода всех зависимостей:
- удалить legacy `Models/MeasurementRecord.cs` (или превратить его в новый слой без UI)

**Гейт после Фазы D**
```bash
grep -r "using Nivtropy.Presentation" --include="*.cs" .
```
Не должно найтись в “не-UI” слоях.

---

## 9) Фаза E — Убить TraverseBuilder (перенести логику и удалить)

### E1) Перенести алгоритм Build в новый мир
Вариант (рекомендуется):
- Встроить логику сборки станций в `Application/Services/TraverseCalculationService`
- Вход: список чистых measurement (Domain/DTO)
- Выход: `List<StationDto>` + `List<RunSummaryDto>` (если нужно)

Важно: внутри не должно быть ссылок на UI типы.

### E2) Удалить legacy файлы
После переноса логики удалить:
- `Services/ITraverseBuilder.cs`
- `Services/TraverseBuilder.cs`

---

## 10) Фаза F — Экспорт без UI (Infrastructure только генерит контент)

### F1) Новый контракт экспорта
Сделать экспорт так:
- `ITraverseExportService` в Application (порт)
- `TraverseExportService` в Infrastructure (адаптер)
- вход: DTO (`StationDto`/`RunSummaryDto`) или доменные модели
- выход: `byte[]` или `string` (CSV)

### F2) SaveFileDialog — только в Presentation
- ViewModel спрашивает путь через `IDialogService`/WPF
- затем вызывает export и пишет файл

**Гейт**
- В Infrastructure нет `Microsoft.Win32` и нет `Presentation.Models`.

---

## 11) Фаза G — Провода и защита от “воскрешения” легаси

### G1) DI
Проверить регистрацию:
- Use case сервисы — Application
- Доменные сервисы — Domain
- Реализации портов — Infrastructure
- ViewModels — Presentation

### G2) Запрет отката
Если legacy классы удалены, отката “назад” не будет физически.
Плюс добавить CI-гейты (см. ниже).

---

## 12) Фаза H — Финальная метла (удаление старья)

Удалять только после прохождения гейтов:

- `Models/` legacy слой — удалён или полностью переработан (без UI зависимостей)
- legacy services удалены
- Infrastructure очищен от UI

---

## 13) CI/хуки: обязательные гейты

Добавить как минимум:

```bash
# Запрет протекания UI вниз
grep -r "using Nivtropy.Presentation" Domain/ Application/ Infrastructure/

# Запрет WPF диалогов в Infrastructure
grep -r "Microsoft.Win32" Infrastructure/

# Сборка
dotnet build
```

Опционально:
- тестовый прогон use cases
- smoke-тест “загрузка→расчёт→экспорт”

---

## 14) Чек-лист ручной проверки (на каждый PR)

- [ ] Build зелёный (`dotnet build`)
- [ ] Запуск приложения
- [ ] Импорт измерений
- [ ] Расчёт (невязки/поправки/высоты)
- [ ] Экспорт
- [ ] `grep`-гейты проходят
- [ ] В Domain/Application/Infrastructure нет `Presentation.*`

---

## 15) Частые ошибки (не делай так)

- “Временно” добавить `using Nivtropy.Presentation` в Domain — потом это останется навсегда.
- Хранить display строки (например `LineRangeDisplay`) в доменных типах.
- Пытаться делать UI-диалоги из Infrastructure “потому что так проще”.
- Оставлять два источника истины: Domain сеть и параллельные UI-модели с расчётами.

---

## 16) Результат

После выполнения этого кодекса:
- у проекта будет единый DDD-центр (Domain),
- use cases будут жить в Application,
- инфраструктура станет чистой (ввод/вывод без UI),
- Presentation будет только отображать и вызывать сценарии,
- легаси будет удалено физически, но функциональность сохранится.
