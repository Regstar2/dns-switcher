<div align="center">

# DnsSwitcher

Windows-утилита для DNS-профилей, диагностики, Health Failover и Split DNS.

**Русский** · [English](README_EN.md)

[![Source version](https://img.shields.io/badge/source-v1.5.0-4C8BF5?style=for-the-badge)](Directory.Build.props)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?style=for-the-badge&logo=windows&logoColor=white)](#требования)
[![CI](https://img.shields.io/badge/CI-Windows-555555?style=for-the-badge)](.github/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-2EA44F?style=for-the-badge)](LICENSE.md)

[Быстрый старт](#быстрый-старт) · [Обновления](#обновления) · [Документация](#документация) · [GitHub Releases](https://github.com/Regstar2/dns-switcher/releases)

</div>

## О проекте

DnsSwitcher переключает сохранённые DNS-профили через WPF UI, системный трей или CLI, умеет возвращать автоматический DNS и выполнять DNS/site/benchmark-диагностику. Для привилегированных операций используется Windows Agent по Named Pipes.

Версия исходного кода в ветке подготовки релиза — `1.5.0`. Stable tag/release `v1.5.0` ещё не создаётся этим PR: публикация выполняется отдельно после закрытия release gates.

## Возможности

- статические DNS-профили и возврат к автоматическому DNS;
- WPF UI, настраиваемое tray-меню и CLI;
- создание, редактирование, импорт и экспорт профилей;
- DNS-, site- и benchmark-диагностика;
- опциональный DNS Health Failover;
- опциональный Split DNS через Windows NRPT;
- Windows Agent для привилегированных операций;
- RU/EN, System/Light/Dark theme;
- разделы **О приложении** и **Помощь** со ссылкой на GitHub;
- ручная и отключаемая автоматическая проверка обновлений;
- безопасная загрузка installer только после проверки SHA-256.

## Быстрый старт

Из исходников на Windows:

```powershell
git clone https://github.com/Regstar2/dns-switcher.git
cd dns-switcher
dotnet restore DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Ui -c Release
```

Для release build:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

## Требования

- Windows x64;
- .NET 10 SDK согласно `global.json` — только для сборки из исходников;
- административные права для установки Agent и прямых системных DNS/NRPT операций;
- Inno Setup 6 — только на машине сборки installer.

Installer собирается self-contained и не должен требовать отдельной установки .NET Desktop Runtime на целевой машине.

## Установка и assets

Финальный `v1.5.0` готовится с тремя release assets:

```text
DnsSwitcher-1.5.0-win-x64-setup.exe
DnsSwitcher-1.5.0-win-x64.zip
SHA256SUMS.txt
```

До фактической публикации используйте только artifacts, связанные с конкретным commit SHA. Исторический опубликованный `v1.4.1` не переписывается.

Сборка installer:

```powershell
.\installer\build-installer.ps1 -Version 1.5.0 -Runtime win-x64
```

Скрипт также создаёт `SHA256SUMS.txt` для installer и portable ZIP.

## Использование

1. Создайте или импортируйте DNS-профиль.
2. Выберите сетевой адаптер.
3. Примените профиль через UI, Tray или CLI.
4. Проверьте состояние через `status` или диагностику.
5. Используйте reset для возврата к автоматическому DNS.

Основные CLI-команды:

```text
profiles
adapters
status
apply <profile-id>
reset
test
test-sites
benchmark
health <...>
split-dns <...>
service <install|reinstall|uninstall|start|stop|status>
```

## Обновления

В Settings доступны:

- **Проверить обновления** — ручная проверка;
- **Автоматически проверять обновления** — пользовательская опция, включённая по умолчанию.

Tray выполняет фоновую проверку с persisted throttle; обычные сетевые ошибки не блокируют запуск и не показывают error-dialog. Stable channel игнорирует draft/prerelease releases.

Update delivery использует официальный GitHub Releases API без токена. При наличии новой версии DnsSwitcher выбирает только asset `DnsSwitcher-<version>-win-x64-setup.exe`, загружает `SHA256SUMS.txt`, проверяет SHA-256 и только затем позволяет запустить Inno Setup installer через Windows/UAC.

**Текущий release gate:** репозиторий остаётся private, поэтому production-клиент не может анонимно читать его Releases. В приложение не встроен PAT или другой secret. До публично читаемого release source update gate остаётся `BLOCKED`.

Подробнее: [архитектура](docs/architecture/architecture.md) и [правило update delivery](.project-rules/AUTO_UPDATE_STANDARD.md).

## Конфигурация

Runtime data хранятся в `data/`:

```text
data/
  config/
    app-preferences.json
    profiles.json
    tray-settings.json
    ui-settings.json
    dns-health-settings.json
    dns-health-state.json
    split-dns-rules.json
    update-state.json
  logs/
```

`app-preferences.json` хранит в том числе пользовательскую настройку automatic update checks. `update-state.json` содержит только throttle/последнюю уведомлённую версию и не содержит токенов или installer binaries.

## Скриншоты

Для `v1.5.0` требуются реальные Windows screenshots финального UI. В текущей ветке они намеренно не заменены mockup/Figma/синтетическими изображениями: `SCREENSHOTS REQUIRED — awaiting real Windows capture`.

После capture ожидаются файлы в `docs/assets/screenshots/`: Main, Tray, Settings/About, DNS Health и Split DNS.

## Архитектура

```text
UI / CLI / Tray
       │
       ├──> DnsSwitcher.Core
       └──> DnsSwitcher.Infrastructure.Windows
                    ├── Windows DNS / NRPT / storage
                    ├── update delivery
                    └── Named Pipes ──> DnsSwitcher.Agent.Windows
```

Core содержит модели и orchestration; Windows-specific API/IO находятся в Infrastructure.Windows. Update flow также разделён: SemVer/models — Core, GitHub/download/checksum/installer launch — Infrastructure.Windows, presentation — UI/Tray.

## Безопасность и приватность

- не добавляйте в репозиторий приватные DNS-профили, внутренние домены и локальные config/log files;
- update client не содержит GitHub credentials;
- скачанный installer не запускается при SHA-256 mismatch;
- update URL не является произвольной командой: принимаются только ожидаемые HTTPS GitHub release paths для configured repository;
- автоматическую сетевую проверку обновлений можно отключить.

## Тестирование

Автоматические проверки:

```powershell
dotnet test DnsSwitcher.sln -c Release
```

Windows CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml). Финальные installer/portable/checksums собираются [`.github/workflows/release-candidate.yml`](.github/workflows/release-candidate.yml) из exact candidate commit.

Системные сценарии: [`docs/testing/manual-test-plan.md`](docs/testing/manual-test-plan.md). Финальный About/Update smoke: [`docs/testing/v1.5.0-final-smoke.md`](docs/testing/v1.5.0-final-smoke.md).

## Документация

| Раздел | Документ |
|---|---|
| Индекс | [`docs/README.md`](docs/README.md) |
| Архитектура | [`docs/architecture/architecture.md`](docs/architecture/architecture.md) |
| Roadmap | [`docs/product/roadmap.md`](docs/product/roadmap.md) |
| Версии | [`docs/versions/versions-index.md`](docs/versions/versions-index.md) |
| Release notes v1.5.0 | [`RU`](docs/releases/v1.5.0.md) · [`EN`](docs/releases/v1.5.0_EN.md) |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) |
| Installer / portable | [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md) · [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md) |
| DNS Health / Split DNS | [`DNS_HEALTH_FAILOVER.md`](DNS_HEALTH_FAILOVER.md) · [`SPLIT_DNS.md`](SPLIT_DNS.md) |

## Ограничения

- Windows-specific проект; поддержка других ОС не заявляется;
- Split DNS основан на Windows NRPT и может обходиться приложениями со своим DNS/DoH stack;
- точная минимальная версия Windows не закреплена отдельным project property;
- production update source для `v1.5.0` заблокирован, пока repository/release channel недоступен анонимно;
- финальные реальные screenshots ещё требуют Windows capture.

## Лицензия

MIT — [`LICENSE.md`](LICENSE.md).
