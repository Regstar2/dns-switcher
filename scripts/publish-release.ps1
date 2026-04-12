param(
    [string]$Version = "1.3.0",
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

Push-Location $repoRoot
try {
    if (-not $SkipTests) {
        dotnet test "tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj" -c $configuration /p:UseSharedCompilation=false /nr:false
    }

    if (Test-Path $releaseRoot) {
        Remove-Item $releaseRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageDir | Out-Null

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
    Copy-Item "CHANGELOG.md" (Join-Path $packageDir "CHANGELOG.md")

    $docsDir = Join-Path $packageDir "docs"
    New-Item -ItemType Directory -Path $docsDir | Out-Null
    Copy-Item "docs\config-layout.md" (Join-Path $docsDir "config-layout.md")
    Copy-Item "docs\profiles.example.json" (Join-Path $docsDir "profiles.example.json")
    Copy-Item "docs\profiles.schema.json" (Join-Path $docsDir "profiles.schema.json")

    Compress-Archive -Path $packageDir -DestinationPath $archivePath -Force

    Write-Host "Release package created:"
    Write-Host $archivePath
}
finally {
    Pop-Location
}
