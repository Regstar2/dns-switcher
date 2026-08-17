# Документация DnsSwitcher

Публичная документация проекта разделена по назначению:

- [`product/`](product/README.md) — идея, ретроспективные границы MVP, feasibility и подтверждённый roadmap;
- [`architecture/`](architecture/README.md) — фактическая архитектура, project dependencies и технологический стек;
- [`versions/`](versions/README.md) — подтверждённая история версий и legacy-ограничения;
- [`testing/`](testing/README.md) — automated checks и ручной Windows test plan;
- [`releases/`](releases/README.md) — двуязычные release notes;
- [`profiles.example.json`](profiles.example.json) — пример конфигурации DNS-профилей;
- [`profiles.schema.json`](profiles.schema.json) — JSON schema профилей;
- [`ipc-integration-tests.md`](ipc-integration-tests.md) — интеграционные проверки Named Pipes.

Основные документы:

| Область | Документ |
|---|---|
| Идея | [`product/idea.md`](product/idea.md) |
| Feasibility | [`product/feasibility.md`](product/feasibility.md) |
| MVP scope | [`product/mvp-scope.md`](product/mvp-scope.md) |
| Roadmap | [`product/roadmap.md`](product/roadmap.md) |
| Architecture | [`architecture/architecture.md`](architecture/architecture.md) |
| Tech stack | [`architecture/tech-stack.md`](architecture/tech-stack.md) |
| Versions | [`versions/versions-index.md`](versions/versions-index.md) |
| Manual tests | [`testing/manual-test-plan.md`](testing/manual-test-plan.md) |

Документы по установке и поставке остаются в корне репозитория, чтобы не ломать существующие публичные ссылки. Это намеренное compatibility-решение, а не предложение размещать новые документы в корне.
