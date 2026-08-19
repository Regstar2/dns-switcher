# Технологический стек

Документ описывает фактический стек release-candidate `v1.5.0`.

## Основной стек

| Область | Технология |
|---|---|
| Язык / runtime | C# / .NET 10 |
| SDK selection | `global.json`, base `10.0.201`, `rollForward: latestFeature` |
| Desktop UI | WPF, `net10.0-windows` |
| Tray | Windows Forms, `net10.0-windows` |
| CLI / Core | .NET `net10.0` |
| Windows infrastructure | .NET class library с Windows implementations |
| Agent | .NET host / Windows Service, `net10.0-windows` |
| IPC | Named Pipes |
| Split DNS | Windows NRPT |
| Config/state | локальные JSON-файлы |
| Update metadata | GitHub Releases REST API over HTTPS |
| Update integrity | SHA-256 / `SHA256SUMS.txt` |
| Logging | `Microsoft.Extensions.Logging` |
| Unit / integration tests | xUnit |
| Installer | Inno Setup 6 |
| Build | `dotnet` CLI + PowerShell |
| CI | GitHub Actions на self-hosted Windows x64 runner |

## Update delivery

`v1.5.0` не добавляет стороннюю updater-библиотеку. SemVer comparison находится в Core, а Windows infrastructure использует стандартные .NET `HttpClient`, `System.Text.Json`, `SHA256` и `ProcessStartInfo` для release discovery, checksum verification и installer handoff.

Production client не содержит GitHub credential. Update discovery рассчитан на anonymous GitHub Releases source; отсутствие публичного доступа обрабатывается как nonfatal update-source/network failure без credential workaround.

## Зафиксированные версии

- .NET SDK base: `10.0.201`, `rollForward: latestFeature`;
- UI / Tray / Agent / IPC integration tests: `net10.0-windows`;
- Core / Contracts / CLI / Infrastructure: `net10.0`;
- `Microsoft.Extensions.Logging` family: .NET 10-compatible versions from project files;
- xUnit test stack from `tests/*.csproj`;
- Inno Setup major line: 6; exact patch is not pinned in the repository.

## Среда и ограничения

- полноценный desktop/Agent runtime и manual UI smoke требуют Windows;
- DNS/Windows Service/NRPT operations могут требовать elevation;
- installer build требует Inno Setup 6 на build machine;
- `.github/workflows/ci.yml` выполняет Release restore/build/test;
- `.github/workflows/release-candidate.yml` собирает exact-commit installer/portable/checksums;
- self-hosted runner environment warnings не считаются project errors, если build/test result остаётся успешным и warning не относится к source/configuration проекта.

## Сборка и тестирование

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

Final candidate package:

```powershell
.\installer\build-installer.ps1 -Version 1.5.0 -Runtime win-x64
```

Unit tests находятся в `tests/DnsSwitcher.Tests`, Windows IPC integration tests — в `tests/DnsSwitcher.IntegrationTests`.

## Официальные источники технологий

- .NET SDK / `global.json`: https://learn.microsoft.com/dotnet/core/tools/global-json
- WPF .NET 10: https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net100
- Windows Service hosting: https://learn.microsoft.com/dotnet/core/extensions/windows-service
- Inno Setup 6: https://jrsoftware.org/ishelp/
