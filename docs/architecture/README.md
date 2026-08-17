# Архитектура

Основные документы:

- [`architecture.md`](architecture.md) — фактические компоненты, зависимости, data flow, IPC и ограничения;
- [`tech-stack.md`](tech-stack.md) — SDK, frameworks, packages, installer toolchain и официальные технические источники;
- [`decisions/`](decisions/README.md) — место для будущих значимых architecture decisions.

Кратко, solution разделён на:

- `DnsSwitcher.Core` — модели, валидация и общие сервисы;
- `DnsSwitcher.Contracts` — IPC contracts;
- `DnsSwitcher.Infrastructure.Windows` — Windows-specific adapters, DNS, storage, NRPT и Agent client;
- `DnsSwitcher.Cli`, `DnsSwitcher.Ui`, `DnsSwitcher.Tray` — пользовательские клиенты;
- `DnsSwitcher.Agent.Windows` — привилегированная Windows-служба;
- unit и IPC integration test projects.

Исторические architecture decisions не создаются задним числом без подтверждённых источников.
