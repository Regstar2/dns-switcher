# DnsSwitcher

[English version](README.md)

Портативная утилита для Windows для быстрого переключения DNS-профилей с общим ядром, CLI, desktop UI, tray-клиентом и встроенной диагностикой.

## Обзор

`DnsSwitcher` - Windows-first проект для управления DNS-профилями без дублирования логики между разными клиентами.
Одно общее ядро используется тремя клиентами:

- `DnsSwitcher.Cli` - команды, автоматизация и консольное меню
- `DnsSwitcher.Ui` - основное desktop-приложение
- `DnsSwitcher.Tray` - быстрое переключение из системного трея

В проект также входит привилегированный Windows Agent service. Он нужен, чтобы UI и tray могли менять DNS без запроса прав администратора при каждом действии.

## Возможности

- Портативное хранение данных рядом с приложением
- Хранение DNS-профилей в `profiles.json`
- Быстрое применение профиля и сброс DNS в автоматический режим
- Определение текущего DNS-статуса
- Автоматический выбор основного сетевого адаптера
- Проверка DNS по доменам
- Проверка доступности сайтов по URL
- Benchmark нескольких DNS-профилей с выбором лучшего и историей результатов
- Опциональный DNS Health Failover
- Опциональный Split DNS через Windows NRPT
- Интерактивный консольный режим
- Desktop UI для обычного использования
- Tray-клиент для быстрых действий
- Автоматический выбор языка по системе с ручным переключением
- Автоматическая светлая/темная тема по системе с ручным переключением
- Создание, редактирование, удаление, импорт и экспорт профилей в UI
- Agent/service модель для привилегированных DNS-операций
- Файловое логирование и дружелюбная обработка ошибок

## Архитектура

```text
DnsSwitcher.Core
  Модели, валидация, сервисы, выбор адаптера, оркестрация DNS/site тестов

DnsSwitcher.Infrastructure.Windows
  Управление DNS в Windows, поиск адаптеров, хранение конфигов, логирование, IPC-клиент

DnsSwitcher.Agent.Windows
  Привилегированный Windows Service / agent поверх Named Pipes

DnsSwitcher.Cli
  CLI и интерактивный консольный клиент

DnsSwitcher.Ui
  WPF desktop-клиент

DnsSwitcher.Tray
  WinForms tray-клиент

DnsSwitcher.Tests
  Unit-тесты для конфига, валидации, выбора адаптера, сопоставления профилей и диагностики
```

### Runtime flow

- `CLI`, `UI` и `Tray` используют одно общее ядро.
- Привилегированные DNS-операции выполняются через `DnsSwitcher.Agent.Windows`, если агент установлен и запущен.
- Если агент недоступен, прямое применение возможно только из процесса с правами администратора.
- Конфиги и логи хранятся портативно внутри папки приложения.

## Стек

- C# / .NET 10
- WPF для desktop UI
- WinForms `NotifyIcon` для tray
- Windows Service для агента
- Named Pipes для IPC
- xUnit для unit-тестов

## Проекты

- `src/DnsSwitcher.Core`
- `src/DnsSwitcher.Infrastructure.Windows`
- `src/DnsSwitcher.Contracts`
- `src/DnsSwitcher.Agent.Windows`
- `src/DnsSwitcher.Cli`
- `src/DnsSwitcher.Ui`
- `src/DnsSwitcher.Tray`
- `tests/DnsSwitcher.Tests`

## Portable layout

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

Во время разработки клиенты, запущенные из репозитория, используют общий solution-level путь `data/config/profiles.json`, если он доступен.

## CLI

Показать help:

```powershell
dotnet run --project src/DnsSwitcher.Cli -- help
```

Основные команды:

```powershell
dns-switcher profiles
dns-switcher adapters
dns-switcher status
dns-switcher current
dns-switcher apply <profile-id>
dns-switcher reset
dns-switcher test
dns-switcher test-sites
dns-switcher benchmark
dns-switcher health <status|enable|disable|check|chain|fallback|action|domains>
dns-switcher split-dns <status|enable|disable|list|add|remove|update|enable-rule|disable-rule|test|apply|reset>
dns-switcher validate-config
dns-switcher service <install|reinstall|uninstall|start|stop|status>
```

Глобальные опции:

```powershell
--adapter <id|name>
--config <path>
```

Интерактивный консольный режим:

```powershell
dotnet run --project src/DnsSwitcher.Cli
```

## UI

`DnsSwitcher.Ui` предоставляет:

