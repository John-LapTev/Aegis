using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Раздел «Дашборд»: плитки-переходы (Сканирование/Здоровье) + «Удаление программ» (список установленных,
/// удаление с чисткой остатков) + «грубое» удаление занятого файла/папки. Отдельная VM — не раздуваем главную.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IInstalledProgramsProbe _programsProbe;
    private readonly IProgramUninstaller _uninstaller;
    private readonly IForceDeleteService _forceDelete;
    private readonly InstallMonitor _installMonitor;
    private readonly IActivityStatsStore _stats;
    private readonly ILeftoverService _leftovers;
    private readonly ILeftoverPrompt _leftoverPrompt;
    private readonly IAppIconLoader _iconLoader;
    private readonly IAiAssistant _aiAssistant;
    private readonly Action<string> _navigate;

    public DashboardViewModel(
        IInstalledProgramsProbe programsProbe,
        IProgramUninstaller uninstaller,
        IForceDeleteService forceDelete,
        InstallMonitor installMonitor,
        IActivityStatsStore stats,
        ILeftoverService leftovers,
        ILeftoverPrompt leftoverPrompt,
        IAppIconLoader iconLoader,
        IAiAssistant aiAssistant,
        Action<string> navigate)
    {
        _programsProbe = programsProbe;
        _uninstaller = uninstaller;
        _forceDelete = forceDelete;
        _installMonitor = installMonitor;
        _stats = stats;
        _leftovers = leftovers;
        _leftoverPrompt = leftoverPrompt;
        _iconLoader = iconLoader;
        _aiAssistant = aiAssistant;
        _navigate = navigate;
        RefreshStats();
    }

    // «Сравнить состояние»: накопленная статистика (что Aegis сделал для компьютера).
    [ObservableProperty] private string _junkCleanedText = "0";
    [ObservableProperty] private string _driversUpdatedText = "0";
    [ObservableProperty] private string _programsRemovedText = "0";
    [ObservableProperty] private string _threatsNeutralizedText = "0";

    /// <summary>Перечитать накопленную статистику из хранилища (после чистки/удаления/починки).</summary>
    public void RefreshStats()
    {
        var s = _stats.Load();
        JunkCleanedText = s.JunkCleanedBytes > 0 ? HumanSize.Format(s.JunkCleanedBytes) : "0";
        DriversUpdatedText = s.DriversUpdated.ToString();
        ProgramsRemovedText = s.ProgramsRemoved.ToString();
        ThreatsNeutralizedText = s.ThreatsNeutralized.ToString();
    }

    /// <summary>Открыть раздел «Сравнить состояние» (плитка Дашборда).</summary>
    [RelayCommand]
    private void GoCompare()
    {
        RefreshStats();
        _navigate("compare");
    }

    /// <summary>Все установленные программы (полный список из реестра).</summary>
    private readonly List<InstalledProgramViewModel> _all = [];

    /// <summary>Отфильтрованные поиском программы (то, что показываем).</summary>
    public ObservableCollection<InstalledProgramViewModel> VisiblePrograms { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrograms))]
    private bool _loaded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Строка поиска по названию/издателю.</summary>
    [ObservableProperty]
    private string _search = string.Empty;

    /// <summary>Варианты сортировки списка программ (для выпадающего списка).</summary>
    public IReadOnlyList<string> SortOptions { get; } =
    [
        "По названию (А→Я)", "По названию (Я→А)",
        "Сначала новые", "Сначала старые",
        "Сначала мелкие", "Сначала крупные",
    ];

    /// <summary>Выбранный вариант сортировки (индекс в <see cref="SortOptions"/>).</summary>
    [ObservableProperty]
    private int _selectedSortIndex;

    partial void OnSelectedSortIndexChanged(int value) => ApplyFilter();

    /// <summary>Показывать ли системные и скрытые программы Windows (по запросу, правка Ивана 1201).</summary>
    [ObservableProperty]
    private bool _showHidden;

    partial void OnShowHiddenChanged(bool value)
    {
        if (!IsLoading)
        {
            _ = LoadAsync();
        }
    }

    public bool HasPrograms => VisiblePrograms.Count > 0;

    public int TotalCount => _all.Count;

    partial void OnSearchChanged(string value) => ApplyFilter();

    /// <summary>Открыть раздел сканирования (плитка «Проверка компьютера»).</summary>
    [RelayCommand]
    private void GoScans() => _navigate("scans");

    /// <summary>Открыть раздел «Здоровье» (плитка).</summary>
    [RelayCommand]
    private void GoHealth() => _navigate("health");

    /// <summary>Открыть раздел «Тесты» (плитка).</summary>
    [RelayCommand]
    private void GoTests() => _navigate("tests");

    /// <summary>Открыть раздел «Удаление программ» (плитка → отдельный раздел, а не скролл вниз).</summary>
    [RelayCommand]
    private void GoUninstall() => _navigate("uninstall");

    /// <summary>Открыть раздел «Оптимизация» (плитка).</summary>
    [RelayCommand]
    private void GoOptimize() => _navigate("optimize");

    /// <summary>Открыть раздел «Игры» — игровой режим (плитка).</summary>
    [RelayCommand]
    private void GoGames() => _navigate("games");

    /// <summary>Открыть раздел «Удалить занятый файл/папку» (плитка).</summary>
    [RelayCommand]
    private void GoForceDelete() => _navigate("forcedelete");

    /// <summary>Загрузить список установленных программ (при первом входе в раздел).</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusText = "Читаю список программ…";

        // Сбрасываем прошлый ответ ИИ про дубли версий: его кнопки «Удалить» ссылались на записи СТАРОГО списка,
        // а после перечитки список другой — иначе висит устаревший блок с осиротевшими действиями (аудит 2026-07-04).
        VersionAnswer = string.Empty;
        VersionDuplicates.Clear();
        OnPropertyChanged(nameof(HasVersionDuplicates));

        try
        {
            var programs = await Task.Run(() => _programsProbe.FindAsync(ShowHidden)).ConfigureAwait(true);
            _all.Clear();
            foreach (var program in programs)
            {
                var vm = new InstalledProgramViewModel(program, _aiAssistant) { SelectionChanged = OnSelectionChanged };
                _all.Add(vm);
            }

            OnSelectionChanged();
            ApplyFilter();
            LoadIconsInBackground(); // значки программ подтягиваем не блокируя UI
            Loaded = true;
            StatusText = _all.Count > 0 ? $"Установлено программ: {_all.Count}." : "Установленных программ не найдено.";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось прочитать список программ: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Удалить программу штатным деинсталлятором и дочистить остатки (пустая папка + запись реестра).</summary>
    [RelayCommand]
    private async Task UninstallAsync(InstalledProgramViewModel? item)
    {
        if (item is null || item.IsUninstalling || item.IsRemoved || !item.CanUninstall)
        {
            return;
        }

        item.IsUninstalling = true;
        StatusText = $"Удаляю «{item.Name}»…";
        try
        {
            // Штатным деинсталлятором БЕЗ авто-чистки — остатки покажем и вычистим в отдельном окне (в духе Revo).
            var result = await _uninstaller.UninstallAsync(item.Program, cleanLeftovers: false).ConfigureAwait(true);
            if (!result.Success)
            {
                StatusText = $"«{item.Name}»: {result.Message}";
                return;
            }

            // ЧЕСТНАЯ проверка: реально ли программа удалилась. У игр/лаунчеров деинсталлятор часто возвращает «успех»,
            // но программу не убирает — тогда НЕ рапортуем «удалена», а до-удаляем принудительно (папку + реестр).
            var fullyRemoved = !result.StillRegistered;
            if (fullyRemoved)
            {
                item.IsRemoved = true;
                _stats.AddProgramsRemoved();
                RefreshStats();
            }

            StatusText = fullyRemoved
                ? $"«{item.Name}» удалена. Ищу остатки…"
                : $"Деинсталлятор не убрал «{item.Name}» до конца — ищу, что осталось, чтобы вычистить принудительно…";

            var found = await _leftovers.ScanAsync(item.Program).ConfigureAwait(true);
            if (found.Count == 0)
            {
                StatusText = fullyRemoved
                    ? $"«{item.Name}» удалена. Остатков не найдено — чисто."
                    : $"«{item.Name}»: штатный деинсталлятор не сработал, но и остатков не найдено — проверь вручную.";
                return;
            }

            var chosen = await _leftoverPrompt.ConfirmAsync(item.Name, found, fullyRemoved).ConfigureAwait(true);
            if (chosen.Count == 0)
            {
                StatusText = $"«{item.Name}»: остатки ({found.Count}) оставлены по твоему выбору.";
                return;
            }

            // Остатки удалённой программы удаляем НАСОВСЕМ (файлы/папки), реестр — с бэкапом.
            var removed = await _leftovers.RemoveAsync(chosen).ConfigureAwait(true);
            var countText = removed < chosen.Count ? $"{removed} из {chosen.Count}" : removed.ToString();

            // Если штатно не удалилось, но мы вычистили её файлы/реестр — теперь считаем удалённой.
            if (!fullyRemoved && removed > 0)
            {
                item.IsRemoved = true;
                _stats.AddProgramsRemoved();
                RefreshStats();
            }

            StatusText = $"«{item.Name}»: вычищено {countText} (файлы и папки — насовсем, реестр — с бэкапом).";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось удалить: " + ex.Message;
        }
        finally
        {
            item.IsUninstalling = false;
            OnSelectionChanged(); // удалённая перестаёт считаться выбранной (счётчики «Удалить выбранные (N)»)
        }
    }

    /// <summary>«Грубое» удаление выбранного пути: завершить мешающие процессы и убрать в Корзину (вызывается из code-behind после выбора файла/папки).</summary>
    public async Task ForceDeleteAsync(string path)
    {
        StatusText = "Освобождаю и удаляю…";
        try
        {
            var result = await _forceDelete.DeleteAsync(path).ConfigureAwait(true);
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось удалить: " + ex.Message;
        }
    }

    /// <summary>Показывает раздел «Установка с наблюдением».</summary>
    public bool IsWatchingInstall { get; private set; }

    /// <summary>
    /// Установка с наблюдением (в духе Revo): снимает состояние ДО, запускает выбранный установщик, ждёт его
    /// завершения, снимает состояние ПОСЛЕ и запоминает «след» — всё, что добавила программа. Потом её удаление
    /// вычистит эти остатки полностью. Вызывается из code-behind после выбора файла установщика.
    /// </summary>
    public async Task WatchInstallAsync(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || IsWatchingInstall)
        {
            return;
        }

        IsWatchingInstall = true;
        StatusText = "Запоминаю состояние системы до установки…";
        try
        {
            var before = new HashSet<string>(
                (await Task.Run(() => _programsProbe.FindAsync()).ConfigureAwait(true)).Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);
            var baseline = await _installMonitor.CaptureBaselineAsync().ConfigureAwait(true);

            StatusText = "Установщик запущен — пройдите установку. Жду её завершения…";
            await RunInstallerAsync(installerPath).ConfigureAwait(true);

            StatusText = "Записываю, что добавила программа…";
            var after = await Task.Run(() => _programsProbe.FindAsync()).ConfigureAwait(true);
            var programName = after.Select(p => p.Name).FirstOrDefault(name => !before.Contains(name))
                              ?? Path.GetFileNameWithoutExtension(installerPath);

            var trace = await _installMonitor.RecordAsync(programName, baseline, DateTimeOffset.Now).ConfigureAwait(true);
            StatusText = $"Готово. Запомнил след «{programName}»: {trace.AddedPaths.Count} файлов/папок и " +
                         $"{trace.AddedRegistryKeys.Count} веток реестра. Теперь удаление этой программы вычистит всё до конца.";

            await LoadAsync().ConfigureAwait(true); // обновим список установленных
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось выполнить установку с наблюдением: " + ex.Message;
        }
        finally
        {
            IsWatchingInstall = false;
        }
    }

    private static async Task RunInstallerAsync(string installerPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo { FileName = installerPath, UseShellExecute = true },
        };
        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);
    }

    // «Проверить дубли версий (AI)» — нейросеть смотрит распространяемые пакеты (Visual C++, .NET…) и советует, что лишнее (правка Ивана 1201).
    [ObservableProperty]
    private bool _isCheckingVersions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVersionAnswer))]
    private string _versionAnswer = string.Empty;

    public bool HasVersionAnswer => VersionAnswer.Length > 0;

    public bool CanCheckVersions => _aiAssistant.IsConfigured;

    /// <summary>Пакеты, которые ИИ пометил как удаляемые (дубли/старьё) — те же VM из списка, с готовой кнопкой «Удалить» (правка Ивана 1221).</summary>
    public ObservableCollection<InstalledProgramViewModel> VersionDuplicates { get; } = new();

    public bool HasVersionDuplicates => VersionDuplicates.Count > 0;

    // Массовое удаление по галочкам (правка Ивана 1227): считаем выбранные отдельно для основного списка и для списка дублей.
    public int SelectedProgramsCount => VisiblePrograms.Count(p => p.IsSelected && !p.IsRemoved);

    public bool HasSelectedPrograms => SelectedProgramsCount > 0;

    public int SelectedDuplicatesCount => VersionDuplicates.Count(p => p.IsSelected && !p.IsRemoved);

    public bool HasSelectedDuplicates => SelectedDuplicatesCount > 0;

    public string SelectedProgramsText => $"Удалить выбранные ({SelectedProgramsCount})";

    public string SelectedDuplicatesText => $"Удалить выбранные ({SelectedDuplicatesCount})";

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedProgramsCount));
        OnPropertyChanged(nameof(HasSelectedPrograms));
        OnPropertyChanged(nameof(SelectedDuplicatesCount));
        OnPropertyChanged(nameof(HasSelectedDuplicates));
        OnPropertyChanged(nameof(SelectedProgramsText));
        OnPropertyChanged(nameof(SelectedDuplicatesText));
    }

    [RelayCommand]
    private Task UninstallSelectedAsync() => UninstallManyAsync(VisiblePrograms.Where(p => p.IsSelected).ToList());

    [RelayCommand]
    private Task UninstallSelectedDuplicatesAsync() => UninstallManyAsync(VersionDuplicates.Where(p => p.IsSelected).ToList());

    /// <summary>Удаляет выбранные галочками программы ПО ОЧЕРЕДИ (каждая — штатным деинсталлятором + окно остатков).</summary>
    private async Task UninstallManyAsync(IReadOnlyList<InstalledProgramViewModel> items)
    {
        var targets = items.Where(p => !p.IsRemoved && !p.IsUninstalling && p.CanUninstall).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var item in targets)
        {
            item.IsSelected = false;
            await UninstallAsync(item).ConfigureAwait(true);
        }

        OnSelectionChanged();
        StatusText = $"Готово: обработано выбранных — {targets.Count} шт. (по очереди).";
    }

    [RelayCommand]
    private void DismissVersionAnswer()
    {
        VersionAnswer = string.Empty;
        VersionDuplicates.Clear();
        OnPropertyChanged(nameof(HasVersionDuplicates));
    }

    [RelayCommand]
    private async Task CheckDuplicateVersionsAsync()
    {
        if (IsCheckingVersions)
        {
            return;
        }

        // Уже удалённые в этой сессии пакеты в проверку НЕ берём — иначе нейросеть находит их снова и снова
        // при повторном нажатии «Проверить дубли версий» (баг 1244).
        var markers = new[] { "visual c++", "redistributable", ".net", "runtime", "directx", "webview" };
        var redist = _all
            .Where(p => !p.IsRemoved && markers.Any(m => p.Name.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        VersionDuplicates.Clear();
        OnPropertyChanged(nameof(HasVersionDuplicates));

        if (redist.Count == 0)
        {
            VersionAnswer = "Распространяемых пакетов (Visual C++, .NET и т.п.) не нашлось — проверять на дубли нечего.";
            return;
        }

        IsCheckingVersions = true;
        VersionAnswer = string.Empty;
        try
        {
            var list = string.Join("\n", redist
                .Select(p => "- " + p.Name + (string.IsNullOrWhiteSpace(p.Program.Version) ? string.Empty : $" ({p.Program.Version})"))
                .Distinct());
            var prompt = AiSystemPrompt.Text + "\n" +
                "Ты — эксперт по распространяемым пакетам Windows. Пользователь НЕ разбирается в компьютерах и полностью " +
                "доверяет твоему решению: что ты пометишь к удалению, он удалит не думая. Поэтому ошибаться нельзя — " +
                "лучше НЕ удалить нужное, чем сломать программы.\n\n" +
                "ТОЧНЫЕ ФАКТЫ, которыми обязан руководствоваться:\n" +
                "1) Microsoft Visual C++ Redistributable ПО ГОДАМ — это РАЗНЫЕ независимые пакеты, а не версии одного: " +
                "2005, 2008, 2010, 2012, 2013 — каждый самостоятельный рантайм. Игры и программы требуют КОНКРЕТНЫЙ год. " +
                "НИКОГДА не помечай их к удалению «потому что старые» — без них перестанут запускаться программы.\n" +
                "2) ИСКЛЮЧЕНИЕ: 2015, 2017, 2019, 2022 — это ОДНА линейка (рантайм 14.x), новее включает старое. " +
                "Из них одной разрядности разумно оставить только САМЫЙ новый (обычно 2015-2022), более старые той же " +
                "разрядности из этой четвёрки можно удалить.\n" +
                "3) x86 (32-бит) и x64 (64-бит) — РАЗНЫЕ пакеты, оба нужны разным программам. НИКОГДА не удаляй один " +
                "из-за наличия другого.\n" +
                "4) .NET / .NET Runtime / ASP.NET Core / Windows Desktop Runtime РАЗНЫХ мажорных версий (6, 7, 8, 9) " +
                "сосуществуют — приложения требуют конкретную. Не помечай к удалению просто как «старую».\n" +
                "4b) Microsoft .NET Framework (2.0 / 3.5 / 4.x) — это ОТДЕЛЬНЫЙ встроенный компонент Windows, он НЕ " +
                "заменяется новым .NET (5/6/7/8/9) и нужен множеству старых программ. НИКОГДА не помечай его к удалению.\n" +
                "5) Реально лишнее = только точный дубликат того же самого ИЛИ устаревшая версия ВНУТРИ одной линейки и " +
                "одной разрядности, когда новее уже установлена (пункт 2). Всё остальное — оставить.\n" +
                "6) Если явных дублей нет — так и скажи прямо: удалять нечего, всё разное и нужное. Это нормальный ответ.\n\n" +
                "Установленные пакеты:\n" + list +
                "\n\nСначала — короткое объяснение простыми словами (что оставляем и почему, что реально лишнее). " +
                "Затем, ТОЛЬКО для реально лишних по правилам выше, каждый отдельной строкой РОВНО так: " +
                "DEL::<точное название пакета из списка>. Если удалять нечего — не пиши ни одной строки DEL.";

            var result = await _aiAssistant.AskAsync(prompt).ConfigureAwait(true);
            if (!result.Success)
            {
                VersionAnswer = result.Error ?? "ИИ-помощник не ответил.";
                return;
            }

            var (text, delNames) = RedistDeletionMatcher.SplitDeletions(result.Text);
            VersionAnswer = text.Length > 0 ? text : "Готово. Дубликатов и явно лишних версий ИИ не нашёл.";

            // Сопоставляем имена из ответа ИИ с реально установленными БЕЗОПАСНО: разрядность обязана совпадать,
            // при неоднозначности пакет не помечаем (см. RedistDeletionMatcher — защита от подмены x86/x64).
            var installedNames = redist.Select(p => p.Name).ToList();
            foreach (var name in delNames)
            {
                var matchedName = RedistDeletionMatcher.MatchInstalled(name, installedNames);
                if (matchedName is null)
                {
                    continue;
                }

                var match = redist.FirstOrDefault(p => p.Name == matchedName);
                if (match is not null && match.CanUninstall && !VersionDuplicates.Contains(match))
                {
                    VersionDuplicates.Add(match);
                }
            }

            OnPropertyChanged(nameof(HasVersionDuplicates));
            OnSelectionChanged();
        }
        catch (Exception ex)
        {
            VersionAnswer = "Не удалось проверить: " + ex.Message;
        }
        finally
        {
            IsCheckingVersions = false;
        }
    }

    /// <summary>Извлекает значки программ в фоне (медленно) и ставит их на UI-потоке — список не подтормаживает.</summary>
    private void LoadIconsInBackground()
    {
        var items = _all.ToList();
        _ = Task.Run(() =>
        {
            foreach (var item in items)
            {
                var path = item.Program.IconPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var png = _iconLoader.LoadPng(path);
                if (png is { Length: > 0 })
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => item.ApplyIcon(png));
                }
            }
        });
    }

    private void ApplyFilter()
    {
        VisiblePrograms.Clear();
        var query = Search?.Trim() ?? string.Empty;
        var filtered = query.Length == 0
            ? _all
            : _all.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (p.Program.Publisher?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

        // Сортировка по выбранному варианту (название / дата / размер), порядок как в SortOptions.
        filtered = SelectedSortIndex switch
        {
            1 => filtered.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase),
            2 => filtered.OrderByDescending(p => p.Program.InstallDate ?? DateOnly.MinValue), // сначала новые
            3 => filtered.OrderBy(p => p.Program.InstallDate ?? DateOnly.MaxValue),           // сначала старые
            4 => filtered.OrderBy(p => p.Program.EstimatedSizeBytes),                          // сначала мелкие
            5 => filtered.OrderByDescending(p => p.Program.EstimatedSizeBytes),                // сначала крупные
            _ => filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
        };

        foreach (var program in filtered)
        {
            VisiblePrograms.Add(program);
        }

        // Понятный статус при поиске (иначе оставался устаревший «Установлено программ: N», а список пустой — аудит 2026-07-04).
        if (!IsLoading && query.Length > 0)
        {
            StatusText = VisiblePrograms.Count == 0
                ? $"По запросу «{query}» ничего не найдено."
                : $"Найдено: {VisiblePrograms.Count}.";
        }
        else if (!IsLoading && query.Length == 0)
        {
            StatusText = _all.Count > 0 ? $"Установлено программ: {_all.Count}." : "Установленных программ не найдено.";
        }

        OnPropertyChanged(nameof(HasPrograms));
        OnSelectionChanged();
    }
}
