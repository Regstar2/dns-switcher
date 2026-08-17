# Технологический стек

Документ описывает фактический стек текущего `main`. Версии взяты из `global.json`, `.csproj`, build scripts и installer scripts; зависимости не обновлялись ради документации.

## Выбранный стек

| Область | Технология |
|---|---|
| Язык и runtime | C# / .NET 10 |
| SDK selection | `global.json`, базовая версия `10.0.201`, `rollForward: latestFeature` |
| Desktop UI | WPF, `net10.0-windows` |
| Tray | Windows Forms, `net10.0-windows` |
| CLI | .NET console application, `net10.0` |
| Core | class library, `net10.0` |
| Windows infrastructure | class library, `net10.0` с Windows-specific реализацией |
| Agent | .NET host / Windows Service, `net10.0-windows` |
| IPC | Named Pipes |
| Split DNS | Windows NRPT |
| Конфигурация | локальные JSON-файлы |
| Логирование | `Microsoft.Extensions.Logging` |
| Unit / integration tests | xUnit |
| Installer | Inno Setup 6 |
| Build | `dotnet` CLI + PowerShell scripts |

## Почему выбран именно он

Текущая реализация использует .NET и Windows desktop frameworks непосредственно для Windows-specific задачи. WPF обслуживает основное desktop UI, Windows Forms — tray-клиент, а общий Core отделён от системных реализаций.

Исходная историческая мотивация выбора WPF, Windows Forms, Named Pipes или Inno Setup полностью не зафиксирована. Документ не приписывает авторам решения, которых нельзя подтвердить по истории.

## Что сознательно не используем

По текущему solution и project files нет обязательного:

- собственного web backend;
- внешней базы данных;
- cloud synchronization service;
- кроссплатформенного UI framework.

Это описание текущей архитектуры, а не запрет на будущие изменения.

## Зафиксированные версии

### SDK и framework

- .NET SDK base version: `10.0.201`;
- SDK roll-forward: `latestFeature` внутри совместимого .NET 10 диапазона;
- Core / Contracts / CLI / Infrastructure: `net10.0`;
- UI / Tray / Agent / IPC integration tests: `net10.0-windows`.

### Основные NuGet packages

- `Microsoft.Extensions.Logging` — `10.0.5` в UI, CLI, Tray и Windows infrastructure;
- `Microsoft.Extensions.Logging.Abstractions` — `10.0.5` в Core и Windows infrastructure;
- `Microsoft.Extensions.Hosting` — `10.0.0` в Agent;
- `Microsoft.Extensions.Hosting.WindowsServices` — `10.0.0` в Agent;
- `Microsoft.NET.Test.Sdk` — `17.14.1`;
- `xunit` — `2.9.3`;
- `xunit.runner.visualstudio` — `3.1.4`;
- `coverlet.collector` — `6.0.4` в unit-test проекте.

### Installer

Build script ищет Inno Setup 6 (`ISCC.exe`). Конкретный patch Inno Setup в репозитории не закреплён.

## Ограничения среды

- полноценная сборка desktop/Agent частей и ручная проверка требуют Windows;
- DNS, Windows Service и NRPT сценарии могут требовать административных прав;
- installer build требует установленный Inno Setup 6;
- GitHub Actions в репозитории не настроены;
- текущий `main` делает installer self-contained по умолчанию, тогда как standalone portable script без `-SelfContained` остаётся framework-dependent.

## Сборка

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
```

Repository helper:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-setup.ps1
```

## Запуск

WPF UI из исходников:

```powershell
dotnet run --project src/DnsSwitcher.Ui -c Release
```

CLI help:

```powershell
dotnet run --project src/DnsSwitcher.Cli -- help
```

## Тестирование

```powershell
dotnet test DnsSwitcher.sln -c Release
```

Unit tests находятся в `tests/DnsSwitcher.Tests`, Windows-specific IPC integration tests — в `tests/DnsSwitcher.IntegrationTests`.

## Официальные источники технологий

- .NET `global.json` and SDK roll-forward: https://learn.microsoft.com/dotnet/core/tools/global-json — применяется к .NET SDK, включая используемую ветку .NET 10.
- WPF for .NET 10: https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net100 — относится к `net10.0-windows` UI.
- Windows Service with .NET hosting: https://learn.microsoft.com/dotnet/core/extensions/windows-service — описывает integration model, используемую `Microsoft.Extensions.Hosting.WindowsServices`.
- Inno Setup 6 documentation: https://jrsoftware.org/ishelp/ — относится к installer toolchain.
- Inno Setup 6 downloads/revision line: https://jrsoftware.org/isdl.php — подтверждает ветку Inno Setup 6; проект закрепляет major line, а не patch.
