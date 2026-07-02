using System.Text;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Ai;

/// <summary>
/// Оборачивает ЛЮБОЙ ИИ-помощник (цепочку Gemini→ChatGPT→Claude) реальным веб-поиском: перед ответом ищем в
/// интернете по теме вопроса и отдаём модели свежие результаты со ссылками как контекст. Так «в сеть лезут»
/// все модели одинаково (сами они в интернет не ходят). Поиск не вышел/нет сети — модель отвечает по знаниям.
/// </summary>
public sealed class WebAugmentedAiAssistant : IAiAssistant
{
    private readonly IAiAssistant _inner;
    private readonly IWebSearch _search;

    public WebAugmentedAiAssistant(IAiAssistant inner, IWebSearch search)
    {
        _inner = inner;
        _search = search;
    }

    public string Name => _inner.Name;

    public bool IsConfigured => _inner.IsConfigured;

    /// <summary>Модели обёрнутой цепочки — чтобы раздел «Нейросети» показал статус каждой (сквозь обёртку поиска).</summary>
    public IReadOnlyList<IAiAssistant> Providers => (_inner as FallbackAiAssistant)?.Providers ?? [];

    public async Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default)
    {
        // Без явного запроса для поиска — отвечаем как обычно (например, проверка статуса модели).
        if (string.IsNullOrWhiteSpace(webQuery))
        {
            return await _inner.AskAsync(prompt, null, cancellationToken).ConfigureAwait(false);
        }

        var results = await _search.SearchAsync(webQuery, 5, cancellationToken).ConfigureAwait(false);
        if (results.Count == 0)
        {
            // Поиск не дал результатов — пусть модель ответит по своим знаниям.
            return await _inner.AskAsync(prompt, null, cancellationToken).ConfigureAwait(false);
        }

        return await _inner.AskAsync(Augment(prompt, webQuery, results), null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Дописывает к промпту свежие результаты веб-поиска и просьбу опираться на них со ссылкой.</summary>
    private static string Augment(string prompt, string query, IReadOnlyList<WebSearchResult> results)
    {
        var builder = new StringBuilder(prompt);
        builder.Append("\n\nСвежие результаты веб-поиска по запросу «").Append(query).Append("»:\n");
        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            builder.Append(i + 1).Append(". ").Append(item.Title);
            if (item.Snippet.Length > 0)
            {
                builder.Append(" — ").Append(item.Snippet);
            }

            builder.Append('\n').Append("   ").Append(item.Url).Append('\n');
        }

        builder.Append(
            "\nЕсли в результатах есть актуальная информация (последняя версия, где скачать, что это за процесс) — " +
            "приведи её и ОБЯЗАТЕЛЬНО дай ссылку-источник из списка. Если результаты не относятся к вопросу — " +
            "отвечай по своим знаниям, не выдумывая ссылок.");
        return builder.ToString();
    }
}
