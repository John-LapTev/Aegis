using System.Net;
using Aegis.Core.Models;
using Aegis.Threats.VirusTotal;
using Xunit;

namespace Aegis.Threats.Tests.VirusTotal;

public sealed class VirusTotalClientTests
{
    private const string Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static string StatsJson(int malicious, int suspicious, int harmless, int undetected) =>
        "{\"data\":{\"attributes\":{\"last_analysis_stats\":{"
        + "\"malicious\":" + malicious + ",\"suspicious\":" + suspicious
        + ",\"harmless\":" + harmless + ",\"undetected\":" + undetected
        + ",\"timeout\":0}}}}";

    [Fact]
    public async Task CheckHashAsync_ManyMaliciousEngines_IsMalicious()
    {
        var client = ClientWith(HttpStatusCode.OK, StatsJson(malicious: 58, suspicious: 1, harmless: 0, undetected: 12), out _);

        var reputation = await client.CheckHashAsync(Hash);

        Assert.Equal(ReputationVerdict.Malicious, reputation.Verdict);
        Assert.Equal(58, reputation.MaliciousCount);
        Assert.Equal(71, reputation.TotalEngines);
    }

    [Fact]
    public async Task CheckHashAsync_NoDetections_IsClean()
    {
        var client = ClientWith(HttpStatusCode.OK, StatsJson(malicious: 0, suspicious: 0, harmless: 50, undetected: 20), out _);

        var reputation = await client.CheckHashAsync(Hash);

        Assert.Equal(ReputationVerdict.Clean, reputation.Verdict);
    }

    [Fact]
    public async Task CheckHashAsync_OneMaliciousEngine_IsSuspicious()
    {
        var client = ClientWith(HttpStatusCode.OK, StatsJson(malicious: 1, suspicious: 0, harmless: 60, undetected: 10), out _);

        var reputation = await client.CheckHashAsync(Hash);

        Assert.Equal(ReputationVerdict.Suspicious, reputation.Verdict);
    }

    [Fact]
    public async Task CheckHashAsync_NotInDatabase_IsUnknown()
    {
        var client = ClientWith(HttpStatusCode.NotFound, "{}", out _);

        var reputation = await client.CheckHashAsync(Hash);

        Assert.Equal(ReputationVerdict.Unknown, reputation.Verdict);
    }

    [Fact]
    public async Task CheckHashAsync_RateLimited_ReturnsRateLimitedVerdict()
    {
        var client = ClientWith(HttpStatusCode.TooManyRequests, "{}", out _);

        var reputation = await client.CheckHashAsync(Hash);

        Assert.Equal(ReputationVerdict.RateLimited, reputation.Verdict);
    }

    [Fact]
    public async Task CheckHashAsync_SendsApiKeyHeader()
    {
        var client = ClientWith(HttpStatusCode.NotFound, "{}", out var handler);

        await client.CheckHashAsync(Hash);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("my-secret-key", handler.LastRequest!.Headers.GetValues("x-apikey"));
        Assert.Contains(Hash, handler.LastRequest.RequestUri!.ToString());
    }

    private static VirusTotalClient ClientWith(HttpStatusCode status, string body, out StubHandler handler)
    {
        handler = new StubHandler(status, body);
        return new VirusTotalClient(new HttpClient(handler), "my-secret-key");
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
