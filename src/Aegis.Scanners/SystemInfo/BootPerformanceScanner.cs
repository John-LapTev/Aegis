using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.SystemInfo;

/// <summary>
/// Скорость загрузки Windows (вкладка «Автозапуск»). Показывает РЕАЛЬНОЕ общее время загрузки (журнал
/// «Diagnostics-Performance», а не «влияние Низкое/Высокое» из Диспетчера задач) и РЕЙТИНГ программ, которые дольше
/// всего заняты при старте. ЧЕСТНО: программы стартуют параллельно, поэтому это НЕ «сэкономишь N секунд» — это время
/// активности программы при запуске (кандидат на отключение). Тормозящие программы, найденные в автозапуске, получают
/// кнопку «Отключить» прямо здесь. Системные компоненты Windows (Защитник/Проводник/Поиск) — только пометка «часть
/// системы», без совета отключать. Сводка времени — отдельной подсекцией сверху.
/// </summary>
public sealed class BootPerformanceScanner : IScanner
{
    private const string SummarySection = "Скорость загрузки Windows";
    private const string CulpritSection = "Что дольше всего грузится при старте";
    private static readonly TimeSpan CulpritFloor = TimeSpan.FromSeconds(2); // мельче не показываем — это шум
    private const int MaxCulprits = 8;

    private readonly IBootPerformanceProbe _probe;
    private readonly IAutostartProbe _autostartProbe;

    public BootPerformanceScanner(IBootPerformanceProbe probe, IAutostartProbe autostartProbe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(autostartProbe);
        _probe = probe;
        _autostartProbe = autostartProbe;
    }

    public ScanGroup Group => ScanGroup.Autostart;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var boot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var autostart = await _autostartProbe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = new List<Finding>();

        if (boot.BootDuration is null && boot.Culprits.Count == 0)
        {
            findings.Add(Make(Severity.Info, "boot-no-data", SummarySection, "Данных о скорости загрузки пока нет",
                "Windows ещё не записала замеры загрузки (это бывает на новых или недавно обновлённых системах) " +
                "или журнал производительности отключён. После нескольких обычных включений компьютера данные появятся.",
                InfoData()));
            return new ScanResult { Group = ScanGroup.Autostart, Findings = findings };
        }

        if (boot.BootDuration is TimeSpan duration)
        {
            findings.Add(OverallFinding(duration));
        }

        foreach (var culprit in boot.Culprits.Where(c => c.Impact >= CulpritFloor).Take(MaxCulprits))
        {
            findings.Add(CulpritFinding(culprit, autostart));
        }

