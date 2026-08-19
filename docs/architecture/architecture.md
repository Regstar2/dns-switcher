# Архитектура

## Обзор

DnsSwitcher разделён на Core, Windows infrastructure, IPC contracts, привилегированный Agent и пользовательские UI/CLI/Tray clients.

```text
UI / CLI / Tray ──> DnsSwitcher.Core
       │
       └──> DnsSwitcher.Infrastructure.Windows ──> DnsSwitcher.Core
                    │
                    ├──> DnsSwitcher.Contracts
                    ├──> GitHub Releases / HTTPS update source
                    └── Named Pipes ──> DnsSwitcher.Agent.Windows
```

## Solution layers

| Проект | Ответственность |
|---|---|
| `DnsSwitcher.Core` | модели, абстракции, валидация, DNS/profile orchestration, SemVer/update contracts |
| `DnsSwitcher.Contracts` | Agent request/response protocol |
| `DnsSwitcher.Infrastructure.Windows` | Windows DNS/NRPT, JSON storage, Agent client, update HTTP/download/checksum/installer launch |
| `DnsSwitcher.Ui` | WPF presentation, Settings/About/Help/manual update UX |
| `DnsSwitcher.Tray` | quick actions и throttled automatic update notification |
| `DnsSwitcher.Agent.Windows` | privileged Windows Service и Named Pipe server |

## Composition root

`WindowsDnsSwitcherHost` собирает profile/DNS/diagnostic/Agent/Health/Split services и update delivery. Каноническая runtime version и repository URL читаются из assembly metadata, сформированного `Directory.Build.props`.

## DNS и привилегии

DNS switching, Health Failover и Split DNS сохраняют существующие contracts. Для привилегированных операций client может использовать Agent по Named Pipes; Windows NRPT остаётся системной реализацией Split DNS.

Update delivery не участвует в DNS apply/reset и при сетевой ошибке не должен влиять на запуск или DNS functionality.

## Update delivery

```text
Directory.Build.props
        │
        └─ assembly metadata ──> installed SemanticVersion

Tray automatic check / UI manual check
        │
        └─ IUpdateService
             │
             └─ GitHubReleaseUpdateService
                   ├─ anonymous HTTPS release metadata
                   ├─ stable channel: no draft/prerelease
                   ├─ exact win-x64 installer asset
                   ├─ SHA256SUMS.txt
                   ├─ temporary download directory
                   └─ Inno Setup launch via Windows shell/UAC
```

### Security invariants

- PAT/OAuth/API token не хранится в client binaries/config/environment fallback;
- repository source должен быть HTTPS GitHub `owner/repository`;
- download принимается только из ожидаемого `github.com/<owner>/<repo>/releases/download/...`;
- asset name строго `DnsSwitcher-<version>-win-x64-setup.exe`;
- installer не запускается до SHA-256 match;
- stable channel игнорирует GitHub draft/prerelease и SemVer prerelease;
- user может отключить automatic network checks.

### Automatic check

Tray запускает `AutomaticUpdateMonitor`. Он периодически перечитывает `app-preferences.json`, а `update-state.json` хранит только `lastCheckedUtc` и `lastNotifiedVersion`. Это предотвращает частые запросы/повторные уведомления без хранения secret или installer.

### Release source requirement

Production update discovery требует publicly readable HTTPS GitHub Releases source. Client credentials намеренно не встраиваются; если release metadata недоступна anonymous client, update-check завершается как nonfatal update-source/network failure, а пользователь может установить release asset вручную после проверки SHA-256.

## Configuration

Runtime files находятся в `data/config/`; private runtime data не коммитятся. Update preference добавлен в существующий `app-preferences.json`; throttle state — отдельный `update-state.json`, поскольку это operational state, а не пользовательская настройка.

## Testing

- `DnsSwitcher.Tests` покрывает Core/infrastructure, включая SemVer, release parsing, HTTP failures, checksum verification, preferences/state/localization static contracts;
- `DnsSwitcher.IntegrationTests` проверяет Windows Named Pipe IPC;
- `.github/workflows/ci.yml` выполняет Windows Release build/tests;
- `.github/workflows/release-candidate.yml` строит exact-commit installer/portable/checksums;
- DNS/NRPT/service и новый About/Update UI требуют Windows manual smoke согласно `docs/testing/`.
