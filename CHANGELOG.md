# Changelog

## [1.3.0] - 2026-04-12

Release candidate for the current desktop/tray workflow.

### Added

- UI profile management: create, edit, delete, import, and export DNS profiles
- Editable per-profile DNS test domains and site test URLs
- DNS benchmark flow across switchable profiles with latency comparison, best-profile selection, history storage, and restore of original DNS settings
- UI settings for language, theme, autostart, and continue-in-tray behavior
- Automatic system language detection with Russian and English UI resources
- Automatic system theme detection with manual light/dark/system selection
- Themed window icons, tray icons, and dark title bars across UI dialogs
- Tray reaction to theme preference changes without requiring a manual tray click
- Release metadata for assemblies

### Improved

- UI action layout for profile operations, DNS operations, diagnostics, and tools
- Tray diagnostics menu organization and persistent tray preferences
- Autostart now targets the tray client instead of the desktop UI
- Shared profile editing/import/export logic and tests
- Additional tests for UI layout, profile service operations, settings storage, autostart, and tray formatting

### Notes

- This release is still Windows-only.
- Screenshots are intentionally not included yet.
- Private DNS profiles must remain in ignored local config files.

## [1.0.0] - 2026-04-09

First portfolio-ready release.

### Added

- Shared domain core for DNS profiles, validation, adapter selection, status matching, and diagnostics
- Windows infrastructure layer for config storage, DNS management, logging, and connectivity probing
- CLI client with command mode and interactive console mode
- Desktop UI client for regular usage
- Tray client for quick switching
- Privileged Windows agent/service with Named Pipes
- DNS diagnostics by domain
- Site accessibility diagnostics by URL
- Portable config and log layout
- File logging across CLI, UI, tray, and agent
- Friendly error formatting for common operational failures
- Unit test coverage for config handling, validation, selection logic, matching, and diagnostics

### Improved

- Shared host/bootstrap helpers across clients
- Shared diagnostic text formatting across CLI, UI, and tray
- More resilient startup and unhandled exception handling in UI and tray
- Reduced duplication across clients

### Notes

- Screenshots are intentionally not included in `1.0.0`
- UI-based profile editing remains out of scope for this release
- Full localization and dark theme remain planned future improvements
