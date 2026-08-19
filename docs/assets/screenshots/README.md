# Final UI screenshots

`v1.5.0` использует реальные screenshots запущенного Windows-приложения. Синтетические изображения, Figma mockups и снимки старой версии сюда не добавляются.

Текущий набор:

```text
main.png
tray.png
settings.png
settings-tray.png
settings-updates.png
dns-health.png
split-dns.png
agent-manager.png
```

Перед commit проверить отсутствие private DNS/IP, username, machine name, внутренних доменов и других локальных данных. Эти файлы закрывают screenshots gate, но не заменяют Windows smoke, installer, upgrade или uninstall validation.
