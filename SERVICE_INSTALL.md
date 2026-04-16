# DnsSwitcher Agent Service

## Why the agent exists

Changing DNS settings requires administrator rights. UI and Tray should not run elevated all the time.

The agent runs as a Windows Service and performs privileged operations for UI/Tray/CLI through a named pipe.

## Why it is not fully portable

A Windows Service is registered in Windows Service Control Manager. That registration is machine state, not portable folder state.

Portable DnsSwitcher keeps binaries portable, but service installation remains a real Windows install step.

## Commands

From portable root:

```powershell
.\cli\DnsSwitcher.Cli.exe service install
.\cli\DnsSwitcher.Cli.exe service start
.\cli\DnsSwitcher.Cli.exe service status
.\cli\DnsSwitcher.Cli.exe service stop
.\cli\DnsSwitcher.Cli.exe service uninstall
.\cli\DnsSwitcher.Cli.exe service reinstall
```

BAT equivalents:

```text
Install Agent.bat
Reinstall Agent.bat
Uninstall Agent.bat
Start Agent.bat
Stop Agent.bat
Agent Status.bat
```

Desktop UI:
- open `Agent`
- use Install, Reinstall, Start, Stop, Uninstall or Refresh
- operations that change the service start an elevated CLI process and show a UAC prompt
- status shows whether the service points to the expected `service\agent\` runtime path

Tray:
- open the Agent submenu
- use Status, Start, Stop or Reinstall
- operations that change the service also use the elevated CLI path

## Install behavior

`service install`:
1. Locates source `agent\DnsSwitcher.Agent.Windows.exe`.
2. Copies the agent folder into `service\agent\`.
3. Registers Windows Service against `service\agent\DnsSwitcher.Agent.Windows.exe`.

This protects upgrades: release `agent\` is a source folder, service `service\agent\` is the runtime copy.

`service install` is safe to run repeatedly. If the service is already installed it reports that state instead of failing. If the service points to an old folder, run `service reinstall`.

## Reinstall / upgrade

Use:

```powershell
.\cli\DnsSwitcher.Cli.exe service reinstall
```

This performs:
1. stop if running
2. uninstall if installed
3. install from current package
4. start

## After moving the app folder

Run:

```text
Reinstall Agent.bat
```

The existing service still points to the old folder until reinstalled.

## Logs

```text
data\logs\dns-switcher.log
```
