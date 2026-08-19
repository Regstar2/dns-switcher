# Portable Release

## Package model

The portable package contains:

```text
cli\
ui\
tray\
agent\
service\agent\
data\config\
data\logs\
```

Runtime data stays beside the package under `data\`. Installing the Agent registers a Windows Service and therefore is not a strictly portable operation.

## Config layout

Typical `data/config/` files include:

```text
profiles.json
app-preferences.json
tray-settings.json
ui-settings.json
dns-benchmark-history.json
dns-health-settings.json
dns-health-state.json
split-dns-rules.json
update-state.json
```

`update-state.json` stores only update-check throttle/notification state. It does not store credentials or downloaded installer binaries.

## Root scripts

Portable release contains Agent lifecycle and shortcut helper BAT files. They run relative to `%~dp0`, elevate only when required, and keep the Windows Service registered against `service\agent\` rather than the development/publish `agent\` directory.

## Running

```powershell
.\ui\DnsSwitcher.Ui.exe
.\tray\DnsSwitcher.Tray.exe
.\cli\DnsSwitcher.Cli.exe status
```

## Building `v1.5.0`

```powershell
.\scripts\publish-release.ps1 -Version 1.5.0 -Runtime win-x64 -SelfContained
```

Expected archive:

```text
artifacts\release\v1.5.0\DnsSwitcher-1.5.0-win-x64.zip
```

The final installer build calls the same publish script with self-contained delivery enabled and subsequently creates `SHA256SUMS.txt` for both installer and portable ZIP.

## Updating portable builds

Recommended flow:

1. close UI/Tray;
2. stop Agent;
3. back up `data/config/`;
4. extract the new package to a new directory or replace the application files while preserving `data/`;
5. run `Reinstall Agent.bat` if the package path changed;
6. start Tray/UI again.

The in-app installer update path is intended for installed builds; portable users should use the portable package and preserve their local `data/` directory explicitly.
