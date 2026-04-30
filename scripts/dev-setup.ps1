param(
    [string]$SdkVersion = "10.0.201",
    [string]$InstallDir = "$env:USERPROFILE\.dotnet"
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message)
{
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Add-UserPathEntry([string]$PathEntry)
{
    if ([string]::IsNullOrWhiteSpace($PathEntry))
    {
        return
    }

    $currentUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $entries = @()
    if (-not [string]::IsNullOrWhiteSpace($currentUserPath))
    {
        $entries = $currentUserPath.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
    }

    if ($entries -contains $PathEntry)
    {
        return
    }

    $updated = @($entries + $PathEntry) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $updated, "User")
}

Write-Step "Checking .NET SDKs"
$dotnetOnPath = Get-Command dotnet -ErrorAction SilentlyContinue
$hasRequestedSdk = $false

if ($dotnetOnPath)
{
    $installed = dotnet --list-sdks
    $hasRequestedSdk = $installed -match "^$([regex]::Escape($SdkVersion))\s"
}

if (-not $hasRequestedSdk)
{
    Write-Step "Installing .NET SDK $SdkVersion into $InstallDir"
    $installerPath = Join-Path $env:TEMP "dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installerPath
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installerPath -Version $SdkVersion -InstallDir $InstallDir -NoPath
}
else
{
    Write-Step ".NET SDK $SdkVersion is already installed"
}

Write-Step "Persisting DOTNET_ROOT and PATH for current user"
[Environment]::SetEnvironmentVariable("DOTNET_ROOT", $InstallDir, "User")
Add-UserPathEntry -PathEntry $InstallDir
Add-UserPathEntry -PathEntry (Join-Path $InstallDir "tools")

$dotnetExe = Join-Path $InstallDir "dotnet.exe"
if (-not (Test-Path $dotnetExe))
{
    throw "dotnet.exe was not found at '$dotnetExe'."
}

Write-Step "Restoring solution"
& $dotnetExe restore "$PSScriptRoot\..\DnsSwitcher.sln"

Write-Step "Building solution (Release)"
& $dotnetExe build "$PSScriptRoot\..\DnsSwitcher.sln" -c Release --no-restore

Write-Step "Running tests"
& $dotnetExe test "$PSScriptRoot\..\tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj" -c Release --no-build

Write-Host ""
Write-Host "Development environment is ready." -ForegroundColor Green
Write-Host "If 'dotnet' is still not available in your shell, restart terminal/IDE."
