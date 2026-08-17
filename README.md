<div align="center">

# DnsSwitcher

Приложение для Windows, которое переключает DNS-профили через desktop UI, системный трей или CLI и использует общее ядро для пользовательских клиентов.

**Русский** · [English](README_EN.md)

[![Release](https://img.shields.io/badge/release-v1.4.1-4C8BF5?style=for-the-badge)](../../releases/tag/v1.4.1)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)](#требования)
[![Documentation](https://img.shields.io/badge/docs-available-555555?style=for-the-badge)](docs/README.md)
[![License](https://img.shields.io/badge/license-MIT-2EA44F?style=for-the-badge)](LICENSE.md)

[Быстрый старт](#быстрый-старт) · [Документация](#документация) · [Релизы](../../releases)

</div>

## О проекте

`DnsSwitcher` предназначен для Windows-сценариев, где нужно быстро применять сохранённые DNS-профили, возвращаться к автоматическим настройкам и диагностировать DNS или доступность сайтов.

CLI, WPF-приложение и tray-клиент используют общие модели и сервисы. Привилегированные операции могут выполняться через Windows-службу `DnsSwitcher.Agent.Windows` по Named Pipes, чтобы не запрашивать повышение прав при каждом переключении.

## Статус проекта

Текущая версия метаданных исходного кода — `1.4.1`. Последний опубликованный GitHub Release — `v1.4.1`. В `main` после этого релиза уже есть unreleased-изменения процесса поставки, поэтому содержимое `main` не следует считать побайтово идентичным историческому tag `v1.4.1`.

Основные сценарии DNS switching, диагностики, управления профилями, DNS Health Failover и Split DNS реализованы. GitHub Actions в репозитории не настроены; README не заявляет наличие CI.

## Возможности

- применение статических DNS-профилей и возврат к DHCP;
- CLI, интерактивный консольный режим, WPF UI и tray-клиент;
- создание, редактирование, удаление, импорт и экспорт профилей;
- определение текущего DNS и выбор сетевого адаптера;
- DNS-, site- и benchmark-диагностика;
- опциональный DNS Health Failover;
- опциональный Split DNS через Windows NRPT;
- Windows-служба и Named Pipes для привилегированных операций;
- локальное хранение конфигурации, истории benchmark и логов;
- русский и английский интерфейс, системная светлая или тёмная тема.

## Быстрый старт

Для запуска из исходников на Windows:

```powershell
git clone https://github.com/Regstar2/dns-switcher.git
cd dns-switcher
dotnet restore DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Ui -c Release
```

Для изменения DNS без установленного агента запустите приложение с правами администратора. Установку и управление агентом можно выполнить из UI, tray или CLI.

## Требования

- Windows; опубликованные assets `v1.4.1` предназначены для `win-x64`;
- .NET SDK `10.0.201` или совместимый более новый .NET 10 SDK согласно `global.json` (`rollForward: latestFeature`) — для сборки из исходников;
- права администратора для установки Windows-службы и прямого изменения системных DNS-настроек;
- Inno Setup 6 — только для сборки установщика.

Минимальная версия Windows не закреплена отдельным значением в project files, поэтому совместимость с конкретными неподтверждёнными выпусками Windows не заявляется.

## Установка

Опубликованный релиз `v1.4.1` содержит два assets:

- `DnsSwitcher-1.4.1-win-x64.zip` — portable package;
- `DnsSwitcher-1.4.1-win-x64-setup.exe` — installer package.

Текущий `main` собирает installer self-contained по умолчанию. Portable release script остаётся framework-dependent, если явно не передан `-SelfContained`. Это поведение текущего `main` нельзя задним числом переносить на уже опубликованные исторические assets `v1.4.1`.

Подробности: [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md), [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md) и [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md).

## Использование

Основной сценарий:

1. Создайте или импортируйте DNS-профиль.
2. Выберите сетевой адаптер.
3. Примените профиль через UI, tray или CLI.
4. Используйте `status`, DNS test или site test для проверки.
5. Выполните `reset`, чтобы вернуть автоматический DNS.

Профили с приватными адресами и внутренними доменами следует хранить только в локальных файлах конфигурации.

## Режимы работы

- **UI** — управление профилями, диагностикой, агентом, DNS Health Failover и Split DNS.
- **Tray** — быстрое переключение и основные действия без открытия главного окна.
- **CLI** — команды для ручного использования и автоматизации.
- **Agent** — Windows-служба, выполняющая привилегированные операции через Named Pipes.

## Конфигурация

Данные хранятся в каталоге `data/` рядом с приложением:

```text
data/
  config/
    app-preferences.json
    dns-benchmark-history.json
    dns-health-settings.json
    dns-health-state.json
    profiles.json
    split-dns-rules.json
    tray-settings.json
    ui-settings.json
  logs/
    dns-switcher.log
```

Пример профилей: [`docs/profiles.example.json`](docs/profiles.example.json).

## Команды

Показать справку:

```powershell
dotnet run --project src/DnsSwitcher.Cli -- help
```

Основные команды:

```text
profiles
adapters
status
current
apply <profile-id>
reset
test
test-sites
benchmark
health <status|enable|disable|check|chain|fallback|action|domains>
split-dns <status|enable|disable|list|add|remove|update|enable-rule|disable-rule|test|apply|reset>
validate-config
service <install|reinstall|uninstall|start|stop|status>
```

Глобальные параметры: `--adapter <id|name>` и `--config <path>`.

## Архитектура

```text
UI / CLI / Tray ──> DnsSwitcher.Core
       │
       └──> DnsSwitcher.Infrastructure.Windows ──> DnsSwitcher.Core
                    │
                    ├──> DnsSwitcher.Contracts ──> DnsSwitcher.Core
                    │
                    └── Named Pipes ──> DnsSwitcher.Agent.Windows
                                            ├──> Core
                                            ├──> Contracts
                                            └──> Infrastructure.Windows
```

`DnsSwitcher.Core` содержит модели, валидацию и общие сервисы. Windows-специфичные адаптеры, DNS, файловая конфигурация, NRPT и IPC-клиент находятся в `DnsSwitcher.Infrastructure.Windows`. Привилегированный Agent зависит от Core, Contracts и Windows infrastructure.

Подробнее: [`docs/architecture/architecture.md`](docs/architecture/architecture.md) и [`docs/architecture/tech-stack.md`](docs/architecture/tech-stack.md).

## Безопасность

Изменение системных DNS-настроек является привилегированной операцией. Агент принимает запросы через локальный Named Pipe, а внешние данные профилей проходят валидацию до применения системных настроек.

Не добавляйте в репозиторий приватные DNS-профили, секреты, внутренние домены и локальные конфигурации. Каталог `data/` исключён из Git.

## Приватность

Проект хранит конфигурацию, историю benchmark и логи локально. В текущем коде и project files не заявлены обязательная телеметрия или централизованный сбор пользовательских данных. Диагностические проверки обращаются к доменам и URL, заданным в профилях или конфигурации пользователя.

## Диагностика

- `test` проверяет DNS-резолвинг доменов;
- `test-sites` последовательно проверяет DNS, TCP, TLS и HTTP;
- `benchmark` сравнивает профили и восстанавливает исходные DNS-настройки;
- `health` выполняет фоновые проверки и опциональные failover-действия;
- `split-dns test` проверяет сопоставление домена с правилом NRPT.

Логи находятся в `data/logs/dns-switcher.log`. Интеграционные проверки IPC описаны в [`docs/ipc-integration-tests.md`](docs/ipc-integration-tests.md).

## Обновление

Для portable-версии распакуйте новую сборку в отдельную папку и переносите `data/` только после резервного копирования. Для установленной версии используйте установщик той же архитектуры. Перед заменой runtime Windows-службы остановите или переустановите Agent штатной командой.

## Резервное копирование и миграция

Для переноса настроек сохраните каталог `data/config/`. Не переносите старые исполняемые файлы поверх новой версии без проверки структуры пакета.

## Разработка

Репозиторий фиксирует .NET SDK через `global.json`. Подготовка Windows-среды:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-setup.ps1
```

Скрипт устанавливает требуемый SDK при необходимости, затем выполняет restore, Release build и tests.

## Сборка

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
```

Portable package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.4.1 -Runtime win-x64
```

Installer:

```powershell
.\installer\build-installer.ps1 -Version 1.4.1 -Runtime win-x64
```

## Тестирование

Автоматические проверки, предусмотренные репозиторием:

```powershell
dotnet test DnsSwitcher.sln -c Release
```

Решение содержит unit tests и отдельный Windows-specific проект IPC integration tests. Ручные проверки описаны в [`docs/testing/manual-test-plan.md`](docs/testing/manual-test-plan.md).

## Документация

| Раздел | Документ |
|---|---|
| Индекс | [`docs/README.md`](docs/README.md) |
| Идея и границы продукта | [`docs/product/idea.md`](docs/product/idea.md), [`docs/product/mvp-scope.md`](docs/product/mvp-scope.md) |
| Проверка целесообразности | [`docs/product/feasibility.md`](docs/product/feasibility.md) |
| Дорожная карта | [`docs/product/roadmap.md`](docs/product/roadmap.md) |
| Архитектура и стек | [`docs/architecture/architecture.md`](docs/architecture/architecture.md), [`docs/architecture/tech-stack.md`](docs/architecture/tech-stack.md) |
| Версии | [`docs/versions/versions-index.md`](docs/versions/versions-index.md) |
| Ручное тестирование | [`docs/testing/manual-test-plan.md`](docs/testing/manual-test-plan.md) |
| Release notes | [`docs/releases/README.md`](docs/releases/README.md) |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) |
| Portable / installer / service | [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md), [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md), [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md) |
| DNS Health Failover / Split DNS | [`DNS_HEALTH_FAILOVER.md`](DNS_HEALTH_FAILOVER.md), [`SPLIT_DNS.md`](SPLIT_DNS.md) |

## Ограничения

- проект зависит от Windows APIs и не заявляет поддержку других ОС;
- опубликованный `v1.4.1` ориентирован на `win-x64`;
- установка Agent и прямое изменение DNS требуют прав администратора;
- Split DNS основан на Windows NRPT и может обходиться приложениями с собственным DNS/DoH;
- CLI локализован не полностью;
- актуальные screenshots интерфейса не опубликованы;
- точная минимальная версия Windows не закреплена в project files;
- GitHub Actions/CI в репозитории не настроены.

## Лицензия

Проект распространяется по лицензии MIT. См. [`LICENSE.md`](LICENSE.md).
