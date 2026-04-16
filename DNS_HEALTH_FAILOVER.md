# DNS Health Failover

## Status

Implemented as an optional feature. Disabled by default.

## Files

Settings:

```text
data\config\dns-health-settings.json
```

State:

```text
data\config\dns-health-state.json
```

## Settings model

```json
{
  "enabled": false,
  "monitorIntervalSeconds": 60,
  "failureThreshold": 3,
  "recoveryThreshold": 2,
  "cooldownSeconds": 300,
  "checkMode": "resolveOnly",
  "actionOnFailure": "notifyOnly",
  "fallbackProfileId": null,
  "failoverChain": [],
  "testDomains": ["cloudflare.com", "github.com", "openai.com"],
  "expectedAddresses": {}
}
```

Actions:
- `notifyOnly`
- `switchToNextProfile`
- `switchToFallbackProfile`

Check modes:
- `resolveOnly`
- `resolveWithExpectedIp`

## How it works

The agent runs `DnsHealthMonitorWorker`.

On every tick:
1. Load settings.
2. If disabled, do nothing except persist disabled state.
3. Run DNS resolve tests using `DnsTester`.
4. Increase failure count only on failed checks or expected-IP mismatch.
5. Switch only after `failureThreshold`.
6. Prevent flapping with `cooldownSeconds`.
7. Require `recoveryThreshold` successful checks before returning to healthy state.

The app does not rely on Windows to switch DNS automatically. Failover is explicit and logged.

## CLI

```powershell
dns-switcher health status
dns-switcher health enable
dns-switcher health disable
dns-switcher health check
dns-switcher health action notify-only
dns-switcher health action next
dns-switcher health action fallback
dns-switcher health fallback google
dns-switcher health fallback none
dns-switcher health chain list
dns-switcher health chain set cloudflare google
dns-switcher health chain clear
dns-switcher health domains list
dns-switcher health domains set github.com openai.com
```

## UI / Tray

UI:
- shows health monitor status in Current Status
- can run Health Check
- can enable/disable health monitor
- has a dedicated Health Failover settings window
- edits monitor interval, failure threshold, recovery threshold and cooldown
- edits action mode: notify only, switch to next profile, switch to fallback profile
- edits fallback profile and ordered failover chain
- edits test domains and optional expected IP records
- shows current state, last successful check, last failure reason and last action

Tray:
- can run Health Check
- can enable/disable health monitor
- shows detailed dialog when notifications are disabled or action is important

## Limitations

- The feature is disabled by default.
- Automatic return to the primary profile is not implemented as a separate policy yet.
- Expected-IP checks are supported by the model and DNS query parser, but should be used carefully because public DNS answers can legitimately vary by geography/CDN.
- Current expected-IP probing prefers A records and falls back to AAAA only when A returns no answers.
- Tray intentionally exposes only quick enable/disable/check actions. Full tuning is in the desktop UI.
