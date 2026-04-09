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
- When running from the repository build output, CLI/UI/Tray/Agent share the solution-level `data` directory so they stay in sync during development.
- Fixed `profiles.json` format in `docs/profiles.example.json` and `docs/profiles.schema.json`.
- Basic file logging.
- CLI/UI/tray skeletons.

## Commands

```powershell
dotnet build DnsSwitcher.sln
dotnet test DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Cli
dotnet run --project src/DnsSwitcher.Cli -- profiles
dotnet run --project src/DnsSwitcher.Cli -- adapters
dotnet run --project src/DnsSwitcher.Cli -- status
dotnet run --project src/DnsSwitcher.Cli -- apply <profile-id>
dotnet run --project src/DnsSwitcher.Cli -- reset
dotnet run --project src/DnsSwitcher.Cli -- test
dotnet run --project src/DnsSwitcher.Cli -- test-sites
dotnet run --project src/DnsSwitcher.Cli -- validate-config
dotnet run --project src/DnsSwitcher.Cli -- service status
dotnet run --project src/DnsSwitcher.Cli -- help
dotnet run --project src/DnsSwitcher.Tray
dotnet run --project src/DnsSwitcher.Agent.Windows
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

## v0.6 scope

- Console/CLI MVP with two modes:
  - interactive console menu when started without arguments;
  - command mode when started with arguments.
- Commands:
  - `profiles`
  - `adapters`
  - `status`
  - `apply <profile-id>`
  - `reset`
  - `test`
  - `validate-config`
- Global options:
  - `--adapter <id|name>`
  - `--config <path>`
- Improved help and stable exit codes for command mode.
- Legacy aliases preserved:
  - `list -> profiles`
  - `switch`, `enable -> apply`
  - `disable -> reset`
  - `validate -> validate-config`

## v0.7 scope

- Separate tray application with a live tray icon.
- Tray context menu actions:
  - `Enable DNS`
  - `Disable DNS`
  - `Switch Next`
  - `Show Profiles`
- Current DNS status is shown directly in the tray tooltip and menu header.
- Tray state refreshes automatically and after each DNS switch operation.
- Double-clicking the tray icon opens a detailed status dialog.

## v0.7.1 scope

- Prepare the architecture for removing Administrator requirements from tray forever.
- Implement the first agent/service foundation for removing repeated elevation:
  - privileged Windows Service;
  - non-elevated `Tray`, `UI`, and normal `CLI`;
  - Named Pipes between clients and the service.
- Add CLI service commands:
  - `service install`
  - `service uninstall`
  - `service start`
  - `service stop`
  - `service status`
- Install the agent service from a dedicated deployment directory outside `bin\Debug` and `bin\Release`.
- Document the service install model and the portable-app constraint.
- Detailed plan: `docs/v0.7.1-privileged-access-plan.md`

## v0.7.2 scope

- Add persistent tray settings in portable config:
  - `data/config/tray-settings.json`
- Tray settings:
  - `notificationsEnabled`
  - `showAdapterName`
- Add tray settings menu actions:
  - enable or disable action notifications
  - enable or disable adapter name display
- Split tray header into separate lines:
  - status line
  - adapter line
- Trim long dynamic menu texts so the tray menu does not stretch excessively.
- Keep full status information in the detailed status dialog.
- Add tests for tray settings storage and tray text formatting.

## v0.8 scope

- Add the first desktop UI MVP in `DnsSwitcher.Ui`.
- Main window includes:
  - profile list;
  - adapter selection in the right details column;
  - current DNS status block;
  - buttons for `Apply`, `Reset`, and `Reload`.
- The UI reloads `profiles.json` and current system status:
  - on startup;
  - on manual `Reload`;
  - automatically after external config changes from CLI or tray;
  - periodically for current system and agent status.
- The UI shows:
  - current matched profile;
  - configured active profile;
  - selected adapter;
  - current DNS mode;
  - agent service status;
  - agent availability;
  - IPv4 and IPv6 DNS servers.
- Normal window mode starts with equal left and right columns.
- The default window height is kept tighter so the initial layout avoids excess empty space in the right column.
- Compact width mode hides the right column and keeps the profile list with `Apply` and `Reset` usable at smaller widths.
- Compact height mode hides optional right-side sections step by step instead of collapsing the whole right column immediately, while keeping the status block visible longer.
- The profiles area is intentionally laid out so profile management can be added later without redesigning the whole window.

## v0.9 scope

- Add `DnsTester` for the currently selected adapter and DNS state.
- DNS testing uses `testDomains` from the matched profile when available.
- If the current DNS does not match a profile, the tester falls back to all configured profile test domains.
- If config test domains are missing, the tester uses a small built-in fallback domain set.
- Each domain is tested with multiple resolve attempts and latency measurement.
- Final DNS test status is classified as:
  - `Ok`
  - `Slow`
  - `Failed`
- CLI adds:
  - `test`
- Desktop UI adds:
  - `Test DNS`
- Tray adds:
  - `Test DNS`
- v0.9 tests DNS resolution only.
- `testUrls` stay reserved for a future HTTP/site accessibility check, which is a separate concern from DNS resolution.

## v0.9.1 scope

- Add `ConnectivityTester` as a separate layer from `DnsTester`.
- Site accessibility testing uses `testUrls`.
- If the current DNS matches a configured profile, the tester uses that profile's `testUrls`.
- If the current DNS does not match a configured profile, the tester falls back to the union of configured `testUrls`.
- If no `testUrls` are configured, the tester returns `NotConfigured`.
- Each URL is tested in stages:
  - DNS resolve
  - TCP connect
  - TLS handshake for `https`
  - HTTP probe with `HEAD`, then fallback to `GET` when needed
- Multiple attempts and latency measurement are used per URL.
- Final site test status is classified as:
  - `Ok`
  - `Slow`
  - `Blocked`
  - `Failed`
  - `NotConfigured`
- CLI adds:
  - `test-sites`
- Desktop UI adds:
  - `Test Sites`
- Tray adds:
  - `Test Sites`
- `test` and `test-sites` stay separate on purpose:
  - `test` checks DNS resolution
  - `test-sites` checks real site accessibility over DNS/TCP/TLS/HTTP

## Cross-Cutting Requirements

- `paths` was removed from the public CLI surface. It was only a portable-debug helper, not a user scenario.
- Russian language support and full i18n are required for the final product.
- All user-facing strings in CLI, UI and tray should move to a centralized localization mechanism.
- English should remain as a fallback language.
- Desktop UI must support a dark theme in a future version.
- Centralized profile catalogs/import sources are a valid next step, but are not implemented yet.
- Private DNS profiles must stay in local ignored config files and must not be committed to the repository.
