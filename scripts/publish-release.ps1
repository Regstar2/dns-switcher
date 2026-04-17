param(
    [string]$Version = "1.4.0",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$configuration = "Release"
$releaseRoot = Join-Path $repoRoot "artifacts\release\v$Version"
$packageName = "DnsSwitcher-$Version-$Runtime"
$packageDir = Join-Path $releaseRoot $packageName
$archivePath = Join-Path $releaseRoot "$packageName.zip"
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

$apps = @(
    @{ Name = "cli"; Project = "src\DnsSwitcher.Cli\DnsSwitcher.Cli.csproj" },
    @{ Name = "ui"; Project = "src\DnsSwitcher.Ui\DnsSwitcher.Ui.csproj" },
    @{ Name = "tray"; Project = "src\DnsSwitcher.Tray\DnsSwitcher.Tray.csproj" },
    @{ Name = "agent"; Project = "src\DnsSwitcher.Agent.Windows\DnsSwitcher.Agent.Windows.csproj" }
)

function Get-NormalizedFullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Assert-ReleasePathIsNotUsedByService([string]$Path) {
    $service = Get-CimInstance Win32_Service -Filter "Name='DnsSwitcherAgent'" -ErrorAction SilentlyContinue
    if ($null -eq $service -or [string]::IsNullOrWhiteSpace($service.PathName)) {
        return
    }

    $serviceExecutable = $service.PathName.Trim('"')
    $serviceDirectory = Split-Path -Parent $serviceExecutable
    if ([string]::IsNullOrWhiteSpace($serviceDirectory)) {
        return
    }

    $normalizedServiceDirectory = Get-NormalizedFullPath $serviceDirectory
    $normalizedReleaseRoot = Get-NormalizedFullPath $Path
    $serviceUsesReleaseRoot = $normalizedServiceDirectory.StartsWith($normalizedReleaseRoot, [System.StringComparison]::OrdinalIgnoreCase)

    if ($serviceUsesReleaseRoot -and $service.State -eq "Running") {
        Write-Host "Cannot rebuild release '$normalizedReleaseRoot' because DnsSwitcherAgent is currently running from this directory:" -ForegroundColor Yellow
        Write-Host $serviceExecutable
        Write-Host ""
        Write-Host "Stop the agent from an elevated PowerShell, then rerun this script:"
        Write-Host "  .\artifacts\release\v$Version\$packageName\cli\DnsSwitcher.Cli.exe service stop"
        Write-Host ""
        Write-Host "Alternative:"
        Write-Host "  .\artifacts\release\v$Version\$packageName\Stop Agent.bat"
        Write-Host ""
        Write-Host "This guard prevents partially deleting a portable release that is currently used as the Windows Service runtime."
        exit 1
    }
}

Push-Location $repoRoot
try {
    if (-not $SkipTests) {
        dotnet test "tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj" -c $configuration /p:UseSharedCompilation=false /nr:false
    }

    if (Test-Path $releaseRoot) {
        Assert-ReleasePathIsNotUsedByService $releaseRoot
        Remove-Item $releaseRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageDir | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $packageDir "data\config") | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $packageDir "data\logs") | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $packageDir "service\agent") | Out-Null

    foreach ($app in $apps) {
        $outputDir = Join-Path $packageDir $app.Name
        $publishArgs = @(
            "publish",
            $app.Project,
            "-c",
            $configuration,
            "-r",
            $Runtime,
            "--self-contained",
            $selfContainedValue,
            "-o",
            $outputDir,
            "/p:Version=$Version",
            "/p:DebugType=None",
            "/p:DebugSymbols=false",
            "/p:UseSharedCompilation=false",
            "/nr:false"
        )

        dotnet @publishArgs
    }

    Copy-Item "README.md" (Join-Path $packageDir "README.md")
    Copy-Item "README.ru.md" (Join-Path $packageDir "README.ru.md")
    Copy-Item "CHANGELOG.md" (Join-Path $packageDir "CHANGELOG.md")
    Copy-Item "scripts\portable-bat\*.bat" $packageDir

    $docsDir = Join-Path $packageDir "docs"
    New-Item -ItemType Directory -Path $docsDir | Out-Null
    Copy-Item "docs\config-layout.md" (Join-Path $docsDir "config-layout.md")
    Copy-Item "docs\profiles.example.json" (Join-Path $docsDir "profiles.example.json")
    Copy-Item "docs\profiles.schema.json" (Join-Path $docsDir "profiles.schema.json")

    $releaseDocs = @(
        "ARCHITECTURE_AUDIT.md",
        "PORTABLE_RELEASE.md",
        "SERVICE_INSTALL.md",
        "INSTALLER_RELEASE.md",
        "DNS_HEALTH_FAILOVER.md",
        "SPLIT_DNS.md",
        "STORE_READINESS.md"
    )

    foreach ($doc in $releaseDocs) {
        Copy-Item $doc (Join-Path $docsDir $doc)
    }

    Compress-Archive -Path $packageDir -DestinationPath $archivePath -Force

    Write-Host "Release package created:"
    Write-Host $archivePath
}
finally {
    Pop-Location
}
