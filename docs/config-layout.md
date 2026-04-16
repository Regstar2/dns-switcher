# Config Layout

DnsSwitcher is portable. Runtime data is stored under the shared portable root, not in `%APPDATA%` or `%LOCALAPPDATA%`.

Default layout:

```text
<portable-root>/
  cli/
  ui/
  tray/
  agent/
  service/
    agent/
  data/
    config/
      app-preferences.json
      dns-benchmark-history.json
      dns-health-settings.json
      dns-health-state.json
      profiles.json
      split-dns-rules.json
      tray-settings.json
      ui-settings.json
    logs/
      dns-switcher.log
```

In development, clients started from this repository share the solution-level `data/config/` path when available.

In release packages, `cli\`, `ui\`, `tray\`, `agent\`, and `service\agent\` all resolve the same `<portable-root>\data\` directory.
