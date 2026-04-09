# Changelog

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
