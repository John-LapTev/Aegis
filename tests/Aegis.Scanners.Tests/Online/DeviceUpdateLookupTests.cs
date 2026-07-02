using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Online;
using Xunit;

namespace Aegis.Scanners.Tests.Online;

public class DeviceUpdateLookupTests
{
    [Fact]
    public async Task LookupAsync_PrefersTrustedDomain_AndExtractsVersion()
    {
        var search = new StubSearch([
            new WebSearchResult { Title = "Driver Pack 1.0", Url = "https://random-drivers.example/x" },
            new WebSearchResult { Title = "Realtek Audio Driver 6.0.9659.1", Url = "https://www.realtek.com/downloads" },
        ]);
        var lookup = new DeviceUpdateLookup(search);

        var result = await lookup.LookupAsync("Realtek High Definition Audio");

        Assert.Equal("https://www.realtek.com/downloads", result.DownloadUrl); // официальный домен предпочтён
        Assert.Equal("6.0.9659.1", result.LatestVersion);
        Assert.True(result.Found);
    }

    [Fact]
    public async Task LookupAsync_NoTrusted_NoLinkButKeepsVersion()
    {
        // Недоверенный домен → ссылку НЕ отдаём (не шлём на сомнительный сайт-сборник), но версию из выдачи
        // показать можно — это информация, а не кнопка перехода (правка аудита 2026-07-02).
        var search = new StubSearch([new WebSearchResult { Title = "Some Tool 2.1", Url = "https://example.com/a" }]);

        var result = await new DeviceUpdateLookup(search).LookupAsync("Some Device");

        Assert.Null(result.DownloadUrl);
        Assert.Equal("2.1", result.LatestVersion);
    }

    [Fact]
    public async Task LookupAsync_EmptyResults_ReturnsEmpty()
    {
        var result = await new DeviceUpdateLookup(new StubSearch([])).LookupAsync("Anything");
        Assert.False(result.Found);
    }

    private sealed class StubSearch(IReadOnlyList<WebSearchResult> results) : IWebSearch
    {
        public Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default) =>
            Task.FromResult(results);
    }
}
