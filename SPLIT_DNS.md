# Split DNS

## Status

Implemented as a Windows NRPT-based MVP.

DnsSwitcher does not pretend to implement Split DNS with a UI-only flag. It applies real Windows Name Resolution Policy Table rules through `DnsClient` PowerShell cmdlets.

## Files

```text
data\config\split-dns-rules.json
```

## Model

```json
{
  "enabled": false,
  "mode": "windowsNrpt",
  "defaultBehavior": "systemDns",
  "rules": [
    {
      "id": "example-com",
      "namespace": ".example.com",
      "profileId": "cloudflare",
      "enabled": true,
      "priority": 0,
      "comment": "Route example.com through Cloudflare"
    }
  ]
}
```

Namespace examples:
- `*.example.com`
- `.example.com`
- `internal.corp.local`

`*.example.com` is normalized to `.example.com`.

## Rule precedence

When testing a domain:
1. disabled rules are ignored
2. higher `priority` wins
3. longer namespace wins

Conflicting enabled rules for the same namespace with different profiles are rejected.

## CLI

```powershell
dns-switcher split-dns status
dns-switcher split-dns enable
dns-switcher split-dns disable
dns-switcher split-dns add *.example.com cloudflare
dns-switcher split-dns update example-com *.example.org google
dns-switcher split-dns remove example-com
dns-switcher split-dns enable-rule example-com
dns-switcher split-dns disable-rule example-com
dns-switcher split-dns test api.example.com
dns-switcher split-dns apply
dns-switcher split-dns reset
```

## UI / Tray

UI:
- shows Split DNS enabled/rule count in Current Status
- has a dedicated Split DNS rules editor
- can enable/disable Split DNS
- can add, edit, delete, enable and disable rules
- edits namespace, target DNS profile, priority and comment
- can test which rule matches a domain
- can apply/reset NRPT rules through the agent/admin path
- shows NRPT apply/reset status and Windows/DoH warnings

Tray:
- has Split DNS submenu
- can show status
- can apply/reset NRPT rules

Tray intentionally exposes only quick status/apply/reset actions. Full rule editing is in the desktop UI.

## Privileged application

Applying and resetting NRPT rules requires elevation.

Flow:
- UI/Tray/CLI ask the agent through the named pipe.
- Agent applies/removes NRPT rules as LocalSystem.
- If agent is unavailable and the client is elevated, direct admin fallback is used.

## Rollback

`split-dns reset` removes only rules whose display name/comment is owned by DnsSwitcher:

```text
DnsSwitcher Split DNS:*
DnsSwitcher managed rule*
```

Uninstall also removes the service. It does not automatically reset Split DNS rules unless the uninstall flow can still run the CLI before files are removed. Use `dns-switcher split-dns reset` before uninstall if you manually delete files.

## Windows limitations

NRPT is the native Windows-friendly option for per-namespace DNS routing.

Known limits:
- Browser-level Secure DNS / DoH can bypass OS DNS policy in some scenarios.
- Some applications may use custom resolvers and ignore Windows DNS APIs.
- NRPT is Windows-specific.
- Split DNS routing applies to namespaces, not arbitrary URL paths.
- Local DNS proxy mode is not implemented.

## Why not a local DNS proxy now

A local proxy would require:
- binding port 53
- changing system DNS to localhost
- robust process/service supervision
- careful rollback after crashes
- extra security review

NRPT is safer for MVP because it only adds/removes OS-supported namespace routing rules.
