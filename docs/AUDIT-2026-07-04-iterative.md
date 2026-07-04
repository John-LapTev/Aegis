# Итеративный аудит Aegis — 2026-07-04 (процесс Ивана 1324)

**Задача (Иван, 1324):** провести полный аудит «со стороны» по всем функциям/кнопкам/логике; между раундами чинить; повторять, пока **два аудита подряд** не покажут, что проблем нет — только тогда отчитаться.

**Метод:** каждый раунд — параллельные субагенты-ревьюеры (read-only) по направлениям; проверенные находки чинятся; следующий раунд перепроверяет фиксы на регрессии + свежий взгляд. Между раундами — сборка `dotnet build`/`dotnet test`, 0 warnings.

**Итог:** 6 раундов. Раунды 5 и 6 — чисто (регрессий нет, серьёзных багов нет). Тесты **463** (+1 Windows-only), 0 warnings.

## Что найдено и исправлено по раундам

### Раунд 1 (4 агента) — 2 HIGH + 7 LOW
- **HIGH** `StartupProgramRemover.MatchProgram` наивный `Contains` → снос ЧУЖОЙ программы (Rave→Brave). Фикс: `NameMatch.ReferencesName` (граница слова), общий хелпер.
- **HIGH** чистка остатков безвозвратна, но сообщение «(обратимо)»; `PathSafety ≥2` пропускал `C:\Tools`. Фиксы: честный текст; денилист общих контейнеров + отказ UNC/«..»; повторная проверка перед `Directory.Delete`; честные докстроки.
- **MED** `FixOrchestrator` при отмене пакета терял применённые правки → break + частичные outcomes.
- **LOW** периодическая перепроверка обновления; VM `IDisposable`; `DriverUpdateOffer` HashSet по ссылке + стабильный хеш в Id; `NamesOverlap`; `HasSelectable` уведомление; дубликат стиля `Button.chip`; Id-различители NetworkThreat(+PID)/Temperature(+index).

### Раунд 2 (2 агента) — фиксы р1 верны; +6
- `PathSafety` обход пробелами/точками в сегментах; «Позже» держится до перезапуска/новой версии; барьеры `PathSafety` в `RegistryKeyDeleteFix`/`FolderRecycleFix`; `VirusTotalClient` `TryGetProperty`; удалён мёртвый `IsGenericDevice`; dedup-гард в `UtilitiesScanner`.

### Раунд 3 (2 агента) — фиксы р2 верны; +5 (честная обратимость)
- `RegistryKeyBackupStore.Restore`/`ScheduledTaskBackupStore.Restore` игнорировали код выхода → молчаливый ложный успех → бросают при провале.
- `QuarantineStore` пишет запись до `File.Move` (нет осиротевших).
- `MinerRemovalFix` — снятие автозапуска без восстановимого бэкапа (footgun: пересоздание автозапуска майнера из «Бэкапов»); зонтик — точка восстановления.
- Атомарная запись индексов (`AtomicFile`).
- Ребут-флоу RunOnce+UAC — задокументировано как известное ограничение (роадмап).

### Раунд 4 (2 агента) — фиксы р3 верны; +1 HIGH +2 MED +2 LOW
- **HIGH** «Отключить службу» (Group=Autostart, kind=service-disable) перехватывалась веткой Autostart в `FixFactory` → всегда падала. Ветка ограничена autostart-kind → `ServiceDisable`→`RegistryValueFix`. +регресс-тест `FixFactoryTests`.
- **MED** `AppxRemovalBackupStore.Restore` ложный успех → бросает; `ScheduledTaskBackupStore.Restore` PS-fallback `Enable-ScheduledTask`.
- **LOW** `ProcessStopFix` честно сообщает, если процесс выжил; `QuarantineStore` `File.Delete` в catch не маскирует исходное исключение.

### Раунд 5 (2 агента) — ЧИСТО (1-й проход)
Фиксы р4 верны, регрессий нет; трассировка всех находок сканер→kind→Fix→кнопка: «мёртвых кнопок» нет (служба была единственной). Косметика: подписи `suspicious-task-`/`dangerous-driver-` → «Отключить»/«Удалить файл».

### Раунд 6 (2 агента) — ЧИСТО (2-й проход подряд)
Независимая перепроверка: контракты Data-ключей (producer↔consumer, 24 kind), арифметика «Здоровья», ViewModel, граничные случаи, обратимость — реальных дефектов нет. Пограничные заметки (ребут-флоу, майнер-quarantine) — задокументированные design-tradeoffs.

## Новые файлы
- `src/Aegis.System/Internal/NameMatch.cs` — сопоставление имени по границе слова.
- `src/Aegis.System/Internal/AtomicFile.cs` — атомарная запись (temp+rename).
- `tests/Aegis.System.Tests/Fixing/FixFactoryTests.cs` — регресс маршрутизации service-disable.
