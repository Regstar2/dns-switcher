# План ручного тестирования

Этот план описывает проверки, которые требуют Windows runtime, реальных сетевых настроек, Windows Service, NRPT или installer. Он не утверждает, что сценарии выполнены.

## Обозначения

- `Не выполнено` — сценарий описан, но результат текущей проверки не зафиксирован.
- `Пройдено` / `Не пройдено` можно ставить только вместе с датой, Windows environment и наблюдаемым результатом.
- Перед тестами, изменяющими DNS или NRPT, сохраните исходные сетевые настройки.

## Общие предусловия

- Windows test machine или VM;
- подходящая x64 environment для опубликованного `v1.4.1`, если проверяются release assets;
- административные права для Agent, прямого DNS и NRPT;
- резервная копия `data/config/`;
- известный рабочий DNS для восстановления;
- при installer validation — Inno Setup нужен только на build machine, не на target machine.

## Матрица

| ID | Сценарий | Статус |
|---|---|---|
| MT-01 | Запуск WPF UI | Не выполнено |
| MT-02 | Запуск CLI и `help` | Не выполнено |
| MT-03 | Tray startup и menu | Не выполнено |
| MT-04 | Чтение и редактирование профилей | Не выполнено |
| MT-05 | Применение DNS-профиля | Не выполнено |
| MT-06 | `reset` на автоматический DNS | Не выполнено |
| MT-07 | Текущий DNS / `status` | Не выполнено |
| MT-08 | DNS и site diagnostics | Не выполнено |
| MT-09 | Windows Agent lifecycle | Не выполнено |
| MT-10 | Named Pipe IPC | Не выполнено |
| MT-11 | Split DNS / NRPT | Не выполнено |
| MT-12 | DNS Health Failover | Не выполнено |
| MT-13 | Portable package | Не выполнено |
| MT-14 | Installer package | Не выполнено |

## MT-01 — WPF UI

1. Запустить `DnsSwitcher.Ui.exe` или `dotnet run --project src/DnsSwitcher.Ui -c Release`.
2. Проверить открытие главного окна.
3. Проверить загрузку профилей и списка адаптеров.
4. Открыть Settings и переключить язык/theme.
5. Перезапустить приложение и проверить сохранение настроек.

Ожидаемо: приложение запускается без unhandled exception, отображает доступные данные и сохраняет поддерживаемые настройки.

## MT-02 — CLI

1. Запустить `DnsSwitcher.Cli.exe help` или source-equivalent команду.
2. Выполнить `profiles`, `adapters`, `status`.
3. Проверить обработку заведомо неверного аргумента.

Ожидаемо: справка и read-only команды завершаются предсказуемо; неверный ввод возвращает понятную ошибку.

## MT-03 — Tray

1. Запустить Tray.
2. Проверить появление иконки и меню.
3. Открыть основные read-only actions.
4. Проверить закрытие/перезапуск и реакцию на theme preference.

Ожидаемо: tray не создаёт зависшее или дублирующее состояние и может открыть поддерживаемые actions.

## MT-04 — Профили

1. Создать тестовый профиль с валидными DNS addresses.
2. Отредактировать его.
3. Export и повторный import.
4. Попробовать невалидный адрес или некорректный JSON.
5. Удалить тестовый профиль.

Ожидаемо: валидные данные сохраняются; невалидные отклоняются до системного изменения.

## MT-05 — Применение DNS

1. Зафиксировать исходный DNS выбранного тестового адаптера.
2. Применить тестовый статический профиль.
3. Проверить Windows adapter settings и `status`.
4. Выполнить DNS query к тестовому домену.

Ожидаемо: выбранный адаптер получает ожидаемые DNS servers, а status соответствует фактической настройке.

## MT-06 — Reset

1. После MT-05 выполнить `reset` через тот же client path.
2. Проверить Windows adapter settings.
3. Обновить network lease/connection при необходимости среды.

Ожидаемо: DNS возвращается в автоматический режим; тестовая статическая настройка не остаётся активной.

## MT-07 — Status

1. Проверить `status` при автоматическом DNS.
2. Применить известный профиль и проверить `status` снова.
3. Изменить DNS вне приложения и повторить проверку.

Ожидаемо: приложение не сообщает совпадение с профилем, если фактические DNS values отличаются.

## MT-08 — Diagnostics

1. Запустить DNS test для рабочего домена.
2. Запустить site test для доступного HTTPS URL.
3. Запустить benchmark на нескольких тестовых профилях.
4. Проверить восстановление исходного DNS после benchmark.

Ожидаемо: результаты отражают этапы проверки, ошибки не скрываются, исходный DNS восстанавливается после benchmark.

## MT-09 — Windows Agent

1. Из elevated context выполнить install/reinstall Agent.
2. Проверить `service status`, затем start/stop.
3. Перезапустить Windows при необходимости отдельного lifecycle test.
4. Выполнить uninstall.
5. Проверить отсутствие stale service path.

Ожидаемо: service commands приводят службу к заявленному состоянию и не оставляют старый runtime path.

## MT-10 — Named Pipe IPC

1. Запустить Agent.
2. Выполнить операцию через client path, который использует Agent.
3. Остановить Agent и повторить операцию для проверки error/fallback path.
4. Отдельно выполнить automated `DnsSwitcher.IntegrationTests` на Windows.

Ожидаемо: requests/responses соответствуют contracts; недоступный Agent не приводит к зависанию.

## MT-11 — Split DNS

1. Сохранить текущее NRPT состояние тестовой машины.
2. Создать тестовое правило для контролируемого домена.
3. Выполнить `split-dns test`, затем apply.
4. Проверить NRPT через Windows tools.
5. Выполнить reset и убедиться, что тестовое правило удалено.
6. Восстановить исходное NRPT состояние при необходимости.

Ожидаемо: применяются только ожидаемые правила; reset не оставляет тестовые записи.

## MT-12 — DNS Health Failover

1. Убедиться, что feature можно оставить disabled.
2. Настроить тестовую failover chain.
3. Смоделировать последовательность успешных и неуспешных checks.
4. Проверить threshold/cooldown/notify-only behavior.
5. Проверить восстановление ожидаемого профиля после теста.

Ожидаемо: failover происходит только по configured rules; disabled mode не меняет DNS.

## MT-13 — Portable

1. Собрать package через `scripts/publish-release.ps1` или взять опубликованный ZIP.
2. Распаковать в новый каталог.
3. Проверить запуск UI, Tray и CLI.
4. Проверить создание/использование `data/` внутри package root.
5. Проверить Agent helper scripts только на disposable test environment.

Ожидаемо: package не зависит от repository layout и хранит runtime data в собственном root.

## MT-14 — Installer

1. Собрать installer через `installer/build-installer.ps1` или использовать отдельно выбранный published asset.
2. Установить на чистую Windows VM.
3. Проверить shortcuts, UI, Tray, CLI и Agent.
4. Проверить write access к `data/`.
5. Выполнить upgrade test только между явно выбранными версиями.
6. Выполнить uninstall и проверить удаление Agent.

Ожидаемо: installer корректно устанавливает и удаляет приложение и service runtime. Для current `main` отдельно проверить self-contained запуск без предустановленного .NET Desktop Runtime.

## Автоматические проверки рядом с ручным планом

Перед release candidate на Windows:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
dotnet test DnsSwitcher.sln -c Release
```

Автоматический test pass не заменяет MT-05, MT-06, MT-09, MT-11, MT-13 и MT-14.
