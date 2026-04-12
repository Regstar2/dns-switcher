# Config Layout

DnsSwitcher is portable. Runtime data is stored next to the executable, not in `%APPDATA%` or `%LOCALAPPDATA%`.

Default layout:

```text
<app-base-directory>/
  data/
    config/
      app-preferences.json
      dns-benchmark-history.json
      profiles.json
      tray-settings.json
      ui-settings.json
    logs/
      dns-switcher.log
```

In development, clients started from this repository share the solution-level `data/config/profiles.json` path when available.
