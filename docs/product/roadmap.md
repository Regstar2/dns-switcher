# Дорожная карта

Roadmap фиксирует только подтверждённую текущую цель; следующие версии не придумываются заранее.

## v1.5.0 — Update Delivery / stable release

Статус: подготовка финального release candidate.

### Цель

Завершить уже реализованную пользовательскую базу DnsSwitcher и обеспечить нормальный stable delivery для устанавливаемого Windows-приложения.

### Scope

- настраиваемое tray menu;
- финальные UI improvements Main/Settings/DNS Health/Split DNS;
- About и Help;
- manual update check;
- отключаемая automatic update check из Tray;
- SemVer stable-channel selection;
- installer download только после SHA-256 verification;
- self-contained installer + portable ZIP + `SHA256SUMS.txt`;
- RU/EN README и release notes;
- Windows CI/release-candidate evidence.

### Критерии готовности

- build/tests проходят на exact release commit;
- production update source читается приложением анонимно по HTTPS без embedded secret;
- real Windows screenshots добавлены;
- About/Update Windows smoke пройден;
- installer/portable/checksums связаны с exact commit;
- stable tag/release создаются только после отдельного подтверждения.

### Текущие blockers

- основной GitHub repository private, поэтому production GitHub Releases недоступны anonymous client;

До закрытия этого пункта `v1.5.0` не считается готовым к публикации, даже если код, screenshots и candidate artifacts собраны успешно.
