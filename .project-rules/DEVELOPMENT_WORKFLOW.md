# Development workflow

## Цикл изменения

1. Прочитать `AGENTS.md` и применимые project rules/docs.
2. Зафиксировать base SHA и clean scope.
3. Проверить официальную документацию для version-sensitive API.
4. Найти существующий слой реализации и тесты.
5. Внести минимальное изменение без unrelated refactoring.
6. Проверить diff и локализацию.
7. Выполнить доступные build/tests/static checks.
8. Для Windows-only поведения использовать воспроизводимый manual plan.
9. Обновить changelog/docs только фактами.
10. Сделать осмысленный commit и PR.

## Diff review

Проверить отсутствие:

- случайных удалений;
- hardcoded новых UI-строк;
- новых secrets;
- изменения DNS/NRPT/Agent логики вне scope;
- массового форматирования;
- необоснованных dependencies;
- фиктивных результатов тестирования.

## Минимальные проверки

```powershell
dotnet restore DnsSwitcher.sln
dotnet build DnsSwitcher.sln -c Release --no-restore
dotnet test DnsSwitcher.sln -c Release --no-build
```

Integration tests, installer packaging и Windows smoke выполняются по существующей release/test инфраструктуре.

Если обязательная сборка не проходит, версия не считается завершённой.
