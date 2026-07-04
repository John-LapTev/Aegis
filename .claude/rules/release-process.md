# Правило: выпуск версии (авто-обновление через GitHub Release)

Иван тестирует на Win11, забирая сборки через кнопку «Обновить» (авто-апдейт из GitHub Releases). Это согласованный канал доставки (диалоги 1250/1252). Промежуточные `.exe` в Telegram НЕ шлём — только релиз.

## Порядок выпуска (по явной готовности задачи)
1. **Версия:** поднять `<Version>` в `src/Aegis.App/Aegis.App.csproj` (SemVer, шаг minor на заметное).
2. **Сборка/тесты:** `dotnet build` + `dotnet test` — **0 warnings**, все тесты зелёные (кроме 1 Windows-only skip на Linux).
3. **Доки в ТОМ ЖЕ изменении:** `docs/CHANGELOG.md` (запись версии), `docs/PROJECT_STRUCTURE.md` (новые/удалённые файлы), `.session-log.md` (кратко). См. [`documentation-discipline`](documentation-discipline.md).
4. **Публикация `.exe` СО ВСЕМИ ключами** (иначе тумблеры AI/ИИ/поиск/VirusTotal молча отключаются — ключи вшиваются при publish):
   `dotnet publish src/Aegis.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:VirusTotalKey=… -p:GeminiKey=… -p:GoogleKey=… -p:GoogleCx=… -p:TavilyKey=… -p:SerperKey=… -p:OpenRouterKey=… -p:ApiglueKey=…`
   Ключи — из `.personal/credentials.md`. Сжатие обязательно (~51 МБ вместо ~108 МБ).
5. **SHA-256:** `sha256sum Aegis.exe | awk '{print $1}' > Aegis.exe.sha256` (приложение сверяет размер + MZ + хэш при обновлении).
6. **Git (только на этом шаге коммитим):** `git add -A`, коммит (semantic, с трейлерами Co-Authored-By/Claude-Session), тег `vX.Y.Z`, `git push origin main --tags`.
7. **Релиз:** `gh release create vX.Y.Z Aegis.exe Aegis.exe.sha256 --title "Aegis X.Y.Z" --notes "…"`. Проверить, что оба ассета прикреплены.
8. **Отчёт Ивану** в Telegram — коротко: что вошло, что проверить на ПК (системные операции — только у него).

## Важно
- Ветка `main` (upstream `origin/main`); коммиты/пуш — только по факту готовности релиза (git-действия по [`git-workflow`](git-workflow.md)).
- Периодическая авто-проверка обновления в приложении — каждые 15 мин; для немедленного получения свежей версии Иван перезапускает или жмёт «О программе → Проверить обновление».
- Ключи/токены — НИКОГДА в репозиторий, память или CLAUDE.md.
