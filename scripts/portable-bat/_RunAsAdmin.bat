@echo off
net session >nul 2>&1
if "%errorlevel%"=="0" exit /b 0

echo Requesting administrator privileges...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~1' -Verb RunAs"
if errorlevel 1 (
  echo Failed to request administrator privileges.
)
exit /b 1
