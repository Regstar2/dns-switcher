# GitHub About

## Short description

Portable Windows DNS switcher with CLI, desktop UI, tray client, and built-in DNS/site diagnostics.

## Extended description

DnsSwitcher is a Windows-first DNS profile manager built around a shared core with three user-facing clients: CLI, desktop UI, and tray.  
It supports fast DNS profile switching, DHCP reset, current DNS status detection, DNS diagnostics, site accessibility checks, and a privileged Windows agent for DNS changes without requiring elevation on every tray/UI action.

## Suggested topics

- windows
- dotnet
- csharp
- dns
- networking
- wpf
- winforms
- tray
- windows-service
- named-pipes
- cli
- diagnostics

## Notes

- `gh auth status` currently reports an invalid GitHub token for the active account, so the repository About section was not updated automatically.
- After re-authentication, this description can be applied with:

```powershell
gh auth login -h github.com
gh repo edit Regstar2/DnsSwitcher --description "Portable Windows DNS switcher with CLI, desktop UI, tray client, and built-in DNS/site diagnostics."
```
