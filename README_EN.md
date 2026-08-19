<div align="center">

# DnsSwitcher

Fast DNS profile switching on Windows through the desktop app, system tray, or CLI.

[Русский](README.md) · **English**

[![Version](https://img.shields.io/badge/version-v1.5.0-4C8BF5?style=for-the-badge)](https://github.com/Regstar2/dns-switcher/releases/tag/v1.5.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?style=for-the-badge&logo=windows&logoColor=white)](#requirements)
[![CI](https://github.com/Regstar2/dns-switcher/actions/workflows/ci.yml/badge.svg)](https://github.com/Regstar2/dns-switcher/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-2EA44F?style=for-the-badge)](LICENSE.md)

[Download](https://github.com/Regstar2/dns-switcher/releases/tag/v1.5.0) · [Quick start](#quick-start) · [Documentation](#documentation) · [Changelog](CHANGELOG.md)

</div>

## About

DnsSwitcher is a Windows x64 application for saving and applying DNS profiles, DNS/site diagnostics, DNS Health Failover, and Split DNS through Windows NRPT. Privileged operations can run through the Windows Agent over Named Pipes.

## Project status

The current stable version is **v1.5.0**. Core UI, Tray, CLI, Agent, DNS Health, Split DNS, installer/portable, and update scenarios completed final Windows validation.

## Features

- DNS profiles with fast apply and restore-to-automatic DNS;
- WPF UI, configurable system-tray menu, and CLI;
- profile create/edit/import/export;
- DNS, site, and benchmark diagnostics;
- DNS Health Failover with thresholds, cooldown, and fallback profiles;
- Split DNS through Windows NRPT;
- Windows Agent for privileged operations;
- Russian and English UI with System / Light / Dark themes;
- dedicated **About** and **Help** sections;
- manual and opt-out automatic update checks;
- SHA-256 verification before a downloaded installer can be launched.

## Screenshots

| Main window | System tray |
|---|---|
| ![DnsSwitcher main window](docs/assets/screenshots/main.png) | ![DnsSwitcher system tray](docs/assets/screenshots/tray.png) |

| Settings | Tray settings |
|---|---|
| ![DnsSwitcher settings](docs/assets/screenshots/settings.png) | ![System tray settings](docs/assets/screenshots/settings-tray.png) |

| DNS Health | Split DNS |
|---|---|
| ![DNS Health Failover](docs/assets/screenshots/dns-health.png) | ![Split DNS](docs/assets/screenshots/split-dns.png) |

Additional captures are available in [`docs/assets/screenshots/`](docs/assets/screenshots/).

## Quick start

1. Download `DnsSwitcher-1.5.0-win-x64-setup.exe` from [GitHub Release v1.5.0](https://github.com/Regstar2/dns-switcher/releases/tag/v1.5.0).
2. Install DnsSwitcher.
3. Create or import a DNS profile.
4. Select a network adapter and apply the profile.
5. Use **Restore automatic DNS** when the static configuration is no longer needed.

A portable package is available as `DnsSwitcher-1.5.0-win-x64.zip`.

## Requirements

- Windows x64;
- administrator rights for Agent installation and system DNS/NRPT operations;
- .NET 10 SDK only when building from source.

The installer and portable package are self-contained and do not require a separately installed .NET Desktop Runtime.

## Installation

The stable release contains:

```text
DnsSwitcher-1.5.0-win-x64-setup.exe
DnsSwitcher-1.5.0-win-x64.zip
SHA256SUMS.txt
```

Before launching the installer manually, its SHA-256 can be checked against `SHA256SUMS.txt` from the same release.

## Usage

Main CLI commands:

```text
profiles
adapters
status
apply <profile-id>
reset
test
test-sites
benchmark
health <...>
split-dns <...>
service <install|reinstall|uninstall|start|stop|status>
```

For everyday use, the UI and Tray are sufficient; the CLI is available for automation and diagnostics.

## Configuration

User data is stored under `data/`:

```text
data/
  config/
    app-preferences.json
    profiles.json
    tray-settings.json
    ui-settings.json
    dns-health-settings.json
    dns-health-state.json
    split-dns-rules.json
    update-state.json
  logs/
```

Installed upgrades preserve `data/config/`. Portable users should preserve the `data/` directory when replacing application files.

## Architecture

```text
UI / CLI / Tray
       │
       ├──> DnsSwitcher.Core
       └──> DnsSwitcher.Infrastructure.Windows
                    ├── Windows DNS / NRPT / storage
                    ├── update delivery
                    └── Named Pipes ──> DnsSwitcher.Agent.Windows
```

Details: [`docs/architecture/architecture.md`](docs/architecture/architecture.md).

## Security

- the update client contains no PAT, OAuth token, or embedded GitHub secret;
- an installer can start only after SHA-256 verification succeeds;
- only expected HTTPS GitHub release URLs for the configured repository are accepted;
- runtime configuration, logs, and local profiles are excluded from Git.

## Privacy

DnsSwitcher does not require a cloud account and stores configuration locally. Automatic update checks only query the configured release source and can be disabled in Settings.

## Updating

The installed build can be upgraded in place through the Inno Setup installer while preserving profiles and settings. DnsSwitcher provides a manual check and an opt-out automatic update check.

Built-in update checks use a publicly readable GitHub Releases source without a token: DnsSwitcher reads metadata, selects the expected installer asset, downloads `SHA256SUMS.txt` from the same release, and verifies SHA-256 before the installer can launch.

## Build

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

Installer:

```powershell
.\installer\build-installer.ps1 -Version 1.5.0 -Runtime win-x64
```

The SDK is pinned in [`global.json`](global.json).

## Testing

The final version completed automated tests and manual Windows validation covering DNS apply/reset, Agent, Health Failover, Split DNS, Tray customization, installer/portable, upgrade preservation, RU/EN localization, and UI scaling.

Plans and evidence: [`docs/testing/`](docs/testing/).

## Documentation

- [Documentation index](docs/README.md)
- [Architecture](docs/architecture/architecture.md)
- [v1.5.0 release notes](docs/releases/v1.5.0_EN.md)
- [DNS Health Failover](DNS_HEALTH_FAILOVER.md)
- [Split DNS](SPLIT_DNS.md)
- [Installer](INSTALLER_RELEASE.md)
- [Portable package](PORTABLE_RELEASE.md)
- [CHANGELOG](CHANGELOG.md)

## Limitations

- Windows x64 only;
- Split DNS uses NRPT and can be bypassed by applications with their own DNS/DoH stack;
- the exact minimum Windows version is not currently encoded as a dedicated project property.

## License

MIT — [`LICENSE.md`](LICENSE.md).
