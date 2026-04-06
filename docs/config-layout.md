# Config Layout

DnsSwitcher is portable. Runtime data is stored next to the executable, not in `%APPDATA%` or `%LOCALAPPDATA%`.

Default layout:

```text
<app-base-directory>/
  data/
    config/
      profiles.json
    logs/
      dns-switcher.log
```

In development, `<app-base-directory>` is the built project's output directory, for example `src/DnsSwitcher.Cli/bin/Debug/net10.0/`.
