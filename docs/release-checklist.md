# Release Checklist

## v1.4.1

1. Verify working tree does not contain private config:

```powershell
git status --short
git diff --cached --name-only | rg "profiles\.json|(^|/)data/|(^|/)bin/|(^|/)obj/"
```

2. Build and test:

```powershell
dotnet build-server shutdown
dotnet build DnsSwitcher.sln -c Release /p:UseSharedCompilation=false /nr:false
dotnet test tests\DnsSwitcher.Tests\DnsSwitcher.Tests.csproj -c Release /p:UseSharedCompilation=false /nr:false
```

3. Create release package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.4.1 -Runtime win-x64
```

4. Optional installer build:

```powershell
.\installer\build-installer.ps1 -Version 1.4.1 -Runtime win-x64
```

5. Check release output:

```text
artifacts/release/v1.4.1/DnsSwitcher-1.4.1-win-x64.zip
artifacts/installer/v1.4.1/DnsSwitcher-1.4.1-win-x64-setup.exe
```

6. Tag after the release commit:

```powershell
git tag v1.4.1
git push
git push origin v1.4.1
```

7. Attach the zip from `artifacts/release/v1.4.1/` to the GitHub release.

## Notes

- Do not commit local `data/config/profiles.json`.
- The package is framework-dependent by default and requires .NET 10 on the target machine.
- Use `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -SelfContained` if a larger package without a separate .NET runtime install is needed.
