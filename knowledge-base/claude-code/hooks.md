# Claude Code: хуки

> Источник: code.claude.com/docs/en/hooks · сверено 2026-05-30.

Хуки — shell-команды на событиях жизненного цикла. В отличие от CLAUDE.md (контекст), хуки **выполняются гарантированно**.

## События (основные)
- `SessionStart` — старт/возобновление сессии.
- `UserPromptSubmit` — перед обработкой ввода пользователя.
- `PreToolUse` — перед вызовом инструмента (может **заблокировать**).
- `PostToolUse` — после успешного вызова.
- `Stop` — когда Claude закончил ответ.

## Конфигурация (в settings.json)
```json
{
  "hooks": {
    "SessionStart": [
      { "hooks": [ { "type": "command",
                     "command": "${CLAUDE_PROJECT_DIR}/.claude/hooks/x.sh",
                     "timeout": 10 } ] }
    ],
    "PreToolUse": [
      { "matcher": "Bash|Write", "hooks": [ { "type": "command", "command": "..." } ] }
    ]
  }
}
```
- `matcher`: `"*"`/пусто = все; `Edit|Write` = список; прочее = regex.
- `${CLAUDE_PROJECT_DIR}` — путь к проекту.

## Как вернуть контекст/решение Claude
- **Exit 0**: успех. stdout для `SessionStart`/`UserPromptSubmit` добавляется в контекст.
- **Exit 2**: блокирующая ошибка; stderr уходит Claude как сообщение об ошибке.
- **JSON на stdout** (exit 0): `{"hookSpecificOutput":{"hookEventName":"...","additionalContext":"..."}}` — вставляет system-reminder в контекст.

Пример SessionStart-хука — просто `cat` ориентировки и `exit 0` (см. наш `.claude/hooks/session-context.sh`).
