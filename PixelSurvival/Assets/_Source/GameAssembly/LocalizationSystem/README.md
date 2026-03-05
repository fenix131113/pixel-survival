# LocalizationSystem

## Что добавлено

- `LocalizationLanguagePreset` — ScriptableObject с кодом языка (`en`, `ru`) и списком `ключ -> перевод`.
- `LocalizationDatabase` — ScriptableObject, содержащий набор языковых пресетов.
- `LocalizationService` — статическая точка доступа к переводам из любого места кода.
- `LocalizationBootstrap` — MonoBehaviour для инициализации сервиса на старте.

## Рекомендованный формат пресетов

Для Unity-проекта удобнее всего использовать **ScriptableObject пресеты**:

1. Создать пресеты языков через меню:
   - `Game/Localization/Language Preset`
2. Создать общую базу:
   - `Game/Localization/Database`
3. Добавить языковые пресеты в базу.
4. В сцене добавить `LocalizationBootstrap` и назначить базу + язык по умолчанию.

Плюсы этого подхода:
- не нужно писать парсер JSON/CSV на старте;
- удобно редактировать локализацию прямо в инспекторе;
- просто масштабировать (добавлять новые языки отдельными ассетами).

## Пример использования

```csharp
using LocalizationSystem;

var title = LocalizationService.Get("ui.main_menu.title");

LocalizationService.SetLanguage("ru");
```

Если ключ не найден, `Get` возвращает сам ключ — это упрощает поиск пропущенных переводов.
