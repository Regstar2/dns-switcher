# Final UI screenshots

`v1.5.0` требует реальные screenshots запущенного Windows-приложения. Синтетические изображения, Figma mockups и снимки старой версии сюда не добавляются.

Ожидаемый набор после Windows capture:

```text
main.png
tray.png
settings.png
dns-health.png
split-dns.png
```

Перед commit проверить отсутствие private DNS/IP, username, machine name, внутренних доменов и других локальных данных. До появления этих файлов release gate: `SCREENSHOTS REQUIRED — awaiting real Windows capture`.
