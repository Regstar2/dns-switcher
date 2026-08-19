# Release review checklist

- [ ] Version metadata синхронизирована и не переписывает исторические версии.
- [ ] CHANGELOG содержит конкретные факты.
- [ ] RU/EN release notes синхронны.
- [ ] Build/tests имеют фактический результат.
- [ ] Installer и portable построены из exact release commit.
- [ ] `SHA256SUMS.txt` соответствует финальным assets.
- [ ] Localization gate (`ru`, `en`) пройден.
- [ ] Update delivery gate пройден или честно заблокирован.
- [ ] Upgrade path/данные проверены либо ограничение явно зафиксировано.
- [ ] Screenshots реальные и актуальные либо release gate отмечен BLOCKED.
- [ ] Нет secrets/private data/debug artifacts.
- [ ] GitHub Release body ссылается на tag.
