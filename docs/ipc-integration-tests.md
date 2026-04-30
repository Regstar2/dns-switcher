# IPC Integration Tests

`DnsSwitcher.IntegrationTests` covers safe Named Pipe integration scenarios between a real IPC client and a test pipe server.

## Run

```powershell
dotnet test tests\DnsSwitcher.IntegrationTests\DnsSwitcher.IntegrationTests.csproj -c Release
```

Or run all tests from solution:

```powershell
dotnet test DnsSwitcher.sln -c Release
```

## Design Notes

- Each test uses a unique pipe name (`Guid`) to avoid collisions.
- Tests use timeouts and cancellation tokens to prevent hangs.
- Test server is in-process and disposable; no production agent/service is used.
- DNS settings are never modified. Tests validate request/response contract only.

## Adding New IPC Scenarios

1. Reuse `TestPipeServer` in `NamedPipeIpcIntegrationTests`.
2. Keep unique pipe names.
3. Add explicit timeout assertions for failure paths.
4. Prefer deterministic sync (`TaskCompletionSource`, awaited tasks) over sleeps.

## Timeout/Flaky Diagnostics

- If a test times out, check that server writes a newline-terminated response (`WriteLineAsync`).
- Ensure client timeout is shorter than server delay in timeout tests.
- Verify each test uses a fresh pipe name and disposes server resources.
