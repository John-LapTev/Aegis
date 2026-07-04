# Структура проекта — Aegis

> **Статус: каркас Фазы 1 создан** (часть .NET-решения реализована; остальные проекты — ПЛАН). **ОБЯЗАТЕЛЬНО** держать в синхроне с реальностью: обновляй в том же изменении, что добавляет/удаляет/переименовывает файлы (см. [`.claude/rules/documentation-discipline.md`](../.claude/rules/documentation-discipline.md)).

## Репозиторий (документация и конфиг)

```
aegis/
├── CLAUDE.md                 # «Мозг»: ориентировка + ссылки на правила
├── README.md
├── .gitignore               # вкл. .personal/, секреты, артефакты, bin/obj
├── .gitattributes
├── .claude/
│   ├── settings.json        # permissions + SessionStart-hook
│   ├── hooks/session-context.sh
│   ├── rules/               # правила (авто-загрузка; вкл. coding-standards для C#/.NET)
│   ├── commands/            # слэш-команды
│   └── agents/              # субагенты
├── docs/
│   ├── PROJECT_STRUCTURE.md # этот файл
│   ├── BUILD.md             # как собрать/запустить/протестировать (на Windows)
│   ├── VISION.md · ARCHITECTURE.md · ROADMAP.md · CHANGELOG.md
│   ├── AUDIT-2026-07-02.md · AUDIT-2026-07-03.md · AUDIT-2026-07-03-fresh-eyes.md · AUDIT-2026-07-04.md · AUDIT-2026-07-04-round2.md · AUDIT-2026-07-04-round3.md # отчёты аудитов
│   ├── DESIGN.md            # тёмные токены/стиль для Avalonia/XAML
│   ├── DESIGN_BRIEF.md      # промпт для Claude Design
│   ├── design/mockups/      # утверждённые макеты экранов (HTML+PNG, референс UI)
│   ├── design/logo/         # исходник логотипа (aegis-logo.svg + png)
│   ├── security/            # threat-backlog.md (индикаторы из red-team) · miner-removal.md · threat-verification.md (схема heuristics→Defender→VirusTotal)
│   └── decisions/           # ADR (0001 стек · 0002 бэкап/откат · 0003 угрозы · 0004 права · 0005 драйверы/GPU)
├── knowledge-base/          # база знаний (по требованию)
│   └── INDEX.md
└── .personal/               # ЛИЧНОЕ — не в репозитории (.gitignore)
```

## Реализовано (Фаза 1 — каркас .NET)

> Сборка и публикация `.exe` — **на любой ОС** (Avalonia, ADR 0006); запуск окна и Windows-операции — на Windows. См. [`BUILD.md`](BUILD.md).