- список профилей
- выбор адаптера
- блок текущего DNS-статуса
- применение профиля и сброс DNS
- DNS test
- site test
- benchmark профилей
- DNS health check и включение/выключение health monitor
- окно настроек DNS Health Failover с thresholds, cooldown, action mode, fallback profile, failover chain, test domains и expected IPs
- редактор Split DNS rules с add/edit/delete/enable/disable/test/apply/reset
- окно управления агентом: install/reinstall/start/stop/uninstall/status
- создание, редактирование и удаление профилей
- импорт и экспорт профилей
- запоминание последнего адаптера и профиля
- опциональное сворачивание в tray при закрытии окна
- открытие папок конфигов и логов
- автозапуск tray-клиента вместе с Windows
- автоматический выбор языка и темы по системе
- ручной выбор языка
- ручной выбор темы
- фоновое обновление при изменении конфига или внешнего состояния

### Скриншоты

Скриншоты намеренно не включены в `v1.4.1`.
Их можно добавить позже без изменения поставляемой функциональности.

## Tray

`DnsSwitcher.Tray` предоставляет:

- текущее состояние в tooltip/menu
- включение DNS-профиля
- выключение DNS / возврат к автоматическому DNS
- переключение на следующий профиль
- список профилей
- DNS и site tests
- benchmark профилей
- DNS health check и включение/выключение health monitor
- Split DNS status/apply/reset
- подменю Agent status/start/stop/reinstall
- сохранение tray-настроек
- открытие UI
- общий язык приложения
- общую тему приложения

## Диагностика

Встроены несколько диагностических сценариев:

- `test`
  Проверка DNS-резолва по `testDomains`
- `test-sites`
  Проверка доступности сайтов по `testUrls` с этапами:
  DNS -> TCP -> TLS -> HTTP
- `benchmark`
  Последовательно применяет переключаемые DNS-профили, тестирует домены, сравнивает latency, сохраняет последние результаты и восстанавливает исходные DNS-настройки
- `health`
  Опциональные фоновые health-checks с failover-действиями
- `split-dns`
  Per-namespace DNS routing через Windows NRPT

Эти проверки разделены намеренно, чтобы DNS-проблемы, HTTP/connectivity-проблемы и сравнение профилей не смешивались в один неясный результат.

## Пример конфига

Полный пример: [`docs/profiles.example.json`](docs/profiles.example.json)

```json
{
  "version": 1,
  "activeProfileId": null,
  "profiles": [
    {
      "id": "cloudflare",
      "name": "Cloudflare",
      "mode": "static",
      "ipv4": ["1.1.1.1", "1.0.0.1"],
      "ipv6": ["2606:4700:4700::1111", "2606:4700:4700::1001"],
      "tags": ["public", "general"],
      "testDomains": ["cloudflare.com", "openai.com"],
      "testUrls": ["https://cloudflare.com/", "https://openai.com/"]
    },
    {
      "id": "dhcp",
      "name": "Automatic DNS",
      "mode": "dhcp",
      "ipv4": [],
      "ipv6": []
    }
  ]
}
```

## Сборка

Сборка и тесты:

```powershell
dotnet build DnsSwitcher.sln -c Release
dotnet test tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj -c Release
```

### Release build

Создать framework-dependent Windows portable package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

Архив будет создан в:

```text
artifacts/release/v1.4.1/
```

Собрать installer:

```powershell
.\installer\build-installer.ps1 -Version 1.4.1 -Runtime win-x64
```

Release-документация:

- [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md)
- [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md)
- [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md)
- [`DNS_HEALTH_FAILOVER.md`](DNS_HEALTH_FAILOVER.md)
- [`SPLIT_DNS.md`](SPLIT_DNS.md)
- [`STORE_READINESS.md`](STORE_READINESS.md)

## Ограничения

- Только Windows
- Изменение DNS зависит от Windows networking APIs и системных command-line tools
- Установка агента требует прав администратора
- Split DNS использует Windows NRPT и может обходиться приложениями с собственным DNS/DoH стеком
- В `v1.4.1` скриншоты пока не добавлены
- CLI пока не полностью локализован
- Частные DNS-профили должны оставаться в локальных ignored config-файлах и не должны попадать в коммиты

## Для портфолио

Проект показывает:

- layered architecture с общим ядром
- отделение domain logic от platform-specific infrastructure
- несколько клиентов поверх одного core
- интеграцию Windows Service + IPC
- валидацию, диагностику и обработку ошибок
- итеративную поставку от MVP до release-ready состояния

## Changelog

См. [`CHANGELOG.md`](CHANGELOG.md).
