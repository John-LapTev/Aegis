# Интеграция: быстрая команда запуска проекта в консоли

Когда каркас готов, **предложи** пользователю быструю команду, которая открывает Claude Code прямо из папки проекта — с неограниченными правами (без окон-разрешений). Это его устоявшийся паттерн (см. функции в `~/.bashrc`).

## Механизм (так уже сделано у пользователя)
Короткая bash-функция в `~/.bashrc`, которая заходит в папку проекта и запускает Claude Code с `--settings '{"ultracode": true}'` (режим **ultracode** = xhigh + воркфлоу, по умолчанию во всех лаунчерах — см. правило [`launcher-effort`](../../.claude/rules/launcher-effort.md)), `--dangerously-skip-permissions` (никаких окон с запросом разрешений), `--chrome` и `--remote-control <project>` (Remote Control — управление сессией из приложения Claude, тоже по умолчанию):

```bash
# Запуск Claude Code для проекта <Name>
<short>() {
  cd /home/john-laptev/AI/<Project> || return 1
  claude --settings '{"ultracode": true}' --dangerously-skip-permissions --chrome --remote-control <project> "$@"
}
```

Существующие примеры: `MF` (Media-Flow), `halo` (catalog-agent), `FX` (smelter), `john` (portfolio), `project` (Project-Forge), `efir3` (Efir-Chat-v3). **`ultracode` нельзя задать флагом `--effort`** (тот принимает только low/medium/high/xhigh/max) — только через `--settings '{"ultracode": true}'`; `--effort xhigh` можно оставить опциональным фолбэком.

Для проекта с изолированным Telegram-ботом — вариант с env + `--channels` (см. [`telegram-channel.md`](telegram-channel.md)):

```bash
<short>() {
  cd /home/john-laptev/AI/<Project> || return 1
  TELEGRAM_BOT_TOKEN=<token> \
  TELEGRAM_STATE_DIR="$HOME/.claude/channels/telegram-<project>" \
  claude --settings '{"ultracode": true}' --effort xhigh --dangerously-skip-permissions --chrome --remote-control <project> --channels plugin:telegram@claude-plugins-official "$@"
}
```

## Шаги
1. Предложи имя команды — короткое, латиницей, НЕ конфликтующее с существующими (`efir`, `MF`, `john`, `halo`, `FX`, `project`, `efir3`, `QR`, `consultant`) и с системными командами. По умолчанию — короткое от имени проекта.
2. **Только по согласию пользователя** добавь функцию в конец `~/.bashrc` (это личный dotfile — не редактируй молча; токены/секреты — по правилу `personal-info`).
3. Скажи применить: `source ~/.bashrc` (или новый терминал). Дальше команда `<short>` открывает Claude Code из папки проекта.

## Файл-лаунчер `launch.desktop` (двойной клик / запуск ассистентом)
Дополнительно к функции в `~/.bashrc` создай в папке проекта `launch.desktop` — он открывает терминал с уже запущенной командой:

```
[Desktop Entry]
Type=Application
Name=<Project> — Claude Code
Exec=gnome-terminal --title="<Project> · Claude Code" -- bash -ic '<launcher>; exec bash'
Icon=utilities-terminal
Terminal=false
Categories=Development;
```

- `chmod +x launch.desktop` + `gio set launch.desktop metadata::trusted true` — двойной клик в Nautilus без предупреждения.
- Зовёт функцию из `~/.bashrc` (`bash -ic`) — токен НЕ дублируется в файле.
- Добавь `launch.desktop` в `.gitignore` (машинно-зависимый — на другой машине функции-лаунчера нет).
- **Ассистент может запустить проект сам** (по согласию): `gio launch <project>/launch.desktop` или прямой `gnome-terminal -- bash -ic '<launcher>; exec bash'` → откроется НАСТОЯЩИЙ терминал с сессией (TTY есть → интерактивный Claude Code работает). Нужны доступный дисплей (`DISPLAY`/Wayland) и `gnome-terminal`.

## Почему без окон
- `--dangerously-skip-permissions` отключает окна-разрешения целиком — пользователь не любит окна вида «да / никогда больше не спрашивать».
- Дисциплина (commit/push **только по команде**) держится **в чате** через правило `git-workflow`, а НЕ через окна разрешений. Поэтому в каркасе `.claude/settings.json` git-операции лежат в `allow` (без окон), а блока `ask` нет.

## Что показать пользователю
> «Заведу команду `<short>`: из любого места в консоли откроет Claude Code в папке проекта с неограниченными правами (без окон-разрешений). Добавить в `~/.bashrc`?»
