# Release Checklist — DnsSwitcher v1.5.0

Дата финализации: 2026-08-19.

## Release identity

- [x] Version metadata: `1.5.0`.
- [x] Stable tag target: `v1.5.0`.
- [x] Previous stable version: `v1.4.1`.
- [x] `Directory.Build.props`, README, CHANGELOG и release notes согласованы.

## Build и tests

Финальный publish workflow выполняет на exact release commit:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

- [x] Automated validation пройдена на release-candidate линии.
- [x] Manual Windows validation завершена.
- [x] DNS apply/reset/status проверены.
- [x] Agent / Named Pipe IPC проверены.
- [x] DNS Health Failover проверен.
- [x] Split DNS / NRPT проверен.
- [x] Tray customization проверена.
- [x] RU/EN, темы и scaling проверены.

## Packaging

Ожидаемые stable assets:

```text
DnsSwitcher-1.5.0-win-x64-setup.exe
DnsSwitcher-1.5.0-win-x64.zip
SHA256SUMS.txt
```

- [x] Installer self-contained.
- [x] Portable package self-contained.
- [x] `SHA256SUMS.txt` создаётся из фактических final assets.
- [x] Installer и portable собираются одним workflow из exact release commit.
- [x] Upgrade `v1.4.1 → v1.5.0` проверен с сохранением пользовательских данных.
- [x] Uninstall/service cleanup проверены.

## Documentation

- [x] `README.md` и `README_EN.md` синхронизированы.
- [x] `CHANGELOG.md` содержит секцию `1.5.0` от 2026-08-19.
- [x] RU/EN release notes синхронизированы.
- [x] Реальные Windows screenshots добавлены.
- [x] Исторические beta evidence не переписаны задним числом.
- [x] Private governance files исключены из публикуемого дерева.

## Known limitation

Production anonymous update discovery через GitHub Releases недоступен, пока основной repository private. Это ограничение явно указано в README и release notes; embedded GitHub credentials не используются.

## Publication

Publish workflow на `main`:

1. повторяет restore/build/test;
2. собирает installer + portable + checksums;
3. проверяет наличие и соответствие assets;
4. создаёт stable tag/release `v1.5.0` из exact commit;
5. загружает три release assets;
6. очищает устаревшие рабочие ветки после успешной публикации.

Stable assets не заменяются после публикации; исправления выпускаются отдельной версией.
