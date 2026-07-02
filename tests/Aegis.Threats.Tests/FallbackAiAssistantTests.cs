using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Threats.Ai;
using Xunit;

namespace Aegis.Threats.Tests;

public sealed class FallbackAiAssistantTests
{
    [Fact]
    public async Task AskAsync_FirstHasLimit_FallsToNext()
    {
        var chain = new FallbackAiAssistant(
        [
            new Stub("Gemini", AiResult.Limit("лимит", null)),
            new Stub("Groq", AiResult.Ok("ответ")),
        ]);

        var result = await chain.AskAsync("вопрос");

        Assert.True(result.Success);
        Assert.Equal("ответ", result.Text);
        Assert.Equal("Groq", result.Provider);
    }

    [Fact]
    public async Task AskAsync_FirstWorks_UsesFirst()
    {
        var chain = new FallbackAiAssistant([new Stub("Gemini", AiResult.Ok("a")), new Stub("Groq", AiResult.Ok("b"))]);

        var result = await chain.AskAsync("вопрос");

        Assert.Equal("Gemini", result.Provider);
        Assert.Equal("a", result.Text);
    }

    [Fact]
    public async Task AskAsync_AllLimited_ReportsLimitReached()
    {
        var chain = new FallbackAiAssistant(
            [new Stub("Gemini", AiResult.Limit("л", null)), new Stub("Groq", AiResult.Limit("л", null))]);

        var result = await chain.AskAsync("вопрос");

        Assert.False(result.Success);
        Assert.True(result.LimitReached);
    }

    [Fact]
    public void IsConfigured_OnlyCountsConfiguredProviders()
    {
        var chain = new FallbackAiAssistant([new Stub("X", AiResult.Ok("a"), configured: false)]);
        Assert.False(chain.IsConfigured);
    }

    [Fact]
    public void OpenAiCompatible_ExtractText_ParsesChoice() =>
        Assert.Equal("привет", OpenAiCompatibleClient.ExtractText(
            "{\"choices\":[{\"message\":{\"content\":\"привет\"}}]}"));

    private sealed class Stub(string name, AiResult result, bool configured = true) : IAiAssistant
    {
        public string Name => name;
        public bool IsConfigured => configured;
        public Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
