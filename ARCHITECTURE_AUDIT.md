# DnsSwitcher Architecture Audit

Date: 2026-04-16

## Portable vs non-portable

Portable:
- `cli\`, `ui\`, `tray\`, `agent\` are published as ordinary portable folders.
- Runtime data is stored under the package root in `data\`.
- Config and logs are not stored in `%APPDATA%` or `%LOCALAPPDATA%` by default.

Not strictly portable:
- `DnsSwitcher.Agent.Windows` is a Windows Service.
- Installing the service writes to the Service Control Manager and requires elevation.
- The installed service executable is copied to `service\agent\` and Windows Service points to that runtime copy.

## Path resolution audit

Before this change, clients could derive data from their own executable folder, which could create separate `cli\data`, `ui\data`, `tray\data`, or `service\agent\data`.

Current resolver:
- `PortableRootResolver.ResolvePortableRoot(AppContext.BaseDirectory)` walks up to the solution root in dev builds.
- In release layout, `cli\`, `ui\`, `tray\`, and `agent\` resolve to their parent package directory.
- In service runtime layout, `service\agent\` resolves to the package root two levels above.
- All runtime paths are built from `PortableRoot\data`.

Created directories:
- `data\`
- `data\config\`
- `data\logs\`

Shared config/log files:
- `data\config\profiles.json`
- `data\config\app-preferences.json`
- `data\config\tray-settings.json`
- `data\config\ui-settings.json`
- `data\config\dns-benchmark-history.json`
- `data\config\dns-health-settings.json`
- `data\config\dns-health-state.json`
- `data\config\split-dns-rules.json`
- `data\logs\dns-switcher.log`

Migration:
- On startup, `PortableAppPaths` checks legacy local `data\config\` beside the current executable.
- Missing config files are copied into the shared `data\config\`.
- Existing shared files are not overwritten.

## Service lifecycle

CLI commands:
- `dns-switcher service install [agent-exe-path]`
- `dns-switcher service reinstall [agent-exe-path]`
- `dns-switcher service uninstall`
- `dns-switcher service start`
- `dns-switcher service stop`
- `dns-switcher service status`

Install behavior:
- Source agent is resolved from `agent\DnsSwitcher.Agent.Windows.exe` in release packages or from build outputs in dev mode.
- Install copies the whole agent folder into `service\agent\`.
- The Windows Service is registered against `service\agent\DnsSwitcher.Agent.Windows.exe`.
- The service is not registered against `agent\` directly.

## Elevation dependencies

Requires administrator or LocalSystem:
- Installing/uninstalling/starting/stopping the service.
- Applying DNS profile when the agent is unavailable.
- Resetting DNS to DHCP when the agent is unavailable.
- Applying/resetting Split DNS NRPT rules when the agent is unavailable.

Does not require elevation when agent is running:
- UI/Tray/CLI profile switching through named pipe agent.
- Split DNS apply/reset through named pipe agent.

User-facing management:
- Desktop UI has an Agent window for install/reinstall/start/stop/uninstall/status.
- Tray has a compact Agent submenu for status/start/stop/reinstall.
- Service-changing UI/Tray actions launch the CLI with UAC instead of forcing the entire UI/Tray process to run elevated.

## Extension points

DNS switching:
- Core: `IDnsManager`, `IDnsProfileActivator`, `DnsSwitchService`.
- Windows: `WindowsDnsManager`, `AgentAwareDnsSwitchService`.

DNS testing:
- Core: `DnsTester`, `ConnectivityTester`, `DnsBenchmarkService`.
- Windows: `UdpDnsQueryClient`, `HttpSiteProbeClient`.

Health failover:
- Core: `DnsHealthFailoverService`.
- Settings/state stores: `IDnsHealthSettingsStore`, `IDnsHealthStateStore`.
- Agent background worker: `DnsHealthMonitorWorker`.

Split DNS:
- Core: `SplitDnsRuleService`, `ISplitDnsManager`.
- Windows implementation: `WindowsNrptSplitDnsManager`.
- Agent commands: `ApplySplitDns`, `ResetSplitDns`.

## Changes made

- Added a shared portable root resolver.
- Added service `reinstall`.
- Added portable BAT scripts.
- Added installer track based on Inno Setup.
- Added DNS health settings/state/config stores and failover service.
- Added agent background health monitor.
- Added Split DNS config/rule model and Windows NRPT applier.
- Added CLI/UI/Tray surfaces for health and Split DNS.
- Added desktop UI windows for Agent management, DNS Health Failover settings, and Split DNS rule editing.
- Added service path diagnostics to detect stale service registrations after moving/upgrading the app folder.
- Added tests for path resolution, health failover, and Split DNS rule logic.
