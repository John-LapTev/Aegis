# Интеграция: Telegram-бот для проекта (ИЗОЛИРОВАННО)

Цель: у каждого проекта **свой** Telegram-бот, связанный с Claude Code — изолированно, **не общий**.

Механизм — официальный плагин **`telegram@claude-plugins-official`** + **Channels** (бот шлёт сообщения прямо в открытую сессию Claude Code, Claude отвечает в Telegram). Требует Bun и Claude Code v2.1.80+.
> Источник: code.claude.com/docs/en/channels.

## Как у пользователя устроена изоляция (по его проектам)
- Плагин включён **в настройках самого проекта**: `.claude/settings.json` → `"enabledPlugins": { "telegram@claude-plugins-official": true }` (видно в Media-Flow, platform, QR-Scan).
- **Отдельный канал-каталог на проект**: напр. в QR-Scan — `~/.claude/channels/telegram-qr-scan` (создаётся `mkdir -p` + `chmod 700`). Так у каждого проекта свой бот/токен, не общий.
- **Привязка к кастомному каталогу — через переменную окружения `TELEGRAM_STATE_DIR`** (НЕ через `settings.local.json` — там лишь permissions на `mkdir`/`chmod`/`Read` каталога). По умолчанию плагин пишет токен/состояние в `~/.claude/channels/telegram/`. Чтобы изолировать бота на проект, запуск делают launch-скриптом, который экспортит `TELEGRAM_BOT_TOKEN` + `TELEGRAM_STATE_DIR=~/.claude/channels/telegram-<project>`, затем `claude --channels …` (рабочий пример — `~/start-qr-claude.sh` у QR-Scan).

## Правильная настройка (как у efir/MF/consultant/qr/neuro)
1. **@BotFather** → `/newbot` → имя → username на `bot` → токен (свой на ЭТОТ проект). Токен → `<project>/.personal/credentials.md` (gitignored).
2. **State-каталог проекта:** `mkdir -p ~/.claude/channels/telegram-<project>` + `chmod 700`.
3. **Плагин — точечно в проекте:** в `<project>/.claude/settings.json` добавь `"enabledPlugins": { "telegram@claude-plugins-official": true }` (плагин уже установлен глобально — другие боты его используют; отдельный `/plugin install` обычно не нужен).
4. **Команда запуска в `~/.bashrc`** (как `efir`/`MF`/`neuro`) — экспортит токен и state-dir, запускает с `--channels`:
   ```bash
   <short>() {
     cd /home/john-laptev/AI/<Project> || return 1
     TELEGRAM_BOT_TOKEN=<токен> \
     TELEGRAM_STATE_DIR="$HOME/.claude/channels/telegram-<project>" \
     claude --effort xhigh --dangerously-skip-permissions --chrome --remote-control <project> --channels plugin:telegram@claude-plugins-official "$@"
   }
   ```
5. **БЕЗ кода пэйринга (обязательно так):** пере-сей `~/.claude/channels/telegram-<project>/access.json` (`chmod 600`) — по умолчанию в allowlist ТОЛЬКО личный id пользователя:
   ```json
   { "dmPolicy": "allowlist", "allowFrom": ["<личный id пользователя>"], "groups": {}, "pending": {} }
   ```
   Личный id пользователя есть в памяти ассистента (и в его существующих каналах). НЕ копируй allowlist из старых каналов вслепую — там может быть и чужой id. Другие id добавляй ТОЛЬКО по явному запросу пользователя (он пришлёт). Тогда бот узнаёт пользователя с ПЕРВОГО сообщения — `/telegram:access pair <код>` не нужен (пользователь не любит этот шаг).
6. `source ~/.bashrc` → запусти `<short>` → пиши боту в Telegram, сразу работает.

## Безопасность / изоляция
- Токен — личное: продублируй в `.personal/credentials.md`, не коммить.
- Канал работает только пока сессия открыта и запущена с `--channels`. Быть в `.mcp.json` недостаточно — нужен флаг `--channels`.
