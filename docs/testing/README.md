# Тестирование

Автоматические проверки, предусмотренные репозиторием:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
dotnet test DnsSwitcher.sln -c Release
```

Решение содержит:

- `DnsSwitcher.Tests` — unit tests;
- `DnsSwitcher.IntegrationTests` — Windows-specific Named Pipe integration tests.

Ручные Windows/system scenarios описаны в [`manual-test-plan.md`](manual-test-plan.md). Проверки, которые требуют реального DNS, прав администратора, Windows Service, NRPT, portable package или installer, нельзя отмечать выполненными по одному факту успешных unit tests.

Дополнительное описание IPC integration tests: [`../ipc-integration-tests.md`](../ipc-integration-tests.md).
