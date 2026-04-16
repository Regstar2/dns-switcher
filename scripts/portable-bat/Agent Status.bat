@echo off
setlocal
call "%~dp0_RunAsAdmin.bat" "%~f0"
if errorlevel 1 exit /b %errorlevel%
cd /d "%~dp0"
"%~dp0cli\DnsSwitcher.Cli.exe" service status
if errorlevel 1 (
  echo Failed to read DnsSwitcher Agent status. Exit code: %errorlevel%
  pause
  exit /b %errorlevel%
)
pause
