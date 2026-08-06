<div align="center">

# DnsSwitcher

Портативное приложение для Windows, которое переключает DNS-профили через desktop UI, системный трей или CLI и использует общее ядро для всех клиентов.

![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![Version](https://img.shields.io/badge/version-1.4.1-blue)
[![License: MIT](https://img.shields.io/badge/license-MIT-2EA44F)](LICENSE.md)

**Русский** · [English](README_EN.md)

[Быстрый старт](#быстрый-старт) · [Документация](#документация) · [Релизы](../../releases)

</div>

## О проекте

`DnsSwitcher` предназначен для пользователей Windows, которым нужно быстро применять сохранённые DNS-профили, возвращаться к автоматическим настройкам и диагностировать проблемы с DNS или доступностью сайтов.

CLI, WPF-приложение и tray-клиент используют общие модели и сервисы. Привилегированные операции могут выполняться через Windows-службу `DnsSwitcher.Agent.Windows`, чтобы не запрашивать повышение прав при каждом переключении.

## Статус проекта

Текущая версия исходного кода — `1.4.1`. Реализованы основные сценарии переключения DNS, диагностики, управления профилями, DNS Health Failover и Split DNS. Проект поддерживает только Windows и использует `.NET 10`.

Сборка и тесты не запускались в рамках изменения документации; ниже приведены команды, зафиксированные в репозитории.

## Возможности

- применение статических DNS-профилей и возврат к DHCP;
- CLI, интерактивный консольный режим, WPF UI и tray-клиент;
- создание, редактирование, удаление, импорт и экспорт профилей;
- определение текущего DNS и выбор сетевого адаптера;
- DNS-, site- и benchmark-диагностика;
- опциональный DNS Health Failover;
- опциональный Split DNS через Windows NRPT;
- Windows-служба и Named Pipes для привилегированных операций;
- переносимое хранение конфигурации и логов рядом с приложением;
- русский и английский интерфейс, системная светлая или тёмная тема.

## Быстрый старт

Требуется Windows и установленный `.NET 10 SDK`.

```powershell
git clone https://github.com/Regstar2/DnsSwitcher.git
cd DnsSwitcher
dotnet restore DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Ui -c Release
```

Для изменения DNS без установленного агента запустите приложение с правами администратора. Установку и управление агентом можно выполнить из UI, tray или CLI.

## Требования

- Windows 10/11 или совместимая Windows-среда;
- `.NET 10 SDK` для сборки из исходников;
- права администратора для установки службы и прямого изменения системных DNS-настроек;
- Inno Setup 6 — только для сборки установщика.

## Установка

Доступны два сценария поставки:

- portable-пакет, который хранит данные внутри собственной папки;
- установщик Inno Setup с регистрацией приложения и поддержкой службы.

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
CLI / WPF UI / Tray
        │
        ▼
DnsSwitcher.Core
        │
        ├── DnsSwitcher.Infrastructure.Windows
        └── DnsSwitcher.Contracts ── Named Pipes ── DnsSwitcher.Agent.Windows
```

Domain-логика и сценарии диагностики находятся в `DnsSwitcher.Core`; Windows-специфичная работа с адаптерами, DNS, файлами и IPC вынесена в инфраструктурный проект. Подробнее: [`docs/architecture/README.md`](docs/architecture/README.md).

## Безопасность

Изменение системных DNS-настроек является привилегированной операцией. Агент принимает запросы через локальный Named Pipe, а входные данные профилей валидируются до выполнения системных команд.

Не добавляйте в репозиторий приватные DNS-профили, секреты, внутренние домены и локальные конфигурации. Каталог `data/` исключён из Git.

## Приватность

Проект хранит конфигурацию, историю benchmark и логи локально. В репозитории не заявлены телеметрия или централизованный сбор пользовательских данных. Диагностические проверки обращаются к доменам и URL, заданным в профилях или конфигурации пользователя.

## Диагностика

- `test` проверяет DNS-резолвинг доменов;
- `test-sites` последовательно проверяет DNS, TCP, TLS и HTTP;
- `benchmark` сравнивает профили и восстанавливает исходные DNS-настройки;
- `health` выполняет фоновые проверки и опциональные failover-действия;
- `split-dns test` проверяет сопоставление домена с правилом NRPT.

Логи находятся в `data/logs/dns-switcher.log`. Интеграционные проверки IPC описаны в [`docs/ipc-integration-tests.md`](docs/ipc-integration-tests.md).

## Обновление

Для portable-версии распакуйте новую сборку в отдельную папку и перенесите каталог `data/` после резервного копирования. Для установленной версии используйте новый установщик той же архитектуры. Перед обновлением службы остановите агент или выполните переустановку через штатную команду.

## Резервное копирование и миграция

Для переноса настроек сохраните каталог `data/config/`. Не переносите старые исполняемые файлы поверх новой версии без проверки структуры пакета.

## Разработка

Подготовка Windows-среды:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-setup.ps1
```

Решение разделено на `src/` и `tests/`; общие настройки сборки и версия находятся в `Directory.Build.props`.

## Сборка

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
```

Portable-пакет:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.4.1 -Runtime win-x64
```

Установщик:

```powershell
.\installer\build-installer.ps1 -Version 1.4.1 -Runtime win-x64
```

## Тестирование

```powershell
dotnet test DnsSwitcher.sln -c Release
```

Решение содержит unit-тесты и отдельный проект интеграционных тестов IPC. Ручные проверки и ограничения среды следует фиксировать в [`docs/testing/`](docs/testing/README.md).

## Документация

- [Индекс документации](docs/README.md)
- [Архитектура](docs/architecture/README.md)
- [Release notes](docs/releases/README.md)
- [Changelog](CHANGELOG.md)
- [Portable release](PORTABLE_RELEASE.md)
- [Установка службы](SERVICE_INSTALL.md)
- [Установщик](INSTALLER_RELEASE.md)
- [DNS Health Failover](DNS_HEALTH_FAILOVER.md)
- [Split DNS](SPLIT_DNS.md)
- [Готовность к Microsoft Store](STORE_READINESS.md)

## Ограничения

- поддерживается только Windows;
- установка агента и прямое изменение DNS требуют прав администратора;
- Split DNS основан на Windows NRPT и может обходиться приложениями с собственным DNS/DoH;
- CLI локализован не полностью;
- актуальные скриншоты интерфейса пока не опубликованы;
- совместимость с неподтверждёнными версиями Windows и архитектурами не заявляется.

## Лицензия

Проект распространяется по лицензии MIT. См. [`LICENSE.md`](LICENSE.md).
