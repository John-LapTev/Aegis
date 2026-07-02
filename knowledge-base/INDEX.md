# База знаний — индекс

Грузится по требованию. Сюда Project-Forge перенёс нужные доки при создании проекта, и пользователь добавляет своё.

## Claude Code
- [`claude-code/claude-md-and-memory.md`](claude-code/claude-md-and-memory.md) — CLAUDE.md и память.
- [`claude-code/settings.md`](claude-code/settings.md) — settings.json, permissions, хуки.
- [`claude-code/hooks.md`](claude-code/hooks.md) — хуки (SessionStart и др.).
- [`claude-code/subagents.md`](claude-code/subagents.md) — субагенты.
- [`claude-code/slash-commands-and-skills.md`](claude-code/slash-commands-and-skills.md) — слэш-команды и скиллы.

## Дизайн
- [`claude-design/claude-design.md`](claude-design/claude-design.md) — **Claude Design** (claude.ai/design): спроектировать дизайн на холсте → handoff в Claude Code.
- [`claude-design/frontend-design-plugin.md`](claude-design/frontend-design-plugin.md) — плагин **Frontend Design** (улучшение дизайна прямо при кодогенерации).
- Облик приложения — тёмный дашборд с градиентом: токены в [`../docs/DESIGN.md`](../docs/DESIGN.md), промпт для Claude Design в [`../docs/DESIGN_BRIEF.md`](../docs/DESIGN_BRIEF.md).
- ⚠️ Веб дизайн-система **Halo** (светлая, для веба) к Aegis **НЕ применяется** — это десктоп Avalonia; дизайн описан прямо под Avalonia/XAML в `docs/DESIGN.md`.

## Интеграции
- [`integrations/github-repo.md`](integrations/github-repo.md) — публикация репозитория на GitHub.
- [`integrations/telegram-channel.md`](integrations/telegram-channel.md) — изолированный Telegram-бот проекта.
- [`integrations/launch-command.md`](integrations/launch-command.md) — быстрая команда запуска (лаунчер `aegis`).

## Внешние темы (добавятся по ходу)
- Avalonia / MVVM (CommunityToolkit.Mvvm), DI, async.
- WMI (System.Management), реестр (Microsoft.Win32.Registry), управление службами/автозапуском, Process API, P/Invoke.
- Windows **System Restore** API (точки восстановления) и экспорт/импорт веток реестра.
- **VirusTotal API** (сверка хэшей при поиске угроз).
- GPU-тюнинг: **NVAPI** / nvidia-smi (NVIDIA), **ADLX/ADL** (AMD).

## Пользовательское
- _(добавляется по ходу)_

> Принцип: одно знание — один файл, с датой и источником; без дублей.
