# Auto-update standard

DnsSwitcher — устанавливаемое Windows-приложение, поэтому update delivery является release requirement.

## Минимум

- установленная версия определяется из одного канонического assembly/package metadata;
- metadata новой версии читается из доверенного HTTPS release source;
- сравнение поддерживает SemVer и prerelease;
- stable channel не предлагает prerelease;
- manual check доступен пользователю;
- automatic check выполняется асинхронно, throttled и может быть отключён;
- network/API failure не ломает запуск;
- отсутствуют embedded PAT/OAuth/API secrets.

## Source

GitHub Releases допустим только при anonymous HTTPS access к production release metadata/assets. Для private source repository требуется отдельный public release channel; клиентский постоянный secret запрещён.

## Download/install

Если DnsSwitcher сам скачивает installer:

1. выбирается только `DnsSwitcher-<version>-win-x64-setup.exe`;
2. скачивается `SHA256SUMS.txt` из того же trusted release;
3. SHA-256 вычисляется локально и сравнивается с записью exact filename;
4. при mismatch файл не запускается;
5. installer запускается штатным Windows shell/UAC flow;
6. пользователь может отложить необязательное обновление.

URL/manifest не могут задавать произвольную команду запуска или недоверенный executable host.

## Channels

`stable` игнорирует draft/prerelease и prerelease SemVer. Тестовый channel вводится только отдельным продуктовым решением.

## Release gate

До stable release должен работать либо полный безопасный update flow, либо документированный safe fallback: automatic/manual version check и переход к официальному способу обновления.

Если production source недоступен анонимно, статус: `UPDATE RELEASE GATE = BLOCKED`.
