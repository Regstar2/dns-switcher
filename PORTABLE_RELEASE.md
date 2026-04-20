# Portable Release

## What is portable

The application binaries are portable:
- `cli\`
- `ui\`
- `tray\`
- `agent\`

Runtime data is stored beside the package under `data\`.

## What is not strictly portable

The agent is a Windows Service. Installing it registers a service in Windows and requires administrator rights.

The service executable is copied into:

```text
service\agent\
```

This is intentional. Do not register the service directly against `agent\`.

## Layout

```text
DnsSwitcher\
  cli\
  ui\
  tray\
  agent\
  service\
    agent\
  data\
    config\
      profiles.json
      app-preferences.json
      tray-settings.json
      ui-settings.json
      dns-benchmark-history.json
      dns-health-settings.json
      dns-health-state.json
      split-dns-rules.json
    logs\
      dns-switcher.log
```

## Root scripts

Portable release contains:
- `Install Agent.bat`
- `Reinstall Agent.bat`
- `Uninstall Agent.bat`
- `Start Agent.bat`
- `Stop Agent.bat`
- `Agent Status.bat`
- `Create Shortcuts.bat`
- `_RunAsAdmin.bat`

All scripts:
- run relative to `%~dp0`
- request UAC automatically when needed
- call `cli\DnsSwitcher.Cli.exe service ...`
- do not hardcode an install path
- handle paths with spaces by quoting `%~dp0`
- keep the service registered against `service\agent\`, not `agent\`

`Create Shortcuts.bat` does not require administrator rights. It creates a Desktop shortcut for the UI and Start Menu shortcuts for UI, Tray, and CLI for the current Windows user.

## Running

UI:

```powershell
.\ui\DnsSwitcher.Ui.exe
```

Tray:

```powershell
.\tray\DnsSwitcher.Tray.exe
```

CLI:

```powershell
.\cli\DnsSwitcher.Cli.exe status
```

Typical first run:

```text
Install Agent.bat
Start Agent.bat
Create Shortcuts.bat
ui\DnsSwitcher.Ui.exe
```

After install, the UI Agent window and Tray Agent submenu can manage the service without manually opening PowerShell.

## Building portable release

```powershell
.\scripts\publish-release.ps1 -Version 1.4.0 -Runtime win-x64
```

Output:

```text
artifacts\release\v1.4.0\DnsSwitcher-1.4.0-win-x64.zip
```

## Updating portable release

Recommended flow:
1. Stop tray/UI.
2. Run `Stop Agent.bat`.
3. Extract new package over the old package or into a new folder.
4. Run `Reinstall Agent.bat`.
5. Start tray/UI again.

If the folder was moved, run `Reinstall Agent.bat` so the Windows Service points to the new `service\agent\` runtime copy.
