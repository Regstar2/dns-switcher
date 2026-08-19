# AGENTS.md

Обязательный контракт для AI-агентов, работающих с DnsSwitcher.

## 1. Проект

- Публичное название: `DnsSwitcher`
- Репозиторий: `dns-switcher`
- Назначение: Windows-утилита для переключения DNS-профилей, диагностики, DNS Health Failover и Split DNS.
- Стадия: `STABLE`
- Текущая целевая версия: `v1.5.0`
- Стек: `.NET 10 / C# / WPF / WinForms Tray / Windows Service / Inno Setup`
- Поддерживаемая платформа: `Windows x64`

## 2. Приоритет инструкций

1. Безопасность и достоверность.
2. Явная текущая задача.
3. Этот `AGENTS.md`.
4. Документ текущей версии/релиза.
5. Фактическая архитектура DnsSwitcher.
6. `.project-rules/*`.

При конфликте не выбирать молча удобный вариант: зафиксировать конфликт и продолжать только независимые безопасные изменения.

## 3. Непереопределяемые правила

Запрещено:

- выдумывать результаты сборки, тестов, VM-проверок, release assets или screenshots;
- публиковать токены, пароли, приватные URL или пользовательские данные;
- изменять DNS Core, Health Failover, Split DNS/NRPT или Agent protocol вне явного scope;
- удалять данные/историю без явного требования;
- внедрять клиентский секрет для чтения private GitHub Releases;
- заявлять stable release готовым при незакрытом release gate.

## 4. Обязательное чтение

Перед изменениями проверить:

- этот файл;
- `.project-rules/PROJECT_NAMING.md`;
- `.project-rules/DEVELOPMENT_WORKFLOW.md`;
- `.project-rules/ENGINEERING_PRINCIPLES.md`;
- `.project-rules/AI_TEXT_GUARDRAILS.md`;
- `.project-rules/README_STANDARD.md`;
- `.project-rules/README_REVIEW_CHECKLIST.md`;
- `.project-rules/RELEASE_STANDARD.md`;
- `.project-rules/RELEASE_REVIEW_CHECKLIST.md`;
- `.project-rules/AUTO_UPDATE_STANDARD.md`;
- `.project-rules/LOCALIZATION_STANDARD.md`;
- `README.md`, `README_EN.md`, `CHANGELOG.md`;
- `docs/product/*`, `docs/architecture/*`, `docs/versions/*`, относящиеся к задаче `docs/testing/*`.

Для version-sensitive API/SDK/build tool использовать официальную документацию фактической версии проекта.

## 5. Scope v1.5.0 finalization

Разрешено: About/Help, update delivery, update preferences/state, RU/EN localization, version metadata, release scripts/workflows, checksums, README/release notes и release-blocking fixes.

Не изменять без отдельной подтверждённой причины:

- DNS switching algorithms;
- Health Failover thresholds/actions;
- Split DNS matching/NRPT ownership;
- Agent IPC contract;
- profile/config formats.

## 6. Реализация

- сохранять фактическое разделение Core / Infrastructure.Windows / UI / Tray / CLI / Agent;
- не размещать HTTP/GitHub update logic в WPF code-behind;
- пользовательские строки — через существующую локализацию, обязательны `ru` и `en`;
- английский — fallback;
- комментарии и code-level docs — на английском;
- не добавлять dependency без необходимости;
- ошибки на границах update delivery представлять типизированно;
- загружаемый installer не запускать без проверки SHA-256.

## 7. Проверки

Минимум перед merge-ready:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

Windows-only/manual проверки фиксировать отдельно и не подменять автоматическими тестами.

## 8. Release gate

До публикации stable `v1.5.0` должны быть честно определены статусы:

- Update source;
- Screenshots;
- Build/tests;
- Installer/portable/checksums;
- Localization;
- Documentation;
- Windows smoke/upgrade path.

Tag и GitHub Release создаются только по отдельному подтверждению владельца после review финального PR.
