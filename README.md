<div align="center">

# DnsSwitcher

**One Windows utility. Three interfaces. One shared DNS engine.**

Portable DNS profile management for the command line, desktop, and system tray.

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)](#requirements)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](#technology)
[![Release](https://img.shields.io/github/v/release/Regstar2/DnsSwitcher?display_name=tag)](../../releases)

[Download](../../releases/latest) · [Quick start](#quick-start) · [CLI](#command-line) · [Documentation](#documentation)

[Русская версия](README.ru.md)

</div>

---

## At a glance

| | |
|---|---|
| **Purpose** | Switch, test, and monitor DNS profiles on Windows |
| **Interfaces** | WPF desktop UI, tray client, CLI |
| **Storage** | Portable JSON configuration beside the application |
| **Diagnostics** | DNS resolution, website reachability, benchmarks, health checks |
| **Advanced routing** | Split DNS through Windows NRPT |

## Why DnsSwitcher

Windows DNS configuration is usually scattered across system dialogs, scripts, and separate diagnostic tools. DnsSwitcher places the same profile model, validation rules, diagnostics, and privileged operations behind three clients.

<table>
<tr>
<td width="33%" valign="top">

### Desktop

Manage profiles, adapters, diagnostics, health monitoring, and Split DNS from a regular WPF interface.

</td>
<td width="33%" valign="top">

### Tray

See the current state and switch profiles without opening the main window.

</td>
<td width="33%" valign="top">

### CLI

Automate DNS operations, inspect state, run tests, and manage the Windows agent.

</td>
</tr>
</table>

## Quick start

### Use a release build

1. Download the latest package from [GitHub Releases](../../releases/latest).
2. Extract it to a permanent folder.
3. Start the desktop or tray client.
4. Install the privileged agent when prompted if you want DNS changes without repeated elevation.

> [!NOTE]
> DnsSwitcher is portable. Configuration and logs are stored under the application directory.

### Run from source

```powershell
git clone https://github.com/Regstar2/DnsSwitcher.git
cd DnsSwitcher
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-setup.ps1
dotnet run --project src\DnsSwitcher.Ui
```

## Core capabilities

- Create, edit, import, and export DNS profiles.
- Apply static IPv4/IPv6 DNS or restore automatic DHCP settings.
- Detect the current DNS configuration and select a default adapter.
- Test DNS resolution separately from website accessibility.
- Benchmark multiple profiles and restore the original configuration afterwards.
- Monitor DNS health and execute configured failover actions.
- Route selected namespaces through Split DNS using Windows NRPT.
- Share one configuration model between UI, tray, and CLI clients.
- Use a privileged Windows agent over Named Pipes for protected operations.

## Command line

```powershell
dns-switcher profiles
dns-switcher adapters
dns-switcher status
dns-switcher apply <profile-id>
dns-switcher reset
dns-switcher test
dns-switcher test-sites
dns-switcher benchmark
dns-switcher health status
dns-switcher split-dns status
dns-switcher service status
```

Run the complete help:

```powershell
dotnet run --project src\DnsSwitcher.Cli -- help
```

## Architecture

```text
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│   WPF Desktop    │  │   Tray Client    │  │       CLI        │
└────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘
         └─────────────────────┼─────────────────────┘
                               ▼
                    ┌──────────────────────┐
                    │  DnsSwitcher.Core    │
                    │ models · validation  │
                    │ tests · selection    │
                    └──────────┬───────────┘
                               ▼
              ┌────────────────────────────────┐
              │ Windows infrastructure + IPC   │
              └───────────────┬────────────────┘
                              ▼
                  ┌────────────────────────┐
                  │ Privileged Windows     │
                  │ service / agent        │
                  └────────────────────────┘
```

## Portable data layout

```text
data/
├── config/
│   ├── profiles.json
│   ├── app-preferences.json
│   ├── dns-health-settings.json
│   ├── split-dns-rules.json
│   └── tray-settings.json
└── logs/
    └── dns-switcher.log
```

Private DNS profiles should remain in ignored local configuration files and must not be committed.

## Technology

| Area | Technology |
|---|---|
| Runtime | C# / .NET 10 |
| Desktop | WPF |
| Tray | WinForms `NotifyIcon` |
| Privileged operations | Windows Service + Named Pipes |
| Tests | xUnit |

## Build and test

```powershell
dotnet build DnsSwitcher.sln -c Release
dotnet test tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj -c Release
```

Create a release package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

## Documentation

- [Portable release](PORTABLE_RELEASE.md)
- [Service installation](SERVICE_INSTALL.md)
- [Installer release](INSTALLER_RELEASE.md)
- [DNS health failover](DNS_HEALTH_FAILOVER.md)
- [Split DNS](SPLIT_DNS.md)
- [IPC integration tests](docs/ipc-integration-tests.md)
- [Changelog](CHANGELOG.md)

## Requirements and limitations

- Windows 10 or Windows 11.
- Administrator rights are required to install the agent and modify protected network settings.
- Split DNS relies on Windows NRPT and cannot control applications that use their own DNS or DoH implementation.
- The CLI is not yet fully localized.

---

<div align="center">

Built as a portfolio-grade Windows networking utility with a shared core and multiple clients.

</div>
