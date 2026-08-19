# Дорожная карта

Roadmap фиксирует только подтверждённые цели и не придумывает будущие версии заранее.

## v1.5.0 — Stable release

Статус: **завершено 2026-08-19**.

В `v1.5.0` завершены:

- настраиваемое Tray menu;
- финальные UI improvements Main / Settings / DNS Health / Split DNS;
- About и Help;
- manual update check и отключаемая automatic Tray check;
- stable SemVer selection;
- SHA-256 verification перед installer launch;
- self-contained installer + portable ZIP + `SHA256SUMS.txt`;
- RU/EN README и release notes;
- Windows CI, final smoke и release packaging.

## Следующая версия

Отдельный scope следующей версии пока не утверждён. Новые задачи добавляются в roadmap только после выбора конкретной цели и критериев готовности.

## Release source

Встроенная проверка обновлений использует публично читаемые GitHub Releases без embedded credentials. Release source должен быть доступен anonymous HTTPS client; при недоступном source update-check завершается как nonfatal network/update-source failure.
