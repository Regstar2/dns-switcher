# Localization standard

DnsSwitcher — пользовательское приложение; обязательные locale:

```text
ru
en
```

English — fallback.

## Правила

- Новая пользовательская функция добавляет RU и EN строки в том же scope.
- Не размещать новые пользовательские сообщения hardcoded в UI/Tray code.
- Использовать существующий `AppLocalizer`/его project extensions; не создавать параллельную i18n-систему.
- Missing optional locale должен fallback-иться на English; raw localization key не является допустимым пользовательским текстом.
- Даты/числа форматировать locale-aware там, где они являются пользовательским представлением.
- Layout обязан учитывать более длинные RU/EN строки, keyboard/accessibility labels также локализуются.

## Release gate

- [ ] новые ключи существуют в RU и EN;
- [ ] placeholders совместимы;
- [ ] critical About/Help/Update flows проверены на RU/EN;
- [ ] нет известных raw keys/clipping в новом flow.
