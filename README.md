# DnsSwitcher

Portable Windows utility for fast DNS profile switching with a shared core, CLI, desktop UI, tray client, and built-in diagnostics.

## Overview

`DnsSwitcher` is a Windows-first project for managing DNS profiles without scattering logic across multiple apps.  
One shared core drives three clients:

- `DnsSwitcher.Cli` for commands and automation
- `DnsSwitcher.Ui` for regular desktop usage
- `DnsSwitcher.Tray` for instant switching from the system tray

The project also includes a privileged Windows agent so UI and tray can change DNS without requiring elevation on every action.

## Features

- Portable data layout stored next to the app
- DNS profile storage in `profiles.json`
- Fast apply/reset workflow
- Current DNS status detection
- Default network adapter selection
- DNS diagnostics by domain
- Site accessibility diagnostics by URL
- Multi-profile DNS benchmark with best-profile selection and history storage
- Optional DNS health failover monitor
- Optional Split DNS through Windows NRPT rules
- Interactive console mode for users who prefer a console menu
- Desktop UI for standard usage
- Tray client for fast switching
- Automatic language selection from the system with manual override
- Automatic light/dark UI theme from the system with manual override
- UI profile create/edit/delete plus import/export
- Agent/service model for privileged DNS operations
- File logging and graceful error handling

## Architecture

```text
DnsSwitcher.Core
  Models, validation, services, selection logic, DNS/site test orchestration

DnsSwitcher.Infrastructure.Windows
  Windows DNS management, adapter discovery, config storage, logging, IPC client

DnsSwitcher.Agent.Windows
  Privileged Windows Service / agent over Named Pipes

DnsSwitcher.Cli
  Command-line and interactive console client

DnsSwitcher.Ui
  WPF desktop client

DnsSwitcher.Tray
  WinForms tray client

DnsSwitcher.Tests
  Unit tests for config, validation, adapter selection, matching, and diagnostics
```

### Runtime flow

- `CLI`, `UI`, and `Tray` use the same shared core.
- Privileged DNS changes go through `DnsSwitcher.Agent.Windows` when available.
- If the agent is unavailable, direct fallback is possible only from an elevated process.
- Config and logs are portable and live under the app directory.

## Stack

- C# / .NET 10
- WPF for desktop UI
- WinForms `NotifyIcon` for tray
- Windows Service hosting for the agent
- Named Pipes for IPC
- xUnit for unit tests

## Projects

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

During development, clients started from this repository share the solution-level `data/config/profiles.json` path when available.

## CLI

Run help:

```powershell
dotnet run --project src/DnsSwitcher.Cli -- help
```

Main commands:

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

Global options:

```powershell
--adapter <id|name>
--config <path>
```

Interactive console mode:

```powershell
dotnet run --project src/DnsSwitcher.Cli
```

## UI

`DnsSwitcher.Ui` provides:

- profile list
- adapter selection
- current DNS status block
- apply/reset actions
- DNS test
- site test
- profile benchmark
- DNS health check and health monitor toggle
- DNS Health Failover settings window with thresholds, cooldown, action mode, fallback profile, failover chain, test domains, and expected IPs
- Split DNS rules editor with add/edit/delete/enable/disable/test/apply/reset
- Agent manager window for install/reinstall/start/stop/uninstall/status
- create/edit/delete profiles
- import/export profiles
- remember last selected adapter and profile
- optional continue-in-tray behavior on window close
- open config/log folders
- optional Windows autostart for the tray client
- automatic language/theme from the system
- manual language selection
- manual theme selection
- background refresh for config and external state changes

### Screenshots

Screenshots are intentionally not included in `v1.4.0` yet.
UI and tray screenshots can be added later without changing the shipped functionality.

## Tray

`DnsSwitcher.Tray` provides:

- current state in tooltip/menu
- enable DNS
- disable DNS
- switch next profile
- profile list
- DNS and site tests
- profile benchmark
- DNS health check and health monitor toggle
- Split DNS status/apply/reset
- Agent status/start/stop/reinstall submenu
- persistent tray settings
- open UI action
- shared app language preference
- shared app theme preference

## Diagnostics

Three diagnostics are built in:

- `test`
  DNS resolution check using `testDomains`
- `test-sites`
  Site accessibility check using `testUrls` with staged:
  DNS -> TCP -> TLS -> HTTP probing
- `benchmark`
  sequentially applies switchable DNS profiles, tests domains, compares latency, stores recent results, and restores the original DNS settings
- `health`
  optional background health checks with failover actions
- `split-dns`
  Windows NRPT-based per-namespace DNS routing

These are kept separate on purpose so DNS issues, HTTP/connectivity issues, and profile comparison are not mixed into one unclear result.

## Example config

Full example: [`docs/profiles.example.json`](docs/profiles.example.json)

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

## Build

Build and test:

```powershell
dotnet build DnsSwitcher.sln -c Release
dotnet test tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj -c Release
```

### Release Build

Create a framework-dependent Windows release package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

The release archive is written under:

```text
artifacts/release/v1.4.0/
```

Build installer:

```powershell
.\installer\build-installer.ps1 -Version 1.4.0 -Runtime win-x64
```

Release docs:

- [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md)
- [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md)
- [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md)
- [`DNS_HEALTH_FAILOVER.md`](DNS_HEALTH_FAILOVER.md)
- [`SPLIT_DNS.md`](SPLIT_DNS.md)
- [`STORE_READINESS.md`](STORE_READINESS.md)

## Limitations

- Windows-only
- DNS changes still depend on Windows networking APIs and command-line tools
- Agent/service installation requires administrator rights
- Split DNS uses Windows NRPT and can be bypassed by apps using their own DNS/DoH stack
- Without screenshots, portfolio presentation is documentation-first in `v1.4.0`
- CLI is still not fully localized
- Private DNS profiles should stay in local ignored config files and must not be committed

## Portfolio notes

This project demonstrates:

- layered architecture with a shared core
- separation of domain logic from platform-specific infrastructure
- multiple clients over one core
- Windows service + IPC integration
- validation, diagnostics, and error handling
- iterative delivery from MVPs to a release-ready structure

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md).