        return new ScanResult { Group = ScanGroup.Autostart, Findings = findings };
    }

    private static Finding OverallFinding(TimeSpan duration)
    {
        var seconds = duration.TotalSeconds;
        var (severity, verdict) = seconds switch
        {
            < 60 => (Severity.Ok, "это нормальная скорость, всё в порядке"),
            < 100 => (Severity.Info, "это терпимо; можно ускорить, отключив лишние программы старта (ниже)"),
            _ => (Severity.Warning, "долговато — стоит отключить лишние программы старта (список ниже)"),
        };

        return Make(severity, "boot-duration", SummarySection,
            $"Компьютер загружается за {HumanTime(duration)}",
            $"От включения до готовности к работе проходит примерно {HumanTime(duration)} — {verdict}. " +
            "Дольше всего загрузку обычно растягивают программы, которые сами запускаются при старте Windows.",
            InfoData());
    }

    private static Finding CulpritFinding(BootCulprit culprit, IReadOnlyList<AutostartEntry> autostart)
    {
        var busy = HumanTime(culprit.Impact);

        // Системные компоненты Windows — только пометка «часть системы», без совета отключать.
        if (WindowsComponents.TryGetValue(StripExe(culprit.Name), out var friendly))
        {
            return Make(Severity.Ok, $"boot-culprit-{culprit.Kind}-{culprit.Name}", CulpritSection,
                $"{friendly} — часть Windows (занят ~{busy} при старте)",
                $"{friendly} при включении компьютера работает примерно {busy}. Это встроенный компонент Windows: он " +
                "нужен для работы системы, отключать его не нужно. Показываем просто для понимания, на что уходит время запуска.",
                InfoData());
        }

        // Тормозящую программу пытаемся найти в автозапуске — тогда даём кнопку «Отключить» прямо здесь.
        var match = culprit.Kind == BootCulpritKind.Application ? FindInAutostart(culprit.Name, autostart) : null;
        if (match?.FixData is { } fixData)
        {
            var data = new Dictionary<string, string>(fixData) { ["section"] = CulpritSection };
            return new Finding
            {
                Id = $"boot-culprit-{culprit.Kind}-{culprit.Name}",
                Group = ScanGroup.Autostart,
                Severity = Severity.Info,
                Title = $"«{culprit.Name}» дольше многих грузится при старте (~{busy})",
                Detail = match.Command,
                Explain = $"При включении Windows программа «{culprit.Name}» занята дольше многих — около {busy}. " +
                          "Программы старта запускаются одновременно, поэтому отключение не обязательно сэкономит ровно " +
                          "столько, но это первый кандидат, если хочешь ускорить включение. Она есть в автозапуске — " +
                          "можешь отключить её кнопкой справа (запускать вручную, когда понадобится).",
                Data = data,
            };
        }

        // Системная служба Windows (SysMain/поиск/обновления/телеметрия и т.п.) — НЕ предлагаем отключать: только справка.
        if (culprit.Kind == BootCulpritKind.Service && CoreServices.Contains(StripExe(culprit.Name)))
        {
            return Make(Severity.Ok, $"boot-culprit-{culprit.Kind}-{culprit.Name}", CulpritSection,
                $"Служба Windows «{culprit.Name}» — часть системы (занята ~{busy} при старте)",
                $"Служба «{culprit.Name}» при запуске Windows работает примерно {busy}. Это системная служба — она нужна " +
                "для работы Windows, отключать её не стоит. Показываем для понимания, на что уходит время запуска.",
                InfoData());
        }

        // Тормоз-СЛУЖБА (не системная) — даём кнопку «Отключить службу» (обратимо, с бэкапом): реально ускоряет запуск.
        if (culprit.Kind == BootCulpritKind.Service)
        {
            return new Finding
            {
                Id = $"boot-culprit-{culprit.Kind}-{culprit.Name}",
                Group = ScanGroup.Autostart,
                Severity = Severity.Info,
                Title = $"Служба «{culprit.Name}» дольше многих грузится при старте (~{busy})",
                Explain = $"Служба «{culprit.Name}» занята при запуске Windows дольше многих — около {busy}. Если это " +
                          "служба ненужной программы (например, фирменной утилиты вроде Dell SupportAssist), её можно " +
                          "отключить — запуск станет быстрее. Отключение обратимо (с бэкапом). Системные службы Windows так не трогай.",
                Data = new Dictionary<string, string>
                {
                    ["section"] = CulpritSection,
                    ["kind"] = FindingKinds.ServiceDisable,
                    ["service"] = StripExe(culprit.Name),
                },
            };
        }

        // Драйвер — только справка (его не удаляют, обновляют через раздел драйверов).
        if (culprit.Kind == BootCulpritKind.Driver)
        {
            return Make(Severity.Info, $"boot-culprit-{culprit.Kind}-{culprit.Name}", CulpritSection,
                $"Драйвер «{culprit.Name}» дольше многих грузится при старте (~{busy})",
                $"При включении Windows драйвер «{culprit.Name}» занят дольше многих — около {busy}. Драйверы отвечают " +
                "за оборудование, удалять их не нужно; ускорить может обновление драйвера, если он устарел.",
                InfoData());
        }

        // Программа (не в автозапуске, не служба) — даём «Удалить полностью»: Aegis найдёт её в установленных и снесёт
        // через деинсталлятор + вычистит остатки. Data["exe"] — чтобы кнопка «Удалить полностью» знала, кого искать.
        return new Finding
        {
            Id = $"boot-culprit-{culprit.Kind}-{culprit.Name}",
            Group = ScanGroup.Autostart,
            Severity = Severity.Info,
            Title = $"Программа «{culprit.Name}» дольше многих грузится при старте (~{busy})",
            Explain = $"При включении Windows программа «{culprit.Name}» занята дольше многих — около {busy}. В списке " +
                      "автозапуска её нет — это фоновая часть программы или её служба. Если программа тебе не нужна, можешь " +
                      "удалить её целиком кнопкой «Удалить полностью» (Aegis найдёт её среди установленных, снесёт через " +
                      "деинсталлятор и вычистит остатки).",
            Data = new Dictionary<string, string> { ["section"] = CulpritSection, ["exe"] = culprit.Name },
        };
    }

    /// <summary>Ищет тормозящую программу в автозапуске по совпадению имени exe в команде запуска.</summary>
    private static AutostartEntry? FindInAutostart(string culpritName, IReadOnlyList<AutostartEntry> autostart)
    {
        var exe = culpritName.Contains('.') ? culpritName : culpritName + ".exe";
        foreach (var entry in autostart)
        {
            if (!string.IsNullOrWhiteSpace(entry.Command)
                && entry.Command.Contains(exe, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Человеческая запись длительности: «42 сек» или «1 мин 20 сек».</summary>
    private static string HumanTime(TimeSpan value)
    {
        var totalSeconds = (int)Math.Round(value.TotalSeconds);
        if (totalSeconds < 60)
        {
            return $"{totalSeconds} сек";
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return seconds == 0 ? $"{minutes} мин" : $"{minutes} мин {seconds} сек";
    }

    private static Finding Make(Severity severity, string id, string section, string title, string explain,
        Dictionary<string, string>? extraData)
    {
        var data = extraData ?? new Dictionary<string, string>();
        data["section"] = section;
        return new Finding
        {
            Id = id,
            Group = ScanGroup.Autostart,
            Severity = severity,
            Title = title,
            Explain = explain,
            Data = data,
        };
    }

    /// <summary>Пометка «информационная находка»: без квадратика выделения и кнопки «Безопасно» (запрос Ивана).</summary>
    private static Dictionary<string, string> InfoData() => new() { ["info"] = "1" };

    /// <summary>Убирает расширение .exe (регистронезависимо) для сопоставления по имени.</summary>
    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    /// <summary>Системные службы Windows, которые НЕ предлагаем отключать (только справка), даже если тормозят загрузку.</summary>
    private static readonly HashSet<string> CoreServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "SysMain", "Superfetch", "wuauserv", "WSearch", "DiagTrack", "BITS", "wscsvc", "WinDefend",
        "Schedule", "Themes", "AudioSrv", "Audiosrv", "Spooler", "EventLog", "Dnscache", "Dhcp",
        "LanmanServer", "LanmanWorkstation", "ProfSvc", "gpsvc", "CryptSvc", "DcomLaunch", "RpcSs",
        "nsi", "Power", "SamSs", "UsoSvc", "TrustedInstaller", "sppsvc", "WlanSvc", "Netman",
    };

    /// <summary>Известные системные компоненты Windows → понятное имя (их не отключают, показываем «это норма»).</summary>
    private static readonly Dictionary<string, string> WindowsComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MsMpEng"] = "Антивирус Windows (Защитник)",
        ["explorer"] = "Проводник Windows",
        ["SearchIndexer"] = "Поиск Windows",
        ["SearchHost"] = "Поиск Windows",
        ["SearchApp"] = "Поиск Windows",
        ["dwm"] = "Отрисовка окон Windows",
        ["svchost"] = "Системная служба Windows",
        ["RuntimeBroker"] = "Служба безопасности Windows",
        ["sihost"] = "Оболочка Windows",
        ["taskhostw"] = "Планировщик задач Windows",
        ["ctfmon"] = "Ввод и раскладка Windows",
        ["SecurityHealthService"] = "Центр безопасности Windows",
        ["SecurityHealthSystray"] = "Центр безопасности Windows",
        ["StartMenuExperienceHost"] = "Меню «Пуск» Windows",
        ["ShellExperienceHost"] = "Оболочка Windows",
        ["SystemSettings"] = "Параметры Windows",
        ["spoolsv"] = "Служба печати Windows",
        ["fontdrvhost"] = "Служба шрифтов Windows",
    };
}
