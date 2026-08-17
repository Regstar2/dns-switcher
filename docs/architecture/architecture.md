# Архитектура

## Обзор

DnsSwitcher разделён на общее ядро, Windows infrastructure, IPC contracts, привилегированный Agent и три пользовательских клиента.

```text
UI / CLI / Tray ──> DnsSwitcher.Core
       │
       └──> DnsSwitcher.Infrastructure.Windows ──> DnsSwitcher.Core
                    │
                    ├──> DnsSwitcher.Contracts ──> DnsSwitcher.Core
                    │
                    └── Named Pipes ──> DnsSwitcher.Agent.Windows
                                            ├──> Core
                                            ├──> Contracts
                                            └──> Infrastructure.Windows
```

Диаграмма показывает направление основных project references и IPC-грань. `DnsSwitcher.Agent.Windows` дополнительно напрямую ссылается на Core, Contracts и Windows infrastructure.

## Проекты solution

| Проект | Назначение | Основные project references |
|---|---|---|
| `DnsSwitcher.Core` | модели, абстракции, валидация, DNS/profile/diagnostic orchestration | нет ссылок на другие проекты solution |
| `DnsSwitcher.Contracts` | request/response contracts и protocol constants Agent IPC | Core |
| `DnsSwitcher.Infrastructure.Windows` | Windows adapters, DNS, JSON storage, logging, NRPT, Agent client, desktop helpers | Core, Contracts |
| `DnsSwitcher.Cli` | CLI и interactive console | Core, Infrastructure.Windows |
| `DnsSwitcher.Ui` | WPF desktop UI | Core, Infrastructure.Windows |
| `DnsSwitcher.Tray` | Windows Forms tray client | Core, Infrastructure.Windows |
| `DnsSwitcher.Agent.Windows` | privileged Windows Service и Named Pipe server | Core, Contracts, Infrastructure.Windows |
| `DnsSwitcher.Tests` | unit tests | CLI, Core, Infrastructure.Windows |
| `DnsSwitcher.IntegrationTests` | Windows-specific Named Pipe integration tests | Contracts, Core, Infrastructure.Windows |

## Composition root

`WindowsDnsSwitcherHost` собирает основные runtime dependencies:

- JSON stores;
- adapter provider и DNS manager;
- profile, DNS test, site test и benchmark services;
- Named Pipe Agent client;
- Agent-aware DNS и Split DNS services;
- DNS Health Failover;
- NRPT Split DNS manager;
- Windows Agent service manager.

Это один из ключевых composition points пользовательских Windows-клиентов.

## Поток применения DNS

Высокоуровневый поток:

1. UI, Tray или CLI получает выбранный профиль и adapter context.
2. Общие правила и профильная логика проходят через Core services.
3. Windows infrastructure выполняет системную операцию.
4. Для привилегированных сценариев приложение может использовать Agent client по Named Pipes.
5. Agent выполняет разрешённую операцию в Windows service context и возвращает contract response.
6. Клиент обновляет status или показывает ошибку.

Точный fallback между Agent и прямым выполнением определяется runtime-кодом и правами процесса; документация не предполагает успех привилегированной операции без соответствующих прав.

## Конфигурация и состояние

Portable path resolver приводит клиентов к общему package root. Runtime data располагаются в `data/`:

```text
data/
  config/
  logs/
```

Основные конфигурации хранятся в JSON. Приватные runtime-файлы не должны попадать в Git.

## DNS diagnostics

Core координирует DNS и connectivity scenarios, а Windows infrastructure предоставляет:

- UDP DNS query client;
- HTTP/TCP/TLS site probing;
- Windows DNS manager;
- adapter provider.

Benchmark service использует общие DNS test и switching abstractions и хранит историю локально.

## DNS Health Failover

`DnsHealthFailoverService` находится в Core и работает с profile/DNS abstractions и stores состояния. Agent worker может выполнять фоновые проверки. Failover отключаемый и не является обязательным для базового switching scenario.

## Split DNS

Модели и правила находятся в Core. Windows implementation использует NRPT через `WindowsNrptSplitDnsManager`. Agent-aware слой позволяет выполнять привилегированные операции через Agent.

Приложения с собственным DNS или DoH stack могут обходить Windows NRPT, поэтому Split DNS не гарантирует перехват каждого DNS-запроса системы.

## IPC и привилегии

`DnsSwitcher.Contracts` отделяет wire contracts от реализации Agent. Named Pipe client находится в Windows infrastructure, server — в Agent project.

Установка/управление Agent и прямое изменение системного DNS относятся к привилегированным операциям. Это граница доверия и обязательная область ручной проверки.

## Тестовая архитектура

- `DnsSwitcher.Tests` проверяет Core и часть Windows infrastructure на уровне unit tests.
- `DnsSwitcher.IntegrationTests` содержит отдельные Named Pipe IPC tests и таргетирует `net10.0-windows`.
- системные DNS, Windows Service, NRPT, portable и installer сценарии дополнительно требуют ручной проверки.

План ручных сценариев: [`../testing/manual-test-plan.md`](../testing/manual-test-plan.md).

## Архитектурные ограничения

- проект Windows-specific на infrastructure и client boundary;
- GitHub Actions отсутствуют, поэтому автоматическая серверная проверка repository state не заявляется;
- фактические DNS/NRPT/service операции зависят от Windows environment и прав;
- historical architecture decisions до появления текущей документации не восстанавливаются искусственно.

Значимые будущие решения следует фиксировать в [`decisions/`](decisions/README.md), не создавая ADR задним числом без источников.
