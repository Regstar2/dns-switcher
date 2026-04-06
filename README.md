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
dotnet run --project src/DnsSwitcher.Cli -- validate
```

DNS switching itself is intentionally not implemented in v0.1. It will require a separate administrator-permission-aware Windows infrastructure layer.

## v0.2 scope

- Config model: `AppConfig`.
- Profile model: `DnsProfile` with `ProfileMode.Static` and `ProfileMode.Dhcp`.
- Validation model: `ValidationError`.
- `profiles.json` loading validates:
  - empty profile ids and names;
  - duplicate profile ids and names;
  - invalid IPv4/IPv6 addresses;
  - `Dhcp` profiles with static DNS addresses;
  - `Static` profiles without DNS addresses;
  - unknown `activeProfileId`.