```
aegis/
├── Aegis.sln                         # решение (Core · App · Core.Tests)
├── Directory.Build.props             # общие стандарты (nullable, warnings-as-errors, analyzers)
├── src/
│   ├── Aegis.Core/                   # контракты + модели + оркестрация (net9.0, без UI-зависимостей)
│   │   ├── Aegis.Core.csproj
│   │   ├── Abstractions/             #   IScanner · IFix · IFixFactory · IWhitelist · IFileReputationCheck · IScanOrchestrator · IFixOrchestrator · IRestorePointService · IThreatReputationService · IProgressReportingFix(живой % SFC/DISM) · IAiAssistant(цепочка моделей) · IWebSearch(веб-поиск) · IDeviceUpdateLookup(поиск драйверов/утилит) · IDriverUpdateCatalog(каталог обновлений драйверов Windows Update) · IRebootRollbackScheduler
│   │   ├── Models/                   #   Severity · ScanGroup · Finding · ScanResult · ScanProgress · FindingKinds(значения Data["kind"]) · FindingDataKeys(имена ключей Data) · AiResult · WebSearchResult · DeviceUpdateResult · DriverUpdateOffer(доступное обновление драйвера из Windows Update) · DriverInstallResult(итог установки драйвера в программе)
│   │   │                             #   · BackupKind · BackupRecord · FixOutcome · FixProgress · BatchFixResult
│   │   ├── HumanSize.cs              #   единый формат размеров («2.1 ГБ») — для сканов и UI
│   │   ├── RedistDeletionMatcher.cs  #   разбор ответа ИИ про дубли пакетов + безопасное сопоставление (x86/x64, fail-safe)
│   │   ├── TrustedDomains.cs         #   единый белый список официальных доменов (драйверы + проверка ссылок ИИ)
│   │   ├── UpdateVersion.cs          #   сравнение версий для авто-обновления (тег GitHub vs текущая)
│   │   ├── Scanning/                 #   ScanOrchestrator (прогресс, агрегация, ошибка→находка)
│   │   ├── Monitoring/               #   InstallMonitor (слежение за установкой: снимок до/после → «след» → полное удаление)
│   │   ├── Fixing/                   #   FixOrchestrator (бэкап ПЕРЕД пакетом; нет бэкапа → нет правок)
│   ├── Aegis.Scanners/               # сканеры-плагины (net9.0; Windows-доступ через пробники)
│   │   ├── Probing/                  #   I*Probe + модели: Junk · Autostart · Process · SignatureStatus · Audio(устройства+улучшайзеры) · Utilities(модель ПК+периферия) · Leftover(остатки программ) · SteamLeftover(остатки игр Steam) · StaleFile(битые ярлыки/пустые/старые) · AppCache(кэш приложений) · Battery(износ батареи) · MaintenanceHistory(дата запуска инструментов) · DangerousDriver(опасные драйверы) · WmiPersistence(подписки WMI) · SuspiciousService · SuspiciousTask · UserFolderKind(простые имена больших папок) · FolderEntry(содержимое большой папки: файл/подпапка + размер)
│   │   ├── Junk/JunkScanner.cs       #   мусор (блок «А»): группировка, размеры, объяснения
│   │   ├── Autostart/AutostartScanner.cs  # автозапуск: подпись+путь → severity, MS-записи пропускает
│   │   ├── Processes/ProcessesScanner.cs  # процессы: подозрительные из temp / возможные майнеры
│   │   ├── SystemInfo/SystemScanner.cs    # здоровье: защита откл. / мало места / ожидание ребута
│   │   ├── SystemInfo/ChangesScanner.cs   # «Что изменилось» (Система): новые программы/hosts с прошлой проверки
│   │   ├── SystemInfo/AutostartChangesScanner.cs # «Новое в автозапуске» (вкладка Автозапуск): новые записи автозапуска, свой файл-снимок
│   │   ├── SystemInfo/TrendsScanner.cs    # тренды дисков: рост секторов/износа/температуры + «занято было/стало»
│   │   ├── SystemInfo/BootPerformanceScanner.cs # время загрузки Windows + кто тормозит (журнал Diagnostics-Performance); группа Autostart, связка с автозапуском (кнопка «Отключить»)
│   │   ├── SystemInfo/DiskHealthScanner.cs # SMART: шкала 🟢/🟡/🔴 по дискам (раздел «Здоровье»)
│   │   ├── SystemInfo/TemperatureScanner.cs # температуры CPU/GPU: шкала 🟢/🟡/🔴 (раздел «Здоровье»)
│   │   ├── SystemInfo/BatteryScanner.cs  # износ батареи % + вердикт (раздел «Здоровье»; на десктопе скрыт)
│   │   ├── Registry/RegistryScanner.cs    # реестр: осиротевшие/битые ссылки → понятные находки
│   │   ├── Settings/SettingsScanner.cs    # настройки: брандмауэр/UAC/обновления/RDP
│   │   ├── Guard/GuardEvaluator.cs   # мозг фонового стража: процессы+простой→тревоги (майнер в реальном времени)
│   │   ├── Threats/                  # угрозы: NetworkThreatScanner(hosts/DNS/подключения+майнинг-порты) · MinerBehaviorScanner(поведенческий детект майнеров: без подписи+CPU+прячется+автозапуск+простой) · DangerousDriverScanner(опасные драйверы по LOLDrivers) · WmiPersistenceScanner(скрытые подписки WMI) · SuspiciousServiceScanner(службы из Temp/AppData) · SuspiciousTaskScanner(задачи с закодированным запуском, отключаемые)
│   │   ├── Privacy/PrivacyDebloatScanner.cs # телеметрия/реклама + лишний фон Windows (отключить)
│   │   ├── Apps/AppxBloatScanner.cs     # встроенный UWP-хлам (Candy Crush и т.п.) — удалить (обратимо)
│   │   ├── Files/LargeDuplicateScanner.cs   # большие и дублирующиеся файлы (освободить место)
│   │   ├── Files/DiskUsageScanner.cs    # анализатор места: крупнейшие папки с простыми именами (Загрузки/Рабочий стол…), без галочки (noBatch)
│   │   ├── Maintenance/WindowsUpdateCleanupScanner.cs # старые компоненты обновлений Windows (DISM-очистка)
│   │   ├── Maintenance/SystemMaintenanceScanner.cs # обслуживание (вкладка «Система»): починка системных файлов (SFC/DISM) + сброс сетевых настроек
│   │   ├── Drivers/                  #   DriversScanner (модель ПК · устройства без драйвера/с проблемой · видеокарта · сверка версий драйверов ВСЕХ устройств через Windows Update) · DriverUpdateMatcher (устройство↔обновление по HardwareID/имени; чистая функция)
│   │   ├── Drivers/AudioScanner.cs      # звук (вкладка «Драйверы»): схема звука простыми словами + «улучшайзеры» (Nahimic/Dolby/MaxxAudio) → обратимое отключение службы
│   │   ├── Utilities/UtilitiesScanner.cs # фирм. утилиты под модель ПК/периферию (Vantage/Armoury Crate/G HUB…), установка через winget (вкладка «Утилиты»)
│   │   ├── Programs/ProgramLeftoverScanner.cs # остатки удалённых программ (вкладка «Мусор»): пустые папки профиля → удаление в Корзину
│   │   ├── Programs/SteamLeftoverScanner.cs # остатки игр Steam (вкладка «Мусор»): по базе appmanifest — кэши удалённых игр (безопасно) + следы пираток (осторожно)
│   │   ├── Programs/StaleFileScanner.cs # «Мусор»: битые ярлыки (.lnk в никуда), пустые (0 байт) файлы, давно не тронутые загрузки
│   │   ├── Programs/AppCacheScanner.cs  # «Мусор»: глубокая чистка кэша приложений (подход Winapp2, свой каталог) — в Корзину
│   │   ├── Online/                   #   DeviceUpdateLookup (IDeviceUpdateLookup: веб-поиск драйвера/утилиты + версии, предпочитает офиц. домены) · VersionExtractor (версия из выдачи)
│   │   └── Internal/                 #   PathHeuristics · TrustedPublishers · NetworkHeuristics · LolBinHeuristics · ProgramCatalog (происхождение процесса: Windows/вендор + метка)
│   ├── Aegis.Threats/                # внешняя репутация + ИИ (net9.0)
│   │   ├── VirusTotal/               #   VirusTotalClient (IThreatReputationService) + ReputationMapper + ThrottledCachingReputationService (кеш+лимит ≈4/мин)
│   │   ├── Ai/                       #   Цепочка моделей: GeminiClient + OpenAiCompatibleClient(Groq/Mistral) → FallbackAiAssistant(авто-переключение по лимитам) → WebAugmentedAiAssistant(подмешивает веб-поиск) + NullAiAssistant(без ключа). Ключи из окр./.personal/-p:GeminiKey/GroqKey/MistralKey. AiResult/IAiAssistant — в Aegis.Core
│   │   └── Web/                       #   Веб-поиск (IWebSearch): DuckDuckGoSearch (без ключа, HTML) + TavilySearch + SerperSearch (для ИИ, без карты — ОСНОВНЫЕ) + BraveSearch + GoogleSearch (по ключу) + FallbackWebSearch (цепочка Tavily→Serper→…→DuckDuckGo резерв) — свежие данные/ссылки для ИИ и поиска драйверов
│   ├── Aegis.System/                 # Windows-чтение системы (net9.0-windows; компилируется на Linux)
│   │   ├── Probes/                   #   пробники: Junk·Autostart·Process·Settings·SystemHealth·Registry·Privacy·DiskHealth(SMART)·NetworkThreat·FileInventory(дубли-воронкой)·Driver·Temperature(CPU/GPU)·DiskUsage·Appx(UWP)·Audio·Utilities·Leftover·SteamLeftover·StaleFile·AppCache·Battery(износ батареи)·MaintenanceHistory(дата запуска инструментов; DiskHealth переписан на MSFT_Disk)·DangerousDriver(SHA-256 загруженных драйверов→LOLDrivers)·WmiPersistence(root\subscription)·SuspiciousService(службы из Temp/AppData+подпись)·SuspiciousTask(schtasks /query /xml→команды задач)·NvidiaDriverCheck(AjaxDriverService: последняя версия драйвера NVIDIA+ссылка)·WindowsUpdateDriverCatalog(IDriverUpdateCatalog: обновления драйверов ВСЕХ устройств + установка прямо из программы через WUA COM, best-effort)·SystemSnapshot(«Что изменилось»)·UserActivity(GetLastInputInfo: простой пользователя для детекта майнеров)·BootPerformance(журнал Diagnostics-Performance: время загрузки+тормоза)·InstallSnapshot(снимок мест установки: папки+реестр для слежения за установкой)
│   │   ├── Guard/SystemGuard.cs        #   тихий фоновый страж (таймер: процессы+простой→GuardEvaluator→уведомления в трее)
│   │   ├── Fixing/StartupProgramRemover.cs #   «Удалить полностью» из автозапуска: инсталлятор+чистка / нет установщика → чистка остатков по имени (LeftoverService), обратимо
│   │   ├── Fixing/LeftoverService.cs   #   поиск/удаление остатков программы (папка/AppData/реестр/след установки) для окна остатков (Revo)
│   │   ├── Backup/                    #   RegistryBackupStore · QuarantineStore · RegistryKeyBackupStore · ScheduledTaskBackupStore · AppxRemovalBackupStore · RestorePointService · WhitelistStore · RebootRollbackScheduler(RunOnce→проверка отката после перезагрузки) · SystemSnapshotStore(«Что изменилось») · HealthTrendStore(история дисков для трендов) · InstallTraceStore(следы установки для полного удаления) · ActivityStatsStore(статистика для «Сравнить состояние»)
│   │   ├── Fixing/                    #   RegistryValueFix · JunkCleanupFix · AutostartDisableFix · ProcessStopFix · RecycleBinFix · FolderRecycleFix(папка-остаток) · FolderItemsDeleteFix(выбранные файлы/подпапки из большой папки) — удаления поддерживают режим «в Корзину/навсегда» (выбор в DeleteConfirmWindow) · RegistryKeyDeleteFix · DeviceEnableFix · ScheduledTaskDisableFix · AppxRemoveFix · SystemRestoreEnableFix · WingetInstallFix(фирм. утилиты) · Dism/Sfc/NetworkReset/DriverSearchFix · MinerRemovalFix(обезвреживание майнера: стоп-дерево+снятие автозапуска+карантин/удаление после ребута) · DriverUpdateInstallFix(установка драйвера прямо из программы через Windows Update по updateId) · RebootFix(кнопка «Перезагрузить» с задержкой) · FixFactory
│   │   ├── Reputation/                #   FileReputationCheck (Защитник + VirusTotal по хэшу)
│   │   └── Internal/                 #   AuthenticodeTrust (WinVerifyTrust) · ShortcutResolver · DefenderScanner (MpCmdRun) · FileSignatureInspector · RegistryReader · ScheduledTaskReader · ProcessRunner · CommandLine · RecycleBin · RegistryHiveNames · RegistryValueCodec(тип значения) · CpuUsage · NetstatParser · AppxBloatCatalog(белый список UWP) · UsbVendors(VID→вендор периферии) · InstalledPrograms(общий список Uninstall) · ShellFileOperation(удаление в Корзину с предупреждением о безвозвратном) · SteamVdf(парсер libraryfolders.vdf/appmanifest.acf — установленные игры Steam) · FileLockInspector(Restart Manager — кто держит файл) · FileIdentity(том+индекс файла — отсев жёстких ссылок при поиске дублей) · AppCachePathExpander(переменные+маски путей кэша) · AppCacheCatalog(свой каталог кэшей приложений) · UsbIdDatabase(встроенная база usb.ids: VID→вендор, VID+PID→модель) · Data/usb.ids(linux-usb.org, GPLv2/BSD) · PciIdDatabase(pci.ids: VEN→вендор, VEN+DEV→модель — имена PCI-железа) · Data/pci.ids(pci-ids.ucw.cz, BSD/GPL) · LolDriversDatabase(опасные драйверы по SHA-256) · Data/loldrivers.txt(LOLDrivers, Apache-2.0; сжатый список хэшей) · NvidiaGpuData(видеокарта→pfid для проверки обновлений) · Data/nvidia-gpu-data.json(ZenitH-AT/nvidia-data) · PendingDelete(MoveFileEx: удаление запертого файла при следующей загрузке)
│   └── Aegis.App/                    # Avalonia-приложение (net9.0-windows; собирается на Linux, ADR 0006)
│       ├── Aegis.App.csproj          #   Avalonia 11 + CommunityToolkit.Mvvm + DI + Serilog; ссылается на System/Scanners
│       ├── Assets/                   #   aegis.ico (иконка .exe/окна) · aegis-logo.png (шапка)
│       ├── app.manifest              #   requireAdministrator
│       ├── Program.cs                #   точка входа (AppBuilder) + установка перехватчика аварий
│       ├── StartupCrashHandler.cs    #   ловит сбои запуска: файл crash-*.txt + окно с ошибкой (иначе GUI закрывается молча)
│       ├── App.axaml · App.axaml.cs  #   тёмная тема + стили (блоки-вкладки, шкала сканирования, выпадающие списки) + DI (пробники→сканеры→оркестратор), без авто-скана
│       ├── Views/MainWindow.axaml(.cs)#  рельс (Сканы/Бэкапы/О программе) · блоки-разделы со шкалой · флажки · карточки находок · «?» · действия
│       ├── Views/DeleteConfirmWindow.axaml(.cs)# диалог удаления: Отменить / Удалить навсегда / в Корзину (DeleteChoice)
│       ├── Views/MessageDialog.axaml(.cs)#  всплывающее окно-результат: заголовок + текст + необязательная кнопка действия (напр. «Открыть „Приложения“ Windows»)
│       ├── Views/RollbackConfirmWindow.axaml(.cs)# окно после перезагрузки «всё работает?» + таймер → авто-откат (RollbackChoice)
│       ├── StatusColors.cs          #   единые цвета статусов (зелёный/жёлтый/красный/синий) — один источник на C#
│       ├── ViewModels/               #   MainWindow (скан-по-разделам/починка/разделы) · NavSection · ScanGroup · Finding · DuplicateCopy · FileEntry(элемент содержимого большой папки: иконка типа+размер+открыть) · BackupItem · BackupGroup · AiModel (раздел «Нейросети»: модель + статус + ключ Заменить/Вернуть) · ScanViewHelpers (чистые хелперы экрана сканов)
│       ├── Converters/               #   SeverityToBrushConverter · IconKeyToGeometryConverter (SVG-иконки) · DigitCountToWidthConverter (ширина флажка по числу цифр) · FractionToAngleConverter (кольцо-заполнение прогресса) · ModelIconConverter/ModelBrushConverter (SVG-знак+цвет ИИ-модели) · ModelImageConverter (картинка-логотип модели, напр. Gemini — Assets/models/)
│       └── Services/                 #   IElevationService · ElevationService (права админа)
└── tests/
    ├── Aegis.Core.Tests/             # xUnit: оркестраторы (вкл. честность точки восстановления), модели, ключи Data
    ├── Aegis.Scanners.Tests/         # xUnit: сканеры (большинство групп; SystemMaintenance — без отдельного теста)
    ├── Aegis.System.Tests/           # xUnit (net9.0-windows): нормализация кустов реестра, деградация RestorePointService, round-trip бэкап ветки ([WindowsOnlyFact])
    └── Aegis.Threats.Tests/          # xUnit: VirusTotalClient + GeminiClient (фейковый HTTP, разбор/429) + FallbackAiAssistant(цепочка) + DuckDuckGo/Tavily/Serper/Brave/Google(парсеры выдачи) + WebAugmentedAiAssistant(подмешивание поиска)
```

