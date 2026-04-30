param(
    [string]$Version = "1.4.1",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$publishScript = Join-Path $repoRoot "scripts\publish-release.ps1"
$innoScript = Join-Path $scriptRoot "DnsSwitcher.iss"

function Resolve-InnoCompiler {
    $isccCandidates = @()

    if (${env:ProgramFiles(x86)}) {
        $isccCandidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    }

    if ($env:ProgramFiles) {
        $isccCandidates += Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
    }

    if ($env:LOCALAPPDATA) {
        $isccCandidates += Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    }

    $iscc = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if (-not $iscc) {
        $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
        if ($command) {
            $iscc = $command.Source
        }
    }

    if (-not $iscc) {
        Write-Host "Inno Setup 6 compiler (ISCC.exe) was not found." -ForegroundColor Yellow
        Write-Host "Install Inno Setup 6 or add ISCC.exe to PATH, then rerun this script."
        Write-Host "Suggested winget command:"
        Write-Host "  winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements"
        Write-Host "Portable release packaging does not require Inno Setup."
        exit 1
    }

    return $iscc
}

Push-Location $repoRoot
try {
    $iscc = Resolve-InnoCompiler

    $publishArgs = @{
        Version = $Version
        Runtime = $Runtime
    }

    if ($SelfContained) {
        $publishArgs.SelfContained = $true
    }

    if ($SkipTests) {
        $publishArgs.SkipTests = $true
    }

    & $publishScript @publishArgs

    & $iscc $innoScript "/DAppVersion=$Version" "/DRuntime=$Runtime"
}
finally {
    Pop-Location
}
