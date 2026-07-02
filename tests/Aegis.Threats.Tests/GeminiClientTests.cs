using System.Net;
using Aegis.Threats.Ai;
using Xunit;

namespace Aegis.Threats.Tests;

public sealed class GeminiClientTests
{
    [Fact]
    public void ExtractText_ValidResponse_ReturnsText()
    {
        const string json = """
        {
          "candidates": [
            { "content": { "parts": [ { "text": "Это системный процесс Windows, безопасен." } ] } }
          ]
        }
        """;

        Assert.Equal("Это системный процесс Windows, безопасен.", GeminiClient.ExtractText(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("not json")]
    public void ExtractText_BadResponse_ReturnsNull(string json) =>
        Assert.Null(GeminiClient.ExtractText(json));

    [Fact]
    public async Task AskAsync_QuotaExceeded_ReturnsLimitReached()
    {
        var client = new GeminiClient(
            new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, "{}")),
            "test-key");

        var result = await client.AskAsync("test");

        Assert.False(result.Success);
        Assert.True(result.LimitReached);
    }

    [Fact]
    public void ParseRetryAfter_WithRetryInfo_ReturnsFormatted()
    {
        const string json = """
        { "error": { "code": 429, "details": [
            { "@type": "type.googleapis.com/google.rpc.RetryInfo", "retryDelay": "42s" }
        ]}}
        """;

        Assert.Equal("42 сек", GeminiClient.ParseRetryAfter(json));
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":429}}")]
    [InlineData("{}")]
    public void ParseRetryAfter_NoRetryInfo_ReturnsNull(string json) =>
        Assert.Null(GeminiClient.ParseRetryAfter(json));

    [Fact]
    public async Task AskAsync_Success_ReturnsText()
    {
        const string json = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Ответ\"}]}}]}";
        var client = new GeminiClient(new HttpClient(new StubHandler(HttpStatusCode.OK, json)), "test-key");

        var result = await client.AskAsync("test");

        Assert.True(result.Success);
        Assert.Equal("Ответ", result.Text);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
