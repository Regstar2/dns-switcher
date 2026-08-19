# AI text guardrails

Документация и пользовательский текст должны быть фактическими и проверяемыми.

Запрещено:

- рекламные сверхутверждения (`best`, `perfect`, `production-ready`) без доказательства;
- фиктивные даты, hashes, test counts, screenshots, supported platforms или release assets;
- обещание функции, которой нет в текущем code path;
- скрытие известного blocker за расплывчатой формулировкой;
- изменение исторических release facts задним числом.

Если проверка не выполнена, писать `не выполнено` / `not run`. Если источник update недоступен из production client, писать `BLOCKED`, а не `works in theory`.

README и release notes должны объяснять пользовательское поведение кратко; технические детали выносить в docs.
