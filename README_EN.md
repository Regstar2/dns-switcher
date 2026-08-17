<div align="center">

# DnsSwitcher

A Windows application for switching DNS profiles through a desktop UI, system tray, or CLI while sharing one core across the user-facing clients.

[Русский](README.md) · **English**

[![Release](https://img.shields.io/badge/release-v1.4.1-4C8BF5?style=for-the-badge)](../../releases/tag/v1.4.1)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)](#requirements)
[![Documentation](https://img.shields.io/badge/docs-available-555555?style=for-the-badge)](docs/README.md)
[![License](https://img.shields.io/badge/license-MIT-2EA44F?style=for-the-badge)](LICENSE.md)

[Quick start](#quick-start) · [Documentation](#documentation) · [Releases](../../releases)

</div>

## About

`DnsSwitcher` targets Windows scenarios where users need to apply saved DNS profiles quickly, return to automatic settings, and diagnose DNS or website connectivity.

The CLI, WPF application, and tray client share common models and services. Privileged operations can be delegated to the `DnsSwitcher.Agent.Windows` Windows service over Named Pipes so elevation is not requested for every switch.

## Project status

The current source metadata version is `1.4.1`. The latest published GitHub Release is `v1.4.1`. `main` already contains unreleased delivery-process changes made after that release, so `main` should not be treated as byte-for-byte identical to the historical `v1.4.1` tag.

Core DNS switching, diagnostics, profile management, DNS Health Failover, and Split DNS scenarios are implemented. GitHub Actions are not configured in the repository, and this README does not claim CI coverage.

## Features

- static DNS profile application and DHCP reset;
- CLI, interactive console mode, WPF UI, and tray client;
- profile creation, editing, deletion, import, and export;
- current DNS detection and network adapter selection;
- DNS, website, and profile benchmark diagnostics;
- optional DNS Health Failover;
- optional Split DNS through Windows NRPT;
- a Windows service and Named Pipes for privileged operations;
- local configuration, benchmark history, and log storage;
- Russian and English UI with system-aware light and dark themes.

## Quick start

To run from source on Windows:

```powershell
git clone https://github.com/Regstar2/dns-switcher.git
cd dns-switcher
dotnet restore DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Ui -c Release
```

Run the application as administrator when changing DNS without the agent. The agent can be installed and managed from the UI, tray client, or CLI.

## Requirements

- Windows; published `v1.4.1` assets target `win-x64`;
- .NET SDK `10.0.201` or a compatible later .NET 10 SDK allowed by `global.json` (`rollForward: latestFeature`) for source builds;
- administrator privileges for Windows service installation and direct system DNS changes;
- Inno Setup 6 only when building the installer.

The project files do not pin a separate minimum Windows version, so compatibility with specific unverified Windows releases is not claimed.

## Installation

The published `v1.4.1` release contains two assets:

- `DnsSwitcher-1.4.1-win-x64.zip` — portable package;
- `DnsSwitcher-1.4.1-win-x64-setup.exe` — installer package.

Current `main` builds the installer as self-contained by default. The portable release script remains framework-dependent unless `-SelfContained` is passed explicitly. This current-branch behavior must not be projected backward onto the already published historical `v1.4.1` assets.

See [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md), [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md), and [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md).

## Usage

Typical workflow:

1. Create or import a DNS profile.
2. Select a network adapter.
3. Apply the profile from the UI, tray client, or CLI.
4. Use `status`, DNS test, or site test to verify the result.
5. Run `reset` to restore automatic DNS.

Profiles containing private addresses or internal domains should remain in local configuration files.

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
UI / CLI / Tray ──> DnsSwitcher.Core
       │
       └──> DnsSwitcher.Infrastructure.Windows ──> DnsSwitcher.Core
                    │
                    ├──> DnsSwitcher.Contracts ──> DnsSwitcher.Core
                    │
                    └── Named Pipes ──> DnsSwitcher.Agent.Windows
                                            ├──> Core
                                            ├──> Contracts
                                            └──> Infrastructure.Windows
```

`DnsSwitcher.Core` contains models, validation, and shared services. Windows-specific adapters, DNS operations, file-backed configuration, NRPT, and the IPC client live in `DnsSwitcher.Infrastructure.Windows`. The privileged Agent references Core, Contracts, and Windows infrastructure.

See [`docs/architecture/architecture.md`](docs/architecture/architecture.md) and [`docs/architecture/tech-stack.md`](docs/architecture/tech-stack.md).

## Security

Changing system DNS settings is a privileged operation. The agent receives requests through a local Named Pipe, and external profile data is validated before system settings are applied.

Do not commit private DNS profiles, secrets, internal domains, or local configuration. The `data/` directory is ignored by Git.

## Privacy

The project stores configuration, benchmark history, and logs locally. The current code and project files do not declare mandatory telemetry or centralized collection of user data. Diagnostic checks contact domains and URLs selected in profiles or user configuration.

## Troubleshooting

- `test` checks DNS resolution for configured domains;
- `test-sites` performs staged DNS, TCP, TLS, and HTTP checks;
- `benchmark` compares profiles and restores the original DNS settings;
- `health` performs background checks and optional failover actions;
- `split-dns test` verifies domain matching against NRPT rules.

Logs are stored in `data/logs/dns-switcher.log`. IPC integration checks are documented in [`docs/ipc-integration-tests.md`](docs/ipc-integration-tests.md).

## Updating

For portable installations, extract the new build to a separate directory and move `data/` only after making a backup. For installed versions, use an installer for the same architecture. Stop or reinstall the Agent through a supported command before replacing the service runtime.

## Backup and migration

Back up `data/config/` to move settings. Do not copy old executables over a new package without checking the package layout.

## Development

The repository pins the .NET SDK through `global.json`. To prepare a Windows development environment:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev-setup.ps1
```

The script installs the requested SDK when needed and then performs restore, Release build, and tests.

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

Automated checks defined by the repository:

```powershell
dotnet test DnsSwitcher.sln -c Release
```

The solution contains unit tests and a separate Windows-specific IPC integration test project. Manual checks are documented in [`docs/testing/manual-test-plan.md`](docs/testing/manual-test-plan.md).

## Documentation

| Area | Document |
|---|---|
| Index | [`docs/README.md`](docs/README.md) |
| Product idea and scope | [`docs/product/idea.md`](docs/product/idea.md), [`docs/product/mvp-scope.md`](docs/product/mvp-scope.md) |
| Feasibility | [`docs/product/feasibility.md`](docs/product/feasibility.md) |
| Roadmap | [`docs/product/roadmap.md`](docs/product/roadmap.md) |
| Architecture and stack | [`docs/architecture/architecture.md`](docs/architecture/architecture.md), [`docs/architecture/tech-stack.md`](docs/architecture/tech-stack.md) |
| Versions | [`docs/versions/versions-index.md`](docs/versions/versions-index.md) |
| Manual testing | [`docs/testing/manual-test-plan.md`](docs/testing/manual-test-plan.md) |
| Release notes | [`docs/releases/README.md`](docs/releases/README.md) |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) |
| Portable / installer / service | [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md), [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md), [`SERVICE_INSTALL.md`](SERVICE_INSTALL.md) |
| DNS Health Failover / Split DNS | [`DNS_HEALTH_FAILOVER.md`](DNS_HEALTH_FAILOVER.md), [`SPLIT_DNS.md`](SPLIT_DNS.md) |

## Limitations

- the project depends on Windows APIs and does not claim support for other operating systems;
- the published `v1.4.1` release targets `win-x64`;
- Agent installation and direct DNS changes require administrator privileges;
- Split DNS relies on Windows NRPT and can be bypassed by applications using their own DNS/DoH stack;
- the CLI is not fully localized;
- current UI screenshots are not published;
- an exact minimum Windows version is not pinned in the project files;
- GitHub Actions/CI are not configured in the repository.

## License

The project is distributed under the MIT License. See [`LICENSE.md`](LICENSE.md).
