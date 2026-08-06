<div align="center">

# DnsSwitcher

A portable Windows application for switching DNS profiles through a desktop UI, system tray, or CLI, with one shared core for every client.

![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![Version](https://img.shields.io/badge/version-1.4.1-blue)
[![License: MIT](https://img.shields.io/badge/license-MIT-2EA44F)](LICENSE.md)

[Русский](README.md) · **English**

[Quick start](#quick-start) · [Documentation](#documentation) · [Releases](../../releases)

</div>

## About

`DnsSwitcher` is intended for Windows users who need to apply saved DNS profiles quickly, return to automatic settings, and diagnose DNS or website connectivity problems.

The CLI, WPF application, and tray client share the same models and services. Privileged operations can be delegated to the `DnsSwitcher.Agent.Windows` service so elevation is not requested for every switch.

## Project status

The current source version is `1.4.1`. Core DNS switching, diagnostics, profile management, DNS Health Failover, and Split DNS scenarios are implemented. The project is Windows-only and targets `.NET 10`.

Builds and tests were not run as part of this documentation-only change; the commands below are taken from the repository.

## Features

- static DNS profile application and DHCP reset;
- CLI, interactive console mode, WPF UI, and tray client;
- profile creation, editing, deletion, import, and export;
- current DNS detection and network adapter selection;
- DNS, website, and profile benchmark diagnostics;
- optional DNS Health Failover;
- optional Split DNS through Windows NRPT;
- a Windows service and Named Pipes for privileged operations;
- portable configuration and logs stored next to the application;
- Russian and English UI with system-aware light and dark themes.

## Quick start

Windows and the `.NET 10 SDK` are required.

```powershell
git clone https://github.com/Regstar2/DnsSwitcher.git
cd DnsSwitcher
dotnet restore DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Ui -c Release
```

Run the application as administrator when changing DNS without the agent. The agent can be installed and managed from the UI, tray client, or CLI.

## Requirements

- Windows 10/11 or a compatible Windows environment;
- `.NET 10 SDK` for source builds;
- administrator privileges for service installation and direct system DNS changes;
- Inno Setup 6 only when building the installer.

## Installation

Two delivery modes are supported:

- a portable package that stores data inside its own directory;
- an Inno Setup installer with application registration and service support.

See [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md), [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md), and [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md).

## Usage

Typical workflow:

1. Create or import a DNS profile.
2. Select a network adapter.
3. Apply the profile from the UI, tray client, or CLI.
4. Use `status`, DNS test, or site test to verify the result.
5. Run `reset` to restore automatic DNS.

Profiles containing private addresses or internal domains should remain in local configuration files only.

## Operating modes

- **UI** — profile, diagnostics, agent, DNS Health Failover, and Split DNS management.
- **Tray** — quick switching and common actions without opening the main window.
- **CLI** — commands for manual use and automation.
- **Agent** — a Windows service that performs privileged operations over Named Pipes.

## Configuration

Data is stored in the `data/` directory next to the application:

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

Profile example: [`docs/profiles.example.json`](docs/profiles.example.json).

## Commands

Show help:

```powershell
dotnet run --project src/DnsSwitcher.Cli -- help
```

Main commands:

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

Global options: `--adapter <id|name>` and `--config <path>`.

## Architecture

```text
CLI / WPF UI / Tray
        │
        ▼
DnsSwitcher.Core
        │
        ├── DnsSwitcher.Infrastructure.Windows
        └── DnsSwitcher.Contracts ── Named Pipes ── DnsSwitcher.Agent.Windows
```

Domain logic and diagnostic orchestration live in `DnsSwitcher.Core`; Windows-specific adapter, DNS, file, and IPC operations are kept in the infrastructure project. See [`docs/architecture/README.md`](docs/architecture/README.md).

## Security

Changing system DNS settings is a privileged operation. The agent receives requests through a local Named Pipe, and profile input is validated before system commands are executed.

Do not commit private DNS profiles, secrets, internal domains, or local configuration. The `data/` directory is ignored by Git.

## Privacy

The project stores configuration, benchmark history, and logs locally. The repository does not declare telemetry or centralized collection of user data. Diagnostic checks contact domains and URLs selected in profiles or user configuration.

## Troubleshooting

- `test` checks DNS resolution for configured domains;
- `test-sites` performs staged DNS, TCP, TLS, and HTTP checks;
- `benchmark` compares profiles and restores the original DNS settings;
- `health` performs background checks and optional failover actions;
- `split-dns test` verifies domain matching against NRPT rules.

Logs are stored in `data/logs/dns-switcher.log`. IPC integration checks are documented in [`docs/ipc-integration-tests.md`](docs/ipc-integration-tests.md).

## Updating

For portable installations, extract the new build to a separate directory and move the `data/` directory only after making a backup. For installed versions, use the new installer for the same architecture. Stop or reinstall the agent before replacing service binaries.

## Backup and migration

Back up `data/config/` to move settings. Do not copy old executables over a new package without checking the package layout.

## Development

Prepare a Windows development environment:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-setup.ps1
```

The solution is split into `src/` and `tests/`; shared build settings and version metadata are stored in `Directory.Build.props`.

## Build

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
```

Portable package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.4.1 -Runtime win-x64
```

Installer:

```powershell
.\installer\build-installer.ps1 -Version 1.4.1 -Runtime win-x64
```

## Testing

```powershell
dotnet test DnsSwitcher.sln -c Release
```

The solution contains unit tests and a separate IPC integration test project. Manual checks and environment limitations should be recorded under [`docs/testing/`](docs/testing/README.md).

## Documentation

- [Documentation index](docs/README.md)
- [Architecture](docs/architecture/README.md)
- [Release notes](docs/releases/README.md)
- [Changelog](CHANGELOG.md)
- [Portable release](PORTABLE_RELEASE.md)
- [Service installation](SERVICE_INSTALL.md)
- [Installer](INSTALLER_RELEASE.md)
- [DNS Health Failover](DNS_HEALTH_FAILOVER.md)
- [Split DNS](SPLIT_DNS.md)
- [Microsoft Store readiness](STORE_READINESS.md)

## Limitations

- Windows only;
- agent installation and direct DNS changes require administrator privileges;
- Split DNS relies on Windows NRPT and can be bypassed by applications using their own DNS/DoH stack;
- the CLI is not fully localized;
- current UI screenshots are not published;
- compatibility with unverified Windows versions and architectures is not claimed.

## License

The project is distributed under the MIT License. See [`LICENSE.md`](LICENSE.md).
