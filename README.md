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
dotnet run --project src/DnsSwitcher.Cli -- adapters
dotnet run --project src/DnsSwitcher.Cli -- status
dotnet run --project src/DnsSwitcher.Cli -- validate
dotnet run --project src/DnsSwitcher.Cli -- switch <profile-id>
dotnet run --project src/DnsSwitcher.Cli -- enable <profile-id>
dotnet run --project src/DnsSwitcher.Cli -- disable
```

Changing DNS settings requires administrator privileges on Windows.

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

## v0.3 scope

- Platform-neutral network adapter model and selection logic in `DnsSwitcher.Core`.
- Windows adapter discovery using `NetworkInterface`.
- Adapter facts:
  - active;
  - physical;
  - loopback;
  - supported IP stacks;
  - default gateway presence.
- Default adapter selection heuristics with unit tests.
- CLI command `adapters` for manual inspection.

## v0.4 scope

- Read current DNS status for the selected adapter.
- Detect effective DNS mode:
  - `Dhcp`
  - `Manual`
  - `Mixed`
- Read current IPv4 and IPv6 DNS server lists.
- Match current system DNS settings to a configured profile.

## v0.5 scope

- Apply a DNS profile to the selected adapter.
- Reset DNS settings to DHCP.
- Check administrator privileges before changing DNS settings.
- Handle primary operational errors:
  - profile not found;
  - adapter not found;
  - adapter disabled;
  - insufficient privileges;
  - failed Windows DNS command execution.
