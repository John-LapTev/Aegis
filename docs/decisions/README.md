# Architecture Decision Records (ADR)

Значимые технические решения: контекст, варианты, выбор, последствия.

- Новый ADR: из [`0000-template.md`](0000-template.md), имя `NNNN-kebab-case.md`.
- Статусы: `Proposed` → `Accepted` / `Rejected` / `Superseded by NNNN`.
- При изменении архитектуры — новый ADR + обновить [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

## Список
- [0000](0000-template.md) — Шаблон (не решение).
- [0001](0001-stack.md) — Стек: C#/.NET 9 + WPF (self-contained .exe). `Accepted` (ПЛАН).
- [0002](0002-backup-rollback.md) — Бэкап и откат изменений (System Restore + экспорт веток). `Accepted` (ПЛАН).
- [0003](0003-threat-scanning.md) — Поиск угроз (эвристика + VirusTotal, без своего AV-движка). `Accepted` (ПЛАН).
- [0004](0004-elevation-and-safety.md) — Права администратора и безопасность изменений (обратимость). `Accepted` (ПЛАН).
- [0005](0005-drivers-and-gpu-tuning.md) — Драйверы и тюнинг GPU: применение по кнопке + объяснение + обратимость. `Accepted` (ПЛАН).
- [0006](0006-ui-avalonia.md) — UI на **Avalonia** вместо WPF (сборка `.exe` на Linux). `Accepted`. Заменяет UI-часть 0001.
