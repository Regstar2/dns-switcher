# DnsSwitcher

Windows utility for quickly managing DNS profiles.

## v0.1 scope

- Solution and project skeleton:
  - `DnsSwitcher.Core`
  - `DnsSwitcher.Infrastructure.Windows`
  - `DnsSwitcher.Cli`
  - `DnsSwitcher.Ui`
  - `DnsSwitcher.Tray`
  - `DnsSwitcher.Tests`
- Shared core abstractions for profiles, config storage, paths, and DNS management.
- Portable config/log layout stored next to the app:
  - `data/config/profiles.json`
  - `data/logs/dns-switcher.log`
- Fixed `profiles.json` format in `docs/profiles.example.json` and `docs/profiles.schema.json`.
- Basic file logging.
- CLI/UI/tray skeletons.

## Commands

```powershell
dotnet build DnsSwitcher.sln
dotnet test DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Cli -- paths
dotnet run --project src/DnsSwitcher.Cli -- list
dotnet run --project src/DnsSwitcher.Cli -- status
```

DNS switching itself is intentionally not implemented in v0.1. It will require a separate administrator-permission-aware Windows infrastructure layer.
