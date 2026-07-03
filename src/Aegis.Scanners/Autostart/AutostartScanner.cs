using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Autostart;

/// <summary>
/// Сканер автозапуска (блок «А», группа <see cref="ScanGroup.Autostart"/>). Доверенные системные
/// записи Microsoft пропускает; остальные классифицирует по подписи и расположению:
/// неподписанное из временной папки — «Проблема», прочее без подписи или лишнее подписанное — «Внимание».
/// </summary>
public sealed class AutostartScanner : IScanner
{
    private readonly IAutostartProbe _probe;

    public AutostartScanner(IAutostartProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Autostart;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);

        var findings = new List<Finding>();
        foreach (var entry in entries)
        {
            // LOLBin-злоупотребление проверяем ПЕРВЫМ: сам бинарь (powershell и т.п.) подписан Microsoft,
            // поэтому проверка подписи его пропустила бы — а команда внутри может быть вредоносной.
            var abuse = LolBinHeuristics.DetectAbuse(entry.Command);
            if (abuse is not null)
            {
                findings.Add(CreateLolBinFinding(entry, abuse));
            }
            else if (!IsTrustedSystemEntry(entry))
            {
                findings.Add(CreateFinding(entry));
            }
        }

        return new ScanResult { Group = ScanGroup.Autostart, Findings = findings };
    }

    /// <summary>Подсекция обычных записей автозапуска — чтобы отделить их от подсекций «Скорость загрузки» (запрос Ивана 1135).</summary>
    private const string AutostartSection = "Программы в автозапуске";

    private static Finding CreateLolBinFinding(AutostartEntry entry, string reason)
    {
        var data = entry.FixData is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(entry.FixData);
        data["section"] = AutostartSection;

        return new Finding
        {
            Id = $"autostart-lolbin-{entry.Source}-{entry.Name}",
            Group = ScanGroup.Autostart,
            Severity = Severity.Danger,
            Title = "Маскировка через системную программу в автозапуске",
            Detail = entry.Command,
            Explain = $"При запуске Windows прячется опасная команда внутри системной программы ({reason}). " +
                      "Так вирусы маскируются под «доверенные» программы Windows, чтобы их не заметили. " +
                      "Уберём это из автозапуска — обратимо, с бэкапом.",
            Data = data,
        };
    }

    private static bool IsTrustedSystemEntry(AutostartEntry entry) =>
        entry.Signature == SignatureStatus.Signed && TrustedPublishers.IsMicrosoft(entry.Publisher);

    // Приложения, которые обычно заметно замедляют загрузку (лаунчеры/синхронизация/оверлеи/обновлялки).
    private static readonly string[] HeavyStartupMarkers =
    [
        "steam", "epicgames", "epic games", "discord", "spotify", "onedrive", "dropbox", "google drive",
        "googledrivefs", "creative cloud", "creativecloud", "ccxprocess", "adobe", "itunes", "skype", "teams",
        "razer", "synapse", "logitech", "lghub", "corsair", "icue", "armoury", "nahimic", "afterburner",
        "overwolf", "wallpaper engine", "nvidia", "geforce", "java", "update",
    ];

    private static Finding CreateFinding(AutostartEntry entry)
    {
        // Оценка влияния на загрузку (эвристика по типу программы) — считаем ДО вердикта, чтобы не пугать
        // «замедляет включение» подписанную программу, у которой влияние обычное.
        var text = $"{entry.Name} {entry.Command}".ToLowerInvariant();
        var heavy = HeavyStartupMarkers.Any(text.Contains);
        var impact = heavy ? "высокое" : "обычное";

        var (severity, title, explain) = Classify(entry, heavy);

        var data = entry.FixData is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(entry.FixData);
        data["category"] = "Влияние на загрузку: " + impact;
        data["section"] = AutostartSection;

        return new Finding
        {
            Id = $"autostart-{entry.Source}-{entry.Name}",
            Group = ScanGroup.Autostart,
            Severity = severity,
            Title = title,
            Detail = entry.Command,
            Explain = explain,
            Data = data,
        };
    }

    private static (Severity Severity, string Title, string Explain) Classify(AutostartEntry entry, bool heavy)
    {
        var suspicious = PathHeuristics.IsSuspiciousLocation(entry.Command);

        if (entry.Signature == SignatureStatus.Unsigned && suspicious)
        {
            return (
                Severity.Danger,
                "Неизвестная программа в автозапуске",
                "Программа сама включается при запуске компьютера, а откуда она — непонятно: нет подписи и " +
                "лежит во временной папке. Так часто прячутся вирусы и майнеры. Уберём её из автозапуска и " +
                "отправим в карантин — перед этим сделаем бэкап.");
        }

        if (entry.Signature is SignatureStatus.Unsigned or SignatureStatus.Unknown)
        {
            return (
                Severity.Warning,
                "Программа без цифровой подписи в автозапуске",
                "Эта программа стартует вместе с Windows, но у неё нет подтверждённого издателя. Это не обязательно " +
                "вирус, но стоит проверить. Можно отключить автозапуск — программу останется запускать вручную.");
        }

        // Подписанная программа. Пугаем «замедляет» ТОЛЬКО если влияние на загрузку действительно высокое;
        // иначе это просто информация — не выдаём обычную программу за проблему (правка аудита).
        if (heavy)
        {
            return (
                Severity.Warning,
                "Лишнее в автозапуске замедляет включение",
                "Программа подписана и, скорее всего, безопасна, но она из тех, что заметно замедляют загрузку " +
                "компьютера. Уберём её из автозапуска — запускать можно будет вручную, когда нужно.");
        }

        return (
            Severity.Info,
            "Программа в автозапуске",
            "Программа подписана и, скорее всего, безопасна, и заметно загрузку не замедляет. Трогать не обязательно; " +
            "при желании можно убрать из автозапуска — запускать вручную, когда нужно.");
    }
}
