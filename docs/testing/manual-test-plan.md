# Ручная проверка DnsSwitcher v1.5.0

Статус: **Пройдено**.

Финальная Windows-проверка подтверждена владельцем проекта 2026-08-19. Среда: Windows 11 x64 VM с административными правами для DNS/NRPT/Agent сценариев.

## Матрица

| ID | Сценарий | Статус |
|---|---|---|
| MT-01 | Запуск WPF UI | Пройдено |
| MT-02 | Запуск CLI и `help` | Пройдено |
| MT-03 | Tray startup и menu | Пройдено |
| MT-04 | Чтение и редактирование профилей | Пройдено |
| MT-05 | Применение DNS-профиля | Пройдено |
| MT-06 | `reset` на автоматический DNS | Пройдено |
| MT-07 | Текущий DNS / `status` | Пройдено |
| MT-08 | DNS и site diagnostics | Пройдено |
| MT-09 | Windows Agent lifecycle | Пройдено |
| MT-10 | Named Pipe IPC | Пройдено |
| MT-11 | Split DNS / NRPT | Пройдено |
| MT-12 | DNS Health Failover | Пройдено |
| MT-13 | Portable package | Пройдено |
| MT-14 | Installer install / upgrade / uninstall | Пройдено |
| MT-15 | Настройка состава tray menu и live refresh | Пройдено |
| MT-16 | RU/EN и System/Light/Dark | Пройдено |
| MT-17 | 100% / 125% / 150% scaling и keyboard navigation | Пройдено |
| MT-18 | About / Help / More navigation | Пройдено |
| MT-19 | Update preferences и stable-channel filtering | Пройдено |
| MT-20 | Checksum verification / installer handoff | Пройдено |

## Проверенные инварианты

- DNS apply/reset соответствует фактическим Windows adapter settings.
- Split DNS изменяет только ожидаемые NRPT rules и корректно очищается.
- DNS Health не выполняет failover в disabled/notify-only сценариях и соблюдает threshold/cooldown/fallback rules.
- Agent service и Named Pipe path не зависают при недоступном Agent.
- Installer self-contained и не требует отдельного .NET Desktop Runtime.
- Upgrade `v1.4.1 → v1.5.0` сохраняет profiles, app/UI preferences, Tray settings, Health settings/state и Split DNS rules.
- Tray visibility влияет только на представление меню и не включает/отключает сетевые функции.
- RU/EN и темы System/Light/Dark работают без release-blocking layout defects.

## Автоматические проверки

Release verification использует:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

Финальный publish workflow повторяет эти команды перед созданием release assets.

## Связанные документы

- [`v1.5.0-final-smoke.md`](v1.5.0-final-smoke.md) — финальный UI/update/upgrade smoke.
- [`v1.5.0-beta.1-runtime-plan.md`](v1.5.0-beta.1-runtime-plan.md) — исторический подробный runtime plan beta-этапа; его исходный статус не переписывается задним числом.

## Итог

Ручной Windows gate `v1.5.0` закрыт. Release-blocking функциональных дефектов по выполненной матрице не зафиксировано.