## Целевое .NET-решение (остальное — ПЛАН)

> Появится по мере разработки (Фаза 2+). Каждый проект решения — отдельная зона ответственности; UI отделён от движка сканов, движок — от низкоуровневого доступа к системе. Уже созданные проекты (`Aegis.App`, `Aegis.Core`, `Aegis.Core.Tests`) помечены ✅.

```
aegis/
├── Aegis.sln                        # ПЛАН — решение Visual Studio / dotnet
├── src/
│   ├── Aegis.App/ ✅                # Avalonia-приложение (UI, MVVM, DI-композиция) — каркас готов
│   │   ├── Views/                   #   + Controls/, Assets/ — по мере роста UI (Фаза 2+)
│   │   └── …                        #   состав см. в разделе «Реализовано» выше
│   ├── Aegis.Core/ ✅               # движок сканов (контракты + оркестрация) — каркас готов
│   │   ├── Fixing/                  #   ПЛАН — FixService (обратимые правки, одиночно/пакетно)
│   │   └── Backup/                  #   ПЛАН — RebootFlow (RunOnce→диалог); контракты уже в Abstractions
│   ├── Aegis.Scanners/ ✅           # реализации IScanner (10 групп = вкладки) — проект создан
│   │   ├── SystemScanner.cs         # ✅ готов (здоровье системы)
│   │   ├── JunkScanner.cs           # ✅ готов (мусор, блок «А»)
│   │   ├── RegistryScanner.cs       # ✅ готов (реестр, блок «А»)
│   │   ├── AutostartScanner.cs      # ✅ готов (автозапуск, блок «А»)
│   │   ├── ProcessesScanner.cs      # ✅ готов (процессы)
│   │   ├── SettingsScanner.cs       # ✅ готов (системные настройки)
│   │   ├── DriversScanner.cs
│   │   ├── GpuScanner.cs
│   │   ├── ThreatsScanner.cs        #   делегирует в Aegis.Threats
│   │   └── MissingScanner.cs
│   ├── Aegis.System/ ✅             # низкоуровневый доступ — создан (21 пробник в Probes/; бэкап/правки/репутация реализованы)
│   │   ├── Wmi/                     #   ПЛАН — System.Management (SMART, железо)
│   │   ├── Registry/                #   Microsoft.Win32.Registry (чтение/экспорт веток)
│   │   ├── Services/                #   управление службами Windows
│   │   ├── Processes/               #   Process API
│   │   ├── Gpu/                     #   GpuTuner: NVAPI/nvidia-smi · ADLX/ADL · вендор-реестр
│   │   └── Interop/                 #   P/Invoke-обёртки (безопасные)
│   ├── Aegis.Backup/                # ПЛАН — бэкап/откат
│   │   ├── RestorePoints/           #   точки восстановления Windows (System Restore)
│   │   ├── RegistryExport/          #   экспорт/импорт затронутых веток ПЕРЕД правкой
│   │   └── BackupStore/             #   метаданные бэкапов для раздела «Бэкапы»
│   └── Aegis.Threats/               # ПЛАН — сканер угроз
│       ├── Heuristics/              #   эвристика процессов/автозапуска/реестра/сети, майнеры
│       ├── VirusTotal/              #   сверка хэшей через VirusTotal API
│       └── Defender/                #   опц. базовая сверка (MpCmdRun/AMSI)
└── tests/                           # xUnit
    ├── Aegis.Core.Tests/ ✅         #   логика оркестрации, правок, модели (готово)
    ├── Aegis.Scanners.Tests/ ✅     #   логика сканеров (готово)
    └── Aegis.System.Tests/ ✅       #   бэкап/экспорт веток, нормализация кустов, деградация SR (бэкап живёт в Aegis.System, отдельный Aegis.Backup не выделялся)
```

