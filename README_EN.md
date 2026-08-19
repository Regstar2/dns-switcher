<div align="center">

# DnsSwitcher

Windows utility for DNS profiles, diagnostics, Health Failover, and Split DNS.

[Русский](README.md) · **English**

[![Source version](https://img.shields.io/badge/source-v1.5.0-4C8BF5?style=for-the-badge)](Directory.Build.props)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?style=for-the-badge&logo=windows&logoColor=white)](#requirements)
[![CI](https://img.shields.io/badge/CI-Windows-555555?style=for-the-badge)](.github/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-2EA44F?style=for-the-badge)](LICENSE.md)

[Quick start](#quick-start) · [Updates](#updates) · [Documentation](#documentation) · [GitHub Releases](https://github.com/Regstar2/dns-switcher/releases)

</div>

## About

DnsSwitcher applies saved DNS profiles through a WPF UI, system tray, or CLI, can restore automatic DNS, and provides DNS/site/benchmark diagnostics. Privileged operations can use the Windows Agent over Named Pipes.

The source version on the release-preparation branch is `1.5.0`. This PR does not create the stable `v1.5.0` tag/release; publication is a separate step after all release gates are closed.

## Features

- static DNS profiles and restore-to-automatic DNS;
- WPF UI, configurable tray menu, and CLI;
- profile create/edit/import/export;
- DNS, site, and benchmark diagnostics;
- optional DNS Health Failover;
- optional Split DNS through Windows NRPT;
- Windows Agent for privileged operations;
- RU/EN and System/Light/Dark themes;
- **About** and **Help** sections with the canonical GitHub link;
- manual and opt-out automatic update checks;
- installer download only after SHA-256 verification.

## Quick start

From source on Windows:

```powershell
git clone https://github.com/Regstar2/dns-switcher.git
cd dns-switcher
dotnet restore DnsSwitcher.sln
dotnet run --project src/DnsSwitcher.Ui -c Release
```

Release verification:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

## Requirements

- Windows x64;
- .NET 10 SDK from `global.json` only when building from source;
- administrator rights for Agent installation and direct system DNS/NRPT operations;
- Inno Setup 6 only on the installer build machine.

The installer is built self-contained and should not require a separately installed .NET Desktop Runtime on the target machine.

## Installation and assets

The final `v1.5.0` release is prepared with:

```text
DnsSwitcher-1.5.0-win-x64-setup.exe
DnsSwitcher-1.5.0-win-x64.zip
SHA256SUMS.txt
```

Before publication, use only artifacts tied to a specific commit SHA. Historical `v1.4.1` assets are not rewritten.

Build the installer with:

```powershell
.\installer\build-installer.ps1 -Version 1.5.0 -Runtime win-x64
```

The script also generates `SHA256SUMS.txt` for the installer and portable ZIP.

## Usage

1. Create or import a DNS profile.
2. Select the network adapter.
3. Apply the profile from UI, Tray, or CLI.
4. Verify state with `status` or diagnostics.
5. Use reset to restore automatic DNS.

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

## Updates

Settings includes:

- **Check for updates** — manual check;
- **Automatically check for updates** — an opt-out preference enabled by default.

Tray performs a background check with persisted throttling; ordinary network failures neither block startup nor show an error dialog. The stable channel ignores draft/prerelease releases.

Update delivery uses the official GitHub Releases API without a token. When a newer stable release exists, DnsSwitcher selects only `DnsSwitcher-<version>-win-x64-setup.exe`, downloads `SHA256SUMS.txt`, validates SHA-256, and only then allows the Inno Setup installer to start through the Windows/UAC flow.

**Current release gate:** the repository remains private, so the production client cannot read its Releases anonymously. No PAT or other secret is embedded in the application. Until a publicly readable release source exists, the update gate is `BLOCKED`.

See [architecture](docs/architecture/architecture.md) and the [update-delivery rule](.project-rules/AUTO_UPDATE_STANDARD.md).

## Configuration

Runtime data lives under `data/`:

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

`app-preferences.json` includes the automatic-update preference. `update-state.json` contains only throttle/last-notified state and no token or installer binary.

## Screenshots

`v1.5.0` requires real Windows screenshots of the final UI. This branch intentionally does not substitute mockups, Figma renders, or synthetic images: `SCREENSHOTS REQUIRED — awaiting real Windows capture`.

After capture, expected files under `docs/assets/screenshots/` cover Main, Tray, Settings/About, DNS Health, and Split DNS.

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

Core owns models and orchestration; Windows-specific API/IO lives in Infrastructure.Windows. Update delivery follows the same split: SemVer/models in Core, GitHub/download/checksum/installer launch in Infrastructure.Windows, presentation in UI/Tray.

## Security and privacy

- do not commit private DNS profiles, internal domains, local config, or logs;
- the update client contains no GitHub credentials;
- an installer with a SHA-256 mismatch is not launched;
- update URLs are not arbitrary commands: only expected HTTPS GitHub release paths for the configured repository are accepted;
- automatic update network checks can be disabled.

## Testing

Automated verification:

```powershell
dotnet test DnsSwitcher.sln -c Release
```

Windows CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml). Final installer/portable/checksums are built by [`.github/workflows/release-candidate.yml`](.github/workflows/release-candidate.yml) from the exact candidate commit.

System scenarios: [`docs/testing/manual-test-plan.md`](docs/testing/manual-test-plan.md). Final About/Update smoke: [`docs/testing/v1.5.0-final-smoke.md`](docs/testing/v1.5.0-final-smoke.md).

## Documentation

| Area | Document |
|---|---|
| Index | [`docs/README.md`](docs/README.md) |
| Architecture | [`docs/architecture/architecture.md`](docs/architecture/architecture.md) |
| Roadmap | [`docs/product/roadmap.md`](docs/product/roadmap.md) |
| Versions | [`docs/versions/versions-index.md`](docs/versions/versions-index.md) |
| v1.5.0 notes | [`RU`](docs/releases/v1.5.0.md) · [`EN`](docs/releases/v1.5.0_EN.md) |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) |
| Installer / portable | [`INSTALLER_RELEASE.md`](INSTALLER_RELEASE.md) · [`PORTABLE_RELEASE.md`](PORTABLE_RELEASE.md) |
| DNS Health / Split DNS | [`DNS_HEALTH_FAILOVER.md`](DNS_HEALTH_FAILOVER.md) · [`SPLIT_DNS.md`](SPLIT_DNS.md) |

## Limitations

- Windows-specific project; other operating systems are not claimed as supported;
- Split DNS uses Windows NRPT and may be bypassed by applications with their own DNS/DoH stack;
- the exact minimum Windows version is not encoded as a dedicated project property;
- the `v1.5.0` production update source is blocked while the repository/release channel is not anonymously readable;
- final real screenshots still require Windows capture.

## License

MIT — [`LICENSE.md`](LICENSE.md).
