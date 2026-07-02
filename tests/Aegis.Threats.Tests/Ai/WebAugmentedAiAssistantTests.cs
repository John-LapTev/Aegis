using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Threats.Ai;
using Xunit;

namespace Aegis.Threats.Tests.Ai;

public class WebAugmentedAiAssistantTests
{
    [Fact]
    public async Task AskAsync_WithQuery_InjectsSearchResultsIntoPrompt()
    {
        var inner = new CapturingAssistant();
        var search = new StubSearch([new WebSearchResult { Title = "NVIDIA 610.62", Url = "https://nvidia.com", Snippet = "latest" }]);
        var web = new WebAugmentedAiAssistant(inner, search);

        await web.AskAsync("Что это за драйвер?", "nvidia driver");

        Assert.Contains("Что это за драйвер?", inner.LastPrompt);   // исходный промпт сохранён
        Assert.Contains("NVIDIA 610.62", inner.LastPrompt);        // результат подмешан
        Assert.Contains("https://nvidia.com", inner.LastPrompt);   // со ссылкой
    }

    [Fact]
    public async Task AskAsync_NoQuery_PassesPromptUnchanged()
    {
        var inner = new CapturingAssistant();
        var web = new WebAugmentedAiAssistant(inner, new StubSearch([new WebSearchResult { Title = "x", Url = "y" }]));

        await web.AskAsync("обычный вопрос", webQuery: null);

        Assert.Equal("обычный вопрос", inner.LastPrompt);          // без запроса — поиск не подмешиваем
    }

    [Fact]
    public async Task AskAsync_EmptySearch_PassesPromptUnchanged()
    {
        var inner = new CapturingAssistant();
        var web = new WebAugmentedAiAssistant(inner, new StubSearch([]));

        await web.AskAsync("вопрос", "запрос без результатов");

        Assert.Equal("вопрос", inner.LastPrompt);                  // поиск пуст — отвечаем по знаниям
    }

    private sealed class CapturingAssistant : IAiAssistant
    {
        public string LastPrompt { get; private set; } = string.Empty;
        public string Name => "Stub";
        public bool IsConfigured => true;
        public Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(AiResult.Ok("ответ"));
        }
    }

    private sealed class StubSearch(IReadOnlyList<WebSearchResult> results) : IWebSearch
    {
        public Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default) =>
            Task.FromResult(results);
    }
}