### Соответствие модулей и проектов
| Проект решения | Зона ответственности | См. ARCHITECTURE |
|----------------|----------------------|------------------|
| `Aegis.App` | Avalonia-оболочка, вкладки, MVVM, DI-композиция, тёмная тема | Оболочка приложения |
| `Aegis.Core` | `IScanner`/`Finding`, оркестрация, `FixService`, `RebootFlow` | Движок сканов, FixService, RebootFlow |
| `Aegis.Scanners` | 23 активных сканера-плагина (8 вкладок + «Здоровье») | Сканеры-плагины |
| `Aegis.System` | WMI / реестр / службы / процессы / P-Invoke / GpuTuner | Низкоуровневый доступ, GpuTuner |
| `Aegis.Backup` | System Restore + экспорт веток, хранилище бэкапов | BackupService |
| `Aegis.Threats` | эвристика + VirusTotal (+ опц. Defender) | ThreatScanner |
| `tests/*` | xUnit на логику сканеров/бэкапа/ядра | — |

> Артефакты сборки (`bin/`, `obj/`) и публикации (single-file self-contained, win-x64) — в `.gitignore`, не в репозитории.

- `src/Aegis.Core/Abstractions/IDeviceDriverAction.cs` — перезагрузка/переустановка драйвера устройства (pnputil).
- `src/Aegis.System/Devices/DeviceDriverAction.cs` — реализация через pnputil.
- `src/Aegis.App/ViewModels/FindingAiPrompt.cs` — построение промпта и веб-запроса к ИИ по находке (вынесено из FindingViewModel).
- `src/Aegis.App/ViewModels/OnlineReputationChecker.cs` · `ConnectivityWatcher.cs` · `ExternalOpener.cs` · `HealthTiles.cs` — механики, вынесенные из MainWindowViewModel (онлайн-репутация, слежение за сетью, открытие файлов, плитки «Здоровья»).
- `src/Aegis.App/Services/UpdateService.cs` — обновление внутри программы: проверка релиза GitHub, скачивание, само-замена .exe с перезапуском.
- `src/Aegis.App/Views/Sections/` — разделы главного окна, вынесенные из `MainWindow.axaml` в отдельные `UserControl` (рефакторинг): `AboutView`, `CompareView`, `OptimizeView`, `HealthView`, `DashboardView`, `TestsView`, `BackupsView`, `AiSettingsView`, `ForceDeleteView`, `UninstallView`, `ScansView` (у трёх последних — свой code-behind: выбор файла/установщика, drag-выделение/прокрутка). Все 11 разделов вынесены.
- `src/Aegis.App/ViewModels/DriverEntryViewModel.cs` — строка драйвера с галочкой выбора.
- `src/Aegis.App/ViewModels/FileEntryViewModel.cs` — элемент содержимого большой папки: иконка по типу, имя, размер, галочка, открытие по клику.
- `src/Aegis.Scanners/Probing/FolderEntry.cs` — файл/подпапка внутри большой папки (имя, путь, размер, папка-ли) для списка содержимого.
- `src/Aegis.Scanners/Probing/UserFolderKind.cs` — известная папка профиля (Загрузки/Рабочий стол…) для подписи простыми словами.
- `src/Aegis.System/Fixing/FolderItemsDeleteFix.cs` — удаление выбранных файлов/подпапок из большой папки (Корзина/навсегда).
- `src/Aegis.Scanners/Stress/` — проверка под нагрузкой: `StressTestKind`/`StressTestOptions`/`StressTestProgress`/`StressTestResult`/`StressAbortReason`, `ICpuLoad`+`CpuLoad` (нагрузка CPU), `IStressTestEngine`+`StressTestEngine` (нагрузка+мониторинг+авто-стоп+вердикт).
- `src/Aegis.Scanners/Probing/SystemVitals.cs` + `ISystemVitalsProbe.cs` — быстрые показатели здоровья (RAM, время работы, загрузка CPU, вентиляторы).
- `src/Aegis.System/Probes/SystemVitalsProbe.cs` — Windows-чтение vitals (GlobalMemoryStatusEx + uptime + WMI LoadPercentage/Win32_Fan).
- `src/Aegis.Scanners/SystemInfo/SystemVitalsScanner.cs` — 4 плитки «Здоровья»: память, время без перезагрузки, загрузка CPU, вентиляторы (вердикты).
- `src/Aegis.App/ViewModels/StressTestViewModel.cs` — раздел «Тесты»: запуск/стоп, живая шкала, итог; результат уходит в «Здоровье».
- `src/Aegis.Scanners/Probing/HardwareReadings.cs` + `IHardwareSensorReader.cs` — достоверные датчики железа (температуры ядер/обороты/частоты).
- `src/Aegis.System/Probes/LhmSensorReader.cs` — чтение датчиков через LibreHardwareMonitor (ленивое открытие, тихий откат).
- `src/Aegis.System/Probes/LhmTemperatureProbe.cs` — температуры через LHM с пофакторным откатом на ACPI/nvidia-smi.
- `src/Aegis.Scanners/Probing/IDeviceErrorProbe.cs` + `src/Aegis.System/Probes/DeviceErrorProbe.cs` — устройства с ошибками (Win32_PnPEntity).
- `src/Aegis.Scanners/SystemInfo/DeviceErrorScanner.cs` — плитка «Устройства» (что-то не работает).
- `src/Aegis.Scanners/Probing/ICrashHistoryProbe.cs` + `src/Aegis.System/Probes/CrashHistoryProbe.cs` — синие экраны (дампы Minidump за 7 дней).
- `src/Aegis.Scanners/SystemInfo/CrashHistoryScanner.cs` — плитка «Стабильность» (BSOD за неделю).
