param(
    [string]$Version = "1.4.0",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$publishScript = Join-Path $repoRoot "scripts\publish-release.ps1"
$innoScript = Join-Path $scriptRoot "DnsSwitcher.iss"

Push-Location $repoRoot
try {
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

    $isccCandidates = @()

    if (${env:ProgramFiles(x86)}) {
        $isccCandidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    }

    if ($env:ProgramFiles) {
        $isccCandidates += Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
    }

    $iscc = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if (-not $iscc) {
        $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
        if ($command) {
            $iscc = $command.Source
        }
    }

    if (-not $iscc) {
        throw "Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6 or add ISCC.exe to PATH."
    }

    & $iscc $innoScript "/DAppVersion=$Version" "/DRuntime=$Runtime"
}
finally {
    Pop-Location
}
