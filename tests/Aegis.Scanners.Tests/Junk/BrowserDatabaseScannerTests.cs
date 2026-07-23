using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Junk;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Junk;

public sealed class BrowserDatabaseScannerTests
{
    private const long Mb = 1024 * 1024;

    [Fact]
    public async Task BigGain_ShowsOneFindingPerBrowser()
    {
        var findings = await Scan([
            Database("Google Chrome", @"C:\U\Chrome\Default\History", 40 * Mb),
            Database("Google Chrome", @"C:\U\Chrome\Default\Favicons", 20 * Mb),
            Database("Mozilla Firefox", @"C:\U\FF\p1\places.sqlite", 30 * Mb),
        ]);

        Assert.Equal(2, findings.Count);
        var chrome = Assert.Single(findings, f => f.Title.Contains("Chrome"));
        Assert.Equal(FindingKinds.SqliteVacuum, chrome.Data![FindingDataKeys.Kind]);
        Assert.Equal(2, chrome.Data[FindingDataKeys.Paths].Split('|').Length);
        Assert.Equal((60 * Mb).ToString(), chrome.Data[FindingDataKeys.Bytes]);
    }

    [Fact]
    public async Task SmallGain_IsNotWorthAsking()
    {
        // Ради пары мегабайт просить человека закрыть браузер — плохой обмен.
        var findings = await Scan([Database("Google Chrome", @"C:\U\Chrome\Default\History", 1 * Mb)]);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoDatabases_NoFindings()
    {
        Assert.Empty(await Scan([]));
    }

    [Fact]
    public async Task Explain_PromisesDataStaysIntact()
    {
        // Человек боится потерять историю и пароли — текст обязан это снимать.
        var findings = await Scan([Database("Google Chrome", @"C:\U\Chrome\Default\History", 40 * Mb)]);

        var explain = Assert.Single(findings).Explain;
        Assert.Contains("не пропадут", explain);
        Assert.Contains("закрытом браузере", explain);
    }

    [Fact]
    public void CatalogHasNoDuplicateProcessNames()
    {
        // Одно имя процесса у двух браузеров означало бы, что закрытие одного «разрешает» трогать базы другого.
        var processes = BrowserDatabaseCatalog.Browsers.SelectMany(b => b.Processes).ToList();

        Assert.Equal(processes.Count, processes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static BloatedDatabase Database(string browser, string path, long reclaimable) => new()
    {
        Path = path,
        Browser = browser,
        SizeBytes = reclaimable * 3,
        ReclaimableBytes = reclaimable,
    };

    private static async Task<IReadOnlyList<Finding>> Scan(IReadOnlyList<BloatedDatabase> databases)
    {
        var scanner = new BrowserDatabaseScanner(new FakeProbe(databases));
        return (await scanner.ScanAsync()).Findings;
    }

    private sealed class FakeProbe(IReadOnlyList<BloatedDatabase> databases) : IBrowserDatabaseProbe
    {
        public Task<IReadOnlyList<BloatedDatabase>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(databases);
    }
}
