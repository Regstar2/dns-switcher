# Installer Release

## Installer choice

DnsSwitcher uses Inno Setup 6 for the Windows installer. The installer reuses existing CLI service commands for Agent installation and removal rather than duplicating service-management logic.

## Version source

The normal release version is defined in `Directory.Build.props`. `installer/build-installer.ps1` accepts an explicit `-Version`, but when it is omitted the script resolves the project version from that file.

Current release-candidate version: `1.5.0`.

## Build

Install Inno Setup 6 on the build machine, then run:

```powershell
.\installer\build-installer.ps1 -Version 1.5.0 -Runtime win-x64
```

Installer builds are self-contained by default. UI, Tray, CLI, and Agent include the required .NET runtime; the target machine does not need a separately installed .NET Desktop Runtime.

`-SelfContained` is retained for compatibility with older commands. `-SkipTests` must not be used for the final release build.

Expected outputs:

```text
artifacts\installer\v1.5.0\DnsSwitcher-1.5.0-win-x64-setup.exe
artifacts\installer\v1.5.0\SHA256SUMS.txt
artifacts\release\v1.5.0\DnsSwitcher-1.5.0-win-x64.zip
```

`SHA256SUMS.txt` is generated from the actual installer and portable ZIP after packaging.

## Install behavior

The installer:

- copies the portable package layout into `{autopf}\DnsSwitcher`;
- creates Start Menu shortcuts for UI, Tray, and CLI and a Desktop shortcut for UI;
- creates `data\`, `data\config\`, and `data\logs\` with user-modify permissions;
- runs `cli\DnsSwitcher.Cli.exe service reinstall`;
- can start Tray after installation.

The service runtime is copied to and registered against:

```text
{app}\service\agent\DnsSwitcher.Agent.Windows.exe
```

## Upgrade behavior

Install a newer installer over the existing installation. The package reuses the same `AppId` and installation root and runs `service reinstall`, so the Agent runtime path is refreshed.

The release process must verify that existing `data/config/` content remains available after the upgrade. The final update flow hands off to this installer rather than overwriting running binaries itself.

## Uninstall behavior

Before file removal the installer runs:

```powershell
cli\DnsSwitcher.Cli.exe service stop
cli\DnsSwitcher.Cli.exe service uninstall
```

The existing data-deletion policy is not changed as part of `v1.5.0` finalization.

## Update delivery

The application may download the installer only from the configured trusted release source. Before launch it requires a matching installer entry in `SHA256SUMS.txt` and verifies the local SHA-256. A mismatch prevents installer launch.

The installer is launched through the normal Windows shell/UAC path; DnsSwitcher does not copy new executable files over the installed application itself.

## Manual validation

Validate the final candidate on Windows using `docs/testing/v1.5.0-final-smoke.md`, including:

- clean/self-contained install;
- About/version display;
- update preference and manual check;
- valid and corrupted checksum paths when a public release source is available;
- upgrade preservation of profiles/settings/Health/Split DNS state;
- Agent path after upgrade;
- uninstall/service cleanup.
