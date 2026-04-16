@echo off
setlocal
call "%~dp0_RunAsAdmin.bat" "%~f0"
if errorlevel 1 exit /b %errorlevel%
cd /d "%~dp0"
"%~dp0cli\DnsSwitcher.Cli.exe" service uninstall
if errorlevel 1 (
  echo Failed to uninstall DnsSwitcher Agent. Exit code: %errorlevel%
  pause
  exit /b %errorlevel%
)
echo DnsSwitcher Agent uninstalled.
pause
