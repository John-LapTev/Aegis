using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Programs;

/// <summary>
/// Глубокая чистка кэша установленных приложений (группа <see cref="ScanGroup.Junk"/>, подсекция «Кэш
/// приложений»). Подход Winapp2, но СВОИ безопасные правила (clean-room): чистим только кэш/временное,
/// которое программа пересоздаёт сама. Очистка — обратимая, в Корзину (через обычный мусорный механизм).
/// </summary>
public sealed class AppCacheScanner : IScanner
{
    private const string Section = "Кэш приложений";

    private readonly IAppCacheProbe _probe;

    public AppCacheScanner(IAppCacheProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = snapshot.Apps.Select(ToFinding).ToList();
        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

    private static Finding ToFinding(AppCacheItem app)
    {
        var size = HumanSize.Format(app.Bytes);
        var (idSuffix, severity, title, detail, explain, noBatch) = app.Category switch
        {
            AppCacheCategory.Cookies => (
                "-cookies", Severity.Warning,
                $"«{app.Name}» — cookie (выйдешь из аккаунтов)",
                "файлы входов",
                $"Это файлы cookie браузера «{app.Name}» — в них хранятся твои ВХОДЫ на сайты. Если очистить, " +
                "тебя выкинет из всех аккаунтов (Google, почта, соцсети) и придётся входить заново. Чисти, только " +
                "если точно этого хочешь. Удалённое уходит в Корзину.",
                true),
            AppCacheCategory.History => (
                "-history", Severity.Info,
                $"«{app.Name}» — история браузера",
                "журнал посещений",
                $"Это история посещённых сайтов в браузере «{app.Name}». Очистка уберёт журнал посещений " +
                "(на входы и закладки не влияет). Чисти по желанию. Удалённое уходит в Корзину.",
                true),
            _ => (
                "-cache", Severity.Info,
                $"Кэш приложения «{app.Name}» — {size}",
                $"{app.FileCount} файлов кэша",
                $"Это временный кэш программы «{app.Name}» (ускоряет её работу). Чистим ТОЛЬКО кэш — твои входы " +
                "(cookie), история, закладки и пароли НЕ трогаются. Программа создаст кэш заново. Очистка освободит " +
                "место; удалённое уходит в Корзину.",
                false),
        };

        // Без «kind» + список путей — очистка идёт через JunkCleanupFix (в Корзину) и показывает плашку выбора.
        var data = new Dictionary<string, string>
        {
            ["paths"] = string.Join("|", app.Targets),
            ["section"] = Section,
            ["bytes"] = app.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture), // для суммы «Можно освободить» (правка 960)
        };
        if (noBatch)
        {
            data["noBatch"] = "1"; // cookie/история — только осознанно, не массовой кнопкой
        }

        return new Finding
        {
            Id = "appcache-" + app.Name + idSuffix,
            Group = ScanGroup.Junk,
            Severity = severity,
            Title = title,
            Detail = detail,
            Explain = explain,
            Data = data,
        };
    }
}
