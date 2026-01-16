# Промпт для Codex

## Задача

Рефакторинг DDD архитектуры проекта Nivtropy. Удалить запрещённые зависимости Domain и Application слоёв от Presentation.

## Контекст

Это WPF приложение для обработки данных геометрического нивелирования. Архитектура DDD, но сейчас нарушена - Domain и Application используют UI-модели из Presentation.

## Инструкция

Прочитай файл `CODEX_DDD_REFACTORING.md` - там подробный план.

**Ключевые принципы:**

1. Domain слой НЕ ДОЛЖЕН иметь `using Nivtropy.Presentation.*`
2. Application слой НЕ ДОЛЖЕН иметь `using Nivtropy.Presentation.*`
3. Создай DTO в Application/DTOs для замены Presentation.Models
4. Логику вычислений НЕ МЕНЯЙ - только типы параметров

**Порядок работы:**

1. Создай все DTO файлы в `Application/DTOs/`
2. Создай `Application/Enums/TraverseClosureMode.cs`
3. Рефакторь `Domain/Services/TraverseCorrectionService.cs`
4. Рефакторь `Domain/Services/SystemConnectivityService.cs`
5. Рефакторь сервисы в `Application/Services/`
6. Создай `Presentation/Mappers/DtoMapper.cs`
7. Обнови вызовы в ViewModels

**ЗАПРЕЩЕНО:**

- Добавлять `using Nivtropy.Presentation.*` в Domain или Application
- Удалять методы или менять их логику
- Копировать UI-свойства (Display, INotifyPropertyChanged) в DTO

**Проверка:**

```bash
grep -r "using Nivtropy.Presentation" Domain/ Application/
# Должно быть пусто
```

## Начни с

Создай файлы DTO согласно плану в CODEX_DDD_REFACTORING.md, затем рефакторь Domain/Services/.

---

# Короткий промпт (копировать в Codex)

```
Задача: DDD рефакторинг проекта Nivtropy.

Прочитай CODEX_DDD_REFACTORING.md - там полный план.

ЦЕЛЬ: Удалить все `using Nivtropy.Presentation.*` из Domain/ и Application/.

МЕТОД: Создать DTO в Application/DTOs, заменить Presentation.Models на DTO.

ЗАПРЕЩЕНО:
- Добавлять using Nivtropy.Presentation в Domain/Application
- Удалять функционал
- Менять логику вычислений

Начни с создания DTO файлов, потом рефакторь Domain/Services/.
```
