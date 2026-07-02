using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Apps;

/// <summary>
/// Встроенный UWP-хлам (группа <see cref="ScanGroup.Settings"/>): предустановленные промо-игры и приложения
/// (Candy Crush, Bing News/Weather, пасьянсы и т.п.), которые редко нужны. Помечаются как «можно удалить»
/// (severity Info — это выбор, а не проблема). Удаление обратимо: приложение можно вернуть из Microsoft Store.
/// </summary>
public sealed class AppxBloatScanner : IScanner
{
    private readonly IAppxProbe _probe;

    public AppxBloatScanner(IAppxProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _probe.FindBloatAsync(cancellationToken).ConfigureAwait(false);
        var findings = apps.Select(CreateFinding).ToList();
        return new ScanResult { Group = ScanGroup.Settings, Findings = findings };
    }

    private static Finding CreateFinding(AppxApp app) => new()
    {
        Id = $"appx-{app.PackageFullName}",
        Group = ScanGroup.Settings,
        Severity = Severity.Info,
        Title = $"Встроенное приложение: {app.Name}",
        Detail = app.Category,
        Explain = $"Это приложение ({app.Category}) обычно предустановлено в Windows и редко используется — занимает " +
                  "место и может работать в фоне. Можно удалить: на нужные программы это не повлияет, а при желании " +
                  "приложение легко вернуть бесплатно из Microsoft Store.",
        Data = new Dictionary<string, string>
        {
            ["kind"] = FindingKinds.AppxRemove,
            ["package"] = app.PackageFullName,
            ["name"] = app.Name,
        },
    };
}
