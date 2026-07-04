using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Aegis.App.Services;
using Aegis.App.Views;
using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Monitoring;
using Aegis.Scanners.Internal;
using Aegis.Core.Models;
using Aegis.Scanners.Stress;
using Aegis.Threats.Ai;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static Aegis.App.ViewModels.ScanViewHelpers;

namespace Aegis.App.ViewModels;

/// <summary>Вариант фильтра списка находок по важности.</summary>
public enum FindingFilter
{
    All,
    Problems,
    Warnings,
    Advice,
    Fixed,
}

/// <summary>Пункт фильтра (подпись + значение + цвет-метка) для списка-переключателя.</summary>
public sealed record FilterOption(string Label, FindingFilter Filter, IBrush Dot);

/// <summary>
/// ViewModel главного окна: сканирует систему, раскладывает находки по вкладкам, фильтрует по важности
/// и применяет обратимые исправления (по одному и массово). Перед правками — точка восстановления.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    // Порядок вкладок. Только группы, у которых есть зарегистрированный сканер (Gpu — отдельная фаза,
    // не вкладка: видеокарта показывается в «Драйверах»). «Утилиты» (Missing) — фирменные утилиты под модель ПК.
    private static readonly ScanGroup[] GroupOrder =
    [
        ScanGroup.System, ScanGroup.Junk, ScanGroup.Autostart, ScanGroup.Processes,
        ScanGroup.Registry, ScanGroup.Settings, ScanGroup.Threats, ScanGroup.Drivers,
        ScanGroup.Missing,
    ];

    private readonly IScanOrchestrator _orchestrator;
    private readonly IReadOnlyList<IScanner> _scanners;
    private readonly IFixOrchestrator _fixOrchestrator;
    private readonly IActivityStatsStore _activityStats;
    private readonly IStartupProgramRemover _startupRemover;
    private readonly IUpdateService _updateService;
    private readonly IFixFactory _fixFactory;
    private readonly IWhitelist _whitelist;
    private readonly IRestorePointService _restore;
    private readonly IDeviceDriverAction _deviceAction;
    private readonly OnlineReputationChecker _onlineReputation;
    private readonly IAiAssistant _aiAssistant;
    private readonly IRebootRollbackScheduler _rebootRollback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScans), nameof(IsDashboard), nameof(IsUninstall), nameof(IsCompare), nameof(IsForceDelete), nameof(IsOptimize), nameof(IsHealth), nameof(IsTests), nameof(IsBackups), nameof(IsAbout), nameof(IsAiSettings))]
    private string _activeSection = "scans";

    /// <summary>Идёт ли применение правок (для кнопки «Отменить» долгих операций).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isApplyingFixes;

    /// <summary>Программа занята долгой операцией (скан или починка) — кнопки запуска блокируем, чтобы клик не был «пустышкой».</summary>
    public bool IsBusy => IsScanning || IsApplyingFixes;

    /// <summary>Нет интернета — показываем плашку, что проверка будет менее полной (часть проверок работает онлайн).</summary>
    [ObservableProperty]
    private bool _isOffline;

    /// <summary>Периодический опрос связи — чтобы плашка «нет интернета» сама появлялась/исчезала без перезапуска.</summary>
    private readonly ConnectivityWatcher _connectivity;

    /// <summary>ИИ-помощник ВКЛЮЧЁН (тумблер в шапке). По умолчанию выключен — чтобы не тратить лимит без спроса.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiStatusText), nameof(AiStatusOk), nameof(AiStatusSeverity), nameof(AiStatusTooltip))]
    private bool _aiEnabled;

    /// <summary>Лимит бесплатного тарифа исчерпан — ИИ временно недоступен (показываем в плашке статуса).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiStatusText), nameof(AiStatusOk), nameof(AiStatusSeverity), nameof(AiStatusTooltip))]
    private bool _aiLimitReached;

    private CancellationTokenSource? _fixCts;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private ScanGroupViewModel? _selectedGroup;

    [ObservableProperty]
    private FilterOption _selectedFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectAllLabel))]
    private bool _isAllSelected;

    [ObservableProperty]
    private string _statusText = "Выбери «Проверить всё» или конкретный раздел.";

    /// <summary>Подпись кнопки выделения (тумблер).</summary>
    public string SelectAllLabel => IsAllSelected ? "Снять выделение" : "Выделить всё";

    /// <summary>
    /// Есть ли среди отмеченного хоть что-то с ГОТОВЫМ действием — тогда показываем «Исправить выбранное».
    /// Иначе кнопку не показываем: она была бы «мёртвой» (нажал — ничего не происходит), см. аудит по просьбе Ивана.
    /// </summary>
    public bool HasFixableSelected => VisibleFindings.Any(f => f.IsSelected && f.CanFix && !f.IsFixed);

    /// <summary>Есть ли среди отмеченного что можно пометить «Безопасно» — тогда показываем эту кнопку.</summary>
    public bool HasWhitelistableSelected => VisibleFindings.Any(f => f.IsSelected && f.CanWhitelist && !f.IsMarkedSafe);

    /// <summary>Находки раздела «Мусор» (для подсчёта размеров).</summary>
    private IEnumerable<FindingViewModel> JunkFindings =>
        Groups.FirstOrDefault(g => g.Group == ScanGroup.Junk)?.Findings ?? Enumerable.Empty<FindingViewModel>();

    /// <summary>Размер «безопасной быстрой чистки» — то, что реально чистит кнопка «Быстрая чистка» (кэши/временные).</summary>
    private long JunkSafeBytes => JunkFindings.Where(IsQuickCleanSafe).Sum(f => f.SizeBytes);

    /// <summary>Полный размер мусора в разделе (включая большие файлы, корзину — их чистить с разбором), правка 960.</summary>
    private long JunkTotalBytes => JunkFindings.Sum(f => f.SizeBytes);

    /// <summary>Показывать ли суммы — только в разделе «Мусор» и если есть что считать.</summary>
    public bool HasJunkTotal => SelectedGroup?.Group == ScanGroup.Junk && JunkTotalBytes > 0;

    /// <summary>«Безопасно очистить: X» — сколько освободит «Быстрая чистка».</summary>
    public string JunkSafeLabel => "Безопасно очистить: " + HumanSize.Format(JunkSafeBytes);

    /// <summary>«Всего в разделе: Y» — весь мусор, включая большие файлы и корзину.</summary>
    public string JunkTotalLabel => "Всего в разделе: " + HumanSize.Format(JunkTotalBytes);

    /// <summary>Версия программы (видно, что обновилось).</summary>
    public string VersionText => "v" + (GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>Запущена ли программа с правами администратора (для честного бейджа и предупреждения).</summary>
    public bool IsAdministrator { get; }

    /// <summary>Подпись бейджа прав в шапке (по факту, а не захардкожено).</summary>
    public string AdminBadgeText => IsAdministrator ? "Права администратора" : "Нет прав администратора";

    /// <summary>Цвет точки бейджа: зелёная при правах, красная без них.</summary>
    public IBrush AdminBadgeBrush => Dot(IsAdministrator ? StatusColors.Ok : StatusColors.Danger);

    /// <summary>Показывать ли предупреждение об отсутствии прав администратора.</summary>
    public bool ShowAdminWarning => !IsAdministrator;

    /// <summary>Текст предупреждения, когда прав нет — простыми словами, что делать.</summary>
    public string AdminWarningText =>
        "Программа запущена без прав администратора. Проверка работает, но исправления и бэкап системы " +
        "не выполнятся. Закрой программу и запусти её от имени администратора (правой кнопкой → «Запуск от имени администратора»).";

    /// <summary>Показывать ли управление ИИ в шапке — только если задан ключ Gemini.</summary>
    public bool AiAvailable => _aiAssistant.IsConfigured;

    /// <summary>Активная модель ИИ (Gemini/Groq/Mistral) — какая сейчас отвечает (по цепочке переключения).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiStatusText))]
    private string _aiActiveModel = string.Empty;

    /// <summary>ИИ недоступен: не лимит, а нет связи / сервис не отвечает (напр. геоблок без VPN).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiStatusText), nameof(AiStatusOk), nameof(AiStatusSeverity), nameof(AiStatusTooltip))]
    private bool _aiUnavailable;

    /// <summary>Текст плашки статуса AI рядом с тумблером: работает (модель) / лимит / все заняты.</summary>
    public string AiStatusText => !AiEnabled
        ? "AI выключен"
        : AiUnavailable
            ? "Все модели заняты"
            : AiLimitReached
                ? "Лимит исчерпан"
                : string.IsNullOrEmpty(AiActiveModel) ? "AI доступен" : $"AI: {AiActiveModel}";

    /// <summary>Статус «в порядке» (зелёный): включён, лимит не исчерпан, есть связь.</summary>
    public bool AiStatusOk => AiEnabled && !AiLimitReached && !AiUnavailable;

    /// <summary>Цвет плашки: зелёный — работает, жёлтый — лимит, красный — нет доступа.</summary>
    public Severity AiStatusSeverity => AiUnavailable ? Severity.Danger : AiLimitReached ? Severity.Warning : Severity.Ok;

    private bool _aiStatusChecking;

    partial void OnAiEnabledChanged(bool value)
    {
        if (value)
        {
            _ = CheckAiStatusAsync(); // включили — тихо проверим состояние
        }
    }

    /// <summary>Лёгкая проверка доступности ИИ → обновляет плашку (работает/лимит/нет доступа). Защита от параллельных запусков.</summary>
    private async Task CheckAiStatusAsync()
    {
        if (_aiStatusChecking)
        {
            return;
        }

        _aiStatusChecking = true;
        try
        {
            var result = await _aiAssistant.AskAsync("Ответь одним словом: OK").ConfigureAwait(true);
            if (!AiEnabled)
            {
                return; // пока проверяли — выключили; статус не трогаем
            }

            AiLimitReached = result.LimitReached;
            AiUnavailable = !result.Success && !result.LimitReached; // не успех и не лимит → нет связи/недоступен
            AiResetHint = result.RetryAfter ?? string.Empty;
            if (result.Success && result.Provider is { Length: > 0 } provider)
            {
                AiActiveModel = provider;
            }
        }
        catch (Exception)
        {
            AiUnavailable = true; // на всякий случай — не показываем зелёный, если что-то пошло не так
        }
        finally
        {
            _aiStatusChecking = false;
        }
    }

    /// <summary>Через сколько ИИ снова станет доступен (из ответа Gemini), напр. «30 сек»; пусто — неизвестно.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiStatusTooltip))]
    private string _aiResetHint = string.Empty;

    /// <summary>Подсказка к плашке статуса ИИ (при наведении): доступен / лимит + сколько до сброса + про свой ключ.</summary>
    public string AiStatusTooltip => AiUnavailable
        ? "Сейчас все модели заняты или достигли лимита — поэтому ИИ временно не отвечает. ИИ автоматически переключается между моделями; как только одна освободится, заработает снова. Можно подключить свой ключ или платную модель в разделе «Нейросети». Программа полностью работает и без ИИ."
        : AiLimitReached
            ? (string.IsNullOrEmpty(AiResetHint)
                ? "Лимит бесплатного тарифа исчерпан — ИИ временно не используется. Лимит обновляется периодически. Можно вставить свой ключ в разделе «Нейросети» — будет отдельный лимит."
                : $"Лимит исчерпан. ИИ снова заработает примерно через {AiResetHint}. Или вставь свой ключ в разделе «Нейросети» — будет отдельный лимит.")
            : "ИИ-помощник доступен. Объясняет непонятные находки простыми словами.";

    public MainWindowViewModel(
        IScanOrchestrator orchestrator,
        IEnumerable<IScanner> scanners,
        IFixOrchestrator fixOrchestrator,
        IFixFactory fixFactory,
        IWhitelist whitelist,
        IRestorePointService restore,
        IDeviceDriverAction deviceAction,
        IFileReputationCheck reputationCheck,
        IElevationService elevation,
        IAiAssistant aiAssistant,
        IReadOnlyList<NamedSearchProvider> searchProviders,
        IRebootRollbackScheduler rebootRollback,
        IStressTestEngine stressEngine,
        IInstalledProgramsProbe installedPrograms,
        IProgramUninstaller uninstaller,
        IForceDeleteService forceDelete,
        IMemoryOptimizer memoryOptimizer,
        InstallMonitor installMonitor,
        IActivityStatsStore activityStats,
        IStartupProgramRemover startupRemover,
        ILeftoverService leftovers,
        ILeftoverPrompt leftoverPrompt,
        IAppIconLoader iconLoader,
        IUpdateService updateService)
    {
        _activityStats = activityStats;
        _startupRemover = startupRemover;
        _updateService = updateService;
        _orchestrator = orchestrator;
        _scanners = scanners.ToList();
        _fixOrchestrator = fixOrchestrator;
        _fixFactory = fixFactory;
        _whitelist = whitelist;
        _restore = restore;
        _deviceAction = deviceAction;
        _onlineReputation = new OnlineReputationChecker(reputationCheck);
        _aiAssistant = aiAssistant;
        _rebootRollback = rebootRollback;
        IsAdministrator = elevation.IsAdministrator;
        StressTest = new StressTestViewModel(stressEngine, OnStressTestCompleted);
        Dashboard = new DashboardViewModel(installedPrograms, uninstaller, forceDelete, installMonitor, activityStats, leftovers, leftoverPrompt, iconLoader, aiAssistant, NavigateToSection);
        Optimize = new OptimizeViewModel(memoryOptimizer);

        // «Здоровье» показывает плитки СРАЗУ: до проверки — приглушённые плейсхолдеры «скоро» (правка 1086).
        foreach (var placeholder in HealthTiles.CreatePlaceholders())
        {
            HealthFindings.Add(CreateFindingViewModel(placeholder));
        }

        // Вкладки для всех доступных групп (по порядку) — заранее, без авто-скана.
        // «Здоровье» — не вкладка, а отдельный раздел слева (батарея/диски/температуры).
        foreach (var group in _scanners.Select(s => s.Group).Distinct()
                     .Where(static g => g != ScanGroup.Health)
                     .OrderBy(g => Array.IndexOf(GroupOrder, g)))
        {
            Groups.Add(new ScanGroupViewModel(group, GroupTitle(group), ScanGroupAsync));
        }

        if (Groups.Count > 0)
        {
            Groups[^1].IsLast = true; // у последнего блока связки-«палочки» справа нет
        }

        _selectedGroup = Groups.FirstOrDefault();
        if (_selectedGroup is not null)
        {
            _selectedGroup.IsActive = true; // присвоение поля не вызывает OnSelectedGroupChanged — отмечаем стартовую вкладку вручную
        }

        NavSections =
        [
            new NavSectionViewModel("dashboard", "Дашборд", "dashboard", SelectSection),
            new NavSectionViewModel("scans", "Сканы", "scan", SelectSection, isActive: true),
            new NavSectionViewModel("health", "Здоровье", "health", SelectSection),
            new NavSectionViewModel("tests", "Тесты", "cpu", SelectSection),
            new NavSectionViewModel("ai", "Нейросети", "ai", SelectSection),
            new NavSectionViewModel("backups", "Бэкапы", "backup", SelectSection),
            new NavSectionViewModel("about", "О программе", "about", SelectSection),
        ];

        // Раздел «Нейросети»: ДВЕ группы — поисковые сервисы (находят в сети) и языковые модели (обрабатывают).
        var aiKeyDir = Path.GetDirectoryName(App.GeminiUserKeyPath)!;

        // Поисковые модели: «Проверить» делает тестовый поиск — лампочка зелёная, если вернулись результаты.
        var searchMeta = new Dictionary<string, (string Url, string Desc)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tavily"] = ("https://app.tavily.com/home", "Tavily — поисковик для ИИ: находит свежие данные в интернете (драйверы, версии, ссылки). Основной."),
            ["Serper"] = ("https://serper.dev/api-key", "Serper — поиск через выдачу Google. Запасной к Tavily."),
        };
        for (var i = 0; i < searchProviders.Count; i++)
        {
            var provider = searchProviders[i];
            var (url, desc) = searchMeta.GetValueOrDefault(provider.Name, ("", $"{provider.Name} — поисковый сервис."));
            var search = provider.Search;
            Func<CancellationToken, Task<AiResult>> check = async ct =>
            {
                var results = await search.SearchAsync("nvidia geforce driver latest version", 1, ct).ConfigureAwait(true);
                return results.Count > 0 ? AiResult.Ok("ok") : AiResult.Fail("нет результатов");
            };
            SearchModels.Add(new AiModelViewModel(provider.Name, i == 0 ? "основной поиск" : "запасной поиск",
                desc, Path.Combine(aiKeyDir, provider.Name.ToLowerInvariant() + ".key"), url, OpenUrl, check));
        }

        // Языковые модели (порядок цепочки): Groq → Mistral → Gemini. «Проверить» — крошечный запрос модели.
        var langProviders = (_aiAssistant as WebAugmentedAiAssistant)?.Providers
                            ?? (_aiAssistant as FallbackAiAssistant)?.Providers
                            ?? (IReadOnlyList<IAiAssistant>)[];
        Func<CancellationToken, Task<AiResult>>? LangCheck(string name)
        {
            var provider = langProviders.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            return provider is null ? null : ct => provider.AskAsync("Ответь одним словом: OK", null, ct);
        }

        // Порядок (правки 924/935): Gemini всегда первая; бесплатные сначала, платная (GPT-4o через apiglue) — ПОСЛЕДНЯЯ.
        LanguageModels.Add(new AiModelViewModel("Gemini", "1-я модель · бесплатно",
            "Google Gemini — основная, умная, бесплатная (дневной лимит). Отвечает первой.",
            Path.Combine(aiKeyDir, "gemini.key"), "https://aistudio.google.com/apikey", OpenUrl, LangCheck("Gemini")));
        LanguageModels.Add(new AiModelViewModel("ChatGPT", "2-я модель · бесплатно",
            "ChatGPT (GPT-OSS через OpenRouter) — умная, бесплатная. Включается, если Gemini занята.",
            Path.Combine(aiKeyDir, "openrouter.key"), "https://openrouter.ai/keys", OpenUrl, LangCheck("ChatGPT")));
        LanguageModels.Add(new AiModelViewModel("GPT-4o", "3-я модель · платная",
            "GPT-4o · включается, когда бесплатные не ответили.",
            Path.Combine(aiKeyDir, "apiglue.key"), "https://app.apiglue.ru/dashboard/keys", OpenUrl, LangCheck("GPT-4o")));

        FilterOptions =
        [
            new FilterOption("Все", FindingFilter.All, Dot(StatusColors.Neutral)),
            new FilterOption("Проблемы", FindingFilter.Problems, Dot(StatusColors.Danger)),
            new FilterOption("Внимание", FindingFilter.Warnings, Dot(StatusColors.Warn)),
            new FilterOption("Советы", FindingFilter.Advice, Dot(StatusColors.Info)),
            new FilterOption("Исправлено", FindingFilter.Fixed, Dot(StatusColors.Fixed)),
        ];
        _selectedFilter = FilterOptions[0];

        // Следим за интернетом (подключили кабель — плашка «нет интернета» сама исчезает, и наоборот). Механика — в ConnectivityWatcher.
        _connectivity = new ConnectivityWatcher(online => IsOffline = !online);
        _connectivity.Start();

        // Тихая проверка обновления при запуске: если вышла новая версия — сама покажет плашку (правка Ивана 1250/1252).
        _ = CheckUpdateAsync(silent: true);
    }

    public ObservableCollection<NavSectionViewModel> NavSections { get; }

    public ObservableCollection<ScanGroupViewModel> Groups { get; } = [];

    public IReadOnlyList<FilterOption> FilterOptions { get; }

    /// <summary>Находки выбранной вкладки с учётом фильтра — плоский список (для выделения/починки).</summary>
    public ObservableCollection<FindingViewModel> VisibleFindings { get; } = [];

    /// <summary>Те же находки, сгруппированные по подсекциям (для отображения списком с подзаголовками).</summary>
    public ObservableCollection<FindingSectionViewModel> VisibleSections { get; } = [];

    /// <summary>Раздел проверен, но показывать нечего — вместо пустоты даём понятную плашку (а не «вдруг не сработало»).</summary>
    public bool ShowEmptyGroupNotice => SelectedGroup is { IsScanned: true } && VisibleFindings.Count == 0;

    /// <summary>В разделе есть пункты (проблемы/внимание/советы), но они скрыты текущим фильтром (а не «всё хорошо»).</summary>
    private bool CurrentGroupHasOpenIssues =>
        SelectedGroup is not null &&
        SelectedGroup.Findings.Any(f => f.EffectiveSeverity is Severity.Danger or Severity.Warning or Severity.Info);

    public string EmptyGroupTitle =>
        CurrentGroupHasOpenIssues ? "Под этим фильтром здесь пусто" : "В этом разделе всё в порядке";

    public string EmptyGroupHint =>
        CurrentGroupHasOpenIssues
            ? "Нажми «Все» вверху — в этом разделе есть другие пункты."
            : "Чинить нечего — здесь чисто и безопасно.";

    /// <summary>Сделанные бэкапы, сгруппированные по виду (для раздела «Бэкапы» и отката).</summary>
    public ObservableCollection<BackupGroupViewModel> BackupGroups { get; } = [];

    /// <summary>Есть ли хоть один бэкап (для пустого состояния раздела «Бэкапы»).</summary>
    public bool HasBackups => BackupGroups.Count > 0;

    public bool IsScans => ActiveSection == "scans";

    /// <summary>Открыта вкладка «Мусор» — показываем чипы-навигацию по подкатегориям (правка 1071).</summary>
    public bool IsJunkSection => IsScans && SelectedGroup?.Group == ScanGroup.Junk && VisibleSections.Count > 0;

    /// <summary>Показывать чипы-навигацию по подсекциям — в любом разделе, где есть 2+ подсекции с заголовками
    /// («Мусор», «Автозапуск» и т.п.), а не только в «Мусоре» (запрос Ивана 1124).</summary>
    public bool HasSectionChips => IsScans && VisibleSections.Count(s => s.HasTitle) >= 2;

    public bool IsDashboard => ActiveSection == "dashboard";

    public bool IsUninstall => ActiveSection == "uninstall";

    public bool IsCompare => ActiveSection == "compare";

    public bool IsForceDelete => ActiveSection == "forcedelete";

    public bool IsOptimize => ActiveSection == "optimize";

    public bool IsHealth => ActiveSection == "health";

    public bool IsTests => ActiveSection == "tests";

    public bool IsBackups => ActiveSection == "backups";

    public bool IsAbout => ActiveSection == "about";

    public bool IsAiSettings => ActiveSection == "ai";

    /// <summary>Модели ИИ в порядке цепочки — для раздела «Нейросети» (показ + свой ключ у каждой).</summary>
    /// <summary>Поисковые сервисы (Tavily, Serper) — находят инфу в интернете.</summary>
    public ObservableCollection<AiModelViewModel> SearchModels { get; } = [];

    /// <summary>Языковые модели (Groq, Mistral, Gemini) — обрабатывают найденное и формируют ответ.</summary>
    public ObservableCollection<AiModelViewModel> LanguageModels { get; } = [];

    public bool HasSearchModels => SearchModels.Count > 0;

    public bool HasLanguageModels => LanguageModels.Count > 0;

    /// <summary>Здоровье ПК: батарея, диски (SMART), температуры — для раздела «Здоровье».</summary>
    public ObservableCollection<FindingViewModel> HealthFindings { get; } = [];

    /// <summary>Раздел «Тесты»: проверка под нагрузкой (стресс-тест) с живой шкалой и вердиктом.</summary>
    public StressTestViewModel StressTest { get; }

    /// <summary>Раздел «Дашборд»: плитки-переходы + удаление программ с чисткой остатков + грубое удаление.</summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>Раздел «Оптимизация»: честная память + безопасное закрытие фоновых процессов.</summary>
    public OptimizeViewModel Optimize { get; }

    /// <summary>
    /// Итог проверки под нагрузкой попадает в «Здоровье» отдельной плиткой «Проверка под нагрузкой»
    /// (заменяя прошлый результат). Вызывается уже в UI-потоке после завершения теста.
    /// </summary>
    private void OnStressTestCompleted(StressTestResult result)
    {
        var maxCpu = result.MaxCpuCelsius is int c ? $"{c} °C" : null;
        var data = new Dictionary<string, string> { [FindingDataKeys.HealthIcon] = "cpu" };
        if (maxCpu is not null)
        {
            data["metric"] = maxCpu;
            data["metricLabel"] = "макс";
        }

        var finding = new Finding
        {
            Id = "health-stress",
            Group = ScanGroup.Health,
            Severity = result.Severity,
            Title = "Проверка под нагрузкой",
            Detail = maxCpu is not null ? $"пик: {maxCpu}" : null,
            Explain = result.Verdict,
            Data = data,
        };

        // Убираем и прошлый реальный результат, и приглушённый плейсхолдер «ph-stress» — иначе плитка дублируется.
        foreach (var stale in HealthFindings.Where(f => f.Finding.Id is "health-stress" or "ph-stress").ToList())
        {
            HealthFindings.Remove(stale);
        }

        HealthFindings.Insert(0, CreateFindingViewModel(finding));
        HealthScanned = true;
    }

    /// <summary>Была ли проверка (есть ли данные о здоровье). Нет → показываем «сначала просканируйте».</summary>
    [ObservableProperty]
    private bool _healthScanned;

    private void SelectSection(NavSectionViewModel nav)
    {
        ActiveSection = nav.Key;
        foreach (var section in NavSections)
        {
            section.IsActive = section.Key == nav.Key;
        }

        if (nav.Key == "backups")
        {
            _ = LoadBackupsAsync();
        }

        // «Нейросети» НЕ проверяем автоматически — статус по кнопке «Проверить» (чтобы не жечь лимит моделей зря).
    }

    /// <summary>Переключиться на раздел по ключу. Для плиток «Дашборда»: «scans»/«health»/«tests» (есть в рельсе)
    /// и «uninstall»/«optimize» (в рельсе НЕТ — открываются только плиткой, поэтому переключаем напрямую).</summary>
    public void NavigateToSection(string key)
    {
        var nav = NavSections.FirstOrDefault(s => s.Key == key);
        if (nav is not null)
        {
            SelectSection(nav);
            return;
        }

        // Раздел без пункта в рельсе (Удаление программ / Оптимизация) — переключаем сами и подсвечиваем
        // «Дашборд» как родителя. Иначе клик по плитке «ничего не делал» (баг живого теста 1089).
        ActiveSection = key;
        foreach (var section in NavSections)
        {
            section.IsActive = section.Key == "dashboard";
        }

        // Перечитываем список программ при КАЖДОМ входе в раздел (а не только при первом) — иначе список
        // остаётся устаревшим, когда пользователь ставит/сносит программу, не закрывая Aegis (баг 1223).
        if (key == "uninstall" && !Dashboard.IsLoading)
        {
            _ = Dashboard.LoadCommand.ExecuteAsync(null);
        }

        if (key == "optimize")
        {
            _ = Optimize.RefreshCommand.ExecuteAsync(null);
        }

        if (key == "compare")
        {
            Dashboard.RefreshStats();
        }
    }

    /// <summary>Escape в подразделе Дашборда (удаление/оптимизация/сравнить/занятый файл) → назад на Дашборд (правка Ивана 1168).</summary>
    public bool TryReturnToDashboard()
    {
        if (ActiveSection is "uninstall" or "optimize" or "compare" or "forcedelete")
        {
            NavigateToSection("dashboard");
            return true;
        }

        return false;
    }

    /// <summary>Видимая кнопка «Назад к Дашборду» в разделах-плитках (кроме Escape — новичок его не знает, аудит 2026-07-03).</summary>
    [RelayCommand]
    private void BackToDashboard() => NavigateToSection("dashboard");

    // ===== Обновление программы через релизы GitHub (правка Ивана 1250/1252) =====

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateBannerText))]
    private bool _updateAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateBannerText))]
    private string _updateVersion = string.Empty;

    [ObservableProperty]
    private string _updateNotes = string.Empty;

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _updateProgress;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    private UpdateInfo? _pendingUpdate;

    public string CurrentVersionText =>
        "Версия " + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?");

    public string UpdateBannerText => $"Доступна новая версия {UpdateVersion}";

    /// <summary>Ручная кнопка «Проверить обновление».</summary>
    [RelayCommand]
    private Task CheckUpdateManuallyAsync() => CheckUpdateAsync(silent: false);

    private async Task CheckUpdateAsync(bool silent)
    {
        if (IsCheckingUpdate || IsUpdating)
        {
            return;
        }

        IsCheckingUpdate = true;
        if (!silent)
        {
            UpdateStatus = "Проверяю обновление…";
        }

        try
        {
            var info = await _updateService.CheckForUpdateAsync().ConfigureAwait(true);
            _pendingUpdate = info;
            if (info is not null)
            {
                UpdateVersion = info.Version;
                UpdateNotes = info.Notes ?? string.Empty;
                UpdateAvailable = true;
                UpdateStatus = string.Empty;
            }
            else
            {
                UpdateAvailable = false;
                if (!silent)
                {
                    UpdateStatus = "У вас последняя версия.";
                }
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                UpdateStatus = "Не удалось проверить обновление: " + ex.Message;
            }
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    /// <summary>«Обновить» — скачать новый .exe и установить (программа перезапустится).</summary>
    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (_pendingUpdate is null || IsUpdating)
        {
            return;
        }

        IsUpdating = true;
        UpdateProgress = 0;
        UpdateStatus = "Скачиваю обновление…";
        try
        {
            var progress = new Progress<double>(p => Dispatcher.UIThread.Post(() => UpdateProgress = p));
            var error = await _updateService.DownloadAndApplyAsync(_pendingUpdate, progress).ConfigureAwait(true);
            // Без ошибки — приложение уже закрывается и стартует новая версия. С ошибкой — показываем текст.
            if (error is not null)
            {
                UpdateStatus = error;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = "Не удалось обновить: " + ex.Message;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    /// <summary>«Позже» — скрыть плашку обновления до следующего запуска.</summary>
    [RelayCommand]
    private void DismissUpdate() => UpdateAvailable = false;

    /// <summary>Из раздела «Здоровье» (когда данных ещё нет) — перейти в «Сканы» и запустить полную проверку.</summary>
    [RelayCommand]
    private async Task ScanFromHealthAsync()
    {
        var scans = NavSections.FirstOrDefault(n => n.Key == "scans");
        if (scans is not null)
        {
            SelectSection(scans);
        }

        await ScanAllAsync().ConfigureAwait(true);
    }

    private async Task LoadBackupsAsync()
    {
        BackupGroups.Clear();
        try
        {
            var records = await Task.Run(() => _restore.ListBackupsAsync()).ConfigureAwait(true);
            var grouped = records
                .Select(r => new BackupItemViewModel(r, RestoreBackupAsync))
                .GroupBy(i => i.Section)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                // Внутри группы — новые сверху (даты уже отсортированы в сервисе).
                BackupGroups.Add(new BackupGroupViewModel(group.Key, group));
            }

            OnPropertyChanged(nameof(HasBackups));
            if (BackupGroups.Count == 0)
            {
                StatusText = "Бэкапов пока нет — они появятся после первой починки.";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось загрузить бэкапы: " + ex.Message;
        }
    }

    private async Task RestoreBackupAsync(BackupItemViewModel item)
    {
        try
        {
            await Task.Run(() => _restore.RestoreAsync(item.Id)).ConfigureAwait(true);
            StatusText = "Откат выполнен: " + item.Description;
            await LoadBackupsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось откатить: " + ex.Message;
        }
    }

    /// <summary>Откат именно ЭТОЙ правки по её бэкапу (кнопка «Вернуть» у исправленной находки). Возвращает её вид как было.</summary>
    private async Task UndoFixAsync(FindingViewModel finding)
    {
        if (string.IsNullOrEmpty(finding.BackupId))
        {
            return;
        }

        try
        {
            await Task.Run(() => _restore.RestoreAsync(finding.BackupId)).ConfigureAwait(true);
            finding.IsFixed = false;       // снова показываем как находку (правка отменена)
            finding.BackupId = null;       // отката больше нет
            StatusText = $"Возвращено как было: «{finding.Title}».";
            RefreshAllCounts();
            await LoadBackupsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось вернуть: " + ex.Message;
        }
    }

    /// <summary>Модель реально ответила на «Спросить AI» → шапка показывает её же значок/статус (правка 953).</summary>
    private void OnAiAnswered(string model)
    {
        AiActiveModel = model;
        AiLimitReached = false;
        AiUnavailable = false;
    }

    /// <summary>Перезагрузка/переустановка выбранных драйверов (правка 930). Перед переустановкой — точка восстановления.</summary>
    private async Task DriverActionAsync(FindingViewModel finding, bool reinstall)
    {
        var deviceIds = finding.DriverEntries
            .Where(entry => entry.IsSelected && entry.CanAct && entry.DeviceId is not null)
            .Select(entry => entry.DeviceId!)
            .ToList();
        if (deviceIds.Count == 0)
        {
            StatusText = "Не выбрано ни одного драйвера — поставь галочки.";
            return;
        }

        // Перед РИСКОВАННОЙ переустановкой — точка восстановления. Перезагрузка драйвера безопасна (без неё).
        if (reinstall)
        {
            await Task.Run(() => _restore.CreateRestorePointAsync(
                $"Переустановка драйверов ({deviceIds.Count})", CancellationToken.None)).ConfigureAwait(true);
        }

        var okCount = 0;
        string? lastError = null;
        foreach (var deviceId in deviceIds)
        {
            var result = reinstall
                ? await _deviceAction.ReinstallAsync(deviceId).ConfigureAwait(true)
                : await _deviceAction.RestartAsync(deviceId).ConfigureAwait(true);
            if (result.Success)
            {
                okCount++;
            }
            else
            {
                lastError = result.Message;
            }
        }

        var verb = reinstall ? "Переустановлено" : "Перезагружено";
        StatusText = okCount == deviceIds.Count
            ? $"{verb} драйверов: {okCount}."
            : $"{verb}: {okCount} из {deviceIds.Count}." + (lastError is not null ? " " + lastError : string.Empty);
    }

    /// <summary>
    /// Удаление выбранных элементов из большой папки: в Корзину (обратимо) либо навсегда (по явному выбору
    /// пользователя в меню — с предупреждением). По умолчанию галочки сняты, так что удаляется только отмеченное.
    /// </summary>
    private async Task FolderActionAsync(FindingViewModel finding, bool permanent)
    {
        var entries = finding.SelectedFolderEntries;
        if (entries.Count == 0)
        {
            StatusText = "Не выбрано ни одного файла — поставь галочки в списке.";
            return;
        }

        var synthetic = new Finding
        {
            Id = "folder-items-" + finding.Finding.Id,
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = "Удаление выбранного из папки",
            Explain = string.Empty,
            Data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.FolderItemsDelete,
                ["paths"] = string.Join('|', entries.Select(e => e.Path)),
            },
        };

        var fix = _fixFactory.CreateFix(synthetic, permanent);
        if (fix is null)
        {
            return;
        }

        var label = permanent ? $"Удаление навсегда ({entries.Count})" : $"Удаление в Корзину ({entries.Count})";
        var result = await Task.Run(() => _fixOrchestrator.ApplyAsync([fix], label)).ConfigureAwait(true);
        if (result.Aborted)
        {
            StatusText = result.Message ?? "Не удалось — ничего не удалено.";
            return;
        }

        if (result.Outcomes.Count > 0 && result.Outcomes[0].Success)
        {
            foreach (var entry in entries)
            {
                entry.IsRemoved = true;
                entry.IsSelected = false;
            }

            StatusText = permanent
                ? $"Удалено навсегда: {entries.Count}."
                : $"Перемещено в Корзину: {entries.Count} — вернуть можно оттуда.";
        }
        else
        {
            StatusText = "Не удалось удалить выбранное (заняты, нет прав или отменено).";
        }
    }

    /// <summary>Открыть файл его программой по умолчанию (а папку — в проводнике): клик по элементу содержимого.</summary>
    private void OpenItem(string path)
    {
        if (ExternalOpener.Open(path) is { } error)
        {
            StatusText = "Не удалось открыть: " + error;
        }
    }

    /// <summary>Пересчитать цветные счётчики на всех вкладках (после онлайн-проверки/пометок «Безопасно»).</summary>
    private void RefreshAllCounts()
    {
        foreach (var group in Groups)
        {
            group.NotifyCounts();
        }

        OnPropertyChanged(nameof(HasJunkTotal));   // обновить суммы мусора после скана/изменений (правка 946/960)
        OnPropertyChanged(nameof(JunkSafeLabel));
        OnPropertyChanged(nameof(JunkTotalLabel));
    }

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        // Не сканируем во время починки: иначе список находок подменится под уже идущей правкой.
        if (IsScanning || IsApplyingFixes)
        {
            return;
        }

        IsScanning = true;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        foreach (var group in Groups)
        {
            group.IsScanning = true;
            group.ScanPhase = TabScanPhase.Scanning; // все блоки сразу «идёт» — сканеры работают параллельно
        }

        StatusText = "Идёт полная проверка компьютера…";

        try
        {
            // Progress создаётся на UI-потоке → обновления приходят на UI-поток. Шкала сканирования
            // «по блокам»: текущий раздел — синий (Scanning), пройденные — зелёные (Done), остальные — пусто.
            // КЛЮЧЕВОЕ: каждую вкладку наполняем СРАЗУ, как её сканер завершился (а не все в самом конце),
            // чтобы при переключении вкладок во время долгой проверки они не выглядели пустыми.
            var accumulated = new Dictionary<ScanGroup, List<Finding>>();
            var progress = new Progress<ScanProgress>(p =>
            {
                if (p.IsComplete)
                {
                    // Конец проверки — блоки в нейтральный вид (НЕ оставляем зелёным: иначе подсветка «застревает»).
                    StatusText = "Проверка завершена.";
                    foreach (var g in Groups)
                    {
                        g.ScanPhase = TabScanPhase.Idle;
                    }

                    return;
                }

                if (p.JustCompleted is { } done)
                {
                    if (!accumulated.TryGetValue(done.Group, out var list))
                    {
                        list = [];
                        accumulated[done.Group] = list;
                    }

                    list.AddRange(done.Findings);

                    // Наполняем вкладку/«Здоровье» ОДИН раз — когда ВСЯ группа проверена (а не на каждый сканер):
                    // иначе для группы из 8 сканеров список пересоздавался бы 8 раз (лишние аллокации/подтормаживание).
                    if (p.GroupComplete)
                    {
                        if (done.Group == ScanGroup.Health)
                        {
                            // «Проверка под нагрузкой» появляется только после стресс-теста — пока его не запускали,
                            // добавляем приглушённый плейсхолдер В ОБЩИЙ список ДО сортировки, чтобы он встал рядом
                            // с загрузкой процессора (та же группа), а не в конце (правки 1086/1104).
                            var tiles = list.ToList();
                            // Если стресс-тест уже запускали — СОХРАНЯЕМ его реальный результат (иначе скан затрёт его
                            // плейсхолдером и плитка задвоится). Иначе добавляем приглушённый плейсхолдер.
                            var realStress = HealthFindings.FirstOrDefault(f => f.Finding.Id == "health-stress")?.Finding;
                            if (realStress is not null)
                            {
                                tiles.Add(realStress);
                            }
                            else if (list.All(f => f.Id != "health-stress"))
                            {
                                tiles.Add(HealthTiles.CreatePlaceholders().First(p => p.Id == "ph-stress"));
                            }

                            HealthFindings.Clear();
                            // Группируем показатели по компоненту (процессор → память → диски → батарея…),
                            // чтобы плитки одной детали стояли рядом; внутри группы — сначала тревожное.
                            foreach (var finding in tiles.OrderBy(HealthTiles.Order).ThenByDescending(f => f.Severity))
                            {
                                HealthFindings.Add(CreateFindingViewModel(finding));
                            }

                            HealthScanned = true;
                        }
                        else
                        {
                            var groupVm = Groups.FirstOrDefault(g => g.Group == done.Group);
                            if (groupVm is not null)
                            {
                                groupVm.SetFindings(list.Select(CreateFindingViewModel));
                                groupVm.ScanPhase = TabScanPhase.Done; // блок «готов» (зелёный)
                                if (ReferenceEquals(SelectedGroup, groupVm))
                                {
                                    RefreshVisibleFindings();
                                }
                            }
                        }
                    }

                    StatusText = $"Идёт проверка… найдено: {p.FindingsSoFar}";
                }
            });
            await Task.Run(() => _orchestrator.ScanAllAsync(progress, token), token).ConfigureAwait(true);
            HealthScanned = true; // если Health-сканеров нет/пусто — раздел всё равно «проверен»

            RefreshVisibleFindings();

            // Автоматическая онлайн-проверка неподписанных файлов; чистые станут зелёными.
            await _onlineReputation.AutoCheckAsync(Groups.SelectMany(g => g.Findings), s => StatusText = s, token).ConfigureAwait(true);
            RefreshAllCounts(); // онлайн-проверка перекрасила часть находок в зелёное — обновить счётчики-флажки
            RefreshVisibleFindings();
            StatusText = "Проверка завершена.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Проверка отменена."; // пользователь нажал «Стоп» — это не ошибка
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось выполнить проверку: " + ex.Message;
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            IsScanning = false;
            foreach (var group in Groups)
            {
                group.IsScanning = false;
                group.ScanPhase = TabScanPhase.Idle; // всё просканировано → блоки возвращаются в нейтральный вид
            }
        }
    }

    private async Task ScanGroupAsync(ScanGroupViewModel groupVm)
    {
        if (groupVm.IsScanning || IsScanning || IsApplyingFixes)
        {
            return;
        }

        IsScanning = true; // глобальный флаг занятости: пока идёт скан раздела, «Проверить всё» недоступно
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        groupVm.IsScanning = true;
        StatusText = $"Проверяю раздел «{groupVm.Title}»…";

        try
        {
            await RunGroupScanAsync(groupVm, _scanCts.Token).ConfigureAwait(true);
            StatusText = $"Раздел «{groupVm.Title}» проверен — найдено: {groupVm.Count}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Проверка раздела «{groupVm.Title}» отменена.";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось проверить «{groupVm.Title}»: {ex.Message}";
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            IsScanning = false;
            groupVm.IsScanning = false;
            groupVm.ScanPhase = TabScanPhase.Idle;
        }
    }

    /// <summary>Ядро проверки одного раздела: синий→зелёный по блоку, находки, онлайн-проверка. Без сброса фазы и флага (за это отвечает вызывающий).</summary>
    private async Task RunGroupScanAsync(ScanGroupViewModel groupVm, CancellationToken cancellationToken)
    {
        groupVm.ScanPhase = TabScanPhase.Scanning;
        var findings = new List<Finding>();
        var groupScanners = _scanners.Where(s => s.Group == groupVm.Group).ToList();
        for (var i = 0; i < groupScanners.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await Task.Run(() => groupScanners[i].ScanAsync(cancellationToken), cancellationToken).ConfigureAwait(true);
            findings.AddRange(result.Findings);
        }

        groupVm.ScanPhase = TabScanPhase.Done;
        groupVm.SetFindings(findings.Select(CreateFindingViewModel));
        if (ReferenceEquals(SelectedGroup, groupVm))
        {
            RefreshVisibleFindings();
        }

        // Автоматическая онлайн-проверка неподписанных файлов этого раздела; чистые станут зелёными.
        await _onlineReputation.AutoCheckAsync(groupVm.Findings, s => StatusText = s, cancellationToken).ConfigureAwait(true);
        groupVm.NotifyCounts(); // онлайн-проверка перекрасила часть находок — обновить флажки-счётчики
        if (ReferenceEquals(SelectedGroup, groupVm))
        {
            RefreshVisibleFindings();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        // Тумблер: если что-то выбрано — снять всё; иначе — выделить всё, что можно выбрать галочкой
        // (совпадает с видимостью чекбокса: исправимое + «Безопасно», кроме noBatch-остатков).
        var anySelected = VisibleFindings.Any(f => f.IsSelected);
        foreach (var finding in VisibleFindings.Where(f => f.CanBatchSelect && !f.IsFixed))
        {
            finding.IsSelected = !anySelected;
        }
    }

    private void OnFindingSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FindingViewModel.IsSelected))
        {
            IsAllSelected = VisibleFindings.Any(f => f.IsSelected);
            OnPropertyChanged(nameof(HasFixableSelected));
            OnPropertyChanged(nameof(HasWhitelistableSelected));
        }
    }

    [RelayCommand]
    private void MarkSelectedSafe()
    {
        var targets = VisibleFindings.Where(f => f.IsSelected && f.CanWhitelist && !f.IsMarkedSafe).ToList();
        if (targets.Count == 0)
        {
            StatusText = "Отметь галочками то, что хочешь пометить безопасным.";
            return;
        }

        foreach (var target in targets)
        {
            _whitelist.Add(target.WhitelistKey);
            target.IsMarkedSafe = true;
            target.IsSelected = false;
        }

        SelectedGroup?.NotifyCounts();
        RefreshVisibleFindings(); // помеченные «Безопасно» уходят из текущего фильтра (кроме «Все»)
        StatusText = $"Помечено «Безопасно»: {targets.Count}.";
    }

    [RelayCommand]
    private async Task FixSelectedAsync()
    {
        var targets = VisibleFindings.Where(f => f.IsSelected && f.CanFix && !f.IsFixed).ToList();
        if (targets.Count == 0)
        {
            StatusText = "Отметь галочками, что исправить (галочки есть у пунктов с готовым исправлением).";
            return;
        }

        await ApplyFixesAsync(targets, $"Исправление выбранного ({targets.Count})").ConfigureAwait(true);
    }

    private async Task FixOneAsync(FindingViewModel finding)
    {
        await ApplyFixesAsync([finding], "Починка: " + finding.Title).ConfigureAwait(true);
    }

    private async Task ApplyFixesAsync(IReadOnlyList<FindingViewModel> targets, string description, bool forcePermanent = false)
    {
        // Защита от повторного входа: пока идёт одна починка, вторую не запускаем (иначе второй вызов
        // перетёр бы _fixCts первого — ломалась «Отмена» и состояние). Кнопки тоже заблокированы по IsApplyingFixes.
        if (IsApplyingFixes)
        {
            StatusText = "Дождись окончания текущей операции.";
            return;
        }

        // Не чиним во время сканирования: находки ещё меняются — правка могла бы примениться не к тому.
        if (IsScanning)
        {
            StatusText = "Дождись окончания проверки.";
            return;
        }

        // Удаления файлов/папок мусора — спросить, как удалять: отменить / в Корзину / навсегда.
        // forcePermanent (быстрая чистка) — без диалога, сразу навсегда (это безопасный кэш/мусор).
        var permanent = forcePermanent;
        var deleteCount = targets.Count(IsDeleteTarget);
        if (deleteCount > 0 && !forcePermanent)
        {
            var choice = await AskDeleteChoiceAsync(deleteCount).ConfigureAwait(true);
            if (choice == DeleteChoice.Cancel)
            {
                StatusText = "Удаление отменено.";
                return;
            }

            permanent = choice == DeleteChoice.Permanent;
        }

        // Свободно до — чтобы показать «освобождено N» (только при удалении НАВСЕГДА: в Корзину место не освобождает).
        var freeBefore = deleteCount > 0 && permanent ? TotalFixedDriveFreeSpace() : -1L;

        foreach (var target in targets)
        {
            target.IsFixing = true;
            target.FixProgress = 0;
        }

        // У долгих SFC/DISM есть НАСТОЯЩИЙ прогресс (читаем % из их вывода) — тогда таймер лишь «оживляет»
        // кольцо в самом начале (низкий потолок 0.12), а дальше его ведёт реальный процент. У остальных правок
        // прогресса нет — кольцо быстро до ~25%, потом медленно ползёт к ~85% (не «быстро до края и виснет»).
        var hasLiveProgress = targets.Any(static t => t.Finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.SfcDismRepair);
        var floorCap = hasLiveProgress ? 0.12 : 0.85;
        var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        progressTimer.Tick += (_, _) =>
        {
            foreach (var t in targets)
            {
                var step = t.FixProgress < 0.25 ? 0.06 : 0.0015;
                t.FixProgress = Math.Max(t.FixProgress, Math.Min(floorCap, t.FixProgress + (floorCap - t.FixProgress) * step));
            }
        };
        progressTimer.Start();

        var cts = new CancellationTokenSource();
        _fixCts = cts;
        IsApplyingFixes = true; // показать кнопку «Отменить» (для долгих SFC/DISM)
        if (hasLiveProgress)
        {
            StatusText = "Идёт восстановление системных файлов Windows — это может занять несколько минут. Не закрывай программу.";
        }

        try
        {
            // Пара target↔fix, чтобы результаты не «съехали», если для какой-то находки фабрика вернёт null.
            // permanent действует только на правки-удаления; прочие правки флаг игнорируют.
            var pairs = targets
                .Select(t => (target: t, fix: _fixFactory.CreateFix(t.Finding, permanent)))
                .Where(p => p.fix is not null)
                .Select(p => (p.target, fix: p.fix!))
                .ToList();

            // Долгие правки с живым прогрессом (SFC/DISM) крутят своё кольцо по реальному проценту.
            // Progress создаётся на UI-потоке → обновления приходят на UI-поток. Math.Max — только вперёд.
            foreach (var (target, fix) in pairs)
            {
                if (fix is IProgressReportingFix reporting)
                {
                    var captured = target;
                    reporting.Progress = new Progress<double>(p => captured.FixProgress = Math.Max(captured.FixProgress, Math.Min(1.0, p)));
                }
            }

            var token = _fixCts.Token;
            var result = await Task.Run(() => _fixOrchestrator.ApplyAsync(
                pairs.Select(p => p.fix).ToList(), description, cancellationToken: token), token).ConfigureAwait(true);
            foreach (var t in targets)
            {
                t.FixProgress = 1.0; // докрутить кольцо до полного при завершении
            }

            if (result.Aborted)
            {
                StatusText = result.Message ?? "Не удалось создать бэкап — изменения не внесены (так безопаснее).";
                return;
            }

            string? firstFailure = null;
            for (var i = 0; i < pairs.Count && i < result.Outcomes.Count; i++)
            {
                if (result.Outcomes[i].Success)
                {
                    pairs[i].target.IsFixed = true;
                    pairs[i].target.IsSelected = false;
                    pairs[i].target.BackupId = result.Outcomes[i].BackupId; // для кнопки «Вернуть» (откат именно этой правки)
                    RecordActivityStats(pairs[i].target); // копим статистику для «Сравнить состояние»
                }
                else
                {
                    firstFailure ??= result.Outcomes[i].Message;
                }
            }

            // Рискованная системная правка → запланировать проверку после перезагрузки: при следующем входе
            // программа спросит «всё работает?»; не подтвердят → авто-откат ИМЕННО этих правок по их бэкапам.
            // Планируем только когда есть что откатывать (обратимые бэкапы); необратимые (SFC/DISM) не в счёт.
            var reversibleBackupIds = result.Outcomes
                .Where(static o => o.Success && !string.IsNullOrEmpty(o.BackupId))
                .Select(static o => o.BackupId!)
                .ToList();
            if (reversibleBackupIds.Count > 0)
            {
                _rebootRollback.Schedule(reversibleBackupIds, description);
            }

            // Исправленные стали «зелёными» (EffectiveSeverity=Ok) → сразу обновить счётчики-флажки и список
            // (уходят из активного фильтра «Внимание»/«Советы»), чтобы «−1» происходило в реальном времени.
            SelectedGroup?.NotifyCounts();
            RefreshVisibleFindings();
            Dashboard.RefreshStats(); // обновить «Сравнить состояние» после починок

            var rebootNote = result.RequiresReboot ? " Часть изменений вступит в силу после перезагрузки." : string.Empty;
            var undoNote = deleteCount > 0 && permanent
                ? " Удалённые навсегда файлы вернуть нельзя."
                : " Откат: системные правки — в разделе «Бэкапы», удалённые файлы и папки — в Корзине Windows.";
            // «Освобождено N» — при удалении навсегда (в Корзину место не освобождает до её очистки).
            var freedNote = string.Empty;
            if (freeBefore >= 0)
            {
                var freed = Math.Max(0, TotalFixedDriveFreeSpace() - freeBefore);
                if (freed > 1024L * 1024)
                {
                    freedNote = $" Освобождено {HumanSize.Format(freed)}.";
                }
            }

            // Если что-то не получилось — показать ПОЧЕМУ (например, устройство, которое Windows не даёт включить).
            var failNote = !string.IsNullOrEmpty(firstFailure) ? " Не получилось: " + firstFailure : string.Empty;
            StatusText = $"Исправлено: {result.SuccessCount}." + failNote + freedNote
                + (failNote.Length == 0 ? undoNote : string.Empty) + rebootNote;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Операция отменена.";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось применить исправление: " + ex.Message;
        }
        finally
        {
            progressTimer.Stop();
            IsApplyingFixes = false;
            if (ReferenceEquals(_fixCts, cts))
            {
                _fixCts = null;
            }

            cts.Dispose();
            foreach (var target in targets)
            {
                target.IsFixing = false;
            }
        }
    }

    /// <summary>«Удалить полностью» программу автозапуска: снос программы (инсталлятор+чистка / папка в Корзину) + снятие записи автозапуска.</summary>
    private async Task DeleteStartupCompletelyAsync(FindingViewModel finding)
    {
        var exePath = finding.StartupExecutablePath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        StatusText = $"Удаляю «{finding.StartupDisplayName}» полностью…";
        try
        {
            // 1) Сносим саму программу (штатный деинсталлятор + чистка / папку в Корзину). Best-effort: файлы могли быть
            //    удалены раньше — тогда снос «не удастся», но это НЕ повод оставлять запись автозапуска висеть.
            var result = await Task.Run(() => _startupRemover.RemoveAsync(exePath, finding.StartupDisplayName)).ConfigureAwait(true);

            // 2) ГЛАВНОЕ: убираем саму запись автозапуска (Run-значение / ярлык) — это и есть цель «убрать из автозапуска».
            //    Делаем ВСЕГДА, даже если снос программы не удался (иначе запись оставалась висеть — баг 1244).
            var entryCleared = false;
            if (finding.CanFix && finding.FixCommand is not null)
            {
                await finding.FixCommand.ExecuteAsync(null).ConfigureAwait(true);
                entryCleared = finding.IsFixed;
            }

            if (entryCleared || result.Success)
            {
                finding.IsFixed = true;
            }

            // Счётчик «Удалено программ» увеличиваем ТОЛЬКО когда реально снесли программу, а не просто убрали
            // запись автозапуска (иначе статистика завышалась — аудит 2026-07-04).
            if (result.Success)
            {
                _activityStats.AddProgramsRemoved();
            }

            Dashboard.RefreshStats();
            SelectedGroup?.NotifyCounts();
            RefreshVisibleFindings();

            if (result.Success)
            {
                StatusText = $"«{finding.StartupDisplayName}» удалена полностью. {result.Message}";
                return;
            }

            // Снести не удалось (нет папки/деинсталлятора или папка защищена). Показываем ПОНЯТНОЕ окно с причиной
            // и предлагаем удалить через «Приложения» Windows — вместо бледной строки статуса, которую не видно (запрос Ивана).
            var title = entryCleared ? "Убрано из автозапуска, но папку удалить нельзя" : "Не удалось удалить программу";
            var openApps = await ShowMessageDialogAsync(title, result.Message, "Открыть «Приложения» Windows").ConfigureAwait(true);
            if (openApps)
            {
                ExternalOpener.Open("ms-settings:appsfeatures");
            }

            StatusText = entryCleared
                ? $"«{finding.StartupDisplayName}» убрана из автозапуска. {result.Message}"
                : $"«{finding.StartupDisplayName}»: {result.Message}";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось удалить полностью: " + ex.Message;
        }
    }

    /// <summary>Копит статистику успешной починки для раздела «Сравнить состояние» (мусор/угрозы/драйверы).</summary>
    private void RecordActivityStats(FindingViewModel target)
    {
        var finding = target.Finding;
        var kind = finding.Data?.GetValueOrDefault(FindingDataKeys.Kind);

        switch (finding.Group)
        {
            case ScanGroup.Junk:
                _activityStats.AddJunkCleaned(target.SizeBytes);
                break;
            case ScanGroup.Threats:
                _activityStats.AddThreatsNeutralized();
                break;
            case ScanGroup.Processes when kind == FindingKinds.ProcessStop:
                _activityStats.AddThreatsNeutralized();
                break;
        }

        if (kind == FindingKinds.DriverSearch)
        {
            _activityStats.AddDriversUpdated();
        }
    }

    /// <summary>Прервать долгую операцию (SFC/DISM) — завершает её процесс.</summary>
    [RelayCommand]
    private void CancelFix()
    {
        StatusText = "Отменяю…";
        _fixCts?.Cancel();
    }

    /// <summary>Прервать идущую проверку — по кнопке «Стоп» в центре кольца прогресса.</summary>
    [RelayCommand]
    private void CancelScan()
    {
        StatusText = "Останавливаю проверку…";
        _scanCts?.Cancel();
    }

    /// <summary>Быстрая чистка: одной кнопкой чистит безопасный кэш/мусор/пустышки разом и показывает, сколько освободил.</summary>
    [RelayCommand]
    private async Task QuickCleanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        // Нет данных — сначала полная проверка (нужны находки мусора).
        if (!HealthScanned)
        {
            await ScanAllAsync().ConfigureAwait(true);
        }

        var junk = Groups.FirstOrDefault(g => g.Group == ScanGroup.Junk);
        var targets = junk?.Findings.Where(static f => !f.IsFixed && IsQuickCleanSafe(f)).ToList() ?? [];
        if (targets.Count == 0)
        {
            StatusText = "Быстрая чистка: чистить нечего — кэша и мусора почти нет.";
            return;
        }

        // Навсегда (это безопасный кэш/мусор) — чтобы реально освободить место и показать «освобождено N».
        await ApplyFixesAsync(targets, "Быстрая чистка", forcePermanent: true).ConfigureAwait(true);
    }

    /// <summary>Что входит в «быструю чистку»: кэш/временное/пустышки. НЕ трогаем большие/дубли/cookie/историю/долгий DISM.</summary>
    private static bool IsQuickCleanSafe(FindingViewModel finding)
    {
        var id = finding.Finding.Id;
        if (finding.Finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DismCleanup)
        {
            return false; // долго — не для «быстрой»
        }

        if (id == "junk-RecycleBin")
        {
            return false; // Корзину «навсегда» молча не чистим (там могут быть нужные удалённые файлы)
        }

        return id.StartsWith("junk-", StringComparison.Ordinal)
               || (id.StartsWith("appcache-", StringComparison.Ordinal) && id.EndsWith("-cache", StringComparison.Ordinal));
    }

    // Чистые хелперы (TotalFixedDriveFreeSpace / Dot / GroupTitle / ExtractExecutablePath) вынесены в ScanViewHelpers.

    /// <summary>Эта находка приведёт к удалению файлов/папок (мусор/большие/дубли/остатки) — для этого спросим способ удаления.</summary>
    private static bool IsDeleteTarget(FindingViewModel finding)
    {
        var data = finding.Finding.Data;
        var kind = data?.GetValueOrDefault(FindingDataKeys.Kind);
        if (kind is FindingKinds.FileDelete or FindingKinds.FolderDelete)
        {
            return true;
        }

        // Очистка мусора по списку путей (kind не задан, есть paths) — но НЕ DISM/SFC/прочее в группе «Мусор».
        return finding.Finding.Group == ScanGroup.Junk && kind is null && data?.ContainsKey("paths") == true;
    }

    /// <summary>Показать диалог «Как удалить?» поверх главного окна и вернуть выбор. Нет окна → безопасно в Корзину.</summary>
    private static async Task<DeleteChoice> AskDeleteChoiceAsync(int count)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
        {
            return DeleteChoice.Recycle;
        }

        var message = count == 1
            ? "Выбранное будет удалено. Как поступить?"
            : $"Выбранных пунктов: {count}. Они будут удалены. Как поступить?";
        return await new DeleteConfirmWindow(message).ShowDialog<DeleteChoice>(owner).ConfigureAwait(true);
    }

    /// <summary>Показать окно-результат поверх главного окна. true — нажали кнопку действия (иначе просто закрыли).</summary>
    private static async Task<bool> ShowMessageDialogAsync(string title, string message, string? actionLabel = null)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
        {
            return false;
        }

        return await new MessageDialog(title, message, actionLabel).ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    partial void OnSelectedGroupChanged(ScanGroupViewModel? value)
    {
        RefreshVisibleFindings();
        OnPropertyChanged(nameof(HasJunkTotal));   // «Можно освободить» — только в разделе «Мусор» (правка 946)
        OnPropertyChanged(nameof(JunkSafeLabel));
        OnPropertyChanged(nameof(JunkTotalLabel));
        // Синяя окантовка активной вкладки — через IsActive (надёжнее descendant-:selected у шаблонной капсулы).
        foreach (var group in Groups)
        {
            group.IsActive = ReferenceEquals(group, value);
        }
    }

    partial void OnSelectedFilterChanged(FilterOption value) => RefreshVisibleFindings();

    private void RefreshVisibleFindings()
    {
        IsAllSelected = false;
        VisibleFindings.Clear();
        if (SelectedGroup is not null)
        {
            foreach (var finding in SelectedGroup.Findings)
            {
                if (Matches(finding))
                {
                    VisibleFindings.Add(finding);
                }
            }
        }

        RebuildSections();
        IsAllSelected = VisibleFindings.Any(f => f.IsSelected);
        OnPropertyChanged(nameof(HasFixableSelected));
        OnPropertyChanged(nameof(HasWhitelistableSelected));

        // Плашка «всё в порядке / под фильтром пусто» зависит от видимых находок — обновить.
        OnPropertyChanged(nameof(ShowEmptyGroupNotice));
        OnPropertyChanged(nameof(EmptyGroupTitle));
        OnPropertyChanged(nameof(EmptyGroupHint));
        OnPropertyChanged(nameof(IsJunkSection)); // чипы-навигация «Мусора» — по составу секций
        OnPropertyChanged(nameof(HasSectionChips)); // чипы-навигация в любом разделе с подсекциями
    }

    /// <summary>Сгруппировать видимые находки по подсекциям (для «Мусора» — диски/чистка/файлы/папки/дубли).</summary>
    // Запоминаем «свёрнуто/развёрнуто» по названию секции, чтобы при перестроении списка (после починки/
    // пометки/смены фильтра) свёрнутые группы не разворачивались обратно.
    private readonly Dictionary<string, bool> _sectionExpanded = new(StringComparer.Ordinal);

    private void RebuildSections()
    {
        VisibleSections.Clear();
        foreach (var section in VisibleFindings
                     .GroupBy(f => f.SectionTitle)
                     .OrderBy(g => g.First().SectionOrder))
        {
            var sectionVm = new FindingSectionViewModel(section.Key, section);
            if (_sectionExpanded.TryGetValue(section.Key, out var expanded))
            {
                sectionVm.IsExpanded = expanded;
            }

            sectionVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FindingSectionViewModel.IsExpanded))
                {
                    _sectionExpanded[sectionVm.Title] = sectionVm.IsExpanded;
                }
            };
            VisibleSections.Add(sectionVm);
        }
    }

    private bool Matches(FindingViewModel finding)
    {
        // Информационные находки (отчёт «Скорость загрузки» и т.п.) — это справка, а не проблемы: показываем
        // их всегда (кроме фильтра «Исправлено»), иначе «ОК»-пункты вроде Защитника пропадают из списка (баг 1138).
        if (finding.IsInformational)
        {
            return SelectedFilter.Filter != FindingFilter.Fixed;
        }

        // Фильтруем по ЭФФЕКТИВНОЙ важности: зелёное (OK / «проверено-безопасно» онлайн / вручную
        // «Безопасно») не попадает в «Проблемы»/«Внимание»/«Советы» и видно только в «Все».
        var severity = finding.EffectiveSeverity;
        return SelectedFilter.Filter switch
        {
            FindingFilter.Problems => severity == Severity.Danger && !finding.IsFixed,
            FindingFilter.Warnings => severity == Severity.Warning && !finding.IsFixed,
            FindingFilter.Advice => severity == Severity.Info && !finding.IsFixed,
            FindingFilter.Fixed => finding.IsFixed || finding.IsAlreadyDone,
            _ => true,
        };
    }

    private FindingViewModel CreateFindingViewModel(Finding finding)
    {
        var viewModel = new FindingViewModel(
            finding, _fixFactory.CanFix(finding), FixOneAsync, MarkSafe, DeleteFileAsync, OpenPath, OpenUrl, _aiAssistant, UndoFixAsync, DriverActionAsync, OnAiAnswered, FolderActionAsync, OpenItem, DeleteStartupCompletelyAsync);
        // Помеченное «Безопасно» помним между запусками — показываем зелёным (не скрываем).
        viewModel.IsMarkedSafe = _whitelist.Contains(viewModel.WhitelistKey);
        viewModel.PropertyChanged += OnFindingSelectionChanged;
        return viewModel;
    }

    // Открыть проводник с выделенным файлом (из команды автозапуска берём именно путь к exe).
    private void OpenPath(string path)
    {
        if (ExternalOpener.RevealInExplorer(ExtractExecutablePath(path)) is { } error)
        {
            StatusText = "Не удалось открыть папку: " + error;
        }
    }

    // «Открыть страницу» — официальный сайт фирменной утилиты (раздел «Утилиты»), где winget недоступен.
    private void OpenUrl(string url)
    {
        if (ExternalOpener.Open(url) is { } error)
        {
            StatusText = "Не удалось открыть страницу: " + error;
        }
    }

    private async Task<bool> DeleteFileAsync(string path)
    {
        var synthetic = new Finding
        {
            Id = "filedelete-" + path,
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = "Удаление файла",
            Explain = string.Empty,
            Data = new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.FileDelete, ["path"] = path },
        };

        var fix = _fixFactory.CreateFix(synthetic);
        if (fix is null)
        {
            return false;
        }

        var result = await Task.Run(() => _fixOrchestrator.ApplyAsync([fix], "Удаление файла")).ConfigureAwait(true);
        if (result.Aborted)
        {
            StatusText = result.Message ?? "Не удалось создать бэкап — файл не удалён.";
            return false;
        }

        var ok = result.Outcomes.Count > 0 && result.Outcomes[0].Success;
        StatusText = ok
            ? "Файл удалён в Корзину Windows — восстановить можно оттуда."
            : "Не удалось удалить файл (или удаление было бы безвозвратным — отменено).";
        return ok;
    }

    private void MarkSafe(FindingViewModel finding)
    {
        // Тоггл: пометить «Безопасно» (зелёным, не скрывая) либо снять пометку. Помнится между запусками.
        if (finding.IsMarkedSafe)
        {
            _whitelist.Remove(finding.WhitelistKey);
            finding.IsMarkedSafe = false;
            StatusText = "Пометка «Безопасно» снята — пункт снова активен.";
        }
        else
        {
            _whitelist.Add(finding.WhitelistKey);
            finding.IsMarkedSafe = true;
            StatusText = "Помечено «Безопасно» (зелёным). Кнопкой «Вернуть» можно отменить.";
        }

        SelectedGroup?.NotifyCounts();
        RefreshVisibleFindings(); // пометка/снятие «Безопасно» меняет видимость под активным фильтром
    }

    /// <summary>
    /// Извлекает путь к исполняемому файлу из строки запуска автозапуска (может содержать кавычки и аргументы),
    /// чтобы «Открыть папку» и онлайн-проверка работали именно по файлу, а не по всей команде с ключами.
    /// </summary>
}
