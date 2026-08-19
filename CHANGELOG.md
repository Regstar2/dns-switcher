# Changelog

## Unreleased

### Added

- Configurable optional tray-menu groups in Desktop Settings with backward-compatible defaults and live Tray refresh.
- About and Help surfaces with the installed assembly version and canonical GitHub repository link, plus direct Health, About, and Help navigation from the main-window **More** menu.
- Detailed in-app Help covering DNS profiles, adapter selection, diagnostics, Health Failover, Split DNS, Agent, Tray, import/export, settings, updates, config, and logs with usage guidance and relevant limitations.
- Manual stable-channel update checks and an opt-out automatic update preference.
- Typed SemVer comparison, anonymous GitHub Releases discovery, strict Windows x64 installer selection, SHA-256 validation, and installer handoff through Windows/UAC.
- Persisted update-check throttle/last-notified state without storing credentials or installer binaries.
- General Windows CI and a stable release-candidate workflow that builds installer, portable ZIP, checksums, and exact-commit evidence.
- Repo-local project contract aligned with the applicable v9 project rules.

### Changed

- Refined Main, Settings, DNS Health Failover, and Split DNS windows while preserving their existing DNS/NRPT/Agent contracts.
- Installer builds remain self-contained by default; final packaging now emits `SHA256SUMS.txt` for installer and portable assets.
- Release version metadata is prepared for `1.5.0`, with `Directory.Build.props` as the canonical source version for normal builds.
- Russian and English README/release documentation has been aligned with the final `v1.5.0` delivery model.

### Fixed

- Installer packaging no longer requires a separately installed .NET Desktop Runtime on target Windows machines.
- Release packaging uses the current bilingual README filenames and retains exact-commit candidate evidence.

## [1.4.1] - 2026-04-30

Release metadata and packaging version update for portable and installer artifacts.

### Changed

- Bumped assembly and informational version metadata to `1.4.1`.
- Updated portable release script defaults and release documentation paths to `v1.4.1`.
- Updated installer build defaults and output naming to `1.4.1`.
- Added `v1.4.1` candidate path for desktop sibling executable discovery.

## [1.4.0] - 2026-04-16

Public release preparation with portable and installer delivery tracks.

### Added

- Unified portable root resolver shared by CLI, UI, Tray, Agent, and service runtime copy.
- Root BAT scripts for portable agent install/reinstall/uninstall/start/stop/status with UAC elevation.
- Inno Setup installer track that reuses CLI service commands and grants write access to `data\`.
- Optional DNS health failover settings, state, CLI commands, UI/Tray status, and agent background monitor.
- Optional Split DNS rules with Windows NRPT apply/reset through Agent or admin fallback.
- CLI commands for `health` and `split-dns`.
- Desktop UI windows for Agent management, DNS Health Failover settings, and Split DNS rule editing.
- Tray Agent submenu for status/start/stop/reinstall.
- Release documentation for architecture audit, portable release, service install, installer release, health failover, Split DNS, and Store readiness.
- Tests for health threshold/cooldown/notify-only behavior and Split DNS rule validation/matching.

### Fixed

- Release packages inside the repository resolve their own package root instead of the solution root.
- First-run config creation uses unique temporary files to avoid UI/Tray/CLI startup races.
- Service status reports the registered service executable path and detects stale service paths.
- Installer build script passes named parameters correctly to portable packaging.

### Notes

- DNS Health Failover is disabled by default.
- Split DNS is Windows NRPT-based and can be bypassed by applications using their own DNS/DoH stack.

## [1.3.0] - 2026-04-12

### Added

- UI profile create/edit/delete/import/export.
- Editable per-profile DNS test domains and site test URLs.
- DNS benchmark with best-profile selection, history, and original-DNS restoration.
- UI language/theme/autostart/continue-in-tray settings.
- Russian and English UI plus System/Light/Dark theme support.
- Tray theme refresh and persistent tray preferences.

### Improved

- UI action layout and tray diagnostics organization.
- Autostart targets Tray rather than Desktop UI.
- Shared profile editing/import/export logic and tests.

## [1.0.0] - 2026-04-09

### Added

- Shared domain core for DNS profiles, validation, adapter selection, status matching, and diagnostics.
- Windows infrastructure for config storage, DNS management, logging, and connectivity probing.
- CLI, Desktop UI, Tray, and privileged Windows Agent/Named Pipes.
- Portable config/log layout and friendly error formatting.
- Unit test coverage for core configuration, validation, selection, matching, and diagnostics.
