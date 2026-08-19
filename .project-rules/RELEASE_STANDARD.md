# Release standard

## Release notes

Для каждого опубликованного release:

- `docs/releases/vX.Y.Z.md` — русский;
- `docs/releases/vX.Y.Z_EN.md` — English.

RU header:

```markdown
**Русский** · [English](vX.Y.Z_EN.md)
```

EN header:

```markdown
[Русский](vX.Y.Z.md) · **English**
```

Полные notes содержат highlights, changes, updating, validation, known limitations, assets и SHA-256. Не дублировать README или commit history.

## Assets

Stable Windows x64 release DnsSwitcher использует:

```text
DnsSwitcher-<version>-win-x64-setup.exe
DnsSwitcher-<version>-win-x64.zip
SHA256SUMS.txt
```

Assets должны быть построены из одного exact commit. Hashes публикуются только из фактических файлов финальной сборки.

## GitHub Release

GitHub Release body краткий и содержит ссылки на RU/EN notes по immutable tag, а не `main`.

Tag/release не создаются до прохождения release review и явного подтверждения владельца.
