#!/usr/bin/env bash
# SessionStart hook — ориентировка для свежей сессии. stdout уходит в контекст Claude.
cat <<'EOF'
[Aegis — ориентировка]
Aegis — десктоп .exe под Windows 10/11 (C#/.NET 9 + Avalonia): оптимизатор + чистильщик + сканер безопасности «для себя». Статус — в разработке: решение Aegis.sln (Core/Scanners/Threats/App + тесты); 10 сканеров, движок сканов и обратимых правок, удаление майнеров, VirusTotal — 75 тестов на реальной сборке. UI на Avalonia собирается в Windows .exe прямо на Linux (ADR 0006). Код УЖЕ ЕСТЬ в src/ и tests/.
Читай по порядку: CLAUDE.md → docs/ROADMAP.md → docs/PROJECT_STRUCTURE.md → docs/ARCHITECTURE.md. Сборка/публикация .exe — docs/BUILD.md (на любой ОС).
Ключевое: каждое системное изменение ОБРАТИМО — точка восстановления Windows + бэкап ветки реестра ПЕРЕД правкой; приложение требует прав администратора; будь деликатен к системе, без безвозвратных удалений.
Дисциплина: при любом изменении файлов сразу обновляй docs/PROJECT_STRUCTURE.md и затронутые доки.
Личные данные (токен бота, VirusTotal API key) — только в .personal/ (не в репо, не в память, не в CLAUDE.md).
Правила — в .claude/rules/ (грузятся автоматически).
Память диалога: транскрипт bot-сессий ненадёжен → ведём `.session-log.md` (см. правило session-log). Ниже — последние записи, ПРОДОЛЖАЙ с этого контекста.
EOF

# Недавняя память диалога (session-log) — независимая от ненадёжного транскрипта bot-сессий
LOG="$CLAUDE_PROJECT_DIR/.session-log.md"
if [ -f "$LOG" ]; then
  echo
  echo "===== .session-log.md (последние записи) ====="
  tail -n 60 "$LOG"
fi
exit 0
