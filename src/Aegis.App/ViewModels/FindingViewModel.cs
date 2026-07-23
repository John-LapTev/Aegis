using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Ссылка из ответа ИИ: URL для перехода + короткая подпись для показа (вместо сырого %D0%…-URL), правка 903.</summary>
public sealed record AiLink(string Url, string Label);

/// <summary>Находка в списке результатов: оборачивает <see cref="Finding"/> + состояние выбора/починки/пометки.</summary>
public sealed partial class FindingViewModel : ObservableObject
{
    private readonly Finding _finding;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFixButton), nameof(EffectiveSeverity), nameof(DisplayStatusText), nameof(CanWhitelistNow), nameof(ShowUndoButton), nameof(CanDeleteCompletely), nameof(HasActionMenu), nameof(ShowInlineActions))]
    private bool _isFixed;

    /// <summary>Id бэкапа этой правки (из FixOutcome) — для кнопки «Вернуть» (откат именно её). null — отката нет.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUndoButton), nameof(HasActionMenu), nameof(ShowInlineActions))]
    private string? _backupId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFixButton), nameof(CanWhitelistNow), nameof(CanDeleteCompletely), nameof(HasActionMenu), nameof(ShowInlineActions))]
    private bool _isFixing;

    /// <summary>Прогресс починки 0..1 — для кольца-заполнения (не вращение, а реальная шкала).</summary>
    [ObservableProperty]
    private double _fixProgress;

    [ObservableProperty]
    private bool _isCheckingOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOnlineVerdict))]
    private string _onlineVerdict = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFixButton), nameof(EffectiveSeverity), nameof(DisplayStatusText), nameof(MarkSafeLabel), nameof(HasActionMenu), nameof(ShowInlineActions))]
    private bool _isMarkedSafe;

    /// <summary>Онлайн-проверка (Защитник + VirusTotal) подтвердила, что файл без подписи, но чистый.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFixButton), nameof(EffectiveSeverity), nameof(DisplayStatusText), nameof(HasActionMenu), nameof(ShowInlineActions))]
    private bool _isVerifiedSafeOnline;

    private readonly IAiAssistant? _aiAssistant;

    /// <summary>Открыть ссылку в браузере — для кликабельных ссылок в ответе ИИ (правка 899).</summary>
    private readonly Action<string>? _onOpenUrl;

    /// <summary>Действие над выбранными драйверами: (находка, переустановить?) — перезагрузка/переустановка (правка 930).</summary>
    private readonly Func<FindingViewModel, bool, Task>? _onDriverAction;

    /// <summary>Удаление выбранных элементов содержимого большой папки: (находка, навсегда?) — в Корзину/навсегда.</summary>
    private readonly Func<FindingViewModel, bool, Task>? _onFolderAction;

    /// <summary>Какая модель реально ответила — чтобы шапка показывала тот же значок, что и ответ (правка 953).</summary>
    private readonly Action<string>? _onAiAnswered;

    /// <summary>Идёт ли запрос к ИИ-помощнику (для индикатора на кнопке «Спросить ИИ»).</summary>
    [ObservableProperty]
    private bool _isAskingAi;

    /// <summary>Ответ ИИ-помощника простыми словами (или причина неудачи) — показываем под находкой.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiAnswer), nameof(AiAnswerBody))]
    private string _aiAnswer = string.Empty;

    private static readonly Regex UrlRegex = new(@"https?://[^\s)»]+", RegexOptions.Compiled);

    // Groq (Llama) иногда подмешивает иероглифы вместо русских букв — вырезаем CJK/японские/корейские символы (правка 903).
    private static readonly Regex ForeignScriptRegex =
        new(@"[\u3000-\u303F\u3040-\u30FF\u3400-\u4DBF\u4E00-\u9FFF\uAC00-\uD7AF\uF900-\uFAFF\uFF00-\uFFEF]+", RegexOptions.Compiled);

    /// <summary>Ссылки из ответа ИИ — отдельными синими кликабельными кнопками с короткой подписью (правка 899/903).</summary>
    public ObservableCollection<AiLink> AiAnswerLinks { get; } = [];

    /// <summary>Текст ответа без ВЫНЕСЕННЫХ (доверенных) ссылок. Недоверенные ссылки остаются в тексте как есть —
    /// кнопкой их не делаем, чтобы доверчивый пользователь не открыл фейковый сайт одним кликом (аудит 2026-07-04).</summary>
    public string AiAnswerBody
    {
        get
        {
            var text = UrlRegex.Replace(AiAnswer, m =>
                Aegis.Core.TrustedDomains.IsTrusted(m.Value.TrimEnd('.', ',', ')', '»', ':')) ? string.Empty : m.Value);
            text = Regex.Replace(text, @"\(\s*\)", string.Empty);        // пустые скобки «()» от вынесенной ссылки
            text = Regex.Replace(text, @"\(\s*(?=\n|$)", string.Empty);  // одинокая «(» в конце строки
            text = Regex.Replace(text, @"(?<=\n|^)\s*\)", string.Empty); // одинокая «)» в начале строки
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && line.Any(char.IsLetterOrDigit)); // выкидываем строки-обрывки вида ")" / "()"
            return string.Join("\n", lines).Trim();
        }
    }

    /// <summary>Чистим ответ ИИ: убираем иероглифы (глюк Groq) и схлопываем лишние пробелы (правка 903).</summary>
    private static string SanitizeAnswer(string text)
    {
        var cleaned = ForeignScriptRegex.Replace(text, string.Empty);
        return Regex.Replace(cleaned, @"[ \t]{2,}", " ").Trim();
    }

    partial void OnAiAnswerChanged(string value)
    {
        // Кнопкой-ссылкой делаем ТОЛЬКО доверенные официальные домены (белый список). Недоверенные/галлюцинированные
        // URL кнопкой не выносим — они остаются простым текстом в ответе, чтобы не увести на фейковый сайт (аудит 2026-07-04).
        AiAnswerLinks.Clear();
        foreach (Match match in UrlRegex.Matches(value))
        {
            var url = match.Value.TrimEnd('.', ',', ')', '»', ':');
            if (Aegis.Core.TrustedDomains.IsTrusted(url) && AiAnswerLinks.All(link => link.Url != url))
            {
                AiAnswerLinks.Add(new AiLink(url, LinkLabel(url)));
            }
        }
    }

    /// <summary>
    /// Короткая подпись ссылки вместо сырого %D0%…-URL (правка 903). «Официальная страница» пишем ТОЛЬКО если домен в
    /// белом списке производителей — иначе нейтральное «Открыть ссылку (домен)», чтобы не выдать фейковый сайт за
    /// официальный доверчивому пользователю (аудит 2026-07-03).
    /// </summary>
    private static string LinkLabel(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            return Aegis.Core.TrustedDomains.IsTrusted(url)
                ? $"Открыть официальную страницу ({host})"
                : $"Открыть ссылку ({host})";
        }

        return "Открыть страницу";
    }

    /// <summary>Открыть ссылку из ответа ИИ в браузере (кликабельная синяя ссылка, правка 899).</summary>
    [RelayCommand]
    private void OpenLink(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            _onOpenUrl?.Invoke(url);
        }
    }

    public FindingViewModel(
        Finding finding,
        bool canFix = false,
        Func<FindingViewModel, Task>? onFix = null,
        Action<FindingViewModel>? onMarkSafe = null,
        Func<string, Task<bool>>? onDeleteFile = null,
        Action<string>? onOpenPath = null,
        Action<string>? onOpenUrl = null,
        IAiAssistant? aiAssistant = null,
        Func<FindingViewModel, Task>? onUndo = null,
        Func<FindingViewModel, bool, Task>? onDriverAction = null,
        Action<string>? onAiAnswered = null,
        Func<FindingViewModel, bool, Task>? onFolderAction = null,
        Action<string>? onOpenItem = null,
        Func<FindingViewModel, Task>? onDeleteCompletely = null)
    {
        _finding = finding;
        _onAiAnswered = onAiAnswered;
        _onFolderAction = onFolderAction;

        // «Удалить полностью» — для программ в автозапуске (снос программы + чистка остатков).
        if (CanDeleteCompletely && onDeleteCompletely is not null)
        {
            DeleteCompletelyCommand = new AsyncRelayCommand(() => onDeleteCompletely(this));
        }
        CanFix = canFix;
        if (canFix && onFix is not null)
        {
            FixCommand = new AsyncRelayCommand(() => onFix(this));
        }

        // «Вернуть» — откат именно этой правки (по её бэкапу). Доступно после починки, если бэкап есть.
        if (onUndo is not null)
        {
            UndoCommand = new AsyncRelayCommand(() => onUndo(this));
        }

        // «Спросить ИИ» — помощник объяснит непонятный процесс/файл/устройство простыми словами.
        _aiAssistant = aiAssistant;
        _onOpenUrl = onOpenUrl;
        if (CanAskAi)
        {
            AskAiCommand = new AsyncRelayCommand(AskAiAsync);
            // Закрыть ответ ИИ — большой блок больше не нужен, пользователь его убирает (правка 820).
            DismissAiAnswerCommand = new RelayCommand(() => AiAnswer = string.Empty);
        }

        // «Открыть папку» — для находок с реальным путём к файлу (Windows-путь).
        CanOpenPath = !string.IsNullOrWhiteSpace(finding.Detail)
                      && (finding.Detail!.Contains(":\\", StringComparison.Ordinal)
                          || finding.Detail!.StartsWith("\\\\", StringComparison.Ordinal));
        if (CanOpenPath && onOpenPath is not null)
        {
            OpenPathCommand = new RelayCommand(() => onOpenPath(finding.Detail!));
        }

        // «Открыть страницу» — для находок с официальной ссылкой (раздел «Утилиты»).
        DownloadUrl = finding.Data?.GetValueOrDefault("url");
        if (!string.IsNullOrEmpty(DownloadUrl) && onOpenUrl is not null)
        {
            var url = DownloadUrl;
            DownloadCommand = new RelayCommand(() => onOpenUrl(url));
        }

        // «Безопасно» — на любой не-OK находке (пользователь может одобрить ложное срабатывание),
        // КРОМЕ чисто информационных (Data["info"]="1"): для них квадратик выделения и «Безопасно» неуместны (запрос Ивана).
        CanWhitelist = finding.Severity != Severity.Ok && finding.Data?.GetValueOrDefault("info") != "1";
        if (CanWhitelist && onMarkSafe is not null)
        {
            MarkSafeCommand = new RelayCommand(() => onMarkSafe(this));
        }

        // Онлайн-проверка — для неподписанных файлов (процессы/автозапуск) с известным путём.
        // Кнопки нет: проверка идёт автоматически при сканировании раздела (см. MainWindowViewModel).
        CanCheckOnline = finding.Group is ScanGroup.Processes or ScanGroup.Autostart
                         && finding.Severity != Severity.Ok
                         && !string.IsNullOrWhiteSpace(finding.Detail);

        // Дубликаты — раскрывающийся список копий с путями и удалением по отдельности.
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DuplicateGroup
            && finding.Data.TryGetValue("paths", out var paths)
            && onDeleteFile is not null)
        {
            foreach (var path in paths.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                Copies.Add(new DuplicateCopyViewModel(path, onDeleteFile));
            }
        }

        // Драйверы категории — раскрывающийся список с галочками (текст␟DeviceID на каждый драйвер, правка 930).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DriverList
            && finding.Data.TryGetValue("items", out var driverItems))
        {
            foreach (var line in driverItems.Split('\u0001', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\u001F');
                var deviceId = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                var entry = new DriverEntryViewModel(parts[0], deviceId);
                entry.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DriverEntryViewModel.IsSelected))
                    {
                        OnPropertyChanged(nameof(HasSelectedDrivers));
                        OnPropertyChanged(nameof(ToggleSelectLabel));
                    }
                };
                DriverEntries.Add(entry);
            }
        }

        _onDriverAction = onDriverAction;

        // Содержимое большой папки — раскрывающийся список файлов/подпапок с галочками (имя␟размер␟папка-ли␟путь).
        if (finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.FolderContents
            && finding.Data.TryGetValue("items", out var folderItems))
        {
            foreach (var line in folderItems.Split('\u0001', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\u001F');
                if (parts.Length < 4 || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                {
                    continue;
                }

                var entry = new FileEntryViewModel(parts[0], parts[3], size, parts[2] == "1", onOpenItem);
                entry.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(FileEntryViewModel.IsSelected) or nameof(FileEntryViewModel.IsRemoved))
                    {
                        OnPropertyChanged(nameof(HasSelectedFiles));
                        OnPropertyChanged(nameof(ToggleSelectFilesLabel));
                    }
                };
                FolderEntries.Add(entry);
            }
        }

        // Мусор с несколькими расположениями — раскрывающийся список, ЧТО именно будет очищено
        // (пользователь хочет видеть файлы/папки перед очисткой). Для одного расположения список не нужен —
        // путь и так показан плашкой. У дубликатов свой список копий (Copies) — их сюда НЕ дублируем.
        if (finding.Group == ScanGroup.Junk
            && finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) != FindingKinds.DuplicateGroup
            && finding.Data?.GetValueOrDefault("paths") is { Length: > 0 } junkPaths)
        {
            foreach (var path in junkPaths.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                Locations.Add(new JunkLocationViewModel(path, onOpenPath));
            }
        }
    }

    /// <summary>
    /// Находка для построения исправления. Если в списке «что очистим» сняли галочки — отдаём версию ТОЛЬКО с
    /// отмеченными путями, чтобы очистилось именно выбранное. Если всё отмечено (по умолчанию) — исходную.
    /// </summary>
    public Finding Finding =>
        Locations.Count > 0 && Locations.Any(static l => !l.IsSelected) ? WithSelectedLocations() : _finding;

    private Finding WithSelectedLocations()
    {
        var selected = Locations.Where(l => l.IsSelected).Select(l => l.Path);
        var data = new Dictionary<string, string>(_finding.Data ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            ["paths"] = string.Join('|', selected),
        };

        return _finding with { Data = data };
    }

    /// <summary>Команда «Спросить ИИ» — объяснить находку простыми словами (null, если помощник не настроен).</summary>
    public IAsyncRelayCommand? AskAiCommand { get; }

    public IRelayCommand? DismissAiAnswerCommand { get; }

    /// <summary>Показывать ли кнопку «Спросить ИИ»: помощник настроен И находка из «исследуемых» разделов.</summary>
    public bool CanAskAi => _aiAssistant?.IsConfigured == true
        && _finding.Group is ScanGroup.Processes or ScanGroup.Threats or ScanGroup.Drivers
            or ScanGroup.Autostart or ScanGroup.Missing;

    /// <summary>Есть ли ответ ИИ для показа под находкой.</summary>
    public bool HasAiAnswer => !string.IsNullOrEmpty(AiAnswer);

    private async Task AskAiAsync()
    {
        if (_aiAssistant is null || IsAskingAi)
        {
            return;
        }

        IsAskingAi = true;
        AiAnswer = string.Empty;
        try
        {
            // Системный «паспорт программы» + сегодняшняя дата (чтобы «последняя версия» была привязана к сейчас)
            // + конкретный вопрос; веб-поиск (BuildWebQuery) подмешивается обёрткой.
            var today = DateTime.Now.ToString("dd.MM.yyyy");
            var driverTexts = DriverEntries.Select(e => e.DisplayText).ToList();
            var prompt = AiSystemPrompt.Text + $"\n(Справка: сегодня {today} — учитывай это, когда речь о «последней версии».)\n"
                         + FindingAiPrompt.Build(_finding, driverTexts);
            var result = await _aiAssistant.AskAsync(prompt, FindingAiPrompt.WebQuery(_finding)).ConfigureAwait(true);
            AiAnswerModel = result.Provider ?? string.Empty; // какая модель ответила — для её логотипа в боксе
            AiAnswer = result.Success ? SanitizeAnswer(result.Text ?? string.Empty) : result.Error ?? "ИИ-помощник не ответил.";
            if (result.Success && !string.IsNullOrEmpty(result.Provider))
            {
                _onAiAnswered?.Invoke(result.Provider); // шапка покажет ту же модель, что и ответ (правка 953)
            }
        }
        catch (Exception ex)
        {
            // Любой сбой цепочки ИИ доходит до бокса ответа, а НЕ роняет приложение (AsyncRelayCommand
            // иначе пробросил бы исключение в диспетчер Avalonia — процесс завершился бы молча).
            AiAnswer = "Не удалось получить ответ ИИ: " + ex.Message;
        }
        finally
        {
            IsAskingAi = false;
        }
    }

    /// <summary>Модель, которая ответила (Gemini/ChatGPT/Claude) — для её логотипа в боксе ответа.</summary>
    [ObservableProperty]
    private string _aiAnswerModel = string.Empty;

    /// <summary>Стабильный ключ для белого списка (путь для процессов, иначе Id находки).</summary>
    public string WhitelistKey => _finding.Group == ScanGroup.Processes && !string.IsNullOrWhiteSpace(_finding.Detail)
        ? _finding.Detail!
        : _finding.Id;

    /// <summary>Копии дубликата (для раскрывающегося списка).</summary>
    public ObservableCollection<DuplicateCopyViewModel> Copies { get; } = [];

    public bool HasCopies => Copies.Count > 0;

    /// <summary>Драйверы категории (список с галочками для раскрытия) — правка 930.</summary>
    public ObservableCollection<DriverEntryViewModel> DriverEntries { get; } = [];

    public bool HasDriverEntries => DriverEntries.Count > 0;

    /// <summary>
    /// Размер находки в байтах (для мусора). Если после проверки место уже пересчитали — берём свежее
    /// значение, иначе то, что нашёл сканер (Data[bytes]; 0, если не указан).
    /// </summary>
    public long SizeBytes => _liveSizeBytes ?? ScannedSizeBytes;

    /// <summary>Размер, измеренный во время проверки.</summary>
    private long ScannedSizeBytes =>
        _finding.Data?.GetValueOrDefault(FindingDataKeys.Bytes) is { } raw && long.TryParse(raw, out var bytes) ? bytes : 0;

    /// <summary>Размер, измеренный уже после проверки (null — не пересчитывали).</summary>
    private long? _liveSizeBytes;

    /// <summary>
    /// Обновить размер по факту: после чистки (в том числе частичной или сделанной вручную в проводнике)
    /// цифра в списке должна показывать, сколько осталось СЕЙЧАС, а не сколько было при проверке.
    /// </summary>
    public void SetLiveSize(long bytes)
    {
        if (_liveSizeBytes == bytes)
        {
            return;
        }

        _liveSizeBytes = bytes;
        OnPropertyChanged(nameof(SizeBytes));
        OnPropertyChanged(nameof(HasLiveSize));
        OnPropertyChanged(nameof(LiveSizeNote));
    }

    /// <summary>Пути, размер которых можно пересчитать (пусто — у находки нет измеримого места).</summary>
    public IReadOnlyList<string> MeasurablePaths
    {
        get
        {
            if (_finding.Data?.GetValueOrDefault(FindingDataKeys.Paths) is { Length: > 0 } list)
            {
                return list.Split('|', StringSplitOptions.RemoveEmptyEntries);
            }

            var single = _finding.Data?.GetValueOrDefault(FindingDataKeys.Path)
                         ?? _finding.Data?.GetValueOrDefault(FindingDataKeys.Folder);
            return string.IsNullOrWhiteSpace(single) ? [] : [single];
        }
    }

    /// <summary>Показывать ли строку «сейчас: …» — только когда пересчёт дал ДРУГОЕ число.</summary>
    public bool HasLiveSize => _liveSizeBytes is long live && live != ScannedSizeBytes && ScannedSizeBytes > 0;

    /// <summary>Подпись со свежим размером: «сейчас: 240 МБ» либо «уже очищено».</summary>
    public string LiveSizeNote => _liveSizeBytes switch
    {
        null => string.Empty,
        0 => "уже очищено",
        long live => "сейчас: " + Aegis.Core.HumanSize.Format(live),
    };

    /// <summary>Показывать ли кнопку «Действия» — есть хоть один драйвер с DeviceID и обработчик задан.</summary>
    public bool CanDriverAct => _onDriverAction is not null && DriverEntries.Any(e => e.CanAct);

    /// <summary>Выбран ли галочкой хоть один драйвер (для активности кнопок действий).</summary>
    public bool HasSelectedDrivers => DriverEntries.Any(e => e.IsSelected);

    /// <summary>Подпись кнопки-переключателя выделения: что-то выбрано → «Сбросить выделение», иначе → «Выделить все» (правка 943).</summary>
    public string ToggleSelectLabel => HasSelectedDrivers ? "Сбросить выделение" : "Выделить все";

    /// <summary>Переключатель выделения: если выбрано хоть что-то — снять всё; если ничего — выбрать все (доступные).</summary>
    [RelayCommand]
    private void ToggleSelectDrivers()
    {
        var select = !HasSelectedDrivers; // ничего не выбрано → выделяем все; иначе снимаем
        foreach (var entry in DriverEntries)
        {
            entry.IsSelected = select && entry.CanAct;
        }
    }

    /// <summary>«Перезагрузить драйвер» для выбранных (безопасно).</summary>
    [RelayCommand]
    private Task ReloadDrivers() => _onDriverAction?.Invoke(this, false) ?? Task.CompletedTask;

    /// <summary>«Переустановить» драйвер для выбранных (рискованнее, с точкой восстановления).</summary>
    [RelayCommand]
    private Task ReinstallDrivers() => _onDriverAction?.Invoke(this, true) ?? Task.CompletedTask;

    /// <summary>Содержимое большой папки: файлы/подпапки с галочками — раскрывающийся список для удаления выбранного.</summary>
    public ObservableCollection<FileEntryViewModel> FolderEntries { get; } = [];

    public bool HasFolderEntries => FolderEntries.Count > 0;

    /// <summary>Выбран ли галочкой хоть один элемент содержимого (для активности кнопок удаления).</summary>
    public bool HasSelectedFiles => FolderEntries.Any(e => e.IsSelected && !e.IsRemoved);

    /// <summary>Подпись кнопки-переключателя выделения содержимого: что-то выбрано → «Сбросить выделение», иначе → «Выделить все».</summary>
    public string ToggleSelectFilesLabel => HasSelectedFiles ? "Сбросить выделение" : "Выделить все";

    /// <summary>Выбранные (и ещё не удалённые) элементы содержимого папки — их удаляет MainWindowViewModel.</summary>
    public IReadOnlyList<FileEntryViewModel> SelectedFolderEntries =>
        FolderEntries.Where(e => e.IsSelected && !e.IsRemoved).ToList();

    /// <summary>Переключатель выделения содержимого: выбрано что-то — снять всё; ничего — выбрать всё (кроме удалённых).</summary>
    [RelayCommand]
    private void ToggleSelectFiles()
    {
        var select = !HasSelectedFiles;
        foreach (var entry in FolderEntries.Where(e => !e.IsRemoved))
        {
            entry.IsSelected = select;
        }
    }

    /// <summary>«В Корзину» для выбранных элементов папки (обратимо).</summary>
    [RelayCommand]
    private Task RecycleSelectedFiles() => _onFolderAction?.Invoke(this, false) ?? Task.CompletedTask;

    /// <summary>«Удалить навсегда» для выбранных элементов папки (с предупреждением в UI — вернуть нельзя).</summary>
    [RelayCommand]
    private Task DeleteSelectedFilesPermanently() => _onFolderAction?.Invoke(this, true) ?? Task.CompletedTask;

    /// <summary>Расположения мусора (что именно очистим) — для раскрывающегося списка у сводных находок.</summary>
    public ObservableCollection<JunkLocationViewModel> Locations { get; } = [];

    /// <summary>Показывать список расположений только если их несколько (одно уже видно плашкой пути).</summary>
    public bool HasLocations => Locations.Count > 1;

    public bool CanFix { get; }

    public bool CanWhitelist { get; }

    /// <summary>
    /// Можно ли выбрать галочкой для массового действия (исправление / пометка «Безопасно»). Находки с
    /// пометкой <c>noBatch</c> (например, «возможно, остатки программы») в массовое «Выделить всё» НЕ входят —
    /// их удаляют только по одному, осознанно (защита от пакетного удаления нужного).
    /// </summary>
    public bool CanBatchSelect => (CanFix || CanWhitelist) && _finding.Data?.GetValueOrDefault("noBatch") != "1";

    public bool CanCheckOnline { get; }

    public bool CanOpenPath { get; }

    public IRelayCommand? OpenPathCommand { get; }

    /// <summary>Официальная ссылка для находок раздела «Утилиты» (страница продукта).</summary>
    public string? DownloadUrl { get; }

    public bool CanDownload => !string.IsNullOrEmpty(DownloadUrl);

    /// <summary>Подпись кнопки-ссылки: «Скачать драйвер» (есть обновление) / «Переустановить» (видеокарта с актуальной версией) / «Открыть страницу».</summary>
    public string DownloadLabel =>
        _finding.Data?.GetValueOrDefault("driver-update") == "1" ? "Скачать драйвер"
        : _finding.Data?.GetValueOrDefault("driver-wu-update") == "1" ? "Обновить в Windows"
        : _finding.Id.StartsWith("driver-gpu-", StringComparison.Ordinal) ? "Переустановить"
        : "Открыть страницу";

    public IRelayCommand? DownloadCommand { get; }

    public bool HasOnlineVerdict => !string.IsNullOrEmpty(OnlineVerdict);

    public IAsyncRelayCommand? FixCommand { get; }

    /// <summary>Откат именно этой правки (по её бэкапу). Показываем кнопку «Вернуть» только у исправленных с бэкапом.</summary>
    public IAsyncRelayCommand? UndoCommand { get; }

    public bool ShowUndoButton => IsFixed && !string.IsNullOrEmpty(BackupId) && UndoCommand is not null;

    public IRelayCommand? MarkSafeCommand { get; }

    /// <summary>«Удалить полностью» программу автозапуска (через инсталлятор + чистка, иначе — папку в Корзину).</summary>
    public IAsyncRelayCommand? DeleteCompletelyCommand { get; }

    /// <summary>Краткая подпись происхождения процесса («Видеокарта (NVIDIA)», «неизвестно»…), если есть.</summary>
    public string? CategoryLabel => _finding.Data?.GetValueOrDefault("category");

    public bool HasCategory => !string.IsNullOrWhiteSpace(CategoryLabel);

    /// <summary>
    /// Показывать ли кнопку действия. Только для реально исправимых находок (есть готовая правка) — иначе
    /// у информационных находок (температуры, заполненность диска, большие папки) была бы «мёртвая» серая
    /// кнопка, что недопустимо для не-технической аудитории. Нет также для исправленных, «Безопасно»,
    /// подтверждённо-чистых онлайн и дублей.
    /// </summary>
    // Для автозапуска «Отключить» — это ВЫБОР пользователя (убрать программу из автозапуска), а не починка угрозы.
    // Поэтому кнопку показываем даже у проверенных-безопасных записей (например, автообновление Discord/Opera —
    // безопасно, но пользователь хочет его выключить). У остальных разделов — прячем, если уже безопасно/исправлено.
    public bool ShowFixButton => !IsFixed && !IsFixing && !HasCopies && CanFix
        && (_finding.Group == ScanGroup.Autostart || (!IsMarkedSafe && !IsVerifiedSafeOnline));

    /// <summary>Подпись кнопки «Безопасно» / «Вернуть» (отмена пометки).</summary>
    public string MarkSafeLabel => IsMarkedSafe ? "Вернуть" : "Безопасно";

    /// <summary>Важность для бейджа: помеченное «Безопасно», подтверждённо-чистое онлайн ИЛИ уже исправленное — зелёным (OK).</summary>
    public Severity EffectiveSeverity => IsMarkedSafe || IsVerifiedSafeOnline || IsFixed ? Severity.Ok : _finding.Severity;

    /// <summary>Текст бейджа с учётом исправления / пометки «Безопасно» / онлайн-проверки.</summary>
    /// <summary>Пункт уже в нужном состоянии (например, телеметрия уже отключена) — показываем как «Исправлено» (правка 729).</summary>
    public bool IsAlreadyDone => _finding.Data?.GetValueOrDefault("done") == "1";

    public string DisplayStatusText => IsHealthPlaceholder ? "скоро" : IsFixed || IsAlreadyDone ? "Исправлено" : IsMarkedSafe ? "Безопасно" : IsVerifiedSafeOnline ? "Проверено" : StatusText;

    /// <summary>Плитка «Здоровья» — плейсхолдер (данных ещё нет): показываем приглушённо с подписью «появится после проверки».</summary>
    public bool IsHealthPlaceholder => _finding.Data?.GetValueOrDefault(FindingDataKeys.Placeholder) == "1";

    /// <summary>Прозрачность плитки: плейсхолдеры (без данных) — полупрозрачные, чтобы было видно, что ещё недоступны.</summary>
    public double TileOpacity => IsHealthPlaceholder ? 0.5 : 1.0;

    /// <summary>Показывать ли кнопку «Безопасно»: не у исправленных и не во время починки (чтобы не наезжала).</summary>
    public bool CanWhitelistNow => CanWhitelist && !IsFixed && !IsFixing;

    /// <summary>Путь/имя exe программы — для «Удалить полностью» (из Data["exe"], иначе из команды запуска).</summary>
    public string StartupExecutablePath =>
        _finding.Data?.GetValueOrDefault("exe") is { Length: > 0 } exe ? exe : ParseExecutable(_finding.Detail);

    /// <summary>Имя программы для сообщений при удалении (файл exe или заголовок).</summary>
    public string StartupDisplayName
    {
        get
        {
            var path = StartupExecutablePath;
            if (path.Length == 0)
            {
                return _finding.Title;
            }

            var slash = path.LastIndexOfAny(['\\', '/']);
            return slash >= 0 ? path[(slash + 1)..] : path;
        }
    }

    /// <summary>Доступно ли «Удалить полностью» — только для программ в автозапуске с понятным путём к exe.</summary>
    public bool CanDeleteCompletely => _finding.Group == ScanGroup.Autostart
                                       && !IsFixed && !IsFixing && StartupExecutablePath.Length > 0;

    /// <summary>Сколько «основных» действий доступно (без «Спросить AI») — для решения, сворачивать ли в «Действия».</summary>
    private int PrimaryActionCount =>
        (ShowFixButton ? 1 : 0) + (CanWhitelistNow ? 1 : 0) + (CanDeleteCompletely ? 1 : 0) + (ShowUndoButton ? 1 : 0);

    /// <summary>Свернуть действия в одну кнопку «Действия» (выпадающий список) — когда их 2+ (запрос Ивана 1142).</summary>
    public bool HasActionMenu => PrimaryActionCount >= 2;

    /// <summary>Показывать действия обычными кнопками в ряд (когда их мало — дропдаун не нужен).</summary>
    public bool ShowInlineActions => !HasActionMenu;

    /// <summary>Достаёт путь к exe из команды запуска автозапуска (учитывает кавычки).</summary>
    private static string ParseExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return string.Empty;
        }

        command = command.Trim();
        string path;
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            path = end > 0 ? command[1..end] : command;
        }
        else
        {
            var space = command.IndexOf(' ');
            path = space < 0 ? command : command[..space];
        }

        // Считаем путём только то, что похоже на файл на диске (иначе это не команда с exe).
        return path.Contains(":\\", StringComparison.Ordinal) || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? path
            : string.Empty;
    }

    public string FixButtonLabel => _finding.Id switch
    {
        var id when id.StartsWith("boot-culprit-Service", StringComparison.Ordinal) => "Отключить службу",
        "system-pending-reboot" => "Перезагрузить",
        "settings-rdp-on" => "Отключить",
        "maintenance-sfc-dism" => "Починить",
        "maintenance-network-reset" => "Сбросить",
        var id when id.StartsWith("threat-port-", StringComparison.Ordinal) => "Остановить",
        var id when id.StartsWith("miner-behavior-", StringComparison.Ordinal) => "Обезвредить",
        var id when id.StartsWith("driver-device-", StringComparison.Ordinal) => "Найти драйвер",
        var id when id.StartsWith("driver-update-", StringComparison.Ordinal) => "Установить драйвер",
        var id when id.StartsWith("suspicious-task-", StringComparison.Ordinal) => "Отключить",
        var id when id.StartsWith("dangerous-driver-", StringComparison.Ordinal) => "Удалить файл",
        var id when id.StartsWith("audio-enhancer-", StringComparison.Ordinal) => "Отключить",
        var id when id.StartsWith("util-", StringComparison.Ordinal) =>
            _finding.Data?.GetValueOrDefault("reinstall") == "1" ? "Переустановить" : "Установить",
        var id when id.StartsWith("device-disabled-", StringComparison.Ordinal) => "Включить",
        var id when id.StartsWith("largefile-", StringComparison.Ordinal) => "Очистить",
        var id when id.StartsWith("appx-", StringComparison.Ordinal) => "Удалить",
        var id when id.StartsWith("registry-", StringComparison.Ordinal) => "Исправить",
        var id when id.StartsWith("privacy-", StringComparison.Ordinal) => "Отключить",
        var id when id.StartsWith("debloat-", StringComparison.Ordinal) => "Отключить",
        var id when id.StartsWith("settings-", StringComparison.Ordinal) => "Включить",
        // Чужое ограничение Windows: кнопка «Исправить» не объясняет, что произойдёт — снимаем именно запрет.
        var id when id.StartsWith("policy-", StringComparison.Ordinal) => "Снять запрет",
        _ => _finding.Group switch
        {
            ScanGroup.Junk => "Очистить",
            ScanGroup.Processes => "Остановить",
            ScanGroup.Autostart => "Отключить",
            _ => "Исправить",
        },
    };

    public string Title => _finding.Title;

    /// <summary>У плитки диска есть блок заполнения (%) в правом-верхнем углу.</summary>
    public bool HasDiskFill => _finding.Data is not null && _finding.Data.ContainsKey("fillPercent");

    /// <summary>Диск с нечитаемым форматом (RAW) — вместо % показываем серый бейдж «RAW» + «?» с пояснением.</summary>
    public bool IsFilesystemUnreadable => _finding.Data?.GetValueOrDefault("raw") == "1";

    /// <summary>Текст блока заполнения диска, например «60%».</summary>
    public string DiskFillText =>
        _finding.Data is not null && _finding.Data.TryGetValue("fillPercent", out var percent) ? percent + "%" : string.Empty;

    /// <summary>Подпись в левом-верхнем углу иконки диска: буква (C/D…) или «RAW», если буквы нет.</summary>
    public string DiskLabel =>
        _finding.Data?.GetValueOrDefault("letter") is { Length: > 0 } driveLetter ? driveLetter
        : IsFilesystemUnreadable ? "RAW"
        : string.Empty;

    /// <summary>Показывать ли подпись (буква/RAW) на иконке диска.</summary>
    public bool HasDiskLabel => DiskLabel.Length > 0;

    /// <summary>Информационная находка (отчёт, а не проблема): показываем всегда, не прячем фильтром важности (баг 1138).</summary>
    public bool IsInformational => _finding.Data?.GetValueOrDefault("info") == "1";

    /// <summary>Модель железа под заголовком плитки «Здоровья» (процессор/видеокарта) — из Data["model"].</summary>
    public string HealthModel => _finding.Data?.GetValueOrDefault("model") ?? string.Empty;

    /// <summary>Показывать ли строку модели под заголовком плитки.</summary>
    public bool HasHealthModel => HealthModel.Length > 0;

    /// <summary>Цвет блока заполнения (по заполненности, отдельно от здоровья диска).</summary>
    public Severity DiskFillSeverity => _finding.Data?.GetValueOrDefault("fillSeverity") switch
    {
        "danger" => Severity.Danger,
        "warning" => Severity.Warning,
        _ => Severity.Ok,
    };

    /// <summary>Подсказка к блоку заполнения диска (отдельно от здоровья — чтобы красный «92%» не противоречил зелёному статусу).</summary>
    public string DiskFillHint => DiskFillSeverity switch
    {
        Severity.Danger => "Диск почти заполнен. Освободите место — иначе компьютер начнёт тормозить, а обновления могут не ставиться.",
        Severity.Warning => "Диск заполняется — свободного места немного. Скоро стоит почистить.",
        _ => "Свободного места на диске достаточно — беспокоиться не о чем.",
    };

    /// <summary>Иконка плитки «Здоровья»: диск/батарея/видеокарта/процессор/градусник — по типу находки.</summary>
    public string HealthIconKey
    {
        get
        {
            // Новые параметры «Здоровья» задают иконку прямо в данных — не плодим ветки по Id.
            if (_finding.Data?.GetValueOrDefault(FindingDataKeys.HealthIcon) is { Length: > 0 } dataIcon)
            {
                return dataIcon;
            }

            var id = _finding.Id;
            if (id.StartsWith("disk-health-", StringComparison.Ordinal))
            {
                return "disk";
            }

            if (id.StartsWith("health-battery", StringComparison.Ordinal))
            {
                // Уровень делений в батарейке = здоровье: зелёная полная (3), жёлтая 2, красная 1.
                return EffectiveSeverity switch
                {
                    Severity.Danger => "battery-low",
                    Severity.Warning => "battery-mid",
                    _ => "battery-full",
                };
            }

            if (id.StartsWith("temp-", StringComparison.Ordinal))
            {
                if (_finding.Title.Contains("Видеокарт", StringComparison.OrdinalIgnoreCase))
                {
                    return "gpu";
                }

                return _finding.Title.Contains("Процессор", StringComparison.OrdinalIgnoreCase) ? "cpu" : "thermometer";
            }

            return "health";
        }
    }

    private bool IsBatteryCard => HealthIconKey is "battery-full" or "battery-mid" or "battery-low";
    private bool IsDiskCard => HealthIconKey == "disk";
    private bool IsTempCard => _finding.Id.StartsWith("temp-", StringComparison.Ordinal);

    /// <summary>Размер иконки «Здоровья» — единый у всех (48), чтобы иконки и заголовки карточек стояли ровно (правка 790).</summary>
    public double HealthIconWidth => 48;

    public double HealthIconHeight => 48;

    /// <summary>Износ батареи в формате «3%» (для плашки в правом-верхнем углу); null — нет данных.</summary>
    private string? BatteryWear =>
        _finding.Data?.GetValueOrDefault("wear") is { Length: > 0 } wear ? wear + "%" : null;

    /// <summary>Значение метрики правого-верхнего угла, заданное прямо в данных (RAM, время работы, загрузка…); null — нет.</summary>
    private string? DataMetric => _finding.Data?.GetValueOrDefault("metric") is { Length: > 0 } m ? m : null;

    /// <summary>Есть ли метрика для правого-верхнего угла: из данных (новые параметры) либо температура/износ батареи.</summary>
    public bool HasTopMetric => HasNoData || DataMetric is not null || (IsTempCard && !string.IsNullOrWhiteSpace(Detail)) || (IsBatteryCard && BatteryWear is not null);

    /// <summary>Текст метрики правого-верхнего угла: из данных / температура (28 °C) / износ батареи (3%).</summary>
    // Прочерк вместо цифры, когда датчик молчит: «0 °C» человек читает как измеренный ноль (баг 2026-07-23).
    public string TopMetricText => HasNoData
        ? "—"
        : DataMetric ?? (IsBatteryCard ? BatteryWear ?? string.Empty : Detail ?? string.Empty);

    /// <summary>Серая подпись слева от метрики: из данных («занято», «работает»…) либо «износ» у батареи.</summary>
    public string TopMetricLabel => _finding.Data?.GetValueOrDefault("metricLabel") is { Length: > 0 } label
        ? label
        : IsBatteryCard ? "износ" : string.Empty;

    public bool HasTopMetricLabel => TopMetricLabel.Length > 0;

    /// <summary>Подсказка-«норма» для плитки «Здоровья» (эталонная температура) — показывается всплывающей
    /// подсказкой при наведении на значение; null, если нет (тогда тултип не появляется).</summary>
    public string? HealthHint => _finding.Data?.GetValueOrDefault("hint") is { Length: > 0 } hint ? hint : null;

    public bool HasHealthHint => HealthHint is not null;

    /// <summary>Диск с нераспознанным форматом (RAW) — буква не присвоена; показываем серую плашку в углу.</summary>
    public bool IsRawDisk => _finding.Data?.GetValueOrDefault("raw") == "1";

    /// <summary>Пояснение к плашке RAW (по наведению).</summary>
    public string RawTooltip =>
        "Формат этого диска Windows не распознаёт (RAW) — поэтому буква ему не присвоена. " +
        "Это не ошибка программы: так бывает у пустых/служебных или повреждённых разделов.";

    public string? Detail => _finding.Detail;

    public string Explain => _finding.Explain;

    public Severity Severity => _finding.Severity;

    /// <summary>Текстовая метка статуса (дублирует цвет — для доступности).</summary>
    public string StatusText => HasNoData
        // Показатель не измерен: любой вердикт («OK», «Совет») тут был бы враньём — датчик просто молчит.
        ? "Нет данных"
        : _finding.Severity switch
        {
            Severity.Ok => "OK",
            Severity.Info => "Совет",
            Severity.Warning => "Внимание",
            Severity.Danger => "Проблема",
            _ => string.Empty,
        };

    /// <summary>Показатель измерить не удалось (датчик молчит) — плитка не должна изображать вердикт.</summary>
    public bool HasNoData => !string.IsNullOrEmpty(_finding.Data?.GetValueOrDefault(FindingDataKeys.NoData));

    /// <summary>
    /// Заголовок подсекции внутри вкладки (для группировки списка). Пусто = без отдельного заголовка
    /// (тогда раздел показывается плоским списком). Сейчас подсекции есть у «Мусора»: диски, чистка,
    /// крупные файлы, крупные папки, дубликаты — чтобы они не смешивались (запрос пользователя).
    /// </summary>
    public string SectionTitle => _finding.Group == ScanGroup.Junk
        ? JunkSection()
        : _finding.Data?.GetValueOrDefault(FindingDataKeys.Section) ?? string.Empty;

    /// <summary>Порядок подсекции в списке (меньше — выше).</summary>
    public int SectionOrder => SectionTitle switch
    {
        // Угрозы: поведенческий детект майнеров — всегда первым блоком (запрос Ивана 1165).
        "Поведение процессов (майнеры)" => 0,
        // Автозапуск: сводка времени загрузки — над списком тормозов, затем сами программы автозапуска
        // отдельной подсекцией (чтобы граница разделов была видна — запрос Ивана 1135).
        "Скорость загрузки Windows" => 0,
        "Что дольше всего грузится при старте" => 1,
        "Новое в автозапуске" => 2,
        "Программы в автозапуске" => 5,
        "Можно безопасно очистить" => 1,
        "Кэш приложений" => 2,
        "Остатки удалённых программ" => 3,
        "Старые файлы в «Загрузках»" => 4,
        "Крупные файлы" => 5,
        "Крупные папки" => 6,
        "Одинаковые файлы (дубликаты)" => 7,
        "Для твоего компьютера" => 0,
        "Для подключённых устройств" => 1,
        "Подключённые устройства" => 2,
        "Инструменты обслуживания — запускать только при проблемах" => 9, // ниже реальных находок
        _ => 8,
    };

    private string JunkSection()
    {
        if (_finding.Id.StartsWith("appcache-", StringComparison.Ordinal))
        {
            return "Кэш приложений";
        }

        if (_finding.Id.StartsWith("old-download-", StringComparison.Ordinal))
        {
            return "Старые файлы в «Загрузках»";
        }

        if (_finding.Id.StartsWith("leftover-", StringComparison.Ordinal))
        {
            return "Остатки удалённых программ";
        }

        if (_finding.Id.StartsWith("disk-folder-", StringComparison.Ordinal))
        {
            return "Крупные папки";
        }

        if (HasCopies || _finding.Data?.GetValueOrDefault(FindingDataKeys.Kind) == FindingKinds.DuplicateGroup)
        {
            return "Одинаковые файлы (дубликаты)";
        }

        if (_finding.Id.StartsWith("largefile-", StringComparison.Ordinal))
        {
            return "Крупные файлы";
        }

        return "Можно безопасно очистить";
    }
}
