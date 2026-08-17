# Release Checklist

Этот checklist описывает текущий release process. Исторический tag `v1.4.1` не перемещается и не пересоздаётся.

## Перед началом

До выбора новой версии должны быть утверждены scope и критерии готовности. Текущий repository metadata version остаётся `1.4.1`, пока отдельная release-задача не требует изменения.

Проверьте working tree:

```powershell
git status --short
git diff --cached --name-only
```

Перед commit проверьте `git status`, staged diff и `.gitignore`. В commit не должны попадать ignored local files, secrets, runtime configuration, build output или generated artifacts.

## Build и tests

Команды, зафиксированные в репозитории:

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release
dotnet test DnsSwitcher.sln -c Release
```

Упавшая обязательная проверка не заменяется более простой проверкой без явного указания ограничения.

## Release documentation

До создания tag:

1. обновить `CHANGELOG.md`;
2. создать синхронные `docs/releases/vX.Y.Z.md` и `docs/releases/vX.Y.Z_EN.md`;
3. указать только реально выполненные проверки;
4. указать known issues/limitations;
5. проверить все ссылки;
6. убедиться, что release notes уже входят в release commit.

Для исторического `v1.4.1` это условие выполнить задним числом нельзя без переписывания tag; это зафиксированное legacy-исключение.

## Portable package

Текущий portable script framework-dependent по умолчанию:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.4.1 -Runtime win-x64
```

Для self-contained portable package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version 1.4.1 -Runtime win-x64 -SelfContained
```

`1.4.1` здесь показывает текущую metadata version и не является инструкцией повторно публиковать существующий release.

## Installer package

Current `main` принудительно передаёт self-contained publishing для installer build:

```powershell
.\installer\build-installer.ps1 -Version 1.4.1 -Runtime win-x64
```

Ожидаемый локальный output для этой версии:

```text
artifacts\installer\v1.4.1\DnsSwitcher-1.4.1-win-x64-setup.exe
```

Installer build требует Inno Setup 6.

## Manual validation

Используйте [`testing/manual-test-plan.md`](testing/manual-test-plan.md). Минимально для release candidate должны быть отдельно рассмотрены:

- UI / CLI / Tray startup;
- profiles read/write;
- DNS apply / reset / status;
- diagnostics;
- Agent lifecycle и IPC;
- Split DNS;
- DNS Health Failover;
- portable package;
- installer install/uninstall/upgrade, когда применимо.

Не отмечайте сценарий выполненным без фактического запуска.

## Tag и GitHub Release

Только после проверенного release commit:

```powershell
git tag vX.Y.Z
git push origin vX.Y.Z
```

`vX.Y.Z` в этой команде означает уже утверждённую release version; не используйте команду до её выбора.

GitHub Release должен:

- указывать на неизменяемый tag;
- содержать короткое summary;
- ссылаться на RU/EN notes из того же tag;
- перечислять только фактически загруженные assets;
- не дублировать полные notes HTML-мусором;
- не заменять опубликованные binaries без отдельного исправляющего release.

## Финальный контроль

- [ ] release commit содержит RU/EN notes;
- [ ] changelog обновлён;
- [ ] build выполнен;
- [ ] tests выполнены;
- [ ] manual checks зафиксированы;
- [ ] ignored/local runtime files отсутствуют;
- [ ] version согласована между metadata/scripts/docs;
- [ ] tag указывает на release commit;
- [ ] assets собраны из release commit;
- [ ] GitHub Release body короткий и tag-relative;
- [ ] checksums получены из финальных assets, если публикуются.
