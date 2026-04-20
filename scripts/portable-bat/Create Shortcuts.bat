@echo off
setlocal

set "DNS_SWITCHER_ROOT=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $root = [IO.Path]::GetFullPath($env:DNS_SWITCHER_ROOT); $ui = Join-Path $root 'ui\DnsSwitcher.Ui.exe'; $tray = Join-Path $root 'tray\DnsSwitcher.Tray.exe'; $cli = Join-Path $root 'cli\DnsSwitcher.Cli.exe'; if (-not (Test-Path $ui)) { throw \"DnsSwitcher.Ui.exe was not found: $ui\" }; $shell = New-Object -ComObject WScript.Shell; function New-Shortcut($path, $target, $workingDirectory, $description, $icon) { $shortcut = $shell.CreateShortcut($path); $shortcut.TargetPath = $target; $shortcut.WorkingDirectory = $workingDirectory; $shortcut.Description = $description; if (Test-Path $icon) { $shortcut.IconLocation = $icon }; $shortcut.Save() }; $desktop = [Environment]::GetFolderPath('DesktopDirectory'); $startMenu = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\DnsSwitcher'; New-Item -ItemType Directory -Path $startMenu -Force | Out-Null; New-Shortcut (Join-Path $desktop 'DnsSwitcher.lnk') $ui $root 'DnsSwitcher UI' $ui; New-Shortcut (Join-Path $startMenu 'DnsSwitcher.lnk') $ui $root 'DnsSwitcher UI' $ui; if (Test-Path $tray) { New-Shortcut (Join-Path $startMenu 'DnsSwitcher Tray.lnk') $tray $root 'DnsSwitcher Tray' $tray }; if (Test-Path $cli) { New-Shortcut (Join-Path $startMenu 'DnsSwitcher CLI.lnk') $cli (Split-Path -Parent $cli) 'DnsSwitcher CLI' $cli }; Write-Host 'Shortcuts were created on Desktop and in Start Menu.'"

if errorlevel 1 (
    echo Failed to create shortcuts.
    pause
    exit /b 1
)

pause
