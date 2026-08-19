param(
    [string]$Version,
    [string]$Runtime = "win-x64",
    # Retained for compatibility with existing build commands. Installer builds are always self-contained.
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$publishScript = Join-Path $repoRoot "scripts\publish-release.ps1"
$innoScript = Join-Path $scriptRoot "DnsSwitcher.iss"

function Resolve-ProjectVersion {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    [xml]$props = Get-Content $propsPath
    $resolved = [string]$props.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($resolved)) {
        throw "Version is missing from Directory.Build.props."
    }

    return $resolved.Trim()
}

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

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Resolve-ProjectVersion
}

Push-Location $repoRoot
try {
    $iscc = Resolve-InnoCompiler

    $publishArgs = @{
        Version = $Version
        Runtime = $Runtime
        SelfContained = $true
    }

    if ($SelfContained) {
        Write-Verbose "-SelfContained is retained for compatibility; installer builds are self-contained by default."
    }

    if ($SkipTests) {
        $publishArgs.SkipTests = $true
    }

    & $publishScript @publishArgs
    & $iscc $innoScript "/DAppVersion=$Version" "/DRuntime=$Runtime"

    $installerDirectory = Join-Path $repoRoot "artifacts\installer\v$Version"
    $installerPath = Join-Path $installerDirectory "DnsSwitcher-$Version-$Runtime-setup.exe"
    $portablePath = Join-Path $repoRoot "artifacts\release\v$Version\DnsSwitcher-$Version-$Runtime.zip"
    $checksumPath = Join-Path $installerDirectory "SHA256SUMS.txt"

    if (-not (Test-Path $installerPath)) {
        throw "Expected installer was not created: $installerPath"
    }

    if (-not (Test-Path $portablePath)) {
        throw "Expected portable ZIP was not created: $portablePath"
    }

    $installerHash = (Get-FileHash $installerPath -Algorithm SHA256).Hash
    $portableHash = (Get-FileHash $portablePath -Algorithm SHA256).Hash
    @(
        "$installerHash  $(Split-Path $installerPath -Leaf)"
        "$portableHash  $(Split-Path $portablePath -Leaf)"
    ) | Set-Content -Path $checksumPath -Encoding ascii

    Write-Host "Release checksums created:"
    Get-Content $checksumPath
}
finally {
    Pop-Location
}
