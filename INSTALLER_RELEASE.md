# Installer Release

## Installer choice

DnsSwitcher uses Inno Setup for the installer track.

Reason:
- DnsSwitcher is a classic Win32 desktop application.
- Inno Setup supports admin install, service commands, shortcuts, uninstall hooks, and simple CI use.
- It lets the installer reuse existing CLI service commands instead of duplicating service logic.

WiX is still a valid future option if MSI/MSIX enterprise packaging becomes important.

## Build

Install Inno Setup 6, then run:

```powershell
.\installer\build-installer.ps1 -Version 1.4.0 -Runtime win-x64
```

Output:

```text
artifacts\installer\v1.4.0\DnsSwitcher-1.4.0-win-x64-setup.exe
```

## Install behavior

The installer:
- copies the same portable package layout into `{autopf}\DnsSwitcher`
- creates Start Menu shortcuts for UI, Tray, and CLI
- creates a Desktop shortcut for UI
- creates `data\`, `data\config\`, `data\logs\`
- grants normal users modify permission to `data\`
- runs `cli\DnsSwitcher.Cli.exe service reinstall`
- optionally starts Tray after install

The service is still registered to:

```text
{app}\service\agent\DnsSwitcher.Agent.Windows.exe
```

not to:

```text
{app}\agent\DnsSwitcher.Agent.Windows.exe
```

## Uninstall behavior

The installer runs:

```powershell
cli\DnsSwitcher.Cli.exe service stop
cli\DnsSwitcher.Cli.exe service uninstall
```

before removing files.

## Data location

Installer and portable builds use the same rule:

```text
<install-root>\data\
```

For a default machine-wide install:

```text
C:\Program Files\DnsSwitcher\data\
```

The installer grants modify permissions to `data\` so non-admin UI/Tray can write settings and logs.

## Updating

Run the newer installer. It calls `service reinstall`, so the agent runtime copy is refreshed.

`service install`, `service stop`, and `service uninstall` are idempotent enough for repeated install/uninstall attempts. If the service exists but points to an old path, the UI Agent window and `service status` show that warning and `service reinstall` repairs it.

## Manual validation

If Inno Setup is not available on the build machine, the installer script is still ready but the `.exe` cannot be produced there. Install Inno Setup 6 or provide `ISCC.exe` in `PATH`, then run the build command above.
