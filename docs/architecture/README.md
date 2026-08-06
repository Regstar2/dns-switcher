# Архитектура

DnsSwitcher использует слоистую структуру с общим domain-ядром и Windows-специфичной инфраструктурой.

```text
DnsSwitcher.Core
  Модели, валидация, сервисы и оркестрация диагностических сценариев

DnsSwitcher.Infrastructure.Windows
  Адаптеры, системный DNS, конфигурация, логирование и IPC-клиент

DnsSwitcher.Contracts
  Контракты запросов и ответов агента

DnsSwitcher.Agent.Windows
  Привилегированная Windows-служба и Named Pipe server

DnsSwitcher.Cli / DnsSwitcher.Ui / DnsSwitcher.Tray
  Пользовательские клиенты поверх общего ядра
```

Основное правило зависимостей: пользовательские клиенты не должны дублировать domain-логику, а Windows-специфичные операции не должны проникать в `DnsSwitcher.Core`.

Значимые изменения архитектуры фиксируются в [`decisions/`](decisions/README.md).
