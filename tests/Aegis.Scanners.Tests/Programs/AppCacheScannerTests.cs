using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Programs;
using Xunit;

namespace Aegis.Scanners.Tests.Programs;

public sealed class AppCacheScannerTests
{
    [Fact]
    public async Task ScanAsync_Cache_Info_BatchSelectable()
    {
        var finding = Assert.Single((await Make(new AppCacheItem
        {
            Name = "Google Chrome", Category = AppCacheCategory.Cache,
            Targets = [@"C:\Users\u\AppData\Local\Google\Chrome\User Data\Default\Cache"], Bytes = 524_288_000, FileCount = 1234,
        }).ScanAsync()).Findings);

        Assert.Equal(ScanGroup.Junk, finding.Group);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.StartsWith("appcache-", finding.Id, StringComparison.Ordinal);
        Assert.EndsWith("-cache", finding.Id, StringComparison.Ordinal);
        Assert.False(finding.Data!.ContainsKey("kind"));
        Assert.False(finding.Data!.ContainsKey("noBatch")); // кэш можно массово
    }

    [Fact]
    public async Task ScanAsync_Cookies_Warning_NotBatchSelectable_WarnsAboutLogout()
    {
        var finding = Assert.Single((await Make(new AppCacheItem
        {
            Name = "Google Chrome", Category = AppCacheCategory.Cookies,
            Targets = [@"C:\Users\u\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies"], Bytes = 65_536, FileCount = 1,
        }).ScanAsync()).Findings);

        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.EndsWith("-cookies", finding.Id, StringComparison.Ordinal);
        Assert.Equal("1", finding.Data!["noBatch"]); // cookie — только осознанно
        Assert.Contains("аккаунт", finding.Explain);
    }

    [Fact]
    public async Task ScanAsync_History_Info_NotBatchSelectable()
    {
        var finding = Assert.Single((await Make(new AppCacheItem
        {
            Name = "Google Chrome", Category = AppCacheCategory.History,
            Targets = [@"C:\Users\u\AppData\Local\Google\Chrome\User Data\Default\History"], Bytes = 131_072, FileCount = 1,
        }).ScanAsync()).Findings);

        Assert.Equal(Severity.Info, finding.Severity);
        Assert.EndsWith("-history", finding.Id, StringComparison.Ordinal);
        Assert.Equal("1", finding.Data!["noBatch"]);
    }

    [Fact]
    public async Task ScanAsync_NoApps_ReturnsEmpty() =>
        Assert.Empty((await Make().ScanAsync()).Findings);

    private static AppCacheScanner Make(params AppCacheItem[] apps) => new(new FakeAppCacheProbe(apps));

    private sealed class FakeAppCacheProbe(params AppCacheItem[] apps) : IAppCacheProbe
    {
        public Task<AppCacheSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppCacheSnapshot { Apps = apps });
    }
}
