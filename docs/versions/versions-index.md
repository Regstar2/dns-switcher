# Индекс версий

## Текущее состояние

- source metadata в release branch: `1.5.0`;
- target stable version: `v1.5.0`;
- последний исторически опубликованный Release до финальной публикации: `v1.4.1`;
- `v1.5.0` tag/release не создаётся до закрытия финальных release gates.

## Версии

| Версия | Состояние | Release notes |
|---|---|---|
| `v1.5.0` | candidate, не опубликован | [`RU`](../releases/v1.5.0.md) · [`EN`](../releases/v1.5.0_EN.md) |
| `v1.4.1` | опубликован 2026-04-30 | [`RU`](../releases/v1.4.1.md) · [`EN`](../releases/v1.4.1_EN.md) |
| `v1.4.0` | changelog/tag history | [`CHANGELOG`](../../CHANGELOG.md) |
| `v1.3.0` | changelog/tag history | [`CHANGELOG`](../../CHANGELOG.md) |
| `1.0.0` | changelog history | [`CHANGELOG`](../../CHANGELOG.md) |

Исторические tags/releases не переписываются. Полные notes `v1.5.0` находятся в release commit до будущего tag, чтобы GitHub Release мог ссылаться на immutable tag-relative документы.

## v1.5.0 gates

- Build/tests: должен дать фактический Windows CI result на final candidate commit.
- Installer/portable/checksums: должны быть собраны одним workflow из того же commit.
- Update source: `BLOCKED`, пока release source private/не читается anonymous client.
- Screenshots: реальные Windows screenshots добавлены в `docs/assets/screenshots/`.
- Stable tag/release: только после отдельного подтверждения владельца.
