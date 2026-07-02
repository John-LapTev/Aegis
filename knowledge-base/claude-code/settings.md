# Claude Code: settings.json

> Источник: code.claude.com/docs/en/settings · сверено 2026-05-30.

## Файлы и приоритет (выше → ниже)
1. Managed (организация) — `/etc/claude-code/managed-settings.json` (Linux).
2. CLI-аргументы.
3. **`.claude/settings.local.json`** — личное, per-project, **авто-в .gitignore**.
4. **`.claude/settings.json`** — командное, **в git**.
5. `~/.claude/settings.json` — личное, все проекты.

| Файл | В git | Кого касается |
|------|-------|---------------|
| `.claude/settings.json` | ✅ да | всю команду |
| `.claude/settings.local.json` | ❌ нет (авто) | только тебя, этот репо |
| `~/.claude/settings.json` | — | тебя, все проекты |

## Пример
```json
{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",
  "permissions": {
    "allow": ["Bash(npm run test:*)", "Read(~/.zshrc)"],
    "deny": ["Bash(curl:*)", "Read(./.env)", "Read(./secrets/**)"],
    "ask": ["Bash(git push:*)"],
    "defaultMode": "acceptEdits"
  },
  "env": { "FOO": "bar" },
  "hooks": { },
  "model": "claude-sonnet-4-6"
}
```

## permissions
- `allow` / `deny` / `ask` — массивы правил. Синтаксис: `Tool` или `Tool(specifier)`, напр. `Bash(npm run *)`, `Read(./.env)`, `WebFetch(domain:example.com)`, `Edit(./src/**)`.
- Порядок проверки: **deny → ask → allow**, побеждает первое совпадение.
- `defaultMode`: `default` | `acceptEdits` | `plan` | `auto`.
- `additionalDirectories` — доступ к доп. каталогам.

## Другие ключи
- `env` — переменные окружения для всех сессий/подпроцессов.
- `hooks` — команды на события жизненного цикла (см. hooks.md).
- `model`, `outputStyle`, `autoMemoryEnabled`, `cleanupPeriodDays`, `includeCoAuthoredBy`.
- `$schema` — даёт автодополнение/валидацию в редакторе.
