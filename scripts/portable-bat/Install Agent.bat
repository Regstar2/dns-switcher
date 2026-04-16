@echo off
setlocal
call "%~dp0_RunAsAdmin.bat" "%~f0"
if errorlevel 1 exit /b %errorlevel%
cd /d "%~dp0"
"%~dp0cli\DnsSwitcher.Cli.exe" service install
if errorlevel 1 (
  echo Failed to install DnsSwitcher Agent. Exit code: %errorlevel%
  pause
  exit /b %errorlevel%
)
"%~dp0cli\DnsSwitcher.Cli.exe" service start
if errorlevel 1 (
  echo Agent was installed, but failed to start. Exit code: %errorlevel%
  pause
  exit /b %errorlevel%
)
echo DnsSwitcher Agent installed and started.
pause
